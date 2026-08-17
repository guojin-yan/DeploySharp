using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class OfficialQwenCatalogTests
    {
        [TestMethod]
        public void BundledCatalogSelectsThePublishedQwenPreview()
        {
            ValidatedModelCatalog catalog = LoadOfficialCatalog();
            Assert.AreEqual("models-20260817.detectors.2", catalog.CatalogRevision);
            Assert.AreEqual(33, catalog.Document.Entries.Count);

            ModelCatalogEntry entry = catalog.Document.Entries.Single(value => value.ModelId == "llm/qwen2.5-0.5b-instruct-q4-k-m");
            Assert.AreEqual("llm/qwen2.5-0.5b-instruct-q4-k-m", entry.ModelId);
            Assert.AreEqual(ModelCatalogStatus.Preview, entry.Status);
            Assert.IsTrue(entry.Source!.RedistributionAllowed);
            Assert.AreEqual("Apache-2.0", entry.Source.LicenseExpression);
            Assert.AreEqual("models-20260817.qwen2.5-0.5b-instruct-q4-k-m.1", entry.Release!.Tag);
            Assert.AreEqual("d8c4ffaed3684d120f80dec832c74a1a83e562a5", entry.Release.Commit);

            ModelCatalogArtifact artifact = entry.Artifacts.Single();
            Assert.AreEqual("qwen-modelpack", artifact.ManifestAssetId);
            Assert.AreEqual(7, artifact.Assets.Count);
            Assert.AreEqual(491400032L, artifact.Assets.Single(asset => asset.AssetId == "qwen-model").Size);
            Assert.AreEqual("74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db", artifact.Assets.Single(asset => asset.AssetId == "qwen-model").Sha256);
            Assert.IsFalse(ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: entry.ModelId)).Any());
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: entry.ModelId, backend: "llamasharp", format: "gguf", includePreview: true)).Count);

            string manifestPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "qwen2.5-0.5b-instruct-q4-k-m.public.modelpack.json");
            ValidatedModelPackage manifest = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(manifestPath));
            Assert.AreEqual(entry.ModelId, manifest.ModelId.Value);
            Assert.IsNotNull(manifest.Document.Source);
            Assert.IsTrue(manifest.Document.Source!.RedistributionAllowed);
            Assert.AreEqual("Apache-2.0", manifest.Document.Source.LicenseExpression);
            Assert.AreEqual("74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db", manifest.Document.Artifacts.Single().Files.Single(file => file.Role == ModelFileRole.Model).Sha256);
        }

        private static ValidatedModelCatalog LoadOfficialCatalog()
        {
            try
            {
                return OfficialModelCatalog.Load();
            }
            catch (ModelFactoryException exception)
            {
                Assert.Fail(string.Join(Environment.NewLine, exception.Diagnostics.Select(diagnostic => diagnostic.Code + " " + diagnostic.JsonPath + " " + diagnostic.Message)));
                throw;
            }
        }
    }
}
