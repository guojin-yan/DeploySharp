using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Identifies the geometric image operation. / 标识图像几何操作。</summary>
    public enum OpenCvResizeMode
    {
        /// <summary>Resize width and height independently. / 独立缩放宽度和高度。</summary>
        Resize = 0,
        /// <summary>Preserve aspect ratio and pad around the image. / 保持宽高比并在图像周围填充。</summary>
        Letterbox = 1,
        /// <summary>Crop from the center to the target aspect ratio and resize. / 从中心裁剪到目标宽高比后缩放。</summary>
        CenterCrop = 2,
        /// <summary>Resize the longest side and pad only the bottom and right edges. / 缩放最长边并仅在底边与右边补齐。</summary>
        LongestSidePadBottomRight = 3,
        /// <summary>Resize the shortest edge, then crop the requested canvas from the center. / 缩放最短边，再从中心裁剪所需画布。</summary>
        ShortestEdgeCenterCrop = 4
    }

    /// <summary>Identifies the audited OpenCV resize interpolation. / 标识已审计的 OpenCV 缩放插值。</summary>
    public enum OpenCvInterpolation
    {
        /// <summary>Use bilinear interpolation. / 使用双线性插值。</summary>
        Linear = 0,
        /// <summary>Use bicubic interpolation. / 使用双三次插值。</summary>
        Cubic = 1,
        /// <summary>Use nearest-neighbor interpolation. / 使用最近邻插值。</summary>
        Nearest = 2,
        /// <summary>Use Pillow-compatible antialiased bicubic resize for an exact fixed-resize model contract. / 为精确固定缩放模型合同使用兼容 Pillow 的抗锯齿双三次缩放。</summary>
        PillowBicubic = 3
    }

    /// <summary>Identifies how letterbox content dimensions are converted to integral pixels. / 标识如何将 letterbox 内容尺寸转换为整数像素。</summary>
    public enum OpenCvLetterboxRounding
    {
        /// <summary>Round each scaled dimension to the nearest integer. / 将每个缩放尺寸舍入到最近整数。</summary>
        Nearest = 0,
        /// <summary>Take the floor of each scaled dimension. / 对每个缩放尺寸向下取整。</summary>
        Floor = 1,
        /// <summary>Round by taking floor(value + 0.5), matching Segment Anything's longest-side transform. / 使用 floor(value + 0.5) 舍入，与 Segment Anything 最长边变换一致。</summary>
        HalfUp = 2
    }

    /// <summary>Identifies how a decoded alpha channel is handled. / 标识解码后的 alpha 通道处理方式。</summary>
    public enum OpenCvAlphaMode
    {
        /// <summary>Discard alpha when the requested output has no alpha channel. / 请求的输出不含 alpha 时丢弃 alpha。</summary>
        Drop = 0,
        /// <summary>Composite alpha against the configured background. / 将 alpha 与配置的背景色合成。</summary>
        Composite = 1,
        /// <summary>Preserve alpha; the output color order must contain alpha. / 保留 alpha；输出颜色顺序必须包含 alpha。</summary>
        Preserve = 2
    }

    /// <summary>Identifies the managed tensor element type produced by preprocessing. / 标识预处理生成的 managed 张量元素类型。</summary>
    public enum OpenCvOutputType
    {
        /// <summary>Produce normalized single-precision values. / 生成归一化的单精度值。</summary>
        Float32 = 0,
        /// <summary>Produce unnormalized unsigned bytes. / 生成未归一化的无符号字节。</summary>
        UInt8 = 1
    }

    /// <summary>Stores an RGB color without exposing an OpenCV scalar. / 存储 RGB 颜色且不暴露 OpenCV Scalar。</summary>
    public readonly struct OpenCvRgbColor : IEquatable<OpenCvRgbColor>
    {
        /// <summary>Initializes an RGB color. / 初始化 RGB 颜色。</summary>
        public OpenCvRgbColor(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        /// <summary>Gets the red channel. / 获取红色通道。</summary>
        public byte Red { get; }
        /// <summary>Gets the green channel. / 获取绿色通道。</summary>
        public byte Green { get; }
        /// <summary>Gets the blue channel. / 获取蓝色通道。</summary>
        public byte Blue { get; }
        /// <summary>Gets black. / 获取黑色。</summary>
        public static OpenCvRgbColor Black { get; } = new OpenCvRgbColor(0, 0, 0);
        /// <summary>Compares this color with another RGB color. / 将此颜色与另一个 RGB 颜色比较。</summary>
        /// <param name="other">The color to compare. / 要比较的颜色。</param>
        public bool Equals(OpenCvRgbColor other) => Red == other.Red && Green == other.Green && Blue == other.Blue;
        /// <summary>Compares this color with an object. / 将此颜色与对象比较。</summary>
        /// <param name="obj">The object to compare. / 要比较的对象。</param>
        public override bool Equals(object? obj) => obj is OpenCvRgbColor other && Equals(other);
        /// <summary>Gets a stable hash code for this color. / 获取此颜色的稳定哈希码。</summary>
        public override int GetHashCode() => (Red << 16) | (Green << 8) | Blue;
        /// <summary>Compares two colors. / 比较两个颜色。</summary>
        public static bool operator ==(OpenCvRgbColor left, OpenCvRgbColor right) => left.Equals(right);
        /// <summary>Compares two colors for inequality. / 比较两个颜色是否不相等。</summary>
        public static bool operator !=(OpenCvRgbColor left, OpenCvRgbColor right) => !left.Equals(right);
    }

    /// <summary>Describes immutable OpenCV image preprocessing. / 描述不可变的 OpenCV 图像预处理。</summary>
    public sealed class OpenCvPreprocessOptions
    {
        private readonly IReadOnlyList<float> _means;
        private readonly IReadOnlyList<float> _standardDeviations;
        private readonly IReadOnlyList<float> _inputDivisors;

        /// <summary>Initializes and validates preprocessing options. / 初始化并校验预处理配置。</summary>
        public OpenCvPreprocessOptions(
            VisualSize modelSize,
            OpenCvResizeMode resizeMode = OpenCvResizeMode.Resize,
            VisualColorOrder colorOrder = VisualColorOrder.Rgb,
            OpenCvAlphaMode alphaMode = OpenCvAlphaMode.Drop,
            IEnumerable<float>? means = null,
            IEnumerable<float>? standardDeviations = null,
            VisualTensorLayout layout = VisualTensorLayout.Nchw,
            int batchSize = 1,
            OpenCvOutputType outputType = OpenCvOutputType.Float32,
            OpenCvRgbColor? paddingColor = null,
            OpenCvRgbColor? alphaBackground = null,
            OpenCvLetterboxRounding letterboxRounding = OpenCvLetterboxRounding.Nearest,
            OpenCvInterpolation interpolation = OpenCvInterpolation.Linear,
            IEnumerable<float>? inputDivisors = null)
        {
            if (!Enum.IsDefined(typeof(OpenCvResizeMode), resizeMode)) throw Invalid("The resize mode is invalid.");
            if (!Enum.IsDefined(typeof(VisualColorOrder), colorOrder) || colorOrder == VisualColorOrder.Unspecified) throw Invalid("A concrete output color order is required.");
            if (!Enum.IsDefined(typeof(OpenCvAlphaMode), alphaMode)) throw Invalid("The alpha mode is invalid.");
            if (!Enum.IsDefined(typeof(VisualTensorLayout), layout)) throw Invalid("The tensor layout is invalid.");
            if (!Enum.IsDefined(typeof(OpenCvOutputType), outputType)) throw Invalid("The output type is invalid.");
            if (!Enum.IsDefined(typeof(OpenCvLetterboxRounding), letterboxRounding)) throw Invalid("The letterbox rounding mode is invalid.");
            if (!Enum.IsDefined(typeof(OpenCvInterpolation), interpolation)) throw Invalid("The interpolation mode is invalid.");
            if (interpolation == OpenCvInterpolation.PillowBicubic && resizeMode != OpenCvResizeMode.Resize) throw Invalid("Pillow-compatible bicubic interpolation currently supports fixed resize only.");
            if (batchSize <= 0) throw Invalid("The batch size must be positive.");
            int channels = GetChannelCount(colorOrder);
            bool outputHasAlpha = colorOrder == VisualColorOrder.Rgba || colorOrder == VisualColorOrder.Bgra;
            if (alphaMode == OpenCvAlphaMode.Preserve && !outputHasAlpha) throw Invalid("Preserving alpha requires RGBA or BGRA output.");
            if (alphaMode == OpenCvAlphaMode.Composite && outputHasAlpha) throw Invalid("Alpha compositing cannot produce an alpha-bearing output.");

            _means = CopyFinite(means, channels, "means", false);
            _standardDeviations = CopyFinite(standardDeviations, channels, "standardDeviations", true);
            _inputDivisors = CopyFinite(inputDivisors, channels, "inputDivisors", true);
            if (outputType == OpenCvOutputType.UInt8 && (_means.Count != 0 || _standardDeviations.Count != 0)) throw Invalid("UInt8 output cannot apply floating-point normalization.");

            ModelSize = modelSize;
            ResizeMode = resizeMode;
            ColorOrder = colorOrder;
            AlphaMode = alphaMode;
            Layout = layout;
            BatchSize = batchSize;
            OutputType = outputType;
            PaddingColor = paddingColor ?? OpenCvRgbColor.Black;
            AlphaBackground = alphaBackground ?? OpenCvRgbColor.Black;
            LetterboxRounding = letterboxRounding;
            Interpolation = interpolation;
        }

        /// <summary>Gets the model input size. / 获取模型输入尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets the geometric operation. / 获取几何操作。</summary>
        public OpenCvResizeMode ResizeMode { get; }
        /// <summary>Gets the requested output channel order. / 获取请求的输出通道顺序。</summary>
        public VisualColorOrder ColorOrder { get; }
        /// <summary>Gets alpha handling. / 获取 alpha 处理方式。</summary>
        public OpenCvAlphaMode AlphaMode { get; }
        /// <summary>Gets per-channel subtracted means. / 获取逐通道减去的均值。</summary>
        public IReadOnlyList<float> Means => _means;
        /// <summary>Gets positive per-channel divisors. / 获取逐通道正除数。</summary>
        public IReadOnlyList<float> StandardDeviations => _standardDeviations;
        /// <summary>Gets per-channel divisors applied to decoded byte values before mean subtraction. / 获取在减均值前应用于解码字节值的逐通道除数。</summary>
        public IReadOnlyList<float> InputDivisors => _inputDivisors;
        /// <summary>Gets the output tensor layout. / 获取输出张量布局。</summary>
        public VisualTensorLayout Layout { get; }
        /// <summary>Gets the batch size; one prepared image is duplicated for each batch slot. / 获取批次大小；同一已准备图像复制到每个批次位置。</summary>
        public int BatchSize { get; }
        /// <summary>Gets the output element type. / 获取输出元素类型。</summary>
        public OpenCvOutputType OutputType { get; }
        /// <summary>Gets the letterbox padding color. / 获取 letterbox 填充颜色。</summary>
        public OpenCvRgbColor PaddingColor { get; }
        /// <summary>Gets the alpha compositing background. / 获取 alpha 合成背景色。</summary>
        public OpenCvRgbColor AlphaBackground { get; }
        /// <summary>Gets how letterbox content dimensions are rounded. / 获取 letterbox 内容尺寸的舍入方式。</summary>
        public OpenCvLetterboxRounding LetterboxRounding { get; }
        /// <summary>Gets the resize interpolation. / 获取缩放插值。</summary>
        public OpenCvInterpolation Interpolation { get; }

        internal int ChannelCount => GetChannelCount(ColorOrder);
        internal float Mean(int channel) => _means.Count == 0 ? 0f : _means[_means.Count == 1 ? 0 : channel];
        internal float StandardDeviation(int channel) => _standardDeviations.Count == 0 ? 1f : _standardDeviations[_standardDeviations.Count == 1 ? 0 : channel];
        internal float InputDivisor(int channel) => _inputDivisors.Count == 0 ? 1f : _inputDivisors[_inputDivisors.Count == 1 ? 0 : channel];

        private static IReadOnlyList<float> CopyFinite(IEnumerable<float>? values, int channels, string name, bool requirePositive)
        {
            var result = new List<float>();
            if (values != null)
            {
                foreach (float value in values)
                {
                    if (float.IsNaN(value) || float.IsInfinity(value) || (requirePositive && value <= 0)) throw Invalid("Normalization values must be finite and standard deviations must be positive.", name);
                    result.Add(value);
                }
            }
            if (result.Count != 0 && result.Count != 1 && result.Count != channels) throw Invalid("Normalization values must be empty, scalar, or match the output channel count.", name + "Count=" + result.Count + ";channels=" + channels);
            return result.AsReadOnly();
        }

        private static int GetChannelCount(VisualColorOrder colorOrder)
        {
            if (colorOrder == VisualColorOrder.Gray) return 1;
            if (colorOrder == VisualColorOrder.Rgb || colorOrder == VisualColorOrder.Bgr) return 3;
            if (colorOrder == VisualColorOrder.Rgba || colorOrder == VisualColorOrder.Bgra) return 4;
            throw Invalid("The output color order is unsupported.");
        }

        private static OpenCvVisualException Invalid(string message, string? details = null) => new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, message, technicalDetails: details);
    }
}
