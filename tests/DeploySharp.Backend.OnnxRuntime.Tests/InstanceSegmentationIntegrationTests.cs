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
    public sealed class InstanceSegmentationIntegrationTests
    {
        [TestMethod]
        public void RealOnnxRuntimeCpuExecutesDirectAndPrototypeInstanceSegmentation()
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            ModelArtifact directArtifact = OnnxRuntimeTestData.Artifact("direct-instance-segmentation.onnx");
            using (VisualPipeline pipeline = CreatePipeline(registry, directArtifact, DirectProfile(directArtifact.ModelId, "onnx")))
            using (PreparedVisualInput input = Input())
            {
                InstanceSegmentationResult result = pipeline.Run(input).GetValue<InstanceSegmentationResult>();
                Assert.AreEqual(2, result.Instances.Count);
                Assert.AreEqual(9, result.Instances[0].Mask.ForegroundPixelCount);
                Assert.AreEqual(4, result.Instances[1].Mask.ForegroundPixelCount);
                Assert.AreEqual(DirectGoldenSha256, result.ComputeSha256());
            }

            ModelArtifact prototypeArtifact = OnnxRuntimeTestData.Artifact("prototype-instance-segmentation.onnx");
            using (VisualPipeline pipeline = CreatePipeline(registry, prototypeArtifact, PrototypeProfile(prototypeArtifact.ModelId)))
            using (PreparedVisualInput input = Input())
            {
                InstanceSegmentationResult result = pipeline.Run(input).GetValue<InstanceSegmentationResult>();
                Assert.AreEqual(2, result.Instances.Count);
                Assert.AreEqual(8, result.Instances[0].Mask.ForegroundPixelCount);
                Assert.AreEqual(8, result.Instances[1].Mask.ForegroundPixelCount);
                Assert.IsTrue(result.Instances[0].Mask.IsForeground(0, 0));
                Assert.IsTrue(result.Instances[1].Mask.IsForeground(3, 0));
            }
        }

        [TestMethod]
        public void VerifiedOnnxModelPackAndOfflinePreviewCatalogEnterRealInstanceSelection()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-instance-pack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string modelPath = Path.Combine(root, "direct-instance-segmentation.onnx");
                File.Copy(OnnxRuntimeTestData.Fixture("direct-instance-segmentation.onnx"), modelPath);
                string hash = OnnxRuntimeTestData.Sha256(modelPath);
                long size = new FileInfo(modelPath).Length;
                var modelId = new ModelId("tests/instance-segmentation-supply-chain");
                var artifactDocument = new ModelArtifactDocument(
                    "onnx.cpu", "onnx", ModelArtifactLocationKind.File, Path.GetFileName(modelPath), new[] { "onnxruntime", "openvino" },
                    new[] { new ModelFileDocument(Path.GetFileName(modelPath), hash, size, "application/onnx", ModelFileRole.Model) },
                    precision: "fp32", portable: true);
                var packageDocument = new ModelPackageDocument(
                    "2.0", modelId.Value, "DeploySharp instance segmentation contract fixture", "deploysharp-fixture", "instance-segmentation", "1.0",
                    new ModelExporterDocument("ONNX", "1.22.0", "eng/test-models/Generate-OnnxRuntimeFixtures.py"),
                    new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-05T00:00:00Z"), "tests/instance-segmentation.v1",
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
                Assert.AreEqual(hash, package.Artifacts[0].Files[0].Document.Sha256);
                Assert.AreEqual(size, package.Artifacts[0].Files[0].Document.Size);
                ModelArtifact artifact = package.ToCoreArtifacts()[0];

                var entry = new ModelCatalogEntry(
                    modelId.Value, "DeploySharp instance segmentation contract fixture", "deploysharp-fixture", "instance-segmentation", "1.0", ModelCatalogStatus.Preview,
                    "Offline adapter contract fixture; not an algorithm-verified model.", packageDocument.Source,
                    new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260805.1", "0123456789abcdef"),
                    new[] { new ModelCatalogArtifact("onnx.cpu", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-05T00:00:00Z", "tests-local-only.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "instance-segmentation", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "instance-segmentation", format: "onnx", backend: "openvino", includePreview: true)).Count);

                using var registry = new BackendRegistry();
                registry.UseOnnxRuntime();
                using VisualPipeline pipeline = CreatePipeline(registry, artifact, DirectProfile(modelId, "onnx"));
                using PreparedVisualInput input = Input();
                InstanceSegmentationResult result = pipeline.Run(input).GetValue<InstanceSegmentationResult>();
                Assert.AreEqual(2, result.Instances.Count);
                Assert.AreEqual("f0230bfddcdc93219d8a9e7e344b52f43e20e2e72ad1505892d88e99cb0fb5ae", result.Instances[0].Mask.ComputeSha256());
                Assert.AreEqual("98da0b32f6f202c623dcb3b5a6917b34dc20920687b422f4c5c12371f6f3e848", result.Instances[1].Mask.ComputeSha256());
            }
            finally { Directory.Delete(root, true); }
        }

        internal const string DirectGoldenSha256 = "ff39f67c056c6235a27b23edb9cec6e7ff22c2fea2aaea546c34ce4a1210873a";

        private static VisualPipeline CreatePipeline(BackendRegistry registry, ModelArtifact artifact, VisualModelProfile profile)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            return new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.InstanceSegmentation), request);
        }

        internal static VisualModelProfile DirectProfile(ModelId modelId, string format)
        {
            var schema = new DirectInstanceSegmentationOutputSchema(new InstanceSegmentationCandidateSchema("boxes", "scores", "classes"), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Probabilities);
            var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, maximumCandidates: 3, maximumInstances: 3));
            return new VisualModelProfile(
                "tests/direct-instance-segmentation.v1", modelId, VisualTaskId.InstanceSegmentation, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,4,4), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("masks", TensorElementType.Float32, new TensorShape(1,3,4,4))
                }, new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
        }

        private static VisualModelProfile PrototypeProfile(ModelId modelId)
        {
            var schema = new PrototypeInstanceSegmentationOutputSchema(new InstanceSegmentationCandidateSchema("boxes", "scores", "classes"), "prototypes", "coefficients", InstanceMaskTensorLayout.Nchw, cropSpace: InstanceMaskCropSpace.None);
            var decoder = new PrototypeInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, maximumCandidates: 3, maximumInstances: 3));
            return new VisualModelProfile(
                "tests/prototype-instance-segmentation.v1", modelId, VisualTaskId.InstanceSegmentation, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,4,4), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("prototypes", TensorElementType.Float32, new TensorShape(1,2,4,4)),
                    new VisualOutputBinding("coefficients", TensorElementType.Float32, new TensorShape(1,3,2))
                }, new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
        }

        internal static PreparedVisualInput Input()
        {
            var size = new VisualSize(4,4);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,4,4), new float[48]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size,size));
        }
    }
}
