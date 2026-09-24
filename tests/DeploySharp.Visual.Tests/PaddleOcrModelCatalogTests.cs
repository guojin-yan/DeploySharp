using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

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
    }
}
