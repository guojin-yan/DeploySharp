using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage22SamModelPackageCandidateTests
    {
        [TestMethod]
        public void SamExternalBundlesBindEveryComponentAndSidecarWithoutRedistribution()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "sam");
            string[] files = Directory.GetFiles(directory, "*.modelpack.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(3, files.Length);
            foreach (string file in files)
            {
                ModelPackageDocument document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(file)).Document;
                Assert.IsNotNull(document.Source);
                Assert.IsFalse(document.Source!.RedistributionAllowed);
                Assert.IsTrue(document.Artifacts.Count >= 2);
                foreach (ModelArtifactDocument artifact in document.Artifacts)
                {
                    Assert.IsTrue(artifact.Extensions.ContainsKey("deploysharp.bundle-role"));
                    Assert.IsTrue(artifact.Extensions["deploysharp.release-admission"].StartsWith("blocked", StringComparison.Ordinal));
                    foreach (ModelFileDocument modelFile in artifact.Files)
                    {
                        Assert.AreEqual(64, modelFile.Sha256!.Length);
                        Assert.IsTrue(modelFile.Size > 0);
                    }
                }
            }

            ModelPackageDocument sam2 = Read(files, "sam2-hiera-tiny.modelpack.json");
            Assert.IsTrue(sam2.Artifacts.All(artifact => artifact.Files.Any(file => file.Role == ModelFileRole.Weights)));
            Assert.AreEqual(2, sam2.Artifacts.Count);

            ModelPackageDocument sam3 = Read(files, "sam3-four-graph.modelpack.json");
            Assert.AreEqual(4, sam3.Artifacts.Count);
            Assert.AreEqual(0, sam3.Artifacts.Count(artifact => artifact.CompatibleBackends.Contains("openvino")));
        }

        private static ModelPackageDocument Read(string[] files, string name) => ModelPackageJsonSerializer.Deserialize(File.ReadAllText(files.Single(path => Path.GetFileName(path) == name))).Document;
    }
}
