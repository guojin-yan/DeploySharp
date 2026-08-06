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
        var orientationId = new ModelId("consumer/ocr-orientation");
        var detectorId = new ModelId("consumer/ocr-detector");
        var recognizerId = new ModelId("consumer/ocr-recognizer");
        var orientation = new ModelArtifact(orientationId, "onnx", Path.Combine(AppContext.BaseDirectory, "text-orientation.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var detector = new ModelArtifact(detectorId, "onnx", Path.Combine(AppContext.BaseDirectory, "text-detection.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var recognizer = new ModelArtifact(recognizerId, "onnx", Path.Combine(AppContext.BaseDirectory, "text-recognition-ctc.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
        var profiles = new VisualProfileRegistry();
        profiles.Register(OrientationProfile(orientationId));
        profiles.Register(DetectorProfile(detectorId));
        profiles.Register(RecognizerProfile(recognizerId));
        profiles.Freeze();
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var orientationPipeline = new OcrOrientationPipeline(registry, profiles.Select(orientation, registry, request, VisualTaskId.TextOrientationClassification), request);
        using var ocrPipeline = new OcrPipeline(registry, profiles.Select(detector, registry, request, VisualTaskId.TextDetection), request, profiles.Select(recognizer, registry, request, VisualTaskId.TextRecognition), request, new TextCropProfile("consumer/crop", 8, OcrRecognitionWidthMode.Fixed, 16, 16), new OcrPipelineOptions(maximumRecognitionBatch: 2));
        using var workflow = new OcrOrientationWorkflow(orientationPipeline, ocrPipeline);
        var factory = new OpenCvOcrImageInputFactory();
        var orientationOptions = new OpenCvPreprocessOptions(new VisualSize(2, 2), OpenCvResizeMode.Resize, VisualColorOrder.Gray, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
        var detectorOptions = new OpenCvPreprocessOptions(new VisualSize(32, 16), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
        using OpenCvOcrImageInput input = factory.CreateOrientationInput(OpenCvImageSource.FromFile(Path.Combine(AppContext.BaseDirectory, "ocr-orientation-180.png")), "images", orientationOptions, "images", detectorOptions);
        OcrResult result = workflow.Run(input);
        if (result.Orientation?.AcceptedOrientation != TextOrientation.Degrees180) return 2;
        if (result.Regions.Count != 2 || result.Regions[0].Recognition.Text != "AB" || result.Regions[1].Recognition.Text != "CA") return 3;
        if (result.ComputeSha256().Length != 64) return 4;
        Console.WriteLine("DEPLOYSHARP_VISUAL_OCR_ORIENTATION_CONSUMER_OK");
        return 0;
    }

    private static VisualModelProfile OrientationProfile(ModelId id) => new VisualModelProfile("consumer/orientation.v1", id, VisualTaskId.TextOrientationClassification, "1.0", "onnx", new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 1, 2, 2), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("orientation_scores", TensorElementType.Float32, new TensorShape(1, 4)) }, Array.Empty<VisualLabel>(), new OcrOrientationDecoder(new OcrOrientationSchema("orientation_scores", new TensorShape(1, 4), TensorElementType.Float32, new[] { TextOrientation.Degrees0, TextOrientation.CounterClockwise90, TextOrientation.Clockwise90, TextOrientation.Degrees180 })));
    private static VisualModelProfile DetectorProfile(ModelId id) => new VisualModelProfile("consumer/detector.v1", id, VisualTaskId.TextDetection, "1.0", "onnx", new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 16, 32), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("polygons", TensorElementType.Float32, new TensorShape(1, 3, 4, 2)), new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) }, Array.Empty<VisualLabel>(), new ExplicitTextDetectionDecoder(new ExplicitTextDetectionSchema("polygons", "scores", 4, quadrilateralCornerOrder: TextCornerOrder.TopLeftClockwise), new TextDetectionDecoderOptions(.1f, .3f, maximumCandidates: 3, maximumRegions: 3)));
    private static VisualModelProfile RecognizerProfile(ModelId id) => new VisualModelProfile("consumer/recognizer.v1", id, VisualTaskId.TextRecognition, "1.0", "onnx", new VisualInputBinding("crops", TensorElementType.Float32, new TensorShape(2, 3, 8, 16), VisualTensorLayout.Nchw, minimumBatch: 2, maximumBatch: 2), new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(2, 6, 4)) }, Array.Empty<VisualLabel>(), new GreedyCtcDecoder(new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses), new OcrCharacterSet("consumer.latin", "1.0", "ABC"), new CtcDecoderOptions(blankIndex: 0)));
}
