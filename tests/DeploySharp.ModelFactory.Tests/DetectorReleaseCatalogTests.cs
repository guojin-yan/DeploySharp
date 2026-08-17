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
        [DataRow("models-20260817.yolo.1", 22, 66)]
        [DataRow("models-20260817.detr.1", 7, 22)]
        public void OfficialDetectorEntriesUseSharedImmutableReleaseAssets(string tag, int expectedEntries, int expectedAssets)
        {
            const string releaseCommit = "571e6a7d9f72fb94caba9003238a5c1d7ff3a0e1";
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            ModelCatalogEntry[] entries = catalog.Document.Entries
                .Where(entry => entry.Release?.Tag == tag)
                .ToArray();

            Assert.AreEqual(expectedEntries, entries.Length);
            Assert.AreEqual(expectedAssets, entries.Sum(entry => entry.Artifacts.Single().Assets.Count));
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
