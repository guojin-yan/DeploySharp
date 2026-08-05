using System;
using System.IO;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
