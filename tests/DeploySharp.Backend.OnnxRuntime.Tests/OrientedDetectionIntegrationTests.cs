using System;
using System.IO;
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
    public sealed class OrientedDetectionIntegrationTests
    {
        [TestMethod]
        public void RealOnnxRuntimeCpuExecutesDirectAndCornerObbContracts()
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();

            ModelArtifact directArtifact = OnnxRuntimeTestData.Artifact("direct-obb.onnx");
            using (VisualPipeline pipeline = CreatePipeline(registry, directArtifact, DirectProfile(directArtifact.ModelId, "onnx")))
            using (PreparedVisualInput input = Input())
            {
                OrientedDetectionResult first = pipeline.Run(input).GetValue<OrientedDetectionResult>();
                OrientedDetectionResult second = pipeline.Run(input).GetValue<OrientedDetectionResult>();
                Assert.AreEqual(2, first.Detections.Count);
                Assert.AreEqual(0, first.Detections[0].SourceIndex);
                Assert.AreEqual(2, first.Detections[1].SourceIndex);
                Assert.AreEqual(first.ComputeSha256(), second.ComputeSha256());
                Assert.IsTrue(first.Detections[0].HasExactRotatedRectangle);
                Assert.AreEqual(-0.4f, first.Detections[0].AngleRadiansCounterClockwise!.Value, .0001f);
            }

            ModelArtifact cornerArtifact = OnnxRuntimeTestData.Artifact("corner-obb.onnx");
            using (VisualPipeline pipeline = CreatePipeline(registry, cornerArtifact, CornerProfile(cornerArtifact.ModelId)))
            using (PreparedVisualInput input = Input())
            {
                OrientedDetectionResult result = pipeline.Run(input).GetValue<OrientedDetectionResult>();
                Assert.AreEqual(3, result.Detections.Count);
                Assert.AreEqual(400f, result.Detections[0].Quadrilateral.Area, .0001f);
                Assert.IsFalse(result.Detections[0].HasExactRotatedRectangle);
                Assert.AreEqual(0f, result.Detections[0].AngleRadiansCounterClockwise!.Value, .0001f);
            }
        }

        [TestMethod]
        public void VerifiedOnnxModelPackAndOfflinePreviewCatalogEnterRealObbSelection()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-obb-pack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string modelPath = Path.Combine(root, "direct-obb.onnx");
                File.Copy(OnnxRuntimeTestData.Fixture("direct-obb.onnx"), modelPath);
                string hash = OnnxRuntimeTestData.Sha256(modelPath);
                long size = new FileInfo(modelPath).Length;
                var modelId = new ModelId("tests/oriented-detection-supply-chain");
                var artifactDocument = new ModelArtifactDocument(
                    "onnx.cpu", "onnx", ModelArtifactLocationKind.File, Path.GetFileName(modelPath), new[] { "onnxruntime", "openvino" },
                    new[] { new ModelFileDocument(Path.GetFileName(modelPath), hash, size, "application/onnx", ModelFileRole.Model) },
                    precision: "fp32", portable: true);
                var packageDocument = new ModelPackageDocument(
                    "2.0", modelId.Value, "DeploySharp OBB contract fixture", "deploysharp-fixture", "oriented-object-detection", "1.0",
                    new ModelExporterDocument("ONNX", "1.22.0", "eng/test-models/Generate-OnnxRuntimeFixtures.py"),
                    new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-05T00:00:00Z"), "tests/oriented-detection.v1",
                    new[] { new ModelTensorSignatureDocument("images", "float32", new long[] { 1,3,100,100 }) },
                    new[]
                    {
                        new ModelTensorSignatureDocument("boxes", "float32", new long[] { 1,4,5 }),
                        new ModelTensorSignatureDocument("scores", "float32", new long[] { 1,4 }),
                        new ModelTensorSignatureDocument("classes", "float32", new long[] { 1,4 })
                    }, new[] { artifactDocument });
                string manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(packageDocument)));
                LocalModelPackage package = ModelPackageLoader.Load(manifestPath);
                Assert.AreEqual(hash, package.Artifacts[0].Files[0].Document.Sha256);
                Assert.AreEqual(size, package.Artifacts[0].Files[0].Document.Size);

                var entry = new ModelCatalogEntry(
                    modelId.Value, "DeploySharp OBB contract fixture", "deploysharp-fixture", "oriented-object-detection", "1.0", ModelCatalogStatus.Preview,
                    "Offline adapter contract fixture; not an algorithm-verified model.", packageDocument.Source,
                    new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260805.1", "0123456789abcdef"),
                    new[] { new ModelCatalogArtifact("onnx.cpu", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-05T00:00:00Z", "tests-local-only.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "oriented-object-detection", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "oriented-object-detection", format: "onnx", backend: "openvino", includePreview: true)).Count);

                ModelArtifact artifact = package.ToCoreArtifacts()[0];
                using var registry = new BackendRegistry();
                registry.UseOnnxRuntime();
                using VisualPipeline pipeline = CreatePipeline(registry, artifact, DirectProfile(modelId, "onnx"));
                using PreparedVisualInput input = Input();
                Assert.AreEqual(2, pipeline.Run(input).GetValue<OrientedDetectionResult>().Detections.Count);
            }
            finally { Directory.Delete(root, true); }
        }

        internal static VisualModelProfile DirectProfile(ModelId modelId, string format)
        {
            var schema = new CenterSizeAngleOutputSchema("boxes", "scores", "classes");
            var decoder = new DirectOrientedDetectionDecoder(schema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, iouThreshold: .3f, maximumCandidates: 4, maximumDetections: 4));
            return Profile(modelId, format, decoder, "boxes", 5);
        }

        private static VisualModelProfile CornerProfile(ModelId modelId)
        {
            var schema = new FourCornerOutputSchema("corners", "scores", "classes");
            var decoder = new FourCornerOrientedDetectionDecoder(schema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, iouThreshold: .3f, maximumCandidates: 4, maximumDetections: 4));
            return Profile(modelId, "onnx", decoder, "corners", 8);
        }

        private static VisualModelProfile Profile(ModelId modelId, string format, IVisualDecoder decoder, string geometryName, int geometryComponents)
        {
            return new VisualModelProfile(
                "tests/oriented-detection.v1", modelId, VisualTaskId.OrientedObjectDetection, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,100,100), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding(geometryName, TensorElementType.Float32, new TensorShape(1,4,geometryComponents)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,4)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,4))
                }, new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
        }

        private static VisualPipeline CreatePipeline(BackendRegistry registry, ModelArtifact artifact, VisualModelProfile profile)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            return new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.OrientedObjectDetection), request);
        }

        internal static PreparedVisualInput Input()
        {
            var size = new VisualSize(100,100);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,100,100), new float[30000]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size,size));
        }
    }
}
