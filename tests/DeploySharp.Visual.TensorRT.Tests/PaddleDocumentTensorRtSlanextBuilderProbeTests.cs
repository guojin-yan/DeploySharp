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
/// recurrent exports and, optionally, their OpenVINO compatibility graphs.
/// This is intentionally a builder-only probe: an Engine is never treated as
/// usable until the table decoder and an ORT parity run also pass.
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
        bool staticBatchOne = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SLANEXT_STATIC_BATCH1") == "1";
        TensorRtOnnxInputProfile inputProfile = staticBatchOne
            ? new TensorRtOnnxInputProfile(
                "x",
                new TensorShape(1, 3, 512, 512),
                new TensorShape(1, 3, 512, 512),
                new TensorShape(1, 3, 512, 512))
            : new TensorRtOnnxInputProfile(
                "x",
                new TensorShape(1, 3, 512, 512),
                new TensorShape(2, 3, 512, 512),
                new TensorShape(2, 3, 512, 512));
        var models = new List<(string ModelId, string FileName, string Root, string ArtifactKind, string ExpectedSha256)>
        {
            ("paddle-table/slanext-wired", "slanext-wired.onnx", modelRoot, "official", "0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f"),
            ("paddle-table/slanext-wireless", "slanext-wireless.onnx", modelRoot, "official", "5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97")
        };
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SLANEXT_INCLUDE_OPENVINO_COMPAT") == "1")
        {
            string compatibilityRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_SLANEXT_COMPAT_MODEL_ROOT")
                ?? Path.Combine(modelRoot, "..", "onnx-normalized-rerun-20260929"));
            models.Add(("paddle-table/slanext-wired-openvino-compat", "slanext-wired-openvino-compat.onnx", compatibilityRoot, "openvino-compatibility", "2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0"));
            models.Add(("paddle-table/slanext-wireless-openvino-compat", "slanext-wireless-openvino-compat.onnx", compatibilityRoot, "openvino-compatibility", "9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6"));
        }

        var records = new List<object>();
        foreach ((string modelId, string fileName, string modelDirectory, string artifactKind, string expectedSha256) in models)
        {
            string modelPath = Path.Combine(modelDirectory, fileName);
            if (!File.Exists(modelPath))
            {
                records.Add(new { modelId, fileName, artifactKind, status = "missing-model" });
                continue;
            }

            string sha256 = FileSha(modelPath);
            if (!string.Equals(expectedSha256, sha256, StringComparison.OrdinalIgnoreCase))
            {
                records.Add(new { modelId, fileName, artifactKind, expectedSha256, modelSha256 = sha256, status = "sha-mismatch" });
                continue;
            }

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
                            inputProfile
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
                artifactKind,
                expectedSha256,
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
            profile = staticBatchOne
                ? new { minimum = "[1,3,512,512]", optimum = "[1,3,512,512]", maximum = "[1,3,512,512]" }
                : new { minimum = "[1,3,512,512]", optimum = "[2,3,512,512]", maximum = "[2,3,512,512]" },
            models = records,
            boundary = "Builder-only probe for the selected original and optional OpenVINO-compatibility recurrent SLANeXt exports. A non-empty Engine is not support evidence until TensorRT execution, Paddle table Decoder output, ORT parity and HTML/quality checks pass."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(reportPath);

        Assert.AreEqual(models.Count, records.Count, "The probe must record every selected SLANeXt artifact.");
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
