using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>
    /// Runs all local PP-OCR v4/v5/v6 full pipelines through OpenCV DNN and
    /// compares them with ORT on the same three images. This is opt-in because
    /// it loads external model files and OpenCV DNN is intentionally strict.
    /// </summary>
    [TestClass]
    public sealed class PaddleOcrCoreOpenCvThreeImageParityTests
    {
        private const string ImageRootDefault = @"E:\Data\ocr";
        private const string ModelRootDefault = @"E:\Model\paddleocr";
        private const string V4DictionarySha = "8e9dc37300253c08a5db6d75f8ae7dbf9ab8dc8c8f88827400bfe974269d3953";
        private const string V5DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AllCorePpOcrVariantsMatchOrtAcrossThreeImagesOnOpenCvDnn()
        {
            RequireExternal();
            string imageRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_IMAGE_ROOT") ?? ImageRootDefault;
            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? ModelRootDefault;
            string[] images =
            {
                RequireFile(Path.Combine(imageRoot, "demo_1.jpg")),
                RequireFile(Path.Combine(imageRoot, "demo_2.jpg")),
                RequireFile(Path.Combine(imageRoot, "demo_3.jpg"))
            };
            string[] imageSha = images.Select(Sha256File).ToArray();
            var rows = new List<RunRecord>();
            foreach (CoreCase testCase in Cases)
            {
                foreach (string image in images)
                {
                    string modelFolder = Path.Combine(modelRoot, testCase.Folder);
                    string detectorPath = RequireFile(Path.Combine(modelFolder, testCase.DetectorFile));
                    string classifierFolder = testCase.ClassifierModelId.StartsWith("paddleocr/ppocrv4/", StringComparison.OrdinalIgnoreCase)
                        ? Path.Combine(modelRoot, "PP-OCRv4")
                        : Path.Combine(modelRoot, "PP-OCRv5");
                    string classifierPath = RequireFile(Path.Combine(classifierFolder, testCase.ClassifierFile));
                    string recognizerPath = RequireFile(Path.Combine(modelFolder, testCase.RecognizerFile));
                    string dictionaryPath = RequireFile(Path.Combine(modelFolder, testCase.DictionaryFile));

                    try
                    {
                        RunRecord row = RunOne(testCase, image, detectorPath, classifierPath, recognizerPath, dictionaryPath);
                        rows.Add(row);
                        Console.WriteLine("PADDLEOCR_OPENCV_MATRIX variant=" + testCase.Name
                            + ";image=" + Path.GetFileName(image)
                            + ";status=pass;regions=" + row.OpenCvRegions.ToString(CultureInfo.InvariantCulture)
                            + ";textMatch=" + row.TextMatch.ToString().ToLowerInvariant()
                            + ";coordinateMatch=" + row.CoordinateMatch.ToString().ToLowerInvariant()
                            + ";opencvMs=" + row.OpenCvElapsedMs.ToString("F3", CultureInfo.InvariantCulture)
                            + ";ortMs=" + row.OrtElapsedMs.ToString("F3", CultureInfo.InvariantCulture));
                    }
                    catch (Exception exception)
                    {
                        rows.Add(new RunRecord(
                            testCase.Name,
                            image,
                            Sha256File(detectorPath),
                            Sha256File(classifierPath),
                            Sha256File(recognizerPath),
                            "unsupported-or-failed",
                            0,
                            0,
                            false,
                            false,
                            0,
                            0,
                            string.Empty,
                            string.Empty,
                            exception.GetType().FullName ?? exception.GetType().Name,
                            exception.ToString()));
                        Console.WriteLine("PADDLEOCR_OPENCV_MATRIX variant=" + testCase.Name
                            + ";image=" + Path.GetFileName(image)
                            + ";status=unsupported-or-failed;errorType=" + (exception.GetType().FullName ?? exception.GetType().Name)
                            + ";message=" + exception.Message.Replace("\r", " ").Replace("\n", " "));
                    }
                }
            }

            string? evidencePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(evidencePath)) WriteEvidence(evidencePath!, modelRoot, images, imageSha, rows);
            int failures = rows.Count(row => row.Status != "pass");
            Assert.AreEqual(0, failures, "OpenCV DNN matrix has unsupported/failed rows. Inspect the JSON evidence for importer details.");
            Assert.AreEqual(Cases.Length * images.Length, rows.Count(row => row.Status == "pass"));
        }

        private static readonly CoreCase[] Cases =
        {
            new CoreCase("v4-mobile", "paddleocr/ppocrv4/mobile-det", "paddleocr/ppocrv4/legacy-cls", "paddleocr/ppocrv4/mobile-rec", "PP-OCRv4", "PP-OCRv4_mobile_det.onnx", "PP-OCRv4_mobile_cls.onnx", "PP-OCRv4_mobile_rec.onnx", "ppocrv4_keys.txt", V4DictionarySha),
            new CoreCase("v4-server", "paddleocr/ppocrv4/server-det", "paddleocr/ppocrv4/legacy-cls", "paddleocr/ppocrv4/server-rec", "PP-OCRv4", "PP-OCRv4_server_det.onnx", "PP-OCRv4_mobile_cls.onnx", "PP-OCRv4_server_rec.onnx", "ppocrv4_keys.txt", V4DictionarySha),
            new CoreCase("v5-mobile", "paddleocr/ppocrv5/mobile-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv5/mobile-rec", "PP-OCRv5", "PP-OCRv5_mobile_det.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv5_mobile_rec.onnx", "ppocrv5_dict.txt", V5DictionarySha),
            new CoreCase("v5-server", "paddleocr/ppocrv5/server-det", "paddleocr/ppocrv5/server-cls", "paddleocr/ppocrv5/server-rec", "PP-OCRv5", "PP-OCRv5_server_det.onnx", "PP-OCRv5_server_cls.onnx", "PP-OCRv5_server_rec.onnx", "ppocrv5_dict.txt", V5DictionarySha),
            new CoreCase("v6-tiny", "paddleocr/ppocrv6/tiny-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv6/tiny-rec", Path.Combine("PP-OCRv6", "tiny"), "PP-OCRv6_tiny_det_inference.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv6_tiny_rec_inference.onnx", "PP-OCRv6_tiny_rec_dict.txt", null),
            new CoreCase("v6-small", "paddleocr/ppocrv6/small-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv6/small-rec", Path.Combine("PP-OCRv6", "small"), "PP-OCRv6_small_det_inference.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv6_small_rec_inference.onnx", "PP-OCRv6_small_rec_dict.txt", null),
            new CoreCase("v6-medium", "paddleocr/ppocrv6/medium-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv6/medium-rec", Path.Combine("PP-OCRv6", "medium"), "PP-OCRv6_medium_det_inference.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv6_medium_rec_inference.onnx", "PP-OCRv6_medium_rec_dict.txt", null)
        };

        private static RunRecord RunOne(CoreCase testCase, string imagePath, string detectorPath, string classifierPath, string recognizerPath, string dictionaryPath)
        {
            PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.DetectorModelId));
            PaddleOcrModelDescriptor classifierDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.ClassifierModelId));
            PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.RecognizerModelId));
            OcrCharacterSet characterSet = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "external.opencv.matrix." + testCase.Name, testCase.Name, true, testCase.DictionarySha);
            PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorDescriptor), maximumBatch: 1);
            // OpenCV DNN has no reliable dynamic-batch contract for these Paddle exports.
            // Keep the parity run at batch=1; ORT remains the reference for the result.
            PaddleOcrProfile classifier = PaddleOcrModelCatalog.CreateProfile(classifierDescriptor, Artifact(classifierDescriptor), maximumBatch: 1);
            PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerDescriptor, characterSet.Sha256), characterSet, maximumBatch: 1);

            VisualSize sourceSize;
            using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr)))
                sourceSize = probe.SourceSize;
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
            long detectorOutputHeight = input.DetectionInput.Tensor.Shape[2];
            var detectorContract = new OpenCvDnnModelContract(detector.VisualProfile.ModelId,
                new[] { new TensorDescriptor(detector.VisualProfile.Input.Name, TensorElementType.Float32, new TensorShape(1, 3, -1, -1)) },
                new[] { new TensorDescriptor(detector.VisualProfile.Outputs[0].Name, TensorElementType.Float32, new TensorShape(1, 1, detectorOutputHeight, -1)) });
            var classifierContract = new OpenCvDnnModelContract(classifier.VisualProfile.ModelId,
                new[] { new TensorDescriptor(classifier.VisualProfile.Input.Name, TensorElementType.Float32, classifier.VisualProfile.Input.ShapePattern) },
                new[] { new TensorDescriptor(classifier.VisualProfile.Outputs[0].Name, TensorElementType.Float32, new TensorShape(1, 2)) });
            int classes = ((GreedyCtcDecoder)recognizer.VisualProfile.Decoder).ExpectedClassCount;
            var recognizerContract = new OpenCvDnnModelContract(recognizer.VisualProfile.ModelId,
                new[] { new TensorDescriptor(recognizer.VisualProfile.Input.Name, TensorElementType.Float32, new TensorShape(1, 3, 48, -1)) },
                new[] { new TensorDescriptor(recognizer.VisualProfile.Outputs[0].Name, TensorElementType.Float32, new TensorShape(1, -1, classes)) });

            using var openCvDetectorRegistry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(detectorContract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
            using var openCvClassifierRegistry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(classifierContract, enableFusion: true, enableWinograd: true));
            using var openCvRecognizerRegistry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(recognizerContract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
            var profiles = new VisualProfileRegistry();
            profiles.Register(detector.VisualProfile);
            profiles.Register(classifier.VisualProfile);
            profiles.Register(recognizer.VisualProfile);
            profiles.Freeze();
            BackendRequest openCvRequest = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
            using var openCvPipeline = new OcrPipeline(
                openCvDetectorRegistry,
                profiles.Select(detector.CreateArtifact(detectorPath, OpenCvDnnBackendProvider.BackendId), openCvDetectorRegistry, openCvRequest, VisualTaskId.TextDetection), openCvRequest,
                openCvClassifierRegistry,
                profiles.Select(classifier.CreateArtifact(classifierPath, OpenCvDnnBackendProvider.BackendId), openCvClassifierRegistry, openCvRequest, VisualTaskId.TextOrientationClassification), openCvRequest,
                classifier.CropProfile!,
                openCvRecognizerRegistry,
                profiles.Select(recognizer.CreateArtifact(recognizerPath, OpenCvDnnBackendProvider.BackendId), openCvRecognizerRegistry, openCvRequest, VisualTaskId.TextRecognition), openCvRequest,
                recognizer.CropProfile!,
                new OcrPipelineOptions(maximumRegions: 64, maximumRecognitionBatch: 1, maximumConcurrency: 1),
                orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);
            Stopwatch openCvWatch = Stopwatch.StartNew();
            OcrResult openCvResult = openCvPipeline.Run(input);
            openCvWatch.Stop();

            using var ortRegistry = new BackendRegistry().UseOnnxRuntime();
            BackendRequest ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var ortPipeline = new OcrPipeline(
                ortRegistry,
                profiles.Select(detector.CreateArtifact(detectorPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, VisualTaskId.TextDetection), ortRequest,
                profiles.Select(classifier.CreateArtifact(classifierPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, VisualTaskId.TextOrientationClassification), ortRequest,
                classifier.CropProfile!,
                profiles.Select(recognizer.CreateArtifact(recognizerPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, VisualTaskId.TextRecognition), ortRequest,
                recognizer.CropProfile!,
                new OcrPipelineOptions(maximumRegions: 64, maximumRecognitionBatch: 1, maximumConcurrency: 1),
                orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);
            Stopwatch ortWatch = Stopwatch.StartNew();
            OcrResult ortResult = ortPipeline.Run(input);
            ortWatch.Stop();
            CompareResults(ortResult, openCvResult, testCase.Name, imagePath);
            return new RunRecord(testCase.Name, imagePath, Sha256File(detectorPath), Sha256File(classifierPath), Sha256File(recognizerPath), "pass", openCvResult.Regions.Count, ortResult.Regions.Count, true, true, openCvWatch.Elapsed.TotalMilliseconds, ortWatch.Elapsed.TotalMilliseconds, ComputeTextSha(openCvResult), ComputeTextSha(ortResult), string.Empty, string.Empty);
        }

        private static void CompareResults(OcrResult expected, OcrResult actual, string variant, string imagePath)
        {
            if (expected.Regions.Count != actual.Regions.Count) throw new InvalidOperationException(variant + " " + Path.GetFileName(imagePath) + " region count mismatch: ORT=" + expected.Regions.Count + ", OpenCV=" + actual.Regions.Count + ".");
            for (int index = 0; index < expected.Regions.Count; index++)
            {
                OcrRegionResult left = expected.Regions[index];
                OcrRegionResult right = actual.Regions[index];
                string leftText = left.Recognition?.Text ?? string.Empty;
                string rightText = right.Recognition?.Text ?? string.Empty;
                if (!string.Equals(leftText, rightText, StringComparison.Ordinal)) throw new InvalidOperationException(variant + " " + Path.GetFileName(imagePath) + " text mismatch at region " + index + ".");
                if (Math.Abs(left.Recognition!.Confidence - right.Recognition!.Confidence) > 0.01f) throw new InvalidOperationException(variant + " " + Path.GetFileName(imagePath) + " confidence mismatch at region " + index + ".");
                if (left.Region.Polygon.Vertices.Count != right.Region.Polygon.Vertices.Count) throw new InvalidOperationException(variant + " " + Path.GetFileName(imagePath) + " polygon vertex mismatch at region " + index + ".");
                for (int vertex = 0; vertex < left.Region.Polygon.Vertices.Count; vertex++)
                {
                    if (Math.Abs(left.Region.Polygon.Vertices[vertex].X - right.Region.Polygon.Vertices[vertex].X) > 0.5f || Math.Abs(left.Region.Polygon.Vertices[vertex].Y - right.Region.Polygon.Vertices[vertex].Y) > 0.5f)
                        throw new InvalidOperationException(variant + " " + Path.GetFileName(imagePath) + " polygon mismatch at region/vertex " + index + "/" + vertex + ".");
                }
            }
        }

        private static PaddleOcrArtifactContract Artifact(PaddleOcrModelDescriptor descriptor, string? dictionarySha = null)
        {
            PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
            return new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "ppocr-official-inference-v1", "deploysharp-paddleocr-db-ctc-v1", dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");
        }

        private static void WriteEvidence(string path, string modelRoot, IReadOnlyList<string> images, IReadOnlyList<string> imageSha, IReadOnlyList<RunRecord> rows)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var evidence = new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceRevision = ResolveSourceRevision(),
                runtime = new { left = "onnxruntime-cpu", right = "opencv-dnn-cpu" },
                modelRoot,
                protocol = new { images = images.Select(Path.GetFileName).ToArray(), combinations = Cases.Length * images.Count, textAndGeometryTolerance = "text exact; confidence <= 0.01; polygon vertices <= 0.5 px" },
                summary = new { expectedCombinations = Cases.Length * images.Count, passed = rows.Count(row => row.Status == "pass"), unsupportedOrFailed = rows.Count(row => row.Status != "pass"), textMatches = rows.Count(row => row.Status == "pass" && row.TextMatch), coordinateMatches = rows.Count(row => row.Status == "pass" && row.CoordinateMatch) },
                inputs = images.Select((image, index) => new { path = image, sha256 = imageSha[index] }).ToArray(),
                runs = rows
            };
            File.WriteAllText(fullPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        }

        private static string ResolveSourceRevision()
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo("git", "rev-parse HEAD") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppContext.BaseDirectory };
                process.Start();
                string value = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return value;
            }
            catch { return "unknown"; }
        }

        private static string ComputeTextSha(OcrResult result)
        {
            string text = string.Join("\n", result.Regions.Select(region => region.Recognition?.Text ?? string.Empty));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        private static string Sha256File(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR external file does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_RUN_EXTERNAL=1 to run the local OpenCV DNN PP-OCR matrix.");
        }

        private sealed record CoreCase(string Name, string DetectorModelId, string ClassifierModelId, string RecognizerModelId, string Folder, string DetectorFile, string ClassifierFile, string RecognizerFile, string DictionaryFile, string? DictionarySha);
        private sealed record RunRecord(string Variant, string ImagePath, string DetectorSha256, string ClassifierSha256, string RecognizerSha256, string Status, int OpenCvRegions, int OrtRegions, bool TextMatch, bool CoordinateMatch, double OpenCvElapsedMs, double OrtElapsedMs, string OpenCvTextSha256, string OrtTextSha256, string ErrorType, string ErrorMessage);
    }
}
