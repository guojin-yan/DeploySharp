using System;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Selects one explicit low-contrast crop operation, not an automatic recipe. / 选择一种显式低对比度裁剪操作，而非自动配方。</summary>
    public enum OcrCropEnhancementMode
    {
        /// <summary>Scales color channels around sampled luminance mean with bounded gain. / 以采样亮度均值为中心，用有界增益缩放颜色通道。</summary>
        ContrastNormalize,
        /// <summary>Applies contrast-limited local histogram equalization to grayscale content. / 对灰度内容执行限制对比度的局部直方图均衡。</summary>
        GrayClahe,
    }

    /// <summary>Explains whether a requested crop enhancement was applied. / 说明请求的裁剪增强是否应用。</summary>
    public enum OcrCropEnhancementDecision
    {
        /// <summary>No enhancement was requested. / 未请求增强。</summary>
        NotConfigured,
        /// <summary>Contrast already meets the configured threshold. / 对比度已达到配置阈值。</summary>
        SufficientContrast,
        /// <summary>Too little variation exists to enhance safely. / 变化太少，不执行增强。</summary>
        InsufficientVariation,
        /// <summary>The selected low-contrast operation was applied. / 已应用所选低对比度操作。</summary>
        Applied,
    }

    /// <summary>Bounds one opt-in enhancement; thresholds require application-specific validation. / 限制一种显式增强；阈值需要具体应用验证。</summary>
    public sealed class OcrCropEnhancementOptions
    {
        /// <summary>Initializes contrast gates, gain, CLAHE and per-crop pixel bounds. / 初始化对比度门限、增益、CLAHE 及逐裁剪像素限制。</summary>
        public OcrCropEnhancementOptions(OcrCropEnhancementMode mode, double lowContrastThreshold = 32, double minimumContrast = 1,
            double targetStandardDeviation = 64, double maximumGain = 3, double claheClipLimit = 2, int claheGridSize = 8, int maximumPixelsPerCrop = 1048576)
        {
            if (mode != OcrCropEnhancementMode.ContrastNormalize && mode != OcrCropEnhancementMode.GrayClahe) throw new ArgumentOutOfRangeException(nameof(mode));
            if (!(lowContrastThreshold > 0 && lowContrastThreshold <= 128)) throw new ArgumentOutOfRangeException(nameof(lowContrastThreshold));
            if (!(minimumContrast > 0 && minimumContrast < lowContrastThreshold)) throw new ArgumentOutOfRangeException(nameof(minimumContrast));
            if (!(targetStandardDeviation >= lowContrastThreshold && targetStandardDeviation <= 128)) throw new ArgumentOutOfRangeException(nameof(targetStandardDeviation));
            if (!(maximumGain > 1 && maximumGain <= 8)) throw new ArgumentOutOfRangeException(nameof(maximumGain));
            if (!(claheClipLimit > 0 && claheClipLimit <= 16)) throw new ArgumentOutOfRangeException(nameof(claheClipLimit));
            if (claheGridSize < 2 || claheGridSize > 16) throw new ArgumentOutOfRangeException(nameof(claheGridSize));
            if (maximumPixelsPerCrop < 1 || maximumPixelsPerCrop > 16777216) throw new ArgumentOutOfRangeException(nameof(maximumPixelsPerCrop));
            Mode = mode; LowContrastThreshold = lowContrastThreshold; MinimumContrast = minimumContrast;
            TargetStandardDeviation = targetStandardDeviation; MaximumGain = maximumGain; ClaheClipLimit = claheClipLimit;
            ClaheGridSize = claheGridSize; MaximumPixelsPerCrop = maximumPixelsPerCrop;
        }
        /// <summary>Gets the explicit operation. / 获取显式操作。</summary>
        public OcrCropEnhancementMode Mode { get; }
        /// <summary>Gets the exclusive low-contrast threshold in 8-bit luminance standard deviation. / 获取以8位亮度标准差计的低对比度排他阈值。</summary>
        public double LowContrastThreshold { get; }
        /// <summary>Gets the inclusive minimum variation; flatter crops are unchanged. / 获取包含式最小变化量，更平坦裁剪保持不变。</summary>
        public double MinimumContrast { get; }
        /// <summary>Gets the target before gain limiting and byte saturation; actual contrast may differ. / 获取增益限制和字节饱和前的目标，实际对比度可能不同。</summary>
        public double TargetStandardDeviation { get; }
        /// <summary>Gets maximum linear gain, not applicable to CLAHE. / 获取最大线性增益，不适用于CLAHE。</summary>
        public double MaximumGain { get; }
        /// <summary>Gets CLAHE's histogram clipping parameter. / 获取CLAHE直方图截断参数。</summary>
        public double ClaheClipLimit { get; }
        /// <summary>Gets tile counts per axis, not pixels per tile. / 获取每轴分块数量，而非每块像素数。</summary>
        public int ClaheGridSize { get; }
        /// <summary>Gets the hard bound on each enhanced rectified image; exceeding it fails. / 获取每个增强校正图像的硬上限，超限失败。</summary>
        public int MaximumPixelsPerCrop { get; }
    }

    /// <summary>Shares deterministic quality gates between crop adapters and result validation. / 在裁剪适配器与结果验证间共享确定性质量门限。</summary>
    public static class OcrCropEnhancementPolicy
    {
        /// <summary>Decides from rectified crop statistics, with no image allocation or inference. / 根据校正裁剪统计决定，不分配图像或执行推理。</summary>
        public static OcrCropEnhancementDecision Decide(OcrPixelQualityDiagnostics rectified, OcrCropEnhancementOptions? options)
        {
            if (rectified == null) throw new ArgumentNullException(nameof(rectified));
            if (options == null) return OcrCropEnhancementDecision.NotConfigured;
            double? deviation = rectified.LuminanceStandardDeviation;
            if (!deviation.HasValue || deviation.Value < options.MinimumContrast) return OcrCropEnhancementDecision.InsufficientVariation;
            if (deviation.Value >= options.LowContrastThreshold) return OcrCropEnhancementDecision.SufficientContrast;
            if ((long)rectified.InputSize.Width * rectified.InputSize.Height > options.MaximumPixelsPerCrop)
                throw new OcrPipelineException(VisualErrorCodes.OcrLimitExceeded, "Enhanced crop exceeds its pixel limit.", OcrPipelineStage.CropAndBatch);
            return OcrCropEnhancementDecision.Applied;
        }
    }
}
