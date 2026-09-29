using System;
using System.Collections.Generic;
using System.Globalization;
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

/// <summary>Runs known-angle crops through Perspective and safe Affine preprocessing. / 使用已知角度裁剪图运行透视与安全仿射预处理。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrSyntheticAngleIntegrationTests
{
    private const string DefaultRoot = @"artifacts\synthetic-ocr-angle-20260929";
    private const string ModelRoot = @"E:\Model\paddleocr";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task KnownAngleCropsComparePerspectiveAndSafeAffine(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SYNTHETIC_ANGLE"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_SYNTHETIC_ANGLE=1 to run the controlled angle sample.");

        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ANGLE_ROOT") ?? DefaultRoot);
        string manifestPath = Path.Combine(root, "data", "annotations", "manifests", "synthetic-angle-ocr.jsonl");
        if (!File.Exists(manifestPath)) Assert.Inconclusive("Generate the synthetic angle case first: " + root);
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
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "synthetic-angle-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor,
            Artifact(detectorRelease), maximumBatch: 1);
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor,
            Artifact(recognizerRelease, DictionarySha), characters, maximumBatch: 8);

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

        var records = new List<object>();
        var preprocessingTensors = new Dictionary<double, Dictionary<string, float[]>>();
        foreach (string json in File.ReadLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            using JsonDocument record = JsonDocument.Parse(json);
            JsonElement item = record.RootElement;
            string imagePath = Path.Combine(root, item.GetProperty("image_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(imagePath)) Assert.Inconclusive("Missing generated angle image: " + imagePath);
            string expected = item.GetProperty("expected_text").GetString()!;
            double angle = item.GetProperty("angle_degrees_ccw").GetDouble();
            PointF[] points = item.GetProperty("polygon").EnumerateArray()
                .Select(point => new PointF(point[0].GetSingle(), point[1].GetSingle())).ToArray();
            var quad = new TextQuadrilateral(points[0], points[1], points[2], points[3], TextCornerOrder.TopLeftClockwise);
            var region = new TextRegion(0, 1f, quad.Polygon, quad, TextOrientation.Degrees0,
                (float)(angle * Math.PI / 180d));

            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath,
                detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));

            foreach (OcrCropTransformMode transformMode in new[] { OcrCropTransformMode.Perspective, OcrCropTransformMode.AffineWhenEquivalent })
            {
                TextCropProfile crop = recognizer.CropProfile!.WithTransformMode(transformMode);
                using PreparedVisualInput prepared = input.PrepareRecognitionBatch("angle-contract",
                    new[] { new TextCropRequest(region, crop) }, CancellationToken.None);
                string preprocessingNotes = prepared.Preprocessing.Notes ?? string.Empty;
                bool actualAffine = preprocessingNotes.Contains("cropTransform=Affine", StringComparison.Ordinal);
                if (transformMode == OcrCropTransformMode.AffineWhenEquivalent)
                    Assert.IsTrue(actualAffine, "The rotated rectangle must use the proven affine fast path.");
                else
                    Assert.IsTrue(preprocessingNotes.Contains("cropTransform=Perspective", StringComparison.Ordinal));
                float[] preparedValues = prepared.Tensor.Buffer as float[] ?? throw new AssertFailedException("The OCR crop tensor must be Float32.");
                string preparedTensorSha256 = ComputeFloatTensorSha256(prepared.Tensor);
                if (!preprocessingTensors.TryGetValue(angle, out Dictionary<string, float[]>? transformTensors))
                {
                    transformTensors = new Dictionary<string, float[]>(StringComparer.Ordinal);
                    preprocessingTensors.Add(angle, transformTensors);
                }
                transformTensors.Add(transformMode.ToString(), (float[])preparedValues.Clone());
                if (transformTensors.Count == 2)
                {
                    float[] perspective = transformTensors[OcrCropTransformMode.Perspective.ToString()];
                    float[] affine = transformTensors[OcrCropTransformMode.AffineWhenEquivalent.ToString()];
                    Assert.AreEqual(perspective.Length, affine.Length);
                    float maximumTensorAbs = 0;
                    for (int valueIndex = 0; valueIndex < perspective.Length; valueIndex++)
                        maximumTensorAbs = Math.Max(maximumTensorAbs, Math.Abs(perspective[valueIndex] - affine[valueIndex]));
                    Assert.IsTrue(maximumTensorAbs <= 0.02f, "Perspective and affine preprocessing drift exceeds the interpolation tolerance; max abs=" + maximumTensorAbs.ToString("R", CultureInfo.InvariantCulture));
                    transformTensors["__maxAbs"] = new[] { maximumTensorAbs };
                }
                using var pipeline = new OcrPipeline(registry, detectorSelection, request, recognizerSelection, request, crop,
                    new OcrPipelineOptions(maximumRegions: 2, maximumRecognitionBatch: 8, maximumSourcePixels: 64L * 1024L * 1024L),
                    new SessionOptions(1), new SessionOptions(1));
                IReadOnlyList<OcrRegionResult> results = await pipeline.RecognizeOnlyAsync(input, new[] { region }).ConfigureAwait(false);
                Assert.AreEqual(1, results.Count);
                OcrRegionResult result = results[0];
                Assert.IsNotNull(result.Recognition);
                Assert.IsFalse(string.IsNullOrEmpty(result.Recognition.Text));
                double polygonRoundTripMaxAbs = 0;
                Assert.AreEqual(quad.Polygon.Vertices.Count, result.Region.Polygon.Vertices.Count);
                for (int vertex = 0; vertex < quad.Polygon.Vertices.Count; vertex++)
                {
                    polygonRoundTripMaxAbs = Math.Max(polygonRoundTripMaxAbs, Math.Abs(quad.Polygon.Vertices[vertex].X - result.Region.Polygon.Vertices[vertex].X));
                    polygonRoundTripMaxAbs = Math.Max(polygonRoundTripMaxAbs, Math.Abs(quad.Polygon.Vertices[vertex].Y - result.Region.Polygon.Vertices[vertex].Y));
                }
                Assert.AreEqual(0d, polygonRoundTripMaxAbs, 0.0001d, "Recognition must retain the annotated source-space polygon.");
                OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(expected, result.Recognition.Text);
                records.Add(new
                {
                    angleDegreesCcw = angle,
                    imagePath,
                    imageSha256 = FileSha256(imagePath),
                    transform = transformMode.ToString(),
                    text = result.Recognition.Text,
                    textSha256 = Sha256(result.Recognition.Text),
                    expectedCharacters = expected.Length,
                    recognizedCharacters = result.Recognition.Text.Length,
                    characterEditDistance = metrics.CharacterEditDistance,
                    characterErrorRate = metrics.CharacterErrorRate,
                    wordEditDistance = metrics.WordEditDistance,
                    wordErrorRate = metrics.WordErrorRate,
                    confidence = result.Recognition.Confidence,
                    preparedTensorSha256,
                    affineParityMaximumAbs = transformTensors.TryGetValue("__maxAbs", out float[]? affineParity) ? affineParity[0] : (float?)null,
                    polygonRoundTripMaxAbs,
                    affineEquivalent = OcrCropTransformPolicy.IsAffineEquivalent(quad),
                    actualAffine,
                    preprocessingNotes,
                    sourcePolygon = quad.Polygon.Vertices.Select(point => new { point.X, point.Y }).ToArray(),
                    cropTransformContract = "controlled angle contract; not a natural-image accuracy claim"
                });
            }
        }

        string evidencePath = Path.Combine(TestContext.TestResultsDirectory!, "paddleocr-angle-" + backend + ".json");
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
        Console.WriteLine("PADDLEOCR_SYNTHETIC_ANGLE backend=" + backend + ";records=" + records.Count.ToString(CultureInfo.InvariantCulture));
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new PaddleOcrArtifactContract(release.Opset, release.Sha256,
            "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2",
            dictionarySha256: dictionarySha,
            dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string FileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string ComputeFloatTensorSha256(ITensor tensor)
    {
        float[] values = tensor.Buffer as float[] ?? throw new AssertFailedException("The prepared OCR tensor must be Float32.");
        byte[] bytes = new byte[checked(values.Length * sizeof(float))];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
