using System;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.TensorRT.Tests;

[TestClass]
public sealed class PaddleOcrV4ServerTensorRtIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalModels")]
    public void TensorRt11BridgeLoadsAndRunsV4ServerEngines()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_RUN_EXTERNAL") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1 to reproduce the local v4 server TensorRT bridge blocker.");
        string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? @"E:\Model\paddleocr";
        string[] engines =
        {
            Path.Combine(root, "PP-OCRv4", "PP-OCRv4_server_det.onnx.engine"),
            Path.Combine(root, "PP-OCRv4", "PP-OCRv4_server_rec.onnx.engine"),
            Path.Combine(root, "PP-OCRv4", "PP-OCRv4_mobile_cls.onnx.engine")
        };
        foreach (string enginePath in engines) if (!File.Exists(enginePath)) Assert.Inconclusive("Missing v4 server engine: " + enginePath);
        var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11, cudaTargetArchitecture: Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86"));
        try
        {
            foreach (string enginePath in engines)
            {
                string modelId = Path.GetFileName(enginePath).Contains("_det", StringComparison.OrdinalIgnoreCase) ? "paddleocr/ppocrv4/server-det" : Path.GetFileName(enginePath).Contains("_cls", StringComparison.OrdinalIgnoreCase) ? "paddleocr/ppocrv4/legacy-cls" : "paddleocr/ppocrv4/server-rec";
                try
                {
                    using var session = provider.CreateSession(
                        new ModelArtifact(new ModelId(modelId), "tensorrt-engine", enginePath, Sha256(enginePath), TensorRtBackendProvider.BackendId),
                        new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
                        SessionOptions.Default);
                    Assert.IsTrue(session.Metadata.Inputs.Count > 0, "TensorRT session metadata must expose an input binding.");
                    TensorDescriptor input = session.Metadata.Inputs[0];
                    TensorShape shape = modelId.EndsWith("server-det", StringComparison.Ordinal) ? new TensorShape(1, 3, 736, 736)
                        : modelId.EndsWith("legacy-cls", StringComparison.Ordinal) ? new TensorShape(1, 3, 48, 192)
                        : new TensorShape(1, 3, 48, 160);
                    InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.Name, new Tensor<float>(shape, new float[checked((int)shape.GetElementCount())])), System.Threading.CancellationToken.None);
                    Assert.IsTrue(outputs.Count > 0 && outputs[0].Tensor.Length > 0, "The v4 server engine must return a non-empty output.");
                    Console.WriteLine("TENSORRT_V4_SERVER_ENGINE_PASS model=" + modelId + ";input=" + input.Name + shape + ";outputs=" + outputs.Count + ";firstOutputElements=" + outputs[0].Tensor.Length);
                }
                catch (TensorRtBackendException exception)
                {
                    Assert.Fail("The v4 server bridge still failed for " + modelId + ": " + exception);
                }
            }
        }
        finally { provider.Dispose(); }
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
