using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines named direct-coordinate Pose outputs and their exact component semantics. / 定义命名直接坐标姿态输出及其精确分量语义。</summary>
    public sealed class DirectPoseOutputSchema
    {
        /// <summary>Initializes a direct Pose output schema for `[1,candidates,keypoints,components]`. / 为 `[1,候选,关键点,分量]` 初始化直接姿态输出 Schema。</summary>
        public DirectPoseOutputSchema(
            string keypointsOutputName,
            int keypointCount,
            int componentCount,
            int xComponentIndex = 0,
            int yComponentIndex = 1,
            int scoreComponentIndex = 2,
            int visibilityComponentIndex = -1,
            PoseCoordinateSpace coordinateSpace = PoseCoordinateSpace.ModelPixels,
            VisualSize? tensorGridSize = null,
            PoseGridMappingMode gridMappingMode = PoseGridMappingMode.HalfPixel,
            string? boxesOutputName = null,
            DetectionBoxFormat boxFormat = DetectionBoxFormat.Xyxy,
            bool normalizedBoxes = false,
            string? instanceScoresOutputName = null,
            PoseScoreKind keypointScoreKind = PoseScoreKind.Probability,
            PoseScoreKind instanceScoreKind = PoseScoreKind.Probability,
            float defaultInstanceScore = 1f)
        {
            if (string.IsNullOrWhiteSpace(keypointsOutputName)) throw new ArgumentException("A keypoint output name is required.", nameof(keypointsOutputName));
            if (keypointCount <= 0) throw new ArgumentOutOfRangeException(nameof(keypointCount));
            if (componentCount < 2 || componentCount > 4) throw new ArgumentOutOfRangeException(nameof(componentCount));
            ValidateComponent(xComponentIndex, componentCount, nameof(xComponentIndex));
            ValidateComponent(yComponentIndex, componentCount, nameof(yComponentIndex));
            ValidateOptionalComponent(scoreComponentIndex, componentCount, nameof(scoreComponentIndex));
            ValidateOptionalComponent(visibilityComponentIndex, componentCount, nameof(visibilityComponentIndex));
            var used = new HashSet<int> { xComponentIndex };
            if (!used.Add(yComponentIndex) || (scoreComponentIndex >= 0 && !used.Add(scoreComponentIndex)) || (visibilityComponentIndex >= 0 && !used.Add(visibilityComponentIndex))) throw new ArgumentException("Pose component indices must be unique.");
            if (!Enum.IsDefined(typeof(PoseCoordinateSpace), coordinateSpace)) throw new ArgumentOutOfRangeException(nameof(coordinateSpace));
            if (!Enum.IsDefined(typeof(PoseGridMappingMode), gridMappingMode)) throw new ArgumentOutOfRangeException(nameof(gridMappingMode));
            if (coordinateSpace == PoseCoordinateSpace.TensorGrid && !tensorGridSize.HasValue) throw new ArgumentException("Tensor-grid coordinates require an explicit grid size.", nameof(tensorGridSize));
            if (coordinateSpace != PoseCoordinateSpace.TensorGrid && tensorGridSize.HasValue) throw new ArgumentException("A tensor-grid size is only valid for tensor-grid coordinates.", nameof(tensorGridSize));
            if (!Enum.IsDefined(typeof(DetectionBoxFormat), boxFormat)) throw new ArgumentOutOfRangeException(nameof(boxFormat));
            if (!Enum.IsDefined(typeof(PoseScoreKind), keypointScoreKind)) throw new ArgumentOutOfRangeException(nameof(keypointScoreKind));
            if (!Enum.IsDefined(typeof(PoseScoreKind), instanceScoreKind)) throw new ArgumentOutOfRangeException(nameof(instanceScoreKind));
            if (float.IsNaN(defaultInstanceScore) || float.IsInfinity(defaultInstanceScore) || defaultInstanceScore < 0 || (instanceScoreKind == PoseScoreKind.Probability && defaultInstanceScore > 1)) throw new ArgumentOutOfRangeException(nameof(defaultInstanceScore));
            KeypointsOutputName = keypointsOutputName;
            KeypointCount = keypointCount;
            ComponentCount = componentCount;
            XComponentIndex = xComponentIndex;
            YComponentIndex = yComponentIndex;
            ScoreComponentIndex = scoreComponentIndex;
            VisibilityComponentIndex = visibilityComponentIndex;
            CoordinateSpace = coordinateSpace;
            TensorGridSize = tensorGridSize;
            GridMappingMode = gridMappingMode;
            BoxesOutputName = NormalizeOptionalName(boxesOutputName);
            BoxFormat = boxFormat;
            NormalizedBoxes = normalizedBoxes;
            InstanceScoresOutputName = NormalizeOptionalName(instanceScoresOutputName);
            KeypointScoreKind = keypointScoreKind;
            InstanceScoreKind = instanceScoreKind;
            DefaultInstanceScore = defaultInstanceScore;
            if (string.Equals(KeypointsOutputName, BoxesOutputName, StringComparison.Ordinal) || string.Equals(KeypointsOutputName, InstanceScoresOutputName, StringComparison.Ordinal) || (BoxesOutputName != null && string.Equals(BoxesOutputName, InstanceScoresOutputName, StringComparison.Ordinal))) throw new ArgumentException("Direct Pose output names must be unique.");
        }

        /// <summary>Gets the keypoint tensor name. / 获取关键点张量名称。</summary>
        public string KeypointsOutputName { get; }
        /// <summary>Gets the exact keypoint count. / 获取精确关键点数量。</summary>
        public int KeypointCount { get; }
        /// <summary>Gets the exact per-keypoint component count. / 获取精确的逐关键点分量数量。</summary>
        public int ComponentCount { get; }
        /// <summary>Gets the X component index. / 获取 X 分量索引。</summary>
        public int XComponentIndex { get; }
        /// <summary>Gets the Y component index. / 获取 Y 分量索引。</summary>
        public int YComponentIndex { get; }
        /// <summary>Gets the score component index or -1 for an explicit constant score of one. / 获取分数分量索引，或以 -1 表示显式常量分数 1。</summary>
        public int ScoreComponentIndex { get; }
        /// <summary>Gets the visibility component index or -1 for Unknown visibility. / 获取可见性分量索引，或以 -1 表示 Unknown 可见性。</summary>
        public int VisibilityComponentIndex { get; }
        /// <summary>Gets the declared coordinate space. / 获取声明的坐标空间。</summary>
        public PoseCoordinateSpace CoordinateSpace { get; }
        /// <summary>Gets an explicit tensor-grid size when required. / 在需要时获取显式张量网格尺寸。</summary>
        public VisualSize? TensorGridSize { get; }
        /// <summary>Gets the grid/normalization mapping rule. / 获取网格或归一化映射规则。</summary>
        public PoseGridMappingMode GridMappingMode { get; }
        /// <summary>Gets an optional `[1,candidates,4]` box tensor name. / 获取可选 `[1,候选,4]` 边界框张量名称。</summary>
        public string? BoxesOutputName { get; }
        /// <summary>Gets the declared box format. / 获取声明的边界框格式。</summary>
        public DetectionBoxFormat BoxFormat { get; }
        /// <summary>Gets whether boxes are normalized to model size. / 获取边界框是否相对于模型尺寸归一化。</summary>
        public bool NormalizedBoxes { get; }
        /// <summary>Gets an optional `[1,candidates]` instance-score tensor name. / 获取可选 `[1,候选]` 实例分数张量名称。</summary>
        public string? InstanceScoresOutputName { get; }
        /// <summary>Gets keypoint score semantics. / 获取关键点分数语义。</summary>
        public PoseScoreKind KeypointScoreKind { get; }
        /// <summary>Gets instance score semantics. / 获取实例分数语义。</summary>
        public PoseScoreKind InstanceScoreKind { get; }
        /// <summary>Gets the explicitly configured instance score used when no score tensor exists. / 获取不存在分数张量时使用的显式实例分数。</summary>
        public float DefaultInstanceScore { get; }

        private static void ValidateComponent(int index, int count, string name) { if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(name); }
        private static void ValidateOptionalComponent(int index, int count, string name) { if (index < -1 || index >= count) throw new ArgumentOutOfRangeException(name); }
        private static string? NormalizeOptionalName(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Identifies a batched heatmap tensor layout. / 标识带批次热力图张量布局。</summary>
    public enum PoseHeatmapLayout
    {
        /// <summary>Batch, keypoint, height, width. / 批次、关键点、高度、宽度。</summary>
        Nchw = 0,
        /// <summary>Batch, height, width, keypoint. / 批次、高度、宽度、关键点。</summary>
        Nhwc = 1
    }

    /// <summary>Defines a single-instance Pose heatmap without model-specific sub-pixel heuristics. / 定义不含模型特有亚像素启发式的单实例姿态热力图。</summary>
    public sealed class HeatmapPoseOutputSchema
    {
        /// <summary>Initializes a `[1,K,H,W]` or `[1,H,W,K]` heatmap schema. / 初始化 `[1,K,H,W]` 或 `[1,H,W,K]` 热力图 Schema。</summary>
        public HeatmapPoseOutputSchema(string heatmapOutputName, int keypointCount, PoseHeatmapLayout layout, PoseScoreKind valueKind = PoseScoreKind.Probability, PoseGridMappingMode gridMappingMode = PoseGridMappingMode.HalfPixel, string? instanceScoreOutputName = null, PoseScoreKind instanceScoreKind = PoseScoreKind.Probability, float defaultInstanceScore = 1f)
        {
            if (string.IsNullOrWhiteSpace(heatmapOutputName)) throw new ArgumentException("A heatmap output name is required.", nameof(heatmapOutputName));
            if (keypointCount <= 0) throw new ArgumentOutOfRangeException(nameof(keypointCount));
            if (!Enum.IsDefined(typeof(PoseHeatmapLayout), layout)) throw new ArgumentOutOfRangeException(nameof(layout));
            if (!Enum.IsDefined(typeof(PoseScoreKind), valueKind)) throw new ArgumentOutOfRangeException(nameof(valueKind));
            if (!Enum.IsDefined(typeof(PoseGridMappingMode), gridMappingMode)) throw new ArgumentOutOfRangeException(nameof(gridMappingMode));
            if (!Enum.IsDefined(typeof(PoseScoreKind), instanceScoreKind)) throw new ArgumentOutOfRangeException(nameof(instanceScoreKind));
            if (float.IsNaN(defaultInstanceScore) || float.IsInfinity(defaultInstanceScore) || defaultInstanceScore < 0 || (instanceScoreKind == PoseScoreKind.Probability && defaultInstanceScore > 1)) throw new ArgumentOutOfRangeException(nameof(defaultInstanceScore));
            HeatmapOutputName = heatmapOutputName;
            KeypointCount = keypointCount;
            Layout = layout;
            ValueKind = valueKind;
            GridMappingMode = gridMappingMode;
            InstanceScoreOutputName = string.IsNullOrWhiteSpace(instanceScoreOutputName) ? null : instanceScoreOutputName;
            InstanceScoreKind = instanceScoreKind;
            DefaultInstanceScore = defaultInstanceScore;
            if (string.Equals(HeatmapOutputName, InstanceScoreOutputName, StringComparison.Ordinal)) throw new ArgumentException("Heatmap and instance-score output names must differ.", nameof(instanceScoreOutputName));
        }

        /// <summary>Gets the heatmap tensor name. / 获取热力图张量名称。</summary>
        public string HeatmapOutputName { get; }
        /// <summary>Gets the exact keypoint channel count. / 获取精确关键点通道数。</summary>
        public int KeypointCount { get; }
        /// <summary>Gets the heatmap layout. / 获取热力图布局。</summary>
        public PoseHeatmapLayout Layout { get; }
        /// <summary>Gets heatmap value semantics. / 获取热力图数值语义。</summary>
        public PoseScoreKind ValueKind { get; }
        /// <summary>Gets the tensor-grid to model-pixel mapping rule. / 获取张量网格到模型像素的映射规则。</summary>
        public PoseGridMappingMode GridMappingMode { get; }
        /// <summary>Gets an optional one-value instance score tensor name. / 获取可选单值实例分数张量名称。</summary>
        public string? InstanceScoreOutputName { get; }
        /// <summary>Gets instance score semantics. / 获取实例分数语义。</summary>
        public PoseScoreKind InstanceScoreKind { get; }
        /// <summary>Gets the explicit instance score used when no score tensor exists. / 获取不存在分数张量时使用的显式实例分数。</summary>
        public float DefaultInstanceScore { get; }
    }

    /// <summary>Configures deterministic pairwise OKS suppression. / 配置确定性的成对 OKS 抑制。</summary>
    public sealed class PoseOksOptions
    {
        /// <summary>Initializes OKS suppression options. / 初始化 OKS 抑制选项。</summary>
        public PoseOksOptions(float suppressionThreshold = 0.9f, float minimumKeypointScore = 0f, float areaEpsilon = 1e-7f)
        {
            if (float.IsNaN(suppressionThreshold) || float.IsInfinity(suppressionThreshold) || suppressionThreshold < 0 || suppressionThreshold > 1) throw new ArgumentOutOfRangeException(nameof(suppressionThreshold));
            if (float.IsNaN(minimumKeypointScore) || float.IsInfinity(minimumKeypointScore)) throw new ArgumentOutOfRangeException(nameof(minimumKeypointScore));
            if (float.IsNaN(areaEpsilon) || float.IsInfinity(areaEpsilon) || areaEpsilon <= 0) throw new ArgumentOutOfRangeException(nameof(areaEpsilon));
            SuppressionThreshold = suppressionThreshold;
            MinimumKeypointScore = minimumKeypointScore;
            AreaEpsilon = areaEpsilon;
        }

        /// <summary>Gets the exclusive OKS suppression threshold. / 获取排他 OKS 抑制阈值。</summary>
        public float SuppressionThreshold { get; }
        /// <summary>Gets the inclusive keypoint score used in pairwise similarity. / 获取参与成对相似度的包含边界关键点分数。</summary>
        public float MinimumKeypointScore { get; }
        /// <summary>Gets the positive area denominator epsilon. / 获取面积分母的正 epsilon。</summary>
        public float AreaEpsilon { get; }
    }

    /// <summary>Controls Pose score filtering, coordinate boundaries, bounded output, and optional OKS. / 控制姿态分数筛选、坐标边界、有界输出和可选 OKS。</summary>
    public sealed class PoseDecoderOptions
    {
        /// <summary>Initializes Pose decoder options. / 初始化姿态解码选项。</summary>
        public PoseDecoderOptions(float instanceScoreThreshold = 0.25f, float keypointScoreThreshold = 0f, float visibilityThreshold = 0.5f, PoseBoundaryMode boundaryMode = PoseBoundaryMode.MarkInvalid, PoseInstanceScoreMode instanceScoreMode = PoseInstanceScoreMode.InstanceScore, int maximumCandidates = 3000, int maximumInstances = 300, int maximumKeypoints = 1024, long maximumResultBytes = 256L * 1024 * 1024, PoseOksOptions? oks = null)
        {
            if (float.IsNaN(instanceScoreThreshold) || float.IsInfinity(instanceScoreThreshold) || instanceScoreThreshold < 0) throw new ArgumentOutOfRangeException(nameof(instanceScoreThreshold));
            if (float.IsNaN(keypointScoreThreshold) || float.IsInfinity(keypointScoreThreshold)) throw new ArgumentOutOfRangeException(nameof(keypointScoreThreshold));
            if (float.IsNaN(visibilityThreshold) || float.IsInfinity(visibilityThreshold) || visibilityThreshold < 0 || visibilityThreshold > 1) throw new ArgumentOutOfRangeException(nameof(visibilityThreshold));
            if (!Enum.IsDefined(typeof(PoseBoundaryMode), boundaryMode)) throw new ArgumentOutOfRangeException(nameof(boundaryMode));
            if (!Enum.IsDefined(typeof(PoseInstanceScoreMode), instanceScoreMode)) throw new ArgumentOutOfRangeException(nameof(instanceScoreMode));
            if (maximumCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCandidates));
            if (maximumInstances <= 0 || maximumInstances > maximumCandidates) throw new ArgumentOutOfRangeException(nameof(maximumInstances));
            if (maximumKeypoints <= 0) throw new ArgumentOutOfRangeException(nameof(maximumKeypoints));
            if (maximumResultBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumResultBytes));
            InstanceScoreThreshold = instanceScoreThreshold;
            KeypointScoreThreshold = keypointScoreThreshold;
            VisibilityThreshold = visibilityThreshold;
            BoundaryMode = boundaryMode;
            InstanceScoreMode = instanceScoreMode;
            MaximumCandidates = maximumCandidates;
            MaximumInstances = maximumInstances;
            MaximumKeypoints = maximumKeypoints;
            MaximumResultBytes = maximumResultBytes;
            Oks = oks;
        }

        /// <summary>Gets the inclusive instance score threshold. / 获取包含边界的实例分数阈值。</summary>
        public float InstanceScoreThreshold { get; }
        /// <summary>Gets the inclusive keypoint score validity threshold. / 获取包含边界的关键点分数有效阈值。</summary>
        public float KeypointScoreThreshold { get; }
        /// <summary>Gets the inclusive explicit visibility threshold. / 获取包含边界的显式可见性阈值。</summary>
        public float VisibilityThreshold { get; }
        /// <summary>Gets source-image boundary behavior. / 获取源图边界行为。</summary>
        public PoseBoundaryMode BoundaryMode { get; }
        /// <summary>Gets instance score composition. / 获取实例分数组合方式。</summary>
        public PoseInstanceScoreMode InstanceScoreMode { get; }
        /// <summary>Gets the maximum accepted backend candidates. / 获取接受的最大后端候选数量。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets the maximum returned instances. / 获取最大返回实例数量。</summary>
        public int MaximumInstances { get; }
        /// <summary>Gets the maximum accepted keypoint definitions. / 获取接受的最大关键点定义数量。</summary>
        public int MaximumKeypoints { get; }
        /// <summary>Gets the maximum estimated managed result bytes. / 获取估算的最大托管结果字节数。</summary>
        public long MaximumResultBytes { get; }
        /// <summary>Gets optional pairwise OKS suppression. / 获取可选成对 OKS 抑制。</summary>
        public PoseOksOptions? Oks { get; }
    }

    /// <summary>Provides an explicit pairwise OKS variant for deterministic inference suppression. / 为确定性推理抑制提供显式成对 OKS 变体。</summary>
    public static class PoseOks
    {
        /// <summary>Calculates pairwise OKS using explicit reference area and per-keypoint sigmas; this is not the COCO evaluator. / 使用显式参考面积和逐关键点 sigma 计算成对 OKS；它不是 COCO 评估器。</summary>
        public static float CalculateSimilarity(PoseInstance reference, PoseInstance candidate, IReadOnlyList<PoseKeypointDefinition> definitions, float referenceArea, float minimumKeypointScore = 0f, float areaEpsilon = 1e-7f)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (definitions.Count != reference.Keypoints.Count || definitions.Count != candidate.Keypoints.Count) throw new ArgumentException("OKS definitions must match both Pose instances.", nameof(definitions));
            if (float.IsNaN(referenceArea) || float.IsInfinity(referenceArea) || referenceArea <= 0) throw new ArgumentOutOfRangeException(nameof(referenceArea));
            if (float.IsNaN(minimumKeypointScore) || float.IsInfinity(minimumKeypointScore)) throw new ArgumentOutOfRangeException(nameof(minimumKeypointScore));
            if (float.IsNaN(areaEpsilon) || float.IsInfinity(areaEpsilon) || areaEpsilon <= 0) throw new ArgumentOutOfRangeException(nameof(areaEpsilon));
            double total = 0;
            int included = 0;
            for (int index = 0; index < definitions.Count; index++)
            {
                PoseKeypoint first = reference.Keypoints[index];
                PoseKeypoint second = candidate.Keypoints[index];
                if (!first.IsValid || !second.IsValid || first.Visibility == PoseKeypointVisibility.NotVisible || second.Visibility == PoseKeypointVisibility.NotVisible || first.Score < minimumKeypointScore || second.Score < minimumKeypointScore) continue;
                float? sigmaValue = definitions[index].OksSigma;
                if (!sigmaValue.HasValue) throw new ArgumentException("Every OKS keypoint requires an explicit positive sigma.", nameof(definitions));
                double sigma = sigmaValue.Value;
                double variance = (sigma * 2d) * (sigma * 2d);
                double dx = second.Point.X - first.Point.X;
                double dy = second.Point.Y - first.Point.Y;
                double exponent = ((dx * dx) + (dy * dy)) / variance / (referenceArea + areaEpsilon) / 2d;
                total += Math.Exp(-exponent);
                included++;
            }
            return included == 0 ? 0f : (float)(total / included);
        }
    }

    /// <summary>Decodes strict named direct-coordinate tensors into owned source-space Pose instances. / 将严格命名的直接坐标张量解码为自有源图空间姿态实例。</summary>
    public sealed class DirectPoseDecoder : IVisualDecoder
    {
        /// <summary>Initializes a direct-coordinate Pose decoder. / 初始化直接坐标姿态解码器。</summary>
        public DirectPoseDecoder(DirectPoseOutputSchema schema, PoseTopology topology, PoseDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            Options = options ?? new PoseDecoderOptions();
            if (Schema.KeypointCount != Topology.Keypoints.Count) throw new ArgumentException("Schema and topology keypoint counts must match.", nameof(topology));
            if (Schema.KeypointCount > Options.MaximumKeypoints) throw new ArgumentException("Topology exceeds the configured keypoint bound.", nameof(topology));
            if (Options.InstanceScoreMode == PoseInstanceScoreMode.InstanceScoreTimesMeanKeypointScore && (Schema.ScoreComponentIndex < 0 || Schema.KeypointScoreKind != PoseScoreKind.Probability)) throw new ArgumentException("Mean keypoint score composition requires an explicit probability component.", nameof(options));
            if (Options.Oks != null)
            {
                if (Schema.BoxesOutputName == null) throw new ArgumentException("OKS suppression requires explicit boxes for reference area.", nameof(schema));
                for (int index = 0; index < Topology.Keypoints.Count; index++) if (!Topology.Keypoints[index].OksSigma.HasValue) throw new ArgumentException("OKS suppression requires a sigma for every keypoint.", nameof(topology));
            }
        }

        /// <inheritdoc />
        /// <remarks>Direct Pose decoder task is immutable. / 直接姿态解码器任务不可变。</remarks>
        public VisualTaskId Task => VisualTaskId.PoseEstimation;
        /// <summary>Gets exact output semantics. / 获取精确输出语义。</summary>
        public DirectPoseOutputSchema Schema { get; }
        /// <summary>Gets immutable keypoint topology. / 获取不可变关键点拓扑。</summary>
        public PoseTopology Topology { get; }
        /// <summary>Gets bounded filtering and OKS options. / 获取有界筛选与 OKS 选项。</summary>
        public PoseDecoderOptions Options { get; }

        /// <inheritdoc />
        /// <remarks>Decoding borrows backend arrays only for this call and returns owned managed results. / 解码仅在本次调用借用后端数组，并返回自有托管结果。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw Failure(context, VisualErrorCodes.DecodeFailed, "Direct Pose decoder currently requires batch size one.", Schema.KeypointsOutputName);
            int expectedOutputs = 1 + (Schema.BoxesOutputName == null ? 0 : 1) + (Schema.InstanceScoresOutputName == null ? 0 : 1);
            if (context.Outputs.Count != expectedOutputs) throw Failure(context, VisualErrorCodes.TensorInvalid, "Direct Pose outputs contain missing or undeclared tensors.", Schema.KeypointsOutputName);
            ITensor keypointTensor = Required(context, Schema.KeypointsOutputName);
            TensorShape shape = keypointTensor.Shape;
            if (shape.Rank != 4 || shape[0] != 1 || shape[2] != Schema.KeypointCount || shape[3] != Schema.ComponentCount) throw Failure(context, VisualErrorCodes.TensorInvalid, "Direct Pose keypoints must match [1,candidates,keypoints,components].", Schema.KeypointsOutputName, shape.ToString());
            int candidateCount = CheckedDimension(shape[1], context, Schema.KeypointsOutputName);
            if (candidateCount > Options.MaximumCandidates) throw Failure(context, VisualErrorCodes.DecodeFailed, "Direct Pose candidate count exceeds the configured bound.", Schema.KeypointsOutputName);
            long expectedLength = checked((long)candidateCount * Schema.KeypointCount * Schema.ComponentCount);
            if (keypointTensor.Length != expectedLength) throw Failure(context, VisualErrorCodes.TensorInvalid, "Direct Pose keypoint element count is inconsistent with its shape.", Schema.KeypointsOutputName);
            EnsureResultBound(context, candidateCount, Schema.KeypointCount, keypointTensor);
            float[] keypointValues = VisualTensorReader.ReadFiniteScores(keypointTensor, context.Profile.ProfileId, Schema.KeypointsOutputName);
            float[]? boxes = null;
            float[]? instanceScores = null;
            if (Schema.BoxesOutputName != null)
            {
                ITensor tensor = Required(context, Schema.BoxesOutputName);
                ValidateCandidateTensor(context, tensor, Schema.BoxesOutputName, candidateCount, 4);
                boxes = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Schema.BoxesOutputName);
            }
            if (Schema.InstanceScoresOutputName != null)
            {
                ITensor tensor = Required(context, Schema.InstanceScoresOutputName);
                ValidateCandidateTensor(context, tensor, Schema.InstanceScoresOutputName, candidateCount, 1);
                instanceScores = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Schema.InstanceScoresOutputName);
            }

            var candidates = new List<PoseInstance>(candidateCount);
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                int candidateOffset = checked(candidateIndex * Schema.KeypointCount * Schema.ComponentCount);
                var points = new List<PoseKeypoint>(Schema.KeypointCount);
                double keypointScoreTotal = 0;
                for (int keypointIndex = 0; keypointIndex < Schema.KeypointCount; keypointIndex++)
                {
                    int offset = candidateOffset + (keypointIndex * Schema.ComponentCount);
                    float x = keypointValues[offset + Schema.XComponentIndex];
                    float y = keypointValues[offset + Schema.YComponentIndex];
                    float score = Schema.ScoreComponentIndex < 0 ? 1f : ValidateScore(keypointValues[offset + Schema.ScoreComponentIndex], Schema.KeypointScoreKind, context, Schema.KeypointsOutputName, "keypoint-score");
                    PoseKeypointVisibility visibility = PoseKeypointVisibility.Unknown;
                    if (Schema.VisibilityComponentIndex >= 0)
                    {
                        float value = ValidateScore(keypointValues[offset + Schema.VisibilityComponentIndex], PoseScoreKind.Probability, context, Schema.KeypointsOutputName, "visibility");
                        visibility = value >= Options.VisibilityThreshold ? PoseKeypointVisibility.Visible : PoseKeypointVisibility.NotVisible;
                    }
                    PointF modelPoint = PoseDecoderGeometry.ToModelPoint(x, y, Schema.CoordinateSpace, Schema.TensorGridSize, Schema.GridMappingMode, context.Input.ModelSize);
                    MappedPosePoint mapped = PoseDecoderGeometry.ToSourcePoint(modelPoint, context.Input.Transform, Options.BoundaryMode);
                    bool valid = score >= Options.KeypointScoreThreshold && visibility != PoseKeypointVisibility.NotVisible && mapped.BoundaryValid;
                    points.Add(new PoseKeypoint(keypointIndex, mapped.Point, score, visibility, valid));
                    keypointScoreTotal += score;
                }

                float instanceScore = instanceScores == null ? Schema.DefaultInstanceScore : ValidateScore(instanceScores[candidateIndex], Schema.InstanceScoreKind, context, Schema.InstanceScoresOutputName!, "instance-score");
                if (instanceScore < 0) throw Failure(context, VisualErrorCodes.DecodeFailed, "Pose instance scores must be non-negative.", Schema.InstanceScoresOutputName ?? Schema.KeypointsOutputName);
                if (Options.InstanceScoreMode == PoseInstanceScoreMode.InstanceScoreTimesMeanKeypointScore) instanceScore *= (float)(keypointScoreTotal / Schema.KeypointCount);
                if (instanceScore < Options.InstanceScoreThreshold) continue;
                RectangleF? box = boxes == null ? (RectangleF?)null : DecodeBox(boxes, candidateIndex * 4, context);
                candidates.Add(new PoseInstance(candidateIndex, instanceScore, points, box, null, null));
            }

            candidates.Sort(CompareInstances);
            List<PoseInstance> kept = Suppress(candidates, context);
            return new PoseEstimationResult(Topology, kept, context.Input.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private List<PoseInstance> Suppress(List<PoseInstance> ordered, VisualDecodeContext context)
        {
            var kept = new List<PoseInstance>(Math.Min(ordered.Count, Options.MaximumInstances));
            for (int candidateIndex = 0; candidateIndex < ordered.Count && kept.Count < Options.MaximumInstances; candidateIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                PoseInstance candidate = ordered[candidateIndex];
                bool suppressed = false;
                if (Options.Oks != null)
                {
                    for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                    {
                        PoseInstance reference = kept[keptIndex];
                        RectangleF box = reference.BoundingBox!.Value;
                        float similarity = PoseOks.CalculateSimilarity(reference, candidate, Topology.Keypoints, checked(box.Width * box.Height), Options.Oks.MinimumKeypointScore, Options.Oks.AreaEpsilon);
                        if (similarity > Options.Oks.SuppressionThreshold) { suppressed = true; break; }
                    }
                }
                if (!suppressed) kept.Add(candidate);
            }
            return kept;
        }

        private RectangleF DecodeBox(float[] values, int offset, VisualDecodeContext context)
        {
            float first = values[offset]; float second = values[offset + 1]; float third = values[offset + 2]; float fourth = values[offset + 3];
            if (Schema.NormalizedBoxes) { first *= context.Input.ModelSize.Width; third *= context.Input.ModelSize.Width; second *= context.Input.ModelSize.Height; fourth *= context.Input.ModelSize.Height; }
            float left; float top; float right; float bottom;
            if (Schema.BoxFormat == DetectionBoxFormat.Xyxy) { left = first; top = second; right = third; bottom = fourth; }
            else if (Schema.BoxFormat == DetectionBoxFormat.Xywh) { left = first; top = second; right = first + third; bottom = second + fourth; }
            else { left = first - (third / 2f); top = second - (fourth / 2f); right = first + (third / 2f); bottom = second + (fourth / 2f); }
            if (right <= left || bottom <= top) throw Failure(context, VisualErrorCodes.DecodeFailed, "Pose box must have positive width and height.", Schema.BoxesOutputName!);
            RectangleF source = context.Input.Transform.ClipToSource(context.Input.Transform.ToSource(new RectangleF(left, top, right - left, bottom - top)));
            if (source.Width <= 0 || source.Height <= 0) throw Failure(context, VisualErrorCodes.DecodeFailed, "Pose box does not intersect the source image.", Schema.BoxesOutputName!);
            return source;
        }

        private void EnsureResultBound(VisualDecodeContext context, int candidates, int keypoints, ITensor tensor)
        {
            try
            {
                long convertedTensor = tensor.ElementType == TensorElementType.Float64 ? checked(tensor.Length * sizeof(float)) : 0;
                long estimate = checked(convertedTensor + ((long)candidates * keypoints * 64L) + ((long)candidates * 192L));
                if (estimate > Options.MaximumResultBytes) throw Failure(context, VisualErrorCodes.DecodeFailed, "Estimated Pose result memory exceeds the configured bound.", Schema.KeypointsOutputName, "estimatedBytes=" + estimate);
            }
            catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Pose result memory estimation overflowed.", Schema.KeypointsOutputName, exception.ToString(), exception); }
        }

        private static int CompareInstances(PoseInstance left, PoseInstance right)
        {
            int score = right.Score.CompareTo(left.Score);
            return score != 0 ? score : left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private static void ValidateCandidateTensor(VisualDecodeContext context, ITensor tensor, string name, int candidates, int fields)
        {
            TensorShape shape = tensor.Shape;
            bool valid = fields == 1
                ? (shape.Rank == 1 && shape[0] == candidates) || (shape.Rank == 2 && shape[0] == 1 && shape[1] == candidates)
                : (shape.Rank == 2 && shape[0] == candidates && shape[1] == fields) || (shape.Rank == 3 && shape[0] == 1 && shape[1] == candidates && shape[2] == fields);
            if (!valid || tensor.Length != (long)candidates * fields) throw Failure(context, VisualErrorCodes.TensorInvalid, "Pose companion output shape is incompatible with the keypoint candidates.", name, shape.ToString());
        }

        private static int CheckedDimension(long value, VisualDecodeContext context, string tensorName)
        {
            try { return checked((int)value); }
            catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "Pose tensor dimension exceeds Int32 bounds.", tensorName, value.ToString(System.Globalization.CultureInfo.InvariantCulture), exception); }
        }

        internal static ITensor Required(VisualDecodeContext context, string name)
        {
            try { return context.Outputs.GetRequired(name); }
            catch (KeyNotFoundException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "A required Pose output tensor is missing.", name, null, exception); }
        }

        internal static float ValidateScore(float value, PoseScoreKind kind, VisualDecodeContext context, string tensorName, string field)
        {
            if (kind == PoseScoreKind.Probability && (value < 0 || value > 1)) throw Failure(context, VisualErrorCodes.DecodeFailed, "Pose probability values must be in [0,1].", tensorName, field);
            return value;
        }

        internal static VisualException Failure(VisualDecodeContext context, string code, string message, string tensorName, string? details = null, Exception? exception = null)
            => new VisualException(code, message, exception, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: details);
    }

    /// <summary>Decodes one strict Pose heatmap into a single owned source-space instance. / 将一个严格姿态热力图解码为单个自有源图空间实例。</summary>
    public sealed class HeatmapPoseDecoder : IVisualDecoder
    {
        /// <summary>Initializes a heatmap Pose decoder. / 初始化热力图姿态解码器。</summary>
        public HeatmapPoseDecoder(HeatmapPoseOutputSchema schema, PoseTopology topology, PoseDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            Options = options ?? new PoseDecoderOptions(maximumCandidates: 1, maximumInstances: 1);
            if (Schema.KeypointCount != Topology.Keypoints.Count) throw new ArgumentException("Schema and topology keypoint counts must match.", nameof(topology));
            if (Schema.KeypointCount > Options.MaximumKeypoints) throw new ArgumentException("Topology exceeds the configured keypoint bound.", nameof(topology));
            if (Options.MaximumCandidates != 1 || Options.MaximumInstances != 1) throw new ArgumentException("A heatmap decoder produces exactly one candidate and requires bounds of one.", nameof(options));
            if (Options.Oks != null) throw new ArgumentException("Single-instance heatmap decoding cannot apply pairwise OKS suppression.", nameof(options));
            if (Options.InstanceScoreMode == PoseInstanceScoreMode.InstanceScoreTimesMeanKeypointScore && Schema.ValueKind != PoseScoreKind.Probability) throw new ArgumentException("Mean heatmap score composition requires probability heatmaps.", nameof(options));
        }

        /// <inheritdoc />
        /// <remarks>Heatmap Pose decoder task is immutable. / 热力图姿态解码器任务不可变。</remarks>
        public VisualTaskId Task => VisualTaskId.PoseEstimation;
        /// <summary>Gets exact heatmap semantics. / 获取精确热力图语义。</summary>
        public HeatmapPoseOutputSchema Schema { get; }
        /// <summary>Gets immutable keypoint topology. / 获取不可变关键点拓扑。</summary>
        public PoseTopology Topology { get; }
        /// <summary>Gets bounded filtering options. / 获取有界筛选选项。</summary>
        public PoseDecoderOptions Options { get; }

        /// <inheritdoc />
        /// <remarks>Peak ties retain the smallest row-major index and no activation or sub-pixel correction is applied. / 峰值同分保留最小行优先索引，且不应用激活或亚像素修正。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw DirectPoseDecoder.Failure(context, VisualErrorCodes.DecodeFailed, "Heatmap Pose decoder currently requires batch size one.", Schema.HeatmapOutputName);
            int expectedOutputs = Schema.InstanceScoreOutputName == null ? 1 : 2;
            if (context.Outputs.Count != expectedOutputs) throw DirectPoseDecoder.Failure(context, VisualErrorCodes.TensorInvalid, "Heatmap Pose outputs contain missing or undeclared tensors.", Schema.HeatmapOutputName);
            ITensor tensor = DirectPoseDecoder.Required(context, Schema.HeatmapOutputName);
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 4 || shape[0] != 1) throw DirectPoseDecoder.Failure(context, VisualErrorCodes.TensorInvalid, "Pose heatmap must have rank four and batch one.", Schema.HeatmapOutputName, shape.ToString());
            int keypoints; int height; int width;
            try
            {
                if (Schema.Layout == PoseHeatmapLayout.Nchw) { keypoints = checked((int)shape[1]); height = checked((int)shape[2]); width = checked((int)shape[3]); }
                else { height = checked((int)shape[1]); width = checked((int)shape[2]); keypoints = checked((int)shape[3]); }
            }
            catch (OverflowException exception) { throw DirectPoseDecoder.Failure(context, VisualErrorCodes.TensorInvalid, "Pose heatmap dimensions exceed Int32 bounds.", Schema.HeatmapOutputName, shape.ToString(), exception); }
            if (keypoints != Schema.KeypointCount || height <= 0 || width <= 0 || tensor.Length != (long)keypoints * height * width) throw DirectPoseDecoder.Failure(context, VisualErrorCodes.TensorInvalid, "Pose heatmap shape is incompatible with its schema.", Schema.HeatmapOutputName, shape.ToString());
            long estimate;
            try { estimate = checked((tensor.ElementType == TensorElementType.Float64 ? tensor.Length * sizeof(float) : 0) + ((long)keypoints * 64L) + 256L); }
            catch (OverflowException exception) { throw DirectPoseDecoder.Failure(context, VisualErrorCodes.DecodeFailed, "Pose heatmap memory estimation overflowed.", Schema.HeatmapOutputName, null, exception); }
            if (estimate > Options.MaximumResultBytes) throw DirectPoseDecoder.Failure(context, VisualErrorCodes.DecodeFailed, "Estimated Pose result memory exceeds the configured bound.", Schema.HeatmapOutputName, "estimatedBytes=" + estimate);
            float[] values = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Schema.HeatmapOutputName);
            if (Schema.ValueKind == PoseScoreKind.Probability)
            {
                // Validate once before the peak-search hot loop so every failure retains the full Visual context. / 在峰值搜索热循环前一次性校验，使每个失败都保留完整 Visual 上下文。
                for (int index = 0; index < values.Length; index++)
                {
                    if ((index & 4095) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                    DirectPoseDecoder.ValidateScore(values[index], Schema.ValueKind, context, Schema.HeatmapOutputName, "heatmap-value[" + index + "]");
                }
            }
            var points = new List<PoseKeypoint>(keypoints);
            double scoreTotal = 0;
            int plane = checked(height * width);
            for (int keypointIndex = 0; keypointIndex < keypoints; keypointIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                int bestIndex = 0;
                float bestValue = ReadHeatmap(values, keypointIndex, 0, 0, keypoints, height, width, plane);
                for (int position = 1; position < plane; position++)
                {
                    int y = position / width;
                    int x = position - (y * width);
                    float value = ReadHeatmap(values, keypointIndex, y, x, keypoints, height, width, plane);
                    if (value > bestValue) { bestValue = value; bestIndex = position; }
                }
                bestValue = DirectPoseDecoder.ValidateScore(bestValue, Schema.ValueKind, context, Schema.HeatmapOutputName, "heatmap-peak");
                int bestY = bestIndex / width;
                int bestX = bestIndex - (bestY * width);
                PointF modelPoint = PoseDecoderGeometry.ToModelPoint(bestX, bestY, PoseCoordinateSpace.TensorGrid, new VisualSize(width, height), Schema.GridMappingMode, context.Input.ModelSize);
                MappedPosePoint mapped = PoseDecoderGeometry.ToSourcePoint(modelPoint, context.Input.Transform, Options.BoundaryMode);
                bool valid = bestValue >= Options.KeypointScoreThreshold && mapped.BoundaryValid;
                points.Add(new PoseKeypoint(keypointIndex, mapped.Point, bestValue, PoseKeypointVisibility.Unknown, valid));
                scoreTotal += bestValue;
            }

            float instanceScore = Schema.DefaultInstanceScore;
            if (Schema.InstanceScoreOutputName != null)
            {
                ITensor scoreTensor = DirectPoseDecoder.Required(context, Schema.InstanceScoreOutputName);
                if (scoreTensor.Length != 1 || !((scoreTensor.Shape.Rank == 1 && scoreTensor.Shape[0] == 1) || scoreTensor.Shape.Rank == 0)) throw DirectPoseDecoder.Failure(context, VisualErrorCodes.TensorInvalid, "Heatmap instance score must contain exactly one value.", Schema.InstanceScoreOutputName, scoreTensor.Shape.ToString());
                float[] scoreValues = VisualTensorReader.ReadFiniteScores(scoreTensor, context.Profile.ProfileId, Schema.InstanceScoreOutputName);
                instanceScore = DirectPoseDecoder.ValidateScore(scoreValues[0], Schema.InstanceScoreKind, context, Schema.InstanceScoreOutputName, "instance-score");
            }
            if (instanceScore < 0) throw DirectPoseDecoder.Failure(context, VisualErrorCodes.DecodeFailed, "Pose instance scores must be non-negative.", Schema.InstanceScoreOutputName ?? Schema.HeatmapOutputName);
            if (Options.InstanceScoreMode == PoseInstanceScoreMode.InstanceScoreTimesMeanKeypointScore) instanceScore *= (float)(scoreTotal / keypoints);
            var instances = new List<PoseInstance>(1);
            if (instanceScore >= Options.InstanceScoreThreshold) instances.Add(new PoseInstance(0, instanceScore, points, null, null, null));
            return new PoseEstimationResult(Topology, instances, context.Input.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private float ReadHeatmap(float[] values, int keypoint, int y, int x, int keypoints, int height, int width, int plane)
        {
            int index = Schema.Layout == PoseHeatmapLayout.Nchw ? (keypoint * plane) + (y * width) + x : ((y * width) + x) * keypoints + keypoint;
            return values[index];
        }
    }

    internal readonly struct MappedPosePoint
    {
        public MappedPosePoint(PointF point, bool boundaryValid) { Point = point; BoundaryValid = boundaryValid; }
        public PointF Point { get; }
        public bool BoundaryValid { get; }
    }

    internal static class PoseDecoderGeometry
    {
        public static PointF ToModelPoint(float x, float y, PoseCoordinateSpace space, VisualSize? gridSize, PoseGridMappingMode mapping, VisualSize modelSize)
        {
            if (space == PoseCoordinateSpace.ModelPixels) return new PointF(x, y);
            if (space == PoseCoordinateSpace.Normalized)
            {
                return mapping == PoseGridMappingMode.AlignCorners
                    ? new PointF(x * Math.Max(0, modelSize.Width - 1), y * Math.Max(0, modelSize.Height - 1))
                    : new PointF(x * modelSize.Width, y * modelSize.Height);
            }
            if (!gridSize.HasValue) throw new ArgumentException("Tensor-grid coordinates require a grid size.", nameof(gridSize));
            VisualSize grid = gridSize.Value;
            if (mapping == PoseGridMappingMode.AlignCorners)
            {
                float mappedX = grid.Width == 1 ? 0 : x * (modelSize.Width - 1f) / (grid.Width - 1f);
                float mappedY = grid.Height == 1 ? 0 : y * (modelSize.Height - 1f) / (grid.Height - 1f);
                return new PointF(mappedX, mappedY);
            }
            return new PointF(((x + 0.5f) * modelSize.Width / grid.Width) - 0.5f, ((y + 0.5f) * modelSize.Height / grid.Height) - 0.5f);
        }

        public static MappedPosePoint ToSourcePoint(PointF modelPoint, ImageTransform transform, PoseBoundaryMode mode)
        {
            PointF source = transform.ToSource(modelPoint);
            float maximumX = transform.SourceSize.Width - 1f;
            float maximumY = transform.SourceSize.Height - 1f;
            bool inside = source.X >= 0 && source.X <= maximumX && source.Y >= 0 && source.Y <= maximumY;
            if (mode == PoseBoundaryMode.Clip)
            {
                float x = Math.Max(0, Math.Min(maximumX, source.X));
                float y = Math.Max(0, Math.Min(maximumY, source.Y));
                return new MappedPosePoint(new PointF(x, y), true);
            }
            return new MappedPosePoint(source, mode == PoseBoundaryMode.Preserve || inside);
        }
    }
}
