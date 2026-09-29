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

/// <summary>Runs >3200 characters composed from real HierText crops and preserves source mapping. / 使用真实 HierText 裁剪拼接的 >3200 字符行并保留来源映射。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrHierTextComposedLongIntegrationTests
{
    private const string DefaultRoot = @"artifacts\hiertext-composed-long-a2-20260929";
    private const string ModelRoot = @"E:\Model\paddleocr";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task ComposedHierTextLongLineRunsBoundedSlidingWindows(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LONGTEXT"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_HIERTEXT_LONGTEXT=1 to run the attributable composed long-text case.");
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LONGTEXT_ROOT") ?? DefaultRoot);
        string manifestPath = Path.Combine(root, "data", "annotations", "manifests", "hiertext-composed-long.jsonl");
        string imagePath = Path.Combine(root, "data", "images", "synthetic", "hiertext-composed-long.png");
        if (!File.Exists(manifestPath) || !File.Exists(imagePath)) Assert.Inconclusive("Generate the composed HierText case first: " + root);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement item = manifest.RootElement;
        string expected = item.GetProperty("expected_text").GetString()!;
        int sourceWidth = item.GetProperty("width").GetInt32();
        int sourceHeight = item.GetProperty("height").GetInt32();
        Assert.IsTrue(expected.Length > 3200);

        string directory = Path.Combine(ModelRoot, "PP-OCRv6", "small");
        string detectorPath = Path.Combine(directory, "PP-OCRv6_small_det_inference.onnx");
        string recognizerPath = Path.Combine(directory, "PP-OCRv6_small_rec_inference.onnx");
        string dictionaryPath = Path.Combine(directory, "PP-OCRv6_small_rec_dict.txt");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath) || !File.Exists(dictionaryPath)) Assert.Inconclusive("PP-OCRv6 Small assets are incomplete.");
        ModelId detectorId = new ModelId("paddleocr/ppocrv6/small-det");
        ModelId recognizerId = new ModelId("paddleocr/ppocrv6/small-rec");
        PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
        PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(detectorId);
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(recognizerId);
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "hiertext-composed-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorRelease), maximumBatch: 1);
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerRelease, DictionarySha), characters, maximumBatch: 16);
        TextCropProfile crop = recognizer.CropProfile!.WithRecognitionWindows(new OcrRecognitionWindowOptions(overlapRatio: .2, maximumWindowsPerRegion: 256, maximumWindowsPerImage: 512, maximumMergedCharacters: 8192, maximumMergedTimesteps: 65536));
        BackendId backendId;
        using var registry = new BackendRegistry();
        if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)) { registry.UseOpenVino(); backendId = OpenVinoBackendProvider.BackendId; }
        else { registry.UseOnnxRuntime(); backendId = OnnxRuntimeBackendProvider.BackendId; }
        var profiles = new VisualProfileRegistry(); profiles.Register(detector.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, "CPU");
        using var pipeline = new OcrPipeline(registry, profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection), request,
            profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition), request, crop,
            new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 16, maximumSourcePixels: 256L * 1024L * 1024L), new SessionOptions(1), new SessionOptions(1));
        using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
        using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
        var quad = new TextQuadrilateral(new PointF(0, 0), new PointF(sourceWidth, 0), new PointF(sourceWidth, sourceHeight), new PointF(0, sourceHeight), TextCornerOrder.TopLeftClockwise);
        IReadOnlyList<OcrRegionResult> results = await pipeline.RecognizeOnlyAsync(input, new[] { new TextRegion(0, 1f, quad.Polygon, quad) }).ConfigureAwait(false);
        Assert.AreEqual(1, results.Count);
        OcrRegionResult result = results[0];
        OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(expected, result.Recognition.Text);
        OcrTextAccuracyMetrics whitespaceMetrics = OcrTextAccuracy.Compare(NormalizeWhitespace(expected), NormalizeWhitespace(result.Recognition.Text));
        Assert.IsTrue(result.RecognitionWidth!.Value.WindowCount > 1);
        JsonElement[] sourceSegments = item.GetProperty("segments").EnumerateArray().ToArray();
        int windowMappingCount = 0;
        var windowMappings = result.RecognitionWindows.Select(window =>
        {
            double xStart = window.Start * sourceWidth;
            double xEnd = window.End * sourceWidth;
            JsonElement[] sources = sourceSegments.Where(segment => segment.GetProperty("xStart").GetDouble() < xEnd && segment.GetProperty("xEnd").GetDouble() > xStart).ToArray();
            Assert.IsTrue(sources.Length > 0, "Every recognition window must map back to at least one source crop.");
            windowMappingCount += sources.Length;
            return new
            {
                window.Index,
                window.Start,
                window.End,
                sourceXStart = xStart,
                sourceXEnd = xEnd,
                sourceSegments = sources.Select(segment => new
                {
                    index = segment.GetProperty("index").GetInt32(),
                    cropId = segment.GetProperty("cropId").GetString(),
                    parentImageId = segment.GetProperty("parentImageId").GetString(),
                    sourceInstanceId = segment.GetProperty("sourceInstanceId").GetString(),
                    expectedCharStart = segment.GetProperty("expectedCharStart").GetInt32(),
                    expectedCharEnd = segment.GetProperty("expectedCharEnd").GetInt32(),
                    sourceCropSha256 = segment.GetProperty("sourceCropSha256").GetString()
                }).ToArray(),
                window.RemovedPrefixTokens,
                window.SeamUncertain,
                window.OverlapEditDistance,
                text = window.Recognition.Text
            };
        }).ToArray();
        Assert.AreEqual(result.RecognitionWidth.Value.WindowCount, windowMappings.Length);
        string report = Path.Combine(TestContext.TestResultsDirectory!, "hiertext-composed-long-a2-" + backend + ".json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, generatedUtc = DateTimeOffset.UtcNow, backend,
            model = "paddleocr/ppocrv6/small-rec", detectorSha256 = detectorRelease.Sha256, recognizerSha256 = recognizerRelease.Sha256, dictionarySha256 = DictionarySha,
            imageSha256 = FileSha(imagePath), expectedCharacters = expected.Length, recognizedCharacters = result.Recognition.Text.Length,
            windowCount = result.RecognitionWidth.Value.WindowCount, naturalWidth = result.RecognitionWidth.Value.NaturalWidth,
            characterErrorRate = metrics.CharacterErrorRate, wordErrorRate = metrics.WordErrorRate,
            whitespaceNormalizedCharacterErrorRate = whitespaceMetrics.CharacterErrorRate, whitespaceNormalizedWordErrorRate = whitespaceMetrics.WordErrorRate,
            expectedTextSha256 = TextSha(expected), recognizedTextSha256 = TextSha(result.Recognition.Text), recognizedText = result.Recognition.Text,
            sourceSegments = sourceSegments,
            sourceCropPageCount = sourceSegments.Select(segment => segment.GetProperty("parentImageId").GetString()).Distinct().Count(),
            sourceSegmentCount = sourceSegments.Length,
            windowSourceMappingCount = windowMappingCount,
            windows = windowMappings,
            boundary = "Attributable composition of real HierText text-line crops; not a natural continuous line or dataset accuracy claim."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Console.WriteLine("PADDLEOCR_HIERTEXT_COMPOSED backend=" + backend + ";characters=" + expected.Length + ";windows=" + result.RecognitionWidth.Value.WindowCount + ";cer=" + metrics.CharacterErrorRate.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";wsCer=" + whitespaceMetrics.CharacterErrorRate.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string NormalizeWhitespace(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string TextSha(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null) => new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2", dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");
}
