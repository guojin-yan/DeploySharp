using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the numeric ordering of four canonical vertices. / 标识四个规范顶点的数值顺序。</summary>
    public enum OrientedVertexOrder
    {
        /// <summary>Counter-clockwise in the mathematical x/y plane. / 在数学 x/y 平面中按逆时针排列。</summary>
        CounterClockwise = 0,
        /// <summary>Clockwise in the mathematical x/y plane. / 在数学 x/y 平面中按顺时针排列。</summary>
        Clockwise = 1
    }

    /// <summary>Identifies deterministic first-vertex selection. / 标识确定性的首顶点选择规则。</summary>
    public enum OrientedStartVertexRule
    {
        /// <summary>Select the smallest y, then smallest x vertex. / 选择 y 最小、再 x 最小的顶点。</summary>
        MinimumYThenX = 0,
        /// <summary>Select the smallest x, then smallest y vertex. / 选择 x 最小、再 y 最小的顶点。</summary>
        MinimumXThenY = 1,
        /// <summary>Select the largest y, then smallest x vertex. / 选择 y 最大、再 x 最小的顶点。</summary>
        MaximumYThenX = 2
    }

    /// <summary>Identifies whether an OBB coordinate is model pixels or normalized. / 标识 OBB 坐标是模型像素还是归一化值。</summary>
    public enum OrientedCoordinateSpace
    {
        /// <summary>Coordinates are model-input pixels. / 坐标为模型输入像素。</summary>
        ModelPixels = 0,
        /// <summary>Coordinates are normalized to model width and height. / 坐标按模型宽高归一化。</summary>
        Normalized = 1
    }

    /// <summary>Identifies angle units. / 标识角度单位。</summary>
    public enum OrientedAngleUnit
    {
        /// <summary>Radians. / 弧度。</summary>
        Radians = 0,
        /// <summary>Degrees. / 角度。</summary>
        Degrees = 1
    }

    /// <summary>Identifies positive angle direction in image coordinates. / 标识图像坐标中的正角方向。</summary>
    public enum OrientedAngleDirection
    {
        /// <summary>Positive angle rotates toward increasing y. / 正角朝 y 增大的方向旋转。</summary>
        Clockwise = 0,
        /// <summary>Positive angle rotates toward decreasing y. / 正角朝 y 减小的方向旋转。</summary>
        CounterClockwise = 1
    }

    /// <summary>Defines the accepted input angle interval. / 定义允许的输入角度区间。</summary>
    public enum OrientedAngleRange
    {
        /// <summary>Half-open interval [-pi/2, pi/2). / 半开区间 [-pi/2, pi/2)。</summary>
        MinusHalfPiToHalfPi = 0,
        /// <summary>Half-open interval [0, pi). / 半开区间 [0, pi)。</summary>
        ZeroToPi = 1,
        /// <summary>Half-open interval [-pi, pi). / 半开区间 [-pi, pi)。</summary>
        MinusPiToPi = 2,
        /// <summary>Half-open interval [0, 2*pi). / 半开区间 [0, 2*pi)。</summary>
        ZeroToTwoPi = 3
    }

    /// <summary>Defines the physical meaning of width and height. / 定义 width 与 height 的物理含义。</summary>
    public enum OrientedWidthConvention
    {
        /// <summary>The supplied width follows the supplied angle. / 输入 width 沿输入角度轴。</summary>
        WidthAxis = 0,
        /// <summary>Width is normalized to the long side and angle is adjusted explicitly. / 将 width 规范为长边并显式调整角度。</summary>
        LongSide = 1
    }

    /// <summary>Defines the treatment of vertices outside the source image. / 定义超出源图边界顶点的处理方式。</summary>
    public enum OrientedDetectionBoundaryMode
    {
        /// <summary>Retain the exact source-space quadrilateral, including out-of-bounds coordinates. / 保留精确源图四边形，包括越界坐标。</summary>
        Preserve = 0,
        /// <summary>Reject a candidate when any source-space vertex is outside the image. / 任一源图顶点越界时拒绝候选。</summary>
        RejectOutsideSource = 1
    }

    /// <summary>Specifies the five values in a center-size-angle tensor row. / 指定 center-size-angle 张量行中的五个值位置。</summary>
    public sealed class OrientedCenterSizeAngleOrder
    {
        /// <summary>Initializes an explicit five-component order. / 初始化显式的五分量顺序。</summary>
        public OrientedCenterSizeAngleOrder(int centerXIndex = 0, int centerYIndex = 1, int widthIndex = 2, int heightIndex = 3, int angleIndex = 4)
        {
            int[] values = { centerXIndex, centerYIndex, widthIndex, heightIndex, angleIndex };
            for (int index = 0; index < values.Length; index++) if (values[index] < 0 || values[index] >= 5) throw new ArgumentOutOfRangeException(nameof(values));
            for (int first = 0; first < values.Length; first++) for (int second = first + 1; second < values.Length; second++) if (values[first] == values[second]) throw new ArgumentException("Center-size-angle component indexes must be unique.", nameof(values));
            CenterXIndex = centerXIndex;
            CenterYIndex = centerYIndex;
            WidthIndex = widthIndex;
            HeightIndex = heightIndex;
            AngleIndex = angleIndex;
        }

        /// <summary>Gets the center-x index. / 获取 center-x 索引。</summary>
        public int CenterXIndex { get; }
        /// <summary>Gets the center-y index. / 获取 center-y 索引。</summary>
        public int CenterYIndex { get; }
        /// <summary>Gets the width index. / 获取 width 索引。</summary>
        public int WidthIndex { get; }
        /// <summary>Gets the height index. / 获取 height 索引。</summary>
        public int HeightIndex { get; }
        /// <summary>Gets the angle index. / 获取 angle 索引。</summary>
        public int AngleIndex { get; }
    }

    /// <summary>Represents four owned, strictly convex source-space vertices. / 表示四个自有的严格凸源图空间顶点。</summary>
    public sealed class OrientedQuadrilateral
    {
        private readonly IReadOnlyList<PointF> _vertices;

        /// <summary>Initializes an already canonical counter-clockwise quadrilateral. / 初始化已经规范化为逆时针的四边形。</summary>
        public OrientedQuadrilateral(PointF first, PointF second, PointF third, PointF fourth, float epsilon = 0.000001f)
        {
            OrientedQuadrilateral canonical = Canonicalize(new[] { first, second, third, fourth }, OrientedVertexOrder.CounterClockwise, OrientedStartVertexRule.MinimumYThenX, epsilon);
            PointF[] points = canonical.Vertices.ToArray();
            First = points[0];
            Second = points[1];
            Third = points[2];
            Fourth = points[3];
            _vertices = new ReadOnlyCollection<PointF>(points);
            SignedArea = OrientedGeometry.SignedArea(points);
            if (SignedArea <= epsilon) throw new ArgumentException("The quadrilateral must have positive area.", nameof(first));
            Area = SignedArea;
            AxisAlignedBounds = OrientedGeometry.Bounds(points);
        }

        internal OrientedQuadrilateral(PointF[] canonicalPoints, bool takeOwnership, float epsilon)
        {
            if (canonicalPoints == null || canonicalPoints.Length != 4) throw new ArgumentException("Exactly four points are required.", nameof(canonicalPoints));
            PointF[] points = takeOwnership ? canonicalPoints : (PointF[])canonicalPoints.Clone();
            ValidateCanonical(points, epsilon);
            First = points[0];
            Second = points[1];
            Third = points[2];
            Fourth = points[3];
            _vertices = new ReadOnlyCollection<PointF>(points);
            SignedArea = OrientedGeometry.SignedArea(points);
            Area = SignedArea;
            AxisAlignedBounds = OrientedGeometry.Bounds(points);
        }

        /// <summary>Gets the first canonical vertex. / 获取第一个规范顶点。</summary>
        public PointF First { get; }
        /// <summary>Gets the second canonical vertex. / 获取第二个规范顶点。</summary>
        public PointF Second { get; }
        /// <summary>Gets the third canonical vertex. / 获取第三个规范顶点。</summary>
        public PointF Third { get; }
        /// <summary>Gets the fourth canonical vertex. / 获取第四个规范顶点。</summary>
        public PointF Fourth { get; }
        /// <summary>Gets canonical counter-clockwise vertices. / 获取规范逆时针顶点。</summary>
        public IReadOnlyList<PointF> Vertices => _vertices;
        /// <summary>Gets the signed shoelace area. / 获取鞋带公式有符号面积。</summary>
        public float SignedArea { get; }
        /// <summary>Gets the positive quadrilateral area. / 获取正的四边形面积。</summary>
        public float Area { get; }
        /// <summary>Gets the derived axis-aligned bounds. / 获取派生轴对齐边界。</summary>
        public RectangleF AxisAlignedBounds { get; }

        /// <summary>Computes deterministic convex-quadrilateral IoU. / 计算确定性的凸四边形 IoU。</summary>
        public static float IntersectionOverUnion(OrientedQuadrilateral first, OrientedQuadrilateral second, float epsilon = 0.000001f)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            if (float.IsNaN(epsilon) || float.IsInfinity(epsilon) || epsilon <= 0) throw new ArgumentOutOfRangeException(nameof(epsilon));
            return OrientedGeometry.IntersectionOverUnion(first, second, epsilon, System.Threading.CancellationToken.None);
        }

        /// <summary>Canonicalizes an explicitly ordered convex quadrilateral without guessing its format. / 在不猜测格式的情况下规范化显式有序凸四边形。</summary>
        public static OrientedQuadrilateral Canonicalize(IReadOnlyList<PointF> input, OrientedVertexOrder inputOrder, OrientedStartVertexRule startRule = OrientedStartVertexRule.MinimumYThenX, float epsilon = 0.000001f)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Count != 4) throw new ArgumentException("Exactly four vertices are required.", nameof(input));
            if (!Enum.IsDefined(typeof(OrientedVertexOrder), inputOrder)) throw new ArgumentOutOfRangeException(nameof(inputOrder));
            if (!Enum.IsDefined(typeof(OrientedStartVertexRule), startRule)) throw new ArgumentOutOfRangeException(nameof(startRule));
            if (float.IsNaN(epsilon) || float.IsInfinity(epsilon) || epsilon <= 0) throw new ArgumentOutOfRangeException(nameof(epsilon));
            var ordered = new PointF[4];
            for (int index = 0; index < 4; index++) ordered[index] = input[index];
            for (int index = 0; index < 4; index++) EnsureFinite(ordered[index]);
            float signed = OrientedGeometry.SignedArea(ordered);
            if (inputOrder == OrientedVertexOrder.CounterClockwise && signed <= epsilon) throw new ArgumentException("The declared counter-clockwise vertex order is invalid.", nameof(input));
            if (inputOrder == OrientedVertexOrder.Clockwise && signed >= -epsilon) throw new ArgumentException("The declared clockwise vertex order is invalid.", nameof(input));
            if (inputOrder == OrientedVertexOrder.Clockwise)
            {
                PointF swap = ordered[1];
                ordered[1] = ordered[3];
                ordered[3] = swap;
            }
            ValidateCanonical(ordered, epsilon);
            int start = SelectStart(ordered, startRule);
            var canonical = new PointF[4];
            for (int index = 0; index < 4; index++) canonical[index] = ordered[(start + index) % 4];
            return new OrientedQuadrilateral(canonical, true, epsilon);
        }

        private static int SelectStart(PointF[] points, OrientedStartVertexRule rule)
        {
            int selected = 0;
            for (int index = 1; index < points.Length; index++)
            {
                PointF candidate = points[index];
                PointF current = points[selected];
                bool before = rule == OrientedStartVertexRule.MinimumYThenX
                    ? candidate.Y < current.Y || (candidate.Y == current.Y && candidate.X < current.X)
                    : rule == OrientedStartVertexRule.MinimumXThenY
                        ? candidate.X < current.X || (candidate.X == current.X && candidate.Y < current.Y)
                        : candidate.Y > current.Y || (candidate.Y == current.Y && candidate.X < current.X);
                if (before) selected = index;
            }

            return selected;
        }

        private static void ValidateCanonical(PointF[] points, float epsilon)
        {
            for (int index = 0; index < points.Length; index++)
            {
                EnsureFinite(points[index]);
                int next = (index + 1) % 4;
                int following = (index + 2) % 4;
                float cross = OrientedGeometry.Cross(points[index], points[next], points[following]);
                if (cross <= epsilon) throw new ArgumentException("The quadrilateral must be strictly convex and non-self-intersecting.", nameof(points));
                for (int other = index + 1; other < points.Length; other++) if (points[index] == points[other]) throw new ArgumentException("Quadrilateral vertices must be distinct.", nameof(points));
            }
        }

        private static void EnsureFinite(PointF point)
        {
            if (float.IsNaN(point.X) || float.IsInfinity(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.Y)) throw new ArgumentException("Quadrilateral coordinates must be finite.", nameof(point));
        }
    }

    /// <summary>Represents one deterministic oriented detection. / 表示一个确定性的旋转目标检测结果。</summary>
    public sealed class OrientedDetection
    {
        private readonly IReadOnlyDictionary<string, string> _metadata;

        /// <summary>Initializes an oriented detection with an authoritative quadrilateral. / 使用权威四边形初始化旋转目标检测结果。</summary>
        public OrientedDetection(int sourceIndex, int classIndex, string label, float score, OrientedQuadrilateral quadrilateral, float? angleRadiansCounterClockwise = null, bool exactRotatedRectangle = false, string? externalId = null, IEnumerable<KeyValuePair<string, string>>? metadata = null)
        {
            if (sourceIndex < 0) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            if (classIndex < 0) throw new ArgumentOutOfRangeException(nameof(classIndex));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A class label is required.", nameof(label));
            if (float.IsNaN(score) || float.IsInfinity(score) || score < 0) throw new ArgumentOutOfRangeException(nameof(score));
            if (quadrilateral == null) throw new ArgumentNullException(nameof(quadrilateral));
            if (angleRadiansCounterClockwise.HasValue && (float.IsNaN(angleRadiansCounterClockwise.Value) || float.IsInfinity(angleRadiansCounterClockwise.Value))) throw new ArgumentOutOfRangeException(nameof(angleRadiansCounterClockwise));
            if (exactRotatedRectangle && !angleRadiansCounterClockwise.HasValue) throw new ArgumentException("An exact rotated rectangle requires an angle.", nameof(exactRotatedRectangle));
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (metadata != null) foreach (KeyValuePair<string, string> pair in metadata)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null || copy.ContainsKey(pair.Key)) throw new ArgumentException("Metadata keys must be unique and non-empty.", nameof(metadata));
                copy.Add(pair.Key, pair.Value);
            }
            SourceIndex = sourceIndex;
            ClassIndex = classIndex;
            Label = label;
            Score = score;
            Center = new PointF((quadrilateral.First.X + quadrilateral.Second.X + quadrilateral.Third.X + quadrilateral.Fourth.X) / 4f, (quadrilateral.First.Y + quadrilateral.Second.Y + quadrilateral.Third.Y + quadrilateral.Fourth.Y) / 4f);
            EdgeLength01 = Distance(quadrilateral.First, quadrilateral.Second);
            EdgeLength12 = Distance(quadrilateral.Second, quadrilateral.Third);
            EdgeLength23 = Distance(quadrilateral.Third, quadrilateral.Fourth);
            EdgeLength30 = Distance(quadrilateral.Fourth, quadrilateral.First);
            Quadrilateral = quadrilateral;
            AngleRadiansCounterClockwise = angleRadiansCounterClockwise;
            HasExactRotatedRectangle = exactRotatedRectangle;
            AxisAlignedBounds = quadrilateral.AxisAlignedBounds;
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId;
            _metadata = new ReadOnlyDictionary<string, string>(copy);
        }

        /// <summary>Gets the original candidate index. / 获取原始候选索引。</summary>
        public int SourceIndex { get; }
        /// <summary>Gets the zero-based class index. / 获取从零开始的类别索引。</summary>
        public int ClassIndex { get; }
        /// <summary>Gets the display label. / 获取显示标签。</summary>
        public string Label { get; }
        /// <summary>Gets the confidence score. / 获取置信分数。</summary>
        public float Score { get; }
        /// <summary>Gets the authoritative source-space quadrilateral. / 获取权威源图空间四边形。</summary>
        public OrientedQuadrilateral Quadrilateral { get; }
        /// <summary>Gets the arithmetic center of the four vertices. / 获取四个顶点的算术中心。</summary>
        public PointF Center { get; }
        /// <summary>Gets the first edge length. / 获取第一条边长度。</summary>
        public float EdgeLength01 { get; }
        /// <summary>Gets the second edge length. / 获取第二条边长度。</summary>
        public float EdgeLength12 { get; }
        /// <summary>Gets the third edge length. / 获取第三条边长度。</summary>
        public float EdgeLength23 { get; }
        /// <summary>Gets the fourth edge length. / 获取第四条边长度。</summary>
        public float EdgeLength30 { get; }
        /// <summary>Gets the derived axis-aligned bounds. / 获取派生轴对齐边界。</summary>
        public RectangleF AxisAlignedBounds { get; }
        /// <summary>Gets the exact center-size angle in counter-clockwise radians when declared. / 获取声明的精确逆时针弧度角；无精确旋转矩形时为空。</summary>
        public float? AngleRadiansCounterClockwise { get; }
        /// <summary>Gets whether the angle is an exact rotated-rectangle value. / 获取角度是否为精确旋转矩形值。</summary>
        public bool HasExactRotatedRectangle { get; }
        /// <summary>Gets an optional external identifier. / 获取可选外部标识。</summary>
        public string? ExternalId { get; }
        /// <summary>Gets immutable metadata. / 获取不可变元数据。</summary>
        public IReadOnlyDictionary<string, string> Metadata => _metadata;

        private static float Distance(PointF first, PointF second)
        {
            double x = second.X - first.X;
            double y = second.Y - first.Y;
            return (float)Math.Sqrt((x * x) + (y * y));
        }
    }

    /// <summary>Contains ordered, owned oriented detections. / 包含有序且自有的旋转目标检测结果。</summary>
    public sealed class OrientedDetectionResult
    {
        private readonly IReadOnlyList<OrientedDetection> _detections;

        /// <summary>Initializes an ordered OBB result. / 初始化有序 OBB 结果。</summary>
        public OrientedDetectionResult(IEnumerable<OrientedDetection> detections, VisualSize sourceSize, string profileId, ModelId modelId)
        {
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("A profile identifier is required.", nameof(profileId));
            if (modelId.IsEmpty) throw new ArgumentException("A model identifier is required.", nameof(modelId));
            var copy = new List<OrientedDetection>();
            var sourceIndexes = new HashSet<int>();
            OrientedDetection? previous = null;
            foreach (OrientedDetection detection in detections)
            {
                if (detection == null || !sourceIndexes.Add(detection.SourceIndex)) throw new ArgumentException("Detections must be non-null and have unique source indexes.", nameof(detections));
                if (previous != null && (detection.Score > previous.Score || (detection.Score == previous.Score && detection.SourceIndex < previous.SourceIndex))) throw new ArgumentException("Detections are not in deterministic score/source-index order.", nameof(detections));
                copy.Add(detection);
                previous = detection;
            }
            _detections = new ReadOnlyCollection<OrientedDetection>(copy);
            SourceSize = sourceSize;
            ProfileId = profileId;
            ModelId = modelId;
        }

        /// <summary>Gets detections in score-descending/source-index order. / 获取按分数降序和源索引升序排列的检测结果。</summary>
        public IReadOnlyList<OrientedDetection> Detections => _detections;
        /// <summary>Gets source image size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets profile identifier. / 获取 Profile 标识。</summary>
        public string ProfileId { get; }
        /// <summary>Gets logical model identifier. / 获取逻辑模型标识。</summary>
        public ModelId ModelId { get; }

        /// <summary>Computes a canonical SHA-256 result digest. / 计算规范 SHA-256 结果摘要。</summary>
        public string ComputeSha256()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(ProfileId);
                writer.Write(ModelId.Value);
                writer.Write(SourceSize.Width);
                writer.Write(SourceSize.Height);
                writer.Write(_detections.Count);
                for (int index = 0; index < _detections.Count; index++)
                {
                    OrientedDetection detection = _detections[index];
                    writer.Write(detection.SourceIndex);
                    writer.Write(detection.ClassIndex);
                    writer.Write(detection.Label);
                    writer.Write(detection.Score);
                    writer.Write(detection.HasExactRotatedRectangle);
                    writer.Write(detection.AngleRadiansCounterClockwise ?? float.NaN);
                    foreach (PointF point in detection.Quadrilateral.Vertices) { writer.Write(point.X); writer.Write(point.Y); }
                    writer.Write(detection.ExternalId != null);
                    if (detection.ExternalId != null) writer.Write(detection.ExternalId);
                    writer.Write(detection.Metadata.Count);
                    foreach (KeyValuePair<string, string> pair in detection.Metadata.OrderBy(value => value.Key, StringComparer.Ordinal)) { writer.Write(pair.Key); writer.Write(pair.Value); }
                }
            }

            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(stream.ToArray());
            var result = new char[bytes.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++) { result[index * 2] = alphabet[bytes[index] >> 4]; result[(index * 2) + 1] = alphabet[bytes[index] & 15]; }
            return new string(result);
        }
    }
}
