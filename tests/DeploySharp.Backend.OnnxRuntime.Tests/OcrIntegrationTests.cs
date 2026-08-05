using System;
using System.Collections.Generic;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    [TestClass]
    public sealed class OcrIntegrationTests
    {
        private static readonly ModelId DetectorModelId = new ModelId("tests/ocr-detector");
        private static readonly ModelId RecognizerModelId = new ModelId("tests/ocr-recognizer");

        [TestMethod]
        public void RealOnnxRuntimeCpuExecutesTwoStageOcrAndOwnsResults()
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            ModelArtifact detector = Artifact(DetectorModelId, "text-detection.onnx");
            ModelArtifact recognizer = Artifact(RecognizerModelId, "text-recognition-ctc.onnx");
            using OcrPipeline pipeline = CreatePipeline(registry, detector, recognizer, "onnx");

            string firstDigest;
            using (var input = new ManagedOcrInput())
            {
                OcrResult result = pipeline.Run(input);
                AssertResult(result);
                firstDigest = result.ComputeSha256();
            }

            // The second run proves that copied outputs outlive backend requests and the sessions remain reusable.
            // 第二次运行证明复制后的输出不依赖后端请求生命周期，并且会话可复用。
            using (var input = new ManagedOcrInput())
            {
                OcrResult result = pipeline.Run(input);
                AssertResult(result);
                Assert.AreEqual(firstDigest, result.ComputeSha256());
            }
        }

        internal static OcrPipeline CreatePipeline(BackendRegistry registry, ModelArtifact detector, ModelArtifact recognizer, string format)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(DetectorProfile(format));
            profiles.Register(RecognizerProfile(format));
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            return new OcrPipeline(
                registry,
                profiles.Select(detector, registry, request, VisualTaskId.TextDetection), request,
                profiles.Select(recognizer, registry, request, VisualTaskId.TextRecognition), request,
                CropProfile(), new OcrPipelineOptions(maximumRecognitionBatch: 2));
        }

        internal static VisualModelProfile DetectorProfile(string format)
        {
            var decoder = new ExplicitTextDetectionDecoder(
                new ExplicitTextDetectionSchema("polygons", "scores", 4, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise),
                new TextDetectionDecoderOptions(scoreThreshold: .1f, polygonIouThreshold: .3f, maximumCandidates: 3, maximumRegions: 3));
            return new VisualModelProfile(
                "tests/ocr-detector.v1", DetectorModelId, VisualTaskId.TextDetection, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,16,32), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("polygons", TensorElementType.Float32, new TensorShape(1,3,4,2)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3))
                }, Array.Empty<VisualLabel>(), decoder);
        }

        internal static VisualModelProfile RecognizerProfile(string format)
        {
            var characterSet = new OcrCharacterSet("tests.latin", "1.0", "ABC");
            var decoder = new GreedyCtcDecoder(
                new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses), characterSet,
                new CtcDecoderOptions(blankIndex: 0));
            return new VisualModelProfile(
                "tests/ocr-recognizer.v1", RecognizerModelId, VisualTaskId.TextRecognition, "1.0", format,
                new VisualInputBinding("crops", TensorElementType.Float32, new TensorShape(2,3,8,16), VisualTensorLayout.Nchw, minimumBatch: 2, maximumBatch: 2),
                new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(2,6,4)) },
                Array.Empty<VisualLabel>(), decoder);
        }

        internal static TextCropProfile CropProfile() => new TextCropProfile("tests/ocr-crop.v1", 8, OcrRecognitionWidthMode.Fixed, 16, 16);

        internal static void AssertResult(OcrResult result)
        {
            Assert.AreEqual(new VisualSize(32,16), result.SourceSize);
            Assert.AreEqual(2, result.Regions.Count);
            Assert.AreEqual(0, result.Regions[0].Region.SourceIndex);
            Assert.AreEqual("AB", result.Regions[0].Recognition.Text);
            Assert.AreEqual(2, result.Regions[1].Region.SourceIndex);
            Assert.AreEqual("CA", result.Regions[1].Recognition.Text);
            Assert.AreEqual("tests.latin", result.Regions[0].Recognition.CharacterSetId);
            Assert.AreEqual(64, result.ComputeSha256().Length);
        }

        private static ModelArtifact Artifact(ModelId id, string fixture) => new ModelArtifact(id, "onnx", OnnxRuntimeTestData.Fixture(fixture), preferredBackend: OnnxRuntimeBackendProvider.BackendId);

        internal sealed class ManagedOcrInput : IOcrImageInput
        {
            private bool _disposed;

            public ManagedOcrInput()
            {
                var size = new VisualSize(32,16);
                DetectionInput = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,16,32), new float[1536]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            }

            public VisualSize SourceSize { get; } = new VisualSize(32,16);
            public PreparedVisualInput DetectionInput { get; }

            public PreparedVisualInput PrepareRecognitionBatch(string inputName, IReadOnlyList<TextCropRequest> requests, System.Threading.CancellationToken cancellationToken)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ManagedOcrInput));
                cancellationToken.ThrowIfCancellationRequested();
                Assert.AreEqual(2, requests.Count);
                var size = new VisualSize(16,8);
                return new PreparedVisualInput(inputName, new Tensor<float>(new TensorShape(2,3,8,16), new float[768]), size, size, 2, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
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
