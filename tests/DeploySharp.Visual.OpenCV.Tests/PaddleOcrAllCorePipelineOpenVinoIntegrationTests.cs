using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            IReadOnlyList<PipelineRunEvidence> runs = RunCoreVariants(imagePath, root);
            WriteReportIfConfigured(runs);
        }

        private static IReadOnlyList<PipelineRunEvidence> RunCoreVariants(string imagePath, string root)
        {
            VisualSize sourceSize = ProbeSourceSize(imagePath);
            string imageSha = ComputeFileSha(imagePath);
            var runs = new List<PipelineRunEvidence>();

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
                string folder = ResolveCaseFolder(root, testCase);
                string detectorPath = RequireFile(Path.Combine(folder, testCase.DetectorFile));
                string classifierFolder = testCase.ClassifierModelId.StartsWith("paddleocr/ppocrv4/", StringComparison.OrdinalIgnoreCase) ? Path.Combine(root, "PP-OCRv4") : Path.Combine(root, "PP-OCRv5");
                string classifierPath = RequireFile(Path.Combine(classifierFolder, testCase.ClassifierFile));
                string recognizerPath = RequireFile(Path.Combine(folder, testCase.RecognizerFile));
                string dictionaryPath = RequireFile(Path.Combine(folder, testCase.DictionaryFile));

                PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.DetectorModelId));
                PaddleOcrModelDescriptor classifierDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.ClassifierModelId));
                PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.RecognizerModelId));
                OcrCharacterSet characterSet = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, GetCharacterSetId(testCase), testCase.Name, true, testCase.DictionarySha);
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
                    new OcrPipelineOptions(maximumRegions: 512, maximumRecognitionBatch: 16, maximumConcurrency: 1),
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
                int recognized = result.Regions.Count(region => region.Recognition != null);
                string resultSha = result.ComputeSha256();
                string textSha = ComputeTextSha(result);
                string geometrySha = ComputeGeometrySha(result);
                string recognitionSha = ComputeRecognitionSha(result);
                List<RegionParityEvidence> regionEvidence = CaptureRegionEvidence(result);
                string detectorSha = ComputeFileSha(detectorPath);
                string classifierSha = ComputeFileSha(classifierPath);
                string recognizerSha = ComputeFileSha(recognizerPath);
                string dictionarySha = ComputeFileSha(dictionaryPath);
                Console.WriteLine("PADDLEOCR_OPENVINO_FULL_PIPELINE image=" + Path.GetFileName(imagePath) + ";imageSha=" + imageSha + ";variant=" + testCase.Name + ";detSha=" + detectorSha + ";clsSha=" + classifierSha + ";recSha=" + recognizerSha + ";dictionarySha=" + dictionarySha + ";characterSetId=" + characterSet.Id + ";characterSetVersion=" + characterSet.Version + ";characterSetSha=" + characterSet.Sha256 + ";regions=" + result.Regions.Count.ToString(CultureInfo.InvariantCulture) + ";recognized=" + recognized.ToString(CultureInfo.InvariantCulture) + ";elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";resultSha=" + resultSha + ";geometrySha=" + geometrySha + ";recognitionSha=" + recognitionSha + ";textSha=" + textSha);
                runs.Add(new PipelineRunEvidence(
                    Path.GetFileName(imagePath), imageSha, testCase.Name,
                    detectorDescriptor.ModelId.Value, detectorSha,
                    classifierDescriptor.ModelId.Value, classifierSha,
                    recognizerDescriptor.ModelId.Value, recognizerSha,
                    dictionarySha, characterSet.Id, characterSet.Version, characterSet.Sha256, result.Regions.Count, recognized,
                    watch.Elapsed.TotalMilliseconds, resultSha, geometrySha, recognitionSha, textSha, regionEvidence));
            }

            return runs;
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void EveryCorePpOcrVariantRunsOpenVinoAcrossThreeLocalImages()
        {
            RequireExternal();
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? ModelRootDefault;
            string[] images = GetConfiguredImages();
            var runs = new List<PipelineRunEvidence>();
            foreach (string image in images)
            {
                if (!File.Exists(image)) Assert.Inconclusive("Missing multi-image OCR input: " + image);
                runs.AddRange(RunCoreVariants(image, root));
            }
            WriteReportIfConfigured(runs);
        }

        private static string[] GetConfiguredImages()
        {
            string? configured = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_IMAGES");
            if (string.IsNullOrWhiteSpace(configured))
            {
                return new[] { @"E:\Data\ocr\demo_1.jpg", @"E:\Data\ocr\demo_2.jpg", @"E:\Data\ocr\demo_3.jpg" };
            }

            string[] images = configured
                .Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            if (images.Length == 1 && !File.Exists(images[0]) && configured.Contains(':', StringComparison.Ordinal))
            {
                images = configured
                    .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static path => !string.IsNullOrWhiteSpace(path))
                    .ToArray();
            }
            if (images.Length == 0)
            {
                Assert.Inconclusive("DEPLOYSHARP_PADDLEOCR_OPENVINO_IMAGES did not contain any image paths.");
            }

            return images;
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

        private static string ComputeTextSha(OcrResult result)
        {
            string text = string.Join("\n", result.Regions.Select(region => region.Recognition?.Text ?? string.Empty));
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        private static string ComputeGeometrySha(OcrResult result)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(result.SourceSize.Width);
                writer.Write(result.SourceSize.Height);
                writer.Write(result.Regions.Count);
                foreach (OcrRegionResult item in result.Regions)
                {
                    writer.Write(item.Region.SourceIndex);
                    writer.Write(item.Region.Score);
                    writer.Write(item.Region.Polygon.Vertices.Count);
                    foreach (var point in item.Region.Polygon.Vertices) { writer.Write(point.X); writer.Write(point.Y); }
                    writer.Write((int)item.Region.Orientation);
                }
            }
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream.ToArray())).ToLowerInvariant();
        }

        private static string ComputeRecognitionSha(OcrResult result)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(result.Regions.Count);
                foreach (OcrRegionResult item in result.Regions)
                {
                    RecognizedText recognition = item.Recognition;
                    writer.Write(item.Region.SourceIndex);
                    writer.Write(recognition.Text);
                    writer.Write(recognition.Confidence);
                    writer.Write(recognition.CharacterSetId);
                    writer.Write(recognition.CharacterSetVersion);
                    writer.Write(recognition.CharacterSetSha256);
                    writer.Write(recognition.Tokens.Count);
                    foreach (OcrToken token in recognition.Tokens)
                    {
                        writer.Write(token.Timestep); writer.Write(token.ClassIndex); writer.Write(token.Confidence);
                        writer.Write(token.Text ?? string.Empty); writer.Write(token.IsBlank); writer.Write(token.IsCollapsedRepeat);
                        writer.Write(token.IsUnknown); writer.Write(token.Emitted);
                    }
                }
            }
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream.ToArray())).ToLowerInvariant();
        }

        private static List<RegionParityEvidence> CaptureRegionEvidence(OcrResult result)
        {
            var evidence = new List<RegionParityEvidence>(result.Regions.Count);
            foreach (OcrRegionResult item in result.Regions)
            {
                var vertices = item.Region.Polygon.Vertices
                    .Select(static point => new VertexParityEvidence(point.X, point.Y))
                    .ToList();
                var tokens = item.Recognition.Tokens
                    .Select(static token => new TokenParityEvidence(token.Timestep, token.ClassIndex, token.Confidence, token.Text, token.IsBlank, token.IsCollapsedRepeat, token.IsUnknown, token.Emitted))
                    .ToList();
                evidence.Add(new RegionParityEvidence(item.Region.SourceIndex, item.Region.Score, vertices, (int)item.Region.Orientation,
                    item.Recognition.Text, item.Recognition.Confidence, item.Recognition.CharacterSetId,
                    item.Recognition.CharacterSetVersion, item.Recognition.CharacterSetSha256, tokens));
            }
            return evidence;
        }

        private static string GetCharacterSetId(CoreCase testCase) => "external." + testCase.Name;

        private static string ResolveCaseFolder(string root, CoreCase testCase)
        {
            var candidates = new List<string> { Path.Combine(root, testCase.Folder) };
            if (testCase.Name.StartsWith("v4-", StringComparison.Ordinal)) candidates.Add(Path.Combine(root, "PP-OCRv4"));
            if (testCase.Name.StartsWith("v5-", StringComparison.Ordinal)) candidates.Add(Path.Combine(root, "PP-OCRv5"));
            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(Path.Combine(candidate, testCase.DetectorFile))
                    && File.Exists(Path.Combine(candidate, testCase.RecognizerFile))
                    && File.Exists(Path.Combine(candidate, testCase.DictionaryFile))) return candidate;
            }

            throw new FileNotFoundException("Could not locate the complete model/dictionary files for " + testCase.Name + " under " + root + ". Searched: " + string.Join("; ", candidates));
        }

        private static string ComputeFileSha(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        private static void WriteReportIfConfigured(IReadOnlyList<PipelineRunEvidence> runs)
        {
            string? reportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_OPENVINO_REPORT");
            if (string.IsNullOrWhiteSpace(reportPath)) return;
            var report = new PipelineReport
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                SourceCommit = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_COMMIT"),
                TestSourceSha256 = Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_SOURCE_SHA256"),
                SourceTreeDirty = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_TREE_DIRTY"),
                OperatingSystem = RuntimeInformation.OSDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                CpuModel = (Environment.GetEnvironmentVariable("DEPLOYSHARP_CPU_MODEL") ?? "unspecified").Trim(),
                LogicalProcessorCount = Environment.ProcessorCount,
                DotNetRuntime = Environment.Version.ToString(),
                OpenVinoProviderAssemblyVersion = typeof(OpenVinoBackendProvider).Assembly.GetName().Version?.ToString() ?? "unknown",
                OpenVinoRuntimeVersion = GetOpenVinoRuntimeVersion(),
                Backend = "OpenVINO CPU",
                WarmupRuns = 0,
                MeasuredRunsPerModelImage = 1,
                Boundary = "Single-run functional integration evidence only; elapsed time covers OcrPipeline.Run after image loading/preprocessing and Session construction, is not steady-state latency or a performance ranking, and does not establish recognition accuracy.",
                Runs = runs.ToList()
            };
            string fullPath = Path.GetFullPath(reportPath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static string GetOpenVinoRuntimeVersion()
        {
            string? nativeRuntimePath = Directory
                .EnumerateFiles(AppContext.BaseDirectory, "openvino_c.dll", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (nativeRuntimePath is null)
            {
                string? configuredVersion = Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENVINO_RUNTIME_VERSION");
                return !string.IsNullOrWhiteSpace(configuredVersion) ? configuredVersion.Trim() : "unavailable";
            }

            FileVersionInfo version = FileVersionInfo.GetVersionInfo(nativeRuntimePath);
            return !string.IsNullOrWhiteSpace(version.ProductVersion)
                ? version.ProductVersion.Trim()
                : !string.IsNullOrWhiteSpace(version.FileVersion)
                    ? version.FileVersion.Trim()
                    : "unknown";
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

        private sealed class PipelineReport
        {
            public DateTimeOffset GeneratedAtUtc { get; set; }
            public string? SourceCommit { get; set; }
            public string? TestSourceSha256 { get; set; }
            public string? SourceTreeDirty { get; set; }
            public string OperatingSystem { get; set; } = string.Empty;
            public string ProcessArchitecture { get; set; } = string.Empty;
            public string CpuModel { get; set; } = string.Empty;
            public int LogicalProcessorCount { get; set; }
            public string DotNetRuntime { get; set; } = string.Empty;
            public string OpenVinoProviderAssemblyVersion { get; set; } = string.Empty;
            public string OpenVinoRuntimeVersion { get; set; } = string.Empty;
            public string Backend { get; set; } = string.Empty;
            public int WarmupRuns { get; set; }
            public int MeasuredRunsPerModelImage { get; set; }
            public string Boundary { get; set; } = string.Empty;
            public List<PipelineRunEvidence> Runs { get; set; } = new List<PipelineRunEvidence>();
        }

        private sealed class PipelineRunEvidence
        {
            public PipelineRunEvidence(string imageFileName, string imageSha256, string variant, string detectorModelId, string detectorSha256, string classifierModelId, string classifierSha256, string recognizerModelId, string recognizerSha256, string dictionarySha256, string characterSetId, string characterSetVersion, string characterSetSha256, int regionCount, int recognizedRegionCount, double elapsedMilliseconds, string resultSha256, string geometrySha256, string recognitionSha256, string textSha256, List<RegionParityEvidence> regionEvidence)
            {
                ImageFileName = imageFileName;
                ImageSha256 = imageSha256;
                Variant = variant;
                DetectorModelId = detectorModelId;
                DetectorSha256 = detectorSha256;
                ClassifierModelId = classifierModelId;
                ClassifierSha256 = classifierSha256;
                RecognizerModelId = recognizerModelId;
                RecognizerSha256 = recognizerSha256;
                DictionarySha256 = dictionarySha256;
                CharacterSetId = characterSetId;
                CharacterSetVersion = characterSetVersion;
                CharacterSetSha256 = characterSetSha256;
                RegionCount = regionCount;
                RecognizedRegionCount = recognizedRegionCount;
                ElapsedMilliseconds = elapsedMilliseconds;
                ResultSha256 = resultSha256;
                GeometrySha256 = geometrySha256;
                RecognitionSha256 = recognitionSha256;
                TextSha256 = textSha256;
                Regions = regionEvidence;
            }

            public string ImageFileName { get; }
            public string ImageSha256 { get; }
            public string Variant { get; }
            public string DetectorModelId { get; }
            public string DetectorSha256 { get; }
            public string ClassifierModelId { get; }
            public string ClassifierSha256 { get; }
            public string RecognizerModelId { get; }
            public string RecognizerSha256 { get; }
            public string DictionarySha256 { get; }
            public string CharacterSetId { get; }
            public string CharacterSetVersion { get; }
            public string CharacterSetSha256 { get; }
            public int RegionCount { get; }
            public int RecognizedRegionCount { get; }
            public double ElapsedMilliseconds { get; }
            public string ResultSha256 { get; }
            public string GeometrySha256 { get; }
            public string RecognitionSha256 { get; }
            public string TextSha256 { get; }
            public List<RegionParityEvidence> Regions { get; }
        }

        private sealed class RegionParityEvidence
        {
            public RegionParityEvidence(int sourceIndex, float detectorScore, List<VertexParityEvidence> vertices, int orientation, string text, float recognitionConfidence, string characterSetId, string characterSetVersion, string characterSetSha256, List<TokenParityEvidence> tokens)
            {
                SourceIndex = sourceIndex; DetectorScore = detectorScore; Vertices = vertices; Orientation = orientation; Text = text;
                RecognitionConfidence = recognitionConfidence; CharacterSetId = characterSetId; CharacterSetVersion = characterSetVersion;
                CharacterSetSha256 = characterSetSha256; Tokens = tokens;
            }
            public int SourceIndex { get; }
            public float DetectorScore { get; }
            public List<VertexParityEvidence> Vertices { get; }
            public int Orientation { get; }
            public string Text { get; }
            public float RecognitionConfidence { get; }
            public string CharacterSetId { get; }
            public string CharacterSetVersion { get; }
            public string CharacterSetSha256 { get; }
            public List<TokenParityEvidence> Tokens { get; }
        }

        private sealed class VertexParityEvidence
        {
            public VertexParityEvidence(float x, float y) { X = x; Y = y; }
            public float X { get; }
            public float Y { get; }
        }

        private sealed class TokenParityEvidence
        {
            public TokenParityEvidence(int timestep, int classIndex, float confidence, string? text, bool isBlank, bool isCollapsedRepeat, bool isUnknown, bool emitted)
            {
                Timestep = timestep; ClassIndex = classIndex; Confidence = confidence; Text = text;
                IsBlank = isBlank; IsCollapsedRepeat = isCollapsedRepeat; IsUnknown = isUnknown; Emitted = emitted;
            }
            public int Timestep { get; }
            public int ClassIndex { get; }
            public float Confidence { get; }
            public string? Text { get; }
            public bool IsBlank { get; }
            public bool IsCollapsedRepeat { get; }
            public bool IsUnknown { get; }
            public bool Emitted { get; }
        }
    }
}
