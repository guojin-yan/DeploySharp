using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Selects how one enhancement candidate can affect final text. / 选择单个增强候选如何影响最终文字。</summary>
    public enum OcrEnhancementSelectionPolicy
    {
        /// <summary>Collects a candidate but never replaces the original. / 收集候选但从不替换原始结果。</summary>
        PreserveOriginal,
        /// <summary>Allows replacement by an explicit confidence-gain heuristic, not accuracy proof. / 允许显式置信度增益启发式替换，不代表准确率证明。</summary>
        ConfidenceGain,
    }

    /// <summary>Explains the outcome for a low-confidence line considered for enhancement retry. / 说明低置信度行的增强重试结果。</summary>
    public enum OcrEnhancementRetryDecision
    {
        /// <summary>No original crop passed the quality gate; no REC was repeated. / 无原裁剪通过质量门限，未重复REC。</summary>
        NoEligibleCrop,
        /// <summary>Earlier eligible lines consumed the region admission limit. / 更早符合条件的行已用完区域接收上限。</summary>
        RegionLimit,
        /// <summary>The explicit policy preserves the original while retaining the candidate. / 显式策略保留原结果并记录候选。</summary>
        PreservedByPolicy,
        /// <summary>The candidate was empty or failed the strict confidence-gain test. / 候选为空或未满足严格置信度增益要求。</summary>
        InsufficientGain,
        /// <summary>The candidate was selected by the configured heuristic. / 根据配置的启发式选中了候选。</summary>
        CandidateSelected,
    }

    /// <summary>Bounds one quality-gated enhancement retry after any orientation retries. / 限制方向重试之后的一次质量门控增强重试。</summary>
    public sealed class OcrEnhancementRetryOptions
    {
        /// <summary>Initializes a single candidate and admission/resource bounds; defaults never replace text. / 初始化单候选及接收和资源限制，默认不替换文字。</summary>
        public OcrEnhancementRetryOptions(OcrCropEnhancementOptions enhancement, float confidenceThreshold = .8f,
            OcrEnhancementSelectionPolicy selectionPolicy = OcrEnhancementSelectionPolicy.PreserveOriginal,
            float minimumConfidenceGain = .05f, int maximumRegionsPerImage = 16, int maximumCropsPerImage = 128)
        {
            Enhancement = enhancement ?? throw new ArgumentNullException(nameof(enhancement));
            if (!(confidenceThreshold > 0 && confidenceThreshold <= 1)) throw new ArgumentOutOfRangeException(nameof(confidenceThreshold));
            if (!(minimumConfidenceGain >= 0 && minimumConfidenceGain <= 1)) throw new ArgumentOutOfRangeException(nameof(minimumConfidenceGain));
            if (selectionPolicy != OcrEnhancementSelectionPolicy.PreserveOriginal && selectionPolicy != OcrEnhancementSelectionPolicy.ConfidenceGain) throw new ArgumentOutOfRangeException(nameof(selectionPolicy));
            if (maximumRegionsPerImage < 1 || maximumRegionsPerImage > 4096) throw new ArgumentOutOfRangeException(nameof(maximumRegionsPerImage));
            if (maximumCropsPerImage < 1 || maximumCropsPerImage > 4096) throw new ArgumentOutOfRangeException(nameof(maximumCropsPerImage));
            ConfidenceThreshold = confidenceThreshold; SelectionPolicy = selectionPolicy; MinimumConfidenceGain = minimumConfidenceGain;
            MaximumRegionsPerImage = maximumRegionsPerImage; MaximumCropsPerImage = maximumCropsPerImage;
        }
        /// <summary>Gets the single candidate operation and quality gates. / 获取单个候选操作及质量门限。</summary>
        public OcrCropEnhancementOptions Enhancement { get; }
        /// <summary>Gets the exclusive confidence threshold; empty text always qualifies. / 获取排他置信度阈值，空文字始终符合条件。</summary>
        public float ConfidenceThreshold { get; }
        /// <summary>Gets the explicit selection policy. / 获取显式选择策略。</summary>
        public OcrEnhancementSelectionPolicy SelectionPolicy { get; }
        /// <summary>Gets minimum gain for nonempty originals; ties never replace them. / 获取非空原文所需最小增益，平局从不替换。</summary>
        public float MinimumConfidenceGain { get; }
        /// <summary>Gets eligible lines admitted in reading order; excess lines retain the original. / 获取按阅读顺序接收的合格行上限，超出行保留原结果。</summary>
        public int MaximumRegionsPerImage { get; }
        /// <summary>Gets extra physical crop rows including windows and minimum-batch padding; excess fails. / 获取包含窗口及最小批次补齐的额外物理裁剪行上限，超限失败。</summary>
        public int MaximumCropsPerImage { get; }
        internal bool NeedsRetry(RecognizedText text) => text.Text.Length == 0 || text.Confidence < ConfidenceThreshold;
        internal bool Prefer(RecognizedText candidate, RecognizedText original) => SelectionPolicy == OcrEnhancementSelectionPolicy.ConfidenceGain
            && candidate.Text.Length != 0 && (original.Text.Length == 0 ||
                (candidate.Confidence > original.Confidence && candidate.Confidence - original.Confidence >= MinimumConfidenceGain));
    }

    /// <summary>Retains one recognition outcome without images or recursive retry traces. / 保留一次识别结果，不含图像或递归重试记录。</summary>
    public sealed class OcrEnhancementAttempt
    {
        private readonly OcrOrientationAttempt _value;
        internal OcrEnhancementAttempt(OcrRegionResult result) { _value = new OcrOrientationAttempt(result); }
        private OcrEnhancementAttempt(OcrOrientationAttempt value) { _value = value; }
        /// <summary>Gets the absolute line orientation; window corners may encode rotation instead. / 获取行绝对方向，窗口可能改用角点编码旋转。</summary>
        public TextOrientation Orientation => _value.Orientation;
        /// <summary>Gets raw text, confidence and token trace. / 获取原始文字、置信度和token记录。</summary>
        public RecognizedText Recognition => _value.Recognition;
        /// <summary>Gets recognition width evidence. / 获取识别宽度证据。</summary>
        public OcrRecognitionWidthInfo? RecognitionWidth => _value.RecognitionWidth;
        /// <summary>Gets ordered window results, empty for an unsliced line. / 获取有序窗口结果，未切分行为空。</summary>
        public IReadOnlyList<OcrRecognitionWindowResult> RecognitionWindows => _value.RecognitionWindows;
        /// <summary>Gets crop evidence in its original evaluation space, retained after ROI projection. / 获取原评估空间裁剪证据，在ROI投影后保留。</summary>
        public IReadOnlyList<OcrCropDiagnostics> CropDiagnostics => _value.CropDiagnostics;
        internal OcrEnhancementAttempt WithSourceIndex(int index) => new OcrEnhancementAttempt(_value.WithSourceIndex(index));
    }

    /// <summary>Records a low-confidence enhancement decision with original and optional candidate. / 记录低置信度增强决策及原始和可选候选结果。</summary>
    public sealed class OcrEnhancementRetryResult
    {
        internal OcrEnhancementRetryResult(OcrEnhancementRetryOptions options, OcrEnhancementAttempt original, OcrEnhancementAttempt? candidate, OcrEnhancementRetryDecision decision)
        { Options = options; Original = original; Candidate = candidate; Decision = decision; }
        /// <summary>Gets configured thresholds, selection policy and limits. / 获取配置的阈值、选择策略及限制。</summary>
        public OcrEnhancementRetryOptions Options { get; }
        /// <summary>Gets the selected pre-enhancement result, after any orientation retries. / 获取增强前所选结果，位于可选方向重试之后。</summary>
        public OcrEnhancementAttempt Original { get; }
        /// <summary>Gets the single candidate; null means no additional recognition ran. / 获取唯一候选，null表示没有执行额外识别。</summary>
        public OcrEnhancementAttempt? Candidate { get; }
        /// <summary>Gets the explicit outcome, not a correctness judgment. / 获取显式结果，不表示正确性判断。</summary>
        public OcrEnhancementRetryDecision Decision { get; }
        internal OcrEnhancementRetryResult WithSourceIndex(int index) => Original.Recognition.SourceRegionIndex == index ? this
            : new OcrEnhancementRetryResult(Options, Original.WithSourceIndex(index), Candidate?.WithSourceIndex(index), Decision);
    }
}
