using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Records formal OpenVINO CPU latency and bounded ORT parity for UVDoc. / 记录 UVDoc 的 OpenVINO CPU 正式延迟和有界 ORT 对照。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentUvDocOpenVinoBenchmarkTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string ModelId = "paddle-doc/uvdoc";
    private const string ModelFile = "uvdoc.onnx";
    private const string ImagePath = @"E:\Data\image\bus.jpg";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void UvDoc_OpenVinoCpu_RecordsPreparedInputLatency()
    {
        RequireEnabled();
        string modelPath = Path.Combine(ModelRoot, ModelFile);
        RequireFile(modelPath, ModelId);
        RequireFile(ImagePath, "UVDoc benchmark input");

        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateUnwarping(
            PaddleDocumentModelCatalog.Get(ModelId), new VisualSize(640, 640));
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            ImagePath, profile.VisualProfile, inputId: "paddle-document-openvino-uvdoc-benchmark");

        using var openVinoRegistry = new BackendRegistry().UseOpenVino();
        var openVinoRequest = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        using var openVinoPipeline = new VisualPipeline(
            openVinoRegistry,
            profiles.Select(profile.CreateArtifact(modelPath, OpenVinoBackendProvider.BackendId), openVinoRegistry, openVinoRequest, profile.VisualProfile.Task),
            openVinoRequest);

        using var ortRegistry = new BackendRegistry().UseOnnxRuntime();
        var ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var ortPipeline = new VisualPipeline(
            ortRegistry,
            profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, profile.VisualProfile.Task),
            ortRequest);

        PaddleDocumentUnwarpingResult expected = ortPipeline.Run(input).GetValue<PaddleDocumentUnwarpingResult>();
        ValidateResult(expected);

        const int warmupCount = 5;
        const int measurementCount = 50;
        for (int index = 0; index < warmupCount; index++)
            ValidateResult(openVinoPipeline.Run(input).GetValue<PaddleDocumentUnwarpingResult>());

        var samples = new double[measurementCount];
        PaddleDocumentUnwarpingResult? firstMeasuredResult = null;
        for (int index = 0; index < measurementCount; index++)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            PaddleDocumentUnwarpingResult result = openVinoPipeline.Run(input).GetValue<PaddleDocumentUnwarpingResult>();
            stopwatch.Stop();
            ValidateResult(result);
            firstMeasuredResult ??= result;
            Assert.AreEqual(firstMeasuredResult.Width, result.Width, "UVDoc output width changed during the measured run.");
            Assert.AreEqual(firstMeasuredResult.Height, result.Height, "UVDoc output height changed during the measured run.");
            Assert.AreEqual(firstMeasuredResult.Channels, result.Channels, "UVDoc output channels changed during the measured run.");
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        PaddleDocumentUnwarpingResult actual = firstMeasuredResult!;
        (double maxAbs, double meanAbs) = Difference(expected.Pixels, actual.Pixels);
        Assert.IsTrue(maxAbs <= 0.2 && meanAbs <= 0.01,
            $"UVDoc OpenVINO/ORT bounded raw parity exceeded: max={maxAbs:R}, mean={meanAbs:R}.");

        string reportPath = Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-openvino-uvdoc-benchmark.json");
        WriteJson(reportPath, new
        {
            schemaVersion = "deploysharp-paddle-document-openvino-uvdoc-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            device = Device(),
            model = new { id = ModelId, file = ModelFile, sha256 = Sha256(modelPath), inputShape = "[1,3,640,640]" },
            input = new { file = Path.GetFileName(ImagePath), sha256 = Sha256(ImagePath), inputId = input.InputId },
            backend = new { id = "openvino-cpu", runtimePackage = "OpenVINO C# API 3.3.1 / runtime 2026.2.1" },
            protocol = new { warmupCount, measurementCount, batch = 1, sessions = 1, inputReuse = "One PreparedVisualInput reused for every call.", timing = "Synchronous VisualPipeline.Run including OpenVINO execution and PaddleDocumentUnwarpingDecoder; excludes image decode/preprocessing, model/session creation and one-time ORT reference.", percentile = "Nearest-rank" },
            parity = new { referenceBackend = "onnxruntime-cpu", outputShape = new[] { expected.Width, expected.Height, expected.Channels }, maxAbsoluteDifference = maxAbs, meanAbsoluteDifference = meanAbs, maxTolerance = 0.2, meanTolerance = 0.01, acceptance = "Bounded raw-output comparison only; not pixel equivalence or visual-quality evidence." },
            result = new { width = actual.Width, height = actual.Height, channels = actual.Channels, finite = actual.Pixels.All(float.IsFinite), pixelFingerprintSha256 = FloatFingerprint(actual.Pixels) },
            timing = Percentiles(samples),
            samplesMs = samples
        });
    }

    private static void ValidateResult(PaddleDocumentUnwarpingResult result)
    {
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Width > 0 && result.Height > 0 && result.Channels == 3);
        Assert.IsTrue(result.Pixels.Count == result.Width * result.Height * result.Channels);
        Assert.IsTrue(result.Pixels.All(float.IsFinite), "UVDoc output contains a non-finite pixel.");
    }

    private static (double maxAbs, double meanAbs) Difference(System.Collections.Generic.IReadOnlyList<float> expected, System.Collections.Generic.IReadOnlyList<float> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        double max = 0;
        double sum = 0;
        for (int index = 0; index < expected.Count; index++)
        {
            double delta = Math.Abs(expected[index] - actual[index]);
            max = Math.Max(max, delta);
            sum += delta;
        }
        return (max, sum / expected.Count);
    }

    private static object Percentiles(double[] samples)
    {
        double[] sorted = samples.OrderBy(value => value).ToArray();
        return new { p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1], p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], minMs = sorted[0], maxMs = sorted[^1], meanMs = sorted.Average() };
    }

    private static string FloatFingerprint(System.Collections.Generic.IReadOnlyList<float> values)
    {
        byte[] bytes = new byte[values.Count * sizeof(float)];
        Buffer.BlockCopy(values.ToArray(), 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static object Device() => new { machine = Environment.MachineName, os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessorCount = Environment.ProcessorCount, processorIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not-recorded" };

    private void WriteJson(string path, object value)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(path);
        Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_UVDOC_BENCHMARK=" + path);
    }

    private static void RequireEnabled() => Assert.IsTrue(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_BENCHMARK") == "1", "Set DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_BENCHMARK=1 to run the opt-in OpenVINO 5+50 benchmark.");
    private static void RequireFile(string path, string label) => Assert.IsTrue(File.Exists(path), "Missing " + label + ": " + path);
    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
