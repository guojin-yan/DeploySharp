using System.Text.Json;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using DeploySharpApp.Contracts;
using DeploySharpApp.Infrastructure;
using DeploySharpApp.Web;

namespace DeploySharpApp.Application.Tests;

[TestClass]
public sealed class WebPersistenceTests
{
    [TestMethod]
    public void BenchmarkHistoryPersistsVerifiedReportsAndCapsEntries()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DeploySharpAppTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "history.json");
        try
        {
            var store = new BenchmarkHistoryStore(path);
            for (int index = 0; index < 55; index++)
            {
                var request = new BenchmarkRequest("tests/model-" + index, "deploysharp.backend.onnxruntime", iterations: 1);
                store.Add(new BenchmarkReport(request, index % 2 == 0, "report-" + index, p50Ms: index, p95Ms: index + 1, throughput: 100 + index, executionMode: "worker", metadata: new Dictionary<string, string> { ["rid"] = "win-x64" }));
            }

            Assert.AreEqual(50, store.Entries.Count);
            Assert.AreEqual("tests/model-54", store.Entries[0].ModelId);
            Assert.IsTrue(File.Exists(path));

            var reloaded = new BenchmarkHistoryStore(path);
            Assert.AreEqual(50, reloaded.Entries.Count);
            Assert.AreEqual("tests/model-54", reloaded.Entries[0].ModelId);
            using JsonDocument json = JsonDocument.Parse(reloaded.ExportJson());
            Assert.AreEqual(50, json.RootElement.GetArrayLength());
            Assert.AreEqual("win-x64", json.RootElement[0].GetProperty("Metadata").GetProperty("rid").GetString());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void DefaultManifestsUsePublishedBackendPackageVersions()
    {
        IReadOnlyList<DeploySharpApp.Plugin.Abstractions.PluginManifest> manifests = DeploySharpApp.Application.DefaultManifests.Create();
        Assert.AreEqual("5.0.0", manifests.Single(item => item.PluginId == "deploysharp.backend.opencv").Version);
        Assert.AreEqual("3.3.1", manifests.Single(item => item.PluginId == "deploysharp.backend.openvino").Version);
        var tensorRt = manifests.Single(item => item.PluginId == "deploysharp.backend.tensorrt");
        Assert.IsTrue(tensorRt.RuntimeDependencies!.Any(item => item.PackageId!.StartsWith("JYPPX.TensorRT.CSharp.API.Runtime.win-x64", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task BackendLifecycleStagesVerifiedNugetPackagesBeforeActivation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DeploySharpAppTests", Guid.NewGuid().ToString("N"));
        try
        {
            byte[] package = CreatePackage();
            using var http = new HttpClient(new PackageHandler(package));
            var lifecycle = new BackendLifecycleService(
                AppComposition.CreateService(),
                http,
                Path.Combine(directory, "state.json"),
                Path.Combine(directory, "packages"));

            BackendLifecycleRecord staged = await lifecycle.InstallOrUpgradeAsync("deploysharp.backend.opencv");
            Assert.IsTrue(staged.State is BackendLifecycleState.Staged or BackendLifecycleState.Active);
            Assert.AreEqual(2, staged.Packages!.Count);
            Assert.IsTrue(Directory.EnumerateFiles(staged.InstallRoot!, "*.nupkg", SearchOption.AllDirectories).Any());
            Assert.IsTrue(Directory.EnumerateFiles(staged.InstallRoot!, "opencv_core500.dll", SearchOption.AllDirectories).Any());

            BackendLifecycleRecord rejected = lifecycle.ActivateAfterProbe(staged.BackendId, new RuntimeProbeEvidence(false, staged.BackendId, "Unavailable", "DSAPP-TEST", "smoke failed", new Dictionary<string, string>()));
            Assert.AreEqual(BackendLifecycleState.Staged, rejected.State);
            BackendLifecycleRecord active = lifecycle.ActivateAfterProbe(staged.BackendId, new RuntimeProbeEvidence(true, staged.BackendId, "Available", "DSAPP-TEST", "smoke passed", new Dictionary<string, string>()));
            Assert.AreEqual(BackendLifecycleState.Active, active.State);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] CreatePackage()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("runtimes/win-x64/native/opencv_core500.dll");
            using Stream target = entry.Open();
            target.Write(new byte[] { 1, 2, 3, 4 });
        }
        return stream.ToArray();
    }

    private sealed class PackageHandler : HttpMessageHandler
    {
        private readonly byte[] _package;
        private readonly string _sha512;

        public PackageHandler(byte[] package)
        {
            _package = package;
            _sha512 = Convert.ToBase64String(SHA512.HashData(package));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpContent content = request.RequestUri!.AbsolutePath.EndsWith(".sha512", StringComparison.OrdinalIgnoreCase)
                ? new StringContent(_sha512)
                : new ByteArrayContent(_package);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
        }
    }
}
