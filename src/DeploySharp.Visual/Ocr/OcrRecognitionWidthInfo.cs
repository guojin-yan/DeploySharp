using System;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Controls recognition crops whose aspect-preserving width exceeds the configured limit. / 控制保留长宽比后的宽度超出限制的识别裁剪。</summary>
    public enum RecognitionOverflowMode
    {
        /// <summary>Compress to the bounded width and report the compression, preserving existing behavior. / 压缩到受限宽度并记录压缩，保持既有行为。</summary>
        Clamp = 0,
        /// <summary>Reject before allocating recognition crops or invoking the recognizer. / 在分配识别裁剪或调用识别器之前拒绝。</summary>
        Reject = 1,
        /// <summary>Recognize bounded overlapping windows and merge them into the original region. / 识别有界重叠窗口并合并回原区域。</summary>
        SlidingWindow = 2
    }

    /// <summary>Describes one recognition row's geometric width, bounded width and batch padding without owning image data. / 描述一个识别行的几何宽度、受限宽度和批填充，不持有图像数据。</summary>
    public readonly struct OcrRecognitionWidthInfo
    {
        internal OcrRecognitionWidthInfo(long naturalWidth, int targetWidth, int tensorWidth, RecognitionOverflowMode overflowMode, int windowCount = 1)
        {
            if (naturalWidth <= 0 || targetWidth <= 0 || tensorWidth < targetWidth) throw new ArgumentOutOfRangeException(nameof(targetWidth));
            NaturalWidth = naturalWidth;
            TargetWidth = targetWidth;
            TensorWidth = tensorWidth;
            OverflowMode = overflowMode;
            WindowCount = windowCount;
        }

        /// <summary>Gets ceil(target height times oriented quadrilateral aspect ratio), before minimum, alignment or upper bounds. This is geometric planning, not a measurement of rounded OpenCV raster dimensions. / 获取最小值、对齐和上限处理前的 ceil(目标高度乘以已定向四边形长宽比)；这是几何规划值，不是 OpenCV 取整栅格尺寸的测量值。</summary>
        public long NaturalWidth { get; }
        /// <summary>Gets the bounded/aligned width before batch padding; a merged region reports its largest window width. / 获取批填充前受限和对齐后的宽度；合并区域报告最大窗口宽度。</summary>
        public int TargetWidth { get; }
        /// <summary>Gets submitted tensor width including batch padding; a merged region reports its largest submitted width. / 获取包含批填充的提交张量宽度；合并区域报告最大提交宽度。</summary>
        public int TensorWidth { get; }
        /// <summary>Gets whether the natural width exceeds this region's bounded width; padding alone is not compression. / 获取自然宽度是否超过该区域的受限宽度；仅批填充不属于压缩。</summary>
        public bool WidthClamped => WindowCount <= 1 && NaturalWidth > TargetWidth;
        /// <summary>Gets the number of recognition windows; aggregate widths describe the largest window when this exceeds one. / 获取识别窗口数；大于一时聚合宽度表示最大窗口。</summary>
        public int WindowCount { get; }
        /// <summary>Gets whether submitted width exceeds target width; inspect individual windows for padding within a merged region. / 获取提交宽度是否超过目标宽度；合并区域内部的填充请检查逐窗口诊断。</summary>
        public bool BatchPadded => TensorWidth > TargetWidth;
        /// <summary>Gets the configured overflow policy. / 获取配置的超宽策略。</summary>
        public RecognitionOverflowMode OverflowMode { get; }

        internal OcrRecognitionWidthInfo WithTensorWidth(int width) => new OcrRecognitionWidthInfo(NaturalWidth, TargetWidth, width, OverflowMode, WindowCount);
    }
}
