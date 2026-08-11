using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private static int Main()
    {
        var cases = new[]
        {
            new YoloCase("classification", "DEPLOYSHARP_YOLO_CLS_MODEL", CreateClassification()),
            new YoloCase("segmentation", "DEPLOYSHARP_YOLO_SEG_MODEL", CreateSegmentation()),
            new YoloCase("pose", "DEPLOYSHARP_YOLO_POSE_MODEL", CreatePose()),
            new YoloCase("obb", "DEPLOYSHARP_YOLO_OBB_MODEL", CreateObb())
        };
        string? imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_IMAGE");
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            Console.Error.WriteLine("DEPLOYSHARP_YOLO_IMAGE must point to an external validation image.");
            return 2;
        }

        var summaries = new List<string>(cases.Length);
        foreach (YoloCase item in cases)
        {
            string? modelPath = Environment.GetEnvironmentVariable(item.Variable);
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            {
                Console.Error.WriteLine(item.Variable + " must point to an audited ONNX artifact.");
                return 3;
            }
            summaries.Add(Run(item, Path.GetFullPath(modelPath), Path.GetFullPath(imagePath)));
        }

        Console.WriteLine(string.Join(Environment.NewLine, summaries));
        Console.WriteLine("DEPLOYSHARP_VISUAL_YOLO_MULTITASK_CONSUMER_OK");
        return 0;
    }

    private static string Run(YoloCase item, string modelPath, string imagePath)
    {
        ModelArtifact artifact = item.Profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId);
        using var backends = new BackendRegistry();
        backends.UseOnnxRuntime();
        var profiles = new VisualProfileRegistry();
        profiles.Register(item.Profile.VisualProfile);
        profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var pipeline = new VisualPipeline(
            backends,
            profiles.Select(artifact, backends, request, item.Profile.VisualProfile.Task),
            request);
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
            imagePath,
            item.Profile.VisualProfile.Input.Name,
            OpenCvYoloPreprocessing.CreateOptions(item.Profile));
        VisualInferenceResult inference = pipeline.Run(input);
        if (inference.Value == null) throw new InvalidOperationException("The " + item.Name + " pipeline returned no result.");
        return string.Format(CultureInfo.InvariantCulture, "{0}:task={1};backend={2};result={3}", item.Name, inference.Task, inference.BackendId, Describe(inference.Value));
    }

    private static string Describe(object value)
    {
        if (value is JYPPX.DeploySharp.Results.Vision.ClassificationResult classification) return "predictions=" + classification.Predictions.Count.ToString(CultureInfo.InvariantCulture);
        if (value is InstanceSegmentationResult segmentation) return "instances=" + segmentation.Instances.Count.ToString(CultureInfo.InvariantCulture);
        if (value is PoseEstimationResult pose) return "instances=" + pose.Instances.Count.ToString(CultureInfo.InvariantCulture);
        if (value is OrientedDetectionResult obb) return "detections=" + obb.Detections.Count.ToString(CultureInfo.InvariantCulture);
        return value.GetType().Name;
    }

    private static YoloMultiTaskProfile CreateClassification()
    {
        return YoloMultiTaskProfiles.CreateClassification(
            new ModelId("clean/yolov8s-cls"),
            "6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee",
            Enumerable.Range(0, 1000).Select(index => "class" + index.ToString(CultureInfo.InvariantCulture)),
            "ef141af4b837e0a1c34ff187ac40ef36af56c135",
            "8.1.6",
            new YoloClassificationProfileOptions(17, new VisualSize(224, 224), topK: 5));
    }

    private static YoloMultiTaskProfile CreateSegmentation()
    {
        return YoloMultiTaskProfiles.CreateInstanceSegmentation(
            YoloDetectionFamily.YoloV8,
            new ModelId("clean/yolov8n-seg"),
            "986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2",
            YoloLabelSets.Coco80,
            "ef141af4b837e0a1c34ff187ac40ef36af56c135",
            "8.0.119",
            new YoloPackedProfileOptions(12, 8400, new VisualSize(640, 640), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: 8400)));
    }

    private static YoloMultiTaskProfile CreatePose()
    {
        return YoloMultiTaskProfiles.CreatePose(
            YoloDetectionFamily.YoloV8,
            new ModelId("clean/yolov8s-pose"),
            "253504de521c91115afba4dcee4c77d23a7a0a87b8f8101b170d6cae4f9c302b",
            "ef141af4b837e0a1c34ff187ac40ef36af56c135",
            "8.1.6",
            new YoloPackedProfileOptions(17, 8400, new VisualSize(640, 640), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: 8400)));
    }

    private static YoloMultiTaskProfile CreateObb()
    {
        return YoloMultiTaskProfiles.CreateObb(
            YoloDetectionFamily.YoloV8,
            new ModelId("clean/yolov8s-obb"),
            "2bbf67f4cbab45e18779f9a0b602a71cd9f266cb8d34f8df5bd3e8ab4bdcb981",
            YoloLabelSets.Dota15,
            "ef141af4b837e0a1c34ff187ac40ef36af56c135",
            "8.1.6",
            new YoloPackedProfileOptions(17, 21504, new VisualSize(1024, 1024), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: 21504)));
    }

    private sealed class YoloCase
    {
        internal YoloCase(string name, string variable, YoloMultiTaskProfile profile) { Name = name; Variable = variable; Profile = profile; }
        internal string Name { get; }
        internal string Variable { get; }
        internal YoloMultiTaskProfile Profile { get; }
    }
}
