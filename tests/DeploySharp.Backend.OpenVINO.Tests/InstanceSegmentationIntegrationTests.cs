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
    public sealed class InstanceSegmentationIntegrationTests
    {
        private const string GoldenSha256 = "ff39f67c056c6235a27b23edb9cec6e7ff22c2fea2aaea546c34ce4a1210873a";
        private static readonly ModelId GoldenModelId = new ModelId("tests/onnxruntime-direct-instance-segmentation");

        [TestMethod]
        public void RealOpenVinoCpuOnnxAndIrMatchOnnxRuntimeGoldenResult()
        {
            var onnxArtifact = new ModelArtifact(GoldenModelId, "onnx", OpenVinoTestData.Onnx("direct-instance-segmentation.onnx"), preferredBackend: OpenVinoBackendProvider.BackendId);
            var irArtifact = new ModelArtifact(GoldenModelId, "openvino-ir", OpenVinoTestData.Ir("direct-instance-segmentation.xml"), preferredBackend: OpenVinoBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            using (VisualPipeline pipeline = CreatePipeline(registry, onnxArtifact, Profile("onnx")))
            using (PreparedVisualInput input = Input())
            {
                InstanceSegmentationResult result = pipeline.Run(input).GetValue<InstanceSegmentationResult>();
                Assert.AreEqual(2, result.Instances.Count);
                Assert.AreEqual(GoldenSha256, result.ComputeSha256());
            }

            using (VisualPipeline pipeline = CreatePipeline(registry, irArtifact, Profile("openvino-ir")))
            using (PreparedVisualInput input = Input())
            {
                InstanceSegmentationResult result = pipeline.Run(input).GetValue<InstanceSegmentationResult>();
                Assert.AreEqual(2, result.Instances.Count);
                Assert.AreEqual(GoldenSha256, result.ComputeSha256());
            }
        }

        [TestMethod]
        public void VerifiedIrSidecarsAndOfflinePreviewCatalogEnterRealInstanceSelection()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-instance-ir-pack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string xml = Path.Combine(root, "direct-instance-segmentation.xml");
                string bin = Path.Combine(root, "direct-instance-segmentation.bin");
                File.Copy(OpenVinoTestData.Ir("direct-instance-segmentation.xml"), xml);
                File.Copy(OpenVinoTestData.Ir("direct-instance-segmentation.bin"), bin);
                var artifactDocument = new ModelArtifactDocument(
                    "openvino-ir.cpu", "openvino-ir", ModelArtifactLocationKind.File, Path.GetFileName(xml), new[] { "openvino" },
                    new[]
                    {
                        new ModelFileDocument(Path.GetFileName(xml), OpenVinoTestData.Sha256(xml), new FileInfo(xml).Length, "application/xml", ModelFileRole.Model),
                        new ModelFileDocument(Path.GetFileName(bin), OpenVinoTestData.Sha256(bin), new FileInfo(bin).Length, "application/octet-stream", ModelFileRole.Weights)
                    }, precision: "fp32", portable: true, minimumRuntimeVersion: "2026.2.1");
                var packageDocument = new ModelPackageDocument(
                    "2.0", GoldenModelId.Value, "DeploySharp OpenVINO IR instance segmentation fixture", "deploysharp-fixture", "instance-segmentation", "1.0",
                    new ModelExporterDocument("OpenVINO", "2026.2.1", "ov.convert_model + ov.save_model"),
                    new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-05T00:00:00Z"), "tests/instance-segmentation-ir.v1",
                    new[] { new ModelTensorSignatureDocument("images", "float32", new long[] { 1,3,4,4 }) },
                    new[]
                    {
                        new ModelTensorSignatureDocument("boxes", "float32", new long[] { 1,3,4 }),
                        new ModelTensorSignatureDocument("scores", "float32", new long[] { 1,3 }),
                        new ModelTensorSignatureDocument("classes", "float32", new long[] { 1,3 }),
                        new ModelTensorSignatureDocument("masks", "float32", new long[] { 1,3,4,4 })
                    }, new[] { artifactDocument });
                string manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(packageDocument)));
                LocalModelPackage package = ModelPackageLoader.Load(manifestPath);
                Assert.AreEqual(2, package.Artifacts[0].Files.Count);
                Assert.AreEqual(new FileInfo(xml).Length, package.Artifacts[0].Files[0].Document.Size);
                Assert.AreEqual(new FileInfo(bin).Length, package.Artifacts[0].Files[1].Document.Size);

                var entry = new ModelCatalogEntry(
                    GoldenModelId.Value, "DeploySharp OpenVINO IR instance segmentation fixture", "deploysharp-fixture", "instance-segmentation", "1.0", ModelCatalogStatus.Preview,
                    "Offline adapter contract fixture; not an algorithm-verified model.", packageDocument.Source,
                    new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260805.1", "0123456789abcdef"),
                    new[] { new ModelCatalogArtifact("openvino-ir.cpu", "openvino-ir", new[] { "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-05T00:00:00Z", "tests-local-only.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "instance-segmentation", format: "openvino-ir", backend: "openvino", includePreview: true)).Count);

                ModelArtifact artifact = package.ToCoreArtifacts()[0];
                using var registry = new BackendRegistry();
                registry.UseOpenVino();
                using VisualPipeline pipeline = CreatePipeline(registry, artifact, Profile("openvino-ir"));
                using PreparedVisualInput input = Input();
                Assert.AreEqual(GoldenSha256, pipeline.Run(input).GetValue<InstanceSegmentationResult>().ComputeSha256());
            }
            finally { Directory.Delete(root, true); }
        }

        private static VisualPipeline CreatePipeline(BackendRegistry registry, ModelArtifact artifact, VisualModelProfile profile)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            return new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.InstanceSegmentation), request);
        }

        private static VisualModelProfile Profile(string format)
        {
            var schema = new DirectInstanceSegmentationOutputSchema(new InstanceSegmentationCandidateSchema("boxes", "scores", "classes"), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Probabilities);
            var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, maximumCandidates: 3, maximumInstances: 3));
            return new VisualModelProfile(
                "tests/direct-instance-segmentation.v1", GoldenModelId, VisualTaskId.InstanceSegmentation, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,4,4), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("masks", TensorElementType.Float32, new TensorShape(1,3,4,4))
                }, new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
        }

        private static PreparedVisualInput Input()
        {
            var size = new VisualSize(4,4);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,4,4), new float[48]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size,size));
        }
    }
}
