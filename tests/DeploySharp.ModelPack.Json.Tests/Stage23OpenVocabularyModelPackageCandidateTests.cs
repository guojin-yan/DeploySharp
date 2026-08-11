using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage23OpenVocabularyModelPackageCandidateTests
    {
        [TestMethod]
        public void ExternalOpenVocabularyManifestsBindHashesLicensesEvidenceAndBlockers()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "open-vocabulary");
            string[] files = Directory.GetFiles(directory, "*.modelpack.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(5, files.Length);
            foreach (string file in files)
            {
                ModelPackageDocument document;
                try { document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(file)).Document; }
                catch (ModelPackageValidationException exception) { Assert.Fail(Path.GetFileName(file) + ": " + string.Join(" | ", exception.Diagnostics.Select(value => value.JsonPath + " " + value.Message))); throw; }
                Assert.IsNotNull(document.Source);
                Assert.IsFalse(document.Source!.RedistributionAllowed);
                Assert.IsTrue(document.Artifacts.Count > 0);
                foreach (ModelArtifactDocument artifact in document.Artifacts)
                {
                    Assert.IsTrue(artifact.Files.Count > 0);
                    foreach (ModelFileDocument modelFile in artifact.Files)
                    {
                        Assert.AreEqual(64, modelFile.Sha256!.Length);
                        Assert.IsTrue(modelFile.Size > 0);
                    }
                }
            }
            ModelPackageDocument yoloWorld = Read(files, "ultralytics-yoloworldv2-person-bus.modelpack.json");
            Assert.AreEqual(4, yoloWorld.Artifacts.Single().Files.Count);
            Assert.AreEqual("fixed-vocabulary", yoloWorld.Artifacts.Single().Extensions["deploysharp.prompt-mode"]);
            Assert.AreEqual("local-ort-openvino-official-onnx-predictor-real-image-verified", yoloWorld.Artifacts.Single().Extensions["deploysharp.validation-status"]);
            ModelPackageDocument grounded = Read(files, "grounded-sam-yoloworld-samv1.modelpack.json");
            Assert.AreEqual(3, grounded.Artifacts.Count);
            Assert.IsTrue(grounded.Artifacts.All(value => value.CompatibleBackends.Contains("onnxruntime") && value.CompatibleBackends.Contains("openvino")));
            Assert.AreEqual("2f4ebf145d27b48ff4f5175d886ac22bddbfc95e801c3d749b4e4e3f5efcab4e", grounded.Artifacts.Last().Extensions["deploysharp.official-mask-sha256"]);
        }

        private static ModelPackageDocument Read(string[] files, string name) => ModelPackageJsonSerializer.Deserialize(File.ReadAllText(files.Single(path => Path.GetFileName(path) == name))).Document;
    }
}
