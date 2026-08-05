using System;
using System.IO;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests
{
    [TestClass]
    public sealed class VisualAndSupplyChainTests
    {
        [TestMethod]
        public void RealOpenVinoIrClassificationFlowsThroughCoreRegistryAndVisualDecoder()
        {
            ModelArtifact artifact = OpenVinoTestData.IrArtifact();
            VisualModelProfile profile = ClassificationProfile(artifact.ModelId, "openvino-ir");
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile);
            using PreparedVisualInput input = ClassificationInput();
            ClassificationResult result = pipeline.Run(input).GetValue<ClassificationResult>();
            Assert.AreEqual(2, result.TopPrediction!.Index);
            Assert.AreEqual("three", result.TopPrediction.Label);
        }

        [TestMethod]
        public void RealOpenVinoOnnxDetectionAppliesInverseResizeAndClassAwareNms()
        {
            ModelArtifact artifact = OpenVinoTestData.OnnxArtifact("detection.onnx");
            var schema = new DetectionOutputSchema("detections", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ObjectnessTimesClassScore, 2, 5, 4);
            var profile = new VisualModelProfile(
                "tests/openvino-detection.v1", artifact.ModelId, VisualTaskId.ObjectDetection, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("detections", TensorElementType.Float32, new TensorShape(1, 3, 7)) },
                new[] { new VisualLabel(0, "cat"), new VisualLabel(1, "dog") },
                new DetectionDecoder(schema, new DetectionDecoderOptions(scoreThreshold: 0.25f, iouThreshold: 0.45f)));
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile);
            var source = new VisualSize(200, 100);
            var model = new VisualSize(100, 100);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, model));
            DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
            Assert.AreEqual(2, result.Detections.Count);
            Assert.AreEqual("dog", result.Detections[0].Label.Label);
            Assert.AreEqual(120f, result.Detections[0].Box.X, 0.001f);
            Assert.AreEqual(60f, result.Detections[0].Box.Width, 0.001f);
            Assert.AreEqual("cat", result.Detections[1].Label.Label);
        }

        [TestMethod]
        public void RealOpenVinoOnnxAndIrProduceTheSameGoldenSemanticMask()
        {
            ModelArtifact onnxArtifact = OpenVinoTestData.OnnxArtifact("semantic-segmentation.onnx");
            ModelArtifact irArtifact = OpenVinoTestData.IrArtifact("semantic-segmentation.xml");
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            using (VisualPipeline pipeline = CreatePipeline(registry, onnxArtifact, SegmentationProfile(onnxArtifact.ModelId, "onnx")))
            using (PreparedVisualInput input = SegmentationInput())
            {
                SemanticSegmentationResult result = pipeline.Run(input).GetValue<SemanticSegmentationResult>();
                CollectionAssert.AreEqual(new ushort[] { 0, 1, 2, 0, 0, 1 }, result.Mask.ToArray());
                Assert.AreEqual("2ed4fa5094662ebe63d9265149adf86858fd7b03983a35118880f09517f824de", result.Mask.ComputeSha256());
            }

            using (VisualPipeline pipeline = CreatePipeline(registry, irArtifact, SegmentationProfile(irArtifact.ModelId, "openvino-ir")))
            using (PreparedVisualInput input = SegmentationInput())
            {
                SemanticSegmentationResult result = pipeline.Run(input).GetValue<SemanticSegmentationResult>();
                CollectionAssert.AreEqual(new ushort[] { 0, 1, 2, 0, 0, 1 }, result.Mask.ToArray());
            }
        }

        [TestMethod]
        public void RealOpenVinoOnnxAndIrProduceTheSameGoldenPose()
        {
            ModelArtifact onnxArtifact = OpenVinoTestData.OnnxArtifact("direct-pose.onnx");
            ModelArtifact irArtifact = OpenVinoTestData.IrArtifact("direct-pose.xml");
            const string expectedHash = "5368c9887690613a6a343fde5014bf814dd59fbfe40a16ec592b7a55f8d5cba5";
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            using (VisualPipeline pipeline = CreatePipeline(registry, onnxArtifact, DirectPoseProfile(onnxArtifact.ModelId, "onnx")))
            using (PreparedVisualInput input = PoseInput())
            {
                PoseEstimationResult result = pipeline.Run(input).GetValue<PoseEstimationResult>();
                Assert.AreEqual(2, result.Instances.Count);
                Assert.AreEqual(expectedHash, result.ComputeSha256());
            }

            using (VisualPipeline pipeline = CreatePipeline(registry, irArtifact, DirectPoseProfile(irArtifact.ModelId, "openvino-ir")))
            using (PreparedVisualInput input = PoseInput())
            {
                PoseEstimationResult result = pipeline.Run(input).GetValue<PoseEstimationResult>();
                Assert.AreEqual(2, result.Instances.Count);
                Assert.AreEqual(expectedHash, result.ComputeSha256());
            }
        }

        [TestMethod]
        public void VerifiedMultiFileIrModelPackAndOfflinePreviewEnterRealVisualSelection()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-openvino-supply-chain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string xml = Path.Combine(root, "classification.xml");
                string bin = Path.Combine(root, "classification.bin");
                File.Copy(OpenVinoTestData.Ir("classification.xml"), xml);
                File.Copy(OpenVinoTestData.Ir("classification.bin"), bin);
                var modelId = new ModelId("tests/openvino-supply-chain-classifier");
                var artifactDocument = new ModelArtifactDocument(
                    "openvino-ir.cpu", "openvino-ir", ModelArtifactLocationKind.File, "classification.xml", new[] { "openvino" },
                    new[]
                    {
                        new ModelFileDocument("classification.xml", OpenVinoTestData.Sha256(xml), new FileInfo(xml).Length, "application/xml", ModelFileRole.Model),
                        new ModelFileDocument("classification.bin", OpenVinoTestData.Sha256(bin), new FileInfo(bin).Length, "application/octet-stream", ModelFileRole.Weights)
                    },
                    precision: "fp32", portable: true, minimumBackendVersion: "2.0.0-alpha.1", minimumRuntimeVersion: "2026.2.1");
                var packageDocument = new ModelPackageDocument(
                    "2.0", modelId.Value, "DeploySharp OpenVINO IR contract classifier", "deploysharp-fixture", "image-classification", "1.0",
                    new ModelExporterDocument("OpenVINO", "2026.2.1", "ov.convert_model + ov.save_model"),
                    new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-05T00:00:00Z"), "tests/openvino-supply-chain.v1",
                    new[] { new ModelTensorSignatureDocument("images", "float32", new long[] { 1, 3, 2, 2 }) },
                    new[] { new ModelTensorSignatureDocument("scores", "float32", new long[] { 1, 3 }) },
                    new[] { artifactDocument });
                string manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(packageDocument)));
                LocalModelPackage package = ModelPackageLoader.Load(manifestPath);
                Assert.AreEqual(2, package.Artifacts[0].Files.Count);
                ModelArtifact artifact = package.ToCoreArtifacts()[0];

                var entry = new ModelCatalogEntry(
                    modelId.Value, "DeploySharp OpenVINO IR contract classifier", "deploysharp-fixture", "image-classification", "1.0", ModelCatalogStatus.Preview,
                    "Offline adapter contract fixture; not an official algorithm model.", packageDocument.Source,
                    new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260805.1", "0123456789abcdef"),
                    new[] { new ModelCatalogArtifact("openvino-ir.cpu", "openvino-ir", new[] { "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
                var validationOptions = new ModelCatalogValidationOptions(admittedFormats: new[] { "openvino-ir" }, admittedBackends: new[] { "openvino" });
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-05T00:00:00Z", "tests-local-only.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }), validationOptions);
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: modelId.Value, format: "openvino-ir", backend: "openvino", includePreview: true)).Count);
                CollectionAssert.Contains((System.Collections.ICollection)ModelCatalogValidationOptions.Default.AdmittedFormats, "openvino-ir");
                CollectionAssert.Contains((System.Collections.ICollection)ModelCatalogValidationOptions.Default.AdmittedBackends, "openvino");

                using var registry = new BackendRegistry();
                registry.UseOpenVino();
                using VisualPipeline pipeline = CreatePipeline(registry, artifact, ClassificationProfile(modelId, "openvino-ir"));
                using PreparedVisualInput input = ClassificationInput();
                Assert.AreEqual(2, pipeline.Run(input).GetValue<ClassificationResult>().TopPrediction!.Index);
            }
            finally { Directory.Delete(root, true); }
        }

        [TestMethod]
        public void VerifiedPoseIrModelPackAndOfflinePreviewEnterRealPoseSelection()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-openvino-pose-supply-chain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string xml = Path.Combine(root, "direct-pose.xml");
                string bin = Path.Combine(root, "direct-pose.bin");
                File.Copy(OpenVinoTestData.Ir("direct-pose.xml"), xml);
                File.Copy(OpenVinoTestData.Ir("direct-pose.bin"), bin);
                var modelId = new ModelId("tests/openvino-supply-chain-pose");
                var artifactDocument = new ModelArtifactDocument(
                    "openvino-ir.cpu", "openvino-ir", ModelArtifactLocationKind.File, "direct-pose.xml", new[] { "openvino" },
                    new[]
                    {
                        new ModelFileDocument("direct-pose.xml", OpenVinoTestData.Sha256(xml), new FileInfo(xml).Length, "application/xml", ModelFileRole.Model),
                        new ModelFileDocument("direct-pose.bin", OpenVinoTestData.Sha256(bin), new FileInfo(bin).Length, "application/octet-stream", ModelFileRole.Weights)
                    },
                    precision: "fp32", portable: true, minimumBackendVersion: "2.0.0-alpha.1", minimumRuntimeVersion: "2026.2.1");
                var packageDocument = new ModelPackageDocument(
                    "2.0", modelId.Value, "DeploySharp OpenVINO IR Pose contract fixture", "deploysharp-fixture", "pose-estimation", "1.0",
                    new ModelExporterDocument("OpenVINO", "2026.2.1", "ov.convert_model + ov.save_model"),
                    new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-05T00:00:00Z"), "tests/openvino-pose.v1",
                    new[] { new ModelTensorSignatureDocument("images", "float32", new long[] { 1, 3, 100, 100 }) },
                    new[]
                    {
                        new ModelTensorSignatureDocument("boxes", "float32", new long[] { 1, 3, 4 }),
                        new ModelTensorSignatureDocument("scores", "float32", new long[] { 1, 3 }),
                        new ModelTensorSignatureDocument("keypoints", "float32", new long[] { 1, 3, 3, 4 })
                    },
                    new[] { artifactDocument });
                string manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(packageDocument)));
                LocalModelPackage package = ModelPackageLoader.Load(manifestPath);
                Assert.AreEqual(2, package.Artifacts[0].Files.Count);
                ModelArtifact artifact = package.ToCoreArtifacts()[0];

                var entry = new ModelCatalogEntry(
                    modelId.Value, "DeploySharp OpenVINO IR Pose contract fixture", "deploysharp-fixture", "pose-estimation", "1.0", ModelCatalogStatus.Preview,
                    "Offline adapter contract fixture; not an official Pose algorithm model.", packageDocument.Source,
                    new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260805.1", "0123456789abcdef"),
                    new[] { new ModelCatalogArtifact("openvino-ir.cpu", "openvino-ir", new[] { "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
                var validationOptions = new ModelCatalogValidationOptions(admittedFormats: new[] { "openvino-ir" }, admittedBackends: new[] { "openvino" });
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-05T00:00:00Z", "tests-local-only.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }), validationOptions);
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "pose-estimation", format: "openvino-ir", backend: "openvino", includePreview: true)).Count);

                using var registry = new BackendRegistry();
                registry.UseOpenVino();
                using VisualPipeline pipeline = CreatePipeline(registry, artifact, DirectPoseProfile(modelId, "openvino-ir"));
                using PreparedVisualInput input = PoseInput();
                Assert.AreEqual("5368c9887690613a6a343fde5014bf814dd59fbfe40a16ec592b7a55f8d5cba5", pipeline.Run(input).GetValue<PoseEstimationResult>().ComputeSha256());
            }
            finally { Directory.Delete(root, true); }
        }

        private static VisualPipeline CreatePipeline(BackendRegistry registry, ModelArtifact artifact, VisualModelProfile profile)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoTestData.BackendId, "CPU");
            VisualProfileSelection selection = profiles.Select(artifact, registry, request, profile.Task);
            return new VisualPipeline(registry, selection, request);
        }

        private static VisualModelProfile ClassificationProfile(ModelId modelId, string format)
        {
            return new VisualModelProfile(
                "tests/openvino-classification.v1", modelId, VisualTaskId.ImageClassification, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
                new[] { new VisualLabel(0, "one"), new VisualLabel(1, "two"), new VisualLabel(2, "three") },
                new ClassificationDecoder("scores", ClassificationScoreMode.Logits, topK: 3));
        }

        private static VisualModelProfile SegmentationProfile(ModelId modelId, string format)
        {
            var schema = new SegmentationOutputSchema("logits", SegmentationOutputKind.Logits, SegmentationTensorLayout.Nchw, 3);
            return new VisualModelProfile(
                "tests/openvino-semantic-segmentation.v1", modelId, VisualTaskId.SemanticSegmentation, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 3), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(1, 3, 2, 3)) },
                new[] { new VisualLabel(0, "background"), new VisualLabel(1, "green"), new VisualLabel(2, "blue") },
                new SemanticSegmentationDecoder(schema));
        }

        private static VisualModelProfile DirectPoseProfile(ModelId modelId, string format)
        {
            var topology = new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "left", 1, oksSigma: .1f),
                new PoseKeypointDefinition(1, "right", 0, oksSigma: .1f),
                new PoseKeypointDefinition(2, "center", oksSigma: .1f)
            }, new[] { new PoseSkeletonEdge(0,2), new PoseSkeletonEdge(1,2) });
            var schema = new DirectPoseOutputSchema("keypoints", 3, 4, visibilityComponentIndex: 3, boxesOutputName: "boxes", instanceScoresOutputName: "scores");
            var decoder = new DirectPoseDecoder(schema, topology, new PoseDecoderOptions(instanceScoreThreshold: .1f, maximumCandidates: 3, maximumInstances: 3, oks: new PoseOksOptions(.8f)));
            return new VisualModelProfile(
                "tests/openvino-direct-pose.v1", modelId, VisualTaskId.PoseEstimation, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,100,100), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("keypoints", TensorElementType.Float32, new TensorShape(1,3,3,4))
                },
                Array.Empty<VisualLabel>(), decoder);
        }

        private static PreparedVisualInput PoseInput()
        {
            var size = new VisualSize(100,100);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,100,100), new float[30000]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }

        private static PreparedVisualInput SegmentationInput()
        {
            var size = new VisualSize(3, 2);
            float[] values =
            {
                9, 0, 0, 1, 5, 0,
                0, 9, 0, 1, 5, 9,
                0, 0, 9, 0, 5, 9
            };
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 3), values), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }

        private static PreparedVisualInput ClassificationInput()
        {
            var size = new VisualSize(2, 2);
            return new PreparedVisualInput("images", (Tensor<float>)OpenVinoTestData.ClassificationInputs().GetRequired("images"), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }
    }
}
