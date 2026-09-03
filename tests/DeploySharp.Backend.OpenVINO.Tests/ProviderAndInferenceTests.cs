using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests
{
    [TestClass]
    public sealed class ProviderAndInferenceTests
    {
        [TestMethod]
        public void DescriptorRegistryAndOptionsExposeOnlyVerifiedCapabilities()
        {
            using var provider = new OpenVinoBackendProvider();
            Assert.AreEqual("openvino", provider.Descriptor.Id.Value);
            CollectionAssert.AreEquivalent(new[] { "onnx", "openvino-ir" }, provider.Descriptor.SupportedFormats.ToArray());
            Assert.IsTrue(provider.Descriptor.Supports(BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes));
            Assert.ThrowsExactly<ArgumentException>(() => new OpenVinoOptions(device: "AUTO"));
            Assert.ThrowsExactly<ArgumentException>(() => new OpenVinoOptions(compileProperties: new[] { new KeyValuePair<string, string>("DEVICE_ID", "0") }));
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            using IInferenceSession selected = registry.CreateSession(OpenVinoTestData.OnnxArtifact("classification.onnx"), new BackendRequest(BackendCapabilities.TensorInference, device: "CPU"));
            Assert.AreEqual("onnx", selected.Metadata.Format);
        }

        [TestMethod]
        public void ArtifactValidationCoversHashFormatIrSidecarAndInvalidModel()
        {
            string hash = OpenVinoTestData.Sha256(OpenVinoTestData.Onnx("classification.onnx"));
            using (IInferenceSession session = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("classification.onnx", hash))) Assert.AreEqual("onnx", session.Metadata.Format);
            OpenVinoBackendException mismatch = Assert.ThrowsExactly<OpenVinoBackendException>(() => OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("classification.onnx", new string('0', 64))));
            Assert.AreEqual(OpenVinoErrorCodes.ModelLoadFailed, mismatch.ErrorCode);
            var wrong = new ModelArtifact(new ModelId("tests/wrong-format"), "openvino-ir", OpenVinoTestData.Onnx("classification.onnx"), preferredBackend: OpenVinoTestData.BackendId);
            Assert.AreEqual(OpenVinoErrorCodes.ModelLoadFailed, Assert.ThrowsExactly<OpenVinoBackendException>(() => OpenVinoTestData.Open(wrong)).ErrorCode);
            var invalid = OpenVinoTestData.OnnxArtifact("unsupported-types.onnx");
            Assert.AreEqual(OpenVinoErrorCodes.ElementTypeUnsupported, Assert.ThrowsExactly<OpenVinoBackendException>(() => OpenVinoTestData.Open(invalid)).ErrorCode);
        }

        [TestMethod]
        public async Task RealCpuOnnxAndIrProduceOwnedDeterministicOutputs()
        {
            using IInferenceSession onnx = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("classification.onnx"));
            Assert.AreEqual("images", onnx.Metadata.Inputs[0].Name);
            Assert.AreEqual("scores", onnx.Metadata.Outputs[0].Name);
            InferenceOutputs sync = onnx.Run(OpenVinoTestData.ClassificationInputs(), CancellationToken.None);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, ((Tensor<float>)sync.GetRequired("scores")).ToArray());
            InferenceOutputs asyncResult = await onnx.RunAsync(OpenVinoTestData.ClassificationInputs(), CancellationToken.None);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, ((Tensor<float>)asyncResult.GetRequired("scores")).ToArray());

            using IInferenceSession ir = OpenVinoTestData.Open(OpenVinoTestData.IrArtifact());
            InferenceOutputs irResult = ir.Run(OpenVinoTestData.ClassificationInputs(), CancellationToken.None);
            float[] retained = ((Tensor<float>)irResult.GetRequired("scores")).ToArray();
            ir.Dispose();
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, retained);
        }

        [TestMethod]
        public void DynamicMetadataAndRuntimeShapeArePreserved()
        {
            using IInferenceSession session = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("dynamic-identity.onnx"));
            Assert.IsTrue(session.Metadata.Inputs[0].Shape.IsDynamic);
            var input = InferenceInputs.Create("input", new Tensor<float>(new TensorShape(3, 2), new[] { 1f, 2f, 3f, 4f, 5f, 6f }));
            Tensor<float> output = (Tensor<float>)session.Run(input, CancellationToken.None).GetRequired("output");
            CollectionAssert.AreEqual(new long[] { 3, 2 }, output.Shape.ToArray());
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f, 4f, 5f, 6f }, output.ToArray());
        }

        [TestMethod]
        public void NamedMultiInputNumericTypesRoundTripAndInvalidInputsAreStable()
        {
            using IInferenceSession session = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("numeric-types.onnx"));
            InferenceOutputs outputs = session.Run(OpenVinoTestData.NumericInputs(), CancellationToken.None);
            Assert.AreEqual(11, outputs.Count);
            CollectionAssert.AreEqual(new long[] { -2000, 3000 }, ((Tensor<long>)outputs.GetRequired("int64_out")).ToArray());
            CollectionAssert.AreEqual(new ulong[] { 2000, 3000 }, ((Tensor<ulong>)outputs.GetRequired("uint64_out")).ToArray());
            CollectionAssert.AreEqual(new[] { 1.25d, -2.5d }, ((Tensor<double>)outputs.GetRequired("float64_out")).ToArray());
            OpenVinoBackendException missing = Assert.ThrowsExactly<OpenVinoBackendException>(() => session.Run(InferenceInputs.Create("bool_in", new Tensor<bool>(new TensorShape(2), new[] { true, false })), CancellationToken.None));
            Assert.AreEqual(OpenVinoErrorCodes.TensorInvalid, missing.ErrorCode);
            var wrongShape = InferenceInputs.Create("images", new Tensor<float>(new TensorShape(1, 3, 1, 4), new float[12]));
            using IInferenceSession classification = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("classification.onnx"));
            Assert.AreEqual(OpenVinoErrorCodes.TensorInvalid, Assert.ThrowsExactly<OpenVinoBackendException>(() => classification.Run(wrongShape, CancellationToken.None)).ErrorCode);
        }

        [TestMethod]
        public async Task CancellationConcurrencyAndDisposalKeepSessionReusable()
        {
            using IInferenceSession session = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("classification.onnx"), new SessionOptions(maxConcurrency: 2));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                OpenVinoBackendException error = await Assert.ThrowsExactlyAsync<OpenVinoBackendException>(() => session.RunAsync(OpenVinoTestData.ClassificationInputs(), cancelled.Token));
                Assert.AreEqual(OpenVinoErrorCodes.Cancelled, error.ErrorCode);
            }
            Task<InferenceOutputs> first = session.RunAsync(OpenVinoTestData.ClassificationInputs(), CancellationToken.None);
            Task<InferenceOutputs> second = session.RunAsync(OpenVinoTestData.ClassificationInputs(), CancellationToken.None);
            await Task.WhenAll(first, second);
            Assert.AreEqual(3f, ((Tensor<float>)first.Result.GetRequired("scores")).ToArray()[2]);
            session.Dispose();
            session.Dispose();
            Assert.AreEqual(OpenVinoErrorCodes.ObjectDisposed, Assert.ThrowsExactly<OpenVinoBackendException>(() => session.Run(OpenVinoTestData.ClassificationInputs(), CancellationToken.None)).ErrorCode);
        }

        [TestMethod]
        public async Task IndependentSessionPoolPreservesResultsAcrossConcurrentCalls()
        {
            using IInferenceSession session = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("classification.onnx"), new SessionOptions(maxConcurrency: 2));
            Task<float[]>[] calls = Enumerable.Range(0, 4)
                .Select(_ => Task.Run(() => ((Tensor<float>)session.Run(OpenVinoTestData.ClassificationInputs(), CancellationToken.None).GetRequired("scores")).ToArray()))
                .ToArray();
            float[][] results = await Task.WhenAll(calls);
            foreach (float[] actual in results) CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, actual);
        }

        [TestMethod]
        public async Task AsyncPoolCallsPreserveResultsAcrossIndependentCompiledModels()
        {
            using IInferenceSession session = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("classification.onnx"), new SessionOptions(maxConcurrency: 2));
            Task<InferenceOutputs>[] calls = Enumerable.Range(0, 4).Select(_ => session.RunAsync(OpenVinoTestData.ClassificationInputs(), CancellationToken.None)).ToArray();
            InferenceOutputs[] results = await Task.WhenAll(calls);
            foreach (InferenceOutputs result in results) CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, ((Tensor<float>)result.GetRequired("scores")).ToArray());
        }

        [TestMethod]
        public void UnsupportedDeviceAndDisposedProviderRemainDiagnosable()
        {
            using var provider = new OpenVinoBackendProvider();
            ModelArtifact artifact = OpenVinoTestData.OnnxArtifact("classification.onnx");
            OpenVinoBackendException device = Assert.ThrowsExactly<OpenVinoBackendException>(() => provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenVinoTestData.BackendId, "GPU"), SessionOptions.Default));
            Assert.AreEqual(OpenVinoErrorCodes.ConfigurationInvalid, device.ErrorCode);
            provider.Dispose();
            Assert.AreEqual(OpenVinoErrorCodes.ObjectDisposed, Assert.ThrowsExactly<OpenVinoBackendException>(() => provider.CanCreate(artifact, new BackendRequest(BackendCapabilities.TensorInference))).ErrorCode);
        }
    }
}
