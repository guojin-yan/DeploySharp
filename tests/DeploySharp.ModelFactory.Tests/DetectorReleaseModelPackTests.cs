using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class DetectorReleaseModelPackTests
    {
        [TestMethod]
        [DataRow("yolo-releases", 22)]
        [DataRow("detr-releases", 7)]
        public void PublicDetectorModelPacksAreValidRedistributableDirectoryBundles(string fixtureDirectory, int expectedCount)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", fixtureDirectory);
            string[] files = Directory.GetFiles(path, "*.modelpack.json", SearchOption.TopDirectoryOnly);

            Assert.AreEqual(expectedCount, files.Length);
            foreach (string file in files)
            {
                ValidatedModelPackage package = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(file));
                Assert.IsFalse(package.ModelId.Value.EndsWith("/external", StringComparison.Ordinal));
                Assert.IsNotNull(package.Document.Source);
                ModelSourceDocument source = package.Document.Source!;
                Assert.IsTrue(source.RedistributionAllowed, package.ModelId.Value);
                Assert.IsFalse(string.IsNullOrWhiteSpace(source.LicenseExpression));
                Assert.IsFalse(string.IsNullOrWhiteSpace(source.LicenseFile));
                string licenseFile = source.LicenseFile!;

                ModelArtifactDocument artifact = package.Document.Artifacts.Single();
                Assert.AreEqual(ModelArtifactLocationKind.Directory, artifact.LocationKind);
                Assert.IsTrue(artifact.Files.Any(modelFile => modelFile.Role == ModelFileRole.Model));
                Assert.IsTrue(artifact.Files.Any(modelFile => modelFile.Role == ModelFileRole.License));
                Assert.IsTrue(artifact.Files.Any(modelFile => string.Equals(modelFile.RelativePath, licenseFile, StringComparison.Ordinal)));
                Assert.IsTrue(artifact.Files.All(modelFile => modelFile.Size > 0));
                Assert.IsTrue(artifact.Files.All(modelFile => modelFile.Sha256?.Length == 64));
            }
        }
    }
}
