using System;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the semantic range of anomaly-map and image-score values. / 标识异常图与图像分数值的语义范围。</summary>
    public enum AnomalyMapValueMode
    {
        /// <summary>Values are unconstrained raw logits. / 值为不受约束的原始 logits。</summary>
        Logits = 0,
        /// <summary>Values are probabilities in the inclusive range [0,1]. / 值为闭区间 [0,1] 内的概率。</summary>
        Probabilities = 1,
        /// <summary>Values are non-negative distances where larger values are more anomalous. / 值为非负距离，值越大表示异常程度越高。</summary>
        Distances = 2,
        /// <summary>Values are exactly zero or one. / 值严格为零或一。</summary>
        Binary = 3
    }

    /// <summary>Identifies supported anomaly-map tensor dimension orders. / 标识支持的异常图张量维度顺序。</summary>
    public enum AnomalyTensorLayout
    {
        /// <summary>Batch, channels, height, width. / 批次、通道、高度、宽度。</summary>
        Nchw = 0,
        /// <summary>Batch, height, width, channels. / 批次、高度、宽度、通道。</summary>
        Nhwc = 1,
        /// <summary>Channels, height, width. / 通道、高度、宽度。</summary>
        Chw = 2,
        /// <summary>Height, width, channels. / 高度、宽度、通道。</summary>
        Hwc = 3
    }

    /// <summary>Identifies the coordinate space represented by an anomaly map. / 标识异常图表示的坐标空间。</summary>
    public enum AnomalyMapCoordinateSpace
    {
        /// <summary>The map is aligned with model-input coordinates. / 异常图与模型输入坐标对齐。</summary>
        ModelInput = 0,
        /// <summary>The map is already aligned with source-image coordinates. / 异常图已与源图坐标对齐。</summary>
        SourceImage = 1
    }

    /// <summary>Identifies deterministic anomaly-map normalization modes. / 标识确定性的异常图归一化模式。</summary>
    public enum AnomalyNormalizationMode
    {
        /// <summary>Preserve aggregated values without normalization. / 不归一化并保留聚合值。</summary>
        None = 0,
        /// <summary>Normalize the current map using its finite minimum and maximum. / 使用当前图的有限最小值和最大值归一化。</summary>
        MinMax = 1,
        /// <summary>Normalize using an explicit fixed finite range and clamp to [0,1]. / 使用显式固定有限范围归一化并裁剪到 [0,1]。</summary>
        FixedRange = 2
    }

    /// <summary>Identifies supported and explicitly unsupported threshold policies. / 标识支持及明确不支持的阈值策略。</summary>
    public enum AnomalyThresholdPolicy
    {
        /// <summary>Use one explicit fixed threshold. / 使用一个显式固定阈值。</summary>
        Fixed = 0,
        /// <summary>Percentile thresholds are reserved and currently unsupported. / 百分位阈值已预留且当前不支持。</summary>
        Percentile = 1,
        /// <summary>Model-provided thresholds are reserved and currently unsupported. / 模型提供阈值已预留且当前不支持。</summary>
        ModelProvided = 2
    }

    /// <summary>Identifies channel aggregation for a multi-channel anomaly map. / 标识多通道异常图的通道聚合方式。</summary>
    public enum AnomalyChannelAggregation
    {
        /// <summary>Select one configured channel. / 选择一个已配置通道。</summary>
        SingleChannel = 0,
        /// <summary>Select the maximum channel value at each pixel. / 逐像素选择最大通道值。</summary>
        Maximum = 1,
        /// <summary>Compute the arithmetic channel mean at each pixel. / 逐像素计算通道算术平均值。</summary>
        Mean = 2
    }

    /// <summary>Identifies the spatial size returned for normalized anomaly maps and masks. / 标识归一化异常图与掩码的返回空间尺寸。</summary>
    public enum AnomalyOutputSizeMode
    {
        /// <summary>Restore to source-image size. / 恢复到源图尺寸。</summary>
        Source = 0,
        /// <summary>Restore to model-input size. / 恢复到模型输入尺寸。</summary>
        Model = 1,
        /// <summary>Keep tensor spatial size. / 保留张量空间尺寸。</summary>
        Tensor = 2
    }

    /// <summary>Identifies anomaly-map resize interpolation. / 标识异常图缩放插值方式。</summary>
    public enum AnomalyMapInterpolation
    {
        /// <summary>Nearest-neighbor sampling using pixel centers. / 使用像素中心的最近邻采样。</summary>
        Nearest = 0,
        /// <summary>Bilinear sampling using half-pixel centers. / 使用半像素中心的双线性采样。</summary>
        BilinearHalfPixel = 1
    }

    /// <summary>Defines strict named image-score and pixel-map tensor semantics. / 定义严格命名的图像分数与像素图张量语义。</summary>
    public sealed class AnomalyMapSchema
    {
        /// <summary>Initializes an anomaly output schema. / 初始化异常输出 Schema。</summary>
        public AnomalyMapSchema(string scoreOutputName, string mapOutputName, AnomalyMapValueMode valueMode, AnomalyTensorLayout layout, int channelCount, AnomalyMapCoordinateSpace coordinateSpace = AnomalyMapCoordinateSpace.ModelInput)
        {
            if (string.IsNullOrWhiteSpace(scoreOutputName)) throw new ArgumentException("An image-score output name is required.", nameof(scoreOutputName));
            if (string.IsNullOrWhiteSpace(mapOutputName)) throw new ArgumentException("An anomaly-map output name is required.", nameof(mapOutputName));
            if (string.Equals(scoreOutputName, mapOutputName, StringComparison.Ordinal)) throw new ArgumentException("Score and map outputs must use different names.", nameof(mapOutputName));
            if (!Enum.IsDefined(typeof(AnomalyMapValueMode), valueMode)) throw new ArgumentOutOfRangeException(nameof(valueMode));
            if (!Enum.IsDefined(typeof(AnomalyTensorLayout), layout)) throw new ArgumentOutOfRangeException(nameof(layout));
            if (channelCount <= 0 || channelCount > 4096) throw new ArgumentOutOfRangeException(nameof(channelCount));
            if (!Enum.IsDefined(typeof(AnomalyMapCoordinateSpace), coordinateSpace)) throw new ArgumentOutOfRangeException(nameof(coordinateSpace));
            ScoreOutputName = scoreOutputName;
            MapOutputName = mapOutputName;
            ValueMode = valueMode;
            Layout = layout;
            ChannelCount = channelCount;
            CoordinateSpace = coordinateSpace;
        }

        /// <summary>Gets the exact image-score output name. / 获取精确的图像分数输出名称。</summary>
        public string ScoreOutputName { get; }
        /// <summary>Gets the exact anomaly-map output name. / 获取精确的异常图输出名称。</summary>
        public string MapOutputName { get; }
        /// <summary>Gets score and map value semantics. / 获取分数与异常图值语义。</summary>
        public AnomalyMapValueMode ValueMode { get; }
        /// <summary>Gets the map tensor layout. / 获取异常图张量布局。</summary>
        public AnomalyTensorLayout Layout { get; }
        /// <summary>Gets the required map channel count. / 获取所需异常图通道数。</summary>
        public int ChannelCount { get; }
        /// <summary>Gets the map coordinate space. / 获取异常图坐标空间。</summary>
        public AnomalyMapCoordinateSpace CoordinateSpace { get; }
    }

    /// <summary>Controls bounded deterministic anomaly decoding and source restoration. / 控制有界且确定性的异常解码与源图恢复。</summary>
    public sealed class AnomalyDecoderOptions
    {
        /// <summary>Initializes anomaly decoder options. / 初始化异常解码选项。</summary>
        public AnomalyDecoderOptions(
            AnomalyNormalizationMode normalization = AnomalyNormalizationMode.None,
            AnomalyThresholdPolicy thresholdPolicy = AnomalyThresholdPolicy.Fixed,
            float threshold = 0.5f,
            float fixedRangeMinimum = 0f,
            float fixedRangeMaximum = 1f,
            AnomalyChannelAggregation channelAggregation = AnomalyChannelAggregation.SingleChannel,
            int channelIndex = 0,
            AnomalyOutputSizeMode outputSizeMode = AnomalyOutputSizeMode.Source,
            AnomalyMapInterpolation interpolation = AnomalyMapInterpolation.BilinearHalfPixel,
            bool preserveRawMap = true,
            long maximumMapPixels = 64L * 1024 * 1024,
            long maximumWorkspaceBytes = 512L * 1024 * 1024,
            long maximumOutputBytes = 512L * 1024 * 1024)
        {
            if (!Enum.IsDefined(typeof(AnomalyNormalizationMode), normalization)) throw new ArgumentOutOfRangeException(nameof(normalization));
            if (!Enum.IsDefined(typeof(AnomalyThresholdPolicy), thresholdPolicy)) throw new ArgumentOutOfRangeException(nameof(thresholdPolicy));
            if (float.IsNaN(threshold) || float.IsInfinity(threshold)) throw new ArgumentOutOfRangeException(nameof(threshold));
            if (float.IsNaN(fixedRangeMinimum) || float.IsInfinity(fixedRangeMinimum)) throw new ArgumentOutOfRangeException(nameof(fixedRangeMinimum));
            if (float.IsNaN(fixedRangeMaximum) || float.IsInfinity(fixedRangeMaximum)) throw new ArgumentOutOfRangeException(nameof(fixedRangeMaximum));
            if (normalization == AnomalyNormalizationMode.FixedRange && fixedRangeMaximum <= fixedRangeMinimum) throw new ArgumentException("A fixed normalization range requires maximum greater than minimum.", nameof(fixedRangeMaximum));
            if (normalization != AnomalyNormalizationMode.None && (threshold < 0f || threshold > 1f)) throw new ArgumentOutOfRangeException(nameof(threshold), "A normalized threshold must be in [0,1].");
            if (!Enum.IsDefined(typeof(AnomalyChannelAggregation), channelAggregation)) throw new ArgumentOutOfRangeException(nameof(channelAggregation));
            if (channelIndex < 0) throw new ArgumentOutOfRangeException(nameof(channelIndex));
            if (!Enum.IsDefined(typeof(AnomalyOutputSizeMode), outputSizeMode)) throw new ArgumentOutOfRangeException(nameof(outputSizeMode));
            if (!Enum.IsDefined(typeof(AnomalyMapInterpolation), interpolation)) throw new ArgumentOutOfRangeException(nameof(interpolation));
            if (maximumMapPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumMapPixels));
            if (maximumWorkspaceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWorkspaceBytes));
            if (maximumOutputBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
            Normalization = normalization;
            ThresholdPolicy = thresholdPolicy;
            Threshold = threshold;
            FixedRangeMinimum = fixedRangeMinimum;
            FixedRangeMaximum = fixedRangeMaximum;
            ChannelAggregation = channelAggregation;
            ChannelIndex = channelIndex;
            OutputSizeMode = outputSizeMode;
            Interpolation = interpolation;
            PreserveRawMap = preserveRawMap;
            MaximumMapPixels = maximumMapPixels;
            MaximumWorkspaceBytes = maximumWorkspaceBytes;
            MaximumOutputBytes = maximumOutputBytes;
        }

        /// <summary>Gets the normalization mode. / 获取归一化模式。</summary>
        public AnomalyNormalizationMode Normalization { get; }
        /// <summary>Gets the threshold policy. / 获取阈值策略。</summary>
        public AnomalyThresholdPolicy ThresholdPolicy { get; }
        /// <summary>Gets the fixed threshold applied after normalization and restoration. / 获取归一化和恢复后应用的固定阈值。</summary>
        public float Threshold { get; }
        /// <summary>Gets the fixed normalization minimum. / 获取固定归一化最小值。</summary>
        public float FixedRangeMinimum { get; }
        /// <summary>Gets the fixed normalization maximum. / 获取固定归一化最大值。</summary>
        public float FixedRangeMaximum { get; }
        /// <summary>Gets channel aggregation. / 获取通道聚合方式。</summary>
        public AnomalyChannelAggregation ChannelAggregation { get; }
        /// <summary>Gets the selected channel for single-channel aggregation. / 获取单通道聚合选择的通道。</summary>
        public int ChannelIndex { get; }
        /// <summary>Gets returned normalized-map and mask size. / 获取返回的归一化图与掩码尺寸。</summary>
        public AnomalyOutputSizeMode OutputSizeMode { get; }
        /// <summary>Gets spatial interpolation. / 获取空间插值方式。</summary>
        public AnomalyMapInterpolation Interpolation { get; }
        /// <summary>Gets whether the aggregated tensor-resolution raw map is retained. / 获取是否保留聚合后的张量分辨率原始图。</summary>
        public bool PreserveRawMap { get; }
        /// <summary>Gets the maximum pixel count of any map. / 获取任一异常图的最大像素数。</summary>
        public long MaximumMapPixels { get; }
        /// <summary>Gets the maximum estimated temporary workspace. / 获取最大估算临时工作区。</summary>
        public long MaximumWorkspaceBytes { get; }
        /// <summary>Gets the maximum estimated owned result bytes. / 获取最大估算自有结果字节数。</summary>
        public long MaximumOutputBytes { get; }
        /// <summary>Gets default bounded options. / 获取默认有界选项。</summary>
        public static AnomalyDecoderOptions Default { get; } = new AnomalyDecoderOptions();
    }

    /// <summary>Defines a replaceable backend-neutral anomaly postprocessor. / 定义可替换的后端无关异常后处理器。</summary>
    public interface IAnomalyPostprocessor : IVisualDecoder
    {
        /// <summary>Decodes one validated anomaly inference response. / 解码一个已验证的异常推理响应。</summary>
        public AnomalyDetectionResult DecodeAnomaly(VisualDecodeContext context);
    }
}
