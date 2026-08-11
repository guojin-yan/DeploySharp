using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CoreClassificationResult = JYPPX.DeploySharp.Results.Vision.ClassificationResult;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>Runs the locally supplied YOLO classification, segmentation, Pose, and OBB artifacts through the real image and backend path. / 使用真实图像与后端链路运行本机提供的 YOLO 分类、分割、Pose 与 OBB 工件。</summary>
    [TestClass]
    public sealed class OpenCvYoloMultiTaskIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LocalYoloMultiTaskMatrixRunsThroughOpenCvAndOnnxRuntimeCpu()
        {
            RequireExternalModels("DEPLOYSHARP_YOLO_RUN_EXTERNAL");
            RunMatrix(OnnxRuntimeBackendProvider.BackendId, "cpu");
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LocalYoloMultiTaskMatrixRunsThroughOpenCvAndOpenVinoCpu()
        {
            RequireExternalModels("DEPLOYSHARP_YOLO_RUN_EXTERNAL_OPENVINO");
            RunMatrix(OpenVinoBackendProvider.BackendId, "CPU");
        }

        private static void RunMatrix(BackendId backendId, string device)
        {
            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_MODEL_ROOT") ?? @"E:\Model\yolo";
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_IMAGE") ?? @"E:\Data\image\bus.jpg";
            if (!File.Exists(imagePath)) Assert.Inconclusive("The configured integration image does not exist: " + imagePath);

            IReadOnlyList<ExternalYoloMultiTaskCase> cases = Cases(modelRoot);
            foreach (ExternalYoloMultiTaskCase item in cases)
            {
                if (!File.Exists(item.ModelPath)) Assert.Inconclusive("The multi-task model matrix is incomplete: " + item.ModelPath);
            }

            var summaries = new List<string>(cases.Count);
            foreach (ExternalYoloMultiTaskCase item in cases)
            {
                YoloMultiTaskProfile profile = CreateProfile(item);
                ModelArtifact artifact = profile.CreateArtifact(item.ModelPath, backendId);
                using var registry = new BackendRegistry();
                if (backendId == OnnxRuntimeBackendProvider.BackendId) registry.UseOnnxRuntime();
                else registry.UseOpenVino();
                var profiles = new VisualProfileRegistry();
                profiles.Register(profile.VisualProfile);
                profiles.Freeze();
                var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
                using var pipeline = new VisualPipeline(
                    registry,
                    profiles.Select(artifact, registry, request, profile.VisualProfile.Task),
                    request);
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
                    imagePath,
                    profile.VisualProfile.Input.Name,
                    OpenCvYoloPreprocessing.CreateOptions(profile));

                VisualInferenceResult inference = pipeline.Run(input);
                Assert.AreEqual(profile.VisualProfile.Task, inference.Task);
                Assert.AreEqual(backendId, inference.BackendId);
                Assert.IsNotNull(inference.Value);
                summaries.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:{1};backend={2};inferenceMs={3:F2};decodeMs={4:F2}",
                    item.Name,
                    DescribeResult(inference.Value),
                    inference.BackendId,
                    inference.Timing.Inference.TotalMilliseconds,
                    inference.Timing.Postprocessing.TotalMilliseconds));
            }

            Assert.AreEqual(12, summaries.Count);
            Console.WriteLine(string.Join(Environment.NewLine, summaries));
        }

        private static string DescribeResult(object value)
        {
            if (value is CoreClassificationResult classification) return "classification=" + classification.Predictions.Count.ToString(CultureInfo.InvariantCulture);
            if (value is InstanceSegmentationResult segmentation) return "segmentation=" + segmentation.Instances.Count.ToString(CultureInfo.InvariantCulture);
            if (value is PoseEstimationResult pose) return "pose=" + pose.Instances.Count.ToString(CultureInfo.InvariantCulture);
            if (value is OrientedDetectionResult obb) return "obb=" + obb.Detections.Count.ToString(CultureInfo.InvariantCulture);
            return value.GetType().FullName ?? value.GetType().Name;
        }

        private static YoloMultiTaskProfile CreateProfile(ExternalYoloMultiTaskCase item)
        {
            var decoderOptions = new YoloPackedDecoderOptions(
                scoreThreshold: 0.25f,
                iouThreshold: 0.45f,
                maximumCandidates: item.CandidateCount,
                maximumDetections: 100,
                maximumWorkspaceBytes: 512L * 1024 * 1024);
            if (item.Kind == MultiTaskKind.Classification)
            {
                return YoloMultiTaskProfiles.CreateClassification(
                    item.ModelId,
                    item.Sha256,
                    Enumerable.Range(0, 1000).Select(index => "class" + index.ToString(CultureInfo.InvariantCulture)),
                    item.UpstreamCommit,
                    item.ExporterVersion,
                    new YoloClassificationProfileOptions(item.Opset, item.ModelSize, topK: 5));
            }

            var options = new YoloPackedProfileOptions(
                item.Opset,
                item.CandidateCount,
                item.ModelSize,
                decoderOptions: decoderOptions,
                profileId: "external.yolo." + item.Name.ToLowerInvariant());
            if (item.Kind == MultiTaskKind.Segmentation)
            {
                return YoloMultiTaskProfiles.CreateInstanceSegmentation(item.Family, item.ModelId, item.Sha256, YoloLabelSets.Coco80, item.UpstreamCommit, item.ExporterVersion, options);
            }
            if (item.Kind == MultiTaskKind.Pose)
            {
                return YoloMultiTaskProfiles.CreatePose(item.Family, item.ModelId, item.Sha256, item.UpstreamCommit, item.ExporterVersion, options);
            }
            return YoloMultiTaskProfiles.CreateObb(item.Family, item.ModelId, item.Sha256, YoloLabelSets.Dota15, item.UpstreamCommit, item.ExporterVersion, options);
        }

        private static void RequireExternalModels(string variable)
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(variable), "1", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Set " + variable + "=1 to run the external YOLO multi-task matrix.");
            }
        }

        private static IReadOnlyList<ExternalYoloMultiTaskCase> Cases(string root)
        {
            const string UltralyticsV816 = "ef141af4b837e0a1c34ff187ac40ef36af56c135";
            const string UltralyticsV8324 = "636685ace98527cd0113656fd024a82291fa3122";
            const string UltralyticsV840 = "6f6158be448c73471c000cf41db5cd9169300ed9";
            const string YoloV9 = "5b1ea9a8b3f0ffe4fe0e203ec6232d788bb3fcff";
            return new[]
            {
                new ExternalYoloMultiTaskCase("YOLOCls", MultiTaskKind.Classification, YoloDetectionFamily.YoloV8, Path.Combine(root, "yolov8", "yolov8s-cls.onnx"), "6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee", UltralyticsV816, "8.1.6", 17, new VisualSize(224, 224), 1000),
                new ExternalYoloMultiTaskCase("YOLOv5Seg", MultiTaskKind.Segmentation, YoloDetectionFamily.YoloV5, Path.Combine(root, "yolov5", "yolov5s-seg.onnx"), "ab44adf19119521f4764966a48f76fbac9125d22f5db776589bf049b49267576", "20d1d78a08277e365d57bfa3a2cce752772d9e59", "local-pytorch2.1.2-export", 17, new VisualSize(640, 640), 25200),
                new ExternalYoloMultiTaskCase("YOLOv8Seg", MultiTaskKind.Segmentation, YoloDetectionFamily.YoloV8, Path.Combine(root, "yolov8", "yolov8n-seg.onnx"), "986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2", UltralyticsV816, "8.0.119", 12, new VisualSize(640, 640), 8400),
                new ExternalYoloMultiTaskCase("YOLOv9Seg", MultiTaskKind.Segmentation, YoloDetectionFamily.YoloV9, Path.Combine(root, "yolov9-c-seg.onnx"), "2cc4ea632009115d72f30841d7295d5ca064cc9697a2fb4efbea3ce41ac0a2a0", YoloV9, "local-pytorch2.2.1-export", 12, new VisualSize(640, 640), 8400),
                new ExternalYoloMultiTaskCase("YOLOv11Seg", MultiTaskKind.Segmentation, YoloDetectionFamily.YoloV11, Path.Combine(root, "yolov11", "yolo11s-seg.onnx"), "0707f946915fcdfdbc5438d1f45ca446e70d388805e422ac849996240880fe48", UltralyticsV8324, "8.3.24", 19, new VisualSize(640, 640), 8400),
                new ExternalYoloMultiTaskCase("YOLOv26Seg", MultiTaskKind.Segmentation, YoloDetectionFamily.YoloV26, Path.Combine(root, "yolov26", "yolo26s-seg.onnx"), "79682f271d30833adfe97c97572cd85d348eb1636be8d5b13009ae48e51dbd6f", UltralyticsV840, "8.4.0-end2end", 19, new VisualSize(640, 640), 300),
                new ExternalYoloMultiTaskCase("YOLOv8Pose", MultiTaskKind.Pose, YoloDetectionFamily.YoloV8, Path.Combine(root, "yolov8", "yolov8s-pose.onnx"), "253504de521c91115afba4dcee4c77d23a7a0a87b8f8101b170d6cae4f9c302b", UltralyticsV816, "8.1.6", 17, new VisualSize(640, 640), 8400),
                new ExternalYoloMultiTaskCase("YOLOv11Pose", MultiTaskKind.Pose, YoloDetectionFamily.YoloV11, Path.Combine(root, "yolov11", "yolo11s-pose.onnx"), "5b8d5bce3dff5ac176ea922faf14705fa46fa3b0d3a4b7974b765c355806bae5", UltralyticsV8324, "8.3.24", 19, new VisualSize(640, 640), 8400),
                new ExternalYoloMultiTaskCase("YOLOv26Pose", MultiTaskKind.Pose, YoloDetectionFamily.YoloV26, Path.Combine(root, "yolov26", "yolo26s-pose.onnx"), "55c609d18dc635b54a91c8f038d29138a421a4f8e700f645c78779fe6080ddcc", UltralyticsV840, "8.4.0-end2end", 19, new VisualSize(640, 640), 300),
                new ExternalYoloMultiTaskCase("YOLOv8Obb", MultiTaskKind.Obb, YoloDetectionFamily.YoloV8, Path.Combine(root, "yolov8", "yolov8s-obb.onnx"), "2bbf67f4cbab45e18779f9a0b602a71cd9f266cb8d34f8df5bd3e8ab4bdcb981", UltralyticsV816, "8.1.6", 17, new VisualSize(1024, 1024), 21504),
                new ExternalYoloMultiTaskCase("YOLOv11Obb", MultiTaskKind.Obb, YoloDetectionFamily.YoloV11, Path.Combine(root, "yolov11", "yolo11s-obb.onnx"), "50ae0e11b742007fcd297408382be94a25c884093d63dce00ead62f37ea2cad0", UltralyticsV8324, "8.3.24", 19, new VisualSize(1024, 1024), 21504),
                new ExternalYoloMultiTaskCase("YOLOv26Obb", MultiTaskKind.Obb, YoloDetectionFamily.YoloV26, Path.Combine(root, "yolov26", "yolo26s-obb.onnx"), "bbc7c924dcac9e94888ef706f7aa5648cbc38f5fbd4c8a360401ebee7be955df", UltralyticsV840, "8.4.0-end2end", 19, new VisualSize(1024, 1024), 300)
            };
        }

        private enum MultiTaskKind { Classification, Segmentation, Pose, Obb }

        private sealed class ExternalYoloMultiTaskCase
        {
            internal ExternalYoloMultiTaskCase(string name, MultiTaskKind kind, YoloDetectionFamily family, string modelPath, string sha256, string upstreamCommit, string exporterVersion, int opset, VisualSize modelSize, int candidateCount)
            {
                Name = name; Kind = kind; Family = family; ModelPath = modelPath; Sha256 = sha256; UpstreamCommit = upstreamCommit; ExporterVersion = exporterVersion; Opset = opset; ModelSize = modelSize; CandidateCount = candidateCount;
                ModelId = new ModelId("external/" + name.ToLowerInvariant());
            }

            internal string Name { get; }
            internal MultiTaskKind Kind { get; }
            internal YoloDetectionFamily Family { get; }
            internal string ModelPath { get; }
            internal string Sha256 { get; }
            internal string UpstreamCommit { get; }
            internal string ExporterVersion { get; }
            internal int Opset { get; }
            internal VisualSize ModelSize { get; }
            internal int CandidateCount { get; }
            internal ModelId ModelId { get; }
        }
    }
}
