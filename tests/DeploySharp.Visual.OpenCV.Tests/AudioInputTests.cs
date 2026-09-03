using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class AudioInputTests
    {
        [TestMethod]
        public void PcmMonoStereoInt16AndFloat32UseOneMixAndNormalization()
        {
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx(); var factory = new OpenCvAudioInputFactory(); short[] mono = { -16000, -8000, 0, 8000, 16000 }; short[] stereo = mono.SelectMany(value => new[] { value, value }).ToArray();
            using PreparedAudioInput first = factory.CreateFromPcm16(mono, 16000, 1, profile, "unit-test-generated", "mono"); using PreparedAudioInput second = factory.CreateFromPcm16(stereo, 16000, 2, profile, "unit-test-generated", "stereo");
            CollectionAssert.AreEqual((float[])first.Tensor.Buffer, (float[])second.Tensor.Buffer); Assert.AreEqual(AudioChannelLayout.Mono, first.Source.Layout); Assert.AreEqual(AudioChannelLayout.StereoInterleaved, second.Source.Layout); Assert.AreNotEqual(first.Source.SourceSha256, second.Source.SourceSha256); Assert.AreEqual(first.FeatureSha256, second.FeatureSha256);
            float[] floats = mono.Select(value => value / 32768f).ToArray(); using PreparedAudioInput third = factory.CreateFromFloat32(floats, 16000, 1, profile, "unit-test-generated", "float"); CollectionAssert.AreEqual((float[])first.Tensor.Buffer, (float[])third.Tensor.Buffer);
            floats[2] = float.NaN; Assert.AreEqual(VisualErrorCodes.AudioNonFinite, Assert.ThrowsExactly<VisualException>(() => factory.CreateFromFloat32(floats, 16000, 1, profile, "unit-test-generated", "nan")).ErrorCode);
        }

        [TestMethod]
        public void WavBytesValidateHeadersTruncationEncodingRateChannelsCapacityAndCancellation()
        {
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx(); var factory = new OpenCvAudioInputFactory(); byte[] valid = Wav(new float[] { -.5f, 0, .5f, 0 }, 16000, 1, false);
            using PreparedAudioInput input = factory.CreateFromWavBytes(valid, profile, "unit-test-generated", "wav"); Assert.AreEqual(4, input.Tensor.Shape[1]); Assert.AreEqual(AudioPcmEncoding.SignedInt16LittleEndian, input.Source.Encoding);
            Assert.AreEqual(VisualErrorCodes.AudioMalformed, Assert.ThrowsExactly<VisualException>(() => factory.CreateFromWavBytes(new byte[44], profile, "unit", "bad")).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.AudioMalformed, Assert.ThrowsExactly<VisualException>(() => factory.CreateFromWavBytes(valid.Take(valid.Length - 1).ToArray(), profile, "unit", "truncated")).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.AudioSampleRateMismatch, Assert.ThrowsExactly<VisualException>(() => factory.CreateFromWavBytes(Wav(new float[] { 0, 1 }, 8000, 1, false), profile, "unit", "rate")).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.AudioChannelMismatch, Assert.ThrowsExactly<VisualException>(() => factory.CreateFromPcm16(new short[] { 1, 2, 3 }, 16000, 3, profile, "unit", "channels")).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.AudioLimitExceeded, Assert.ThrowsExactly<VisualException>(() => factory.CreateFromPcm16(new short[480001], 16000, 1, profile, "unit", "capacity")).ErrorCode);
            byte[] floatWav = Wav(new float[] { -.5f, 0, .5f }, 16000, 1, true); using PreparedAudioInput floatInput = factory.CreateFromWavBytes(floatWav, profile, "unit-test-generated", "float-wav"); Assert.AreEqual(AudioPcmEncoding.Float32LittleEndian, floatInput.Source.Encoding);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); Assert.AreEqual(VisualErrorCodes.AudioCancelled, Assert.ThrowsExactly<VisualException>(() => factory.CreateFromWavBytes(valid, profile, "unit", "cancel", cancellation.Token)).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.AudioContractInvalid, Assert.ThrowsExactly<VisualException>(() => new AudioChunkDescriptor(1, 10, 4, 4, 0, 20)).ErrorCode);
        }

        [TestMethod]
        public void OfficialLibriSpeechWaveformMatchesPinnedProcessorGolden()
        {
            string root = @"E:\DeploySharp-Models\wav2vec2-base-960h"; string wav = Path.Combine(root, "dataset", "6930-75918-0000.wav"); string golden = Path.Combine(root, "evidence", "6930-75918-0000", "input-values.f32");
            if (!File.Exists(wav) || !File.Exists(golden)) Assert.Inconclusive("External Stage 28 Wav2Vec2 evidence is missing.");
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx(); using PreparedAudioInput input = new OpenCvAudioInputFactory().CreateFromWavFile(wav, profile, "LibriSpeech CC-BY-4.0 row 6930-75918-0000", "openslr/librispeech_asr:clean/test:0"); float[] actual = (float[])input.Tensor.Buffer; byte[] bytes = File.ReadAllBytes(golden); var expected = new float[bytes.Length / sizeof(float)]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length); Assert.AreEqual(expected.Length, actual.Length);
            double maximum = 0, mean = 0; for (int index = 0; index < actual.Length; index++) { double difference = Math.Abs(actual[index] - expected[index]); maximum = Math.Max(maximum, difference); mean += difference; } mean /= actual.Length;
            Assert.IsTrue(maximum <= 0.0000005, "max=" + maximum + ";mean=" + mean); Assert.IsTrue(mean <= 0.00000002, "mean=" + mean); Assert.AreEqual("103c3f15eb3715ebc6243d142244128a3ac39b6bfae315baa6b8dc4a8be14aa8", input.Source.SourceSha256); Assert.AreEqual(56080, input.Tensor.Shape[1]);
        }

        [TestMethod]
        public void WhisperWavPreparationMatchesDirectLogMelExtractionWhenCheckpointIsPresent()
        {
#if NET8_0 || NET9_0 || NET10_0
            string checkpoint = Environment.GetEnvironmentVariable("DEPLOYSHARP_WHISPER_CHECKPOINT") ?? @"E:\DeploySharp-Models\whisper-tiny.en\checkpoint";
            if (!Directory.Exists(checkpoint)) Assert.Inconclusive("The pinned Whisper checkpoint is not available: " + checkpoint);
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWhisperTinyEnglishOnnx(); var extractor = new WhisperLogMelExtractor(checkpoint, profile.Processor); var samples = new float[16000]; samples[4000] = 0.5f; byte[] wav = Wav(samples, 16000, 1, false);
            using PreparedWhisperInput input = new OpenCvAudioInputFactory().CreateWhisperFromWavBytes(wav, profile, extractor, "unit-whisper-wav"); Tensor<float> expected = extractor.Extract(samples);
            Assert.AreEqual(new TensorShape(1, 80, 3000), input.Tensor.Shape); CollectionAssert.AreEqual((float[])expected.Buffer, (float[])input.Tensor.Buffer); Assert.AreEqual(64, input.SourceSha256.Length); Assert.AreEqual(64, input.FeatureSha256.Length); Assert.IsTrue(input.PreprocessTime > TimeSpan.Zero); Console.WriteLine("STAGE28_WHISPER_WAV prepMs=" + input.PreprocessTime.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";shape=" + string.Join("x", input.Tensor.Shape.ToArray()));
#else
            Assert.Inconclusive("The managed Whisper WAV preparation requires net8.0 or later.");
#endif
        }

        private static byte[] Wav(float[] values, int sampleRate, int channels, bool float32)
        {
            int bytesPerSample = float32 ? 4 : 2; int dataLength = checked(values.Length * bytesPerSample); using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.ASCII, true); writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataLength); writer.Write(Encoding.ASCII.GetBytes("WAVE")); writer.Write(Encoding.ASCII.GetBytes("fmt ")); writer.Write(16); writer.Write((short)(float32 ? 3 : 1)); writer.Write((short)channels); writer.Write(sampleRate); writer.Write(sampleRate * channels * bytesPerSample); writer.Write((short)(channels * bytesPerSample)); writer.Write((short)(bytesPerSample * 8)); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataLength);
            foreach (float value in values) { if (float32) writer.Write(value); else writer.Write((short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(value * 32768f)))); } writer.Flush(); return stream.ToArray();
        }
    }
}
