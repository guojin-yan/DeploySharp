using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class PortableDetectorCatalogAdmissionTests
    {
        [TestMethod]
        public void OfflineExternalMatrixIsQueryableButCannotEnterOfficialCatalog()
        {
            using JsonDocument support = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "portable-detector-support.json")));
            var entries = new List<ModelCatalogEntry>();
            foreach (JsonElement model in support.RootElement.GetProperty("models").EnumerateArray())
            {
                string family = model.GetProperty("family").GetString()!;
                string task = model.GetProperty("task").GetString()!;
                string format = model.GetProperty("format").GetString()!;
                string[] backends = model.GetProperty("verifiedBackends").EnumerateArray().Select(value => value.GetString()!).ToArray();
                var source = new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", null, "external-local-artifact", "Upstream maintainers", null, "NOASSERTION", null, false);
                var artifact = new ModelCatalogArtifact("external.fp32", format, backends.Length == 0 ? new[] { "unverified" } : backends, "fp32", "none", true, null, Array.Empty<ModelCatalogAsset>(), new ModelCatalogConversion("audited-upstream-export", null, null, model.GetProperty("blocker").GetString()));
                entries.Add(new ModelCatalogEntry(model.GetProperty("modelId").GetString(), family + " external candidate", family, task, "external", ModelCatalogStatus.External, model.GetProperty("blocker").GetString(), source, null, new[] { artifact }, Array.Empty<ModelCatalogAsset>(), documentationPath: "articles/visual-portable-detectors.md"));
            }

            ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-08T02:37:00Z", "external-portable-detectors.2", new Uri("https://github.com/guojin-yan/DeploySharp"), entries));
            Assert.AreEqual(8, catalog.Document.Entries.Count);
            Assert.AreEqual(5, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "object-detection", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
            Assert.AreEqual(3, ModelCatalogQuery.Select(catalog, new ModelQuery(format: "onnx", backend: "openvino", includePreview: true)).Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(format: "openvino-ir", backend: "openvino", includePreview: true)).Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "instance-segmentation", backend: "openvino", includePreview: true)).Count);
            foreach (ModelCatalogEntry entry in catalog.Document.Entries)
            {
                Assert.AreEqual(ModelCatalogStatus.External, entry.Status);
                Assert.IsFalse(entry.Source!.RedistributionAllowed);
                Assert.IsNull(entry.Release);
            }
            OfficialCatalogAssertions.Excludes(catalog);
        }
    }
}
