using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

/// <summary>Scores recognizer-only OCR on source-linked HierText line crops. / 在带来源映射的 HierText 文本行裁剪图上隔离评测识别器。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrHierTextLineRecognitionIntegrationTests
{
    private const string DefaultDatasetRoot = @"F:\OCRBenchmarkTesting";
    private const string ModelRoot = @"E:\Model\paddleocr";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task HierTextLineCropsProduceTraceableRecognitionMetrics(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC=1 to run line-level HierText recognition evidence.");

        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_ROOT") ?? DefaultDatasetRoot);
        int maximumRows = ReadMaximumRows();
        List<LineCrop> crops = LoadLineCrops(root, maximumRows);
        if (crops.Count == 0) Assert.Inconclusive("No source-linked HierText text-line crops were found under " + root);

        string directory = Path.Combine(ModelRoot, "PP-OCRv6", "small");
        string detectorPath = Path.Combine(directory, "PP-OCRv6_small_det_inference.onnx");
        string recognizerPath = Path.Combine(directory, "PP-OCRv6_small_rec_inference.onnx");
        string dictionaryPath = Path.Combine(directory, "PP-OCRv6_small_rec_dict.txt");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath) || !File.Exists(dictionaryPath))
            Assert.Inconclusive("PP-OCRv6 Small assets are incomplete.");

        ModelId detectorId = new ModelId("paddleocr/ppocrv6/small-det");
        ModelId recognizerId = new ModelId("paddleocr/ppocrv6/small-rec");
        PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
        PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(detectorId);
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(recognizerId);
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "hiertext-line-rec-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorRelease), maximumBatch: 1);
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerRelease, DictionarySha), characters, maximumBatch: 16);

        using var registry = new BackendRegistry();
        BackendId backendId;
        if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)) { registry.UseOpenVino(); backendId = OpenVinoBackendProvider.BackendId; }
        else { registry.UseOnnxRuntime(); backendId = OnnxRuntimeBackendProvider.BackendId; }
        var profiles = new VisualProfileRegistry(); profiles.Register(detector.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, "CPU");
        using var pipeline = new OcrPipeline(registry,
            profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection), request,
            profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition), request,
            recognizer.CropProfile!, new OcrPipelineOptions(maximumRegions: 1, maximumRecognitionBatch: 16, maximumSourcePixels: 128L * 1024L * 1024L),
            new SessionOptions(1), new SessionOptions(1));

        var rows = new List<object>(crops.Count);
        int exact = 0, referenceCharacters = 0, characterEdits = 0, referenceWords = 0, wordEdits = 0;
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
            OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(crop.Text, result[0].Recognition.Text);
            if (metrics.CharacterEditDistance == 0 && metrics.HypothesisCharacters == metrics.ReferenceCharacters) exact++;
            referenceCharacters += metrics.ReferenceCharacters; characterEdits += metrics.CharacterEditDistance;
            referenceWords += metrics.ReferenceWords; wordEdits += metrics.WordEditDistance;
            rows.Add(new
            {
                cropId = crop.CropId, parentImageId = crop.ParentImageId, parentInstanceId = crop.ParentInstanceId,
                sourceCropSha256 = crop.SourceCropSha256, imageSha256 = crop.ImageSha256, expectedText = crop.Text,
                recognizedText = result[0].Recognition.Text, confidence = result[0].Recognition.Confidence,
                characterEditDistance = metrics.CharacterEditDistance, characterErrorRate = metrics.CharacterErrorRate,
                wordEditDistance = metrics.WordEditDistance, wordErrorRate = metrics.WordErrorRate,
                expectedTextSha256 = Sha256(crop.Text), recognizedTextSha256 = Sha256(result[0].Recognition.Text)
            });
        }

        string outputDirectory = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_OUTPUT") ?? TestContext.TestResultsDirectory!;
        Directory.CreateDirectory(outputDirectory);
        string report = Path.Combine(Path.GetFullPath(outputDirectory), "paddleocr-hiertext-line-rec-" + backend + ".json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, generatedUtc = DateTimeOffset.UtcNow, backend, model = "PP-OCRv6 Small REC",
            detectorSha256 = detectorRelease.Sha256, recognizerSha256 = recognizerRelease.Sha256, dictionarySha256 = DictionarySha,
            rows = rows.Count, exactRows = exact, exactRate = (double)exact / rows.Count,
            referenceCharacters, characterEdits, characterErrorRate = referenceCharacters == 0 ? 0d : (double)characterEdits / referenceCharacters,
            referenceWords, wordEdits, wordErrorRate = referenceWords == 0 ? 0d : (double)wordEdits / referenceWords,
            crops = rows, boundary = "Recognizer-only score on source-linked HierText text-line crops; detection recall, page reading order and formal release gates are out of scope."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(crops.Count, rows.Count);
    }

    private static List<LineCrop> LoadLineCrops(string root, int maximumRows)
    {
        var expected = new Dictionary<string, (string ImageId, string Text)>(StringComparer.Ordinal);
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
                    string text = instance.GetProperty("text").GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text)) expected[instance.GetProperty("instance_id").GetString()!] = (image.GetProperty("image_id").GetString()!, text);
                }
            }
        }
        var output = new List<LineCrop>();
        foreach (string sample in new[] { "sample-002", "sample-003" })
        {
            string path = Path.Combine(root, "data", "crops", "hiertext", "70b6620b2b112597d8219e11eee9773a1403827c", "validation", sample, "manifest.jsonl");
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
                output.Add(new LineCrop(crop.GetProperty("crop_id").GetString()!, reference.ImageId, parent, reference.Text,
                    crop.GetProperty("crop_sha256").GetString()!, Sha256File(imagePath), imagePath));
                if (output.Count >= maximumRows) return output;
            }
        }
        return output;
    }

    private static int ReadMaximumRows()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_MAX");
        return int.TryParse(value, out int parsed) && parsed > 0 && parsed <= 256 ? parsed : 64;
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2",
            dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string Sha256File(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private sealed record LineCrop(string CropId, string ParentImageId, string ParentInstanceId, string Text, string SourceCropSha256, string ImageSha256, string ImagePath);
}
