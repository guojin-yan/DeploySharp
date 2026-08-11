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
    public sealed class Stage20CatalogAdmissionTests
    {
        [TestMethod]
        public void PaddleOcrClsExternalCandidateIsOptInOnlyAndNotRedistributable()
        {
            using JsonDocument support = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "stage20-support.json")));
            JsonElement row = support.RootElement.GetProperty("rows")[0];
            string blocker = row.GetProperty("blocker").GetString()!;
            var source = new ModelSourceDocument("https://github.com/PaddlePaddle/PaddleOCR", null, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "PaddleOCR maintainers", null, "Apache-2.0", null, false);
            var artifact = new ModelCatalogArtifact("paddle-ocr-cls.onnx", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", "none", true, null, Array.Empty<ModelCatalogAsset>(), new ModelCatalogConversion("external-export", null, null, blocker));
            var entry = new ModelCatalogEntry("stage20/paddle-ocr-cls/external", "PaddleOCRCls external candidate", "paddle-ocr-cls", row.GetProperty("task").GetString()!, "external", ModelCatalogStatus.External, blocker, source, null, new[] { artifact }, Array.Empty<ModelCatalogAsset>(), documentationPath: "articles/visual-paddle-ocr3.md");
            ValidatedModelCatalog catalog = ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-08T00:00:00Z", "external-stage20.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));

            Assert.AreEqual(1, catalog.Document.Entries.Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(family: "paddle-ocr-cls", format: "onnx", backend: "onnxruntime", includePreview: true)).Count);
            Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "text-orientation-classification", backend: "openvino", includePreview: true)).Count);
            Assert.AreEqual(0, ModelCatalogQuery.Select(catalog, new ModelQuery(family: "paddle-ocr-cls", includePreview: false)).Count);
            Assert.AreEqual(ModelCatalogStatus.External, catalog.Document.Entries[0].Status);
            Assert.IsFalse(catalog.Document.Entries[0].Source!.RedistributionAllowed);
            Assert.AreEqual(0, OfficialModelCatalog.Load().Document.Entries.Count);
        }
    }
}
