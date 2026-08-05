using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the declared corner order of a text quadrilateral. / 标识文本四边形声明的角点顺序。</summary>
    public enum TextCornerOrder
    {
        /// <summary>Top-left, top-right, bottom-right, bottom-left in image coordinates. / 图像坐标中的左上、右上、右下、左下。</summary>
        TopLeftClockwise = 0,
        /// <summary>Top-left, bottom-left, bottom-right, top-right in image coordinates. / 图像坐标中的左上、左下、右下、右上。</summary>
        TopLeftCounterClockwise = 1
    }

    /// <summary>Represents an owned, bounded, strictly convex text polygon in source coordinates. / 表示源图坐标中自有、有界且严格凸的文本多边形。</summary>
    public sealed class TextPolygon
    {
        /// <summary>Gets the maximum supported polygon vertex count. / 获取支持的最大多边形顶点数。</summary>
        public const int MaximumVertices = 32;

        private readonly IReadOnlyList<PointF> _vertices;

        private TextPolygon(PointF[] vertices, float epsilon)
        {
            ValidateCanonical(vertices, epsilon);
            _vertices = new ReadOnlyCollection<PointF>(vertices);
            SignedArea = OcrGeometry.SignedArea(vertices);
            Area = SignedArea;
            AxisAlignedBounds = OcrGeometry.Bounds(vertices);
        }

        /// <summary>Gets canonical counter-clockwise vertices, starting at minimum y then minimum x. / 获取规范逆时针顶点，首点按最小 y 再最小 x 选择。</summary>
        public IReadOnlyList<PointF> Vertices => _vertices;

        /// <summary>Gets the positive signed shoelace area. / 获取鞋带公式计算的正有符号面积。</summary>
        public float SignedArea { get; }

        /// <summary>Gets polygon area. / 获取多边形面积。</summary>
        public float Area { get; }

        /// <summary>Gets derived axis-aligned bounds. / 获取派生的轴对齐边界。</summary>
        public RectangleF AxisAlignedBounds { get; }

        /// <summary>Canonicalizes an explicitly ordered convex polygon without inferring missing vertices. / 规范化显式有序凸多边形，不推断缺失顶点。</summary>
        public static TextPolygon Canonicalize(IReadOnlyList<PointF> input, OrientedVertexOrder inputOrder, float epsilon = 0.000001f)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Count < 3 || input.Count > MaximumVertices) throw new ArgumentOutOfRangeException(nameof(input), "A text polygon requires 3 through 32 vertices.");
            if (!Enum.IsDefined(typeof(OrientedVertexOrder), inputOrder)) throw new ArgumentOutOfRangeException(nameof(inputOrder));
            ValidateEpsilon(epsilon);
            var ordered = new PointF[input.Count];
            for (int index = 0; index < input.Count; index++)
            {
                EnsureFinite(input[index]);
                ordered[index] = input[index];
            }

            float signed = OcrGeometry.SignedArea(ordered);
            if (inputOrder == OrientedVertexOrder.CounterClockwise && signed <= epsilon) throw new ArgumentException("The declared counter-clockwise polygon order is invalid.", nameof(input));
            if (inputOrder == OrientedVertexOrder.Clockwise && signed >= -epsilon) throw new ArgumentException("The declared clockwise polygon order is invalid.", nameof(input));
            if (inputOrder == OrientedVertexOrder.Clockwise) Array.Reverse(ordered);
            ValidateCanonical(ordered, epsilon);

            int start = 0;
            for (int index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].Y < ordered[start].Y || (ordered[index].Y == ordered[start].Y && ordered[index].X < ordered[start].X)) start = index;
            }

            var canonical = new PointF[ordered.Length];
            for (int index = 0; index < ordered.Length; index++) canonical[index] = ordered[(start + index) % ordered.Length];
            return new TextPolygon(canonical, epsilon);
        }

        /// <summary>Computes exact IoU for two convex polygons using bounded clipping. / 使用有界裁剪计算两个凸多边形的精确 IoU。</summary>
        public static float IntersectionOverUnion(TextPolygon first, TextPolygon second, float epsilon = 0.000001f)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            ValidateEpsilon(epsilon);
            return OcrGeometry.IntersectionOverUnion(first, second, epsilon, CancellationToken.None);
        }

        private static void ValidateCanonical(PointF[] points, float epsilon)
        {
            if (points.Length < 3 || points.Length > MaximumVertices) throw new ArgumentOutOfRangeException(nameof(points));
            for (int index = 0; index < points.Length; index++)
            {
                EnsureFinite(points[index]);
                for (int other = index + 1; other < points.Length; other++) if (points[index] == points[other]) throw new ArgumentException("Text polygon vertices must be distinct.", nameof(points));
                PointF current = points[index];
                PointF next = points[(index + 1) % points.Length];
                PointF following = points[(index + 2) % points.Length];
                if (OcrGeometry.Cross(current, next, following) <= epsilon) throw new ArgumentException("The text polygon must be strictly convex and non-self-intersecting.", nameof(points));
            }
        }

        private static void EnsureFinite(PointF point)
        {
            if (float.IsNaN(point.X) || float.IsInfinity(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.Y)) throw new ArgumentException("Text polygon coordinates must be finite.", nameof(point));
        }

        private static void ValidateEpsilon(float epsilon)
        {
            if (float.IsNaN(epsilon) || float.IsInfinity(epsilon) || epsilon <= 0) throw new ArgumentOutOfRangeException(nameof(epsilon));
        }
    }

    /// <summary>Stores explicit top-left, top-right, bottom-right, and bottom-left crop roles. / 存储显式左上、右上、右下、左下裁剪角色。</summary>
    public sealed class TextQuadrilateral
    {
        /// <summary>Initializes an explicitly ordered quadrilateral. / 初始化显式有序四边形。</summary>
        public TextQuadrilateral(PointF first, PointF second, PointF third, PointF fourth, TextCornerOrder order, float epsilon = 0.000001f)
        {
            if (!Enum.IsDefined(typeof(TextCornerOrder), order)) throw new ArgumentOutOfRangeException(nameof(order));
            PointF[] clockwise = order == TextCornerOrder.TopLeftClockwise
                ? new[] { first, second, third, fourth }
                : new[] { first, fourth, third, second };
            // Image-clockwise TL/TR/BR/BL has positive shoelace area in the numeric x/y coordinate plane.
            // 图像顺时针 TL/TR/BR/BL 在数值 x/y 坐标平面中具有正鞋带面积。
            Polygon = TextPolygon.Canonicalize(clockwise, OrientedVertexOrder.CounterClockwise, epsilon);
            TopLeft = clockwise[0];
            TopRight = clockwise[1];
            BottomRight = clockwise[2];
            BottomLeft = clockwise[3];
        }

        /// <summary>Gets the top-left source point. / 获取源图左上点。</summary>
        public PointF TopLeft { get; }
        /// <summary>Gets the top-right source point. / 获取源图右上点。</summary>
        public PointF TopRight { get; }
        /// <summary>Gets the bottom-right source point. / 获取源图右下点。</summary>
        public PointF BottomRight { get; }
        /// <summary>Gets the bottom-left source point. / 获取源图左下点。</summary>
        public PointF BottomLeft { get; }
        /// <summary>Gets the canonical polygon view. / 获取规范多边形视图。</summary>
        public TextPolygon Polygon { get; }
    }

    internal static class OcrGeometry
    {
        public static float SignedArea(IReadOnlyList<PointF> points)
        {
            double area = 0;
            for (int index = 0; index < points.Count; index++)
            {
                PointF current = points[index];
                PointF next = points[(index + 1) % points.Count];
                area += ((double)current.X * next.Y) - ((double)next.X * current.Y);
            }
            return checked((float)(area * 0.5));
        }

        public static float Cross(PointF origin, PointF first, PointF second)
        {
            return ((first.X - origin.X) * (second.Y - origin.Y)) - ((first.Y - origin.Y) * (second.X - origin.X));
        }

        public static RectangleF Bounds(IReadOnlyList<PointF> points)
        {
            float minX = points[0].X;
            float maxX = points[0].X;
            float minY = points[0].Y;
            float maxY = points[0].Y;
            for (int index = 1; index < points.Count; index++)
            {
                minX = Math.Min(minX, points[index].X);
                maxX = Math.Max(maxX, points[index].X);
                minY = Math.Min(minY, points[index].Y);
                maxY = Math.Max(maxY, points[index].Y);
            }
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public static float IntersectionOverUnion(TextPolygon first, TextPolygon second, float epsilon, CancellationToken cancellationToken)
        {
            RectangleF a = first.AxisAlignedBounds;
            RectangleF b = second.AxisAlignedBounds;
            if (a.Right <= b.X || b.Right <= a.X || a.Bottom <= b.Y || b.Bottom <= a.Y) return 0;

            int capacity = checked(first.Vertices.Count + second.Vertices.Count);
            var input = new PointF[capacity];
            var output = new PointF[capacity];
            int inputCount = first.Vertices.Count;
            for (int index = 0; index < inputCount; index++) input[index] = first.Vertices[index];
            for (int edge = 0; edge < second.Vertices.Count && inputCount > 0; edge++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PointF clipStart = second.Vertices[edge];
                PointF clipEnd = second.Vertices[(edge + 1) % second.Vertices.Count];
                int outputCount = 0;
                PointF previous = input[inputCount - 1];
                bool previousInside = Cross(clipStart, clipEnd, previous) >= -epsilon;
                for (int index = 0; index < inputCount; index++)
                {
                    PointF current = input[index];
                    bool currentInside = Cross(clipStart, clipEnd, current) >= -epsilon;
                    if (currentInside != previousInside) output[outputCount++] = Intersect(previous, current, clipStart, clipEnd, epsilon);
                    if (currentInside) output[outputCount++] = current;
                    previous = current;
                    previousInside = currentInside;
                }
                PointF[] swap = input;
                input = output;
                output = swap;
                inputCount = outputCount;
            }

            if (inputCount < 3) return 0;
            float intersection = Math.Abs(SignedArea(new ArraySegmentList(input, inputCount)));
            if (intersection <= epsilon) return 0;
            float union = first.Area + second.Area - intersection;
            return union <= epsilon ? 0 : Math.Max(0, Math.Min(1, intersection / union));
        }

        private static PointF Intersect(PointF lineStart, PointF lineEnd, PointF clipStart, PointF clipEnd, float epsilon)
        {
            double lineX = lineEnd.X - lineStart.X;
            double lineY = lineEnd.Y - lineStart.Y;
            double clipX = clipEnd.X - clipStart.X;
            double clipY = clipEnd.Y - clipStart.Y;
            double denominator = (lineX * clipY) - (lineY * clipX);
            if (Math.Abs(denominator) <= epsilon) return lineEnd;
            double offsetX = clipStart.X - lineStart.X;
            double offsetY = clipStart.Y - lineStart.Y;
            double t = ((offsetX * clipY) - (offsetY * clipX)) / denominator;
            return new PointF(checked((float)(lineStart.X + (t * lineX))), checked((float)(lineStart.Y + (t * lineY))));
        }

        private sealed class ArraySegmentList : IReadOnlyList<PointF>
        {
            private readonly PointF[] _values;
            public ArraySegmentList(PointF[] values, int count) { _values = values; Count = count; }
            public int Count { get; }
            public PointF this[int index] => index >= 0 && index < Count ? _values[index] : throw new ArgumentOutOfRangeException(nameof(index));
            public IEnumerator<PointF> GetEnumerator() { for (int index = 0; index < Count; index++) yield return _values[index]; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
