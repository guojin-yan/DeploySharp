using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage29LlmModelPackageTests
    {
        [TestMethod]
        public void GgufManifestIsACompleteExternalBlocker()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "llm");
            ModelPackageDocument document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(Directory.GetFiles(directory, "llama-gguf-external-blocker.modelpack.json").Single())).Document;
            Assert.AreEqual("llm/gguf/external-blocker", document.ModelId);
            Assert.IsFalse(document.Source!.RedistributionAllowed);
            ModelArtifactDocument artifact = document.Artifacts.Single();
            Assert.AreEqual("gguf", artifact.Format);
            Assert.IsFalse(artifact.Portable);
            Assert.AreEqual("unknown", artifact.Quantization);
            Assert.AreEqual("official-source-contract-only", artifact.Extensions["deploysharp.validation-status"]);
            Assert.IsTrue(artifact.Extensions["deploysharp.blocker"].Length > 80);
            Assert.AreEqual("unknown", artifact.Extensions["deploysharp.context-length"]);
            Assert.AreEqual("unknown", artifact.Extensions["deploysharp.bos-eos-pad"]);
            Assert.AreEqual("false", artifact.Extensions["deploysharp.executable"]);
            ModelFileDocument file = artifact.Files.Single(value => value.RelativePath == "evidence/llama-gguf-source-contract.blocked.txt");
            Assert.AreEqual(511, file.Size);
            Assert.AreEqual(64, file.Sha256!.Length);
        }

        [TestMethod]
        public void Stage30AdmissionAuditKeepsEveryMissingRequirementExplicit()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "llm");
            ModelPackageDocument document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(Directory.GetFiles(directory, "llama-gguf-external-blocker.modelpack.json").Single())).Document;
            ModelArtifactDocument artifact = document.Artifacts.Single();
            ModelFileDocument audit = artifact.Files.Single(value => value.RelativePath == "evidence/llama-gguf-admission-stage30.blocked.txt");
            string evidencePath = Path.Combine(directory, "evidence", "llama-gguf-admission-stage30.blocked.txt");

            Assert.IsTrue(File.Exists(evidencePath));
            Assert.AreEqual(new FileInfo(evidencePath).Length, audit.Size);
            Assert.AreEqual(Hash(evidencePath), audit.Sha256);
            Assert.AreEqual("unknown", artifact.Extensions["deploysharp.native-runtime-package"]);
            Assert.AreEqual("unknown", artifact.Extensions["deploysharp.native-runtime-version"]);
            Assert.AreEqual("unknown", artifact.Extensions["deploysharp.runtime-evidence-path"]);
            Assert.AreEqual("unknown", artifact.Extensions["deploysharp.runtime-evidence-sha256"]);
            Assert.AreEqual("unknown", artifact.Extensions["deploysharp.runtime-evidence-operations"]);
            Assert.AreEqual("false", artifact.Extensions["deploysharp.executable"]);
            Assert.AreEqual("false", document.Extensions["deploysharp.algorithm-verified"]);
            Assert.AreEqual("external-blocker", document.Extensions["deploysharp.execution-status"]);
        }

        private static string Hash(string path)
        {
            using SHA256 algorithm = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
    }
}
