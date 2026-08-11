using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies an extensible visual task without a closed enumeration. / 在不使用封闭枚举的情况下标识可扩展视觉任务。</summary>
    public readonly struct VisualTaskId : IEquatable<VisualTaskId>
    {
        private readonly string? _value;

        /// <summary>Initializes a visual task identifier. / 初始化视觉任务标识符。</summary>
        public VisualTaskId(string value) { _value = VisualGuard.Identifier(value, nameof(value)); }
        /// <summary>Gets the normalized task value. / 获取规范化任务值。</summary>
        public string Value => _value ?? string.Empty;
        /// <summary>Gets whether this is the default empty task. / 获取是否为默认空任务。</summary>
        public bool IsEmpty => string.IsNullOrEmpty(_value);
        /// <summary>Gets the image-classification task. / 获取图像分类任务。</summary>
        public static VisualTaskId ImageClassification { get; } = new VisualTaskId("image-classification");
        /// <summary>Gets the object-detection task. / 获取目标检测任务。</summary>
        public static VisualTaskId ObjectDetection { get; } = new VisualTaskId("object-detection");
        /// <summary>Gets the semantic-segmentation task. / 获取语义分割任务。</summary>
        public static VisualTaskId SemanticSegmentation { get; } = new VisualTaskId("semantic-segmentation");
        /// <summary>Gets the pose-estimation task. / 获取姿态估计任务。</summary>
        public static VisualTaskId PoseEstimation { get; } = new VisualTaskId("pose-estimation");
        /// <summary>Gets the instance-segmentation task. / 获取实例分割任务。</summary>
        public static VisualTaskId InstanceSegmentation { get; } = new VisualTaskId("instance-segmentation");
        /// <summary>Gets the oriented-object-detection task. / 获取旋转目标检测任务。</summary>
        public static VisualTaskId OrientedObjectDetection { get; } = new VisualTaskId("oriented-object-detection");
        /// <summary>Gets the text-detection task. / 获取文本检测任务。</summary>
        public static VisualTaskId TextDetection { get; } = new VisualTaskId("text-detection");
        /// <summary>Gets the text-recognition task. / 获取文本识别任务。</summary>
        public static VisualTaskId TextRecognition { get; } = new VisualTaskId("text-recognition");
        /// <summary>Gets the four-class OCR text-orientation classification task. / 获取四分类 OCR 文本方向分类任务。</summary>
        public static VisualTaskId TextOrientationClassification { get; } = new VisualTaskId("text-orientation-classification");
        /// <summary>Gets the complete optical-character-recognition task. / 获取完整光学字符识别任务。</summary>
        public static VisualTaskId OpticalCharacterRecognition { get; } = new VisualTaskId("optical-character-recognition");
        /// <summary>Gets the anomaly-detection and anomaly-segmentation task. / 获取异常检测与异常分割任务。</summary>
        public static VisualTaskId AnomalyDetection { get; } = new VisualTaskId("anomaly-detection");
        /// <summary>Gets foreground matting and semantic-alpha extraction. / 获取前景抠图与语义 Alpha 提取任务。</summary>
        public static VisualTaskId ForegroundMatting { get; } = new VisualTaskId("foreground-matting");
        /// <summary>Gets image promptable segmentation. / 获取图像可提示分割任务。</summary>
        public static VisualTaskId PromptableSegmentation { get; } = new VisualTaskId("promptable-segmentation");
        /// <summary>Gets stateful video prompt propagation. / 获取有状态视频提示传播任务。</summary>
        public static VisualTaskId PromptableVideoSegmentation { get; } = new VisualTaskId("promptable-video-segmentation");
        /// <inheritdoc />
        /// <remarks>Uses ordinal task equality. / 使用序号任务相等性。</remarks>
        public bool Equals(VisualTaskId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        /// <inheritdoc />
        /// <remarks>Compares an object by normalized task value. / 按规范化任务值比较对象。</remarks>
        public override bool Equals(object? obj) => obj is VisualTaskId other && Equals(other);
        /// <inheritdoc />
        /// <remarks>Computes an ordinal hash code. / 计算序号哈希码。</remarks>
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        /// <inheritdoc />
        /// <remarks>Returns the normalized task. / 返回规范化任务。</remarks>
        public override string ToString() => Value;
        /// <summary>Compares two tasks for equality. / 比较两个任务是否相等。</summary>
        public static bool operator ==(VisualTaskId left, VisualTaskId right) => left.Equals(right);
        /// <summary>Compares two tasks for inequality. / 比较两个任务是否不相等。</summary>
        public static bool operator !=(VisualTaskId left, VisualTaskId right) => !left.Equals(right);
    }

    /// <summary>Maps a non-negative class index to a stable display label. / 将非负类别索引映射到稳定显示标签。</summary>
    public sealed class VisualLabel
    {
        /// <summary>Initializes a class label. / 初始化类别标签。</summary>
        public VisualLabel(int index, string label)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A label is required.", nameof(label));
            Index = index;
            Label = label;
        }

        /// <summary>Gets the zero-based class index. / 获取从零开始的类别索引。</summary>
        public int Index { get; }
        /// <summary>Gets the display label. / 获取显示标签。</summary>
        public string Label { get; }
    }

    /// <summary>Defines a validated input tensor binding and shape pattern. / 定义已验证的输入张量绑定和形状模式。</summary>
    public sealed class VisualInputBinding
    {
        /// <summary>Initializes an input binding. Use -1 in a shape pattern for a dynamic dimension. / 初始化输入绑定；形状模式中使用 -1 表示动态维度。</summary>
        public VisualInputBinding(string name, TensorElementType elementType, TensorShape shapePattern, VisualTensorLayout layout, int minimumBatch = 1, int maximumBatch = 1)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "An input tensor name is required.", tensorName: name);
            if (elementType == TensorElementType.Unknown || elementType == TensorElementType.String) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Visual input element type is unsupported.", tensorName: name);
            if (shapePattern == null) throw new ArgumentNullException(nameof(shapePattern));
            if (!Enum.IsDefined(typeof(VisualTensorLayout), layout)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Input layout is invalid.", tensorName: name);
            int expectedRank = layout == VisualTensorLayout.Nchw || layout == VisualTensorLayout.Nhwc ? 4 : 3;
            if (shapePattern.Rank != expectedRank) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Input shape rank does not match its layout.", tensorName: name);
            if (minimumBatch <= 0 || maximumBatch < minimumBatch) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Input batch bounds are invalid.", tensorName: name);
            if ((layout == VisualTensorLayout.Chw || layout == VisualTensorLayout.Hwc) && (minimumBatch != 1 || maximumBatch != 1)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Unbatched layouts require batch bounds of one.", tensorName: name);
            if (layout == VisualTensorLayout.Nchw || layout == VisualTensorLayout.Nhwc)
            {
                long declaredBatch = shapePattern[0];
                if (declaredBatch == 0 || declaredBatch < -1) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A batched input requires a positive static batch or -1 for a dynamic batch.", tensorName: name);
                if (declaredBatch > 0 && (declaredBatch != minimumBatch || declaredBatch != maximumBatch)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A static batch dimension must exactly match its batch bounds.", tensorName: name);
            }
            Name = name;
            ElementType = elementType;
            ShapePattern = new TensorShape(shapePattern.ToArray());
            Layout = layout;
            MinimumBatch = minimumBatch;
            MaximumBatch = maximumBatch;
        }

        /// <summary>Gets the tensor name. / 获取张量名称。</summary>
        public string Name { get; }
        /// <summary>Gets the required element type. / 获取所需元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the static or dynamic shape pattern. / 获取静态或动态形状模式。</summary>
        public TensorShape ShapePattern { get; }
        /// <summary>Gets the tensor layout. / 获取张量布局。</summary>
        public VisualTensorLayout Layout { get; }
        /// <summary>Gets the minimum supported batch. / 获取最小支持批次。</summary>
        public int MinimumBatch { get; }
        /// <summary>Gets the maximum supported batch. / 获取最大支持批次。</summary>
        public int MaximumBatch { get; }
    }

    /// <summary>Defines a non-image named input supplied by an image adapter. / 定义由图像适配器提供的非图像命名输入。</summary>
    public sealed class VisualAuxiliaryInputBinding
    {
        /// <summary>Initializes an auxiliary input binding. / 初始化辅助输入绑定。</summary>
        public VisualAuxiliaryInputBinding(string name, TensorElementType elementType, TensorShape shapePattern)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "An auxiliary input tensor name is required.", tensorName: name);
            if (elementType == TensorElementType.Unknown || elementType == TensorElementType.String) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Auxiliary input element type is unsupported.", tensorName: name);
            Name = name;
            ElementType = elementType;
            ShapePattern = shapePattern == null ? throw new ArgumentNullException(nameof(shapePattern)) : new TensorShape(shapePattern.ToArray());
        }

        /// <summary>Gets the exact auxiliary input name. / 获取精确辅助输入名称。</summary>
        public string Name { get; }
        /// <summary>Gets the required element type. / 获取所需元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the static or dynamic shape pattern. / 获取静态或动态形状模式。</summary>
        public TensorShape ShapePattern { get; }
    }

    /// <summary>Defines one required backend output tensor. / 定义一个必需的后端输出张量。</summary>
    public sealed class VisualOutputBinding
    {
        /// <summary>Initializes an output binding. / 初始化输出绑定。</summary>
        public VisualOutputBinding(string name, TensorElementType elementType, TensorShape shapePattern)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "An output tensor name is required.", tensorName: name);
            if (elementType == TensorElementType.Unknown || elementType == TensorElementType.String) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Visual output element type is unsupported.", tensorName: name);
            Name = name;
            ElementType = elementType;
            ShapePattern = shapePattern == null ? throw new ArgumentNullException(nameof(shapePattern)) : new TensorShape(shapePattern.ToArray());
        }

        /// <summary>Gets the tensor name. / 获取张量名称。</summary>
        public string Name { get; }
        /// <summary>Gets the required element type. / 获取所需元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the static or dynamic shape pattern. / 获取静态或动态形状模式。</summary>
        public TensorShape ShapePattern { get; }
    }

    /// <summary>Describes one immutable visual model contract independently from a concrete backend. / 独立于具体后端描述一个不可变视觉模型契约。</summary>
    public sealed class VisualModelProfile
    {
        private readonly IReadOnlyList<VisualOutputBinding> _outputs;
        private readonly IReadOnlyList<VisualAuxiliaryInputBinding> _auxiliaryInputs;
        private readonly IReadOnlyList<VisualLabel> _labels;
        private readonly IReadOnlyDictionary<int, string> _labelsByIndex;

        /// <summary>Initializes and validates a visual model profile. / 初始化并验证视觉模型 Profile。</summary>
        public VisualModelProfile(
            string profileId,
            ModelId modelId,
            VisualTaskId task,
            string version,
            string modelFormat,
            VisualInputBinding input,
            IEnumerable<VisualOutputBinding> outputs,
            IEnumerable<VisualLabel> labels,
            IVisualDecoder decoder,
            BackendCapabilities requiredCapabilities = BackendCapabilities.TensorInference,
            string? minimumBackendVersion = null,
            IEnumerable<VisualAuxiliaryInputBinding>? auxiliaryInputs = null)
        {
            ProfileId = VisualGuard.Identifier(profileId, nameof(profileId));
            if (modelId.IsEmpty) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A model identifier is required.", profileId: ProfileId);
            if (task.IsEmpty) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A visual task is required.", profileId: ProfileId);
            if (string.IsNullOrWhiteSpace(version)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A profile version is required.", profileId: ProfileId);
            ModelId = modelId;
            Task = task;
            Version = version;
            ModelFormat = VisualGuard.Identifier(modelFormat, nameof(modelFormat));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            if (decoder.Task != task) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Decoder task does not match the profile task.", profileId: ProfileId);
            if ((requiredCapabilities & BackendCapabilities.TensorInference) == 0) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Visual profiles require tensor inference capability.", profileId: ProfileId);
            RequiredCapabilities = requiredCapabilities;
            MinimumBackendVersion = string.IsNullOrWhiteSpace(minimumBackendVersion) ? null : minimumBackendVersion;

            var auxiliaryList = new List<VisualAuxiliaryInputBinding>();
            var auxiliaryNames = new HashSet<string>(StringComparer.Ordinal) { Input.Name };
            if (auxiliaryInputs != null)
            {
                foreach (VisualAuxiliaryInputBinding auxiliary in auxiliaryInputs)
                {
                    if (auxiliary == null || !auxiliaryNames.Add(auxiliary.Name)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Auxiliary input names must be unique and cannot equal the image input.", profileId: ProfileId, tensorName: auxiliary == null ? null : auxiliary.Name);
                    auxiliaryList.Add(auxiliary);
                }
            }
            _auxiliaryInputs = auxiliaryList.AsReadOnly();

            var outputList = new List<VisualOutputBinding>();
            var outputNames = new HashSet<string>(StringComparer.Ordinal);
            if (outputs == null) throw new ArgumentNullException(nameof(outputs));
            foreach (VisualOutputBinding output in outputs)
            {
                if (output == null) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Output bindings cannot contain null.", profileId: ProfileId);
                if (!outputNames.Add(output.Name)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Output tensor names must be unique.", profileId: ProfileId, tensorName: output.Name);
                outputList.Add(output);
            }
            if (outputList.Count == 0) throw new VisualException(VisualErrorCodes.ProfileInvalid, "At least one output binding is required.", profileId: ProfileId);
            _outputs = outputList.AsReadOnly();

            var labelList = new List<VisualLabel>();
            var labelsByIndex = new Dictionary<int, string>();
            var labelNames = new HashSet<string>(StringComparer.Ordinal);
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            foreach (VisualLabel label in labels)
            {
                if (label == null) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Labels cannot contain null.", profileId: ProfileId);
                if (labelsByIndex.ContainsKey(label.Index)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Class indices must be unique.", profileId: ProfileId);
                if (!labelNames.Add(label.Label)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Class labels must be unique.", profileId: ProfileId);
                labelsByIndex.Add(label.Index, label.Label);
                labelList.Add(label);
            }
            _labels = labelList.AsReadOnly();
            _labelsByIndex = new Dictionary<int, string>(labelsByIndex);
        }

        /// <summary>Gets the stable profile identifier. / 获取稳定 Profile 标识符。</summary>
        public string ProfileId { get; }
        /// <summary>Gets the logical model identifier. / 获取逻辑模型标识符。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets the visual task. / 获取视觉任务。</summary>
        public VisualTaskId Task { get; }
        /// <summary>Gets the profile version. / 获取 Profile 版本。</summary>
        public string Version { get; }
        /// <summary>Gets the normalized model format. / 获取规范化模型格式。</summary>
        public string ModelFormat { get; }
        /// <summary>Gets the input binding. / 获取输入绑定。</summary>
        public VisualInputBinding Input { get; }
        /// <summary>Gets non-image inputs that the adapter must provide by exact name. / 获取适配器必须按精确名称提供的非图像输入。</summary>
        public IReadOnlyList<VisualAuxiliaryInputBinding> AuxiliaryInputs => _auxiliaryInputs;
        /// <summary>Gets required output bindings. / 获取所需输出绑定。</summary>
        public IReadOnlyList<VisualOutputBinding> Outputs => _outputs;
        /// <summary>Gets class labels. / 获取类别标签。</summary>
        public IReadOnlyList<VisualLabel> Labels => _labels;
        /// <summary>Gets the reusable decoder. / 获取可复用解码器。</summary>
        public IVisualDecoder Decoder { get; }
        /// <summary>Gets required backend capabilities. / 获取所需后端能力。</summary>
        public BackendCapabilities RequiredCapabilities { get; }
        /// <summary>Gets an optional documented minimum backend version. / 获取可选的已记录最低后端版本。</summary>
        public string? MinimumBackendVersion { get; }

        /// <summary>Gets a label by class index, falling back to the invariant index string. / 按类别索引获取标签；缺失时回退到不变索引字符串。</summary>
        public string GetLabel(int classIndex)
        {
            if (classIndex < 0) throw new ArgumentOutOfRangeException(nameof(classIndex));
            return _labelsByIndex.TryGetValue(classIndex, out string? label) ? label : classIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    internal static class TensorShapePattern
    {
        public static bool Matches(TensorShape pattern, TensorShape actual)
        {
            if (pattern.Rank != actual.Rank) return false;
            for (int index = 0; index < pattern.Rank; index++)
            {
                if (pattern[index] >= 0 && pattern[index] != actual[index]) return false;
            }
            return true;
        }
    }
}
