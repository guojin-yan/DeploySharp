using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelPack.Json.Tests
{
    [TestClass]
    public sealed class Stage28AudioSpeechModelPackageTests
    {
        [TestMethod]
        public void ExternalAudioManifestsDeclareExecutableCtcAndHonestBlockers()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "audio-speech");
            ModelPackageDocument[] documents = Directory.GetFiles(directory, "*.modelpack.json").OrderBy(value => value, StringComparer.Ordinal).Select(value => ModelPackageJsonSerializer.Deserialize(File.ReadAllText(value)).Document).ToArray();

            Assert.AreEqual(4, documents.Length);
            Assert.IsTrue(documents.All(value => value.Source != null && !value.Source.RedistributionAllowed));
            Assert.IsTrue(documents.SelectMany(value => value.Artifacts).SelectMany(value => value.Files).All(value => value.Size > 0 && value.Sha256!.Length == 64));

            ModelPackageDocument wav2vec = documents.Single(value => value.Family == "wav2vec2");
            Assert.AreEqual(2, wav2vec.Artifacts.Count(value => value.Portable));
            Assert.IsTrue(wav2vec.Artifacts.Any(value => value.Format == "onnx" && value.Extensions["deploysharp.named-ports"] == "input_values=>logits"));
            Assert.IsTrue(wav2vec.Artifacts.Any(value => value.Format == "openvino-ir" && value.Files.Any(file => file.RelativePath!.EndsWith(".xml", StringComparison.Ordinal)) && value.Files.Any(file => file.RelativePath!.EndsWith(".bin", StringComparison.Ordinal))));
            Assert.IsTrue(wav2vec.Artifacts.SelectMany(value => value.Files).Any(value => value.RelativePath == "dataset/6930-75918-0000.wav" && value.Role == ModelFileRole.TestInput));
            Assert.IsTrue(wav2vec.Artifacts.All(value => value.Extensions["deploysharp.bundle-version"] == "wav2vec2-base-960h-22aad52-opset17"));

            foreach (ModelPackageDocument blocker in documents.Where(value => value.Family != "wav2vec2"))
            {
                ModelArtifactDocument artifact = blocker.Artifacts.Single(value => value.Extensions.ContainsKey("deploysharp.blocker"));
                Assert.IsFalse(artifact.Portable);
                // Whisper now carries local three-graph/C# real-WAV parity evidence while
                // remaining a non-portable source-contract blocker. Keep the assertion
                // explicit so the test tracks the stronger evidence without admitting it
                // as a downloadable release asset.
                string validationStatus = artifact.Extensions["deploysharp.validation-status"];
                if (blocker.Family == "whisper")
                {
                    Assert.AreEqual("local-three-graph-export-csharp-session-real-wav-parity-verified-public-bundle-pending", validationStatus);
                }
                else
                {
                    Assert.AreEqual("official-source-contract-only", validationStatus);
                }
                Assert.IsTrue(artifact.Extensions["deploysharp.blocker"].Length > 40);
            }
        }
    }
}
