using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.TensorRT.Tests;

/// <summary>
/// Probes the exact FormulaNet/UniMERNet ONNX assets with a real TensorRT
/// dynamic-Batch=2 profile. This is deliberately a builder/importer probe:
/// a successful Engine build is not promoted to inference or decoder support
/// until the autoregressive outputs have a separate parity contract.
/// </summary>
[TestClass]
public sealed class PaddleDocumentTensorRtFormulaBuilderProbeTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";

    private static readonly FormulaCase[] Cases =
    {
        new("paddle-formula/pp-formulanet-plus-s", "pp-formulanet-plus-s.onnx", "e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d", 384, 384),
        new("paddle-formula/pp-formulanet-s", "pp-formulanet-s.onnx", "9fbcf2d7b5d7534e4c596e241f5c60be482a4d24d6a9625f2e79e140c7522877", 384, 384),
        new("paddle-formula/pp-formulanet-plus-m", "pp-formulanet-plus-m.onnx", "10d5b4645ec75b441e83e0a543c07e80d3d1704e66eeba9cb605374828465a19", 384, 384),
        new("paddle-formula/pp-formulanet-plus-l", "pp-formulanet-plus-l.onnx", "ba9ee741c8e95b1f9a026494a2bec9185a710e10298efb857fae6d375c2da2e6", 768, 768),
        new("paddle-formula/pp-formulanet-l", "pp-formulanet-l.onnx", "acf3ddbecee98ab1dbecef2339e1c0b8dd22bce3ebf8dfce59b8928e88e0bce3", 768, 768),
        new("paddle-formula/unimernet", "unimernet.onnx", "1d64fafa0161f153dafe40823e97c4b05103030509dd6b286d7c8d4a11b068ab", 672, 192)
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void FormulaModelsDynamicBatchTwoHaveExplicitTensorRtBuilderEvidence()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1 to run the FormulaNet/UniMERNet TensorRT builder probe.");
        }

        var results = new List<object>(Cases.Length);
        int environmentBlocked = 0;
        foreach (FormulaCase item in Cases)
        {
            string onnxPath = Path.Combine(ModelRoot, item.FileName);
            if (!File.Exists(onnxPath))
            {
                results.Add(new { model = item.ModelId, file = item.FileName, status = "missing-model" });
                continue;
            }

            string onnxSha256 = ComputeSha256(onnxPath);
            if (!string.Equals(onnxSha256, item.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new { model = item.ModelId, file = item.FileName, status = "unexpected-asset", expectedOnnxSha256 = item.ExpectedSha256, onnxSha256 });
                continue;
            }

            string root = Path.Combine(Path.GetTempPath(), "deploysharp-paddle-formula-trt-probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string enginePath = Path.Combine(root, Path.GetFileNameWithoutExtension(item.FileName) + "-batch2.engine");
                TensorRtApiVersion apiVersion = ResolveApiVersion();
                TensorRtOnnxEngineBuildOptions options = new(
                    apiVersion: apiVersion,
                    workspaceBytes: 536870912UL,
                    optimizationLevel: 3,
                    disableTf32: true,
                    inputProfiles: new[]
                    {
                        new TensorRtOnnxInputProfile(
                            "x",
                            new TensorShape(1, 1, item.Height, item.Width),
                            new TensorShape(2, 1, item.Height, item.Width),
                            new TensorShape(2, 1, item.Height, item.Width))
                    },
                    overwrite: true);

                try
                {
                    TensorRtOnnxEngineBuildResult build = new TensorRtOnnxEngineBuilder().Build(
                        new ModelArtifact(new ModelId(item.ModelId), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId),
                        enginePath,
                        options);

                    results.Add(new
                    {
                        model = item.ModelId,
                        file = item.FileName,
                        status = File.Exists(build.EnginePath) ? "engine-built-no-inference" : "empty-engine",
                        onnxSha256,
                        engineSha256 = build.EngineSha256,
                        engineBytes = build.EngineBytes,
                        inputShape = $"[2,1,{item.Height},{item.Width}]",
                        boundary = "Builder/importer probe only; no TensorRT execution or autoregressive decoder parity."
                    });
                }
                catch (TensorRtBackendException exception)
                {
                    bool isEnvironment = exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable;
                    if (isEnvironment) environmentBlocked++;
                    results.Add(new
                    {
                        model = item.ModelId,
                        file = item.FileName,
                        status = isEnvironment ? "environment-blocked" : "unsupported",
                        onnxSha256,
                        errorCode = exception.ErrorCode.ToString(),
                        message = exception.TechnicalDetails ?? exception.InnerException?.Message ?? exception.Message,
                        inputShape = $"[2,1,{item.Height},{item.Width}]"
                    });
                }
                catch (Exception exception)
                {
                    results.Add(new
                    {
                        model = item.ModelId,
                        file = item.FileName,
                        status = "probe-error",
                        onnxSha256,
                        exception = exception.GetType().FullName,
                        message = exception.Message,
                        inputShape = $"[2,1,{item.Height},{item.Width}]"
                    });
                }
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_TENSORRT_BUILDER_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), "paddle-document-formula-tensorrt-builder-probe-20261014.json");
        string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedAtUtc = DateTimeOffset.UtcNow,
            backend = "tensorrt-cuda",
            runtime = new { api = ResolveApiVersion().ToString(), architecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86" },
            batch = 2,
            models = results,
            boundary = "Exact FormulaNet/UniMERNet ONNX dynamic-Batch=2 TensorRT builder/importer evidence only. A built Engine is not inference, token generation, tokenizer, mathematical semantic quality, or cross-device support.",
            environmentBlocked
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(Cases.Length, results.Count, "Every registered formula asset must have an explicit probe result.");
        if (environmentBlocked > 0) Assert.Inconclusive("The TensorRT native runtime was unavailable for at least one formula asset; see the emitted probe report.");
    }

    private static TensorRtApiVersion ResolveApiVersion()
    {
        string value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API") ?? "10";
        return string.Equals(value, "11", StringComparison.OrdinalIgnoreCase) ? TensorRtApiVersion.TensorRt11 : TensorRtApiVersion.TensorRt10;
    }

    private static string ComputeSha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private sealed record FormulaCase(string ModelId, string FileName, string ExpectedSha256, int Height, int Width);
}
