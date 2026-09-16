using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies a backend-neutral image geometry operation. / 标识后端无关的图像几何操作。</summary>
    public enum VisualResizeMode
    {
        /// <summary>Resize width and height independently. / 独立缩放宽度和高度。</summary>
        Resize = 0,
        /// <summary>Preserve aspect ratio and add centered padding. / 保持宽高比并居中填充。</summary>
        Letterbox = 1,
        /// <summary>Crop to the target aspect ratio from the center and resize. / 从中心裁剪到目标宽高比后缩放。</summary>
        CenterCrop = 2,
        /// <summary>Resize the longest side and pad only the bottom and right edges. / 缩放最长边并仅在底部和右侧填充。</summary>
        LongestSidePadBottomRight = 3,
        /// <summary>Resize the shortest edge and take a centered target crop. / 缩放最短边并截取居中的目标区域。</summary>
        ShortestEdgeCenterCrop = 4
    }

    /// <summary>Identifies a backend-neutral resize interpolation. / 标识后端无关的缩放插值。</summary>
    public enum VisualInterpolationMode
    {
        /// <summary>Use bilinear interpolation. / 使用双线性插值。</summary>
        Linear = 0,
        /// <summary>Use bicubic interpolation. / 使用双三次插值。</summary>
        Cubic = 1,
        /// <summary>Use nearest-neighbor interpolation. / 使用最近邻插值。</summary>
        Nearest = 2,
        /// <summary>Use Pillow-compatible antialiased bicubic interpolation when the adapter supports it. / 在适配器支持时使用兼容 Pillow 的抗锯齿双三次插值。</summary>
        PillowBicubic = 3
    }

    /// <summary>Identifies how integral letterbox dimensions are rounded. / 标识 Letterbox 整数尺寸的舍入方式。</summary>
    public enum VisualDimensionRounding
    {
        /// <summary>Round to the nearest integer. / 舍入到最近整数。</summary>
        Nearest = 0,
        /// <summary>Round down. / 向下取整。</summary>
        Floor = 1,
        /// <summary>Use floor(value + 0.5). / 使用 floor(value + 0.5)。</summary>
        HalfUp = 2
    }

    /// <summary>Identifies alpha-channel handling before tensor conversion. / 标识张量转换前的 Alpha 通道处理方式。</summary>
    public enum VisualAlphaMode
    {
        /// <summary>Discard alpha when the output has no alpha channel. / 输出不含 Alpha 时丢弃 Alpha。</summary>
        Drop = 0,
        /// <summary>Composite alpha against a configured background. / 将 Alpha 与配置的背景色合成。</summary>
        Composite = 1,
        /// <summary>Preserve alpha in RGBA or BGRA output. / 在 RGBA 或 BGRA 输出中保留 Alpha。</summary>
        Preserve = 2
    }

    /// <summary>Identifies the tensor element type produced by image preprocessing. / 标识图像预处理生成的张量元素类型。</summary>
    public enum VisualPreprocessingOutputType
    {
        /// <summary>Produce single-precision values. / 生成单精度值。</summary>
        Float32 = 0,
        /// <summary>Produce unnormalized unsigned bytes. / 生成未归一化的无符号字节。</summary>
        UInt8 = 1
    }

    /// <summary>Identifies a named normalization form. / 标识具名归一化形式。</summary>
    public enum VisualNormalizationMode
    {
        /// <summary>Keep decoded byte magnitudes unchanged. / 保持解码后的字节数值不变。</summary>
        None = 0,
        /// <summary>Divide each channel by a positive divisor. / 将各通道除以正除数。</summary>
        Scale = 1,
        /// <summary>Apply (pixel / divisor - mean) / standardDeviation. / 应用 (pixel / divisor - mean) / standardDeviation。</summary>
        MeanStandardDeviation = 2
    }

    /// <summary>Stores an image color without depending on a concrete image library. / 存储不依赖具体图像库的颜色。</summary>
    public readonly struct VisualRgbColor : IEquatable<VisualRgbColor>
    {
        /// <summary>Initializes an RGB color. / 初始化 RGB 颜色。</summary>
        public VisualRgbColor(byte red, byte green, byte blue) { Red = red; Green = green; Blue = blue; }
        /// <summary>Gets red. / 获取红色分量。</summary>
        public byte Red { get; }
        /// <summary>Gets green. / 获取绿色分量。</summary>
        public byte Green { get; }
        /// <summary>Gets blue. / 获取蓝色分量。</summary>
        public byte Blue { get; }
        /// <summary>Gets black. / 获取黑色。</summary>
        public static VisualRgbColor Black { get; } = new VisualRgbColor(0, 0, 0);
        /// <inheritdoc />
        public bool Equals(VisualRgbColor other) => Red == other.Red && Green == other.Green && Blue == other.Blue;
        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is VisualRgbColor other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => (Red << 16) | (Green << 8) | Blue;
        /// <summary>Compares two colors. / 比较两个颜色。</summary>
        public static bool operator ==(VisualRgbColor left, VisualRgbColor right) => left.Equals(right);
        /// <summary>Compares two colors for inequality. / 比较两个颜色是否不相等。</summary>
        public static bool operator !=(VisualRgbColor left, VisualRgbColor right) => !left.Equals(right);
    }

    /// <summary>Defines immutable channel normalization independently from an image adapter. / 独立于图像适配器定义不可变的通道归一化。</summary>
    public sealed class VisualNormalizationOptions
    {
        private readonly IReadOnlyList<float> _means;
        private readonly IReadOnlyList<float> _standardDeviations;
        private readonly IReadOnlyList<float> _inputDivisors;

        private VisualNormalizationOptions(VisualNormalizationMode mode, IEnumerable<float>? means, IEnumerable<float>? standardDeviations, IEnumerable<float>? inputDivisors)
        {
            if (!Enum.IsDefined(typeof(VisualNormalizationMode), mode)) throw Invalid("The normalization mode is invalid.");
            Mode = mode;
            _means = Copy(means, false, nameof(means));
            _standardDeviations = Copy(standardDeviations, true, nameof(standardDeviations));
            _inputDivisors = Copy(inputDivisors, true, nameof(inputDivisors));
            if (mode == VisualNormalizationMode.None && (_means.Count != 0 || _standardDeviations.Count != 0 || _inputDivisors.Count != 0)) throw Invalid("None normalization cannot contain channel values.");
            if (mode == VisualNormalizationMode.Scale && (_means.Count != 0 || _standardDeviations.Count != 0 || _inputDivisors.Count == 0)) throw Invalid("Scale normalization requires divisors only.");
            if (mode == VisualNormalizationMode.MeanStandardDeviation && _standardDeviations.Count == 0) throw Invalid("Mean/std normalization requires standard deviations; omitted means are zero.");
        }

        /// <summary>Gets the normalization form. / 获取归一化形式。</summary>
        public VisualNormalizationMode Mode { get; }
        /// <summary>Gets values subtracted after input division. / 获取输入除法后减去的值。</summary>
        public IReadOnlyList<float> Means => _means;
        /// <summary>Gets positive standard deviations. / 获取正标准差。</summary>
        public IReadOnlyList<float> StandardDeviations => _standardDeviations;
        /// <summary>Gets positive input divisors. / 获取正输入除数。</summary>
        public IReadOnlyList<float> InputDivisors => _inputDivisors;
        /// <summary>Gets no normalization. / 获取无归一化配置。</summary>
        public static VisualNormalizationOptions None { get; } = new VisualNormalizationOptions(VisualNormalizationMode.None, null, null, null);

        /// <summary>Creates pixel/divisor scaling. / 创建 pixel/divisor 缩放。</summary>
        public static VisualNormalizationOptions Scale(float divisor = 255f) => new VisualNormalizationOptions(VisualNormalizationMode.Scale, null, null, new[] { divisor });

        /// <summary>Creates channel normalization using (pixel / divisor - mean) / standardDeviation. Scalar or per-channel values are accepted. / 使用 (pixel / divisor - mean) / standardDeviation 创建通道归一化；接受标量或逐通道值。</summary>
        public static VisualNormalizationOptions MeanStandardDeviation(IEnumerable<float> means, IEnumerable<float> standardDeviations, IEnumerable<float>? inputDivisors = null)
            => new VisualNormalizationOptions(VisualNormalizationMode.MeanStandardDeviation, means, standardDeviations, inputDivisors);

        /// <summary>Creates (pixel - optionalMean) / standardDeviation without an input-division stage. / 创建不含输入除法阶段的 (pixel - optionalMean) / standardDeviation。</summary>
        public static VisualNormalizationOptions DivideByStandardDeviation(IEnumerable<float> standardDeviations, IEnumerable<float>? means = null)
            => new VisualNormalizationOptions(VisualNormalizationMode.MeanStandardDeviation, means, standardDeviations, null);

        /// <summary>Creates ImageNet RGB normalization. / 创建 ImageNet RGB 归一化。</summary>
        public static VisualNormalizationOptions ImageNet { get; } = MeanStandardDeviation(new[] { .485f, .456f, .406f }, new[] { .229f, .224f, .225f }, new[] { 255f });

        internal void ValidateChannelCount(int channels)
        {
            ValidateCount(_means, channels, "means");
            ValidateCount(_standardDeviations, channels, "standardDeviations");
            ValidateCount(_inputDivisors, channels, "inputDivisors");
        }

        private static IReadOnlyList<float> Copy(IEnumerable<float>? source, bool positive, string name)
        {
            var values = new List<float>();
            if (source != null)
            {
                foreach (float value in source)
                {
                    if (float.IsNaN(value) || float.IsInfinity(value) || (positive && value <= 0f)) throw Invalid("Normalization values must be finite and divisors/deviations must be positive.", name);
                    values.Add(value);
                }
            }
            return new ReadOnlyCollection<float>(values);
        }

        private static void ValidateCount(IReadOnlyList<float> values, int channels, string name)
        {
            if (values.Count != 0 && values.Count != 1 && values.Count != channels) throw Invalid("Normalization values must be scalar or match the color channel count.", name + "Count=" + values.Count + ";channels=" + channels);
        }

        private static VisualException Invalid(string message, string? details = null) => new VisualException(VisualErrorCodes.ProfileInvalid, message, technicalDetails: details);
    }

    /// <summary>Defines one complete backend-neutral image preprocessing contract. / 定义一份完整的后端无关图像预处理合同。</summary>
    public sealed class VisualPreprocessingOptions
    {
        /// <summary>Initializes an immutable preprocessing contract. A null model size represents a source-dependent dynamic size that must be resolved before execution. / 初始化不可变预处理合同；模型尺寸为 null 表示必须在执行前解析的源图相关动态尺寸。</summary>
        public VisualPreprocessingOptions(
            VisualSize? modelSize,
            VisualResizeMode resizeMode = VisualResizeMode.Resize,
            VisualColorOrder colorOrder = VisualColorOrder.Rgb,
            VisualNormalizationOptions? normalization = null,
            VisualTensorLayout layout = VisualTensorLayout.Nchw,
            int batchSize = 1,
            VisualPreprocessingOutputType outputType = VisualPreprocessingOutputType.Float32,
            VisualRgbColor? paddingColor = null,
            VisualAlphaMode alphaMode = VisualAlphaMode.Drop,
            VisualRgbColor? alphaBackground = null,
            VisualDimensionRounding dimensionRounding = VisualDimensionRounding.Nearest,
            VisualInterpolationMode interpolation = VisualInterpolationMode.Linear,
            string? contractId = null,
            bool scaleUp = true)
        {
            if (!Enum.IsDefined(typeof(VisualResizeMode), resizeMode)) throw Invalid("The resize mode is invalid.");
            if (!Enum.IsDefined(typeof(VisualColorOrder), colorOrder) || colorOrder == VisualColorOrder.Unspecified) throw Invalid("A concrete color order is required.");
            if (!Enum.IsDefined(typeof(VisualTensorLayout), layout)) throw Invalid("The tensor layout is invalid.");
            if (!Enum.IsDefined(typeof(VisualPreprocessingOutputType), outputType)) throw Invalid("The output type is invalid.");
            if (!Enum.IsDefined(typeof(VisualAlphaMode), alphaMode)) throw Invalid("The alpha mode is invalid.");
            if (!Enum.IsDefined(typeof(VisualDimensionRounding), dimensionRounding)) throw Invalid("The dimension rounding mode is invalid.");
            if (!Enum.IsDefined(typeof(VisualInterpolationMode), interpolation)) throw Invalid("The interpolation mode is invalid.");
            if (batchSize <= 0) throw Invalid("The batch size must be positive.");
            if (interpolation == VisualInterpolationMode.PillowBicubic && resizeMode != VisualResizeMode.Resize) throw Invalid("Pillow-compatible interpolation currently requires fixed resize geometry.");
            int channels = ChannelCount(colorOrder);
            if (alphaMode == VisualAlphaMode.Preserve && channels != 4) throw Invalid("Preserving alpha requires RGBA or BGRA output.");
            if (alphaMode == VisualAlphaMode.Composite && channels == 4) throw Invalid("Alpha compositing cannot produce an alpha-bearing output.");
            VisualNormalizationOptions effectiveNormalization = normalization ?? VisualNormalizationOptions.None;
            effectiveNormalization.ValidateChannelCount(channels);
            if (outputType == VisualPreprocessingOutputType.UInt8 && effectiveNormalization.Mode != VisualNormalizationMode.None) throw Invalid("UInt8 preprocessing cannot apply floating-point normalization.");
            ModelSize = modelSize;
            ResizeMode = resizeMode;
            ColorOrder = colorOrder;
            Normalization = effectiveNormalization;
            Layout = layout;
            BatchSize = batchSize;
            OutputType = outputType;
            PaddingColor = paddingColor ?? VisualRgbColor.Black;
            AlphaMode = alphaMode;
            AlphaBackground = alphaBackground ?? VisualRgbColor.Black;
            DimensionRounding = dimensionRounding;
            Interpolation = interpolation;
            ContractId = string.IsNullOrWhiteSpace(contractId) ? null : contractId!.Trim();
            ScaleUp = scaleUp;
        }

        /// <summary>Gets the resolved target size, or null for source-dependent dynamic sizing. / 获取已解析目标尺寸；源图相关动态尺寸返回 null。</summary>
        public VisualSize? ModelSize { get; }
        /// <summary>Gets the geometry operation. / 获取几何操作。</summary>
        public VisualResizeMode ResizeMode { get; }
        /// <summary>Gets output color order. / 获取输出颜色顺序。</summary>
        public VisualColorOrder ColorOrder { get; }
        /// <summary>Gets normalization. / 获取归一化配置。</summary>
        public VisualNormalizationOptions Normalization { get; }
        /// <summary>Gets tensor layout. / 获取张量布局。</summary>
        public VisualTensorLayout Layout { get; }
        /// <summary>Gets prepared batch size. / 获取预处理 Batch 大小。</summary>
        public int BatchSize { get; }
        /// <summary>Gets tensor output type. / 获取张量输出类型。</summary>
        public VisualPreprocessingOutputType OutputType { get; }
        /// <summary>Gets padding color. / 获取填充颜色。</summary>
        public VisualRgbColor PaddingColor { get; }
        /// <summary>Gets alpha handling. / 获取 Alpha 处理方式。</summary>
        public VisualAlphaMode AlphaMode { get; }
        /// <summary>Gets alpha compositing background. / 获取 Alpha 合成背景色。</summary>
        public VisualRgbColor AlphaBackground { get; }
        /// <summary>Gets dimension rounding. / 获取尺寸舍入方式。</summary>
        public VisualDimensionRounding DimensionRounding { get; }
        /// <summary>Gets interpolation. / 获取插值方式。</summary>
        public VisualInterpolationMode Interpolation { get; }
        /// <summary>Gets an optional stable preprocessing contract identifier. / 获取可选稳定预处理合同标识。</summary>
        public string? ContractId { get; }
        /// <summary>Gets whether aspect-preserving modes may enlarge a source image. / 获取保持宽高比模式是否允许放大源图。</summary>
        public bool ScaleUp { get; }

        /// <summary>Returns a copy with resolved target size. / 返回带有已解析目标尺寸的副本。</summary>
        public VisualPreprocessingOptions WithModelSize(VisualSize modelSize) => Copy(modelSize: modelSize);
        /// <summary>Returns a copy with another geometry mode. / 返回使用另一几何模式的副本。</summary>
        public VisualPreprocessingOptions WithResizeMode(VisualResizeMode resizeMode) => Copy(resizeMode: resizeMode);
        /// <summary>Returns a copy with another normalization. / 返回使用另一归一化配置的副本。</summary>
        public VisualPreprocessingOptions WithNormalization(VisualNormalizationOptions normalization) => Copy(normalization: normalization ?? throw new ArgumentNullException(nameof(normalization)));
        /// <summary>Returns a copy with another color order. / 返回使用另一颜色顺序的副本。</summary>
        public VisualPreprocessingOptions WithColorOrder(VisualColorOrder colorOrder) => Copy(colorOrder: colorOrder);
        /// <summary>Returns a copy with another interpolation. / 返回使用另一插值方式的副本。</summary>
        public VisualPreprocessingOptions WithInterpolation(VisualInterpolationMode interpolation) => Copy(interpolation: interpolation);
        /// <summary>Returns a copy with another batch size. / 返回使用另一 Batch 大小的副本。</summary>
        public VisualPreprocessingOptions WithBatchSize(int batchSize) => Copy(batchSize: batchSize);
        /// <summary>Returns a copy with another tensor layout. / 返回使用另一张量布局的副本。</summary>
        public VisualPreprocessingOptions WithLayout(VisualTensorLayout layout) => Copy(layout: layout);
        /// <summary>Returns a copy with another output element type. / 返回使用另一输出元素类型的副本。</summary>
        public VisualPreprocessingOptions WithOutputType(VisualPreprocessingOutputType outputType) => Copy(outputType: outputType);
        /// <summary>Returns a copy with another padding color. / 返回使用另一填充颜色的副本。</summary>
        public VisualPreprocessingOptions WithPaddingColor(VisualRgbColor paddingColor) => Copy(paddingColor: paddingColor);
        /// <summary>Returns a copy with another alpha policy and optional compositing background. / 返回使用另一 Alpha 策略和可选合成背景的副本。</summary>
        public VisualPreprocessingOptions WithAlpha(VisualAlphaMode alphaMode, VisualRgbColor? background = null) => Copy(alphaMode: alphaMode, alphaBackground: background ?? AlphaBackground);
        /// <summary>Returns a copy with another dimension rounding policy. / 返回使用另一尺寸舍入策略的副本。</summary>
        public VisualPreprocessingOptions WithDimensionRounding(VisualDimensionRounding rounding) => Copy(dimensionRounding: rounding);
        /// <summary>Returns a copy with a different aspect-preserving upscale policy. / 返回使用另一保持宽高比放大策略的副本。</summary>
        public VisualPreprocessingOptions WithScaleUp(bool scaleUp) => Copy(scaleUp: scaleUp);

        internal TensorElementType TensorElementType => OutputType == VisualPreprocessingOutputType.Float32 ? TensorElementType.Float32 : TensorElementType.UInt8;

        private VisualPreprocessingOptions Copy(
            VisualSize? modelSize = null,
            VisualResizeMode? resizeMode = null,
            VisualColorOrder? colorOrder = null,
            VisualNormalizationOptions? normalization = null,
            VisualInterpolationMode? interpolation = null,
            int? batchSize = null,
            VisualTensorLayout? layout = null,
            VisualPreprocessingOutputType? outputType = null,
            VisualRgbColor? paddingColor = null,
            VisualAlphaMode? alphaMode = null,
            VisualRgbColor? alphaBackground = null,
            VisualDimensionRounding? dimensionRounding = null,
            bool? scaleUp = null)
            => new VisualPreprocessingOptions(
                modelSize ?? ModelSize,
                resizeMode ?? ResizeMode,
                colorOrder ?? ColorOrder,
                normalization ?? Normalization,
                layout ?? Layout,
                batchSize ?? BatchSize,
                outputType ?? OutputType,
                paddingColor ?? PaddingColor,
                alphaMode ?? AlphaMode,
                alphaBackground ?? AlphaBackground,
                dimensionRounding ?? DimensionRounding,
                interpolation ?? Interpolation,
                ContractId,
                scaleUp ?? ScaleUp);

        internal static int ChannelCount(VisualColorOrder colorOrder)
        {
            if (colorOrder == VisualColorOrder.Gray) return 1;
            if (colorOrder == VisualColorOrder.Rgb || colorOrder == VisualColorOrder.Bgr) return 3;
            if (colorOrder == VisualColorOrder.Rgba || colorOrder == VisualColorOrder.Bgra) return 4;
            throw Invalid("The color order is unsupported.");
        }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.ProfileInvalid, message);
    }
}
