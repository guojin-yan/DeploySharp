using System;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class PaddleOcrReleaseCatalogTests
    {
        [TestMethod]
        public void BundledCatalogSelectsAllPublishedPaddleOcrV5Variants()
        {
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            ModelCatalogEntry[] entries = catalog.Document.Entries
                .Where(entry => entry.Release?.Tag == "models-visual.1"
                    && entry.ModelId!.StartsWith("paddleocr/ppocrv5/", StringComparison.Ordinal))
                .ToArray();

            Assert.AreEqual("models-visual.1", catalog.CatalogRevision);
            Assert.AreEqual(6, entries.Length);
            Assert.AreEqual(20, entries.Sum(entry => entry.Artifacts.Single().Assets.Count));
            Assert.IsTrue(entries.All(entry => entry.Status == ModelCatalogStatus.Preview));
            Assert.IsTrue(entries.All(entry => entry.Source!.RedistributionAllowed));
            Assert.IsTrue(entries.All(entry => entry.Release!.Commit == "1ac899174a7b8848559139750c5ce06768cc0a0a"));

            foreach (ModelCatalogEntry entry in entries)
            {
                ModelCatalogArtifact artifact = entry.Artifacts.Single();
                Assert.AreEqual("onnx", artifact.Format);
                Assert.IsTrue(artifact.CompatibleBackends.Contains("onnxruntime"));
                Assert.IsTrue(artifact.CompatibleBackends.Contains("openvino"));
                Assert.IsTrue(artifact.Assets.Any(asset => asset.Kind == ModelCatalogAssetKind.Manifest));
                Assert.IsTrue(artifact.Assets.Any(asset => asset.Kind == ModelCatalogAssetKind.Model));
                Assert.IsTrue(artifact.Assets.Any(asset => asset.Kind == ModelCatalogAssetKind.License));
                Assert.IsTrue(artifact.Assets.All(asset => asset.ReleaseTag == entry.Release!.Tag));
                Assert.IsTrue(artifact.Assets.Any(asset => asset.RelativePath == entry.Source!.LicenseFile));
                Assert.IsFalse(ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: entry.ModelId)).Any());

                foreach (string backend in artifact.CompatibleBackends)
                {
                    Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: entry.ModelId, backend: backend, format: "onnx", includePreview: true)).Count);
                }
            }

            Assert.AreEqual(2, ModelCatalogQuery.Select(catalog, new ModelQuery(family: "paddle-ocr-det", backend: "onnxruntime", format: "onnx", includePreview: true)).Count);
            Assert.AreEqual(2, ModelCatalogQuery.Select(catalog, new ModelQuery(family: "paddle-ocr-rec", backend: "openvino", format: "onnx", includePreview: true)).Count);
            Assert.AreEqual(2, ModelCatalogQuery.Select(catalog, new ModelQuery(task: "text-orientation-classification", backend: "onnxruntime", format: "onnx", includePreview: true)).Count);
        }

        [TestMethod]
        public void PaddleOcrAssetsComeFromTheMatchingPublishedRelease()
        {
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();

            foreach (ModelCatalogEntry entry in catalog.Document.Entries.Where(value => value.ModelId!.StartsWith("paddleocr/ppocrv5/", StringComparison.Ordinal)))
            {
                string key = entry.ModelId!.Substring("paddleocr/ppocrv5/".Length);
                ModelCatalogAsset model = entry.Artifacts.Single().Assets.Single(asset => asset.Kind == ModelCatalogAssetKind.Model);
                string releaseBase = "https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/";
                Assert.AreEqual(releaseBase + "ppocrv5-" + key + ".model.onnx", model.DownloadUri!.AbsoluteUri);
                Assert.IsTrue(entry.Artifacts.Single().Assets.All(asset => asset.DownloadUri!.AbsoluteUri.StartsWith(releaseBase, StringComparison.Ordinal)));
                if (key.EndsWith("-rec", StringComparison.Ordinal)) Assert.IsTrue(entry.Artifacts.Single().Assets.Single(asset => asset.Kind == ModelCatalogAssetKind.Other).DownloadUri!.AbsoluteUri.EndsWith("/ppocrv5_dict.txt", StringComparison.Ordinal));
            }
        }
    }
}
