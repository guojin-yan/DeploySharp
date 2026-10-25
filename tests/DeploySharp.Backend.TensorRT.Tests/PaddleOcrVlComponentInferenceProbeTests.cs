using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.TensorRT.Tests;

/// <summary>Runs one previously built PaddleOCR-VL component per process.</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlComponentInferenceProbeTests
{
    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SelectedGraphDeserializesAndRunsWithProductionSession()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_BACKEND_PROBE") != "1")
            Assert.Inconclusive("Run Invoke-PaddleOcrVlBackendProbe.ps1 to isolate native failures.");

        string graph = Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_GRAPH");
        Assert.IsTrue(graph is "embedding" or "head", "Only bounded single-input components are admitted by this probe.");
        string engine = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_ENGINE"));
        string onnx = Path.Combine(Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT"), graph + ".onnx");
        string expectedHash = Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_ENGINE_SHA256");
        string reportPath = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_REPORT"));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        string engineHash = Hash(engine);
        Assert.AreEqual(expectedHash, engineHash, "The plan must match its Builder report.");
        TensorRtApiVersion api = Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_TENSORRT_API") switch
        {
            "10" => TensorRtApiVersion.TensorRt10,
            "11" => TensorRtApiVersion.TensorRt11,
            _ => throw new InvalidOperationException("Expected TensorRT API 10 or 11.")
        };

        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-tensorrt-component-inference-v1",
            ["generatedUtc"] = DateTimeOffset.UtcNow, ["graph"] = graph,
            ["engineSha256"] = engineHash, ["engineBytes"] = new FileInfo(engine).Length,
            ["onnxSha256"] = Hash(onnx),
            ["sourceCommit"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT"),
            ["testAssemblySha256"] = Hash(GetType().Assembly.Location),
            ["os"] = RuntimeInformation.OSDescription, ["framework"] = RuntimeInformation.FrameworkDescription,
            ["backend"] = "tensorrt", ["api"] = api.ToString(),
            ["status"] = "probing", ["phase"] = "session-load",
            ["boundary"] = "One deterministic component input compared to ONNX Runtime CPU. This does not validate vision, decoder, zero-length KV prefill, token parity, generation or Bundle support."
        };
        void Save() => File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Save();
        var watch = Stopwatch.StartNew();
        try
        {
            var modelId = new ModelId("paddleocr-vl/1.6/" + graph);
            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(api));
            using IInferenceSession session = provider.CreateSession(
                new ModelArtifact(modelId, "tensorrt-engine", engine, engineHash, TensorRtBackendProvider.BackendId),
                new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
                SessionOptions.Default);
            report["phase"] = "inference"; Save();
            InferenceInputs inputs = graph == "embedding"
                ? InferenceInputs.Create("input_ids", new Tensor<long>(new TensorShape(1, 1), new long[] { 1 }))
                : InferenceInputs.Create("last_hidden_state", new Tensor<float>(new TensorShape(1, 1, 1024),
                    Enumerable.Range(0, 1024).Select(index => (index % 31 - 15) / 1000f).ToArray()));
            InferenceOutputs outputs = session.Run(inputs, CancellationToken.None);
            string outputName = graph == "embedding" ? "inputs_embeds" : "logits";
            ITensor tensor = outputs.GetRequired(outputName);
            TensorShape expectedShape = graph == "embedding" ? new TensorShape(1, 1, 1024) : new TensorShape(1, 1, 103424);
            Assert.AreEqual(TensorElementType.Float32, tensor.ElementType);
            Assert.AreEqual(expectedShape, tensor.Shape);
            float[] values = (float[])tensor.Buffer;
            Assert.IsTrue(values.All(float.IsFinite), "The component produced non-finite values.");
            Assert.IsTrue(values.Any(value => value != 0), "The component produced an all-zero output.");
            report["outputName"] = outputName;
            report["outputShape"] = expectedShape.ToString();
            report["outputSha256"] = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(values.AsSpan()))).ToLowerInvariant();
            report["outputMinimum"] = values.Min();
            report["outputMaximum"] = values.Max();
            report["phase"] = "onnxruntime-comparison"; Save();
            using var referenceProvider = new OnnxRuntimeBackendProvider(new OnnxRuntimeOptions(intraOpThreads: 4));
            using IInferenceSession referenceSession = referenceProvider.CreateSession(
                new ModelArtifact(modelId, "onnx", onnx, (string)report["onnxSha256"]!, OnnxRuntimeBackendProvider.BackendId),
                new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"),
                SessionOptions.Default);
            ITensor referenceTensor = referenceSession.Run(inputs, CancellationToken.None).GetRequired(outputName);
            Assert.AreEqual(expectedShape, referenceTensor.Shape);
            float[] reference = (float[])referenceTensor.Buffer;
            double largestAbsolute = 0;
            int mismatches = 0;
            for (int index = 0; index < values.Length; index++)
            {
                double absolute = Math.Abs((double)values[index] - reference[index]);
                largestAbsolute = Math.Max(largestAbsolute, absolute);
                if (absolute > 0.001 + Math.Abs(reference[index]) * 0.001) mismatches++;
            }
            bool sameArgMax = graph != "head" || Array.IndexOf(values, values.Max()) == Array.IndexOf(reference, reference.Max());
            report["referenceOutputSha256"] = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(reference.AsSpan()))).ToLowerInvariant();
            report["maximumAbsoluteDifference"] = largestAbsolute;
            report["outsideToleranceCount"] = mismatches;
            report["tolerance"] = "absolute <= 0.001 + 0.001 * abs(reference)";
            report["sameArgMax"] = sameArgMax;
            report["status"] = mismatches == 0 && sameArgMax ? "component-parity-pass" : "component-parity-failed";
        }
        catch (Exception exception)
        {
            report["status"] = "blocked";
            report["exceptionType"] = exception.GetType().FullName;
            report["diagnostic"] = exception.ToString();
        }
        finally
        {
            report["elapsedMilliseconds"] = watch.Elapsed.TotalMilliseconds;
            Save();
        }
        Assert.AreEqual("component-parity-pass", report["status"], "Inspect the component report for the failure phase.");
        Console.WriteLine("VL_TENSORRT_COMPONENT graph=" + graph + ";status=" + report["status"]);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException("Missing " + name);

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
