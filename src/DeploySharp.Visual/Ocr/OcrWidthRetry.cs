using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Selects how a wider recognition candidate can affect final text. / 选择更宽识别候选如何影响最终文字。</summary>
    public enum OcrWidthRetrySelectionPolicy
    {
        /// <summary>Collects a candidate but never replaces the original. / 收集候选但从不替换原始结果。</summary>
        PreserveOriginal,
        /// <summary>Allows replacement only after an explicit confidence-gain check. / 仅在通过显式置信度增益检查后允许替换。</summary>
        ConfidenceGain
    }

    /// <summary>Explains the outcome for one wider recognition retry. / 说明一次更宽识别重试的结果。</summary>
    public enum OcrWidthRetryDecision
    {
        /// <summary>Earlier eligible lines consumed the admission limit. / 更早符合条件的行已用完接收上限。</summary>
        RegionLimit,
        /// <summary>The explicit policy preserves the original while retaining the candidate. / 显式策略保留原结果并记录候选。</summary>
        PreservedByPolicy,
        /// <summary>The candidate was empty or did not reach the strict gain threshold. / 候选为空或未达到严格增益阈值。</summary>
        InsufficientGain,
        /// <summary>The candidate was selected by the configured heuristic. / 根据配置启发式选中了候选。</summary>
        CandidateSelected
    }

    /// <summary>Bounds one low-confidence retry using a larger dynamic recognition width. / 限制一次使用更大动态识别宽度的低置信度重试。</summary>
    public sealed class OcrWidthRetryOptions
    {
        /// <summary>Initializes width retry admission, selection, and resource bounds. / 初始化宽度重试接收、选择和资源限制。</summary>
        public OcrWidthRetryOptions(int candidateMaximumWidth, float confidenceThreshold = .8f,
            OcrWidthRetrySelectionPolicy selectionPolicy = OcrWidthRetrySelectionPolicy.PreserveOriginal,
            float minimumConfidenceGain = .05f, int maximumRegionsPerImage = 16, int maximumCropsPerImage = 128)
        {
            if (candidateMaximumWidth <= 0 || candidateMaximumWidth > 65536) throw new ArgumentOutOfRangeException(nameof(candidateMaximumWidth));
            if (!(confidenceThreshold > 0 && confidenceThreshold <= 1)) throw new ArgumentOutOfRangeException(nameof(confidenceThreshold));
            if (!(minimumConfidenceGain >= 0 && minimumConfidenceGain <= 1)) throw new ArgumentOutOfRangeException(nameof(minimumConfidenceGain));
            if (selectionPolicy != OcrWidthRetrySelectionPolicy.PreserveOriginal && selectionPolicy != OcrWidthRetrySelectionPolicy.ConfidenceGain) throw new ArgumentOutOfRangeException(nameof(selectionPolicy));
            if (maximumRegionsPerImage < 1 || maximumRegionsPerImage > 4096) throw new ArgumentOutOfRangeException(nameof(maximumRegionsPerImage));
            if (maximumCropsPerImage < 1 || maximumCropsPerImage > 4096) throw new ArgumentOutOfRangeException(nameof(maximumCropsPerImage));
            CandidateMaximumWidth = candidateMaximumWidth;
            ConfidenceThreshold = confidenceThreshold;
            SelectionPolicy = selectionPolicy;
            MinimumConfidenceGain = minimumConfidenceGain;
            MaximumRegionsPerImage = maximumRegionsPerImage;
            MaximumCropsPerImage = maximumCropsPerImage;
        }

        /// <summary>Gets the candidate maximum tensor width; it must exceed the first-pass crop maximum. / 获取候选最大张量宽度，必须大于首轮裁剪最大宽度。</summary>
        public int CandidateMaximumWidth { get; }
        /// <summary>Gets the exclusive confidence threshold; empty text always qualifies. / 获取排他的置信度阈值，空文字始终符合条件。</summary>
        public float ConfidenceThreshold { get; }
        /// <summary>Gets the candidate selection policy. / 获取候选选择策略。</summary>
        public OcrWidthRetrySelectionPolicy SelectionPolicy { get; }
        /// <summary>Gets the minimum gain for replacing a non-empty original. / 获取替换非空原文所需的最小增益。</summary>
        public float MinimumConfidenceGain { get; }
        /// <summary>Gets the maximum admitted low-confidence regions in one image. / 获取一张图最多接收的低置信度区域数。</summary>
        public int MaximumRegionsPerImage { get; }
        /// <summary>Gets the maximum physical candidate crops including batch padding and windows. / 获取候选物理裁剪最大数，包含批填充和窗口。</summary>
        public int MaximumCropsPerImage { get; }

        internal bool NeedsRetry(RecognizedText text) => text.Text.Length == 0 || text.Confidence < ConfidenceThreshold;

        internal bool Prefer(RecognizedText candidate, RecognizedText original)
            => SelectionPolicy == OcrWidthRetrySelectionPolicy.ConfidenceGain && candidate.Text.Length != 0 &&
                (original.Text.Length == 0 || (candidate.Confidence > original.Confidence && candidate.Confidence - original.Confidence >= MinimumConfidenceGain));
    }

    /// <summary>Retains one width-retry recognition outcome without images or native buffers. / 保留一次宽度重试识别结果，不含图像或原生缓冲。</summary>
    public sealed class OcrWidthRetryAttempt
    {
        private readonly OcrOrientationAttempt _value;

        internal OcrWidthRetryAttempt(OcrRegionResult result) { _value = new OcrOrientationAttempt(result); }
        private OcrWidthRetryAttempt(OcrOrientationAttempt value) { _value = value; }

        /// <summary>Gets the absolute text orientation. / 获取文本绝对方向。</summary>
        public TextOrientation Orientation => _value.Orientation;
        /// <summary>Gets recognition text and CTC trace. / 获取识别文本及 CTC 追踪。</summary>
        public RecognizedText Recognition => _value.Recognition;
        /// <summary>Gets recognition-width provenance. / 获取识别宽度来源。</summary>
        public OcrRecognitionWidthInfo? RecognitionWidth => _value.RecognitionWidth;
        /// <summary>Gets ordered recognition windows, empty for an unsliced line. / 获取有序识别窗口，未切片时为空。</summary>
        public IReadOnlyList<OcrRecognitionWindowResult> RecognitionWindows => _value.RecognitionWindows;
        /// <summary>Gets crop evidence for the selected candidate, without retaining pixels. / 获取所选候选的裁剪证据，不保留像素。</summary>
        public IReadOnlyList<OcrCropDiagnostics> CropDiagnostics => _value.CropDiagnostics;

        internal OcrWidthRetryAttempt WithSourceIndex(int index) => new OcrWidthRetryAttempt(_value.WithSourceIndex(index));
    }

    /// <summary>Records original and wider-candidate outcomes for one OCR region. / 记录一个 OCR 区域的原始与更宽候选结果。</summary>
    public sealed class OcrWidthRetryResult
    {
        internal OcrWidthRetryResult(OcrWidthRetryOptions options, OcrWidthRetryAttempt original, OcrWidthRetryAttempt? candidate, OcrWidthRetryDecision decision)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Original = original ?? throw new ArgumentNullException(nameof(original));
            Candidate = candidate;
            Decision = decision;
        }

        /// <summary>Gets retry thresholds, policy, and resource bounds. / 获取重试阈值、策略和资源限制。</summary>
        public OcrWidthRetryOptions Options { get; }
        /// <summary>Gets the first-pass result. / 获取首轮结果。</summary>
        public OcrWidthRetryAttempt Original { get; }
        /// <summary>Gets the result produced with the wider candidate profile, or null when admission prevented a retry. / 获取使用更宽候选 Profile 产生的结果，因接收限制未重试时为 null。</summary>
        public OcrWidthRetryAttempt? Candidate { get; }
        /// <summary>Gets the explicit selection decision, not an accuracy judgment. / 获取显式选择决策，不代表准确率判断。</summary>
        public OcrWidthRetryDecision Decision { get; }

        internal OcrWidthRetryResult WithSourceIndex(int index) => Original.Recognition.SourceRegionIndex == index ? this
            : new OcrWidthRetryResult(Options, Original.WithSourceIndex(index), Candidate?.WithSourceIndex(index), Decision);
    }
}
