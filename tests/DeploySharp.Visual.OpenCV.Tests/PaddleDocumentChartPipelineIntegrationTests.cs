using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Results.Language;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Runs the four-graph Chart2Table bundle as a dependent document task. / 将四图 Chart2Table Bundle 作为文档任务阶段运行。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentChartPipelineIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task ChartParsingStageProducesStructuredDataOnOrtCpu()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_PIPELINE_RUN_EXTERNAL") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_PIPELINE_RUN_EXTERNAL=1 to run the Chart2Table document stage.");
        string sourceRoot = Required("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
        string exportRoot = Required("DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT");
        string textRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TEXT_ROOT") ?? Path.Combine(exportRoot, "text-onnx-verify-20260923");
        string image = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_IMAGE") ?? @"E:\Model\PaddleDocument\validation\chart_parsing_02.png";
        if (!File.Exists(image)) Assert.Inconclusive("Missing Chart2Table image: " + image);
        var bundle = new PaddleChart2TableOnnxBundle(
            Path.Combine(exportRoot, "chart-vision.onnx"),
            GraphPath(textRoot, "chart-token-embedding-dynamic"),
            GraphPath(textRoot, "chart-text-prefill-full"),
            GraphPath(textRoot, "chart-text-decoder-dynamic-past-full"),
            OnnxRuntimeBackendProvider.BackendId);
        using var registry = new BackendRegistry();
        registry.Register(new OnnxRuntimeBackendProvider());
        var tokenizer = new PaddleChart2TableTokenizer(sourceRoot);
        using var session = new PaddleChart2TableOnnxSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "CPU"));
        using PreparedVisualInput input = new OpenCvPaddleChart2TableInputFactory().CreateFromFile(image);
        var descriptor = PaddleDocumentModelCatalog.Get("paddle-chart/pp-chart2table");
        var stage = new PaddleDocumentDependentStage(PaddleDocumentModule.ChartParsing, Array.Empty<PaddleDocumentModule>(), async (context, token) =>
        {
            PaddleChart2TableGenerationResult generation = await session.GenerateAsync(input, tokenizer, ParseMaximumNewTokens(), cancellationToken: token).ConfigureAwait(false);
            return new PaddleDocumentChartResult(
                new PaddleDocumentResultMetadata(descriptor, OnnxRuntimeBackendProvider.BackendId.Value, generation.VisionTime + generation.EmbeddingTime + generation.PrefillTime + generation.DecodeSteps.Aggregate(TimeSpan.Zero, (sum, value) => sum + value), Sha256(image), context.Page.PageIndex),
                generation.Text,
                tokenIds: generation.TokenIds,
                finishReason: generation.FinishReason.ToString());
        });
        PaddleDocumentPipelineResult page = await new PaddleDocumentPipeline(new[] { stage }).RunAsync(new PaddleDocumentPage(image, new VisualSize(1024, 1024), 0), CancellationToken.None).ConfigureAwait(false);
        PaddleDocumentChartResult result = page.GetRequired<PaddleDocumentChartResult>(PaddleDocumentModule.ChartParsing);
        Assert.AreEqual(GenerationFinishReason.EndOfSequence.ToString(), result.FinishReason);
        Assert.IsTrue(result.TokenIds.Count >= 100);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StructuredData));
        Assert.IsTrue(result.StructuredData.Contains("2018", StringComparison.Ordinal));
        string reportDirectory = Path.Combine(TestContext.TestResultsDirectory!, "paddle-document");
        Directory.CreateDirectory(reportDirectory);
        string report = Path.Combine(reportDirectory, "chart2table-document-stage.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            model = descriptor.ModelId,
            backend = OnnxRuntimeBackendProvider.BackendId.Value,
            image,
            imageSha256 = Sha256(image),
            pageIndex = page.Page.PageIndex,
            finish = result.FinishReason,
            tokenCount = result.TokenIds.Count,
            totalMs = page.Elapsed.TotalMilliseconds,
            stageMs = page.Timings.Select(item => new { module = item.Module.ToString(), elapsedMs = item.Elapsed.TotalMilliseconds }).ToArray(),
            result.StructuredData,
            tokenIds = result.TokenIds
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
    }

    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Required external integration variable is missing: " + name);
        return value!;
    }

    private static int ParseMaximumNewTokens()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_PIPELINE_MAX_NEW_TOKENS");
        if (string.IsNullOrWhiteSpace(value)) return 256;
        if (!int.TryParse(value, out int parsed) || parsed < 100 || parsed > 2048) Assert.Fail("DEPLOYSHARP_CHART2TABLE_PIPELINE_MAX_NEW_TOKENS must be from 100 to 2048.");
        return parsed;
    }

    private static string GraphPath(string root, string stem)
    {
        string standard = Path.Combine(root, stem + ".onnx");
        if (File.Exists(standard)) return standard;
        string epsilon = Path.Combine(root, stem + "-epsilon.onnx");
        return File.Exists(epsilon) ? epsilon : standard;
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
