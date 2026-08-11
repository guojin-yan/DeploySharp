using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage31QwenGgufModelPackageTests
    {
        private const string ManifestName = "qwen2.5-0.5b-instruct-q4-k-m.modelpack.json";
        private const string ModelSha256 = "74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db";
        private const string RuntimeEvidenceSha256 = "68f2b1e144c3d4537cb2f7c91473554296bda97a52bc5e5b5e9517dfb0dfc973";

        [TestMethod]
        public void ExactQwenManifestBindsAuthorizedRuntimeEvidence()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "llm");
            string manifestPath = Path.Combine(directory, ManifestName);
            ModelPackageDocument document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(manifestPath)).Document;
            ModelArtifactDocument artifact = document.Artifacts.Single();

            Assert.AreEqual("llm/qwen2.5-0.5b-instruct-q4-k-m/external", document.ModelId);
            Assert.AreEqual("https", new Uri(document.Source!.SourceUrl!).Scheme);
            Assert.AreEqual("9217f5db79a29953eb74d5343926648285ec7e67", document.Source.Revision);
            Assert.AreEqual("Apache-2.0", document.Source.LicenseExpression);
            Assert.IsFalse(document.Source.RedistributionAllowed);
            Assert.AreEqual("q4-k-m", artifact.Quantization);
            Assert.IsFalse(artifact.Portable);
            Assert.AreEqual("true", artifact.Extensions["deploysharp.executable"]);
            Assert.AreEqual("32768", artifact.Extensions["deploysharp.context-length"]);
            StringAssert.Contains(artifact.Extensions["deploysharp.bos-eos-pad"], "151645");
            StringAssert.Contains(artifact.Extensions["deploysharp.tokenizer-identity"], "gpt2");
            StringAssert.Contains(artifact.Extensions["deploysharp.chat-template-identity"], "Qwen ChatML");
            StringAssert.Contains(artifact.Extensions["deploysharp.generation-identity"], "temperature=0.7");
            Assert.AreEqual("supported; LLamaSharp mean-pooled embedding dimension 896", artifact.Extensions["deploysharp.embedding-capability"]);
            Assert.AreEqual("LLamaSharp.Backend.Cpu", artifact.Extensions["deploysharp.native-runtime-package"]);
            Assert.AreEqual("0.27.0", artifact.Extensions["deploysharp.native-runtime-version"]);
            Assert.AreEqual("cpu-generate,stream,cancel,repeat,contention,dispose,embedding", artifact.Extensions["deploysharp.runtime-evidence-operations"]);
            Assert.AreEqual(ModelSha256, artifact.Files.Single(value => value.Role == ModelFileRole.Model).Sha256);
            Assert.AreEqual(491400032, artifact.Files.Single(value => value.Role == ModelFileRole.Model).Size);
            Assert.AreEqual(RuntimeEvidenceSha256, artifact.Extensions["deploysharp.runtime-evidence-sha256"]);
            Assert.IsFalse(document.Extensions["deploysharp.algorithm-verified"].Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual("false", document.Extensions["deploysharp.uploaded"]);
            Assert.AreEqual("false", document.Extensions["deploysharp.downloadable"]);
            Assert.AreEqual("false", document.Extensions["deploysharp.redistribution-allowed"]);
            Assert.AreEqual("external-runtime-evidence-complete", document.Extensions["deploysharp.execution-status"]);
        }

        [TestMethod]
        public void RuntimeEvidenceHashAndModelHashRemainAuditableWhenAvailable()
        {
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_LLAMA_EVIDENCE_ROOT") ?? @"E:\DeploySharp-Models\qwen2.5-0.5b-instruct-q4_k_m";
            string modelPath = Path.Combine(root, "qwen2.5-0.5b-instruct-q4_k_m.gguf");
            string evidencePath = Path.Combine(root, "evidence", "deploysharp-stage31-runtime.json");
            if (!File.Exists(modelPath) || !File.Exists(evidencePath)) Assert.Inconclusive("Stage 31 external GGUF evidence is not present.");

            string manifestPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "llm", ManifestName);
            ModelPackageDocument document = ModelPackageJsonSerializer.Deserialize(File.ReadAllText(manifestPath)).Document;
            ModelArtifactDocument artifact = document.Artifacts.Single();
            string rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (ModelFileDocument file in artifact.Files)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(file.RelativePath));
                string relativePath = file.RelativePath!;
                string artifactPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                Assert.IsTrue(artifactPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase), $"Manifest file escapes the authorized model directory: {relativePath}");
                Assert.IsTrue(File.Exists(artifactPath), $"Manifest file is missing: {artifactPath}");
                Assert.AreEqual(file.Size, new FileInfo(artifactPath).Length, $"Manifest file size changed: {relativePath}");
                Assert.AreEqual(file.Sha256, Hash(artifactPath), $"Manifest file hash changed: {relativePath}");
            }

            Assert.AreEqual(Path.GetFullPath(evidencePath), Path.GetFullPath(artifact.Extensions["deploysharp.runtime-evidence-path"]));
            Assert.AreEqual(RuntimeEvidenceSha256, Hash(evidencePath));
            using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
            JsonElement evidenceRoot = evidence.RootElement;
            JsonElement evidenceModel = evidenceRoot.GetProperty("model");
            Assert.AreEqual(Path.GetFullPath(modelPath), Path.GetFullPath(evidenceModel.GetProperty("path").GetString()!));
            Assert.AreEqual(491400032, evidenceModel.GetProperty("size").GetInt64());
            Assert.AreEqual(ModelSha256, evidenceModel.GetProperty("sha256").GetString());
            Assert.AreEqual("GGUF", evidenceModel.GetProperty("magic").GetString());

            JsonElement managedRuntime = evidenceRoot.GetProperty("managedRuntime");
            Assert.AreEqual("llamasharp", managedRuntime.GetProperty("backend").GetString());
            Assert.AreEqual("0.27.0", managedRuntime.GetProperty("version").GetString());
            JsonElement nativeRuntime = evidenceRoot.GetProperty("nativeRuntime");
            Assert.AreEqual("LLamaSharp.Backend.Cpu", nativeRuntime.GetProperty("package").GetString());
            Assert.AreEqual("0.27.0", nativeRuntime.GetProperty("version").GetString());
            Assert.AreEqual(0, nativeRuntime.GetProperty("gpuLayerCount").GetInt32());

            JsonElement operations = evidenceRoot.GetProperty("operations");
            JsonElement generate = operations.GetProperty("cpuGenerate");
            Assert.IsFalse(string.IsNullOrWhiteSpace(generate.GetProperty("Text").GetString()));
            Assert.IsTrue(generate.GetProperty("GeneratedTokens").GetInt32() > 0);
            Assert.AreEqual("Cancelled", operations.GetProperty("cancel").GetProperty("terminal").GetString());
            Assert.IsTrue(operations.GetProperty("repeat").GetProperty("identical").GetBoolean());
            Assert.AreEqual(generate.GetProperty("textSha256").GetString(), operations.GetProperty("repeat").GetProperty("textSha256").GetString());
            Assert.AreEqual("DS-LLM-4004", operations.GetProperty("contention").GetProperty("errorCode").GetString());
            Assert.IsTrue(operations.GetProperty("dispose").GetProperty("idempotent").GetBoolean());
            Assert.AreEqual("ObjectDisposedException", operations.GetProperty("dispose").GetProperty("useAfterDispose").GetString());
            Assert.AreEqual(896, operations.GetProperty("embedding").GetProperty("dimensions").GetInt32());
            Assert.IsTrue(operations.GetProperty("embedding").GetProperty("normalized").GetBoolean());
        }

        private static string Hash(string path)
        {
            using SHA256 algorithm = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
    }
}
