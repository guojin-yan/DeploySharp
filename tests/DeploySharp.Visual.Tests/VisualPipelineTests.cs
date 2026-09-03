using System;
using System.Collections.Generic;
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
        public void DynamicClassificationBatchRunsThroughVisualPipeline()
        {
            var profile = new VisualModelProfile(
                "tests/classification.dynamic.v1", VisualTestData.ClassificationModelId, VisualTaskId.ImageClassification, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2), VisualTensorLayout.Nchw, 1, 4),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(-1, 3)) },
                new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
                new ClassificationDecoder("scores", ClassificationScoreMode.Probabilities, topK: 1));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(-1, 3), inputs =>
            {
                Assert.AreEqual(2L, inputs.GetRequired("images").Shape[0]);
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(2, 3), new[] { .1f, .8f, .1f, .7f, .2f, .1f }));
            });
            var size = new VisualSize(2, 2);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]), size, size, 2, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size), inputId: "classification-dynamic");

            VisualInferenceResult result = fixture.Pipeline.Run(input);
            ClassificationBatchResult batch = result.GetValue<ClassificationBatchResult>();
            Assert.AreEqual(2, batch.Count);
            Assert.AreEqual("one", batch[0].TopPrediction!.Label);
            Assert.AreEqual("zero", batch[1].TopPrediction!.Label);
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
        public async Task RunManyUsesIndependentSessionsAndPreservesInputOrder()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), inputs =>
            {
                int classIndex = (int)((float[])inputs.GetRequired("images").Buffer)[0];
                var scores = new float[3];
                scores[classIndex] = 1f;
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), scores));
            }, maximumConcurrency: 2);
            fixture.Provider.Delay = TimeSpan.FromMilliseconds(30);
            PreparedVisualInput[] inputs = Enumerable.Range(0, 5).Select(index => ClassificationInput(index % 3)).ToArray();
            try
            {
                IReadOnlyList<VisualInferenceResult> results = await fixture.Pipeline.RunManyAsync(inputs, new VisualExecutionOptions(correlationId: "many"));

                CollectionAssert.AreEqual(new[] { "zero", "one", "two", "zero", "one" }, results.Select(result => result.GetValue<ClassificationResult>().TopPrediction!.Label).ToArray());
                Assert.IsTrue(results.All(result => result.CorrelationId == "many"));
                Assert.AreEqual(2, fixture.Provider.CreatedSessions.Count);
                Assert.AreEqual(5, fixture.Provider.CreatedSessions.Sum(session => session.RunCount));
                Assert.IsTrue(fixture.Provider.CreatedSessions.All(session => session.RunCount > 0));
            }
            finally
            {
                foreach (PreparedVisualInput input in inputs) input.Dispose();
            }
        }

        [TestMethod]
        public async Task RunManyValidatesAllInputsBeforeStartingAnyModelCall()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 2f, 3f })), maximumConcurrency: 2);
            using PreparedVisualInput valid = VisualTestData.ClassificationInput();
            var size = new VisualSize(2, 2);
            using var invalid = new PreparedVisualInput("wrong", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));

            await Assert.ThrowsExactlyAsync<VisualException>(() => fixture.Pipeline.RunManyAsync(new[] { valid, invalid }));

            Assert.AreEqual(0, fixture.Provider.CreatedSessions.Sum(session => session.RunCount));
        }

        [TestMethod]
        public async Task SlidingWindowRunnerPrefetchesTilesAndSuppressesOverlappingSourceDetections()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(1, 6));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 6), _ => InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 6), new[] { 0f, 0f, 100f, 100f, 0.95f, 0.05f })), maximumConcurrency: 2);
            var runner = new SlidingWindowDetectionRunner(fixture.Pipeline);
            var source = new VisualSize(150, 100);
            var options = new SlidingWindowDetectionOptions(new VisualSize(100, 100), overlap: 0.5f, globalIouThreshold: 0.3f, maximumDetections: 10);

            SlidingWindowDetectionResult result = await runner.RunAsync(source, options, (window, _) =>
                new PreparedVisualInput(
                    "images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), source,
                    new VisualSize(100, 100), 1, VisualTensorLayout.Nchw,
                    ImageTransform.Crop(source, new VisualSize(100, 100), window.Bounds)));

            Assert.AreEqual(2, result.WindowCount);
            Assert.AreEqual(1, result.Detections.Detections.Count);
            Assert.AreEqual(0f, result.Detections.Detections[0].Box.X);
            Assert.AreEqual(100f, result.Detections.Detections[0].Box.Width);
        }

        [TestMethod]
        public async Task RunPrefetchedAsyncOverlapsPreparationAndPreservesInputOrder()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), inputs =>
            {
                int index = (int)((float[])inputs.GetRequired("images").Buffer)[0];
                var scores = new float[3];
                scores[index % scores.Length] = 1f;
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), scores));
            }, maximumConcurrency: 2);
            fixture.Provider.Delay = TimeSpan.FromMilliseconds(15);
            int prepared = 0;
            IReadOnlyList<VisualInferenceResult> results = await fixture.Pipeline.RunPrefetchedAsync(
                Enumerable.Range(0, 6).ToArray(),
                (index, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    Interlocked.Increment(ref prepared);
                    var size = new VisualSize(2, 2);
                    var values = new float[12];
                    values[0] = index;
                    return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), values), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
                },
                prefetch: 2);

            CollectionAssert.AreEqual(new[] { "zero", "one", "two", "zero", "one", "two" }, results.Select(value => value.GetValue<ClassificationResult>().TopPrediction!.Label).ToArray());
            Assert.AreEqual(6, prepared);
            Assert.AreEqual(6, fixture.Provider.CreatedSessions.Sum(session => session.RunCount));
        }

        [TestMethod]
        public async Task RunPrefetchedAsyncAcceptsAsynchronousPreparation()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 2f, 3f })));
            IReadOnlyList<VisualInferenceResult> results = await fixture.Pipeline.RunPrefetchedAsync(
                new[] { 0, 1 },
                async (index, token) =>
                {
                    await Task.Yield();
                    token.ThrowIfCancellationRequested();
                    var size = new VisualSize(2, 2);
                    return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size), inputId: "async-" + index);
                },
                prefetch: 1);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(2, fixture.Provider.CreatedSessions.Sum(session => session.RunCount));
        }

        [TestMethod]
        public async Task SlidingWindowRunnerMapsExplicitTileLocalCoordinates()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(1, 6));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 6), _ =>
                InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 6), new[] { 0f, 0f, 10f, 10f, 0.95f, 0.05f })), maximumConcurrency: 2);
            var runner = new SlidingWindowDetectionRunner(fixture.Pipeline);
            var options = new SlidingWindowDetectionOptions(new VisualSize(100, 100), overlap: 0.5f, globalIouThreshold: 0.9f, coordinateMode: SlidingWindowCoordinateMode.TileLocal);

            SlidingWindowDetectionResult result = await runner.RunAsync(new VisualSize(150, 100), options, (window, _) =>
            {
                var tile = new VisualSize((int)window.Bounds.Width, (int)window.Bounds.Height);
                return new PreparedVisualInput(
                    "images", new Tensor<float>(new TensorShape(1, 3, tile.Height, tile.Width), new float[tile.Width * tile.Height * 3]),
                    tile, tile, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(tile, tile));
            });

            Assert.AreEqual(2, result.WindowCount);
            Assert.AreEqual(2, result.Detections.Detections.Count);
            Assert.AreEqual(0f, result.Detections.Detections[0].Box.X);
            Assert.AreEqual(50f, result.Detections.Detections[1].Box.X);
        }

        [TestMethod]
        public async Task SlidingWindowRunnerAcceptsAsynchronousTilePreparation()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(1, 6));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 6), _ =>
                InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 6), new[] { 5f, 6f, 15f, 16f, 0.95f, 0.05f })), maximumConcurrency: 2);
            var runner = new SlidingWindowDetectionRunner(fixture.Pipeline);
            var options = new SlidingWindowDetectionOptions(new VisualSize(100, 100), overlap: 0.5f, globalIouThreshold: 0.9f, coordinateMode: SlidingWindowCoordinateMode.TileLocal);
            int activePreparations = 0;
            int maximumActivePreparations = 0;

            SlidingWindowDetectionResult result = await runner.RunAsync(new VisualSize(150, 100), options, async (window, token) =>
            {
                int active = Interlocked.Increment(ref activePreparations);
                while (true)
                {
                    int observed = Volatile.Read(ref maximumActivePreparations);
                    if (active <= observed || Interlocked.CompareExchange(ref maximumActivePreparations, active, observed) == observed) break;
                }
                try
                {
                    await Task.Delay(10, token);
                    token.ThrowIfCancellationRequested();
                    var tile = new VisualSize((int)window.Bounds.Width, (int)window.Bounds.Height);
                    return new PreparedVisualInput(
                        "images", new Tensor<float>(new TensorShape(1, 3, tile.Height, tile.Width), new float[tile.Width * tile.Height * 3]),
                        tile, tile, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(tile, tile));
                }
                finally
                {
                    Interlocked.Decrement(ref activePreparations);
                }
            });

            Assert.AreEqual(2, result.WindowCount);
            Assert.AreEqual(2, result.Detections.Detections.Count);
            Assert.AreEqual(5f, result.Detections.Detections[0].Box.X);
            Assert.AreEqual(55f, result.Detections.Detections[1].Box.X);
            Assert.IsTrue(maximumActivePreparations >= 2);
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

        private static PreparedVisualInput ClassificationInput(int classIndex)
        {
            var size = new VisualSize(2, 2);
            var values = new float[12];
            values[0] = classIndex;
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), values), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size), inputId: "classification-" + classIndex);
        }
    }
}
