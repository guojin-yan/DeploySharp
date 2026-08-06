using System;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests
{
    [TestClass]
    public sealed class AnomalyIntegrationTests
    {
        private static readonly ModelId ModelId = new ModelId("tests/anomaly-detection");
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public void RealOpenVinoCpuOnnxAndIrProduceTheSameAnomalyResult()
        {
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            AnomalyDetectionResult onnx = Run(registry, new ModelArtifact(ModelId, "onnx", OpenVinoTestData.Onnx("anomaly-detection.onnx"), preferredBackend: OpenVinoBackendProvider.BackendId), "onnx");
            AnomalyDetectionResult ir = Run(registry, new ModelArtifact(ModelId, "openvino-ir", OpenVinoTestData.Ir("anomaly-detection.xml"), preferredBackend: OpenVinoBackendProvider.BackendId), "openvino-ir");

            AssertResult(onnx);
            AssertResult(ir);
            Assert.AreEqual(onnx.ComputeSha256(), ir.ComputeSha256());
            TestContext?.WriteLine("ANOMALY_OPENVINO_CANONICAL_SHA256=" + onnx.ComputeSha256());
        }

        private static AnomalyDetectionResult Run(BackendRegistry registry, ModelArtifact artifact, string format)
        {
            var profiles = new VisualProfileRegistry(); profiles.Register(Profile(format)); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            using var pipeline = new AnomalyPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.AnomalyDetection), request);
            using PreparedVisualInput input = Input();
            return pipeline.Run(input);
        }

        private static VisualModelProfile Profile(string format)
        {
            var decoder = new AnomalyDecoder(
                new AnomalyMapSchema("image_score", "anomaly_map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 2),
                new AnomalyDecoderOptions(normalization: AnomalyNormalizationMode.FixedRange, threshold: .6f, channelAggregation: AnomalyChannelAggregation.Maximum));
            return new VisualModelProfile(
                "tests/anomaly-detection.v1", ModelId, VisualTaskId.AnomalyDetection, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,3,5), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("image_score", TensorElementType.Float32, new TensorShape(1)), new VisualOutputBinding("anomaly_map", TensorElementType.Float32, new TensorShape(1,2,3,5)) },
                Array.Empty<VisualLabel>(), decoder);
        }

        private static PreparedVisualInput Input()
        {
            var size = new VisualSize(5,3);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,3,5), new float[45]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }

        private static void AssertResult(AnomalyDetectionResult result)
        {
            Assert.AreEqual(.875f, result.ImageScore, .000001f);
            CollectionAssert.AreEqual(new byte[] { 0,0,0,0,0, 0,1,1,1,1, 1,0,0,1,1 }, result.Mask.ToArray());
            Assert.AreEqual(7d / 15d, result.AnomalousPixelRatio, .000001d);
            Assert.AreEqual("f418bc5e06bb64863b38860375335aa9fdde1c6cd706ac3776457dbf53dbf7da", result.ComputeSha256());
        }
    }
}
