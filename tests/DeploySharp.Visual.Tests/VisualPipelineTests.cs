using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class VisualPipelineTests
    {
        [TestMethod]
        public void SynchronousClassificationRunsThroughCoreSession()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), inputs =>
            {
                Assert.IsNotNull(inputs.GetRequired("images"));
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 3f, 2f }));
            });
            using PreparedVisualInput input = VisualTestData.ClassificationInput();
            VisualInferenceResult result = fixture.Pipeline.Run(input, new VisualExecutionOptions(correlationId: "sync"));
            Assert.AreEqual("one", result.GetValue<ClassificationResult>().TopPrediction!.Label);
            Assert.AreEqual("sync", result.CorrelationId);
            Assert.AreEqual(VisualTestData.BackendId, result.BackendId);
            Assert.AreEqual(1, fixture.Provider.LastSession!.RunCount);
        }

        [TestMethod]
        public async Task AsynchronousDetectionRunsThroughCoreSessionAndMapsCoordinates()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(-1, 6));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(-1, 6), _ => InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 6), new[] { 10f, 10f, 40f, 40f, 0.9f, 0.1f })));
            using PreparedVisualInput input = VisualTestData.DetectionInput();
            VisualInferenceResult result = await fixture.Pipeline.RunAsync(input);
            DetectionResult detections = result.GetValue<DetectionResult>();
            Assert.AreEqual(1, detections.Detections.Count);
            Assert.AreEqual("cat", detections.Detections[0].Label.Label);
        }

        [TestMethod]
        public async Task CallerCancellationAndTimeoutHaveDistinctDiagnostics()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture cancelledFixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 2f, 3f })));
            cancelledFixture.Provider.Delay = TimeSpan.FromMilliseconds(200);
            using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
            VisualException cancelled = await Assert.ThrowsExactlyAsync<VisualException>(() => cancelledFixture.Pipeline.RunAsync(VisualTestData.ClassificationInput(), cancellationToken: source.Token));
            Assert.AreEqual(VisualErrorCodes.Cancelled, cancelled.ErrorCode);
            Assert.IsInstanceOfType<OperationCanceledException>(cancelled.InnerException);

            using PipelineFixture timeoutFixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 2f, 3f })));
            timeoutFixture.Provider.Delay = TimeSpan.FromMilliseconds(200);
            VisualException timeout = await Assert.ThrowsExactlyAsync<VisualException>(() => timeoutFixture.Pipeline.RunAsync(VisualTestData.ClassificationInput(), new VisualExecutionOptions(TimeSpan.FromMilliseconds(20))));
            Assert.AreEqual(VisualErrorCodes.Timeout, timeout.ErrorCode);
        }

        [TestMethod]
        public async Task BackendFailureAndWrongOutputPreserveDiagnostics()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture failureFixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 2f, 3f })));
            failureFixture.Provider.Failure = new InvalidOperationException("synthetic backend failure");
            VisualException failure = await Assert.ThrowsExactlyAsync<VisualException>(() => failureFixture.Pipeline.RunAsync(VisualTestData.ClassificationInput()));
            Assert.AreEqual(VisualErrorCodes.InferenceFailed, failure.ErrorCode);
            Assert.IsInstanceOfType<InvalidOperationException>(failure.InnerException);
            StringAssert.Contains(failure.TechnicalDetails!, "synthetic backend failure");

            using PipelineFixture shapeFixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 2), new[] { 1f, 2f })));
            VisualException wrongShape = await Assert.ThrowsExactlyAsync<VisualException>(() => shapeFixture.Pipeline.RunAsync(VisualTestData.ClassificationInput()));
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, wrongShape.ErrorCode);
            Assert.AreEqual("scores", wrongShape.TensorName);
        }

        [TestMethod]
        public async Task PipelineSerializesNonConcurrentSessionCalls()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 2f, 3f })), maximumConcurrency: 1);
            fixture.Provider.Delay = TimeSpan.FromMilliseconds(40);
            using PreparedVisualInput input = VisualTestData.ClassificationInput();
            await Task.WhenAll(fixture.Pipeline.RunAsync(input), fixture.Pipeline.RunAsync(input));
            Assert.AreEqual(2, fixture.Provider.LastSession!.RunCount);
            Assert.AreEqual(1, fixture.Provider.LastSession.MaximumActive);
        }

        [TestMethod]
        public async Task OwnedInputDisposesOnFailureWhenExplicitlyRequested()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ => throw new InvalidOperationException("decode should not be reached"));
            fixture.Provider.Failure = new InvalidOperationException("failure");
            var resource = new TrackingDisposable();
            PreparedVisualInput input = VisualTestData.ClassificationInput(PreparedInputOwnership.Owned, resource);
            await Assert.ThrowsExactlyAsync<VisualException>(() => fixture.Pipeline.RunAsync(input, new VisualExecutionOptions(disposeOwnedInputOnCompletion: true)));
            Assert.AreEqual(1, resource.DisposeCount);
            Assert.IsTrue(input.IsDisposed);
            input.Dispose();
            Assert.AreEqual(1, resource.DisposeCount);
        }

        [TestMethod]
        public void DisposeIsIdempotentAndSubsequentCallsFailStably()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 2f, 3f })));
            FakeVisualSession session = fixture.Provider.LastSession!;
            fixture.Pipeline.Dispose();
            fixture.Pipeline.Dispose();
            Assert.AreEqual(1, session.DisposeCount);
            VisualException disposed = Assert.ThrowsExactly<VisualException>(() => fixture.Pipeline.Run(VisualTestData.ClassificationInput()));
            Assert.AreEqual(VisualErrorCodes.ObjectDisposed, disposed.ErrorCode);
            fixture.Registry.Dispose();
            Assert.AreEqual(1, fixture.Provider.DisposeCount);
        }

        [TestMethod]
        public void PreparedInputValidationRejectsWrongBindingAndShape()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 2f, 3f })));
            var size = new VisualSize(2, 2);
            using var wrong = new PreparedVisualInput("wrong", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            VisualException error = Assert.ThrowsExactly<VisualException>(() => fixture.Pipeline.Run(wrong));
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, error.ErrorCode);
        }
    }
}
