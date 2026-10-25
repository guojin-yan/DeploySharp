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
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CoreSessionOptions = JYPPX.DeploySharp.Models.SessionOptions;
using InferenceSession = Microsoft.ML.OnnxRuntime.InferenceSession;
using OrtEnv = Microsoft.ML.OnnxRuntime.OrtEnv;

namespace DeploySharp.Backend.OnnxRuntime.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlCudaIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalIntegration")]
    public async Task FourCudaGraphsGenerateSixRealTasksWithExactTokensAndGrowingKv()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_RUN_CUDA") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_RUN_CUDA=1 with matching managed/native CUDA runtimes.");
        string bundle = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string model = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        string fixtureRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES");
        string referencePath = Required("DEPLOYSHARP_PADDLEOCR_VL_REFERENCE");
        string output = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_CUDA_REPORT"));
        string profiles = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output) + "-profiles");
        Directory.CreateDirectory(profiles);
        using var reference = JsonDocument.Parse(File.ReadAllBytes(referencePath));
        using var fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "fixtures.json")));
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, reference.RootElement.GetProperty("checkpoint").GetProperty("revision").GetString());
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, fixtures.RootElement.GetProperty("checkpointRevision").GetString());
        Assert.AreEqual("reference-generation-complete", reference.RootElement.GetProperty("status").GetString());
        var cudaOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["use_tf32"] = "0", ["enable_cuda_graph"] = "0", ["do_copy_in_default_stream"] = "1",
            ["arena_extend_strategy"] = "kSameAsRequested", ["cudnn_conv_algo_search"] = "HEURISTIC",
            ["cudnn_conv_use_max_workspace"] = "0"
        };
        string? unifiedStream = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_CUDA_UNIFIED_STREAM");
        if (unifiedStream != null)
        {
            Assert.IsTrue(unifiedStream is "0" or "1", "Unified-stream option must be 0 or 1.");
            cudaOptions["use_ep_level_unified_stream"] = unifiedStream;
        }
        string? shrinkVisionArena = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_CUDA_SHRINK_VISION_ARENA");
        Assert.IsTrue(shrinkVisionArena is null or "0" or "1", "Vision-arena shrink option must be 0 or 1.");
        var visionRunOptions = new Dictionary<string, string>(StringComparer.Ordinal);
        if (shrinkVisionArena == "1") visionRunOptions["memory.enable_memory_arena_shrinkage"] = "gpu:0";
        var graphRows = new List<object>();
        var rows = new List<Dictionary<string, object?>>();
        var observed = new List<ObservedSession>();
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-cuda-generation-v1", ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["status"] = "running", ["checkpointRevision"] = PaddleOcrVlInputPlanning.OfficialV16Revision,
            ["referenceSha256"] = Hash(referencePath), ["processorFixtureManifestSha256"] = Hash(Path.Combine(fixtureRoot, "fixtures.json")),
            ["sourceCommit"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT") ?? "not-recorded",
            ["testSourceSha256"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_TEST_SOURCE_SHA256") ?? "not-recorded",
            ["environment"] = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), processorCount = Environment.ProcessorCount,
                provider = "CUDAExecutionProvider", device = 0, intraOpThreads = 4, interOpThreads = 1,
                graphOptimization = "All", enableMemoryPattern = false, enableCpuMemoryArena = false,
                cudaProviderOptions = cudaOptions, visionRunConfigEntries = visionRunOptions,
                streamsPerGraph = 1, requestsPerGraph = 1, batch = 1 },
            ["resourcePolicy"] = new { gcHeapHardLimit = Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"),
                glibcArenaMax = Environment.GetEnvironmentVariable("MALLOC_ARENA_MAX") },
            ["execution"] = "one-token profiled preflight followed by fresh unprofiled sessions; GenerateAsync; six sequential requests sharing four graph sessions",
            ["boundary"] = "Exact fixed-task generation comparison, not labelled quality, page routing, Windows, other weights or formal performance. Provider attribution profiles only one OCR preflight token with the same graph/provider options. Full generation uses CPU-owned intermediate tensors: KV/logits materialize on CPU every step; this is not device-resident execution. No clock lock is applied or assumed.",
            ["assemblies"] = new[] { typeof(PaddleOcrVlOnnxSession).Assembly, typeof(OnnxRuntimeOptions).Assembly, typeof(TensorShape).Assembly,
                typeof(InferenceSession).Assembly, typeof(PaddleOcrVlCudaIntegrationTests).Assembly }
                .Select(assembly => new { name = assembly.GetName().Name, version = assembly.GetName().Version?.ToString(), sha256 = Hash(assembly.Location) }).ToArray(),
            ["graphs"] = graphRows, ["cases"] = rows, ["gpuBefore"] = GpuSnapshot(), ["memoryBefore"] = MemorySnapshot()
        };
        void Save() => File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Save();
        var tokenizer = new PaddleOcrVlTokenizer(model);
        try
        {
            report["nativeRuntime"] = NativeRuntime(); Save();
            report["phase"] = "provider-attribution-preflight"; Save();
            JsonElement first = reference.RootElement.GetProperty("cases")[0];
            PaddleOcrVlPreparedInput firstInput = Prepare(first, out _);
            foreach (string role in Roles) observed.Add(Open(role, profiling: true));
            using (var preflight = new PaddleOcrVlOnnxSession(observed[0], observed[1], observed[2], observed[3]))
            {
                var result = await preflight.GenerateAsync(firstInput, tokenizer, new PaddleOcrVlGenerationOptions(maximumNewTokens: 1, timeout: TimeSpan.FromMinutes(15)));
                Assert.AreEqual(first.GetProperty("generatedTokenIds")[0].GetInt32(), result.TokenIds[0], "Preflight first token");
                report["preflight"] = new { id = first.GetProperty("id").GetString(), tokens = result.TokenIds,
                    finishReason = result.FinishReason.ToString(), generatedTokens = result.TokenIds.Count };
            }
            foreach (ObservedSession item in observed) item.Dispose();
            observed.Clear();
            report["providerAttribution"] = ReadProfiles(); Save();
            report["phase"] = "full-generation"; Save();
            foreach (string role in Roles)
            {
                report["loadingGraph"] = role; Save();
                var watch = Stopwatch.StartNew();
                var inner = Open(role, profiling: false); observed.Add(inner);
                graphRows.Add(new { role, sha256 = Hash(Path.Combine(bundle, role + ".onnx")), loadMilliseconds = watch.Elapsed.TotalMilliseconds,
                    memoryAfterLoad = MemorySnapshot(),
                    inputs = inner.Metadata.Inputs.Select(port => new { name = port.Name, elementType = port.ElementType.ToString(), shape = port.Shape.ToArray() }).ToArray(),
                    outputs = inner.Metadata.Outputs.Select(port => new { name = port.Name, elementType = port.ElementType.ToString(), shape = port.Shape.ToArray() }).ToArray() });
                Console.WriteLine("VL_CUDA_LOADED " + role); Save();
            }
            report.Remove("loadingGraph");
            report["nativeRuntime"] = NativeRuntime(); report["memoryAfterGraphLoads"] = MemorySnapshot(); Save();
            using var session = new PaddleOcrVlOnnxSession(observed[0], observed[1], observed[2], observed[3]);
            foreach (JsonElement image in reference.RootElement.GetProperty("cases").EnumerateArray())
            {
                PaddleOcrVlPreparedInput input = Prepare(image, out string patchSha);
                var row = new Dictionary<string, object?> { ["id"] = image.GetProperty("id").GetString(), ["task"] = image.GetProperty("task").GetString(),
                    ["inputSha256"] = input.SourceSha256, ["patchSha256"] = patchSha, ["patchFloatCount"] = input.PixelPatches.Shape.GetElementCount(),
                    ["grid"] = new[] { input.ImagePlan.GridHeight, input.ImagePlan.GridWidth }, ["status"] = "running" };
                rows.Add(row); foreach (ObservedSession item in observed) item.Reset(); Save();
                try
                {
                    var actual = await session.GenerateAsync(input, tokenizer, new PaddleOcrVlGenerationOptions(timeout: TimeSpan.FromMinutes(15)));
                    int[] expected = image.GetProperty("generatedTokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    row["generatedTokenIds"] = actual.TokenIds; row["text"] = actual.Text; row["finishReason"] = actual.FinishReason.ToString();
                    row["tokenExact"] = actual.TokenIds.SequenceEqual(expected); row["textExact"] = actual.Text == image.GetProperty("decoded").GetString();
                    row["elapsedMilliseconds"] = actual.Elapsed.TotalMilliseconds; row["promptTokens"] = actual.PromptTokenCount;
                    row["firstTokenDifference"] = Enumerable.Range(0, Math.Max(expected.Length, actual.TokenIds.Count))
                        .Where(index => index >= expected.Length || index >= actual.TokenIds.Count || expected[index] != actual.TokenIds[index]).Select(index => (int?)index).FirstOrDefault();
                    Assert.AreEqual(1, observed[0].Calls);
                    foreach (ObservedSession item in observed.Skip(1)) Assert.AreEqual(actual.TokenIds.Count, item.Calls);
                    Assert.AreEqual(actual.TokenIds.Count, observed[2].Trace.Count);
                    for (int step = 0; step < observed[2].Trace.Count; step++)
                    {
                        DecoderCall trace = observed[2].Trace[step];
                        Assert.AreEqual(step == 0 ? actual.PromptTokenCount : 1, trace.QueryLength);
                        Assert.AreEqual(step == 0 ? 0 : actual.PromptTokenCount + step - 1, trace.PastLength);
                        Assert.AreEqual(actual.PromptTokenCount + step, trace.PresentLength);
                    }
                    row["status"] = actual.FinishReason == GenerationFinishReason.EndOfSequence ? "generated-eos" : "incomplete-generation";
                    Console.WriteLine("VL_CUDA_GENERATED " + row["id"] + " tokens=" + actual.TokenIds.Count + " exact=" + row["tokenExact"]);
                }
                catch (Exception exception) { row["status"] = "failed"; row["error"] = exception.ToString(); }
                finally
                {
                    row["visionCalls"] = observed[0].Calls; row["embeddingCalls"] = observed[1].Calls;
                    row["decoderCalls"] = observed[2].Calls; row["headCalls"] = observed[3].Calls;
                    row["decoderTrace"] = observed[2].Trace.ToArray(); row["memoryAfter"] = MemorySnapshot(); row["gpuAfter"] = GpuSnapshot();
                }
                Save();
            }
            bool success = rows.Count == 6 && rows.All(row => Equals(row["status"], "generated-eos") && Equals(row["tokenExact"], true) && Equals(row["textExact"], true));
            report["nativeRuntime"] = NativeRuntime(); report["status"] = success ? "six-task-cuda-managed-rgb-generation-exact" : "completed-with-failures-or-generation-differences";
            report.Remove("phase"); Save();
            Assert.IsTrue(success, "Inspect the complete CUDA report for task failures or generation differences.");
        }
        catch (Exception exception) { report["status"] = "failed"; report["error"] = exception.ToString(); Save(); throw; }
        finally
        {
            foreach (ObservedSession item in observed) item.Dispose();
            report["gpuAfterDisposal"] = GpuSnapshot(); report["memoryAfterDisposal"] = MemorySnapshot(); Save();
        }

        ObservedSession Open(string role, bool profiling)
        {
            var options = new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1, enableMemoryPattern: false, enableCpuMemoryArena: false,
                executionProvider: OnnxRuntimeExecutionProvider.Cuda, cudaProviderOptions: cudaOptions,
                profilingOutputPathPrefix: profiling ? Path.Combine(profiles, role) : null);
            if (role == "vision") options = options.WithRunConfigEntries(visionRunOptions);
            using var provider = new OnnxRuntimeBackendProvider(options);
            string path = Path.Combine(bundle, role + ".onnx");
            return new ObservedSession(provider.CreateSession(new ModelArtifact(new ModelId("paddleocr-vl/1.6/" + role), "onnx", path,
                Hash(path), OnnxRuntimeBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cuda"),
                new CoreSessionOptions(enableProfiling: profiling)));
        }

        PaddleOcrVlPreparedInput Prepare(JsonElement image, out string patchSha)
        {
            string id = image.GetProperty("id").GetString()!;
            string task = image.GetProperty("task").GetString()!;
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
            patchSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.AreEqual(fixture.GetProperty("patchSha256").GetString(), patchSha);
            return input;
        }

        object[] ReadProfiles() => Roles.Select(role =>
        {
            string[] files = Directory.GetFiles(profiles, role + "_*.json"); Assert.AreEqual(1, files.Length, "Unique profile for " + role);
            using var profile = JsonDocument.Parse(File.ReadAllBytes(files[0]));
            var kernels = profile.RootElement.EnumerateArray().Where(item => item.TryGetProperty("cat", out var category) && category.GetString() == "Node"
                && item.TryGetProperty("args", out var args) && args.TryGetProperty("provider", out _)).ToArray();
            var providers = kernels.GroupBy(item => item.GetProperty("args").GetProperty("provider").GetString()!).OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            Assert.IsTrue(providers.TryGetValue("CUDAExecutionProvider", out int calls) && calls > 0, "No observed CUDA kernels for " + role);
            return (object)new { role, fileName = Path.GetFileName(files[0]), sha256 = Hash(files[0]), kernelEventsByProvider = providers,
                operationsByProvider = kernels.GroupBy(item => item.GetProperty("args").GetProperty("provider").GetString()!).ToDictionary(group => group.Key,
                    group => group.Select(item => item.GetProperty("args").GetProperty("op_name").GetString()).Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal) };
        }).ToArray();
    }

    private static readonly string[] Roles = { "vision", "embedding", "decoder", "head" };
    private sealed record DecoderCall(int QueryLength, int PastLength, int? PresentLength = null);
    private sealed class ObservedSession : IInferenceSession
    {
        private readonly IInferenceSession _inner;
        internal ObservedSession(IInferenceSession inner) => _inner = inner;
        public ModelMetadata Metadata => _inner.Metadata;
        internal int Calls { get; private set; }
        internal List<DecoderCall> Trace { get; } = new();
        internal void Reset() { Calls = 0; Trace.Clear(); }
        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        { Observe(inputs); var outputs = _inner.Run(inputs, cancellationToken); ObserveOutputs(outputs); return outputs; }
        public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        { Observe(inputs); var outputs = await _inner.RunAsync(inputs, cancellationToken); ObserveOutputs(outputs); return outputs; }
        private void Observe(InferenceInputs inputs)
        {
            Calls++;
            if (!inputs.Any(value => value.Name == "past_key_0")) return;
            int query = checked((int)inputs.GetRequired("inputs_embeds").Shape[1]);
            int past = checked((int)inputs.GetRequired("past_key_0").Shape[2]);
            NamedTensor[] caches = inputs.Where(value => value.Name.StartsWith("past_", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(36, caches.Length); foreach (NamedTensor cache in caches) Assert.AreEqual(past, cache.Tensor.Shape[2]);
            Trace.Add(new DecoderCall(query, past));
        }
        private void ObserveOutputs(InferenceOutputs outputs)
        {
            if (!outputs.Any(value => value.Name == "present_key_0")) return;
            int present = checked((int)outputs.GetRequired("present_key_0").Shape[2]);
            NamedTensor[] caches = outputs.Where(value => value.Name.StartsWith("present_", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(36, caches.Length); foreach (NamedTensor cache in caches) Assert.AreEqual(present, cache.Tensor.Shape[2]);
            Trace[^1] = Trace[^1] with { PresentLength = present };
        }
        public void Dispose() => _inner.Dispose();
    }
    private static object NativeRuntime() => new { version = OrtEnv.Instance().GetVersionString(), providers = OrtEnv.Instance().GetAvailableProviders(),
        modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Select(item => item.FileName)
            .Where(path => new[] { "onnxruntime", "cudnn", "cublas", "cudart", "libcuda.so", "nvcuda.dll" }.Any(name => path.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).Select(path => new { path, sha256 = Hash(path) }).ToArray() };
    private static object MemorySnapshot()
    {
        using Process process = Process.GetCurrentProcess();
        return new { workingSetBytes = process.WorkingSet64, peakWorkingSetBytes = process.PeakWorkingSet64,
            managedLiveBytes = GC.GetTotalMemory(false), managedHeapSizeBytes = GC.GetGCMemoryInfo().HeapSizeBytes };
    }
    private static object GpuSnapshot()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("nvidia-smi", "--query-gpu=name,driver_version,memory.total,memory.used,pstate,clocks.current.graphics,clocks.current.memory --format=csv")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            if (process == null) return new { status = "unavailable" };
            if (!process.WaitForExit(10000)) return new { status = "observation-timeout" };
            return new { sampledUtc = DateTimeOffset.UtcNow, exitCode = process.ExitCode, csv = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd() };
        }
        catch (Exception exception) { return new { status = "unavailable", error = exception.Message }; }
    }
    private static string Hash(string path)
    { using FileStream stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static string Required(string name)
    { string? value = Environment.GetEnvironmentVariable(name); if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Set " + name); return value!; }
}
