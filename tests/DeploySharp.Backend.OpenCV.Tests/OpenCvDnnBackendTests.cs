using System;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvDnnBackendTests
    {
        private static readonly ModelId Model = new ModelId("fixture/opencv-classification");
        private const string Sha256 = "05a885298cca6e04b83732a46ff340f48203cc62e5fa89af74fe3eeab259de2a";

        [TestMethod]
        public void RealOpenCvDnnMatchesPinnedOnnxGolden()
        {
            string path = Fixture();
            ModelArtifact artifact = new ModelArtifact(Model, "onnx", path, Sha256);
            OpenCvDnnModelContract contract = Contract();
            using var openCvProvider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
            using IInferenceSession openCv = openCvProvider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
            InferenceInputs inputs = Inputs();
            float[] expected = { 2.5f, 6.5f, 10.5f };
            float[] actual = (float[])openCv.Run(inputs, CancellationToken.None).Single().Tensor.Buffer;
            Assert.AreEqual(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++) Assert.AreEqual(expected[index], actual[index], 0.00001f, "Output drift at index " + index);
            CollectionAssert.AreEqual(new long[] { 1, 3 }, openCv.Metadata.Outputs.Single().Shape.ToArray());
        }

        [TestMethod]
        public void ContractAndArtifactDriftFailClosed()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new OpenCvDnnModelContract(Model, new[] { new TensorDescriptor("images", TensorElementType.Int64, new TensorShape(1, 3, 2, 2)) }, Contract().Outputs));
            Assert.ThrowsExactly<ArgumentException>(() => new OpenCvDnnModelContract(Model, new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2)) }, Contract().Outputs));
            var bad = new ModelArtifact(Model, "onnx", Fixture(), new string('a', 64));
            Assert.AreEqual(DeploySharpErrorCodes.ModelArtifactInvalid, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => OpenCvDnnModelArtifactValidator.Validate(bad)).ErrorCode);
        }

        [TestMethod]
        public void WrongShapeNameDeviceAndDisposedSessionAreRejected()
        {
            var artifact = new ModelArtifact(Model, "onnx", Fixture(), Sha256);
            using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(Contract()));
            Assert.IsFalse(provider.CanCreate(artifact, new BackendRequest(BackendCapabilities.TensorInference, device: "cuda")));
            Assert.AreEqual(OpenCvDnnErrorCodes.ConfigurationInvalid, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, device: "cuda"), SessionOptions.Default)).ErrorCode);
            var session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference), SessionOptions.Default);
            Assert.AreEqual(OpenCvDnnErrorCodes.TensorInvalid, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => session.Run(new InferenceInputs(new[] { new NamedTensor("wrong", Inputs().Single().Tensor) }), CancellationToken.None)).ErrorCode);
            Assert.AreEqual(OpenCvDnnErrorCodes.TensorInvalid, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => session.Run(new InferenceInputs(new[] { new NamedTensor("images", new Tensor<float>(new TensorShape(1, 1, 2, 2), new float[4])) }), CancellationToken.None)).ErrorCode);
            session.Dispose();
            Assert.AreEqual(OpenCvDnnErrorCodes.ObjectDisposed, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => session.Run(Inputs(), CancellationToken.None)).ErrorCode);
        }

        [TestMethod]
        public void CancellationAndRegistryBoundaryAreStable()
        {
            var artifact = new ModelArtifact(Model, "onnx", Fixture(), Sha256);
            using var registry = new JYPPX.DeploySharp.Registry.BackendRegistry();
            registry.UseOpenCvDnn(new OpenCvDnnOptions(Contract()));
            using IInferenceSession session = registry.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.AreEqual(OpenCvDnnErrorCodes.Cancelled, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => session.Run(Inputs(), cancellation.Token)).ErrorCode);
        }

        private static OpenCvDnnModelContract Contract() => new OpenCvDnnModelContract(Model, new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)) }, new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, 3)) });
        private static InferenceInputs Inputs() => new InferenceInputs(new[] { new NamedTensor("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f })) });
        private static string Fixture() => Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");
    }
}
