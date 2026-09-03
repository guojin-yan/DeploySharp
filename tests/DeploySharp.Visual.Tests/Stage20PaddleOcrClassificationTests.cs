using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class Stage20PaddleOcrClassificationTests
    {
        private const string Sha = "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2";

        [TestMethod]
        public void PaddleClassificationProfilesKeepLegacyAndTextLineContractsDistinct()
        {
            PaddleOcrProfile legacy = PaddleOcrProfiles.CreateLegacyClassification(new ModelId("tests/paddle-cls-legacy"), Artifact());
            PaddleOcrProfile textLine = PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("tests/paddle-cls-textline"), Artifact());

            Assert.AreEqual(PaddleOcrFamily.PaddleOcrCls, legacy.Family);
            Assert.AreEqual(new TensorShape(1, 3, 48, 192), legacy.VisualProfile.Input.ShapePattern);
            Assert.AreEqual("legacy-0", legacy.VisualProfile.Labels[0].Label);
            Assert.AreEqual(VisualColorOrder.Bgr, legacy.CropProfile!.ColorOrder);
            Assert.AreEqual(127.5f, legacy.CropProfile.Means[0]);

            Assert.AreEqual(PaddleOcrFamily.PaddleOcrCls, textLine.Family);
            Assert.AreEqual(new TensorShape(1, 3, 80, 160), textLine.VisualProfile.Input.ShapePattern);
            Assert.AreEqual(new TensorShape(1, 2), textLine.VisualProfile.Outputs[0].ShapePattern);
            Assert.AreEqual("0_degree", textLine.VisualProfile.Labels[0].Label);
            Assert.AreEqual("180_degree", textLine.VisualProfile.Labels[1].Label);
            Assert.AreEqual(VisualColorOrder.Rgb, textLine.CropProfile!.ColorOrder);
            Assert.AreEqual(123.675f, textLine.CropProfile.Means[0], .0001f);
            Assert.AreEqual(Sha, textLine.CreateArtifact("model.onnx").Sha256);
            PaddleOcrProfile dynamicLegacy = PaddleOcrProfiles.CreateLegacyClassification(new ModelId("tests/paddle-cls-dynamic"), Artifact(), outputName: "softmax_0.tmp_0", allowDynamicBatch: true);
            Assert.AreEqual(new TensorShape(-1, 3, 48, 192), dynamicLegacy.VisualProfile.Input.ShapePattern);
            Assert.AreEqual(new TensorShape(-1, 2), dynamicLegacy.VisualProfile.Outputs[0].ShapePattern);
            Assert.AreEqual(1, dynamicLegacy.VisualProfile.Input.MaximumBatch);
            PaddleOcrProfile dynamicBatch = PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("tests/batch"), Artifact(), maximumBatch: 2, allowDynamicBatch: true);
            Assert.AreEqual(2, dynamicBatch.VisualProfile.Input.MaximumBatch);
            Assert.AreEqual(new TensorShape(-1, 3, 80, 160), dynamicBatch.VisualProfile.Input.ShapePattern);
            Assert.ThrowsExactly<ArgumentException>(() => PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("tests/static-batch"), Artifact(), maximumBatch: 2));
        }

        [TestMethod]
        public void BinaryOrientationDecodingHonorsOrderTieRejectAndOwnedScores()
        {
            OcrOrientationResult degrees180 = Decode(new[] { .1f, .9f }, .5f);
            Assert.AreEqual(TextOrientation.Degrees180, degrees180.AcceptedOrientation);
            Assert.AreEqual(1, degrees180.ClassIndex);
            Assert.AreEqual(2, degrees180.Scores.Count);

            float[] tied = { .5f, .5f };
            OcrOrientationResult tie = Decode(tied, .5f);
            tied[0] = 0f;
            Assert.AreEqual(TextOrientation.Degrees0, tie.AcceptedOrientation);
            Assert.AreEqual(.5f, tie.Scores[0]);

            OcrOrientationResult rejected = Decode(new[] { .1f, .9f }, .95f);
            Assert.IsTrue(rejected.Rejected);
            Assert.IsNull(rejected.AcceptedOrientation);
            Assert.AreEqual(TextOrientation.Degrees0, rejected.Orientation);
        }

        [TestMethod]
        public void BinaryOrientationRejectsInvalidMappingAndNonFiniteOutput()
        {
            Assert.AreEqual(VisualErrorCodes.OcrOrientationContractInvalid, Assert.ThrowsExactly<VisualException>(() =>
                new OcrOrientationSchema("fetch_name_0", new TensorShape(1, 2), TensorElementType.Float32, new[] { TextOrientation.Degrees0, TextOrientation.Clockwise90 }, OcrOrientationValueSemantics.Probability, false)).ErrorCode);

            var decoder = Decoder(.5f);
            VisualException exception = Assert.ThrowsExactly<VisualException>(() => Decode(decoder, new[] { float.NaN, 1f }));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, exception.ErrorCode);

            var dynamicDecoder = new OcrOrientationDecoder(new OcrOrientationSchema("fetch_name_0", new TensorShape(-1, 2), TensorElementType.Float32,
                new[] { TextOrientation.Degrees0, TextOrientation.Degrees180 }, OcrOrientationValueSemantics.Probability, false, allowDynamicBatch: true));
            VisualException batch = Assert.ThrowsExactly<VisualException>(() => Decode(dynamicDecoder, new Tensor<float>(new TensorShape(2, 2), new[] { .9f, .1f, .1f, .9f })));
            Assert.AreEqual(VisualErrorCodes.OcrOrientationContractInvalid, batch.ErrorCode);
        }

        [TestMethod]
        public void ExistingFourDirectionContractRemainsExplicitlyFourClass()
        {
            var schema = new OcrOrientationSchema("orientation_scores", new TensorShape(1, 4), TensorElementType.Float32,
                new[] { TextOrientation.Degrees0, TextOrientation.CounterClockwise90, TextOrientation.Clockwise90, TextOrientation.Degrees180 });
            Assert.AreEqual(4, schema.ClassCount);
            Assert.AreEqual(TextOrientation.Clockwise90, schema.ClassToOrientation[2]);
        }

        [TestMethod]
        public void OcrTimingIncludesPerRegionOrientationStage()
        {
            var timing = new OcrStageTiming(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2), TimeSpan.FromMilliseconds(3), TimeSpan.FromMilliseconds(4), TimeSpan.FromMilliseconds(5));
            Assert.AreEqual(TimeSpan.FromMilliseconds(5), timing.OrientationClassification);
            Assert.AreEqual(TimeSpan.FromMilliseconds(15), timing.Total);
        }

        [TestMethod]
        public void ThreeModelPipelineClassifiesEachRegionAndPreservesSourceGeometry()
        {
            VisualModelProfile detector = DetectorProfile();
            VisualModelProfile classifier = ClassifierProfile();
            VisualModelProfile recognizer = RecognizerProfile();
            int classificationCall = 0;
            var detectionProvider = new FakeVisualBackendProvider(VisualTestData.Metadata(detector, detector.Outputs[0].ShapePattern), _ => DetectionOutputs(), "fake-detector", new BackendId("fake-stage20-detector"));
            var classificationProvider = new FakeVisualBackendProvider(VisualTestData.Metadata(classifier, classifier.Outputs[0].ShapePattern), _ =>
            {
                float[] values = classificationCall++ == 0 ? new[] { .95f, .05f } : new[] { .05f, .95f };
                return InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 2), values));
            }, "fake-classifier", new BackendId("fake-stage20-classifier"));
            var recognitionProvider = new FakeVisualBackendProvider(VisualTestData.Metadata(recognizer, recognizer.Outputs[0].ShapePattern), _ => RecognitionOutputs(), "fake-recognizer", new BackendId("fake-stage20-recognizer"));

            using var registry = new BackendRegistry();
            registry.Register(detectionProvider); registry.Register(classificationProvider); registry.Register(recognitionProvider);
            var profiles = new VisualProfileRegistry(); profiles.Register(detector); profiles.Register(classifier); profiles.Register(recognizer); profiles.Freeze();
            VisualProfileSelection detSelection = Select(detector, detectionProvider, profiles, registry);
            VisualProfileSelection clsSelection = Select(classifier, classificationProvider, profiles, registry);
            VisualProfileSelection recSelection = Select(recognizer, recognitionProvider, profiles, registry);
            using var pipeline = new OcrPipeline(registry, detSelection, Request(detectionProvider), clsSelection, Request(classificationProvider),
                new TextCropProfile("tests/stage20-cls-crop", 80, OcrRecognitionWidthMode.Fixed, 160, 160), recSelection, Request(recognitionProvider),
                new TextCropProfile("tests/stage20-rec-crop", 8, OcrRecognitionWidthMode.Fixed, 16, 16), new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 2));
            using var input = new Stage20OcrInput();

            OcrResult result = pipeline.Run(input);

            Assert.AreEqual(OcrOrientationStrategy.PerTextRegion, pipeline.OrientationStrategy);
            Assert.AreEqual(TextOrientation.Degrees0, result.Regions[0].Region.Orientation);
            Assert.AreEqual(TextOrientation.Degrees180, result.Regions[1].Region.Orientation);
            Assert.AreEqual("fake-stage20-classifier", result.Regions[1].Region.Metadata["ocr.orientation.backendId"]);
            Assert.AreEqual("per-text-region", result.Regions[1].Region.Metadata["ocr.orientation.strategy"]);
            Assert.AreEqual(new PointF(50, 50), result.Regions[1].Region.Polygon.Vertices[0]);
            CollectionAssert.AreEqual(new[] { TextOrientation.Degrees0, TextOrientation.Degrees180 }, input.RecognitionOrientations.ToArray());
            CollectionAssert.AreEqual(new[] { "AB", "CA" }, result.Regions.Select(value => value.Recognition.Text).ToArray());
            Assert.AreEqual(2, classificationProvider.LastSession!.RunCount);
            Assert.IsTrue(result.Timing.OrientationClassification > TimeSpan.Zero);
        }

        [TestMethod]
        public async Task ThreeModelPipelineCancelsRunsConcurrentlyAndDisposesEverySessionOnce()
        {
            VisualModelProfile detector = DetectorProfile();
            VisualModelProfile classifier = ClassifierProfile();
            VisualModelProfile recognizer = RecognizerProfile();
            var detectionProvider = new FakeVisualBackendProvider(VisualTestData.Metadata(detector, detector.Outputs[0].ShapePattern), _ => DetectionOutputs(), "fake-detector", new BackendId("fake-stage20-lifecycle-detector"));
            var classificationProvider = new FakeVisualBackendProvider(VisualTestData.Metadata(classifier, classifier.Outputs[0].ShapePattern), _ => InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 2), new[] { .95f, .05f })), "fake-classifier", new BackendId("fake-stage20-lifecycle-classifier")) { Delay = TimeSpan.FromMilliseconds(100) };
            var recognitionProvider = new FakeVisualBackendProvider(VisualTestData.Metadata(recognizer, recognizer.Outputs[0].ShapePattern), _ => RecognitionOutputs(), "fake-recognizer", new BackendId("fake-stage20-lifecycle-recognizer"));
            using var registry = new BackendRegistry();
            registry.Register(detectionProvider); registry.Register(classificationProvider); registry.Register(recognitionProvider);
            var profiles = new VisualProfileRegistry(); profiles.Register(detector); profiles.Register(classifier); profiles.Register(recognizer); profiles.Freeze();
            var pipeline = new OcrPipeline(registry,
                Select(detector, detectionProvider, profiles, registry), Request(detectionProvider),
                Select(classifier, classificationProvider, profiles, registry), Request(classificationProvider), new TextCropProfile("tests/stage20-lifecycle-cls", 80, OcrRecognitionWidthMode.Fixed, 160, 160),
                Select(recognizer, recognitionProvider, profiles, registry), Request(recognitionProvider), new TextCropProfile("tests/stage20-lifecycle-rec", 8, OcrRecognitionWidthMode.Fixed, 16, 16),
                new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 2, maximumConcurrency: 2),
                new SessionOptions(2), new SessionOptions(2), new SessionOptions(2));

            using (var cancelledInput = new Stage20OcrInput())
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25)))
            {
                OcrPipelineException cancelled = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => pipeline.RunAsync(cancelledInput, cancellationToken: cancellation.Token));
                Assert.AreEqual(VisualErrorCodes.Cancelled, cancelled.ErrorCode);
                Assert.AreEqual(OcrPipelineStage.OrientationClassification, cancelled.Stage);
            }

            classificationProvider.Delay = TimeSpan.FromMilliseconds(10);
            using (var firstInput = new Stage20OcrInput())
            using (var secondInput = new Stage20OcrInput())
            {
                OcrResult[] results = await Task.WhenAll(pipeline.RunAsync(firstInput), pipeline.RunAsync(secondInput));
                Assert.AreEqual(2, results[0].Regions.Count);
                Assert.AreEqual(2, results[1].Regions.Count);
                Assert.AreEqual(2, classificationProvider.CreatedSessions.Count);
                Assert.IsTrue(classificationProvider.CreatedSessions.All(value => value.MaximumActive == 1));
            }

            pipeline.Dispose();
            pipeline.Dispose();
            Assert.AreEqual(1, detectionProvider.LastSession!.DisposeCount);
            Assert.AreEqual(1, classificationProvider.LastSession!.DisposeCount);
            Assert.AreEqual(1, recognitionProvider.LastSession!.DisposeCount);
            using var disposedInput = new Stage20OcrInput();
            OcrPipelineException disposed = Assert.ThrowsExactly<OcrPipelineException>(() => pipeline.Run(disposedInput));
            Assert.AreEqual(VisualErrorCodes.ObjectDisposed, disposed.ErrorCode);
            Assert.AreEqual(OcrPipelineStage.Disposal, disposed.Stage);
        }

        private static PaddleOcrArtifactContract Artifact()
        {
            return new PaddleOcrArtifactContract(7, Sha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1");
        }

        private static OcrOrientationResult Decode(float[] values, float threshold)
        {
            return Decode(Decoder(threshold), values);
        }

        private static OcrOrientationResult Decode(OcrOrientationDecoder decoder, float[] values)
        {
            return Decode(decoder, new Tensor<float>(new TensorShape(1, 2), values));
        }

        private static OcrOrientationResult Decode(OcrOrientationDecoder decoder, Tensor<float> tensor)
        {
            var size = new VisualSize(160, 80);
            using var input = new PreparedVisualInput("x", new Tensor<float>(new TensorShape(1, 3, 80, 160), new float[38400]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            var profile = new VisualModelProfile("tests/paddle-cls", new ModelId("tests/paddle-cls"), VisualTaskId.TextOrientationClassification, "1", "fake",
                new VisualInputBinding("x", TensorElementType.Float32, new TensorShape(1, 3, 80, 160), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("fetch_name_0", TensorElementType.Float32, tensor.Shape) }, Array.Empty<VisualLabel>(), decoder);
            return (OcrOrientationResult)decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create("fetch_name_0", tensor), CancellationToken.None));
        }

        private static OcrOrientationDecoder Decoder(float threshold)
        {
            return new OcrOrientationDecoder(new OcrOrientationSchema("fetch_name_0", new TensorShape(1, 2), TensorElementType.Float32,
                new[] { TextOrientation.Degrees0, TextOrientation.Degrees180 }, OcrOrientationValueSemantics.Probability, false), new OcrOrientationDecoderOptions(threshold));
        }

        private static VisualModelProfile DetectorProfile()
        {
            var decoder = new ExplicitTextDetectionDecoder(new ExplicitTextDetectionSchema("polygons", "scores", 4, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise), new TextDetectionDecoderOptions(.1f, .3f, maximumCandidates: 2, maximumRegions: 2));
            return new VisualModelProfile("tests/stage20-detector", new ModelId("tests/stage20-detector"), VisualTaskId.TextDetection, "1", "fake-detector",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("polygons", TensorElementType.Float32, new TensorShape(1, 2, 4, 2)), new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 2)) }, Array.Empty<VisualLabel>(), decoder);
        }

        private static VisualModelProfile ClassifierProfile()
        {
            var decoder = new OcrOrientationDecoder(new OcrOrientationSchema("fetch_name_0", new TensorShape(1, 2), TensorElementType.Float32, new[] { TextOrientation.Degrees0, TextOrientation.Degrees180 }, OcrOrientationValueSemantics.Probability, false), new OcrOrientationDecoderOptions(.5f));
            return new VisualModelProfile("tests/stage20-classifier", new ModelId("tests/stage20-classifier"), VisualTaskId.TextOrientationClassification, "1", "fake-classifier",
                new VisualInputBinding("x", TensorElementType.Float32, new TensorShape(1, 3, 80, 160), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("fetch_name_0", TensorElementType.Float32, new TensorShape(1, 2)) }, Array.Empty<VisualLabel>(), decoder);
        }

        private static VisualModelProfile RecognizerProfile()
        {
            var decoder = new GreedyCtcDecoder(new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses), new OcrCharacterSet("tests.stage20", "1", "ABC"), new CtcDecoderOptions(0, applySoftmax: false));
            return new VisualModelProfile("tests/stage20-recognizer", new ModelId("tests/stage20-recognizer"), VisualTaskId.TextRecognition, "1", "fake-recognizer",
                new VisualInputBinding("crops", TensorElementType.Float32, new TensorShape(2, 3, 8, 16), VisualTensorLayout.Nchw, 2, 2),
                new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(2, 6, 4)) }, Array.Empty<VisualLabel>(), decoder);
        }

        private static VisualProfileSelection Select(VisualModelProfile profile, FakeVisualBackendProvider provider, VisualProfileRegistry profiles, BackendRegistry registry)
        {
            return profiles.Select(new ModelArtifact(profile.ModelId, provider.Format, profile.ModelId.Value + ".fake", preferredBackend: provider.Descriptor.Id), registry, Request(provider), profile.Task);
        }

        private static BackendRequest Request(FakeVisualBackendProvider provider) => new BackendRequest(BackendCapabilities.TensorInference, provider.Descriptor.Id);

        private static InferenceOutputs DetectionOutputs()
        {
            return new InferenceOutputs(new[]
            {
                new NamedTensor("polygons", new Tensor<float>(new TensorShape(1, 2, 4, 2), new[] { 5f,5f, 45f,5f, 45f,25f, 5f,25f, 50f,50f, 95f,50f, 95f,80f, 50f,80f })),
                new NamedTensor("scores", new Tensor<float>(new TensorShape(1, 2), new[] { .95f, .9f }))
            });
        }

        private static InferenceOutputs RecognitionOutputs()
        {
            int[] selected = { 0,1,1,0,2,2, 3,3,0,1,1,0 };
            var values = new float[2 * 6 * 4];
            for (int index = 0; index < selected.Length; index++) values[index * 4 + selected[index]] = .9f;
            return InferenceOutputs.Create("logits", new Tensor<float>(new TensorShape(2, 6, 4), values));
        }

        private sealed class Stage20OcrInput : IOcrImageInput
        {
            private bool _disposed;
            public Stage20OcrInput()
            {
                var size = new VisualSize(100, 100);
                DetectionInput = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            }

            public VisualSize SourceSize { get; } = new VisualSize(100, 100);
            public PreparedVisualInput DetectionInput { get; }
            public List<TextOrientation> RecognitionOrientations { get; } = new List<TextOrientation>();

            public PreparedVisualInput PrepareRecognitionBatch(string inputName, IReadOnlyList<TextCropRequest> requests, CancellationToken cancellationToken)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(Stage20OcrInput));
                cancellationToken.ThrowIfCancellationRequested();
                if (inputName == "crops") foreach (TextCropRequest request in requests) RecognitionOrientations.Add(request.Region.Orientation);
                int width = requests[0].TargetWidth;
                int height = requests[0].TargetHeight;
                var size = new VisualSize(width, height);
                return new PreparedVisualInput(inputName, new Tensor<float>(new TensorShape(requests.Count, 3, height, width), new float[requests.Count * 3 * height * width]), size, size, requests.Count, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                DetectionInput.Dispose();
            }
        }
    }
}
