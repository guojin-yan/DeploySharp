using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class OcrModelPackageTests
    {
        private string _root = string.Empty;

        [TestInitialize]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "deploysharp-ocr-modelpack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestMethod]
        public void LoadsIntegrityProtectedDetectorRecognizerOnnxIrAndCharacterSetSuite()
        {
            Copy("onnx/text-detection.onnx");
            Copy("onnx/text-recognition-ctc.onnx");
            Copy("ir/text-detection.xml");
            Copy("ir/text-detection.bin");
            Copy("ir/text-recognition-ctc.xml");
            Copy("ir/text-recognition-ctc.bin");
            Copy("ocr/charset.txt");
            string manifestPath = Path.Combine(_root, "manifest.json");
            File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(Document())));

            LocalModelPackage package = ModelPackageLoader.Load(manifestPath);

            Assert.AreEqual(4, package.Artifacts.Count);
            Assert.AreEqual(7, package.Artifacts.Sum(artifact => artifact.Files.Count));
            Assert.AreEqual("ocr.detector.onnx", package.Manifest.Document.Extensions["deploysharp.ocr.detector.onnx-artifact"]);
            Assert.AreEqual("ocr.recognizer.openvino-ir", package.Manifest.Document.Extensions["deploysharp.ocr.recognizer.openvino-ir-artifact"]);
            Assert.AreEqual("tests.latin", package.Manifest.Document.Extensions["deploysharp.ocr.character-set-id"]);
            Assert.AreEqual(Hash(Path.Combine(_root, "ocr", "charset.txt")), package.Manifest.Document.Extensions["deploysharp.ocr.character-set-sha256"]);
            CollectionAssert.AreEqual(new[] { "onnx", "onnx", "openvino-ir", "openvino-ir" }, package.ToCoreArtifacts().Select(artifact => artifact.Format).ToArray());
        }

        [TestMethod]
        public void RejectsMissingCharacterSetAndIrSidecar()
        {
            Copy("onnx/text-detection.onnx");
            Copy("onnx/text-recognition-ctc.onnx");
            Copy("ir/text-detection.xml");
            Copy("ir/text-detection.bin");
            Copy("ir/text-recognition-ctc.xml");
            Copy("ir/text-recognition-ctc.bin");
            Copy("ocr/charset.txt");
            string manifestPath = Path.Combine(_root, "manifest.json");
            File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(Document())));

            File.Delete(Path.Combine(_root, "ocr", "charset.txt"));
            ModelPackageValidationException missingCharset = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageLoader.Load(manifestPath));
            Assert.IsTrue(missingCharset.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.FileNotFound));

            Copy("ocr/charset.txt");
            File.Delete(Path.Combine(_root, "ir", "text-recognition-ctc.bin"));
            ModelPackageValidationException missingWeights = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageLoader.Load(manifestPath));
            Assert.IsTrue(missingWeights.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.FileNotFound));
        }

        private ModelPackageDocument Document()
        {
            string charsetHash = Hash(Path.Combine(_root, "ocr", "charset.txt"));
            var charset = FileDocument("ocr/charset.txt", "text/plain", ModelFileRole.Vocabulary);
            var artifacts = new[]
            {
                new ModelArtifactDocument("ocr.detector.onnx", "onnx", ModelArtifactLocationKind.File, "onnx/text-detection.onnx", new[] { "onnxruntime", "openvino" }, new[] { FileDocument("onnx/text-detection.onnx", "application/onnx", ModelFileRole.Model) }, precision: "fp32", opset: 13, portable: true, extensions: Role("detector")),
                new ModelArtifactDocument("ocr.recognizer.onnx", "onnx", ModelArtifactLocationKind.File, "onnx/text-recognition-ctc.onnx", new[] { "onnxruntime", "openvino" }, new[] { FileDocument("onnx/text-recognition-ctc.onnx", "application/onnx", ModelFileRole.Model), charset }, precision: "fp32", opset: 13, portable: true, extensions: Role("recognizer")),
                new ModelArtifactDocument("ocr.detector.openvino-ir", "openvino-ir", ModelArtifactLocationKind.File, "ir/text-detection.xml", new[] { "openvino" }, new[] { FileDocument("ir/text-detection.xml", "application/xml", ModelFileRole.Model), FileDocument("ir/text-detection.bin", "application/octet-stream", ModelFileRole.Weights) }, precision: "fp32", portable: true, minimumRuntimeVersion: "2026.2.1", extensions: Role("detector")),
                new ModelArtifactDocument("ocr.recognizer.openvino-ir", "openvino-ir", ModelArtifactLocationKind.File, "ir/text-recognition-ctc.xml", new[] { "openvino" }, new[] { FileDocument("ir/text-recognition-ctc.xml", "application/xml", ModelFileRole.Model), FileDocument("ir/text-recognition-ctc.bin", "application/octet-stream", ModelFileRole.Weights) }, precision: "fp32", portable: true, minimumRuntimeVersion: "2026.2.1", extensions: Role("recognizer"))
            };
            return new ModelPackageDocument(
                "2.0", "tests/ocr-suite", "DeploySharp OCR contract suite", "deploysharp-fixture", "optical-character-recognition", "1.0",
                new ModelExporterDocument("DeploySharp fixtures", "1.0", "eng/test-models"),
                new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true),
                DateTimeOffset.Parse("2026-08-05T00:00:00Z"), "tests/ocr-suite.v1",
                new[]
                {
                    new ModelTensorSignatureDocument("images", "float32", new long[] { 1,3,16,32 }),
                    new ModelTensorSignatureDocument("crops", "float32", new long[] { 2,3,8,16 })
                },
                new[]
                {
                    new ModelTensorSignatureDocument("polygons", "float32", new long[] { 1,3,4,2 }),
                    new ModelTensorSignatureDocument("scores", "float32", new long[] { 1,3 }),
                    new ModelTensorSignatureDocument("logits", "float32", new long[] { 2,6,4 })
                }, artifacts,
                new[]
                {
                    Pair("deploysharp.ocr.contract-version", "1"),
                    Pair("deploysharp.ocr.detector.onnx-artifact", "ocr.detector.onnx"),
                    Pair("deploysharp.ocr.recognizer.onnx-artifact", "ocr.recognizer.onnx"),
                    Pair("deploysharp.ocr.detector.openvino-ir-artifact", "ocr.detector.openvino-ir"),
                    Pair("deploysharp.ocr.recognizer.openvino-ir-artifact", "ocr.recognizer.openvino-ir"),
                    Pair("deploysharp.ocr.detector-profile", "tests/ocr-detector.v1"),
                    Pair("deploysharp.ocr.recognizer-profile", "tests/ocr-recognizer.v1"),
                    Pair("deploysharp.ocr.character-set", "ocr/charset.txt"),
                    Pair("deploysharp.ocr.character-set-id", "tests.latin"),
                    Pair("deploysharp.ocr.character-set-version", "1.0"),
                    Pair("deploysharp.ocr.character-set-sha256", charsetHash),
                    Pair("deploysharp.ocr.language", "und"),
                    Pair("deploysharp.ocr.script", "Latn"),
                    Pair("deploysharp.ocr.preprocess-version", "1"),
                    Pair("deploysharp.ocr.postprocess-version", "1")
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

        private static IEnumerable<KeyValuePair<string, string>> Role(string value) => new[] { Pair("deploysharp.ocr.role", value) };
        private static KeyValuePair<string, string> Pair(string key, string value) => new KeyValuePair<string, string>(key, value);
        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            return ModelPackageTestFactory.ToLowerHex(sha.ComputeHash(stream));
        }
    }
}
