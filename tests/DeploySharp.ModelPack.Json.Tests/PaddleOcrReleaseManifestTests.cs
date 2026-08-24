using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class PaddleOcrReleaseManifestTests
    {
        [TestMethod]
        public void SixPaddleOcrV5ReleaseManifestsAreDownloadableAndIntegrityBound()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "paddleocr-release");
            string[] files = Directory.GetFiles(directory, "*.modelpack.json")
                .Where(path => Path.GetFileName(path).StartsWith("mobile-", StringComparison.Ordinal)
                    || Path.GetFileName(path).StartsWith("server-", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(6, files.Length);
            foreach (string file in files)
            {
                ModelPackageDocument document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(file)).Document;
                Assert.IsTrue(document.ModelId!.StartsWith("paddleocr/ppocrv5/", StringComparison.Ordinal));
                Assert.IsTrue(document.Source!.RedistributionAllowed);
                Assert.AreEqual("Apache-2.0", document.Source.LicenseExpression);
                Assert.AreEqual("bundle/source/licenses/paddleocr.LICENSE.txt", document.Source.LicenseFile);
                Assert.AreEqual("alpha-preview", document.Extensions["deploysharp.publication-status"]);
                Assert.AreEqual("true", document.Extensions["deploysharp.downloadable"]);
                Assert.AreEqual("models-20260818.ppocrv5.1", document.Extensions["deploysharp.release-tag"]);

                ModelArtifactDocument artifact = document.Artifacts.Single();
                Assert.AreEqual(ModelArtifactLocationKind.Directory, artifact.LocationKind);
                Assert.AreEqual("bundle", artifact.Entrypoint);
                Assert.AreEqual("alpha-preview-redistributable-source-recorded", artifact.Extensions["deploysharp.release-admission"]);
                Assert.IsTrue(artifact.Files.Any(value => value.Role == ModelFileRole.Model && value.RelativePath == "bundle/model.onnx"));
                Assert.IsTrue(artifact.Files.Any(value => value.Role == ModelFileRole.License && value.RelativePath == "bundle/source/licenses/paddleocr.LICENSE.txt"));
            }

            foreach (string name in new[] { "mobile-rec.modelpack.json", "server-rec.modelpack.json" })
            {
                ModelPackageDocument recognition = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(files.Single(path => Path.GetFileName(path) == name))).Document;
                ModelFileDocument labels = recognition.Artifacts.Single().Files.Single(value => value.Role == ModelFileRole.Labels);
                Assert.AreEqual("bundle/ppocrv5_dict.txt", labels.RelativePath);
                Assert.AreEqual("d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b", labels.Sha256);
                Assert.AreEqual(74012, labels.Size);
            }
        }
    }
}
