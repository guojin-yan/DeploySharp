using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents one collapsed CTC token and its frame-aligned span; it is not a word timestamp. / 表示一个折叠 CTC Token 及其帧对齐区间；它不是单词时间戳。</summary>
    public sealed class AudioCtcTokenSegment
    {
        internal AudioCtcTokenSegment(int tokenId, string token, int startFrame, int endFrameExclusive, double secondsPerFrame, float meanSelectedProbability)
        {
            TokenId = tokenId; Token = token; StartFrame = startFrame; EndFrameExclusive = endFrameExclusive; Start = TimeSpan.FromSeconds(startFrame * secondsPerFrame); End = TimeSpan.FromSeconds(endFrameExclusive * secondsPerFrame); MeanSelectedProbability = meanSelectedProbability;
        }
        /// <summary>Gets token ID. / 获取 Token ID。</summary>
        public int TokenId { get; }
        /// <summary>Gets vocabulary token text. / 获取词表 Token 文本。</summary>
        public string Token { get; }
        /// <summary>Gets inclusive start frame. / 获取包含起始帧。</summary>
        public int StartFrame { get; }
        /// <summary>Gets exclusive end frame. / 获取不包含结束帧。</summary>
        public int EndFrameExclusive { get; }
        /// <summary>Gets frame-derived start time. / 获取帧派生起始时间。</summary>
        public TimeSpan Start { get; }
        /// <summary>Gets frame-derived end time. / 获取帧派生结束时间。</summary>
        public TimeSpan End { get; }
        /// <summary>Gets mean selected-class softmax probability across contributing frames. / 获取贡献帧上所选类别 Softmax 概率均值。</summary>
        public float MeanSelectedProbability { get; }
    }

    /// <summary>Contains deterministic greedy CTC output before session provenance is attached. / 包含附加 Session 来源前的确定性 Greedy CTC 输出。</summary>
    public sealed class AudioCtcDecodedResult
    {
        private readonly IReadOnlyList<int> _frameTokenIds;
        private readonly IReadOnlyList<int> _collapsedTokenIds;
        private readonly IReadOnlyList<AudioCtcTokenSegment> _segments;
        internal AudioCtcDecodedResult(string transcript, List<int> frameTokenIds, List<int> collapsedTokenIds, List<AudioCtcTokenSegment> segments)
        {
            Transcript = transcript; _frameTokenIds = new ReadOnlyCollection<int>(frameTokenIds); _collapsedTokenIds = new ReadOnlyCollection<int>(collapsedTokenIds); _segments = new ReadOnlyCollection<AudioCtcTokenSegment>(segments);
        }
        /// <summary>Gets owned transcript text. / 获取自有转录文本。</summary>
        public string Transcript { get; }
        /// <summary>Gets every raw frame argmax token ID. / 获取每个原始帧 Argmax Token ID。</summary>
        public IReadOnlyList<int> FrameTokenIds => _frameTokenIds;
        /// <summary>Gets blank-removed, repeat-collapsed token IDs. / 获取移除 Blank 并折叠重复后的 Token ID。</summary>
        public IReadOnlyList<int> CollapsedTokenIds => _collapsedTokenIds;
        /// <summary>Gets collapsed token frame spans. / 获取折叠 Token 帧区间。</summary>
        public IReadOnlyList<AudioCtcTokenSegment> Segments => _segments;
    }

    /// <summary>Contains one diagnostic execution split; it is not a benchmark distribution. / 包含一次诊断执行拆分；它不是基准分布。</summary>
    public sealed class AudioExecutionTiming
    {
        internal AudioExecutionTiming(TimeSpan preprocess, TimeSpan inference, TimeSpan decode) { Preprocess = preprocess; Inference = inference; Decode = decode; Total = preprocess + inference + decode; }
        /// <summary>Gets input decode/mix/normalization duration. / 获取输入解码、混音与归一化时长。</summary>
        public TimeSpan Preprocess { get; }
        /// <summary>Gets one backend inference duration. / 获取一次后端推理时长。</summary>
        public TimeSpan Inference { get; }
        /// <summary>Gets greedy CTC/timestamp restoration duration. / 获取 Greedy CTC/时间戳还原时长。</summary>
        public TimeSpan Decode { get; }
        /// <summary>Gets the sum of recorded stages. / 获取所记录阶段之和。</summary>
        public TimeSpan Total { get; }
    }

    /// <summary>Owns a complete CTC transcript, raw decisions, provenance, and timing. / 拥有完整 CTC 转录、原始决策、来源与计时。</summary>
    public sealed class AudioTranscriptionResult
    {
        internal AudioTranscriptionResult(AudioTranscriptionRequest request, AudioCtcDecodedResult decoded, AudioStateSummary state, AudioUnderstandingProfile profile, AudioExecutionTiming timing)
        {
            Request = request; Decoded = decoded; State = state; ProfileId = profile.ProfileId; ProfileIdentity = profile.Identity; ArtifactIdentity = profile.ArtifactIdentity; TokenizerIdentity = profile.Tokenizer!.Identity; TimestampIdentity = profile.Timestamps.Identity; SpeakerIdentity = profile.Speaker.Identity; Timing = timing; ConfidenceSemantics = "mean selected-class softmax probability across frames contributing to each collapsed CTC token"; ParseStatus = "success";
        }
        /// <summary>Gets original request. / 获取原始请求。</summary>
        public AudioTranscriptionRequest Request { get; }
        /// <summary>Gets transcript and raw CTC decisions. / 获取转录与原始 CTC 决策。</summary>
        public AudioCtcDecodedResult Decoded { get; }
        /// <summary>Gets cached source/feature state. / 获取缓存来源/Feature State。</summary>
        public AudioStateSummary State { get; }
        /// <summary>Gets profile ID. / 获取 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets exact profile identity. / 获取精确 Profile Identity。</summary>
        public string ProfileIdentity { get; }
        /// <summary>Gets aggregate artifact identity. / 获取聚合工件 Identity。</summary>
        public string ArtifactIdentity { get; }
        /// <summary>Gets tokenizer/vocabulary identity. / 获取 Tokenizer/词表 Identity。</summary>
        public string TokenizerIdentity { get; }
        /// <summary>Gets timestamp conversion identity. / 获取时间戳转换 Identity。</summary>
        public string TimestampIdentity { get; }
        /// <summary>Gets explicit no-speaker identity for this model. / 获取此模型显式无说话人 Identity。</summary>
        public string SpeakerIdentity { get; }
        /// <summary>Gets confidence semantics. / 获取置信度语义。</summary>
        public string ConfidenceSemantics { get; }
        /// <summary>Gets deterministic parse status. / 获取确定性解析状态。</summary>
        public string ParseStatus { get; }
        /// <summary>Gets one-run diagnostic timing. / 获取单次运行诊断计时。</summary>
        public AudioExecutionTiming Timing { get; }
    }
}
