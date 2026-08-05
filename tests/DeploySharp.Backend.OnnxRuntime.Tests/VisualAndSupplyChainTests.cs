using System;
using System.IO;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    [TestClass]
    public sealed class VisualAndSupplyChainTests
    {
        [TestMethod]
        public void RealOnnxRuntimeClassificationFlowsThroughCoreRegistryAndVisualDecoder()
        {
            ModelArtifact artifact = OnnxRuntimeTestData.Artifact("classification.onnx");
            VisualModelProfile profile = ClassificationProfile(artifact.ModelId);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile);
            using PreparedVisualInput input = ClassificationInput();
            ClassificationResult result = pipeline.Run(input).GetValue<ClassificationResult>();
            Assert.AreEqual(2, result.TopPrediction!.Index);
            Assert.AreEqual("three", result.TopPrediction.Label);
        }

        [TestMethod]
        public void RealOnnxRuntimeDetectionAppliesInverseResizeAndClassAwareNms()
        {
            ModelArtifact artifact = OnnxRuntimeTestData.Artifact("detection.onnx");
            var schema = new DetectionOutputSchema("detections", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ObjectnessTimesClassScore, 2, 5, 4);
            var profile = new VisualModelProfile(
                "tests/onnxruntime-detection.v1", artifact.ModelId, VisualTaskId.ObjectDetection, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("detections", TensorElementType.Float32, new TensorShape(1, 3, 7)) },
                new[] { new VisualLabel(0, "cat"), new VisualLabel(1, "dog") },
                new DetectionDecoder(schema, new DetectionDecoderOptions(scoreThreshold: 0.25f, iouThreshold: 0.45f)));
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
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
            Assert.AreEqual(20f, result.Detections[1].Box.X, 0.001f);
        }

        [TestMethod]
        public void RealOnnxRuntimeMulticlassLogitsProduceGoldenSemanticMask()
        {
            ModelArtifact artifact = OnnxRuntimeTestData.Artifact("semantic-segmentation.onnx");
            VisualModelProfile profile = SegmentationProfile(artifact.ModelId, "onnx", "logits", SegmentationOutputKind.Logits, SegmentationTensorLayout.Nchw, TensorElementType.Float32, new TensorShape(1, 3, 2, 3));
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile);
            using PreparedVisualInput input = SegmentationInput();
            SemanticSegmentationResult result = pipeline.Run(input).GetValue<SemanticSegmentationResult>();
            CollectionAssert.AreEqual(new ushort[] { 0, 1, 2, 0, 0, 1 }, result.Mask.ToArray());
            Assert.AreEqual("2ed4fa5094662ebe63d9265149adf86858fd7b03983a35118880f09517f824de", result.Mask.ComputeSha256());
        }

        [TestMethod]
        public void RealOnnxRuntimeBinaryProbabilityAndIntegerLabelMapAreDecoded()
        {
            ModelArtifact binaryArtifact = OnnxRuntimeTestData.Artifact("binary-segmentation.onnx");
            VisualModelProfile binaryProfile = SegmentationProfile(binaryArtifact.ModelId, "onnx", "probabilities", SegmentationOutputKind.Probabilities, SegmentationTensorLayout.Nchw, TensorElementType.Float32, new TensorShape(1, 1, 2, 3), classCount: 2);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            using (VisualPipeline pipeline = CreatePipeline(registry, binaryArtifact, binaryProfile))
            using (PreparedVisualInput input = BinarySegmentationInput())
            {
                SemanticSegmentationResult result = pipeline.Run(input).GetValue<SemanticSegmentationResult>();
                CollectionAssert.AreEqual(new ushort[] { 0, 1, 1, 0, 0, 1 }, result.Mask.ToArray());
            }

            ModelArtifact labelArtifact = OnnxRuntimeTestData.Artifact("semantic-label-map.onnx");
            VisualModelProfile labelProfile = SegmentationProfile(labelArtifact.ModelId, "onnx", "labels", SegmentationOutputKind.LabelMap, SegmentationTensorLayout.Nhw, TensorElementType.Int64, new TensorShape(1, 2, 3));
            using (VisualPipeline pipeline = CreatePipeline(registry, labelArtifact, labelProfile))
            using (PreparedVisualInput input = SegmentationInput())
            {
                SemanticSegmentationResult result = pipeline.Run(input).GetValue<SemanticSegmentationResult>();
                CollectionAssert.AreEqual(new ushort[] { 0, 1, 2, 0, 0, 1 }, result.Mask.ToArray());
            }
        }

        [TestMethod]
        public void RealOnnxRuntimeDirectPoseProducesStableOksFilteredGoldenResult()
        {
            ModelArtifact artifact = OnnxRuntimeTestData.Artifact("direct-pose.onnx");
            VisualModelProfile profile = DirectPoseProfile(artifact.ModelId, "onnx");
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile);
            using PreparedVisualInput input = PoseInput(new VisualSize(100, 100));
            PoseEstimationResult result = pipeline.Run(input).GetValue<PoseEstimationResult>();
            Assert.AreEqual(2, result.Instances.Count);
            Assert.AreEqual(0, result.Instances[0].SourceIndex);
            Assert.AreEqual(2, result.Instances[1].SourceIndex);
            Assert.AreEqual(20f, result.Instances[0].Keypoints[0].Point.X, 0.0001f);
            Assert.AreEqual("5368c9887690613a6a343fde5014bf814dd59fbfe40a16ec592b7a55f8d5cba5", result.ComputeSha256());
        }

        [TestMethod]
        public void RealOnnxRuntimeHeatmapPoseUsesStableRowMajorTieAndBoundaryPeak()
        {
            ModelArtifact artifact = OnnxRuntimeTestData.Artifact("heatmap-pose.onnx");
            VisualModelProfile profile = HeatmapPoseProfile(artifact.ModelId, "onnx");
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            using VisualPipeline pipeline = CreatePipeline(registry, artifact, profile);
            using PreparedVisualInput input = PoseInput(new VisualSize(8, 8));
            PoseEstimationResult result = pipeline.Run(input).GetValue<PoseEstimationResult>();
            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual(0f, result.Instances[0].Keypoints[0].Point.X, 0.0001f);
            Assert.AreEqual(7f, result.Instances[0].Keypoints[1].Point.X, 0.0001f);
            Assert.AreEqual(0f, result.Instances[0].Keypoints[1].Point.Y, 0.0001f);
            Assert.AreEqual(7f, result.Instances[0].Keypoints[2].Point.X, 0.0001f);
            Assert.AreEqual(7f, result.Instances[0].Keypoints[2].Point.Y, 0.0001f);
            Assert.AreEqual("a1781de6187f007aa168f62d0c9507975eece00155a63d061ec60313d2e96f5e", result.ComputeSha256());
        }

        [TestMethod]
        public void VerifiedModelPackAndOfflinePreviewCatalogEnterRealBackendAndVisualSelection()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-ort-supply-chain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string modelPath = Path.Combine(root, "classification.onnx");
                File.Copy(OnnxRuntimeTestData.Fixture("classification.onnx"), modelPath);
                string hash = OnnxRuntimeTestData.Sha256(modelPath);
                long size = new FileInfo(modelPath).Length;
                var modelId = new ModelId("tests/onnxruntime-supply-chain-classifier");
                var artifactDocument = new ModelArtifactDocument(
                    "onnx.cpu", "onnx", ModelArtifactLocationKind.File, "classification.onnx", new[] { "onnxruntime" },
                    new[] { new ModelFileDocument("classification.onnx", hash, size, "application/onnx", ModelFileRole.Model) },
                    precision: "fp32", opset: 13, portable: true, minimumBackendVersion: "2.0.0-alpha.1", minimumRuntimeVersion: "1.28.0");
                var packageDocument = new ModelPackageDocument(
                    "2.0", modelId.Value, "DeploySharp ONNX contract classifier", "deploysharp-fixture", "image-classification", "1.0",
                    new ModelExporterDocument("DeploySharp.Tests", "2.0.0", "generated"),
                    new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-04T00:00:00Z"), "tests/onnxruntime-supply-chain.v1",
                    new[] { new ModelTensorSignatureDocument("images", "float32", new long[] { 1, 3, 2, 2 }) },
                    new[] { new ModelTensorSignatureDocument("scores", "float32", new long[] { 1, 3 }) },
                    new[] { artifactDocument });
                string manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(packageDocument)));
                LocalModelPackage package = ModelPackageLoader.Load(manifestPath);
                ModelArtifact artifact = package.ToCoreArtifacts()[0];

                var entry = new ModelCatalogEntry(
                    modelId.Value, "DeploySharp ONNX contract classifier", "deploysharp-fixture", "image-classification", "1.0", ModelCatalogStatus.Preview,
                    "Offline adapter contract fixture; not an official algorithm model.", packageDocument.Source,
                    new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260804.1", "0123456789abcdef"),
                    new[] { new ModelCatalogArtifact("onnx.cpu", "onnx", new[] { "onnxruntime" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
                var catalogOptions = new ModelCatalogValidationOptions(admittedFormats: new[] { "gguf", "onnx" }, admittedBackends: new[] { "llama-sharp", "onnxruntime" });
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-04T00:00:00Z", "tests-local-only.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }), catalogOptions);
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: modelId.Value, format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
                CollectionAssert.Contains((System.Collections.ICollection)ModelCatalogValidationOptions.Default.AdmittedFormats, "onnx");
                CollectionAssert.Contains((System.Collections.ICollection)ModelCatalogValidationOptions.Default.AdmittedBackends, "onnxruntime");

                using var registry = new BackendRegistry();
                registry.UseOnnxRuntime();
                using VisualPipeline pipeline = CreatePipeline(registry, artifact, ClassificationProfile(modelId));
                using PreparedVisualInput input = ClassificationInput();
                Assert.AreEqual(2, pipeline.Run(input).GetValue<ClassificationResult>().TopPrediction!.Index);
            }
            finally { Directory.Delete(root, true); }
        }

        [TestMethod]
        public void SemanticModelPackAndOfflinePreviewCatalogEnterRealSegmentationSelection()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-ort-segmentation-supply-chain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string modelPath = Path.Combine(root, "semantic-segmentation.onnx");
                File.Copy(OnnxRuntimeTestData.Fixture("semantic-segmentation.onnx"), modelPath);
                string hash = OnnxRuntimeTestData.Sha256(modelPath);
                long size = new FileInfo(modelPath).Length;
                var modelId = new ModelId("tests/onnxruntime-supply-chain-segmenter");
                var artifactDocument = new ModelArtifactDocument(
                    "onnx.cpu", "onnx", ModelArtifactLocationKind.File, "semantic-segmentation.onnx", new[] { "onnxruntime", "openvino" },
                    new[] { new ModelFileDocument("semantic-segmentation.onnx", hash, size, "application/onnx", ModelFileRole.Model) },
                    precision: "fp32", opset: 13, portable: true, minimumBackendVersion: "2.0.0-alpha.1", minimumRuntimeVersion: "1.28.0");
                var packageDocument = new ModelPackageDocument(
                    "2.0", modelId.Value, "DeploySharp semantic segmentation contract fixture", "deploysharp-fixture", "semantic-segmentation", "1.0",
                    new ModelExporterDocument("DeploySharp.Tests", "2.0.0", "generated deterministic ONNX Identity graph"),
                    new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-05T00:00:00Z"), "tests/onnxruntime-semantic-segmentation.v1",
                    new[] { new ModelTensorSignatureDocument("images", "float32", new long[] { 1, 3, 2, 3 }) },
                    new[] { new ModelTensorSignatureDocument("logits", "float32", new long[] { 1, 3, 2, 3 }) },
                    new[] { artifactDocument });
                string manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(packageDocument)));
                LocalModelPackage package = ModelPackageLoader.Load(manifestPath);
                ModelArtifact artifact = package.ToCoreArtifacts()[0];
                Assert.AreEqual(hash, artifact.Sha256);

                var entry = new ModelCatalogEntry(
                    modelId.Value, "DeploySharp semantic segmentation contract fixture", "deploysharp-fixture", "semantic-segmentation", "1.0", ModelCatalogStatus.Preview,
                    "Offline adapter contract fixture; not an official algorithm model.", packageDocument.Source,
                    new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260805.1", "0123456789abcdef"),
                    new[] { new ModelCatalogArtifact("onnx.cpu", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-05T00:00:00Z", "tests-local-only.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "semantic-segmentation", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);

                using var registry = new BackendRegistry();
                registry.UseOnnxRuntime();
                using VisualPipeline pipeline = CreatePipeline(registry, artifact, SegmentationProfile(modelId, "onnx", "logits", SegmentationOutputKind.Logits, SegmentationTensorLayout.Nchw, TensorElementType.Float32, new TensorShape(1, 3, 2, 3)));
                using PreparedVisualInput input = SegmentationInput();
                CollectionAssert.AreEqual(new ushort[] { 0, 1, 2, 0, 0, 1 }, pipeline.Run(input).GetValue<SemanticSegmentationResult>().Mask.ToArray());
            }
            finally { Directory.Delete(root, true); }
        }

        [TestMethod]
        public void PoseModelPackAndOfflinePreviewCatalogEnterRealPoseSelection()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-ort-pose-supply-chain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string modelPath = Path.Combine(root, "direct-pose.onnx");
                File.Copy(OnnxRuntimeTestData.Fixture("direct-pose.onnx"), modelPath);
                string hash = OnnxRuntimeTestData.Sha256(modelPath);
                long size = new FileInfo(modelPath).Length;
                var modelId = new ModelId("tests/onnxruntime-supply-chain-pose");
                var artifactDocument = new ModelArtifactDocument(
                    "onnx.cpu", "onnx", ModelArtifactLocationKind.File, "direct-pose.onnx", new[] { "onnxruntime", "openvino" },
                    new[] { new ModelFileDocument("direct-pose.onnx", hash, size, "application/onnx", ModelFileRole.Model) },
                    precision: "fp32", opset: 13, portable: true, minimumBackendVersion: "2.0.0-alpha.1", minimumRuntimeVersion: "1.28.0");
                var packageDocument = new ModelPackageDocument(
                    "2.0", modelId.Value, "DeploySharp direct Pose contract fixture", "deploysharp-fixture", "pose-estimation", "1.0",
                    new ModelExporterDocument("DeploySharp.Tests", "2.0.0", "generated deterministic ONNX constants"),
                    new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-05T00:00:00Z"), "tests/onnxruntime-pose.v1",
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
                ModelArtifact artifact = package.ToCoreArtifacts()[0];
                Assert.AreEqual(hash, artifact.Sha256);

                var entry = new ModelCatalogEntry(
                    modelId.Value, "DeploySharp direct Pose contract fixture", "deploysharp-fixture", "pose-estimation", "1.0", ModelCatalogStatus.Preview,
                    "Offline adapter contract fixture; not an official Pose algorithm model.", packageDocument.Source,
                    new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260805.1", "0123456789abcdef"),
                    new[] { new ModelCatalogArtifact("onnx.cpu", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-05T00:00:00Z", "tests-local-only.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "pose-estimation", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "pose-estimation", format: "onnx", backend: "openvino", includePreview: true)).Count);

                using var registry = new BackendRegistry();
                registry.UseOnnxRuntime();
                using VisualPipeline pipeline = CreatePipeline(registry, artifact, DirectPoseProfile(modelId, "onnx"));
                using PreparedVisualInput input = PoseInput(new VisualSize(100, 100));
                Assert.AreEqual(2, pipeline.Run(input).GetValue<PoseEstimationResult>().Instances.Count);
            }
            finally { Directory.Delete(root, true); }
        }

        private static VisualPipeline CreatePipeline(BackendRegistry registry, ModelArtifact artifact, VisualModelProfile profile)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeTestData.BackendId, "cpu");
            VisualProfileSelection selection = profiles.Select(artifact, registry, request, profile.Task);
            return new VisualPipeline(registry, selection, request);
        }

        private static VisualModelProfile ClassificationProfile(ModelId modelId)
        {
            return new VisualModelProfile(
                "tests/onnxruntime-classification.v1", modelId, VisualTaskId.ImageClassification, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
                new[] { new VisualLabel(0, "one"), new VisualLabel(1, "two"), new VisualLabel(2, "three") },
                new ClassificationDecoder("scores", ClassificationScoreMode.Logits, topK: 3));
        }

        private static VisualModelProfile SegmentationProfile(ModelId modelId, string format, string outputName, SegmentationOutputKind kind, SegmentationTensorLayout layout, TensorElementType elementType, TensorShape outputShape, int classCount = 3)
        {
            var schema = new SegmentationOutputSchema(outputName, kind, layout, classCount);
            VisualLabel[] labels = classCount == 2
                ? new[] { new VisualLabel(0, "background"), new VisualLabel(1, "foreground") }
                : new[] { new VisualLabel(0, "background"), new VisualLabel(1, "green"), new VisualLabel(2, "blue") };
            return new VisualModelProfile(
                "tests/onnxruntime-semantic-segmentation.v1", modelId, VisualTaskId.SemanticSegmentation, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, kind == SegmentationOutputKind.Probabilities && classCount == 2 ? 1 : 3, 2, 3), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(outputName, elementType, outputShape) },
                labels,
                new SemanticSegmentationDecoder(schema));
        }

        private static VisualModelProfile DirectPoseProfile(ModelId modelId, string format)
        {
            var schema = new DirectPoseOutputSchema("keypoints", 3, 4, visibilityComponentIndex: 3, boxesOutputName: "boxes", instanceScoresOutputName: "scores");
            var decoder = new DirectPoseDecoder(schema, PoseTopologyWithSigmas(), new PoseDecoderOptions(instanceScoreThreshold: 0.1f, maximumCandidates: 3, maximumInstances: 3, oks: new PoseOksOptions(0.8f)));
            return new VisualModelProfile(
                "tests/onnxruntime-direct-pose.v1", modelId, VisualTaskId.PoseEstimation, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("keypoints", TensorElementType.Float32, new TensorShape(1,3,3,4))
                },
                Array.Empty<VisualLabel>(), decoder);
        }

        private static VisualModelProfile HeatmapPoseProfile(ModelId modelId, string format)
        {
            var schema = new HeatmapPoseOutputSchema("heatmaps", 3, PoseHeatmapLayout.Nchw, PoseScoreKind.Probability, PoseGridMappingMode.AlignCorners, "pose_score");
            var decoder = new HeatmapPoseDecoder(schema, PoseTopologyWithSigmas(), new PoseDecoderOptions(instanceScoreThreshold: 0, maximumCandidates: 1, maximumInstances: 1));
            return new VisualModelProfile(
                "tests/onnxruntime-heatmap-pose.v1", modelId, VisualTaskId.PoseEstimation, "1.0", format,
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 8, 8), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("heatmaps", TensorElementType.Float32, new TensorShape(1,3,2,2)),
                    new VisualOutputBinding("pose_score", TensorElementType.Float32, new TensorShape(1))
                },
                Array.Empty<VisualLabel>(), decoder);
        }

        private static PoseTopology PoseTopologyWithSigmas()
        {
            return new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "left", 1, oksSigma: .1f),
                new PoseKeypointDefinition(1, "right", 0, oksSigma: .1f),
                new PoseKeypointDefinition(2, "center", oksSigma: .1f)
            }, new[] { new PoseSkeletonEdge(0,2), new PoseSkeletonEdge(1,2) });
        }

        private static PreparedVisualInput PoseInput(VisualSize size)
            => new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,size.Height,size.Width), new float[checked(3 * size.Height * size.Width)]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));

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

        private static PreparedVisualInput BinarySegmentationInput()
        {
            var size = new VisualSize(3, 2);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 1, 2, 3), new[] { 0.2f, 0.5f, 0.8f, 0f, 0.49f, 0.51f }), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }

        private static PreparedVisualInput ClassificationInput()
        {
            var size = new VisualSize(2, 2);
            return new PreparedVisualInput(
                "images", (Tensor<float>)OnnxRuntimeTestData.ClassificationInputs().GetRequired("images"), size, size, 1,
                VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }
    }
}
