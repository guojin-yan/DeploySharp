using System;
using System.IO;
using System.Linq;
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
        var detectorId = new ModelId("consumer/ocr-detector");
        var recognizerId = new ModelId("consumer/ocr-recognizer");
        var detector = new ModelArtifact(detectorId, "onnx", Path.Combine(AppContext.BaseDirectory, "text-detection.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var recognizer = new ModelArtifact(recognizerId, "onnx", Path.Combine(AppContext.BaseDirectory, "text-recognition-ctc.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var detectorDecoder = new ExplicitTextDetectionDecoder(
            new ExplicitTextDetectionSchema("polygons", "scores", 4, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise),
            new TextDetectionDecoderOptions(.1f, .3f, maximumCandidates: 3, maximumRegions: 3));
        var recognizerDecoder = new GreedyCtcDecoder(
            new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses),
            new OcrCharacterSet("consumer.latin", "1.0", "ABC"),
            new CtcDecoderOptions(blankIndex: 0));
        var detectorProfile = new VisualModelProfile(
            "consumer/ocr-detector.v1", detectorId, VisualTaskId.TextDetection, "1.0", "onnx",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,16,32), VisualTensorLayout.Nchw),
            new[]
            {
                new VisualOutputBinding("polygons", TensorElementType.Float32, new TensorShape(1,3,4,2)),
                new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3))
            }, Array.Empty<VisualLabel>(), detectorDecoder);
        var recognizerProfile = new VisualModelProfile(
            "consumer/ocr-recognizer.v1", recognizerId, VisualTaskId.TextRecognition, "1.0", "onnx",
            new VisualInputBinding("crops", TensorElementType.Float32, new TensorShape(2,3,8,16), VisualTensorLayout.Nchw, minimumBatch: 2, maximumBatch: 2),
            new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(2,6,4)) },
            Array.Empty<VisualLabel>(), recognizerDecoder);
        var profiles = new VisualProfileRegistry();
        profiles.Register(detectorProfile);
        profiles.Register(recognizerProfile);
        profiles.Freeze();
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var pipeline = new OcrPipeline(
            registry,
            profiles.Select(detector, registry, request, VisualTaskId.TextDetection), request,
            profiles.Select(recognizer, registry, request, VisualTaskId.TextRecognition), request,
            new TextCropProfile("consumer/ocr-crop.v1", 8, OcrRecognitionWidthMode.Fixed, 16, 16),
            new OcrPipelineOptions(maximumRecognitionBatch: 2));
        var preprocessing = new OpenCvPreprocessOptions(new VisualSize(32,16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
        using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(Path.Combine(AppContext.BaseDirectory, "ocr.png"), "images", preprocessing);
        OcrResult result = pipeline.Run(input);
        string[] text = result.Regions.Select(region => region.Recognition.Text).ToArray();
        if (result.Regions.Count != 2 || text[0] != "AB" || text[1] != "CA") return 2;
        if (result.Regions[0].Region.SourceIndex != 0 || result.Regions[1].Region.SourceIndex != 2) return 3;
        if (result.ComputeSha256().Length != 64) return 4;
        Console.WriteLine("DEPLOYSHARP_VISUAL_OCR_CONSUMER_OK");
        return 0;
    }
}
