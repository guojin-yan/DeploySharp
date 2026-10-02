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
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Runs naturally labeled HierText vertical lines with explicit orientation candidates. / 使用带自然竖排标注的 HierText 文本行测试显式方向候选。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrHierTextVerticalIntegrationTests
{
    private const string DefaultDatasetRoot = @"F:\OCRBenchmarkTesting";
    private const string ModelRoot = @"E:\Model\paddleocr";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task NaturalVerticalLinesRunWithExplicitOrientationCandidates(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_VERTICAL"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_HIERTEXT_VERTICAL=1 to run the local HierText vertical-line evidence.");

        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_ROOT") ?? DefaultDatasetRoot);
        int maximumRows = ReadMaximumRows();
        List<VerticalCrop> crops = LoadVerticalCrops(root, maximumRows);
        if (crops.Count == 0)
            Assert.Inconclusive("No non-empty HierText vertical crops were found under " + root);

        string modelDirectory = Path.Combine(ModelRoot, "PP-OCRv6", "small");
        string detectorPath = Path.Combine(modelDirectory, "PP-OCRv6_small_det_inference.onnx");
        string recognizerPath = Path.Combine(modelDirectory, "PP-OCRv6_small_rec_inference.onnx");
        string dictionaryPath = Path.Combine(modelDirectory, "PP-OCRv6_small_rec_dict.txt");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath) || !File.Exists(dictionaryPath))
            Assert.Inconclusive("PP-OCRv6 Small assets are incomplete.");

        ModelId detectorId = new ModelId("paddleocr/ppocrv6/small-det");
        ModelId recognizerId = new ModelId("paddleocr/ppocrv6/small-rec");
        PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
        PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(detectorId);
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(recognizerId);
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "hiertext-vertical-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorRelease), maximumBatch: 1);
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerRelease, DictionarySha), characters, maximumBatch: 8);

        BackendId backendId;
        using var registry = new BackendRegistry();
        if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase))
        {
            registry.UseOpenVino();
            backendId = OpenVinoBackendProvider.BackendId;
        }
        else
        {
            registry.UseOnnxRuntime();
            backendId = OnnxRuntimeBackendProvider.BackendId;
        }

        var profiles = new VisualProfileRegistry();
        profiles.Register(detector.VisualProfile);
        profiles.Register(recognizer.VisualProfile);
        profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, "CPU");
        VisualProfileSelection detectorSelection = profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection);
        VisualProfileSelection recognizerSelection = profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition);
        using var pipeline = new OcrPipeline(registry, detectorSelection, request, recognizerSelection, request, recognizer.CropProfile!,
            new OcrPipelineOptions(maximumRegions: 1, maximumRecognitionBatch: 8, maximumSourcePixels: 128L * 1024L * 1024L),
            new SessionOptions(1), new SessionOptions(1));

        var records = new List<object>(crops.Count);
        foreach (VerticalCrop crop in crops)
        {
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(crop.ImagePath, "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(crop.ImagePath,
                detector.VisualProfile.Input.Name,
                OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
            TextQuadrilateral quad = new TextQuadrilateral(new PointF(0, 0), new PointF(probe.SourceSize.Width, 0),
                new PointF(probe.SourceSize.Width, probe.SourceSize.Height), new PointF(0, probe.SourceSize.Height),
                TextCornerOrder.TopLeftClockwise);

            OrientationObservation zero = await RecognizeAsync(pipeline, input, quad, TextOrientation.Degrees0, crop.Text).ConfigureAwait(false);
            OrientationObservation clockwise = await RecognizeAsync(pipeline, input, quad, TextOrientation.Clockwise90, crop.Text).ConfigureAwait(false);
            OrientationObservation counterClockwise = await RecognizeAsync(pipeline, input, quad, TextOrientation.CounterClockwise90, crop.Text).ConfigureAwait(false);
            OrientationObservation[] observations = { zero, clockwise, counterClockwise };
            OrientationObservation selected = observations
                .OrderBy(value => value.CaseFoldedCharacterErrorRate)
                .ThenByDescending(value => value.Confidence)
                .First();
            records.Add(new
            {
                cropId = crop.CropId,
                parentImageId = crop.ParentImageId,
                parentInstanceId = crop.ParentInstanceId,
                textDirectionLabel = "vertical",
                expectedText = crop.Text,
                sourceCropSha256 = crop.SourceCropSha256,
                imageSha256 = crop.ImageSha256,
                inputWidth = probe.SourceSize.Width,
                inputHeight = probe.SourceSize.Height,
                candidates = observations.Select(value => new
                {
                    orientation = value.Orientation.ToString(),
                    text = value.Text,
                    confidence = value.Confidence,
                    caseFoldedCharacterErrorRate = value.CaseFoldedCharacterErrorRate,
                    caseFoldedWordErrorRate = value.CaseFoldedWordErrorRate,
                    textSha256 = Sha256(value.Text)
                }).ToArray(),
                selectedOrientation = selected.Orientation.ToString(),
                selectedCaseFoldedCharacterErrorRate = selected.CaseFoldedCharacterErrorRate,
                selectedCaseFoldedWordErrorRate = selected.CaseFoldedWordErrorRate
            });
        }

        string outputDirectory = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_VERTICAL_OUTPUT") ?? TestContext.TestResultsDirectory!;
        Directory.CreateDirectory(outputDirectory);
        string report = Path.Combine(Path.GetFullPath(outputDirectory), "paddleocr-hiertext-vertical-" + backend + ".json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            backend,
            model = "PP-OCRv6 Small REC",
            detectorSha256 = detectorRelease.Sha256,
            recognizerSha256 = recognizerRelease.Sha256,
            dictionarySha256 = DictionarySha,
            rows = records,
            boundary = "Natural HierText vertical=true line labels with explicit 0/clockwise90/counterClockwise90 candidates; selecting the lowest CER is diagnostic and does not prove automatic orientation classification or natural angle accuracy."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(crops.Count, records.Count);
    }

    private static async Task<OrientationObservation> RecognizeAsync(OcrPipeline pipeline, OpenCvOcrImageInput input,
        TextQuadrilateral quad, TextOrientation orientation, string expected)
    {
        IReadOnlyList<OcrRegionResult> results = await pipeline.RecognizeOnlyAsync(input,
            new[] { new TextRegion(0, 1f, quad.Polygon, quad, orientation) }).ConfigureAwait(false);
        Assert.AreEqual(1, results.Count);
        OcrRegionResult result = results[0];
        OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(expected.ToLowerInvariant(), result.Recognition.Text.ToLowerInvariant());
        return new OrientationObservation(orientation, result.Recognition.Text, result.Recognition.Confidence,
            metrics.CharacterErrorRate, metrics.WordErrorRate);
    }

    private static List<VerticalCrop> LoadVerticalCrops(string root, int maximumRows)
    {
        var parents = new Dictionary<string, ParentLine>(StringComparer.Ordinal);
        foreach (string manifestName in new[] { "hiertext-validation-sample-002.jsonl", "hiertext-validation-sample-003.jsonl" })
        {
            string manifestPath = Path.Combine(root, "data", "annotations", "manifests", manifestName);
            if (!File.Exists(manifestPath)) continue;
            foreach (string line in File.ReadLines(manifestPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement image = document.RootElement;
                string imageId = image.GetProperty("image_id").GetString()!;
                string imageSha = image.GetProperty("source_provenance").GetProperty("image_sha256").GetString()!;
                foreach (JsonElement instance in image.GetProperty("instances").EnumerateArray())
                {
                    JsonElement attributes = instance.GetProperty("attributes");
                    if (!attributes.TryGetProperty("text_direction", out JsonElement direction) || direction.GetString() != "vertical") continue;
                    string text = instance.GetProperty("text").GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    parents[instance.GetProperty("instance_id").GetString()!] = new ParentLine(imageId, imageSha, text);
                }
            }
        }

        var output = new List<VerticalCrop>();
        foreach (string sample in new[] { "sample-002", "sample-003" })
        {
            string cropManifest = Path.Combine(root, "data", "crops", "hiertext", "70b6620b2b112597d8219e11eee9773a1403827c", "validation", sample, "manifest.jsonl");
            if (!File.Exists(cropManifest)) continue;
            foreach (string line in File.ReadLines(cropManifest))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement crop = document.RootElement;
                string parentId = crop.GetProperty("parent_instance_id").GetString()!;
                if (!parents.TryGetValue(parentId, out ParentLine? parent)) continue;
                string cropRelativePath = crop.GetProperty("crop_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar);
                string imagePath = Path.Combine(root, cropRelativePath);
                if (!File.Exists(imagePath)) continue;
                output.Add(new VerticalCrop(
                    crop.GetProperty("crop_id").GetString()!, parent.ImageId, parentId, parent.Text,
                    crop.GetProperty("crop_sha256").GetString()!, Sha256File(imagePath), imagePath));
                if (output.Count >= maximumRows) return output;
            }
        }
        return output;
    }

    private static int ReadMaximumRows()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_VERTICAL_MAX");
        return int.TryParse(value, out int parsed) && parsed > 0 && parsed <= 128 ? parsed : 18;
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2",
            dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string Sha256File(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record ParentLine(string ImageId, string ImageSha256, string Text);

    private sealed record VerticalCrop(string CropId, string ParentImageId, string ParentInstanceId, string Text,
        string SourceCropSha256, string ImageSha256, string ImagePath);

    private sealed record OrientationObservation(TextOrientation Orientation, string Text, float Confidence,
        double CaseFoldedCharacterErrorRate, double CaseFoldedWordErrorRate);
}
