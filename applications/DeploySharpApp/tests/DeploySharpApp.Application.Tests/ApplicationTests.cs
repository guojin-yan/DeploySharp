using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DeploySharpApp.Application;
using DeploySharpApp.BackendHost.Protocol;
using DeploySharpApp.Contracts;
using DeploySharpApp.Engine;
using DeploySharpApp.Infrastructure;
using DeploySharpApp.Web;

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
        public async Task WorkerRejectsModelAssetOutsideBundleRoot()
        {
            string modelPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");
            string outsidePath = Path.Combine(Path.GetTempPath(), "deploysharp-outside-" + Guid.NewGuid().ToString("N") + ".txt");
            var request = new ModelRunRequest(
                AppOperationKind.Vision,
                "tests/modelpack-bundle",
                "deploysharp.backend.onnxruntime",
                modelPath: modelPath,
                modelFormat: "onnx",
                modelAssets: new[] { new ModelAssetReference("../outside.txt", outsidePath, "license") });

            ModelRunResult result = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AppErrorCode.ModelUnavailable, result.ErrorCode);
            Assert.IsTrue(result.Diagnostics.Any(item => item.Code == "DSAPP-WORKER-MODEL-ASSET-OUTSIDE-BUNDLE"));
        }

        [TestMethod]
        public async Task GenericModelPackRoutesToWorkerWithoutDedicatedAdapter()
        {
            var worker = new StubWorkerClient();
            var runner = new EngineModelRunner(new ThrowingEngine(), new FakeModelRunner(), worker);
            ModelRunResult result = await runner.RunAsync(new ModelRunRequest(
                AppOperationKind.Vision,
                "custom/modelpack",
                "deploysharp.backend.onnxruntime",
                modelPath: "bundle/main.onnx",
                modelFormat: "onnx",
                modelAssets: new[] { new ModelAssetReference("main.onnx", "bundle/main.onnx", "model") }), null, CancellationToken.None);
            Assert.IsTrue(worker.RunCalled);
            Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
        }

        [TestMethod]
        public async Task WorkerCapabilityAndNativeProbesCoverFiveBackends()
        {
            var client = new BackendHostWorkerClient(LocateBackendHost());
            WorkerResponse capability = await client.SendAsync(new WorkerRequest(WorkerMessageKind.Capability, "capability-test"), TimeSpan.FromSeconds(10), CancellationToken.None);
            Assert.IsTrue(capability.Succeeded);
            string backendList = capability.Payload["backends"];
            StringAssert.Contains(capability.Payload["multimodal"], "blip-caption");
            StringAssert.Contains(capability.Payload["multimodal"], "clip-image-embedding");
            StringAssert.Contains(capability.Payload["probe"], "abi-smoke");
            foreach (string backendId in new[] { "deploysharp.backend.onnxruntime", "deploysharp.backend.llamasharp", "deploysharp.backend.tensorrt", "deploysharp.backend.opencv", "deploysharp.backend.openvino" })
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
        public async Task OnnxRuntimeBenchmarkWithModelRoutesToWorker()
        {
            var worker = new StubWorkerClient();
            var runner = new EngineModelRunner(new ThrowingEngine(), new FakeModelRunner(), worker);
            BenchmarkReport report = await runner.BenchmarkAsync(new BenchmarkRequest("tests/onnx-benchmark", "deploysharp.backend.onnxruntime", modelPath: "model.onnx", modelFormat: "onnx"), null, CancellationToken.None);
            Assert.IsTrue(worker.BenchmarkCalled);
            Assert.IsFalse(report.Available);
        }

        [TestMethod]
        public async Task StreamingMultimodalRequestRoutesToWorkerAndForwardsDelta()
        {
            var worker = new StubWorkerClient();
            var runner = new EngineModelRunner(new ThrowingEngine(), new FakeModelRunner(), worker);
            var deltas = new List<string>();
            ModelRunResult result = await runner.RunStreamingAsync(
                new ModelRunRequest(AppOperationKind.Multimodal, "tests/blip", "deploysharp.backend.onnxruntime", modelPath: "vision.onnx", modelFormat: "onnx", options: new Dictionary<string, string> { ["executionMode"] = "worker" }),
                null,
                new Progress<string>(value => deltas.Add(value)),
                CancellationToken.None);
            Assert.IsTrue(worker.StreamingCalled);
            Assert.IsTrue(result.Succeeded);
            await Task.Delay(25);
            CollectionAssert.Contains(deltas, "stub-delta");
        }

        [TestMethod]
        public async Task MultimodalWorkerRejectsIncompleteBundleWithoutFakeResult()
        {
            string modelPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");
            var request = new ModelRunRequest(AppOperationKind.Multimodal, "tests/blip", "deploysharp.backend.onnxruntime", inputPath: "missing.png", modelPath: modelPath, modelFormat: "onnx", options: new Dictionary<string, string> { ["executionMode"] = "worker", ["multimodalProfile"] = "blip-caption-base" });
            ModelRunResult result = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AppErrorCode.ModelUnavailable, result.ErrorCode);
            Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
            Assert.IsTrue(result.Diagnostics.Any(item => item.Code == "DSAPP-WORKER-MULTIMODAL-BUNDLE-INCOMPLETE"));
        }

        [TestMethod]
        public async Task MultimodalCudaRequestDoesNotFallBackToCpu()
        {
            string modelPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");
            var options = new Dictionary<string, string> { ["executionMode"] = "worker", ["multimodalProfile"] = "blip-caption-base", ["languageDecoderPath"] = modelPath, ["vocabularyPath"] = modelPath };
            var request = new ModelRunRequest(AppOperationKind.Multimodal, "tests/blip", "deploysharp.backend.onnxruntime", device: "cuda", inputPath: modelPath, modelPath: modelPath, modelFormat: "onnx", options: options);
            ModelRunResult result = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
            Assert.IsTrue(result.Diagnostics.Any(item => item.Code == "DSAPP-WORKER-BLIP-DEVICE-UNAVAILABLE"));
        }

        [TestMethod]
        public async Task NativeBenchmarkWorkerRejectsMissingModelWithoutFakeNumbers()
        {
            var client = new BackendHostWorkerClient(LocateBackendHost());
            BenchmarkReport report = await client.BenchmarkAsync(
                new BenchmarkRequest("tests/missing-benchmark", "deploysharp.backend.openvino", warmup: 0, iterations: 1, modelPath: Path.Combine(Path.GetTempPath(), "deploysharp-missing-benchmark.onnx")),
                null,
                CancellationToken.None);
            Assert.IsFalse(report.Available);
            Assert.AreNotEqual(0, report.Diagnostics.Count);
            StringAssert.StartsWith(report.Diagnostics[0].Code, "DSAPP-WORKER-");
            Assert.AreEqual(0, report.P50Ms);
            Assert.AreEqual(0, report.P95Ms);
            Assert.AreEqual(0, report.Throughput);
        }

        [TestMethod]
        public async Task AbiSmokeNeverMarksMissingModelAvailable()
        {
            var client = new BackendHostWorkerClient(LocateBackendHost());
            string missing = Path.Combine(Path.GetTempPath(), "deploysharp-smoke-missing-" + Guid.NewGuid().ToString("N") + ".onnx");
            var payload = new Dictionary<string, string> { ["smokeModelPath"] = missing };
            WorkerResponse response = await client.SendAsync(new WorkerRequest(WorkerMessageKind.Probe, "probe-smoke-missing", "deploysharp.backend.openvino", payload: payload), TimeSpan.FromSeconds(15), CancellationToken.None);
            Assert.IsFalse(response.Succeeded);
            Assert.AreNotEqual(AppRuntimeState.Available.ToString(), response.Payload["state"]);
            Assert.AreNotEqual("DSAPP-WORKER-ABI-SMOKE-PASSED", response.Payload["diagnosticCode"]);
        }

        [TestMethod]
        public async Task OpenCvAndOpenVinoAbiSmokeValidatesRealFixtureWhenAvailable()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages Windows native runtimes.");
            string smokeModel = Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");
            Assert.IsTrue(File.Exists(smokeModel), "The checked-in classification fixture is required for the ABI smoke contract.");
            var client = new BackendHostWorkerClient(LocateBackendHost());
            foreach (string backendId in new[] { "deploysharp.backend.opencv", "deploysharp.backend.openvino" })
            {
                var payload = new Dictionary<string, string> { ["smokeModelPath"] = smokeModel };
                if (backendId.EndsWith("opencv", StringComparison.OrdinalIgnoreCase))
                {
                    payload["smokeInputName"] = "images";
                    payload["smokeInputShape"] = "[1,3,2,2]";
                    payload["smokeOutputName"] = "scores";
                    payload["smokeOutputShape"] = "[1,3]";
                }
                WorkerResponse response = await client.SendAsync(new WorkerRequest(WorkerMessageKind.Probe, "probe-real-smoke-" + backendId.Replace('.', '-'), backendId, payload: payload), TimeSpan.FromSeconds(30), CancellationToken.None);
                if (!response.Succeeded && response.Payload.TryGetValue("diagnosticCode", out string? code) && code is "DSAPP-WORKER-NATIVE-MISSING" or "DSAPP-WORKER-ABI-SMOKE-MISSING-NATIVE" or "DSAPP-WORKER-NATIVE-PREFLIGHT")
                    Assert.Inconclusive(backendId + " native runtime is unavailable: " + response.Message);
                Assert.IsTrue(response.Succeeded, backendId + ": " + response.Message + " [" + string.Join(";", response.Payload.Select(item => item.Key + "=" + item.Value)) + "]");
                Assert.AreEqual("Available", response.Payload["state"], backendId);
                Assert.AreEqual("DSAPP-WORKER-ABI-SMOKE-PASSED", response.Payload["diagnosticCode"], backendId);
                Assert.IsTrue(response.Payload.TryGetValue("probe.abiSmokePath", out string? smokePath) && string.Equals(Path.GetFullPath(smokeModel), smokePath, StringComparison.OrdinalIgnoreCase), backendId);
            }
        }

        [TestMethod]
        public async Task NativeBenchmarkReusesPreparedOpenVinoSession()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages the Windows OpenVINO runtime.");
            var inputs = new[] { new ModelTensorInput("images", "float32", new long[] { 1, 3, 2, 2 }, valuesJson: "[1,1,1,1,2,2,2,2,3,3,3,3]") };
            var request = new BenchmarkRequest("tests/classification-benchmark", "deploysharp.backend.openvino", warmup: 1, iterations: 2, modelPath: Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx"), modelFormat: "onnx", tensorInputs: inputs);
            BenchmarkReport report = await new BackendHostWorkerClient(LocateBackendHost()).BenchmarkAsync(request, null, CancellationToken.None);
            if (!report.Available && report.Diagnostics.Any(item => item.Code == "DSAPP-WORKER-NATIVE-MISSING" || item.Code == "DSAPP-WORKER-NATIVE-PREFLIGHT")) Assert.Inconclusive(report.Message);
            Assert.IsTrue(report.Available, report.Message + Environment.NewLine + string.Join(Environment.NewLine, report.Diagnostics.Select(item => item.Code + ": " + item.Message)));
            Assert.IsTrue(report.P50Ms > 0);
            Assert.IsTrue(report.P95Ms > 0);
            Assert.IsTrue(report.Throughput > 0);
            StringAssert.Contains(report.Message, "reused provider/session");
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
        [DataRow("yolo/v5/detect/n")]
        [DataRow("yolo/v5/segment/s")]
        [DataRow("yolo/v6/detect/s")]
        [DataRow("yolo/v7/detect/base")]
        [DataRow("yolo/v8/classify/s")]
        [DataRow("yolo/v8/detect/n")]
        [DataRow("yolo/v8/segment/n")]
        [DataRow("yolo/v8/pose/s")]
        [DataRow("yolo/v8/obb/s")]
        [DataRow("yolo/v9/detect/s")]
        [DataRow("yolo/v9/segment/c")]
        [DataRow("yolo/v10/detect/n")]
        [DataRow("yolo/v11/detect/n")]
        [DataRow("yolo/v11/segment/s")]
        [DataRow("yolo/v11/pose/s")]
        [DataRow("yolo/v11/obb/s")]
        [DataRow("yolo/v12/detect/n")]
        [DataRow("yolo/v13/detect/n")]
        [DataRow("yolo/v26/detect/n")]
        [DataRow("yolo/v26/segment/s")]
        [DataRow("yolo/v26/pose/s")]
        [DataRow("yolo/v26/obb/s")]
        [DataRow("deim/v2/detect")]
        [DataRow("rf-detr/detect")]
        [DataRow("rf-detr/segment")]
        [DataRow("rt-detr/r50vd-raw-query")]
        [DataRow("rt-detr/r50vd-decoded-vector-ir")]
        [DataRow("rt-detr/r50vd-decoded-vector-onnx")]
        [DataRow("pp-yoloe/plus-crn-l")]
        [DataRow("segmentation/sam-v1-vit-b")]
        [DataRow("paddleocr/ppocrv5/mobile-cls")]
        [DataRow("paddleocr/ppocrv5/mobile-det")]
        [DataRow("paddleocr/ppocrv5/mobile-rec")]
        [DataRow("paddleocr/ppocrv5/server-cls")]
        [DataRow("paddleocr/ppocrv5/server-det")]
        [DataRow("paddleocr/ppocrv5/server-rec")]
        [DataRow("anomalib/padim/mvtec-bottle")]
        [DataRow("bria/rmbg-1.4")]
        [DataRow("bria/rmbg-2.0")]
        public async Task DedicatedReleaseVisualModelsAreRoutedToWorker(string modelId)
        {
            var worker = new StubWorkerClient();
            var runner = new EngineModelRunner(new ThrowingEngine(), new FakeModelRunner(), worker);
            ModelRunResult result = await runner.RunAsync(new ModelRunRequest(AppOperationKind.Vision, modelId, "deploysharp.backend.onnxruntime", modelPath: "model.onnx", modelFormat: "onnx"), null, CancellationToken.None);
            Assert.IsTrue(worker.RunCalled, modelId);
            Assert.AreEqual(ModelRunMode.Worker, result.RunMode, modelId);
        }

        [TestMethod]
        public async Task OpenVinoReleaseVisualRequestUsesDedicatedAdapter()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages the Windows OpenVINO runtime.");
            var request = new ModelRunRequest(
                AppOperationKind.Vision,
                "yolo/v8/detect-n",
                "deploysharp.backend.openvino",
                modelPath: Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx"),
                modelFormat: "onnx",
                options: new Dictionary<string, string> { ["executionMode"] = "worker" });

            ModelRunResult result = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);

            if (result.Diagnostics.Any(item => item.Code is "DSAPP-WORKER-NATIVE-MISSING" or "DSAPP-WORKER-NATIVE-PREFLIGHT")) Assert.Inconclusive(result.Message);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
            Assert.IsTrue(result.Diagnostics.Any(item => item.Code == "DSAPP-VISUAL-INPUT-REQUIRED"), string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public async Task CachedReleaseYoloSegmentationRunsRealWorkerWhenAvailable()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages Windows native runtimes.");
            string visualRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharpApp", "visual-models");
            string imageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharpApp", "test-images");
            string? modelPath = Directory.Exists(visualRoot) ? Directory.EnumerateFiles(visualRoot, "*yolo-v8-segment-n*.onnx", SearchOption.AllDirectories).FirstOrDefault() : null;
            string? imagePath = Directory.Exists(imageRoot) ? Directory.EnumerateFiles(imageRoot, "bus.jpg", SearchOption.AllDirectories).FirstOrDefault() : null;
            if (modelPath is null || imagePath is null) Assert.Inconclusive("The verified yolo/v8/segment/n Release cache and bus.jpg are not installed.");

            var options = new Dictionary<string, string>
            {
                ["executionMode"] = "worker",
                ["visualOpset"] = "12",
                ["visualUpstreamRepository"] = "https://github.com/ultralytics/ultralytics",
                ["visualUpstreamRevision"] = "ef141af4b837e0a1c34ff187ac40ef36af56c135",
                ["visualExporter"] = "Upstream YOLO ONNX export",
                ["visualExporterVersion"] = "8.0.119",
                ["visualLicense"] = "AGPL-3.0-only",
                ["visualAssetPathsJson"] = "{}"
            };
            var request = new ModelRunRequest(AppOperationKind.Vision, "yolo/v8/segment/n", "deploysharp.backend.onnxruntime", inputPath: imagePath, modelPath: modelPath, modelFormat: "onnx", modelSha256: "986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2", options: options, timeout: TimeSpan.FromMinutes(2));

            ModelRunResult result = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Message + Environment.NewLine + string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message + " [" + string.Join(";", item.Details.Select(pair => pair.Key + "=" + pair.Value)) + "]")));
            Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
            using JsonDocument output = JsonDocument.Parse(result.Output!);
            Assert.AreEqual("deploysharp.visual.result.v1", output.RootElement.GetProperty("schema").GetString());
            Assert.AreEqual("segmentation", output.RootElement.GetProperty("kind").GetString());
            Assert.AreEqual(810, output.RootElement.GetProperty("sourceWidth").GetInt32());
            Assert.AreEqual(1080, output.RootElement.GetProperty("sourceHeight").GetInt32());
            JsonElement instances = output.RootElement.GetProperty("instances");
            Assert.IsTrue(instances.GetArrayLength() > 0);
            JsonElement mask = instances[0].GetProperty("mask");
            Assert.IsTrue(mask.GetProperty("sampleWidth").GetInt32() > 0);
            Assert.IsTrue(mask.GetProperty("sampleHeight").GetInt32() > 0);
            Assert.AreEqual(JsonValueKind.String, mask.GetProperty("values").ValueKind, "Byte masks must use System.Text.Json Base64 encoding instead of megabytes of integer tokens.");
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public async Task CachedReleaseRtDetrIrRunsRealOpenVinoWorkerWhenAvailable()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages the Windows OpenVINO runtime.");
            string visualRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharpApp", "visual-models");
            string imageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharpApp", "test-images");
            string? modelPath = Directory.Exists(visualRoot) ? Directory.EnumerateFiles(visualRoot, "rt-detr-r50vd-decoded-vector-ir.model.xml", SearchOption.AllDirectories).FirstOrDefault() : null;
            string? imagePath = Directory.Exists(imageRoot) ? Directory.EnumerateFiles(imageRoot, "bus.jpg", SearchOption.AllDirectories).FirstOrDefault() : null;
            if (modelPath is null || imagePath is null || !File.Exists(Path.ChangeExtension(modelPath, ".bin"))) Assert.Inconclusive("The verified RT-DETR OpenVINO IR Release bundle and bus.jpg are not installed.");

            var options = new Dictionary<string, string>
            {
                ["executionMode"] = "worker", ["visualOpset"] = "16",
                ["visualUpstreamRepository"] = "https://github.com/PaddlePaddle/PaddleDetection",
                ["visualUpstreamRevision"] = "b25522a0f4bde8c80603f3ba5e3472059972e3b5",
                ["visualExporter"] = "OpenVINO-model-conversion", ["visualExporterVersion"] = "local-converter-unverified",
                ["visualLicense"] = "Apache-2.0", ["visualAssetPathsJson"] = "{}"
            };
            var request = new ModelRunRequest(AppOperationKind.Vision, "rt-detr/r50vd-decoded-vector-ir", "deploysharp.backend.openvino", inputPath: imagePath, modelPath: modelPath, modelFormat: "openvino-ir", modelSha256: "9d49703964c07567de7f00bda85bae1760da322e2b0655bfae110f2c222c778d", options: options, timeout: TimeSpan.FromMinutes(2));

            ModelRunResult result = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);

            Assert.IsTrue(result.Succeeded, result.Message + Environment.NewLine + string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message + " [" + string.Join(";", item.Details.Select(pair => pair.Key + "=" + pair.Value)) + "]")));
            Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
            using JsonDocument output = JsonDocument.Parse(result.Output!);
            Assert.AreEqual("deploysharp.visual.result.v1", output.RootElement.GetProperty("schema").GetString());
            Assert.AreEqual("detection", output.RootElement.GetProperty("kind").GetString());
            Assert.AreEqual(810, output.RootElement.GetProperty("sourceWidth").GetInt32());
            Assert.AreEqual(1080, output.RootElement.GetProperty("sourceHeight").GetInt32());
            Assert.IsTrue(output.RootElement.GetProperty("detections").GetArrayLength() > 0);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        [DataRow("paddleocr/ppocrv5/mobile-rec", "ppocrv5-mobile-rec.model.onnx", "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", 7)]
        [DataRow("paddleocr/ppocrv5/server-rec", "ppocrv5-server-rec.model.onnx", "5c4927aa0736ab598025a37b71daae061363642b1848a90a0cb1e02e2ce823d7", 10)]
        public async Task CachedReleasePaddleOcrRecognitionRunsFullDictionaryDecode(string modelId, string modelFile, string modelSha, int opset)
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages Windows native runtimes.");
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharpApp", "verification-assets", "paddleocr-v5");
            string modelPath = Path.Combine(root, modelFile);
            string dictionaryPath = Path.Combine(root, "ppocrv5_dict.txt");
            string imagePath = Path.Combine(root, "ocr-demo_1.jpg");
            if (!File.Exists(modelPath) || !File.Exists(dictionaryPath) || !File.Exists(imagePath)) Assert.Inconclusive("The verified PP-OCRv5 model, dictionary, or ocr-demo_1.jpg is not installed.");
            var options = new Dictionary<string, string>
            {
                ["executionMode"] = "worker", ["visualOpset"] = opset.ToString(CultureInfo.InvariantCulture),
                ["visualUpstreamRepository"] = "https://github.com/PaddlePaddle/PaddleOCR",
                ["visualUpstreamRevision"] = "release-ppocrv5", ["visualExporter"] = "Paddle2ONNX", ["visualExporterVersion"] = "2.0.2rc3",
                ["visualLicense"] = "Apache-2.0", ["paddleDictionarySha256"] = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b",
                ["visualAssetPathsJson"] = JsonSerializer.Serialize(new Dictionary<string, string> { ["labels"] = dictionaryPath, ["vocabulary"] = dictionaryPath })
            };
            var request = new ModelRunRequest(AppOperationKind.Vision, modelId, "deploysharp.backend.onnxruntime", inputPath: imagePath, modelPath: modelPath, modelFormat: "onnx", modelSha256: modelSha, options: options, timeout: TimeSpan.FromMinutes(3));
            Assert.AreEqual(64, request.ModelSha256!.Length);
            ModelRunResult result = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);
            Assert.IsTrue(result.Succeeded, result.Message + Environment.NewLine + string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Code + ": " + item.Message + " [" + string.Join(";", item.Details.Select(pair => pair.Key + "=" + pair.Value)) + "]")));
            using JsonDocument output = JsonDocument.Parse(result.Output!);
            Assert.AreEqual("ocr-recognition", output.RootElement.GetProperty("kind").GetString());
            Assert.IsTrue(output.RootElement.GetProperty("items").GetArrayLength() > 0);
            JsonElement item = output.RootElement.GetProperty("items")[0];
            Assert.AreEqual("release.ppocr", item.GetProperty("characterSetId").GetString());
            Assert.AreEqual("v5", item.GetProperty("characterSetVersion").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.GetProperty("characterSetSha256").GetString()));
            Assert.IsNotNull(item.GetProperty("text").GetString());
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public async Task ConfiguredTensorRtEngineRejectsBadIdentityThenRunsRealGpuInference()
        {
            string? enginePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_TENSORRT_ENGINE");
            string? identityPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_TENSORRT_IDENTITY");
            string? sourceOnnxPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_TENSORRT_ONNX");
            string? imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_TENSORRT_IMAGE");
            string? cudaRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_CUDA_ROOT");
            string? cudnnRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_CUDNN_ROOT");
            string? tensorRtRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_TENSORRT_ROOT");
            if (new[] { enginePath, identityPath, sourceOnnxPath, imagePath, cudaRoot, cudnnRoot, tensorRtRoot }.Any(string.IsNullOrWhiteSpace))
                Assert.Inconclusive("Configure the DEPLOYSHARP_APP_TENSORRT_* and native root variables to run the external TensorRT validation.");

            string badIdentityPath = Path.Combine(Path.GetTempPath(), "deploysharp-app-bad-trt-identity-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                using JsonDocument identity = JsonDocument.Parse(await File.ReadAllTextAsync(identityPath!));
                var badIdentity = identity.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                badIdentity["engineSha256"] = new string('0', 64);
                await File.WriteAllTextAsync(badIdentityPath, JsonSerializer.Serialize(badIdentity));

                var tensorInputs = new[] { new ModelTensorInput("images", "float32", new long[] { 1, 3, 640, 640 }, imageInput: true) };
                var options = new Dictionary<string, string>
                {
                    ["executionMode"] = "worker",
                    ["cudaRoot"] = cudaRoot!, ["cudnnRoot"] = cudnnRoot!, ["tensorRtRoot"] = tensorRtRoot!,
                    ["bridgePath"] = Path.Combine(Path.GetDirectoryName(LocateBackendHost())!, "jyppxtrtbridge.dll"),
                    ["tensorRtApiVersion"] = "10", ["apiVersion"] = "10", ["sourceOnnxPath"] = sourceOnnxPath!,
                    ["imageResizeMode"] = "stretch", ["imageColorOrder"] = "rgb", ["imageScale"] = "0.0039215686"
                };

                options["engineIdentityPath"] = badIdentityPath;
                var invalidRequest = new ModelRunRequest(AppOperationKind.Vision, "external/yolov8s", "deploysharp.backend.tensorrt", device: "cuda", inputPath: imagePath, modelPath: enginePath, modelFormat: "tensorrt-engine", tensorInputs: tensorInputs, options: options, timeout: TimeSpan.FromMinutes(2));
                ModelRunResult invalid = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(invalidRequest, null, CancellationToken.None);
                Assert.IsFalse(invalid.Succeeded);
                Assert.IsTrue(invalid.Diagnostics.Any(item => item.Code == "DSAPP-TENSORRT-ENGINE-SHA256-MISMATCH"));

                options["engineIdentityPath"] = identityPath!;
                var validRequest = new ModelRunRequest(AppOperationKind.Vision, "external/yolov8s", "deploysharp.backend.tensorrt", device: "cuda", inputPath: imagePath, modelPath: enginePath, modelFormat: "tensorrt-engine", tensorInputs: tensorInputs, options: options, timeout: TimeSpan.FromMinutes(2));
                ModelRunResult valid = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(validRequest, null, CancellationToken.None);

                Assert.IsTrue(valid.Succeeded, valid.Message + Environment.NewLine + string.Join(Environment.NewLine, valid.Diagnostics.Select(item => item.Code + ": " + item.Message)));
                Assert.AreEqual(ModelRunMode.Worker, valid.RunMode);
                Assert.IsTrue(valid.InferenceMs > 0);
                using JsonDocument output = JsonDocument.Parse(valid.Output!);
                Assert.AreEqual(JsonValueKind.Array, output.RootElement.ValueKind);
                Assert.IsTrue(output.RootElement.GetArrayLength() > 0);
                Console.WriteLine("TENSORRT_REAL_INFERENCE preprocessMs={0:F2}; inferenceMs={1:F2}; postprocessMs={2:F2}; outputChars={3}", valid.PreprocessMs, valid.InferenceMs, valid.PostprocessMs, valid.Output!.Length);
            }
            finally { if (File.Exists(badIdentityPath)) File.Delete(badIdentityPath); }
        }

        [TestMethod]
        public async Task TensorRtOnnxRequestReachesNativeBuildBoundary()
        {
            string modelPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");
            var inputs = new[] { new ModelTensorInput("images", "float32", new long[] { 1, 3, 2, 2 }, valuesJson: "[1,1,1,1,2,2,2,2,3,3,3,3]") };
            var request = new ModelRunRequest(AppOperationKind.Vision, "tests/tensorrt-onnx-build", "deploysharp.backend.tensorrt", device: "cuda", modelPath: modelPath, modelFormat: "onnx", tensorInputs: inputs, options: new Dictionary<string, string> { ["executionMode"] = "worker" });

            ModelRunResult result = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);

            Assert.IsFalse(result.Diagnostics.Any(item => item.Code == "DSAPP-WORKER-MODEL-FORMAT-INVALID"), "ONNX must reach TensorRT native preflight/build instead of the old engine-only format rejection.");
            if (result.Succeeded)
            {
                Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.Output));
            }
            else
            {
                Assert.IsTrue(result.ErrorCode is AppErrorCode.NativeDependencyMissing or AppErrorCode.BackendUnavailable);
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public async Task ConfiguredTensorRtOnnxBuildsRunsAndReusesEngine()
        {
            string? cudaRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_CUDA_ROOT");
            string? cudnnRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_CUDNN_ROOT");
            string? tensorRtRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_APP_TENSORRT_ROOT");
            if (new[] { cudaRoot, cudnnRoot, tensorRtRoot }.Any(string.IsNullOrWhiteSpace))
                Assert.Inconclusive("Configure DEPLOYSHARP_APP_CUDA_ROOT, DEPLOYSHARP_APP_CUDNN_ROOT and DEPLOYSHARP_APP_TENSORRT_ROOT to run the real ONNX-to-engine test.");

            string modelPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");
            string outputRoot = Path.Combine(Path.GetTempPath(), "deploysharp-app-trt-build-" + Guid.NewGuid().ToString("N"));
            string enginePath = Path.Combine(outputRoot, "classification.engine");
            string identityPath = enginePath + ".identity.json";
            Directory.CreateDirectory(outputRoot);
            try
            {
                var options = new Dictionary<string, string>
                {
                    ["executionMode"] = "worker",
                    ["cudaRoot"] = cudaRoot!, ["cudnnRoot"] = cudnnRoot!, ["tensorRtRoot"] = tensorRtRoot!,
                    ["bridgePath"] = Path.Combine(Path.GetDirectoryName(LocateBackendHost())!, "jyppxtrtbridge.dll"),
                    ["tensorRtApiVersion"] = "10", ["apiVersion"] = "10", ["tensorRtPrecision"] = "runtime-default",
                    ["tensorRtWorkspaceMiB"] = "64", ["tensorRtEngineOutputPath"] = enginePath, ["engineIdentityPath"] = identityPath,
                    ["tensorRtForceRebuild"] = "true"
                };
                var inputs = new[] { new ModelTensorInput("images", "float32", new long[] { 1, 3, 2, 2 }, valuesJson: "[1,1,1,1,2,2,2,2,3,3,3,3]") };
                var firstRequest = new ModelRunRequest(AppOperationKind.Vision, "tests/tensorrt-onnx-build-real", "deploysharp.backend.tensorrt", device: "cuda", modelPath: modelPath, modelFormat: "onnx", tensorInputs: inputs, options: options, timeout: TimeSpan.FromMinutes(5));
                ModelRunResult first = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(firstRequest, null, CancellationToken.None);
                Assert.IsTrue(first.Succeeded, first.Message + Environment.NewLine + string.Join(Environment.NewLine, first.Diagnostics.Select(item => item.Code + ": " + item.Message)));
                Assert.IsTrue(File.Exists(enginePath));
                Assert.IsTrue(File.Exists(identityPath));
                StringAssert.Contains(first.Message, "converted to a device-bound TensorRT engine");

                options["tensorRtForceRebuild"] = "false";
                var secondRequest = new ModelRunRequest(AppOperationKind.Vision, "tests/tensorrt-onnx-build-real", "deploysharp.backend.tensorrt", device: "cuda", modelPath: modelPath, modelFormat: "onnx", tensorInputs: inputs, options: options, timeout: TimeSpan.FromMinutes(5));
                ModelRunResult second = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(secondRequest, null, CancellationToken.None);
                Assert.IsTrue(second.Succeeded, second.Message);
                StringAssert.Contains(second.Message, "reused from the local cache");
                using JsonDocument output = JsonDocument.Parse(second.Output!);
                Assert.AreEqual(3, output.RootElement[0].GetProperty("values").GetArrayLength());
            }
            finally
            {
                if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
            }
        }

        [TestMethod]
        public void VisionRendererUsesOneFittedSourceCoordinateSpace()
        {
            const string output = "{\"schema\":\"deploysharp.visual.result.v1\",\"kind\":\"segmentation\",\"sourceWidth\":100,\"sourceHeight\":200,\"instances\":[{\"label\":\"item\",\"score\":0.9,\"box\":{\"x\":0,\"y\":0,\"width\":100,\"height\":200},\"mask\":{\"width\":100,\"height\":200,\"sampleWidth\":1,\"sampleHeight\":1,\"coordinateSpace\":\"SourceImage\",\"originX\":0,\"originY\":0,\"values\":\"AQ==\"}}]}";

            string dataUrl = VisionResultRenderer.Render("data:image/png;base64,AA==", output, "segmentation");
            string svg = Encoding.UTF8.GetString(Convert.FromBase64String(dataUrl[(dataUrl.IndexOf(',') + 1)..]));

            StringAssert.Contains(svg, "<image href=\"data:image/png;base64,AA==\" x=\"230\" y=\"20\" width=\"180\" height=\"360\"");
            StringAssert.Contains(svg, "<rect x=\"230\" y=\"20\" width=\"180\" height=\"360\" fill=\"none\"");
            StringAssert.Contains(svg, "<rect x=\"230\" y=\"20\" width=\"180.75\" height=\"360.75\" fill=\"#4d7cff\"");
        }

        [TestMethod]
        public void VisionResultSummaryOmitsMaskValues()
        {
            const string output = "{\"schema\":\"deploysharp.visual.result.v1\",\"kind\":\"segmentation\",\"instances\":[{\"mask\":{\"width\":2,\"height\":2,\"values\":\"AQEBAQ==\"}}]}";

            string summary = VisionResultRenderer.FormatForDisplay(output);

            Assert.IsFalse(summary.Contains("AQEBAQ==", StringComparison.Ordinal));
            Assert.IsFalse(summary.Contains("\"values\"", StringComparison.Ordinal));
            StringAssert.Contains(summary, "maskValuesOmitted");
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

        [TestMethod]
        public async Task OpenVinoWorkerPreprocessesRealImageInput()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages the Windows OpenVINO runtime.");
            string imagePath = Path.Combine(Path.GetTempPath(), "deploysharp-worker-image-" + Guid.NewGuid().ToString("N") + ".png");
            await File.WriteAllBytesAsync(imagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            try
            {
                var inputs = new[] { new ModelTensorInput("images", "float32", new long[] { 1, 3, 2, 2 }, imageInput: true) };
                var options = new Dictionary<string, string> { ["imageResizeMode"] = "stretch", ["imageColorOrder"] = "rgb", ["imageScale"] = "0.0039215686" };
                var request = new ModelRunRequest(AppOperationKind.Vision, "tests/classification-image", "deploysharp.backend.openvino", inputPath: imagePath, modelPath: Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx"), modelFormat: "onnx", tensorInputs: inputs, options: options);
                ModelRunResult result = await new BackendHostWorkerClient(LocateBackendHost()).RunAsync(request, null, CancellationToken.None);
                if (result.ErrorCode == AppErrorCode.NativeDependencyMissing) Assert.Inconclusive(result.Message);
                Assert.IsTrue(result.Succeeded, result.Message);
                Assert.AreEqual(ModelRunMode.Worker, result.RunMode);
                Assert.IsTrue(result.PreprocessMs > 0);
                using JsonDocument output = JsonDocument.Parse(result.Output!);
                Assert.AreEqual(3, output.RootElement[0].GetProperty("values").GetArrayLength());
            }
            finally { File.Delete(imagePath); }
        }

        [TestMethod]
        public async Task WorkerCancelTargetsActiveInferenceOperation()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages the Windows OpenVINO runtime.");
            string imagePath = Path.Combine(Path.GetTempPath(), "deploysharp-worker-cancel-" + Guid.NewGuid().ToString("N") + ".png");
            await File.WriteAllBytesAsync(imagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            using Process process = StartBackendHost();
            try
            {
                await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, new WorkerRequest(WorkerMessageKind.Handshake, "cancel-handshake", payload: new Dictionary<string, string> { ["protocolVersion"] = WorkerProtocol.ProtocolVersion.ToString() }));
                WorkerResponse handshake = await ReadResponseAsync(process, "cancel-handshake", TimeSpan.FromSeconds(10));
                Assert.IsTrue(handshake.Succeeded);

                var tensor = new[] { new ModelTensorInput("images", "float32", new long[] { 1, 3, 2048, 2048 }, imageInput: true) };
                var payload = new Dictionary<string, string>
                {
                    ["modelPath"] = Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx"),
                    ["modelFormat"] = "onnx",
                    ["inputPath"] = imagePath,
                    ["device"] = "cpu",
                    ["timeoutMs"] = "30000",
                    ["tensorInputsJson"] = JsonSerializer.Serialize(tensor)
                };
                await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, new WorkerRequest(WorkerMessageKind.Inference, "active-inference", "deploysharp.backend.openvino", "tests/cancel", payload));
                WorkerResponse accepted = await ReadResponseAsync(process, "active-inference", TimeSpan.FromSeconds(10), WorkerResponseKind.Progress);
                Assert.AreEqual("dispatch", accepted.Payload["stage"]);

                await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, new WorkerRequest(WorkerMessageKind.Cancel, "cancel-active"));
                WorkerResponse cancelled = await ReadResponseAsync(process, "cancel-active", TimeSpan.FromSeconds(10));
                Assert.IsTrue(cancelled.Succeeded);
                Assert.AreEqual("DSAPP-WORKER-CANCEL-REQUESTED", cancelled.Payload["diagnosticCode"]);
                Assert.AreEqual("active-inference", cancelled.Payload["activeRequestId"]);
            }
            finally
            {
                try { await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, new WorkerRequest(WorkerMessageKind.Shutdown, "cancel-shutdown")); } catch (Exception) { }
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                File.Delete(imagePath);
            }
        }

        [TestMethod]
        public async Task WorkerTimeoutReturnsTimedOutDiagnostic()
        {
            if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The application Worker currently packages the Windows OpenVINO runtime.");
            using Process process = StartBackendHost();
            try
            {
                await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, new WorkerRequest(WorkerMessageKind.Handshake, "timeout-handshake", payload: new Dictionary<string, string> { ["protocolVersion"] = WorkerProtocol.ProtocolVersion.ToString() }));
                WorkerResponse handshake = await ReadResponseAsync(process, "timeout-handshake", TimeSpan.FromSeconds(10));
                Assert.IsTrue(handshake.Succeeded);

                var tensor = new[] { new ModelTensorInput("images", "float32", new long[] { 1, 3, 2, 2 }, valuesJson: "[1,1,1,1,2,2,2,2,3,3,3,3]") };
                var payload = new Dictionary<string, string>
                {
                    ["modelPath"] = Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx"),
                    ["modelFormat"] = "onnx",
                    ["device"] = "cpu",
                    ["timeoutMs"] = "1",
                    ["tensorInputsJson"] = JsonSerializer.Serialize(tensor)
                };
                await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, new WorkerRequest(WorkerMessageKind.Inference, "timeout-inference", "deploysharp.backend.openvino", "tests/timeout", payload));
                WorkerResponse response = await ReadResponseAsync(process, "timeout-inference", TimeSpan.FromSeconds(10), WorkerResponseKind.Error);
                if (response.Payload.TryGetValue("diagnosticCode", out string? code) && code == "DSAPP-WORKER-NATIVE-PREFLIGHT")
                    Assert.Inconclusive("Native Worker runtime is unavailable: " + response.Message);
                Assert.AreEqual("DSAPP-WORKER-TIMED-OUT", response.Payload["diagnosticCode"]);
                Assert.AreEqual(AppRuntimeState.Unavailable.ToString(), response.Payload["state"]);
            }
            finally
            {
                try { await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, new WorkerRequest(WorkerMessageKind.Shutdown, "timeout-shutdown")); } catch (Exception) { }
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            }
        }

        private static Process StartBackendHost()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", "\"" + LocateBackendHost() + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            Assert.IsTrue(process.Start());
            return process;
        }

        private static async Task<WorkerResponse> ReadResponseAsync(Process process, string requestId, TimeSpan timeout, WorkerResponseKind? kind = null)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            while (true)
            {
                string? line = await process.StandardOutput.ReadLineAsync(cancellation.Token);
                if (line == null) throw new EndOfStreamException("BackendHost closed stdout before returning " + requestId + ".");
                WorkerResponse response;
                try { response = WorkerProtocol.DeserializeResponse(line); }
                catch (FormatException) { continue; }
                if (string.Equals(response.RequestId, requestId, StringComparison.Ordinal) && (!kind.HasValue || response.Kind == kind.Value)) return response;
            }
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
            public bool StreamingCalled { get; private set; }
            public bool BenchmarkCalled { get; private set; }
            public Task<WorkerResponse> SendAsync(WorkerRequest request, TimeSpan timeout, CancellationToken cancellationToken) => Task.FromResult(new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "stub"));
            public Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
            {
                RunCalled = true;
                return Task.FromResult(new ModelRunResult(false, AppErrorCode.WorkerRequired, "stub worker", diagnostics: new[] { new RuntimeDiagnostic("DSAPP-TEST-WORKER", DiagnosticSeverity.Information, "stub") }, runMode: ModelRunMode.Worker));
            }
            public Task<ModelRunResult> RunStreamingAsync(ModelRunRequest request, IProgress<double>? progress, IProgress<string>? textProgress, CancellationToken cancellationToken)
            {
                RunCalled = true;
                StreamingCalled = true;
                textProgress?.Report("stub-delta");
                return Task.FromResult(new ModelRunResult(true, AppErrorCode.None, "stub worker", "stub-delta", runMode: ModelRunMode.Worker));
            }
            public Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
            {
                BenchmarkCalled = true;
                return Task.FromResult(new BenchmarkReport(request, false, "stub worker"));
            }
        }

        private static string LocateBackendHost()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string ridCandidate = Path.Combine(directory.FullName, "src", "DeploySharpApp.BackendHost", "bin", "Debug", "net10.0", "win-x64", "DeploySharpApp.BackendHost.dll");
                if (File.Exists(ridCandidate)) return ridCandidate;
                string candidate = Path.Combine(directory.FullName, "src", "DeploySharpApp.BackendHost", "bin", "Debug", "net10.0", "DeploySharpApp.BackendHost.dll");
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            Assert.Fail("The built BackendHost DLL could not be located.");
            return string.Empty;
        }
    }
}
