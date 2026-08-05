using System;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

internal static class Program
{
    private static int Main()
    {
        var artifact = new ModelArtifact(new ModelId("consumer/openvino-missing-native"), "onnx", Path.Combine(AppContext.BaseDirectory, "classification.onnx"));
        try
        {
            using var provider = new OpenVinoBackendProvider();
            using IInferenceSession session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU"), SessionOptions.Default);
            Console.Error.WriteLine("A session was unexpectedly created without an application-selected OpenVINO runtime package.");
            return 2;
        }
        catch (OpenVinoBackendException exception) when (exception.ErrorCode == DeploySharpErrorCodes.NativeRuntimeUnavailable)
        {
            Console.WriteLine("DEPLOYSHARP_OPENVINO_NATIVE_MISSING_DIAGNOSTIC_OK");
            return 0;
        }
    }
}
