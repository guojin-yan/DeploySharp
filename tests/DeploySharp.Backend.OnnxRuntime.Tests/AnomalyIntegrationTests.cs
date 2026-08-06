using System;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    [TestClass]
    public sealed class AnomalyIntegrationTests
    {
        internal static readonly ModelId ModelId = new ModelId("tests/anomaly-detection");
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public async Task RealOnnxRuntimeCpuRunsSyncAndAsyncAnomalyInference()
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            ModelSelection selection = SelectOnnxRuntimePreviewArtifact();
            string format = selection.Artifact.Format ?? throw new InvalidOperationException("The validated catalog selection has no model format.");
            var artifact = new ModelArtifact(ModelId, format, OnnxRuntimeTestData.Fixture("anomaly-detection.onnx"), preferredBackend: new BackendId(selection.Artifact.CompatibleBackends[0]));
            var profiles = new VisualProfileRegistry(); profiles.Register(Profile("onnx")); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var pipeline = new AnomalyPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.AnomalyDetection), request, new SessionOptions(2));

            AnomalyDetectionResult synchronous;
            using (PreparedVisualInput input = Input()) synchronous = pipeline.Run(input);
            AnomalyDetectionResult asynchronous;
            using (PreparedVisualInput input = Input()) asynchronous = await pipeline.RunAsync(input);

            AssertResult(synchronous);
            AssertResult(asynchronous);
            Assert.AreEqual(synchronous.ComputeSha256(), asynchronous.ComputeSha256());
            TestContext?.WriteLine("ANOMALY_CANONICAL_SHA256=" + synchronous.ComputeSha256());
        }

        private static ModelSelection SelectOnnxRuntimePreviewArtifact()
        {
            var source = new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true);
            var release = new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260806.1", "0123456789abcdef");
            var entry = new ModelCatalogEntry(
                ModelId.Value, "DeploySharp anomaly contract fixture", "deploysharp-fixture", "anomaly-detection", "1.0", ModelCatalogStatus.Preview,
                "Offline adapter fixture; not an algorithm-verified anomaly model.", source, release,
                new[] { new ModelCatalogArtifact("anomaly.onnx", "onnx", new[] { OnnxRuntimeBackendProvider.BackendId.Value, "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
            ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                "1.0", "2026-08-06T00:00:00Z", "tests-anomaly.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
            return ModelCatalogQuery.Select(catalog, new ModelQuery(task: "anomaly-detection", format: "onnx", backend: OnnxRuntimeBackendProvider.BackendId.Value, includePreview: true))[0];
        }

        internal static VisualModelProfile Profile(string format)
        {
            var schema = new AnomalyMapSchema("image_score", "anomaly_map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 2);
            var options = new AnomalyDecoderOptions(
                normalization: AnomalyNormalizationMode.FixedRange,
                threshold: .6f,
                fixedRangeMinimum: 0f,
                fixedRangeMaximum: 1f,
                channelAggregation: AnomalyChannelAggregation.Maximum,
                outputSizeMode: AnomalyOutputSizeMode.Source,
                interpolation: AnomalyMapInterpolation.BilinearHalfPixel);
            return new VisualModelProfile(
                "tests/anomaly-detection.v1", ModelId, VisualTaskId.AnomalyDetection, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,3,5), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("image_score", TensorElementType.Float32, new TensorShape(1)), new VisualOutputBinding("anomaly_map", TensorElementType.Float32, new TensorShape(1,2,3,5)) },
                Array.Empty<VisualLabel>(), new AnomalyDecoder(schema, options));
        }

        internal static PreparedVisualInput Input()
        {
            var size = new VisualSize(5,3);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,3,5), new float[45]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }

        internal static void AssertResult(AnomalyDetectionResult result)
        {
            Assert.AreEqual(.875f, result.ImageScore, .000001f);
            Assert.AreEqual(new VisualSize(5,3), result.NormalizedMap.SourceSize);
            Assert.AreEqual(5, result.NormalizedMap.Width);
            Assert.AreEqual(3, result.NormalizedMap.Height);
            Assert.AreEqual(5, result.RawMap!.Width);
            Assert.AreEqual(3, result.RawMap.Height);
            CollectionAssert.AreEqual(new byte[] { 0,0,0,0,0, 0,1,1,1,1, 1,0,0,1,1 }, result.Mask.ToArray());
            Assert.AreEqual(7d / 15d, result.AnomalousPixelRatio, .000001d);
            Assert.AreEqual("f418bc5e06bb64863b38860375335aa9fdde1c6cd706ac3776457dbf53dbf7da", result.ComputeSha256());
        }
    }
}
