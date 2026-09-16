using System;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Controls recognition crops whose aspect-preserving width exceeds the configured limit. / 控制保留长宽比后的宽度超出限制的识别裁剪。</summary>
    public enum RecognitionOverflowMode
    {
        /// <summary>Compress to the bounded width and report the compression, preserving existing behavior. / 压缩到受限宽度并记录压缩，保持既有行为。</summary>
        Clamp = 0,
        /// <summary>Reject before allocating recognition crops or invoking the recognizer. / 在分配识别裁剪或调用识别器之前拒绝。</summary>
        Reject = 1
    }

    /// <summary>Describes one recognition row's geometric width, bounded width and batch padding without owning image data. / 描述一个识别行的几何宽度、受限宽度和批填充，不持有图像数据。</summary>
    public readonly struct OcrRecognitionWidthInfo
    {
        internal OcrRecognitionWidthInfo(long naturalWidth, int targetWidth, int tensorWidth, RecognitionOverflowMode overflowMode)
        {
            if (naturalWidth <= 0 || targetWidth <= 0 || tensorWidth < targetWidth) throw new ArgumentOutOfRangeException(nameof(targetWidth));
            NaturalWidth = naturalWidth;
            TargetWidth = targetWidth;
            TensorWidth = tensorWidth;
            OverflowMode = overflowMode;
        }

        /// <summary>Gets ceil(target height times oriented quadrilateral aspect ratio), before minimum, alignment or upper bounds. This is geometric planning, not a measurement of rounded OpenCV raster dimensions. / 获取最小值、对齐和上限处理前的 ceil(目标高度乘以已定向四边形长宽比)；这是几何规划值，不是 OpenCV 取整栅格尺寸的测量值。</summary>
        public long NaturalWidth { get; }
        /// <summary>Gets the region's bounded/aligned width before padding it to other batch rows. / 获取为其他批行补齐之前，该区域受限和对齐后的宽度。</summary>
        public int TargetWidth { get; }
        /// <summary>Gets the actual submitted tensor width, including padding shared with wider batch rows. / 获取实际提交的张量宽度，包含与更宽批行对齐的填充。</summary>
        public int TensorWidth { get; }
        /// <summary>Gets whether the natural width exceeds this region's bounded width; padding alone is not compression. / 获取自然宽度是否超过该区域的受限宽度；仅批填充不属于压缩。</summary>
        public bool WidthClamped => NaturalWidth > TargetWidth;
        /// <summary>Gets whether batch grouping added width beyond this region's target width. / 获取批分组是否在该区域目标宽度之外增加了填充。</summary>
        public bool BatchPadded => TensorWidth > TargetWidth;
        /// <summary>Gets the configured overflow policy. / 获取配置的超宽策略。</summary>
        public RecognitionOverflowMode OverflowMode { get; }

        internal OcrRecognitionWidthInfo WithTensorWidth(int width) => new OcrRecognitionWidthInfo(NaturalWidth, TargetWidth, width, OverflowMode);
    }
}
