using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>
/// Records a fixed-input PaddleOCR-VL latency slice with a reproducible warm-up
/// and measurement protocol. / 使用固定输入、可复现预热和测量协议记录 PaddleOCR-VL 延迟切片。
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlFormalLatencyIntegrationTests
{
    private const string DefaultReference = @"eng\models\paddle-ocr\verification\paddleocr-vl-six-task-reference-linux-20261023.json";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task FixedTaskRecordsFiveWarmupsAndFiftyMeasuredGenerations(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN=1 to run the opt-in PaddleOCR-VL latency protocol.");

        string bundle = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string model = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        string fixtureRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES");
        string referencePath = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_REFERENCE") ?? DefaultReference);
        if (!File.Exists(referencePath)) Assert.Inconclusive("Missing PaddleOCR-VL reference: " + referencePath);

        int warmups = ReadCount("DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_WARMUPS", 5, 0, 100);
        int measurements = ReadCount("DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_MEASUREMENTS", 50, 1, 200);
        string taskName = (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK") ?? "table").Trim().ToLowerInvariant();
        (PaddleOcrVlTask task, string referenceId) = TaskFor(taskName);

        using JsonDocument reference = JsonDocument.Parse(File.ReadAllBytes(referencePath));
        JsonElement expected = reference.RootElement.GetProperty("cases").EnumerateArray()
            .Single(value => string.Equals(value.GetProperty("task").GetString(), taskName, StringComparison.Ordinal));
        Assert.AreEqual(referenceId, expected.GetProperty("id").GetString());
        using JsonDocument fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "fixtures.json")));
        JsonElement fixture = fixtures.RootElement.GetProperty("cases").EnumerateArray()
            .Single(value => string.Equals(value.GetProperty("id").GetString(), referenceId, StringComparison.Ordinal));
        string rgbName = fixture.GetProperty("rgbFile").GetString()!;
        Assert.AreEqual(rgbName, Path.GetFileName(rgbName));
        string rgbPath = Path.Combine(fixtureRoot, rgbName);
        Assert.AreEqual(fixture.GetProperty("rgbSha256").GetString(), Hash(rgbPath));
        var sourceSize = new VisualSize(fixture.GetProperty("sourceSize")[0].GetInt32(), fixture.GetProperty("sourceSize")[1].GetInt32());
        PaddleOcrVlPreparedInput prepared = new PaddleOcrVlImageProcessor().Create(File.ReadAllBytes(rgbPath), sourceSize, task,
            sourceSha256: expected.GetProperty("inputSha256").GetString());
        float[] patches = prepared.PixelPatches.ToArray();
        byte[] patchBytes = new byte[patches.Length * sizeof(float)];
        Buffer.BlockCopy(patches, 0, patchBytes, 0, patchBytes.Length);
        Assert.AreEqual(fixture.GetProperty("patchSha256").GetString(), Convert.ToHexString(SHA256.HashData(patchBytes)).ToLowerInvariant());

        using var registry = new BackendRegistry();
        BackendId backendId;
        string device;
        if (backend == "openvino")
        {
            registry.UseOpenVino(new OpenVinoOptions(performanceHint: OpenVinoPerformanceHint.Latency, streams: 1, inferenceThreads: 4, requestCount: 1));
            backendId = OpenVinoBackendProvider.BackendId;
            device = "CPU";
        }
        else
        {
            registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1,
                enableMemoryPattern: false, enableCpuMemoryArena: false));
            backendId = OnnxRuntimeBackendProvider.BackendId;
            device = "cpu";
        }

        using PaddleOcrVlOnnxSession session = PaddleOcrVlOnnxSession.Create(registry, bundle,
            new BackendRequest(BackendCapabilities.TensorInference, backendId, device));
        var tokenizer = new PaddleOcrVlTokenizer(model);
        var options = new PaddleOcrVlGenerationOptions(maximumNewTokens: 512, timeout: TimeSpan.FromMinutes(15));
        var warmupRows = new List<object>(warmups);
        for (int index = 0; index < warmups; index++)
            warmupRows.Add(await RunOneAsync(session, tokenizer, prepared, expected, index, options, measure: false).ConfigureAwait(false));

        var samples = new List<LatencySample>(measurements);
        for (int index = 0; index < measurements; index++)
        {
            var row = (LatencyRow)await RunOneAsync(session, tokenizer, prepared, expected, index, options, measure: true).ConfigureAwait(false);
            samples.Add(new LatencySample(row.WallClockMilliseconds, row.SessionMilliseconds, row.TokenCount));
        }

        Assert.AreEqual(measurements, samples.Count);
        double[] wall = samples.Select(value => value.WallClockMilliseconds).ToArray();
        double[] inner = samples.Select(value => value.SessionMilliseconds).ToArray();
        string outputDirectory = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_OUTPUT")
            ?? TestContext.TestResultsDirectory!);
        Directory.CreateDirectory(outputDirectory);
        string reportPath = Path.Combine(outputDirectory, "paddleocr-vl-formal-latency-" + backend + "-" + taskName + ".json");
        var report = new
        {
            schemaVersion = "deploysharp-paddleocr-vl-formal-latency-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            status = "passed",
            backend,
            device,
            task = taskName,
            model = new { id = "paddleocr-vl/1.6", checkpointRevision = PaddleOcrVlInputPlanning.OfficialV16Revision,
                referenceId, referenceSha256 = Hash(referencePath), bundleSha256 = new[] { "vision", "embedding", "decoder", "head" }
                    .ToDictionary(role => role, role => Hash(Path.Combine(bundle, role + ".onnx"))) },
            input = new { rgbFile = rgbName, rgbSha256 = Hash(rgbPath), patchSha256 = Convert.ToHexString(SHA256.HashData(patchBytes)).ToLowerInvariant(),
                sourceSize = new[] { sourceSize.Width, sourceSize.Height }, grid = new[] { prepared.ImagePlan.GridHeight, prepared.ImagePlan.GridWidth } },
            environment = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), processorCount = Environment.ProcessorCount,
                processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not-recorded", sourceCommit = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT") ?? "not-recorded" },
            protocol = new { warmupCount = warmups, measurementCount = measurements, sessions = 1, threads = 4,
                timing = "Wall clock surrounds GenerateAsync; Session elapsed is the production result.Elapsed and excludes caller preprocessing and session queue.",
                percentile = "Nearest-rank", inputReuse = "One immutable prepared RGB input reused for each generation; model/session creation is excluded." },
            warmups = warmupRows,
            samples,
            summary = new { wallClockP50Milliseconds = Percentile(wall, .50), wallClockP95Milliseconds = Percentile(wall, .95),
                sessionP50Milliseconds = Percentile(inner, .50), sessionP95Milliseconds = Percentile(inner, .95),
                tokenCount = samples.Select(value => value.TokenCount).Distinct().Single() },
            boundary = "Fixed PaddleOCR-VL-1.6 single-task correctness and repeated latency on one host. Every invocation reached EOS and matched reference tokens/text. This is not labelled quality, page-level quality, backend ranking, clock-locked performance, GPU/TensorRT/OpenCV support, memory-soak or cross-device evidence."
        };
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        TestContext.AddResultFile(reportPath);
        TestContext.WriteLine("PADDLEOCR_VL_FORMAL_LATENCY backend=" + backend + " task=" + taskName +
            " wallP50Ms=" + Percentile(wall, .50).ToString("F3") + " wallP95Ms=" + Percentile(wall, .95).ToString("F3") + " report=" + reportPath);
    }

    private static async Task<object> RunOneAsync(PaddleOcrVlOnnxSession session, PaddleOcrVlTokenizer tokenizer,
        PaddleOcrVlPreparedInput prepared, JsonElement expected, int index, PaddleOcrVlGenerationOptions options, bool measure)
    {
        Stopwatch? watch = measure ? Stopwatch.StartNew() : null;
        PaddleOcrVlGenerationResult actual = await session.GenerateAsync(prepared, tokenizer, options).ConfigureAwait(false);
        watch?.Stop();
        int[] expectedTokens = expected.GetProperty("generatedTokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        Assert.AreEqual(GenerationFinishReason.EndOfSequence, actual.FinishReason, "Generation did not reach EOS at iteration " + index);
        CollectionAssert.AreEqual(expectedTokens, actual.TokenIds.ToArray(), "Token output changed at iteration " + index);
        Assert.AreEqual(expected.GetProperty("decoded").GetString(), actual.Text, "Text output changed at iteration " + index);
        if (!measure) return new { index, tokenCount = actual.TokenIds.Count, sessionMilliseconds = actual.Elapsed.TotalMilliseconds };
        return new LatencyRow(watch!.Elapsed.TotalMilliseconds, actual.Elapsed.TotalMilliseconds, actual.TokenIds.Count);
    }

    private static (PaddleOcrVlTask Task, string ReferenceId) TaskFor(string name) => name switch
    {
        "ocr" => (PaddleOcrVlTask.TextRecognition, "ocr-demo-1"),
        "table" => (PaddleOcrVlTask.TableRecognition, "table-official-example"),
        "formula" => (PaddleOcrVlTask.FormulaRecognition, "formula-official-example"),
        "chart" => (PaddleOcrVlTask.ChartRecognition, "chart-official-example"),
        "spotting" => (PaddleOcrVlTask.TextSpotting, "spotting-demo-1"),
        "seal" => (PaddleOcrVlTask.SealRecognition, "seal-official-example"),
        _ => throw new ArgumentException("Choose ocr, table, formula, chart, spotting or seal.", nameof(name))
    };

    private static int ReadCount(string name, int fallback, int minimum, int maximum)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (!int.TryParse(value, out int parsed) || parsed < minimum || parsed > maximum) throw new ArgumentException(name + " must be between " + minimum + " and " + maximum + ".");
        return parsed;
    }

    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Set " + name + " for the external PaddleOCR-VL latency test.");
        return value!;
    }

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        int index = Math.Max(0, Math.Min(sorted.Length - 1, (int)Math.Ceiling(percentile * sorted.Length) - 1));
        return sorted[index];
    }

    private sealed record LatencySample(double WallClockMilliseconds, double SessionMilliseconds, int TokenCount);
    private sealed record LatencyRow(double WallClockMilliseconds, double SessionMilliseconds, int TokenCount);
}
