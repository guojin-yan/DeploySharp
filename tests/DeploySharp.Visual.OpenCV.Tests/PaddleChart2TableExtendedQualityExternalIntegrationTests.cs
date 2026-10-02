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
    public TestContext TestContext { get; set; } = null!;

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

        string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_EXTENDED_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, "chart2table-" + backend + "-extended-quality.json");
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
