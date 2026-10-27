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
                // Aggregate routing modules do not represent an independently downloadable detector/decoder.
                if (module == PaddleDocumentModule.StructurePipeline || module == PaddleDocumentModule.VisionLanguageParsing) continue;
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

        [TestMethod]
        public void OpenVinoSlanextCompatibilityAssetsDoNotReplaceOfficialModels()
        {
            foreach (string variant in new[] { "wired", "wireless" })
            {
                string sourceId = "paddle-table/slanext-" + variant;
                string compatibilityId = sourceId + "-openvino-compat";
                PaddleDocumentReleaseArtifact source = PaddleDocumentModelCatalog.GetReleaseArtifact(sourceId);
                PaddleDocumentReleaseArtifact compatibility = PaddleDocumentModelCatalog.GetReleaseArtifact(compatibilityId);

                Assert.AreNotEqual(source.AssetName, compatibility.AssetName);
                Assert.AreNotEqual(source.Sha256, compatibility.Sha256);
                Assert.AreEqual(variant == "wired"
                    ? "2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0"
                    : "9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6", compatibility.Sha256);
                Assert.AreEqual("models-paddleocr", compatibility.ReleaseTag);
                Assert.AreEqual(367743498, compatibility.Size);
                Assert.AreEqual(PaddleDocumentModule.TableStructureRecognition, compatibility.Module);
                Assert.IsFalse(PaddleDocumentModelCatalog.Official.Any(model => model.ModelId == compatibilityId),
                    "Derived compatibility graphs must not inflate the official model catalog.");
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
