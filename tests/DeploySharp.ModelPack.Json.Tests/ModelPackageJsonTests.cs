using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.ModelPack.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class ModelPackageJsonTests
    {
        [TestMethod]
        public void DeterministicRoundTripSortsExtensionsAndPreservesCoreConversion()
        {
            byte[] bytes = new byte[] { 1, 2, 3, 4 };
            ValidatedModelPackage package = ModelPackageValidator.Validate(ModelPackageTestFactory.Document(ModelPackageTestFactory.FileArtifact("onnx.cpu", "model.onnx", bytes)));
            string first = ModelPackageJsonSerializer.Serialize(package);
            string second = ModelPackageJsonSerializer.Serialize(ModelPackageJsonSerializer.Deserialize(first));

            Assert.AreEqual(first, second);
            Assert.IsTrue(first.IndexOf("a-extension", StringComparison.Ordinal) < first.IndexOf("z-extension", StringComparison.Ordinal));
            Assert.AreEqual("2.0", ModelPackageSchema.Version);
            using (JsonDocument schema = JsonDocument.Parse(ModelPackageSchema.GetJson())) Assert.AreEqual(JsonValueKind.Object, schema.RootElement.ValueKind);
            Assert.AreEqual("tests/model-pack", package.ModelId.Value);
            IDictionary<string, string> extensions = (IDictionary<string, string>)package.Document.Extensions;
            Assert.ThrowsExactly<NotSupportedException>(() => extensions.Add("mutate", "blocked"));
        }

        [TestMethod]
        public void BundledSchemaMatchesValidatorVersionLimitsAndWireEnums()
        {
            using JsonDocument schema = JsonDocument.Parse(ModelPackageSchema.GetJson());
            JsonElement root = schema.RootElement;
            Assert.IsFalse(root.GetProperty("additionalProperties").GetBoolean());
            string[] required = root.GetProperty("required").EnumerateArray().Select(value => value.GetString()!).ToArray();
            CollectionAssert.AreEquivalent(new[] { "schemaVersion", "modelId", "name", "family", "task", "modelVersion", "exporter", "source", "inputs", "outputs", "artifacts" }, required);

            JsonElement definitions = root.GetProperty("$defs");
            Assert.AreEqual(ModelPackageValidationOptions.Default.MaximumArtifacts, root.GetProperty("properties").GetProperty("artifacts").GetProperty("maxItems").GetInt32());
            Assert.AreEqual(ModelPackageValidationOptions.Default.MaximumTensors, root.GetProperty("properties").GetProperty("inputs").GetProperty("maxItems").GetInt32());
            string[] locations = definitions.GetProperty("artifact").GetProperty("properties").GetProperty("locationKind").GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToArray();
            CollectionAssert.AreEquivalent(new[] { "file", "directory" }, locations);
            string[] roles = definitions.GetProperty("file").GetProperty("properties").GetProperty("role").GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToArray();
            CollectionAssert.AreEquivalent(new[] { "model", "weights", "externalData", "labels", "vocabulary", "tokenizer", "chatTemplate", "configuration", "license", "testInput", "other" }, roles);
        }

        [TestMethod]
        public void StreamAndAsyncStreamRoundTripWithoutTakingOwnership()
        {
            byte[] bytes = new byte[] { 5, 6, 7 };
            ValidatedModelPackage package = ModelPackageValidator.Validate(ModelPackageTestFactory.Document(ModelPackageTestFactory.FileArtifact("gguf.cpu", "model.gguf", bytes, "gguf", "llama-sharp")));
            using (var output = new MemoryStream())
            {
                ModelPackageJsonSerializer.Serialize(output, package);
                Assert.IsTrue(output.CanWrite);
                output.Position = 0;
                ValidatedModelPackage loaded = ModelPackageJsonSerializer.Deserialize(output);
                Assert.AreEqual(package.ModelId, loaded.ModelId);
            }

            using (var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ModelPackageJsonSerializer.Serialize(package))))
            {
                ValidatedModelPackage loaded = ModelPackageJsonSerializer.DeserializeAsync(input).GetAwaiter().GetResult();
                Assert.AreEqual("gguf.cpu", loaded.Document.Artifacts[0].ArtifactId);
            }
        }

        [TestMethod]
        public void RejectsUnknownDuplicateMissingAndUnsupportedValues()
        {
            string json = ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(ModelPackageTestFactory.Document(ModelPackageTestFactory.FileArtifact("onnx.cpu", "model.onnx", new byte[] { 1 }))));
            Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageJsonSerializer.Deserialize(json.Replace("\"modelId\": \"tests/model-pack\"", "\"modelId\": \"tests/model-pack\", \"unexpected\": true")));
            Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageJsonSerializer.Deserialize(json.Replace("\"modelId\": \"tests/model-pack\"", "\"modelId\": \"tests/model-pack\", \"modelId\": \"duplicate\"")));
            Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageJsonSerializer.Deserialize(json.Replace("\"inputs\": [", "\"omittedInputs\": [")));
            Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageJsonSerializer.Deserialize(json.Replace("\"locationKind\": \"file\"", "\"locationKind\": \"invalid\"")));
            Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageJsonSerializer.Deserialize(json.Replace("\"schemaVersion\": \"2.0\"", "\"schemaVersion\": \"3.0\"")));
            Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageJsonSerializer.Deserialize(json.Replace("\"portable\": true", "\"omittedPortable\": true")));
            Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageJsonSerializer.Deserialize(json.Replace("\"redistributionAllowed\": true", "\"omittedRedistributionAllowed\": true")));

            ModelPackageValidationException syntax = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageJsonSerializer.Deserialize("{ invalid"));
            Assert.IsNotNull(syntax.InnerException);
            Assert.IsFalse(string.IsNullOrWhiteSpace(syntax.TechnicalDetails));
            Assert.IsTrue(syntax.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.InvalidJson));
        }

        [TestMethod]
        public void AcceptsNewerMinorOnlyAccordingToOption()
        {
            string json = ModelPackageJsonSerializer.Serialize(ModelPackageValidator.Validate(ModelPackageTestFactory.Document(ModelPackageTestFactory.FileArtifact("onnx.cpu", "model.onnx", new byte[] { 1 })))).Replace("\"schemaVersion\": \"2.0\"", "\"schemaVersion\": \"2.1\"");
            Assert.IsNotNull(ModelPackageJsonSerializer.Deserialize(json));
            var options = new ModelPackageValidationOptions(allowNewerMinorVersions: false);
            ModelPackageValidationException exception = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageJsonSerializer.Deserialize(json, options));
            Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.InvalidVersion));
        }

        [TestMethod]
        public void RejectsUnsafePortablePathsAndDuplicateArtifacts()
        {
            string[] unsafePaths = { "../model.onnx", "..\\model.onnx", "/model.onnx", "C:\\model.onnx", "//server/share/model.onnx", "a/./model.onnx", "a//model.onnx", "CON.txt" };
            foreach (string path in unsafePaths) Assert.ThrowsExactly<ArgumentException>(() => ModelPackagePath.NormalizeRelativePath(path));
            Assert.AreEqual("a/b/model.onnx", ModelPackagePath.NormalizeRelativePath("a\\b/model.onnx"));

            byte[] bytes = new byte[] { 1 };
            ModelArtifactDocument first = ModelPackageTestFactory.FileArtifact("onnx.a", "a.onnx", bytes);
            ModelArtifactDocument second = ModelPackageTestFactory.FileArtifact("onnx.b", "a.onnx", bytes);
            ModelPackageValidationException exception = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageValidator.Validate(ModelPackageTestFactory.Document(first, second)));
            Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.Duplicate));
        }

        [TestMethod]
        public void ValidatesSourceLicenseHashAndUnicodePathMetadata()
        {
            byte[] bytes = new byte[] { 2, 4, 6 };
            ModelArtifactDocument unicode = ModelPackageTestFactory.FileArtifact("onnx.unicode", "模型/分类.onnx", bytes);
            ValidatedModelPackage valid = ModelPackageValidator.Validate(ModelPackageTestFactory.Document(unicode));
            Assert.AreEqual("模型/分类.onnx", valid.Document.Artifacts[0].Entrypoint);

            var invalidHashFile = new ModelFileDocument("model.onnx", "not-a-hash", bytes.Length, null, ModelFileRole.Model);
            var invalidHashArtifact = new ModelArtifactDocument("onnx.bad", "onnx", ModelArtifactLocationKind.File, "model.onnx", new[] { "onnxruntime" }, new[] { invalidHashFile });
            ModelPackageValidationException hashException = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageValidator.Validate(ModelPackageTestFactory.Document(invalidHashArtifact)));
            Assert.IsTrue(hashException.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.InvalidHash && diagnostic.FilePath == "model.onnx"));

            ModelPackageDocument noLicense = new ModelPackageDocument(
                "2.0", "tests/no-license", "No License", "test", "inference", "1.0",
                new ModelExporterDocument("tests", "1.0"),
                new ModelSourceDocument("https://example.com/source", null, "main", "DeploySharp", null, null, null, false),
                null, null, Array.Empty<ModelTensorSignatureDocument>(), Array.Empty<ModelTensorSignatureDocument>(), new[] { ModelPackageTestFactory.FileArtifact("onnx.cpu", "model.onnx", bytes) });
            ModelPackageValidationException licenseException = Assert.ThrowsExactly<ModelPackageValidationException>(() => ModelPackageValidator.Validate(noLicense));
            Assert.IsTrue(licenseException.Diagnostics.Any(diagnostic => diagnostic.Code == ModelPackageDiagnosticCodes.Required && diagnostic.JsonPath == "$.source"));
        }
    }
}
