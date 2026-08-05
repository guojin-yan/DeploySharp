using System;
using JYPPX.DeploySharp.Core.Tests.Fakes;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.DeploySharp.Core.Tests.Registry
{
    [TestClass]
    public sealed class BackendRegistryTests
    {
        private static readonly ModelId TestModelId = new ModelId("vision/test/detection");

        [TestMethod]
        public void ExplicitBackendIsSelected()
        {
            var first = new FakeBackendProvider("first");
            var second = new FakeBackendProvider("second");
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder()
                .AddBackend(first)
                .AddBackend(second)
                .Build();

            using IInferenceSession session = runtime.CreateSession(
                CreateArtifact("onnx"),
                new BackendRequest(BackendCapabilities.TensorInference, new BackendId("second")));

            Assert.AreEqual(new BackendId("second"), ((FakeInferenceSession)session).BackendId);
            Assert.AreEqual(0, first.CreatedSessionCount);
            Assert.AreEqual(1, second.CreatedSessionCount);
        }

        [TestMethod]
        public void AutomaticSelectionSkipsIncompatibleProvider()
        {
            var unsupported = new FakeBackendProvider("unsupported", "gguf");
            var compatible = new FakeBackendProvider("compatible", "onnx");
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder()
                .AddBackend(unsupported)
                .AddBackend(compatible)
                .Build();

            using IInferenceSession session = runtime.CreateSession(
                CreateArtifact("onnx"),
                new BackendRequest(BackendCapabilities.TensorInference));

            Assert.AreEqual(new BackendId("compatible"), ((FakeInferenceSession)session).BackendId);
        }

        [TestMethod]
        public void MissingExplicitBackendReturnsStableErrorCode()
        {
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder().Build();

            BackendNotFoundException exception = Assert.ThrowsExactly<BackendNotFoundException>(
                () => runtime.CreateSession(
                    CreateArtifact("onnx"),
                    new BackendRequest(
                        BackendCapabilities.TensorInference,
                        new BackendId("missing"))));

            Assert.AreEqual(DeploySharpErrorCodes.BackendNotFound, exception.ErrorCode);
            Assert.AreEqual(TestModelId, exception.ModelId);
        }

        [TestMethod]
        public void CapabilityMismatchReturnsStableErrorCode()
        {
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder()
                .AddBackend(new FakeBackendProvider("tensor"))
                .Build();

            BackendNotCompatibleException exception = Assert.ThrowsExactly<BackendNotCompatibleException>(
                () => runtime.CreateSession(
                    CreateArtifact("onnx"),
                    new BackendRequest(BackendCapabilities.TextGeneration)));

            Assert.AreEqual(DeploySharpErrorCodes.BackendNotCompatible, exception.ErrorCode);
        }

        [TestMethod]
        public void RuntimeOwnsAndDisposesProviders()
        {
            var provider = new FakeBackendProvider("owned");
            DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder()
                .AddBackend(provider)
                .Build();

            runtime.Dispose();

            Assert.IsTrue(provider.IsDisposed);
            Assert.ThrowsExactly<ObjectDisposedException>(() => runtime.GetBackends());
        }

        [TestMethod]
        public void DuplicateBackendIdIsRejected()
        {
            var registry = new BackendRegistry();
            registry.Register(new FakeBackendProvider("duplicate"));

            DeploySharpException exception = Assert.ThrowsExactly<DeploySharpException>(
                () => registry.Register(new FakeBackendProvider("duplicate")));

            Assert.AreEqual(DeploySharpErrorCodes.BackendAlreadyRegistered, exception.ErrorCode);
            registry.Dispose();
        }

        private static ModelArtifact CreateArtifact(string format)
        {
            return new ModelArtifact(TestModelId, format, "model." + format);
        }
    }
}
