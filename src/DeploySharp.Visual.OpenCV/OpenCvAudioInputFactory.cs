using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Provides bounded WAV/PCM media preparation alongside the OpenCV visual adapters. / 在 OpenCV 视觉 Adapter 旁提供受限 WAV/PCM 媒体准备。</summary>
    /// <remarks>This adapter performs one decode, one optional stereo mean, and one profile-bound normalization. It does not provide VAD, resampling, diarization, timestamp repair, language detection, or streaming. / 此 Adapter 执行一次解码、一次可选立体声均值与一次 Profile-bound 归一化；不提供 VAD、重采样、说话人分离、时间戳修复、语言检测或流式处理。</remarks>
    public sealed class OpenCvAudioInputFactory
    {
        /// <summary>Reads one WAV file exactly once and prepares its model waveform. / 严格一次读取 WAV 文件并准备模型波形。</summary>
        public PreparedAudioInput CreateFromWavFile(string path, AudioUnderstandingProfile profile, string authorization, string? sourceId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An audio path is required.", nameof(path));
            try
            {
                cancellationToken.ThrowIfCancellationRequested(); byte[] bytes = File.ReadAllBytes(path); cancellationToken.ThrowIfCancellationRequested();
                return CreateFromWavBytesCore(bytes, profile, authorization, sourceId ?? Path.GetFileName(path), cancellationToken);
            }
            catch (OperationCanceledException exception) { throw new VisualException(VisualErrorCodes.AudioCancelled, "Audio preparation was cancelled.", exception, profileId: profile?.ProfileId); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw new VisualException(VisualErrorCodes.AudioMalformed, "The WAV file could not be read.", exception, profileId: profile?.ProfileId, technicalDetails: path); }
        }

#if NET8_0 || NET9_0 || NET10_0
        /// <summary>Reads one WAV file and prepares the profile-bound Whisper log-Mel tensor. / 读取一个 WAV 文件并准备 Profile-bound Whisper log-Mel 张量。</summary>
        /// <remarks>The extractor is supplied by the caller so its verified processor configuration and Mel workspace can be reused across requests. This method performs one WAV decode, one optional stereo mix, and one fixed `[1,80,3000]` extraction; it does not resample. / Extractor 由调用方提供，以便复用已校验的 Processor 配置和 Mel 工作区。本方法执行一次 WAV 解码、一次可选立体声混音和一次固定 `[1,80,3000]` 提取；不执行重采样。</remarks>
        public PreparedWhisperInput CreateWhisperFromWavFile(string path, AudioUnderstandingProfile profile, WhisperLogMelExtractor extractor, string? sourceId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An audio path is required.", nameof(path));
            try
            {
                cancellationToken.ThrowIfCancellationRequested(); byte[] bytes = File.ReadAllBytes(path); cancellationToken.ThrowIfCancellationRequested();
                return CreateWhisperFromWavBytesCore(bytes, profile, extractor, sourceId ?? Path.GetFileName(path), cancellationToken);
            }
            catch (OperationCanceledException exception) { throw new VisualException(VisualErrorCodes.AudioCancelled, "Audio preparation was cancelled.", exception, profileId: profile?.ProfileId); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw new VisualException(VisualErrorCodes.AudioMalformed, "The WAV file could not be read.", exception, profileId: profile?.ProfileId, technicalDetails: path); }
        }

        /// <summary>Decodes one WAV byte array and prepares the profile-bound Whisper log-Mel tensor. / 解码一个 WAV 字节数组并准备 Profile-bound Whisper log-Mel 张量。</summary>
        public PreparedWhisperInput CreateWhisperFromWavBytes(byte[] bytes, AudioUnderstandingProfile profile, WhisperLogMelExtractor extractor, string sourceId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            try { return CreateWhisperFromWavBytesCore(bytes, profile, extractor, sourceId, cancellationToken); }
            catch (OperationCanceledException exception) { throw new VisualException(VisualErrorCodes.AudioCancelled, "Audio preparation was cancelled.", exception, profileId: profile?.ProfileId); }
        }
#endif

        /// <summary>Decodes one in-memory WAV byte array exactly once. / 严格一次解码内存 WAV 字节数组。</summary>
        public PreparedAudioInput CreateFromWavBytes(byte[] bytes, AudioUnderstandingProfile profile, string authorization, string sourceId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            try { return CreateFromWavBytesCore(bytes, profile, authorization, sourceId, cancellationToken); }
            catch (OperationCanceledException exception) { throw new VisualException(VisualErrorCodes.AudioCancelled, "Audio preparation was cancelled.", exception, profileId: profile?.ProfileId); }
        }

        /// <summary>Prepares interleaved signed-int16 PCM without interpreting a WAV header. / 准备交错 Signed-int16 PCM 且不解释 WAV Header。</summary>
        public PreparedAudioInput CreateFromPcm16(short[] interleavedSamples, int sampleRate, int channels, AudioUnderstandingProfile profile, string authorization, string sourceId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (interleavedSamples == null) throw new ArgumentNullException(nameof(interleavedSamples));
            try
            {
                var bytes = new byte[checked(interleavedSamples.Length * sizeof(short))]; Buffer.BlockCopy(interleavedSamples, 0, bytes, 0, bytes.Length);
                var decoded = new float[interleavedSamples.Length]; for (int index = 0; index < decoded.Length; index++) decoded[index] = interleavedSamples[index] / 32768f;
                return Prepare(decoded, bytes, sampleRate, channels, AudioPcmEncoding.SignedInt16LittleEndian, profile, authorization, sourceId, cancellationToken, TimeSpan.Zero);
            }
            catch (OperationCanceledException exception) { throw new VisualException(VisualErrorCodes.AudioCancelled, "Audio preparation was cancelled.", exception, profileId: profile?.ProfileId); }
        }

        /// <summary>Prepares interleaved float32 PCM and rejects non-finite samples. / 准备交错 Float32 PCM 并拒绝非有限样本。</summary>
        public PreparedAudioInput CreateFromFloat32(float[] interleavedSamples, int sampleRate, int channels, AudioUnderstandingProfile profile, string authorization, string sourceId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (interleavedSamples == null) throw new ArgumentNullException(nameof(interleavedSamples));
            try
            {
                var copy = (float[])interleavedSamples.Clone(); var bytes = new byte[checked(copy.Length * sizeof(float))]; Buffer.BlockCopy(copy, 0, bytes, 0, bytes.Length);
                return Prepare(copy, bytes, sampleRate, channels, AudioPcmEncoding.Float32LittleEndian, profile, authorization, sourceId, cancellationToken, TimeSpan.Zero);
            }
            catch (OperationCanceledException exception) { throw new VisualException(VisualErrorCodes.AudioCancelled, "Audio preparation was cancelled.", exception, profileId: profile?.ProfileId); }
        }

        private static PreparedAudioInput CreateFromWavBytesCore(byte[] bytes, AudioUnderstandingProfile profile, string authorization, string sourceId, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew(); cancellationToken.ThrowIfCancellationRequested();
            WavData wav = DecodeWav(bytes, profile); cancellationToken.ThrowIfCancellationRequested(); watch.Stop();
            return Prepare(wav.Samples, bytes, wav.SampleRate, wav.Channels, wav.Encoding, profile, authorization, sourceId, cancellationToken, watch.Elapsed);
        }

#if NET8_0 || NET9_0 || NET10_0
        private static PreparedWhisperInput CreateWhisperFromWavBytesCore(byte[] bytes, AudioUnderstandingProfile profile, WhisperLogMelExtractor extractor, string sourceId, CancellationToken cancellationToken)
        {
            if (extractor == null) throw new ArgumentNullException(nameof(extractor));
            var watch = Stopwatch.StartNew(); cancellationToken.ThrowIfCancellationRequested();
            WavData wav = DecodeWav(bytes, profile); cancellationToken.ThrowIfCancellationRequested();
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!profile.Executable || profile.Family != AudioUnderstandingFamily.Whisper || profile.Generation == null) throw AudioError(VisualErrorCodes.AudioCapabilityUnavailable, "This media adapter executes only an executable Whisper profile.", profile);
            if (!string.Equals(extractor.ProcessorIdentity, profile.Processor.Identity, StringComparison.Ordinal) || !string.Equals(extractor.FeatureIdentity, profile.Processor.FeatureIdentity, StringComparison.Ordinal)) throw AudioError(VisualErrorCodes.AudioIdentityMismatch, "The Whisper log-Mel extractor does not match the profile processor contract.", profile);
            AudioProcessorContract processor = profile.Processor;
            if (!processor.Encodings.Contains(wav.Encoding)) throw AudioError(VisualErrorCodes.AudioMalformed, "PCM encoding is not accepted by the profile.", profile);
            if (wav.SampleRate != processor.SampleRate) throw AudioError(VisualErrorCodes.AudioSampleRateMismatch, "The model requires native 16000 Hz audio; this adapter does not perform unverified resampling.", profile);
            if (wav.Channels != 1 && wav.Channels != 2) throw AudioError(VisualErrorCodes.AudioChannelMismatch, "Only mono or interleaved stereo PCM is supported.", profile);
            if (wav.Samples.Length % wav.Channels != 0) throw AudioError(VisualErrorCodes.AudioChannelMismatch, "PCM sample count and channel layout are inconsistent.", profile);
            int frames = wav.Samples.Length / wav.Channels;
            if (frames <= 0 || frames > processor.MaximumSamples) throw AudioError(VisualErrorCodes.AudioLimitExceeded, "Audio duration exceeds the profile capacity.", profile);
            float[] mono = wav.Samples;
            if (wav.Channels == 2)
            {
                mono = new float[frames];
                for (int frame = 0; frame < frames; frame++)
                {
                    cancellationToken.ThrowIfCancellationRequested(); float value = (wav.Samples[frame * 2] + wav.Samples[(frame * 2) + 1]) * 0.5f;
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw AudioError(VisualErrorCodes.AudioNonFinite, "PCM contains NaN or Infinity.", profile); mono[frame] = value;
                }
            }
            string sourceSha = HashBytes(bytes); Tensor<float> features = extractor.Extract(mono, cancellationToken); string featureSha = HashFloats((float[])features.Buffer); watch.Stop();
            return new PreparedWhisperInput(profile, "input_features", features, sourceId, sourceSha, featureSha, watch.Elapsed);
        }
