using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvYoloExternalIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LocalV1YoloDetectionMatrixRunsThroughOpenCvAndOnnxRuntime()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_YOLO_RUN_EXTERNAL=1 to run the external YOLO model matrix.");
            }

            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_MODEL_ROOT") ?? @"E:\Model\yolo";
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_IMAGE") ?? @"E:\Data\image\bus.jpg";
            if (!File.Exists(imagePath)) Assert.Inconclusive("The external YOLO image does not exist: " + imagePath);

            IReadOnlyList<ExternalYoloCase> cases = Cases(modelRoot);
            foreach (ExternalYoloCase item in cases)
            {
                if (!File.Exists(item.ModelPath)) Assert.Inconclusive("The external YOLO model matrix is incomplete: " + item.ModelPath);
            }

            var summaries = new List<string>(cases.Count);
            string? preparedTensorSha256 = null;
            foreach (ExternalYoloCase item in cases)
            {
                YoloDetectionProfile profile = YoloDetectionProfiles.Create(
                    item.Family,
                    new ModelId("external/yolo-" + ((int)item.Family).ToString(CultureInfo.InvariantCulture) + "-detect"),
                    item.Sha256,
                    YoloLabelSets.Coco80,
                    item.UpstreamCommit,
                    item.ExporterVersion,
                    new YoloDetectionProfileOptions(item.Opset));
                var artifact = profile.CreateArtifact(item.ModelPath, OnnxRuntimeBackendProvider.BackendId);
                using var registry = new BackendRegistry();
                registry.UseOnnxRuntime();
                var profiles = new VisualProfileRegistry();
                profiles.Register(profile.VisualProfile);
                profiles.Freeze();
                var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
                using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.ObjectDetection), request);
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
                    imagePath,
                    profile.VisualProfile.Input.Name,
                    OpenCvYoloPreprocessing.CreateOptions(profile));
                string currentPreparedSha256 = ComputeFloatTensorSha256(input.Tensor);
                if (preparedTensorSha256 == null) preparedTensorSha256 = currentPreparedSha256;
                else Assert.AreEqual(preparedTensorSha256, currentPreparedSha256, "All 640x640 YOLO profiles must produce the same prepared input tensor.");

                DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
                Assert.IsTrue(result.Detections.Count > 0, item.Family + " did not produce a detection for the configured integration image.");
                Detection first = result.Detections[0];
                Assert.IsTrue(first.Box.X >= 0f && first.Box.Y >= 0f, item.Family + " produced a negative source coordinate.");
                Assert.IsTrue(first.Box.Right <= input.SourceSize.Width + 0.001f && first.Box.Bottom <= input.SourceSize.Height + 0.001f, item.Family + " produced an unclipped source coordinate.");
                summaries.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:count={1};top={2};score={3:F6};box={4:F3},{5:F3},{6:F3},{7:F3}",
                    item.Family,
                    result.Detections.Count,
                    first.Label.Label,
                    first.Label.Score,
                    first.Box.X,
                    first.Box.Y,
                    first.Box.Width,
                    first.Box.Height));
            }

            Assert.AreEqual(10, summaries.Count);
            Assert.AreEqual("48af3a194d046f683585c8c8deffa953d415122ec0f2398bd27d8a67f34978df", preparedTensorSha256);
            Console.WriteLine("preparedTensorSha256=" + preparedTensorSha256);
            Console.WriteLine(string.Join(Environment.NewLine, summaries));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LocalV1YoloDetectionMatrixRunsThroughOpenCvAndOpenVinoCpu()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_YOLO_RUN_EXTERNAL=1 to run the external YOLO model matrix.");
            }

            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_MODEL_ROOT") ?? @"E:\Model\yolo";
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_IMAGE") ?? @"E:\Data\image\bus.jpg";
            if (!File.Exists(imagePath)) Assert.Inconclusive("The external YOLO image does not exist: " + imagePath);

            IReadOnlyList<ExternalYoloCase> cases = Cases(modelRoot);
            foreach (ExternalYoloCase item in cases)
            {
                if (!File.Exists(item.ModelPath)) Assert.Inconclusive("The external YOLO model matrix is incomplete: " + item.ModelPath);
            }

            var summaries = new List<string>(cases.Count);
            foreach (ExternalYoloCase item in cases)
            {
                YoloDetectionProfile profile = YoloDetectionProfiles.Create(
                    item.Family,
                    new ModelId("external/openvino-yolo-" + ((int)item.Family).ToString(CultureInfo.InvariantCulture) + "-detect"),
                    item.Sha256,
                    YoloLabelSets.Coco80,
                    item.UpstreamCommit,
                    item.ExporterVersion,
                    new YoloDetectionProfileOptions(item.Opset));
                var artifact = profile.CreateArtifact(item.ModelPath, OpenVinoBackendProvider.BackendId);
                using var registry = new BackendRegistry();
                registry.UseOpenVino();
                var profiles = new VisualProfileRegistry();
                profiles.Register(profile.VisualProfile);
                profiles.Freeze();
                var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
                using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.ObjectDetection), request);
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
                    imagePath,
                    profile.VisualProfile.Input.Name,
                    OpenCvYoloPreprocessing.CreateOptions(profile));

                DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
                Assert.IsTrue(result.Detections.Count > 0, item.Family + " did not produce a detection for the configured integration image.");
                Detection first = result.Detections[0];
                Assert.IsTrue(first.Box.X >= 0f && first.Box.Y >= 0f, item.Family + " produced a negative source coordinate.");
                Assert.IsTrue(first.Box.Right <= input.SourceSize.Width + 0.001f && first.Box.Bottom <= input.SourceSize.Height + 0.001f, item.Family + " produced an unclipped source coordinate.");
                summaries.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:count={1};top={2};score={3:F6};box={4:F3},{5:F3},{6:F3},{7:F3}",
                    item.Family,
                    result.Detections.Count,
                    first.Label.Label,
                    first.Label.Score,
                    first.Box.X,
                    first.Box.Y,
                    first.Box.Width,
                    first.Box.Height));
            }

            Assert.AreEqual(10, summaries.Count);
            Console.WriteLine(string.Join(Environment.NewLine, summaries));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void ReproducibleYoloV8OpenVinoIrRunsThroughOpenCvAndOpenVinoCpu()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_YOLO_RUN_EXTERNAL=1 to run the external YOLO IR integration test.");
            }

            string? modelPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_IR_MODEL");
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_IMAGE") ?? @"E:\Data\image\bus.jpg";
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            {
                Assert.Inconclusive("Run eng/models/yolo/Convert-YoloOnnxToOpenVinoIr.ps1 and set DEPLOYSHARP_YOLO_IR_MODEL to the generated XML file.");
            }
            if (!File.Exists(imagePath)) Assert.Inconclusive("The external YOLO image does not exist: " + imagePath);

            YoloDetectionProfile profile = YoloDetectionProfiles.Create(
                YoloDetectionFamily.YoloV8,
                new ModelId("external/openvino-ir-yolo-v8-detect"),
                "065b06a5d8c60ab18bf0ccd0baa285e21f31c9e517042b79cd5d78971b1551a1",
                YoloLabelSets.Coco80,
                "1367566337fb8056223a1aeb469360747f1b1bcd",
                "OpenVINO OVC 2025.4.0 from Ultralytics 8.3.78 ONNX",
                new YoloDetectionProfileOptions(19, modelFormat: "openvino-ir"));
            ModelArtifact artifact = profile.CreateArtifact(modelPath!, OpenVinoBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.ObjectDetection), request);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
                imagePath,
                profile.VisualProfile.Input.Name,
                OpenCvYoloPreprocessing.CreateOptions(profile));

            DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
            Assert.AreEqual("openvino-ir", profile.VisualProfile.ModelFormat);
            Assert.AreEqual(5, result.Detections.Count);
            Assert.AreEqual("person", result.Detections[0].Label.Label);
            Assert.AreEqual(0.900904f, result.Detections[0].Label.Score, 0.00001f);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "YOLOv8 IR:count={0};top={1};score={2:F6}", result.Detections.Count, result.Detections[0].Label.Label, result.Detections[0].Label.Score));
        }

        private static IReadOnlyList<ExternalYoloCase> Cases(string root)
        {
            return new[]
            {
                new ExternalYoloCase(YoloDetectionFamily.YoloV5, Path.Combine(root, "yolov5", "yolov5n.onnx"), "1cad0ece41bc351e2e1a3bd9b244dc4219f1b7b4d322928f13b6e7d19a00ef9d", "20d1d78a08277e365d57bfa3a2cce752772d9e59", "local-onnx-export", 12),
                new ExternalYoloCase(YoloDetectionFamily.YoloV6, Path.Combine(root, "yolov6s.onnx"), "f6fddae83fb23ff02578d5b5e9f4eb9d68b5d8e7f469bb80edf4041681c757f6", "e86a483f3f6bded25d45970b56831345a99744a4", "local-onnx-export", 12),
                new ExternalYoloCase(YoloDetectionFamily.YoloV7, Path.Combine(root, "yolov7.onnx"), "8ee07ed4aa95070ae1c9e7a37c2407c2aa065e989f887cb1193bcb117603c641", "a207844b1ce82d204ab36d87d496728d3d2348e7", "local-onnx-export", 12),
                new ExternalYoloCase(YoloDetectionFamily.YoloV8, Path.Combine(root, "yolov8", "yolov8n.onnx"), "50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4", "1367566337fb8056223a1aeb469360747f1b1bcd", "8.3.78", 19),
                new ExternalYoloCase(YoloDetectionFamily.YoloV9, Path.Combine(root, "yolov9s.onnx"), "e985aab9f5031b5e34e1846b1ed9535de23e77b792c70680010979eb5d98f6c7", "5b1ea9a8b3f0ffe4fe0e203ec6232d788bb3fcff", "8.3.78", 19),
                new ExternalYoloCase(YoloDetectionFamily.YoloV10, Path.Combine(root, "yolov10", "yolov10n.onnx"), "908f513fda6e38eeb4230d53d1fcea1d7e068b8cec4b7bbd4e818f704320ca81", "453c6e38a51e9d1d5a2aa5fb7f1014a711913397", "8.3.78", 19),
                new ExternalYoloCase(YoloDetectionFamily.YoloV11, Path.Combine(root, "yolov11", "yolo11n.onnx"), "7060132736a0e5856a8b91d68fd7558ac6daf8c5fb7cec46dbc9cb034f8409c3", "1367566337fb8056223a1aeb469360747f1b1bcd", "8.3.78", 19),
                new ExternalYoloCase(YoloDetectionFamily.YoloV12, Path.Combine(root, "yolov12", "yolo12n.onnx"), "9a99a764c60423ffaef870bf22687c66da284c6b2ad7f249605ced9c8a2a3e80", "01a22c0603e0eaa6d9bd62120a391e744d92cea2", "8.3.78", 19),
                new ExternalYoloCase(YoloDetectionFamily.YoloV13, Path.Combine(root, "yolov13n.onnx"), "a589a4e351e9f9be6712ba4d6831cfbcc16b7ac58d6498c02a8386eca828cf80", "73289949533efac82bb5f72ec19b746618656bd2", "8.3.63", 17),
                new ExternalYoloCase(YoloDetectionFamily.YoloV26, Path.Combine(root, "yolov26", "yolo26n.onnx"), "bd169d41c0c04abe18bc1ea6220ff295cf77a38c165071b1acc76ee6ef0c10c4", "1367566337fb8056223a1aeb469360747f1b1bcd", "8.4.0", 19)
            };
        }

        private static string ComputeFloatTensorSha256(ITensor tensor)
        {
            float[] values = tensor.Buffer as float[] ?? throw new AssertFailedException("The YOLO prepared tensor must be Float32.");
            var bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using SHA256 algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class ExternalYoloCase
        {
            internal ExternalYoloCase(YoloDetectionFamily family, string modelPath, string sha256, string upstreamCommit, string exporterVersion, int opset)
            {
                Family = family;
                ModelPath = modelPath;
                Sha256 = sha256;
                UpstreamCommit = upstreamCommit;
                ExporterVersion = exporterVersion;
                Opset = opset;
            }

            internal YoloDetectionFamily Family { get; }
            internal string ModelPath { get; }
            internal string Sha256 { get; }
            internal string UpstreamCommit { get; }
            internal string ExporterVersion { get; }
            internal int Opset { get; }
        }
    }
}
