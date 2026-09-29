using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    /// <summary>Guards the official PP-Structure catalog against missing converted Release mappings. / 防止官方 PP-Structure 目录和转换 Release 映射发生漏项。</summary>
    [TestClass]
    public sealed class PaddleDocumentAssetCoverageTests
    {
        [TestMethod]
        public void EveryConvertedCatalogRowHasOneIndependentOnnxAssetAndChart2TableIsExplicitlyBlocked()
        {
            IReadOnlyList<PaddleDocumentModelDescriptor> catalog = PaddleDocumentModelCatalog.Official;
            Assert.AreEqual(30, catalog.Count, "The official catalog changed; update this coverage test together with the catalog.");

            var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int converted = 0;
            foreach (PaddleDocumentModelDescriptor descriptor in catalog)
            {
                if (!PaddleDocumentModelCatalog.TryGetReleaseArtifact(descriptor.ModelId, out PaddleDocumentReleaseArtifact? artifact))
                {
                    Assert.AreEqual("paddle-chart/pp-chart2table", descriptor.ModelId, "Only Chart2Table is currently conversion-blocked.");
                    Assert.AreEqual(PaddleDocumentModule.ChartParsing, descriptor.Module);
                    Assert.AreEqual("paddle-inference", descriptor.ModelFormat);
                    Assert.AreEqual(PaddleDocumentArtifactStatus.ConversionBlocked, descriptor.Status);
                    continue;
                }

                converted++;
                Assert.IsNotNull(artifact);
                Assert.AreEqual("models-paddleocr", artifact!.ReleaseTag);
                Assert.IsTrue(artifact.AssetName.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(assets.Add(artifact.AssetName), "Two catalog rows point to the same Release asset: " + artifact.AssetName);
                Assert.IsTrue(artifact.Size > 0);
                Assert.AreEqual(64, artifact.Sha256.Length);
                Assert.AreEqual(descriptor.Module, artifact.Module);
            }

            Assert.AreEqual(29, converted);
            Assert.AreEqual(29, assets.Count);
        }

        [TestMethod]
        public void EveryDocumentModuleHasAtLeastOneCatalogEntryAndNoReleaseEntryIsOrphaned()
        {
            foreach (PaddleDocumentModule module in Enum.GetValues<PaddleDocumentModule>())
            {
                if (module == PaddleDocumentModule.StructurePipeline) continue;
                Assert.IsTrue(PaddleDocumentModelCatalog.ForModule(module).Count > 0, "Missing catalog module: " + module);
            }

            var catalogIds = new HashSet<string>(PaddleDocumentModelCatalog.Official.Select(item => item.ModelId), StringComparer.OrdinalIgnoreCase);
            foreach (PaddleDocumentModelDescriptor descriptor in PaddleDocumentModelCatalog.Official)
            {
                if (!PaddleDocumentModelCatalog.TryGetReleaseArtifact(descriptor.ModelId, out PaddleDocumentReleaseArtifact? artifact)) continue;
                Assert.IsTrue(catalogIds.Contains(descriptor.ModelId));
                Assert.AreEqual(descriptor.ModelId, ResolveCatalogId(artifact!.ModelId));
            }
        }

        private static string ResolveCatalogId(string releaseId)
        {
            if (string.Equals(releaseId, "ppocrv4-mobile-seal-det", StringComparison.OrdinalIgnoreCase)) return "paddle-seal/ppocrv4-mobile";
            if (string.Equals(releaseId, "ppocrv4-server-seal-det", StringComparison.OrdinalIgnoreCase)) return "paddle-seal/ppocrv4-server";
            foreach (string prefix in new[] { "paddle-doc/", "paddle-table/", "paddle-formula/", "paddle-seal/" })
            {
                if (releaseId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return releaseId;
            }

            // Release metadata intentionally stores short names; verify each one resolves back to exactly one catalog row.
            PaddleDocumentModelDescriptor? match = PaddleDocumentModelCatalog.Official.SingleOrDefault(item =>
                string.Equals(item.ModelId.Substring(item.ModelId.IndexOf('/') + 1), releaseId, StringComparison.OrdinalIgnoreCase));
            return match?.ModelId ?? releaseId;
        }
    }
}
