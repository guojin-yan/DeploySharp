using System;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class DetectorReleaseCatalogTests
    {
        [TestMethod]
        public void OfficialDetectorEntriesUseSharedImmutableReleaseAssets()
        {
            const string tag = "models-visual.1";
            const string releaseCommit = "1ac899174a7b8848559139750c5ce06768cc0a0a";
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            ModelCatalogEntry[] entries = catalog.Document.Entries
                .Where(entry => entry.Release?.Tag == tag
                    && (entry.ModelId!.StartsWith("yolo/", StringComparison.Ordinal)
                        || entry.ModelId.StartsWith("deim/", StringComparison.Ordinal)
                        || entry.ModelId.StartsWith("pp-yoloe/", StringComparison.Ordinal)
                        || entry.ModelId.StartsWith("rf-detr/", StringComparison.Ordinal)
                        || entry.ModelId.StartsWith("rt-detr/", StringComparison.Ordinal)))
                .ToArray();

            Assert.AreEqual(29, entries.Length);
            Assert.AreEqual(88, entries.Sum(entry => entry.Artifacts.Single().Assets.Count));
            Assert.IsTrue(entries.All(entry => entry.Status == ModelCatalogStatus.Preview));
            Assert.IsTrue(entries.All(entry => entry.Source!.RedistributionAllowed));
            Assert.IsTrue(entries.All(entry => entry.Release!.Commit == releaseCommit));
            Assert.IsTrue(entries.All(entry => !entry.ModelId!.EndsWith("/external", StringComparison.Ordinal)));

            foreach (ModelCatalogEntry entry in entries)
            {
                ModelCatalogArtifact artifact = entry.Artifacts.Single();
                Assert.IsTrue(artifact.Assets.Any(asset => asset.Kind == ModelCatalogAssetKind.Manifest));
                Assert.IsTrue(artifact.Assets.Any(asset => asset.Kind == ModelCatalogAssetKind.Model));
                Assert.IsTrue(artifact.Assets.Any(asset => asset.Kind == ModelCatalogAssetKind.License));
                Assert.IsTrue(artifact.Assets.All(asset => asset.ReleaseTag == tag));
                Assert.IsTrue(artifact.Assets.Any(asset => asset.RelativePath == entry.Source!.LicenseFile));
                Assert.IsFalse(ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: entry.ModelId)).Any());

                foreach (string backend in artifact.CompatibleBackends)
                {
                    Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: entry.ModelId, backend: backend, format: artifact.Format, includePreview: true)).Count);
                }
            }
        }
    }
}
