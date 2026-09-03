using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    /// <summary>Runs the locally exported Whisper three-graph contract end to end when explicitly enabled. / 在显式启用时端到端运行本地导出的 Whisper 三图合同。</summary>
    [TestClass]
    public sealed class Stage28WhisperExternalIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LocalWhisperTinyEnglishThreeGraphGreedyParityRunsThroughCSharpSession()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_RUN_EXTERNAL_MODELS"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_RUN_EXTERNAL_MODELS=1 to run the local Whisper three-graph evidence.");
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_WHISPER_ONNX_DIR") ?? @"E:\DeploySharp-Models\whisper-tiny.en\onnx-whisper-three-graph";
            string checkpoint = Environment.GetEnvironmentVariable("DEPLOYSHARP_WHISPER_CHECKPOINT") ?? @"E:\DeploySharp-Models\whisper-tiny.en\checkpoint";
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWhisperTinyEnglishOnnx();
            string encoderPath = Path.Combine(root, "whisper-tiny.en-encoder.onnx");
            string prefillPath = Path.Combine(root, "whisper-tiny.en-decoder-prefill.onnx");
            string decodePath = Path.Combine(root, "whisper-tiny.en-decoder-with-past.onnx");
            AssertFile(encoderPath, profile.GetArtifact(AudioArtifactRole.WhisperEncoder).Sha256);
            AssertFile(prefillPath, profile.GetArtifact(AudioArtifactRole.WhisperDecoderPrefill).Sha256);
            AssertFile(decodePath, profile.GetArtifact(AudioArtifactRole.WhisperDecoderWithPast).Sha256);
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var bundle = new AudioUnderstandingBundle(profile, new[]
            {
                new AudioArtifactBinding(AudioArtifactRole.WhisperEncoder, profile.GetArtifact(AudioArtifactRole.WhisperEncoder).CreateArtifact(encoderPath, OnnxRuntimeBackendProvider.BackendId)),
                new AudioArtifactBinding(AudioArtifactRole.WhisperDecoderPrefill, profile.GetArtifact(AudioArtifactRole.WhisperDecoderPrefill).CreateArtifact(prefillPath, OnnxRuntimeBackendProvider.BackendId)),
                new AudioArtifactBinding(AudioArtifactRole.WhisperDecoderWithPast, profile.GetArtifact(AudioArtifactRole.WhisperDecoderWithPast).CreateArtifact(decodePath, OnnxRuntimeBackendProvider.BackendId))
            });
            using var session = new WhisperUnderstandingSession(registry, bundle, request);
            float[] features = new float[240000]; for (int index = 0; index < features.Length; index++) features[index] = -1f + (2f * index / (features.Length - 1));
            using var input = new PreparedWhisperInput(profile, "input_features", new Tensor<float>(new TensorShape(1, 80, 3000), features, TensorBufferOwnership.Transfer), "whisper-synthetic-linspace", new string('1', 64), new string('2', 64), TimeSpan.Zero);
            WhisperEncodedState state = session.SetAudio(input);
            Assert.AreEqual(new TensorShape(1, 1500, 384), new TensorShape(state.Shape));
            var tokenizer = new WhisperTokenizer(checkpoint, profile.Generation!);
            WhisperTranscriptionResult result = session.Transcribe(tokenizer, new WhisperTranscriptionRequest(maximumTokens: 8));
            CollectionAssert.AreEqual(new[] { 685, 22648, 60, 50256 }, result.TokenIds.ToArray());
            Assert.AreEqual("[Music]", result.Text);
            Assert.AreEqual(4, result.TokenIds.Count); Assert.IsTrue(result.Timing.Total >= result.Timing.Encode); Console.WriteLine("STAGE28_WHISPER_ORT encoderMs=" + result.Timing.Encode.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";prefillMs=" + result.Timing.Prefill.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";decodeMs=" + result.Timing.DecodeTotal.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";tokens=" + string.Join(",", result.TokenIds));
        }

        private static void AssertFile(string path, string expectedSha256)
        {
            if (!File.Exists(path)) Assert.Inconclusive("A required local Whisper graph is missing: " + path);
            using FileStream stream = File.OpenRead(path);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            string actual = string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
            Assert.AreEqual(expectedSha256, actual, "Whisper graph identity changed: " + path);
        }
    }
}
