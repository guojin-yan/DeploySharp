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
    public sealed class Stage19CatalogAdmissionTests
    {
        [TestMethod]
        public void FiveExternalRowsAreOptInQueryableButCannotEnterOfficialCatalog()
        {
            using JsonDocument support = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "stage19-support.json")));
            var entries = new List<ModelCatalogEntry>();
            foreach (JsonElement row in support.RootElement.GetProperty("rows").EnumerateArray())
            {
                string rowName = row.GetProperty("v1Row").GetString()!;
                string task = row.GetProperty("task").GetString()!;
                string family = ToFamily(rowName);
                var source = new ModelSourceDocument("https://github.com/guojin-yan/DeploySharp", null, "external-local-contract", "Upstream maintainers", null, "NOASSERTION", null, false);
                var artifact = new ModelCatalogArtifact("onnx.fp32", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", "none", true, null, Array.Empty<ModelCatalogAsset>(), new ModelCatalogConversion("external-export", null, null, row.GetProperty("blocker").GetString()));
                entries.Add(new ModelCatalogEntry("stage19/" + family + "/external", rowName + " external family", family, task, "external", ModelCatalogStatus.External, row.GetProperty("blocker").GetString(), source, null, new[] { artifact }, Array.Empty<ModelCatalogAsset>(), documentationPath: "articles/visual-ocr-anomaly-rmbg.md"));
            }

            ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-07T08:00:00Z", "external-stage19.1", new Uri("https://github.com/guojin-yan/DeploySharp"), entries));
            Assert.AreEqual(5, catalog.Document.Entries.Count);
            Assert.AreEqual(2, ModelCatalogQuery.Select(catalog, new ModelQuery(family: "paddle-ocr-det", format: "onnx", backend: "onnxruntime", includePreview: true)).Count + ModelCatalogQuery.Select(catalog, new ModelQuery(family: "paddle-ocr-rec", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(family: "paddle-ocr-cls", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "anomaly-detection", backend: "openvino", includePreview: true)).Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "foreground-matting", backend: "onnxruntime", includePreview: true)).Count);
            Assert.AreEqual(0, ModelCatalogQuery.Select(catalog, new ModelQuery(format: "onnx", includePreview: false)).Count);
            foreach (ModelCatalogEntry entry in catalog.Document.Entries)
            {
                Assert.AreEqual(ModelCatalogStatus.External, entry.Status);
                Assert.IsFalse(entry.Source!.RedistributionAllowed);
                Assert.IsNull(entry.Release);
            }
            OfficialCatalogAssertions.Excludes(catalog);
        }

        private static string ToFamily(string rowName)
        {
            if (rowName == "PaddleOcrDet") return "paddle-ocr-det";
            if (rowName == "PaddleOcrRec") return "paddle-ocr-rec";
            if (rowName == "PaddleOcrCls") return "paddle-ocr-cls";
            if (rowName == "AnomalibSeg") return "anomalib-seg";
            if (rowName == "BriaRmbg") return "bria-rmbg";
            throw new InvalidDataException("Unknown stage-19 row: " + rowName);
        }
    }
}
