using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class PortableDetectorModelPackageCandidateTests
    {
        [TestMethod]
        public void EightExternalCandidatesCarryExactTensorIntegrityAndReleaseBlockers()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "detr");
            string[] files = Directory.GetFiles(directory, "*.modelpack.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(8, files.Length);
            foreach (string file in files)
            {
                ModelPackageDocument document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(file)).Document;
                Assert.IsNotNull(document.Source);
                Assert.IsFalse(document.Source!.RedistributionAllowed);
                Assert.IsTrue(document.Inputs.Count > 0);
                Assert.IsTrue(document.Outputs.Count > 0);
                Assert.IsTrue(document.Artifacts.Count > 0);
                foreach (ModelArtifactDocument artifact in document.Artifacts)
                {
                    Assert.IsTrue(artifact.Portable);
                    Assert.IsTrue(artifact.Extensions["deploysharp.release-admission"].StartsWith("blocked-", StringComparison.Ordinal));
                    foreach (ModelFileDocument item in artifact.Files)
                    {
                        Assert.AreEqual(64, item.Sha256!.Length);
                        Assert.IsTrue(item.Size > 0);
                    }
                }
            }

            ModelPackageDocument rtDetr = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(files.Single(path => Path.GetFileName(path) == "rt-detr-detect.modelpack.json"))).Document;
            ModelArtifactDocument ir = rtDetr.Artifacts.Single(artifact => artifact.Format == "openvino-ir");
            Assert.AreEqual(2, ir.Files.Count);
            CollectionAssert.AreEquivalent(new[] { ModelFileRole.Model, ModelFileRole.Weights }, ir.Files.Select(item => item.Role).ToArray());

            ModelPackageDocument decodedIr = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(files.Single(path => Path.GetFileName(path) == "rt-detr-decoded-vector-ir.modelpack.json"))).Document;
            CollectionAssert.AreEqual(new[] { "save_infer_model/scale_0.tmp_0", "cast_5.tmp_0" }, decodedIr.Outputs.Select(output => output.Name).ToArray());
            CollectionAssert.AreEquivalent(new[] { "openvino" }, decodedIr.Artifacts.Single().CompatibleBackends.ToArray());

            ModelPackageDocument raw = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(files.Single(path => Path.GetFileName(path) == "rt-detr-raw-query.modelpack.json"))).Document;
            CollectionAssert.AreEqual(new[] { "image" }, raw.Inputs.Select(input => input.Name).ToArray());
            CollectionAssert.AreEquivalent(new[] { "onnxruntime", "openvino" }, raw.Artifacts.Single().CompatibleBackends.ToArray());
        }
    }
}
