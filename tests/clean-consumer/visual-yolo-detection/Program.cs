using System;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private const string ModelSha256 = "50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4";

    private static int Main()
    {
        string? modelPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_MODEL");
        string? imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_IMAGE");
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            Console.Error.WriteLine("DEPLOYSHARP_YOLO_MODEL must point to the audited YOLOv8n ONNX artifact.");
            return 2;
        }
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            Console.Error.WriteLine("DEPLOYSHARP_YOLO_IMAGE must point to an external validation image.");
            return 3;
        }

        YoloDetectionProfile profile = YoloDetectionProfiles.Create(
            YoloDetectionFamily.YoloV8,
            new ModelId("clean/yolov8n-detect"),
            ModelSha256,
            YoloLabelSets.Coco80,
            "1367566337fb8056223a1aeb469360747f1b1bcd",
            "8.3.78",
            new YoloDetectionProfileOptions(19));
        ModelArtifact artifact = profile.CreateArtifact(Path.GetFullPath(modelPath), OnnxRuntimeBackendProvider.BackendId);

        using var backends = new BackendRegistry();
        backends.UseOnnxRuntime();
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var pipeline = new VisualPipeline(backends, profiles.Select(artifact, backends, request, VisualTaskId.ObjectDetection), request);
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            Path.GetFullPath(imagePath),
            profile.VisualProfile.Input.Name,
            OpenCvYoloPreprocessing.CreateOptions(profile));
        DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
        if (result.Detections.Count == 0) return 4;

        Console.WriteLine("DEPLOYSHARP_VISUAL_YOLO_DETECTION_CONSUMER_OK count=" + result.Detections.Count + ";top=" + result.Detections[0].Label.Label);
        return 0;
    }
}
