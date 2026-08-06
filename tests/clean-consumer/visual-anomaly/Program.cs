using System;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private static int Main()
    {
        var modelId = new ModelId("consumer/anomaly-detection");
        var artifact = new ModelArtifact(
            modelId,
            "onnx",
            Path.Combine(AppContext.BaseDirectory, "anomaly-detection.onnx"),
            preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var decoder = new AnomalyDecoder(
            new AnomalyMapSchema("image_score", "anomaly_map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 2),
            new AnomalyDecoderOptions(
                normalization: AnomalyNormalizationMode.FixedRange,
                threshold: .6f,
                channelAggregation: AnomalyChannelAggregation.Maximum));
        var profile = new VisualModelProfile(
            "consumer/anomaly-detection.v1",
            modelId,
            VisualTaskId.AnomalyDetection,
            "1.0",
            "onnx",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,3,5), VisualTensorLayout.Nchw),
            new[]
            {
                new VisualOutputBinding("image_score", TensorElementType.Float32, new TensorShape(1)),
                new VisualOutputBinding("anomaly_map", TensorElementType.Float32, new TensorShape(1,2,3,5))
            },
            Array.Empty<VisualLabel>(),
            decoder);
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile);
        profiles.Freeze();
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var pipeline = new AnomalyPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.AnomalyDetection), request);
        var preprocessing = new OpenCvPreprocessOptions(
            new VisualSize(5,3),
            OpenCvResizeMode.Resize,
            VisualColorOrder.Rgb,
            standardDeviations: new[] { 255f,255f,255f },
            layout: VisualTensorLayout.Nchw,
            outputType: OpenCvOutputType.Float32);
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            Path.Combine(AppContext.BaseDirectory, "anomaly.png"),
            "images",
            preprocessing);
        AnomalyDetectionResult result = pipeline.Run(input);
        if (Math.Abs(result.ImageScore - .875f) > .000001f) return 2;
        if (result.Mask.ToArray().Length != 15 || result.AnomalousPixelRatio <= 0d) return 3;
        if (result.ComputeSha256() != "f418bc5e06bb64863b38860375335aa9fdde1c6cd706ac3776457dbf53dbf7da") return 4;
        Console.WriteLine("DEPLOYSHARP_VISUAL_ANOMALY_CONSUMER_OK");
        return 0;
    }
}
