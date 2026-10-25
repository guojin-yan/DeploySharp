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
        Assert.IsTrue(graph is "embedding" or "head" or "vision" or "decoder", "Unknown PaddleOCR-VL graph role.");
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
            ["boundary"] = "One component input compared to ONNX Runtime CPU. Decoder uses positive-length synthetic KV, not the zero-length prefill required for generation. Component parity is not token, generation or Bundle parity."
        };
        void Save() => File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Save();
        var watch = Stopwatch.StartNew();
        try
        {
            var modelId = new ModelId("paddleocr-vl/1.6/" + graph);
            InferenceInputs inputs = graph switch
            {
                "embedding" => InferenceInputs.Create("input_ids", new Tensor<long>(new TensorShape(1, 1), new long[] { 1 })),
                "head" => InferenceInputs.Create("last_hidden_state", new Tensor<float>(new TensorShape(1, 1, 1024),
                    Enumerable.Range(0, 1024).Select(index => (index % 31 - 15) / 1000f).ToArray())),
                "vision" => CreateVisionInput(report),
                _ => CreateDecoderInput(report)
            };
            string outputName = graph switch { "embedding" => "inputs_embeds", "head" => "logits", "vision" => "image_features", _ => "last_hidden_state" };
            TensorShape expectedShape = graph switch
            {
                "embedding" => new TensorShape(1, 1, 1024),
                "head" => new TensorShape(1, 1, 103424),
                "vision" => new TensorShape(324, 1024),
                _ => new TensorShape(1, 1, 1024)
            };
            float[] values;
            var present = new Dictionary<string, float[]>(StringComparer.Ordinal);
            {
                using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(api));
                using IInferenceSession session = provider.CreateSession(
                    new ModelArtifact(modelId, "tensorrt-engine", engine, engineHash, TensorRtBackendProvider.BackendId),
                    new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
                    SessionOptions.Default);
                report["phase"] = "inference"; Save();
                InferenceOutputs outputs = session.Run(inputs, CancellationToken.None);
                ITensor tensor = outputs.GetRequired(outputName);
                Assert.AreEqual(TensorElementType.Float32, tensor.ElementType);
                Assert.AreEqual(expectedShape, tensor.Shape);
                values = (float[])tensor.Buffer;
                if (graph == "decoder")
                {
                    TensorShape presentShape = new TensorShape(1, 2, 2, 128);
                    for (int layer = 0; layer < 18; layer++)
                        foreach (string kind in new[] { "key", "value" })
                        {
                            string name = "present_" + kind + "_" + layer;
                            ITensor cached = outputs.GetRequired(name);
                            Assert.AreEqual(TensorElementType.Float32, cached.ElementType, name);
                            Assert.AreEqual(presentShape, cached.Shape, name);
                            present.Add(name, (float[])cached.Buffer);
                        }
                }
            }
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
            InferenceOutputs referenceOutputs = referenceSession.Run(inputs, CancellationToken.None);
            ITensor referenceTensor = referenceOutputs.GetRequired(outputName);
            Assert.AreEqual(expectedShape, referenceTensor.Shape);
            float[] reference = (float[])referenceTensor.Buffer;
            Assert.IsTrue(reference.All(float.IsFinite), "The ONNX Runtime reference produced non-finite values.");
            double largestAbsolute = 0;
            double squaredError = 0;
            int mismatches = 0;
            int totalElements = 0;
            double absoluteTolerance = graph == "vision" ? 0.003 : 0.001;
            var mismatchedOutputs = new List<string>();
            void Compare(string name, float[] actual, float[] expected)
            {
                Assert.AreEqual(expected.Length, actual.Length, name);
                bool mismatch = false;
                for (int index = 0; index < actual.Length; index++)
                {
                    double absolute = Math.Abs((double)actual[index] - expected[index]);
                    largestAbsolute = Math.Max(largestAbsolute, absolute);
                    squaredError += absolute * absolute;
                    if (absolute > absoluteTolerance + Math.Abs(expected[index]) * 0.001) { mismatches++; mismatch = true; }
                }
                totalElements += actual.Length;
                if (mismatch) mismatchedOutputs.Add(name);
            }
            Compare(outputName, values, reference);
            if (graph == "decoder")
                foreach ((string name, float[] actual) in present)
                {
                    ITensor referenceCache = referenceOutputs.GetRequired(name);
                    Assert.AreEqual(new TensorShape(1, 2, 2, 128), referenceCache.Shape, name);
                    float[] expected = (float[])referenceCache.Buffer;
                    Assert.IsTrue(expected.All(float.IsFinite) && actual.All(float.IsFinite), "Non-finite decoder cache: " + name);
                    Compare(name, actual, expected);
                }
            bool sameArgMax = graph != "head" || Array.IndexOf(values, values.Max()) == Array.IndexOf(reference, reference.Max());
            report["referenceOutputSha256"] = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(reference.AsSpan()))).ToLowerInvariant();
            report["maximumAbsoluteDifference"] = largestAbsolute;
            report["rootMeanSquareDifference"] = Math.Sqrt(squaredError / totalElements);
            report["outsideToleranceCount"] = mismatches;
            report["comparedOutputCount"] = 1 + present.Count;
            report["comparedElementCount"] = totalElements;
            report["mismatchedOutputs"] = mismatchedOutputs;
            report["tolerance"] = "absolute <= " + absoluteTolerance.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " + 0.001 * abs(reference)";
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

    private static InferenceInputs CreateVisionInput(Dictionary<string, object?> report)
    {
        string root = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_FIXTURE_ROOT"));
        string manifestPath = Path.Combine(root, "fixtures.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        Assert.AreEqual("c5630abae1d940eafe0697512a0325494b02ab42", manifest.RootElement.GetProperty("checkpointRevision").GetString());
        JsonElement fixture = manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(value => value.GetProperty("id").GetString() == "ocr-demo-1");
        string fileName = fixture.GetProperty("patchFile").GetString()!;
        Assert.AreEqual("ocr-demo-1.patch-fp32.bin", fileName);
        string path = Path.Combine(root, fileName);
        byte[] bytes = File.ReadAllBytes(path);
        string patchHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.AreEqual("b4e4d8db74e1d1d6188dec09c84827ad42db6b420a10217ad62e21c2af9d31ae", patchHash);
        Assert.AreEqual(fixture.GetProperty("patchSha256").GetString(), patchHash);
        TensorShape shape = new TensorShape(fixture.GetProperty("patchShape").EnumerateArray().Select(value => value.GetInt64()).ToArray());
        Assert.AreEqual(new TensorShape(36, 36, 3, 14, 14), shape);
        Assert.AreEqual(checked((int)shape.GetElementCount() * sizeof(float)), bytes.Length);
        var pixels = new float[checked((int)shape.GetElementCount())];
        Buffer.BlockCopy(bytes, 0, pixels, 0, bytes.Length);
        report["fixtureCase"] = "ocr-demo-1";
        report["fixtureManifestSha256"] = Hash(manifestPath);
        report["fixturePatchSha256"] = patchHash;
        report["fixtureInputShape"] = shape.ToString();
        return InferenceInputs.Create("pixel_patches", new Tensor<float>(shape, pixels, TensorBufferOwnership.Transfer));
    }

    private static InferenceInputs CreateDecoderInput(Dictionary<string, object?> report)
    {
        var inputs = new List<NamedTensor>(38)
        {
            new NamedTensor("inputs_embeds", new Tensor<float>(new TensorShape(1, 1, 1024),
                Enumerable.Range(0, 1024).Select(index => (index % 53 - 26) / 1000f).ToArray())),
            new NamedTensor("position_ids", new Tensor<long>(new TensorShape(3, 1, 1), new long[] { 1, 1, 1 }))
        };
        var cacheShape = new TensorShape(1, 2, 1, 128);
        for (int layer = 0; layer < 18; layer++)
            foreach (string kind in new[] { "key", "value" })
            {
                int offset = layer * 2 + (kind == "value" ? 1 : 0);
                float[] cache = Enumerable.Range(0, 256)
                    .Select(index => ((index + offset * 17) % 47 - 23) / 1000f).ToArray();
                inputs.Add(new NamedTensor("past_" + kind + "_" + layer,
                    new Tensor<float>(cacheShape, cache, TensorBufferOwnership.Transfer)));
            }
        report["fixtureCase"] = "synthetic-positive-kv-1";
        report["fixtureInputShape"] = "inputs_embeds=[1,1,1024];position_ids=[3,1,1];past_key/value_0..17=[1,2,1,128]";
        return new InferenceInputs(inputs);
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
