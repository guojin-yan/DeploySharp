using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    public sealed class OpenCvOcrIntegrationTests
    {
        private static readonly ModelId DetectorModelId = new ModelId("tests/opencv-ocr-detector");
        private static readonly ModelId RecognizerModelId = new ModelId("tests/opencv-ocr-recognizer");
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);
        private static string Onnx(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", "onnx", name);

        [TestMethod]
        public void RealPngOpenCvAndOnnxRuntimeExecuteCompleteOcrPipeline()
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            ModelArtifact detector = new ModelArtifact(DetectorModelId, "onnx", Onnx("text-detection.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            ModelArtifact recognizer = new ModelArtifact(RecognizerModelId, "onnx", Onnx("text-recognition-ctc.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            var profiles = new VisualProfileRegistry();
            profiles.Register(DetectorProfile());
            profiles.Register(RecognizerProfile());
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var pipeline = new OcrPipeline(
                registry,
                profiles.Select(detector, registry, request, VisualTaskId.TextDetection), request,
                profiles.Select(recognizer, registry, request, VisualTaskId.TextRecognition), request,
                CropProfile(), new OcrPipelineOptions(maximumRecognitionBatch: 2));
            var detectorOptions = new OpenCvPreprocessOptions(new VisualSize(32,16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(Fixture("ocr.png"), "images", detectorOptions);

            OcrResult result = pipeline.Run(input);

            Assert.AreEqual(new VisualSize(32,16), result.SourceSize);
            CollectionAssert.AreEqual(new[] { "AB", "CA" }, result.Regions.Select(item => item.Recognition.Text).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 2 }, result.Regions.Select(item => item.Region.SourceIndex).ToArray());
            Assert.AreEqual(64, result.ComputeSha256().Length);
        }

        [TestMethod]
        public void PerspectiveCropSupportsAllExplicitRightAngleOrientationsAndOwnsTensor()
        {
            var detectorOptions = new OpenCvPreprocessOptions(new VisualSize(32,16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(Fixture("ocr.png"), "images", detectorOptions);
            var digests = new HashSet<string>(StringComparer.Ordinal);
            foreach (TextOrientation orientation in new[] { TextOrientation.Degrees0, TextOrientation.Clockwise90, TextOrientation.Degrees180, TextOrientation.CounterClockwise90 })
            {
                TextCropRequest request = Request(orientation, CropProfile());
                using PreparedVisualInput batch = input.PrepareRecognitionBatch("crops", new[] { request }, CancellationToken.None);
                Assert.AreEqual(new TensorShape(1,3,8,16), batch.Tensor.Shape);
                float[] values = ((Tensor<float>)batch.Tensor).ToArray();
                Assert.IsTrue(values.Any(value => value != 0));
                digests.Add(Sha256(values));
            }
            Assert.AreEqual(4, digests.Count, "The asymmetric fixture must make every configured orientation observable.");
        }

        [TestMethod]
        public void CropBatchHonorsNormalizationCancellationAndDisposal()
        {
            var detectorOptions = new OpenCvPreprocessOptions(new VisualSize(32,16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            var input = new OpenCvOcrImageInputFactory().CreateFromFile(Fixture("ocr.png"), "images", detectorOptions);
            var normalized = new TextCropProfile(
                "tests/opencv-ocr-normalized.v1", 8, OcrRecognitionWidthMode.Fixed, 16, 16,
                colorOrder: VisualColorOrder.Bgr, means: new[] { 10f,20f,30f }, scales: new[] { .5f,.25f,.125f });
            using (PreparedVisualInput batch = input.PrepareRecognitionBatch("crops", new[] { Request(TextOrientation.Degrees0, normalized) }, CancellationToken.None))
            {
                float[] values = ((Tensor<float>)batch.Tensor).ToArray();
                Assert.IsTrue(values.Any(value => value < 0));
                Assert.IsTrue(values.Any(value => value > 0));
            }

            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                OpenCvVisualException exception = Assert.ThrowsExactly<OpenCvVisualException>(() => input.PrepareRecognitionBatch("crops", new[] { Request(TextOrientation.Degrees0, normalized) }, cancelled.Token));
                Assert.AreEqual(OpenCvErrorCodes.Cancelled, exception.ErrorCode);
            }

            input.Dispose();
            input.Dispose();
            OpenCvVisualException disposed = Assert.ThrowsExactly<OpenCvVisualException>(() => input.PrepareRecognitionBatch("crops", new[] { Request(TextOrientation.Degrees0, normalized) }, CancellationToken.None));
            Assert.AreEqual(OpenCvErrorCodes.ObjectDisposed, disposed.ErrorCode);
        }

        [TestMethod]
        public void RecognitionTensorPoolReusesSequentialBuffersAndIsolatesActiveLeases()
        {
            var detectorOptions = new OpenCvPreprocessOptions(new VisualSize(32,16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            var input = new OpenCvOcrImageInputFactory().CreateFromFile(Fixture("ocr.png"), "images", detectorOptions);
            TextCropRequest request = Request(TextOrientation.Degrees0, CropProfile());
            float[] firstBuffer;
            string firstDigest;
            using (PreparedVisualInput first = input.PrepareRecognitionBatch("crops", new[] { request }, CancellationToken.None))
            {
                firstBuffer = (float[])first.Tensor.Buffer;
                firstDigest = Sha256(firstBuffer);
            }

            PreparedVisualInput active = input.PrepareRecognitionBatch("crops", new[] { request }, CancellationToken.None);
            Assert.AreSame(firstBuffer, active.Tensor.Buffer, "A disposed sequential batch should return its exact-sized tensor buffer to the image-local pool.");
            Assert.AreEqual(firstDigest, Sha256((float[])active.Tensor.Buffer));
            using (PreparedVisualInput concurrent = input.PrepareRecognitionBatch("crops", new[] { request }, CancellationToken.None))
            {
                Assert.AreNotSame(active.Tensor.Buffer, concurrent.Tensor.Buffer, "Simultaneously active batches must never share a writable tensor buffer.");
                Assert.AreEqual(firstDigest, Sha256((float[])concurrent.Tensor.Buffer));
            }

            input.Dispose();
            active.Dispose();
        }

        [TestMethod]
        public void DynamicOddWidthHandlesVerticalThinAndOutsideSourceCrops()
        {
            var detectorOptions = new OpenCvPreprocessOptions(new VisualSize(32,16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(Fixture("ocr.png"), "images", detectorOptions);
            var dynamicProfile = new TextCropProfile("tests/opencv-ocr-dynamic.v1", 7, OcrRecognitionWidthMode.Dynamic, 9, 17, widthAlignment: 2, paddingColor: new TextCropColor(1,2,3));
            var verticalCorners = new TextQuadrilateral(new PointF(20,1), new PointF(24,1), new PointF(24,15), new PointF(20,15), TextCornerOrder.TopLeftClockwise);
            var vertical = new TextCropRequest(new TextRegion(0,.9f,verticalCorners.Polygon,verticalCorners,TextOrientation.Clockwise90), dynamicProfile);
            Assert.AreEqual(17, vertical.TargetWidth);
            using (PreparedVisualInput batch = input.PrepareRecognitionBatch("crops", new[] { vertical }, CancellationToken.None))
            {
                Assert.AreEqual(new TensorShape(1,3,7,17), batch.Tensor.Shape);
                Assert.IsTrue(((Tensor<float>)batch.Tensor).ToArray().Any(value => value != 0));
            }

            var outsideCorners = new TextQuadrilateral(new PointF(-2,2), new PointF(5,2), new PointF(5,3), new PointF(-2,3), TextCornerOrder.TopLeftClockwise);
            var outside = new TextCropRequest(new TextRegion(1,.8f,outsideCorners.Polygon,outsideCorners), dynamicProfile);
            using PreparedVisualInput padded = input.PrepareRecognitionBatch("crops", new[] { outside }, CancellationToken.None);
            Assert.AreEqual(17, padded.ModelSize.Width);
            Assert.AreEqual(7, padded.ModelSize.Height);
        }

        [TestMethod]
        public void SlidingWindowsCropCorrectColorAxesAfterEveryRightAngleOrientation()
        {
            foreach (TextOrientation orientation in Enum.GetValues<TextOrientation>())
            {
                bool vertical = orientation == TextOrientation.Clockwise90 || orientation == TextOrientation.CounterClockwise90;
                int width = vertical ? 40 : 160;
                int height = vertical ? 160 : 40;
                byte[] header = Encoding.ASCII.GetBytes("P6\n" + width + " " + height + "\n255\n");
                var ppm = new byte[header.Length + width * height * 3];
                Buffer.BlockCopy(header, 0, ppm, 0, header.Length);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int offset = header.Length + (y * width + x) * 3;
                        ppm[offset] = (byte)(x * 200 / (width - 1));
                        ppm[offset + 1] = 100;
                        ppm[offset + 2] = (byte)(y * 200 / (height - 1));
                    }
                using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().Create(OpenCvImageSource.FromBytes(ppm), "images",
                    new OpenCvPreprocessOptions(new VisualSize(32, 16)));
                var quad = new TextQuadrilateral(new PointF(0, 0), new PointF(width - 1, 0), new PointF(width - 1, height - 1), new PointF(0, height - 1), TextCornerOrder.TopLeftClockwise);
                var region = new TextRegion(0, .9f, quad.Polygon, quad, orientation);
                var profile = new TextCropProfile("tests/native-windows", 40, OcrRecognitionWidthMode.Dynamic, 64, 64,
                    interpolation: TextCropInterpolation.Nearest).WithRecognitionOverflowMode(RecognitionOverflowMode.SlidingWindow);
                foreach (OcrRecognitionWindow window in OcrRecognitionWindowPlanner.Plan(region, profile))
                {
                    using PreparedVisualInput batch = input.PrepareRecognitionBatch("crops", new[] { window.Crop }, CancellationToken.None);
                    float[] data = ((Tensor<float>)batch.Tensor).ToArray();
                    int pixels = window.Crop.TargetWidth * window.Crop.TargetHeight;
                    int center = (window.Crop.TargetHeight / 2) * window.Crop.TargetWidth + window.Crop.TargetWidth / 2;
                    double u = (window.Start + window.End) * .5;
                    double expectedX = orientation == TextOrientation.Degrees0 ? u : orientation == TextOrientation.Degrees180 ? 1 - u : .5;
                    double expectedY = orientation == TextOrientation.Clockwise90 ? 1 - u : orientation == TextOrientation.CounterClockwise90 ? u : .5;
                    Assert.AreEqual(expectedX * 200, data[center], 8, orientation + "/red");
                    Assert.AreEqual(100f, data[pixels + center]);
                    Assert.AreEqual(expectedY * 200, data[2 * pixels + center], 8, orientation + "/blue");
                }
            }
        }

        [TestMethod]
        public void ArbitraryAngleAndPerspectiveCropsSampleTheOriginalImageCoordinates()
        {
            const int side = 256;
            byte[] header = Encoding.ASCII.GetBytes("P6\n256 256\n255\n");
            var ppm = new byte[header.Length + side * side * 3];
            Buffer.BlockCopy(header, 0, ppm, 0, header.Length);
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                int offset = header.Length + (y * side + x) * 3;
                ppm[offset] = (byte)x; ppm[offset + 1] = 100; ppm[offset + 2] = (byte)y;
            }
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().Create(OpenCvImageSource.FromBytes(ppm), "images", new OpenCvPreprocessOptions(new VisualSize(32, 32)));
            var profile = new TextCropProfile("tests/native-angle", 20, OcrRecognitionWidthMode.Dynamic, 120, 160,
                interpolation: TextCropInterpolation.Linear).WithGeometryValidation(new OcrGeometryOptions());
            var quads = new List<TextQuadrilateral>();
            foreach (int degrees in new[] { -45, -30, -15, -5, 0, 5, 15, 30, 45, 90, 180 })
            {
                double angle = degrees * Math.PI / 180;
                PointF Rotate(double x, double y) => new PointF((float)(128 + x * Math.Cos(angle) - y * Math.Sin(angle)), (float)(128 + x * Math.Sin(angle) + y * Math.Cos(angle)));
                quads.Add(new TextQuadrilateral(Rotate(-60, -10), Rotate(60, -10), Rotate(60, 10), Rotate(-60, 10), TextCornerOrder.TopLeftClockwise));
            }
            quads.Add(new TextQuadrilateral(new PointF(50, 80), new PointF(200, 70), new PointF(170, 120), new PointF(80, 125), TextCornerOrder.TopLeftClockwise));
            foreach (TextQuadrilateral quad in quads)
            {
                var region = new TextRegion(0, 1, quad.Polygon, quad);
                var request = new TextCropRequest(region, profile);
                OcrGeometryDiagnostics diagnostic = OcrGeometryAnalyzer.Analyze(region, input.SourceSize);
                Assert.AreEqual(OcrGeometryRisk.None, diagnostic.Risks);
                using PreparedVisualInput prepared = input.PrepareRecognitionBatch("crops", new[] { request }, CancellationToken.None);
                float[] data = ((Tensor<float>)prepared.Tensor).ToArray();
                int plane = request.TargetWidth * request.TargetHeight;
                ImageTransform transform = ImageTransform.Perspective(input.SourceSize, new VisualSize(1, 1),
                    new[] { quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft },
                    new[] { new PointF(0, 0), new PointF(1, 0), new PointF(1, 1), new PointF(0, 1) });
                foreach (double u in new[] { .25, .75 }) foreach (double v in new[] { .25, .75 })
                {
                    int x = (int)(u * (request.TargetWidth - 1)), y = (int)(v * (request.TargetHeight - 1));
                    PointF expected = transform.ToSource(new PointF((float)x / (request.TargetWidth - 1), (float)y / (request.TargetHeight - 1)));
                    int pixel = y * request.TargetWidth + x;
                    Assert.AreEqual(expected.X, data[pixel], 4, "source X after warp/resize");
                    Assert.AreEqual(100, data[plane + pixel], .01);
                    Assert.AreEqual(expected.Y, data[2 * plane + pixel], 4, "source Y after warp/resize");
                }
            }
        }

        [TestMethod]
        public void AffineCropModeReportsTheSelectedSafeFastPathAndFallsBackForPerspective()
        {
            var detectorOptions = new OpenCvPreprocessOptions(new VisualSize(32,16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(Fixture("ocr.png"), "images", detectorOptions);
            TextCropProfile affineProfile = CropProfile().WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent);

            var parallelogram = new TextQuadrilateral(new PointF(2, 2), new PointF(14, 2), new PointF(16, 8), new PointF(4, 8), TextCornerOrder.TopLeftClockwise);
            using (PreparedVisualInput affine = input.PrepareRecognitionBatch("crops", new[] { new TextCropRequest(new TextRegion(0, 1, parallelogram.Polygon, parallelogram), affineProfile) }, CancellationToken.None))
            {
                StringAssert.Contains(affine.Preprocessing.Notes, "cropTransform=Affine");
                Assert.IsTrue(((Tensor<float>)affine.Tensor).ToArray().Any(value => value != 0));
            }

            var trapezoid = new TextQuadrilateral(new PointF(2, 2), new PointF(14, 2), new PointF(15, 8), new PointF(4, 8), TextCornerOrder.TopLeftClockwise);
            using (PreparedVisualInput perspective = input.PrepareRecognitionBatch("crops", new[] { new TextCropRequest(new TextRegion(1, 1, trapezoid.Polygon, trapezoid), affineProfile) }, CancellationToken.None))
            {
                StringAssert.Contains(perspective.Preprocessing.Notes, "cropTransform=Perspective");
                Assert.IsTrue(((Tensor<float>)perspective.Tensor).ToArray().Any(value => value != 0));
            }
        }

        private static TextCropRequest Request(TextOrientation orientation, TextCropProfile profile)
        {
            var corners = new TextQuadrilateral(new PointF(2,2), new PointF(14,2), new PointF(14,6), new PointF(2,6), TextCornerOrder.TopLeftClockwise);
            return new TextCropRequest(new TextRegion(0, .95f, corners.Polygon, corners, orientation), profile);
        }

        private static TextCropProfile CropProfile() => new TextCropProfile("tests/opencv-ocr-crop.v1", 8, OcrRecognitionWidthMode.Fixed, 16, 16);

        private static VisualModelProfile DetectorProfile()
        {
            var decoder = new ExplicitTextDetectionDecoder(
                new ExplicitTextDetectionSchema("polygons", "scores", 4, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise),
                new TextDetectionDecoderOptions(scoreThreshold: .1f, polygonIouThreshold: .3f, maximumCandidates: 3, maximumRegions: 3));
            return new VisualModelProfile(
                "tests/opencv-ocr-detector.v1", DetectorModelId, VisualTaskId.TextDetection, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,16,32), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("polygons", TensorElementType.Float32, new TensorShape(1,3,4,2)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3))
                }, Array.Empty<VisualLabel>(), decoder);
        }

        private static VisualModelProfile RecognizerProfile()
        {
            var decoder = new GreedyCtcDecoder(
                new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses),
                new OcrCharacterSet("tests.latin", "1.0", "ABC"),
                new CtcDecoderOptions(blankIndex: 0));
            return new VisualModelProfile(
                "tests/opencv-ocr-recognizer.v1", RecognizerModelId, VisualTaskId.TextRecognition, "1.0", "onnx",
                new VisualInputBinding("crops", TensorElementType.Float32, new TensorShape(2,3,8,16), VisualTensorLayout.Nchw, minimumBatch: 2, maximumBatch: 2),
                new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(2,6,4)) },
                Array.Empty<VisualLabel>(), decoder);
        }

        private static string Sha256(float[] values)
        {
            byte[] bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using SHA256 sha = SHA256.Create();
            var text = new StringBuilder(64);
            foreach (byte value in sha.ComputeHash(bytes)) text.Append(value.ToString("x2"));
            return text.ToString();
        }
    }
}
