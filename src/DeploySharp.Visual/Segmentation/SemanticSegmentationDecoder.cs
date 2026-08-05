using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the semantic meaning of a segmentation output tensor. / 标识分割输出张量的语义含义。</summary>
    public enum SegmentationOutputKind
    {
        /// <summary>Raw scores; no implicit softmax or sigmoid is applied. / 原始分数；不隐式应用 softmax 或 sigmoid。</summary>
        Logits = 0,
        /// <summary>Values already constrained to probabilities in [0,1]. / 已限制在 [0,1] 的概率值。</summary>
        Probabilities = 1,
        /// <summary>Integer class indices. / 整数类别索引。</summary>
        LabelMap = 2
    }

    /// <summary>Identifies supported segmentation tensor dimension orders. / 标识支持的分割张量维度顺序。</summary>
    public enum SegmentationTensorLayout
    {
        /// <summary>Batch, channels, height, width. / 批次、通道、高度、宽度。</summary>
        Nchw = 0,
        /// <summary>Batch, height, width, channels. / 批次、高度、宽度、通道。</summary>
        Nhwc = 1,
        /// <summary>Channels, height, width. / 通道、高度、宽度。</summary>
        Chw = 2,
        /// <summary>Height, width, channels. / 高度、宽度、通道。</summary>
        Hwc = 3,
        /// <summary>Batch, height, width for integer label maps. / 用于整数标签图的批次、高度、宽度。</summary>
        Nhw = 4,
        /// <summary>Height, width for integer label maps. / 用于整数标签图的高度、宽度。</summary>
        Hw = 5
    }

    /// <summary>Identifies the spatial resolution returned by a segmentation decoder. / 标识分割解码器返回的空间分辨率。</summary>
    public enum SegmentationOutputSizeMode
    {
        /// <summary>Restore the result to the original source image size. / 将结果恢复到原始源图尺寸。</summary>
        Source = 0,
        /// <summary>Restore the result to the model input size. / 将结果恢复到模型输入尺寸。</summary>
        Model = 1,
        /// <summary>Keep the output tensor spatial size. / 保留输出张量空间尺寸。</summary>
        Tensor = 2
    }

    /// <summary>Defines a backend-neutral semantic segmentation output contract. / 定义后端无关的语义分割输出契约。</summary>
    public sealed class SegmentationOutputSchema
    {
        /// <summary>Initializes and validates a segmentation output schema. / 初始化并验证分割输出 Schema。</summary>
        public SegmentationOutputSchema(string outputName, SegmentationOutputKind kind, SegmentationTensorLayout layout, int classCount, int backgroundClassIndex = 0, int? ignoreClassIndex = null)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output tensor name is required.", nameof(outputName));
            if (!Enum.IsDefined(typeof(SegmentationOutputKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(SegmentationTensorLayout), layout)) throw new ArgumentOutOfRangeException(nameof(layout));
            if (classCount < 2 || classCount > ushort.MaxValue + 1) throw new ArgumentOutOfRangeException(nameof(classCount));
            if (backgroundClassIndex < 0 || backgroundClassIndex >= classCount) throw new ArgumentOutOfRangeException(nameof(backgroundClassIndex));
            if (ignoreClassIndex.HasValue && (ignoreClassIndex.Value < 0 || ignoreClassIndex.Value >= classCount)) throw new ArgumentOutOfRangeException(nameof(ignoreClassIndex));
            if (ignoreClassIndex.HasValue && ignoreClassIndex.Value == backgroundClassIndex) throw new ArgumentException("The ignore class must differ from the background class.", nameof(ignoreClassIndex));
            if (kind != SegmentationOutputKind.LabelMap && (layout == SegmentationTensorLayout.Nhw || layout == SegmentationTensorLayout.Hw)) throw new ArgumentException("Score maps require an explicit channel dimension.", nameof(layout));
            OutputName = outputName;
            Kind = kind;
            Layout = layout;
            ClassCount = classCount;
            BackgroundClassIndex = backgroundClassIndex;
            IgnoreClassIndex = ignoreClassIndex;
        }

        /// <summary>Gets the required backend output name. / 获取所需后端输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the output value semantics. / 获取输出值语义。</summary>
        public SegmentationOutputKind Kind { get; }
        /// <summary>Gets the tensor dimension order. / 获取张量维度顺序。</summary>
        public SegmentationTensorLayout Layout { get; }
        /// <summary>Gets the complete semantic class count. / 获取完整语义类别数。</summary>
        public int ClassCount { get; }
        /// <summary>Gets the class used for padding, filtering, and binary negatives. / 获取用于填充、过滤及二分类负样本的类别。</summary>
        public int BackgroundClassIndex { get; }
        /// <summary>Gets the optional class excluded from region filtering. / 获取不参与区域过滤的可选类别。</summary>
        public int? IgnoreClassIndex { get; }
    }

    /// <summary>Controls deterministic semantic segmentation decoding and bounded output retention. / 控制确定性语义分割解码及有界输出保留。</summary>
    public sealed class SegmentationDecoderOptions
    {
        /// <summary>Initializes segmentation decoder options. A null threshold selects 0 for logits and 0.5 for probabilities. / 初始化分割解码选项；空阈值对 logits 选择 0，对概率选择 0.5。</summary>
        public SegmentationDecoderOptions(float? binaryThreshold = null, SegmentationOutputSizeMode outputSizeMode = SegmentationOutputSizeMode.Source, int minimumRegionPixels = 1, bool generateRle = true, bool preserveProbabilityMap = false, bool generatePolygons = false, long maximumOutputBytes = 256L * 1024 * 1024)
        {
            if (binaryThreshold.HasValue && (float.IsNaN(binaryThreshold.Value) || float.IsInfinity(binaryThreshold.Value))) throw new ArgumentOutOfRangeException(nameof(binaryThreshold));
            if (!Enum.IsDefined(typeof(SegmentationOutputSizeMode), outputSizeMode)) throw new ArgumentOutOfRangeException(nameof(outputSizeMode));
            if (minimumRegionPixels <= 0) throw new ArgumentOutOfRangeException(nameof(minimumRegionPixels));
            if (maximumOutputBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
            BinaryThreshold = binaryThreshold;
            OutputSizeMode = outputSizeMode;
            MinimumRegionPixels = minimumRegionPixels;
            GenerateRle = generateRle;
            PreserveProbabilityMap = preserveProbabilityMap;
            GeneratePolygons = generatePolygons;
            MaximumOutputBytes = maximumOutputBytes;
        }

        /// <summary>Gets the optional explicit single-channel threshold. / 获取可选的显式单通道阈值。</summary>
        public float? BinaryThreshold { get; }
        /// <summary>Gets the returned mask resolution policy. / 获取返回掩码分辨率策略。</summary>
        public SegmentationOutputSizeMode OutputSizeMode { get; }
        /// <summary>Gets the minimum four-connected foreground region size. / 获取四连通前景区域最小尺寸。</summary>
        public int MinimumRegionPixels { get; }
        /// <summary>Gets whether DeploySharp row-major RLE is retained. / 获取是否保留 DeploySharp 行优先 RLE。</summary>
        public bool GenerateRle { get; }
        /// <summary>Gets whether probability output is retained in canonical tensor-resolution HWC order. / 获取是否按规范张量分辨率 HWC 顺序保留概率输出。</summary>
        public bool PreserveProbabilityMap { get; }
        /// <summary>Gets whether polygon extraction was requested. / 获取是否请求多边形提取。</summary>
        public bool GeneratePolygons { get; }
        /// <summary>Gets the maximum estimated bytes for decoded and retained data. / 获取解码及保留数据的最大估算字节数。</summary>
        public long MaximumOutputBytes { get; }
        /// <summary>Gets default bounded decoding options. / 获取默认有界解码选项。</summary>
        public static SegmentationDecoderOptions Default { get; } = new SegmentationDecoderOptions();
    }

    /// <summary>Decodes logits, probabilities, or integer label maps into an owned semantic result. / 将 logits、概率或整数标签图解码为自有语义结果。</summary>
    public sealed class SemanticSegmentationDecoder : IVisualDecoder
    {
        /// <summary>Initializes a semantic segmentation decoder. / 初始化语义分割解码器。</summary>
        public SemanticSegmentationDecoder(SegmentationOutputSchema schema, SegmentationDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options ?? SegmentationDecoderOptions.Default;
            if (Options.PreserveProbabilityMap && Schema.Kind != SegmentationOutputKind.Probabilities) throw new ArgumentException("Probability retention requires a probability output schema.", nameof(options));
            if (Schema.Kind == SegmentationOutputKind.Probabilities && Options.BinaryThreshold.HasValue && (Options.BinaryThreshold.Value < 0 || Options.BinaryThreshold.Value > 1)) throw new ArgumentException("A probability threshold must be in [0,1].", nameof(options));
            if (Schema.Kind == SegmentationOutputKind.LabelMap && Options.BinaryThreshold.HasValue) throw new ArgumentException("An integer label map does not use a binary threshold.", nameof(options));
        }

        /// <summary>Gets the immutable output schema. / 获取不可变输出 Schema。</summary>
        public SegmentationOutputSchema Schema { get; }
        /// <summary>Gets the immutable decoding options. / 获取不可变解码选项。</summary>
        public SegmentationDecoderOptions Options { get; }
        /// <inheritdoc />
        /// <remarks>Produces semantic segmentation results. / 生成语义分割结果。</remarks>
        public VisualTaskId Task => VisualTaskId.SemanticSegmentation;

        /// <inheritdoc />
        /// <remarks>Validates tensor semantics, applies deterministic decoding and nearest-neighbor geometry restoration, and returns owned arrays. / 验证张量语义，应用确定性解码和最近邻几何恢复，并返回自有数组。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw Failure(context, VisualErrorCodes.TensorInvalid, "Semantic segmentation currently supports batch size one.");
            if (Options.GeneratePolygons) throw Failure(context, VisualErrorCodes.CapabilityUnavailable, "Semantic polygon extraction is unsupported because hole and multi-component semantics are not yet guaranteed.");

            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(Schema.OutputName); }
            catch (KeyNotFoundException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "The semantic segmentation output is missing.", exception); }
            TensorDimensions dimensions = ResolveDimensions(tensor, context);
            ValidateElementType(tensor, context);
            foreach (VisualLabel label in context.Profile.Labels) if (label.Index >= Schema.ClassCount) throw Failure(context, VisualErrorCodes.DecodeFailed, "A profile label index exceeds the segmentation class count.", technicalDetails: "labelIndex=" + label.Index);
            VisualSize targetSize = GetTargetSize(context, dimensions);
            EnsureBounded(dimensions, targetSize, tensor, context);

            float[]? canonicalProbabilities = null;
            ushort[] tensorMask = Schema.Kind == SegmentationOutputKind.LabelMap
                ? DecodeLabelMap(tensor, dimensions, context)
                : DecodeScores(tensor, dimensions, context, out canonicalProbabilities);
            ushort[] resultValues = RestoreMask(tensorMask, dimensions.Width, dimensions.Height, targetSize, context);
            if (Options.MinimumRegionPixels > 1) FilterSmallRegions(resultValues, targetSize.Width, targetSize.Height, context.CancellationToken);
            var mask = new SemanticSegmentationMask(targetSize.Width, targetSize.Height, resultValues, true);
            IReadOnlyList<SemanticSegmentationClass> classes = CreateClasses(context.Profile);
            IReadOnlyList<SegmentationClassStatistics> statistics = CreateStatistics(resultValues);
            SegmentationRle? rle = Options.GenerateRle ? SegmentationRle.Encode(mask) : null;
            SegmentationProbabilityMap? probabilityMap = canonicalProbabilities == null ? null : new SegmentationProbabilityMap(dimensions.Width, dimensions.Height, dimensions.Channels, canonicalProbabilities, true);
            return new SemanticSegmentationResult(mask, classes, statistics, rle, probabilityMap);
        }

        private TensorDimensions ResolveDimensions(ITensor tensor, VisualDecodeContext context)
        {
            TensorShape shape = tensor.Shape;
            long batch = 1;
            long channels = 1;
            long height;
            long width;
            switch (Schema.Layout)
            {
                case SegmentationTensorLayout.Nchw:
                    RequireRank(shape, 4, context);
                    batch = shape[0]; channels = shape[1]; height = shape[2]; width = shape[3];
                    break;
                case SegmentationTensorLayout.Nhwc:
                    RequireRank(shape, 4, context);
                    batch = shape[0]; height = shape[1]; width = shape[2]; channels = shape[3];
                    break;
                case SegmentationTensorLayout.Chw:
                    RequireRank(shape, 3, context);
                    channels = shape[0]; height = shape[1]; width = shape[2];
                    break;
                case SegmentationTensorLayout.Hwc:
                    RequireRank(shape, 3, context);
                    height = shape[0]; width = shape[1]; channels = shape[2];
                    break;
                case SegmentationTensorLayout.Nhw:
                    RequireRank(shape, 3, context);
                    batch = shape[0]; height = shape[1]; width = shape[2];
                    break;
                case SegmentationTensorLayout.Hw:
                    RequireRank(shape, 2, context);
                    height = shape[0]; width = shape[1];
                    break;
                default:
                    throw Failure(context, VisualErrorCodes.TensorInvalid, "The segmentation tensor layout is invalid.");
            }

            if (batch != 1) throw Failure(context, VisualErrorCodes.TensorInvalid, "The segmentation output batch dimension must be one.", technicalDetails: shape.ToString());
            if (height <= 0 || width <= 0 || height > int.MaxValue || width > int.MaxValue) throw Failure(context, VisualErrorCodes.TensorInvalid, "The segmentation spatial dimensions are invalid.", technicalDetails: shape.ToString());
            if (Schema.Kind == SegmentationOutputKind.LabelMap)
            {
                if (channels != 1) throw Failure(context, VisualErrorCodes.TensorInvalid, "An integer label map must contain one channel.", technicalDetails: shape.ToString());
            }
            else if (!((channels == 1 && Schema.ClassCount == 2) || channels == Schema.ClassCount))
            {
                throw Failure(context, VisualErrorCodes.TensorInvalid, "The score-map channel count does not match the schema class count.", technicalDetails: shape.ToString());
            }

            long expected;
            try { expected = checked(batch * channels * height * width); }
            catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "The segmentation tensor dimensions overflow the supported element count.", exception, shape.ToString()); }
            if (tensor.Length != expected) throw Failure(context, VisualErrorCodes.TensorInvalid, "The segmentation tensor element count is inconsistent.", technicalDetails: shape.ToString());
            return new TensorDimensions(checked((int)width), checked((int)height), checked((int)channels));
        }

        private void ValidateElementType(ITensor tensor, VisualDecodeContext context)
        {
            if (Schema.Kind == SegmentationOutputKind.LabelMap)
            {
                bool integer = tensor.ElementType == TensorElementType.Int8 || tensor.ElementType == TensorElementType.UInt8 ||
                    tensor.ElementType == TensorElementType.Int16 || tensor.ElementType == TensorElementType.UInt16 ||
                    tensor.ElementType == TensorElementType.Int32 || tensor.ElementType == TensorElementType.UInt32 ||
                    tensor.ElementType == TensorElementType.Int64 || tensor.ElementType == TensorElementType.UInt64;
                if (!integer) throw Failure(context, VisualErrorCodes.TensorInvalid, "A label map requires an integer tensor.");
            }
            else if (tensor.ElementType != TensorElementType.Float32 && tensor.ElementType != TensorElementType.Float64)
            {
                throw Failure(context, VisualErrorCodes.TensorInvalid, "A score map requires a Float32 or Float64 tensor.");
            }
        }

        private void EnsureBounded(TensorDimensions dimensions, VisualSize target, ITensor tensor, VisualDecodeContext context)
        {
            try
            {
                long tensorPixels = checked((long)dimensions.Width * dimensions.Height);
                long targetPixels = checked((long)target.Width * target.Height);
                if (tensorPixels > int.MaxValue || targetPixels > int.MaxValue || tensor.Length > int.MaxValue) throw new OverflowException();
                long bytes = checked(tensor.Length * (Schema.Kind == SegmentationOutputKind.LabelMap ? 8L : 4L));
                bytes = checked(bytes + (targetPixels * 2L));
                bytes = checked(bytes + (Schema.ClassCount * 128L));
                if (Options.GenerateRle) bytes = checked(bytes + (targetPixels * 48L));
                if (Options.MinimumRegionPixels > 1) bytes = checked(bytes + (targetPixels * 5L));
                if (Options.PreserveProbabilityMap) bytes = checked(bytes + (tensor.Length * 4L));
                if (bytes > Options.MaximumOutputBytes) throw Failure(context, VisualErrorCodes.DecodeFailed, "The estimated segmentation output exceeds the configured memory limit.", technicalDetails: "estimatedBytes=" + bytes + "; maximumBytes=" + Options.MaximumOutputBytes);
            }
            catch (OverflowException exception)
            {
                throw Failure(context, VisualErrorCodes.DecodeFailed, "The segmentation output dimensions exceed supported managed-array limits.", exception);
            }
        }

        private ushort[] DecodeScores(ITensor tensor, TensorDimensions dimensions, VisualDecodeContext context, out float[]? canonicalProbabilities)
        {
            float[] values = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Schema.OutputName);
            int pixels = checked(dimensions.Width * dimensions.Height);
            var result = new ushort[pixels];
            canonicalProbabilities = Options.PreserveProbabilityMap ? new float[values.Length] : null;
            float threshold = Options.BinaryThreshold ?? (Schema.Kind == SegmentationOutputKind.Logits ? 0f : 0.5f);
            int foreground = Schema.BackgroundClassIndex == 0 ? 1 : 0;
            for (int pixel = 0; pixel < pixels; pixel++)
            {
                if ((pixel & 4095) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                int bestClass = 0;
                float bestValue = float.NegativeInfinity;
                for (int channel = 0; channel < dimensions.Channels; channel++)
                {
                    float value = values[GetTensorIndex(pixel, channel, dimensions)];
                    if (Schema.Kind == SegmentationOutputKind.Probabilities && (value < 0 || value > 1)) throw Failure(context, VisualErrorCodes.DecodeFailed, "A segmentation probability must be in [0,1].", technicalDetails: "pixel=" + pixel + "; channel=" + channel);
                    if (canonicalProbabilities != null) canonicalProbabilities[(pixel * dimensions.Channels) + channel] = value;
                    if (value > bestValue) { bestValue = value; bestClass = channel; }
                }

                result[pixel] = dimensions.Channels == 1
                    ? (ushort)(bestValue >= threshold ? foreground : Schema.BackgroundClassIndex)
                    : (ushort)bestClass;
            }

            return result;
        }

        private ushort[] DecodeLabelMap(ITensor tensor, TensorDimensions dimensions, VisualDecodeContext context)
        {
            int length = checked(dimensions.Width * dimensions.Height);
            var result = new ushort[length];
            for (int index = 0; index < length; index++)
            {
                if ((index & 4095) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                ulong value = ReadUnsignedInteger(tensor, index, context);
                if (value >= (ulong)Schema.ClassCount || value > ushort.MaxValue) throw Failure(context, VisualErrorCodes.DecodeFailed, "A label-map class index exceeds the configured class count.", technicalDetails: "index=" + index + "; value=" + value);
                result[index] = (ushort)value;
            }

            return result;
        }

        private ulong ReadUnsignedInteger(ITensor tensor, int index, VisualDecodeContext context)
        {
            switch (tensor.ElementType)
            {
                case TensorElementType.Int8:
                    sbyte int8 = ((sbyte[])tensor.Buffer)[index];
                    if (int8 < 0) throw NegativeLabel(context, index);
                    return (ulong)int8;
                case TensorElementType.UInt8: return ((byte[])tensor.Buffer)[index];
                case TensorElementType.Int16:
                    short int16 = ((short[])tensor.Buffer)[index];
                    if (int16 < 0) throw NegativeLabel(context, index);
                    return (ulong)int16;
                case TensorElementType.UInt16: return ((ushort[])tensor.Buffer)[index];
                case TensorElementType.Int32:
                    int int32 = ((int[])tensor.Buffer)[index];
                    if (int32 < 0) throw NegativeLabel(context, index);
                    return (ulong)int32;
                case TensorElementType.UInt32: return ((uint[])tensor.Buffer)[index];
                case TensorElementType.Int64:
                    long int64 = ((long[])tensor.Buffer)[index];
                    if (int64 < 0) throw NegativeLabel(context, index);
                    return (ulong)int64;
                case TensorElementType.UInt64: return ((ulong[])tensor.Buffer)[index];
                default: throw Failure(context, VisualErrorCodes.TensorInvalid, "The label-map element type is unsupported.");
            }
        }

        private VisualException NegativeLabel(VisualDecodeContext context, int index) => Failure(context, VisualErrorCodes.DecodeFailed, "A label-map class index cannot be negative.", technicalDetails: "index=" + index);

        private int GetTensorIndex(int pixel, int channel, TensorDimensions dimensions)
        {
            int y = pixel / dimensions.Width;
            int x = pixel - (y * dimensions.Width);
            if (Schema.Layout == SegmentationTensorLayout.Nchw || Schema.Layout == SegmentationTensorLayout.Chw) return ((channel * dimensions.Height) + y) * dimensions.Width + x;
            return ((y * dimensions.Width) + x) * dimensions.Channels + channel;
        }

        private ushort[] RestoreMask(ushort[] tensorMask, int tensorWidth, int tensorHeight, VisualSize target, VisualDecodeContext context)
        {
            if (Options.OutputSizeMode == SegmentationOutputSizeMode.Tensor) return tensorMask;
            ushort[] modelMask = tensorWidth == context.Input.ModelSize.Width && tensorHeight == context.Input.ModelSize.Height
                ? tensorMask
                : ResizeNearest(tensorMask, tensorWidth, tensorHeight, context.Input.ModelSize.Width, context.Input.ModelSize.Height, context.CancellationToken);
            if (Options.OutputSizeMode == SegmentationOutputSizeMode.Model) return modelMask;

            var sourceMask = new ushort[checked(target.Width * target.Height)];
            ushort fill = (ushort)Schema.BackgroundClassIndex;
            for (int y = 0; y < target.Height; y++)
            {
                if ((y & 63) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < target.Width; x++)
                {
                    // Pixel centers preserve nearest-neighbor semantics for resize, letterbox, crop, and explicit affine transforms. / 像素中心可为缩放、letterbox、裁剪及显式仿射变换保持最近邻语义。
                    float modelX = ((x + 0.5f) * context.Input.Transform.ScaleX) + context.Input.Transform.OffsetX;
                    float modelY = ((y + 0.5f) * context.Input.Transform.ScaleY) + context.Input.Transform.OffsetY;
                    int sourceX = (int)Math.Floor(modelX);
                    int sourceY = (int)Math.Floor(modelY);
                    sourceMask[(y * target.Width) + x] = sourceX >= 0 && sourceX < context.Input.ModelSize.Width && sourceY >= 0 && sourceY < context.Input.ModelSize.Height
                        ? modelMask[(sourceY * context.Input.ModelSize.Width) + sourceX]
                        : fill;
                }
            }

            return sourceMask;
        }

        private static ushort[] ResizeNearest(ushort[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, CancellationToken cancellationToken)
        {
            var result = new ushort[checked(targetWidth * targetHeight)];
            for (int y = 0; y < targetHeight; y++)
            {
                if ((y & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                int sourceY = Math.Min(sourceHeight - 1, (int)(((long)y * sourceHeight) / targetHeight));
                for (int x = 0; x < targetWidth; x++)
                {
                    int sourceX = Math.Min(sourceWidth - 1, (int)(((long)x * sourceWidth) / targetWidth));
                    result[(y * targetWidth) + x] = source[(sourceY * sourceWidth) + sourceX];
                }
            }

            return result;
        }

        private void FilterSmallRegions(ushort[] values, int width, int height, CancellationToken cancellationToken)
        {
            var visited = new bool[values.Length];
            var queue = new int[values.Length];
            ushort background = (ushort)Schema.BackgroundClassIndex;
            ushort? ignored = Schema.IgnoreClassIndex.HasValue ? (ushort?)Schema.IgnoreClassIndex.Value : null;
            for (int start = 0; start < values.Length; start++)
            {
                if ((start & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (visited[start]) continue;
                ushort value = values[start];
                if (value == background || (ignored.HasValue && value == ignored.Value)) { visited[start] = true; continue; }
                int head = 0;
                int tail = 0;
                visited[start] = true;
                queue[tail++] = start;
                while (head < tail)
                {
                    int current = queue[head++];
                    int x = current % width;
                    int y = current / width;
                    if (x > 0) Visit(current - 1, value, values, visited, queue, ref tail);
                    if (x + 1 < width) Visit(current + 1, value, values, visited, queue, ref tail);
                    if (y > 0) Visit(current - width, value, values, visited, queue, ref tail);
                    if (y + 1 < height) Visit(current + width, value, values, visited, queue, ref tail);
                }

                if (tail < Options.MinimumRegionPixels) for (int index = 0; index < tail; index++) values[queue[index]] = background;
            }
        }

        private static void Visit(int index, ushort value, ushort[] values, bool[] visited, int[] queue, ref int tail)
        {
            if (visited[index] || values[index] != value) return;
            visited[index] = true;
            queue[tail++] = index;
        }

        private IReadOnlyList<SemanticSegmentationClass> CreateClasses(VisualModelProfile profile)
        {
            var result = new List<SemanticSegmentationClass>(Schema.ClassCount);
            for (int index = 0; index < Schema.ClassCount; index++)
            {
                bool background = index == Schema.BackgroundClassIndex;
                bool ignored = Schema.IgnoreClassIndex.HasValue && index == Schema.IgnoreClassIndex.Value;
                result.Add(new SemanticSegmentationClass(index, profile.GetLabel(index), ColorFor(index, background, ignored), background, ignored));
            }

            return result.AsReadOnly();
        }

        private IReadOnlyList<SegmentationClassStatistics> CreateStatistics(ushort[] values)
        {
            var counts = new long[Schema.ClassCount];
            for (int index = 0; index < values.Length; index++) counts[values[index]]++;
            var result = new List<SegmentationClassStatistics>(Schema.ClassCount);
            for (int index = 0; index < counts.Length; index++) result.Add(new SegmentationClassStatistics(index, counts[index], (double)counts[index] / values.Length));
            return result.AsReadOnly();
        }

        private static SegmentationColor ColorFor(int classIndex, bool background, bool ignored)
        {
            if (background) return new SegmentationColor(0, 0, 0);
            if (ignored) return new SegmentationColor(128, 128, 128);
            int value = classIndex;
            int red = 0;
            int green = 0;
            int blue = 0;
            for (int shift = 0; shift < 8; shift++)
            {
                red |= (value & 1) << (7 - shift);
                green |= ((value >> 1) & 1) << (7 - shift);
                blue |= ((value >> 2) & 1) << (7 - shift);
                value >>= 3;
            }

            return new SegmentationColor((byte)red, (byte)green, (byte)blue);
        }

        private VisualSize GetTargetSize(VisualDecodeContext context, TensorDimensions dimensions)
        {
            if (Options.OutputSizeMode == SegmentationOutputSizeMode.Source) return context.Input.SourceSize;
            if (Options.OutputSizeMode == SegmentationOutputSizeMode.Model) return context.Input.ModelSize;
            return new VisualSize(dimensions.Width, dimensions.Height);
        }

        private void RequireRank(TensorShape shape, int rank, VisualDecodeContext context)
        {
            if (shape.Rank != rank) throw Failure(context, VisualErrorCodes.TensorInvalid, "The segmentation output rank does not match its layout.", technicalDetails: shape.ToString());
        }

        private VisualException Failure(VisualDecodeContext context, string code, string message, Exception? innerException = null, string? technicalDetails = null)
        {
            return new VisualException(code, message, innerException, context.Profile.ProfileId, Schema.OutputName, modelId: context.Profile.ModelId, technicalDetails: technicalDetails);
        }

        private sealed class TensorDimensions
        {
            public TensorDimensions(int width, int height, int channels) { Width = width; Height = height; Channels = channels; }
            public int Width { get; }
            public int Height { get; }
            public int Channels { get; }
        }
    }
}
