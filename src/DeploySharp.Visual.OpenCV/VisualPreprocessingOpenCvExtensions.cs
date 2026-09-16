using System;
using JYPPX.DeploySharp.Visual;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Maps the shared visual preprocessing contract to OpenCV without changing its semantics. / 将共享视觉预处理合同映射到 OpenCV，且不改变语义。</summary>
    public static class VisualPreprocessingOpenCvExtensions
    {
        /// <summary>Creates OpenCV options from a shared contract. The target size must be resolved for the selected image. / 从共享合同创建 OpenCV 选项；所选图像必须已解析目标尺寸。</summary>
        public static OpenCvPreprocessOptions ToOpenCvOptions(this VisualPreprocessingOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (!options.ModelSize.HasValue) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "A concrete model size is required by the OpenCV image factory.");
            return new OpenCvPreprocessOptions(
                options.ModelSize.Value,
                ToResizeMode(options.ResizeMode),
                options.ColorOrder,
                ToAlphaMode(options.AlphaMode),
                options.Normalization.Means,
                options.Normalization.StandardDeviations,
                options.Layout,
                options.BatchSize,
                ToOutputType(options.OutputType),
                ToColor(options.PaddingColor),
                ToColor(options.AlphaBackground),
                ToRounding(options.DimensionRounding),
                ToInterpolation(options.Interpolation),
                options.Normalization.InputDivisors,
                options.ScaleUp);
        }

        private static OpenCvResizeMode ToResizeMode(VisualResizeMode mode) => mode switch
        {
            VisualResizeMode.Resize => OpenCvResizeMode.Resize,
            VisualResizeMode.Letterbox => OpenCvResizeMode.Letterbox,
            VisualResizeMode.CenterCrop => OpenCvResizeMode.CenterCrop,
            VisualResizeMode.LongestSidePadBottomRight => OpenCvResizeMode.LongestSidePadBottomRight,
            VisualResizeMode.ShortestEdgeCenterCrop => OpenCvResizeMode.ShortestEdgeCenterCrop,
            _ => throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The shared resize mode is unsupported by OpenCV.")
        };

        private static OpenCvAlphaMode ToAlphaMode(VisualAlphaMode mode) => (OpenCvAlphaMode)(int)mode;
        private static OpenCvOutputType ToOutputType(VisualPreprocessingOutputType type) => (OpenCvOutputType)(int)type;
        private static OpenCvLetterboxRounding ToRounding(VisualDimensionRounding mode) => (OpenCvLetterboxRounding)(int)mode;
        private static OpenCvInterpolation ToInterpolation(VisualInterpolationMode mode) => (OpenCvInterpolation)(int)mode;
        private static OpenCvRgbColor ToColor(VisualRgbColor color) => new OpenCvRgbColor(color.Red, color.Green, color.Blue);
    }
}
