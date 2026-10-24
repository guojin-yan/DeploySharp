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
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalIntegration")]
    public async Task FourDynamicGraphsGenerateAllSixRealTasksWithGrowingKvAndExactReferenceTokens()
    {
        string bundle = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string model = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        string fixtureRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES");
        string referencePath = Required("DEPLOYSHARP_PADDLEOCR_VL_REFERENCE");
        string output = Required("DEPLOYSHARP_PADDLEOCR_VL_OPENVINO_REPORT");
        var options = new OpenVinoOptions(performanceHint: OpenVinoPerformanceHint.Latency, streams: 1,
            inferenceThreads: 4, requestCount: 1);
        using var reference = JsonDocument.Parse(File.ReadAllBytes(referencePath));
        using var fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "fixtures.json")));
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, reference.RootElement.GetProperty("checkpoint").GetProperty("revision").GetString());
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, fixtures.RootElement.GetProperty("checkpointRevision").GetString());
        Assert.AreEqual("reference-generation-complete", reference.RootElement.GetProperty("status").GetString());
        var graphRows = new List<object>();
        var rows = new List<Dictionary<string, object?>>();
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-openvino-generation-v1", ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["status"] = "running", ["checkpointRevision"] = PaddleOcrVlInputPlanning.OfficialV16Revision,
            ["referenceSha256"] = Hash(referencePath), ["processorFixtureManifestSha256"] = Hash(Path.Combine(fixtureRoot, "fixtures.json")),
            ["environment"] = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), processorCount = Environment.ProcessorCount,
                processor = Processor(), backend = "OpenVINO", device = "CPU", requestedPrecision = "native-default", compileProperties = options.CompileProperties,
                threads = 4, streams = 1, requests = 1, modelInstancesPerGraph = 1 },
            ["execution"] = "GenerateAsync; six sequential requests sharing four independent graph sessions",
            ["runLabel"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_RUN_LABEL") ?? "not-recorded",
            ["memoryBeforeGraphLoads"] = MemorySnapshot(),
            ["resourcePolicy"] = new { gcHeapHardLimit = Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"),
                availableManagedMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                glibcArenaMax = Environment.GetEnvironmentVariable("MALLOC_ARENA_MAX") },
            ["sourceCommit"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT") ?? "not-recorded",
            ["testSourceSha256"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_TEST_SOURCE_SHA256") ?? "not-recorded",
            ["assemblies"] = new[] { typeof(PaddleOcrVlOnnxSession).Assembly, typeof(OpenVinoOptions).Assembly,
                typeof(OpenVinoSharp.Core).Assembly, typeof(TensorShape).Assembly, GetType().Assembly }
                .Select(assembly => new { name = assembly.GetName().Name, version = assembly.GetName().Version?.ToString(), sha256 = Hash(assembly.Location) }).ToArray(),
            ["graphs"] = graphRows, ["cases"] = rows,
            ["boundary"] = "Production C# four-graph generation with managed RGB preparation on one OpenVINO CPU device. Exact checkpoint/images and per-step empty/growing KV traced. Not labelled quality, automatic layout, CUDA/TensorRT/OpenCV inference or formal performance; input/output float32 alone does not establish native internal computation precision."
        };
        void Save()
        {
            string full = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        Save();
        var sessions = new List<ObservedSession>();
        try
        {
            using var registry = new BackendRegistry(); registry.UseOpenVino(options);
            foreach (string role in new[] { "vision", "embedding", "decoder", "head" })
            {
                string path = Path.Combine(bundle, role + ".onnx"); string sha = Hash(path);
                report["loadingGraph"] = role; Save();
                var watch = Stopwatch.StartNew();
                var artifact = new ModelArtifact(new ModelId("paddleocr-vl/1.6/" + role), "onnx", path, sha, OpenVinoBackendProvider.BackendId);
                var inner = registry.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU"));
                sessions.Add(new ObservedSession(inner));
                graphRows.Add(new { role, sha256 = sha, loadCompileMilliseconds = watch.Elapsed.TotalMilliseconds, memoryAfterCompilation = MemorySnapshot(),
                    inputs = inner.Metadata.Inputs.Select(port => new { name = port.Name, elementType = port.ElementType.ToString(), shape = port.Shape.ToArray() }).ToArray(),
                    outputs = inner.Metadata.Outputs.Select(port => new { name = port.Name, elementType = port.ElementType.ToString(), shape = port.Shape.ToArray() }).ToArray() });
                Console.WriteLine("VL_OPENVINO_LOADED " + role); Save();
            }
            report.Remove("loadingGraph");
            report["memoryAfterGraphLoads"] = MemorySnapshot();
            OpenVinoSharp.Version version = OpenVinoSharp.Ov.get_openvino_version();
            report["nativeRuntime"] = new { buildNumber = version.buildNumber, description = version.description,
                modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                    .Where(module => module.FileName.Contains("openvino", StringComparison.OrdinalIgnoreCase))
                    .Select(module => module.FileName).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => new { path, sha256 = Hash(path) }).ToArray() };
            Save();
            using var session = new PaddleOcrVlOnnxSession(sessions[0], sessions[1], sessions[2], sessions[3]);
            var tokenizer = new PaddleOcrVlTokenizer(model);
            foreach (JsonElement image in reference.RootElement.GetProperty("cases").EnumerateArray())
            {
                string id = image.GetProperty("id").GetString()!, task = image.GetProperty("task").GetString()!;
                PaddleOcrVlTask taskId = task switch { "ocr" => PaddleOcrVlTask.TextRecognition, "table" => PaddleOcrVlTask.TableRecognition,
                    "formula" => PaddleOcrVlTask.FormulaRecognition, "chart" => PaddleOcrVlTask.ChartRecognition,
                    "spotting" => PaddleOcrVlTask.TextSpotting, "seal" => PaddleOcrVlTask.SealRecognition, _ => throw new InvalidDataException("Unknown task") };
                JsonElement fixture = fixtures.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("id").GetString() == id);
                Assert.AreEqual(image.GetProperty("inputSha256").GetString(), fixture.GetProperty("inputSha256").GetString());
                string rgbName = fixture.GetProperty("rgbFile").GetString()!; Assert.AreEqual(rgbName, Path.GetFileName(rgbName));
                string rgbPath = Path.Combine(fixtureRoot, rgbName); Assert.AreEqual(fixture.GetProperty("rgbSha256").GetString(), Hash(rgbPath));
                var size = new VisualSize(fixture.GetProperty("sourceSize")[0].GetInt32(), fixture.GetProperty("sourceSize")[1].GetInt32());
                var input = new PaddleOcrVlImageProcessor().Create(File.ReadAllBytes(rgbPath), size, taskId, sourceSha256: image.GetProperty("inputSha256").GetString());
                float[] values = input.PixelPatches.ToArray(); var bytes = new byte[values.Length * sizeof(float)]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
                string patchSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                Assert.AreEqual(fixture.GetProperty("patchSha256").GetString(), patchSha);
                var row = new Dictionary<string, object?> { ["id"] = id, ["task"] = task, ["inputSha256"] = input.SourceSha256,
                    ["patchSha256"] = patchSha, ["patchFloatCount"] = values.Length, ["grid"] = new[] { input.ImagePlan.GridHeight, input.ImagePlan.GridWidth }, ["status"] = "running" };
                rows.Add(row); foreach (ObservedSession observed in sessions) observed.Reset(); Save();
                try
                {
                    PaddleOcrVlGenerationResult actual = await session.GenerateAsync(input, tokenizer, new PaddleOcrVlGenerationOptions(timeout: TimeSpan.FromMinutes(15)));
                    int[] expected = image.GetProperty("generatedTokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    bool exact = actual.TokenIds.SequenceEqual(expected);
                    row["generatedTokenIds"] = actual.TokenIds; row["text"] = actual.Text; row["finishReason"] = actual.FinishReason.ToString();
                    row["tokenExact"] = exact; row["textExact"] = actual.Text == image.GetProperty("decoded").GetString();
                    row["elapsedMilliseconds"] = actual.Elapsed.TotalMilliseconds; row["promptTokens"] = actual.PromptTokenCount;
                    row["firstTokenDifference"] = Enumerable.Range(0, Math.Max(expected.Length, actual.TokenIds.Count))
                        .Where(index => index >= expected.Length || index >= actual.TokenIds.Count || expected[index] != actual.TokenIds[index]).Select(index => (int?)index).FirstOrDefault();
                    Assert.AreEqual(1, sessions[0].Calls, "Image must only be encoded once");
                    Assert.AreEqual(actual.TokenIds.Count, sessions[2].Calls);
                    Assert.AreEqual(actual.TokenIds.Count, sessions[1].Calls);
                    Assert.AreEqual(actual.TokenIds.Count, sessions[3].Calls);
                    for (int step = 0; step < sessions[2].Trace.Count; step++)
                    {
                        var trace = sessions[2].Trace[step];
                        Assert.AreEqual(step == 0 ? actual.PromptTokenCount : 1, trace.QueryLength);
                        Assert.AreEqual(step == 0 ? 0 : actual.PromptTokenCount + step - 1, trace.PastLength);
                        Assert.AreEqual(actual.PromptTokenCount + step, trace.PresentLength);
                    }
                    row["status"] = actual.FinishReason == GenerationFinishReason.EndOfSequence ? "generated-eos" : "incomplete-generation";
                    Console.WriteLine("VL_OPENVINO_GENERATED " + id + " tokens=" + actual.TokenIds.Count + " exact=" + exact + " finish=" + actual.FinishReason);
                }
                catch (Exception exception) { row["status"] = "failed"; row["error"] = exception.ToString(); }
                finally
                {
                    row["visionCalls"] = sessions[0].Calls; row["embeddingCalls"] = sessions[1].Calls; row["decoderCalls"] = sessions[2].Calls; row["headCalls"] = sessions[3].Calls;
                    row["decoderTrace"] = sessions[2].Trace.ToArray();
                    using Process process = Process.GetCurrentProcess();
                    row["processWorkingSetBytes"] = process.WorkingSet64; row["processPeakWorkingSetBytes"] = process.PeakWorkingSet64;
                    row["managedLiveBytes"] = GC.GetTotalMemory(false);
                    row["managedHeapSizeBytes"] = GC.GetGCMemoryInfo().HeapSizeBytes;
                    row["managedFragmentedBytes"] = GC.GetGCMemoryInfo().FragmentedBytes;
                    row["processThreadCount"] = process.Threads.Count;
                }
                Save();
            }
            bool success = rows.Count == 6 && rows.All(row => Equals(row["status"], "generated-eos") && Equals(row["tokenExact"], true) && Equals(row["textExact"], true));
            report["status"] = success ? "six-task-openvino-managed-rgb-generation-exact" : "completed-with-failures-or-generation-differences"; Save();
            Assert.IsTrue(success, "One or more tasks failed or differed; inspect the complete six-task report.");
        }
        catch (Exception exception) { report["status"] = "failed"; report["error"] = exception.ToString(); Save(); throw; }
        finally { foreach (ObservedSession observed in sessions) observed.Dispose(); }
    }

    private sealed class ObservedSession : IInferenceSession
    {
        private readonly IInferenceSession _inner;
        internal ObservedSession(IInferenceSession inner) { _inner = inner; }
        public ModelMetadata Metadata => _inner.Metadata;
        internal int Calls { get; private set; }
        internal List<DecoderCall> Trace { get; } = new();
        internal void Reset() { Calls = 0; Trace.Clear(); }
        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken token)
        { Observe(inputs); InferenceOutputs outputs = _inner.Run(inputs, token); ObserveOutputs(outputs); return outputs; }
        public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken token)
        { Observe(inputs); InferenceOutputs outputs = await _inner.RunAsync(inputs, token); ObserveOutputs(outputs); return outputs; }
        private void Observe(InferenceInputs inputs)
        {
            Calls++;
            if (!inputs.Any(value => value.Name == "past_key_0")) return;
            int query = checked((int)inputs.GetRequired("inputs_embeds").Shape[1]);
            int past = checked((int)inputs.GetRequired("past_key_0").Shape[2]);
            int count = 0;
            foreach (NamedTensor tensor in inputs.Where(value => value.Name.StartsWith("past_", StringComparison.Ordinal)))
            { Assert.AreEqual(past, tensor.Tensor.Shape[2]); count++; }
            Assert.AreEqual(36, count); Trace.Add(new DecoderCall(query, past));
        }
        private void ObserveOutputs(InferenceOutputs outputs)
        {
            if (!outputs.Any(value => value.Name == "present_key_0")) return;
            DecoderCall trace = Trace[^1];
            int present = checked((int)outputs.GetRequired("present_key_0").Shape[2]);
            int count = 0;
            foreach (NamedTensor tensor in outputs.Where(value => value.Name.StartsWith("present_", StringComparison.Ordinal)))
            { Assert.AreEqual(present, tensor.Tensor.Shape[2]); count++; }
            Assert.AreEqual(36, count); Trace[^1] = trace with { PresentLength = present };
        }
        public void Dispose() => _inner.Dispose();
    }
    private sealed record DecoderCall(int QueryLength, int PastLength, int? PresentLength = null);
    private static object MemorySnapshot()
    {
        using Process process = Process.GetCurrentProcess();
        return new { workingSetBytes = process.WorkingSet64, peakWorkingSetBytes = process.PeakWorkingSet64,
            privateMemoryBytes = process.PrivateMemorySize64, managedLiveBytes = GC.GetTotalMemory(false),
            managedHeapSizeBytes = GC.GetGCMemoryInfo().HeapSizeBytes, threads = process.Threads.Count };
    }
    private static string Processor()
    {
        const string linuxPath = "/proc/cpuinfo";
        if (File.Exists(linuxPath))
            return File.ReadLines(linuxPath).First(line => line.StartsWith("model name", StringComparison.Ordinal)).Split(':', 2)[1].Trim();
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not-recorded";
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Optional real PaddleOCR-VL OpenVINO validation requires " + name);
        return value!;
    }
}
