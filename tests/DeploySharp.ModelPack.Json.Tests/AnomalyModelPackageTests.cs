using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class AnomalyModelPackageTests
    {
        private string _root = string.Empty;

        [TestInitialize]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "deploysharp-anomaly-modelpack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestMethod]
        public void LoadsIntegrityProtectedPortableOnnxAndOpenVinoIrArtifacts()
        {
            Copy("onnx/anomaly-detection.onnx");
            Copy("ir/anomaly-detection.xml");
            Copy("ir/anomaly-detection.bin");
            string manifestPath = Path.Combine(_root, "manifest.json");
            File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(Document())));

            LocalModelPackage package = ModelPackageLoader.Load(manifestPath);

            Assert.AreEqual(2, package.Artifacts.Count);
            Assert.AreEqual(3, package.Artifacts.Sum(artifact => artifact.Files.Count));
            CollectionAssert.AreEqual(new[] { "onnx", "openvino-ir" }, package.ToCoreArtifacts().Select(artifact => artifact.Format).ToArray());
            Assert.AreEqual("anomaly-map-v1", package.Manifest.Document.Extensions["deploysharp.anomaly.contract"]);
        }

        [TestMethod]
        public void RejectsMissingOrModifiedOpenVinoIrSidecar()
        {
            Copy("onnx/anomaly-detection.onnx");
            Copy("ir/anomaly-detection.xml");
            Copy("ir/anomaly-detection.bin");
            string manifestPath = Path.Combine(_root, "manifest.json");
            File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(Document())));

            File.Delete(Path.Combine(_root, "ir", "anomaly-detection.bin"));
            ModelPackageValidationException missing = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageLoader.Load(manifestPath));
            Assert.IsTrue(missing.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.FileNotFound));

            Copy("ir/anomaly-detection.bin");
            File.AppendAllText(Path.Combine(_root, "ir", "anomaly-detection.bin"), "modified");
            ModelPackageValidationException modified = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageLoader.Load(manifestPath));
            Assert.IsTrue(modified.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.IntegrityMismatch));
        }

        private ModelPackageDocument Document()
        {
            var artifacts = new[]
            {
                new ModelArtifactDocument(
                    "anomaly.onnx", "onnx", ModelArtifactLocationKind.File, "onnx/anomaly-detection.onnx",
                    new[] { "onnxruntime", "openvino" },
                    new[] { FileDocument("onnx/anomaly-detection.onnx", "application/onnx", ModelFileRole.Model) },
                    precision: "fp32", opset: 13, portable: true),
                new ModelArtifactDocument(
                    "anomaly.openvino-ir", "openvino-ir", ModelArtifactLocationKind.File, "ir/anomaly-detection.xml",
                    new[] { "openvino" },
                    new[]
                    {
                        FileDocument("ir/anomaly-detection.xml", "application/xml", ModelFileRole.Model),
                        FileDocument("ir/anomaly-detection.bin", "application/octet-stream", ModelFileRole.Weights)
                    },
                    precision: "fp32", portable: true, minimumRuntimeVersion: "2026.2.1")
            };
            return new ModelPackageDocument(
                "2.0", "tests/anomaly-detection", "DeploySharp anomaly detection contract fixture", "deploysharp-fixture", "anomaly-detection", "1.0",
                new ModelExporterDocument("DeploySharp fixtures", "1.0", "eng/test-models"),
                new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                DateTimeOffset.Parse("2026-08-06T00:00:00Z"), "tests/anomaly-detection.v1",
                new[] { new ModelTensorSignatureDocument("images", "float32", new long[] { 1,3,3,5 }) },
                new[]
                {
                    new ModelTensorSignatureDocument("image_score", "float32", new long[] { 1 }),
                    new ModelTensorSignatureDocument("anomaly_map", "float32", new long[] { 1,2,3,5 })
                },
                artifacts,
                new[]
                {
                    Pair("deploysharp.anomaly.contract", "anomaly-map-v1"),
                    Pair("deploysharp.anomaly.score-output", "image_score"),
                    Pair("deploysharp.anomaly.map-output", "anomaly_map"),
                    Pair("deploysharp.anomaly.map-semantics", "probability")
                });
        }

        private ModelFileDocument FileDocument(string relativePath, string mediaType, ModelFileRole role)
        {
            string path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return new ModelFileDocument(relativePath, Hash(path), new FileInfo(path).Length, mediaType, role);
        }

        private void Copy(string relativePath)
        {
            string source = Path.Combine(AppContext.BaseDirectory, "fixtures", relativePath.Replace('/', Path.DirectorySeparatorChar));
            string destination = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
        }

        private static KeyValuePair<string, string> Pair(string key, string value) => new KeyValuePair<string, string>(key, value);

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            return ModelPackageTestFactory.ToLowerHex(sha.ComputeHash(stream));
        }
    }
}
