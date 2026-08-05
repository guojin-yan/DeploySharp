using System;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;

internal static class Program
{
    private static int Main()
    {
        var modelId = new ModelId("consumer/semantic-segmentation");
        var artifact = new ModelArtifact(modelId, "onnx", Path.Combine(AppContext.BaseDirectory, "semantic-segmentation.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var schema = new SegmentationOutputSchema("logits", SegmentationOutputKind.Logits, SegmentationTensorLayout.Nchw, 3);
        var profile = new VisualModelProfile(
            "consumer/semantic-segmentation.v1", modelId, VisualTaskId.SemanticSegmentation, "1.0", "onnx",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 3), VisualTensorLayout.Nchw),
            new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(1, 3, 2, 3)) },
            new[] { new VisualLabel(0, "background"), new VisualLabel(1, "green"), new VisualLabel(2, "blue") },
            new SemanticSegmentationDecoder(schema));
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile);
        profiles.Freeze();
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.SemanticSegmentation), request);
        var size = new VisualSize(3, 2);
        using var input = new PreparedVisualInput("images", CreateTensor(), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        SemanticSegmentationResult result = pipeline.Run(input).GetValue<SemanticSegmentationResult>();
        ushort[] expected = { 0, 1, 2, 0, 0, 1 };
        ushort[] actual = result.Mask.ToArray();
        if (actual.Length != expected.Length) return 2;
        for (int index = 0; index < expected.Length; index++) if (actual[index] != expected[index]) return 3;
        if (result.Rle == null) return 4;
        ushort[] decoded = result.Rle.Decode().ToArray();
        for (int index = 0; index < expected.Length; index++) if (decoded[index] != expected[index]) return 5;
        if (!string.Equals(result.Mask.ComputeSha256(), "2ed4fa5094662ebe63d9265149adf86858fd7b03983a35118880f09517f824de", StringComparison.Ordinal)) return 6;
        Console.WriteLine("DEPLOYSHARP_VISUAL_SEGMENTATION_CONSUMER_OK");
        return 0;
    }

    private static Tensor<float> CreateTensor()
    {
        return new Tensor<float>(new TensorShape(1, 3, 2, 3), new[]
        {
            9f, 0f, 0f, 1f, 5f, 0f,
            0f, 9f, 0f, 1f, 5f, 9f,
            0f, 0f, 9f, 0f, 5f, 9f
        });
    }
}
