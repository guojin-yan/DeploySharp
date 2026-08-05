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
        var modelId = new ModelId("consumer/direct-obb");
        var artifact = new ModelArtifact(modelId, "onnx", Path.Combine(AppContext.BaseDirectory, "direct-obb.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var decoder = new DirectOrientedDetectionDecoder(
            new CenterSizeAngleOutputSchema("boxes", "scores", "classes"),
            new OrientedDetectionDecoderOptions(scoreThreshold: .1f, iouThreshold: .3f, maximumCandidates: 4, maximumDetections: 4));
        var profile = new VisualModelProfile(
            "consumer/direct-obb.v1", modelId, VisualTaskId.OrientedObjectDetection, "1.0", "onnx",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,100,100), VisualTensorLayout.Nchw),
            new[]
            {
                new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,4,5)),
                new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,4)),
                new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,4))
            }, new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile);
        profiles.Freeze();
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.OrientedObjectDetection), request);
        var size = new VisualSize(100,100);
        using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,100,100), new float[30000]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size,size));
        OrientedDetectionResult result = pipeline.Run(input).GetValue<OrientedDetectionResult>();
        if (result.Detections.Count != 2) return 2;
        if (result.Detections[0].SourceIndex != 0 || result.Detections[1].SourceIndex != 2) return 3;
        if (!result.Detections[0].HasExactRotatedRectangle || Math.Abs(result.Detections[0].AngleRadiansCounterClockwise!.Value + .4f) > .0001f) return 4;
        if (result.ComputeSha256().Length != 64) return 5;
        Console.WriteLine("DEPLOYSHARP_VISUAL_OBB_CONSUMER_OK");
        return 0;
    }
}
