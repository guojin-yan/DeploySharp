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
using JYPPX.DeploySharp.Visual.TensorRT;
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
            ["visualTensorRtAssemblySha256"] = Hash(typeof(PaddleOcrVlTensorRtDeviceSession).Assembly.Location),
            ["deviceSessionSourceSha256"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_DEVICE_SOURCE_SHA256"),
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
            bool latencyEnabled = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_TENSORRT_LATENCY_RUN") == "1";
            bool deviceKvEnabled = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_TENSORRT_DEVICE_KV") == "1";
            int warmups = latencyEnabled ? ReadCount("DEPLOYSHARP_PADDLEOCR_VL_TENSORRT_LATENCY_WARMUPS", 5, 1, 100) : 0;
            int measurements = latencyEnabled ? ReadCount("DEPLOYSHARP_PADDLEOCR_VL_TENSORRT_LATENCY_MEASUREMENTS", 50, 1, 200) : 0;
            if (latencyEnabled && maximum < expected.Length)
                throw new ArgumentException("The latency protocol requires enough tokens to reach the reference EOS.");
            report["referenceSha256"] = Hash(referencePath);
            report["fixtureManifestSha256"] = Hash(Path.Combine(fixtures, "fixtures.json"));
            report["patchSha256"] = Hash(patchPath);
            report["grid"] = new[] { imagePlan.GridHeight, imagePlan.GridWidth };
            report["maximumNewTokens"] = maximum;
            report["expectedTokenCount"] = expected.Length;
            report["phase"] = "load-engines"; Save();

            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt10));
            string[] roles = { "vision", "embedding", "decoder", "head", "prefill" };
            foreach (string role in roles)
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
            TimedSession[] timed = latencyEnabled && !deviceKvEnabled
                ? sessions.Select((session, index) => new TimedSession(roles[index], session)).ToArray()
                : Array.Empty<TimedSession>();
            IInferenceSession[] active = timed.Length > 0 ? timed.Cast<IInferenceSession>().ToArray() : sessions.ToArray();
            using var pipeline = deviceKvEnabled ? null : new PaddleOcrVlOnnxSession(active[0], active[1], active[2], active[3], active[4]);
            using var devicePipeline = deviceKvEnabled
                ? new PaddleOcrVlTensorRtDeviceSession(active[0], active[1], active[2], active[3], active[4]) : null;
            var generationOptions = new PaddleOcrVlGenerationOptions(maximumNewTokens: maximum, timeout: TimeSpan.FromMinutes(15));
            async Task<PaddleOcrVlGenerationResult> GenerateOneAsync() => devicePipeline != null
                ? devicePipeline.Generate(input, tokenizer, generationOptions)
                : await pipeline!.GenerateAsync(input, tokenizer, generationOptions, CancellationToken.None);
            report["deviceKvMode"] = deviceKvEnabled;
            var generationWatch = Stopwatch.StartNew();
            PaddleOcrVlGenerationResult actual = await GenerateOneAsync();
            generationWatch.Stop();
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
            if (latencyEnabled)
            {
                Assert.IsTrue(complete, "Repeated latency requires a complete reference-matching generation.");
                var warmupRows = new List<LatencyRow>(warmups)
                {
                    new(0, generationWatch.Elapsed.TotalMilliseconds, actual.Elapsed.TotalMilliseconds, actual.TokenIds.Count)
                };
                var sampleRows = new List<LatencyRow>(measurements);
                report["latencyProtocol"] = new
                {
                    warmupCount = warmups, measurementCount = measurements, sessions = 1,
                    percentile = "Nearest-rank",
                    timing = "Wall clock surrounds one generation call (Generate for device KV, GenerateAsync otherwise); Session elapsed is the production result.Elapsed. Engine/session creation, caller preprocessing and report writes are excluded.",
                    inputReuse = "One immutable prepared patch tensor reused for all generations; the initial correctness generation is warm-up zero."
                };
                report["latencyWarmups"] = warmupRows;
                report["latencySamples"] = sampleRows;
                report["status"] = "running";
                report["phase"] = "latency-warmup";
                Save();

                async Task<LatencyRow> RunOneAsync(int index)
                {
                    var timer = Stopwatch.StartNew();
                    PaddleOcrVlGenerationResult generated = await GenerateOneAsync();
                    timer.Stop();
                    Assert.AreEqual(GenerationFinishReason.EndOfSequence, generated.FinishReason, "Iteration " + index + " did not reach EOS.");
                    CollectionAssert.AreEqual(expected, generated.TokenIds.ToArray(), "Iteration " + index + " changed Token IDs.");
                    Assert.AreEqual(expectedCase.GetProperty("decoded").GetString(), generated.Text, "Iteration " + index + " changed text.");
                    return new LatencyRow(index, timer.Elapsed.TotalMilliseconds, generated.Elapsed.TotalMilliseconds, generated.TokenIds.Count);
                }

                for (int index = 1; index < warmups; index++)
                {
                    warmupRows.Add(await RunOneAsync(index));
                    Save();
                }
                report["phase"] = "latency-measure"; Save();
                for (int index = 0; index < measurements; index++)
                {
                    sampleRows.Add(await RunOneAsync(index));
                    Save();
                }
                report["latencySummary"] = new
                {
                    wallClockP50Milliseconds = Percentile(sampleRows.Select(row => row.WallClockMilliseconds), .50),
                    wallClockP95Milliseconds = Percentile(sampleRows.Select(row => row.WallClockMilliseconds), .95),
                    sessionP50Milliseconds = Percentile(sampleRows.Select(row => row.SessionMilliseconds), .50),
                    sessionP95Milliseconds = Percentile(sampleRows.Select(row => row.SessionMilliseconds), .95),
                    tokenCount = expected.Length
                };
                if (!deviceKvEnabled)
                {
                    int totalGenerations = warmups + measurements;
                    Assert.AreEqual(totalGenerations, timed[0].CallCount, "Vision must run once per generation.");
                    Assert.AreEqual(totalGenerations * expected.Length, timed[1].CallCount, "Embedding call count changed.");
                    Assert.AreEqual(totalGenerations * (expected.Length - 1), timed[2].CallCount, "Decoder call count changed.");
                    Assert.AreEqual(totalGenerations * expected.Length, timed[3].CallCount, "Head call count changed.");
                    Assert.AreEqual(totalGenerations, timed[4].CallCount, "Prefill must run once per generation.");
                    report["graphTimings"] = timed.Select(session => session.Summary()).ToArray();
                }
                report["boundary"] = deviceKvEnabled
                    ? "Repeated latency for one fixed fixture and device-specific TensorRT engines. Decode KV and hidden state stay on CUDA; vision, prompt prefill, token embedding and logits still cross the host. Every iteration matched EOS, Token IDs and text. Engine creation, caller preprocessing, other shapes/devices, labelled accuracy and long-run memory are excluded."
                    : "Repeated latency for one fixed fixture and five device-specific TensorRT engines. Every iteration matched EOS, Token IDs and text. Engine/session creation and caller preprocessing are excluded; GPU clocks are not locked. Host-owned KV/logits, other shapes/devices, labelled accuracy, page quality and long-run memory remain outside this result.";
                report["status"] = "eos-token-text-exact";
                report["phase"] = "completed";
                Save();
            }
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

    private static int ReadCount(string name, int fallback, int minimum, int maximum)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (!int.TryParse(value, out int parsed) || parsed < minimum || parsed > maximum)
            throw new ArgumentException(name + " must be between " + minimum + " and " + maximum + ".");
        return parsed;
    }

    private static double Percentile(IEnumerable<double> values, double quantile)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        return sorted[Math.Clamp((int)Math.Ceiling(quantile * sorted.Length) - 1, 0, sorted.Length - 1)];
    }

    private sealed record LatencyRow(int Index, double WallClockMilliseconds, double SessionMilliseconds, int TokenCount);

    private sealed class TimedSession : IInferenceSession
    {
        private readonly IInferenceSession _inner;
        private readonly List<double> _samples = new();

        public TimedSession(string role, IInferenceSession inner) { Role = role; _inner = inner; }
        public string Role { get; }
        public int CallCount => _samples.Count;
        public ModelMetadata Metadata => _inner.Metadata;

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            try { return _inner.Run(inputs, cancellationToken); }
            finally { watch.Stop(); _samples.Add(watch.Elapsed.TotalMilliseconds); }
        }

        public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            try { return await _inner.RunAsync(inputs, cancellationToken).ConfigureAwait(false); }
            finally { watch.Stop(); _samples.Add(watch.Elapsed.TotalMilliseconds); }
        }

        public object Summary() => new
        {
            role = Role, callCount = CallCount, totalMilliseconds = _samples.Sum(),
            perCallP50Milliseconds = Percentile(_samples, .50), perCallP95Milliseconds = Percentile(_samples, .95)
        };

        public void Dispose() { }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
