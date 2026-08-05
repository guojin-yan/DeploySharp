using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    /// <summary>Proves that verified ModelPack artifacts and offline ModelFactory catalog selection feed the Visual contract. / 验证 ModelPack 工件和离线 ModelFactory 目录选择可以进入 Visual 契约。</summary>
    [TestClass]
    public sealed class SupplyChainIntegrationTests
    {
        [TestMethod]
        public void VerifiedModelPackArtifactAndCatalogSelectionEnterVisualRegistryWithoutNetwork()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-visual-supply-chain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] modelBytes = Encoding.UTF8.GetBytes("synthetic onnx graph");
                string hash = Hash(modelBytes);
                string modelPath = Path.Combine(root, "classifier.onnx");
                File.WriteAllBytes(modelPath, modelBytes);

                var manifestArtifact = new ModelArtifactDocument(
                    "onnx.cpu", "onnx", ModelArtifactLocationKind.File, "classifier.onnx", new[] { "fake-onnx" },
                    new[] { new ModelFileDocument("classifier.onnx", hash, modelBytes.LongLength, "application/octet-stream", ModelFileRole.Model) },
                    precision: "fp32", opset: 17, portable: true);
                var manifest = new ModelPackageDocument(
                    "2.0", "tests/supply-chain-classifier", "Supply-chain classifier", "vision", "image-classification", "1.0",
                    new ModelExporterDocument("DeploySharp.Tests", "2.0.0", "fixture"),
                    new ModelSourceDocument("https://example.com/model", "https://example.com/project", "fixture", "DeploySharp", null, "Apache-2.0", null, true),
                    DateTimeOffset.Parse("2026-08-04T00:00:00Z"), "tests/classification.v1",
                    new[] { new ModelTensorSignatureDocument("images", "float32", new long[] { 1, 3, 2, 2 }) },
                    new[] { new ModelTensorSignatureDocument("scores", "float32", new long[] { 1, 3 }) },
                    new[] { manifestArtifact });
                string manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(manifest)));

                LocalModelPackage local = ModelPackageLoader.Load(manifestPath);
                ModelArtifact artifact = local.ToCoreArtifacts()[0];

                var release = new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260804.1", "0123456789abcdef");
                var catalogEntry = new ModelCatalogEntry(
                    "tests/supply-chain-classifier", "Supply-chain classifier", "vision", "image-classification", "1.0",
                    ModelCatalogStatus.Preview, "offline contract fixture",
                    new ModelSourceDocument("https://example.com/model", "https://example.com/project", "fixture", "DeploySharp", null, "Apache-2.0", null, true),
                    release,
                    new[] { new ModelCatalogArtifact("onnx.cpu", "onnx", new[] { "fake-onnx" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()) },
                    Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-supply-chain-classifier.md");
                ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                    "1.0", "2026-08-04T00:00:00Z", "tests.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { catalogEntry }));
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: artifact.ModelId.Value, format: "onnx", backend: "fake-onnx", includePreview: true)).Count);

                var provider = new FakeVisualBackendProvider(
                    new ModelMetadata(artifact.ModelId, "onnx", new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)) }, new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, 3)) }),
                    _ => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 0.1f, 0.8f, 0.1f })),
                    format: "onnx", backendId: new BackendId("fake-onnx"));
                using (var backends = new BackendRegistry())
                {
                    backends.Register(provider);
                    var profiles = new VisualProfileRegistry();
                    profiles.Register(new VisualModelProfile(
                        "tests/classification.v1", artifact.ModelId, VisualTaskId.ImageClassification, "1.0", "onnx",
                        new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
                        new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
                        new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
                        new ClassificationDecoder("scores")));
                    profiles.Freeze();
                    VisualProfileSelection selection = profiles.Select(artifact, backends, new BackendRequest(BackendCapabilities.TensorInference, new BackendId("fake-onnx")), VisualTaskId.ImageClassification);
                    Assert.AreEqual("tests/classification.v1", selection.Profile.ProfileId);
                    Assert.AreEqual("fake-onnx", selection.Backend.Id.Value);
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
