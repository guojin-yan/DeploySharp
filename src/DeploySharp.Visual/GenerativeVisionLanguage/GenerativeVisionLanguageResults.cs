using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Results.Language;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies cached vision state by profile, all artifacts, processor, and exact encoded image. / 按 Profile、全部工件、Processor 与精确编码图像标识缓存的 Vision State。</summary>
    public sealed class GenerativeVisionLanguageImageIdentity
    {
        /// <summary>Initializes an immutable image identity. / 初始化不可变图像 Identity。</summary>
        public GenerativeVisionLanguageImageIdentity(string profileId, string artifactIdentity, string processorSha256, string sourceImageSha256, VisualSize sourceSize, VisualSize modelSize)
        {
            if (string.IsNullOrWhiteSpace(profileId) || !GenerativeVisionLanguageHash.IsSha256(artifactIdentity) || !GenerativeVisionLanguageHash.IsSha256(processorSha256) || !GenerativeVisionLanguageHash.IsSha256(sourceImageSha256)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "Image identity fields are invalid.", profileId: profileId);
            ProfileId = profileId;
            ArtifactIdentity = artifactIdentity.ToLowerInvariant();
            ProcessorSha256 = processorSha256.ToLowerInvariant();
            SourceImageSha256 = sourceImageSha256.ToLowerInvariant();
            SourceSize = sourceSize;
            ModelSize = modelSize;
            Identity = GenerativeVisionLanguageHash.Text(string.Join("|", profileId, ArtifactIdentity, ProcessorSha256, SourceImageSha256, sourceSize.Width, sourceSize.Height, modelSize.Width, modelSize.Height));
        }

        /// <summary>Gets profile ID. / 获取 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets all-artifact identity. / 获取全部工件 Identity。</summary>
        public string ArtifactIdentity { get; }
        /// <summary>Gets processor SHA256. / 获取 Processor SHA256。</summary>
        public string ProcessorSha256 { get; }
        /// <summary>Gets encoded source image SHA256. / 获取编码源图 SHA256。</summary>
        public string SourceImageSha256 { get; }
        /// <summary>Gets source size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets model size. / 获取模型尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets stable composite identity. / 获取稳定组合 Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Reports an owned summary of cached encoder state without exposing its mutable tensor. / 报告缓存 Encoder State 的自有摘要且不公开其可变张量。</summary>
    public sealed class GenerativeVisionLanguageImageState
    {
        /// <summary>Initializes image state evidence. / 初始化图像状态证据。</summary>
        public GenerativeVisionLanguageImageState(GenerativeVisionLanguageImageIdentity identity, string tensorName, long[] shape, string valueSha256, float minimum, float maximum, double l2Norm, TimeSpan encoderTime)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (string.IsNullOrWhiteSpace(tensorName) || shape == null || shape.Length == 0 || shape.Any(value => value <= 0) || !GenerativeVisionLanguageHash.IsSha256(valueSha256) || float.IsNaN(minimum) || float.IsInfinity(minimum) || float.IsNaN(maximum) || float.IsInfinity(maximum) || double.IsNaN(l2Norm) || double.IsInfinity(l2Norm) || l2Norm < 0) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Encoder state summary is invalid.", profileId: identity.ProfileId);
            TensorName = tensorName;
            Shape = new ReadOnlyCollection<long>((long[])shape.Clone());
            ValueSha256 = valueSha256.ToLowerInvariant();
            Minimum = minimum;
            Maximum = maximum;
            L2Norm = l2Norm;
            EncoderTime = encoderTime;
        }

        /// <summary>Gets image identity. / 获取图像 Identity。</summary>
        public GenerativeVisionLanguageImageIdentity Identity { get; }
        /// <summary>Gets encoder output name. / 获取 Encoder 输出名。</summary>
        public string TensorName { get; }
        /// <summary>Gets owned output shape. / 获取自有输出 Shape。</summary>
        public IReadOnlyList<long> Shape { get; }
        /// <summary>Gets SHA256 over float values. / 获取 Float 值 SHA256。</summary>
        public string ValueSha256 { get; }
        /// <summary>Gets minimum finite value. / 获取最小有限值。</summary>
        public float Minimum { get; }
        /// <summary>Gets maximum finite value. / 获取最大有限值。</summary>
        public float Maximum { get; }
        /// <summary>Gets L2 norm. / 获取 L2 Norm。</summary>
        public double L2Norm { get; }
        /// <summary>Gets single encoder invocation time. / 获取单次 Encoder 调用时间。</summary>
        public TimeSpan EncoderTime { get; }
    }

    /// <summary>Binds generated tokens to image, prompt, tokenizer, artifacts, config, and completed step count. / 将生成 Token 绑定到图像、Prompt、Tokenizer、工件、配置与完成 Step 数。</summary>
    public sealed class GenerationIdentity
    {
        /// <summary>Initializes generation identity. / 初始化 Generation Identity。</summary>
        public GenerationIdentity(GenerativeVisionLanguageImageIdentity image, string promptSha256, string tokenizerSha256, string generationConfigIdentity, int completedSteps)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
            if (!GenerativeVisionLanguageHash.IsSha256(promptSha256) || !GenerativeVisionLanguageHash.IsSha256(tokenizerSha256) || !GenerativeVisionLanguageHash.IsSha256(generationConfigIdentity) || completedSteps < 0) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "Generation identity is invalid.", profileId: image.ProfileId);
            PromptSha256 = promptSha256.ToLowerInvariant();
            TokenizerSha256 = tokenizerSha256.ToLowerInvariant();
            GenerationConfigIdentity = generationConfigIdentity.ToLowerInvariant();
            CompletedSteps = completedSteps;
            Identity = GenerativeVisionLanguageHash.Text(string.Join("|", image.Identity, PromptSha256, TokenizerSha256, GenerationConfigIdentity, completedSteps));
        }

        /// <summary>Gets image identity. / 获取图像 Identity。</summary>
        public GenerativeVisionLanguageImageIdentity Image { get; }
        /// <summary>Gets normalized prompt/token SHA256. / 获取规范化 Prompt/Token SHA256。</summary>
        public string PromptSha256 { get; }
        /// <summary>Gets tokenizer SHA256. / 获取 Tokenizer SHA256。</summary>
        public string TokenizerSha256 { get; }
        /// <summary>Gets generation config identity. / 获取 Generation Config Identity。</summary>
        public string GenerationConfigIdentity { get; }
        /// <summary>Gets completed decoder step count. / 获取已完成 Decoder Step 数。</summary>
        public int CompletedSteps { get; }
        /// <summary>Gets stable composite identity. / 获取稳定组合 Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Contains one selected token's auditable score. / 包含一个已选择 Token 的可审计分数。</summary>
    public sealed class GenerativeTokenScore
    {
        /// <summary>Initializes a token score. / 初始化 Token 分数。</summary>
        public GenerativeTokenScore(int step, int tokenId, float logit, float logProbability)
        {
            if (step < 0 || tokenId < 0 || float.IsNaN(logit) || float.IsInfinity(logit) || float.IsNaN(logProbability) || float.IsInfinity(logProbability) || logProbability > .00001f) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageGenerationInvalid, "Token score is invalid.");
            Step = step;
            TokenId = tokenId;
            Logit = logit;
            LogProbability = logProbability;
        }

        /// <summary>Gets zero-based decoder step. / 获取从零开始的 Decoder Step。</summary>
        public int Step { get; }
        /// <summary>Gets selected token ID. / 获取已选择 Token ID。</summary>
        public int TokenId { get; }
        /// <summary>Gets selected raw logit. / 获取已选择原始 Logit。</summary>
        public float Logit { get; }
        /// <summary>Gets selected log-softmax probability. / 获取已选择 Log-softmax 概率。</summary>
        public float LogProbability { get; }
    }

    /// <summary>Contains single-invocation stage timings; values are observations, not benchmark claims. / 包含单次调用分阶段 Timing；这些值是观测而非基准结论。</summary>
    public sealed class GenerativeVisionLanguageTiming
    {
        private readonly IReadOnlyList<TimeSpan> _decodeSteps;

        /// <summary>Initializes generation timing. / 初始化生成 Timing。</summary>
        public GenerativeVisionLanguageTiming(TimeSpan promptTokenize, IEnumerable<TimeSpan> decodeSteps, TimeSpan finalDecode)
        {
            if (decodeSteps == null) throw new ArgumentNullException(nameof(decodeSteps));
            PromptTokenize = promptTokenize;
            _decodeSteps = new ReadOnlyCollection<TimeSpan>(decodeSteps.ToList());
            FinalDecode = finalDecode;
            DecoderTotal = TimeSpan.FromTicks(_decodeSteps.Sum(value => value.Ticks));
        }

        /// <summary>Gets prompt/tokenizer time. / 获取 Prompt/Tokenizer 时间。</summary>
        public TimeSpan PromptTokenize { get; }
        /// <summary>Gets each full-prefix decoder step time. / 获取每个全前缀 Decoder Step 时间。</summary>
        public IReadOnlyList<TimeSpan> DecodeSteps => _decodeSteps;
        /// <summary>Gets sum of decoder steps. / 获取 Decoder Step 时间总和。</summary>
        public TimeSpan DecoderTotal { get; }
        /// <summary>Gets final tokenizer decode time. / 获取最终 Tokenizer Decode 时间。</summary>
        public TimeSpan FinalDecode { get; }
    }

    /// <summary>Contains an owned Caption/VQA generation plus provenance, token scores, finish reason, and timing. / 包含自有 Caption/VQA 生成结果及 Provenance、Token 分数、结束原因与 Timing。</summary>
    public sealed class GenerativeVisionLanguageResult
    {
        private readonly IReadOnlyList<GenerativeTokenScore> _tokenScores;

        /// <summary>Initializes an owned generation result. / 初始化自有生成结果。</summary>
        public GenerativeVisionLanguageResult(GenerationResult generation, GenerativeVisionLanguageRequest request, string normalizedPrompt, GenerationIdentity identity, IEnumerable<GenerativeTokenScore> tokenScores, GenerativeVisionLanguageTiming timing)
        {
            Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            NormalizedPrompt = normalizedPrompt ?? throw new ArgumentNullException(nameof(normalizedPrompt));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (tokenScores == null) throw new ArgumentNullException(nameof(tokenScores));
            _tokenScores = new ReadOnlyCollection<GenerativeTokenScore>(tokenScores.ToList());
            if (_tokenScores.Count != generation.TokenIds.Count) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageGenerationInvalid, "Token score and result token counts differ.", profileId: identity.Image.ProfileId);
            Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        }

        /// <summary>Gets common owned text-generation result. / 获取通用自有文本生成结果。</summary>
        public GenerationResult Generation { get; }
        /// <summary>Gets original request. / 获取原始请求。</summary>
        public GenerativeVisionLanguageRequest Request { get; }
        /// <summary>Gets exact normalized/template-applied prompt. / 获取精确规范化/应用模板后的 Prompt。</summary>
        public string NormalizedPrompt { get; }
        /// <summary>Gets image/prompt/tokenizer/config identity. / 获取图像/Prompt/Tokenizer/配置 Identity。</summary>
        public GenerationIdentity Identity { get; }
        /// <summary>Gets owned selected-token scores. / 获取自有已选择 Token 分数。</summary>
        public IReadOnlyList<GenerativeTokenScore> TokenScores => _tokenScores;
        /// <summary>Gets single-invocation timing. / 获取单次调用 Timing。</summary>
        public GenerativeVisionLanguageTiming Timing { get; }
    }
}
