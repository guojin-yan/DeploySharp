using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage25GenerativeVisionLanguageModelPackageCandidateTests
    {
        [TestMethod]
        public void ExternalFamilyManifestsBindExecutableBundleAndHonestBlockers()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "generative-vision-language");
            string[] files = Directory.GetFiles(directory, "*.modelpack.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(4, files.Length);
            ModelPackageDocument[] documents = files.Select(path => ModelPackageJsonSerializer.Deserialize(File.ReadAllText(path)).Document).ToArray();
            Assert.IsTrue(documents.All(value => value.Source != null && !value.Source.RedistributionAllowed));
            Assert.IsTrue(documents.SelectMany(value => value.Artifacts).SelectMany(value => value.Files).All(value => value.Size > 0 && value.Sha256!.Length == 64));

            ModelPackageDocument caption = documents.Single(value => value.ModelId == "generative-vision-language/blip-caption-base/external");
            CollectionAssert.AreEqual(new[] { "vision-encoder", "language-decoder" }, caption.Artifacts.Select(value => value.Extensions["deploysharp.bundle-role"]).ToArray());
            Assert.IsTrue(caption.Artifacts.All(value => value.CompatibleBackends.Contains("onnxruntime") && value.CompatibleBackends.Contains("openvino")));
            Assert.IsTrue(caption.Artifacts.All(value => value.Extensions["deploysharp.validation-status"] == "external-ort-openvino-official-golden-verified"));
            Assert.AreEqual(1, caption.Artifacts.Select(value => value.Extensions["deploysharp.bundle-version"]).Distinct(StringComparer.Ordinal).Count());

            foreach (ModelPackageDocument blocker in documents.Where(value => value != caption))
            {
                ModelArtifactDocument artifact = blocker.Artifacts.Single();
                Assert.AreEqual("official-source-contract-only", artifact.Extensions["deploysharp.validation-status"]);
                Assert.IsTrue(artifact.Extensions["deploysharp.blocker"].Contains("ONNX", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(artifact.Extensions["deploysharp.blocker"].Contains("OpenVINO", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(artifact.Portable);
            }
        }
    }
}