#endif

        private static PreparedAudioInput Prepare(float[] interleaved, byte[] sourceBytes, int sampleRate, int channels, AudioPcmEncoding encoding, AudioUnderstandingProfile profile, string authorization, string sourceId, CancellationToken cancellationToken, TimeSpan decodeTime)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(authorization) || string.IsNullOrWhiteSpace(sourceId)) throw AudioError(VisualErrorCodes.AudioContractInvalid, "Audio authorization and source ID are required.", profile);
            if (!profile.Executable || profile.Family != AudioUnderstandingFamily.Wav2Vec2) throw AudioError(VisualErrorCodes.AudioCapabilityUnavailable, "This media adapter executes only the audited Wav2Vec2 CTC profile.", profile);
            AudioProcessorContract processor = profile.Processor;
            if (!processor.Encodings.Contains(encoding)) throw AudioError(VisualErrorCodes.AudioMalformed, "PCM encoding is not accepted by the profile.", profile);
            if (sampleRate != processor.SampleRate) throw AudioError(VisualErrorCodes.AudioSampleRateMismatch, "The model requires native 16000 Hz audio; this adapter does not perform unverified resampling.", profile);
            if (channels != 1 && channels != 2) throw AudioError(VisualErrorCodes.AudioChannelMismatch, "Only mono or interleaved stereo PCM is supported.", profile);
            if (channels > processor.MaximumChannels || interleaved.Length == 0 || interleaved.Length % channels != 0) throw AudioError(VisualErrorCodes.AudioChannelMismatch, "PCM sample count and channel layout are inconsistent.", profile);
            int frames = interleaved.Length / channels;
            if (frames > processor.MaximumSamples) throw AudioError(VisualErrorCodes.AudioLimitExceeded, "Audio duration exceeds the profile capacity.", profile);
            var watch = Stopwatch.StartNew(); var mono = new float[frames];
            for (int frame = 0; frame < frames; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested(); float value = channels == 1 ? interleaved[frame] : (interleaved[frame * 2] + interleaved[(frame * 2) + 1]) * 0.5f;
                if (float.IsNaN(value) || float.IsInfinity(value)) throw AudioError(VisualErrorCodes.AudioNonFinite, "PCM contains NaN or Infinity.", profile); mono[frame] = value;
            }
            string waveformSha = HashFloats(mono); var features = processor.NormalizeWaveform ? Normalize(mono, profile, cancellationToken) : (float[])mono.Clone(); string featureSha = HashFloats(features); watch.Stop();
            string sourceSha = HashBytes(sourceBytes); var layout = channels == 1 ? AudioChannelLayout.Mono : AudioChannelLayout.StereoInterleaved;
            var source = new AudioSourceDescriptor(sourceId, sourceSha, sourceBytes.LongLength, sampleRate, channels, frames, encoding, layout, authorization);
            var tensor = new Tensor<float>(new TensorShape(1, frames), features, TensorBufferOwnership.Transfer);
            return new PreparedAudioInput(profile, "input_values", tensor, source, waveformSha, featureSha, decodeTime + watch.Elapsed);
        }

        private static float[] Normalize(float[] samples, AudioUnderstandingProfile profile, CancellationToken cancellationToken)
        {
            double mean = 0; for (int index = 0; index < samples.Length; index++) { cancellationToken.ThrowIfCancellationRequested(); mean += samples[index]; } mean /= samples.Length;
            double variance = 0; for (int index = 0; index < samples.Length; index++) { double difference = samples[index] - mean; variance += difference * difference; } variance /= samples.Length;
            double scale = Math.Sqrt(variance + 1e-7); if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale)) throw AudioError(VisualErrorCodes.AudioNonFinite, "Waveform normalization scale is invalid.", profile);
            var output = new float[samples.Length]; for (int index = 0; index < samples.Length; index++) { float value = (float)((samples[index] - mean) / scale); if (float.IsNaN(value) || float.IsInfinity(value)) throw AudioError(VisualErrorCodes.AudioNonFinite, "Normalized waveform contains NaN or Infinity.", profile); output[index] = value; }
            return output;
        }

        private static WavData DecodeWav(byte[] bytes, AudioUnderstandingProfile profile)
        {
            if (bytes.Length < 44 || Text(bytes, 0) != "RIFF" || Text(bytes, 8) != "WAVE") throw AudioError(VisualErrorCodes.AudioMalformed, "WAV RIFF/WAVE header is missing.", profile);
            uint riffSize = UInt32(bytes, 4); if ((long)riffSize + 8 > bytes.LongLength) throw AudioError(VisualErrorCodes.AudioMalformed, "WAV RIFF payload is truncated.", profile);
            ushort format = 0, channels = 0, bits = 0, blockAlign = 0; uint sampleRate = 0; int dataOffset = -1, dataLength = 0; bool fmt = false;
            int offset = 12;
            while (offset + 8 <= bytes.Length)
            {
                string id = Text(bytes, offset); uint length = UInt32(bytes, offset + 4); long next = (long)offset + 8 + length + (length & 1);
                if (length > int.MaxValue || next > bytes.Length) throw AudioError(VisualErrorCodes.AudioMalformed, "WAV chunk is truncated.", profile);
                if (id == "fmt ")
                {
                    if (fmt || length < 16) throw AudioError(VisualErrorCodes.AudioMalformed, "WAV fmt chunk is missing or duplicated.", profile);
                    format = UInt16(bytes, offset + 8); channels = UInt16(bytes, offset + 10); sampleRate = UInt32(bytes, offset + 12); blockAlign = UInt16(bytes, offset + 20); bits = UInt16(bytes, offset + 22); fmt = true;
                }
                else if (id == "data")
                {
                    if (dataOffset >= 0) throw AudioError(VisualErrorCodes.AudioMalformed, "WAV data chunk is duplicated.", profile); dataOffset = offset + 8; dataLength = checked((int)length);
                }
                offset = checked((int)next);
            }
            if (!fmt || dataOffset < 0 || dataLength == 0) throw AudioError(VisualErrorCodes.AudioMalformed, "WAV fmt or data chunk is missing.", profile);
            AudioPcmEncoding encoding; int bytesPerSample;
            if (format == 1 && bits == 16) { encoding = AudioPcmEncoding.SignedInt16LittleEndian; bytesPerSample = 2; }
            else if (format == 3 && bits == 32) { encoding = AudioPcmEncoding.Float32LittleEndian; bytesPerSample = 4; }
            else throw AudioError(VisualErrorCodes.AudioMalformed, "Only PCM int16 and IEEE float32 WAV encodings are supported.", profile);
            if (channels != 1 && channels != 2) throw AudioError(VisualErrorCodes.AudioChannelMismatch, "WAV channel count is unsupported.", profile);
            if (sampleRate > int.MaxValue || blockAlign != channels * bytesPerSample || dataLength % blockAlign != 0) throw AudioError(VisualErrorCodes.AudioMalformed, "WAV block alignment is invalid.", profile);
            int sampleCount = dataLength / bytesPerSample; var samples = new float[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                int position = checked(dataOffset + (index * bytesPerSample)); float value = encoding == AudioPcmEncoding.SignedInt16LittleEndian ? Int16(bytes, position) / 32768f : Single(bytes, position);
                if (float.IsNaN(value) || float.IsInfinity(value)) throw AudioError(VisualErrorCodes.AudioNonFinite, "WAV contains NaN or Infinity.", profile); samples[index] = value;
            }
            return new WavData(samples, checked((int)sampleRate), channels, encoding);
        }

        private static string Text(byte[] bytes, int offset) => new string(new[] { (char)bytes[offset], (char)bytes[offset + 1], (char)bytes[offset + 2], (char)bytes[offset + 3] });
        private static ushort UInt16(byte[] bytes, int offset) => (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        private static short Int16(byte[] bytes, int offset) => unchecked((short)UInt16(bytes, offset));
        private static uint UInt32(byte[] bytes, int offset) => (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
        private static float Single(byte[] bytes, int offset)
        {
            if (BitConverter.IsLittleEndian) return BitConverter.ToSingle(bytes, offset);
            var copy = new[] { bytes[offset + 3], bytes[offset + 2], bytes[offset + 1], bytes[offset] }; return BitConverter.ToSingle(copy, 0);
        }
        private static string HashFloats(float[] values) { var bytes = new byte[checked(values.Length * sizeof(float))]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length); return HashBytes(bytes); }
        private static string HashBytes(byte[] bytes) { using (SHA256 hash = SHA256.Create()) return string.Concat(hash.ComputeHash(bytes).Select(value => value.ToString("x2"))); }
        private static VisualException AudioError(string code, string message, AudioUnderstandingProfile? profile) => new VisualException(code, message, profileId: profile?.ProfileId);

        private sealed class WavData
        {
            internal WavData(float[] samples, int sampleRate, int channels, AudioPcmEncoding encoding) { Samples = samples; SampleRate = sampleRate; Channels = channels; Encoding = encoding; }
            internal float[] Samples { get; } internal int SampleRate { get; } internal int Channels { get; } internal AudioPcmEncoding Encoding { get; }
        }
    }
}
