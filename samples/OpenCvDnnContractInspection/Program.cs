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
        var modelId = new ModelId("sample/opencv-classification");
        var contract = new OpenCvDnnModelContract(modelId, new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)) }, new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, 3)) });
        using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, false, false));
        using IInferenceSession session = provider.CreateSession(new ModelArtifact(modelId, "onnx", Path.Combine(AppContext.BaseDirectory, "classification.onnx"), "05a885298cca6e04b83732a46ff340f48203cc62e5fa89af74fe3eeab259de2a"), new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
        var input = new Tensor<float>(new TensorShape(1, 3, 2, 2), new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f });
        float[] output = (float[])session.Run(new InferenceInputs(new[] { new NamedTensor("images", input) }), CancellationToken.None).Single().Tensor.Buffer;
        if (output.Length != 3 || Math.Abs(output[0] - 2.5f) > 0.00001f) return 2;
        Console.WriteLine("DEPLOYSHARP_OPENCV_DNN_SAMPLE_OK model=classification.onnx output=2.5,6.5,10.5");
        return 0;
    }
}
