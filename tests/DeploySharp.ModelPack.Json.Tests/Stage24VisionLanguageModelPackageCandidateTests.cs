using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage24VisionLanguageModelPackageCandidateTests
    {
        [TestMethod]
        public void ExternalDualEncoderManifestsBindFilesRolesLicensesEvidenceAndBlocker()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "vision-language");
            string[] files = Directory.GetFiles(directory, "*.modelpack.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(3, files.Length);
            ModelPackageDocument[] documents = files.Select(path => ModelPackageJsonSerializer.Deserialize(File.ReadAllText(path)).Document).ToArray();
            Assert.IsTrue(documents.All(value => value.Source != null && !value.Source.RedistributionAllowed));
            foreach (ModelPackageDocument document in documents)
            {
                foreach (ModelArtifactDocument artifact in document.Artifacts)
                {
                    Assert.IsTrue(artifact.Files.Count > 0);
                    Assert.IsTrue(artifact.Files.All(value => value.Size > 0 && value.Sha256!.Length == 64));
                }
            }
            foreach (ModelPackageDocument executable in documents.Where(value => value.Family != "siglip2"))
            {
                CollectionAssert.AreEquivalent(new[] { "image-encoder", "text-encoder" }, executable.Artifacts.Select(value => value.Extensions["deploysharp.bundle-role"]).ToArray());
                Assert.IsTrue(executable.Artifacts.All(value => value.CompatibleBackends.Contains("onnxruntime") && value.CompatibleBackends.Contains("openvino")));
                Assert.IsTrue(executable.Artifacts.All(value => value.Extensions["deploysharp.validation-status"].Contains("official-golden", StringComparison.Ordinal)));
            }
            ModelPackageDocument blocker = documents.Single(value => value.Family == "siglip2");
            Assert.AreEqual("official-source-contract-only", blocker.Artifacts.Single().Extensions["deploysharp.validation-status"]);
            Assert.IsTrue(blocker.Artifacts.Single().Extensions["deploysharp.blocker"].Contains("ONNX/OpenVINO", StringComparison.Ordinal));
        }
    }
}
