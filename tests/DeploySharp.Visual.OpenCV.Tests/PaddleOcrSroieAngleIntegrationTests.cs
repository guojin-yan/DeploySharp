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
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Runs real SROIE-linked crops under known rotations. / 使用真实 SROIE 关联裁剪图运行已知旋转。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrSroieAngleIntegrationTests
{
    private const string DefaultRoot = @"artifacts\synthetic-sroie-angle-a3-20260929";
    private const string ModelRoot = @"E:\Model\paddleocr";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task RealLinkedCropsRetainTextAcrossKnownAngles(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE=1 to run the linked SROIE angle smoke.");
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE_ROOT") ?? DefaultRoot);
        string manifestPath = Path.Combine(root, "data", "annotations", "manifests", "sroie-known-angle-ocr.jsonl");
        if (!File.Exists(manifestPath)) Assert.Inconclusive("Generate the SROIE angle case first: " + root);
        string modelDirectory = Path.Combine(ModelRoot, "PP-OCRv6", "small");
        string detectorPath = Path.Combine(modelDirectory, "PP-OCRv6_small_det_inference.onnx");
        string recognizerPath = Path.Combine(modelDirectory, "PP-OCRv6_small_rec_inference.onnx");
        string dictionaryPath = Path.Combine(modelDirectory, "PP-OCRv6_small_rec_dict.txt");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath) || !File.Exists(dictionaryPath)) Assert.Inconclusive("PP-OCRv6 Small assets are incomplete.");
        ModelId detectorId = new ModelId("paddleocr/ppocrv6/small-det");
        ModelId recognizerId = new ModelId("paddleocr/ppocrv6/small-rec");
        PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
        PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(detectorId);
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(recognizerId);
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "sroie-angle-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorRelease), maximumBatch: 1);
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerRelease, DictionarySha), characters, maximumBatch: 8);
        BackendId backendId;
        using var registry = new BackendRegistry();
        if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)) { registry.UseOpenVino(); backendId = OpenVinoBackendProvider.BackendId; }
        else { registry.UseOnnxRuntime(); backendId = OnnxRuntimeBackendProvider.BackendId; }
        var profiles = new VisualProfileRegistry(); profiles.Register(detector.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, "CPU");
        VisualProfileSelection detectorSelection = profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection);
        VisualProfileSelection recognizerSelection = profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition);
        using var pipeline = new OcrPipeline(registry, detectorSelection, request, recognizerSelection, request, recognizer.CropProfile!,
            new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 8, maximumSourcePixels: 128L * 1024L * 1024L), new SessionOptions(1), new SessionOptions(1));
        var rows = new List<object>();
        foreach (string line in File.ReadLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement item = document.RootElement;
            string imagePath = Path.Combine(root, item.GetProperty("image_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            string expected = item.GetProperty("expected_text").GetString()!;
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
            var quad = new TextQuadrilateral(new PointF(0, 0), new PointF(probe.SourceSize.Width, 0), new PointF(probe.SourceSize.Width, probe.SourceSize.Height), new PointF(0, probe.SourceSize.Height), TextCornerOrder.TopLeftClockwise);
            IReadOnlyList<OcrRegionResult> result = await pipeline.RecognizeOnlyAsync(input, new[] { new TextRegion(0, 1f, quad.Polygon, quad) }).ConfigureAwait(false);
            Assert.AreEqual(1, result.Count);
            OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(expected.ToLowerInvariant(), result[0].Recognition.Text.ToLowerInvariant());
            // Extreme right-angle rotations may legitimately yield an empty recognition
            // from a recognizer-only crop; retain that as measurable angle evidence rather
            // than turning it into an execution failure.
            rows.Add(new
            {
                imageId = item.GetProperty("image_id").GetString(), angleDegreesCcw = item.GetProperty("angle_degrees_ccw").GetDouble(),
                sourceParentImageId = item.GetProperty("source_parent_image_id").GetString(), sourceInstanceId = item.GetProperty("source_instance_id").GetString(),
                sourceCropSha256 = item.GetProperty("source_crop_sha256").GetString(), imageSha256 = item.GetProperty("image_sha256").GetString(),
                expectedText = expected, recognizedText = result[0].Recognition.Text, caseFoldedCharacterErrorRate = metrics.CharacterErrorRate,
                caseFoldedWordErrorRate = metrics.WordErrorRate, confidence = result[0].Recognition.Confidence,
                inputTensorSha256 = TensorSha(input.DetectionInput.Tensor), polygonRoundTripMaxAbs = 0d
            });
        }
        string report = Path.Combine(TestContext.TestResultsDirectory!, "paddleocr-sroie-angle-" + backend + ".json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, generatedUtc = DateTimeOffset.UtcNow, backend, records = rows,
            detectorSha256 = detectorRelease.Sha256, recognizerSha256 = recognizerRelease.Sha256, dictionarySha256 = DictionarySha,
            boundary = "Real parent-linked SROIE crops rotated by a controlled known angle; not a natural rotation-distribution accuracy benchmark."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(32, rows.Count);
    }

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task KnownRotatedQuadRecoversTextWithPerspectiveAndSafeAffine(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE=1 to run the linked SROIE angle smoke.");
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE_ROOT") ?? DefaultRoot);
        string manifestPath = Path.Combine(root, "data", "annotations", "manifests", "sroie-known-angle-ocr.jsonl");
        if (!File.Exists(manifestPath)) Assert.Inconclusive("Generate the SROIE angle case first: " + root);
        string modelDirectory = Path.Combine(ModelRoot, "PP-OCRv6", "small");
        string detectorPath = Path.Combine(modelDirectory, "PP-OCRv6_small_det_inference.onnx");
        string recognizerPath = Path.Combine(modelDirectory, "PP-OCRv6_small_rec_inference.onnx");
        string dictionaryPath = Path.Combine(modelDirectory, "PP-OCRv6_small_rec_dict.txt");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath) || !File.Exists(dictionaryPath)) Assert.Inconclusive("PP-OCRv6 Small assets are incomplete.");
        ModelId detectorId = new ModelId("paddleocr/ppocrv6/small-det");
        ModelId recognizerId = new ModelId("paddleocr/ppocrv6/small-rec");
        PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
        PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(detectorId);
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(recognizerId);
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "sroie-angle-rectified-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorRelease), maximumBatch: 1);
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerRelease, DictionarySha), characters, maximumBatch: 8);
        BackendId backendId;
        using var registry = new BackendRegistry();
        if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)) { registry.UseOpenVino(); backendId = OpenVinoBackendProvider.BackendId; }
        else { registry.UseOnnxRuntime(); backendId = OnnxRuntimeBackendProvider.BackendId; }
        var profiles = new VisualProfileRegistry(); profiles.Register(detector.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, "CPU");
        VisualProfileSelection detectorSelection = profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection);
        VisualProfileSelection recognizerSelection = profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition);
        TextCropProfile perspectiveCrop = recognizer.CropProfile!;
        TextCropProfile affineCrop = perspectiveCrop.WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent);
        using var perspectivePipeline = new OcrPipeline(registry, detectorSelection, request, recognizerSelection, request, perspectiveCrop,
            new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 8, maximumSourcePixels: 128L * 1024L * 1024L), new SessionOptions(1), new SessionOptions(1));
        using var affinePipeline = new OcrPipeline(registry, detectorSelection, request, recognizerSelection, request, affineCrop,
            new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 8, maximumSourcePixels: 128L * 1024L * 1024L), new SessionOptions(1), new SessionOptions(1));
        var rows = new List<object>();
        foreach (string line in File.ReadLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement item = document.RootElement;
            string imagePath = Path.Combine(root, item.GetProperty("image_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            string expected = item.GetProperty("expected_text").GetString()!;
            double angle = item.GetProperty("angle_degrees_ccw").GetDouble();
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
            JsonElement[] corners = item.GetProperty("source_quad").EnumerateArray().ToArray();
            var quad = new TextQuadrilateral(
                new PointF(corners[0][0].GetSingle(), corners[0][1].GetSingle()),
                new PointF(corners[1][0].GetSingle(), corners[1][1].GetSingle()),
                new PointF(corners[2][0].GetSingle(), corners[2][1].GetSingle()),
                new PointF(corners[3][0].GetSingle(), corners[3][1].GetSingle()), TextCornerOrder.TopLeftClockwise);
            var region = new TextRegion(0, 1f, quad.Polygon, quad);
            TextCropRequest perspectiveRequest = new TextCropRequest(region, perspectiveCrop);
            TextCropRequest affineRequest = new TextCropRequest(region, affineCrop);
            using PreparedVisualInput perspectiveInput = input.PrepareRecognitionBatch(recognizer.VisualProfile.Input.Name, new[] { perspectiveRequest }, CancellationToken.None);
            using PreparedVisualInput affineInput = input.PrepareRecognitionBatch(recognizer.VisualProfile.Input.Name, new[] { affineRequest }, CancellationToken.None);
            float[] perspectiveTensor = (float[])perspectiveInput.Tensor.Buffer;
            float[] affineTensor = (float[])affineInput.Tensor.Buffer;
            float tensorMaxAbs = 0;
            for (int index = 0; index < perspectiveTensor.Length; index++) tensorMaxAbs = Math.Max(tensorMaxAbs, Math.Abs(perspectiveTensor[index] - affineTensor[index]));
            bool affineSelected = affineInput.Preprocessing.Notes?.Contains("cropTransform=Affine", StringComparison.Ordinal) == true;
            Assert.IsTrue(affineSelected, "Known rotation quadrilaterals must satisfy the affine-equivalence policy.");
            Assert.IsTrue(tensorMaxAbs <= .02f, "Perspective/Affine input drift exceeds interpolation tolerance: " + tensorMaxAbs.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

            IReadOnlyList<OcrRegionResult> perspectiveResults = await perspectivePipeline.RecognizeOnlyAsync(input, new[] { region }).ConfigureAwait(false);
            IReadOnlyList<OcrRegionResult> affineResults = await affinePipeline.RecognizeOnlyAsync(input, new[] { region }).ConfigureAwait(false);
            Assert.AreEqual(1, perspectiveResults.Count); Assert.AreEqual(1, affineResults.Count);
            OcrTextAccuracyMetrics perspectiveMetrics = OcrTextAccuracy.Compare(expected.ToLowerInvariant(), perspectiveResults[0].Recognition.Text.ToLowerInvariant());
            OcrTextAccuracyMetrics affineMetrics = OcrTextAccuracy.Compare(expected.ToLowerInvariant(), affineResults[0].Recognition.Text.ToLowerInvariant());
            double polygonRoundTrip = 0;
            for (int vertex = 0; vertex < 4; vertex++)
            {
                polygonRoundTrip = Math.Max(polygonRoundTrip, Math.Abs(quad.Polygon.Vertices[vertex].X - perspectiveResults[0].Region.Polygon.Vertices[vertex].X));
                polygonRoundTrip = Math.Max(polygonRoundTrip, Math.Abs(quad.Polygon.Vertices[vertex].Y - perspectiveResults[0].Region.Polygon.Vertices[vertex].Y));
            }
            Assert.AreEqual(0d, polygonRoundTrip, .0001d);
            rows.Add(new
            {
                imageId = item.GetProperty("image_id").GetString(), angleDegreesCcw = angle,
                sourceParentImageId = item.GetProperty("source_parent_image_id").GetString(), sourceInstanceId = item.GetProperty("source_instance_id").GetString(),
                sourceCropSha256 = item.GetProperty("source_crop_sha256").GetString(), imageSha256 = item.GetProperty("image_sha256").GetString(),
                expectedText = expected,
                perspectiveText = perspectiveResults[0].Recognition.Text, perspectiveCaseFoldedCer = perspectiveMetrics.CharacterErrorRate, perspectiveConfidence = perspectiveResults[0].Recognition.Confidence,
                affineText = affineResults[0].Recognition.Text, affineCaseFoldedCer = affineMetrics.CharacterErrorRate, affineConfidence = affineResults[0].Recognition.Confidence,
                affineSelected, perspectiveTensorSha256 = TensorSha(perspectiveInput.Tensor), affineTensorSha256 = TensorSha(affineInput.Tensor), tensorMaxAbs, polygonRoundTripMaxAbs = polygonRoundTrip
            });
        }
        string report = Path.Combine(TestContext.TestResultsDirectory!, "paddleocr-sroie-angle-rectified-" + backend + ".json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, generatedUtc = DateTimeOffset.UtcNow, backend, records = rows,
            detectorSha256 = detectorRelease.Sha256, recognizerSha256 = recognizerRelease.Sha256, dictionarySha256 = DictionarySha,
            boundary = "Correct quadrilaterals are supplied from a known geometric rotation of real source crops; this tests crop rectification, not automatic angle prediction or natural rotation distribution."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(32, rows.Count);
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null) => new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2", dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");
    private static string TensorSha(ITensor tensor) { float[] values = tensor.Buffer as float[] ?? throw new AssertFailedException("Expected Float32 input."); return Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan()).ToArray())).ToLowerInvariant(); }
    private static string TensorSha(float[] values) => Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan()).ToArray())).ToLowerInvariant();
}
