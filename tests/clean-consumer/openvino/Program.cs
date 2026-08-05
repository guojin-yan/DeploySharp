using System;
using System.IO;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;

internal static class Program
{
    private static int Main()
    {
        var modelId = new ModelId("consumer/openvino-classification");
        var artifact = new ModelArtifact(modelId, "onnx", Path.Combine(AppContext.BaseDirectory, "classification.onnx"), preferredBackend: OpenVinoBackendProvider.BackendId);
        var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        using var registry = new BackendRegistry();
        registry.UseOpenVino();
        using (IInferenceSession session = registry.CreateSession(artifact, request, SessionOptions.Default))
        {
            float[] scores = (float[])session.Run(InferenceInputs.Create("images", CreateTensor()), CancellationToken.None).GetRequired("scores").Buffer;
            if (scores.Length != 3 || scores[0] != 1f || scores[1] != 2f || scores[2] != 3f) return 2;
        }

        var profile = new VisualModelProfile(
            "consumer/openvino-classification.v1", modelId, VisualTaskId.ImageClassification, "1.0", "onnx",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
            new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
            new[] { new VisualLabel(0, "one"), new VisualLabel(1, "two"), new VisualLabel(2, "three") },
            new ClassificationDecoder("scores"));
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile);
        profiles.Freeze();
        using (var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.ImageClassification), request))
        {
            var size = new VisualSize(2, 2);
            using var input = new PreparedVisualInput("images", CreateTensor(), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            ClassificationResult result = pipeline.Run(input).GetValue<ClassificationResult>();
            if (result.TopPrediction?.Index != 2) return 3;
        }
        Console.WriteLine("DEPLOYSHARP_OPENVINO_CONSUMER_OK");
        return 0;
    }

    private static Tensor<float> CreateTensor() => new Tensor<float>(new TensorShape(1, 3, 2, 2), new[]
    {
        1f, 1f, 1f, 1f,
        2f, 2f, 2f, 2f,
        3f, 3f, 3f, 3f
    });
}
