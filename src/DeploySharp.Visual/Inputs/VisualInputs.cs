using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the logical order of image tensor dimensions. / 标识图像张量维度的逻辑顺序。</summary>
    public enum VisualTensorLayout
    {
        /// <summary>Batch, channels, height, width. / 批次、通道、高度、宽度。</summary>
        Nchw = 0,
        /// <summary>Batch, height, width, channels. / 批次、高度、宽度、通道。</summary>
        Nhwc = 1,
        /// <summary>Channels, height, width without a batch dimension. / 无批次维的通道、高度、宽度。</summary>
        Chw = 2,
        /// <summary>Height, width, channels without a batch dimension. / 无批次维的高度、宽度、通道。</summary>
        Hwc = 3
    }

    /// <summary>Describes the semantic color channel order expected by a model. / 描述模型期望的语义颜色通道顺序。</summary>
    public enum VisualColorOrder
    {
        /// <summary>Color order is unspecified. / 未指定颜色顺序。</summary>
        Unspecified = 0,
        /// <summary>Red, green, blue. / 红、绿、蓝。</summary>
        Rgb = 1,
        /// <summary>Blue, green, red. / 蓝、绿、红。</summary>
        Bgr = 2,
        /// <summary>Single luminance or grayscale channel. / 单亮度或灰度通道。</summary>
        Gray = 3,
        /// <summary>Red, green, blue, alpha. / 红、绿、蓝、透明度。</summary>
        Rgba = 4,
        /// <summary>Blue, green, red, alpha. / 蓝、绿、红、透明度。</summary>
        Bgra = 5
    }

    /// <summary>Describes preprocessing already applied by an image adapter without processing pixels in Visual. / 描述图像适配器已经应用的预处理，Visual 本身不处理像素。</summary>
    public sealed class VisualPreprocessingDescriptor
    {
        private readonly IReadOnlyList<float> _means;
        private readonly IReadOnlyList<float> _scales;

        /// <summary>Initializes preprocessing metadata. / 初始化预处理元数据。</summary>
        public VisualPreprocessingDescriptor(VisualColorOrder colorOrder, IEnumerable<float>? means = null, IEnumerable<float>? scales = null, string? notes = null)
        {
            if (!Enum.IsDefined(typeof(VisualColorOrder), colorOrder)) throw new VisualException(VisualErrorCodes.InputInvalid, "Color order is invalid.");
            ColorOrder = colorOrder;
            _means = CopyFinite(means, nameof(means));
            _scales = CopyFinite(scales, nameof(scales));
            if (_means.Count != 0 && _scales.Count != 0 && _means.Count != _scales.Count) throw new VisualException(VisualErrorCodes.InputInvalid, "Preprocessing means and scales must have equal lengths when both are provided.");
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        }

        /// <summary>Gets the semantic channel order. / 获取语义通道顺序。</summary>
        public VisualColorOrder ColorOrder { get; }
        /// <summary>Gets per-channel subtracted means. / 获取逐通道减去的均值。</summary>
        public IReadOnlyList<float> Means => _means;
        /// <summary>Gets per-channel multiplication scales. / 获取逐通道乘法缩放值。</summary>
        public IReadOnlyList<float> Scales => _scales;
        /// <summary>Gets optional adapter notes. / 获取可选适配器说明。</summary>
        public string? Notes { get; }

        private static IReadOnlyList<float> CopyFinite(IEnumerable<float>? values, string parameterName)
        {
            var result = new List<float>();
            if (values != null)
            {
                foreach (float value in values)
                {
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.InputInvalid, "Preprocessing values must be finite.", technicalDetails: parameterName);
                    result.Add(value);
                }
            }

            return result.AsReadOnly();
        }
    }

    /// <summary>Identifies who releases an optional resource associated with a prepared tensor. / 标识由谁释放与已准备张量关联的可选资源。</summary>
    public enum PreparedInputOwnership
    {
        /// <summary>The caller retains and releases every associated resource. / 调用方保留并释放所有关联资源。</summary>
        Borrowed = 0,
        /// <summary>The prepared input owns and idempotently releases the supplied resource. / 已准备输入拥有并幂等释放所提供的资源。</summary>
        Owned = 1
    }

    /// <summary>Contains an image tensor already prepared by an external adapter plus reversible geometry metadata. / 包含外部适配器已准备的图像张量及可逆几何元数据。</summary>
    public sealed class PreparedVisualInput : IDisposable
    {
        private readonly IDisposable? _ownedResource;
        private bool _disposed;

        /// <summary>Initializes a prepared visual input. Visual does not decode, resize, or modify pixels. / 初始化已准备视觉输入；Visual 不解码、缩放或修改像素。</summary>
        public PreparedVisualInput(
            string inputName,
            ITensor tensor,
            VisualSize sourceSize,
            VisualSize modelSize,
            int batchSize,
            VisualTensorLayout layout,
            ImageTransform transform,
            VisualPreprocessingDescriptor? preprocessing = null,
            string? inputId = null,
            PreparedInputOwnership ownership = PreparedInputOwnership.Borrowed,
            IDisposable? ownedResource = null)
        {
            if (string.IsNullOrWhiteSpace(inputName)) throw new VisualException(VisualErrorCodes.InputInvalid, "An input tensor name is required.", tensorName: inputName);
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (batchSize <= 0) throw new VisualException(VisualErrorCodes.InputInvalid, "Batch size must be positive.", tensorName: inputName);
            if (!Enum.IsDefined(typeof(VisualTensorLayout), layout)) throw new VisualException(VisualErrorCodes.InputInvalid, "Tensor layout is invalid.", tensorName: inputName);
            if (!Enum.IsDefined(typeof(PreparedInputOwnership), ownership)) throw new VisualException(VisualErrorCodes.InputInvalid, "Input ownership is invalid.", tensorName: inputName);
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (transform.SourceSize != sourceSize || transform.ModelSize != modelSize) throw new VisualException(VisualErrorCodes.InputInvalid, "Transform sizes must match the prepared input sizes.", tensorName: inputName);
            if (ownership == PreparedInputOwnership.Owned && ownedResource == null) throw new VisualException(VisualErrorCodes.InputInvalid, "Owned prepared input requires a disposable resource.", tensorName: inputName);
            if (ownership == PreparedInputOwnership.Borrowed && ownedResource != null) throw new VisualException(VisualErrorCodes.InputInvalid, "Borrowed prepared input cannot accept an owned resource.", tensorName: inputName);
            InputName = inputName;
            Tensor = tensor;
            SourceSize = sourceSize;
            ModelSize = modelSize;
            BatchSize = batchSize;
            Layout = layout;
            Transform = transform;
            Preprocessing = preprocessing ?? new VisualPreprocessingDescriptor(VisualColorOrder.Unspecified);
            InputId = string.IsNullOrWhiteSpace(inputId) ? null : inputId;
            Ownership = ownership;
            _ownedResource = ownedResource;
        }

        /// <summary>Gets the backend tensor input name. / 获取后端张量输入名称。</summary>
        public string InputName { get; }
        /// <summary>Gets the already prepared Core tensor. / 获取已经准备好的 Core 张量。</summary>
        public ITensor Tensor { get; }
        /// <summary>Gets the original source image size. / 获取原始源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the model spatial input size. / 获取模型空间输入尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets the declared batch size. / 获取声明的批次大小。</summary>
        public int BatchSize { get; }
        /// <summary>Gets the tensor layout. / 获取张量布局。</summary>
        public VisualTensorLayout Layout { get; }
        /// <summary>Gets the reversible source-to-model transform. / 获取可逆的源图到模型变换。</summary>
        public ImageTransform Transform { get; }
        /// <summary>Gets preprocessing metadata supplied by the image adapter. / 获取图像适配器提供的预处理元数据。</summary>
        public VisualPreprocessingDescriptor Preprocessing { get; }
        /// <summary>Gets an optional application input identifier. / 获取可选应用输入标识符。</summary>
        public string? InputId { get; }
        /// <summary>Gets resource ownership for this prepared input. / 获取此已准备输入的资源所有权。</summary>
        public PreparedInputOwnership Ownership { get; }
        /// <summary>Gets whether the prepared input has released its owned resource. / 获取已准备输入是否已释放其拥有的资源。</summary>
        public bool IsDisposed => _disposed;

        /// <inheritdoc />
        /// <remarks>Idempotently releases only an explicitly owned resource; borrowed tensors are never released. / 仅幂等释放显式拥有的资源；绝不释放借用张量。</remarks>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ownedResource?.Dispose();
        }

        internal void EnsureUsable()
        {
            if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The prepared visual input has been disposed.", tensorName: InputName);
        }
    }

    /// <summary>Controls one Visual inference call without changing pipeline configuration. / 控制一次 Visual 推理调用，而不更改 Pipeline 配置。</summary>
    public sealed class VisualExecutionOptions
    {
        /// <summary>Initializes execution options. / 初始化执行选项。</summary>
        public VisualExecutionOptions(TimeSpan? timeout = null, bool disposeOwnedInputOnCompletion = false, string? correlationId = null)
        {
            if (timeout.HasValue && timeout.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            Timeout = timeout;
            DisposeOwnedInputOnCompletion = disposeOwnedInputOnCompletion;
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
        }

        /// <summary>Gets the optional end-to-end timeout. / 获取可选端到端超时时间。</summary>
        public TimeSpan? Timeout { get; }
        /// <summary>Gets whether an owned input is disposed after success, failure, or cancellation. / 获取是否在成功、失败或取消后释放拥有的输入。</summary>
        public bool DisposeOwnedInputOnCompletion { get; }
        /// <summary>Gets an optional correlation identifier copied to the result. / 获取复制到结果的可选关联标识符。</summary>
        public string? CorrelationId { get; }
        /// <summary>Gets default execution options. / 获取默认执行选项。</summary>
        public static VisualExecutionOptions Default { get; } = new VisualExecutionOptions();
    }
}
