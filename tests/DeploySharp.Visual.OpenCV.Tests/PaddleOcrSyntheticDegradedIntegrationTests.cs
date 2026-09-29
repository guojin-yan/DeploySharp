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
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Runs real SROIE-linked crops through controlled quality degradations. / 使用真实 SROIE 关联裁剪图运行受控质量退化。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrSyntheticDegradedIntegrationTests
{
    private const string DefaultRoot = @"artifacts\synthetic-degraded-ocr-b1b-20260929";
    private const string ModelRoot = @"E:\Model\paddleocr";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task RealLinkedCropsRunAcrossControlledQualityDegradations(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_SYNTHETIC_DEGRADED=1 to run the controlled B1b sample.");
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DEGRADED_ROOT") ?? DefaultRoot);
        string manifestPath = Path.Combine(root, "data", "annotations", "manifests", "synthetic-degraded-ocr.jsonl");
        if (!File.Exists(manifestPath)) Assert.Inconclusive("Generate the controlled degraded crop case first: " + root);

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
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "synthetic-degraded-v6-small", "v6-small", true, DictionarySha);
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
        var request = new BackendRequest(BackendCapabilities.TensorInference, backendId,
            string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase) ? "CPU" : "cpu");
        VisualProfileSelection detectorSelection = profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection);
        VisualProfileSelection recognizerSelection = profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition);
        using var pipeline = new OcrPipeline(registry, detectorSelection, request, recognizerSelection, request, recognizer.CropProfile!,
            new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 8, maximumSourcePixels: 64L * 1024L * 1024L),
            new SessionOptions(1), new SessionOptions(1));

        var records = new List<object>();
        foreach (string json in File.ReadLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement item = document.RootElement;
            string imagePath = Path.Combine(root, item.GetProperty("image_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            string expected = item.GetProperty("source_text").GetString()!;
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath,
                detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
            var quad = new TextQuadrilateral(new PointF(0, 0), new PointF(probe.SourceSize.Width, 0),
                new PointF(probe.SourceSize.Width, probe.SourceSize.Height), new PointF(0, probe.SourceSize.Height), TextCornerOrder.TopLeftClockwise);
            IReadOnlyList<OcrRegionResult> results = await pipeline.RecognizeOnlyAsync(input,
                new[] { new TextRegion(0, 1f, quad.Polygon, quad) }).ConfigureAwait(false);
            Assert.AreEqual(1, results.Count);
            OcrRegionResult result = results[0];
            OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(expected, result.Recognition.Text);
            OcrTextAccuracyMetrics caseFoldedMetrics = OcrTextAccuracy.Compare(expected.ToLowerInvariant(), result.Recognition.Text.ToLowerInvariant());
            records.Add(new
            {
                imageId = item.GetProperty("image_id").GetString(),
                condition = item.GetProperty("condition").GetString(),
                sourceDataset = item.GetProperty("source_dataset").GetString(),
                sourceRevision = item.GetProperty("source_revision").GetString(),
                sourceParentImageId = item.GetProperty("source_parent_image_id").GetString(),
                sourceInstanceId = item.GetProperty("source_instance_id").GetString(),
                sourceCropSha256 = item.GetProperty("source_crop_sha256").GetString(),
                degradedImageSha256 = item.GetProperty("image_sha256").GetString(),
                expectedText = expected,
                recognizedText = result.Recognition.Text,
                expectedCharacters = expected.Length,
                recognizedCharacters = result.Recognition.Text.Length,
                characterEditDistance = metrics.CharacterEditDistance,
                characterErrorRate = metrics.CharacterErrorRate,
                wordEditDistance = metrics.WordEditDistance,
                wordErrorRate = metrics.WordErrorRate,
                caseFoldedCharacterEditDistance = caseFoldedMetrics.CharacterEditDistance,
                caseFoldedCharacterErrorRate = caseFoldedMetrics.CharacterErrorRate,
                caseFoldedWordEditDistance = caseFoldedMetrics.WordEditDistance,
                caseFoldedWordErrorRate = caseFoldedMetrics.WordErrorRate,
                confidence = result.Recognition.Confidence,
                inputTensorSha256 = ComputeFloatTensorSha256(input.DetectionInput.Tensor),
                recognitionWidth = result.RecognitionWidth,
                resultTextSha256 = Sha256(result.Recognition.Text)
            });
        }

        Assert.AreEqual(24, records.Count);
        string evidencePath = Path.Combine(TestContext.TestResultsDirectory!, "paddleocr-degraded-" + backend + ".json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            backend,
            runtime = string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase) ? "OpenVINO CPU" : "ONNX Runtime CPU",
            detectorSha256 = detectorRelease.Sha256,
            recognizerSha256 = recognizerRelease.Sha256,
            dictionarySha256 = DictionarySha,
            manifestPath,
            records
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(evidencePath);
        Console.WriteLine("PADDLEOCR_SYNTHETIC_DEGRADED backend=" + backend + ";records=" + records.Count.ToString(CultureInfo.InvariantCulture));
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2", dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string ComputeFloatTensorSha256(ITensor tensor)
    {
        float[] values = tensor.Buffer as float[] ?? throw new AssertFailedException("The detection input must be Float32.");
        byte[] bytes = new byte[checked(values.Length * sizeof(float))];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
