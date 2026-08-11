using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage19ModelPackageCandidateTests
    {
        [TestMethod]
        public void EightExternalCandidatesCarryNamedContractsIntegrityAndExplicitBlockers()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "stage19");
            string[] files = Directory.GetFiles(directory, "*.modelpack.json").Where(path => !Path.GetFileName(path).Contains("cls", StringComparison.OrdinalIgnoreCase)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(8, files.Length);
            foreach (string file in files)
            {
                ModelPackageDocument document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(file)).Document;
                Assert.IsNotNull(document.Source);
                Assert.IsFalse(document.Source!.RedistributionAllowed);
                Assert.IsTrue(document.Inputs.All(value => !string.IsNullOrWhiteSpace(value.Name)));
                Assert.IsTrue(document.Outputs.All(value => !string.IsNullOrWhiteSpace(value.Name)));
                Assert.IsTrue(document.Artifacts.Count > 0);
                foreach (ModelArtifactDocument artifact in document.Artifacts)
                {
                    CollectionAssert.AreEquivalent(new[] { "onnxruntime", "openvino" }, artifact.CompatibleBackends.ToArray());
                    Assert.IsTrue(artifact.Portable);
                    Assert.IsTrue(artifact.Opset.HasValue && artifact.Opset.Value > 0);
                    Assert.IsFalse(string.Equals("AlgorithmVerified", artifact.Extensions["deploysharp.validation-status"], StringComparison.Ordinal));
                    StringAssert.StartsWith(artifact.Extensions["deploysharp.release-admission"], "blocked-");
                    foreach (ModelFileDocument modelFile in artifact.Files)
                    {
                        Assert.AreEqual(64, modelFile.Sha256!.Length);
                        Assert.IsTrue(modelFile.Size > 0);
                    }
                }
            }

            ModelPackageDocument recognition = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(files.Single(path => Path.GetFileName(path) == "ppocrv5-mobile-rec.modelpack.json"))).Document;
            Assert.AreEqual(2, recognition.Artifacts[0].Files.Count);
            Assert.AreEqual("fetch_name_0", recognition.Outputs.Single().Name);

            ModelPackageDocument anomalib = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(files.Single(path => Path.GetFileName(path) == "anomalib-padim.modelpack.json"))).Document;
            CollectionAssert.AreEquivalent(new[] { "pred_score", "pred_label", "anomaly_map", "pred_mask" }, anomalib.Outputs.Select(value => value.Name).ToArray());

            ModelPackageDocument rmbg20 = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(files.Single(path => Path.GetFileName(path) == "bria-rmbg-2.0.modelpack.json"))).Document;
            Assert.AreEqual(2, rmbg20.Artifacts.Count);
            Assert.AreEqual("alphas", rmbg20.Outputs.Single().Name);
        }

        [TestMethod]
        public void PaddleOcrClsExternalCandidatesCarryTwoClassOutputAndBlockedAdmission()
        {
            string[] files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures", "stage20"), "*.modelpack.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(3, files.Length);
            foreach (string file in files)
            {
                ModelPackageDocument document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(file)).Document;
                Assert.AreEqual("text-orientation-classification", document.Task);
                ModelTensorSignatureDocument output = document.Outputs.Single();
                Assert.AreEqual(2, output.Shape[output.Shape.Count - 1]);
                Assert.IsTrue(output.Name == "fetch_name_0" || output.Name == "softmax_0.tmp_0");
                string labelOrder = document.Artifacts[0].Extensions["deploysharp.label-order"];
                StringAssert.Contains(labelOrder, "180");
                Assert.IsFalse(document.Source!.RedistributionAllowed);
                Assert.AreNotEqual("AlgorithmVerified", document.Artifacts[0].Extensions["deploysharp.validation-status"]);
                StringAssert.StartsWith(document.Artifacts[0].Extensions["deploysharp.release-admission"], "blocked-");
            }
        }
    }
}
