using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class DownloadCacheTests
    {
        [TestMethod]
        public async Task DownloadsValidatesModelPackReportsProgressAndReusesOfflineCache()
        {
            var fixture = new CatalogFixture();
            using var directory = new TestDirectory();
            var handler = new ScriptedHttpHandler(fixture.Responses());
            var updates = new List<ModelDownloadProgress>();
            var updatesGate = new object();
            using (var factory = CreateFactory(fixture, directory.Path, handler))
            {
                MaterializedModel model = await factory.GetModelAsync(fixture.Selection, new InlineProgress<ModelDownloadProgress>(value => { lock (updatesGate) updates.Add(value); }));
                Assert.AreEqual(CatalogFixture.ModelId, model.Package.Manifest.ModelId.Value);
                Assert.AreEqual("gguf", model.Package.ToCoreArtifacts()[0].Format);
                Assert.IsTrue(await factory.VerifyModelCacheAsync(fixture.Selection));
                MaterializedAsset testInput = await factory.GetTestInputAsync(fixture.Selection.Entry, "prompt");
                CollectionAssert.AreEqual(fixture.TestInputBytes, File.ReadAllBytes(testInput.FullPath));
            }

            Assert.AreEqual(3, handler.RequestCount);
            Assert.IsTrue(updates.Any(update => update.Stage == ModelDownloadStage.Downloading));
            Assert.IsTrue(updates.Any(update => update.Stage == ModelDownloadStage.Verifying));
            Assert.IsTrue(updates.Any(update => update.Stage == ModelDownloadStage.Completed));

            var offlineHandler = new ScriptedHttpHandler(new Dictionary<string, byte[]>());
            using var offline = new ModelFactoryClient(fixture.Catalog, Options(directory.Path, offline: true), new HttpClient(offlineHandler));
            MaterializedModel cached = await offline.GetModelAsync(fixture.Selection);
            MaterializedAsset cachedInput = await offline.GetTestInputAsync(fixture.Selection.Entry, "prompt");
            Assert.IsTrue(File.Exists(cached.Package.ManifestPath));
            Assert.IsTrue(File.Exists(cachedInput.FullPath));
            Assert.AreEqual(0, offlineHandler.RequestCount);
        }

        [TestMethod]
        public async Task CorruptCacheIsRedownloadedOnlineAndRejectedOffline()
        {
            var fixture = new CatalogFixture();
            using var directory = new TestDirectory();
            var handler = new ScriptedHttpHandler(fixture.Responses());
            string modelPath;
            using (var first = CreateFactory(fixture, directory.Path, handler))
            {
                MaterializedModel model = await first.GetModelAsync(fixture.Selection);
                modelPath = Path.Combine(model.PackageRoot, "models", "model.gguf");
            }

            File.WriteAllBytes(modelPath, Enumerable.Repeat((byte)0x44, fixture.ModelBytes.Length).ToArray());
            using (var online = CreateFactory(fixture, directory.Path, handler))
            {
                MaterializedModel repaired = await online.GetModelAsync(fixture.Selection);
                CollectionAssert.AreEqual(fixture.ModelBytes, File.ReadAllBytes(Path.Combine(repaired.PackageRoot, "models", "model.gguf")));
            }
            Assert.AreEqual(3, handler.RequestCount);

            File.WriteAllBytes(modelPath, new byte[] { 1 });
            using var offline = new ModelFactoryClient(fixture.Catalog, Options(directory.Path, offline: true), new HttpClient(new ScriptedHttpHandler(fixture.Responses())));
            ModelFactoryException exception = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => offline.GetModelAsync(fixture.Selection));
            Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.OfflineCacheMiss));
        }

        [TestMethod]
        public async Task SameSelectionConcurrentCallsShareOneDownloadAndRespectConcurrencyLimit()
        {
            var fixture = new CatalogFixture();
            using var directory = new TestDirectory();
            var handler = new ScriptedHttpHandler(fixture.Responses(), TimeSpan.FromMilliseconds(30));
            using var factory = new ModelFactoryClient(fixture.Catalog, Options(directory.Path, maximumConcurrentDownloads: 1), new HttpClient(handler));
            Task<MaterializedModel> first = factory.GetModelAsync(fixture.Selection);
            Task<MaterializedModel> second = factory.GetModelAsync(fixture.Selection);
            MaterializedModel[] results = await Task.WhenAll(first, second);
            Assert.AreEqual(results[0].CacheKey, results[1].CacheKey);
            Assert.AreEqual(2, handler.RequestCount);
            Assert.AreEqual(1, handler.MaximumActive);
        }

        [TestMethod]
        public async Task CallerCancellationDoesNotBreakAnotherSharedCaller()
        {
            var fixture = new CatalogFixture();
            using var directory = new TestDirectory();
            var handler = new ScriptedHttpHandler(fixture.Responses(), TimeSpan.FromMilliseconds(80));
            using var factory = CreateFactory(fixture, directory.Path, handler);
            using var source = new CancellationTokenSource();
            Task<MaterializedModel> cancelled = factory.GetModelAsync(fixture.Selection, cancellationToken: source.Token);
            Task<MaterializedModel> survivor = factory.GetModelAsync(fixture.Selection);
            source.CancelAfter(10);
            ModelFactoryException exception = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => cancelled);
            Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.Cancelled));
            Assert.IsNotNull(await survivor);
            Assert.AreEqual(2, handler.RequestCount);
        }

        [TestMethod]
        public async Task IntegrityMismatchDeletesTemporaryFilesAndDoesNotRetry()
        {
            var fixture = new CatalogFixture();
            using var directory = new TestDirectory();
            Dictionary<string, byte[]> responses = fixture.Responses();
            responses[fixture.Selection.Artifact.Assets[1].DownloadUri!.AbsoluteUri] = Enumerable.Repeat((byte)0x55, fixture.ModelBytes.Length).ToArray();
            var handler = new ScriptedHttpHandler(responses);
            using var factory = CreateFactory(fixture, directory.Path, handler);
            ModelFactoryException exception = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => factory.GetModelAsync(fixture.Selection));
            Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.IntegrityMismatch));
            Assert.AreEqual(2, handler.RequestCount);
            Assert.AreEqual(0, Directory.EnumerateFiles(directory.Path, "*.tmp", SearchOption.AllDirectories).Count());
        }

        [TestMethod]
        public async Task RetriesRateLimitAndTimeoutButNotNotFound()
        {
            var fixture = new CatalogFixture();
            using var rateDirectory = new TestDirectory();
            var rateHandler = new ScriptedHttpHandler(fixture.Responses());
            rateHandler.EnqueueStatus((HttpStatusCode)429);
            using (var rateFactory = new ModelFactoryClient(fixture.Catalog, Options(rateDirectory.Path, maximumConcurrentDownloads: 1, maximumRetries: 1), new HttpClient(rateHandler)))
            {
                Assert.IsNotNull(await rateFactory.GetModelAsync(fixture.Selection));
            }
            Assert.AreEqual(3, rateHandler.RequestCount);
            Assert.IsTrue(rateHandler.LastUserAgent!.Contains("DeploySharp-ModelFactory"));

            using var missingDirectory = new TestDirectory();
            var missingHandler = new ScriptedHttpHandler(fixture.Responses());
            missingHandler.EnqueueStatus(HttpStatusCode.NotFound);
            using (var missingFactory = new ModelFactoryClient(fixture.Catalog, Options(missingDirectory.Path, maximumConcurrentDownloads: 1, maximumRetries: 3), new HttpClient(missingHandler)))
            {
                ModelFactoryException missing = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => missingFactory.GetModelAsync(fixture.Selection));
                Assert.AreEqual(HttpStatusCode.NotFound, missing.Diagnostics[0].StatusCode);
            }
            Assert.AreEqual(1, missingHandler.RequestCount);

            using var timeoutDirectory = new TestDirectory();
            var timeoutHandler = new ScriptedHttpHandler(fixture.Responses(), TimeSpan.FromMilliseconds(100));
            using var timeoutFactory = new ModelFactoryClient(fixture.Catalog, Options(timeoutDirectory.Path, maximumConcurrentDownloads: 1, maximumRetries: 1, timeout: TimeSpan.FromMilliseconds(10)), new HttpClient(timeoutHandler));
            ModelFactoryException timeout = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => timeoutFactory.GetModelAsync(fixture.Selection));
            Assert.IsTrue(timeout.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.Timeout));
            Assert.AreEqual(2, timeoutHandler.RequestCount);
        }

        [TestMethod]
        public async Task RetriesServerAndNetworkFailuresButRejectsForbiddenAndShortResponses()
        {
            var fixture = new CatalogFixture();
            using var serverDirectory = new TestDirectory();
            var serverHandler = new ScriptedHttpHandler(fixture.Responses());
            serverHandler.EnqueueStatus(HttpStatusCode.InternalServerError);
            using (var serverFactory = new ModelFactoryClient(fixture.Catalog, Options(serverDirectory.Path, maximumConcurrentDownloads: 1, maximumRetries: 1), new HttpClient(serverHandler)))
            {
                Assert.IsNotNull(await serverFactory.GetModelAsync(fixture.Selection));
            }
            Assert.AreEqual(3, serverHandler.RequestCount);

            using var networkDirectory = new TestDirectory();
            var networkHandler = new ScriptedHttpHandler(fixture.Responses()) { ThrowNetworkError = true };
            using (var networkFactory = new ModelFactoryClient(fixture.Catalog, Options(networkDirectory.Path, maximumConcurrentDownloads: 1, maximumRetries: 1), new HttpClient(networkHandler)))
            {
                ModelFactoryException network = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => networkFactory.GetModelAsync(fixture.Selection));
                Assert.AreEqual(ModelFactoryDiagnosticCodes.HttpFailure, network.Diagnostics[0].Code);
                Assert.IsNotNull(network.InnerException);
            }
            Assert.AreEqual(2, networkHandler.RequestCount);

            using var forbiddenDirectory = new TestDirectory();
            var forbiddenHandler = new ScriptedHttpHandler(fixture.Responses());
            forbiddenHandler.EnqueueStatus(HttpStatusCode.Forbidden);
            using (var forbiddenFactory = new ModelFactoryClient(fixture.Catalog, Options(forbiddenDirectory.Path, maximumConcurrentDownloads: 1, maximumRetries: 3), new HttpClient(forbiddenHandler)))
            {
                ModelFactoryException forbidden = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => forbiddenFactory.GetModelAsync(fixture.Selection));
                Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.Diagnostics[0].StatusCode);
            }
            Assert.AreEqual(1, forbiddenHandler.RequestCount);

            using var shortDirectory = new TestDirectory();
            Dictionary<string, byte[]> shortResponses = fixture.Responses();
            shortResponses[fixture.Selection.Artifact.Assets[0].DownloadUri!.AbsoluteUri] = Array.Empty<byte>();
            var shortHandler = new ScriptedHttpHandler(shortResponses);
            using var shortFactory = new ModelFactoryClient(fixture.Catalog, Options(shortDirectory.Path), new HttpClient(shortHandler));
            ModelFactoryException shortResponse = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => shortFactory.GetModelAsync(fixture.Selection));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.IntegrityMismatch, shortResponse.Diagnostics[0].Code);

            using var lengthDirectory = new TestDirectory();
            var lengthHandler = new ScriptedHttpHandler(fixture.Responses()) { WrongContentLength = true };
            using var lengthFactory = new ModelFactoryClient(fixture.Catalog, Options(lengthDirectory.Path), new HttpClient(lengthHandler));
            ModelFactoryException wrongLength = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => lengthFactory.GetModelAsync(fixture.Selection));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.IntegrityMismatch, wrongLength.Diagnostics[0].Code);
        }

        [TestMethod]
        public async Task EnforcesSizeLimitsAndTestInputPolicy()
        {
            var fixture = new CatalogFixture();
            using var directory = new TestDirectory();
            var limited = new ModelFactoryOptions(directory.Path, maximumAssetBytes: 1, maximumOperationBytes: 2, maximumRetries: 0);
            using (var factory = new ModelFactoryClient(fixture.Catalog, limited, new HttpClient(new ScriptedHttpHandler(fixture.Responses()))))
            {
                ModelFactoryException exception = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => factory.GetModelAsync(fixture.Selection));
                Assert.AreEqual(ModelFactoryDiagnosticCodes.LimitExceeded, exception.Diagnostics[0].Code);
            }

            using var disabled = new ModelFactoryClient(fixture.Catalog, new ModelFactoryOptions(directory.Path, allowTestInputs: false, offline: true), new HttpClient(new ScriptedHttpHandler(fixture.Responses())));
            ModelFactoryException disabledError = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => disabled.GetTestInputAsync(fixture.Selection.Entry, "prompt"));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.AdmissionRejected, disabledError.Diagnostics[0].Code);

            var proxy = new WebProxy("http://127.0.0.1:8888");
            var proxyOptions = new ModelFactoryOptions(directory.Path, proxy: proxy, userAgent: "DeploySharp-Tests/2.0");
            Assert.AreSame(proxy, proxyOptions.Proxy);
            Assert.AreEqual("DeploySharp-Tests/2.0", proxyOptions.UserAgent);
        }

        [TestMethod]
        public async Task CleanupStaysInsideManagedNamespaceAndSupportsDryRun()
        {
            var fixture = new CatalogFixture();
            using var directory = new TestDirectory();
            string userFile = Path.Combine(directory.Path, "user-owned.txt");
            File.WriteAllText(userFile, "keep");
            using var factory = CreateFactory(fixture, directory.Path, new ScriptedHttpHandler(fixture.Responses()));
            await factory.GetModelAsync(fixture.Selection);
            ModelCacheCleanupResult dryRun = await factory.CleanCacheAsync(new ModelCacheCleanupOptions(olderThan: TimeSpan.Zero, dryRun: true));
            Assert.AreEqual(1, dryRun.EntriesRemoved);
            Assert.IsTrue(await factory.VerifyModelCacheAsync(fixture.Selection));
            ModelCacheCleanupResult removed = await factory.CleanCacheAsync(new ModelCacheCleanupOptions(olderThan: TimeSpan.Zero));
            Assert.AreEqual(1, removed.EntriesRemoved);
            Assert.IsFalse(await factory.VerifyModelCacheAsync(fixture.Selection));
            Assert.IsTrue(File.Exists(userFile));
        }

        [TestMethod]
        public void DisposedFactoryReportsStableDiagnostic()
        {
            var fixture = new CatalogFixture();
            using var directory = new TestDirectory();
            var factory = CreateFactory(fixture, directory.Path, new ScriptedHttpHandler(fixture.Responses()));
            factory.Dispose();
            ModelFactoryException exception = Assert.ThrowsExactly<ModelFactoryException>(() => factory.Select(new ModelQuery(modelId: CatalogFixture.ModelId)));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.ObjectDisposed, exception.Diagnostics[0].Code);
        }

        [TestMethod]
        public async Task RemoteCatalogClientRejectsRedirectAndAcceptsStrictCatalog()
        {
            var fixture = new CatalogFixture();
            var uri = new Uri("https://github.com/catalog.json");
            var responses = new Dictionary<string, byte[]> { [uri.AbsoluteUri] = System.Text.Encoding.UTF8.GetBytes(ModelCatalogJsonSerializer.Serialize(fixture.Catalog)) };
            using (var successClient = new HttpClient(new ScriptedHttpHandler(responses)))
            {
                ValidatedModelCatalog loaded = await ModelCatalogClient.LoadAsync(uri, successClient);
                Assert.AreEqual("tests.1", loaded.CatalogRevision);
            }

            var redirectHandler = new ScriptedHttpHandler(responses);
            redirectHandler.EnqueueStatus(HttpStatusCode.Redirect);
            using var redirectClient = new HttpClient(redirectHandler);
            ModelFactoryException redirect = await Assert.ThrowsExactlyAsync<ModelFactoryException>(() => ModelCatalogClient.LoadAsync(uri, redirectClient));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.HttpFailure, redirect.Diagnostics[0].Code);
        }

        private static ModelFactoryClient CreateFactory(CatalogFixture fixture, string root, ScriptedHttpHandler handler)
        {
            return new ModelFactoryClient(fixture.Catalog, Options(root), new HttpClient(handler));
        }

        private static ModelFactoryOptions Options(string root, bool offline = false, int maximumConcurrentDownloads = 2, int maximumRetries = 0, TimeSpan? timeout = null)
        {
            return new ModelFactoryOptions(root, offline: offline, maximumConcurrentDownloads: maximumConcurrentDownloads, maximumRetries: maximumRetries, requestTimeout: timeout ?? TimeSpan.FromSeconds(5), baseRetryDelay: TimeSpan.Zero, maximumRetryDelay: TimeSpan.Zero);
        }

        private sealed class InlineProgress<T> : IProgress<T>
        {
            private readonly Action<T> _callback;
            public InlineProgress(Action<T> callback) => _callback = callback;
            public void Report(T value) => _callback(value);
        }
    }
}
