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
        public void PaddleOcrAssetsUsePinnedOfficialHuggingFaceAndRawSources()
        {
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            var expected = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mobile-cls"] = "PP-LCNet_x0_25_textline_ori_onnx/resolve/aea1f18d97338aaf84b463f90192f2820524a00a/inference.onnx",
                ["mobile-det"] = "PP-OCRv5_mobile_det_onnx/resolve/e6f4fa85f00e168c862bc462aebca69eef9b3d3d/inference.onnx",
                ["mobile-rec"] = "PP-OCRv5_mobile_rec_onnx/resolve/ed152b8b495f84de93cda5709d768548a9127622/inference.onnx",
                ["server-cls"] = "PP-LCNet_x1_0_textline_ori_onnx/resolve/7fdcf3cf7061163eda7183b224aa334bd33068f7/inference.onnx",
                ["server-det"] = "PP-OCRv5_server_det_onnx/resolve/dcf248c4dcc064c6d030db241255f9e8fbc6733a/inference.onnx",
                ["server-rec"] = "PP-OCRv5_server_rec_onnx/resolve/b70df217f4fd99d14f970bad092cebe7d74cc4d1/inference.onnx"
            };

            foreach (ModelCatalogEntry entry in catalog.Document.Entries.Where(value => value.ModelId!.StartsWith("paddleocr/ppocrv5/", StringComparison.Ordinal)))
            {
                string key = entry.ModelId!.Substring("paddleocr/ppocrv5/".Length);
                ModelCatalogAsset model = entry.Artifacts.Single().Assets.Single(asset => asset.Kind == ModelCatalogAssetKind.Model);
                Assert.AreEqual("https://huggingface.co/PaddlePaddle/" + expected[key], model.DownloadUri!.AbsoluteUri);
                Assert.IsTrue(entry.Artifacts.Single().Assets.Where(asset => asset.Kind == ModelCatalogAssetKind.License || asset.Kind == ModelCatalogAssetKind.Other).All(asset => asset.DownloadUri!.Host == "raw.githubusercontent.com"));
                if (key.EndsWith("-rec", StringComparison.Ordinal)) Assert.IsTrue(entry.Artifacts.Single().Assets.Single(asset => asset.Kind == ModelCatalogAssetKind.Other).DownloadUri!.AbsoluteUri.EndsWith("/ppocrv5_dict.txt", StringComparison.Ordinal));
            }
        }
    }
}
