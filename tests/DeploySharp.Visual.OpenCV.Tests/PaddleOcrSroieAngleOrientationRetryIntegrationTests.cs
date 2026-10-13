using System;
using System.Collections.Generic;
using System.Globalization;
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

/// <summary>
/// Measures the opt-in orientation retry policy on real, source-linked SROIE crops with known transforms.
/// This is controlled angle evidence, not a natural rotation-distribution accuracy benchmark.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrSroieAngleOrientationRetryIntegrationTests
{
    private const string DefaultRoot = @"artifacts\synthetic-sroie-angle-a3-20260929";
    private const string ModelRoot = @"E:\Model\paddleocr";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task ConfidenceOrientationRetryIsMeasuredAgainstKnownAngles(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE_RETRY"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE_RETRY=1 to run the controlled orientation retry evidence.");

        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE_ROOT") ?? DefaultRoot);
        string manifestPath = Path.Combine(root, "data", "annotations", "manifests", "sroie-known-angle-ocr.jsonl");
        if (!File.Exists(manifestPath)) Assert.Inconclusive("Generate the SROIE angle case first: " + root);

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
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "sroie-angle-retry-v6-small", "v6-small", true, DictionarySha);
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
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId,
            string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase) ? "CPU" : "cpu");
        VisualProfileSelection detectorSelection = profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection);
        VisualProfileSelection recognizerSelection = profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition);

        // All three non-zero right angles are tested. The initial orientation is always 0°;
        // the report compares the confidence-selected absolute orientation with the known transform.
        var retry = new OcrOrientationRetryOptions(
            confidenceThreshold: 1f,
            minimumConfidenceGain: 0f,
            maximumRegionsPerImage: 2,
            rotations: new[] { TextOrientation.Clockwise90, TextOrientation.Degrees180, TextOrientation.CounterClockwise90 },
            maximumCropsPerImage: 32);
        TextCropProfile crop = recognizer.CropProfile!.WithOrientationRetry(retry);
        using var pipeline = new OcrPipeline(registry, detectorSelection, request, recognizerSelection, request, crop,
            new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 8, maximumSourcePixels: 128L * 1024L * 1024L),
            new SessionOptions(1), new SessionOptions(1));

        var rows = new List<object>();
        var orientationCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int expectedOrientationCount = 0, expectedOrientationCorrect = 0, exactText = 0, nonEmpty = 0, retryExecuted = 0;
        double totalCer = 0, totalWer = 0;
        foreach (string line in File.ReadLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement item = document.RootElement;
            string imagePath = Path.Combine(root, item.GetProperty("image_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            string expected = item.GetProperty("expected_text").GetString()!;
            double angle = item.GetProperty("angle_degrees_ccw").GetDouble();

            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath,
                detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
            var quad = new TextQuadrilateral(new PointF(0, 0), new PointF(probe.SourceSize.Width, 0),
                new PointF(probe.SourceSize.Width, probe.SourceSize.Height), new PointF(0, probe.SourceSize.Height),
                TextCornerOrder.TopLeftClockwise);
            IReadOnlyList<OcrRegionResult> results = await pipeline.RecognizeOnlyAsync(input,
                new[] { new TextRegion(0, 1f, quad.Polygon, quad) }).ConfigureAwait(false);
            Assert.AreEqual(1, results.Count);
            OcrRegionResult result = results[0];
            OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(expected.ToLowerInvariant(), result.Recognition.Text.ToLowerInvariant());
            string selected = result.Region.Orientation.ToString();
            orientationCounts[selected] = orientationCounts.TryGetValue(selected, out int count) ? count + 1 : 1;
            TextOrientation? expectedOrientation = ExpectedOrientation(angle);
            bool orientationComparable = expectedOrientation.HasValue;
            bool orientationMatch = orientationComparable && result.Region.Orientation == expectedOrientation.GetValueOrDefault();
            if (orientationComparable)
            {
                expectedOrientationCount++;
                if (orientationMatch) expectedOrientationCorrect++;
            }
            if (!string.IsNullOrEmpty(result.Recognition.Text)) nonEmpty++;
            if (metrics.CharacterErrorRate == 0) exactText++;
            totalCer += metrics.CharacterErrorRate;
            totalWer += metrics.WordErrorRate;
            if (result.OrientationRetry is { Attempts.Count: > 1 }) retryExecuted++;
            rows.Add(new
            {
                imageId = item.GetProperty("image_id").GetString(),
                angleDegreesCcw = angle,
                expectedOrientation = expectedOrientation?.ToString(),
                selectedOrientation = selected,
                orientationComparable,
                orientationMatch,
                attemptCount = result.OrientationRetry?.Attempts.Count ?? 1,
                selectedAttempt = result.OrientationRetry?.SelectedIndex ?? 0,
                expectedText = expected,
                recognizedText = result.Recognition.Text,
                characterErrorRate = metrics.CharacterErrorRate,
                wordErrorRate = metrics.WordErrorRate,
                confidence = result.Recognition.Confidence,
                imageSha256 = Sha256File(imagePath),
                sourceCropSha256 = item.GetProperty("source_crop_sha256").GetString()
            });
        }

        Assert.AreEqual(32, rows.Count);
        string report = Path.Combine(TestContext.TestResultsDirectory!, "paddleocr-sroie-angle-orientation-retry-" + backend + ".json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            backend,
            detectorSha256 = detectorRelease.Sha256,
            recognizerSha256 = recognizerRelease.Sha256,
            dictionarySha256 = DictionarySha,
            policy = new
            {
                confidenceThreshold = retry.ConfidenceThreshold,
                minimumConfidenceGain = retry.MinimumConfidenceGain,
                rotations = retry.Rotations.Select(value => value.ToString()).ToArray(),
                maximumRegionsPerImage = retry.MaximumRegionsPerImage,
                maximumCropsPerImage = retry.MaximumCropsPerImage
            },
            summary = new
            {
                rows = rows.Count,
                nonEmpty,
                exactText,
                meanCharacterErrorRate = totalCer / rows.Count,
                meanWordErrorRate = totalWer / rows.Count,
                retryExecuted,
                expectedOrientationCount,
                expectedOrientationCorrect,
                expectedOrientationAccuracy = expectedOrientationCount == 0 ? (double?)null : (double)expectedOrientationCorrect / expectedOrientationCount,
                selectedOrientations = orientationCounts
            },
            boundary = "Controlled known-angle transforms of real parent-linked SROIE crops; orientation selection is a heuristic policy diagnostic, not natural rotation-distribution accuracy.",
            rows
        }, new JsonSerializerOptions { WriteIndented = true }), System.Text.Encoding.UTF8);
        TestContext.AddResultFile(report);
    }

    private static TextOrientation? ExpectedOrientation(double angle)
    {
        if (Math.Abs(angle) < .001) return TextOrientation.Degrees0;
        if (Math.Abs(angle - 180) < .001 || Math.Abs(angle + 180) < .001) return TextOrientation.Degrees180;
        if (Math.Abs(angle - 90) < .001) return TextOrientation.Clockwise90;
        if (Math.Abs(angle + 90) < .001) return TextOrientation.CounterClockwise90;
        return null;
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new PaddleOcrArtifactContract(release.Opset, release.Sha256,
            "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2",
            dictionarySha256: dictionarySha,
            dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string Sha256File(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
