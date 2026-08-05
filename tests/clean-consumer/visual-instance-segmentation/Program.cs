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
        var modelId = new ModelId("consumer/direct-instance-segmentation");
        var artifact = new ModelArtifact(modelId, "onnx", Path.Combine(AppContext.BaseDirectory, "direct-instance-segmentation.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var schema = new DirectInstanceSegmentationOutputSchema(
            new InstanceSegmentationCandidateSchema("boxes", "scores", "classes"),
            "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Probabilities);
        var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, overlapMode: InstanceMaskOverlapMode.ScorePriorityOwnership, maximumCandidates: 3, maximumInstances: 3));
        var profile = new VisualModelProfile(
            "consumer/direct-instance-segmentation.v1", modelId, VisualTaskId.InstanceSegmentation, "1.0", "onnx",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,4,4), VisualTensorLayout.Nchw),
            new[]
            {
                new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,3)),
                new VisualOutputBinding("masks", TensorElementType.Float32, new TensorShape(1,3,4,4))
            }, new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile);
        profiles.Freeze();
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.InstanceSegmentation), request);
        var size = new VisualSize(4,4);
        using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,4,4), new float[48]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size,size));
        InstanceSegmentationResult result = pipeline.Run(input).GetValue<InstanceSegmentationResult>();
        if (result.Instances.Count != 2) return 2;
        if (result.Instances[0].SourceIndex != 0 || result.Instances[1].SourceIndex != 2) return 3;
        if (!string.Equals(result.Instances[0].Mask.ComputeSha256(), "f0230bfddcdc93219d8a9e7e344b52f43e20e2e72ad1505892d88e99cb0fb5ae", StringComparison.Ordinal)) return 4;
        if (!string.Equals(result.Instances[1].Mask.ComputeSha256(), "98da0b32f6f202c623dcb3b5a6917b34dc20920687b422f4c5c12371f6f3e848", StringComparison.Ordinal)) return 5;
        if (result.OwnershipMap == null || result.OwnershipMap.GetOwnerIndex(2,0) != 0) return 6;
        Console.WriteLine("DEPLOYSHARP_VISUAL_INSTANCE_SEGMENTATION_CONSUMER_OK");
        return 0;
    }
}
