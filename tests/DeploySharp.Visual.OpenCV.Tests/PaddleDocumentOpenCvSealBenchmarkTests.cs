using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Records dedicated PP-OCRv4 seal OpenCV DNN latency and decoder evidence. / 记录专用 PP-OCRv4 印章 OpenCV DNN 延迟与 Decoder 证据。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentOpenCvSealBenchmarkTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string ImagePath = @"E:\Data\ocr\demo_1.jpg";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("paddle-seal/ppocrv4-mobile", "ppocrv4-mobile-seal-det.onnx")]
    [DataRow("paddle-seal/ppocrv4-server", "ppocrv4-server-seal-det.onnx")]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void PpOcrV4Seal_OpenCvDnn_RecordsPreparedInputLatency(string modelId, string modelFile)
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_SEAL_BENCHMARK") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_SEAL_BENCHMARK=1 to run the opt-in 5+50 OpenCV DNN seal benchmark.");

        string modelPath = Path.Combine(ModelRoot, modelFile);
        Assert.IsTrue(File.Exists(modelPath), "Missing local PP-OCRv4 seal ONNX: " + modelPath);
        Assert.IsTrue(File.Exists(ImagePath), "Missing seal benchmark input image: " + ImagePath);

        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(
            PaddleDocumentModelCatalog.Get(modelId),
            new VisualSize(224, 224),
            maximumBatch: 1);
        var contract = new OpenCvDnnModelContract(
            profile.VisualProfile.ModelId,
            new[] { new TensorDescriptor("x", TensorElementType.Float32, new TensorShape(-1, 3, 224, 224)) },
            new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(-1, 1, 224, 224)) },
            imageInputNames: new[] { "x" });

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
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            ImagePath,
            profile.VisualProfile,
            inputId: "paddle-document-opencv-seal-benchmark-" + modelId.Replace('/', '-'));

        // Keep a CPU reference for the same exact profile. Seal OpenCV importer
        // implementations can produce a different probability map; this is a
        // shape/decoder contract reference, not a numerical parity assertion.
        PaddleDocumentSealResult referenceResult;
        using (var ort = new BackendRegistry().UseOnnxRuntime())
        {
            var ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var reference = new VisualPipeline(
                ort,
                profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), ort, ortRequest, profile.VisualProfile.Task),
                ortRequest);
            referenceResult = reference.Run(input).GetValue<PaddleDocumentSealResult>();
            ValidateResult(referenceResult);
        }

        const int warmupCount = 5;
        const int measurementCount = 50;
        for (int index = 0; index < warmupCount; index++)
            ValidateResult(pipeline.Run(input).GetValue<PaddleDocumentSealResult>());

        var samples = new double[measurementCount];
        PaddleDocumentSealResult? firstMeasuredResult = null;
        for (int index = 0; index < measurementCount; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            PaddleDocumentSealResult result = pipeline.Run(input).GetValue<PaddleDocumentSealResult>();
            stopwatch.Stop();
            ValidateResult(result);
            firstMeasuredResult ??= result;
            Assert.AreEqual(firstMeasuredResult.MaskWidth, result.MaskWidth, "Seal mask width changed during the measured run.");
            Assert.AreEqual(firstMeasuredResult.MaskHeight, result.MaskHeight, "Seal mask height changed during the measured run.");
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        string reportPath = Path.Combine(
            TestContext.TestResultsDirectory!,
            "paddle-document-opencv-" + modelFile.Replace(".onnx", "-benchmark.json", StringComparison.Ordinal));
        string? configuredReportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_SEAL_BENCHMARK_REPORT_PATH");
        if (!string.IsNullOrWhiteSpace(configuredReportPath) && modelId == "paddle-seal/ppocrv4-mobile")
            reportPath = configuredReportPath;
        string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);

        string report = JsonSerializer.Serialize(new
        {
            schemaVersion = "deploysharp-paddle-document-opencv-seal-benchmark-v1",
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
            model = new { id = modelId, file = modelFile, sha256 = Sha256(modelPath), inputShape = "[1,3,224,224]" },
            input = new { file = Path.GetFileName(ImagePath), sha256 = Sha256(ImagePath), inputId = input.InputId },
            backend = new
            {
                id = "opencv-dnn-cpu",
                runtimePackage = "JYPPX.OpenCV.runtime.win-x64 5.0.0",
                fusion = true,
                winograd = true,
                specializeDynamicInputShapes = true,
                importerNote = "Dedicated seal probability-map contract; no generic NMS decoder is used."
            },
            protocol = new
            {
                warmupCount,
                measurementCount,
                batch = 1,
                sessions = 1,
                inputReuse = "One PreparedVisualInput created before warm-up and reused for all calls.",
                timing = "Synchronous VisualPipeline.Run; includes OpenCV DNN execution and PaddleDocumentSealDecoder; excludes image decode/preprocessing, model load, profile/session creation, hashing and one-time ORT reference.",
                percentile = "Nearest-rank"
            },
            parity = new
            {
                referenceBackend = "onnxruntime-cpu",
                referenceMask = new { width = referenceResult.MaskWidth, height = referenceResult.MaskHeight },
                referenceRegionCount = referenceResult.Regions.Count,
                comparison = "Shape and decoder contract only; raw probability-map numerical parity is not claimed for OpenCV DNN."
            },
            result = new
            {
                mask = new { width = firstMeasuredResult!.MaskWidth, height = firstMeasuredResult.MaskHeight },
                regionCount = firstMeasuredResult.Regions.Count,
                regionFingerprintSha256 = RegionFingerprint(firstMeasuredResult)
            },
            timing = Percentiles(samples),
            samplesMs = samples
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(reportPath, report);
        TestContext.AddResultFile(reportPath);
        Console.WriteLine("PADDLE_DOCUMENT_OPENCV_SEAL_BENCHMARK=" + reportPath);
        Console.WriteLine("PADDLE_DOCUMENT_OPENCV_SEAL_BENCHMARK_STATUS=pass");
    }

    private static void ValidateResult(PaddleDocumentSealResult result)
    {
        Assert.IsNotNull(result);
        Assert.IsTrue(result.MaskWidth > 0 && result.MaskHeight > 0);
        Assert.IsTrue(result.Regions.All(region =>
            float.IsFinite(region.Score) && region.Score >= 0 && region.Score <= 1 &&
            float.IsFinite(region.Bounds.X) && float.IsFinite(region.Bounds.Y) &&
            float.IsFinite(region.Bounds.Width) && float.IsFinite(region.Bounds.Height)));
    }

    private static object Percentiles(double[] samples)
    {
        double[] sorted = samples.OrderBy(value => value).ToArray();
        return new
        {
            p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .50) - 1],
            p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1],
            minMs = sorted[0],
            maxMs = sorted[^1],
            meanMs = sorted.Average()
        };
    }

    private static string RegionFingerprint(PaddleDocumentSealResult result)
    {
        var builder = new StringBuilder();
        foreach (PaddleDocumentRegion region in result.Regions)
        {
            builder.Append(region.Category).Append('|')
                .Append(region.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(region.Bounds.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(region.Bounds.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(region.Bounds.Width.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(region.Bounds.Height.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string Sha256(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
