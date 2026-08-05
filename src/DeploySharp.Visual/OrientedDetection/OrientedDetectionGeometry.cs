using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    internal static class OrientedGeometry
    {
        public static float Cross(PointF first, PointF second, PointF third)
        {
            double abX = second.X - first.X;
            double abY = second.Y - first.Y;
            double bcX = third.X - second.X;
            double bcY = third.Y - second.Y;
            return (float)((abX * bcY) - (abY * bcX));
        }

        public static float SignedArea(IReadOnlyList<PointF> points)
        {
            double sum = 0;
            for (int index = 0; index < points.Count; index++)
            {
                PointF first = points[index];
                PointF second = points[(index + 1) % points.Count];
                sum += ((double)first.X * second.Y) - ((double)second.X * first.Y);
            }

            return (float)(sum * 0.5d);
        }

        public static RectangleF Bounds(IReadOnlyList<PointF> points)
        {
            float minX = points[0].X;
            float maxX = points[0].X;
            float minY = points[0].Y;
            float maxY = points[0].Y;
            for (int index = 1; index < points.Count; index++)
            {
                PointF point = points[index];
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public static float IntersectionOverUnion(OrientedQuadrilateral first, OrientedQuadrilateral second, float epsilon, CancellationToken cancellationToken)
        {
            RectangleF firstBounds = first.AxisAlignedBounds;
            RectangleF secondBounds = second.AxisAlignedBounds;
            if (firstBounds.Right <= secondBounds.X + epsilon || secondBounds.Right <= firstBounds.X + epsilon || firstBounds.Bottom <= secondBounds.Y + epsilon || secondBounds.Bottom <= firstBounds.Y + epsilon) return 0;
            float intersection = IntersectionArea(first.Vertices, second.Vertices, epsilon, cancellationToken);
            if (intersection <= epsilon) return 0;
            float union = first.Area + second.Area - intersection;
            return union <= epsilon ? 0 : intersection / union;
        }

        public static float IntersectionArea(IReadOnlyList<PointF> subject, IReadOnlyList<PointF> clip, float epsilon, CancellationToken cancellationToken)
        {
            // A convex quadrilateral clipped by another convex quadrilateral has at most eight vertices. / 两个凸四边形裁剪后的结果最多八个顶点。
            var current = new PointF[8];
            var next = new PointF[8];
            for (int index = 0; index < subject.Count; index++) current[index] = subject[index];
            int currentCount = subject.Count;
            for (int edgeIndex = 0; edgeIndex < clip.Count && currentCount > 0; edgeIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PointF edgeStart = clip[edgeIndex];
                PointF edgeEnd = clip[(edgeIndex + 1) % clip.Count];
                int nextCount = 0;
                PointF previous = current[currentCount - 1];
                bool previousInside = Cross(edgeStart, edgeEnd, previous) >= -epsilon;
                for (int pointIndex = 0; pointIndex < currentCount; pointIndex++)
                {
                    if ((pointIndex & 3) == 0) cancellationToken.ThrowIfCancellationRequested();
                    PointF point = current[pointIndex];
                    bool inside = Cross(edgeStart, edgeEnd, point) >= -epsilon;
                    if (inside != previousInside && nextCount < next.Length) next[nextCount++] = LineIntersection(previous, point, edgeStart, edgeEnd, epsilon);
                    if (inside && nextCount < next.Length) next[nextCount++] = point;
                    previous = point;
                    previousInside = inside;
                }

                PointF[] swap = current;
                current = next;
                next = swap;
                currentCount = nextCount;
            }

            if (currentCount < 3) return 0;
            return Math.Max(0, SignedArea(new ArraySegmentPointList(current, currentCount)));
        }

        public static float NormalizeInputAngle(float value, OrientedAngleUnit unit, OrientedAngleRange range, float epsilon)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Angle must be finite.", nameof(value));
            double radians = unit == OrientedAngleUnit.Degrees ? value * Math.PI / 180d : value;
            double lower;
            double upper;
            GetRange(range, out lower, out upper);
            // The schema interval is half-open: tolerate only a small lower-bound conversion error, never the excluded upper endpoint. / Schema 区间为半开区间：仅容忍下界换算的小误差，绝不接受已排除的上界端点。
            if (radians < lower - epsilon || radians >= upper) throw new ArgumentException("Angle is outside the declared half-open range.", nameof(value));
            if (radians < lower) radians = lower;
            return (float)radians;
        }

        public static float NormalizeMathAngle(float radians, OrientedAngleRange range)
        {
            double lower;
            double upper;
            GetRange(range, out lower, out upper);
            double period = upper - lower;
            double value = radians - lower;
            value %= period;
            if (value < 0) value += period;
            return (float)(value + lower);
        }

        public static float NormalizeCenterMathAngle(float inputAngle, float inputWidth, float inputHeight, CenterSizeAngleOutputSchema schema, VisualSize modelSize)
        {
            float raw = NormalizeInputAngle(inputAngle, schema.AngleUnit, schema.AngleRange, schema.Epsilon);
            float width = inputWidth;
            float height = inputHeight;
            if (schema.CoordinateSpace == OrientedCoordinateSpace.Normalized) { width *= modelSize.Width; height *= modelSize.Height; }
            double mathAngle = schema.AngleDirection == OrientedAngleDirection.Clockwise ? -raw : raw;
            if (schema.WidthConvention == OrientedWidthConvention.LongSide && width < height) mathAngle += schema.AngleDirection == OrientedAngleDirection.Clockwise ? -Math.PI / 2d : Math.PI / 2d;
            return NormalizeMathAngle((float)mathAngle, schema.AngleRange);
        }

        public static OrientedQuadrilateral CreateCenterSizeAngleCorners(float centerX, float centerY, float width, float height, float inputAngle, CenterSizeAngleOutputSchema schema, VisualSize modelSize)
        {
            if (schema.CoordinateSpace == OrientedCoordinateSpace.Normalized)
            {
                centerX *= modelSize.Width;
                centerY *= modelSize.Height;
                width *= modelSize.Width;
                height *= modelSize.Height;
            }
            float angle = NormalizeInputAngle(inputAngle, schema.AngleUnit, schema.AngleRange, schema.Epsilon);
            double mathAngle = schema.AngleDirection == OrientedAngleDirection.Clockwise ? -angle : angle;
            if (schema.WidthConvention == OrientedWidthConvention.LongSide && width < height)
            {
                float swap = width;
                width = height;
                height = swap;
                mathAngle += schema.AngleDirection == OrientedAngleDirection.Clockwise ? -Math.PI / 2d : Math.PI / 2d;
            }
            mathAngle = NormalizeMathAngle((float)mathAngle, schema.AngleRange);
            double cos = Math.Cos(mathAngle);
            double sin = Math.Sin(mathAngle);
            double widthX = cos;
            double widthY = -sin;
            double heightX = sin;
            double heightY = cos;
            float halfWidth = width / 2f;
            float halfHeight = height / 2f;
            var points = new[]
            {
                new PointF((float)(centerX - (halfWidth * widthX) - (halfHeight * heightX)), (float)(centerY - (halfWidth * widthY) - (halfHeight * heightY))),
                new PointF((float)(centerX + (halfWidth * widthX) - (halfHeight * heightX)), (float)(centerY + (halfWidth * widthY) - (halfHeight * heightY))),
                new PointF((float)(centerX + (halfWidth * widthX) + (halfHeight * heightX)), (float)(centerY + (halfWidth * widthY) + (halfHeight * heightY))),
                new PointF((float)(centerX - (halfWidth * widthX) + (halfHeight * heightX)), (float)(centerY - (halfWidth * widthY) + (halfHeight * heightY)))
            };
            return OrientedQuadrilateral.Canonicalize(points, OrientedVertexOrder.CounterClockwise, OrientedStartVertexRule.MinimumYThenX, schema.Epsilon);
        }

        private static PointF LineIntersection(PointF first, PointF second, PointF edgeStart, PointF edgeEnd, float epsilon)
        {
            float firstSide = Cross(edgeStart, edgeEnd, first);
            float secondSide = Cross(edgeStart, edgeEnd, second);
            double denominator = firstSide - secondSide;
            if (Math.Abs(denominator) <= epsilon) return second;
            double t = firstSide / denominator;
            return new PointF((float)(first.X + ((second.X - first.X) * t)), (float)(first.Y + ((second.Y - first.Y) * t)));
        }

        private static void GetRange(OrientedAngleRange range, out double lower, out double upper)
        {
            switch (range)
            {
                case OrientedAngleRange.MinusHalfPiToHalfPi: lower = -Math.PI / 2d; upper = Math.PI / 2d; break;
                case OrientedAngleRange.ZeroToPi: lower = 0; upper = Math.PI; break;
                case OrientedAngleRange.MinusPiToPi: lower = -Math.PI; upper = Math.PI; break;
                case OrientedAngleRange.ZeroToTwoPi: lower = 0; upper = Math.PI * 2d; break;
                default: throw new ArgumentOutOfRangeException(nameof(range));
            }
        }

        private sealed class ArraySegmentPointList : IReadOnlyList<PointF>
        {
            private readonly PointF[] _points;
            private readonly int _count;
            public ArraySegmentPointList(PointF[] points, int count) { _points = points; _count = count; }
            public int Count => _count;
            public PointF this[int index] => _points[index];
            public IEnumerator<PointF> GetEnumerator() { for (int index = 0; index < _count; index++) yield return _points[index]; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
