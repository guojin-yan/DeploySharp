using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class VisionReleaseModelPackTests
    {
        [TestMethod]
        [DataRow("clip-vit-b-32.public.modelpack.json", "vision-language/clip-vit-b-32", "MIT", 7)]
        [DataRow("sam-v1-vit-b.public.modelpack.json", "segmentation/sam-v1-vit-b", "Apache-2.0", 3)]
        [DataRow("blip-caption-base.public.modelpack.json", "generative-vision-language/blip-caption-base", "BSD-3-Clause", 6)]
        public void PublicVisionModelPacksAreValidRedistributableDirectoryBundles(string fileName, string modelId, string license, int fileCount)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", fileName);
            ValidatedModelPackage package = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(path));

            Assert.AreEqual(modelId, package.ModelId.Value);
            Assert.IsNotNull(package.Document.Source);
            Assert.IsTrue(package.Document.Source!.RedistributionAllowed);
            Assert.AreEqual(license, package.Document.Source.LicenseExpression);
            Assert.IsFalse(string.IsNullOrWhiteSpace(package.Document.Source.LicenseFile));
            ModelArtifactDocument artifact = package.Document.Artifacts.Single();
            Assert.AreEqual(ModelArtifactLocationKind.Directory, artifact.LocationKind);
            Assert.AreEqual(fileCount, artifact.Files.Count);
            Assert.IsTrue(artifact.Files.Any(file => file.Role == ModelFileRole.Model));
            Assert.IsTrue(artifact.Files.Any(file => file.Role == ModelFileRole.License));
        }

        [TestMethod]
        public void OfficialVisionEntriesShareOneImmutableRelease()
        {
            const string releaseTag = "models-20260817.vision.1";
            const string releaseCommit = "93947165a7d6bd474b4acfc30b18ca38e4dd468c";
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            ModelCatalogEntry[] entries = catalog.Document.Entries
                .Where(entry => entry.Release?.Tag == releaseTag)
                .ToArray();

            Assert.AreEqual(3, entries.Length);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "vision-language/clip-vit-b-32",
                    "segmentation/sam-v1-vit-b",
                    "generative-vision-language/blip-caption-base"
                },
                entries.Select(entry => entry.ModelId).ToArray());
            Assert.IsTrue(entries.All(entry => entry.Status == ModelCatalogStatus.Preview));
            Assert.IsTrue(entries.All(entry => entry.Release!.Commit == releaseCommit));
            Assert.IsTrue(entries.All(entry => entry.Source!.RedistributionAllowed));
            Assert.IsTrue(entries.All(entry => entry.Artifacts.Single().Assets.All(asset => asset.ReleaseTag == releaseTag)));
            Assert.AreEqual(19, entries.Sum(entry => entry.Artifacts.Single().Assets.Count));

            foreach (ModelCatalogEntry entry in entries)
            {
                Assert.IsFalse(ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: entry.ModelId)).Any());
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: entry.ModelId, backend: "onnxruntime", format: "onnx", includePreview: true)).Count);
                Assert.AreEqual(1, ModelCatalogQuery.Select(catalog, new ModelQuery(modelId: entry.ModelId, backend: "openvino", format: "onnx", includePreview: true)).Count);
            }
        }
    }
}
