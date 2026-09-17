using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Bounds opt-in recognition retries at alternative right angles. / 限制显式启用的其他直角方向识别重试。</summary>
    public sealed class OcrOrientationRetryOptions
    {
        /// <summary>Initializes REC-confidence retries; rotations are relative to the initial post-CLS crop, not cumulative. / 初始化基于 REC 置信度的重试；旋转相对于初次 CLS 后裁剪，不逐次累积。</summary>
        public OcrOrientationRetryOptions(float confidenceThreshold = .8f, float minimumConfidenceGain = .05f,
            int maximumRegionsPerImage = 16, IEnumerable<TextOrientation>? rotations = null, int maximumCropsPerImage = 1024)
        {
            ValidateScore(confidenceThreshold, nameof(confidenceThreshold));
            ValidateScore(minimumConfidenceGain, nameof(minimumConfidenceGain));
            if (maximumRegionsPerImage < 1 || maximumRegionsPerImage > 4096) throw new ArgumentOutOfRangeException(nameof(maximumRegionsPerImage));
            if (maximumCropsPerImage < 1 || maximumCropsPerImage > 4096) throw new ArgumentOutOfRangeException(nameof(maximumCropsPerImage));
            var values = new List<TextOrientation>();
            foreach (TextOrientation value in rotations ?? new[] { TextOrientation.Degrees180 })
            {
                if (!Enum.IsDefined(typeof(TextOrientation), value) || value == TextOrientation.Degrees0 || values.Contains(value)) throw new ArgumentException("Retry rotations must be distinct, nonzero right angles.", nameof(rotations));
                if (values.Count >= 3) throw new ArgumentOutOfRangeException(nameof(rotations));
                values.Add(value);
            }
            if (values.Count == 0) throw new ArgumentException("At least one retry rotation is required.", nameof(rotations));
            ConfidenceThreshold = confidenceThreshold; MinimumConfidenceGain = minimumConfidenceGain;
            MaximumRegionsPerImage = maximumRegionsPerImage; Rotations = values.AsReadOnly();
            MaximumCropsPerImage = maximumCropsPerImage;
        }
        /// <summary>Gets the REC confidence below which a nonempty line is retried; empty lines always qualify. / 获取非空行触发重试的 REC 置信度下限；空行始终符合条件。</summary>
        public float ConfidenceThreshold { get; }
        /// <summary>Gets the minimum confidence improvement to replace a nonempty incumbent; ties never replace it. / 获取替换非空当前结果所需的最小置信度增量；平局不替换。</summary>
        public float MinimumConfidenceGain { get; }
        /// <summary>Gets the maximum eligible lines admitted in reading order, not the number of crops. / 获取按阅读顺序接收的最大符合条件行数，不是裁剪数量。</summary>
        public int MaximumRegionsPerImage { get; }
        /// <summary>Gets at most three ordered rotations relative to the initial crop. / 获取相对于初始裁剪的最多三个有序旋转。</summary>
        public IReadOnlyList<TextOrientation> Rotations { get; }
        /// <summary>Gets the total additional crops across all rounds, excluding mandatory minimum-batch duplicates. / 获取所有轮次的额外裁剪总数上限，不含模型最小批量所需的重复行。</summary>
        public int MaximumCropsPerImage { get; }
        private static void ValidateScore(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(name);
        }
        internal bool NeedsRetry(RecognizedText text) => text.Text.Length == 0 || text.Confidence < ConfidenceThreshold;
        internal bool Prefer(RecognizedText candidate, RecognizedText current) => candidate.Text.Length != 0
            && (current.Text.Length == 0 || (candidate.Confidence > current.Confidence && candidate.Confidence - current.Confidence >= MinimumConfidenceGain));

        internal static TextOrientation Rotate(TextOrientation initial, TextOrientation relative)
        {
            int Steps(TextOrientation orientation) => orientation == TextOrientation.Degrees0 ? 0 : orientation == TextOrientation.Clockwise90 ? 1 : orientation == TextOrientation.Degrees180 ? 2 : 3;
            int steps = (Steps(initial) + Steps(relative)) % 4;
            return steps == 0 ? TextOrientation.Degrees0 : steps == 1 ? TextOrientation.Clockwise90 : steps == 2 ? TextOrientation.Degrees180 : TextOrientation.CounterClockwise90;
        }
    }

    /// <summary>Preserves one original or retry recognition without copying pixel or token buffers. / 保留一次原始或重试识别，不复制像素或 token 缓冲。</summary>
    public sealed class OcrOrientationAttempt
    {
        internal OcrOrientationAttempt(OcrRegionResult result)
        { Orientation = result.Region.Orientation; Recognition = result.Recognition; RecognitionWidth = result.RecognitionWidth; RecognitionWindows = result.RecognitionWindows; CropDiagnostics = result.CropDiagnostics; }
        private OcrOrientationAttempt(OcrOrientationAttempt source, int index)
        {
            Orientation = source.Orientation; Recognition = source.Recognition.WithSourceRegionIndex(index); RecognitionWidth = source.RecognitionWidth;
            var windows = new List<OcrRecognitionWindowResult>(source.RecognitionWindows.Count);
            foreach (OcrRecognitionWindowResult window in source.RecognitionWindows)
                windows.Add(new OcrRecognitionWindowResult(window.Index, window.Start, window.End, window.Recognition.WithSourceRegionIndex(index), window.Width, window.RemovedPrefixTokens, window.SeamUncertain));
            RecognitionWindows = windows.AsReadOnly();
            CropDiagnostics = source.CropDiagnostics;
        }
        /// <summary>Gets the effective absolute crop rotation. / 获取生效的绝对裁剪旋转。</summary>
        public TextOrientation Orientation { get; }
        /// <summary>Gets the unmodified text, confidence and token trace. / 获取未修改的文本、置信度和 token 追踪。</summary>
        public RecognizedText Recognition { get; }
        /// <summary>Gets actual recognition width diagnostics. / 获取实际识别宽度诊断。</summary>
        public OcrRecognitionWidthInfo? RecognitionWidth { get; }
        /// <summary>Gets any underlying recognition windows and uncertain seams. / 获取底层识别窗口及不确定接缝。</summary>
        public IReadOnlyList<OcrRecognitionWindowResult> RecognitionWindows { get; }
        /// <summary>Gets crop-local evidence for this attempt, not just the selected attempt. / 获取本次尝试的裁剪局部证据，而非仅所选尝试。</summary>
        public IReadOnlyList<OcrCropDiagnostics> CropDiagnostics { get; }
        internal OcrOrientationAttempt WithSourceIndex(int index) => index == Recognition.SourceRegionIndex ? this : new OcrOrientationAttempt(this, index);
    }

    /// <summary>Records bounded alternative orientations and the heuristic selection, not proof of accuracy. / 记录有界候选方向与启发式选择，不代表准确率证明。</summary>
    public sealed class OcrOrientationRetryResult
    {
        internal OcrOrientationRetryResult(IReadOnlyList<OcrOrientationAttempt> attempts, int selectedIndex, bool skippedByRegionLimit)
        { Attempts = new List<OcrOrientationAttempt>(attempts).AsReadOnly(); SelectedIndex = selectedIndex; SkippedByRegionLimit = skippedByRegionLimit; }
        /// <summary>Gets attempts in execution order; index zero always preserves the initial result. / 获取执行顺序中的尝试；索引零始终保留初次结果。</summary>
        public IReadOnlyList<OcrOrientationAttempt> Attempts { get; }
        /// <summary>Gets the selected attempt index; zero means the original won. / 获取所选尝试索引；零表示保留初次结果。</summary>
        public int SelectedIndex { get; }
        /// <summary>Gets whether this eligible line was not retried because earlier lines used the image limit. / 获取该符合条件行是否因前面行已用完全图限制而未重试。</summary>
        public bool SkippedByRegionLimit { get; }
        internal OcrOrientationRetryResult WithSourceIndex(int index)
        {
            if (Attempts[0].Recognition.SourceRegionIndex == index) return this;
            var attempts = new List<OcrOrientationAttempt>(Attempts.Count);
            foreach (OcrOrientationAttempt attempt in Attempts) attempts.Add(attempt.WithSourceIndex(index));
            return new OcrOrientationRetryResult(attempts, SelectedIndex, SkippedByRegionLimit);
        }
    }
}
