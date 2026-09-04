using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DeploySharpApp.Application;
using DeploySharpApp.Contracts;
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
    }
}
