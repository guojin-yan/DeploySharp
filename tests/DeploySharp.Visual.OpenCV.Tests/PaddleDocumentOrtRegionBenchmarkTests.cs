using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Records formal ONNX Runtime CPU latency for PP-Structure region and seal exports. / 记录 PP-Structure 区域和印章模型的 ONNX Runtime CPU 正式延迟。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentOrtRegionBenchmarkTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string LayoutImagePath = @"E:\Data\image\bus.jpg";
    private const string TableImagePath = @"E:\Model\PaddleDocument\validation\table_recognition.jpg";
    private const string SealImagePath = @"E:\Data\ocr\demo_1.jpg";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("paddle-doc/pp-doclayout-l", "pp-doclayout-l.onnx", "layout", 640)]
    [DataRow("paddle-doc/pp-docblocklayout", "pp-docblocklayout.onnx", "blocklayout", 640)]
    [DataRow("paddle-doc/pp-doclayout-plus-l", "pp-doclayout-plus-l.onnx", "layout-plus", 800)]
    [DataRow("paddle-doc/rt-detr-h-layout-3cls", "rt-detr-h-layout-3cls.onnx", "layout-3cls", 640)]
    [DataRow("paddle-doc/rt-detr-h-layout-17cls", "rt-detr-h-layout-17cls.onnx", "layout-17cls", 640)]
    [DataRow("paddle-table/rt-detr-l-wired-cell-det", "rt-detr-l-wired-cell-det.onnx", "cell", 640)]
    [DataRow("paddle-table/rt-detr-l-wireless-cell-det", "rt-detr-l-wireless-cell-det.onnx", "cell", 640)]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void RegionModel_OnnxRuntimeCpu_RecordsPreparedInputLatency(string modelId, string modelFile, string kind, int canvasSize)
    {
        RequireEnabled();
        string modelPath = Path.Combine(ModelRoot, modelFile);
        string imagePath = kind == "cell" ? TableImagePath : LayoutImagePath;
        RequireFile(modelPath, modelId);
        RequireFile(imagePath, "ORT region benchmark input");

        IReadOnlyList<string> labels = kind switch
        {
            "cell" => new[] { "table-cell" },
            "layout-plus" => PaddleDocumentProfiles.LayoutPlusLabels,
            "layout-3cls" => PaddleDocumentProfiles.Layout3Labels,
            "layout-17cls" => PaddleDocumentProfiles.Layout17Labels,
            "blocklayout" => PaddleDocumentProfiles.RegionLabels,
            _ => PaddleDocumentProfiles.Layout23Labels
        };
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
            PaddleDocumentModelCatalog.Get(modelId), labels, new VisualSize(canvasSize, canvasSize),
            includeGeometryInputs: true, countOutputName: null, scoreThreshold: 0);

        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        using var ortRegistry = new BackendRegistry().UseOnnxRuntime();
        var ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var ortPipeline = new VisualPipeline(
            ortRegistry,
            profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, profile.VisualProfile.Task),
            ortRequest);
        using var openVinoRegistry = new BackendRegistry().UseOpenVino();
        var openVinoRequest = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        using var openVinoPipeline = new VisualPipeline(
            openVinoRegistry,
            profiles.Select(profile.CreateArtifact(modelPath, OpenVinoBackendProvider.BackendId), openVinoRegistry, openVinoRequest, profile.VisualProfile.Task),
            openVinoRequest);
        using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(
            new OpenCvVisualInputFactory(), imagePath, profile, inputId: "paddle-document-ort-region-" + modelId.Replace('/', '-'));

        DetectionResult expected = openVinoPipeline.Run(input).GetValue<DetectionResult>();
        ValidateResult(expected);
        const int warmupCount = 5;
        const int measurementCount = 50;
        for (int index = 0; index < warmupCount; index++) ValidateResult(ortPipeline.Run(input).GetValue<DetectionResult>());
        var samples = new double[measurementCount];
        DetectionResult? firstMeasuredResult = null;
        for (int index = 0; index < measurementCount; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            DetectionResult result = ortPipeline.Run(input).GetValue<DetectionResult>();
            stopwatch.Stop();
            ValidateResult(result);
            firstMeasuredResult ??= result;
            Assert.AreEqual(firstMeasuredResult.Detections.Count, result.Detections.Count, "Detection count changed during the measured run.");
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        DetectionResult actual = firstMeasuredResult!;
        const float confidenceThreshold = 0.5f;
        const float scoreTolerance = 0.01f;
        const float coordinateTolerancePx = 1.5f;
        int expectedHighConfidence = HighConfidence(expected, confidenceThreshold).Length;
        int actualHighConfidence = HighConfidence(actual, confidenceThreshold).Length;
        int matched = MatchHighConfidence(expected, actual, confidenceThreshold, scoreTolerance, coordinateTolerancePx);
        Assert.IsTrue(matched >= Math.Ceiling(Math.Min(expectedHighConfidence, actualHighConfidence) * 0.95),
            $"ORT/OpenVINO high-confidence parity below 95%: matched={matched}, expected={expectedHighConfidence}, actual={actualHighConfidence}.");

        WriteJson(Path.Combine(OutputDirectory(), "paddle-document-ort-" + modelFile.Replace(".onnx", "-benchmark-20261018.json", StringComparison.Ordinal)), new
        {
            schemaVersion = "deploysharp-paddle-document-ort-region-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            device = Device(),
            model = new { id = modelId, file = modelFile, sha256 = Sha256(modelPath), inputShape = profile.VisualProfile.Input.ShapePattern.ToString(), labels },
            input = new { file = Path.GetFileName(imagePath), sha256 = Sha256(imagePath), inputId = input.InputId },
            backend = new { id = "onnxruntime-cpu", runtimePackage = "Microsoft.ML.OnnxRuntime 1.28.0" },
            protocol = new { warmupCount, measurementCount, batch = 1, sessions = 1, inputReuse = "One PreparedVisualInput reused for every call.", timing = "Synchronous VisualPipeline.Run including ONNX Runtime CPU execution and Paddle NMS Decoder; excludes image decode/preprocessing, model/session creation and one-time OpenVINO reference.", percentile = "Nearest-rank" },
            parity = new { referenceBackend = "openvino-cpu", confidenceThreshold, scoreTolerance, coordinateTolerancePx, expectedHighConfidence, actualHighConfidence, matchedHighConfidence = matched, acceptance = "At least 95% of the smaller high-confidence candidate set is matched by label, score and IoU." },
            result = new { detectionCount = actual.Detections.Count, highConfidenceDetectionCount = actualHighConfidence, highConfidenceFingerprintSha256 = DetectionFingerprint(actual, confidenceThreshold) },
            timing = Percentiles(samples),
            samplesMs = samples
        });
    }

    [TestMethod]
    [DataRow("paddle-seal/ppocrv4-mobile", "ppocrv4-mobile-seal-det.onnx")]
    [DataRow("paddle-seal/ppocrv4-server", "ppocrv4-server-seal-det.onnx")]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void SealModel_OnnxRuntimeCpu_RecordsPreparedInputLatency(string modelId, string modelFile)
    {
        RequireEnabled();
        string modelPath = Path.Combine(ModelRoot, modelFile);
        RequireFile(modelPath, modelId);
        RequireFile(SealImagePath, "ORT seal benchmark input");
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(PaddleDocumentModelCatalog.Get(modelId), new VisualSize(224, 224));
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        using var ortRegistry = new BackendRegistry().UseOnnxRuntime();
        var ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var ortPipeline = new VisualPipeline(ortRegistry, profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, profile.VisualProfile.Task), ortRequest);
        using var openVinoRegistry = new BackendRegistry().UseOpenVino();
        var openVinoRequest = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        using var openVinoPipeline = new VisualPipeline(openVinoRegistry, profiles.Select(profile.CreateArtifact(modelPath, OpenVinoBackendProvider.BackendId), openVinoRegistry, openVinoRequest, profile.VisualProfile.Task), openVinoRequest);
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(SealImagePath, profile.VisualProfile, inputId: "paddle-document-ort-seal-" + modelId.Replace('/', '-'));
        PaddleDocumentSealResult expected = openVinoPipeline.Run(input).GetValue<PaddleDocumentSealResult>();
        ValidateSealResult(expected);
        const int warmupCount = 5;
        const int measurementCount = 50;
        for (int index = 0; index < warmupCount; index++) ValidateSealResult(ortPipeline.Run(input).GetValue<PaddleDocumentSealResult>());
        var samples = new double[measurementCount];
        PaddleDocumentSealResult? firstMeasuredResult = null;
        for (int index = 0; index < measurementCount; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            PaddleDocumentSealResult result = ortPipeline.Run(input).GetValue<PaddleDocumentSealResult>();
            stopwatch.Stop();
            ValidateSealResult(result);
            firstMeasuredResult ??= result;
            Assert.AreEqual(firstMeasuredResult.MaskWidth, result.MaskWidth);
            Assert.AreEqual(firstMeasuredResult.MaskHeight, result.MaskHeight);
            Assert.AreEqual(firstMeasuredResult.Regions.Count, result.Regions.Count);
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }
        PaddleDocumentSealResult actual = firstMeasuredResult!;
        Assert.AreEqual(expected.Regions.Count, actual.Regions.Count, "ORT/OpenVINO seal region counts differ.");
        WriteJson(Path.Combine(OutputDirectory(), "paddle-document-ort-" + modelFile.Replace(".onnx", "-benchmark-20261018.json", StringComparison.Ordinal)), new
        {
            schemaVersion = "deploysharp-paddle-document-ort-seal-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            device = Device(),
            model = new { id = modelId, file = modelFile, sha256 = Sha256(modelPath), inputShape = "[1,3,224,224]" },
            input = new { file = Path.GetFileName(SealImagePath), sha256 = Sha256(SealImagePath), inputId = input.InputId },
            backend = new { id = "onnxruntime-cpu", runtimePackage = "Microsoft.ML.OnnxRuntime 1.28.0" },
            protocol = new { warmupCount, measurementCount, batch = 1, sessions = 1, inputReuse = "One PreparedVisualInput reused for every call.", timing = "Synchronous VisualPipeline.Run including ONNX Runtime CPU execution and PaddleDocumentSealDecoder; excludes image decode/preprocessing, model/session creation and one-time OpenVINO reference.", percentile = "Nearest-rank" },
            parity = new { referenceBackend = "openvino-cpu", referenceMask = new { width = expected.MaskWidth, height = expected.MaskHeight }, referenceRegionCount = expected.Regions.Count, comparison = "Mask shape and decoder region-count contract only; no raw probability-map numerical parity claim." },
            result = new { mask = new { width = actual.MaskWidth, height = actual.MaskHeight }, regionCount = actual.Regions.Count, regionFingerprintSha256 = RegionFingerprint(actual) },
            timing = Percentiles(samples),
            samplesMs = samples
        });
    }

    private static void ValidateResult(DetectionResult result) => Assert.IsTrue(result.Detections.Count > 0 && result.Detections.All(value => float.IsFinite(value.Label.Score) && value.Label.Score >= 0 && value.Label.Score <= 1 && float.IsFinite(value.Box.X) && float.IsFinite(value.Box.Y) && float.IsFinite(value.Box.Width) && float.IsFinite(value.Box.Height)));
    private static void ValidateSealResult(PaddleDocumentSealResult result) => Assert.IsTrue(result.MaskWidth > 0 && result.MaskHeight > 0 && result.Regions.All(value => float.IsFinite(value.Score) && value.Score >= 0 && value.Score <= 1));
    private static Detection[] HighConfidence(DetectionResult result, float threshold) => result.Detections.Where(value => value.Label.Score >= threshold).OrderByDescending(value => value.Label.Score).ToArray();
    private static int MatchHighConfidence(DetectionResult expected, DetectionResult actual, float threshold, float scoreTolerance, float coordinateTolerancePx)
    {
        Detection[] left = HighConfidence(expected, threshold), right = HighConfidence(actual, threshold);
        var used = new bool[right.Length];
        int matched = 0;
        foreach (Detection candidate in left)
        {
            int best = -1;
            float bestIou = 0;
            for (int index = 0; index < right.Length; index++)
            {
                if (used[index] || right[index].Label.Index != candidate.Label.Index || MathF.Abs(right[index].Label.Score - candidate.Label.Score) > scoreTolerance) continue;
                float iou = IoU(candidate, right[index]);
                if (iou >= .80f && MathF.Abs(right[index].Box.X - candidate.Box.X) <= coordinateTolerancePx && MathF.Abs(right[index].Box.Y - candidate.Box.Y) <= coordinateTolerancePx && iou > bestIou) { best = index; bestIou = iou; }
            }
            if (best >= 0) { used[best] = true; matched++; }
        }
        return matched;
    }
    private static float IoU(Detection left, Detection right)
    {
        float x1 = MathF.Max(left.Box.X, right.Box.X), y1 = MathF.Max(left.Box.Y, right.Box.Y), x2 = MathF.Min(left.Box.X + left.Box.Width, right.Box.X + right.Box.Width), y2 = MathF.Min(left.Box.Y + left.Box.Height, right.Box.Y + right.Box.Height);
        float intersection = MathF.Max(0, x2 - x1) * MathF.Max(0, y2 - y1), union = left.Box.Width * left.Box.Height + right.Box.Width * right.Box.Height - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
    private static object Percentiles(double[] samples) { double[] sorted = samples.OrderBy(value => value).ToArray(); return new { p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1], p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], minMs = sorted[0], maxMs = sorted[^1], meanMs = sorted.Average() }; }
    private static string DetectionFingerprint(DetectionResult result, float threshold) { var builder = new StringBuilder(); foreach (Detection value in HighConfidence(result, threshold)) builder.Append(value.Label.Index).Append('|').Append(value.Label.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.Width.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.Height.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n'); return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant(); }
    private static string RegionFingerprint(PaddleDocumentSealResult result) { var builder = new StringBuilder(); foreach (PaddleDocumentRegion value in result.Regions) builder.Append(value.Category).Append('|').Append(value.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Bounds.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Bounds.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Bounds.Width.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Bounds.Height.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n'); return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant(); }
    private static object Device() => new { machine = Environment.MachineName, os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessorCount = Environment.ProcessorCount, processorIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not-recorded" };
    private string OutputDirectory() => Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ORT_EVIDENCE_DIR") ?? TestContext.TestResultsDirectory!;
    private void WriteJson(string path, object value) { string? directory = Path.GetDirectoryName(Path.GetFullPath(path)); if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory); File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true })); TestContext.AddResultFile(path); Console.WriteLine("PADDLE_DOCUMENT_ORT_REGION_BENCHMARK=" + path); }
    private static void RequireEnabled() => Assert.IsTrue(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK") == "1", "Set DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK=1 to run the opt-in ONNX Runtime CPU 5+50 benchmark.");
    private static void RequireFile(string path, string label) => Assert.IsTrue(File.Exists(path), "Missing " + label + ": " + path);
    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
