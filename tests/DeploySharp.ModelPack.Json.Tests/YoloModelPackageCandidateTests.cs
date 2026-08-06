using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class YoloModelPackageCandidateTests
    {
        [TestMethod]
        public void TenPortableOnnxCandidatesCarryIntegrityAndBlockedReleaseAdmission()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "yolo");
            string[] files = Directory.GetFiles(directory, "*.modelpack.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(10, files.Length);

            foreach (string file in files)
            {
                ValidatedModelPackage package = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(file));
                ModelPackageDocument document = package.Document;
                Assert.AreEqual("object-detection", document.Task);
                Assert.IsNotNull(document.Source);
                Assert.IsFalse(document.Source!.RedistributionAllowed);
                Assert.AreEqual(1, document.Inputs.Count);
                Assert.AreEqual(1, document.Outputs.Count);
                Assert.AreEqual(document.ModelId == "yolo/v8/detect/n" ? 2 : 1, document.Artifacts.Count);

                ModelArtifactDocument artifact = document.Artifacts.Single(value => value.Format == "onnx");
                Assert.AreEqual("onnx", artifact.Format);
                Assert.IsTrue(artifact.Portable);
                Assert.IsTrue(artifact.Opset.HasValue && artifact.Opset.Value > 0);
                CollectionAssert.AreEquivalent(new[] { "onnxruntime", "openvino" }, artifact.CompatibleBackends.ToArray());
                Assert.AreEqual("local-backend-verified", artifact.Extensions["deploysharp.validation-status"]);
                Assert.AreEqual("unverified-local-file", artifact.Extensions["deploysharp.artifact-provenance"]);
                Assert.AreEqual(64, artifact.Extensions["deploysharp.prepared-tensor-sha256"].Length);
                Assert.IsTrue(artifact.Extensions["deploysharp.release-admission"].StartsWith("blocked-", StringComparison.Ordinal));
                Assert.AreEqual(1, artifact.Files.Count);
                Assert.AreEqual(64, artifact.Files[0].Sha256!.Length);
                Assert.IsTrue(artifact.Files[0].Size > 0);

                ModelArtifactDocument? ir = document.Artifacts.SingleOrDefault(value => value.Format == "openvino-ir");
                if (document.ModelId == "yolo/v8/detect/n")
                {
                    Assert.IsNotNull(ir);
                    CollectionAssert.AreEqual(new[] { "openvino" }, ir!.CompatibleBackends.ToArray());
                    Assert.AreEqual(2, ir.Files.Count);
                    Assert.AreEqual("locally-converted-from-audited-onnx", ir.Extensions["deploysharp.artifact-provenance"]);
                    Assert.AreEqual(64, ir.Files[0].Sha256!.Length);
                    Assert.AreEqual(64, ir.Files[1].Sha256!.Length);
                }
                else Assert.IsNull(ir);
            }
        }
    }
}
