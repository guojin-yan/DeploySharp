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
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Runs all four curated ChartQA human samples through ORT/OpenVINO full EOS generation. / 使用 ORT/OpenVINO 对四张精选 ChartQA human 图执行完整 EOS 生成。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleChart2TableMultiImageOrtExternalIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    public async Task FourChartQaHumanSamplesGenerateExactTables(string backend)
    {
        string gate = string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)
            ? "DEPLOYSHARP_CHART2TABLE_OPENVINO_MULTI_RUN_EXTERNAL"
            : "DEPLOYSHARP_CHART2TABLE_ORT_MULTI_RUN_EXTERNAL";
        if (!string.Equals(Environment.GetEnvironmentVariable(gate), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set " + gate + "=1 to run the four-image " + backend + " Chart2Table regression.");
        string modelRoot = Required("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
        string exportRoot = Required("DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT");
        string sampleRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHARTQA_SAMPLE_ROOT") ?? @"E:\Model\PaddleDocument\validation\chartqa-20260924";
        string textRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TEXT_ROOT") ?? Path.Combine(exportRoot, "text-onnx-verify-20260923");
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
        var bundle = new PaddleChart2TableOnnxBundle(
            Path.Combine(exportRoot, "chart-vision.onnx"),
            GraphPath(textRoot, "chart-token-embedding-dynamic"),
            GraphPath(textRoot, "chart-text-prefill-full"),
            GraphPath(textRoot, "chart-text-decoder-dynamic-past-full"),
            backendId);
        var tokenizer = new PaddleChart2TableTokenizer(modelRoot);
        using var session = new PaddleChart2TableOnnxSession(registry, bundle,
            new BackendRequest(BackendCapabilities.TensorInference, backendId, "CPU"));
        var inputFactory = new OpenCvPaddleChart2TableInputFactory();
        int maximumNewTokens = ParseMaximumNewTokens(backend);
        var samples = new[]
        {
            new Sample("png_41699051005347.png", "Country | Long-term price index in food commodities, 1850-2015, World, 1934\nLamb | 103.7\nCorn | 103.13\nBarley | 102.46\nRye | 87.37\nBeef | 85.27\nWheat | 83.73\nCoffee | 82.2\nTea | 68.48\nPeanuts | 64.71\nPalm oil | 57.6\nPork | 55.36\nRice | 42.48\nSugar | 25.56\nCocoa | 18.81"),
            new Sample("png_41810321001157.png", "Characteristic | Value\nMauritania | 0.48%\nFiji | 0.38%\nMadagascar | 0.21%"),
            new Sample("png_8127.png", "Entity | Limit its military role | Play a more active military role\n2015 | 68 | 23\n2016 | 62 | 29"),
            new Sample("png_166.png", "Entity | Values\nCaring about ordinary people | 23.0\nWell-qualified president to be | 26.0\nCharismatic | 39.0\na strong leader | 55.0\nDangerous | 62.0\nIntolerant | 65.0\nArrogant | 75.0")
        };
        var rows = new List<object>();
        foreach (Sample sample in samples)
        {
            string imagePath = Path.Combine(sampleRoot, sample.File);
            if (!File.Exists(imagePath)) Assert.Inconclusive("Missing ChartQA image: " + imagePath);
            using PreparedVisualInput input = inputFactory.CreateFromFile(imagePath);
            Stopwatch watch = Stopwatch.StartNew();
            PaddleChart2TableGenerationResult result = await session.GenerateAsync(input, tokenizer, maximumNewTokens).ConfigureAwait(false);
            watch.Stop();
            Assert.AreEqual(GenerationFinishReason.EndOfSequence, result.FinishReason, sample.File + " must finish by EOS.");
            Assert.AreEqual(sample.ExpectedText, result.Text, sample.File + " table differs from the checked expected table.");
            rows.Add(new
            {
                sample = sample.File,
                imageSha256 = FileSha256(imagePath),
                expectedTextSha256 = Sha256(sample.ExpectedText),
                generatedTextSha256 = Sha256(result.Text),
                tokenCountIncludingEos = result.TokenIds.Count,
                finishReason = result.FinishReason.ToString(),
                totalMs = watch.Elapsed.TotalMilliseconds,
                visionMs = result.VisionTime.TotalMilliseconds,
                embeddingMs = result.EmbeddingTime.TotalMilliseconds,
                prefillMs = result.PrefillTime.TotalMilliseconds,
                decodeStepCount = result.DecodeSteps.Count,
                decodeP50Ms = Percentile(result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray(), .50),
                decodeP95Ms = Percentile(result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray(), .95)
            });
        }
        string report = Path.Combine(TestContext.TestResultsDirectory!, "chart2table-" + backend + "-multi-image-evidence.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            backend = string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase) ? "openvino-cpu" : "onnxruntime-cpu",
            modelId = "paddle-chart/pp-chart2table",
            maximumNewTokens,
            graphRoot = textRoot,
            results = rows,
            boundary = "Four curated ChartQA human samples; exact table reproduction is a multi-image qualitative regression, not a dataset accuracy score."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Console.WriteLine("PADDLE_CHART2TABLE_MULTI backend=" + backend + ";samples=" + rows.Count);
    }

    private static int ParseMaximumNewTokens(string backend)
    {
        string variable = string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)
            ? "DEPLOYSHARP_CHART2TABLE_OPENVINO_MULTI_MAX_NEW_TOKENS"
            : "DEPLOYSHARP_CHART2TABLE_ORT_MULTI_MAX_NEW_TOKENS";
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) return 256;
        if (!int.TryParse(value, out int parsed) || parsed < 3 || parsed > 2048) Assert.Fail(variable + " must be 3..2048.");
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
        if (File.Exists(standard)) return standard;
        string epsilon = Path.Combine(root, stem + "-epsilon.onnx");
        if (File.Exists(epsilon)) return epsilon;
        return standard;
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

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string FileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private sealed record Sample(string File, string ExpectedText);
}
