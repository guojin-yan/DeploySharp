using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.TensorRT.Tests;

/// <summary>
/// Records the exact TensorRT Builder boundary for the original SLANeXt
/// recurrent exports. This is intentionally a builder-only probe: an Engine
/// is never treated as usable until the table decoder and an ORT parity run
/// also pass.
/// </summary>
[TestClass]
public sealed class PaddleDocumentTensorRtSlanextBuilderProbeTests
{
    private const string DefaultModelRoot = @"E:\Model\PaddleDocument\onnx";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void OriginalSlanextExportsRecordBuilderBoundary()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SLANEXT_PROBE") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SLANEXT_PROBE=1 to run the opt-in SLANeXt TensorRT Builder probe.");

        string modelRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_MODEL_ROOT") ?? DefaultModelRoot);
        string reportPath = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SLANEXT_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, "slanext-tensorrt-builder-probe.json"));
        string? reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);

        TensorRtApiVersion apiVersion = ResolveApiVersion();
        var records = new List<object>();
        foreach ((string modelId, string fileName) in new[]
        {
            ("paddle-table/slanext-wired", "slanext-wired.onnx"),
            ("paddle-table/slanext-wireless", "slanext-wireless.onnx")
        })
        {
            string modelPath = Path.Combine(modelRoot, fileName);
            if (!File.Exists(modelPath))
            {
                records.Add(new { modelId, fileName, status = "missing-model" });
                continue;
            }

            string sha256 = FileSha(modelPath);
            string enginePath = Path.Combine(Path.GetTempPath(), "deploysharp-slanext-trt-" + Guid.NewGuid().ToString("N") + ".engine");
            string status = "unknown";
            string? errorCode = null;
            string? message = null;
            string? technicalDetails = null;
            long engineBytes = 0;
            string? engineSha256 = null;
            try
            {
                var artifact = new ModelArtifact(new ModelId(modelId), "onnx", modelPath, sha256, TensorRtBackendProvider.BackendId);
                var build = new TensorRtOnnxEngineBuilder().Build(
                    artifact,
                    enginePath,
                    new TensorRtOnnxEngineBuildOptions(
                        apiVersion: apiVersion,
                        workspaceBytes: 1UL << 30,
                        optimizationLevel: 3,
                        inputProfiles: new[]
                        {
                            new TensorRtOnnxInputProfile(
                                "x",
                                new TensorShape(1, 3, 512, 512),
                                new TensorShape(2, 3, 512, 512),
                                new TensorShape(2, 3, 512, 512))
                        },
                        overwrite: true));
                status = File.Exists(build.EnginePath) && new FileInfo(build.EnginePath).Length > 0
                    ? "engine-built-no-inference"
                    : "empty-engine";
                if (File.Exists(build.EnginePath))
                {
                    engineBytes = new FileInfo(build.EnginePath).Length;
                    engineSha256 = FileSha(build.EnginePath);
                }
            }
            catch (TensorRtBackendException exception)
            {
                status = exception.ErrorCode is TensorRtErrorCodes.OnnxParseFailed or TensorRtErrorCodes.EngineBuildFailed
                    ? "unsupported"
                    : "blocked";
                errorCode = exception.ErrorCode.ToString();
                message = exception.Message;
                technicalDetails = exception.TechnicalDetails;
            }
            finally
            {
                try { if (File.Exists(enginePath)) File.Delete(enginePath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            records.Add(new
            {
                modelId,
                fileName,
                modelSha256 = sha256,
                status,
                errorCode,
                message,
                technicalDetails,
                engineBytes,
                engineSha256
            });
        }

        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            schemaVersion = "deploysharp-paddle-document-slanext-tensorrt-builder-probe-v1",
            generatedAtUtc = DateTimeOffset.UtcNow,
            apiVersion = (int)apiVersion,
            runtime = DescribeRuntime(),
            profile = new { minimum = "[1,3,512,512]", optimum = "[2,3,512,512]", maximum = "[2,3,512,512]" },
            models = records,
            boundary = "Builder-only probe for the two original recurrent SLANeXt exports. A non-empty Engine is not support evidence until TensorRT execution, Paddle table Decoder output, ORT parity and HTML/quality checks pass."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(reportPath);

        Assert.IsTrue(records.Count == 2, "The probe must record both wired and wireless exports.");
    }

    private static TensorRtApiVersion ResolveApiVersion()
    {
        string value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API")
            ?? Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_API")
            ?? "11";
        if (int.TryParse(value, out int number) && Enum.IsDefined(typeof(TensorRtApiVersion), number)) return (TensorRtApiVersion)number;
        if (Enum.TryParse(value, true, out TensorRtApiVersion parsed) && Enum.IsDefined(typeof(TensorRtApiVersion), parsed)) return parsed;
        throw new ArgumentException("TensorRT API must be 8, 10 or 11.", nameof(value));
    }

    private static string FileSha(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string DescribeRuntime() => string.Join(";", new[]
    {
        "JYPPX_NATIVE_BRIDGE_PATH", "JYPPX_TENSORRT_ROOT", "JYPPX_CUDA_ROOT", "JYPPX_CUDNN_ROOT"
    }.Select(name => name + "=" + (Environment.GetEnvironmentVariable(name) ?? "<unset>")));
}
