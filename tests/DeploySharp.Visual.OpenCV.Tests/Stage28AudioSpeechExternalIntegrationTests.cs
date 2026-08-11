using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage28AudioSpeechExternalIntegrationTests
    {
        private const string ExpectedTranscript = "CONCORD RETURNED TO ITS PLACE AMIDST THE TENTS";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialWav2Vec2MatchesMediaProcessorOrtOpenVinoAndOfficialPredictor()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_AUDIO_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_AUDIO_RUN_EXTERNAL=1 to run the Stage 28 audio gate.");
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_AUDIO_MODEL_ROOT") ?? @"E:\DeploySharp-Models\wav2vec2-base-960h"; string wav = Path.Combine(root, "dataset", "6930-75918-0000.wav"); string vocabulary = Path.Combine(root, "checkpoint", "vocab.json"); string official = Path.Combine(root, "evidence", "6930-75918-0000", "official-predictor.json");
            Require(wav); Require(vocabulary); Require(official); Require(Path.Combine(root, "onnx", "wav2vec2-base-960h-ctc.onnx")); Require(Path.Combine(root, "openvino", "wav2vec2-base-960h-ctc.xml")); Require(Path.Combine(root, "openvino", "wav2vec2-base-960h-ctc.bin"));
            int[] officialFrames = ReadOfficialFrames(official); Evidence ort = Run(root, wav, false); Evidence openVino = Run(root, wav, true);
            CollectionAssert.AreEqual(officialFrames, ort.Result.Decoded.FrameTokenIds.ToArray()); CollectionAssert.AreEqual(officialFrames, openVino.Result.Decoded.FrameTokenIds.ToArray()); Assert.AreEqual(ExpectedTranscript, ort.Result.Decoded.Transcript); Assert.AreEqual(ExpectedTranscript, openVino.Result.Decoded.Transcript); CollectionAssert.AreEqual(ort.Result.Decoded.CollapsedTokenIds.ToArray(), openVino.Result.Decoded.CollapsedTokenIds.ToArray()); Assert.AreEqual(47, ort.Result.Decoded.Segments.Count); Assert.AreEqual(47, openVino.Result.Decoded.Segments.Count); Assert.AreEqual(175, officialFrames.Length);
            string evidencePath = Path.Combine(root, "evidence", "6930-75918-0000", "deploysharp-dotnet.json");
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0",
                sourceAudioSha256 = "103c3f15eb3715ebc6243d142244128a3ac39b6bfae315baa6b8dc4a8be14aa8",
                officialFrameDecisionSha256 = HashIds(officialFrames),
                transcript = ExpectedTranscript,
                ort = Json(ort),
                openvino = Json(openVino)
            }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            Console.WriteLine("STAGE28_AUDIO_EVIDENCE sourceSha=103c3f15eb3715ebc6243d142244128a3ac39b6bfae315baa6b8dc4a8be14aa8;ortFeatureSha=" + ort.State.FeatureSha256 + ";openVinoFeatureSha=" + openVino.State.FeatureSha256 + ";ortInferenceMs=" + ort.Result.Timing.Inference.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";openVinoInferenceMs=" + openVino.Result.Timing.Inference.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";frames=" + officialFrames.Length + ";segments=" + ort.Result.Decoded.Segments.Count + ";transcript=" + ExpectedTranscript);
        }

        private static Evidence Run(string root, string wav, bool openVino)
        {
            AudioUnderstandingProfile profile = openVino ? AudioUnderstandingProfiles.CreateWav2Vec2Base960hOpenVino() : AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx(); AudioArtifactContract contract = profile.GetArtifact(AudioArtifactRole.CtcEncoderHead); BackendId backend = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId; string model = Path.Combine(root, openVino ? "openvino" : "onnx", "wav2vec2-base-960h-ctc" + (openVino ? ".xml" : ".onnx"));
            using var registry = new BackendRegistry(); if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime(); var bundle = new AudioUnderstandingBundle(profile, new[] { new AudioArtifactBinding(AudioArtifactRole.CtcEncoderHead, contract.CreateArtifact(model, backend)) }); var vocabulary = Wav2Vec2CtcVocabulary.Load(Path.Combine(root, "checkpoint", "vocab.json"), profile.Tokenizer!);
            using var session = new AudioUnderstandingSession(registry, bundle, vocabulary, new BackendRequest(BackendCapabilities.TensorInference, backend, openVino ? "CPU" : "cpu")); using PreparedAudioInput input = new OpenCvAudioInputFactory().CreateFromWavFile(wav, profile, "LibriSpeech CC-BY-4.0 row 6930-75918-0000", "openslr/librispeech_asr:clean/test:0"); AudioStateSummary state = session.SetAudio(input); AudioTranscriptionResult result = session.Transcribe(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "en", true, "stage28-real")); session.Clear(); Assert.AreEqual(VisualErrorCodes.AudioStateInvalid, Assert.ThrowsExactly<VisualException>(() => session.Transcribe(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "en"))).ErrorCode); return new Evidence(state, result);
        }

        private static int[] ReadOfficialFrames(string path)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path)); return document.RootElement.GetProperty("frameDecisions").EnumerateArray().Select(value => value.GetProperty("tokenId").GetInt32()).ToArray();
        }
        private static string HashIds(int[] values) { var bytes = new byte[values.Length * sizeof(long)]; for (int index = 0; index < values.Length; index++) Buffer.BlockCopy(BitConverter.GetBytes((long)values[index]), 0, bytes, index * sizeof(long), sizeof(long)); using SHA256 hash = SHA256.Create(); return string.Concat(hash.ComputeHash(bytes).Select(value => value.ToString("x2"))); }
        private static object Json(Evidence evidence) => new { stateIdentity = evidence.State.StateIdentity, sourceIdentity = evidence.State.SourceIdentity, sourceSha256 = evidence.State.SourceSha256, featureSha256 = evidence.State.FeatureSha256, processorIdentity = evidence.State.ProcessorIdentity, sampleRate = evidence.State.SampleRate, sourceChannels = evidence.State.SourceChannels, sampleCount = evidence.State.SampleCount, transcript = evidence.Result.Decoded.Transcript, rawFrameTokenIds = evidence.Result.Decoded.FrameTokenIds, collapsedTokenIds = evidence.Result.Decoded.CollapsedTokenIds, segments = evidence.Result.Decoded.Segments.Select(value => new { value.TokenId, value.Token, value.StartFrame, value.EndFrameExclusive, startSeconds = value.Start.TotalSeconds, endSeconds = value.End.TotalSeconds, value.MeanSelectedProbability }).ToArray(), evidence.Result.ConfidenceSemantics };
        private static void Require(string path) { if (!File.Exists(path) && !Directory.Exists(path)) Assert.Inconclusive("External Stage 28 asset is missing: " + path); }
        private sealed class Evidence { internal Evidence(AudioStateSummary state, AudioTranscriptionResult result) { State = state; Result = result; } internal AudioStateSummary State { get; } internal AudioTranscriptionResult Result { get; } }
    }
}
