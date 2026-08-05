using System;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies strict instance-mask tensor layouts. / 标识严格的实例掩码张量布局。</summary>
    public enum InstanceMaskTensorLayout
    {
        /// <summary>Direct masks use [1,N,H,W] and prototypes use [1,C,H,W]. / 直接掩码使用 [1,N,H,W]，原型使用 [1,C,H,W]。</summary>
        Nchw = 0,
        /// <summary>Direct masks use [1,N,H,W,1] and prototypes use [1,H,W,C]. / 直接掩码使用 [1,N,H,W,1]，原型使用 [1,H,W,C]。</summary>
        Nhwc = 1
    }

    /// <summary>Identifies the declared numeric meaning of mask values. / 标识掩码数值的声明语义。</summary>
    public enum InstanceMaskValueKind
    {
        /// <summary>Unbounded raw logits. / 无界原始 logits。</summary>
        Logits = 0,
        /// <summary>Probabilities constrained to [0,1]. / 限制在 [0,1] 的概率。</summary>
        Probabilities = 1,
        /// <summary>Binary values that must be exactly zero or one. / 必须精确为零或一的二值。</summary>
        Binary = 2
    }

    /// <summary>Identifies the explicit activation applied before mask thresholding. / 标识掩码阈值化前显式应用的激活。</summary>
    public enum InstanceMaskActivation
    {
        /// <summary>Apply no activation. / 不应用激活。</summary>
        None = 0,
        /// <summary>Apply a numerically stable sigmoid. / 应用数值稳定的 sigmoid。</summary>
        Sigmoid = 1
    }

    /// <summary>Identifies whether binary thresholding occurs before or after spatial restoration. / 标识二值阈值化发生在空间恢复之前还是之后。</summary>
    public enum InstanceMaskThresholdOrder
    {
        /// <summary>Threshold the tensor grid before nearest-neighbor restoration. / 在最近邻空间恢复前对张量网格阈值化。</summary>
        BeforeResize = 0,
        /// <summary>Restore continuous values before applying the configured threshold. / 先恢复连续值，再应用配置阈值。</summary>
        AfterResize = 1
    }

    /// <summary>Identifies explicit tensor-grid to model-input sampling semantics. / 标识显式的张量网格到模型输入采样语义。</summary>
    public enum InstanceMaskInterpolationMode
    {
        /// <summary>Nearest-neighbor sampling using half-open pixel cells. / 使用半开像素单元的最近邻采样。</summary>
        NearestNeighbor = 0,
        /// <summary>Bilinear sampling with half-pixel centers and clamped edges. / 使用半像素中心及边缘钳制的双线性采样。</summary>
        BilinearHalfPixel = 1,
        /// <summary>Bilinear sampling with aligned corner centers and clamped edges. / 使用对齐角点中心及边缘钳制的双线性采样。</summary>
        BilinearAlignCorners = 2
    }

    /// <summary>Identifies the space in which an instance box crops its mask. / 标识实例边界框裁剪掩码时所在的空间。</summary>
    public enum InstanceMaskCropSpace
    {
        /// <summary>Do not crop masks to candidate boxes. / 不按候选框裁剪掩码。</summary>
        None = 0,
        /// <summary>Crop by the candidate half-open box in model-input space. / 按模型输入空间中的候选半开区间边界框裁剪。</summary>
        ModelInput = 1
    }

    /// <summary>Identifies whether box cropping occurs before or after spatial restoration. / 标识边界框裁剪发生在空间恢复之前还是之后。</summary>
    public enum InstanceMaskCropOrder
    {
        /// <summary>Zero tensor-grid samples outside the model-space box before interpolation. / 在插值前将模型空间边界框外的张量网格采样置零。</summary>
        BeforeResize = 0,
        /// <summary>Reject restored source pixels whose model-space center is outside the box. / 拒绝模型空间中心位于边界框外的已恢复源图像素。</summary>
        AfterResize = 1
    }

    /// <summary>Identifies the declared numeric meaning of instance confidence scores. / 标识实例置信分数的声明数值语义。</summary>
    public enum InstanceScoreKind
    {
        /// <summary>Scores are probabilities constrained to [0,1]. / 分数是限制在 [0,1] 的概率。</summary>
        Probability = 0,
        /// <summary>Scores are finite non-negative values with no implicit activation. / 分数是有限非负值且不隐式激活。</summary>
        NonNegative = 1
    }

    /// <summary>Defines strict named candidate box, score, and class outputs shared by instance-mask families. / 定义实例掩码系列共享的严格命名候选框、分数和类别输出。</summary>
    public sealed class InstanceSegmentationCandidateSchema
    {
        /// <summary>Initializes candidate output bindings and box/score semantics. / 初始化候选输出绑定及边界框和分数语义。</summary>
        public InstanceSegmentationCandidateSchema(
            string boxesOutputName,
            string scoresOutputName,
            string classesOutputName,
            DetectionBoxFormat boxFormat = DetectionBoxFormat.Xyxy,
            bool normalizedBoxes = false,
            InstanceScoreKind scoreKind = InstanceScoreKind.Probability)
        {
            BoxesOutputName = RequiredName(boxesOutputName, nameof(boxesOutputName));
            ScoresOutputName = RequiredName(scoresOutputName, nameof(scoresOutputName));
            ClassesOutputName = RequiredName(classesOutputName, nameof(classesOutputName));
            if (string.Equals(BoxesOutputName, ScoresOutputName, StringComparison.Ordinal) || string.Equals(BoxesOutputName, ClassesOutputName, StringComparison.Ordinal) || string.Equals(ScoresOutputName, ClassesOutputName, StringComparison.Ordinal)) throw new ArgumentException("Candidate output names must be unique.");
            if (!Enum.IsDefined(typeof(DetectionBoxFormat), boxFormat)) throw new ArgumentOutOfRangeException(nameof(boxFormat));
            if (!Enum.IsDefined(typeof(InstanceScoreKind), scoreKind)) throw new ArgumentOutOfRangeException(nameof(scoreKind));
            BoxFormat = boxFormat;
            NormalizedBoxes = normalizedBoxes;
            ScoreKind = scoreKind;
        }

        /// <summary>Gets the [1,N,4] box output name. / 获取 [1,N,4] 边界框输出名称。</summary>
        public string BoxesOutputName { get; }
        /// <summary>Gets the [1,N] score output name. / 获取 [1,N] 分数输出名称。</summary>
        public string ScoresOutputName { get; }
        /// <summary>Gets the [1,N] integer-valued class output name. / 获取 [1,N] 整数值类别输出名称。</summary>
        public string ClassesOutputName { get; }
        /// <summary>Gets the four-value box representation. / 获取四值边界框表示。</summary>
        public DetectionBoxFormat BoxFormat { get; }
        /// <summary>Gets whether box coordinates are normalized to model-input dimensions. / 获取边界框坐标是否按模型输入尺寸归一化。</summary>
        public bool NormalizedBoxes { get; }
        /// <summary>Gets score numeric semantics. / 获取分数数值语义。</summary>
        public InstanceScoreKind ScoreKind { get; }

        internal static string RequiredName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("An output tensor name is required.", parameterName);
            return value;
        }
    }

    /// <summary>Defines strict direct per-candidate mask outputs and all spatial semantics. / 定义严格的逐候选直接掩码输出及全部空间语义。</summary>
    public sealed class DirectInstanceSegmentationOutputSchema
    {
        /// <summary>Initializes a direct-mask schema. / 初始化直接掩码 Schema。</summary>
        public DirectInstanceSegmentationOutputSchema(
            InstanceSegmentationCandidateSchema candidates,
            string masksOutputName,
            InstanceMaskTensorLayout layout,
            InstanceMaskValueKind valueKind,
            InstanceMaskActivation activation = InstanceMaskActivation.None,
            InstanceMaskInterpolationMode interpolation = InstanceMaskInterpolationMode.BilinearHalfPixel,
            InstanceMaskThresholdOrder thresholdOrder = InstanceMaskThresholdOrder.AfterResize,
            InstanceMaskCropSpace cropSpace = InstanceMaskCropSpace.ModelInput,
            InstanceMaskCropOrder cropOrder = InstanceMaskCropOrder.AfterResize)
        {
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            MasksOutputName = InstanceSegmentationCandidateSchema.RequiredName(masksOutputName, nameof(masksOutputName));
            EnsureUnique(Candidates, MasksOutputName);
            InstanceMaskSchemaGuard.Validate(layout, valueKind, activation, interpolation, thresholdOrder, cropSpace, cropOrder);
            Layout = layout;
            ValueKind = valueKind;
            Activation = activation;
            Interpolation = interpolation;
            ThresholdOrder = thresholdOrder;
            CropSpace = cropSpace;
            CropOrder = cropOrder;
        }

        /// <summary>Gets shared candidate outputs. / 获取共享候选输出。</summary>
        public InstanceSegmentationCandidateSchema Candidates { get; }
        /// <summary>Gets the direct mask output name. / 获取直接掩码输出名称。</summary>
        public string MasksOutputName { get; }
        /// <summary>Gets the exact direct mask layout. / 获取精确的直接掩码布局。</summary>
        public InstanceMaskTensorLayout Layout { get; }
        /// <summary>Gets mask value semantics. / 获取掩码数值语义。</summary>
        public InstanceMaskValueKind ValueKind { get; }
        /// <summary>Gets the explicit activation. / 获取显式激活。</summary>
        public InstanceMaskActivation Activation { get; }
        /// <summary>Gets spatial interpolation semantics. / 获取空间插值语义。</summary>
        public InstanceMaskInterpolationMode Interpolation { get; }
        /// <summary>Gets threshold order. / 获取阈值化顺序。</summary>
        public InstanceMaskThresholdOrder ThresholdOrder { get; }
        /// <summary>Gets candidate-box crop space. / 获取候选框裁剪空间。</summary>
        public InstanceMaskCropSpace CropSpace { get; }
        /// <summary>Gets candidate-box crop order. / 获取候选框裁剪顺序。</summary>
        public InstanceMaskCropOrder CropOrder { get; }

        private static void EnsureUnique(InstanceSegmentationCandidateSchema candidates, string masks)
        {
            if (string.Equals(masks, candidates.BoxesOutputName, StringComparison.Ordinal) || string.Equals(masks, candidates.ScoresOutputName, StringComparison.Ordinal) || string.Equals(masks, candidates.ClassesOutputName, StringComparison.Ordinal)) throw new ArgumentException("All output tensor names must be unique.", nameof(masks));
        }
    }

    /// <summary>Defines strict prototype/coefficient mask reconstruction and spatial semantics. / 定义严格的原型/系数掩码重建及空间语义。</summary>
    public sealed class PrototypeInstanceSegmentationOutputSchema
    {
        /// <summary>Initializes a prototype/coefficient schema whose linear combination is sum(coeff[c] * prototype[c,y,x]). / 初始化原型/系数 Schema，其线性组合为 sum(coeff[c] * prototype[c,y,x])。</summary>
        public PrototypeInstanceSegmentationOutputSchema(
            InstanceSegmentationCandidateSchema candidates,
            string prototypesOutputName,
            string coefficientsOutputName,
            InstanceMaskTensorLayout prototypeLayout,
            InstanceMaskValueKind combinationValueKind = InstanceMaskValueKind.Logits,
            InstanceMaskActivation activation = InstanceMaskActivation.Sigmoid,
            InstanceMaskInterpolationMode interpolation = InstanceMaskInterpolationMode.BilinearHalfPixel,
            InstanceMaskThresholdOrder thresholdOrder = InstanceMaskThresholdOrder.AfterResize,
            InstanceMaskCropSpace cropSpace = InstanceMaskCropSpace.ModelInput,
            InstanceMaskCropOrder cropOrder = InstanceMaskCropOrder.BeforeResize)
        {
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            PrototypesOutputName = InstanceSegmentationCandidateSchema.RequiredName(prototypesOutputName, nameof(prototypesOutputName));
            CoefficientsOutputName = InstanceSegmentationCandidateSchema.RequiredName(coefficientsOutputName, nameof(coefficientsOutputName));
            string[] names = { Candidates.BoxesOutputName, Candidates.ScoresOutputName, Candidates.ClassesOutputName, PrototypesOutputName, CoefficientsOutputName };
            for (int first = 0; first < names.Length; first++) for (int second = first + 1; second < names.Length; second++) if (string.Equals(names[first], names[second], StringComparison.Ordinal)) throw new ArgumentException("All output tensor names must be unique.");
            if (combinationValueKind == InstanceMaskValueKind.Binary) throw new ArgumentException("A linear prototype combination cannot declare binary values.", nameof(combinationValueKind));
            InstanceMaskSchemaGuard.Validate(prototypeLayout, combinationValueKind, activation, interpolation, thresholdOrder, cropSpace, cropOrder);
            PrototypeLayout = prototypeLayout;
            CombinationValueKind = combinationValueKind;
            Activation = activation;
            Interpolation = interpolation;
            ThresholdOrder = thresholdOrder;
            CropSpace = cropSpace;
            CropOrder = cropOrder;
        }

        /// <summary>Gets shared candidate outputs. / 获取共享候选输出。</summary>
        public InstanceSegmentationCandidateSchema Candidates { get; }
        /// <summary>Gets the [1,C,H,W] or [1,H,W,C] prototype output name. / 获取 [1,C,H,W] 或 [1,H,W,C] 原型输出名称。</summary>
        public string PrototypesOutputName { get; }
        /// <summary>Gets the [1,N,C] coefficient output name. / 获取 [1,N,C] 系数输出名称。</summary>
        public string CoefficientsOutputName { get; }
        /// <summary>Gets the exact prototype layout. / 获取精确的原型布局。</summary>
        public InstanceMaskTensorLayout PrototypeLayout { get; }
        /// <summary>Gets linear-combination value semantics before activation. / 获取激活前线性组合的数值语义。</summary>
        public InstanceMaskValueKind CombinationValueKind { get; }
        /// <summary>Gets the explicit post-combination activation. / 获取显式的组合后激活。</summary>
        public InstanceMaskActivation Activation { get; }
        /// <summary>Gets spatial interpolation semantics. / 获取空间插值语义。</summary>
        public InstanceMaskInterpolationMode Interpolation { get; }
        /// <summary>Gets threshold order. / 获取阈值化顺序。</summary>
        public InstanceMaskThresholdOrder ThresholdOrder { get; }
        /// <summary>Gets candidate-box crop space. / 获取候选框裁剪空间。</summary>
        public InstanceMaskCropSpace CropSpace { get; }
        /// <summary>Gets candidate-box crop order. / 获取候选框裁剪顺序。</summary>
        public InstanceMaskCropOrder CropOrder { get; }
    }

    /// <summary>Controls deterministic NMS, mask thresholding, overlap output, and all work bounds. / 控制确定性 NMS、掩码阈值、重叠输出及全部工作边界。</summary>
    public sealed class InstanceSegmentationDecoderOptions
    {
        /// <summary>Initializes bounded instance segmentation decoder options. / 初始化有界实例分割解码选项。</summary>
        public InstanceSegmentationDecoderOptions(
            float scoreThreshold = 0.25f,
            float maskThreshold = 0.5f,
            float iouThreshold = 0.45f,
            DetectionNmsMode nmsMode = DetectionNmsMode.ClassAware,
            InstanceMaskOverlapMode overlapMode = InstanceMaskOverlapMode.Independent,
            bool generateRle = true,
            int maximumCandidates = 3000,
            int maximumInstances = 100,
            int maximumPrototypeChannels = 256,
            long maximumMaskPixels = 64L * 1024 * 1024,
            long maximumResultBytes = 256L * 1024 * 1024,
            int maximumRleRuns = 16 * 1024 * 1024,
            long maximumWorkspaceBytes = 256L * 1024 * 1024)
        {
            if (float.IsNaN(scoreThreshold) || float.IsInfinity(scoreThreshold) || scoreThreshold < 0) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (float.IsNaN(maskThreshold) || float.IsInfinity(maskThreshold)) throw new ArgumentOutOfRangeException(nameof(maskThreshold));
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (!Enum.IsDefined(typeof(DetectionNmsMode), nmsMode)) throw new ArgumentOutOfRangeException(nameof(nmsMode));
            if (!Enum.IsDefined(typeof(InstanceMaskOverlapMode), overlapMode)) throw new ArgumentOutOfRangeException(nameof(overlapMode));
            if (maximumCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCandidates));
            if (maximumInstances <= 0) throw new ArgumentOutOfRangeException(nameof(maximumInstances));
            if (maximumPrototypeChannels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPrototypeChannels));
            if (maximumMaskPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumMaskPixels));
            if (maximumResultBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumResultBytes));
            if (maximumRleRuns <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRleRuns));
            if (maximumWorkspaceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWorkspaceBytes));
            ScoreThreshold = scoreThreshold;
            MaskThreshold = maskThreshold;
            IouThreshold = iouThreshold;
            NmsMode = nmsMode;
            OverlapMode = overlapMode;
            GenerateRle = generateRle;
            MaximumCandidates = maximumCandidates;
            MaximumInstances = maximumInstances;
            MaximumPrototypeChannels = maximumPrototypeChannels;
            MaximumMaskPixels = maximumMaskPixels;
            MaximumResultBytes = maximumResultBytes;
            MaximumRleRuns = maximumRleRuns;
            MaximumWorkspaceBytes = maximumWorkspaceBytes;
        }

        /// <summary>Gets the inclusive instance score threshold. / 获取包含边界的实例分数阈值。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets the inclusive binary mask threshold. / 获取包含边界的二值掩码阈值。</summary>
        public float MaskThreshold { get; }
        /// <summary>Gets the IoU NMS threshold. / 获取 IoU NMS 阈值。</summary>
        public float IouThreshold { get; }
        /// <summary>Gets class-aware or class-agnostic NMS mode. / 获取分类别或忽略类别的 NMS 模式。</summary>
        public DetectionNmsMode NmsMode { get; }
        /// <summary>Gets overlap output mode. / 获取重叠输出模式。</summary>
        public InstanceMaskOverlapMode OverlapMode { get; }
        /// <summary>Gets whether DeploySharp row-major foreground-run RLE is generated. / 获取是否生成 DeploySharp 行优先前景游程 RLE。</summary>
        public bool GenerateRle { get; }
        /// <summary>Gets the maximum accepted candidate count. / 获取可接受的最大候选数量。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets the maximum retained instance count. / 获取最大保留实例数量。</summary>
        public int MaximumInstances { get; }
        /// <summary>Gets the maximum prototype channel count. / 获取最大原型通道数。</summary>
        public int MaximumPrototypeChannels { get; }
        /// <summary>Gets the maximum candidate tensor-mask positions. / 获取候选张量掩码位置最大数量。</summary>
        public long MaximumMaskPixels { get; }
        /// <summary>Gets the maximum estimated retained result bytes. / 获取估算保留结果的最大字节数。</summary>
        public long MaximumResultBytes { get; }
        /// <summary>Gets the maximum RLE run count per instance. / 获取每个实例最大 RLE 游程数量。</summary>
        public int MaximumRleRuns { get; }
        /// <summary>Gets the maximum decoder workspace bytes. / 获取解码器最大工作区字节数。</summary>
        public long MaximumWorkspaceBytes { get; }
    }

    internal static class InstanceMaskSchemaGuard
    {
        public static void Validate(InstanceMaskTensorLayout layout, InstanceMaskValueKind valueKind, InstanceMaskActivation activation, InstanceMaskInterpolationMode interpolation, InstanceMaskThresholdOrder thresholdOrder, InstanceMaskCropSpace cropSpace, InstanceMaskCropOrder cropOrder)
        {
            if (!Enum.IsDefined(typeof(InstanceMaskTensorLayout), layout)) throw new ArgumentOutOfRangeException(nameof(layout));
            if (!Enum.IsDefined(typeof(InstanceMaskValueKind), valueKind)) throw new ArgumentOutOfRangeException(nameof(valueKind));
            if (!Enum.IsDefined(typeof(InstanceMaskActivation), activation)) throw new ArgumentOutOfRangeException(nameof(activation));
            if (!Enum.IsDefined(typeof(InstanceMaskInterpolationMode), interpolation)) throw new ArgumentOutOfRangeException(nameof(interpolation));
            if (!Enum.IsDefined(typeof(InstanceMaskThresholdOrder), thresholdOrder)) throw new ArgumentOutOfRangeException(nameof(thresholdOrder));
            if (!Enum.IsDefined(typeof(InstanceMaskCropSpace), cropSpace)) throw new ArgumentOutOfRangeException(nameof(cropSpace));
            if (!Enum.IsDefined(typeof(InstanceMaskCropOrder), cropOrder)) throw new ArgumentOutOfRangeException(nameof(cropOrder));
            if (valueKind != InstanceMaskValueKind.Logits && activation != InstanceMaskActivation.None) throw new ArgumentException("Only declared logits may apply sigmoid activation.", nameof(activation));
            if (valueKind == InstanceMaskValueKind.Binary && (interpolation != InstanceMaskInterpolationMode.NearestNeighbor || thresholdOrder != InstanceMaskThresholdOrder.BeforeResize)) throw new ArgumentException("Binary tensor values require nearest-neighbor restoration and before-resize threshold semantics.");
            if (thresholdOrder == InstanceMaskThresholdOrder.BeforeResize && interpolation != InstanceMaskInterpolationMode.NearestNeighbor) throw new ArgumentException("Before-resize thresholding requires nearest-neighbor restoration.", nameof(interpolation));
        }
    }
}
