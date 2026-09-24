using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>Runs the real PP-OCRv5 det→crop→cls→rec→merge pipeline through OpenCV DNN. / 使用 OpenCV DNN 运行真实 PP-OCRv5 det→crop→cls→rec→merge 全流程。</summary>
    [TestClass]
    public sealed class PaddleOcrOpenCvFullPipelineIntegrationTests
    {
        private const string DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";
        private static readonly BackendId OpenCvBackend = OpenCvDnnBackendProvider.BackendId;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialPpOcrV5MobileRunsCompleteOpenCvPipeline()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_OPENCV_RUN_EXTERNAL=1 to run the real OpenCV PaddleOCR pipeline.");
            }

            string imagePath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_IMAGE") ?? @"E:\Data\ocr\demo_1.jpg");
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? @"E:\Model\paddleocr\PP-OCRv5";
            string detectorPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_DET") ?? Path.Combine(root, "PP-OCRv5_mobile_det.onnx"));
            string classifierPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_CLS") ?? Path.Combine(root, "PP-OCRv5_mobile_cls.onnx"));
            string recognizerPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_REC") ?? Path.Combine(root, "PP-OCRv5_mobile_rec.onnx"));
            string dictionaryPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_DICT") ?? Path.Combine(root, "ppocrv5_dict.txt"));

            PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(new ModelId("external/opencv-ppocrv5-mobile-det"), Artifact(11, Sha256(detectorPath), "ppocr-det-resize-long960-stride128-f32-v2", "ppocr-db-contour-minarea-unclip-v2"));
            PaddleOcrProfile classifier = PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("external/opencv-ppocrv5-mobile-cls"), Artifact(7, Sha256(classifierPath), "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1"));
            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "external.ppocrv5", "v5", true, DictionarySha);
            PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(new ModelId("external/opencv-ppocrv5-mobile-rec"), Artifact(7, Sha256(recognizerPath), "ppocr-rec-bgr-half-range-h48-v1", "ppocr-ctc-probability-greedy-v1", DictionarySha), characters, maximumBatch: 1);

            VisualSize sourceSize;
            using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr))) sourceSize = probe.SourceSize;
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
            // PP-OCRv5 mobile exports a full-resolution probability map for this input. OpenCV's
            // backend contract permits one wildcard output axis, so bind H from the prepared input
            // instead of guessing a downsampled height (the old 128 value reshaped 512x512 values).
            long detectorOutputHeight = input.DetectionInput.Tensor.Shape[2];

            var detectorContract = new OpenCvDnnModelContract(detector.VisualProfile.ModelId,
                new[] { new TensorDescriptor("x", TensorElementType.Float32, new TensorShape(1, 3, -1, -1)) },
                new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(1, 1, detectorOutputHeight, -1)) });
            var classifierContract = new OpenCvDnnModelContract(classifier.VisualProfile.ModelId,
                new[] { new TensorDescriptor("x", TensorElementType.Float32, new TensorShape(1, 3, 80, 160)) },
                new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(1, 2)) });
            int classes = ((GreedyCtcDecoder)recognizer.VisualProfile.Decoder).ExpectedClassCount;
            var recognizerContract = new OpenCvDnnModelContract(recognizer.VisualProfile.ModelId,
                new[] { new TensorDescriptor("x", TensorElementType.Float32, new TensorShape(1, 3, 48, -1)) },
                new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(1, -1, classes)) });

            using var detectorRegistry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(detectorContract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
            using var classifierRegistry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(classifierContract, enableFusion: true, enableWinograd: true));
            using var recognizerRegistry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(recognizerContract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
            var profiles = new VisualProfileRegistry();
            profiles.Register(detector.VisualProfile); profiles.Register(classifier.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvBackend, "cpu");
            using var pipeline = new OcrPipeline(
                detectorRegistry,
                profiles.Select(detector.CreateArtifact(detectorPath, OpenCvBackend), detectorRegistry, request, VisualTaskId.TextDetection), request,
                classifierRegistry,
                profiles.Select(classifier.CreateArtifact(classifierPath, OpenCvBackend), classifierRegistry, request, VisualTaskId.TextOrientationClassification), request,
                classifier.CropProfile!,
                recognizerRegistry,
                profiles.Select(recognizer.CreateArtifact(recognizerPath, OpenCvBackend), recognizerRegistry, request, VisualTaskId.TextRecognition), request,
                recognizer.CropProfile!,
                new OcrPipelineOptions(maximumRegions: 32, maximumRecognitionBatch: 1),
                orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);
            LogDetectorBackendParity(input, detector, detectorPath, detectorContract, imagePath);

            Stopwatch watch = Stopwatch.StartNew();
            OcrResult result = pipeline.Run(input);
            watch.Stop();
            Assert.IsTrue(result.Regions.Count > 0, "OpenCV DNN did not return any detected text regions.");
            Assert.IsTrue(result.Regions.Any(region => region.Recognition != null), "The detector/crop/classifier/recognizer/merge chain did not produce recognition output.");

            using var ortRegistry = new BackendRegistry().UseOnnxRuntime();
            BackendRequest ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var ortPipeline = new OcrPipeline(
                ortRegistry,
                profiles.Select(detector.CreateArtifact(detectorPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, VisualTaskId.TextDetection), ortRequest,
                profiles.Select(classifier.CreateArtifact(classifierPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, VisualTaskId.TextOrientationClassification), ortRequest,
                classifier.CropProfile!,
                profiles.Select(recognizer.CreateArtifact(recognizerPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, VisualTaskId.TextRecognition), ortRequest,
                recognizer.CropProfile!,
                new OcrPipelineOptions(maximumRegions: 32, maximumRecognitionBatch: 1),
                orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);
            OcrResult ortResult = ortPipeline.Run(input);
            Assert.AreEqual(ortResult.Regions.Count, result.Regions.Count, "OpenCV and ORT full pipelines returned different region counts.");
            for (int index = 0; index < ortResult.Regions.Count; index++)
            {
                OcrRegionResult expected = ortResult.Regions[index];
                OcrRegionResult actual = result.Regions[index];
                Assert.AreEqual(expected.Recognition.Text, actual.Recognition.Text, "OpenCV and ORT recognition text differs at region " + index + ".");
                Assert.AreEqual(expected.Recognition.Confidence, actual.Recognition.Confidence, 0.01f, "OpenCV and ORT recognition confidence differs at region " + index + ".");
                for (int vertex = 0; vertex < expected.Region.Polygon.Vertices.Count; vertex++)
                {
                    Assert.AreEqual(expected.Region.Polygon.Vertices[vertex].X, actual.Region.Polygon.Vertices[vertex].X, 0.5f, "OpenCV and ORT polygon X differs at region/vertex " + index + "/" + vertex + ".");
                    Assert.AreEqual(expected.Region.Polygon.Vertices[vertex].Y, actual.Region.Polygon.Vertices[vertex].Y, 0.5f, "OpenCV and ORT polygon Y differs at region/vertex " + index + "/" + vertex + ".");
                }
            }
            Console.WriteLine("PADDLEOCR_OPENCV_FULL_PIPELINE image=" + imagePath + ";imageSha=" + Sha256(imagePath) + ";detectorSha=" + Sha256(detectorPath) + ";classifierSha=" + Sha256(classifierPath) + ";recognizerSha=" + Sha256(recognizerPath) + ";dictionarySha=" + DictionarySha + ";regions=" + result.Regions.Count.ToString(CultureInfo.InvariantCulture) + ";ortRegions=" + ortResult.Regions.Count.ToString(CultureInfo.InvariantCulture) + ";elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";resultSha=" + result.ComputeSha256() + ";ortResultSha=" + ortResult.ComputeSha256());
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialPpOcrV5MobileMatchesOrtAcrossThreeLocalImages()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_OPENCV_RUN_EXTERNAL=1 to run the multi-image OpenCV/ORT parity test.");
            string? previous = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_IMAGE");
            try
            {
                foreach (string image in new[] { @"E:\Data\ocr\demo_1.jpg", @"E:\Data\ocr\demo_2.jpg", @"E:\Data\ocr\demo_3.jpg" })
                {
                    if (!File.Exists(image)) Assert.Inconclusive("Missing multi-image OCR input: " + image);
                    Environment.SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_IMAGE", image);
                    OfficialPpOcrV5MobileRunsCompleteOpenCvPipeline();
                }
            }
            finally { Environment.SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_IMAGE", previous); }
        }

        private static void LogDetectorBackendParity(OpenCvOcrImageInput input, PaddleOcrProfile detector, string detectorPath, OpenCvDnnModelContract detectorContract, string sourceImagePath)
        {
            BackendRequest openCvRequest = new BackendRequest(BackendCapabilities.TensorInference, OpenCvBackend, "cpu");
            BackendRequest ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var openCvRegistry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(detectorContract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
            using var ortRegistry = new BackendRegistry().UseOnnxRuntime();
            using IInferenceSession openCvSession = openCvRegistry.CreateSession(detector.CreateArtifact(detectorPath, OpenCvBackend), openCvRequest, new SessionOptions(1, false));
            using IInferenceSession ortSession = ortRegistry.CreateSession(detector.CreateArtifact(detectorPath, OnnxRuntimeBackendProvider.BackendId), ortRequest, new SessionOptions(1, false));

            PreparedVisualInput prepared = input.DetectionInput;
            InferenceInputs inferenceInput = InferenceInputs.Create(detector.VisualProfile.Input.Name, prepared.Tensor);
            InferenceOutputs openCvOutputs = openCvSession.Run(inferenceInput, CancellationToken.None);
            InferenceOutputs ortOutputs = ortSession.Run(inferenceInput, CancellationToken.None);
            ITensor openCvMap = openCvOutputs.GetRequired(detector.VisualProfile.Outputs[0].Name);
            ITensor ortMap = ortOutputs.GetRequired(detector.VisualProfile.Outputs[0].Name);
            Assert.AreEqual(ortMap.Shape.ToString(), openCvMap.Shape.ToString(), "The detector backends returned different probability-map shapes for the identical prepared tensor.");
            var openCvDecoded = (TextDetectionResult)detector.VisualProfile.Decoder.Decode(new VisualDecodeContext(prepared, detector.VisualProfile, openCvOutputs, CancellationToken.None));
            var ortDecoded = (TextDetectionResult)detector.VisualProfile.Decoder.Decode(new VisualDecodeContext(prepared, detector.VisualProfile, ortOutputs, CancellationToken.None));
            Assert.AreEqual(ortDecoded.Regions.Count, openCvDecoded.Regions.Count, "The detector backends returned different region counts for the identical prepared tensor.");
            for (int index = 0; index < ortDecoded.Regions.Count; index++)
            {
                TextRegion expected = ortDecoded.Regions[index];
                TextRegion actual = openCvDecoded.Regions[index];
                Assert.AreEqual(expected.SourceIndex, actual.SourceIndex, "Detector region order differs at index " + index + ".");
                Assert.AreEqual(expected.Polygon.Vertices.Count, actual.Polygon.Vertices.Count, "Detector polygon vertex count differs at index " + index + ".");
                Assert.AreEqual(expected.Score, actual.Score, 0.001f, "Detector score differs at index " + index + ".");
                for (int vertex = 0; vertex < expected.Polygon.Vertices.Count; vertex++)
                {
                    Assert.AreEqual(expected.Polygon.Vertices[vertex].X, actual.Polygon.Vertices[vertex].X, 0.5f, "Detector polygon X differs at region/vertex " + index + "/" + vertex + ".");
                    Assert.AreEqual(expected.Polygon.Vertices[vertex].Y, actual.Polygon.Vertices[vertex].Y, 0.5f, "Detector polygon Y differs at region/vertex " + index + "/" + vertex + ".");
                }
            }
            string difference;
            if (openCvMap.Length == ortMap.Length)
            {
                (float maximum, double mean) = CompareFloatTensors(openCvMap, ortMap);
                difference = ";maxAbsDiff=" + maximum.ToString("R", CultureInfo.InvariantCulture) + ";meanAbsDiff=" + mean.ToString("R", CultureInfo.InvariantCulture);
            }
            else difference = ";rawOutputLengthMismatch=true";
            Console.WriteLine("PADDLEOCR_DETECTOR_BACKEND_DIAGNOSTIC image=" + input.SourceSize.Width.ToString(CultureInfo.InvariantCulture) + "x" + input.SourceSize.Height.ToString(CultureInfo.InvariantCulture)
                + ";sourceSha=" + Sha256(sourceImagePath)
                + ";detectorSha=" + Sha256(detectorPath)
                + ";inputShape=" + prepared.Tensor.Shape
                + ";inputSha=" + FloatTensorSha(prepared.Tensor)
                + ";opencvOutputShape=" + openCvMap.Shape
                + ";ortOutputShape=" + ortMap.Shape
                + ";opencvOutputSha=" + FloatTensorSha(openCvMap)
                + ";ortOutputSha=" + FloatTensorSha(ortMap)
                + difference
                + ";opencvAboveProbabilityThreshold=" + CountAbove(openCvMap, 0.3f).ToString(CultureInfo.InvariantCulture)
                + ";ortAboveProbabilityThreshold=" + CountAbove(ortMap, 0.3f).ToString(CultureInfo.InvariantCulture)
                + ";opencvRegions=" + openCvDecoded.Regions.Count.ToString(CultureInfo.InvariantCulture)
                + ";ortRegions=" + ortDecoded.Regions.Count.ToString(CultureInfo.InvariantCulture));
        }

        private static (float Maximum, double Mean) CompareFloatTensors(ITensor left, ITensor right)
        {
            if (!(left is Tensor<float> leftFloat) || !(right is Tensor<float> rightFloat)) throw new InvalidOperationException("The PaddleOCR DB diagnostic expects Float32 probability maps.");
            float[] leftValues = leftFloat.Buffer as float[] ?? throw new InvalidOperationException("The OpenCV probability map is not backed by Float32 values.");
            float[] rightValues = rightFloat.Buffer as float[] ?? throw new InvalidOperationException("The ORT probability map is not backed by Float32 values.");
            if (leftValues.Length != rightValues.Length) throw new InvalidOperationException("The detector backend outputs have different element counts.");
            float maximum = 0f;
            double sum = 0d;
            for (int index = 0; index < leftValues.Length; index++)
            {
                float difference = Math.Abs(leftValues[index] - rightValues[index]);
                maximum = Math.Max(maximum, difference);
                sum += difference;
            }
            return (maximum, sum / leftValues.Length);
        }

        private static long CountAbove(ITensor tensor, float threshold)
        {
            if (!(tensor is Tensor<float> values)) throw new InvalidOperationException("The PaddleOCR DB diagnostic expects Float32 probability maps.");
            float[] buffer = values.Buffer as float[] ?? throw new InvalidOperationException("The probability map is not backed by Float32 values.");
            long count = 0;
            for (int index = 0; index < buffer.Length; index++) if (buffer[index] > threshold) count++;
            return count;
        }

        private static string FloatTensorSha(ITensor tensor)
        {
            if (!(tensor is Tensor<float> values)) throw new InvalidOperationException("The PaddleOCR diagnostic expects Float32 tensors.");
            float[] buffer = values.Buffer as float[] ?? throw new InvalidOperationException("The tensor is not backed by Float32 values.");
            byte[] bytes = new byte[checked(buffer.Length * sizeof(float))];
            Buffer.BlockCopy(buffer, 0, bytes, 0, bytes.Length);
            using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static PaddleOcrArtifactContract Artifact(int opset, string sha, string preprocessing, string postprocessing, string? dictionarySha = null)
            => new PaddleOcrArtifactContract(opset, sha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", preprocessing, postprocessing, dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? "" : "official-repository-file-separate-review-required");

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR external file does not exist: " + path);
            return path;
        }

        private static string Sha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }
    }
}
