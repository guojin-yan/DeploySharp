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
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Records opt-in PP-Structure layout OpenCV DNN latency samples. / 记录显式启用的 PP-Structure 版面 OpenCV DNN 延迟样本。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentOpenCvLayoutBenchmarkTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string ImagePath = @"E:\Data\image\bus.jpg";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("paddle-doc/pp-doclayout-l", "pp-doclayout-l.onnx", 640)]
    [DataRow("paddle-doc/pp-docblocklayout", "pp-docblocklayout.onnx", 640)]
    [DataRow("paddle-doc/pp-doclayout-plus-l", "pp-doclayout-plus-l.onnx", 800)]
    [DataRow("paddle-doc/rt-detr-h-layout-3cls", "rt-detr-h-layout-3cls.onnx", 640)]
    [DataRow("paddle-doc/rt-detr-h-layout-17cls", "rt-detr-h-layout-17cls.onnx", 640)]
    [DataRow("paddle-table/rt-detr-l-wired-cell-det", "rt-detr-l-wired-cell-det.onnx", 640)]
    [DataRow("paddle-table/rt-detr-l-wireless-cell-det", "rt-detr-l-wireless-cell-det.onnx", 640)]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void PpDocLayout_OpenCvDnn_RecordsPreparedInputLatency(string modelId, string modelFile, int canvasSize)
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_LAYOUT_BENCHMARK") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_LAYOUT_BENCHMARK=1 to run the opt-in 5+50 OpenCV DNN benchmark.");
        string? selectedModels = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_LAYOUT_BENCHMARK_MODELS");
        if (!string.IsNullOrWhiteSpace(selectedModels) && !selectedModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(modelId, StringComparer.Ordinal))
            Assert.Inconclusive("This benchmark row was not selected by DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_LAYOUT_BENCHMARK_MODELS.");

        string modelPath = Path.Combine(ModelRoot, modelFile);
        Assert.IsTrue(File.Exists(modelPath), "Missing local PP-Structure layout ONNX: " + modelPath);
        Assert.IsTrue(File.Exists(ImagePath), "Missing benchmark input image: " + ImagePath);

        IReadOnlyList<string> labels = modelId.StartsWith("paddle-table/rt-detr-l-", StringComparison.Ordinal)
            ? new[] { "table-cell" }
            : modelId == "paddle-doc/pp-doclayout-plus-l"
            ? PaddleDocumentProfiles.LayoutPlusLabels
            : modelId == "paddle-doc/rt-detr-h-layout-3cls"
                ? PaddleDocumentProfiles.Layout3Labels
                : modelId == "paddle-doc/rt-detr-h-layout-17cls"
                    ? PaddleDocumentProfiles.Layout17Labels
                    : modelId == "paddle-doc/pp-docblocklayout" ? PaddleDocumentProfiles.RegionLabels : PaddleDocumentProfiles.Layout23Labels;
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
            PaddleDocumentModelCatalog.Get(modelId),
            labels,
            new VisualSize(canvasSize, canvasSize),
            includeGeometryInputs: true,
            countOutputName: null,
            scoreThreshold: 0);
        var contract = new OpenCvDnnModelContract(
            profile.VisualProfile.ModelId,
            new[]
            {
                new TensorDescriptor("image", TensorElementType.Float32, new TensorShape(-1, 3, canvasSize, canvasSize)),
                new TensorDescriptor("im_shape", TensorElementType.Float32, new TensorShape(-1, 2)),
                new TensorDescriptor("scale_factor", TensorElementType.Float32, new TensorShape(-1, 2))
            },
            new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(-1, 6)) },
            imageInputNames: new[] { "image" });

        using var registry = new BackendRegistry().UseOpenCvDnn(
            new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
        VisualProfileSelection selection = profiles.Select(
            profile.CreateArtifact(modelPath, OpenCvDnnBackendProvider.BackendId),
            registry,
            request,
            profile.VisualProfile.Task);

        using var pipeline = new VisualPipeline(registry, selection, request);
        using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(
            new OpenCvVisualInputFactory(), ImagePath, profile, inputId: "paddle-document-opencv-layout-benchmark-" + modelId.Replace('/', '-'));

        // Run a one-time ORT reference before timing to keep this performance row
        // attached to the existing bounded semantic contract for the exact input.
        using var ort = new BackendRegistry().UseOnnxRuntime();
        var ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var reference = new VisualPipeline(
            ort,
            profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), ort, ortRequest, profile.VisualProfile.Task),
            ortRequest);
        Detection[] expected = HighConfidence(reference.Run(input).GetValue<DetectionResult>());

        const int warmupCount = 5;
        const int measurementCount = 50;
        const float scoreTolerance = 0.001f;
        const float coordinateTolerancePx = 0.5f;
        for (int index = 0; index < warmupCount; index++)
        {
            DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
            ValidateResult(result);
        }

        var samples = new double[measurementCount];
        DetectionResult? firstMeasuredResult = null;
        for (int index = 0; index < measurementCount; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
            stopwatch.Stop();
            ValidateResult(result);
            if (firstMeasuredResult == null)
                firstMeasuredResult = result;
            else
                Assert.AreEqual(firstMeasuredResult.Detections.Count, result.Detections.Count, "Detection count changed during the measured run.");
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        Detection[] actual = HighConfidence(firstMeasuredResult!);
        Assert.AreEqual(expected.Length, actual.Length, "OpenCV and ORT high-confidence detection counts differ.");
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index].Label.Index, actual[index].Label.Index, "Backend class IDs differ.");
            Assert.AreEqual(expected[index].Label.Score, actual[index].Label.Score, scoreTolerance, "Backend scores exceed tolerance.");
            Assert.AreEqual(expected[index].Box.X, actual[index].Box.X, coordinateTolerancePx, "Backend box X exceeds tolerance.");
            Assert.AreEqual(expected[index].Box.Y, actual[index].Box.Y, coordinateTolerancePx, "Backend box Y exceeds tolerance.");
            Assert.AreEqual(expected[index].Box.Width, actual[index].Box.Width, coordinateTolerancePx, "Backend box width exceeds tolerance.");
            Assert.AreEqual(expected[index].Box.Height, actual[index].Box.Height, coordinateTolerancePx, "Backend box height exceeds tolerance.");
        }

        string? configuredReportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_LAYOUT_BENCHMARK_REPORT_PATH");
        string reportPath = !string.IsNullOrWhiteSpace(configuredReportPath) && modelId == "paddle-doc/pp-doclayout-l"
            ? configuredReportPath
            : Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-opencv-" + modelFile.Replace(".onnx", "-benchmark.json", StringComparison.Ordinal));
        string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
        string report = JsonSerializer.Serialize(new
        {
            schemaVersion = "deploysharp-paddle-document-opencv-layout-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            device = new
            {
                machine = Environment.MachineName,
                os = RuntimeInformation.OSDescription,
                runtime = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                logicalProcessorCount = Environment.ProcessorCount,
                processorIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not-recorded",
                gpuClockState = "not-applicable-to-cpu-backend"
            },
            model = new
            {
                id = modelId,
                file = modelFile,
                sha256 = Sha256(modelPath),
                inputShape = "[1,3,640,640]"
            },
            input = new { file = Path.GetFileName(ImagePath), sha256 = Sha256(ImagePath), inputId = input.InputId },
            backend = new
            {
                id = "opencv-dnn-cpu",
                runtimePackage = "JYPPX.OpenCV.runtime.win-x64 5.0.0",
                fusion = true,
                winograd = true,
                specializeDynamicInputShapes = true,
                importerNote = "OpenCV logged that unused graph output fetch_name_1 has no imported layer; selected fetch_name_0 execution and the bounded ORT parity contract passed."
            },
            protocol = new
            {
                warmupCount,
                measurementCount,
                batch = 1,
                sessions = 1,
                inputReuse = "One PreparedVisualInput created before warm-up and reused for all calls.",
                timing = "Synchronous VisualPipeline.Run; includes OpenCV DNN execution and DeploySharp decoding; excludes image decode/preprocessing, model load, profile/session creation, hashing and one-time ORT parity reference.",
                percentile = "Nearest-rank"
            },
            parity = new
            {
                referenceBackend = "onnxruntime-cpu",
                matchedDetectionCount = actual.Length,
                confidenceThreshold = 0.5f,
                scoreTolerance,
                coordinateTolerancePx
            },
            result = new
            {
                detectionCount = firstMeasuredResult!.Detections.Count,
                highConfidenceDetectionCount = actual.Length,
                highConfidenceFingerprintSha256 = DetectionFingerprint(actual)
            },
            timing = Percentiles(samples),
            samplesMs = samples
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(reportPath, report);
        TestContext.AddResultFile(reportPath);
        Console.WriteLine("PADDLE_DOCUMENT_OPENCV_LAYOUT_BENCHMARK=" + reportPath);
        Console.WriteLine("PADDLE_DOCUMENT_OPENCV_LAYOUT_BENCHMARK_STATUS=pass");
    }

    private static void ValidateResult(DetectionResult result)
    {
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Detections.Count > 0, "PP-DocLayout-L returned no layout detections.");
        Assert.IsTrue(result.Detections.All(detection =>
            float.IsFinite(detection.Box.X) && float.IsFinite(detection.Box.Y) &&
            float.IsFinite(detection.Box.Width) && float.IsFinite(detection.Box.Height) &&
            float.IsFinite(detection.Label.Score)), "PP-DocLayout-L returned non-finite detections.");
    }

    private static Detection[] HighConfidence(DetectionResult result)
        => result.Detections.Where(detection => detection.Label.Score >= 0.5f)
            .OrderBy(detection => detection.Label.Index)
            .ThenByDescending(detection => detection.Label.Score)
            .ToArray();

    private static object Percentiles(double[] samples)
    {
        double[] sorted = samples.OrderBy(value => value).ToArray();
        return new
        {
            p50Ms = sorted[(int)Math.Ceiling(sorted.Length * 0.50) - 1],
            p95Ms = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1],
            minMs = sorted[0],
            maxMs = sorted[^1],
            meanMs = sorted.Average()
        };
    }

    private static string DetectionFingerprint(Detection[] detections)
    {
        var builder = new StringBuilder();
        foreach (Detection detection in detections)
        {
            builder.Append(detection.Label.Index).Append('|')
                .Append(detection.Label.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(detection.Box.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(detection.Box.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(detection.Box.Width.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(detection.Box.Height.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
