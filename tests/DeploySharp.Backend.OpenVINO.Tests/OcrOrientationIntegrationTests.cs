using System;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests
{
    [TestClass]
    public sealed class OcrOrientationIntegrationTests
    {
        private static string Onnx => Path.Combine(AppContext.BaseDirectory, "fixtures", "onnx", "text-orientation.onnx");
        private static string Ir => Path.Combine(AppContext.BaseDirectory, "fixtures", "ir", "text-orientation.xml");

        [TestMethod]
        public void RealCpuOpenVinoOnnxAndIrShareOrientationContract()
        {
            using var registry = new BackendRegistry(); registry.UseOpenVino();
            var profiles = new VisualProfileRegistry(); profiles.Register(Profile("onnx")); profiles.Register(Profile("openvino-ir")); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            foreach (string format in new[] { "onnx", "openvino-ir" })
            {
                string model = format == "onnx" ? Onnx : Ir;
                var artifact = new ModelArtifact(new ModelId("tests/text-orientation"), format, model, preferredBackend: OpenVinoBackendProvider.BackendId);
                using var pipeline = new OcrOrientationPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.TextOrientationClassification), request);
                using PreparedVisualInput input = Input(new[] { 3f, 2f, 1f, 9f });
                OcrOrientationResult result = pipeline.Run(input);
                Assert.AreEqual(TextOrientation.Degrees180, result.AcceptedOrientation);
                Assert.AreEqual(OpenVinoBackendProvider.BackendId, result.BackendId);
            }
        }

        private static VisualModelProfile Profile(string format)
        {
            var decoder = new OcrOrientationDecoder(new OcrOrientationSchema("orientation_scores", new TensorShape(1, 4), TensorElementType.Float32, new[] { TextOrientation.Degrees0, TextOrientation.CounterClockwise90, TextOrientation.Clockwise90, TextOrientation.Degrees180 }));
            return new VisualModelProfile("tests/text-orientation." + format + ".v1", new ModelId("tests/text-orientation"), VisualTaskId.TextOrientationClassification, "1.0", format, new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 1, 2, 2), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("orientation_scores", TensorElementType.Float32, new TensorShape(1, 4)) }, Array.Empty<VisualLabel>(), decoder);
        }

        private static PreparedVisualInput Input(float[] values)
        {
            var size = new VisualSize(2, 2);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 1, 2, 2), values), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }
    }
}
