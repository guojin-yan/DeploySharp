using System;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests
{
    [TestClass]
    public sealed class OrientedDetectionIntegrationTests
    {
        private static readonly ModelId ModelId = new ModelId("tests/openvino-direct-obb");

        [TestMethod]
        public void RealOpenVinoCpuOnnxAndIrProduceTheSameObbResult()
        {
            var onnxArtifact = new ModelArtifact(ModelId, "onnx", OpenVinoTestData.Onnx("direct-obb.onnx"), preferredBackend: OpenVinoBackendProvider.BackendId);
            var irArtifact = new ModelArtifact(ModelId, "openvino-ir", OpenVinoTestData.Ir("direct-obb.xml"), preferredBackend: OpenVinoBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            string onnxDigest;
            using (VisualPipeline pipeline = CreatePipeline(registry, onnxArtifact, Profile("onnx")))
            using (PreparedVisualInput input = Input())
            {
                OrientedDetectionResult result = pipeline.Run(input).GetValue<OrientedDetectionResult>();
                Assert.AreEqual(2, result.Detections.Count);
                onnxDigest = result.ComputeSha256();
            }

            using (VisualPipeline pipeline = CreatePipeline(registry, irArtifact, Profile("openvino-ir")))
            using (PreparedVisualInput input = Input())
            {
                OrientedDetectionResult result = pipeline.Run(input).GetValue<OrientedDetectionResult>();
                Assert.AreEqual(2, result.Detections.Count);
                Assert.AreEqual(onnxDigest, result.ComputeSha256());
            }
        }

        [TestMethod]
        public void VerifiedIrSidecarsAndOfflinePreviewCatalogEnterRealObbSelection()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-obb-ir-pack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string xml = Path.Combine(root, "direct-obb.xml");
                string bin = Path.Combine(root, "direct-obb.bin");
                File.Copy(OpenVinoTestData.Ir("direct-obb.xml"), xml);
                File.Copy(OpenVinoTestData.Ir("direct-obb.bin"), bin);
                var artifactDocument = new ModelArtifactDocument(
                    "openvino-ir.cpu", "openvino-ir", ModelArtifactLocationKind.File, Path.GetFileName(xml), new[] { "openvino" },
                    new[]
                    {
                        new ModelFileDocument(Path.GetFileName(xml), OpenVinoTestData.Sha256(xml), new FileInfo(xml).Length, "application/xml", ModelFileRole.Model),
                        new ModelFileDocument(Path.GetFileName(bin), OpenVinoTestData.Sha256(bin), new FileInfo(bin).Length, "application/octet-stream", ModelFileRole.Weights)
                    }, precision: "fp32", portable: true, minimumRuntimeVersion: "2026.2.1");
                var packageDocument = new ModelPackageDocument(
                    "2.0", ModelId.Value, "DeploySharp OpenVINO IR OBB fixture", "deploysharp-fixture", "oriented-object-detection", "1.0",
                    new ModelExporterDocument("OpenVINO", "2026.2.1", "ov.convert_model + ov.save_model"),
                    new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-05T00:00:00Z"), "tests/oriented-detection-ir.v1",
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
                Assert.AreEqual(2, package.Artifacts[0].Files.Count);
                Assert.AreEqual(OpenVinoTestData.Sha256(xml), package.Artifacts[0].Files[0].Document.Sha256);
                Assert.AreEqual(OpenVinoTestData.Sha256(bin), package.Artifacts[0].Files[1].Document.Sha256);

                var entry = new ModelCatalogEntry(
                    ModelId.Value, "DeploySharp OpenVINO IR OBB fixture", "deploysharp-fixture", "oriented-object-detection", "1.0", ModelCatalogStatus.Preview,
                    "Offline adapter contract fixture; not an algorithm-verified model.", packageDocument.Source,
                    new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260805.1", "0123456789abcdef"),
                    new[] { new ModelCatalogArtifact("openvino-ir.cpu", "openvino-ir", new[] { "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-05T00:00:00Z", "tests-local-only.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "oriented-object-detection", format: "openvino-ir", backend: "openvino", includePreview: true)).Count);

                ModelArtifact artifact = package.ToCoreArtifacts()[0];
                using var registry = new BackendRegistry();
                registry.UseOpenVino();
                using VisualPipeline pipeline = CreatePipeline(registry, artifact, Profile("openvino-ir"));
                using PreparedVisualInput input = Input();
                Assert.AreEqual(2, pipeline.Run(input).GetValue<OrientedDetectionResult>().Detections.Count);
            }
            finally { Directory.Delete(root, true); }
        }

        private static VisualModelProfile Profile(string format)
        {
            var schema = new CenterSizeAngleOutputSchema("boxes", "scores", "classes");
            var decoder = new DirectOrientedDetectionDecoder(schema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, iouThreshold: .3f, maximumCandidates: 4, maximumDetections: 4));
            return new VisualModelProfile(
                "tests/openvino-oriented-detection.v1", ModelId, VisualTaskId.OrientedObjectDetection, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,100,100), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,4,5)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,4)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,4))
                }, new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
        }

        private static VisualPipeline CreatePipeline(BackendRegistry registry, ModelArtifact artifact, VisualModelProfile profile)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            return new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.OrientedObjectDetection), request);
        }

        private static PreparedVisualInput Input()
        {
            var size = new VisualSize(100,100);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,100,100), new float[30000]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size,size));
        }
    }
}
