using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class YoloCatalogAdmissionTests
    {
        [TestMethod]
        public void LocalBackendEvidenceRemainsExternalUntilReleaseAndLicenseReview()
        {
            using JsonDocument support = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "yolo-detection-support.json")));
            var entries = new List<ModelCatalogEntry>();
            foreach (JsonElement model in support.RootElement.GetProperty("models").EnumerateArray())
            {
                string modelId = model.GetProperty("modelId").GetString()!;
                string family = model.GetProperty("family").GetString()!;
                string repository = model.GetProperty("repository").GetString()!;
                string revision = model.GetProperty("referenceCommit").GetString()!;
                string license = model.GetProperty("licenseExpression").GetString()!;
                var source = new ModelSourceDocument(repository, repository, revision, "Upstream YOLO maintainers", null, license, null, false);
                var artifacts = new List<ModelCatalogArtifact>();
                artifacts.Add(new ModelCatalogArtifact(
                    "onnx.fp32",
                    "onnx",
                    new[] { "onnxruntime", "openvino" },
                    "fp32",
                    "none",
                    true,
                    null,
                    Array.Empty<ModelCatalogAsset>(),
                    new ModelCatalogConversion("audited-upstream-onnx-export", model.GetProperty("exporterVersion").GetString(), revision, "Local backend evidence only; exact artifact provenance and redistribution remain blocked.")));
                if (model.TryGetProperty("openVinoIr", out JsonElement ir))
                {
                    artifacts.Add(new ModelCatalogArtifact(
                        "openvino-ir.fp32",
                        "openvino-ir",
                        new[] { "openvino" },
                        "fp32",
                        "none",
                        true,
                        null,
                        Array.Empty<ModelCatalogAsset>(),
                        new ModelCatalogConversion("OpenVINO OVC", ir.GetProperty("converterVersion").GetString(), model.GetProperty("sha256").GetString(), "Locally reproduced from the audited ONNX artifact; release admission remains blocked.")));
                }
                entries.Add(new ModelCatalogEntry(
                    modelId,
                    model.GetProperty("name").GetString(),
                    family,
                    "object-detection",
                    model.GetProperty("modelVersion").GetString(),
                    ModelCatalogStatus.External,
                    "Verified locally on ONNX Runtime and OpenVINO CPU; not downloadable until publication review.",
                    source,
                    null,
                    artifacts,
                    Array.Empty<ModelCatalogAsset>(),
                    documentationPath: "articles/visual-yolo-detection.md"));
            }

            ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument(
                "1.0",
                "2026-08-06T09:45:00Z",
                "external-yolo-detection.1",
                new Uri("https://github.com/guojin-yan/DeploySharp"),
                entries));

            Assert.AreEqual(10, catalog.Document.Entries.Count);
            Assert.AreEqual(10, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "object-detection", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
            Assert.AreEqual(10, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "object-detection", format: "onnx", backend: "openvino", includePreview: true)).Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "object-detection", format: "openvino-ir", backend: "openvino", includePreview: true)).Count);
            Assert.AreEqual(0, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "object-detection", format: "openvino-ir", backend: "onnxruntime", includePreview: true)).Count);
            foreach (ModelCatalogEntry entry in catalog.Document.Entries)
            {
                Assert.AreEqual(ModelCatalogStatus.External, entry.Status);
                Assert.IsFalse(entry.Source!.RedistributionAllowed);
                Assert.IsNull(entry.Release);
            }
            Assert.AreEqual(0, OfficialModelCatalog.Load().Document.Entries.Count);
        }
    }
}
