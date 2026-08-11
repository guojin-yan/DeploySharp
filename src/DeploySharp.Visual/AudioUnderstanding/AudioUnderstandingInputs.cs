using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Describes caller-owned chunk order and overlap without implying streaming support. / 描述调用方拥有的 Chunk 顺序与 Overlap，且不暗示流式支持。</summary>
    public sealed class AudioChunkDescriptor
    {
        /// <summary>Initializes one chunk span in source mono-frame coordinates. / 在源单声道帧坐标中初始化一个 Chunk 区间。</summary>
        public AudioChunkDescriptor(int chunkIndex, long startFrame, int frameCount, int overlapBeforeFrames, int overlapAfterFrames, long totalSourceFrames)
        {
            if (chunkIndex < 0 || startFrame < 0 || frameCount <= 0 || overlapBeforeFrames < 0 || overlapAfterFrames < 0 || overlapBeforeFrames >= frameCount || overlapAfterFrames >= frameCount || totalSourceFrames <= 0 || startFrame + frameCount > totalSourceFrames) throw new VisualException(VisualErrorCodes.AudioContractInvalid, "Audio chunk order or overlap is invalid.");
            ChunkIndex = chunkIndex; StartFrame = startFrame; FrameCount = frameCount; OverlapBeforeFrames = overlapBeforeFrames; OverlapAfterFrames = overlapAfterFrames; TotalSourceFrames = totalSourceFrames; Identity = AudioUnderstandingHash.Text(chunkIndex + "|" + startFrame + "|" + frameCount + "|" + overlapBeforeFrames + "|" + overlapAfterFrames + "|" + totalSourceFrames);
        }
        /// <summary>Gets zero-based caller-owned chunk index. / 获取调用方拥有的零基 Chunk 索引。</summary>
        public int ChunkIndex { get; }
        /// <summary>Gets source start frame. / 获取源起始帧。</summary>
        public long StartFrame { get; }
        /// <summary>Gets chunk frame count including overlap. / 获取包含 Overlap 的 Chunk 帧数。</summary>
        public int FrameCount { get; }
        /// <summary>Gets caller-owned leading overlap frames. / 获取调用方拥有的前置 Overlap 帧数。</summary>
        public int OverlapBeforeFrames { get; }
        /// <summary>Gets caller-owned trailing overlap frames. / 获取调用方拥有的后置 Overlap 帧数。</summary>
        public int OverlapAfterFrames { get; }
        /// <summary>Gets total source frames. / 获取源总帧数。</summary>
        public long TotalSourceFrames { get; }
        /// <summary>Gets complete chunk identity. / 获取完整 Chunk Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Describes the single decoded audio source before model normalization. / 描述模型归一化前唯一解码的音频源。</summary>
    public sealed class AudioSourceDescriptor
    {
        /// <summary>Initializes immutable source provenance. / 初始化不可变来源信息。</summary>
        public AudioSourceDescriptor(string sourceId, string sourceSha256, long sourceByteLength, int sampleRate, int channels, long frameCount, AudioPcmEncoding encoding, AudioChannelLayout layout, string authorization)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || !AudioUnderstandingHash.IsSha256(sourceSha256) || sourceByteLength <= 0 || sampleRate <= 0 || channels <= 0 || frameCount <= 0 || string.IsNullOrWhiteSpace(authorization) || !Enum.IsDefined(typeof(AudioPcmEncoding), encoding) || !Enum.IsDefined(typeof(AudioChannelLayout), layout)) throw AudioFailure.Contract("Audio source metadata is invalid.");
            if ((channels == 1 && layout != AudioChannelLayout.Mono) || (channels == 2 && layout != AudioChannelLayout.StereoInterleaved) || channels > 2) throw new VisualException(VisualErrorCodes.AudioChannelMismatch, "Audio channel count and layout differ.");
            SourceId = sourceId.Trim(); SourceSha256 = sourceSha256.ToLowerInvariant(); SourceByteLength = sourceByteLength; SampleRate = sampleRate; Channels = channels; FrameCount = frameCount; Encoding = encoding; Layout = layout; Authorization = authorization.Trim(); Duration = TimeSpan.FromSeconds((double)frameCount / sampleRate);
            Identity = AudioUnderstandingHash.Text(SourceId + "|" + SourceSha256 + "|" + SourceByteLength + "|" + SampleRate + "|" + Channels + "|" + FrameCount + "|" + Encoding + "|" + Layout + "|" + Authorization);
        }

        /// <summary>Gets caller or dataset source identifier. / 获取调用方或数据集来源标识。</summary>
        public string SourceId { get; }
        /// <summary>Gets exact source-byte SHA-256. / 获取精确源字节 SHA-256。</summary>
        public string SourceSha256 { get; }
        /// <summary>Gets exact source byte length. / 获取精确源字节长度。</summary>
        public long SourceByteLength { get; }
        /// <summary>Gets decoded source sample rate. / 获取已解码源采样率。</summary>
        public int SampleRate { get; }
        /// <summary>Gets decoded source channel count. / 获取已解码源声道数。</summary>
        public int Channels { get; }
        /// <summary>Gets decoded frames before channel mixing. / 获取声道混合前的解码帧数。</summary>
        public long FrameCount { get; }
        /// <summary>Gets decoded PCM encoding. / 获取已解码 PCM 编码。</summary>
        public AudioPcmEncoding Encoding { get; }
        /// <summary>Gets decoded channel layout. / 获取已解码声道布局。</summary>
        public AudioChannelLayout Layout { get; }
        /// <summary>Gets source authorization statement. / 获取来源授权说明。</summary>
        public string Authorization { get; }
        /// <summary>Gets exact decoded duration. / 获取精确解码时长。</summary>
        public TimeSpan Duration { get; }
        /// <summary>Gets complete source identity. / 获取完整来源 Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Contains one owned or borrowed normalized waveform tensor prepared exactly once. / 包含严格一次准备的自有或借用归一化波形张量。</summary>
    public sealed class PreparedAudioInput : IDisposable
    {
        private readonly IDisposable? _ownedResource;
        private bool _disposed;

        /// <summary>Initializes a prepared input from an external media adapter. / 从外部媒体 Adapter 初始化 Prepared Input。</summary>
        public PreparedAudioInput(AudioUnderstandingProfile profile, string inputName, Tensor<float> tensor, AudioSourceDescriptor source, string waveformSha256, string featureSha256, TimeSpan preprocessTime, PreparedInputOwnership ownership = PreparedInputOwnership.Borrowed, IDisposable? ownedResource = null, AudioChunkDescriptor? chunk = null)
        {
            if (profile == null || tensor == null || source == null) throw new ArgumentNullException(profile == null ? nameof(profile) : tensor == null ? nameof(tensor) : nameof(source));
            if (!profile.Executable) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "A source-only audio profile cannot accept executable input.", profileId: profile.ProfileId);
            AudioArtifactContract artifact = profile.GetArtifact(AudioArtifactRole.CtcEncoderHead);
            AudioTensorContract contract = artifact.Inputs[0];
            if (!string.Equals(inputName, contract.Name, StringComparison.Ordinal) || tensor.Shape.Rank != 2 || tensor.Shape[0] != 1 || tensor.Shape[1] <= 0 || tensor.Shape[1] > profile.Processor.MaximumSamples || tensor.Length > contract.MaximumElements) throw AudioFailure.Contract("Prepared waveform shape differs from the profile.", profile.ProfileId, inputName);
            if (source.SampleRate != profile.Processor.SampleRate) throw new VisualException(VisualErrorCodes.AudioSampleRateMismatch, "Prepared audio sample rate differs from the profile.", profileId: profile.ProfileId);
            if (source.Channels > profile.Processor.MaximumChannels) throw new VisualException(VisualErrorCodes.AudioChannelMismatch, "Prepared audio channel count exceeds the profile.", profileId: profile.ProfileId);
            if (!AudioUnderstandingHash.IsSha256(waveformSha256) || !AudioUnderstandingHash.IsSha256(featureSha256) || preprocessTime < TimeSpan.Zero || !Enum.IsDefined(typeof(PreparedInputOwnership), ownership)) throw AudioFailure.Contract("Prepared waveform provenance is invalid.", profile.ProfileId);
            if (ownership == PreparedInputOwnership.Owned && ownedResource == null) throw AudioFailure.Contract("Owned audio input requires a disposable resource.", profile.ProfileId);
            if (ownership == PreparedInputOwnership.Borrowed && ownedResource != null) throw AudioFailure.Contract("Borrowed audio input cannot accept an owned resource.", profile.ProfileId);
            float[] values = (float[])tensor.Buffer; for (int index = 0; index < values.Length; index++) if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) throw new VisualException(VisualErrorCodes.AudioNonFinite, "Prepared audio contains NaN or Infinity.", profileId: profile.ProfileId, tensorName: inputName);
            if (chunk != null && chunk.FrameCount != tensor.Shape[1]) throw AudioFailure.Contract("Audio chunk frame count differs from the prepared tensor.", profile.ProfileId);
            ProfileId = profile.ProfileId; ProfileIdentity = profile.Identity; ProcessorId = profile.Processor.ProcessorId; ProcessorIdentity = profile.Processor.Identity; FeatureIdentity = profile.Processor.FeatureIdentity; InputName = inputName; Tensor = tensor; Source = source; WaveformSha256 = waveformSha256.ToLowerInvariant(); FeatureSha256 = featureSha256.ToLowerInvariant(); PreprocessTime = preprocessTime; Ownership = ownership; _ownedResource = ownedResource; Chunk = chunk;
            Identity = AudioUnderstandingHash.Text(ProfileIdentity + "|" + ProcessorIdentity + "|" + Source.Identity + "|" + WaveformSha256 + "|" + FeatureSha256 + "|" + tensor.Shape[1] + "|" + (chunk == null ? "complete" : chunk.Identity));
        }

        /// <summary>Gets profile ID. / 获取 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets exact profile identity. / 获取精确 Profile Identity。</summary>
        public string ProfileIdentity { get; }
        /// <summary>Gets processor ID. / 获取 Processor ID。</summary>
        public string ProcessorId { get; }
        /// <summary>Gets processor identity. / 获取 Processor Identity。</summary>
        public string ProcessorIdentity { get; }
        /// <summary>Gets waveform/feature contract identity. / 获取波形/特征合同 Identity。</summary>
        public string FeatureIdentity { get; }
        /// <summary>Gets exact backend input name. / 获取精确后端输入名。</summary>
        public string InputName { get; }
        /// <summary>Gets normalized waveform tensor `[1, samples]`. / 获取归一化波形张量 `[1, samples]`。</summary>
        public Tensor<float> Tensor { get; }
        /// <summary>Gets decoded source provenance. / 获取已解码来源信息。</summary>
        public AudioSourceDescriptor Source { get; }
        /// <summary>Gets post-mix pre-normalization float waveform SHA-256. / 获取混音后、归一化前 Float 波形 SHA-256。</summary>
        public string WaveformSha256 { get; }
        /// <summary>Gets normalized feature SHA-256. / 获取归一化 Feature SHA-256。</summary>
        public string FeatureSha256 { get; }
        /// <summary>Gets decode/mix/normalization time for this diagnostic run. / 获取本次诊断运行的解码、混音与归一化时间。</summary>
        public TimeSpan PreprocessTime { get; }
        /// <summary>Gets resource ownership. / 获取资源所有权。</summary>
        public PreparedInputOwnership Ownership { get; }
        /// <summary>Gets optional caller-owned chunk/overlap metadata; executable Stage 28 CTC Session accepts only complete audio. / 获取可选调用方拥有的 Chunk/Overlap 元数据；阶段 28 可执行 CTC Session 仅接受完整音频。</summary>
        public AudioChunkDescriptor? Chunk { get; }
        /// <summary>Gets complete prepared-input identity. / 获取完整 Prepared-input Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets whether the prepared input released its owned resource. / 获取 Prepared Input 是否已释放自有资源。</summary>
        public bool IsDisposed => _disposed;

        /// <summary>Idempotently releases only an explicitly owned external resource. / 幂等释放且仅释放显式拥有的外部资源。</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; _ownedResource?.Dispose(); }
        internal void EnsureUsable() { if (_disposed) throw new VisualException(VisualErrorCodes.AudioDisposed, "The prepared audio input is disposed.", profileId: ProfileId); }
    }

    /// <summary>Defines one bounded speech transcription request. / 定义一个受限语音转录请求。</summary>
    public sealed class AudioTranscriptionRequest
    {
        /// <summary>Initializes a request without implying Beam, sampling, translation, word timestamps, or streaming. / 初始化请求且不暗示 Beam、采样、翻译、单词时间戳或流式能力。</summary>
        public AudioTranscriptionRequest(AudioUnderstandingTask task, string language, bool includeCtcTokenTimestamps = true, string? requestId = null)
        {
            if (task != AudioUnderstandingTask.AutomaticSpeechRecognition && task != AudioUnderstandingTask.CtcTranscription && task != AudioUnderstandingTask.CtcTimestampAlignment) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "The requested audio task is not a CTC transcription task.");
            if (string.IsNullOrWhiteSpace(language)) throw AudioFailure.Contract("Audio request language is required.");
            Task = task; Language = language.Trim().ToLowerInvariant(); IncludeCtcTokenTimestamps = includeCtcTokenTimestamps; string normalizedRequestId = requestId == null ? string.Empty : requestId.Trim(); RequestId = normalizedRequestId.Length == 0 ? null : normalizedRequestId;
        }

        /// <summary>Gets requested task. / 获取请求 Task。</summary>
        public AudioUnderstandingTask Task { get; }
        /// <summary>Gets exact language. / 获取精确语言。</summary>
        public string Language { get; }
        /// <summary>Gets whether CTC token frame spans are returned. / 获取是否返回 CTC Token 帧区间。</summary>
        public bool IncludeCtcTokenTimestamps { get; }
        /// <summary>Gets optional application request ID. / 获取可选应用请求 ID。</summary>
        public string? RequestId { get; }
    }

    /// <summary>Summarizes one atomically cached waveform state. / 汇总一个原子缓存波形状态。</summary>
    public sealed class AudioStateSummary
    {
        internal AudioStateSummary(string stateIdentity, PreparedAudioInput input)
        {
            StateIdentity = stateIdentity; PreparedInputIdentity = input.Identity; SourceIdentity = input.Source.Identity; SourceSha256 = input.Source.SourceSha256; FeatureSha256 = input.FeatureSha256; ProcessorIdentity = input.ProcessorIdentity; SampleRate = input.Source.SampleRate; SourceChannels = input.Source.Channels; SampleCount = checked((int)input.Tensor.Shape[1]); Duration = TimeSpan.FromSeconds((double)SampleCount / SampleRate); PreprocessTime = input.PreprocessTime;
        }
        /// <summary>Gets state identity. / 获取 State Identity。</summary>
        public string StateIdentity { get; }
        /// <summary>Gets prepared-input identity. / 获取 Prepared-input Identity。</summary>
        public string PreparedInputIdentity { get; }
        /// <summary>Gets source identity. / 获取来源 Identity。</summary>
        public string SourceIdentity { get; }
        /// <summary>Gets source SHA-256. / 获取来源 SHA-256。</summary>
        public string SourceSha256 { get; }
        /// <summary>Gets normalized feature SHA-256. / 获取归一化 Feature SHA-256。</summary>
        public string FeatureSha256 { get; }
        /// <summary>Gets processor identity. / 获取 Processor Identity。</summary>
        public string ProcessorIdentity { get; }
        /// <summary>Gets sample rate. / 获取采样率。</summary>
        public int SampleRate { get; }
        /// <summary>Gets original source channels before one mix. / 获取一次混音前的原始源声道数。</summary>
        public int SourceChannels { get; }
        /// <summary>Gets mono model sample count. / 获取模型单声道样本数。</summary>
        public int SampleCount { get; }
        /// <summary>Gets model waveform duration. / 获取模型波形时长。</summary>
        public TimeSpan Duration { get; }
        /// <summary>Gets preprocessing diagnostic duration. / 获取预处理诊断时长。</summary>
        public TimeSpan PreprocessTime { get; }
    }
}
