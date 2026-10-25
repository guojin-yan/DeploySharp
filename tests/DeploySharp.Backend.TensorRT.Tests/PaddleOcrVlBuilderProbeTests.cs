using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.TensorRT.Tests;

/// <summary>Probes fixed VL graphs with the production Builder; each graph needs an isolated process.</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlBuilderProbeTests
{
    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SelectedGraphRecordsProductionBuilderBoundary()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_BACKEND_PROBE") != "1")
            Assert.Inconclusive("Run Invoke-PaddleOcrVlBackendProbe.ps1 to isolate native failures.");
        string graph = Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_GRAPH");
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vision"] = "940600685c0b69453bbfb28371fee5c89782c2ea47fb0ab6f52876ad9f46dd4f",
            ["embedding"] = "1d268b40696b0f1ebf8ee67f6706a9e2feb3b64b71484d6fa87ec624ff734d86",
            ["decoder"] = "823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad",
            ["prefill"] = "9247b437a1551652572b3e25162d4d86fc969edf12ae44577aa6fc75b9fc5bdc",
            ["head"] = "8663d1fd3f2ca1cf2af308c76fadefcee12e65a6a1f402603a81085daa6d0148"
        };
        Assert.IsTrue(hashes.ContainsKey(graph), "Unknown graph role.");
        string path = Path.Combine(Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT"), graph + ".onnx");
        string reportPath = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_REPORT"));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        string hash = Hash(path);
        Assert.AreEqual(hashes[graph], hash, "This probe is limited to the fixed 1.6 export.");
        TensorRtApiVersion api = Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_TENSORRT_API") switch
        { "10" => TensorRtApiVersion.TensorRt10, "11" => TensorRtApiVersion.TensorRt11, _ => throw new InvalidOperationException("Expected TensorRT API 10 or 11.") };
        string profilePreset = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_PROBE_PROFILE") ?? "baseline";
        Assert.IsTrue(profilePreset is "baseline" or "compact-official-tasks" or "all-official-tasks" or "spotting-fixed", "Unknown PaddleOCR-VL profile preset.");
        TensorRtOnnxEnginePrecision precision = (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_PROBE_PRECISION") ?? "Float32") switch
        {
            "Float32" => TensorRtOnnxEnginePrecision.Float32,
            "Float16" => TensorRtOnnxEnginePrecision.Float16,
            _ => throw new InvalidOperationException("Expected Float32 or Float16 precision.")
        };
        int workspaceGiB = int.Parse(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_PROBE_WORKSPACE_GIB") ?? "1");
        Assert.IsTrue(workspaceGiB >= 1 && workspaceGiB <= 4, "The probe workspace must be 1-4 GiB.");
        string engine = Path.Combine(Path.GetDirectoryName(reportPath)!, graph + ".plan");
        var profiles = Profiles(graph, profilePreset);
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-tensorrt-builder-v1",
            ["generatedUtc"] = DateTimeOffset.UtcNow, ["graph"] = graph,
            ["sourceSha256"] = hash, ["sourceBytes"] = new FileInfo(path).Length,
            ["sourceCommit"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT"),
            ["testAssemblySha256"] = Hash(GetType().Assembly.Location),
            ["os"] = RuntimeInformation.OSDescription, ["framework"] = RuntimeInformation.FrameworkDescription,
            ["backend"] = "tensorrt", ["api"] = api.ToString(), ["status"] = "probing",
            ["workspaceBytes"] = (ulong)workspaceGiB << 30, ["optimizationLevel"] = 0, ["disableTf32"] = true,
            ["precision"] = precision.ToString(),
            ["profilePreset"] = profilePreset,
            ["graphPolicy"] = graph == "prefill" ? "derived by replacing 36 empty past inputs with ONNX initializers; production builder compatibility passes" : "production builder compatibility passes; no manual graph changes",
            ["profiles"] = profiles.Select(p => new { name = p.InputName, min = p.Minimum.ToArray(), opt = p.Optimum.ToArray(), max = p.Maximum.ToArray() }).ToArray(),
            ["emptyKvPrefillSupportedByManagedContract"] = false,
            ["boundary"] = "Builder-only probe. Decoder profile uses positive past lengths because the current managed profile/session contracts reject zero-sized KV. Built plans do not prove deserialization, prefill, dynamic inference, token parity or complete Bundle support. A green probe test means diagnostics were recorded."
        };
        void Save() => File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Save();
        var watch = Stopwatch.StartNew();
        try
        {
            var options = new TensorRtOnnxEngineBuildOptions(apiVersion: api,
                precision: api == TensorRtApiVersion.TensorRt10 ? precision : TensorRtOnnxEnginePrecision.RuntimeDefault,
                stronglyTypedNetwork: api == TensorRtApiVersion.TensorRt11,
                workspaceBytes: (ulong)workspaceGiB << 30, optimizationLevel: 0, overwrite: true, inputProfiles: profiles, disableTf32: true);
            var artifact = new ModelArtifact(new ModelId("paddleocr-vl/1.6/" + graph), "onnx", path, hash);
            TensorRtOnnxEngineBuildResult built = new TensorRtOnnxEngineBuilder().Build(artifact, engine, options);
            report["engineBytes"] = built.EngineBytes; report["engineSha256"] = built.EngineSha256;
            report["buildInputsSha256"] = built.BuildInputsSha256; report["status"] = "engine-built-no-inference";
        }
        catch (TensorRtBackendException exception)
        {
            report["status"] = exception.ErrorCode is TensorRtErrorCodes.OnnxParseFailed or TensorRtErrorCodes.EngineBuildFailed ? "unsupported-build" : "blocked";
            report["errorCode"] = exception.ErrorCode; report["operation"] = exception.Operation;
            report["diagnostic"] = exception.ToString(); report["technicalDetails"] = exception.TechnicalDetails;
        }
        catch (Exception exception)
        { report["status"] = "blocked"; report["diagnostic"] = exception.ToString(); }
        finally
        {
            report["elapsedMilliseconds"] = watch.Elapsed.TotalMilliseconds;
            report["nativeModules"] = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .Where(module => new[] { "nvinfer", "nvonnxparser", "jyppxtrt", "cudart", "cudnn" }
                    .Any(name => module.FileName.Contains(name, StringComparison.OrdinalIgnoreCase)))
                .Select(module => new { path = module.FileName, version = module.FileVersionInfo.FileVersion }).ToArray();
            Save();
        }
        Assert.AreNotEqual("blocked", report["status"], "Probe infrastructure failed; inspect the report.");
        Console.WriteLine("VL_TENSORRT_PROBE graph=" + graph + ";status=" + report["status"]);
    }

    private static List<TensorRtOnnxInputProfile> Profiles(string graph, string profilePreset)
    {
        var values = new List<TensorRtOnnxInputProfile>();
        void Add(string name, long[] min, long[] opt, long[] max) => values.Add(new TensorRtOnnxInputProfile(name, new TensorShape(min), new TensorShape(opt), new TensorShape(max)));
        if (graph == "vision")
        {
            if (profilePreset == "spotting-fixed")
                Add("pixel_patches", new long[] { 72, 72, 3, 14, 14 }, new long[] { 72, 72, 3, 14, 14 }, new long[] { 72, 72, 3, 14, 14 });
            else if (profilePreset != "baseline")
                Add("pixel_patches", new long[] { 10, 10, 3, 14, 14 }, new long[] { 36, 36, 3, 14, 14 },
                    new long[] { profilePreset == "all-official-tasks" ? 72 : 64, profilePreset == "all-official-tasks" ? 72 : 64, 3, 14, 14 });
            else Add("pixel_patches", new long[] { 24, 24, 3, 14, 14 }, new long[] { 48, 32, 3, 14, 14 }, new long[] { 64, 64, 3, 14, 14 });
        }
        if (graph == "embedding") Add("input_ids", new long[] { 1, 1 }, new long[] { 1, 384 }, new long[] { 1, 4096 });
        if (graph == "prefill")
        {
            long maximum = profilePreset == "all-official-tasks" ? 2048 : 1024;
            Add("inputs_embeds", new long[] { 1, 1, 1024 }, new long[] { 1, 384, 1024 }, new long[] { 1, maximum, 1024 });
            Add("position_ids", new long[] { 3, 1, 1 }, new long[] { 3, 1, 384 }, new long[] { 3, 1, maximum });
        }
        if (graph == "decoder")
        {
            Add("inputs_embeds", new long[] { 1, 1, 1024 }, new long[] { 1, 1, 1024 }, new long[] { 1, 1024, 1024 });
            Add("position_ids", new long[] { 3, 1, 1 }, new long[] { 3, 1, 1 }, new long[] { 3, 1, 1024 });
            for (int layer = 0; layer < 18; layer++)
                foreach (string kind in new[] { "key", "value" })
                    Add("past_" + kind + "_" + layer, new long[] { 1, 2, 1, 128 }, new long[] { 1, 2, 384, 128 }, new long[] { 1, 2, 4096, 128 });
        }
        return values;
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException("Missing " + name);
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
