using System;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

internal static class Program
{
    private static int Main()
    {
        var modelId = new ModelId("fixture/opencv-classification");
        string path = Path.Combine(AppContext.BaseDirectory, "classification.onnx");
        var contract = new OpenCvDnnModelContract(modelId, new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)) }, new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, 3)) });
        using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, false, false));
        using IInferenceSession session = provider.CreateSession(new ModelArtifact(modelId, "onnx", path, "05a885298cca6e04b83732a46ff340f48203cc62e5fa89af74fe3eeab259de2a"), new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
        var tensor = new Tensor<float>(new TensorShape(1, 3, 2, 2), new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f });
        float[] output = (float[])session.Run(new InferenceInputs(new[] { new NamedTensor("images", tensor) }), CancellationToken.None).Single().Tensor.Buffer;
        if (output.Length != 3) return 2;
        Console.WriteLine("DEPLOYSHARP_OPENCV_DNN_PACKAGE_CONSUMER_OK model=classification.onnx output=3 target=cpu");
        return 0;
    }
}
