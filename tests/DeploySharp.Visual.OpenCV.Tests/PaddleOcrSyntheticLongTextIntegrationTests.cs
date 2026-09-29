using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
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

/// <summary>Runs a controlled 3,600-character line through real recognizer-only SlidingWindow paths. / 使用真实识别器-only SlidingWindow 路径运行受控 3,600 字符文本行。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrSyntheticLongTextIntegrationTests
{
    private const string DefaultRoot = @"artifacts\synthetic-ocr-a2-20260929";
    private const string ModelRoot = @"E:\Model\paddleocr";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task SyntheticLongTextSlidingWindowPreservesBoundedContract(string backend)
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SYNTHETIC_LONGTEXT") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_SYNTHETIC_LONGTEXT=1 to run the controlled A2 sample.");
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SYNTHETIC_ROOT") ?? DefaultRoot);
        string manifestPath = Path.Combine(root, "data", "annotations", "manifests", "synthetic-long-text-3600.jsonl");
        string imagePath = Path.Combine(root, "data", "images", "synthetic", "long-text-3600.png");
        if (!File.Exists(manifestPath) || !File.Exists(imagePath)) Assert.Inconclusive("Generate the synthetic A2 case first: " + root);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        string expected = manifest.RootElement.GetProperty("instances")[0].GetProperty("text").GetString()!;
        int sourceWidth = manifest.RootElement.GetProperty("width").GetInt32();
        int sourceHeight = manifest.RootElement.GetProperty("height").GetInt32();
        Assert.IsTrue(expected.Length > 3200);

        ModelId detectorId = new ModelId("paddleocr/ppocrv6/small-det");
        ModelId recognizerId = new ModelId("paddleocr/ppocrv6/small-rec");
        PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
        PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
        PaddleOcrArtifactContract detectorArtifact = Artifact(detectorRelease);
        PaddleOcrArtifactContract recognizerArtifact = Artifact(recognizerRelease, DictionarySha);
        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(detectorId);
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(recognizerId);
        string detectorPath = Path.Combine(ModelRoot, "PP-OCRv6", "small", "PP-OCRv6_small_det_inference.onnx");
        string recognizerPath = Path.Combine(ModelRoot, "PP-OCRv6", "small", "PP-OCRv6_small_rec_inference.onnx");
        string dictionaryPath = Path.Combine(ModelRoot, "PP-OCRv6", "small", "PP-OCRv6_small_rec_dict.txt");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath) || !File.Exists(dictionaryPath)) Assert.Inconclusive("PP-OCRv6 Small assets are incomplete.");
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "synthetic-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, detectorArtifact, maximumBatch: 1);
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, recognizerArtifact, characters, maximumBatch: 16);
        TextCropProfile crop = recognizer.CropProfile!.WithRecognitionWindows(new OcrRecognitionWindowOptions(overlapRatio: .2, maximumWindowsPerRegion: 256, maximumWindowsPerImage: 512, maximumMergedCharacters: 8192, maximumMergedTimesteps: 65536));

        using var registry = new BackendRegistry();
        BackendId backendId;
        if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)) { registry.UseOpenVino(); backendId = OpenVinoBackendProvider.BackendId; }
        else { registry.UseOnnxRuntime(); backendId = OnnxRuntimeBackendProvider.BackendId; }
        var profiles = new VisualProfileRegistry(); profiles.Register(detector.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase) ? "CPU" : "cpu");
        using var pipeline = new OcrPipeline(
            registry,
            profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection), request,
            profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition), request,
            crop,
            new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 16, maximumSourcePixels: 256L * 1024L * 1024L),
            new SessionOptions(1), new SessionOptions(1));

        using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
        using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
        // Keep a small vertical margin so the synthetic line's aspect ratio stays within
        // the configured per-region window budget while retaining readable glyph height.
        var quad = new TextQuadrilateral(new PointF(20, 10), new PointF(sourceWidth - 20, 10), new PointF(sourceWidth - 20, sourceHeight - 10), new PointF(20, sourceHeight - 10), TextCornerOrder.TopLeftClockwise);
        var region = new TextRegion(0, 1f, quad.Polygon, quad);
        IReadOnlyList<OcrRegionResult> results = await pipeline.RecognizeOnlyAsync(input, new[] { region }).ConfigureAwait(false);
        Assert.AreEqual(1, results.Count);
        OcrRegionResult result = results[0];
        Assert.IsNotNull(result.RecognitionWidth);
        Assert.IsTrue(result.RecognitionWidth!.Value.WindowCount > 1, "The >3200-character line must use multiple bounded windows.");
        Assert.IsTrue(result.RecognitionWidth.Value.NaturalWidth > 3200);
        OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(expected, result.Recognition.Text);
        string imageSha256 = FileSha256(imagePath);
        string evidencePath = Path.Combine(TestContext.TestResultsDirectory!, "synthetic-a2-" + backend + "-" + imageSha256.Substring(0, 12) + ".json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            backend,
            model = "paddleocr/ppocrv6/small-rec",
            detectorSha256 = detectorRelease.Sha256,
            recognizerSha256 = recognizerRelease.Sha256,
            dictionarySha256 = DictionarySha,
            imagePath,
            imageSha256,
            expectedCharacters = expected.Length,
            recognizedCharacters = result.Recognition.Text.Length,
            windowCount = result.RecognitionWidth.Value.WindowCount,
            naturalWidth = result.RecognitionWidth.Value.NaturalWidth,
            targetWidth = result.RecognitionWidth.Value.TargetWidth,
            tensorWidth = result.RecognitionWidth.Value.TensorWidth,
            characterEditDistance = metrics.CharacterEditDistance,
            characterErrorRate = metrics.CharacterErrorRate,
            wordEditDistance = metrics.WordEditDistance,
            wordErrorRate = metrics.WordErrorRate,
            expectedTextSha256 = Sha256(expected),
            recognizedTextSha256 = Sha256(result.Recognition.Text),
            recognizedText = result.Recognition.Text,
            windows = result.RecognitionWindows.Select(window => new
            {
                window.Index,
                window.Start,
                window.End,
                window.Width.NaturalWidth,
                window.Width.TargetWidth,
                window.Width.TensorWidth,
                window.RemovedPrefixTokens,
                window.SeamUncertain,
                window.OverlapEditDistance,
                rawTimesteps = window.Recognition.Tokens.Count,
                window.Recognition.Text,
                emittedTokens = window.Recognition.Tokens.Count(token => token.Emitted),
                emittedTrace = window.Recognition.Tokens.Where(token => token.Emitted).Select(token => new
                {
                    token.Timestep,
                    token.ClassIndex,
                    token.Text,
                    token.Confidence
                }).ToArray()
            }).ToArray(),
            boundary = "Controlled synthetic contract sample; not a public dataset accuracy result."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(evidencePath);
        Console.WriteLine("PADDLEOCR_SYNTHETIC_A2 backend=" + backend + ";characters=" + expected.Length + ";windows=" + result.RecognitionWidth.Value.WindowCount + ";recognizedLength=" + result.Recognition.Text.Length + ";cer=" + metrics.CharacterErrorRate.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";wer=" + metrics.WordErrorRate.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";textSha=" + Sha256(result.Recognition.Text));
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2", dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string FileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
