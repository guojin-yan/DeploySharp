using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies model-defined keypoint visibility without pretending confidence is ground-truth visibility. / 标识模型定义的关键点可见性，且不把置信度冒充为真实标注可见性。</summary>
    public enum PoseKeypointVisibility
    {
        /// <summary>The model does not provide a visibility field. / 模型未提供可见性字段。</summary>
        Unknown = 0,
        /// <summary>The model explicitly reports that the keypoint is not visible. / 模型明确报告关键点不可见。</summary>
        NotVisible = 1,
        /// <summary>The model explicitly reports that the keypoint is visible. / 模型明确报告关键点可见。</summary>
        Visible = 2
    }

    /// <summary>Identifies the coordinate space produced by a direct pose tensor. / 标识直接姿态张量输出的坐标空间。</summary>
    public enum PoseCoordinateSpace
    {
        /// <summary>Coordinates are model-input pixel-center coordinates. / 坐标为模型输入像素中心坐标。</summary>
        ModelPixels = 0,
        /// <summary>Coordinates are normalized to the model input. / 坐标相对于模型输入归一化。</summary>
        Normalized = 1,
        /// <summary>Coordinates are positions in an explicitly sized tensor grid. / 坐标位于显式尺寸的张量网格中。</summary>
        TensorGrid = 2
    }

    /// <summary>Identifies the explicit rule for mapping normalized or grid positions to model pixels. / 标识将归一化或网格位置映射到模型像素的显式规则。</summary>
    public enum PoseGridMappingMode
    {
        /// <summary>Use half-pixel centers for grids and half-open size scaling for normalized coordinates. / 网格使用半像素中心，归一化坐标使用半开尺寸缩放。</summary>
        HalfPixel = 0,
        /// <summary>Align the first and last positions with the first and last model pixel centers. / 将首尾位置与首尾模型像素中心对齐。</summary>
        AlignCorners = 1
    }

    /// <summary>Controls how source-space keypoints outside the image are represented. / 控制如何表示源图范围外的关键点。</summary>
    public enum PoseBoundaryMode
    {
        /// <summary>Preserve the coordinate and its score-defined validity. / 保留坐标及由分数定义的有效性。</summary>
        Preserve = 0,
        /// <summary>Clip the coordinate to the nearest source pixel center. / 将坐标裁剪到最近的源图像素中心。</summary>
        Clip = 1,
        /// <summary>Preserve the coordinate but mark it invalid. / 保留坐标但标记为无效。</summary>
        MarkInvalid = 2
    }

    /// <summary>Identifies whether a tensor score is a probability or an unactivated model-defined value. / 标识张量分数是概率还是未经激活的模型定义值。</summary>
    public enum PoseScoreKind
    {
        /// <summary>Values must remain in the inclusive range [0,1]. / 数值必须位于包含端点的 [0,1] 范围。</summary>
        Probability = 0,
        /// <summary>Any finite value is accepted without implicit activation. / 接受任意有限值且不隐式激活。</summary>
        Raw = 1
    }

    /// <summary>Identifies how the final instance score is formed. / 标识最终实例分数的组合方式。</summary>
    public enum PoseInstanceScoreMode
    {
        /// <summary>Use the declared instance score directly. / 直接使用声明的实例分数。</summary>
        InstanceScore = 0,
        /// <summary>Multiply the instance score by the mean keypoint probability. / 将实例分数乘以关键点概率均值。</summary>
        InstanceScoreTimesMeanKeypointScore = 1
    }

    /// <summary>Stores an RGB color used for Pose labels or skeleton rendering metadata. / 存储用于姿态标签或骨架渲染元数据的 RGB 颜色。</summary>
    public readonly struct PoseColor : IEquatable<PoseColor>
    {
        /// <summary>Initializes an RGB color. / 初始化 RGB 颜色。</summary>
        public PoseColor(byte red, byte green, byte blue) { Red = red; Green = green; Blue = blue; }
        /// <summary>Gets the red component. / 获取红色分量。</summary>
        public byte Red { get; }
        /// <summary>Gets the green component. / 获取绿色分量。</summary>
        public byte Green { get; }
        /// <summary>Gets the blue component. / 获取蓝色分量。</summary>
        public byte Blue { get; }
        /// <inheritdoc />
        /// <remarks>Compares all RGB components. / 比较全部 RGB 分量。</remarks>
        public bool Equals(PoseColor other) => Red == other.Red && Green == other.Green && Blue == other.Blue;
        /// <inheritdoc />
        /// <remarks>Compares an object by RGB components. / 按 RGB 分量比较对象。</remarks>
        public override bool Equals(object? obj) => obj is PoseColor other && Equals(other);
        /// <inheritdoc />
        /// <remarks>Computes a component hash code. / 计算分量哈希码。</remarks>
        public override int GetHashCode() => (Red << 16) | (Green << 8) | Blue;
        /// <summary>Compares two colors for equality. / 比较两个颜色是否相等。</summary>
        public static bool operator ==(PoseColor left, PoseColor right) => left.Equals(right);
        /// <summary>Compares two colors for inequality. / 比较两个颜色是否不相等。</summary>
        public static bool operator !=(PoseColor left, PoseColor right) => !left.Equals(right);
    }

    /// <summary>Defines one stable keypoint label, mirror relationship, palette color, and optional OKS sigma. / 定义一个稳定关键点标签、镜像关系、调色板颜色及可选 OKS sigma。</summary>
    public sealed class PoseKeypointDefinition
    {
        /// <summary>Initializes a keypoint definition. / 初始化关键点定义。</summary>
        public PoseKeypointDefinition(int index, string label, int? mirroredIndex = null, PoseColor? color = null, float? oksSigma = null)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A keypoint label is required.", nameof(label));
            if (mirroredIndex.HasValue && (mirroredIndex.Value < 0 || mirroredIndex.Value == index)) throw new ArgumentOutOfRangeException(nameof(mirroredIndex));
            if (oksSigma.HasValue && (float.IsNaN(oksSigma.Value) || float.IsInfinity(oksSigma.Value) || oksSigma.Value <= 0)) throw new ArgumentOutOfRangeException(nameof(oksSigma));
            Index = index;
            Label = label;
            MirroredIndex = mirroredIndex;
            Color = color;
            OksSigma = oksSigma;
        }

        /// <summary>Gets the zero-based stable keypoint index. / 获取从零开始的稳定关键点索引。</summary>
        public int Index { get; }
        /// <summary>Gets the display label. / 获取显示标签。</summary>
        public string Label { get; }
        /// <summary>Gets an optional mirrored keypoint index. / 获取可选镜像关键点索引。</summary>
        public int? MirroredIndex { get; }
        /// <summary>Gets an optional display color. / 获取可选显示颜色。</summary>
        public PoseColor? Color { get; }
        /// <summary>Gets an optional model/profile-specific OKS sigma. / 获取可选的模型或 Profile 特定 OKS sigma。</summary>
        public float? OksSigma { get; }
    }

    /// <summary>Defines one undirected edge in a Pose skeleton. / 定义姿态骨架中的一条无向边。</summary>
    public sealed class PoseSkeletonEdge
    {
        /// <summary>Initializes a skeleton edge. / 初始化骨架边。</summary>
        public PoseSkeletonEdge(int firstKeypointIndex, int secondKeypointIndex, PoseColor? color = null)
        {
            if (firstKeypointIndex < 0) throw new ArgumentOutOfRangeException(nameof(firstKeypointIndex));
            if (secondKeypointIndex < 0 || secondKeypointIndex == firstKeypointIndex) throw new ArgumentOutOfRangeException(nameof(secondKeypointIndex));
            FirstKeypointIndex = firstKeypointIndex;
            SecondKeypointIndex = secondKeypointIndex;
            Color = color;
        }

        /// <summary>Gets the first keypoint index. / 获取第一个关键点索引。</summary>
        public int FirstKeypointIndex { get; }
        /// <summary>Gets the second keypoint index. / 获取第二个关键点索引。</summary>
        public int SecondKeypointIndex { get; }
        /// <summary>Gets an optional edge display color. / 获取可选边显示颜色。</summary>
        public PoseColor? Color { get; }
    }

    /// <summary>Stores and validates immutable keypoint definitions and skeleton edges. / 存储并验证不可变关键点定义和骨架边。</summary>
    public sealed class PoseTopology
    {
        private readonly IReadOnlyList<PoseKeypointDefinition> _keypoints;
        private readonly IReadOnlyList<PoseSkeletonEdge> _edges;

        /// <summary>Initializes a Pose topology with contiguous keypoint indices. / 使用连续关键点索引初始化姿态拓扑。</summary>
        public PoseTopology(IEnumerable<PoseKeypointDefinition> keypoints, IEnumerable<PoseSkeletonEdge>? edges = null)
        {
            if (keypoints == null) throw new ArgumentNullException(nameof(keypoints));
            var definitions = new List<PoseKeypointDefinition>();
            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (PoseKeypointDefinition definition in keypoints)
            {
                if (definition == null) throw new ArgumentException("Keypoint definitions cannot contain null.", nameof(keypoints));
                if (definition.Index != definitions.Count) throw new ArgumentException("Keypoint indices must be unique, contiguous, and ordered from zero.", nameof(keypoints));
                if (!labels.Add(definition.Label)) throw new ArgumentException("Keypoint labels must be unique.", nameof(keypoints));
                definitions.Add(definition);
            }
            if (definitions.Count == 0) throw new ArgumentException("At least one keypoint definition is required.", nameof(keypoints));
            for (int index = 0; index < definitions.Count; index++)
            {
                int? mirror = definitions[index].MirroredIndex;
                if (!mirror.HasValue) continue;
                if (mirror.Value >= definitions.Count || definitions[mirror.Value].MirroredIndex != index) throw new ArgumentException("Mirrored keypoint relationships must exist and be symmetric.", nameof(keypoints));
            }

            var edgeList = new List<PoseSkeletonEdge>();
            var edgeKeys = new HashSet<long>();
            if (edges != null)
            {
                foreach (PoseSkeletonEdge edge in edges)
                {
                    if (edge == null) throw new ArgumentException("Skeleton edges cannot contain null.", nameof(edges));
                    if (edge.FirstKeypointIndex >= definitions.Count || edge.SecondKeypointIndex >= definitions.Count) throw new ArgumentException("Skeleton edge indices must reference defined keypoints.", nameof(edges));
                    int low = Math.Min(edge.FirstKeypointIndex, edge.SecondKeypointIndex);
                    int high = Math.Max(edge.FirstKeypointIndex, edge.SecondKeypointIndex);
                    long key = ((long)low << 32) | (uint)high;
                    if (!edgeKeys.Add(key)) throw new ArgumentException("Skeleton edges must be unique regardless of direction.", nameof(edges));
                    edgeList.Add(edge);
                }
            }
            _keypoints = definitions.AsReadOnly();
            _edges = edgeList.AsReadOnly();
        }

        /// <summary>Gets ordered keypoint definitions. / 获取有序关键点定义。</summary>
        public IReadOnlyList<PoseKeypointDefinition> Keypoints => _keypoints;
        /// <summary>Gets ordered skeleton edges. / 获取有序骨架边。</summary>
        public IReadOnlyList<PoseSkeletonEdge> Edges => _edges;
    }

    /// <summary>Represents one source-space keypoint with model-defined score and explicit validity. / 表示一个带模型定义分数和显式有效性的源图空间关键点。</summary>
    public sealed class PoseKeypoint
    {
        /// <summary>Initializes a source-space keypoint. Coordinates use source pixel-center units. / 初始化源图空间关键点；坐标使用源图像素中心单位。</summary>
        public PoseKeypoint(int index, PointF point, float score, PoseKeypointVisibility visibility, bool isValid)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (float.IsNaN(point.X) || float.IsInfinity(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.Y)) throw new ArgumentException("Keypoint coordinates must be finite.", nameof(point));
            if (float.IsNaN(score) || float.IsInfinity(score)) throw new ArgumentOutOfRangeException(nameof(score));
            if (!Enum.IsDefined(typeof(PoseKeypointVisibility), visibility)) throw new ArgumentOutOfRangeException(nameof(visibility));
            Index = index;
            Point = point;
            Score = score;
            Visibility = visibility;
            IsValid = isValid;
        }

        /// <summary>Gets the keypoint definition index. / 获取关键点定义索引。</summary>
        public int Index { get; }
        /// <summary>Gets the source-space pixel-center coordinate. / 获取源图空间像素中心坐标。</summary>
        public PointF Point { get; }
        /// <summary>Gets the finite model-defined score without implicit activation. / 获取不经隐式激活的有限模型定义分数。</summary>
        public float Score { get; }
        /// <summary>Gets model-declared visibility or Unknown. / 获取模型声明的可见性或 Unknown。</summary>
        public PoseKeypointVisibility Visibility { get; }
        /// <summary>Gets whether score, visibility, and configured boundary rules accept this keypoint. / 获取分数、可见性和配置的边界规则是否接受此关键点。</summary>
        public bool IsValid { get; }
    }

    /// <summary>Represents one ordered Pose instance with owned keypoints and an optional source-space box. / 表示一个含自有关键点和可选源图空间边界框的有序姿态实例。</summary>
    public sealed class PoseInstance
    {
        private readonly IReadOnlyList<PoseKeypoint> _keypoints;

        /// <summary>Initializes a Pose instance and defensively copies keypoints. / 初始化姿态实例并防御性复制关键点。</summary>
        public PoseInstance(int sourceIndex, float score, IEnumerable<PoseKeypoint> keypoints, RectangleF? boundingBox = null, int? classIndex = null, string? externalId = null)
            : this(sourceIndex, score, CopyKeypoints(keypoints), boundingBox, classIndex, externalId)
        {
        }

        internal PoseInstance(int sourceIndex, float score, List<PoseKeypoint> keypoints, RectangleF? boundingBox, int? classIndex, string? externalId)
        {
            if (sourceIndex < 0) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            if (float.IsNaN(score) || float.IsInfinity(score) || score < 0) throw new ArgumentOutOfRangeException(nameof(score));
            if (keypoints == null || keypoints.Count == 0) throw new ArgumentException("A Pose instance requires keypoints.", nameof(keypoints));
            for (int index = 0; index < keypoints.Count; index++) if (keypoints[index] == null || keypoints[index].Index != index) throw new ArgumentException("Pose keypoints must be non-null, contiguous, and ordered.", nameof(keypoints));
            if (boundingBox.HasValue)
            {
                RectangleF box = boundingBox.Value;
                if (float.IsNaN(box.X) || float.IsInfinity(box.X) || float.IsNaN(box.Y) || float.IsInfinity(box.Y) || float.IsNaN(box.Width) || float.IsInfinity(box.Width) || float.IsNaN(box.Height) || float.IsInfinity(box.Height) || box.Width <= 0 || box.Height <= 0) throw new ArgumentOutOfRangeException(nameof(boundingBox));
            }
            if (classIndex.HasValue && classIndex.Value < 0) throw new ArgumentOutOfRangeException(nameof(classIndex));
            SourceIndex = sourceIndex;
            Score = score;
            _keypoints = keypoints.AsReadOnly();
            BoundingBox = boundingBox;
            ClassIndex = classIndex;
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId;
        }

        /// <summary>Gets the original candidate index used for deterministic ties. / 获取用于确定性同分排序的原始候选索引。</summary>
        public int SourceIndex { get; }
        /// <summary>Gets the non-negative instance score. / 获取非负实例分数。</summary>
        public float Score { get; }
        /// <summary>Gets ordered owned keypoints. / 获取有序自有关键点。</summary>
        public IReadOnlyList<PoseKeypoint> Keypoints => _keypoints;
        /// <summary>Gets an optional clipped source-space half-open bounding box. / 获取可选的已裁剪源图空间半开边界框。</summary>
        public RectangleF? BoundingBox { get; }
        /// <summary>Gets an optional class index. / 获取可选类别索引。</summary>
        public int? ClassIndex { get; }
        /// <summary>Gets an optional caller/model-defined external identifier. / 获取可选的调用方或模型定义外部标识。</summary>
        public string? ExternalId { get; }

        private static List<PoseKeypoint> CopyKeypoints(IEnumerable<PoseKeypoint> keypoints)
        {
            if (keypoints == null) throw new ArgumentNullException(nameof(keypoints));
            var copied = new List<PoseKeypoint>();
            foreach (PoseKeypoint keypoint in keypoints)
            {
                if (keypoint == null) throw new ArgumentException("Pose keypoints cannot contain null.", nameof(keypoints));
                copied.Add(keypoint);
            }
            return copied;
        }
    }

    /// <summary>Contains immutable Pose topology, ordered instances, source size, and profile/model provenance. / 包含不可变姿态拓扑、有序实例、源图尺寸及 Profile/模型来源。</summary>
    public sealed class PoseEstimationResult
    {
        private readonly IReadOnlyList<PoseInstance> _instances;

        /// <summary>Initializes a Pose result and defensively copies instances. / 初始化姿态结果并防御性复制实例。</summary>
        public PoseEstimationResult(PoseTopology topology, IEnumerable<PoseInstance> instances, VisualSize sourceSize, string profileId, ModelId modelId)
            : this(topology, CopyInstances(instances), sourceSize, profileId, modelId)
        {
        }

        internal PoseEstimationResult(PoseTopology topology, List<PoseInstance> instances, VisualSize sourceSize, string profileId, ModelId modelId)
        {
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            var sourceIndices = new HashSet<int>();
            for (int index = 0; index < instances.Count; index++)
            {
                if (instances[index] == null || instances[index].Keypoints.Count != topology.Keypoints.Count) throw new ArgumentException("Every Pose instance must match the topology.", nameof(instances));
                if (!sourceIndices.Add(instances[index].SourceIndex)) throw new ArgumentException("Pose result source indices must be unique.", nameof(instances));
                if (index > 0)
                {
                    PoseInstance previous = instances[index - 1];
                    PoseInstance current = instances[index];
                    if (previous.Score < current.Score || (previous.Score == current.Score && previous.SourceIndex > current.SourceIndex)) throw new ArgumentException("Pose instances must be ordered by descending score and ascending source index for ties.", nameof(instances));
                }
            }
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("A profile ID is required.", nameof(profileId));
            if (modelId.IsEmpty) throw new ArgumentException("A model ID is required.", nameof(modelId));
            _instances = instances.AsReadOnly();
            SourceSize = sourceSize;
            ProfileId = profileId;
            ModelId = modelId;
        }

        /// <summary>Gets immutable keypoint and skeleton topology. / 获取不可变关键点和骨架拓扑。</summary>
        public PoseTopology Topology { get; }
        /// <summary>Gets score-ordered Pose instances. / 获取按分数排序的姿态实例。</summary>
        public IReadOnlyList<PoseInstance> Instances => _instances;
        /// <summary>Gets the source image size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the profile identifier that decoded this result. / 获取解码此结果的 Profile 标识。</summary>
        public string ProfileId { get; }
        /// <summary>Gets the logical model identifier. / 获取逻辑模型标识。</summary>
        public ModelId ModelId { get; }

        /// <summary>Computes a deterministic SHA256 over instance order, scores, boxes, keypoint coordinates, scores, visibility, and validity. / 对实例顺序、分数、边界框、关键点坐标、分数、可见性和有效性计算确定性 SHA256。</summary>
        public string ComputeSha256()
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] buffer = new byte[32];
                AppendInt32(sha, buffer, SourceSize.Width);
                AppendInt32(sha, buffer, SourceSize.Height);
                AppendInt32(sha, buffer, _instances.Count);
                for (int instanceIndex = 0; instanceIndex < _instances.Count; instanceIndex++)
                {
                    PoseInstance instance = _instances[instanceIndex];
                    AppendInt32(sha, buffer, instance.SourceIndex);
                    AppendSingle(sha, buffer, instance.Score);
                    AppendInt32(sha, buffer, instance.BoundingBox.HasValue ? 1 : 0);
                    if (instance.BoundingBox.HasValue)
                    {
                        RectangleF box = instance.BoundingBox.Value;
                        AppendSingle(sha, buffer, box.X); AppendSingle(sha, buffer, box.Y); AppendSingle(sha, buffer, box.Width); AppendSingle(sha, buffer, box.Height);
                    }
                    AppendInt32(sha, buffer, instance.Keypoints.Count);
                    for (int pointIndex = 0; pointIndex < instance.Keypoints.Count; pointIndex++)
                    {
                        PoseKeypoint point = instance.Keypoints[pointIndex];
                        AppendSingle(sha, buffer, point.Point.X); AppendSingle(sha, buffer, point.Point.Y); AppendSingle(sha, buffer, point.Score);
                        AppendInt32(sha, buffer, (int)point.Visibility); AppendInt32(sha, buffer, point.IsValid ? 1 : 0);
                    }
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToLowerHex(sha.Hash!);
            }
        }

        private static List<PoseInstance> CopyInstances(IEnumerable<PoseInstance> instances)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            var copied = new List<PoseInstance>();
            foreach (PoseInstance instance in instances)
            {
                if (instance == null) throw new ArgumentException("Pose instances cannot contain null.", nameof(instances));
                copied.Add(instance);
            }
            return copied;
        }

        private static void AppendInt32(HashAlgorithm hash, byte[] buffer, int value)
        {
            unchecked { buffer[0] = (byte)value; buffer[1] = (byte)(value >> 8); buffer[2] = (byte)(value >> 16); buffer[3] = (byte)(value >> 24); }
            hash.TransformBlock(buffer, 0, 4, buffer, 0);
        }

        private static void AppendSingle(HashAlgorithm hash, byte[] buffer, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            hash.TransformBlock(bytes, 0, bytes.Length, buffer, 0);
        }

        private static string ToLowerHex(byte[] hash)
        {
            char[] characters = new char[hash.Length * 2];
            const string digits = "0123456789abcdef";
            for (int index = 0; index < hash.Length; index++) { characters[index * 2] = digits[hash[index] >> 4]; characters[(index * 2) + 1] = digits[hash[index] & 15]; }
            return new string(characters);
        }
    }
}
