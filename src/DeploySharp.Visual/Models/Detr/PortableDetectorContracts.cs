using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.Detr
{
    /// <summary>Identifies the supported non-YOLO detector family. / 标识支持的非 YOLO 检测模型族。</summary>
    public enum PortableDetectorFamily
    {
        /// <summary>DEIMv2 postprocessor export. / DEIMv2 后处理器导出。</summary>
        DEIMv2Det = 0,
        /// <summary>RF-DETR raw detection export. / RF-DETR 原始检测导出。</summary>
        RFDETRDet = 1,
        /// <summary>RF-DETR raw instance-segmentation export. / RF-DETR 原始实例分割导出。</summary>
        RFDETRSeg = 2,
        /// <summary>PaddleDetection RT-DETR decoded export. / PaddleDetection RT-DETR 解码导出。</summary>
        RTDETRDet = 3,
        /// <summary>PaddleDetection PP-YOLOE decoded export. / PaddleDetection PP-YOLOE 解码导出。</summary>
        PPYOLOEDet = 4,
        /// <summary>Paddle or PyTorch RT-DETR raw query export. / Paddle 或 PyTorch RT-DETR 原始 Query 导出。</summary>
        RTDETRRawDet = 5,
        /// <summary>Official PyTorch RT-DETRv2 decoded triplet export. / 官方 PyTorch RT-DETRv2 已解码三张量导出。</summary>
        RTDETRv2Det = 6
    }

    /// <summary>Identifies the physical output contract used by a portable detector profile. / 标识便携检测 Profile 使用的物理输出合同。</summary>
    public enum PortableDetectorOutputKind
    {
        /// <summary>DEIM labels, boxes, and scores are already postprocessor-decoded. / DEIM 标签、边界框和分数已由后处理器解码。</summary>
        DeimDecoded = 0,
        /// <summary>RF-DETR boxes and class logits are raw query outputs. / RF-DETR 边界框和类别 Logit 是原始 Query 输出。</summary>
        RfDetrRaw = 1,
        /// <summary>Paddle rows contain class, score, and xyxy coordinates. / Paddle 行包含类别、分数和 xyxy 坐标。</summary>
        PaddleDecoded = 2,
        /// <summary>RF-DETR raw query outputs additionally contain mask logits. / RF-DETR 原始 Query 输出还包含掩码 Logit。</summary>
        RfDetrSegmentation = 3,
        /// <summary>RT-DETR query logits and normalized cxcywh boxes require exported-model postprocessing. / RT-DETR Query Logit 与归一化 cxcywh 框需要模型外后处理。</summary>
        RtDetrRaw = 4,
        /// <summary>RT-DETRv2 labels, source-space xyxy boxes, and scores are exported postprocessor outputs. / RT-DETRv2 标签、源图 xyxy 框与分数是导出后处理器输出。</summary>
        RtDetrV2Decoded = 5
    }

    /// <summary>Identifies the box coordinate encoding bound to an artifact. / 标识绑定到工件的边界框坐标编码。</summary>
    public enum PortableDetectorBoxFormat
    {
        /// <summary>Left, top, right, bottom. / 左、上、右、下。</summary>
        Xyxy = 0,
        /// <summary>Center x, center y, width, height. / 中心 x、中心 y、宽、高。</summary>
        Cxcywh = 1
    }

    /// <summary>Identifies the coordinate space emitted by an artifact. / 标识工件输出的坐标空间。</summary>
    public enum PortableDetectorCoordinateSpace
    {
        /// <summary>Coordinates are model-canvas pixels and require ImageTransform restoration. / 坐标为模型画布像素，需要 ImageTransform 恢复。</summary>
        ModelPixels = 0,
        /// <summary>Coordinates are already source-image pixels. / 坐标已经是源图像素。</summary>
        SourcePixels = 1,
        /// <summary>Coordinates are normalized to the source image. / 坐标按源图归一化。</summary>
        NormalizedSource = 2
    }

    /// <summary>Identifies who owns non-maximum suppression for a detector artifact. / 标识检测工件的非极大值抑制归属。</summary>
    public enum PortableDetectorNmsOwnership
    {
        /// <summary>The end-to-end contract performs no NMS. / 端到端合同不执行 NMS。</summary>
        None = 0,
        /// <summary>The exported graph owns NMS; DeploySharp must not repeat it. / 导出图拥有 NMS；DeploySharp 不得重复执行。</summary>
        ExportedGraph = 1,
        /// <summary>DeploySharp owns NMS for raw model outputs. / DeploySharp 为原始模型输出负责 NMS。</summary>
        DeploySharp = 2
    }

    /// <summary>Identifies the physical Paddle count tensor shape. / 标识 Paddle 数量张量的物理 shape。</summary>
    public enum PortableDetectorCountShape
    {
        /// <summary>A rank-zero scalar count. / 零秩标量数量。</summary>
        Scalar = 0,
        /// <summary>A one-value batch vector for single-image execution. / 单图执行的一元素批次向量。</summary>
        BatchVector = 1
    }

    /// <summary>Controls one artifact-bound portable detector profile. / 控制一个绑定工件的便携检测 Profile。</summary>
    public sealed class PortableDetectorProfileOptions
    {
        /// <summary>Initializes profile options. / 初始化 Profile 选项。</summary>
        public PortableDetectorProfileOptions(
            int opset,
            VisualSize? modelSize = null,
            IEnumerable<string>? labels = null,
            string modelFormat = "onnx",
            string inputName = "input",
            string? profileId = null,
            string? artifactSha256 = null,
            string upstreamRepository = "",
            string upstreamCommit = "",
            string exporterVersion = "",
            string license = "",
            float scoreThreshold = 0.4f,
            int maximumCandidates = 3000,
            int maximumResults = 300,
            int topK = 300,
            long maximumMaskPixels = 64L * 1024 * 1024,
            string preprocessingVersion = "official-preprocess-v1",
            string postprocessingVersion = "official-postprocess-v1",
            string? boxesOutputName = null,
            string? labelsOutputName = null,
            string? scoresOutputName = null,
            string? countOutputName = null,
            string? masksOutputName = null,
            int rfDetrQueryCount = -1,
            bool rfDetrIncludesNoObjectClass = false,
            bool deimUsesImageNetNormalization = true,
            bool hasDynamicBatchAxis = false,
            int minimumBatch = 1,
            int maximumBatch = 1,
            PortableDetectorCountShape paddleCountShape = PortableDetectorCountShape.Scalar)
        {
            if (opset <= 0) throw new ArgumentOutOfRangeException(nameof(opset));
            if (string.IsNullOrWhiteSpace(modelFormat)) throw new ArgumentException("A model format is required.", nameof(modelFormat));
            if (string.IsNullOrWhiteSpace(inputName)) throw new ArgumentException("An input name is required.", nameof(inputName));
            if (scoreThreshold < 0 || scoreThreshold > 1 || float.IsNaN(scoreThreshold) || float.IsInfinity(scoreThreshold)) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (maximumCandidates <= 0 || maximumResults <= 0 || maximumResults > maximumCandidates) throw new ArgumentOutOfRangeException(nameof(maximumResults));
            if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));
            if (topK < maximumResults) throw new ArgumentException("Top-k cannot be smaller than the result bound.", nameof(topK));
            if (maximumMaskPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumMaskPixels));
            if (rfDetrQueryCount == 0 || rfDetrQueryCount < -1) throw new ArgumentOutOfRangeException(nameof(rfDetrQueryCount));
            if (!Enum.IsDefined(typeof(PortableDetectorCountShape), paddleCountShape)) throw new ArgumentOutOfRangeException(nameof(paddleCountShape));
            Opset = opset;
            ModelSize = modelSize ?? new VisualSize(640, 640);
            Labels = CopyLabels(labels);
            ModelFormat = modelFormat.Trim();
            InputName = inputName.Trim();
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? null : profileId!.Trim();
            ArtifactSha256 = string.IsNullOrWhiteSpace(artifactSha256) ? null : artifactSha256!.Trim().ToLowerInvariant();
            UpstreamRepository = upstreamRepository ?? string.Empty;
            UpstreamCommit = upstreamCommit ?? string.Empty;
            ExporterVersion = exporterVersion ?? string.Empty;
            License = license ?? string.Empty;
            ScoreThreshold = scoreThreshold;
            MaximumCandidates = maximumCandidates;
            MaximumResults = maximumResults;
            TopK = topK;
            MaximumMaskPixels = maximumMaskPixels;
            PreprocessingVersion = RequiredVersion(preprocessingVersion, nameof(preprocessingVersion));
            PostprocessingVersion = RequiredVersion(postprocessingVersion, nameof(postprocessingVersion));
            BoxesOutputName = OptionalName(boxesOutputName, nameof(boxesOutputName));
            LabelsOutputName = OptionalName(labelsOutputName, nameof(labelsOutputName));
            ScoresOutputName = OptionalName(scoresOutputName, nameof(scoresOutputName));
            CountOutputName = OptionalName(countOutputName, nameof(countOutputName));
            MasksOutputName = OptionalName(masksOutputName, nameof(masksOutputName));
            RfDetrQueryCount = rfDetrQueryCount;
            RfDetrIncludesNoObjectClass = rfDetrIncludesNoObjectClass;
            DeimUsesImageNetNormalization = deimUsesImageNetNormalization;
            Batch = new PortableDetectorBatchContract(hasDynamicBatchAxis, minimumBatch, maximumBatch);
            PaddleCountShape = paddleCountShape;
        }

        /// <summary>Gets the ONNX opset. / 获取 ONNX opset。</summary>
        public int Opset { get; }
        /// <summary>Gets the model input size. / 获取模型输入尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets immutable class labels. / 获取不可变类别标签。</summary>
        public IReadOnlyList<string> Labels { get; }
        /// <summary>Gets the model format. / 获取模型格式。</summary>
        public string ModelFormat { get; }
        /// <summary>Gets the primary image input name. / 获取主图像输入名称。</summary>
        public string InputName { get; }
        /// <summary>Gets an optional stable profile ID. / 获取可选稳定 Profile ID。</summary>
        public string? ProfileId { get; }
        /// <summary>Gets an optional artifact SHA256. / 获取可选工件 SHA256。</summary>
        public string? ArtifactSha256 { get; }
        /// <summary>Gets upstream repository provenance. / 获取上游仓库来源。</summary>
        public string UpstreamRepository { get; }
        /// <summary>Gets upstream commit or release provenance. / 获取上游提交或 Release 来源。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets exporter version provenance. / 获取导出器版本来源。</summary>
        public string ExporterVersion { get; }
        /// <summary>Gets the upstream license identifier. / 获取上游许可证标识。</summary>
        public string License { get; }
        /// <summary>Gets the strict score threshold. / 获取严格分数阈值。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets the candidate bound. / 获取候选上限。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets the result bound. / 获取结果上限。</summary>
        public int MaximumResults { get; }
        /// <summary>Gets RF-DETR global top-k. / 获取 RF-DETR 全局 top-k。</summary>
        public int TopK { get; }
        /// <summary>Gets the maximum total number of source-mask pixels materialized by one decode. / 获取一次解码允许生成的源图掩码像素总数上限。</summary>
        public long MaximumMaskPixels { get; }
        /// <summary>Gets preprocessing contract version. / 获取预处理合同版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets postprocessing contract version. / 获取后处理合同版本。</summary>
        public string PostprocessingVersion { get; }
        /// <summary>Gets an optional exact box or row output override. / 获取可选的精确边界框或行输出覆盖。</summary>
        public string? BoxesOutputName { get; }
        /// <summary>Gets an optional exact label/logit output override. / 获取可选的精确标签或 Logit 输出覆盖。</summary>
        public string? LabelsOutputName { get; }
        /// <summary>Gets an optional exact score output override. / 获取可选的精确分数输出覆盖。</summary>
        public string? ScoresOutputName { get; }
        /// <summary>Gets an optional exact count output override. / 获取可选的精确数量输出覆盖。</summary>
        public string? CountOutputName { get; }
        /// <summary>Gets an optional exact mask output override. / 获取可选的精确掩码输出覆盖。</summary>
        public string? MasksOutputName { get; }
        /// <summary>Gets the RF-DETR query count, or -1 when the artifact is dynamically shaped. / 获取 RF-DETR Query 数；工件使用动态形状时为 -1。</summary>
        public int RfDetrQueryCount { get; }
        /// <summary>Gets whether this RF-DETR artifact reserves one final logit column for no-object. / 获取此 RF-DETR 工件是否为 no-object 保留最后一个 Logit 列。</summary>
        public bool RfDetrIncludesNoObjectClass { get; }
        /// <summary>Gets whether this DEIMv2 artifact uses ImageNet mean and standard deviation normalization. / 获取此 DEIMv2 工件是否使用 ImageNet 均值和标准差归一化。</summary>
        public bool DeimUsesImageNetNormalization { get; }
        /// <summary>Gets the artifact batch-axis and executable batch bounds. / 获取工件批次轴与可执行批次边界。</summary>
        public PortableDetectorBatchContract Batch { get; }
        /// <summary>Gets the physical Paddle result-count shape. / 获取 Paddle 结果数量的物理 shape。</summary>
        public PortableDetectorCountShape PaddleCountShape { get; }

        private static IReadOnlyList<string> CopyLabels(IEnumerable<string>? labels)
        {
            var result = new List<string>();
            if (labels != null)
            {
                foreach (string label in labels)
                {
                    if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Labels cannot contain empty values.", nameof(labels));
                    result.Add(label.Trim());
                }
            }

            return result.AsReadOnly();
        }

        private static string RequiredVersion(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A contract version is required.", name);
            return value.Trim();
        }

        private static string? OptionalName(string? value, string name)
        {
            if (value == null) return null;
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A tensor-name override cannot be empty.", name);
            return value.Trim();
        }
    }

    /// <summary>Describes exact tensor names and output semantics for one detector artifact. / 描述一个检测工件的精确张量名称与输出语义。</summary>
    public sealed class PortableDetectorOutputContract
    {
        internal PortableDetectorOutputContract(
            PortableDetectorOutputKind kind,
            string boxesName,
            string labelsName,
            string scoresName,
            string? countName,
            string? masksName,
            int classCount,
            int queryCount,
            bool includesNoObjectClass = false,
            PortableDetectorBoxFormat boxFormat = PortableDetectorBoxFormat.Xyxy,
            PortableDetectorCoordinateSpace coordinateSpace = PortableDetectorCoordinateSpace.ModelPixels,
            PortableDetectorNmsOwnership nmsOwnership = PortableDetectorNmsOwnership.None,
            PortableDetectorCountShape countShape = PortableDetectorCountShape.Scalar)
        {
            Kind = kind;
            BoxesName = boxesName;
            LabelsName = labelsName;
            ScoresName = scoresName;
            CountName = countName;
            MasksName = masksName;
            ClassCount = classCount;
            QueryCount = queryCount;
            IncludesNoObjectClass = includesNoObjectClass;
            BoxFormat = boxFormat;
            CoordinateSpace = coordinateSpace;
            NmsOwnership = nmsOwnership;
            CountShape = countShape;
        }

        /// <summary>Gets the output semantic kind. / 获取输出语义类型。</summary>
        public PortableDetectorOutputKind Kind { get; }
        /// <summary>Gets the exact box output name. / 获取精确边界框输出名称。</summary>
        public string BoxesName { get; }
        /// <summary>Gets the exact class or label output name. / 获取精确类别或标签输出名称。</summary>
        public string LabelsName { get; }
        /// <summary>Gets the exact score output name. / 获取精确分数输出名称。</summary>
        public string ScoresName { get; }
        /// <summary>Gets the optional Paddle count output name. / 获取可选 Paddle 数量输出名称。</summary>
        public string? CountName { get; }
        /// <summary>Gets the optional mask output name. / 获取可选掩码输出名称。</summary>
        public string? MasksName { get; }
        /// <summary>Gets the declared foreground class count, when known. / 获取已声明的前景类别数（如果已知）。</summary>
        public int ClassCount { get; }
        /// <summary>Gets the exporter query count, or -1 when dynamic. / 获取导出器 Query 数，动态时为 -1。</summary>
        public int QueryCount { get; }
        /// <summary>Gets whether the final RF-DETR logit column is a no-object slot. / 获取最后一个 RF-DETR Logit 列是否为 no-object 槽位。</summary>
        public bool IncludesNoObjectClass { get; }
        /// <summary>Gets the artifact-bound box encoding. / 获取绑定工件的边界框编码。</summary>
        public PortableDetectorBoxFormat BoxFormat { get; }
        /// <summary>Gets the artifact-bound coordinate space. / 获取绑定工件的坐标空间。</summary>
        public PortableDetectorCoordinateSpace CoordinateSpace { get; }
        /// <summary>Gets explicit NMS ownership; a decoder must not repeat graph-owned NMS. / 获取显式 NMS 归属；Decoder 不得重复图内 NMS。</summary>
        public PortableDetectorNmsOwnership NmsOwnership { get; }
        /// <summary>Gets the physical result-count shape when one is present. / 获取结果数量张量存在时的物理 shape。</summary>
        public PortableDetectorCountShape CountShape { get; }
    }

    /// <summary>Contains a complete artifact-bound visual profile and provenance. / 包含完整的工件绑定 Visual Profile 与来源信息。</summary>
    public sealed class PortableDetectorProfile
    {
        private readonly IReadOnlyList<PortableDetectorAuxiliaryInputContract> _auxiliaryInputs;

        internal PortableDetectorProfile(PortableDetectorFamily family, PortableDetectorOutputContract output, PortableDetectorProfileOptions options, IReadOnlyList<PortableDetectorAuxiliaryInputContract> auxiliaryInputs, VisualModelProfile visualProfile)
        {
            Family = family;
            Output = output;
            UpstreamRepository = options.UpstreamRepository;
            UpstreamCommit = options.UpstreamCommit;
            ExporterVersion = options.ExporterVersion;
            License = options.License;
            ArtifactSha256 = NormalizeProfileHash(options.ArtifactSha256);
            DeimUsesImageNetNormalization = options.DeimUsesImageNetNormalization;
            ScoreThreshold = options.ScoreThreshold;
            MaximumCandidates = options.MaximumCandidates;
            MaximumResults = options.MaximumResults;
            TopK = options.TopK;
            MaximumMaskPixels = options.MaximumMaskPixels;
            PreprocessingVersion = options.PreprocessingVersion;
            PostprocessingVersion = options.PostprocessingVersion;
            Batch = options.Batch;
            _auxiliaryInputs = auxiliaryInputs;
            VisualProfile = visualProfile;
        }

        /// <summary>Gets the model family. / 获取模型族。</summary>
        public PortableDetectorFamily Family { get; }
        /// <summary>Gets the exact output contract. / 获取精确输出合同。</summary>
        public PortableDetectorOutputContract Output { get; }
        /// <summary>Gets upstream repository URL. / 获取上游仓库 URL。</summary>
        public string UpstreamRepository { get; }
        /// <summary>Gets upstream commit or release. / 获取上游提交或 Release。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets exporter version. / 获取导出器版本。</summary>
        public string ExporterVersion { get; }
        /// <summary>Gets license evidence. / 获取许可证证据。</summary>
        public string License { get; }
        /// <summary>Gets artifact SHA256. / 获取工件 SHA256。</summary>
        public string ArtifactSha256 { get; }
        /// <summary>Gets whether the DEIMv2 artifact uses ImageNet normalization. / 获取 DEIMv2 工件是否使用 ImageNet 归一化。</summary>
        public bool DeimUsesImageNetNormalization { get; }
        /// <summary>Gets the strict score threshold; equality is rejected. / 获取严格分数阈值；等于阈值时拒绝。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets the maximum accepted candidate count. / 获取允许的最大候选数量。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets the maximum returned detection count. / 获取返回的最大检测数量。</summary>
        public int MaximumResults { get; }
        /// <summary>Gets the raw-query global top-k bound. / 获取原始 Query 的全局 top-k 上限。</summary>
        public int TopK { get; }
        /// <summary>Gets the source-mask pixel budget. / 获取源图掩码像素预算。</summary>
        public long MaximumMaskPixels { get; }
        /// <summary>Gets the immutable preprocessing contract version. / 获取不可变前处理合同版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets the immutable postprocessing contract version. / 获取不可变后处理合同版本。</summary>
        public string PostprocessingVersion { get; }
        /// <summary>Gets the artifact batch-axis and single-decode execution contract. / 获取工件批次轴与单次解码执行合同。</summary>
        public PortableDetectorBatchContract Batch { get; }
        /// <summary>Gets typed auxiliary generation contracts in backend input order. / 获取按后端输入顺序排列的类型化辅助生成合同。</summary>
        public IReadOnlyList<PortableDetectorAuxiliaryInputContract> AuxiliaryInputs => _auxiliaryInputs;
        /// <summary>Gets the backend-neutral Visual profile. / 获取后端无关 Visual Profile。</summary>
        public VisualModelProfile VisualProfile { get; }

        /// <summary>Creates a Core artifact bound to this profile. / 创建绑定到此 Profile 的 Core 工件。</summary>
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null)
        {
            return new ModelArtifact(VisualProfile.ModelId, VisualProfile.ModelFormat, path, string.IsNullOrEmpty(ArtifactSha256) ? null : ArtifactSha256, preferredBackend);
        }

        /// <summary>Creates every required auxiliary tensor once from prepared source/model geometry; returned managed tensors are owned by their tensor objects and need no native disposal. / 从已准备的源图/模型几何一次性创建全部必需辅助张量；返回的托管张量由张量对象拥有，无需释放原生资源。</summary>
        public IReadOnlyList<NamedTensor> CreateAuxiliaryInputs(PreparedVisualInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var values = new List<NamedTensor>(_auxiliaryInputs.Count);
            foreach (PortableDetectorAuxiliaryInputContract contract in _auxiliaryInputs) values.Add(contract.CreateTensor(input));
            return values.AsReadOnly();
        }

        private static string NormalizeProfileHash(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value!.Trim().ToLowerInvariant();
    }

    /// <summary>Creates artifact-bound DEIMv2, RF-DETR, Paddle RT-DETR, RT-DETRv2, and PP-YOLOE profiles. / 创建工件绑定的 DEIMv2、RF-DETR、Paddle RT-DETR、RT-DETRv2 与 PP-YOLOE Profile。</summary>
    public static class PortableDetectorProfiles
    {
        /// <summary>Creates a DEIMv2 decoded-output profile. / 创建 DEIMv2 解码输出 Profile。</summary>
        public static PortableDetectorProfile CreateDEIMv2(ModelId modelId, PortableDetectorProfileOptions options)
            => Create(modelId, PortableDetectorFamily.DEIMv2Det, options, new PortableDetectorOutputContract(PortableDetectorOutputKind.DeimDecoded, options.BoxesOutputName ?? "boxes", options.LabelsOutputName ?? "labels", options.ScoresOutputName ?? "scores", null, null, options.Labels.Count, -1, boxFormat: PortableDetectorBoxFormat.Xyxy, coordinateSpace: PortableDetectorCoordinateSpace.ModelPixels, nmsOwnership: PortableDetectorNmsOwnership.ExportedGraph));

        /// <summary>Creates an RF-DETR detection profile. / 创建 RF-DETR 检测 Profile。</summary>
        public static PortableDetectorProfile CreateRFDETR(ModelId modelId, PortableDetectorProfileOptions options)
            => Create(modelId, PortableDetectorFamily.RFDETRDet, options, new PortableDetectorOutputContract(PortableDetectorOutputKind.RfDetrRaw, options.BoxesOutputName ?? "dets", options.LabelsOutputName ?? "labels", "", null, null, options.Labels.Count, options.RfDetrQueryCount, options.RfDetrIncludesNoObjectClass, PortableDetectorBoxFormat.Cxcywh, PortableDetectorCoordinateSpace.NormalizedSource));

        /// <summary>Creates an RF-DETR instance-segmentation profile. / 创建 RF-DETR 实例分割 Profile。</summary>
        public static PortableDetectorProfile CreateRFDETRSeg(ModelId modelId, PortableDetectorProfileOptions options)
            => Create(modelId, PortableDetectorFamily.RFDETRSeg, options, new PortableDetectorOutputContract(PortableDetectorOutputKind.RfDetrSegmentation, options.BoxesOutputName ?? "dets", options.LabelsOutputName ?? "labels", "", null, options.MasksOutputName ?? "masks", options.Labels.Count, options.RfDetrQueryCount, options.RfDetrIncludesNoObjectClass, PortableDetectorBoxFormat.Cxcywh, PortableDetectorCoordinateSpace.NormalizedSource));

        /// <summary>Creates a PaddleDetection RT-DETR profile. / 创建 PaddleDetection RT-DETR Profile。</summary>
        public static PortableDetectorProfile CreateRTDETR(ModelId modelId, PortableDetectorProfileOptions options)
            => Create(modelId, PortableDetectorFamily.RTDETRDet, options, new PortableDetectorOutputContract(PortableDetectorOutputKind.PaddleDecoded, options.BoxesOutputName ?? "reshape2_95.tmp_0", "", "", options.CountOutputName ?? "tile_3.tmp_0", null, options.Labels.Count, -1, boxFormat: PortableDetectorBoxFormat.Xyxy, coordinateSpace: PortableDetectorCoordinateSpace.SourcePixels, nmsOwnership: PortableDetectorNmsOwnership.ExportedGraph, countShape: options.PaddleCountShape));

        /// <summary>Creates an RT-DETR raw-query profile using sigmoid, global top-k and normalized cxcywh restoration without NMS. / 创建使用 sigmoid、全局 top-k 与归一化 cxcywh 恢复且不执行 NMS 的 RT-DETR 原始 Query Profile。</summary>
        public static PortableDetectorProfile CreateRTDETRRaw(ModelId modelId, PortableDetectorProfileOptions options)
            => Create(modelId, PortableDetectorFamily.RTDETRRawDet, options, new PortableDetectorOutputContract(PortableDetectorOutputKind.RtDetrRaw, options.BoxesOutputName ?? "pred_boxes", options.LabelsOutputName ?? "pred_logits", "", null, null, options.Labels.Count, options.RfDetrQueryCount, options.RfDetrIncludesNoObjectClass, PortableDetectorBoxFormat.Cxcywh, PortableDetectorCoordinateSpace.NormalizedSource));

        /// <summary>Creates an official PyTorch RT-DETRv2 decoded-triplet profile; `orig_target_sizes` is source width then height and graph outputs are already source-space xyxy. / 创建官方 PyTorch RT-DETRv2 已解码三张量 Profile；`orig_target_sizes` 为源图宽后高，图输出已经是源图 xyxy。</summary>
        public static PortableDetectorProfile CreateRTDETRv2(ModelId modelId, PortableDetectorProfileOptions options)
            => Create(modelId, PortableDetectorFamily.RTDETRv2Det, options, new PortableDetectorOutputContract(PortableDetectorOutputKind.RtDetrV2Decoded, options.BoxesOutputName ?? "boxes", options.LabelsOutputName ?? "labels", options.ScoresOutputName ?? "scores", null, null, options.Labels.Count, options.RfDetrQueryCount, boxFormat: PortableDetectorBoxFormat.Xyxy, coordinateSpace: PortableDetectorCoordinateSpace.SourcePixels));

        /// <summary>Creates a PaddleDetection PP-YOLOE profile. / 创建 PaddleDetection PP-YOLOE Profile。</summary>
        public static PortableDetectorProfile CreatePPYOLOE(ModelId modelId, PortableDetectorProfileOptions options)
            => Create(modelId, PortableDetectorFamily.PPYOLOEDet, options, new PortableDetectorOutputContract(PortableDetectorOutputKind.PaddleDecoded, options.BoxesOutputName ?? "save_infer_model/scale_0.tmp_0", "", "", options.CountOutputName ?? "save_infer_model/scale_1.tmp_0", null, options.Labels.Count, -1, boxFormat: PortableDetectorBoxFormat.Xyxy, coordinateSpace: PortableDetectorCoordinateSpace.SourcePixels, nmsOwnership: PortableDetectorNmsOwnership.ExportedGraph, countShape: PortableDetectorCountShape.BatchVector));

        private static PortableDetectorProfile Create(ModelId modelId, PortableDetectorFamily family, PortableDetectorProfileOptions options, PortableDetectorOutputContract output)
        {
            if (modelId.IsEmpty) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A model identifier is required.");
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Labels.Count == 0) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A portable detector profile requires the exact foreground class list.");
            NormalizeHash(options.ArtifactSha256);
            var labels = new List<VisualLabel>();
            for (int index = 0; index < options.Labels.Count; index++) labels.Add(new VisualLabel(index, options.Labels[index]));
            bool segmentation = family == PortableDetectorFamily.RFDETRSeg;
            IVisualDecoder decoder = segmentation
                ? new RFDETRInstanceSegmentationDecoder(output, options.ScoreThreshold, options.TopK, options.MaximumResults, options.MaximumMaskPixels, options.MaximumCandidates)
                : new PortableDetectorDecoder(output, options.ScoreThreshold, options.MaximumCandidates, options.MaximumResults, options.TopK);
            IReadOnlyList<PortableDetectorAuxiliaryInputContract> auxiliaryContracts = PortableDetectorAuxiliaryContracts.Create(family, options.Batch);
            var auxiliaryBindings = new List<VisualAuxiliaryInputBinding>(auxiliaryContracts.Count);
            foreach (PortableDetectorAuxiliaryInputContract contract in auxiliaryContracts) auxiliaryBindings.Add(contract.ToVisualBinding());
            string inputName = family == PortableDetectorFamily.DEIMv2Det ? "images" : options.InputName;
            long batch = options.Batch.ShapeDimension;
            TensorShape inputShape = new TensorShape(batch, 3, options.ModelSize.Height, options.ModelSize.Width);
            var outputs = new List<VisualOutputBinding>();
            if (family == PortableDetectorFamily.DEIMv2Det || family == PortableDetectorFamily.RTDETRv2Det)
            {
                outputs.Add(new VisualOutputBinding(output.LabelsName, TensorElementType.Int64, new TensorShape(batch, output.QueryCount > 0 ? output.QueryCount : -1)));
                outputs.Add(new VisualOutputBinding(output.BoxesName, TensorElementType.Float32, new TensorShape(batch, output.QueryCount > 0 ? output.QueryCount : -1, 4)));
                outputs.Add(new VisualOutputBinding(output.ScoresName, TensorElementType.Float32, new TensorShape(batch, output.QueryCount > 0 ? output.QueryCount : -1)));
            }
            else if (family == PortableDetectorFamily.RFDETRDet || family == PortableDetectorFamily.RTDETRRawDet)
            {
                outputs.Add(new VisualOutputBinding(output.BoxesName, TensorElementType.Float32, new TensorShape(batch, output.QueryCount, 4)));
                outputs.Add(new VisualOutputBinding(output.LabelsName, TensorElementType.Float32, new TensorShape(batch, output.QueryCount, options.Labels.Count + (output.IncludesNoObjectClass ? 1 : 0))));
            }
            else if (family == PortableDetectorFamily.RFDETRSeg)
            {
                outputs.Add(new VisualOutputBinding(output.BoxesName, TensorElementType.Float32, new TensorShape(batch, output.QueryCount, 4)));
                outputs.Add(new VisualOutputBinding(output.LabelsName, TensorElementType.Float32, new TensorShape(batch, output.QueryCount, options.Labels.Count + (output.IncludesNoObjectClass ? 1 : 0))));
                outputs.Add(new VisualOutputBinding(output.MasksName!, TensorElementType.Float32, new TensorShape(batch, output.QueryCount, -1, -1)));
            }
            else
            {
                outputs.Add(new VisualOutputBinding(output.BoxesName, TensorElementType.Float32, new TensorShape(-1, 6)));
                outputs.Add(new VisualOutputBinding(output.CountName!, TensorElementType.Int32, output.CountShape == PortableDetectorCountShape.Scalar ? new TensorShape() : new TensorShape(batch)));
            }

            var visual = new VisualModelProfile(
                options.ProfileId ?? "portable." + family.ToString().ToLowerInvariant() + "." + modelId.Value,
                modelId,
                segmentation ? VisualTaskId.InstanceSegmentation : VisualTaskId.ObjectDetection,
                "2.0.0-stage21",
                options.ModelFormat,
                new VisualInputBinding(inputName, TensorElementType.Float32, inputShape, VisualTensorLayout.Nchw, options.Batch.MinimumBatch, options.Batch.MaximumBatch),
                outputs,
                labels,
                decoder,
                auxiliaryInputs: auxiliaryBindings);
            return new PortableDetectorProfile(family, output, options, auxiliaryContracts, visual);
        }

        private static string NormalizeHash(string? hash)
        {
            if (string.IsNullOrWhiteSpace(hash)) return string.Empty;
            string value = hash!.Trim().ToLowerInvariant();
            if (value.Length != 64) throw new ArgumentException("An artifact SHA256 must contain 64 hexadecimal characters.", nameof(hash));
            for (int index = 0; index < value.Length; index++) if (!Uri.IsHexDigit(value[index])) throw new ArgumentException("An artifact SHA256 must be hexadecimal.", nameof(hash));
            return value;
        }
    }
}
