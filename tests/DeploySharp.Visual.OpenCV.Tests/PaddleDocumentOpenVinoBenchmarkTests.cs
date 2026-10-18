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

/// <summary>Records formal OpenVINO CPU latency for the currently passing PP-Structure region families. / 记录当前已通过 PP-Structure 区域模型的 OpenVINO CPU 正式延迟。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentOpenVinoBenchmarkTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string LayoutImagePath = @"E:\Data\image\bus.jpg";
    private const string TableImagePath = @"E:\Model\PaddleDocument\validation\table_recognition.jpg";
    private const string SealImagePath = @"E:\Data\ocr\demo_1.jpg";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("paddle-doc/pp-doclayout-l", "pp-doclayout-l.onnx", "layout", 640)]
    [DataRow("paddle-doc/pp-docblocklayout", "pp-docblocklayout.onnx", "layout", 640)]
    [DataRow("paddle-doc/pp-doclayout-plus-l", "pp-doclayout-plus-l.onnx", "layout-plus", 800)]
    [DataRow("paddle-doc/rt-detr-h-layout-3cls", "rt-detr-h-layout-3cls.onnx", "layout-3cls", 640)]
    [DataRow("paddle-doc/rt-detr-h-layout-17cls", "rt-detr-h-layout-17cls.onnx", "layout-17cls", 640)]
    [DataRow("paddle-table/rt-detr-l-wired-cell-det", "rt-detr-l-wired-cell-det.onnx", "cell", 640)]
    [DataRow("paddle-table/rt-detr-l-wireless-cell-det", "rt-detr-l-wireless-cell-det.onnx", "cell", 640)]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void RegionModel_OpenVinoCpu_RecordsPreparedInputLatency(string modelId, string modelFile, string kind, int canvasSize)
    {
        RequireEnabled();
        string modelPath = Path.Combine(ModelRoot, modelFile);
        string imagePath = kind == "cell" ? TableImagePath : LayoutImagePath;
        RequireFile(modelPath, modelId);
        RequireFile(imagePath, "OpenVINO benchmark input");

        IReadOnlyList<string> labels = kind switch
        {
            "cell" => new[] { "table-cell" },
            "layout-plus" => PaddleDocumentProfiles.LayoutPlusLabels,
            "layout-3cls" => PaddleDocumentProfiles.Layout3Labels,
            "layout-17cls" => PaddleDocumentProfiles.Layout17Labels,
            "layout" when modelId == "paddle-doc/pp-docblocklayout" => PaddleDocumentProfiles.RegionLabels,
            _ => PaddleDocumentProfiles.Layout23Labels
        };
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
            PaddleDocumentModelCatalog.Get(modelId),
            labels,
            new VisualSize(canvasSize, canvasSize),
            includeGeometryInputs: true,
            countOutputName: null,
            scoreThreshold: 0);

        using var registry = new BackendRegistry().UseOpenVino();
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        VisualProfileSelection selection = profiles.Select(
            profile.CreateArtifact(modelPath, OpenVinoBackendProvider.BackendId),
            registry,
            request,
            profile.VisualProfile.Task);
        using var pipeline = new VisualPipeline(registry, selection, request);
        using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(
            new OpenCvVisualInputFactory(), imagePath, profile, inputId: "paddle-document-openvino-benchmark-" + modelId.Replace('/', '-'));

        using var ort = new BackendRegistry().UseOnnxRuntime();
        var ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var reference = new VisualPipeline(
            ort,
            profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), ort, ortRequest, profile.VisualProfile.Task),
            ortRequest);
        Detection[] expected = HighConfidence(reference.Run(input).GetValue<DetectionResult>());

        const int warmupCount = 5;
        const int measurementCount = 50;
        for (int index = 0; index < warmupCount; index++) ValidateResult(pipeline.Run(input).GetValue<DetectionResult>());
        var samples = new double[measurementCount];
        DetectionResult? firstMeasuredResult = null;
        for (int index = 0; index < measurementCount; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
            stopwatch.Stop();
            ValidateResult(result);
            firstMeasuredResult ??= result;
            Assert.AreEqual(firstMeasuredResult.Detections.Count, result.Detections.Count, "Detection count changed during the measured run.");
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        Detection[] actual = HighConfidence(firstMeasuredResult!);
        const float scoreTolerance = 0.01f;
        const float coordinateTolerancePx = 1.5f;
        Assert.AreEqual(expected.Length, actual.Length, "OpenVINO and ORT high-confidence detection counts differ.");
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index].Label.Index, actual[index].Label.Index, "Backend class IDs differ.");
            Assert.AreEqual(expected[index].Label.Score, actual[index].Label.Score, scoreTolerance, "Backend scores exceed tolerance.");
            Assert.AreEqual(expected[index].Box.X, actual[index].Box.X, coordinateTolerancePx, "Backend box X exceeds tolerance.");
            Assert.AreEqual(expected[index].Box.Y, actual[index].Box.Y, coordinateTolerancePx, "Backend box Y exceeds tolerance.");
            Assert.AreEqual(expected[index].Box.Width, actual[index].Box.Width, coordinateTolerancePx, "Backend box width exceeds tolerance.");
            Assert.AreEqual(expected[index].Box.Height, actual[index].Box.Height, coordinateTolerancePx, "Backend box height exceeds tolerance.");
        }

        WriteReport(modelId, modelFile, imagePath, modelPath, profile.VisualProfile.Input.ShapePattern.ToString(),
            profile.VisualProfile.ModelId.Value, firstMeasuredResult!, expected.Length, actual.Length, samples, warmupCount, measurementCount, scoreTolerance, coordinateTolerancePx);
    }

    [TestMethod]
    [DataRow("paddle-seal/ppocrv4-mobile", "ppocrv4-mobile-seal-det.onnx")]
    [DataRow("paddle-seal/ppocrv4-server", "ppocrv4-server-seal-det.onnx")]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void SealModel_OpenVinoCpu_RecordsPreparedInputLatency(string modelId, string modelFile)
    {
        RequireEnabled();
        string modelPath = Path.Combine(ModelRoot, modelFile);
        RequireFile(modelPath, modelId);
        RequireFile(SealImagePath, "OpenVINO seal benchmark input");
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(PaddleDocumentModelCatalog.Get(modelId), new VisualSize(224, 224));

        using var registry = new BackendRegistry().UseOpenVino();
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        VisualProfileSelection selection = profiles.Select(profile.CreateArtifact(modelPath, OpenVinoBackendProvider.BackendId), registry, request, profile.VisualProfile.Task);
        using var pipeline = new VisualPipeline(registry, selection, request);
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(SealImagePath, profile.VisualProfile, inputId: "paddle-document-openvino-seal-benchmark-" + modelId.Replace('/', '-'));

        using var ort = new BackendRegistry().UseOnnxRuntime();
        var ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var reference = new VisualPipeline(ort, profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), ort, ortRequest, profile.VisualProfile.Task), ortRequest);
        PaddleDocumentSealResult referenceResult = reference.Run(input).GetValue<PaddleDocumentSealResult>();
        ValidateSealResult(referenceResult);

        const int warmupCount = 5;
        const int measurementCount = 50;
        for (int index = 0; index < warmupCount; index++) ValidateSealResult(pipeline.Run(input).GetValue<PaddleDocumentSealResult>());
        var samples = new double[measurementCount];
        PaddleDocumentSealResult? firstMeasuredResult = null;
        for (int index = 0; index < measurementCount; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            PaddleDocumentSealResult result = pipeline.Run(input).GetValue<PaddleDocumentSealResult>();
            stopwatch.Stop();
            ValidateSealResult(result);
            firstMeasuredResult ??= result;
            Assert.AreEqual(firstMeasuredResult.MaskWidth, result.MaskWidth);
            Assert.AreEqual(firstMeasuredResult.MaskHeight, result.MaskHeight);
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        string reportPath = Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-openvino-" + modelFile.Replace(".onnx", "-benchmark.json", StringComparison.Ordinal));
        WriteJson(reportPath, new
        {
            schemaVersion = "deploysharp-paddle-document-openvino-seal-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            device = Device(),
            model = new { id = modelId, file = modelFile, sha256 = Sha256(modelPath), inputShape = "[1,3,224,224]" },
            input = new { file = Path.GetFileName(SealImagePath), sha256 = Sha256(SealImagePath), inputId = input.InputId },
            backend = new { id = "openvino-cpu", runtimePackage = "OpenVINO C# API 3.3.1 / runtime 2026.2.1" },
            protocol = new { warmupCount, measurementCount, batch = 1, sessions = 1, inputReuse = "One PreparedVisualInput reused for every call.", timing = "Synchronous VisualPipeline.Run including OpenVINO execution and PaddleDocumentSealDecoder; excludes image decode/preprocessing, model/session creation and one-time ORT reference.", percentile = "Nearest-rank" },
            parity = new { referenceBackend = "onnxruntime-cpu", referenceMask = new { width = referenceResult.MaskWidth, height = referenceResult.MaskHeight }, referenceRegionCount = referenceResult.Regions.Count, comparison = "Mask shape and decoder contract only; no raw probability-map numerical parity claim." },
            result = new { mask = new { width = firstMeasuredResult!.MaskWidth, height = firstMeasuredResult.MaskHeight }, regionCount = firstMeasuredResult.Regions.Count, regionFingerprintSha256 = RegionFingerprint(firstMeasuredResult) },
            timing = Percentiles(samples),
            samplesMs = samples
        });
    }

    private void WriteReport(string modelId, string modelFile, string imagePath, string modelPath, string inputShape, string profileId, DetectionResult result, int expectedCount, int actualCount, double[] samples, int warmupCount, int measurementCount, float scoreTolerance, float coordinateTolerancePx)
    {
        string reportPath = Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-openvino-" + modelFile.Replace(".onnx", "-benchmark.json", StringComparison.Ordinal));
        WriteJson(reportPath, new
        {
            schemaVersion = "deploysharp-paddle-document-openvino-region-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            device = Device(),
            model = new { id = modelId, file = modelFile, sha256 = Sha256(modelPath), inputShape },
            input = new { file = Path.GetFileName(imagePath), sha256 = Sha256(imagePath), profileId },
            backend = new { id = "openvino-cpu", runtimePackage = "OpenVINO C# API 3.3.1 / runtime 2026.2.1" },
            protocol = new { warmupCount, measurementCount, batch = 1, sessions = 1, inputReuse = "One PreparedVisualInput reused for every call.", timing = "Synchronous VisualPipeline.Run including OpenVINO execution and Paddle NMS Decoder; excludes image decode/preprocessing, model/session creation and one-time ORT reference.", percentile = "Nearest-rank" },
            parity = new { referenceBackend = "onnxruntime-cpu", matchedHighConfidenceDetectionCount = actualCount, expectedHighConfidenceDetectionCount = expectedCount, confidenceThreshold = 0.5f, scoreTolerance, coordinateTolerancePx },
            result = new { detectionCount = result.Detections.Count, highConfidenceDetectionCount = actualCount, highConfidenceFingerprintSha256 = DetectionFingerprint(result) },
            timing = Percentiles(samples),
            samplesMs = samples
        });
    }

    private static void ValidateResult(DetectionResult result)
    {
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Detections.Count > 0);
        Assert.IsTrue(result.Detections.All(value => float.IsFinite(value.Label.Score) && float.IsFinite(value.Box.X) && float.IsFinite(value.Box.Y) && float.IsFinite(value.Box.Width) && float.IsFinite(value.Box.Height)));
    }

    private static void ValidateSealResult(PaddleDocumentSealResult result)
    {
        Assert.IsNotNull(result);
        Assert.IsTrue(result.MaskWidth > 0 && result.MaskHeight > 0);
        Assert.IsTrue(result.Regions.All(value => float.IsFinite(value.Score) && value.Score >= 0 && value.Score <= 1));
    }

    private static Detection[] HighConfidence(DetectionResult result) => result.Detections.Where(value => value.Label.Score >= .5f).OrderBy(value => value.Label.Index).ThenByDescending(value => value.Label.Score).ToArray();

    private static object Percentiles(double[] samples)
    {
        double[] sorted = samples.OrderBy(value => value).ToArray();
        return new { p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1], p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], minMs = sorted[0], maxMs = sorted[^1], meanMs = sorted.Average() };
    }

    private static string DetectionFingerprint(DetectionResult result)
    {
        var builder = new StringBuilder();
        foreach (Detection value in HighConfidence(result)) builder.Append(value.Label.Index).Append('|').Append(value.Label.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.Width.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Box.Height.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string RegionFingerprint(PaddleDocumentSealResult result)
    {
        var builder = new StringBuilder();
        foreach (PaddleDocumentRegion value in result.Regions) builder.Append(value.Category).Append('|').Append(value.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Bounds.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Bounds.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Bounds.Width.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(value.Bounds.Height.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static object Device() => new { machine = Environment.MachineName, os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessorCount = Environment.ProcessorCount, processorIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not-recorded" };

    private void WriteJson(string path, object value)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(path);
        Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_BENCHMARK=" + path);
    }

    private static void RequireEnabled()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_BENCHMARK") != "1") Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_BENCHMARK=1 to run the opt-in OpenVINO 5+50 benchmark.");
    }

    private static void RequireFile(string path, string label) => Assert.IsTrue(File.Exists(path), "Missing " + label + ": " + path);

    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
