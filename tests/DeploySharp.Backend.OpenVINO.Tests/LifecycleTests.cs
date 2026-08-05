using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests
{
    [TestClass]
    public sealed class LifecycleTests
    {
        [TestMethod]
        [Timeout(15000)]
        public async Task ActiveNativeRunCanBeCancelledAndSameSessionReused()
        {
            using IInferenceSession session = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("cancellable-loop.onnx"));
            using var source = new CancellationTokenSource();
            source.CancelAfter(10);

            OpenVinoBackendException cancelled = await Assert.ThrowsExactlyAsync<OpenVinoBackendException>(
                () => session.RunAsync(OpenVinoTestData.LongRunningInputs(), source.Token));
            Assert.AreEqual(OpenVinoErrorCodes.Cancelled, cancelled.ErrorCode, cancelled.Message + " | " + cancelled.TechnicalDetails);

            InferenceOutputs reused = session.Run(OpenVinoTestData.LongRunningInputs(), CancellationToken.None);
            Assert.AreEqual(128 * 128, reused.GetRequired("output").Length);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task MaxConcurrencyQueuesCallsAndQueuedCancellationDoesNotPoisonSession()
        {
            using IInferenceSession session = OpenVinoTestData.Open(
                OpenVinoTestData.OnnxArtifact("cancellable-loop.onnx"),
                new SessionOptions(maxConcurrency: 1));
            using var queuedCancellation = new CancellationTokenSource();
            using var activeCancellation = new CancellationTokenSource();

            Task<InferenceOutputs> active = session.RunAsync(OpenVinoTestData.LongRunningInputs(), activeCancellation.Token);
            queuedCancellation.Cancel();
            Task<InferenceOutputs> queued = session.RunAsync(OpenVinoTestData.LongRunningInputs(), queuedCancellation.Token);

            OpenVinoBackendException queuedError = await Assert.ThrowsExactlyAsync<OpenVinoBackendException>(() => queued);
            Assert.AreEqual(OpenVinoErrorCodes.Cancelled, queuedError.ErrorCode);
            activeCancellation.Cancel();
            await AssertCompletedOrCancelled(active);

            Assert.AreEqual(128 * 128, session.Run(OpenVinoTestData.LongRunningInputs(), CancellationToken.None).GetRequired("output").Length);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task DisposeCancelsActiveAsyncRunWaitsForUnwindAndIsIdempotent()
        {
            IInferenceSession session = OpenVinoTestData.Open(OpenVinoTestData.OnnxArtifact("cancellable-loop.onnx"));
            Task<InferenceOutputs> active = session.RunAsync(OpenVinoTestData.LongRunningInputs(), CancellationToken.None);
            await Task.Delay(10);

            Task dispose = Task.Run(() => session.Dispose());
            await AssertCompletedOrCancelled(active);
            await dispose;

            session.Dispose();
            OpenVinoBackendException disposed = Assert.ThrowsExactly<OpenVinoBackendException>(
                () => session.Run(OpenVinoTestData.LongRunningInputs(), CancellationToken.None));
            Assert.AreEqual(OpenVinoErrorCodes.ObjectDisposed, disposed.ErrorCode);
        }

        private static async Task AssertCompletedOrCancelled(Task<InferenceOutputs> operation)
        {
            // Cancellation cannot retroactively fail an inference that completed before the signal was observed.
            // 取消不能追溯性地让已在信号观察前完成的推理失败。
            try
            {
                InferenceOutputs outputs = await operation;
                Assert.AreEqual(128 * 128, outputs.GetRequired("output").Length);
            }
            catch (OpenVinoBackendException exception)
            {
                Assert.AreEqual(OpenVinoErrorCodes.Cancelled, exception.ErrorCode);
            }
        }
    }
}
