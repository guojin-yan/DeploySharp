using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines one bounded Whisper greedy transcription request. / 定义一个受限 Whisper Greedy 转录请求。</summary>
    public sealed class WhisperTranscriptionRequest
    {
        /// <summary>Initializes an English no-timestamps request. / 初始化 English No-timestamps 请求。</summary>
        public WhisperTranscriptionRequest(bool includeNoTimestamps = true, int? maximumTokens = null, string? requestId = null)
        {
            if (maximumTokens.HasValue && maximumTokens.Value <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTokens));
            IncludeNoTimestamps = includeNoTimestamps; MaximumTokens = maximumTokens; string id = requestId == null ? string.Empty : requestId.Trim(); RequestId = id.Length == 0 ? null : id;
        }
        /// <summary>Gets whether the fixed no-timestamps control token is included. / 获取是否包含固定 No-timestamps 控制 Token。</summary>
        public bool IncludeNoTimestamps { get; }
        /// <summary>Gets optional maximum generated token count. / 获取可选最大生成 Token 数。</summary>
        public int? MaximumTokens { get; }
        /// <summary>Gets optional request identifier. / 获取可选请求标识。</summary>
        public string? RequestId { get; }
    }

    /// <summary>Summarizes one cached Whisper encoder result. / 汇总一个缓存的 Whisper Encoder 结果。</summary>
    public sealed class WhisperEncodedState
    {
        internal WhisperEncodedState(string identity, string inputIdentity, string artifactIdentity, string processorIdentity, long[] shape, string featureSha256, TimeSpan encodeTime)
        {
            if (!AudioUnderstandingHash.IsSha256(identity) || !AudioUnderstandingHash.IsSha256(inputIdentity) || string.IsNullOrWhiteSpace(artifactIdentity) || !AudioUnderstandingHash.IsSha256(processorIdentity) || shape == null || !AudioUnderstandingHash.IsSha256(featureSha256)) throw AudioFailure.Contract("Whisper encoded state is invalid.");
            Identity = identity; InputIdentity = inputIdentity; ArtifactIdentity = artifactIdentity; ProcessorIdentity = processorIdentity; Shape = (long[])shape.Clone(); FeatureSha256 = featureSha256; EncodeTime = encodeTime;
        }
        /// <summary>Gets state identity. / 获取 State Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets prepared feature identity. / 获取 Prepared Feature Identity。</summary>
        public string InputIdentity { get; }
        /// <summary>Gets artifact identity. / 获取 Artifact Identity。</summary>
        public string ArtifactIdentity { get; }
        /// <summary>Gets processor identity. / 获取 Processor Identity。</summary>
        public string ProcessorIdentity { get; }
        /// <summary>Gets encoder output shape. / 获取 Encoder 输出形状。</summary>
        public long[] Shape { get; }
        /// <summary>Gets encoder feature SHA-256. / 获取 Encoder Feature SHA-256。</summary>
        public string FeatureSha256 { get; }
        /// <summary>Gets encoder inference time. / 获取 Encoder 推理耗时。</summary>
        public TimeSpan EncodeTime { get; }
    }

    /// <summary>Contains Whisper stage timings for one greedy transcription. / 包含一次 Whisper Greedy 转录的分阶段耗时。</summary>
    public sealed class WhisperExecutionTiming
    {
        private readonly IReadOnlyList<TimeSpan> _decodeSteps;
        internal WhisperExecutionTiming(TimeSpan preprocess, TimeSpan encode, TimeSpan tokenize, TimeSpan prefill, IEnumerable<TimeSpan> decodeSteps, TimeSpan finalDecode)
        {
            Preprocess = preprocess; Encode = encode; Tokenize = tokenize; Prefill = prefill; _decodeSteps = new ReadOnlyCollection<TimeSpan>((decodeSteps ?? throw new ArgumentNullException(nameof(decodeSteps))).ToList()); FinalDecode = finalDecode; DecodeTotal = TimeSpan.FromTicks(_decodeSteps.Sum(value => value.Ticks)); Total = Preprocess + Encode + Tokenize + Prefill + DecodeTotal + FinalDecode;
        }
        /// <summary>Gets feature preparation time. / 获取 Feature 准备耗时。</summary>
        public TimeSpan Preprocess { get; }
        /// <summary>Gets Encoder time. / 获取 Encoder 耗时。</summary>
        public TimeSpan Encode { get; }
        /// <summary>Gets prompt tokenizer time. / 获取 Prompt Tokenizer 耗时。</summary>
        public TimeSpan Tokenize { get; }
        /// <summary>Gets Decoder Prefill time. / 获取 Decoder Prefill 耗时。</summary>
        public TimeSpan Prefill { get; }
        /// <summary>Gets per-token Decode times. / 获取逐 Token Decode 耗时。</summary>
        public IReadOnlyList<TimeSpan> DecodeSteps => _decodeSteps;
        /// <summary>Gets total Decode time. / 获取 Decode 总耗时。</summary>
        public TimeSpan DecodeTotal { get; }
        /// <summary>Gets final text decode time. / 获取最终文本 Decode 耗时。</summary>
        public TimeSpan FinalDecode { get; }
        /// <summary>Gets end-to-end accounted time. / 获取端到端统计耗时。</summary>
        public TimeSpan Total { get; }
    }

    /// <summary>Contains one complete Whisper greedy transcription and immutable provenance. / 包含一次完整 Whisper Greedy 转录及不可变 Provenance。</summary>
    public sealed class WhisperTranscriptionResult
    {
        private readonly IReadOnlyList<int> _tokenIds;
        internal WhisperTranscriptionResult(string text, IReadOnlyList<int> tokenIds, WhisperTranscriptionRequest request, WhisperEncodedState state, WhisperExecutionTiming timing, string profileId, string bundleIdentity)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text)); _tokenIds = new ReadOnlyCollection<int>((tokenIds ?? throw new ArgumentNullException(nameof(tokenIds))).ToList()); Request = request ?? throw new ArgumentNullException(nameof(request)); State = state ?? throw new ArgumentNullException(nameof(state)); Timing = timing ?? throw new ArgumentNullException(nameof(timing)); ProfileId = profileId ?? throw new ArgumentNullException(nameof(profileId)); BundleIdentity = bundleIdentity ?? throw new ArgumentNullException(nameof(bundleIdentity));
        }
        /// <summary>Gets decoded transcript text. / 获取解码转录文本。</summary>
        public string Text { get; }
        /// <summary>Gets generated token IDs excluding the prompt. / 获取不含 Prompt 的生成 Token ID。</summary>
        public IReadOnlyList<int> TokenIds => _tokenIds;
        /// <summary>Gets request. / 获取请求。</summary>
        public WhisperTranscriptionRequest Request { get; }
        /// <summary>Gets encoder state summary. / 获取 Encoder State Summary。</summary>
        public WhisperEncodedState State { get; }
        /// <summary>Gets execution timing. / 获取执行耗时。</summary>
        public WhisperExecutionTiming Timing { get; }
        /// <summary>Gets profile ID. / 获取 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets bundle identity. / 获取 Bundle Identity。</summary>
        public string BundleIdentity { get; }
    }
}
