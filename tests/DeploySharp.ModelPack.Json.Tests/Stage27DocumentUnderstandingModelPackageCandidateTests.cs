using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage27DocumentUnderstandingModelPackageCandidateTests
    {
        [TestMethod]
        public void ExternalManifestsBindCompleteDonutBundlesAndHonestFamilyBlockers()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "document-understanding");
            ModelPackageDocument[] documents = Directory.GetFiles(directory, "*.modelpack.json")
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(value => ModelPackageJsonSerializer.Deserialize(File.ReadAllText(value)).Document)
                .ToArray();

            Assert.AreEqual(3, documents.Length);
            Assert.IsTrue(documents.All(value => value.Source != null && !value.Source.RedistributionAllowed));
            Assert.IsTrue(documents.SelectMany(value => value.Artifacts).SelectMany(value => value.Files).All(value => value.Size > 0 && value.Sha256!.Length == 64));

            ModelPackageDocument donut = documents.Single(value => value.Family == "donut");
            string[] roles = { "document-encoder", "decoder-prefill", "decoder-with-past" };
            foreach (string format in new[] { "onnx", "openvino-ir" })
            {
                ModelArtifactDocument[] bundle = donut.Artifacts.Where(value => value.Format == format && value.Portable).ToArray();
                CollectionAssert.AreEquivalent(roles, bundle.Select(value => value.Extensions["deploysharp.bundle-role"]).ToArray());
                Assert.IsTrue(bundle.All(value => value.Extensions["deploysharp.processor-id"] == "naver.donut.cord-v2.processor"));
                Assert.IsTrue(bundle.All(value => value.Extensions["deploysharp.tokenizer-id"] == "naver.donut.cord-v2.xlm-roberta"));
                Assert.IsTrue(bundle.All(value => value.Extensions["deploysharp.ocr-ownership"] == "ocr-free"));
                Assert.IsTrue(bundle.All(value => value.Extensions["deploysharp.schema-id"] == "cord-v2.donut-tags.v1"));
                Assert.IsTrue(bundle.All(value => value.Extensions["deploysharp.kv-cache-schema-id"] == "donut.mbart.4x16x64.cross1200.v1"));
                Assert.IsTrue(bundle.All(value => value.Extensions["deploysharp.page-count"] == "1" && value.Extensions["deploysharp.context-length"] == "768"));
            }

            Assert.AreEqual(6, donut.Artifacts.Count(value => value.Portable));
            Assert.IsTrue(donut.Artifacts.SelectMany(value => value.Files).Any(value => value.RelativePath!.EndsWith(".schema.json", StringComparison.Ordinal)));
            Assert.IsTrue(donut.Artifacts.SelectMany(value => value.Files).Count(value => value.RelativePath!.EndsWith(".xml", StringComparison.Ordinal) || value.RelativePath.EndsWith(".bin", StringComparison.Ordinal)) >= 7);

            foreach (ModelPackageDocument blocker in documents.Where(value => value.Family != "donut"))
            {
                ModelArtifactDocument contract = blocker.Artifacts.Single(value => value.Extensions.ContainsKey("deploysharp.blocker"));
                Assert.AreEqual("official-source-contract-only", contract.Extensions["deploysharp.validation-status"]);
                Assert.IsFalse(contract.Portable);
                Assert.IsTrue(contract.Extensions["deploysharp.blocker"].Length > 40);
            }
        }
    }
}
