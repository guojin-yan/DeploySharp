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

/// <summary>Records formal ONNX Runtime CPU latency for static PP-Structure layout exports. / 记录静态 PP-Structure 版面模型的 ONNX Runtime CPU 正式延迟。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentOrtStaticLayoutBenchmarkTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string ImagePath = @"E:\Data\image\bus.jpg";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("paddle-doc/pp-doclayout-m", "pp-doclayout-m.onnx", "layout23", 640, 640)]
    [DataRow("paddle-doc/pp-doclayout-s", "pp-doclayout-s.onnx", "layout23", 480, 480)]
    [DataRow("paddle-doc/picodet-layout-1x", "picodet-layout-1x.onnx", "layout5", 608, 800)]
    [DataRow("paddle-doc/picodet-layout-1x-table", "picodet-layout-1x-table.onnx", "table", 608, 800)]
    [DataRow("paddle-doc/picodet-s-layout-3cls", "picodet-s-layout-3cls.onnx", "layout3", 480, 480)]
    [DataRow("paddle-doc/picodet-l-layout-3cls", "picodet-l-layout-3cls.onnx", "layout3", 640, 640)]
    [DataRow("paddle-doc/picodet-s-layout-17cls", "picodet-s-layout-17cls.onnx", "layout17", 480, 480)]
    [DataRow("paddle-doc/picodet-l-layout-17cls", "picodet-l-layout-17cls.onnx", "layout17", 640, 640)]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void StaticLayoutModel_OnnxRuntimeCpu_RecordsPreparedInputLatency(string modelId, string modelFile, string labelSet, int width, int height)
    {
        RequireEnabled();
        string modelPath = Path.Combine(ModelRoot, modelFile);
        RequireFile(modelPath, modelId);
        RequireFile(ImagePath, "ORT benchmark input");

        IReadOnlyList<string> labels = labelSet switch
        {
            "layout5" => PaddleDocumentProfiles.Layout5Labels,
            "table" => PaddleDocumentProfiles.TableOnlyLabels,
            "layout3" => PaddleDocumentProfiles.Layout3Labels,
            "layout17" => PaddleDocumentProfiles.Layout17Labels,
            _ => PaddleDocumentProfiles.Layout23Labels
        };
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
            PaddleDocumentModelCatalog.Get(modelId), labels, new VisualSize(width, height),
            includeScaleFactorInput: true, scoreThreshold: 0);

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
            new OpenCvVisualInputFactory(), ImagePath, profile, inputId: "paddle-document-ort-static-layout-" + modelId.Replace('/', '-'));

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
        const float confidenceThreshold = 0.05f;
        const float scoreTolerance = 0.05f;
        const float iouTolerance = 0.80f;
        int expectedHighConfidence = HighConfidence(expected, confidenceThreshold).Length;
        int actualHighConfidence = HighConfidence(actual, confidenceThreshold).Length;
        int matched = MatchHighConfidence(expected, actual, confidenceThreshold, scoreTolerance, iouTolerance);
        Assert.IsTrue(matched >= Math.Ceiling(Math.Min(expectedHighConfidence, actualHighConfidence) * 0.95),
            $"ORT/OpenVINO high-confidence parity below 95%: matched={matched}, expected={expectedHighConfidence}, actual={actualHighConfidence}.");

        string outputDirectory = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ORT_EVIDENCE_DIR") ?? TestContext.TestResultsDirectory!;
        string reportPath = Path.Combine(outputDirectory, "paddle-document-ort-" + modelFile.Replace(".onnx", "-benchmark-20261018.json", StringComparison.Ordinal));
        WriteJson(reportPath, new
        {
            schemaVersion = "deploysharp-paddle-document-ort-static-layout-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            device = Device(),
            model = new { id = modelId, file = modelFile, sha256 = Sha256(modelPath), inputShape = $"[1,3,{height},{width}]", labels },
            input = new { file = Path.GetFileName(ImagePath), sha256 = Sha256(ImagePath), inputId = input.InputId },
            backend = new { id = "onnxruntime-cpu", runtimePackage = "Microsoft.ML.OnnxRuntime 1.28.0" },
            protocol = new { warmupCount, measurementCount, batch = 1, sessions = 1, inputReuse = "One PreparedVisualInput reused for every call.", timing = "Synchronous VisualPipeline.Run including ONNX Runtime CPU execution and Paddle NMS Decoder; excludes image decode/preprocessing, model/session creation and one-time OpenVINO reference.", percentile = "Nearest-rank" },
            parity = new { referenceBackend = "openvino-cpu", confidenceThreshold, scoreTolerance, iouTolerance, expectedHighConfidence, actualHighConfidence, matchedHighConfidence = matched, acceptance = "At least 95% of the smaller high-confidence candidate set is matched by label, score and IoU." },
            result = new { detectionCount = actual.Detections.Count, highConfidenceDetectionCount = actualHighConfidence, highConfidenceFingerprintSha256 = DetectionFingerprint(actual, confidenceThreshold) },
            timing = Percentiles(samples),
            samplesMs = samples
        });
    }

    private static void ValidateResult(DetectionResult result)
    {
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Detections.Count > 0);
        Assert.IsTrue(result.Detections.All(value => float.IsFinite(value.Label.Score) && value.Label.Score >= 0 && value.Label.Score <= 1 && float.IsFinite(value.Box.X) && float.IsFinite(value.Box.Y) && float.IsFinite(value.Box.Width) && float.IsFinite(value.Box.Height)));
    }

    private static Detection[] HighConfidence(DetectionResult result, float threshold) => result.Detections.Where(value => value.Label.Score >= threshold).OrderByDescending(value => value.Label.Score).ToArray();

    private static int MatchHighConfidence(DetectionResult expected, DetectionResult actual, float threshold, float scoreTolerance, float iouTolerance)
    {
        Detection[] left = HighConfidence(expected, threshold);
        Detection[] right = HighConfidence(actual, threshold);
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
                if (iou >= iouTolerance && iou > bestIou) { best = index; bestIou = iou; }
            }
            if (best >= 0) { used[best] = true; matched++; }
        }
        return matched;
    }

    private static float IoU(Detection left, Detection right)
    {
        float x1 = MathF.Max(left.Box.X, right.Box.X), y1 = MathF.Max(left.Box.Y, right.Box.Y);
        float x2 = MathF.Min(left.Box.X + left.Box.Width, right.Box.X + right.Box.Width), y2 = MathF.Min(left.Box.Y + left.Box.Height, right.Box.Y + right.Box.Height);
        float intersection = MathF.Max(0, x2 - x1) * MathF.Max(0, y2 - y1);
        float union = left.Box.Width * left.Box.Height + right.Box.Width * right.Box.Height - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static object Percentiles(double[] samples)
    {
        double[] sorted = samples.OrderBy(value => value).ToArray();
        return new { p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1], p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], minMs = sorted[0], maxMs = sorted[^1], meanMs = sorted.Average() };
    }

    private static string DetectionFingerprint(DetectionResult result, float threshold)
    {
        var builder = new StringBuilder();
        foreach (Detection value in HighConfidence(result, threshold)) builder.Append(value.Label.Index).Append('|').Append(value.Label.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.Width.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.Height.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static object Device() => new { machine = Environment.MachineName, os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessorCount = Environment.ProcessorCount, processorIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not-recorded" };

    private void WriteJson(string path, object value)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(path);
        Console.WriteLine("PADDLE_DOCUMENT_ORT_STATIC_LAYOUT_BENCHMARK=" + path);
    }

    private static void RequireEnabled() =>
        Assert.IsTrue(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK") == "1", "Set DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK=1 to run the opt-in ONNX Runtime CPU 5+50 benchmark.");

    private static void RequireFile(string path, string label) => Assert.IsTrue(File.Exists(path), "Missing " + label + ": " + path);

    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
