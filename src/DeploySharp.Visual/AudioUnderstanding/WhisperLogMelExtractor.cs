#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Computes the pinned Whisper log-Mel tensor with a reusable FFT workspace. / 使用可复用 FFT 工作区计算固定 Whisper log-Mel Tensor。</summary>
    /// <remarks>The extractor is read-only with respect to the checkpoint and emits exactly `[1,80,3000]`. It uses the model's serialized Mel filters and Whisper reflect-padding/STFT rules; source provenance is attached by <see cref="PreparedWhisperInput"/>. / Extractor 对 Checkpoint 只读且严格输出 `[1,80,3000]`；使用模型序列化 Mel Filter 与 Whisper Reflect-padding/STFT 规则，源 Provenance 由 <see cref="PreparedWhisperInput"/> 附加。</remarks>
    public sealed class WhisperLogMelExtractor : IDisposable
    {
        private const int FftWorkspaceLength = 1024;
        private readonly int _nfft;
        private readonly int _hopLength;
        private readonly int _sampleCount;
        private readonly int _frameCount;
        private readonly int _featureSize;
        private readonly MelBand[] _bands;
        private readonly double[] _window;
        private readonly double[] _chirpReal;
        private readonly double[] _chirpImag;
        private readonly double[] _kernelReal;
        private readonly double[] _kernelImag;
        // Extraction is commonly fanned out across independent audio requests.
        // Keep FFT scratch per worker instead of sharing mutable arrays between
        // callers; the workspace is created once per participating thread and
        // reused by subsequent calls, so the hot path remains allocation-free.
        private readonly ThreadLocal<WhisperFftWorkspace> _workspace = new ThreadLocal<WhisperFftWorkspace>(() => new WhisperFftWorkspace(FftWorkspaceLength, 201));

        /// <summary>Loads and verifies the pinned Whisper feature-extractor configuration. / 加载并校验固定 Whisper Feature Extractor 配置。</summary>
        public WhisperLogMelExtractor(string checkpointDirectory, AudioProcessorContract processor)
        {
            if (string.IsNullOrWhiteSpace(checkpointDirectory)) throw new ArgumentException("A checkpoint directory is required.", nameof(checkpointDirectory));
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            string path = Path.Combine(Path.GetFullPath(checkpointDirectory), "preprocessor_config.json");
            Verify(path, processor.SidecarSha256);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = document.RootElement;
            _nfft = root.GetProperty("n_fft").GetInt32(); _hopLength = root.GetProperty("hop_length").GetInt32(); _sampleCount = root.GetProperty("n_samples").GetInt32(); _frameCount = root.GetProperty("nb_max_frames").GetInt32(); _featureSize = root.GetProperty("feature_size").GetInt32();
            if (_nfft != 400 || _hopLength != 160 || _sampleCount != processor.MaximumSamples || _frameCount != 3000 || _featureSize != 80 || root.GetProperty("sampling_rate").GetInt32() != processor.SampleRate || _nfft >= FftWorkspaceLength) throw AudioFailure.Contract("The Whisper feature-extractor configuration differs from the fixed 16 kHz/400/160/3000 contract.");
            JsonElement filters = root.GetProperty("mel_filters"); if (filters.GetArrayLength() != _featureSize) throw AudioFailure.Contract("Whisper Mel filter count differs from the feature contract.");
            _bands = new MelBand[_featureSize];
            for (int bandIndex = 0; bandIndex < _featureSize; bandIndex++)
            {
                JsonElement row = filters[bandIndex]; if (row.GetArrayLength() != (_nfft / 2) + 1) throw AudioFailure.Contract("Whisper Mel filter width differs from the FFT contract.");
                var values = new float[row.GetArrayLength()]; int first = values.Length; int last = -1;
                for (int bin = 0; bin < values.Length; bin++) { float value = row[bin].GetSingle(); if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) throw AudioFailure.Contract("Whisper Mel filters must contain finite non-negative values."); values[bin] = value; if (value != 0) { if (first == values.Length) first = bin; last = bin; } }
                if (last < first) throw AudioFailure.Contract("Whisper Mel filter cannot be empty.");
                _bands[bandIndex] = new MelBand(first, values, last + 1);
            }
            _window = new double[_nfft]; for (int index = 0; index < _window.Length; index++) _window[index] = 0.5 - (0.5 * Math.Cos((2.0 * Math.PI * index) / _nfft));
            _chirpReal = new double[_nfft]; _chirpImag = new double[_nfft];
            for (int index = 0; index < _nfft; index++) { double angle = Math.PI * index * index / _nfft; _chirpReal[index] = Math.Cos(angle); _chirpImag[index] = -Math.Sin(angle); }
            _kernelReal = new double[FftWorkspaceLength]; _kernelImag = new double[FftWorkspaceLength];
            for (int index = 0; index < _nfft; index++) { double real = _chirpReal[index]; double imag = -_chirpImag[index]; _kernelReal[index] = real; _kernelImag[index] = imag; if (index != 0) { _kernelReal[FftWorkspaceLength - index] = real; _kernelImag[FftWorkspaceLength - index] = imag; } }
            Fft(_kernelReal, _kernelImag, false);
            ProcessorId = processor.ProcessorId; ProcessorIdentity = processor.Identity; FeatureIdentity = processor.FeatureIdentity; SampleRate = processor.SampleRate; FeatureSize = _featureSize; FrameCount = _frameCount; SampleCount = _sampleCount;
        }

        /// <summary>Gets bound processor ID. / 获取绑定 Processor ID。</summary>
        public string ProcessorId { get; }
        /// <summary>Gets bound processor identity. / 获取绑定 Processor Identity。</summary>
        public string ProcessorIdentity { get; }
        /// <summary>Gets declared feature identity. / 获取声明的 Feature Identity。</summary>
        public string FeatureIdentity { get; }
        /// <summary>Gets required sample rate. / 获取要求的采样率。</summary>
        public int SampleRate { get; }
        /// <summary>Gets output Mel band count. / 获取输出 Mel Band 数。</summary>
        public int FeatureSize { get; }
        /// <summary>Gets output frame count. / 获取输出帧数。</summary>
        public int FrameCount { get; }
        /// <summary>Gets fixed padded sample count. / 获取固定补齐样本数。</summary>
        public int SampleCount { get; }

        /// <summary>Releases thread-local FFT workspaces. / 释放线程本地 FFT 工作区。</summary>
        public void Dispose() => _workspace.Dispose();

        /// <summary>Extracts one finite `[1,80,3000]` tensor from mono samples and pads only at the right edge. / 从单声道样本提取一个有限的 `[1,80,3000]` Tensor，仅在右侧补零。</summary>
        public Tensor<float> Extract(float[] monoSamples, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (monoSamples == null) throw new ArgumentNullException(nameof(monoSamples));
            if (monoSamples.Length == 0 || monoSamples.Length > _sampleCount) throw new VisualException(monoSamples.Length == 0 ? VisualErrorCodes.AudioContractInvalid : VisualErrorCodes.AudioLimitExceeded, "Whisper audio sample count is outside the fixed feature capacity.");
            WhisperFftWorkspace workspace = _workspace.Value!;
            double[] realWorkspace = workspace.Real;
            double[] imagWorkspace = workspace.Imag;
            double[] magnitudeWorkspace = workspace.Magnitude;
            var output = new float[_featureSize * _frameCount]; double maximum = double.NegativeInfinity;
            for (int frame = 0; frame < _frameCount; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int start = (frame * _hopLength) - (_nfft / 2); Array.Clear(realWorkspace, 0, realWorkspace.Length); Array.Clear(imagWorkspace, 0, imagWorkspace.Length);
                for (int sample = 0; sample < _nfft; sample++) { int source = ReflectIndex(start + sample, _sampleCount); double value = (source < monoSamples.Length ? monoSamples[source] : 0f) * _window[sample]; realWorkspace[sample] = value * _chirpReal[sample]; imagWorkspace[sample] = value * _chirpImag[sample]; }
                Fft(realWorkspace, imagWorkspace, false);
                for (int index = 0; index < FftWorkspaceLength; index++)
                {
                    double real = (realWorkspace[index] * _kernelReal[index]) - (imagWorkspace[index] * _kernelImag[index]); imagWorkspace[index] = (realWorkspace[index] * _kernelImag[index]) + (imagWorkspace[index] * _kernelReal[index]); realWorkspace[index] = real;
                }
                Fft(realWorkspace, imagWorkspace, true);
                for (int bin = 0; bin <= _nfft / 2; bin++)
                {
                    double real = (realWorkspace[bin] * _chirpReal[bin]) - (imagWorkspace[bin] * _chirpImag[bin]); double imag = (realWorkspace[bin] * _chirpImag[bin]) + (imagWorkspace[bin] * _chirpReal[bin]); double magnitude = (real * real) + (imag * imag);
                    magnitudeWorkspace[bin] = magnitude;
                }
                for (int band = 0; band < _featureSize; band++)
                {
                    MelBand filter = _bands[band]; double energy = 0;
                    for (int bin = filter.Start; bin < filter.End; bin++) energy += magnitudeWorkspace[bin] * filter.Weights[bin];
                    output[(band * _frameCount) + frame] = (float)energy;
                }
            }
            for (int index = 0; index < output.Length; index++) { double value = Math.Log10(Math.Max(output[index], 1e-10)); output[index] = (float)value; if (value > maximum) maximum = value; }
            double floor = maximum - 8.0; for (int index = 0; index < output.Length; index++) { double value = Math.Max(output[index], floor); output[index] = (float)((value + 4.0) / 4.0); if (float.IsNaN(output[index]) || float.IsInfinity(output[index])) throw new VisualException(VisualErrorCodes.AudioNonFinite, "Whisper log-Mel extraction produced NaN or Infinity."); }
            return new Tensor<float>(new TensorShape(1, _featureSize, _frameCount), output, TensorBufferOwnership.Transfer);
        }

        private static int ReflectIndex(int index, int length)
        {
            if (length <= 1) return 0;
            while (index < 0 || index >= length) index = index < 0 ? -index : (2 * length) - 2 - index;
            return index;
        }

        private static void Fft(double[] real, double[] imag, bool inverse)
        {
            int length = real.Length;
            for (int index = 1, reversed = 0; index < length; index++) { int bit = length >> 1; for (; (reversed & bit) != 0; bit >>= 1) reversed ^= bit; reversed ^= bit; if (index < reversed) { (real[index], real[reversed]) = (real[reversed], real[index]); (imag[index], imag[reversed]) = (imag[reversed], imag[index]); } }
            for (int width = 2; width <= length; width <<= 1)
            {
                double angle = (inverse ? 2.0 : -2.0) * Math.PI / width; double stepReal = Math.Cos(angle); double stepImag = Math.Sin(angle);
                for (int offset = 0; offset < length; offset += width) { double twiddleReal = 1; double twiddleImag = 0; int half = width >> 1; for (int index = 0; index < half; index++) { int even = offset + index; int odd = even + half; double productReal = (twiddleReal * real[odd]) - (twiddleImag * imag[odd]); double productImag = (twiddleReal * imag[odd]) + (twiddleImag * real[odd]); double evenReal = real[even]; double evenImag = imag[even]; real[even] = evenReal + productReal; imag[even] = evenImag + productImag; real[odd] = evenReal - productReal; imag[odd] = evenImag - productImag; double nextReal = (twiddleReal * stepReal) - (twiddleImag * stepImag); twiddleImag = (twiddleReal * stepImag) + (twiddleImag * stepReal); twiddleReal = nextReal; } }
            }
            if (inverse) for (int index = 0; index < length; index++) { real[index] /= length; imag[index] /= length; }
        }

        private static void Verify(string path, string expected)
        {
            if (!File.Exists(path)) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "Whisper preprocessor_config.json is missing.", technicalDetails: path);
            using FileStream stream = File.OpenRead(path); using SHA256 hash = SHA256.Create(); string actual = string.Concat(hash.ComputeHash(stream).Select(value => value.ToString("x2")));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "Whisper preprocessor_config.json SHA-256 differs from the processor contract.", technicalDetails: "expected=" + expected + ";actual=" + actual);
        }

        private readonly struct MelBand
        {
            internal MelBand(int start, float[] weights, int end) { Start = start; Weights = weights; End = end; }
            internal int Start { get; }
            internal float[] Weights { get; }
            internal int End { get; }
        }

        private sealed class WhisperFftWorkspace
        {
            internal WhisperFftWorkspace(int fftLength, int magnitudeLength)
            {
                Real = new double[fftLength];
                Imag = new double[fftLength];
                Magnitude = new double[magnitudeLength];
            }

            internal double[] Real { get; }
            internal double[] Imag { get; }
            internal double[] Magnitude { get; }
        }
    }
}
#endif
