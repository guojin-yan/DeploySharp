using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>
/// Scores all locally available PP-OCR v4/v5/v6 recognizers on the same
/// source-linked HierText line crops. This deliberately bypasses detection so
/// line-level recognition quality is not confused with detector recall.
/// / 使用同一批带来源映射的 HierText 文本行裁剪图评测本地 PP-OCR v4/v5/v6
/// 识别器；显式跳过检测，避免把识别质量和检测召回混在一起。
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrCoreRecognizerQualityExternalIntegrationTests
{
    private const string DefaultDatasetRoot = @"F:\OCRBenchmarkTesting";
    private const string DefaultModelRoot = @"E:\Model\paddleocr";
    private const string CropRevision = "70b6620b2b112597d8219e11eee9773a1403827c";
    private const string V4DictionarySha = "8e9dc37300253c08a5db6d75f8ae7dbf9ab8dc8c8f88827400bfe974269d3953";
    private const string V5DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task AllCoreRecognizersProduceComparableHierTextLineQualityEvidence()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_RUN"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_RUN=1 to run the seven-model HierText line-recognition evidence.");

        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_ROOT") ?? DefaultDatasetRoot);
        string modelRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_MODEL_ROOT") ?? DefaultModelRoot);
        int maximumRows = ReadMaximumRows();
        List<LineCrop> crops = LoadLineCrops(root, maximumRows);
        if (crops.Count == 0) Assert.Inconclusive("No source-linked HierText crops were found under " + root);

        var modelCases = new[]
        {
            new CoreCase("v4-mobile", "paddleocr/ppocrv4/mobile-det", "paddleocr/ppocrv4/mobile-rec", "PP-OCRv4", "PP-OCRv4_mobile_det.onnx", "PP-OCRv4_mobile_rec.onnx", "ppocrv4_keys.txt", V4DictionarySha),
            new CoreCase("v4-server", "paddleocr/ppocrv4/server-det", "paddleocr/ppocrv4/server-rec", "PP-OCRv4", "PP-OCRv4_server_det.onnx", "PP-OCRv4_server_rec.onnx", "ppocrv4_keys.txt", V4DictionarySha),
            new CoreCase("v5-mobile", "paddleocr/ppocrv5/mobile-det", "paddleocr/ppocrv5/mobile-rec", "PP-OCRv5", "PP-OCRv5_mobile_det.onnx", "PP-OCRv5_mobile_rec.onnx", "ppocrv5_dict.txt", V5DictionarySha),
            new CoreCase("v5-server", "paddleocr/ppocrv5/server-det", "paddleocr/ppocrv5/server-rec", "PP-OCRv5", "PP-OCRv5_server_det.onnx", "PP-OCRv5_server_rec.onnx", "ppocrv5_dict.txt", V5DictionarySha),
            new CoreCase("v6-tiny", "paddleocr/ppocrv6/tiny-det", "paddleocr/ppocrv6/tiny-rec", Path.Combine("PP-OCRv6", "tiny"), "PP-OCRv6_tiny_det_inference.onnx", "PP-OCRv6_tiny_rec_inference.onnx", "PP-OCRv6_tiny_rec_dict.txt", null),
            new CoreCase("v6-small", "paddleocr/ppocrv6/small-det", "paddleocr/ppocrv6/small-rec", Path.Combine("PP-OCRv6", "small"), "PP-OCRv6_small_det_inference.onnx", "PP-OCRv6_small_rec_inference.onnx", "PP-OCRv6_small_rec_dict.txt", null),
            new CoreCase("v6-medium", "paddleocr/ppocrv6/medium-det", "paddleocr/ppocrv6/medium-rec", Path.Combine("PP-OCRv6", "medium"), "PP-OCRv6_medium_det_inference.onnx", "PP-OCRv6_medium_rec_inference.onnx", "PP-OCRv6_medium_rec_dict.txt", null)
        };
        string[] requestedBackends = ReadBackends();
        var evidence = new List<object>();

        foreach (string backend in requestedBackends)
        {
            foreach (CoreCase modelCase in modelCases)
            {
                string folder = Path.Combine(modelRoot, modelCase.Folder);
                string detectorPath = RequireFile(Path.Combine(folder, modelCase.DetectorFile));
                string recognizerPath = RequireFile(Path.Combine(folder, modelCase.RecognizerFile));
                string dictionaryPath = RequireFile(Path.Combine(folder, modelCase.DictionaryFile));
                BackendId backendId;
                string device;
                using var registry = new BackendRegistry();
                if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase))
                {
                    registry.UseOpenVino();
                    backendId = OpenVinoBackendProvider.BackendId;
                    device = "CPU";
                }
                else
                {
                    registry.UseOnnxRuntime();
                    backendId = OnnxRuntimeBackendProvider.BackendId;
                    device = "cpu";
                }

                ModelId detectorId = new ModelId(modelCase.DetectorModelId);
                ModelId recognizerId = new ModelId(modelCase.RecognizerModelId);
                PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
                PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
                PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(detectorId);
                PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(recognizerId);
                string dictionarySha = modelCase.DictionarySha ?? Sha256File(dictionaryPath);
                OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "core-line-rec-" + modelCase.Name, modelCase.Name, true, dictionarySha);
                PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorRelease), maximumBatch: 1);
                PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerRelease, dictionarySha), characters, maximumBatch: 16);
                var profiles = new VisualProfileRegistry();
                profiles.Register(detector.VisualProfile);
                profiles.Register(recognizer.VisualProfile);
                profiles.Freeze();
                BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
                using var pipeline = new OcrPipeline(
                    registry,
                    profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection), request,
                    profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition), request,
                    recognizer.CropProfile!,
                    new OcrPipelineOptions(maximumRegions: 1, maximumRecognitionBatch: 16, maximumSourcePixels: 128L * 1024L * 1024L),
                    new SessionOptions(1), new SessionOptions(1));

                var rows = new List<object>(crops.Count);
                int exact = 0, referenceCharacters = 0, characterEdits = 0, referenceWords = 0, wordEdits = 0, empty = 0;
                foreach (LineCrop crop in crops)
                {
                    using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(crop.ImagePath, "probe",
                        new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
                    using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(crop.ImagePath,
                        detector.VisualProfile.Input.Name,
                        OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
                    TextQuadrilateral quad = new TextQuadrilateral(new PointF(0, 0), new PointF(probe.SourceSize.Width, 0),
                        new PointF(probe.SourceSize.Width, probe.SourceSize.Height), new PointF(0, probe.SourceSize.Height), TextCornerOrder.TopLeftClockwise);
                    IReadOnlyList<OcrRegionResult> result = await pipeline.RecognizeOnlyAsync(input,
                        new[] { new TextRegion(0, 1f, quad.Polygon, quad) }).ConfigureAwait(false);
                    Assert.AreEqual(1, result.Count);
                    string text = result[0].Recognition.Text;
                    OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(crop.Text, text);
                    if (metrics.CharacterEditDistance == 0 && metrics.HypothesisCharacters == metrics.ReferenceCharacters) exact++;
                    if (metrics.HypothesisCharacters == 0) empty++;
                    referenceCharacters += metrics.ReferenceCharacters;
                    characterEdits += metrics.CharacterEditDistance;
                    referenceWords += metrics.ReferenceWords;
                    wordEdits += metrics.WordEditDistance;
                    rows.Add(new
                    {
                        cropId = crop.CropId, parentImageId = crop.ParentImageId, parentInstanceId = crop.ParentInstanceId,
                        referenceCharacters = metrics.ReferenceCharacters, characterEditDistance = metrics.CharacterEditDistance,
                        characterErrorRate = metrics.CharacterErrorRate, referenceWords = metrics.ReferenceWords,
                        wordEditDistance = metrics.WordEditDistance, wordErrorRate = metrics.WordErrorRate,
                        expectedTextSha256 = Sha256(crop.Text), recognizedTextSha256 = Sha256(text),
                        handwritten = crop.Handwritten, vertical = crop.Vertical
                    });
                }

                evidence.Add(new
                {
                    backend,
                    model = modelCase.Name,
                    modelIds = new { detector = modelCase.DetectorModelId, recognizer = modelCase.RecognizerModelId },
                    assets = new { detectorSha256 = detectorRelease.Sha256, recognizerSha256 = recognizerRelease.Sha256, dictionarySha256 = dictionarySha },
                    rows = rows.Count,
                    exactRows = exact,
                    exactRate = rows.Count == 0 ? 0d : (double)exact / rows.Count,
                    referenceCharacters,
                    characterEdits,
                    characterErrorRate = referenceCharacters == 0 ? 0d : (double)characterEdits / referenceCharacters,
                    referenceWords,
                    wordEdits,
                    wordErrorRate = referenceWords == 0 ? 0d : (double)wordEdits / referenceWords,
                    emptyPredictions = empty,
                    rowContract = rows
                });
            }
        }

        string outputDirectory = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_OUTPUT") ?? TestContext.TestResultsDirectory!;
        Directory.CreateDirectory(outputDirectory);
        string report = Path.Combine(Path.GetFullPath(outputDirectory), "paddleocr-core-recognizer-quality-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            scope = "PP-OCR v4/v5/v6 recognizer-only quality on one source-linked HierText selection",
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_BENCHMARK_SOURCE_REVISION") ?? "unknown",
            dataset = new { name = "google-research-datasets/HierText", cropRevision = CropRevision, rows = crops.Count, maximumReferenceCharacters = crops.Max(crop => crop.Text.Length), selection = "Evenly spaced source-linked eligible crops from validation sample-002 and sample-003; ignored/empty rows excluded." },
            protocol = new { context = "recognizer-only with known full-crop quadrilateral", backends = requestedBackends, models = modelCases.Select(model => model.Name).ToArray(), batchSize = 1, sessionConcurrency = 1 },
            evidence,
            boundary = "Line-level recognition evidence on a bounded local HierText selection. It does not measure detector recall, page reading order, natural >=3200-character quality, robustness, formal 5+50 performance, GPU behavior or release accuracy. Dataset rights and training overlap remain separate review items."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(requestedBackends.Length * modelCases.Length, evidence.Count);
    }

    private static string[] ReadBackends()
    {
        string value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_BACKENDS") ?? "onnxruntime,openvino";
        string[] values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 0 || values.Any(item => item is not ("onnxruntime" or "openvino")))
            throw new ArgumentException("DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_BACKENDS must contain onnxruntime and/or openvino.");
        return values;
    }

    private static int ReadMaximumRows()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_MAX");
        return int.TryParse(value, out int parsed) && parsed > 0 && parsed <= 4096 ? parsed : 256;
    }

    private static List<LineCrop> LoadLineCrops(string root, int maximumRows)
    {
        var expected = new Dictionary<string, (string ImageId, string Text, bool Handwritten, bool Vertical)>(StringComparer.Ordinal);
        foreach (string manifestName in new[] { "hiertext-validation-sample-002.jsonl", "hiertext-validation-sample-003.jsonl" })
        {
            string path = Path.Combine(root, "data", "annotations", "manifests", manifestName);
            if (!File.Exists(path)) continue;
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement image = document.RootElement;
                foreach (JsonElement instance in image.GetProperty("instances").EnumerateArray())
                {
                    if (instance.TryGetProperty("ignore", out JsonElement ignored) && ignored.GetBoolean()) continue;
                    string text = instance.GetProperty("text").GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    JsonElement attributes = instance.GetProperty("attributes");
                    expected[instance.GetProperty("instance_id").GetString()!] =
                    (image.GetProperty("image_id").GetString()!, text,
                        attributes.GetProperty("handwritten").GetProperty("value").GetBoolean(),
                        attributes.GetProperty("vertical").GetProperty("value").GetBoolean());
                }
            }
        }

        var all = new List<LineCrop>();
        foreach (string sample in new[] { "sample-002", "sample-003" })
        {
            string path = Path.Combine(root, "data", "crops", "hiertext", CropRevision, "validation", sample, "manifest.jsonl");
            if (!File.Exists(path)) continue;
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement crop = document.RootElement;
                string parent = crop.GetProperty("parent_instance_id").GetString()!;
                if (!expected.TryGetValue(parent, out var reference)) continue;
                string imagePath = Path.Combine(root, crop.GetProperty("crop_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(imagePath)) continue;
                string expectedSha = crop.GetProperty("crop_sha256").GetString()!;
                if (!string.Equals(expectedSha, Sha256File(imagePath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("HierText crop SHA-256 mismatch for " + crop.GetProperty("crop_id").GetString());
                all.Add(new LineCrop(sample, crop.GetProperty("crop_id").GetString()!, reference.ImageId, parent, reference.Text, reference.Handwritten, reference.Vertical, imagePath));
            }
        }
        if (all.Count <= maximumRows) return all;
        var selected = new List<LineCrop>(maximumRows);
        for (int index = 0; index < maximumRows; index++) selected.Add(all[(int)((long)index * all.Count / maximumRows)]);
        return selected;
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2",
            dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string RequireFile(string path)
    {
        if (!File.Exists(path)) Assert.Inconclusive("Required external PaddleOCR file does not exist: " + path);
        return path;
    }

    private static string Sha256File(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record CoreCase(string Name, string DetectorModelId, string RecognizerModelId, string Folder, string DetectorFile, string RecognizerFile, string DictionaryFile, string? DictionarySha);
    private sealed record LineCrop(string Sample, string CropId, string ParentImageId, string ParentInstanceId, string Text, bool Handwritten, bool Vertical, string ImagePath);
}
