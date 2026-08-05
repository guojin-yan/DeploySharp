using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void TextPolygonOwnsCanonicalVerticesAndComputesExactIoU()
        {
            var source = new[] { new PointF(0, 0), new PointF(10, 0), new PointF(10, 10), new PointF(0, 10) };
            TextPolygon polygon = TextPolygon.Canonicalize(source, OrientedVertexOrder.CounterClockwise);
            source[0] = new PointF(99, 99);
            Assert.AreEqual(new PointF(0, 0), polygon.Vertices[0]);
            Assert.AreEqual(100f, polygon.Area, .0001f);
            TextPolygon overlap = TextPolygon.Canonicalize(new[] { new PointF(5, 0), new PointF(15, 0), new PointF(15, 10), new PointF(5, 10) }, OrientedVertexOrder.CounterClockwise);
            Assert.AreEqual(1f / 3f, TextPolygon.IntersectionOverUnion(polygon, overlap), .0001f);
            TextPolygon touch = TextPolygon.Canonicalize(new[] { new PointF(10, 0), new PointF(20, 0), new PointF(20, 10), new PointF(10, 10) }, OrientedVertexOrder.CounterClockwise);
            Assert.AreEqual(0f, TextPolygon.IntersectionOverUnion(polygon, touch));
            Assert.ThrowsExactly<ArgumentException>(() => TextPolygon.Canonicalize(new[] { new PointF(0, 0), new PointF(2, 0), new PointF(1, 1), new PointF(2, 2), new PointF(0, 2) }, OrientedVertexOrder.CounterClockwise));
            Assert.ThrowsExactly<ArgumentException>(() => TextPolygon.Canonicalize(new[] { new PointF(0, 0), new PointF(2, 2), new PointF(2, 0), new PointF(0, 2) }, OrientedVertexOrder.CounterClockwise));
        }

        [TestMethod]
        public void ExplicitDetectionUsesPolygonNmsReadingOrderAndSourceRestore()
        {
            var schema = new ExplicitTextDetectionSchema("polygons", "scores", 4, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise);
            var decoder = new ExplicitTextDetectionDecoder(schema, new TextDetectionDecoderOptions(.1f, .3f, maximumCandidates: 4, maximumRegions: 4));
            VisualModelProfile profile = DetectionProfile(decoder, TensorElementType.Float32, 3);
            var sourceSize = new VisualSize(200, 100);
            var modelSize = new VisualSize(100, 100);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), sourceSize, modelSize, 1, VisualTensorLayout.Nchw, ImageTransform.Letterbox(sourceSize, modelSize));
            InferenceOutputs outputs = Outputs(
                ("polygons", new Tensor<float>(new TensorShape(1, 3, 4, 2), new[]
                {
                    25f,25f, 45f,25f, 45f,35f, 25f,35f,
                    26f,25f, 46f,25f, 46f,35f, 26f,35f,
                    60f,60f, 80f,60f, 80f,70f, 60f,70f
                })),
                ("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .9f, .8f, .95f })));
            TextDetectionResult result = (TextDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            Assert.AreEqual(2, result.Regions.Count);
            Assert.AreEqual(0, result.Regions[0].SourceIndex);
            Assert.AreEqual(2, result.Regions[1].SourceIndex);
            TextQuadrilateral restored = result.Regions[0].CropQuadrilateral!;
            Assert.AreEqual(new PointF(50, 0), restored.TopLeft);
            Assert.AreEqual(new PointF(90, 20), restored.BottomRight);
        }

        [TestMethod]
        public void DetectionSupportsFloat64NormalizedAndRejectsStrictFailures()
        {
            var schema = new ExplicitTextDetectionSchema("polygons", "scores", 4, OrientedCoordinateSpace.Normalized, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise, boundaryMode: TextDetectionBoundaryMode.RejectOutsideSource);
            var decoder = new ExplicitTextDetectionDecoder(schema, new TextDetectionDecoderOptions(.1f, maximumCandidates: 2, maximumRegions: 2, maximumWorkspaceBytes: 1024));
            VisualModelProfile profile = DetectionProfile(decoder, TensorElementType.Float64, 1);
            using PreparedVisualInput input = DetectionInput();
            InferenceOutputs valid = Outputs(
                ("polygons", new Tensor<double>(new TensorShape(1, 1, 4, 2), new[] { .1,.1, .9,.1, .9,.9, .1,.9 })),
                ("scores", new Tensor<double>(new TensorShape(1, 1), new[] { .9 })));
            TextDetectionResult result = (TextDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, valid, CancellationToken.None));
            Assert.AreEqual(1, result.Regions.Count);
            Assert.AreEqual(10f, result.Regions[0].Polygon.Vertices[0].X, .0001f);

            InferenceOutputs outside = Outputs(
                ("polygons", new Tensor<double>(new TensorShape(1, 1, 4, 2), new[] { -.1,.1, .9,.1, .9,.9, -.1,.9 })),
                ("scores", new Tensor<double>(new TensorShape(1, 1), new[] { .9 })));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(input, profile, outside, CancellationToken.None))).ErrorCode);

            InferenceOutputs nan = Outputs(
                ("polygons", new Tensor<double>(new TensorShape(1, 1, 4, 2), new[] { .1,.1, .9,.1, .9,.9, .1,.9 })),
                ("scores", new Tensor<double>(new TensorShape(1, 1), new[] { double.NaN })));
            Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(input, profile, nan, CancellationToken.None)));

            InferenceOutputs extra = Outputs(
                ("polygons", new Tensor<double>(new TensorShape(1, 1, 4, 2), new[] { .1,.1, .9,.1, .9,.9, .1,.9 })),
                ("scores", new Tensor<double>(new TensorShape(1, 1), new[] { .9 })),
                ("extra", new Tensor<double>(new TensorShape(1), new[] { 0d })));
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(input, profile, extra, CancellationToken.None))).ErrorCode);
        }

        [TestMethod]
        public void CharacterSetOwnsUnicodeScalarsAndRejectsDuplicatesAndSurrogates()
        {
            var characters = new OcrCharacterSet("tests.charset", "1", "AB😀");
            Assert.AreEqual(3, characters.Count);
            Assert.AreEqual("😀", characters.Characters[2]);
            Assert.AreEqual(64, characters.Sha256.Length);
            Assert.AreEqual(characters.Sha256, new OcrCharacterSet("tests.charset", "1", "AB😀").Sha256);
            Assert.ThrowsExactly<ArgumentException>(() => new OcrCharacterSet("tests.charset", "1", "ABA"));
            Assert.ThrowsExactly<ArgumentException>(() => new OcrCharacterSet("tests.charset", "1", "\uD800"));
        }

        [TestMethod]
        public void CtcBlankMiddleRepeatBlankGapTieAndUnicodeAreDeterministic()
        {
            var characters = new OcrCharacterSet("tests.charset", "1", "AB😀");
            var decoder = new GreedyCtcDecoder(new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses), characters, new CtcDecoderOptions(blankIndex: 1, applySoftmax: false));
            VisualModelProfile profile = RecognitionProfile(decoder, TensorElementType.Float32, 1, 8, 4);
            using PreparedVisualInput input = RecognitionInput(1, 16);
            float[] values = Probabilities(1, 8, 4,
                1, 0, 0, 1, 0, 2, 3, 3);
            // Timestep 7 ties class 0 and class 3; the lowest class index must win.
            values[(7 * 4) + 0] = .9f;
            values[(7 * 4) + 3] = .9f;
            TextRecognitionBatchResult result = (TextRecognitionBatchResult)decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create("logits", new Tensor<float>(new TensorShape(1, 8, 4), values)), CancellationToken.None));
            Assert.AreEqual("AAB😀A", result.Items[0].Text);
            Assert.IsTrue(result.Items[0].Tokens[2].IsCollapsedRepeat);
            Assert.IsTrue(result.Items[0].Tokens[0].IsBlank);
            Assert.AreEqual(0, result.Items[0].Tokens[7].ClassIndex);
        }

        [TestMethod]
        public void CtcSupportsTimeBatchFloat64UnknownAndStrictClassCount()
        {
            var characters = new OcrCharacterSet("tests.charset", "1", "AB");
            var options = new CtcDecoderOptions(blankIndex: 0, applySoftmax: true, unknownClassIndex: 2, unknownBehavior: CtcUnknownTokenBehavior.Replace, unknownReplacement: "?");
            var decoder = new GreedyCtcDecoder(new CtcOutputSchema("logits", CtcTensorLayout.TimeBatchClasses), characters, options);
            VisualModelProfile profile = RecognitionProfile(decoder, TensorElementType.Float64, 2, 3, 4, CtcTensorLayout.TimeBatchClasses);
            using PreparedVisualInput input = RecognitionInput(2, 16);
            var values = new double[3 * 2 * 4];
            SetTimeBatch(values, 3, 2, 4, 0, 0, 1);
            SetTimeBatch(values, 3, 2, 4, 1, 0, 2);
            SetTimeBatch(values, 3, 2, 4, 2, 0, 3);
            SetTimeBatch(values, 3, 2, 4, 0, 1, 3);
            SetTimeBatch(values, 3, 2, 4, 1, 1, 0);
            SetTimeBatch(values, 3, 2, 4, 2, 1, 1);
            TextRecognitionBatchResult result = (TextRecognitionBatchResult)decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create("logits", new Tensor<double>(new TensorShape(3, 2, 4), values)), CancellationToken.None));
            Assert.AreEqual("A?B", result.Items[0].Text);
            Assert.AreEqual("BA", result.Items[1].Text);

            VisualModelProfile wrongProfile = RecognitionProfile(decoder, TensorElementType.Float64, 2, 3, 5, CtcTensorLayout.TimeBatchClasses);
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(input, wrongProfile, InferenceOutputs.Create("logits", new Tensor<double>(new TensorShape(3, 2, 5), new double[30])), CancellationToken.None))).ErrorCode);
        }

        [TestMethod]
        public void CropProfileHasExplicitFixedAndDynamicWidthBounds()
        {
            var quad = new TextQuadrilateral(new PointF(0, 0), new PointF(20, 0), new PointF(20, 10), new PointF(0, 10), TextCornerOrder.TopLeftClockwise);
            var fixedProfile = new TextCropProfile("tests.crop.fixed", 8, OcrRecognitionWidthMode.Fixed, 16, 32);
            var dynamicProfile = new TextCropProfile("tests.crop.dynamic", 8, OcrRecognitionWidthMode.Dynamic, 16, 64, 8);
            Assert.AreEqual(16, fixedProfile.CalculateWidth(quad, TextOrientation.Degrees0));
            Assert.AreEqual(16, dynamicProfile.CalculateWidth(quad, TextOrientation.Degrees0));
            Assert.AreEqual(8, dynamicProfile.CalculateWidth(quad, TextOrientation.Clockwise90));
            TextRegion region = new TextRegion(0, .9f, quad.Polygon, quad);
            Assert.AreEqual(16, new TextCropRequest(region, fixedProfile).TargetWidth);
        }

        [TestMethod]
        public async Task FakeDualSessionPipelineBatchesRestoresOrderCancelsAndDisposes()
        {
            OcrFixture fixture = CreateOcrFixture();
            using (fixture)
            using (var input = new FakeOcrImageInput())
            {
                OcrResult result = await fixture.Pipeline.RunAsync(input);
                Assert.AreEqual(2, result.Regions.Count);
                Assert.AreEqual("AB", result.Regions[0].Recognition.Text);
                Assert.AreEqual("CA", result.Regions[1].Recognition.Text);
                Assert.AreEqual(2, input.LastBatchSize);
                Assert.AreEqual(result.ComputeSha256(), (await fixture.Pipeline.RunAsync(input)).ComputeSha256());

                fixture.DetectionProvider.Delay = TimeSpan.FromMilliseconds(200);
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
                OcrPipelineException cancelled = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, cancellationToken: cancellation.Token));
                Assert.AreEqual(VisualErrorCodes.Cancelled, cancelled.ErrorCode);
                Assert.AreEqual(OcrPipelineStage.Detection, cancelled.Stage);
                fixture.DetectionProvider.Delay = TimeSpan.Zero;
                Assert.AreEqual(2, (await fixture.Pipeline.RunAsync(input)).Regions.Count);
            }
            fixture.Pipeline.Dispose();
            fixture.Pipeline.Dispose();
            Assert.AreEqual(1, fixture.DetectionProvider.LastSession!.DisposeCount);
            Assert.AreEqual(1, fixture.RecognitionProvider.LastSession!.DisposeCount);
        }

        [TestMethod]
        public async Task PipelineTimeoutAndOwnedInputDisposalAreStable()
        {
            using OcrFixture fixture = CreateOcrFixture();
            fixture.RecognitionProvider.Delay = TimeSpan.FromMilliseconds(200);
            var input = new FakeOcrImageInput();
            OcrPipelineException timeout = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, new OcrExecutionOptions(TimeSpan.FromMilliseconds(20), disposeInputOnCompletion: true)));
            Assert.AreEqual(VisualErrorCodes.Timeout, timeout.ErrorCode);
            Assert.AreEqual(1, input.DisposeCount);
        }

        [TestMethod]
        public async Task PipelineSerializesConcurrentCallsCancelsQueuedCallAndRecovers()
        {
            using OcrFixture fixture = CreateOcrFixture();
            fixture.DetectionProvider.Delay = TimeSpan.FromMilliseconds(60);
            using var firstInput = new FakeOcrImageInput();
            using var secondInput = new FakeOcrImageInput();
            Task<OcrResult> first = fixture.Pipeline.RunAsync(firstInput);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
            OcrPipelineException queued = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(secondInput, cancellationToken: cancellation.Token));
            Assert.AreEqual(VisualErrorCodes.Cancelled, queued.ErrorCode);
            Assert.AreEqual(OcrPipelineStage.Input, queued.Stage);
            Assert.AreEqual(2, (await first).Regions.Count);
            fixture.DetectionProvider.Delay = TimeSpan.Zero;
            Assert.AreEqual(2, (await fixture.Pipeline.RunAsync(secondInput)).Regions.Count);
            Assert.AreEqual(1, fixture.DetectionProvider.LastSession!.MaximumActive);
        }

        [TestMethod]
        public async Task RecognitionFailureKeepsStageContextAndPipelineReusable()
        {
            using OcrFixture fixture = CreateOcrFixture();
            using var input = new FakeOcrImageInput();
            fixture.RecognitionProvider.Failure = new InvalidOperationException("synthetic recognition failure");
            OcrPipelineException failure = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input));
            Assert.AreEqual(VisualErrorCodes.OcrPipelineFailed, failure.ErrorCode);
            Assert.AreEqual(OcrPipelineStage.Recognition, failure.Stage);
            Assert.AreEqual("tests/text-recognition.v1", failure.ProfileId);
            Assert.IsNotNull(failure.InnerException);
            fixture.RecognitionProvider.Failure = null;
            Assert.AreEqual(2, (await fixture.Pipeline.RunAsync(input)).Regions.Count);
        }

        [TestMethod]
        public void OcrPolygonAndCtcPerformanceEntryRecordsThroughputAndAllocation()
        {
            const int candidates = 128;
            var detector = new ExplicitTextDetectionDecoder(
                new ExplicitTextDetectionSchema("polygons", "scores", 4, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise),
                new TextDetectionDecoderOptions(.1f, .3f, maximumCandidates: candidates, maximumRegions: candidates));
            VisualModelProfile detectionProfile = DetectionProfile(detector, TensorElementType.Float32, candidates);
            using PreparedVisualInput detectionInput = DetectionInput();
            var polygons = new float[candidates * 8];
            var scores = new float[candidates];
            for (int index = 0; index < candidates; index++)
            {
                int column = index % 16;
                int row = index / 16;
                float left = column * 6f;
                float top = row * 11f;
                int offset = index * 8;
                polygons[offset] = left; polygons[offset + 1] = top;
                polygons[offset + 2] = left + 4; polygons[offset + 3] = top;
                polygons[offset + 4] = left + 4; polygons[offset + 5] = top + 8;
                polygons[offset + 6] = left; polygons[offset + 7] = top + 8;
                scores[index] = .9f;
            }
            InferenceOutputs detectionOutputs = Outputs(
                ("polygons", new Tensor<float>(new TensorShape(1,candidates,4,2), polygons)),
                ("scores", new Tensor<float>(new TensorShape(1,candidates), scores)));

            var ctc = new GreedyCtcDecoder(
                new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses),
                new OcrCharacterSet("tests.performance", "1", "ABC"),
                new CtcDecoderOptions(0, applySoftmax: true, maximumBatch: 16, maximumSequenceLength: 64));
            VisualModelProfile recognitionProfile = RecognitionProfile(ctc, TensorElementType.Float32, 16, 32, 4);
            using PreparedVisualInput recognitionInput = RecognitionInput(16,16);
            float[] logits = Probabilities(16,32,4, Enumerable.Range(0, 16 * 32).Select(index => index % 4).ToArray());
            InferenceOutputs recognitionOutputs = InferenceOutputs.Create("logits", new Tensor<float>(new TensorShape(16,32,4), logits));

            detector.Decode(new VisualDecodeContext(detectionInput, detectionProfile, detectionOutputs, CancellationToken.None));
            ctc.Decode(new VisualDecodeContext(recognitionInput, recognitionProfile, recognitionOutputs, CancellationToken.None));
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            const int iterations = 10;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                detector.Decode(new VisualDecodeContext(detectionInput, detectionProfile, detectionOutputs, CancellationToken.None));
                ctc.Decode(new VisualDecodeContext(recognitionInput, recognitionProfile, recognitionOutputs, CancellationToken.None));
            }
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            double regionsPerSecond = candidates * iterations / watch.Elapsed.TotalSeconds;
            TestContext.WriteLine("OCR_PERF candidates={0} batch={1} sequence={2} iterations={3} elapsedMs={4:F3} regionsPerSecond={5:F1} allocatedBytes={6}", candidates, 16, 32, iterations, watch.Elapsed.TotalMilliseconds, regionsPerSecond, allocated);
            Assert.IsTrue(watch.Elapsed > TimeSpan.Zero);
            Assert.IsTrue(allocated >= 0);
        }

        private static OcrFixture CreateOcrFixture()
        {
            var detectorDecoder = new ExplicitTextDetectionDecoder(new ExplicitTextDetectionSchema("polygons", "scores", 4, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise), new TextDetectionDecoderOptions(.1f, .3f, maximumCandidates: 3, maximumRegions: 3));
            VisualModelProfile detectorProfile = DetectionProfile(detectorDecoder, TensorElementType.Float32, 3, "fake-detector");
            var recognizerDecoder = new GreedyCtcDecoder(new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses), new OcrCharacterSet("tests.abc", "1", "ABC"), new CtcDecoderOptions(0, applySoftmax: false));
            VisualModelProfile recognizerProfile = RecognitionProfile(recognizerDecoder, TensorElementType.Float32, 2, 6, 4, format: "fake-recognizer");
            var detectionProvider = new FakeVisualBackendProvider(VisualTestData.Metadata(detectorProfile, new TensorShape(1, 3, 4, 2)), _ => DetectionOutputs(), "fake-detector", new BackendId("fake-ocr-detector"));
            var recognitionProvider = new FakeVisualBackendProvider(VisualTestData.Metadata(recognizerProfile, new TensorShape(2, 6, 4)), _ => RecognitionOutputs(), "fake-recognizer", new BackendId("fake-ocr-recognizer"));
            var registry = new BackendRegistry();
            registry.Register(detectionProvider);
            registry.Register(recognitionProvider);
            var profiles = new VisualProfileRegistry();
            profiles.Register(detectorProfile);
            profiles.Register(recognizerProfile);
            profiles.Freeze();
            var detectorArtifact = new ModelArtifact(detectorProfile.ModelId, "fake-detector", "detector.fake", preferredBackend: detectionProvider.Descriptor.Id);
            var recognizerArtifact = new ModelArtifact(recognizerProfile.ModelId, "fake-recognizer", "recognizer.fake", preferredBackend: recognitionProvider.Descriptor.Id);
            var detectorRequest = new BackendRequest(BackendCapabilities.TensorInference, detectionProvider.Descriptor.Id);
            var recognizerRequest = new BackendRequest(BackendCapabilities.TensorInference, recognitionProvider.Descriptor.Id);
            VisualProfileSelection detectorSelection = profiles.Select(detectorArtifact, registry, detectorRequest, VisualTaskId.TextDetection);
            VisualProfileSelection recognizerSelection = profiles.Select(recognizerArtifact, registry, recognizerRequest, VisualTaskId.TextRecognition);
            var crop = new TextCropProfile("tests.crop", 8, OcrRecognitionWidthMode.Fixed, 16, 16);
            var pipeline = new OcrPipeline(registry, detectorSelection, detectorRequest, recognizerSelection, recognizerRequest, crop, new OcrPipelineOptions(maximumRegions: 3, maximumRecognitionBatch: 2), new SessionOptions(1), new SessionOptions(1));
            return new OcrFixture(registry, detectionProvider, recognitionProvider, pipeline);
        }

        private static VisualModelProfile DetectionProfile(IVisualDecoder decoder, TensorElementType type, int candidates, string format = "fake")
        {
            return new VisualModelProfile("tests/text-detection.v1", new ModelId("tests/text-detector"), VisualTaskId.TextDetection, "1", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("polygons", type, new TensorShape(1, candidates, 4, 2)), new VisualOutputBinding("scores", type, new TensorShape(1, candidates)) },
                Array.Empty<VisualLabel>(), decoder);
        }

        private static VisualModelProfile RecognitionProfile(IVisualDecoder decoder, TensorElementType type, int batch, int time, int classes, CtcTensorLayout layout = CtcTensorLayout.BatchTimeClasses, string format = "fake")
        {
            TensorShape output = layout == CtcTensorLayout.BatchTimeClasses ? new TensorShape(batch, time, classes) : new TensorShape(time, batch, classes);
            return new VisualModelProfile("tests/text-recognition.v1", new ModelId("tests/text-recognizer"), VisualTaskId.TextRecognition, "1", format,
                new VisualInputBinding("crops", TensorElementType.Float32, new TensorShape(batch, 3, 8, 16), VisualTensorLayout.Nchw, batch, batch),
                new[] { new VisualOutputBinding("logits", type, output) }, Array.Empty<VisualLabel>(), decoder);
        }

        private static PreparedVisualInput DetectionInput()
        {
            var size = new VisualSize(100, 100);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }

        private static PreparedVisualInput RecognitionInput(int batch, int width)
        {
            var size = new VisualSize(width, 8);
            return new PreparedVisualInput("crops", new Tensor<float>(new TensorShape(batch, 3, 8, width), new float[batch * 3 * 8 * width]), size, size, batch, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }

        private static InferenceOutputs DetectionOutputs()
        {
            return Outputs(
                ("polygons", new Tensor<float>(new TensorShape(1, 3, 4, 2), new[]
                {
                    5f,5f, 45f,5f, 45f,25f, 5f,25f,
                    6f,5f, 46f,5f, 46f,25f, 6f,25f,
                    50f,50f, 95f,50f, 95f,80f, 50f,80f
                })),
                ("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .95f, .8f, .9f })));
        }

        private static InferenceOutputs RecognitionOutputs()
        {
            float[] values = Probabilities(2, 6, 4,
                0,1,1,0,2,2,
                3,3,0,1,1,0);
            return InferenceOutputs.Create("logits", new Tensor<float>(new TensorShape(2, 6, 4), values));
        }

        private static float[] Probabilities(int batch, int time, int classes, params int[] selected)
        {
            Assert.AreEqual(batch * time, selected.Length);
            var values = new float[batch * time * classes];
            for (int index = 0; index < selected.Length; index++) values[(index * classes) + selected[index]] = .9f;
            return values;
        }

        private static void SetTimeBatch(double[] values, int time, int batch, int classes, int timeIndex, int batchIndex, int selected)
        {
            values[((timeIndex * batch + batchIndex) * classes) + selected] = 4;
        }

        private static InferenceOutputs Outputs(params (string Name, ITensor Tensor)[] values)
        {
            return new InferenceOutputs(values.Select(value => new NamedTensor(value.Name, value.Tensor)));
        }

        private sealed class FakeOcrImageInput : IOcrImageInput
        {
            private bool _disposed;
            public FakeOcrImageInput() { DetectionInput = DetectionInputFactory(); }
            public VisualSize SourceSize { get; } = new VisualSize(100, 100);
            public PreparedVisualInput DetectionInput { get; }
            public int LastBatchSize { get; private set; }
            public int DisposeCount { get; private set; }
            public PreparedVisualInput PrepareRecognitionBatch(string inputName, IReadOnlyList<TextCropRequest> requests, CancellationToken cancellationToken)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(FakeOcrImageInput));
                cancellationToken.ThrowIfCancellationRequested();
                LastBatchSize = requests.Count;
                int width = requests[0].TargetWidth;
                var size = new VisualSize(width, requests[0].TargetHeight);
                return new PreparedVisualInput(inputName, new Tensor<float>(new TensorShape(requests.Count, 3, size.Height, size.Width), new float[requests.Count * 3 * size.Height * size.Width]), size, size, requests.Count, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            }
            public void Dispose() { if (_disposed) return; _disposed = true; DisposeCount++; DetectionInput.Dispose(); }
            private static PreparedVisualInput DetectionInputFactory()
            {
                var size = new VisualSize(100, 100);
                return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            }
        }

        private sealed class OcrFixture : IDisposable
        {
            public OcrFixture(BackendRegistry registry, FakeVisualBackendProvider detectionProvider, FakeVisualBackendProvider recognitionProvider, OcrPipeline pipeline) { Registry = registry; DetectionProvider = detectionProvider; RecognitionProvider = recognitionProvider; Pipeline = pipeline; }
            public BackendRegistry Registry { get; }
            public FakeVisualBackendProvider DetectionProvider { get; }
            public FakeVisualBackendProvider RecognitionProvider { get; }
            public OcrPipeline Pipeline { get; }
            public void Dispose() { Pipeline.Dispose(); Registry.Dispose(); }
        }
    }
}
