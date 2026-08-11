using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies an upstream Segment Anything generation. / 标识上游 Segment Anything 代际。</summary>
    public enum PromptableSegmentationFamily
    {
        /// <summary>Original Segment Anything model. / 原始 Segment Anything 模型。</summary>
        Sam = 1,
        /// <summary>Segment Anything 2. / 第二代 Segment Anything。</summary>
        Sam2 = 2,
        /// <summary>Segment Anything 3. / 第三代 Segment Anything。</summary>
        Sam3 = 3
    }

    /// <summary>Identifies one independently loaded artifact in a promptable model bundle. / 标识可提示模型 Bundle 中独立加载的一个工件。</summary>
    public enum PromptableSegmentationArtifactRole
    {
        /// <summary>Image or vision encoder. / 图像或视觉 Encoder。</summary>
        ImageEncoder = 1,
        /// <summary>Combined prompt encoder and mask decoder. / 合并的 Prompt Encoder 与 Mask Decoder。</summary>
        PromptMaskDecoder = 2,
        /// <summary>Text encoder. / 文本 Encoder。</summary>
        TextEncoder = 3,
        /// <summary>Geometry prompt encoder. / 几何提示 Encoder。</summary>
        GeometryEncoder = 4,
        /// <summary>Video memory encoder. / 视频 Memory Encoder。</summary>
        MemoryEncoder = 5,
        /// <summary>Video memory bank or attention component. / 视频 Memory Bank 或 Attention 组件。</summary>
        MemoryAttention = 6,
        /// <summary>Stateful video predictor component. / 有状态视频 Predictor 组件。</summary>
        VideoPredictor = 7
    }

    /// <summary>Describes the execution graph DeploySharp can run without replacing upstream algorithms. / 描述 DeploySharp 可在不替代上游算法的前提下执行的图。</summary>
    public enum PromptableSegmentationExecutionKind
    {
        /// <summary>The artifact bundle is documented but has no supported complete native pipeline. / 已记录工件 Bundle，但没有受支持的完整 native Pipeline。</summary>
        ExternalContractOnly = 0,
        /// <summary>Official SAM v1 image encoder plus official prompt/mask decoder contract. / 官方 SAM v1 图像 Encoder 加官方 Prompt/Mask Decoder 合同。</summary>
        SamV1ImageOnnx = 1
    }

    /// <summary>Lists prompt and state capabilities bound to an artifact bundle. / 列出绑定到工件 Bundle 的提示与状态能力。</summary>
    [Flags]
    public enum PromptableSegmentationCapabilities
    {
        /// <summary>No executable prompt capability. / 没有可执行提示能力。</summary>
        None = 0,
        /// <summary>Foreground/background point prompts. / 前景/背景点提示。</summary>
        Points = 1,
        /// <summary>Box prompts. / 框提示。</summary>
        Boxes = 2,
        /// <summary>Low-resolution mask-logit feedback. / 低分辨率 Mask Logit 反馈。</summary>
        MaskFeedback = 4,
        /// <summary>Multiple candidate masks with quality scores. / 带质量分数的多个候选掩码。</summary>
        Multimask = 8,
        /// <summary>Text or concept prompts. / 文本或概念提示。</summary>
        Text = 16,
        /// <summary>Stateful video propagation. / 有状态视频传播。</summary>
        VideoPropagation = 32
    }

    /// <summary>Identifies point meaning in the official prompt-encoder label contract. / 标识官方 Prompt Encoder 标签合同中的点含义。</summary>
    public enum PromptPointLabel
    {
        /// <summary>Background or negative point, encoded as zero. / 背景或负点，编码为零。</summary>
        Background = 0,
        /// <summary>Foreground or positive point, encoded as one. / 前景或正点，编码为一。</summary>
        Foreground = 1
    }

    /// <summary>Identifies the semantic meaning of one mask quality output. / 标识一个掩码质量输出的语义。</summary>
    public enum PromptableMaskQualityKind
    {
        /// <summary>Predicted mask intersection-over-union. / 预测的掩码交并比。</summary>
        PredictedIoU = 1,
        /// <summary>Exporter-defined quality score. / Exporter 定义的质量分数。</summary>
        ExporterQuality = 2
    }

    /// <summary>Describes one exact named tensor port in an artifact. / 描述工件中的一个精确具名张量端口。</summary>
    public sealed class PromptableTensorContract
    {
        /// <summary>Initializes an immutable tensor-port contract; minus one denotes a dynamic dimension. / 初始化不可变张量端口合同；负一表示动态维度。</summary>
        public PromptableTensorContract(string name, TensorElementType elementType, TensorShape shapePattern)
        {
            if (string.IsNullOrWhiteSpace(name)) throw Invalid("A tensor name is required.", name);
            if (elementType == TensorElementType.Unknown || elementType == TensorElementType.String) throw Invalid("The tensor element type is unsupported.", name);
            Name = name.Trim();
            ElementType = elementType;
            ShapePattern = shapePattern == null ? throw new ArgumentNullException(nameof(shapePattern)) : new TensorShape(shapePattern.ToArray());
        }

        /// <summary>Gets the exact case-sensitive port name. / 获取区分大小写的精确端口名。</summary>
        public string Name { get; }
        /// <summary>Gets the tensor element type. / 获取张量元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the static/dynamic shape pattern. / 获取静态/动态 Shape 模式。</summary>
        public TensorShape ShapePattern { get; }

        private static VisualException Invalid(string message, string? tensorName) => new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, message, tensorName: tensorName);
    }

    /// <summary>Binds an independently loaded subgraph to provenance, hash, exact ports, format, and capacity. / 将独立加载的子图绑定到来源、Hash、精确端口、格式与容量。</summary>
    public sealed class PromptableSegmentationArtifactContract
    {
        private readonly IReadOnlyList<PromptableTensorContract> _inputs;
        private readonly IReadOnlyList<PromptableTensorContract> _outputs;

        /// <summary>Initializes an artifact-bound subgraph contract. / 初始化绑定工件的子图合同。</summary>
        public PromptableSegmentationArtifactContract(
            PromptableSegmentationArtifactRole role,
            ModelId modelId,
            string format,
            string artifactSha256,
            int opset,
            IEnumerable<PromptableTensorContract> inputs,
            IEnumerable<PromptableTensorContract> outputs,
            string upstreamRepository,
            string upstreamCommit,
            string exporter,
            string license,
            long maximumTensorElements = 268435456)
        {
            if (!Enum.IsDefined(typeof(PromptableSegmentationArtifactRole), role)) throw Invalid("The artifact role is invalid.");
            if (modelId.IsEmpty) throw Invalid("A model identifier is required.");
            if (string.IsNullOrWhiteSpace(format)) throw Invalid("An artifact format is required.");
            ArtifactSha256 = NormalizeSha256(artifactSha256, nameof(artifactSha256));
            if (opset <= 0) throw Invalid("A positive ONNX opset is required.");
            if (maximumTensorElements <= 0) throw Invalid("The tensor element capacity must be positive.");
            Role = role;
            ModelId = modelId;
            Format = format.Trim().ToLowerInvariant();
            Opset = opset;
            _inputs = CopyPorts(inputs, nameof(inputs));
            _outputs = CopyPorts(outputs, nameof(outputs));
            if (_inputs.Count == 0 || _outputs.Count == 0) throw Invalid("Each executable artifact requires at least one input and output.");
            UpstreamRepository = Required(upstreamRepository, nameof(upstreamRepository));
            UpstreamCommit = Required(upstreamCommit, nameof(upstreamCommit));
            Exporter = Required(exporter, nameof(exporter));
            License = Required(license, nameof(license));
            MaximumTensorElements = maximumTensorElements;
        }

        /// <summary>Gets the subgraph role. / 获取子图角色。</summary>
        public PromptableSegmentationArtifactRole Role { get; }
        /// <summary>Gets the logical model identifier. / 获取逻辑模型标识符。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets the model format. / 获取模型格式。</summary>
        public string Format { get; }
        /// <summary>Gets the exact lowercase artifact SHA256. / 获取精确的小写工件 SHA256。</summary>
        public string ArtifactSha256 { get; }
        /// <summary>Gets the ONNX opset or source graph opset recorded by the exporter. / 获取 Exporter 记录的 ONNX Opset 或源图 Opset。</summary>
        public int Opset { get; }
        /// <summary>Gets exact ordered inputs. / 获取精确的有序输入。</summary>
        public IReadOnlyList<PromptableTensorContract> Inputs => _inputs;
        /// <summary>Gets exact ordered outputs. / 获取精确的有序输出。</summary>
        public IReadOnlyList<PromptableTensorContract> Outputs => _outputs;
        /// <summary>Gets the upstream repository. / 获取上游仓库。</summary>
        public string UpstreamRepository { get; }
        /// <summary>Gets the pinned upstream commit or release. / 获取固定的上游 Commit 或 Release。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets the exporter and dependency-lock identity. / 获取 Exporter 与依赖锁 Identity。</summary>
        public string Exporter { get; }
        /// <summary>Gets license evidence. / 获取许可证证据。</summary>
        public string License { get; }
        /// <summary>Gets the per-tensor element capacity. / 获取单张量元素容量。</summary>
        public long MaximumTensorElements { get; }

        /// <summary>Creates a Core artifact whose ID, format, and hash are fixed by this contract. / 创建 ID、格式和 Hash 均由本合同固定的 Core 工件。</summary>
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null)
        {
            return new ModelArtifact(ModelId, Format, path, ArtifactSha256, preferredBackend);
        }

        internal PromptableTensorContract RequireInput(string name) => Require(_inputs, name, "input");
        internal PromptableTensorContract RequireOutput(string name) => Require(_outputs, name, "output");

        internal static string NormalizeSha256(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) throw Invalid("An exact 64-character SHA256 is required.", parameterName);
            string normalized = value.Trim().ToLowerInvariant();
            for (int index = 0; index < normalized.Length; index++)
            {
                char valueAt = normalized[index];
                if (!((valueAt >= '0' && valueAt <= '9') || (valueAt >= 'a' && valueAt <= 'f'))) throw Invalid("SHA256 must contain lowercase or uppercase hexadecimal characters.", parameterName);
            }
            return normalized;
        }

        private static PromptableTensorContract Require(IReadOnlyList<PromptableTensorContract> ports, string name, string kind)
        {
            PromptableTensorContract? match = ports.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.Ordinal));
            if (match == null) throw Invalid("The declared " + kind + " port is absent: " + name + ".");
            return match;
        }

        private static IReadOnlyList<PromptableTensorContract> CopyPorts(IEnumerable<PromptableTensorContract> ports, string name)
        {
            if (ports == null) throw new ArgumentNullException(name);
            var result = new List<PromptableTensorContract>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (PromptableTensorContract port in ports)
            {
                if (port == null || !names.Add(port.Name)) throw Invalid("Tensor ports must be non-null and uniquely named.");
                result.Add(port);
            }
            return new ReadOnlyCollection<PromptableTensorContract>(result);
        }

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw Invalid("Artifact provenance is incomplete: " + name + ".");
            return value.Trim();
        }

        private static VisualException Invalid(string message, string? details = null) => new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, message, technicalDetails: details);
    }

    /// <summary>Maps semantic SAM v1 fields to exact case-sensitive artifact ports. / 将语义 SAM v1 字段映射到区分大小写的精确工件端口。</summary>
    public sealed class SamV1TensorMap
    {
        /// <summary>Initializes the complete image encoder and prompt/mask decoder port map. / 初始化完整图像 Encoder 与 Prompt/Mask Decoder 端口映射。</summary>
        public SamV1TensorMap(string imageInput, string imageEmbedding, string pointCoordinates, string pointLabels, string maskInput, string hasMaskInput, string originalImageSize, string masks, string quality, string lowResolutionMasks)
        {
            ImageInput = Required(imageInput, nameof(imageInput));
            ImageEmbedding = Required(imageEmbedding, nameof(imageEmbedding));
            PointCoordinates = Required(pointCoordinates, nameof(pointCoordinates));
            PointLabels = Required(pointLabels, nameof(pointLabels));
            MaskInput = Required(maskInput, nameof(maskInput));
            HasMaskInput = Required(hasMaskInput, nameof(hasMaskInput));
            OriginalImageSize = Required(originalImageSize, nameof(originalImageSize));
            Masks = Required(masks, nameof(masks));
            Quality = Required(quality, nameof(quality));
            LowResolutionMasks = Required(lowResolutionMasks, nameof(lowResolutionMasks));
        }

        /// <summary>Gets the image input name. / 获取图像输入名。</summary>
        public string ImageInput { get; }
        /// <summary>Gets the encoder output and decoder embedding input name. / 获取 Encoder 输出与 Decoder Embedding 输入名。</summary>
        public string ImageEmbedding { get; }
        /// <summary>Gets the point-coordinate input name. / 获取点坐标输入名。</summary>
        public string PointCoordinates { get; }
        /// <summary>Gets the point-label input name. / 获取点标签输入名。</summary>
        public string PointLabels { get; }
        /// <summary>Gets the low-resolution mask-feedback input name. / 获取低分辨率掩码反馈输入名。</summary>
        public string MaskInput { get; }
        /// <summary>Gets the mask-feedback presence input name. / 获取掩码反馈存在标志输入名。</summary>
        public string HasMaskInput { get; }
        /// <summary>Gets the original image size input name. / 获取原图尺寸输入名。</summary>
        public string OriginalImageSize { get; }
        /// <summary>Gets the source-space mask-logit output name. / 获取源图空间 Mask Logit 输出名。</summary>
        public string Masks { get; }
        /// <summary>Gets the predicted-IoU output name. / 获取预测 IoU 输出名。</summary>
        public string Quality { get; }
        /// <summary>Gets the low-resolution mask-logit output name. / 获取低分辨率 Mask Logit 输出名。</summary>
        public string LowResolutionMasks { get; }

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "Every SAM tensor name is required.", tensorName: name);
            return value.Trim();
        }
    }

    /// <summary>Documents an official video-state contract or its reproducible native-export blocker. / 记录官方视频状态合同或其可复现 native 导出阻断。</summary>
    public sealed class PromptableVideoStateContract
    {
        /// <summary>Initializes a video contract; a non-executable contract must include a blocker. / 初始化视频合同；不可执行合同必须包含 blocker。</summary>
        public PromptableVideoStateContract(bool executable, string frameOrder, string stateMutation, string cancellationConsistency, int maximumObjects, int maximumFrames, string? blocker)
        {
            if (string.IsNullOrWhiteSpace(frameOrder) || string.IsNullOrWhiteSpace(stateMutation) || string.IsNullOrWhiteSpace(cancellationConsistency)) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "Video state semantics must be explicit.");
            if (maximumObjects <= 0 || maximumFrames <= 0) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "Video capacities must be positive.");
            if (!executable && string.IsNullOrWhiteSpace(blocker)) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "A non-executable video contract requires a reproducible blocker.");
            Executable = executable;
            FrameOrder = frameOrder.Trim();
            StateMutation = stateMutation.Trim();
            CancellationConsistency = cancellationConsistency.Trim();
            MaximumObjects = maximumObjects;
            MaximumFrames = maximumFrames;
            Blocker = string.IsNullOrWhiteSpace(blocker) ? null : blocker!.Trim();
        }

        /// <summary>Gets whether the complete official state path is executable. / 获取完整官方状态路径是否可执行。</summary>
        public bool Executable { get; }
        /// <summary>Gets required frame ordering. / 获取所需帧顺序。</summary>
        public string FrameOrder { get; }
        /// <summary>Gets state-mutation semantics. / 获取状态变更语义。</summary>
        public string StateMutation { get; }
        /// <summary>Gets cancellation consistency. / 获取取消一致性。</summary>
        public string CancellationConsistency { get; }
        /// <summary>Gets the object capacity. / 获取对象容量。</summary>
        public int MaximumObjects { get; }
        /// <summary>Gets the frame capacity. / 获取帧容量。</summary>
        public int MaximumFrames { get; }
        /// <summary>Gets the official-export blocker, when present. / 获取官方导出 blocker（如果有）。</summary>
        public string? Blocker { get; }
    }

    /// <summary>Defines an immutable, artifact-bound promptable-segmentation model-family contract. / 定义不可变且绑定工件的可提示分割模型族合同。</summary>
    public sealed class PromptableSegmentationProfile
    {
        private readonly IReadOnlyList<PromptableSegmentationArtifactContract> _artifacts;

        /// <summary>Initializes a complete image/video family contract with explicit execution support and capacities. / 使用显式执行支持与容量初始化完整图像/视频模型族合同。</summary>
        public PromptableSegmentationProfile(
            string profileId,
            PromptableSegmentationFamily family,
            string version,
            PromptableSegmentationExecutionKind executionKind,
            PromptableSegmentationCapabilities capabilities,
            IEnumerable<PromptableSegmentationArtifactContract> artifacts,
            VisualSize imageInputSize,
            SamV1TensorMap? samV1TensorMap,
            float maskThreshold = 0f,
            PromptableMaskQualityKind qualityKind = PromptableMaskQualityKind.PredictedIoU,
            int maximumPromptPoints = 64,
            int maximumCandidates = 3,
            long maximumSourceMaskPixels = 67108864,
            int lowResolutionMaskSize = 256,
            string preprocessingVersion = "sam-longest-side-pad-bottom-right-v1",
            string postprocessingVersion = "sam-mask-threshold-source-v1",
            PromptableVideoStateContract? video = null)
        {
            ProfileId = VisualGuard.Identifier(profileId, nameof(profileId));
            if (!Enum.IsDefined(typeof(PromptableSegmentationFamily), family)) throw Invalid("The model family is invalid.");
            if (!Enum.IsDefined(typeof(PromptableSegmentationExecutionKind), executionKind)) throw Invalid("The execution kind is invalid.");
            if (!Enum.IsDefined(typeof(PromptableMaskQualityKind), qualityKind)) throw Invalid("The quality kind is invalid.");
            if (string.IsNullOrWhiteSpace(version)) throw Invalid("A model-family version is required.");
            if (maximumPromptPoints <= 0 || maximumCandidates <= 0 || maximumSourceMaskPixels <= 0 || lowResolutionMaskSize <= 0) throw Invalid("Promptable-segmentation capacities must be positive.");
            _artifacts = CopyArtifacts(artifacts);
            if (_artifacts.Count == 0 && executionKind != PromptableSegmentationExecutionKind.ExternalContractOnly) throw Invalid("An executable profile requires at least one artifact contract.");
            if (executionKind == PromptableSegmentationExecutionKind.SamV1ImageOnnx)
            {
                if (family != PromptableSegmentationFamily.Sam || samV1TensorMap == null) throw Invalid("SAM v1 image execution requires a SAM family and exact tensor map.");
                ValidateSamV1(_artifacts, samV1TensorMap);
                PromptableSegmentationCapabilities required = PromptableSegmentationCapabilities.Points | PromptableSegmentationCapabilities.Boxes | PromptableSegmentationCapabilities.MaskFeedback;
                if ((capabilities & required) != required) throw Invalid("SAM v1 image execution requires point, box, and mask-feedback capabilities.");
            }
            if ((capabilities & PromptableSegmentationCapabilities.VideoPropagation) != 0 && video == null) throw Invalid("Video capability requires a video state contract.");
            if (video != null && video.Executable && (capabilities & PromptableSegmentationCapabilities.VideoPropagation) == 0) throw Invalid("An executable video contract requires video capability.");

            Family = family;
            Version = version.Trim();
            ExecutionKind = executionKind;
            Capabilities = capabilities;
            ImageInputSize = imageInputSize;
            SamV1TensorMap = samV1TensorMap;
            MaskThreshold = Finite(maskThreshold, nameof(maskThreshold));
            QualityKind = qualityKind;
            MaximumPromptPoints = maximumPromptPoints;
            MaximumCandidates = maximumCandidates;
            MaximumSourceMaskPixels = maximumSourceMaskPixels;
            LowResolutionMaskSize = lowResolutionMaskSize;
            PreprocessingVersion = Required(preprocessingVersion, nameof(preprocessingVersion));
            PostprocessingVersion = Required(postprocessingVersion, nameof(postprocessingVersion));
            Video = video;
            ArtifactIdentity = _artifacts.Count == 0 ? "external-contract-only" : string.Join(";", _artifacts.OrderBy(value => (int)value.Role).Select(value => value.Role + "=" + value.ArtifactSha256).ToArray());
        }

        /// <summary>Gets the stable profile identifier. / 获取稳定 Profile 标识符。</summary>
        public string ProfileId { get; }
        /// <summary>Gets the upstream family. / 获取上游模型族。</summary>
        public PromptableSegmentationFamily Family { get; }
        /// <summary>Gets the exact model-family version. / 获取精确模型族版本。</summary>
        public string Version { get; }
        /// <summary>Gets the supported execution graph. / 获取受支持的执行图。</summary>
        public PromptableSegmentationExecutionKind ExecutionKind { get; }
        /// <summary>Gets prompt and state capabilities. / 获取提示与状态能力。</summary>
        public PromptableSegmentationCapabilities Capabilities { get; }
        /// <summary>Gets every required sub-artifact. / 获取全部必需子工件。</summary>
        public IReadOnlyList<PromptableSegmentationArtifactContract> Artifacts => _artifacts;
        /// <summary>Gets the fixed encoder canvas. / 获取固定 Encoder 画布。</summary>
        public VisualSize ImageInputSize { get; }
        /// <summary>Gets the SAM v1 semantic tensor map when that execution kind is supported. / 获取支持 SAM v1 执行时的语义张量映射。</summary>
        public SamV1TensorMap? SamV1TensorMap { get; }
        /// <summary>Gets the strict logit threshold; equality is background. / 获取严格 Logit 阈值；相等时为背景。</summary>
        public float MaskThreshold { get; }
        /// <summary>Gets quality-score semantics. / 获取质量分数语义。</summary>
        public PromptableMaskQualityKind QualityKind { get; }
        /// <summary>Gets the point/box-corner capacity. / 获取点/框角点容量。</summary>
        public int MaximumPromptPoints { get; }
        /// <summary>Gets the candidate-mask capacity. / 获取候选掩码容量。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets the total source-mask pixel capacity per decode. / 获取单次解码的源图掩码像素总容量。</summary>
        public long MaximumSourceMaskPixels { get; }
        /// <summary>Gets the square mask-feedback size. / 获取方形 Mask Feedback 尺寸。</summary>
        public int LowResolutionMaskSize { get; }
        /// <summary>Gets preprocessing contract version. / 获取前处理合同版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets postprocessing contract version. / 获取后处理合同版本。</summary>
        public string PostprocessingVersion { get; }
        /// <summary>Gets optional video state semantics. / 获取可选视频状态语义。</summary>
        public PromptableVideoStateContract? Video { get; }
        /// <summary>Gets a stable ordered identity over all artifact roles and hashes. / 获取覆盖全部工件角色与 Hash 的稳定有序 identity。</summary>
        public string ArtifactIdentity { get; }

        /// <summary>Gets one required artifact contract by role. / 按角色获取一个必需工件合同。</summary>
        public PromptableSegmentationArtifactContract GetArtifact(PromptableSegmentationArtifactRole role)
        {
            PromptableSegmentationArtifactContract? artifact = _artifacts.FirstOrDefault(value => value.Role == role);
            if (artifact == null) throw Invalid("The artifact role is absent: " + role + ".");
            return artifact;
        }

        private static IReadOnlyList<PromptableSegmentationArtifactContract> CopyArtifacts(IEnumerable<PromptableSegmentationArtifactContract> artifacts)
        {
            if (artifacts == null) throw new ArgumentNullException(nameof(artifacts));
            var result = new List<PromptableSegmentationArtifactContract>();
            var roles = new HashSet<PromptableSegmentationArtifactRole>();
            foreach (PromptableSegmentationArtifactContract artifact in artifacts)
            {
                if (artifact == null || !roles.Add(artifact.Role)) throw Invalid("Artifact roles must be non-null and unique.");
                result.Add(artifact);
            }
            return new ReadOnlyCollection<PromptableSegmentationArtifactContract>(result);
        }

        private static void ValidateSamV1(IReadOnlyList<PromptableSegmentationArtifactContract> artifacts, SamV1TensorMap map)
        {
            PromptableSegmentationArtifactContract? encoder = artifacts.FirstOrDefault(value => value.Role == PromptableSegmentationArtifactRole.ImageEncoder);
            PromptableSegmentationArtifactContract? decoder = artifacts.FirstOrDefault(value => value.Role == PromptableSegmentationArtifactRole.PromptMaskDecoder);
            if (encoder == null || decoder == null) throw Invalid("SAM v1 execution requires image encoder and prompt/mask decoder artifacts.");
            encoder.RequireInput(map.ImageInput);
            encoder.RequireOutput(map.ImageEmbedding);
            decoder.RequireInput(map.ImageEmbedding);
            decoder.RequireInput(map.PointCoordinates);
            decoder.RequireInput(map.PointLabels);
            decoder.RequireInput(map.MaskInput);
            decoder.RequireInput(map.HasMaskInput);
            decoder.RequireInput(map.OriginalImageSize);
            decoder.RequireOutput(map.Masks);
            decoder.RequireOutput(map.Quality);
            decoder.RequireOutput(map.LowResolutionMasks);
        }

        private static float Finite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw Invalid("A numeric profile value must be finite: " + name + ".");
            return value;
        }

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw Invalid("A versioned contract value is required: " + name + ".");
            return value.Trim();
        }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, message);
    }

    /// <summary>Associates one artifact role with a concrete Core artifact path. / 将一个工件角色关联到具体 Core 工件路径。</summary>
    public sealed class PromptableSegmentationArtifact
    {
        /// <summary>Initializes a role/path association. / 初始化角色/路径关联。</summary>
        public PromptableSegmentationArtifact(PromptableSegmentationArtifactRole role, ModelArtifact artifact)
        {
            if (!Enum.IsDefined(typeof(PromptableSegmentationArtifactRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            Role = role;
            Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
        }

        /// <summary>Gets the artifact role. / 获取工件角色。</summary>
        public PromptableSegmentationArtifactRole Role { get; }
        /// <summary>Gets the concrete Core artifact. / 获取具体 Core 工件。</summary>
        public ModelArtifact Artifact { get; }
    }

    /// <summary>Validates all paths against one immutable profile and rejects missing or mixed-version subgraphs. / 根据一个不可变 Profile 验证全部路径，并拒绝缺失或混版本子图。</summary>
    public sealed class PromptableSegmentationArtifactBundle
    {
        private readonly IReadOnlyDictionary<PromptableSegmentationArtifactRole, ModelArtifact> _artifacts;

        /// <summary>Initializes and validates a complete artifact bundle. / 初始化并验证完整工件 Bundle。</summary>
        public PromptableSegmentationArtifactBundle(PromptableSegmentationProfile profile, IEnumerable<PromptableSegmentationArtifact> artifacts)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (artifacts == null) throw new ArgumentNullException(nameof(artifacts));
            var values = new Dictionary<PromptableSegmentationArtifactRole, ModelArtifact>();
            foreach (PromptableSegmentationArtifact item in artifacts)
            {
                if (item == null || values.ContainsKey(item.Role)) throw Invalid("Artifact bundle roles must be non-null and unique.");
                PromptableSegmentationArtifactContract contract = profile.GetArtifact(item.Role);
                ModelArtifact artifact = item.Artifact;
                if (artifact.ModelId != contract.ModelId || !string.Equals(artifact.Format, contract.Format, StringComparison.OrdinalIgnoreCase) || !string.Equals(artifact.Sha256, contract.ArtifactSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw Invalid("A bundle artifact does not match its profile-bound ID, format, and SHA256: " + item.Role + ".");
                }
                values.Add(item.Role, artifact);
            }
            foreach (PromptableSegmentationArtifactContract contract in profile.Artifacts) if (!values.ContainsKey(contract.Role)) throw Invalid("A required bundle artifact is missing: " + contract.Role + ".");
            _artifacts = new ReadOnlyDictionary<PromptableSegmentationArtifactRole, ModelArtifact>(values);
        }

        /// <summary>Gets the bound profile. / 获取绑定 Profile。</summary>
        public PromptableSegmentationProfile Profile { get; }
        /// <summary>Gets all role/path associations. / 获取全部角色/路径关联。</summary>
        public IReadOnlyDictionary<PromptableSegmentationArtifactRole, ModelArtifact> Artifacts => _artifacts;
        /// <summary>Gets one required concrete artifact. / 获取一个必需的具体工件。</summary>
        public ModelArtifact GetArtifact(PromptableSegmentationArtifactRole role)
        {
            ModelArtifact artifact;
            if (!_artifacts.TryGetValue(role, out artifact!)) throw Invalid("The required artifact role is missing: " + role + ".");
            return artifact;
        }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.PromptableSegmentationIdentityMismatch, message);
    }

    /// <summary>Represents one source-image point prompt. / 表示一个源图空间点提示。</summary>
    public readonly struct PromptPoint : IEquatable<PromptPoint>
    {
        /// <summary>Initializes a finite source-image point prompt. / 初始化有限源图空间点提示。</summary>
        public PromptPoint(float x, float y, PromptPointLabel label)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "Prompt coordinates must be finite.");
            if (!Enum.IsDefined(typeof(PromptPointLabel), label)) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "The point label is invalid.");
            X = x;
            Y = y;
            Label = label;
        }

        /// <summary>Gets source-space X. / 获取源图空间 X。</summary>
        public float X { get; }
        /// <summary>Gets source-space Y. / 获取源图空间 Y。</summary>
        public float Y { get; }
        /// <summary>Gets point meaning. / 获取点含义。</summary>
        public PromptPointLabel Label { get; }
        /// <summary>Compares two prompt points by exact coordinates and label. / 按精确坐标与标签比较两个提示点。</summary>
        public bool Equals(PromptPoint other) => X == other.X && Y == other.Y && Label == other.Label;
        /// <summary>Compares this prompt point with another object. / 将此提示点与另一个对象比较。</summary>
        public override bool Equals(object? obj) => obj is PromptPoint other && Equals(other);
        /// <summary>Returns the hash code for the exact point identity. / 返回精确点 Identity 的哈希码。</summary>
        public override int GetHashCode() => unchecked((((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397) ^ (int)Label);
        /// <summary>Compares two points. / 比较两个点。</summary>
        public static bool operator ==(PromptPoint left, PromptPoint right) => left.Equals(right);
        /// <summary>Compares two points for inequality. / 比较两个点是否不相等。</summary>
        public static bool operator !=(PromptPoint left, PromptPoint right) => !left.Equals(right);
    }

    /// <summary>Contains typed source-space point, box, and identity-bound mask-feedback prompts for one decode. / 包含一次解码的类型化源图点、框及绑定 Identity 的 Mask Feedback 提示。</summary>
    public sealed class PromptableSegmentationPrompt
    {
        private readonly IReadOnlyList<PromptPoint> _points;

        /// <summary>Initializes one immutable prompt; at least one point, box, or feedback mask is required. / 初始化一个不可变提示；至少需要点、框或反馈掩码之一。</summary>
        public PromptableSegmentationPrompt(IEnumerable<PromptPoint>? points = null, RectangleF? box = null, PromptableMaskFeedback? maskFeedback = null, bool returnMultipleMasks = true, string? promptId = null)
        {
            var copy = points == null ? new List<PromptPoint>() : new List<PromptPoint>(points);
            if (box.HasValue)
            {
                RectangleF value = box.Value;
                if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Width) || !Finite(value.Height) || value.Width <= 0 || value.Height <= 0) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "A box prompt must be finite and non-empty.");
            }
            if (copy.Count == 0 && !box.HasValue && maskFeedback == null) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "At least one point, box, or mask-feedback prompt is required.");
            _points = new ReadOnlyCollection<PromptPoint>(copy);
            Box = box;
            MaskFeedback = maskFeedback;
            ReturnMultipleMasks = returnMultipleMasks;
            PromptId = string.IsNullOrWhiteSpace(promptId) ? null : promptId!.Trim();
        }

        /// <summary>Gets source-image point prompts. / 获取源图空间点提示。</summary>
        public IReadOnlyList<PromptPoint> Points => _points;
        /// <summary>Gets an optional half-open source-image box. / 获取可选半开区间源图框。</summary>
        public RectangleF? Box { get; }
        /// <summary>Gets optional identity-bound low-resolution feedback logits. / 获取可选绑定 Identity 的低分辨率反馈 Logit。</summary>
        public PromptableMaskFeedback? MaskFeedback { get; }
        /// <summary>Gets whether all graph candidates are returned; false selects the highest quality candidate deterministically. / 获取是否返回图中全部候选；false 时确定性选择最高质量候选。</summary>
        public bool ReturnMultipleMasks { get; }
        /// <summary>Gets an optional application prompt identifier. / 获取可选应用提示标识符。</summary>
        public string? PromptId { get; }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
