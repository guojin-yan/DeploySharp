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
            await Task.Delay(10);
            queuedCancellation.CancelAfter(10);
            Task<InferenceOutputs> queued = session.RunAsync(OpenVinoTestData.LongRunningInputs(), queuedCancellation.Token);

            OpenVinoBackendException queuedError = await Assert.ThrowsExactlyAsync<OpenVinoBackendException>(() => queued);
            Assert.AreEqual(OpenVinoErrorCodes.Cancelled, queuedError.ErrorCode);
            activeCancellation.Cancel();
            OpenVinoBackendException activeError = await Assert.ThrowsExactlyAsync<OpenVinoBackendException>(() => active);
            Assert.AreEqual(OpenVinoErrorCodes.Cancelled, activeError.ErrorCode);

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
            OpenVinoBackendException activeError = await Assert.ThrowsExactlyAsync<OpenVinoBackendException>(() => active);
            Assert.AreEqual(OpenVinoErrorCodes.Cancelled, activeError.ErrorCode);
            await dispose;

            session.Dispose();
            OpenVinoBackendException disposed = Assert.ThrowsExactly<OpenVinoBackendException>(
                () => session.Run(OpenVinoTestData.LongRunningInputs(), CancellationToken.None));
            Assert.AreEqual(OpenVinoErrorCodes.ObjectDisposed, disposed.ErrorCode);
        }
    }
}
