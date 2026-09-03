using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Core.Tests.Fakes;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
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

            Assert.AreEqual(0, first.CreatedSessionCount);
            Assert.AreEqual(1, second.CreatedSessionCount);
            Assert.AreEqual(new BackendId("second"), second.CreatedSessions[0].BackendId);
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

            Assert.AreEqual(new BackendId("compatible"), compatible.CreatedSessions[0].BackendId);
        }

        [TestMethod]
        public async Task ConcurrencyCreatesIndependentSingleChannelSessionsAndUsesEveryIdleInstance()
        {
            var provider = new FakeBackendProvider("pooled") { RunDelay = TimeSpan.FromMilliseconds(50) };
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder().AddBackend(provider).Build();
            using IInferenceSession session = runtime.CreateSession(CreateArtifact("onnx"), new BackendRequest(BackendCapabilities.TensorInference), new SessionOptions(2));
            var tensor = new Tensor<float>(new TensorShape(1), new[] { 1f });
            var inputs = InferenceInputs.Create("input", tensor);

            await Task.WhenAll(session.RunAsync(inputs, CancellationToken.None), session.RunAsync(inputs, CancellationToken.None));

            Assert.AreEqual(2, provider.CreatedSessionCount);
            Assert.IsTrue(provider.CreatedSessionOptions.All(value => value.MaxConcurrency == 1));
            CollectionAssert.AreEquivalent(new[] { 1, 1 }, provider.CreatedSessions.Select(value => value.RunCount).ToArray());
            session.Dispose();
            Assert.IsTrue(provider.CreatedSessions.All(value => value.IsDisposed));
        }

        [TestMethod]
        public void PreCancelledPooledRunDoesNotLeaseOrEnterBackend()
        {
            var provider = new FakeBackendProvider("pre-cancelled");
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder().AddBackend(provider).Build();
            using IInferenceSession session = runtime.CreateSession(CreateArtifact("onnx"), new BackendRequest(BackendCapabilities.TensorInference), new SessionOptions(2));
            var tensor = new Tensor<float>(new TensorShape(1), new[] { 1f });
            var inputs = InferenceInputs.Create("input", tensor);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsExactly<OperationCanceledException>(() => session.Run(inputs, cancellation.Token));
            Assert.AreEqual(0, provider.CreatedSessions.Sum(value => value.RunCount));
        }

        [TestMethod]
        public async Task SingleSessionPoolQueuesConcurrentCalls()
        {
            var provider = new FakeBackendProvider("single") { RunDelay = TimeSpan.FromMilliseconds(50) };
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder().AddBackend(provider).Build();
            using IInferenceSession session = runtime.CreateSession(CreateArtifact("onnx"), new BackendRequest(BackendCapabilities.TensorInference), new SessionOptions(1));
            var tensor = new Tensor<float>(new TensorShape(1), new[] { 1f });
            var inputs = InferenceInputs.Create("input", tensor);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            await Task.WhenAll(session.RunAsync(inputs, CancellationToken.None), session.RunAsync(inputs, CancellationToken.None));

            watch.Stop();
            Assert.AreEqual(1, provider.CreatedSessionCount);
            Assert.IsTrue(watch.Elapsed >= TimeSpan.FromMilliseconds(80), "A one-session pool must serialize concurrent calls.");
        }

        [TestMethod]
        public async Task IndependentSessionsParallelizeDocumentedSynchronousAsyncFallback()
        {
            var provider = new FakeBackendProvider("sync-fallback") { RunDelay = TimeSpan.FromMilliseconds(80), SynchronousAsyncFallback = true };
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder().AddBackend(provider).Build();
            using IInferenceSession session = runtime.CreateSession(CreateArtifact("onnx"), new BackendRequest(BackendCapabilities.TensorInference), new SessionOptions(2));
            var inputs = InferenceInputs.Create("input", new Tensor<float>(new TensorShape(1), new[] { 1f }));
            var watch = System.Diagnostics.Stopwatch.StartNew();

            await Task.WhenAll(session.RunAsync(inputs, CancellationToken.None), session.RunAsync(inputs, CancellationToken.None));

            watch.Stop();
            Assert.IsTrue(watch.Elapsed < TimeSpan.FromMilliseconds(150), "Independent sessions should not serialize a synchronous async fallback.");
            CollectionAssert.AreEquivalent(new[] { 1, 1 }, provider.CreatedSessions.Select(value => value.RunCount).ToArray());
        }

        [TestMethod]
        public async Task GenericBatchSchedulerSplitsDispatchesAndRestoresInputOrder()
        {
            var provider = new FakeBackendProvider("batch-scheduler") { RunDelay = TimeSpan.FromMilliseconds(25) };
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder().AddBackend(provider).Build();
            using IInferenceSession session = runtime.CreateSession(CreateArtifact("onnx"), new BackendRequest(BackendCapabilities.TensorInference), new SessionOptions(2));
            var scheduler = new InferenceBatchScheduler<int, int>(session, 2,
                batch => InferenceInputs.Create("input", new Tensor<float>(new TensorShape(batch.Count), batch.Select(value => (float)value).ToArray())),
                (outputs, count) => ((float[])outputs.GetRequired("output").Buffer).Take(count).Select(value => (int)value).ToArray());

            IReadOnlyList<int> results = await scheduler.RunAsync(new[] { 5, 4, 3, 2, 1 });

            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, results.ToArray());
            Assert.AreEqual(3, provider.CreatedSessions.Sum(value => value.RunCount));
            Assert.IsTrue(provider.CreatedSessions.All(value => value.RunCount > 0));
        }

        [TestMethod]
        public async Task GenericBatchSchedulerBoundsPreparedInputsToSessionPoolCapacity()
        {
            var provider = new FakeBackendProvider("batch-scheduler-bounded") { RunDelay = TimeSpan.FromMilliseconds(250) };
            using DeploySharpRuntime runtime = DeploySharpRuntime.CreateBuilder().AddBackend(provider).Build();
            using IInferenceSession session = runtime.CreateSession(CreateArtifact("onnx"), new BackendRequest(BackendCapabilities.TensorInference), new SessionOptions(2));
            int prepared = 0;
            var scheduler = new InferenceBatchScheduler<int, int>(session, 1,
                batch =>
                {
                    Interlocked.Increment(ref prepared);
                    return InferenceInputs.Create("input", new Tensor<float>(new TensorShape(batch.Count), batch.Select(value => (float)value).ToArray()));
                },
                (outputs, count) => ((float[])outputs.GetRequired("output").Buffer).Take(count).Select(value => (int)value).ToArray());

            Task<IReadOnlyList<int>> running = scheduler.RunAsync(new[] { 5, 4, 3, 2, 1, 0 });
            await Task.Delay(50);

            Assert.AreEqual(2, Volatile.Read(ref prepared), "Only batches with a leased session should retain prepared tensors.");
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1, 0 }, (await running).ToArray());
            Assert.AreEqual(6, Volatile.Read(ref prepared));
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
