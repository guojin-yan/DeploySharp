using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class PaddleChart2TableOpenVinoExternalIntegrationTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void OfficialChartImageRunsThroughFourGraphOpenVinoBundle()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_OPENVINO_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_OPENVINO_RUN_EXTERNAL=1 to compile and run the local four-graph PP-Chart2Table bundle on OpenVINO CPU.");
            string sourceRoot = Required("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
            string exportRoot = Required("DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT");
            string image = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_IMAGE") ?? @"E:\Model\PaddleDocument\validation\chart_parsing_02.png";
            string textRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TEXT_ROOT") ?? Path.Combine(exportRoot, "text-onnx-verify-20260923");
            var bundle = new PaddleChart2TableOnnxBundle(
                Path.Combine(exportRoot, "chart-vision.onnx"),
                GraphPath(textRoot, "chart-token-embedding-dynamic"),
                GraphPath(textRoot, "chart-text-prefill-full"),
                GraphPath(textRoot, "chart-text-decoder-dynamic-past-full"),
                OpenVinoBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.Register(new OpenVinoBackendProvider(new OpenVinoOptions(device: "CPU")));
            var tokenizer = new PaddleChart2TableTokenizer(sourceRoot);
            var factory = new OpenCvPaddleChart2TableInputFactory();
            using PreparedVisualInput input = factory.CreateFromFile(image);
            string pixelReference = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_PIXEL_REFERENCE") ?? @"E:\Model\PaddleDocument\chart-export\chart2table-tensorrt-verify-20260923\image-pixel-values.bin";
            if (File.Exists(pixelReference))
            {
                byte[] referenceBytes = File.ReadAllBytes(pixelReference);
                float[] reference = new float[referenceBytes.Length / sizeof(float)];
                Buffer.BlockCopy(referenceBytes, 0, reference, 0, referenceBytes.Length);
                float[] actual = (float[])input.Tensor.Buffer;
                if (reference.Length != actual.Length) Assert.Fail("PaddleX/OpenCV processor tensor element counts differ.");
                double absoluteSum = 0;
                float maximumError = 0;
                for (int index = 0; index < reference.Length; index++)
                {
                    float error = Math.Abs(reference[index] - actual[index]);
                    absoluteSum += error;
                    if (error > maximumError) maximumError = error;
                }
                double meanError = absoluteSum / reference.Length;
                TestContext.WriteLine("PREPROCESS_PARITY maxAbs=" + maximumError.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";meanAbs=" + meanError.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Assert.IsTrue(maximumError <= .06f && meanError <= .0002, "PaddleX/OpenCV preprocessing exceeds bicubic normalization parity tolerance.");
            }
            using var session = new PaddleChart2TableOnnxSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU"));

            int maximumNewTokens = ParseMaximumNewTokens();
            var totalWatch = Stopwatch.StartNew();
            PaddleChart2TableGenerationResult result = session.Generate(input, tokenizer, maximumNewTokens);
            totalWatch.Stop();

            CollectionAssert.AreEqual(new[] { 7948, 69442, 760 }, result.TokenIds.Take(3).ToArray());
            Assert.AreEqual(tokenizer.IsTerminalToken(result.TokenIds.Last()) ? JYPPX.DeploySharp.Results.Language.GenerationFinishReason.EndOfSequence : JYPPX.DeploySharp.Results.Language.GenerationFinishReason.MaxTokens, result.FinishReason);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text));
            TestContext.WriteLine(JsonSerializer.Serialize(new { result.Text, tokenIds = result.TokenIds, finish = result.FinishReason.ToString(), requestedNewTokens = maximumNewTokens, totalMs = totalWatch.Elapsed.TotalMilliseconds, visionMs = result.VisionTime.TotalMilliseconds, embeddingMs = result.EmbeddingTime.TotalMilliseconds, prefillMs = result.PrefillTime.TotalMilliseconds, decodeMs = result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray() }));
        }

        private static int ParseMaximumNewTokens()
        {
            string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_OPENVINO_MAX_NEW_TOKENS");
            if (string.IsNullOrWhiteSpace(value)) return 3;
            if (!int.TryParse(value, out int parsed) || parsed < 3 || parsed > 2048) Assert.Fail("DEPLOYSHARP_CHART2TABLE_OPENVINO_MAX_NEW_TOKENS must be an integer between 3 and 2048.");
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
