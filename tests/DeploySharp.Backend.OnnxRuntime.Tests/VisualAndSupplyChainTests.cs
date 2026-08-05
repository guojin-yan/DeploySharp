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

        private static PreparedVisualInput ClassificationInput()
        {
            var size = new VisualSize(2, 2);
            return new PreparedVisualInput(
                "images", (Tensor<float>)OnnxRuntimeTestData.ClassificationInputs().GetRequired("images"), size, size, 1,
                VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }
    }
}
