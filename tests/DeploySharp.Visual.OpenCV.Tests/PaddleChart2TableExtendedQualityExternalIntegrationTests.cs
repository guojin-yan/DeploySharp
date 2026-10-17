using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Runs a bounded official ChartQA image/table selection and records task quality without claiming split accuracy. / 对官方 ChartQA 有界图像/表格选择运行任务质量记录，不宣称数据集准确率。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleChart2TableExtendedQualityExternalIntegrationTests
{
    private const string BenchmarkSampleFile = "OECD_GROSS_PENSION_REPLACEMENT_RATES_HUN_MEX_000001.png";
    private const string BenchmarkInputSha256 = "c2e49c1f2593d65e0c5cce5b573f122f076a0bf0f429121d96e9463c6404a530";
    private const string BenchmarkOutputSha256 = "ebd99e6bb36ee2bc510932a58c049d0d1c2d765c473ec98d6c3a48529fb2d484";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    public async Task OfficialChartQaFixedSampleRunsSteadyStateBenchmark(string backend)
    {
        string backendKey = string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase) ? "OPENVINO" : "ORT";
        string gate = "DEPLOYSHARP_CHART2TABLE_BENCHMARK_" + backendKey + "_RUN_EXTERNAL";
        if (!string.Equals(Environment.GetEnvironmentVariable(gate), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set " + gate + "=1 to run the fixed-sample Chart2Table 5+50 benchmark.");

        string modelRoot = Required("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
        string exportRoot = Required("DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT");
        string textRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TEXT_ROOT") ?? Path.Combine(exportRoot, "text-onnx-verify-20260923");
        string sampleRoot = Required("DEPLOYSHARP_CHARTQA_EXTENDED_ROOT");
        string manifestPath = Path.Combine(sampleRoot, "manifest.json");
        Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Invalid ChartQA manifest.");
        Assert.AreEqual("044eabfc306abfe9340c5741f0093aefc5973d06", manifest.SourceRevision, "Benchmark source revision changed.");
        Sample sample = manifest.Samples.SingleOrDefault(value => string.Equals(value.File, BenchmarkSampleFile, StringComparison.Ordinal))
            ?? throw new InvalidDataException("Fixed benchmark sample is missing from the ChartQA manifest.");

        string imagePath = Path.Combine(sampleRoot, "images", sample.File);
        Assert.IsTrue(File.Exists(imagePath), "Missing ChartQA benchmark image: " + imagePath);
        string inputSha256 = Sha256File(imagePath);
        Assert.AreEqual(BenchmarkInputSha256, inputSha256, "The fixed benchmark input changed.");
        string expectedTextSha256 = Sha256Text(sample.ExpectedText ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(sample.ImageSha256)) Assert.AreEqual(sample.ImageSha256, inputSha256, "Manifest input SHA mismatch.");

        BackendId backendId;
        using var registry = new BackendRegistry();
        if (backendKey == "OPENVINO")
        {
            registry.Register(new OpenVinoBackendProvider(new OpenVinoOptions(device: "CPU")));
            backendId = OpenVinoBackendProvider.BackendId;
        }
        else
        {
            registry.Register(new OnnxRuntimeBackendProvider());
            backendId = OnnxRuntimeBackendProvider.BackendId;
        }

        string visionPath = Path.Combine(exportRoot, "chart-vision.onnx");
        string embeddingPath = GraphPath(textRoot, "chart-token-embedding-dynamic");
        string prefillPath = GraphPath(textRoot, "chart-text-prefill-full");
        string decodePath = GraphPath(textRoot, "chart-text-decoder-dynamic-past-full");
        var bundle = new PaddleChart2TableOnnxBundle(visionPath, embeddingPath, prefillPath, decodePath, backendId);
        var tokenizer = new PaddleChart2TableTokenizer(modelRoot);
        using var session = new PaddleChart2TableOnnxSession(registry, bundle,
            new BackendRequest(BackendCapabilities.TensorInference, backendId, "CPU"));
        using PreparedVisualInput input = new OpenCvPaddleChart2TableInputFactory().CreateFromFile(imagePath);

        const int warmupCount = 5;
        const int measurementCount = 50;
        const int maximumNewTokens = 1024;
        var samples = new List<object>(measurementCount);
        var totalMs = new List<double>(measurementCount);
        var visionMs = new List<double>(measurementCount);
        var embeddingMs = new List<double>(measurementCount);
        var prefillMs = new List<double>(measurementCount);
        var decodeP50Ms = new List<double>(measurementCount);
        var decodeP95Ms = new List<double>(measurementCount);

        for (int iteration = 0; iteration < warmupCount + measurementCount; iteration++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            PaddleChart2TableGenerationResult result = await session.GenerateAsync(input, tokenizer, maximumNewTokens).ConfigureAwait(false);
            watch.Stop();
            string outputSha256 = Sha256Text(result.Text);
            Assert.AreEqual(GenerationFinishReason.EndOfSequence, result.FinishReason, "Iteration " + iteration + " did not finish with EOS.");
            Assert.AreEqual(21, result.TokenIds.Count, "Iteration " + iteration + " changed the generated token count.");
            Assert.AreEqual(BenchmarkOutputSha256, outputSha256, "Iteration " + iteration + " changed the fixed-sample output.");

            if (iteration < warmupCount) continue;
            double[] perTokenMs = result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray();
            double total = watch.Elapsed.TotalMilliseconds;
            totalMs.Add(total);
            visionMs.Add(result.VisionTime.TotalMilliseconds);
            embeddingMs.Add(result.EmbeddingTime.TotalMilliseconds);
            prefillMs.Add(result.PrefillTime.TotalMilliseconds);
            decodeP50Ms.Add(Percentile(perTokenMs, 0.50));
            decodeP95Ms.Add(Percentile(perTokenMs, 0.95));
            samples.Add(new
            {
                iteration = iteration - warmupCount,
                totalMs = total,
                visionMs = result.VisionTime.TotalMilliseconds,
                embeddingMs = result.EmbeddingTime.TotalMilliseconds,
                prefillMs = result.PrefillTime.TotalMilliseconds,
                decodeP50Ms = Percentile(perTokenMs, 0.50),
                decodeP95Ms = Percentile(perTokenMs, 0.95),
                decodeStepCount = perTokenMs.Length,
                tokenCountIncludingEos = result.TokenIds.Count,
                finishReason = result.FinishReason.ToString(),
                generatedTextSha256 = outputSha256
            });
        }

        string reportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_BENCHMARK_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, "chart2table-benchmark-" + backendKey.ToLowerInvariant() + ".json");
        string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
        var report = new
        {
            schemaVersion = "deploysharp-chart2table-fixed-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = manifest.SourceRevision,
            backend = backendKey == "OPENVINO" ? "openvino-cpu" : "onnxruntime-cpu",
            device = new
            {
                machine = Environment.MachineName,
                operatingSystem = Environment.OSVersion.ToString(),
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                processorCount = Environment.ProcessorCount
            },
            protocol = new { warmupCount, measurementCount, maximumNewTokens, sequentialSingleSession = true },
            dataset = new { repository = manifest.SourceRepository, split = manifest.Split, revision = manifest.SourceRevision },
            input = new { sample = sample.File, sha256 = inputSha256, expectedTextSha256 },
            models = new
            {
                vision = new { path = visionPath, sha256 = Sha256File(visionPath) },
                embedding = new { path = embeddingPath, sha256 = Sha256File(embeddingPath) },
                prefill = new { path = prefillPath, sha256 = Sha256File(prefillPath) },
                decode = new { path = decodePath, sha256 = Sha256File(decodePath) }
            },
            resultContract = new { outputSha256 = BenchmarkOutputSha256, tokenCountIncludingEos = 21, finishReason = "EndOfSequence", invariantAcrossAllRuns = true },
            summary = new
            {
                totalMs = TimingSummary(totalMs),
                visionMs = TimingSummary(visionMs),
                embeddingMs = TimingSummary(embeddingMs),
                prefillMs = TimingSummary(prefillMs),
                decodeP50Ms = TimingSummary(decodeP50Ms),
                decodeP95Ms = TimingSummary(decodeP95Ms)
            },
            samples,
            boundary = "One fixed ChartQA validation image on one host, one session and one channel. Not split-level accuracy, a cross-device ranking, or a guarantee for other charts. CPU/GPU frequency is not controlled by this test."
        };
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(reportPath);
        TestContext.WriteLine("CHART2TABLE_BENCHMARK backend=" + backend + ";totalP50Ms=" + Percentile(totalMs.ToArray(), .50) + ";totalP95Ms=" + Percentile(totalMs.ToArray(), .95) + ";report=" + reportPath);
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    public async Task OfficialChartQaSelectionRecordsTableQuality(string backend)
    {
        string gate = string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)
            ? "DEPLOYSHARP_CHART2TABLE_EXTENDED_OPENVINO_RUN_EXTERNAL"
            : "DEPLOYSHARP_CHART2TABLE_EXTENDED_ORT_RUN_EXTERNAL";
        if (!string.Equals(Environment.GetEnvironmentVariable(gate), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set " + gate + "=1 to run the bounded ChartQA quality selection.");

        string modelRoot = Required("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
        string exportRoot = Required("DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT");
        string root = Required("DEPLOYSHARP_CHARTQA_EXTENDED_ROOT");
        string manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath)) Assert.Inconclusive("Missing ChartQA extended manifest: " + manifestPath);
        Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Invalid ChartQA manifest.");
        if (manifest.Samples == null || manifest.Samples.Count == 0) Assert.Fail("ChartQA manifest contains no samples.");

        BackendId backendId;
        using var registry = new BackendRegistry();
        if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase))
        {
            registry.Register(new OpenVinoBackendProvider(new OpenVinoOptions(device: "CPU")));
            backendId = OpenVinoBackendProvider.BackendId;
        }
        else
        {
            registry.Register(new OnnxRuntimeBackendProvider());
            backendId = OnnxRuntimeBackendProvider.BackendId;
        }

        string textRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TEXT_ROOT") ?? Path.Combine(exportRoot, "text-onnx-verify-20260923");
        var bundle = new PaddleChart2TableOnnxBundle(
            Path.Combine(exportRoot, "chart-vision.onnx"),
            GraphPath(textRoot, "chart-token-embedding-dynamic"),
            GraphPath(textRoot, "chart-text-prefill-full"),
            GraphPath(textRoot, "chart-text-decoder-dynamic-past-full"),
            backendId);
        var tokenizer = new PaddleChart2TableTokenizer(modelRoot);
        using var session = new PaddleChart2TableOnnxSession(registry, bundle,
            new BackendRequest(BackendCapabilities.TensorInference, backendId, "CPU"));
        int maximumNewTokens = ParseMaximumNewTokens(backend);
        var inputFactory = new OpenCvPaddleChart2TableInputFactory();
        var rows = new List<object>();
        foreach (Sample sample in manifest.Samples)
        {
            string imagePath = Path.Combine(root, "images", sample.File);
            if (!File.Exists(imagePath)) Assert.Fail("Missing ChartQA image: " + imagePath);
            string tableName = string.IsNullOrWhiteSpace(sample.TableFile)
                ? Path.GetFileNameWithoutExtension(sample.File) + ".csv"
                : sample.TableFile;
            string tablePath = Path.Combine(root, "tables", tableName);
            if (!File.Exists(tablePath)) Assert.Fail("Missing ChartQA table: " + tablePath);
            string imageSha = Sha256File(imagePath);
            if (!string.IsNullOrWhiteSpace(sample.ImageSha256) && !string.Equals(imageSha, sample.ImageSha256, StringComparison.OrdinalIgnoreCase))
                Assert.Fail("ChartQA image SHA mismatch: " + sample.File);
            string tableSha = Sha256File(tablePath);
            if (!string.IsNullOrWhiteSpace(sample.TableSha256) && !string.Equals(tableSha, sample.TableSha256, StringComparison.OrdinalIgnoreCase))
                Assert.Fail("ChartQA table SHA mismatch: " + tableName);
            using PreparedVisualInput input = inputFactory.CreateFromFile(imagePath);
            Stopwatch watch = Stopwatch.StartNew();
            PaddleChart2TableGenerationResult result = await session.GenerateAsync(input, tokenizer, maximumNewTokens).ConfigureAwait(false);
            watch.Stop();
            PaddleChart2TableQualityComparison quality = PaddleChart2TableQualityEvaluator.Compare(sample.ExpectedText ?? string.Empty, result.Text);
            rows.Add(new
            {
                sample = sample.File,
                imageSha256 = imageSha,
                sourceTableSha256 = tableSha,
                expectedTableSha256 = Sha256Text(sample.ExpectedText ?? string.Empty),
                generatedTableSha256 = Sha256Text(result.Text),
                finishReason = result.FinishReason.ToString(),
                tokenCountIncludingEos = result.TokenIds.Count,
                totalMs = watch.Elapsed.TotalMilliseconds,
                visionMs = result.VisionTime.TotalMilliseconds,
                embeddingMs = result.EmbeddingTime.TotalMilliseconds,
                prefillMs = result.PrefillTime.TotalMilliseconds,
                decodeStepCount = result.DecodeSteps.Count,
                decodeP50Ms = Percentile(result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray(), 0.50),
                decodeP95Ms = Percentile(result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray(), 0.95),
                structure = new
                {
                    expectedRows = quality.Expected.RowCount,
                    actualRows = quality.Actual.RowCount,
                    expectedColumns = quality.Expected.ColumnCount,
                    actualColumns = quality.Actual.ColumnCount,
                    expectedCells = quality.Expected.CellCount,
                    actualCells = quality.Actual.CellCount,
                    rowExactMatchCount = quality.RowExactMatchCount,
                    cellExactMatchCount = quality.CellExactMatchCount,
                    rowAccuracy = quality.RowAccuracy,
                    cellAccuracy = quality.CellAccuracy,
                    exactTextMatch = quality.ExactTextMatch,
                    structureMatches = quality.StructureMatches,
                    actualStructurallyValid = quality.Actual.IsStructurallyValid
                }
            });
        }

        string report = ResolveReportPath(backend, TestContext.TestResultsDirectory!);
        string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            backend = string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase) ? "openvino-cpu" : "onnxruntime-cpu",
            modelId = "paddle-chart/pp-chart2table",
            sourceRepository = manifest.SourceRepository,
            sourceRevision = manifest.SourceRevision,
            split = manifest.Split,
            maximumNewTokens,
            sampleCount = rows.Count,
            results = rows,
            boundary = "Bounded official ChartQA image/table selection; structure/row/cell metrics are task-quality evidence, not split-level ChartQA accuracy or a controlled performance benchmark."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Console.WriteLine("PADDLE_CHART2TABLE_EXTENDED backend=" + backend + ";samples=" + rows.Count + ";report=" + report);
    }

    private static string ResolveReportPath(string backend, string testResultsDirectory)
    {
        string? configured = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_EXTENDED_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
            return Path.Combine(testResultsDirectory, "chart2table-" + backend + "-extended-quality.json");

        string backendName = string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)
            ? "openvino"
            : "onnxruntime";
        if (configured.Contains("{backend}", StringComparison.OrdinalIgnoreCase))
            return configured.Replace("{backend}", backendName, StringComparison.OrdinalIgnoreCase);

        bool bothBackendsEnabled = string.Equals(
                Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_EXTENDED_ORT_RUN_EXTERNAL"), "1", StringComparison.Ordinal)
            && string.Equals(
                Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_EXTENDED_OPENVINO_RUN_EXTERNAL"), "1", StringComparison.Ordinal);
        if (!bothBackendsEnabled)
            return configured;

        string extension = Path.GetExtension(configured);
        string stem = extension.Length == 0 ? configured : configured[..^extension.Length];
        string suffix = "-" + backendName;
        return stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? configured
            : stem + suffix + extension;
    }

    private static int ParseMaximumNewTokens(string backend)
    {
        string variable = string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)
            ? "DEPLOYSHARP_CHART2TABLE_EXTENDED_OPENVINO_MAX_NEW_TOKENS"
            : "DEPLOYSHARP_CHART2TABLE_EXTENDED_ORT_MAX_NEW_TOKENS";
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) return 1024;
        if (!int.TryParse(value, out int parsed) || parsed < 3 || parsed > 4096) Assert.Fail(variable + " must be 3..4096.");
        return parsed;
    }

    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Missing external integration variable: " + name);
        return value!;
    }

    private static string GraphPath(string root, string stem)
    {
        string standard = Path.Combine(root, stem + ".onnx");
        return File.Exists(standard) ? standard : Path.Combine(root, stem + "-epsilon.onnx");
    }

    private static double Percentile(double[] values, double p)
    {
        if (values.Length == 0) return 0;
        Array.Sort(values);
        double position = (values.Length - 1) * p;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        return lower == upper ? values[lower] : values[lower] + (values[upper] - values[lower]) * (position - lower);
    }

    private static object TimingSummary(List<double> values) => new
    {
        minMs = values.Min(),
        maxMs = values.Max(),
        p50Ms = Percentile(values.ToArray(), .50),
        p95Ms = Percentile(values.ToArray(), .95),
        samplesMs = values
    };

    private static string Sha256Text(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Sha256File(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private sealed class Manifest
    {
        public string? Dataset { get; set; }
        public string? Split { get; set; }
        public string? SourceRepository { get; set; }
        public string? SourceRevision { get; set; }
        public List<Sample> Samples { get; set; } = new();
    }

    private sealed class Sample
    {
        public string File { get; set; } = string.Empty;
        public string? TableFile { get; set; }
        public string? ExpectedText { get; set; }
        public string? ImageSha256 { get; set; }
        public string? TableSha256 { get; set; }
    }
}
