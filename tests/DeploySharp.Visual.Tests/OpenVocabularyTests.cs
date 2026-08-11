using System;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OpenVocabularyTests
    {
        [TestMethod]
        public void VocabularyIdentityPreservesOrderAndRejectsNormalizedDuplicates()
        {
            var first = new VocabularyPrompt(new[] { "person", "bus" }, VocabularyNormalization.Nfc);
            var second = new VocabularyPrompt(new[] { "bus", "person" }, VocabularyNormalization.Nfc);
            Assert.AreNotEqual(first.Sha256, second.Sha256);
            Assert.AreEqual("person", first.Entries[0].Text);
            Assert.ThrowsExactly<VisualException>(() => new VocabularyPrompt(new[] { "Ａ", "a" }, VocabularyNormalization.NfkcLowerInvariant));
            try { new VocabularyPrompt(new[] { "a", "A" }, VocabularyNormalization.NfkcLowerInvariant); Assert.Fail(); }
            catch (VisualException exception) { Assert.AreEqual(VisualErrorCodes.OpenVocabularyContractInvalid, exception.ErrorCode); }
        }

        [TestMethod]
        public void FixedYoloWorldProfileBindsArtifactsTokensAndEmbedding()
        {
            OpenVocabularyDetectionProfile profile = OpenVocabularyDetectionProfiles.CreateUltralyticsYoloWorldV2PersonBus();
            Assert.IsTrue(profile.Executable);
            Assert.AreEqual(OpenVocabularyPromptMode.FixedVocabulary, profile.PromptMode);
            Assert.AreEqual("images", profile.VisualProfile.Input.Name);
            Assert.AreEqual("output0", profile.VisualProfile.Outputs.Single().Name);
            Assert.AreEqual(17, profile.GetArtifact(OpenVocabularyArtifactRole.Detector).Opset);
            Assert.AreEqual("42f9d408c0ba8f941fa5efd503c8d4faa175fff1705686174684ae5e6de29bdd", profile.GetArtifact(OpenVocabularyArtifactRole.Detector).Sha256);
            Assert.AreEqual(77, profile.Tokenization[0].TokenIds.Count);
            Assert.AreEqual(2533, profile.Tokenization[0].TokenIds[1]);
            Assert.AreEqual(2840, profile.Tokenization[1].TokenIds[1]);
            Assert.AreEqual("e047a003ac4cf14a051aadef984378198c1237c6677a4dff0069cd6da6d74753", profile.EmbeddingIdentity!.EmbeddingSha256);
        }

        [TestMethod]
        public void DecoderReusesCanonicalDetectionAndAddsPhraseProvenance()
        {
            OpenVocabularyDetectionProfile profile = OpenVocabularyDetectionProfiles.CreateUltralyticsYoloWorldV2PersonBus();
            var values = new[] { 320f, 320f, 100f, 200f, .9f, .1f };
            var outputs = InferenceOutputs.Create("output0", new Tensor<float>(new TensorShape(1, 6, 1), values));
            var size = new VisualSize(640, 640);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 640, 640), new float[3 * 640 * 640]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            var result = (OpenVocabularyDetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(1, result.Detections.Detections.Count);
            Assert.AreEqual("person", result.Detections.Detections[0].Label.Label);
            Assert.AreEqual("person", result.Matches[0].Phrase);
            Assert.AreEqual(2533, result.Matches[0].TokenIds[1]);
            Assert.AreEqual(270f, result.Detections.Detections[0].Box.X, .0001f);
            Assert.AreEqual(220f, result.Detections.Detections[0].Box.Y, .0001f);
        }

        [TestMethod]
        public void UnsupportedFamiliesExposeReproducibleBlockers()
        {
            OpenVocabularyDetectionProfile grounding = OpenVocabularyDetectionProfiles.CreateGroundingDinoSwinTBlocker();
            OpenVocabularyDetectionProfile yoloE = OpenVocabularyDetectionProfiles.CreateYoloEBlocker();
            OpenVocabularyDetectionProfile mmyolo = OpenVocabularyDetectionProfiles.CreateMmyoloYoloWorldV2Blocker();
            Assert.IsFalse(grounding.Executable);
            Assert.IsFalse(yoloE.Executable);
            Assert.IsFalse(mmyolo.Executable);
            Assert.IsTrue(grounding.Blocker!.Contains("ONNX", StringComparison.Ordinal));
            Assert.IsTrue(mmyolo.Blocker!.Contains("vocabulary", StringComparison.OrdinalIgnoreCase));
            try { grounding.CreateArtifact("missing.onnx"); Assert.Fail(); }
            catch (VisualException exception) { Assert.AreEqual(VisualErrorCodes.CapabilityUnavailable, exception.ErrorCode); }
        }
    }
}
