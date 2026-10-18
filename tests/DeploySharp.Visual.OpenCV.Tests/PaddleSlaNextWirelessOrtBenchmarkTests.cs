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
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Records formal ONNX Runtime CPU latency for the official wireless SLANeXt graph. / 记录官方 wireless SLANeXt 图的 ONNX Runtime CPU 正式延迟。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleSlaNextWirelessOrtBenchmarkTests
{
    private const string CompatibilityPath = @"E:\Model\PaddleDocument\onnx-normalized-rerun-20260929\slanext-wireless-openvino-compat.onnx";
    private const string OriginalPath = @"E:\Model\PaddleDocument\onnx\slanext-wireless.onnx";
    private const string ImagePath = @"E:\Model\PaddleDocument\validation\table_recognition.jpg";
    private const string CompatibilitySha = "9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6";
    private const string ModelId = "paddle-table/slanext-wireless";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void WirelessSlaNextOnnxRuntimeCpu_RecordsPreparedInputLatency()
    {
        RequireEnabled();
        RequireFile(CompatibilityPath, "OpenVINO-compatible wireless SLANeXt graph");
        RequireFile(OriginalPath, "official wireless SLANeXt graph");
        RequireFile(ImagePath, "wireless SLANeXt benchmark input");
        Assert.AreEqual(CompatibilitySha, Sha256(CompatibilityPath), "The local compatibility graph differs from the registered derived artifact.");

        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateTableStructure(
            PaddleDocumentModelCatalog.Get(ModelId), modelSize: new VisualSize(512, 512));
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            ImagePath, profile.VisualProfile, inputId: "paddle-document-ort-slanext-wireless-benchmark");

        using var ortRegistry = new BackendRegistry().UseOnnxRuntime();
        var ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var ortPipeline = new VisualPipeline(
            ortRegistry,
            profiles.Select(profile.CreateArtifact(OriginalPath, OnnxRuntimeBackendProvider.BackendId), ortRegistry, ortRequest, profile.VisualProfile.Task),
            ortRequest);

        using var openVinoRegistry = new BackendRegistry().UseOpenVino();
        var openVinoRequest = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        using var openVinoPipeline = new VisualPipeline(
            openVinoRegistry,
            profiles.Select(profile.CreateArtifactWithSha256(CompatibilityPath, CompatibilitySha, OpenVinoBackendProvider.BackendId), openVinoRegistry, openVinoRequest, profile.VisualProfile.Task),
            openVinoRequest);

        PaddleDocumentTableResult expected = openVinoPipeline.Run(input).GetValue<PaddleDocumentTableResult>();
        ValidateResult(expected);

        const int warmupCount = 5;
        const int measurementCount = 50;
        for (int index = 0; index < warmupCount; index++)
            ValidateAndMatch(expected, ortPipeline.Run(input).GetValue<PaddleDocumentTableResult>());

        var samples = new double[measurementCount];
        PaddleDocumentTableResult? firstMeasuredResult = null;
        for (int index = 0; index < measurementCount; index++)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            PaddleDocumentTableResult result = ortPipeline.Run(input).GetValue<PaddleDocumentTableResult>();
            stopwatch.Stop();
            ValidateAndMatch(expected, result);
            firstMeasuredResult ??= result;
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        PaddleDocumentTableResult actual = firstMeasuredResult!;
        string expectedMarkupSha = Sha256(Encoding.UTF8.GetBytes(expected.Markup));
        string actualMarkupSha = Sha256(Encoding.UTF8.GetBytes(actual.Markup));
        Assert.AreEqual(expectedMarkupSha, actualMarkupSha, "Wireless SLANeXt markup differs between OpenVINO compatibility graph and ORT source graph.");

        string reportPath = Path.Combine(
            Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ORT_EVIDENCE_DIR") ?? TestContext.TestResultsDirectory!,
            "paddle-document-ort-slanext-wireless-benchmark-20261018.json");
        WriteJson(reportPath, new
        {
            schemaVersion = "deploysharp-paddle-document-ort-slanext-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            device = Device(),
            model = new { id = ModelId, sourceFile = Path.GetFileName(OriginalPath), sourceSha256 = Sha256(OriginalPath), compatibilityFile = Path.GetFileName(CompatibilityPath), compatibilitySha256 = CompatibilitySha, transformation = "Alpha-rename Loop-body formal parameters; source graph and weights unchanged.", inputShape = input.Tensor.Shape.ToString() },
            input = new { file = Path.GetFileName(ImagePath), sha256 = Sha256(ImagePath), inputId = input.InputId },
            backend = new { id = "onnxruntime-cpu", runtimePackage = "Microsoft.ML.OnnxRuntime 1.28.0" },
            protocol = new { warmupCount, measurementCount, batch = 1, sessions = 1, inputReuse = "One PreparedVisualInput reused for every call.", timing = "Synchronous VisualPipeline.Run including ONNX Runtime CPU execution and PaddleDocumentTableStructureDecoder; excludes image decode/preprocessing, model/session creation and one-time OpenVINO compatibility reference.", percentile = "Nearest-rank" },
            parity = new { referenceBackend = "openvino-cpu with derived compatibility graph", tokenCount = expected.Tokens.Count, cellCount = expected.Regions.Count, sourceMarkupSha256 = actualMarkupSha, compatibilityMarkupSha256 = expectedMarkupSha, markupMatch = actualMarkupSha == expectedMarkupSha },
            result = new { tokenCount = actual.Tokens.Count, cellCount = actual.Regions.Count, averageScore = actual.AverageScore, markupSha256 = actualMarkupSha, tokenFingerprintSha256 = TokenFingerprint(actual) },
            timing = Percentiles(samples),
            samplesMs = samples,
            boundary = "Exact official wireless SLANeXt graph execution, table Decoder contract and derived OpenVINO compatibility-graph markup parity on one host; not table dataset accuracy, cell recall, cross-device ranking or support for the original graph in OpenVINO."
        });
    }

    private static void ValidateAndMatch(PaddleDocumentTableResult expected, PaddleDocumentTableResult actual)
    {
        ValidateResult(actual);
        Assert.AreEqual(expected.Tokens.Count, actual.Tokens.Count, "Wireless SLANeXt token count differs from OpenVINO compatibility reference.");
        Assert.AreEqual(expected.Regions.Count, actual.Regions.Count, "Wireless SLANeXt cell count differs from OpenVINO compatibility reference.");
        Assert.AreEqual(Sha256(Encoding.UTF8.GetBytes(expected.Markup)), Sha256(Encoding.UTF8.GetBytes(actual.Markup)), "Wireless SLANeXt HTML differs from OpenVINO compatibility reference.");
    }

    private static void ValidateResult(PaddleDocumentTableResult result)
    {
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Tokens.Count > 10, "Wireless SLANeXt returned too few structure tokens.");
        Assert.IsTrue(result.Regions.Count > 0, "Wireless SLANeXt returned no decoded cells.");
        Assert.IsTrue(result.Tokens.All(token => float.IsFinite(token.Score)));
        Assert.IsTrue(result.Regions.All(region => float.IsFinite(region.Score)));
    }

    private static string TokenFingerprint(PaddleDocumentTableResult result)
    {
        var builder = new StringBuilder();
        foreach (PaddleDocumentTableToken token in result.Tokens) builder.Append(token.Index).Append('|').Append(token.Value).Append('|').Append(token.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        return Sha256(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static object Percentiles(double[] samples)
    {
        double[] sorted = samples.OrderBy(value => value).ToArray();
        return new { p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1], p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], minMs = sorted[0], maxMs = sorted[^1], meanMs = sorted.Average() };
    }

    private static object Device() => new { machine = Environment.MachineName, os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessorCount = Environment.ProcessorCount, processorIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not-recorded" };

    private void WriteJson(string path, object value)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(path);
        Console.WriteLine("PADDLE_DOCUMENT_ORT_SLANEXT_WIRELESS_BENCHMARK=" + path);
    }

    private static void RequireEnabled() => Assert.IsTrue(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK") == "1", "Set DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK=1 to run the opt-in ONNX Runtime CPU 5+50 benchmark.");
    private static void RequireFile(string path, string label) => Assert.IsTrue(File.Exists(path), "Missing " + label + ": " + path);
    private static string Sha256(string path) => Sha256(File.ReadAllBytes(path));
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
