using System;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    [TestClass]
    public sealed class LifecycleTests
    {
        [TestMethod]
        [Timeout(15000)]
        public async Task ActiveNativeRunCancellationRaceLeavesSessionReusable()
        {
            var options = new OnnxRuntimeOptions(intraOpThreads: 1, graphOptimization: OnnxRuntimeGraphOptimization.Disabled);
            using IInferenceSession session = OnnxRuntimeTestData.Open("cancellable-loop.onnx", backendOptions: options);
            using var source = new CancellationTokenSource();
            source.CancelAfter(10);

            // A completed native run cannot be retroactively cancelled; both race outcomes must leave the session healthy. / 已完成的原生调用不能被追溯取消；两种竞态结果都必须保持会话健康。
            await AssertCompletedOrCancelled(session.RunAsync(OnnxRuntimeTestData.LongRunningInputs(), source.Token));

            InferenceOutputs reused = session.Run(OnnxRuntimeTestData.LongRunningInputs(), CancellationToken.None);
            Assert.AreEqual(128 * 128, reused.GetRequired("output").Length);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task MaxConcurrencyQueuesCallsAndCancellationDoesNotPoisonQueuedRun()
        {
            var options = new OnnxRuntimeOptions(intraOpThreads: 1, graphOptimization: OnnxRuntimeGraphOptimization.Disabled);
            using IInferenceSession session = OnnxRuntimeTestData.Open("serialized-loop.onnx", new SessionOptions(maxConcurrency: 1), options);
            using var source = new CancellationTokenSource();
            Task<InferenceOutputs> first = Task.Run(() => session.Run(OnnxRuntimeTestData.LoopInputs(1_000_000), CancellationToken.None));
            Thread.Sleep(10);
            source.CancelAfter(10);
            Task<InferenceOutputs> second = Task.Run(() => session.Run(OnnxRuntimeTestData.LoopInputs(1), source.Token));
            await Assert.ThrowsExactlyAsync<OnnxRuntimeBackendException>(async () => await second);
            Assert.AreEqual(1, (await first).GetRequired("output").Length);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task DisposeCancelsActiveRunWaitsForUnwindAndIsIdempotent()
        {
            var options = new OnnxRuntimeOptions(intraOpThreads: 1, graphOptimization: OnnxRuntimeGraphOptimization.Disabled);
            IInferenceSession session = OnnxRuntimeTestData.Open("serialized-loop.onnx", backendOptions: options);
            Task<InferenceOutputs> active = Task.Run(() => session.Run(OnnxRuntimeTestData.LoopInputs(1_000_000), CancellationToken.None));
            Thread.Sleep(10);
            session.Dispose();
            Assert.AreEqual(OnnxRuntimeErrorCodes.Cancelled, (await Assert.ThrowsExactlyAsync<OnnxRuntimeBackendException>(async () => await active)).ErrorCode);
            session.Dispose();
            Assert.AreEqual(OnnxRuntimeErrorCodes.ObjectDisposed, Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => session.Run(OnnxRuntimeTestData.LoopInputs(1), CancellationToken.None)).ErrorCode);
        }

        [TestMethod]
        public async Task PreCancelledTokensDoNotEnterNativeRun()
        {
            using IInferenceSession session = OnnxRuntimeTestData.Open("classification.onnx");
            using var source = new CancellationTokenSource();
            source.Cancel();
            Assert.AreEqual(OnnxRuntimeErrorCodes.Cancelled, Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => session.Run(OnnxRuntimeTestData.ClassificationInputs(), source.Token)).ErrorCode);
            Assert.AreEqual(OnnxRuntimeErrorCodes.Cancelled, (await Assert.ThrowsExactlyAsync<OnnxRuntimeBackendException>(() => session.RunAsync(OnnxRuntimeTestData.ClassificationInputs(), source.Token))).ErrorCode);
        }

        private static async Task AssertCompletedOrCancelled(Task<InferenceOutputs> operation)
        {
            try
            {
                InferenceOutputs outputs = await operation;
                Assert.AreEqual(128 * 128, outputs.GetRequired("output").Length);
            }
            catch (OnnxRuntimeBackendException exception)
            {
                Assert.AreEqual(OnnxRuntimeErrorCodes.Cancelled, exception.ErrorCode);
            }
        }
    }
}
