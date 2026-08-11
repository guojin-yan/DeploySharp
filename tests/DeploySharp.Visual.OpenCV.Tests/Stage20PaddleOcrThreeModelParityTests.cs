using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage20PaddleOcrThreeModelParityTests
    {
        private const string DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void DetectorClassifierRecognizerMatchAcrossOrtAndOpenVino()
        {
            RequireExternal();
            string image = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_IMAGE") ?? @"E:\Data\image\bus.jpg");
            OcrResult ort = Timed("ort", () => Run(false, image));
            OcrResult openVino = Timed("openvino", () => Run(true, image));

            Assert.AreEqual(ort.Regions.Count, openVino.Regions.Count);
            Assert.IsTrue(ort.Regions.Count > 1);
            for (int index = 0; index < ort.Regions.Count; index++)
            {
                OcrRegionResult left = ort.Regions[index];
                OcrRegionResult right = openVino.Regions[index];
                Assert.AreEqual(left.Region.SourceIndex, right.Region.SourceIndex);
                Assert.AreEqual(left.Region.Orientation, right.Region.Orientation);
                Assert.AreEqual(left.Region.Metadata["ocr.orientation.classIndex"], right.Region.Metadata["ocr.orientation.classIndex"]);
                Assert.AreEqual(left.Region.Metadata["ocr.orientation.rejected"], right.Region.Metadata["ocr.orientation.rejected"]);
                float leftConfidence = float.Parse(left.Region.Metadata["ocr.orientation.confidence"], CultureInfo.InvariantCulture);
                float rightConfidence = float.Parse(right.Region.Metadata["ocr.orientation.confidence"], CultureInfo.InvariantCulture);
                Assert.AreEqual(leftConfidence, rightConfidence, .0001f);
                Assert.AreEqual(left.Recognition.Text, right.Recognition.Text);
                Assert.AreEqual(left.Recognition.Confidence, right.Recognition.Confidence, .001f);
                for (int vertex = 0; vertex < left.Region.Polygon.Vertices.Count; vertex++)
                {
                    PointF a = left.Region.Polygon.Vertices[vertex];
                    PointF b = right.Region.Polygon.Vertices[vertex];
                    Assert.AreEqual(a.X, b.X, .25f);
                    Assert.AreEqual(a.Y, b.Y, .25f);
                }
            }

            Console.WriteLine("STAGE20_OCR3_PARITY inputSha=" + FileSha256(image) + ";regions=" + ort.Regions.Count + ";ortSha=" + ort.ComputeSha256() + ";openvinoSha=" + openVino.ComputeSha256());
        }

        private static OcrResult Run(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(new ModelId("external/stage20-detector"), Artifact(11, "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039", "ppocr-det-resize32-imagenet-bgr-v1", "ppocr-db-managed-rectangle-v1"));
            PaddleOcrProfile classifier = PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("external/stage20-classifier"), Artifact(11, "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2", "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1"));
            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_OCR_DICT") ?? @"E:\Model\ocr\ppocrv5\ppocrv5_dict.txt"), "external.ppocrv5", "v5", true, DictionarySha);
            PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(new ModelId("external/stage20-recognizer"), Artifact(7, "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", "ppocr-rec-bgr-half-range-h48-v1", "ppocr-ctc-probability-greedy-v1", DictionarySha), characters);

            VisualSize sourceSize;
            using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr))) sourceSize = probe.SourceSize;
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry(); profiles.Register(detector.VisualProfile); profiles.Register(classifier.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using var pipeline = new OcrPipeline(backends,
                profiles.Select(detector.CreateArtifact(RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_OCR_DET_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_det_onnx.onnx"), backendId), backends, request, VisualTaskId.TextDetection), request,
                profiles.Select(classifier.CreateArtifact(RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_PADDLE_OCR_CLS_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_cls_onnx.onnx"), backendId), backends, request, VisualTaskId.TextOrientationClassification), request,
                classifier.CropProfile ?? throw new InvalidOperationException("The classifier crop profile is missing."),
                profiles.Select(recognizer.CreateArtifact(RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_OCR_REC_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_rec_onnx.onnx"), backendId), backends, request, VisualTaskId.TextRecognition), request,
                recognizer.CropProfile ?? throw new InvalidOperationException("The recognition crop profile is missing."),
                new OcrPipelineOptions(maximumRegions: 32, maximumRecognitionBatch: 16),
                orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrDetectionOptions(sourceSize));
            OcrResult result = pipeline.Run(input);
            Assert.AreEqual(OcrOrientationStrategy.PerTextRegion, pipeline.OrientationStrategy);
            foreach (OcrRegionResult region in result.Regions) Assert.AreEqual("per-text-region", region.Region.Metadata["ocr.orientation.strategy"]);
            return result;
        }

        private static PaddleOcrArtifactContract Artifact(int opset, string sha, string preprocessing, string postprocessing, string? dictionarySha = null)
            => new PaddleOcrArtifactContract(opset, sha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "local-exporter-unverified", "Apache-2.0;external-artifact-redistribution-unverified", preprocessing, postprocessing, dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? "" : "official-repository-file-separate-review-required");

        private static T Timed<T>(string backend, Func<T> action)
        {
            var watch = Stopwatch.StartNew(); T value = action(); watch.Stop();
            Console.WriteLine("STAGE20_OCR3_TIMING backend=" + backend + ";elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            return value;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE20_RUN_EXTERNAL=1 to run the authorized local three-model OCR parity test.");
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured stage-20 validation file does not exist: " + path);
            return path;
        }

        private static string FileSha256(string path)
        {
            using FileStream stream = File.OpenRead(path); using SHA256 sha = SHA256.Create(); byte[] bytes = sha.ComputeHash(stream);
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
