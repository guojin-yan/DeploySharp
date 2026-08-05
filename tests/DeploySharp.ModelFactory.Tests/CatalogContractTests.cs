using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class CatalogContractTests
    {
        [TestMethod]
        public void DeterministicRoundTripAndBundledResourcesAreValid()
        {
            var fixture = new CatalogFixture();
            string first = ModelCatalogJsonSerializer.Serialize(fixture.Catalog);
            string second = ModelCatalogJsonSerializer.Serialize(ModelCatalogJsonSerializer.Deserialize(first));
            Assert.AreEqual(first, second);
            Assert.AreEqual("1.0", ModelCatalogSchema.Version);
            using (JsonDocument schema = JsonDocument.Parse(ModelCatalogSchema.GetJson())) Assert.IsFalse(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
            ValidatedModelCatalog official = OfficialModelCatalog.Load();
            Assert.AreEqual("bootstrap.1", official.CatalogRevision);
            Assert.AreEqual(0, official.Document.Entries.Count);
        }

        [TestMethod]
        public void SchemaEnumsAndLimitsMatchManagedValidator()
        {
            using JsonDocument schema = JsonDocument.Parse(ModelCatalogSchema.GetJson());
            JsonElement root = schema.RootElement;
            Assert.AreEqual(ModelCatalogValidationOptions.Default.MaximumEntries, root.GetProperty("properties").GetProperty("entries").GetProperty("maxItems").GetInt32());
            JsonElement definitions = root.GetProperty("$defs");
            string[] statuses = definitions.GetProperty("entry").GetProperty("properties").GetProperty("status").GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToArray();
            CollectionAssert.AreEquivalent(new[] { "supported", "preview", "external" }, statuses);
            string[] kinds = definitions.GetProperty("asset").GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToArray();
            CollectionAssert.AreEquivalent(new[] { "manifest", "model", "testInput", "testExpected", "license", "other" }, kinds);
        }

        [TestMethod]
        public void RejectsUnknownDuplicateMissingVersionAndMutableReleaseFields()
        {
            string json = ModelCatalogJsonSerializer.Serialize(new CatalogFixture().Catalog);
            Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogJsonSerializer.Deserialize(json.Replace("\"catalogRevision\": \"tests.1\"", "\"catalogRevision\": \"tests.1\", \"unknown\": true")));
            Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogJsonSerializer.Deserialize(json.Replace("\"catalogRevision\": \"tests.1\"", "\"catalogRevision\": \"tests.1\", \"catalogRevision\": \"duplicate\"")));
            Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogJsonSerializer.Deserialize(json.Replace("\"portable\": true", "\"omittedPortable\": true")));
            Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogJsonSerializer.Deserialize(json.Replace("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"2.0\"")));
            ModelFactoryException missingExpected = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogJsonSerializer.Deserialize(json.Replace("\"expectedResultAssetId\": \"expected\",", string.Empty)));
            Assert.IsTrue(missingExpected.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.AdmissionRejected));
            ModelFactoryException mutable = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogJsonSerializer.Deserialize(json.Replace(CatalogFixture.Tag, "latest")));
            Assert.IsTrue(mutable.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.MutableReleaseTag));
        }

        [TestMethod]
        public void QueryIsDeterministicAndReportsNoMatch()
        {
            var fixture = new CatalogFixture();
            IReadOnlyList<ModelSelection> matches = ModelCatalogQuery.Select(fixture.Catalog, new ModelQuery(modelId: CatalogFixture.ModelId, backend: "llama-sharp", format: "gguf", precision: "fp16"));
            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(CatalogFixture.ArtifactId, matches[0].Artifact.ArtifactId);
            using var directory = new TestDirectory();
            using var factory = new ModelFactoryClient(fixture.Catalog, new ModelFactoryOptions(directory.Path, offline: true), new System.Net.Http.HttpClient(new ScriptedHttpHandler(fixture.Responses())));
            ModelFactoryException exception = Assert.ThrowsExactly<ModelFactoryException>(() => factory.Select(new ModelQuery(modelId: "missing/model")));
            Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.NoMatch));
        }

        [TestMethod]
        public void AdmissionAllowsOnlyVerifiedPortableSupportedFormats()
        {
            Assert.IsNotNull(new CatalogFixture(ModelCatalogStatus.Supported, "gguf", "llama-sharp", true).Catalog);
            Assert.IsNotNull(new CatalogFixture(ModelCatalogStatus.Supported, "onnx", "onnxruntime", true).Catalog);
            ModelFactoryException openVino = Assert.ThrowsExactly<ModelFactoryException>(() => new CatalogFixture(ModelCatalogStatus.Supported, "openvino", "openvino", true));
            Assert.IsTrue(openVino.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.AdmissionRejected));
            Assert.IsNotNull(new CatalogFixture(ModelCatalogStatus.Preview, "onnx", "onnxruntime", true).Catalog);
            Assert.IsNotNull(new CatalogFixture(ModelCatalogStatus.Preview, "openvino", "openvino", true).Catalog);
            Assert.IsNotNull(new CatalogFixture(ModelCatalogStatus.External, "tensorrt", "tensorrt", false, "models/model.engine").Catalog);
            Assert.ThrowsExactly<ModelFactoryException>(() => new CatalogFixture(ModelCatalogStatus.Preview, "tensorrt", "tensorrt", false, "models/model.plan"));
        }

        [TestMethod]
        public void OfflinePreviewObbCatalogSelectsPortableOnnxAndOpenVinoIrBackends()
        {
            var source = new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", "https://github.com/guojin-yan/DeploySharp", "generated", "JYPPX", null, "Apache-2.0", null, true);
            var release = new ModelCatalogRelease("guojin-yan", "DeploySharp", "models-20260805.1", "0123456789abcdef");
            var entry = new ModelCatalogEntry(
                "tests/obb", "DeploySharp OBB contract fixture", "deploysharp-fixture", "oriented-object-detection", "1.0", ModelCatalogStatus.Preview,
                "Offline contract fixture; not an algorithm-verified model.", source, release,
                new[]
                {
                    new ModelCatalogArtifact("onnx.cpu", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>()),
                    new ModelCatalogArtifact("openvino-ir.cpu", "openvino-ir", new[] { "openvino" }, "fp32", null, true, null, Array.Empty<ModelCatalogAsset>())
                }, Array.Empty<ModelCatalogAsset>(), documentationPath: "models/tests-local-only.md");
            ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                "1.0", "2026-08-05T00:00:00Z", "tests-obb.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));

            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "oriented-object-detection", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "oriented-object-detection", format: "onnx", backend: "openvino", includePreview: true)).Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "oriented-object-detection", format: "openvino-ir", backend: "openvino", includePreview: true)).Count);
            Assert.AreEqual(0, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "oriented-object-detection", format: "openvino-ir", backend: "onnxruntime", includePreview: true)).Count);
            Assert.AreEqual(0, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "oriented-object-detection", includePreview: false)).Count);
        }

        [TestMethod]
        public void RejectsUnsafePathsHashesLicensesAndReleaseUrls()
        {
            string[] unsafePaths = { "../model.gguf", "C:\\model.gguf", "//server/share/model.gguf", "a/./model.gguf", "a\\..\\model.gguf", "a\0model.gguf", "CON.gguf" };
            foreach (string unsafePath in unsafePaths)
            {
                ModelFactoryException exception = Assert.ThrowsExactly<ModelFactoryException>(() => new CatalogFixture(catalogModelPath: unsafePath));
                Assert.IsTrue(exception.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.AssetInvalid));
            }

            var fixture = new CatalogFixture();
            ModelCatalogEntry entry = fixture.Catalog.Document.Entries[0];
            ModelCatalogArtifact original = entry.Artifacts[0];
            ModelCatalogAsset badHash = new ModelCatalogAsset("bad", ModelCatalogAssetKind.Model, CatalogFixture.Tag, new Uri("https://github.com/guojin-yan/DeploySharp/releases/download/" + CatalogFixture.Tag + "/bad.gguf"), "models/bad.gguf", 1, "bad", null, null);
            var artifact = new ModelCatalogArtifact("bad.artifact", "gguf", new[] { "llama-sharp" }, "fp16", null, true, "bad", new[] { badHash }, new ModelCatalogConversion("test", "1", "abc", null));
            var invalidEntry = new ModelCatalogEntry("tests/bad", "Bad", "llama", "text-generation", "1", ModelCatalogStatus.Supported, null, new ModelSourceDocument("https://example.com", null, "abc", "Test", null, null, null, false), entry.Release, new[] { artifact }, Array.Empty<ModelCatalogAsset>());
            var document = new ModelCatalogDocument("1.0", "2026-08-04T00:00:00Z", "bad.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { invalidEntry });
            ModelFactoryException invalid = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogValidator.Validate(document));
            Assert.IsTrue(invalid.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.LicenseRejected));
            Assert.IsTrue(invalid.Diagnostics.Any(diagnostic => diagnostic.Code == ModelFactoryDiagnosticCodes.AssetInvalid));
        }

        [TestMethod]
        public void SupportedSupplyChainEntriesHaveManifestModelTestLicenseAndDocumentation()
        {
            var fixture = new CatalogFixture();
            foreach (ModelCatalogEntry entry in fixture.Catalog.Document.Entries.Where(value => value.Status == ModelCatalogStatus.Supported))
            {
                Assert.IsTrue(entry.Source!.RedistributionAllowed);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Source.LicenseExpression));
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.DocumentationPath));
                Assert.IsTrue(entry.TestInputs.Any(asset => asset.Kind == ModelCatalogAssetKind.TestInput));
                Assert.IsTrue(entry.TestInputs.Any(asset => asset.Kind == ModelCatalogAssetKind.TestExpected && asset.AssetId == entry.ExpectedResultAssetId));
                foreach (ModelCatalogArtifact artifact in entry.Artifacts)
                {
                    Assert.IsTrue(artifact.Portable);
                    Assert.IsTrue(artifact.Assets.Any(asset => asset.Kind == ModelCatalogAssetKind.Manifest));
                    Assert.IsTrue(artifact.Assets.Any(asset => asset.Kind == ModelCatalogAssetKind.Model));
                }
            }
        }
    }
}
