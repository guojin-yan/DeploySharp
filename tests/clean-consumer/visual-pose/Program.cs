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
        var modelId = new ModelId("consumer/direct-pose");
        var artifact = new ModelArtifact(modelId, "onnx", Path.Combine(AppContext.BaseDirectory, "direct-pose.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var topology = new PoseTopology(new[]
        {
            new PoseKeypointDefinition(0, "left", 1, oksSigma: .1f),
            new PoseKeypointDefinition(1, "right", 0, oksSigma: .1f),
            new PoseKeypointDefinition(2, "center", oksSigma: .1f)
        }, new[] { new PoseSkeletonEdge(0,2), new PoseSkeletonEdge(1,2) });
        var schema = new DirectPoseOutputSchema("keypoints", 3, 4, visibilityComponentIndex: 3, boxesOutputName: "boxes", instanceScoresOutputName: "scores");
        var decoder = new DirectPoseDecoder(schema, topology, new PoseDecoderOptions(instanceScoreThreshold: .1f, maximumCandidates: 3, maximumInstances: 3, oks: new PoseOksOptions(.8f)));
        var profile = new VisualModelProfile(
            "consumer/direct-pose.v1", modelId, VisualTaskId.PoseEstimation, "1.0", "onnx",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,100,100), VisualTensorLayout.Nchw),
            new[]
            {
                new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                new VisualOutputBinding("keypoints", TensorElementType.Float32, new TensorShape(1,3,3,4))
            },
            Array.Empty<VisualLabel>(), decoder);
        var profiles = new VisualProfileRegistry(); profiles.Register(profile); profiles.Freeze();
        using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
        using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.PoseEstimation), request);
        var size = new VisualSize(100,100);
        using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,100,100), new float[30000]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size,size));
        PoseEstimationResult result = pipeline.Run(input).GetValue<PoseEstimationResult>();
        if (result.Instances.Count != 2) return 2;
        if (result.Instances[0].SourceIndex != 0 || result.Instances[1].SourceIndex != 2) return 3;
        if (Math.Abs(result.Instances[0].Keypoints[0].Point.X - 20f) > .0001f) return 4;
        if (!string.Equals(result.ComputeSha256(), "5368c9887690613a6a343fde5014bf814dd59fbfe40a16ec592b7a55f8d5cba5", StringComparison.Ordinal)) return 5;
        Console.WriteLine("DEPLOYSHARP_VISUAL_POSE_CONSUMER_OK");
        return 0;
    }
}
