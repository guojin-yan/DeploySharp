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
    }
}
