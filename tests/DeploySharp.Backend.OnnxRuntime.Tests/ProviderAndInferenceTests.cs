using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OnnxRuntime.Internal;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    [TestClass]
    public sealed class ProviderAndInferenceTests
    {
        [TestMethod]
        public void DescriptorAndRegistryExposeOnlyVerifiedCpuOnnxCapabilities()
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            BackendDescriptor descriptor = registry.GetDescriptors().Single();
            Assert.AreEqual("onnxruntime", descriptor.Id.Value);
            Assert.AreEqual("1.28.0", descriptor.Version);
            Assert.IsTrue(descriptor.Supports(BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes));
            CollectionAssert.AreEqual(new[] { "onnx" }, descriptor.SupportedFormats.ToArray());

            using var provider = new OnnxRuntimeBackendProvider();
            Assert.IsTrue(provider.CanCreate(OnnxRuntimeTestData.Artifact("classification.onnx"), new BackendRequest(BackendCapabilities.TensorInference, device: "cpu")));
            Assert.IsFalse(provider.CanCreate(OnnxRuntimeTestData.Artifact("classification.onnx"), new BackendRequest(BackendCapabilities.TensorInference, device: "cuda")));
        }

        [TestMethod]
        public void ArtifactValidatorRejectsExtensionMissingTruncatedAndHashMismatch()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-ort-validation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string wrongExtension = Path.Combine(root, "model.bin");
                File.WriteAllBytes(wrongExtension, new byte[16]);
                Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => OnnxModelArtifactValidator.Validate(new ModelArtifact(new ModelId("tests/wrong-extension"), "onnx", wrongExtension)));
                Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => OnnxModelArtifactValidator.Validate(new ModelArtifact(new ModelId("tests/missing"), "onnx", Path.Combine(root, "missing.onnx"))));
                string truncated = Path.Combine(root, "truncated.onnx");
                File.WriteAllBytes(truncated, new byte[3]);
                Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => OnnxModelArtifactValidator.Validate(new ModelArtifact(new ModelId("tests/truncated"), "onnx", truncated)));
                OnnxRuntimeBackendException hash = Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => OnnxModelArtifactValidator.Validate(OnnxRuntimeTestData.Artifact("classification.onnx", new string('0', 64))));
                Assert.AreEqual(DeploySharpErrorCodes.ModelArtifactInvalid, hash.ErrorCode);
            }
            finally { Directory.Delete(root, true); }
        }

        [TestMethod]
        public void InvalidOnnxMapsLoadFailureAndProviderRemainsUsable()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-invalid-" + Guid.NewGuid().ToString("N") + ".onnx");
            File.WriteAllBytes(path, new byte[16]);
            try
            {
                using var provider = new OnnxRuntimeBackendProvider();
                var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeTestData.BackendId, "cpu");
                OnnxRuntimeBackendException error = Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => provider.CreateSession(new ModelArtifact(new ModelId("tests/invalid-onnx"), "onnx", path), request, SessionOptions.Default));
                Assert.AreEqual(OnnxRuntimeErrorCodes.ModelLoadFailed, error.ErrorCode);
                Assert.IsNotNull(error.InnerException);
                using IInferenceSession valid = provider.CreateSession(OnnxRuntimeTestData.Artifact("classification.onnx"), request, SessionOptions.Default);
                Assert.AreEqual(3f, ((float[])valid.Run(OnnxRuntimeTestData.ClassificationInputs(), CancellationToken.None).GetRequired("scores").Buffer)[2]);
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public async Task StaticModelRunsSynchronouslyAndThroughNativeAsyncWithOwnedOutputs()
        {
            InferenceOutputs synchronous;
            InferenceOutputs asynchronous;
            using (IInferenceSession session = OnnxRuntimeTestData.Open("classification.onnx"))
            {
                Assert.AreEqual("scores", session.Metadata.Outputs.Single().Name);
                synchronous = session.Run(OnnxRuntimeTestData.ClassificationInputs(), CancellationToken.None);
                asynchronous = await session.RunAsync(OnnxRuntimeTestData.ClassificationInputs(), CancellationToken.None);
            }
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, (float[])synchronous.GetRequired("scores").Buffer);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, (float[])asynchronous.GetRequired("scores").Buffer);
        }

        [TestMethod]
        public async Task MultipleConcurrencyCreatesIndependentOrtSessionsWithStableResults()
        {
            using IInferenceSession session = OnnxRuntimeTestData.Open("classification.onnx", new SessionOptions(maxConcurrency: 2));
            Task<float[]>[] calls = Enumerable.Range(0, 4).Select(_ => Task.Run(() => (float[])session.Run(OnnxRuntimeTestData.ClassificationInputs(), CancellationToken.None).GetRequired("scores").Buffer)).ToArray();
            float[][] results = await Task.WhenAll(calls);
            foreach (float[] actual in results) CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, actual);
        }

        [TestMethod]
        public async Task AsyncPoolCallsPreserveResultsAcrossIndependentOrtSessions()
        {
            using IInferenceSession session = OnnxRuntimeTestData.Open("classification.onnx", new SessionOptions(maxConcurrency: 2));
            Task<InferenceOutputs>[] calls = Enumerable.Range(0, 4).Select(_ => session.RunAsync(OnnxRuntimeTestData.ClassificationInputs(), CancellationToken.None)).ToArray();
            InferenceOutputs[] results = await Task.WhenAll(calls);
            foreach (InferenceOutputs result in results) CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, (float[])result.GetRequired("scores").Buffer);
        }

        [TestMethod]
        public async Task DynamicMetadataMapsToMinusOneAndAsyncFallbackReturnsRuntimeShape()
        {
            using IInferenceSession session = OnnxRuntimeTestData.Open("dynamic-identity.onnx");
            Assert.AreEqual(-1L, session.Metadata.Inputs.Single().Shape[0]);
            var input = new Tensor<float>(new TensorShape(3, 2), new[] { 1f, 2f, 3f, 4f, 5f, 6f });
            InferenceOutputs result = await session.RunAsync(InferenceInputs.Create("input", input), CancellationToken.None);
            Assert.AreEqual(new TensorShape(3, 2), result.GetRequired("output").Shape);
            CollectionAssert.AreEqual(input.ToArray(), (float[])result.GetRequired("output").Buffer);
        }

        [TestMethod]
        public void NamedMultiInputOutputRoundTripsAllStableNumericTypes()
        {
            using IInferenceSession session = OnnxRuntimeTestData.Open("numeric-types.onnx");
            Assert.AreEqual(11, session.Metadata.Inputs.Count);
            Assert.AreEqual(11, session.Metadata.Outputs.Count);
            InferenceOutputs outputs = session.Run(OnnxRuntimeTestData.NumericInputs(), CancellationToken.None);
            CollectionAssert.AreEqual(new[] { true, false }, (bool[])outputs.GetRequired("bool_out").Buffer);
            CollectionAssert.AreEqual(new sbyte[] { -2, 3 }, (sbyte[])outputs.GetRequired("int8_out").Buffer);
            CollectionAssert.AreEqual(new byte[] { 2, 3 }, (byte[])outputs.GetRequired("uint8_out").Buffer);
            CollectionAssert.AreEqual(new short[] { -20, 30 }, (short[])outputs.GetRequired("int16_out").Buffer);
            CollectionAssert.AreEqual(new ushort[] { 20, 30 }, (ushort[])outputs.GetRequired("uint16_out").Buffer);
            CollectionAssert.AreEqual(new[] { -200, 300 }, (int[])outputs.GetRequired("int32_out").Buffer);
            CollectionAssert.AreEqual(new uint[] { 200, 300 }, (uint[])outputs.GetRequired("uint32_out").Buffer);
            CollectionAssert.AreEqual(new long[] { -2000, 3000 }, (long[])outputs.GetRequired("int64_out").Buffer);
            CollectionAssert.AreEqual(new ulong[] { 2000, 3000 }, (ulong[])outputs.GetRequired("uint64_out").Buffer);
            CollectionAssert.AreEqual(new[] { 1.25f, -2.5f }, (float[])outputs.GetRequired("float32_out").Buffer);
            CollectionAssert.AreEqual(new[] { 1.25d, -2.5d }, (double[])outputs.GetRequired("float64_out").Buffer);
        }

        [TestMethod]
        public void MissingExtraShapeTypeAndUnsupportedStringHaveStableDiagnosticsAndAllowReuse()
        {
            using IInferenceSession session = OnnxRuntimeTestData.Open("classification.onnx");
            Assert.AreEqual(OnnxRuntimeErrorCodes.TensorInvalid, Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => session.Run(new InferenceInputs(Array.Empty<NamedTensor>()), CancellationToken.None)).ErrorCode);
            var extra = new InferenceInputs(new[]
            {
                new NamedTensor("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12])),
                new NamedTensor("extra", new Tensor<float>(new TensorShape(1), new float[1]))
            });
            Assert.AreEqual(OnnxRuntimeErrorCodes.TensorInvalid, Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => session.Run(extra, CancellationToken.None)).ErrorCode);
            Assert.AreEqual(OnnxRuntimeErrorCodes.TensorInvalid, Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => session.Run(InferenceInputs.Create("images", new Tensor<float>(new TensorShape(1, 3, 1, 4), new float[12])), CancellationToken.None)).ErrorCode);
            Assert.AreEqual(3f, ((float[])session.Run(OnnxRuntimeTestData.ClassificationInputs(), CancellationToken.None).GetRequired("scores").Buffer)[2]);

            using IInferenceSession strings = OnnxRuntimeTestData.Open("unsupported-types.onnx");
            OnnxRuntimeBackendException unsupported = Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => strings.Run(InferenceInputs.Create("string_in", new Tensor<string>(new TensorShape(1), new[] { "value" })), CancellationToken.None));
            Assert.AreEqual(OnnxRuntimeErrorCodes.ElementTypeUnsupported, unsupported.ErrorCode);
            Assert.AreEqual("string_in", unsupported.TensorName);
        }

        [TestMethod]
        public void ConfigurationAndCpuDeviceAreValidatedWithoutAdvertisingOtherProviders()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OnnxRuntimeOptions(intraOpThreads: -1));
            using var provider = new OnnxRuntimeBackendProvider();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeTestData.BackendId, "cuda");
            OnnxRuntimeBackendException error = Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => provider.CreateSession(OnnxRuntimeTestData.Artifact("classification.onnx"), request, SessionOptions.Default));
            Assert.AreEqual(OnnxRuntimeErrorCodes.ConfigurationInvalid, error.ErrorCode);
        }

        [TestMethod]
        public void CudaExecutionProviderIsExplicitAndDeviceScoped()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OnnxRuntimeOptions(cudaDeviceId: -1));
            using var cudaProvider = new OnnxRuntimeBackendProvider(new OnnxRuntimeOptions(executionProvider: OnnxRuntimeExecutionProvider.Cuda));
            var artifact = OnnxRuntimeTestData.Artifact("classification.onnx");
            Assert.IsTrue(cudaProvider.CanCreate(artifact, new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cuda")));
            Assert.IsFalse(cudaProvider.CanCreate(artifact, new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu")));
        }

        [TestMethod]
        public void CudaInitializationFailureMapsToExecutionProviderUnavailable()
        {
            Exception mapped = OnnxRuntimeExceptionMapper.Map(
                new InvalidOperationException("CUDA failure 801 in cuda_execution_provider.cc while calling cudaSetDevice"),
                OnnxRuntimeTestData.Artifact("classification.onnx"),
                "load");
            var error = (OnnxRuntimeBackendException)mapped;
            Assert.AreEqual(OnnxRuntimeErrorCodes.ExecutionProviderUnavailable, error.ErrorCode);
        }
    }
}
