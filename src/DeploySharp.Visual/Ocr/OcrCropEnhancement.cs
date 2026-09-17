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
        /// <summary>Applies a bounded Gaussian denoise only when the sampled Laplacian variance exceeds the noise gate. / 仅在采样拉普拉斯方差超过噪声门限时执行有界高斯去噪。</summary>
        GaussianDenoise,
        /// <summary>Applies Gaussian adaptive binarization to a low-contrast crop with a bounded neighborhood. / 对低对比度裁剪使用有界邻域执行高斯自适应二值化。</summary>
        AdaptiveThreshold,
        /// <summary>Applies a bounded unsharp mask only when sampled sharpness is below the configured gate. / 仅在采样清晰度低于配置门限时执行有界反遮罩锐化。</summary>
        UnsharpMask,
    }

    /// <summary>Explains whether a requested crop enhancement was applied. / 说明请求的裁剪增强是否应用。</summary>
    public enum OcrCropEnhancementDecision
    {
        /// <summary>No enhancement was requested. / 未请求增强。</summary>
        NotConfigured,
        /// <summary>Contrast already meets the configured threshold. / 对比度已达到配置阈值。</summary>
        SufficientContrast,
        /// <summary>The requested operation's quality or size gate did not require a change. / 所请求操作的质量或尺寸门限未要求修改。</summary>
        SufficientQuality,
        /// <summary>Too little variation exists to enhance safely. / 变化太少，不执行增强。</summary>
        InsufficientVariation,
        /// <summary>The selected low-contrast operation was applied. / 已应用所选低对比度操作。</summary>
        Applied,
    }

    /// <summary>Bounds one opt-in enhancement; thresholds require application-specific validation. / 限制一种显式增强；阈值需要具体应用验证。</summary>
    public sealed class OcrCropEnhancementOptions
    {
        /// <summary>Initializes contrast gates, gain, CLAHE, denoise, adaptive threshold and per-crop bounds. / 初始化对比度门限、增益、CLAHE、去噪、自适应阈值及逐裁剪限制。</summary>
        public OcrCropEnhancementOptions(OcrCropEnhancementMode mode, double lowContrastThreshold = 32, double minimumContrast = 1,
            double targetStandardDeviation = 64, double maximumGain = 3, double claheClipLimit = 2, int claheGridSize = 8, int maximumPixelsPerCrop = 1048576,
            double noiseThreshold = 512, int denoiseKernelSize = 3, double denoiseSigma = 0, int adaptiveBlockSize = 15, double adaptiveConstant = 5,
            double sharpnessThreshold = 512, int sharpenKernelSize = 3, double sharpenAmount = .5, double sharpenSigma = 1)
        {
            if (mode != OcrCropEnhancementMode.ContrastNormalize && mode != OcrCropEnhancementMode.GrayClahe && mode != OcrCropEnhancementMode.GaussianDenoise && mode != OcrCropEnhancementMode.AdaptiveThreshold && mode != OcrCropEnhancementMode.UnsharpMask) throw new ArgumentOutOfRangeException(nameof(mode));
            if (!(lowContrastThreshold > 0 && lowContrastThreshold <= 128)) throw new ArgumentOutOfRangeException(nameof(lowContrastThreshold));
            if (!(minimumContrast > 0 && minimumContrast < lowContrastThreshold)) throw new ArgumentOutOfRangeException(nameof(minimumContrast));
            if (!(targetStandardDeviation >= lowContrastThreshold && targetStandardDeviation <= 128)) throw new ArgumentOutOfRangeException(nameof(targetStandardDeviation));
            if (!(maximumGain > 1 && maximumGain <= 8)) throw new ArgumentOutOfRangeException(nameof(maximumGain));
            if (!(claheClipLimit > 0 && claheClipLimit <= 16)) throw new ArgumentOutOfRangeException(nameof(claheClipLimit));
            if (claheGridSize < 2 || claheGridSize > 16) throw new ArgumentOutOfRangeException(nameof(claheGridSize));
            if (maximumPixelsPerCrop < 1 || maximumPixelsPerCrop > 16777216) throw new ArgumentOutOfRangeException(nameof(maximumPixelsPerCrop));
            if (double.IsNaN(noiseThreshold) || double.IsInfinity(noiseThreshold) || noiseThreshold <= 0 || noiseThreshold > 1048576) throw new ArgumentOutOfRangeException(nameof(noiseThreshold));
            if (denoiseKernelSize < 3 || denoiseKernelSize > 9 || (denoiseKernelSize & 1) == 0) throw new ArgumentOutOfRangeException(nameof(denoiseKernelSize));
            if (double.IsNaN(denoiseSigma) || double.IsInfinity(denoiseSigma) || denoiseSigma < 0 || denoiseSigma > 32) throw new ArgumentOutOfRangeException(nameof(denoiseSigma));
            if (adaptiveBlockSize < 3 || adaptiveBlockSize > 31 || (adaptiveBlockSize & 1) == 0) throw new ArgumentOutOfRangeException(nameof(adaptiveBlockSize));
            if (double.IsNaN(adaptiveConstant) || double.IsInfinity(adaptiveConstant) || adaptiveConstant < -64 || adaptiveConstant > 64) throw new ArgumentOutOfRangeException(nameof(adaptiveConstant));
            if (double.IsNaN(sharpnessThreshold) || double.IsInfinity(sharpnessThreshold) || sharpnessThreshold <= 0 || sharpnessThreshold > 1048576) throw new ArgumentOutOfRangeException(nameof(sharpnessThreshold));
            if (sharpenKernelSize < 3 || sharpenKernelSize > 9 || (sharpenKernelSize & 1) == 0) throw new ArgumentOutOfRangeException(nameof(sharpenKernelSize));
            if (double.IsNaN(sharpenAmount) || double.IsInfinity(sharpenAmount) || sharpenAmount <= 0 || sharpenAmount > 4) throw new ArgumentOutOfRangeException(nameof(sharpenAmount));
            if (double.IsNaN(sharpenSigma) || double.IsInfinity(sharpenSigma) || sharpenSigma <= 0 || sharpenSigma > 32) throw new ArgumentOutOfRangeException(nameof(sharpenSigma));
            Mode = mode; LowContrastThreshold = lowContrastThreshold; MinimumContrast = minimumContrast;
            TargetStandardDeviation = targetStandardDeviation; MaximumGain = maximumGain; ClaheClipLimit = claheClipLimit;
            ClaheGridSize = claheGridSize; MaximumPixelsPerCrop = maximumPixelsPerCrop;
            NoiseThreshold = noiseThreshold; DenoiseKernelSize = denoiseKernelSize; DenoiseSigma = denoiseSigma;
            AdaptiveBlockSize = adaptiveBlockSize; AdaptiveConstant = adaptiveConstant;
            SharpnessThreshold = sharpnessThreshold; SharpenKernelSize = sharpenKernelSize; SharpenAmount = sharpenAmount; SharpenSigma = sharpenSigma;
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
        /// <summary>Gets the Laplacian-variance gate used by Gaussian denoise; it is not a calibrated noise score. / 获取高斯去噪使用的拉普拉斯方差门限；它不是标定噪声分数。</summary>
        public double NoiseThreshold { get; }
        /// <summary>Gets the odd Gaussian kernel width used by Gaussian denoise. / 获取高斯去噪使用的奇数高斯核宽度。</summary>
        public int DenoiseKernelSize { get; }
        /// <summary>Gets the Gaussian sigma; zero delegates scale selection to OpenCV. / 获取高斯 sigma；零表示交由 OpenCV 根据核尺寸选择。</summary>
        public double DenoiseSigma { get; }
        /// <summary>Gets the odd adaptive-threshold neighborhood size. / 获取自适应阈值的奇数邻域尺寸。</summary>
        public int AdaptiveBlockSize { get; }
        /// <summary>Gets the constant subtracted from the local Gaussian mean. / 获取从局部高斯均值中减去的常数。</summary>
        public double AdaptiveConstant { get; }
        /// <summary>Gets the Laplacian-variance upper gate for unsharp masking; it is not a calibrated blur score. / 获取反遮罩锐化使用的拉普拉斯方差上限；它不是标定的模糊分数。</summary>
        public double SharpnessThreshold { get; }
        /// <summary>Gets the odd Gaussian kernel width used by unsharp masking. / 获取反遮罩锐化使用的奇数高斯核宽度。</summary>
        public int SharpenKernelSize { get; }
        /// <summary>Gets the positive detail amount used by unsharp masking. / 获取反遮罩锐化使用的正细节增益。</summary>
        public double SharpenAmount { get; }
        /// <summary>Gets the positive Gaussian sigma used by unsharp masking. / 获取反遮罩锐化使用的正高斯 sigma。</summary>
        public double SharpenSigma { get; }
    }

    /// <summary>Shares deterministic quality gates between crop adapters and result validation. / 在裁剪适配器与结果验证间共享确定性质量门限。</summary>
    public static class OcrCropEnhancementPolicy
    {
        /// <summary>Decides from rectified crop statistics, with no image allocation or inference. / 根据校正裁剪统计决定，不分配图像或执行推理。</summary>
        public static OcrCropEnhancementDecision Decide(OcrPixelQualityDiagnostics rectified, OcrCropEnhancementOptions? options)
        {
            if (rectified == null) throw new ArgumentNullException(nameof(rectified));
            if (options == null) return OcrCropEnhancementDecision.NotConfigured;
            if (options.Mode == OcrCropEnhancementMode.GaussianDenoise)
            {
                double? laplacian = rectified.LaplacianVariance;
                return laplacian.HasValue && laplacian.Value > options.NoiseThreshold
                    ? OcrCropEnhancementDecision.Applied
                    : OcrCropEnhancementDecision.SufficientQuality;
            }
            if (options.Mode == OcrCropEnhancementMode.AdaptiveThreshold &&
                (rectified.InputSize.Width < options.AdaptiveBlockSize || rectified.InputSize.Height < options.AdaptiveBlockSize))
                return OcrCropEnhancementDecision.SufficientQuality;
            if (options.Mode == OcrCropEnhancementMode.UnsharpMask)
            {
                double? laplacian = rectified.LaplacianVariance;
                if (!rectified.LuminanceStandardDeviation.HasValue || rectified.LuminanceStandardDeviation.Value < options.MinimumContrast ||
                    !laplacian.HasValue || laplacian.Value >= options.SharpnessThreshold)
                    return OcrCropEnhancementDecision.SufficientQuality;
                if ((long)rectified.InputSize.Width * rectified.InputSize.Height > options.MaximumPixelsPerCrop)
                    throw new OcrPipelineException(VisualErrorCodes.OcrLimitExceeded, "Enhanced crop exceeds its pixel limit.", OcrPipelineStage.CropAndBatch);
                return OcrCropEnhancementDecision.Applied;
            }
            double? deviation = rectified.LuminanceStandardDeviation;
            if (!deviation.HasValue || deviation.Value < options.MinimumContrast) return OcrCropEnhancementDecision.InsufficientVariation;
            if (deviation.Value >= options.LowContrastThreshold) return OcrCropEnhancementDecision.SufficientContrast;
            if ((long)rectified.InputSize.Width * rectified.InputSize.Height > options.MaximumPixelsPerCrop)
                throw new OcrPipelineException(VisualErrorCodes.OcrLimitExceeded, "Enhanced crop exceeds its pixel limit.", OcrPipelineStage.CropAndBatch);
            return OcrCropEnhancementDecision.Applied;
        }
    }
}
