using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class PaddleOcrModelCatalogTests
    {
        [TestMethod]
        public void CorePpOcrV4V5V6RowsHaveCodeFactories()
        {
            Assert.AreEqual(17, PaddleOcrModelCatalog.All.Count);
            CollectionAssert.AreEquivalent(new[] { "v4", "v5", "v6" },
                System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(PaddleOcrModelCatalog.All, x => x.Version)).ToArray());
            foreach (PaddleOcrModelDescriptor descriptor in PaddleOcrModelCatalog.All)
            {
                Assert.IsFalse(descriptor.ModelId.IsEmpty);
                Assert.IsFalse(string.IsNullOrWhiteSpace(descriptor.CodeProfile));
                Assert.IsFalse(string.IsNullOrWhiteSpace(descriptor.InputName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(descriptor.OutputName));
                Assert.IsNotNull(PaddleOcrModelCatalog.Find(descriptor.ModelId));
                Assert.IsNotNull(descriptor.ReleaseArtifact);
            }
        }

        [TestMethod]
        public void RecognitionRowsUseTheExistingCtcProfileFactory()
        {
            foreach (PaddleOcrModelDescriptor descriptor in PaddleOcrModelCatalog.All)
            {
                if (descriptor.Family != "rec") continue;
                Assert.AreEqual("CreateRecognition", descriptor.CodeProfile);
                Assert.IsTrue(descriptor.DictionaryRequired);
            }
        }

        [TestMethod]
        public void PublishedTensorNamesMatchTheConvertedV4V5V6Graphs()
        {
            Assert.AreEqual("sigmoid_0.tmp_0", PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv4/mobile-det")).OutputName);
            Assert.AreEqual("fetch_name_0", PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv4/server-det")).OutputName);
            Assert.AreEqual("softmax_11.tmp_0", PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv4/mobile-rec")).OutputName);
            Assert.AreEqual("fetch_name_0", PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv4/server-rec")).OutputName);
            Assert.AreEqual("fetch_name_0", PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv6/medium-det")).OutputName);
            Assert.AreEqual("fetch_name_0", PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv6/medium-rec")).OutputName);
        }

        [TestMethod]
        public void DetectionAndClassificationRowsCreateProfiles()
        {
            var artifact = new PaddleOcrArtifactContract(17, new string('a', 64), "test", "test", "Apache-2.0", "test-pre", "test-post");
            foreach (PaddleOcrModelDescriptor descriptor in PaddleOcrModelCatalog.All)
            {
                if (descriptor.Family == "rec") continue;
                PaddleOcrProfile profile = PaddleOcrModelCatalog.CreateProfile(descriptor, artifact);
                Assert.AreEqual(descriptor.ModelId, profile.VisualProfile.ModelId);
            }
        }

        [TestMethod]
        public void CoreRowsMapToIndependentReleaseAssets()
        {
            foreach (PaddleOcrModelDescriptor descriptor in PaddleOcrModelCatalog.All)
            {
                PaddleOcrReleaseArtifact artifact = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
                Assert.AreEqual("models-paddleocr", artifact.ReleaseTag);
                Assert.IsTrue(artifact.AssetName.EndsWith(".onnx", System.StringComparison.Ordinal));
                Assert.AreEqual(64, artifact.Sha256.Length);
                Assert.AreEqual(descriptor.ModelId, artifact.ModelId);
                Assert.IsTrue(artifact.Size > 0);
                Assert.IsTrue(artifact.Opset > 0);
            }
        }

        [TestMethod]
        public void ConvertedDocumentRowsMapToIndependentReleaseAssetsAndBlockedRowsStayExplicit()
        {
            int converted = 0;
            foreach (PaddleDocumentModelDescriptor descriptor in PaddleDocumentModelCatalog.Official)
            {
                if (PaddleDocumentModelCatalog.TryGetReleaseArtifact(descriptor.ModelId, out PaddleDocumentReleaseArtifact? artifact))
                {
                    converted++;
                    Assert.IsNotNull(descriptor.ReleaseArtifact);
                    Assert.IsTrue(artifact!.AssetName.EndsWith(".onnx", System.StringComparison.Ordinal));
                    Assert.AreEqual(64, artifact.Sha256.Length);
                    Assert.IsTrue(artifact.Size > 0);
                }
            }
            Assert.AreEqual(29, converted);
            Assert.IsFalse(PaddleDocumentModelCatalog.TryGetReleaseArtifact("paddle-chart/pp-chart2table", out _));
        }

        [TestMethod]
        public void PublishedDocumentProfileCreatesAnOnnxArtifactBoundToReleaseSha()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-s");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor, PaddleDocumentProfiles.Layout5Labels, new VisualSize(640, 640));
            JYPPX.DeploySharp.Models.ModelArtifact artifact = profile.CreateArtifact("pp-doclayout-s.onnx");
            Assert.AreEqual("onnx", artifact.Format);
            Assert.AreEqual(descriptor.ReleaseArtifact!.Sha256, artifact.Sha256);
        }
    }
}
