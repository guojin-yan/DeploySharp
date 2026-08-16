using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.TensorRT.Tests
{
    [TestClass]
    public sealed class TensorRtExternalCacheStoreTests
    {
        [TestMethod]
        public void CudaWriteHitReadInvalidateUsesVersionedIntegrityManifest()
        {
            string root = NewRoot();
            try
            {
                var store = new TensorRtExternalCacheStore(root);
                TensorRtCudaRtcArtifact artifact = CudaArtifact("ptx-payload\0");
                TensorRtCudaKernelLookupIdentity identity = CudaIdentity();

                Assert.AreEqual(TensorRtExternalCacheStatus.Miss, store.LookupCuda(identity).Status);
                TensorRtCudaCacheResult stored = store.StoreCuda(identity, artifact);
                Assert.AreEqual(TensorRtExternalCacheStatus.Stored, stored.Status);
                Assert.AreEqual(artifact.ArtifactSha256, stored.Metadata!.PayloadSha256);
                Assert.AreEqual(artifact.Length, stored.Metadata.PayloadLength);
                Assert.AreEqual(64, stored.Metadata.ManifestSha256.Length);
                Assert.AreEqual(".ptx", stored.Metadata.ArtifactExtension);

                TensorRtCudaCacheResult hit = store.LookupCuda(identity);
                Assert.AreEqual(TensorRtExternalCacheStatus.Hit, hit.Status);
                CollectionAssert.AreEqual(artifact.ToArray(), hit.Artifact!.ToArray());
                Assert.AreEqual(new TensorRtCudaKernelCacheIdentity(
                    artifact, CompilerIdentity, CudaVersion, CudaIdentityValue, DriverVersion, DriverIdentity, GpuArchitecture, GpuCompatibilityIdentity, BridgeIdentity).CacheKeySha256,
                    hit.Metadata!.CudaCacheKeySha256);

                (string currentPath, string manifestPath, string payloadPath) = Paths(root, "cuda", identity.LookupKeySha256);
                Assert.IsTrue(File.Exists(currentPath));
                Assert.AreEqual(stored.Metadata.ManifestSha256, Sha256(File.ReadAllBytes(manifestPath)));
                Assert.AreEqual(stored.Metadata.PayloadSha256, Sha256(File.ReadAllBytes(payloadPath)));
                using (JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath)))
                {
                    Assert.AreEqual(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
                    Assert.AreEqual(identity.LookupKeySha256, manifest.RootElement.GetProperty("lookupKeySha256").GetString());
                    Assert.AreEqual(artifact.Length, manifest.RootElement.GetProperty("payload").GetProperty("length").GetInt64());
                }

                Assert.AreEqual(TensorRtExternalCacheStatus.Deleted, store.InvalidateCuda(identity).Status);
                Assert.AreEqual(TensorRtExternalCacheStatus.Miss, store.LookupCuda(identity).Status);
                Assert.AreEqual(TensorRtExternalCacheStatus.NotFound, store.InvalidateCuda(identity).Status);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public void EngineWriteOpenInvalidateNormalizesExtensionAndBindsCompleteIdentity()
        {
            string root = NewRoot();
            try
            {
                var store = new TensorRtExternalCacheStore(root);
                TensorRtEngineCacheIdentity identity = EngineIdentity(".PLAN");
                byte[] engine = Bytes("serialized-engine-one");

                using (var input = new MemoryStream(engine, writable: false))
                using (TensorRtEngineCacheResult stored = store.StoreEngine(identity, input))
                {
                    Assert.AreEqual(TensorRtExternalCacheStatus.Stored, stored.Status);
                    Assert.AreEqual(".plan", stored.Metadata!.ArtifactExtension);
                    Assert.AreEqual(Sha256(engine), stored.Metadata.PayloadSha256);
                }

                using (TensorRtEngineCacheResult hit = store.OpenEngine(identity))
                {
                    Assert.AreEqual(TensorRtExternalCacheStatus.Hit, hit.Status);
                    CollectionAssert.AreEqual(engine, ReadAll(hit.Stream!));
                    Assert.AreEqual(engine.Length, hit.Metadata!.PayloadLength);
                }

                (_, string manifestPath, _) = Paths(root, "engine", identity.LookupKeySha256);
                using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
                JsonElement details = manifest.RootElement.GetProperty("engine");
                Assert.AreEqual(identity.OnnxSha256, details.GetProperty("onnxSha256").GetString());
                Assert.AreEqual(identity.ManagedBuildInputsSha256, details.GetProperty("managedBuildInputsSha256").GetString());
                Assert.AreEqual(identity.ManagedApiContractSha256, details.GetProperty("managedApiContractSha256").GetString());
                Assert.AreEqual(identity.TensorRtIdentity, details.GetProperty("tensorRtIdentity").GetString());
                Assert.AreEqual(identity.CudnnIdentity, details.GetProperty("cudnnIdentity").GetString());
                Assert.AreEqual(identity.GpuCompatibilityIdentity, details.GetProperty("gpuCompatibilityIdentity").GetString());
                Assert.AreEqual(identity.BuildOptions.WorkspaceBytes, details.GetProperty("workspaceBytes").GetUInt64());
                Assert.AreEqual(1, details.GetProperty("profiles").GetArrayLength());
                Assert.AreEqual(2, details.GetProperty("builderFlags").GetArrayLength());

                Assert.AreEqual(TensorRtExternalCacheStatus.Deleted, store.InvalidateEngine(identity).Status);
                using TensorRtEngineCacheResult miss = store.OpenEngine(identity);
                Assert.AreEqual(TensorRtExternalCacheStatus.Miss, miss.Status);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public void CubinWriteHitReadDeleteUsesCubinLayoutAndKind()
        {
            string root = NewRoot();
            try
            {
                TensorRtCudaKernelLookupIdentity identity = CudaIdentity(kind: TensorRtCudaRtcArtifactKind.Cubin);
                TensorRtCudaRtcArtifact artifact = CudaArtifact("cubin-payload", TensorRtCudaRtcArtifactKind.Cubin);
                var store = new TensorRtExternalCacheStore(root);
                TensorRtCudaCacheResult stored = store.StoreCuda(identity, artifact);
                Assert.AreEqual(TensorRtExternalCacheEntryKind.CudaCubin, stored.Metadata!.Kind);
                Assert.AreEqual(".cubin", stored.Metadata.ArtifactExtension);
                TensorRtCudaCacheResult hit = store.LookupCuda(identity);
                Assert.AreEqual(TensorRtCudaRtcArtifactKind.Cubin, hit.Artifact!.Kind);
                CollectionAssert.AreEqual(artifact.ToArray(), hit.Artifact.ToArray());
                Assert.AreEqual(TensorRtExternalCacheStatus.Deleted, store.DeleteCuda(identity).Status);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public void LookupIdentitiesAreStableAndBindEveryCompatibilityDimension()
        {
            TensorRtCudaKernelLookupIdentity cuda1 = CudaIdentity();
            TensorRtCudaKernelLookupIdentity cuda2 = CudaIdentity();
            TensorRtCudaKernelLookupIdentity changedCuda = CudaIdentity(driverIdentity: "nvcuda.dll+sha256:changed");
            Assert.AreEqual(cuda1.LookupKeySha256, cuda2.LookupKeySha256);
            Assert.AreNotEqual(cuda1.LookupKeySha256, changedCuda.LookupKeySha256);
            Assert.AreNotEqual(cuda1.LookupKeySha256, CudaIdentity(role: TensorRtCudaKernelRole.Postprocessing).LookupKeySha256);

            TensorRtEngineCacheIdentity engine1 = EngineIdentity();
            TensorRtEngineCacheIdentity engine2 = EngineIdentity();
            Assert.AreEqual(engine1.LookupKeySha256, engine2.LookupKeySha256);
            Assert.AreNotEqual(engine1.LookupKeySha256, EngineIdentity(gpuCompatibilityIdentity: "GPU-other").LookupKeySha256);
            Assert.AreNotEqual(engine1.LookupKeySha256, EngineIdentity(tensorRtIdentity: "nvinfer+sha256:other").LookupKeySha256);
            Assert.AreNotEqual(engine1.LookupKeySha256, EngineIdentity(operatingSystem: "linux-x64").LookupKeySha256);
            Assert.AreNotEqual(engine1.LookupKeySha256, EngineIdentity(workspaceBytes: 268435456).LookupKeySha256);
            Assert.AreNotEqual(engine1.LookupKeySha256, EngineIdentity(flags: new[] { "DISABLE_TF32" }).LookupKeySha256);
            Assert.ThrowsExactly<ArgumentException>(() => EngineIdentity(extension: ".bin"));
            Assert.ThrowsExactly<ArgumentException>(() => EngineIdentity(adapterSchemaVersion: "1"));
        }

        [TestMethod]
        public void StoreIsIdempotentRejectsConflictAndCanExplicitlyReplace()
        {
            string root = NewRoot();
            try
            {
                TensorRtEngineCacheIdentity identity = EngineIdentity();
                byte[] first = Bytes("engine-first");
                byte[] second = Bytes("engine-second");
                var rejecting = new TensorRtExternalCacheStore(root);
                using (var stream = new MemoryStream(first)) Assert.AreEqual(TensorRtExternalCacheStatus.Stored, rejecting.StoreEngine(identity, stream).Status);
                using (var stream = new MemoryStream(first)) Assert.AreEqual(TensorRtExternalCacheStatus.AlreadyPresent, rejecting.StoreEngine(identity, stream).Status);
                using (var stream = new MemoryStream(second))
                using (TensorRtEngineCacheResult conflict = rejecting.StoreEngine(identity, stream))
                {
                    Assert.AreEqual(TensorRtExternalCacheStatus.Conflict, conflict.Status);
                    Assert.AreEqual(TensorRtErrorCodes.ExternalCacheConflict, conflict.ErrorCode);
                }
                using (TensorRtEngineCacheResult unchanged = rejecting.OpenEngine(identity)) CollectionAssert.AreEqual(first, ReadAll(unchanged.Stream!));

                var replacing = new TensorRtExternalCacheStore(root, new TensorRtExternalCacheOptions(conflictPolicy: TensorRtExternalCacheConflictPolicy.Replace));
                using (var stream = new MemoryStream(second)) Assert.AreEqual(TensorRtExternalCacheStatus.Stored, replacing.StoreEngine(identity, stream).Status);
                using (TensorRtEngineCacheResult replaced = replacing.OpenEngine(identity)) CollectionAssert.AreEqual(second, ReadAll(replaced.Stream!));

                TensorRtCudaKernelLookupIdentity cudaIdentity = CudaIdentity();
                TensorRtCudaRtcArtifact cuda = CudaArtifact("ptx-one\0");
                Assert.AreEqual(TensorRtExternalCacheStatus.Stored, rejecting.StoreCuda(cudaIdentity, cuda).Status);
                Assert.AreEqual(TensorRtExternalCacheStatus.AlreadyPresent, rejecting.StoreCuda(cudaIdentity, cuda).Status);
                Assert.AreEqual(TensorRtExternalCacheStatus.Conflict, rejecting.StoreCuda(cudaIdentity, CudaArtifact("ptx-two\0")).Status);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public void CorruptTruncatedAndSwappedPayloadsAreRejectedNotHits()
        {
            string root = NewRoot();
            try
            {
                var store = new TensorRtExternalCacheStore(root);
                TensorRtEngineCacheIdentity firstIdentity = EngineIdentity();
                TensorRtEngineCacheIdentity secondIdentity = EngineIdentity(gpuCompatibilityIdentity: "GPU-second");
                using (var stream = new MemoryStream(Bytes("first-engine"))) store.StoreEngine(firstIdentity, stream).Dispose();
                using (var stream = new MemoryStream(Bytes("second-engine-longer"))) store.StoreEngine(secondIdentity, stream).Dispose();
                (_, _, string firstPayload) = Paths(root, "engine", firstIdentity.LookupKeySha256);
                (_, _, string secondPayload) = Paths(root, "engine", secondIdentity.LookupKeySha256);

                File.WriteAllBytes(firstPayload, File.ReadAllBytes(secondPayload));
                using (TensorRtEngineCacheResult swapped = store.OpenEngine(firstIdentity))
                {
                    Assert.AreEqual(TensorRtExternalCacheStatus.Rejected, swapped.Status);
                    Assert.AreEqual(TensorRtExternalCacheRejectionReason.IntegrityMismatch, swapped.RejectionReason);
                    Assert.AreEqual(TensorRtErrorCodes.ExternalCacheEntryRejected, swapped.ErrorCode);
                }

                store.InvalidateEngine(firstIdentity);
                using (var stream = new MemoryStream(Bytes("first-engine"))) store.StoreEngine(firstIdentity, stream).Dispose();
                (_, _, firstPayload) = Paths(root, "engine", firstIdentity.LookupKeySha256);
                using (var file = new FileStream(firstPayload, FileMode.Open, FileAccess.Write, FileShare.None)) file.SetLength(2);
                using TensorRtEngineCacheResult truncated = store.OpenEngine(firstIdentity);
                Assert.AreEqual(TensorRtExternalCacheStatus.Rejected, truncated.Status);
                Assert.AreEqual(TensorRtExternalCacheRejectionReason.IntegrityMismatch, truncated.RejectionReason);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public void UnknownSchemaMissingFieldsPathTraversalAndKeyMismatchAreRejected()
        {
            string root = NewRoot();
            try
            {
                TensorRtEngineCacheIdentity identity = EngineIdentity();
                var store = new TensorRtExternalCacheStore(root);
                using (var stream = new MemoryStream(Bytes("engine"))) store.StoreEngine(identity, stream).Dispose();
                (string currentPath, string manifestPath, _) = Paths(root, "engine", identity.LookupKeySha256);

                RewriteJson(currentPath, rootElement => new Dictionary<string, object?>
                {
                    ["schemaVersion"] = 99,
                    ["generation"] = rootElement.GetProperty("generation").GetString(),
                    ["manifestFileName"] = "manifest.json",
                    ["manifestLength"] = rootElement.GetProperty("manifestLength").GetInt64(),
                    ["manifestSha256"] = rootElement.GetProperty("manifestSha256").GetString(),
                    ["payloadLength"] = rootElement.GetProperty("payloadLength").GetInt64(),
                    ["payloadSha256"] = rootElement.GetProperty("payloadSha256").GetString()
                });
                using (TensorRtEngineCacheResult schema = store.OpenEngine(identity)) Assert.AreEqual(TensorRtExternalCacheRejectionReason.ManifestInvalid, schema.RejectionReason);

                store.InvalidateEngine(identity);
                using (var stream = new MemoryStream(Bytes("engine"))) store.StoreEngine(identity, stream).Dispose();
                (currentPath, manifestPath, _) = Paths(root, "engine", identity.LookupKeySha256);
                RewriteManifestAndCompletion(manifestPath, currentPath, element => RewriteManifest(element, payloadFileName: "../escape.plan"));
                using (TensorRtEngineCacheResult traversal = store.OpenEngine(identity)) Assert.AreEqual(TensorRtExternalCacheRejectionReason.UnsafePath, traversal.RejectionReason);

                store.InvalidateEngine(identity);
                using (var stream = new MemoryStream(Bytes("engine"))) store.StoreEngine(identity, stream).Dispose();
                (currentPath, manifestPath, _) = Paths(root, "engine", identity.LookupKeySha256);
                RewriteManifestAndCompletion(manifestPath, currentPath, element => RewriteManifest(element, lookupKeySha256: new string('f', 64)));
                using TensorRtEngineCacheResult mismatch = store.OpenEngine(identity);
                Assert.AreEqual(TensorRtExternalCacheRejectionReason.IdentityMismatch, mismatch.RejectionReason);

                store.InvalidateEngine(identity);
                using (var stream = new MemoryStream(Bytes("engine"))) store.StoreEngine(identity, stream).Dispose();
                (currentPath, manifestPath, _) = Paths(root, "engine", identity.LookupKeySha256);
                RewriteManifestAndCompletion(manifestPath, currentPath, RewriteManifestWithoutEngineDetails);
                using (TensorRtEngineCacheResult missing = store.OpenEngine(identity)) Assert.AreEqual(TensorRtExternalCacheRejectionReason.ManifestInvalid, missing.RejectionReason);

                store.InvalidateEngine(identity);
                using (var stream = new MemoryStream(Bytes("engine"))) store.StoreEngine(identity, stream).Dispose();
                (currentPath, manifestPath, _) = Paths(root, "engine", identity.LookupKeySha256);
                RewriteManifestAndCompletion(manifestPath, currentPath, RewriteManifestWithUnknownSchema);
                using TensorRtEngineCacheResult unknownManifestSchema = store.OpenEngine(identity);
                Assert.AreEqual(TensorRtExternalCacheRejectionReason.ManifestInvalid, unknownManifestSchema.RejectionReason);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public void ManifestSwapIsRejectedEvenWhenCompletionIntegrityIsUpdated()
        {
            string root = NewRoot();
            try
            {
                TensorRtEngineCacheIdentity first = EngineIdentity();
                TensorRtEngineCacheIdentity second = EngineIdentity(gpuCompatibilityIdentity: "GPU-second");
                var store = new TensorRtExternalCacheStore(root);
                using (var stream = new MemoryStream(Bytes("same-engine"))) store.StoreEngine(first, stream).Dispose();
                using (var stream = new MemoryStream(Bytes("same-engine"))) store.StoreEngine(second, stream).Dispose();
                (string firstCurrent, string firstManifest, _) = Paths(root, "engine", first.LookupKeySha256);
                (_, string secondManifest, _) = Paths(root, "engine", second.LookupKeySha256);
                byte[] swapped = File.ReadAllBytes(secondManifest);
                File.WriteAllBytes(firstManifest, swapped);
                RewriteCompletionManifestIntegrity(firstCurrent, swapped);

                using TensorRtEngineCacheResult result = store.OpenEngine(first);
                Assert.AreEqual(TensorRtExternalCacheStatus.Rejected, result.Status);
                Assert.AreEqual(TensorRtExternalCacheRejectionReason.IdentityMismatch, result.RejectionReason);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public void ManifestTamperDirectorySubstitutionAndSizeLimitsAreRejected()
        {
            string root = NewRoot();
            try
            {
                TensorRtEngineCacheIdentity identity = EngineIdentity();
                var store = new TensorRtExternalCacheStore(root, new TensorRtExternalCacheOptions(maximumEngineBytes: 16));
                using (var oversized = new MemoryStream(new byte[17]))
                {
                    TensorRtBackendException exception = Assert.ThrowsExactly<TensorRtBackendException>(() => store.StoreEngine(identity, oversized));
                    Assert.AreEqual(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, exception.ErrorCode);
                }
                AssertNoTemporaryEntries(root);

                var cudaStore = new TensorRtExternalCacheStore(Path.Combine(root, "cuda-limit"), new TensorRtExternalCacheOptions(maximumCudaArtifactBytes: 4));
                TensorRtBackendException cudaLimit = Assert.ThrowsExactly<TensorRtBackendException>(() => cudaStore.StoreCuda(CudaIdentity(), CudaArtifact("12345")));
                Assert.AreEqual(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, cudaLimit.ErrorCode);
                AssertNoTemporaryEntries(Path.Combine(root, "cuda-limit"));

                using (var stream = new MemoryStream(Bytes("engine"))) store.StoreEngine(identity, stream).Dispose();
                (string currentPath, string manifestPath, _) = Paths(root, "engine", identity.LookupKeySha256);
                File.AppendAllText(manifestPath, " ");
                using (TensorRtEngineCacheResult tampered = store.OpenEngine(identity)) Assert.AreEqual(TensorRtExternalCacheRejectionReason.IntegrityMismatch, tampered.RejectionReason);

                store.InvalidateEngine(identity);
                using (var stream = new MemoryStream(Bytes("engine"))) store.StoreEngine(identity, stream).Dispose();
                (currentPath, _, _) = Paths(root, "engine", identity.LookupKeySha256);
                File.Delete(currentPath);
                Directory.CreateDirectory(currentPath);
                using TensorRtEngineCacheResult directory = store.OpenEngine(identity);
                Assert.AreEqual(TensorRtExternalCacheRejectionReason.UnsafePath, directory.RejectionReason);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public void RejectedEntryPolicyIsObservableForKeepDeleteAndQuarantine()
        {
            foreach (TensorRtExternalCacheRejectedEntryPolicy policy in Enum.GetValues<TensorRtExternalCacheRejectedEntryPolicy>())
            {
                string root = NewRoot();
                try
                {
                    TensorRtEngineCacheIdentity identity = EngineIdentity();
                    var writer = new TensorRtExternalCacheStore(root);
                    using (var stream = new MemoryStream(Bytes("engine"))) writer.StoreEngine(identity, stream).Dispose();
                    (_, _, string payloadPath) = Paths(root, "engine", identity.LookupKeySha256);
                    File.WriteAllText(payloadPath, "tampered");
                    var reader = new TensorRtExternalCacheStore(root, new TensorRtExternalCacheOptions(rejectedEntryPolicy: policy));
                    using TensorRtEngineCacheResult result = reader.OpenEngine(identity);
                    Assert.AreEqual(TensorRtExternalCacheStatus.Rejected, result.Status);
                    TensorRtExternalCacheRemediation expected = policy == TensorRtExternalCacheRejectedEntryPolicy.Keep
                        ? TensorRtExternalCacheRemediation.None
                        : policy == TensorRtExternalCacheRejectedEntryPolicy.Delete
                            ? TensorRtExternalCacheRemediation.Deleted
                            : TensorRtExternalCacheRemediation.Quarantined;
                    Assert.AreEqual(expected, result.Remediation);
                    if (policy == TensorRtExternalCacheRejectedEntryPolicy.Quarantine)
                    {
                        Assert.IsNotNull(result.RemediationPath);
                        Assert.IsTrue(Directory.Exists(result.RemediationPath));
                        StringAssert.StartsWith(Path.GetFullPath(result.RemediationPath), Path.GetFullPath(root));
                    }
                }
                finally { DeleteRoot(root); }
            }
        }

        [TestMethod]
        public void CancellationAndFactoryFailureLeaveNoCompletedOrTemporaryEntry()
        {
            string root = NewRoot();
            try
            {
                var store = new TensorRtExternalCacheStore(root);
                TensorRtEngineCacheIdentity engineIdentity = EngineIdentity();
                using (var canceled = new CancellationTokenSource())
                {
                    canceled.Cancel();
                    Assert.ThrowsExactly<OperationCanceledException>(() => store.GetOrBuildEngine(
                        engineIdentity,
                        _ => new MemoryStream(new byte[32]),
                        canceled.Token));
                }
                AssertNoTemporaryEntries(root);
                using (TensorRtEngineCacheResult miss = store.OpenEngine(engineIdentity)) Assert.AreEqual(TensorRtExternalCacheStatus.Miss, miss.Status);

                using (var duringWrite = new CancellationTokenSource())
                using (var cancelingStream = new CancelingReadStream(new byte[256], duringWrite))
                {
                    Assert.ThrowsExactly<OperationCanceledException>(() => store.StoreEngine(engineIdentity, cancelingStream, duringWrite.Token));
                }
                AssertNoTemporaryEntries(root);
                using (TensorRtEngineCacheResult missAfterCanceledWrite = store.OpenEngine(engineIdentity)) Assert.AreEqual(TensorRtExternalCacheStatus.Miss, missAfterCanceledWrite.Status);

                TensorRtCudaKernelLookupIdentity cudaIdentity = CudaIdentity();
                Assert.ThrowsExactly<InvalidOperationException>(() => store.GetOrCompileCuda(cudaIdentity, _ => throw new InvalidOperationException("factory failure")));
                Assert.AreEqual(TensorRtExternalCacheStatus.Miss, store.LookupCuda(cudaIdentity).Status);
                AssertNoTemporaryEntries(root);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public async Task ConcurrentGetOrCompileExecutesFactoryOnceAcrossStoreInstances()
        {
            string root = NewRoot();
            try
            {
                TensorRtCudaKernelLookupIdentity identity = CudaIdentity();
                TensorRtCudaRtcArtifact artifact = CudaArtifact("concurrent-ptx\0");
                var firstStore = new TensorRtExternalCacheStore(root);
                var secondStore = new TensorRtExternalCacheStore(root);
                int calls = 0;
                var start = new ManualResetEventSlim(false);
                Task<TensorRtCudaCacheResult>[] tasks = Enumerable.Range(0, 12).Select(index => Task.Run(() =>
                {
                    start.Wait();
                    TensorRtExternalCacheStore store = index % 2 == 0 ? firstStore : secondStore;
                    return store.GetOrCompileCuda(identity, _ =>
                    {
                        Interlocked.Increment(ref calls);
                        Thread.Sleep(20);
                        return artifact;
                    });
                })).ToArray();
                start.Set();
                TensorRtCudaCacheResult[] results = await Task.WhenAll(tasks);
                Assert.AreEqual(1, calls);
                Assert.AreEqual(1, results.Count(result => result.FactoryExecuted));
                Assert.IsTrue(results.All(result => result.Status == TensorRtExternalCacheStatus.Stored || result.Status == TensorRtExternalCacheStatus.Hit));
                AssertNoTemporaryEntries(root);
            }
            finally { DeleteRoot(root); }
        }

        [TestMethod]
        public async Task DifferentRootsDoNotBlockEachOther()
        {
            string firstRoot = NewRoot();
            string secondRoot = NewRoot();
            try
            {
                TensorRtCudaKernelLookupIdentity identity = CudaIdentity();
                TensorRtCudaRtcArtifact artifact = CudaArtifact("root-isolation\0");
                var firstEntered = new ManualResetEventSlim(false);
                var releaseFirst = new ManualResetEventSlim(false);
                var secondEntered = new ManualResetEventSlim(false);
                var firstStore = new TensorRtExternalCacheStore(firstRoot);
                var secondStore = new TensorRtExternalCacheStore(secondRoot);
                Task<TensorRtCudaCacheResult> first = Task.Run(() => firstStore.GetOrCompileCuda(identity, _ =>
                {
                    firstEntered.Set();
                    releaseFirst.Wait();
                    return artifact;
                }));
                Assert.IsTrue(firstEntered.Wait(TimeSpan.FromSeconds(5)));
                Task<TensorRtCudaCacheResult> second = Task.Run(() => secondStore.GetOrCompileCuda(identity, _ =>
                {
                    secondEntered.Set();
                    return artifact;
                }));
                Assert.IsTrue(secondEntered.Wait(TimeSpan.FromSeconds(5)), "A different root was blocked by an unrelated key gate.");
                releaseFirst.Set();
                await Task.WhenAll(first, second);
            }
            finally
            {
                DeleteRoot(firstRoot);
                DeleteRoot(secondRoot);
            }
        }

        [TestMethod]
        public void ReparsePointPayloadIsRejectedWhenPlatformAllowsCreation()
        {
            string root = NewRoot();
            string external = NewRoot();
            try
            {
                TensorRtEngineCacheIdentity identity = EngineIdentity();
                var store = new TensorRtExternalCacheStore(root);
                using (var stream = new MemoryStream(Bytes("engine"))) store.StoreEngine(identity, stream).Dispose();
                (_, _, string payloadPath) = Paths(root, "engine", identity.LookupKeySha256);
                string externalFile = Path.Combine(external, "external.engine");
                File.WriteAllBytes(externalFile, Bytes("engine"));
                File.Delete(payloadPath);
                try { File.CreateSymbolicLink(payloadPath, externalFile); }
                catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is PlatformNotSupportedException) { return; }

                using TensorRtEngineCacheResult result = store.OpenEngine(identity);
                Assert.AreEqual(TensorRtExternalCacheStatus.Rejected, result.Status);
                Assert.AreEqual(TensorRtExternalCacheRejectionReason.UnsafePath, result.RejectionReason);
            }
            finally
            {
                DeleteRoot(root);
                DeleteRoot(external);
            }
        }

        [TestMethod]
        public void GetOrBuildAndGetOrCompileSkipFactoriesOnHit()
        {
            string root = NewRoot();
            try
            {
                var store = new TensorRtExternalCacheStore(root);
                TensorRtCudaKernelLookupIdentity cudaIdentity = CudaIdentity();
                int compileCalls = 0;
                TensorRtCudaCacheResult compiled = store.GetOrCompileCuda(cudaIdentity, _ => { compileCalls++; return CudaArtifact("compiled\0"); });
                Assert.IsTrue(compiled.FactoryExecuted);
                TensorRtCudaCacheResult cudaHit = store.GetOrCompileCuda(cudaIdentity, _ => throw new AssertFailedException("NVRTC factory ran on hit."));
                Assert.AreEqual(TensorRtExternalCacheStatus.Hit, cudaHit.Status);
                Assert.IsFalse(cudaHit.FactoryExecuted);
                Assert.AreEqual(1, compileCalls);

                TensorRtEngineCacheIdentity engineIdentity = EngineIdentity();
                int buildCalls = 0;
                using (TensorRtEngineCacheResult built = store.GetOrBuildEngine(engineIdentity, _ =>
                {
                    buildCalls++;
                    return new MemoryStream(Bytes("built-engine"));
                }))
                {
                    Assert.AreEqual(TensorRtExternalCacheStatus.Stored, built.Status);
                    Assert.IsTrue(built.FactoryExecuted);
                    CollectionAssert.AreEqual(Bytes("built-engine"), ReadAll(built.Stream!));
                }
                using TensorRtEngineCacheResult engineHit = store.GetOrBuildEngine(engineIdentity, _ => throw new AssertFailedException("TensorRT factory ran on hit."));
                Assert.AreEqual(TensorRtExternalCacheStatus.Hit, engineHit.Status);
                Assert.IsFalse(engineHit.FactoryExecuted);
                Assert.AreEqual(1, buildCalls);
                AssertNoTemporaryEntries(root);
            }
            finally { DeleteRoot(root); }
        }

        private const string CompilerIdentity = "nvrtc64_120_0.dll+sha256:nvrtc";
        private const string CudaVersion = "12.9";
        private const string CudaIdentityValue = "cudart64_12.dll+sha256:cudart";
        private const string DriverVersion = "576.02";
        private const string DriverIdentity = "nvcuda.dll+sha256:driver";
        private const string GpuArchitecture = "sm_86";
        private const string GpuCompatibilityIdentity = "GPU-test";
        private const string BridgeIdentity = "bridge+sha256:bridge";

        private static TensorRtCudaKernelLookupIdentity CudaIdentity(
            string driverIdentity = DriverIdentity,
            TensorRtCudaKernelRole role = TensorRtCudaKernelRole.Preprocessing,
            TensorRtCudaRtcArtifactKind kind = TensorRtCudaRtcArtifactKind.Ptx)
        {
            var definition = new TensorRtCudaRtcKernelDefinition(role, "extern \"C\" __global__ void kernel() {}\n", "kernel", "kernel.cu");
            var options = new TensorRtCudaRtcCompileOptions(kind == TensorRtCudaRtcArtifactKind.Cubin ? "sm_86" : "compute_86", kind);
            return new TensorRtCudaKernelLookupIdentity(
                definition, options, CudaVersion, CompilerIdentity, CudaVersion, CudaIdentityValue,
                DriverVersion, driverIdentity, GpuArchitecture, GpuCompatibilityIdentity, BridgeIdentity);
        }

        private static TensorRtCudaRtcArtifact CudaArtifact(string payload, TensorRtCudaRtcArtifactKind kind = TensorRtCudaRtcArtifactKind.Ptx)
        {
            TensorRtCudaKernelLookupIdentity identity = CudaIdentity(kind: kind);
            return new TensorRtCudaRtcArtifact(
                Encoding.ASCII.GetBytes(payload), identity.ArtifactKind, identity.Role,
                identity.SourceSha256, identity.HeadersSha256, identity.OptionsSha256,
                identity.CompilerVersion, identity.TargetArchitecture, identity.ProgramName, identity.KernelName);
        }

        private static TensorRtEngineCacheIdentity EngineIdentity(
            string extension = ".engine",
            string gpuCompatibilityIdentity = "GPU-test",
            string tensorRtIdentity = "nvinfer+sha256:nvinfer",
            string operatingSystem = "windows-11",
            ulong workspaceBytes = 536870912,
            IEnumerable<string>? flags = null,
            string adapterSchemaVersion = TensorRtEngineCacheIdentity.CurrentAdapterSchemaVersion)
        {
            var profile = new TensorRtOnnxInputProfile(
                "images",
                new TensorShape(1, 3, 224, 224),
                new TensorShape(4, 3, 224, 224),
                new TensorShape(8, 3, 224, 224));
            var options = new TensorRtOnnxEngineBuildOptions(
                TensorRtApiVersion.TensorRt10,
                TensorRtOnnxEnginePrecision.Float16,
                workspaceBytes: workspaceBytes,
                optimizationLevel: 4,
                inputProfiles: new[] { profile });
            return new TensorRtEngineCacheIdentity(
                new string('a', 64), options,
                "JYPPX.TensorRT.CSharp.API/4.0.0+contentHash:test", new string('b', 64),
                "10.11.0.33", tensorRtIdentity,
                "12.9", "cudart+sha256:cudart",
                "9.22", "cudnn+sha256:cudnn",
                DriverVersion, DriverIdentity,
                BridgeIdentity, gpuCompatibilityIdentity, "8.6",
                operatingSystem, "x64", extension,
                flags ?? new[] { "FP16", "PREFER_PRECISION_CONSTRAINTS" }, adapterSchemaVersion);
        }

        private static (string CurrentPath, string ManifestPath, string PayloadPath) Paths(string root, string category, string key)
        {
            string entry = Path.Combine(root, "deploysharp-tensorrt-cache-v1", category, key.Substring(0, 2), key);
            string currentPath = Path.Combine(entry, "current.json");
            using JsonDocument current = JsonDocument.Parse(File.ReadAllBytes(currentPath));
            string generation = current.RootElement.GetProperty("generation").GetString()!;
            string generationPath = Path.Combine(entry, generation);
            string manifestPath = Path.Combine(generationPath, "manifest.json");
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
            string payloadName = manifest.RootElement.GetProperty("payload").GetProperty("fileName").GetString()!;
            return (currentPath, manifestPath, Path.Combine(generationPath, payloadName));
        }

        private static byte[] RewriteManifest(JsonElement source, string? payloadFileName = null, string? lookupKeySha256 = null)
        {
            using var memory = new MemoryStream();
            using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in source.EnumerateObject())
                {
                    if (property.NameEquals("lookupKeySha256") && lookupKeySha256 != null) writer.WriteString(property.Name, lookupKeySha256);
                    else if (property.NameEquals("payload") && payloadFileName != null)
                    {
                        writer.WriteStartObject("payload");
                        writer.WriteString("fileName", payloadFileName);
                        writer.WriteNumber("length", property.Value.GetProperty("length").GetInt64());
                        writer.WriteString("sha256", property.Value.GetProperty("sha256").GetString());
                        writer.WriteEndObject();
                    }
                    else property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            return memory.ToArray();
        }

        private static void RewriteManifestAndCompletion(string manifestPath, string currentPath, Func<JsonElement, byte[]> rewrite)
        {
            byte[] rewritten;
            using (JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath))) rewritten = rewrite(manifest.RootElement);
            File.WriteAllBytes(manifestPath, rewritten);
            byte[] currentBytes = File.ReadAllBytes(currentPath);
            using JsonDocument current = JsonDocument.Parse(currentBytes);
            RewriteJson(currentPath, element => new Dictionary<string, object?>
            {
                ["schemaVersion"] = element.GetProperty("schemaVersion").GetInt32(),
                ["generation"] = element.GetProperty("generation").GetString(),
                ["manifestFileName"] = element.GetProperty("manifestFileName").GetString(),
                ["manifestLength"] = rewritten.LongLength,
                ["manifestSha256"] = Sha256(rewritten),
                ["payloadLength"] = element.GetProperty("payloadLength").GetInt64(),
                ["payloadSha256"] = element.GetProperty("payloadSha256").GetString()
            });
        }

        private static byte[] RewriteManifestWithoutEngineDetails(JsonElement source)
        {
            using var memory = new MemoryStream();
            using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in source.EnumerateObject())
                {
                    if (!property.NameEquals("engine")) property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            return memory.ToArray();
        }

        private static byte[] RewriteManifestWithUnknownSchema(JsonElement source)
        {
            using var memory = new MemoryStream();
            using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in source.EnumerateObject())
                {
                    if (property.NameEquals("schemaVersion")) writer.WriteNumber("schemaVersion", 2);
                    else property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            return memory.ToArray();
        }

        private static void RewriteCompletionManifestIntegrity(string currentPath, byte[] manifestBytes)
        {
            RewriteJson(currentPath, element => new Dictionary<string, object?>
            {
                ["schemaVersion"] = element.GetProperty("schemaVersion").GetInt32(),
                ["generation"] = element.GetProperty("generation").GetString(),
                ["manifestFileName"] = element.GetProperty("manifestFileName").GetString(),
                ["manifestLength"] = manifestBytes.LongLength,
                ["manifestSha256"] = Sha256(manifestBytes),
                ["payloadLength"] = element.GetProperty("payloadLength").GetInt64(),
                ["payloadSha256"] = element.GetProperty("payloadSha256").GetString()
            });
        }

        private static void RewriteJson(string path, Func<JsonElement, Dictionary<string, object?>> replacement)
        {
            Dictionary<string, object?> values;
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path))) values = replacement(document.RootElement);
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(values, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void AssertNoTemporaryEntries(string root)
        {
            if (!Directory.Exists(root)) return;
            string[] temporary = Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).StartsWith("tmp-", StringComparison.Ordinal) ||
                               Path.GetFileName(path).StartsWith(".current-", StringComparison.Ordinal) ||
                               Path.GetExtension(path).Equals(".tmp", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.AreEqual(0, temporary.Length, "Temporary cache entries remained: " + string.Join(",", temporary));
        }

        private static byte[] ReadAll(Stream stream)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        private static string NewRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-trt-cache-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

        private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        private sealed class CancelingReadStream : MemoryStream
        {
            private readonly CancellationTokenSource _cancellation;
            private bool _canceled;

            public CancelingReadStream(byte[] bytes, CancellationTokenSource cancellation) : base(bytes, writable: false)
            {
                _cancellation = cancellation;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = base.Read(buffer, offset, Math.Min(count, 8));
                if (!_canceled && read > 0)
                {
                    _canceled = true;
                    _cancellation.Cancel();
                }
                return read;
            }
        }
    }
}
