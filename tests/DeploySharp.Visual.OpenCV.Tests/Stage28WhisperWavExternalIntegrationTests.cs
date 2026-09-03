using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage28WhisperWavExternalIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void WhisperTinyEnglishMatchesLicensedLibriSpeechWavGreedyReference()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_RUN_EXTERNAL_MODELS"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_RUN_EXTERNAL_MODELS=1 to run the local Whisper WAV evidence.");
            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_WHISPER_MODEL_ROOT") ?? @"E:\DeploySharp-Models\whisper-tiny.en";
            string checkpoint = Path.Combine(modelRoot, "checkpoint"); string graphRoot = Path.Combine(modelRoot, "onnx-whisper-three-graph"); string wav = Environment.GetEnvironmentVariable("DEPLOYSHARP_WHISPER_WAV") ?? @"E:\DeploySharp-Models\wav2vec2-base-960h\dataset\6930-75918-0000.wav";
            string encoderPath = Path.Combine(graphRoot, "whisper-tiny.en-encoder.onnx"); string prefillPath = Path.Combine(graphRoot, "whisper-tiny.en-decoder-prefill.onnx"); string decodePath = Path.Combine(graphRoot, "whisper-tiny.en-decoder-with-past.onnx");
            Require(wav); Require(checkpoint); Require(encoderPath); Require(prefillPath); Require(decodePath);
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWhisperTinyEnglishOnnx(); AudioArtifactContract encoder = profile.GetArtifact(AudioArtifactRole.WhisperEncoder); AudioArtifactContract prefill = profile.GetArtifact(AudioArtifactRole.WhisperDecoderPrefill); AudioArtifactContract decode = profile.GetArtifact(AudioArtifactRole.WhisperDecoderWithPast);
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime(); var bundle = new AudioUnderstandingBundle(profile, new[]
            {
                new AudioArtifactBinding(AudioArtifactRole.WhisperEncoder, encoder.CreateArtifact(encoderPath, OnnxRuntimeBackendProvider.BackendId)),
                new AudioArtifactBinding(AudioArtifactRole.WhisperDecoderPrefill, prefill.CreateArtifact(prefillPath, OnnxRuntimeBackendProvider.BackendId)),
                new AudioArtifactBinding(AudioArtifactRole.WhisperDecoderWithPast, decode.CreateArtifact(decodePath, OnnxRuntimeBackendProvider.BackendId))
            });
            var extractor = new WhisperLogMelExtractor(checkpoint, profile.Processor); using PreparedWhisperInput input = new OpenCvAudioInputFactory().CreateWhisperFromWavFile(wav, profile, extractor, "LibriSpeech CC-BY-4.0 row 6930-75918-0000");
            using var session = new WhisperUnderstandingSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu")); WhisperEncodedState state = session.SetAudio(input); Assert.AreEqual(new TensorShape(1, 1500, 384), new TensorShape(state.Shape));
            var tokenizer = new WhisperTokenizer(checkpoint, profile.Generation!); WhisperTranscriptionResult result = session.Transcribe(tokenizer, new WhisperTranscriptionRequest(maximumTokens: 64, requestId: "stage28-librispeech-whisper"));
            CollectionAssert.AreEqual(new[] { 34732, 4504, 284, 663, 1295, 31095, 262, 29804, 13, 50256 }, result.TokenIds.ToArray()); Assert.AreEqual("Concord returned to its place amidst the tents.", result.Text); Assert.AreEqual("103c3f15eb3715ebc6243d142244128a3ac39b6bfae315baa6b8dc4a8be14aa8", input.SourceSha256); Assert.AreEqual("1c57e9abe250f4cc4f9058b5fee9c45e40552b01a068f208189db495fae2c5fc", input.FeatureSha256); Assert.IsTrue(result.Timing.Total > result.Timing.Preprocess); Console.WriteLine("STAGE28_WHISPER_WAV_ORT preprocessMs=" + result.Timing.Preprocess.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";encodeMs=" + result.Timing.Encode.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";prefillMs=" + result.Timing.Prefill.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";decodeMs=" + result.Timing.DecodeTotal.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";totalMs=" + result.Timing.Total.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";tokens=" + string.Join(",", result.TokenIds));
        }

        private static void Require(string path) { if (!File.Exists(path) && !Directory.Exists(path)) Assert.Inconclusive("External Whisper WAV asset is missing: " + path); }
    }
}
