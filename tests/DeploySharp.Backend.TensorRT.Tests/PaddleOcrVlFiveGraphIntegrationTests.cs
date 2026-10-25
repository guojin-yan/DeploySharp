using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.TensorRT.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlFiveGraphIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalIntegration")]
    public async Task SelectedOfficialImageGeneratesWithSeparateTensorRtPrefill()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_RUN_TENSORRT") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_RUN_TENSORRT=1 with the pinned five Engine paths and official fixtures.");

        string bundle = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string model = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        string fixtures = Required("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES");
        string referencePath = Required("DEPLOYSHARP_PADDLEOCR_VL_REFERENCE");
        string engineRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_ENGINE_ROOT");
        string prefillEngine = Required("DEPLOYSHARP_PADDLEOCR_VL_PREFILL_ENGINE");
        string reportPath = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_TENSORRT_REPORT"));
        string caseId = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_CASE_ID") ?? "ocr-demo-1";
        PaddleOcrVlTask task = caseId switch
        {
            "ocr-demo-1" => PaddleOcrVlTask.TextRecognition,
            "table-official-example" => PaddleOcrVlTask.TableRecognition,
            "formula-official-example" => PaddleOcrVlTask.FormulaRecognition,
            "chart-official-example" => PaddleOcrVlTask.ChartRecognition,
            "spotting-demo-1" => PaddleOcrVlTask.TextSpotting,
            "seal-official-example" => PaddleOcrVlTask.SealRecognition,
            _ => throw new ArgumentException("Unknown fixed official case: " + caseId)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-tensorrt-five-graph-v1",
            ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["status"] = "running",
            ["phase"] = "validate-inputs",
            ["caseId"] = caseId,
            ["task"] = task.ToString(),
            ["sourceCommit"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT"),
            ["testSourceSha256"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_TEST_SOURCE_SHA256"),
            ["visualAssemblySha256"] = Hash(typeof(PaddleOcrVlOnnxSession).Assembly.Location),
            ["tensorRtAssemblySha256"] = Hash(typeof(TensorRtBackendProvider).Assembly.Location),
            ["testAssemblySha256"] = Hash(GetType().Assembly.Location),
            ["os"] = RuntimeInformation.OSDescription,
            ["framework"] = RuntimeInformation.FrameworkDescription,
            ["boundary"] = "One selected official fixture on five distinct TensorRT engines. One-token mode is only a preflight, not EOS or full-generation parity."
        };
        void Save() => File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Save();
        var watch = Stopwatch.StartNew();
        var sessions = new List<IInferenceSession>();
        try
        {
            PaddleOcrVlBundleManifest manifest = PaddleOcrVlBundleManifest.LoadFromDirectory(bundle);
            manifest.VerifyFiles(bundle);
            Assert.AreEqual(PaddleOcrVlBundleManifest.OfficialCheckpointRevision, manifest.CheckpointRevision);
            using JsonDocument fixtureManifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtures, "fixtures.json")));
            using JsonDocument reference = JsonDocument.Parse(File.ReadAllBytes(referencePath));
            JsonElement fixture = fixtureManifest.RootElement.GetProperty("cases").EnumerateArray()
                .Single(item => item.GetProperty("id").GetString() == caseId);
            JsonElement expectedCase = reference.RootElement.GetProperty("cases").EnumerateArray()
                .Single(item => item.GetProperty("id").GetString() == caseId);
            Assert.AreEqual(manifest.CheckpointRevision, fixtureManifest.RootElement.GetProperty("checkpointRevision").GetString());
            Assert.AreEqual(manifest.CheckpointRevision, reference.RootElement.GetProperty("checkpoint").GetProperty("revision").GetString());
            string patchPath = Path.Combine(fixtures, fixture.GetProperty("patchFile").GetString()!);
            Assert.AreEqual(fixture.GetProperty("patchSha256").GetString(), Hash(patchPath));
            byte[] bytes = File.ReadAllBytes(patchPath);
            var shape = new TensorShape(fixture.GetProperty("patchShape").EnumerateArray().Select(value => value.GetInt64()).ToArray());
            Assert.AreEqual(checked((int)shape.GetElementCount() * sizeof(float)), bytes.Length);
            var patches = new float[checked((int)shape.GetElementCount())];
            Buffer.BlockCopy(bytes, 0, patches, 0, bytes.Length);
            var sourceSize = new VisualSize(fixture.GetProperty("sourceSize")[0].GetInt32(), fixture.GetProperty("sourceSize")[1].GetInt32());
            PaddleOcrVlImagePlan imagePlan = PaddleOcrVlInputPlanning.PlanOfficialV16(sourceSize, task);
            Assert.AreEqual(new TensorShape(imagePlan.GridHeight, imagePlan.GridWidth, 3, 14, 14), shape);
            var input = new PaddleOcrVlPreparedInput(imagePlan, new Tensor<float>(shape, patches, TensorBufferOwnership.Transfer),
                expectedCase.GetProperty("inputSha256").GetString());
            var tokenizer = new PaddleOcrVlTokenizer(model);
            int[] expected = expectedCase.GetProperty("generatedTokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            int maximum = int.TryParse(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_TENSORRT_MAX_TOKENS"), out int configured)
                ? configured : 1;
            Assert.IsTrue(maximum >= 1 && maximum <= 512, "Keep this external test bounded to at most 512 tokens.");
            report["referenceSha256"] = Hash(referencePath);
            report["fixtureManifestSha256"] = Hash(Path.Combine(fixtures, "fixtures.json"));
            report["patchSha256"] = Hash(patchPath);
            report["grid"] = new[] { imagePlan.GridHeight, imagePlan.GridWidth };
            report["maximumNewTokens"] = maximum;
            report["expectedTokenCount"] = expected.Length;
            report["phase"] = "load-engines"; Save();

            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt10));
            foreach (string role in new[] { "vision", "embedding", "decoder", "head", "prefill" })
            {
                string path = role == "prefill" ? prefillEngine
                    : role == "vision" && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_VISION_ENGINE"))
                        ? Required("DEPLOYSHARP_PADDLEOCR_VL_VISION_ENGINE")
                        : Path.Combine(engineRoot, "TensorRT-" + role, role + ".plan");
                string hash = Hash(path);
                report["loadingGraph"] = role; Save();
                sessions.Add(provider.CreateSession(new ModelArtifact(new ModelId("paddleocr-vl/1.6/" + role),
                    "tensorrt-engine", path, hash, TensorRtBackendProvider.BackendId),
                    new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"), SessionOptions.Default));
                report[role + "EngineSha256"] = hash;
                report["loadedGraphCount"] = sessions.Count;
                Save();
            }
            report.Remove("loadingGraph");
            report["phase"] = "generate"; Save();
            using var pipeline = new PaddleOcrVlOnnxSession(sessions[0], sessions[1], sessions[2], sessions[3], sessions[4]);
            PaddleOcrVlGenerationResult actual = await pipeline.GenerateAsync(input, tokenizer,
                new PaddleOcrVlGenerationOptions(maximumNewTokens: maximum, timeout: TimeSpan.FromMinutes(15)), CancellationToken.None);
            report["tokens"] = actual.TokenIds;
            report["finishReason"] = actual.FinishReason.ToString();
            report["promptTokens"] = actual.PromptTokenCount;
            report["generationMilliseconds"] = actual.Elapsed.TotalMilliseconds;
            report["firstDifference"] = Enumerable.Range(0, Math.Min(expected.Length, actual.TokenIds.Count))
                .Where(index => expected[index] != actual.TokenIds[index]).Select(index => (int?)index).FirstOrDefault();
            bool prefixExact = actual.TokenIds.SequenceEqual(expected.Take(actual.TokenIds.Count));
            bool textExact = actual.Text == expectedCase.GetProperty("decoded").GetString();
            bool complete = actual.FinishReason == GenerationFinishReason.EndOfSequence
                && actual.TokenIds.SequenceEqual(expected) && textExact;
            report["tokenPrefixExact"] = prefixExact;
            report["textExact"] = textExact;
            report["status"] = complete ? "eos-token-text-exact"
                : prefixExact && maximum < expected.Length ? "token-prefix-exact" : "generation-different";
            Save();
            Assert.IsTrue(complete || prefixExact && maximum < expected.Length, "Inspect the five-graph report for the first difference.");
        }
        catch (Exception exception)
        {
            if (Equals(report["status"], "running")) report["status"] = "blocked";
            report["error"] = exception.ToString();
            Save();
            throw;
        }
        finally
        {
            foreach (IInferenceSession session in sessions) session.Dispose();
            report["elapsedMilliseconds"] = watch.Elapsed.TotalMilliseconds;
            Save();
        }
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException("Missing " + name);

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
