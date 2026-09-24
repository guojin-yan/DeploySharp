using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>
    /// Executes every locally available PP-OCR v4/v5/v6 DET+CLS+REC combination
    /// through the real crop, orientation, CTC and merge pipeline on OpenVINO CPU.
    /// This is opt-in because the test loads the user-provided external models.
    /// / 使用 OpenVINO CPU 运行本机 PP-OCR v4/v5/v6 的完整 DET+CLS+REC 组合。
    /// 测试显式授权后才会加载用户本机模型。
    /// </summary>
    [TestClass]
    public sealed class PaddleOcrAllCorePipelineOpenVinoIntegrationTests
    {
        private const string ImageDefault = @"E:\Data\ocr\demo_1.jpg";
        private const string ModelRootDefault = @"E:\Model\paddleocr";
        private const string V4DictionarySha = "8e9dc37300253c08a5db6d75f8ae7dbf9ab8dc8c8f88827400bfe974269d3953";
        private const string V5DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void EveryCorePpOcrVariantRunsCompleteDetClsRecMergeOnRealOpenVinoCpu()
        {
            RequireExternal();
            string imagePath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_IMAGE") ?? ImageDefault);
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? ModelRootDefault;
            VisualSize sourceSize = ProbeSourceSize(imagePath);

            var cases = new[]
            {
                new CoreCase("v4-mobile", "paddleocr/ppocrv4/mobile-det", "paddleocr/ppocrv4/legacy-cls", "paddleocr/ppocrv4/mobile-rec", "PP-OCRv4", "PP-OCRv4_mobile_det.onnx", "PP-OCRv4_mobile_cls.onnx", "PP-OCRv4_mobile_rec.onnx", "ppocrv4_keys.txt", V4DictionarySha),
                new CoreCase("v4-server", "paddleocr/ppocrv4/server-det", "paddleocr/ppocrv4/legacy-cls", "paddleocr/ppocrv4/server-rec", "PP-OCRv4", "PP-OCRv4_server_det.onnx", "PP-OCRv4_mobile_cls.onnx", "PP-OCRv4_server_rec.onnx", "ppocrv4_keys.txt", V4DictionarySha),
                new CoreCase("v5-mobile", "paddleocr/ppocrv5/mobile-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv5/mobile-rec", "PP-OCRv5", "PP-OCRv5_mobile_det.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv5_mobile_rec.onnx", "ppocrv5_dict.txt", V5DictionarySha),
                new CoreCase("v5-server", "paddleocr/ppocrv5/server-det", "paddleocr/ppocrv5/server-cls", "paddleocr/ppocrv5/server-rec", "PP-OCRv5", "PP-OCRv5_server_det.onnx", "PP-OCRv5_server_cls.onnx", "PP-OCRv5_server_rec.onnx", "ppocrv5_dict.txt", V5DictionarySha),
                new CoreCase("v6-tiny", "paddleocr/ppocrv6/tiny-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv6/tiny-rec", Path.Combine("PP-OCRv6", "tiny"), "PP-OCRv6_tiny_det_inference.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv6_tiny_rec_inference.onnx", "PP-OCRv6_tiny_rec_dict.txt", null),
                new CoreCase("v6-small", "paddleocr/ppocrv6/small-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv6/small-rec", Path.Combine("PP-OCRv6", "small"), "PP-OCRv6_small_det_inference.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv6_small_rec_inference.onnx", "PP-OCRv6_small_rec_dict.txt", null),
                new CoreCase("v6-medium", "paddleocr/ppocrv6/medium-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv6/medium-rec", Path.Combine("PP-OCRv6", "medium"), "PP-OCRv6_medium_det_inference.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv6_medium_rec_inference.onnx", "PP-OCRv6_medium_rec_dict.txt", null)
            };

            foreach (CoreCase testCase in cases)
            {
                string folder = Path.Combine(root, testCase.Folder);
                string detectorPath = RequireFile(Path.Combine(folder, testCase.DetectorFile));
                string classifierFolder = testCase.ClassifierModelId.StartsWith("paddleocr/ppocrv4/", StringComparison.OrdinalIgnoreCase) ? Path.Combine(root, "PP-OCRv4") : Path.Combine(root, "PP-OCRv5");
                string classifierPath = RequireFile(Path.Combine(classifierFolder, testCase.ClassifierFile));
                string recognizerPath = RequireFile(Path.Combine(folder, testCase.RecognizerFile));
                string dictionaryPath = RequireFile(Path.Combine(folder, testCase.DictionaryFile));

                PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.DetectorModelId));
                PaddleOcrModelDescriptor classifierDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.ClassifierModelId));
                PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.RecognizerModelId));
                OcrCharacterSet characterSet = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "external.openvino." + testCase.Name, testCase.Name, true, testCase.DictionarySha);
                PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorDescriptor), maximumBatch: 1);
                PaddleOcrProfile classifier = PaddleOcrModelCatalog.CreateProfile(classifierDescriptor, Artifact(classifierDescriptor), maximumBatch: 2);
                PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerDescriptor, characterSet.Sha256), characterSet, maximumBatch: 16);

                using var registry = new BackendRegistry();
                registry.UseOpenVino();
                var profiles = new VisualProfileRegistry();
                profiles.Register(detector.VisualProfile);
                profiles.Register(classifier.VisualProfile);
                profiles.Register(recognizer.VisualProfile);
                profiles.Freeze();
                BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
                using var pipeline = new OcrPipeline(
                    registry,
                    profiles.Select(detector.CreateArtifact(detectorPath, OpenVinoBackendProvider.BackendId), registry, request, VisualTaskId.TextDetection), request,
                    profiles.Select(classifier.CreateArtifact(classifierPath, OpenVinoBackendProvider.BackendId), registry, request, VisualTaskId.TextOrientationClassification), request,
                    classifier.CropProfile!,
                    profiles.Select(recognizer.CreateArtifact(recognizerPath, OpenVinoBackendProvider.BackendId), registry, request, VisualTaskId.TextRecognition), request,
                    recognizer.CropProfile!,
                    new OcrPipelineOptions(maximumRegions: 64, maximumRecognitionBatch: 16, maximumConcurrency: 1),
                    orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);

                using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(
                    imagePath,
                    detector.VisualProfile.Input.Name,
                    OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
                Stopwatch watch = Stopwatch.StartNew();
                OcrResult result = pipeline.Run(input);
                watch.Stop();

                Assert.IsTrue(result.Regions.Count > 0, testCase.Name + " returned no text regions.");
                Assert.IsTrue(result.Regions.Any(region => region.Recognition != null), testCase.Name + " did not produce recognized text.");
                Console.WriteLine("PADDLEOCR_OPENVINO_FULL_PIPELINE variant=" + testCase.Name + ";regions=" + result.Regions.Count.ToString(CultureInfo.InvariantCulture) + ";recognized=" + result.Regions.Count(region => region.Recognition != null).ToString(CultureInfo.InvariantCulture) + ";elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";resultSha=" + result.ComputeSha256());
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void EveryCorePpOcrVariantRunsOpenVinoAcrossThreeLocalImages()
        {
            RequireExternal();
            string? previous = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_IMAGE");
            try
            {
                foreach (string image in new[] { @"E:\Data\ocr\demo_1.jpg", @"E:\Data\ocr\demo_2.jpg", @"E:\Data\ocr\demo_3.jpg" })
                {
                    if (!File.Exists(image)) Assert.Inconclusive("Missing multi-image OCR input: " + image);
                    Environment.SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_IMAGE", image);
                    EveryCorePpOcrVariantRunsCompleteDetClsRecMergeOnRealOpenVinoCpu();
                }
            }
            finally { Environment.SetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_IMAGE", previous); }
        }

        private static PaddleOcrArtifactContract Artifact(PaddleOcrModelDescriptor descriptor, string? dictionarySha = null)
        {
            PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
            return new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2", dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");
        }

        private static VisualSize ProbeSourceSize(string imagePath)
        {
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            return probe.SourceSize;
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR file does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL=1 to run the complete local v4/v5/v6 PaddleOCR pipeline matrix.");
        }

        private sealed class CoreCase
        {
            public CoreCase(string name, string detectorModelId, string classifierModelId, string recognizerModelId, string folder, string detectorFile, string classifierFile, string recognizerFile, string dictionaryFile, string? dictionarySha)
            {
                Name = name; DetectorModelId = detectorModelId; ClassifierModelId = classifierModelId; RecognizerModelId = recognizerModelId; Folder = folder; DetectorFile = detectorFile; ClassifierFile = classifierFile; RecognizerFile = recognizerFile; DictionaryFile = dictionaryFile; DictionarySha = dictionarySha;
            }
            public string Name { get; }
            public string DetectorModelId { get; }
            public string ClassifierModelId { get; }
            public string RecognizerModelId { get; }
            public string Folder { get; }
            public string DetectorFile { get; }
            public string ClassifierFile { get; }
            public string RecognizerFile { get; }
            public string DictionaryFile { get; }
            public string? DictionarySha { get; }
        }
    }
}
