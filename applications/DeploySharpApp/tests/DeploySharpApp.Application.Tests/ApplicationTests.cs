using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DeploySharpApp.Application;
using DeploySharpApp.BackendHost.Protocol;
using DeploySharpApp.Contracts;
using DeploySharpApp.Engine;
using DeploySharpApp.Infrastructure;

namespace DeploySharpApp.Application.Tests
{
    [TestClass]
    public class ApplicationTests
    {
        [TestMethod]
        public async Task FakeRunnerReportsProgressAndResult()
        {
            var service = AppComposition.CreateService();
            var progress = new Progress<double>();
            var result = await service.RunAsync(new ModelRunRequest(AppOperationKind.Vision, "demo/yolo-v8-n", "deploysharp.backend.onnxruntime"), progress, CancellationToken.None);
            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Output));
            Assert.AreEqual(ModelRunMode.Demo, result.RunMode);
        }

        [TestMethod]
        public void MissingTensorRuntimeIsStructured()
        {
            var catalog = new InMemoryAppCatalog(DefaultManifests.Create(), new LocalRuntimeProbe());
            var status = catalog.GetRuntimeStatuses();
            Assert.IsTrue(status.Count >= 5);
            Assert.IsTrue(status.Any(item => item.BackendId == "deploysharp.backend.tensorrt" && item.State != AppRuntimeState.Available));
        }

        [TestMethod]
        public async Task RunnerHonorsCancellation()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => new FakeModelRunner().RunAsync(new ModelRunRequest(AppOperationKind.Vision, "demo/yolo-v8-n", "deploysharp.backend.onnxruntime"), null, cancellation.Token));
        }

        [TestMethod]
        public async Task FakeRunnerHonorsRequestTimeout()
        {
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => new FakeModelRunner().RunAsync(new ModelRunRequest(AppOperationKind.Vision, "demo/yolo-v8-n", "deploysharp.backend.onnxruntime", timeout: TimeSpan.FromMilliseconds(1)), null, CancellationToken.None));
        }

        [TestMethod]
        public async Task CompositionRoutesNonDemoRequestToRealEngine()
        {
            var service = AppComposition.CreateService();
            var path = Path.Combine(Path.GetTempPath(), "deploysharp-app-missing-" + Guid.NewGuid().ToString("N") + ".onnx");
            var result = await service.RunAsync(new ModelRunRequest(AppOperationKind.Vision, "tests/missing", "deploysharp.backend.onnxruntime", modelPath: path, modelFormat: "onnx"), null, CancellationToken.None);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AppErrorCode.ModelUnavailable, result.ErrorCode);
            Assert.AreEqual(ModelRunMode.RealOnnxRuntime, result.RunMode);
        }

        [TestMethod]
        public async Task MissingWorkerHostReturnsStructuredWorkerRequired()
        {
            var missingHost = Path.Combine(Path.GetTempPath(), "deploysharp-worker-" + Guid.NewGuid().ToString("N"), "BackendHost.dll");
            var request = new ModelRunRequest(AppOperationKind.TextGeneration, "demo/qwen-0.5b", "deploysharp.backend.llamasharp", modelFormat: "gguf", prompt: "hello");
            ModelRunResult result = await new BackendHostWorkerClient(missingHost).RunAsync(request, null, CancellationToken.None);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AppErrorCode.WorkerRequired, result.ErrorCode);
            Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
            Assert.AreEqual("DSAPP-WORKER-HOST-NOT-CONFIGURED", result.Diagnostics.Single().Code);
        }

        [TestMethod]
        public async Task ReachableWorkerReportsNativeStatusWithoutFakeResult()
        {
            string hostPath = LocateBackendHost();
            string modelPath = Path.Combine(Path.GetTempPath(), "deploysharp-app-missing-" + Guid.NewGuid().ToString("N") + ".gguf");
            var request = new ModelRunRequest(AppOperationKind.TextGeneration, "demo/qwen-0.5b", "deploysharp.backend.llamasharp", modelFormat: "gguf", modelPath: modelPath, prompt: "hello");
            var values = new List<double>();
            ModelRunResult result = await new BackendHostWorkerClient(hostPath).RunAsync(request, new Progress<double>(value => values.Add(value)), CancellationToken.None);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AppErrorCode.ModelUnavailable, result.ErrorCode);
            Assert.IsNotNull(result.RuntimeStatus);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == "DSAPP-WORKER-MODEL-NOT-FOUND"));
            Assert.IsTrue(values.Any(value => value >= 0.35), "Worker progress events should be forwarded to the application progress reporter.");
        }

        [TestMethod]
        public async Task WorkerCapabilityAndNativeProbesCoverFourBackends()
        {
            var client = new BackendHostWorkerClient(LocateBackendHost());
            WorkerResponse capability = await client.SendAsync(new WorkerRequest(WorkerMessageKind.Capability, "capability-test"), TimeSpan.FromSeconds(10), CancellationToken.None);
            Assert.IsTrue(capability.Succeeded);
            string backendList = capability.Payload["backends"];
            foreach (string backendId in new[] { "deploysharp.backend.llamasharp", "deploysharp.backend.tensorrt", "deploysharp.backend.opencv", "deploysharp.backend.openvino" })
            {
                StringAssert.Contains(backendList, backendId);
                WorkerResponse probe = await client.SendAsync(new WorkerRequest(WorkerMessageKind.Probe, "probe-" + backendId.Replace('.', '-'), backendId), TimeSpan.FromSeconds(10), CancellationToken.None);
                Assert.AreEqual(WorkerResponseKind.Probe, probe.Kind, backendId);
                Assert.IsFalse(probe.Succeeded, "Filesystem discovery alone must not report an executable backend. " + backendId);
                Assert.AreEqual(backendId, probe.Payload["backendId"]);
                Assert.IsTrue(probe.Payload.ContainsKey("probedPaths"));
                Assert.IsTrue(probe.Payload.ContainsKey("diagnosticCode"));
            }
        }

        [TestMethod]
        public async Task NativeBackendsAreRoutedToWorkerClient()
        {
            foreach (string backendId in new[] { "deploysharp.backend.llamasharp", "deploysharp.backend.tensorrt", "deploysharp.backend.opencv", "deploysharp.backend.openvino" })
            {
                var worker = new StubWorkerClient();
                var runner = new EngineModelRunner(new ThrowingEngine(), new FakeModelRunner(), worker);
                ModelRunResult result = await runner.RunAsync(new ModelRunRequest(AppOperationKind.Vision, "tests/native-backend", backendId, prompt: "hello"), null, CancellationToken.None);
                Assert.IsTrue(worker.RunCalled, backendId);
                Assert.AreEqual(AppErrorCode.WorkerRequired, result.ErrorCode, backendId);
            }
        }

        [TestMethod]
        public async Task OpenVinoWorkerExecutesNamedTensorInference()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages the Windows OpenVINO runtime.");
            ModelRunResult result = await RunTensorWorkerAsync("deploysharp.backend.openvino");
            AssertWorkerValues(result, new[] { 1f, 2f, 3f });
        }

        [TestMethod]
        public async Task OpenCvWorkerExecutesNamedTensorInferenceWithExplicitContract()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages the Windows OpenCV runtime.");
            var options = new Dictionary<string, string>
            {
                ["outputTensorNames"] = "scores",
                ["outputTensorShapesJson"] = "{\"scores\":[1,3]}",
                ["outputTensorElementTypesJson"] = "{\"scores\":\"float32\"}"
            };
            ModelRunResult result = await RunTensorWorkerAsync("deploysharp.backend.opencv", options);
            AssertWorkerValues(result, new[] { 1f, 2f, 3f });
        }

        private static async Task<ModelRunResult> RunTensorWorkerAsync(string backendId, IReadOnlyDictionary<string, string>? options = null)
        {
            var inputs = new[] { new ModelTensorInput("images", "float32", new long[] { 1, 3, 2, 2 }, valuesJson: "[1,1,1,1,2,2,2,2,3,3,3,3]") };
            var request = new ModelRunRequest(AppOperationKind.Vision, "tests/classification", backendId, modelPath: Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx"), modelFormat: "onnx", tensorInputs: inputs, options: options);
            return await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);
        }

        private static void AssertWorkerValues(ModelRunResult result, float[] expected)
        {
            if (result.ErrorCode == AppErrorCode.NativeDependencyMissing || result.Diagnostics.Any(item => item.Code == "DSAPP-WORKER-ABI-SMOKE-PENDING"))
                Assert.Inconclusive("Native Worker runtime is unavailable: " + result.Message);
            Assert.IsTrue(result.Succeeded, result.Message + Environment.NewLine + string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message + " " + string.Join(", ", item.Details.Select(pair => pair.Key + "=" + pair.Value)))));
            Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
            using JsonDocument output = JsonDocument.Parse(result.Output!);
            float[] actual = output.RootElement[0].GetProperty("values").EnumerateArray().Select(item => item.GetSingle()).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        private sealed class ThrowingEngine : IDeploySharpEngine
        {
            public Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken) => throw new AssertFailedException("Worker request was incorrectly sent to the in-process engine.");
        }

        private sealed class StubWorkerClient : IBackendHostWorkerClient
        {
            public bool RunCalled { get; private set; }
            public Task<WorkerResponse> SendAsync(WorkerRequest request, TimeSpan timeout, CancellationToken cancellationToken) => Task.FromResult(new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "stub"));
            public Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
            {
                RunCalled = true;
                return Task.FromResult(new ModelRunResult(false, AppErrorCode.WorkerRequired, "stub worker", diagnostics: new[] { new RuntimeDiagnostic("DSAPP-TEST-WORKER", DiagnosticSeverity.Information, "stub") }, runMode: ModelRunMode.Worker));
            }
            public Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress, CancellationToken cancellationToken) => Task.FromResult(new BenchmarkReport(request, false, "stub worker"));
        }

        private static string LocateBackendHost()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "src", "DeploySharpApp.BackendHost", "bin", "Debug", "net10.0", "DeploySharpApp.BackendHost.dll");
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            Assert.Fail("The built BackendHost DLL could not be located.");
            return string.Empty;
        }
    }
}
