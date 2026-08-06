using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvOcrOrientationIntegrationTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);
        private static string Model(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", "onnx", name);

        [TestMethod]
        public void SingleDecodeRotatesOnceAndFeedsExistingOcrPipeline()
        {
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry(); profiles.Register(OrientationProfile()); profiles.Register(DetectorProfile()); profiles.Register(RecognizerProfile()); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var orientationArtifact = new ModelArtifact(new ModelId("tests/text-orientation"), "onnx", Model("text-orientation.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            var detectorArtifact = new ModelArtifact(new ModelId("tests/opencv-ocr-detector"), "onnx", Model("text-detection.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            var recognizerArtifact = new ModelArtifact(new ModelId("tests/opencv-ocr-recognizer"), "onnx", Model("text-recognition-ctc.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            using var orientation = new OcrOrientationPipeline(registry, profiles.Select(orientationArtifact, registry, request, VisualTaskId.TextOrientationClassification), request);
            using var ocr = new OcrPipeline(registry, profiles.Select(detectorArtifact, registry, request, VisualTaskId.TextDetection), request, profiles.Select(recognizerArtifact, registry, request, VisualTaskId.TextRecognition), request, CropProfile(), new OcrPipelineOptions(maximumRecognitionBatch: 2));
            using var workflow = new OcrOrientationWorkflow(orientation, ocr);
            var factory = new OpenCvOcrImageInputFactory();
            var orientationOptions = new OpenCvPreprocessOptions(new VisualSize(2, 2), OpenCvResizeMode.Resize, VisualColorOrder.Gray, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
            var detectorOptions = new OpenCvPreprocessOptions(new VisualSize(32, 16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
            using OpenCvOcrImageInput input = factory.CreateOrientationInput(OpenCvImageSource.FromFile(Fixture("ocr-orientation-180.png")), "images", orientationOptions, "images", detectorOptions);

            OcrResult result = workflow.Run(input);

            Assert.AreEqual(TextOrientation.Degrees180, result.Orientation?.AcceptedOrientation);
            Assert.AreEqual(new VisualSize(2, 2), result.OriginalSourceSize);
            CollectionAssert.AreEqual(new[] { "AB", "CA" }, result.Regions.Select(value => value.Recognition.Text).ToArray());
            Assert.AreEqual(64, result.ComputeSha256().Length);
            input.Dispose(); input.Dispose();
        }

        [TestMethod]
        public void OrientationInputHonorsPreCancellation()
        {
            var factory = new OpenCvOcrImageInputFactory();
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), OpenCvResizeMode.Resize, VisualColorOrder.Gray, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
            Assert.AreEqual(OpenCvErrorCodes.Cancelled, Assert.ThrowsExactly<OpenCvVisualException>(() => factory.CreateOrientationInput(OpenCvImageSource.FromFile(Fixture("ocr-orientation-180.png")), "images", options, "images", options, cancellationToken: cancelled.Token)).ErrorCode);
        }

        [TestMethod]
        public void CorrectionRejectsAnOrientationResultFromAnotherSourceSize()
        {
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), OpenCvResizeMode.Resize, VisualColorOrder.Gray, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateOrientationInput(OpenCvImageSource.FromFile(Fixture("ocr-orientation-180.png")), "images", options, "images", options);
            var foreign = new OcrOrientationResult(TextOrientation.Degrees180, 3, .9f, new[] { .01f, .02f, .07f, .9f }, false, "tests/foreign", new ModelId("tests/foreign"), OnnxRuntimeBackendProvider.BackendId, new VisualSize(3, 2), new VisualSize(2, 2), TimeSpan.Zero);
            Assert.AreEqual(OpenCvErrorCodes.PreprocessInvalid, Assert.ThrowsExactly<OpenCvVisualException>(() => input.CreateOriented(foreign)).ErrorCode);
        }

        [TestMethod]
        public void FourGeneratedImagesSelectTheirExplicitCorrectionAngles()
        {
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry(); profiles.Register(OrientationProfile()); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var artifact = new ModelArtifact(new ModelId("tests/text-orientation"), "onnx", Model("text-orientation.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            using var pipeline = new OcrOrientationPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.TextOrientationClassification), request);
            var factory = new OpenCvOcrImageInputFactory();
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), OpenCvResizeMode.Resize, VisualColorOrder.Gray, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
            string[] files = { "ocr-orientation-0.png", "ocr-orientation-90.png", "ocr-orientation-180.png", "ocr-orientation-270.png" };
            TextOrientation[] expected = { TextOrientation.Degrees0, TextOrientation.CounterClockwise90, TextOrientation.Degrees180, TextOrientation.Clockwise90 };
            for (int index = 0; index < files.Length; index++)
            {
                using OpenCvOcrImageInput input = factory.CreateOrientationInput(OpenCvImageSource.FromFile(Fixture(files[index])), "images", options, "images", options);
                Assert.AreEqual(expected[index], pipeline.Run(input.DetectionInput).AcceptedOrientation, files[index]);
            }
        }

        [TestMethod]
        public void PerformanceEntryRecordsOrientationRotationOcrAndEndToEndP50P95()
        {
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry(); profiles.Register(OrientationProfile()); profiles.Register(DetectorProfile()); profiles.Register(RecognizerProfile()); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var orientationArtifact = new ModelArtifact(new ModelId("tests/text-orientation"), "onnx", Model("text-orientation.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            var detectorArtifact = new ModelArtifact(new ModelId("tests/opencv-ocr-detector"), "onnx", Model("text-detection.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            var recognizerArtifact = new ModelArtifact(new ModelId("tests/opencv-ocr-recognizer"), "onnx", Model("text-recognition-ctc.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            using var orientationPipeline = new OcrOrientationPipeline(registry, profiles.Select(orientationArtifact, registry, request, VisualTaskId.TextOrientationClassification), request);
            using var ocrPipeline = new OcrPipeline(registry, profiles.Select(detectorArtifact, registry, request, VisualTaskId.TextDetection), request, profiles.Select(recognizerArtifact, registry, request, VisualTaskId.TextRecognition), request, CropProfile(), new OcrPipelineOptions(maximumRecognitionBatch: 2));
            var factory = new OpenCvOcrImageInputFactory();
            var orientationOptions = new OpenCvPreprocessOptions(new VisualSize(2, 2), OpenCvResizeMode.Resize, VisualColorOrder.Gray, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
            var detectorOptions = new OpenCvPreprocessOptions(new VisualSize(32, 16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
            var decode = new List<TimeSpan>(); var classify = new List<TimeSpan>(); var rotate = new List<TimeSpan>(); var ocr = new List<TimeSpan>(); var total = new List<TimeSpan>();
            string? canonical = null;
            RunOnce(false);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10; index++) RunOnce(true);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            TestContext.WriteLine($"OCR_ORIENTATION_PERF iterations=10 decodeP50Ms={Percentile(decode,.50).TotalMilliseconds:F3} decodeP95Ms={Percentile(decode,.95).TotalMilliseconds:F3} orientationP50Ms={Percentile(classify,.50).TotalMilliseconds:F3} orientationP95Ms={Percentile(classify,.95).TotalMilliseconds:F3} rotationP50Ms={Percentile(rotate,.50).TotalMilliseconds:F3} rotationP95Ms={Percentile(rotate,.95).TotalMilliseconds:F3} ocrP50Ms={Percentile(ocr,.50).TotalMilliseconds:F3} ocrP95Ms={Percentile(ocr,.95).TotalMilliseconds:F3} endToEndP50Ms={Percentile(total,.50).TotalMilliseconds:F3} endToEndP95Ms={Percentile(total,.95).TotalMilliseconds:F3} allocatedBytes={allocated} canonicalSha256={canonical}");
            Assert.AreEqual(64, canonical?.Length);

            void RunOnce(bool record)
            {
                var totalWatch = Stopwatch.StartNew();
                var watch = Stopwatch.StartNew();
                using OpenCvOcrImageInput input = factory.CreateOrientationInput(OpenCvImageSource.FromFile(Fixture("ocr-orientation-180.png")), "images", orientationOptions, "images", detectorOptions);
                TimeSpan decodeElapsed = watch.Elapsed;
                watch.Restart();
                OcrOrientationResult orientation = orientationPipeline.Run(input.DetectionInput);
                TimeSpan classifyElapsed = watch.Elapsed;
                watch.Restart();
                using IOcrImageInput corrected = input.CreateOriented(orientation);
                TimeSpan rotateElapsed = watch.Elapsed;
                watch.Restart();
                OcrResult result = ocrPipeline.RunWithOrientation(corrected, orientation);
                TimeSpan ocrElapsed = watch.Elapsed;
                totalWatch.Stop();
                string hash = result.ComputeSha256();
                if (canonical == null) canonical = hash; else Assert.AreEqual(canonical, hash);
                if (!record) return;
                decode.Add(decodeElapsed); classify.Add(classifyElapsed); rotate.Add(rotateElapsed); ocr.Add(ocrElapsed); total.Add(totalWatch.Elapsed);
            }
        }

        private static TimeSpan Percentile(List<TimeSpan> values, double percentile)
        {
            long[] ticks = values.Select(value => value.Ticks).OrderBy(value => value).ToArray();
            int index = Math.Min(ticks.Length - 1, (int)Math.Ceiling(percentile * ticks.Length) - 1);
            return TimeSpan.FromTicks(ticks[Math.Max(0, index)]);
        }

        private static VisualModelProfile OrientationProfile()
        {
            var decoder = new OcrOrientationDecoder(new OcrOrientationSchema("orientation_scores", new TensorShape(1, 4), TensorElementType.Float32, new[] { TextOrientation.Degrees0, TextOrientation.CounterClockwise90, TextOrientation.Clockwise90, TextOrientation.Degrees180 }));
            return new VisualModelProfile("tests/text-orientation.v1", new ModelId("tests/text-orientation"), VisualTaskId.TextOrientationClassification, "1.0", "onnx", new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 1, 2, 2), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("orientation_scores", TensorElementType.Float32, new TensorShape(1, 4)) }, Array.Empty<VisualLabel>(), decoder);
        }

        private static VisualModelProfile DetectorProfile()
        {
            var decoder = new ExplicitTextDetectionDecoder(new ExplicitTextDetectionSchema("polygons", "scores", 4, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise), new TextDetectionDecoderOptions(scoreThreshold: .1f, polygonIouThreshold: .3f, maximumCandidates: 3, maximumRegions: 3));
            return new VisualModelProfile("tests/opencv-ocr-detector.v1", new ModelId("tests/opencv-ocr-detector"), VisualTaskId.TextDetection, "1.0", "onnx", new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 16, 32), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("polygons", TensorElementType.Float32, new TensorShape(1, 3, 4, 2)), new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) }, Array.Empty<VisualLabel>(), decoder);
        }

        private static VisualModelProfile RecognizerProfile()
        {
            var decoder = new GreedyCtcDecoder(new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses), new OcrCharacterSet("tests.latin", "1.0", "ABC"), new CtcDecoderOptions(blankIndex: 0));
            return new VisualModelProfile("tests/opencv-ocr-recognizer.v1", new ModelId("tests/opencv-ocr-recognizer"), VisualTaskId.TextRecognition, "1.0", "onnx", new VisualInputBinding("crops", TensorElementType.Float32, new TensorShape(2, 3, 8, 16), VisualTensorLayout.Nchw, 2, 2), new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(2, 6, 4)) }, Array.Empty<VisualLabel>(), decoder);
        }

        private static TextCropProfile CropProfile() => new TextCropProfile("tests/opencv-ocr-crop.v1", 8, OcrRecognitionWidthMode.Fixed, 16, 16);
    }
}
