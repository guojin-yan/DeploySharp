using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class PaddleChart2TableOrtExternalIntegrationTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public async System.Threading.Tasks.Task OfficialChartImageRunsThroughFourGraphOrtGeneration()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_ORT_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_ORT_RUN_EXTERNAL=1 to execute the local four-graph PP-Chart2Table ONNX bundle on ORT CPU.");
            string sourceRoot = Required("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
            string exportRoot = Required("DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT");
            string image = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_IMAGE") ?? @"E:\Model\PaddleDocument\validation\chart_parsing_02.png";
            string textRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TEXT_ROOT") ?? Path.Combine(exportRoot, "text-onnx-verify-20260923");
            var bundle = new PaddleChart2TableOnnxBundle(
                Path.Combine(exportRoot, "chart-vision.onnx"),
                GraphPath(textRoot, "chart-token-embedding-dynamic"),
                GraphPath(textRoot, "chart-text-prefill-full"),
                GraphPath(textRoot, "chart-text-decoder-dynamic-past-full"),
                OnnxRuntimeBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.Register(new OnnxRuntimeBackendProvider());
            var tokenizer = new PaddleChart2TableTokenizer(sourceRoot);
            using PreparedVisualInput input = new OpenCvPaddleChart2TableInputFactory().CreateFromFile(image);
            using var session = new PaddleChart2TableOnnxSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "CPU"));
            int maximumNewTokens = ParseMaximumNewTokens();

            var totalWatch = Stopwatch.StartNew();
            PaddleChart2TableGenerationResult result = await session.GenerateAsync(input, tokenizer, maximumNewTokens);
            totalWatch.Stop();

            CollectionAssert.AreEqual(new[] { 7948, 69442, 760 }, result.TokenIds.Take(3).ToArray());
            Assert.AreEqual(tokenizer.IsTerminalToken(result.TokenIds.Last()) ? GenerationFinishReason.EndOfSequence : GenerationFinishReason.MaxTokens, result.FinishReason);
            Assert.AreEqual(286, result.PromptTokenCount);
            TestContext.WriteLine(JsonSerializer.Serialize(new { result.Text, tokenIds = result.TokenIds, finishReason = result.FinishReason.ToString(), requestedNewTokens = maximumNewTokens, generatedTokens = result.TokenIds.Count, totalMs = totalWatch.Elapsed.TotalMilliseconds, visionMs = result.VisionTime.TotalMilliseconds, embeddingMs = result.EmbeddingTime.TotalMilliseconds, prefillMs = result.PrefillTime.TotalMilliseconds, decodeMs = result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray() }));
        }

        private static int ParseMaximumNewTokens()
        {
            string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_ORT_MAX_NEW_TOKENS");
            if (string.IsNullOrWhiteSpace(value)) return 3;
            if (!int.TryParse(value, out int parsed) || parsed < 3 || parsed > 2048) Assert.Fail("DEPLOYSHARP_CHART2TABLE_ORT_MAX_NEW_TOKENS must be an integer between 3 and 2048.");
            return parsed;
        }

        private static string Required(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Required external integration variable is missing: " + name);
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
    }
}
