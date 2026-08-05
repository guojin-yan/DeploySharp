using System;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

internal static class Program
{
    private static int Main()
    {
        var artifact = new ModelArtifact(new ModelId("consumer/missing-native"), "onnx", Path.Combine(AppContext.BaseDirectory, "classification.onnx"));
        try
        {
            using var provider = new OnnxRuntimeBackendProvider();
            using IInferenceSession session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"), SessionOptions.Default);
            Console.Error.WriteLine("A session was unexpectedly created without an application-selected runtime package.");
            return 2;
        }
        catch (OnnxRuntimeBackendException exception) when (exception.ErrorCode == DeploySharpErrorCodes.NativeRuntimeUnavailable)
        {
            Console.WriteLine("DEPLOYSHARP_ONNXRUNTIME_NATIVE_MISSING_DIAGNOSTIC_OK");
            return 0;
        }
    }
}
