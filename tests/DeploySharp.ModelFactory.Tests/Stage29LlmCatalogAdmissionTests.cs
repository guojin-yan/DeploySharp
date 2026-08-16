using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class Stage29LlmCatalogAdmissionTests
    {
        [TestMethod]
        public void ExternalGgufCandidateDoesNotOverridePublishedCatalog()
        {
            string externalManifestPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "qwen2.5-0.5b-instruct-q4-k-m.external.modelpack.json");
            using JsonDocument externalManifest = JsonDocument.Parse(File.ReadAllText(externalManifestPath));
            Assert.AreEqual("llm/qwen2.5-0.5b-instruct-q4-k-m/external", externalManifest.RootElement.GetProperty("modelId").GetString());
            Assert.IsFalse(externalManifest.RootElement.GetProperty("source").GetProperty("redistributionAllowed").GetBoolean());

            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            ModelCatalogEntry published = catalog.Document.Entries.Single(entry => entry.ModelId == "llm/qwen2.5-0.5b-instruct-q4-k-m");
            Assert.IsTrue(published.Source!.RedistributionAllowed);
            Assert.IsNotNull(published.Release);
            Assert.IsTrue(catalog.Document.SourceRepository != null);
            Assert.AreEqual("1.0", catalog.Document.SchemaVersion);
        }
    }
}
