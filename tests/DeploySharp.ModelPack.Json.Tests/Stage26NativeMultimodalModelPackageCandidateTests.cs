using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage26NativeMultimodalModelPackageCandidateTests
    {
        [TestMethod]
        public void ExternalManifestsBindExecutableThreeGraphBundleAndHonestBlockers()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "native-multimodal");
            ModelPackageDocument[] documents = Directory.GetFiles(directory, "*.modelpack.json").OrderBy(value => value, StringComparer.Ordinal).Select(value => ModelPackageJsonSerializer.Deserialize(File.ReadAllText(value)).Document).ToArray();
            Assert.AreEqual(3, documents.Length);
            Assert.IsTrue(documents.All(value => value.Source != null && !value.Source.RedistributionAllowed));
            Assert.IsTrue(documents.SelectMany(value => value.Artifacts).SelectMany(value => value.Files).All(value => value.Size > 0 && value.Sha256!.Length == 64));

            ModelPackageDocument llava = documents.Single(value => value.Family == "llava-onevision");
            Assert.AreEqual(53, llava.Inputs.Count);
            Assert.AreEqual(51, llava.Outputs.Count);
            Assert.AreEqual(4, llava.Artifacts.Count);
            ModelArtifactDocument[] executable = llava.Artifacts.Where(value => value.Portable).ToArray();
            CollectionAssert.AreEqual(new[] { "vision-projector", "token-embedding", "prefill-kv-decoder" }, executable.Select(value => value.Extensions["deploysharp.bundle-role"]).ToArray());
            Assert.IsTrue(executable.All(value => value.CompatibleBackends.Contains("onnxruntime") && value.CompatibleBackends.Contains("openvino")));
            Assert.IsTrue(executable.All(value => value.Extensions["deploysharp.image-count"] == "1" && value.Extensions["deploysharp.context-length"] == "6144"));
            ModelArtifactDocument blockedVision = llava.Artifacts.Single(value => !value.Portable);
            Assert.IsTrue(blockedVision.Extensions["deploysharp.blocker"].Contains("ConvInteger(10)", StringComparison.Ordinal));

            foreach (ModelPackageDocument blocker in documents.Where(value => value != llava))
            {
                Assert.AreEqual("official-source-contract-only", blocker.Artifacts.Single().Extensions["deploysharp.validation-status"]);
                Assert.IsTrue(blocker.Artifacts.Single().Extensions["deploysharp.blocker"].Contains("ONNX", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(blocker.Artifacts.Single().Portable);
            }
        }
    }
}
