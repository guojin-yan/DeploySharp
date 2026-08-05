using System;
using System.IO;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VisualOrientedDetectionResult = JYPPX.DeploySharp.Visual.OrientedDetectionResult;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvVisualIntegrationTests
    {
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);
        private static string Onnx(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", "onnx", name);
        private static string Ir(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", "ir", name);

        [TestMethod]
        public void OpenCvInputFlowsThroughOpenVinoCpuClassification()
        {
            using JsonDocument golden = LoadGolden();
            var artifact = new ModelArtifact(new ModelId("tests/opencv-classification"), "openvino-ir", Ir("classification.xml"), preferredBackend: OpenVinoBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            var profile = new VisualModelProfile(
                "tests/opencv-classification.v1", artifact.ModelId, VisualTaskId.ImageClassification, "1.0", "openvino-ir",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
                new[] { new VisualLabel(0, "one"), new VisualLabel(1, "two"), new VisualLabel(2, "three") },
                new ClassificationDecoder("scores", ClassificationScoreMode.Logits, topK: 3));
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile, OpenVinoBackendProvider.BackendId, "CPU");
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), colorOrder: VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options);
            ClassificationResult result = pipeline.Run(input).GetValue<ClassificationResult>();
            Assert.IsNotNull(result.TopPrediction);
            Assert.AreEqual(golden.RootElement.GetProperty("classification").GetProperty("topIndex").GetInt32(), result.TopPrediction!.Index);
            Assert.AreEqual(golden.RootElement.GetProperty("classification").GetProperty("label").GetString(), result.TopPrediction.Label);
        }

        [TestMethod]
        public void OpenCvInputFlowsThroughOnnxRuntimeCpuDetectionAndInverseLetterbox()
        {
            using JsonDocument golden = LoadGolden();
            var artifact = new ModelArtifact(new ModelId("tests/opencv-detection"), "onnx", Onnx("detection.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var schema = new DetectionOutputSchema("detections", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ObjectnessTimesClassScore, 2, 5, 4);
            var profile = new VisualModelProfile(
                "tests/opencv-detection.v1", artifact.ModelId, VisualTaskId.ObjectDetection, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("detections", TensorElementType.Float32, new TensorShape(1, 3, 7)) },
                new[] { new VisualLabel(0, "cat"), new VisualLabel(1, "dog") },
                new DetectionDecoder(schema, new DetectionDecoderOptions(scoreThreshold: 0.25f, iouThreshold: 0.45f)));
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var options = new OpenCvPreprocessOptions(new VisualSize(100, 100), resizeMode: OpenCvResizeMode.Letterbox, colorOrder: VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options);
            DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
            JsonElement expected = golden.RootElement.GetProperty("detection");
            Assert.AreEqual(expected.GetProperty("count").GetInt32(), result.Detections.Count);
            Assert.AreEqual(expected.GetProperty("labels")[0].GetString(), result.Detections[0].Label.Label);
            Assert.AreEqual(expected.GetProperty("labels")[1].GetString(), result.Detections[1].Label.Label);
            Assert.IsTrue(result.Detections[0].Box.X >= 0 && result.Detections[0].Box.Right <= input.SourceSize.Width);
            Assert.IsTrue(result.Detections[0].Box.Y >= 0 && result.Detections[0].Box.Bottom <= input.SourceSize.Height);
        }

        [TestMethod]
        public void OpenCvRgbPngFlowsThroughOnnxRuntimeSemanticSegmentation()
        {
            using JsonDocument golden = LoadGolden();
            var artifact = new ModelArtifact(new ModelId("tests/opencv-semantic-segmentation"), "onnx", Onnx("semantic-segmentation.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var schema = new SegmentationOutputSchema("logits", SegmentationOutputKind.Logits, SegmentationTensorLayout.Nchw, 3);
            var profile = new VisualModelProfile(
                "tests/opencv-semantic-segmentation.v1", artifact.ModelId, VisualTaskId.SemanticSegmentation, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 3), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(1, 3, 2, 3)) },
                new[] { new VisualLabel(0, "red"), new VisualLabel(1, "green"), new VisualLabel(2, "blue") },
                new SemanticSegmentationDecoder(schema));
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var options = new OpenCvPreprocessOptions(new VisualSize(3, 2), colorOrder: VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options);
            SemanticSegmentationResult result = pipeline.Run(input).GetValue<SemanticSegmentationResult>();
            JsonElement expected = golden.RootElement.GetProperty("semanticSegmentation");
            ushort[] expectedClasses = new ushort[expected.GetProperty("classes").GetArrayLength()];
            for (int index = 0; index < expectedClasses.Length; index++) expectedClasses[index] = expected.GetProperty("classes")[index].GetUInt16();
            CollectionAssert.AreEqual(expectedClasses, result.Mask.ToArray());
            Assert.AreEqual(expected.GetProperty("sha256").GetString(), result.Mask.ComputeSha256());
        }

        [TestMethod]
        public void OpenCvRgbPngFlowsThroughOnnxRuntimeDirectPoseAndInverseResize()
        {
            using JsonDocument golden = LoadGolden();
            var artifact = new ModelArtifact(new ModelId("tests/opencv-direct-pose"), "onnx", Onnx("direct-pose.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var topology = new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "left", 1, oksSigma: .1f),
                new PoseKeypointDefinition(1, "right", 0, oksSigma: .1f),
                new PoseKeypointDefinition(2, "center", oksSigma: .1f)
            }, new[] { new PoseSkeletonEdge(0,2), new PoseSkeletonEdge(1,2) });
            var schema = new DirectPoseOutputSchema("keypoints", 3, 4, visibilityComponentIndex: 3, boxesOutputName: "boxes", instanceScoresOutputName: "scores");
            var decoder = new DirectPoseDecoder(schema, topology, new PoseDecoderOptions(instanceScoreThreshold: .1f, maximumCandidates: 3, maximumInstances: 3, oks: new PoseOksOptions(.8f)));
            var profile = new VisualModelProfile(
                "tests/opencv-direct-pose.v1", artifact.ModelId, VisualTaskId.PoseEstimation, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,100,100), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("keypoints", TensorElementType.Float32, new TensorShape(1,3,3,4))
                },
                Array.Empty<VisualLabel>(), decoder);
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var options = new OpenCvPreprocessOptions(new VisualSize(100,100), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options);
            PoseEstimationResult result = pipeline.Run(input).GetValue<PoseEstimationResult>();
            JsonElement expected = golden.RootElement.GetProperty("pose");
            Assert.AreEqual(new VisualSize(3,2), result.SourceSize);
            Assert.AreEqual(expected.GetProperty("count").GetInt32(), result.Instances.Count);
            Assert.AreEqual(expected.GetProperty("firstKeypoint")[0].GetSingle(), result.Instances[0].Keypoints[0].Point.X, .0001f);
            Assert.AreEqual(expected.GetProperty("firstKeypoint")[1].GetSingle(), result.Instances[0].Keypoints[0].Point.Y, .0001f);
            Assert.AreEqual(expected.GetProperty("sha256").GetString(), result.ComputeSha256());
        }

        [TestMethod]
        public void OpenCvRgbPngFlowsThroughOnnxRuntimeDirectInstanceSegmentation()
        {
            var artifact = new ModelArtifact(new ModelId("tests/opencv-direct-instance-segmentation"), "onnx", Onnx("direct-instance-segmentation.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var schema = new DirectInstanceSegmentationOutputSchema(
                new InstanceSegmentationCandidateSchema("boxes", "scores", "classes"),
                "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Probabilities);
            var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, overlapMode: InstanceMaskOverlapMode.ScorePriorityOwnership, maximumCandidates: 3, maximumInstances: 3));
            var profile = new VisualModelProfile(
                "tests/opencv-direct-instance-segmentation.v1", artifact.ModelId, VisualTaskId.InstanceSegmentation, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,4,4), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("masks", TensorElementType.Float32, new TensorShape(1,3,4,4))
                }, new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var options = new OpenCvPreprocessOptions(new VisualSize(4,4), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options);
            InstanceSegmentationResult result = pipeline.Run(input).GetValue<InstanceSegmentationResult>();
            Assert.AreEqual(new VisualSize(3,2), result.SourceSize);
            Assert.AreEqual(2, result.Instances.Count);
            Assert.AreEqual(2, result.Instances[0].Mask.ForegroundPixelCount);
            Assert.AreEqual(2, result.Instances[1].Mask.ForegroundPixelCount);
            Assert.IsNotNull(result.OwnershipMap);
            Assert.AreEqual(0, result.OwnershipMap.GetOwnerIndex(1,0));
        }

        [TestMethod]
        public void OpenCvRgbPngFlowsThroughOnnxRuntimeDirectOrientedDetection()
        {
            var artifact = new ModelArtifact(new ModelId("tests/opencv-direct-obb"), "onnx", Onnx("direct-obb.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var schema = new CenterSizeAngleOutputSchema("boxes", "scores", "classes");
            var decoder = new DirectOrientedDetectionDecoder(schema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, iouThreshold: .3f, maximumCandidates: 4, maximumDetections: 4));
            var profile = new VisualModelProfile(
                "tests/opencv-direct-obb.v1", artifact.ModelId, VisualTaskId.OrientedObjectDetection, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,100,100), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,4,5)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,4)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,4))
                }, new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var options = new OpenCvPreprocessOptions(new VisualSize(100,100), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options);
            VisualOrientedDetectionResult result = pipeline.Run(input).GetValue<VisualOrientedDetectionResult>();
            Assert.AreEqual(new VisualSize(3,2), result.SourceSize);
            Assert.AreEqual(2, result.Detections.Count);
            Assert.IsFalse(result.Detections[0].HasExactRotatedRectangle);
            foreach (PointF point in result.Detections[0].Quadrilateral.Vertices)
            {
                Assert.IsTrue(point.X >= 0 && point.X <= result.SourceSize.Width);
                Assert.IsTrue(point.Y >= 0 && point.Y <= result.SourceSize.Height);
            }
        }

        private static JsonDocument LoadGolden() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden.json")));

        private static VisualPipeline CreatePipeline(BackendRegistry registry, ModelArtifact artifact, VisualModelProfile profile, BackendId backendId, string device)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            VisualProfileSelection selection = profiles.Select(artifact, registry, request, profile.Task);
            return new VisualPipeline(registry, selection, request);
        }
    }
}
