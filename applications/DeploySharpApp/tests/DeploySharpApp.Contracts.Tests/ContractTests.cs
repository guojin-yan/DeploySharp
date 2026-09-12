using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Contracts.Tests
{
    [TestClass]
    public class ContractTests
    {
        [TestMethod]
        public void RequestNormalizesAndValidatesIdentity()
        {
            var request = new ModelRunRequest(AppOperationKind.Vision, "demo/model", "backend.test", "cpu");
            Assert.AreEqual("demo/model", request.ModelId);
            Assert.ThrowsExactly<ArgumentException>(() => new ModelRunRequest(AppOperationKind.Vision, "bad id", "backend.test"));
        }

        [TestMethod]
        public void StatusCopiesDiagnostics()
        {
            var status = new BackendRuntimeStatus("backend.test", AppRuntimeState.MissingNative, "missing", missingItems: new[] { "cuda" });
            Assert.AreEqual("cuda", status.MissingItems[0]);
        }

        [TestMethod]
        public void BenchmarkRejectsZeroIterations()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BenchmarkRequest("demo/model", "backend.test", iterations: 0));
        }

        [TestMethod]
        public void RequestPreservesNamedTensorCompatibilityFields()
        {
            var tensor = new ModelTensorInput("images", "float32", new long[] { 1, 3 }, valuesJson: "[1,2,3]");
            var request = new ModelRunRequest(AppOperationKind.Vision, "demo/model", "backend.test", modelPath: "model.onnx", modelFormat: "ONNX", tensorInputs: new[] { tensor });
            Assert.AreEqual("model.onnx", request.ModelPath);
            Assert.AreEqual("onnx", request.ModelFormat);
            Assert.AreEqual("images", request.TensorInputs[0].Name);
        }

        [TestMethod]
        public void ImageTensorInputCanDeferValuesToTheImagePath()
        {
            var tensor = new ModelTensorInput("images", "float32", new long[] { 1, 3, 224, 224 }, imageInput: true);
            Assert.IsTrue(tensor.ImageInput);
            Assert.IsNull(tensor.ValuesJson);
            Assert.IsNull(tensor.ValuesFilePath);
        }

        [TestMethod]
        public void BenchmarkPreservesNativeExecutionContract()
        {
            var tensor = new ModelTensorInput("images", "float32", new long[] { 1, 3, 2, 2 }, valuesJson: "[1,1,1,1,2,2,2,2,3,3,3,3]");
            var request = new BenchmarkRequest("tests/model", "deploysharp.backend.openvino", 2, 4, "cpu", "model.onnx", "ONNX", "ABC", "image.png", new[] { tensor }, new Dictionary<string, string> { ["performanceHint"] = "Latency" });
            Assert.AreEqual("onnx", request.ModelFormat);
            Assert.AreEqual("model.onnx", request.ModelPath);
            Assert.AreEqual("image.png", request.InputPath);
            Assert.AreEqual("images", request.TensorInputs.Single().Name);
            Assert.AreEqual("Latency", request.Options["performanceHint"]);
        }

        [TestMethod]
        public void BenchmarkCarriesVerifiedModelPackAssetsAndMetadata()
        {
            var asset = new ModelAssetReference("main.onnx", "C:\\models\\main.onnx", "model", "ABC", 42);
            var request = new BenchmarkRequest("tests/modelpack", "deploysharp.backend.onnxruntime", modelPath: asset.FullPath, modelFormat: "onnx", modelAssets: new[] { asset });
            var report = new BenchmarkReport(request, true, "ok", metadata: new Dictionary<string, string> { ["runtimeIdentifier"] = "win-x64" });
            Assert.AreEqual(1, request.ModelAssets.Count);
            Assert.AreEqual("win-x64", report.Metadata["runtimeIdentifier"]);
        }

        [TestMethod]
        public void TensorRtBuildContractCarriesOptionsAndStructuredResult()
        {
            var request = new TensorRtEngineBuildRequest("demo/onnx", "deploysharp.backend.tensorrt", "C:\\models\\demo.onnx", options: new Dictionary<string, string> { ["tensorRtPrecision"] = "fp16" });
            var diagnostic = new RuntimeDiagnostic("DSAPP-TENSORRT-ENGINE-BUILT", DiagnosticSeverity.Information, "built");
            var report = new TensorRtEngineBuildReport(true, AppErrorCode.None, "built", "C:\\models\\demo.engine", "C:\\models\\demo.engine.identity.json", "abc", "built", diagnostics: new[] { diagnostic });
            Assert.AreEqual("fp16", request.Options["tensorRtPrecision"]);
            Assert.AreEqual("built", report.BuildState);
            Assert.AreEqual("C:\\models\\demo.engine", report.EnginePath);
            Assert.AreEqual(1, report.Diagnostics.Count);
        }
    }
}
