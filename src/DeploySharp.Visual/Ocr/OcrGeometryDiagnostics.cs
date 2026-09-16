using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Controls optional pre-recognition geometry checks. / 控制可选的识别前几何检查。</summary>
    public enum OcrGeometryValidationMode
    {
        /// <summary>Preserves the legacy path without diagnostic work. / 保留既有路径，不执行诊断。</summary>
        Disabled = 0,
        /// <summary>Reports risks without removing regions. / 报告风险，不删除区域。</summary>
        Report = 1,
        /// <summary>Fails the complete call when a configured risk is found. / 发现配置的风险时使整次调用失败。</summary>
        Reject = 2
    }

    /// <summary>Describes geometric risks, not measured recognition accuracy. / 描述几何风险，不代表测得的识别准确率。</summary>
    [Flags]
    public enum OcrGeometryRisk
    {
        /// <summary>No configured threshold was exceeded. / 未超出配置阈值。</summary>
        None = 0,
        /// <summary>The polygon area is too small. / 多边形面积过小。</summary>
        SmallArea = 1,
        /// <summary>A polygon edge is too short. / 多边形边长过短。</summary>
        ShortEdge = 2,
        /// <summary>The quadrilateral aspect ratio is too large. / 四边形宽高比过大。</summary>
        ExtremeAspectRatio = 4,
        /// <summary>Too much polygon area lies outside the input image. / 多边形位于输入图外的面积比例过大。</summary>
        OutsideImage = 8,
        /// <summary>The polygon approaches or crosses an input boundary; truncation is possible. / 多边形接近或跨越输入边界，可能存在截断。</summary>
        EdgeContact = 16,
        /// <summary>The normalized projective transform is ill-conditioned or cannot be solved. / 归一化透视变换病态或无法求解。</summary>
        IllConditionedPerspective = 32,
        /// <summary>Explicit crop corner roles were not supplied. / 未提供显式裁剪角点角色。</summary>
        MissingCropCorners = 64
    }

    /// <summary>Defines immutable geometry thresholds in OCR input pixels. / 定义以 OCR 输入像素为单位的不可变几何阈值。</summary>
    public sealed class OcrGeometryOptions
    {
        internal static OcrGeometryOptions Disabled { get; } = new OcrGeometryOptions(OcrGeometryValidationMode.Disabled);
        internal const OcrGeometryRisk AllRisks = (OcrGeometryRisk)127;

        /// <summary>Initializes opt-in checks; boundary contact is reported but is not rejected by default. / 初始化显式启用的检查；默认报告边界接触，但不因此拒绝。</summary>
        public OcrGeometryOptions(OcrGeometryValidationMode mode = OcrGeometryValidationMode.Report,
            double minimumArea = 4, double minimumEdgeLength = 1, double maximumAspectRatio = 200,
            double maximumOutsideFraction = .25, double edgeMargin = 1, double maximumPerspectiveCondition = 10000,
            OcrGeometryRisk rejectedRisks = OcrGeometryRisk.SmallArea | OcrGeometryRisk.ShortEdge | OcrGeometryRisk.ExtremeAspectRatio
                | OcrGeometryRisk.OutsideImage | OcrGeometryRisk.IllConditionedPerspective | OcrGeometryRisk.MissingCropCorners)
        {
            if (!Enum.IsDefined(typeof(OcrGeometryValidationMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Validate(minimumArea, 0, double.MaxValue, nameof(minimumArea));
            Validate(minimumEdgeLength, 0, double.MaxValue, nameof(minimumEdgeLength));
            Validate(maximumAspectRatio, 1, double.MaxValue, nameof(maximumAspectRatio));
            Validate(maximumOutsideFraction, 0, 1, nameof(maximumOutsideFraction));
            Validate(edgeMargin, 0, double.MaxValue, nameof(edgeMargin));
            Validate(maximumPerspectiveCondition, 3, double.MaxValue, nameof(maximumPerspectiveCondition));
            if ((rejectedRisks & ~AllRisks) != 0) throw new ArgumentOutOfRangeException(nameof(rejectedRisks));
            Mode = mode; MinimumArea = minimumArea; MinimumEdgeLength = minimumEdgeLength;
            MaximumAspectRatio = maximumAspectRatio; MaximumOutsideFraction = maximumOutsideFraction;
            EdgeMargin = edgeMargin; MaximumPerspectiveCondition = maximumPerspectiveCondition; RejectedRisks = rejectedRisks;
        }

        /// <summary>Gets the execution policy. / 获取执行策略。</summary>
        public OcrGeometryValidationMode Mode { get; }
        /// <summary>Gets the minimum polygon area in square pixels. / 获取最小多边形像素面积。</summary>
        public double MinimumArea { get; }
        /// <summary>Gets the minimum edge length in pixels. / 获取最小边长，单位为像素。</summary>
        public double MinimumEdgeLength { get; }
        /// <summary>Gets the maximum long-to-short side ratio. / 获取最大长短边比例。</summary>
        public double MaximumAspectRatio { get; }
        /// <summary>Gets the maximum allowed outside-area fraction. / 获取允许的最大图外面积比例。</summary>
        public double MaximumOutsideFraction { get; }
        /// <summary>Gets the boundary-risk margin in pixels. / 获取边界风险距离，单位为像素。</summary>
        public double EdgeMargin { get; }
        /// <summary>Gets the maximum normalized homography Frobenius condition number. / 获取归一化单应矩阵的最大 Frobenius 条件数。</summary>
        public double MaximumPerspectiveCondition { get; }
        /// <summary>Gets the risks that fail a Reject call. / 获取导致 Reject 调用失败的风险集合。</summary>
        public OcrGeometryRisk RejectedRisks { get; }

        private static void Validate(double value, double minimum, double maximum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum) throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>Stores immutable geometry evidence in the evaluated OCR input space, even after ROI or orientation result projection. / 存储被评估的 OCR 输入空间中的不可变几何证据，ROI 或方向结果投影后仍保留该空间。</summary>
    public sealed class OcrGeometryDiagnostics
    {
        internal OcrGeometryDiagnostics(TextPolygon polygon, VisualSize size, double area, double edge, double? aspect,
            double outside, double? angle, double? condition, double? parallelogramError, OcrGeometryRisk risks)
        {
            InputPolygon = polygon; InputSize = size; Area = area; MinimumEdgeLength = edge; AspectRatio = aspect;
            OutsideFraction = outside; BaselineAngleRadians = angle; PerspectiveCondition = condition;
            ParallelogramError = parallelogramError; Risks = risks;
        }

        /// <summary>Gets the immutable evaluated polygon, not a subsequently projected result polygon. / 获取评估时的不可变多边形，而非后续投影的结果多边形。</summary>
        public TextPolygon InputPolygon { get; }
        /// <summary>Gets the image size in which the polygon was evaluated. / 获取评估多边形时所在图像的尺寸。</summary>
        public VisualSize InputSize { get; }
        /// <summary>Gets polygon area in input square pixels. / 获取输入像素坐标中的多边形面积。</summary>
        public double Area { get; }
        /// <summary>Gets the shortest polygon edge. / 获取多边形最短边长。</summary>
        public double MinimumEdgeLength { get; }
        /// <summary>Gets the quadrilateral long-to-short side ratio, or null without explicit corners. / 获取四边形长短边比例；无显式角点时为 null。</summary>
        public double? AspectRatio { get; }
        /// <summary>Gets the exact convex polygon area fraction outside [0,width] × [0,height]. / 获取凸多边形位于 [0,width] × [0,height] 外部的精确面积比例。</summary>
        public double OutsideFraction { get; }
        /// <summary>Gets the mean top/bottom baseline angle before rectification; positive is image-clockwise. / 获取校正前上下基线的平均角度，图像顺时针为正。</summary>
        public double? BaselineAngleRadians { get; }
        /// <summary>Gets the baseline deskew component, not a measured residual angle or a substitute for perspective correction. / 获取基线去倾斜分量，不代表实测残余角度，也不能替代透视校正。</summary>
        public double? RectificationAngleRadians => -BaselineAngleRadians;
        /// <summary>Gets ||H||F × ||inverse(H)||F after independently normalizing the input bounding box to a unit square; null means unavailable. / 获取输入包围框按两轴归一化到单位正方形后 ||H||F × ||inverse(H)||F；无法计算时为 null。</summary>
        public double? PerspectiveCondition { get; }
        /// <summary>Gets opposite-corner closure error divided by the longest edge; zero denotes a parallelogram, not verified pixel equivalence. / 获取对角顶点闭合误差除以最长边；零表示平行四边形，不表示已验证像素等价。</summary>
        public double? ParallelogramError { get; }
        /// <summary>Gets configured risk flags; these are not proof of missing text. / 获取配置的风险标记；这些标记不能证明文本缺失。</summary>
        public OcrGeometryRisk Risks { get; }
    }

    /// <summary>Evaluates bounded convex text geometry without decoding or copying images. / 不解码或复制图像，对有界凸文本几何进行评估。</summary>
    public static class OcrGeometryAnalyzer
    {
        /// <summary>Reports geometry regardless of Mode; the pipeline enforces Disabled and Reject. / 无论 Mode 均报告几何；Disabled 和 Reject 由流水线执行。</summary>
        public static OcrGeometryDiagnostics Analyze(TextRegion region, VisualSize inputSize, OcrGeometryOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (inputSize.Width <= 0 || inputSize.Height <= 0) throw new ArgumentOutOfRangeException(nameof(inputSize));
            cancellationToken.ThrowIfCancellationRequested();
            OcrGeometryOptions limits = options ?? new OcrGeometryOptions();
            IReadOnlyList<PointF> points = region.Polygon.Vertices;
            double area = 0, minEdge = double.MaxValue, maxEdge = 0;
            double minX = points[0].X, maxX = minX, minY = points[0].Y, maxY = minY;
            for (int index = 0; index < points.Count; index++)
            {
                PointF a = points[index], b = points[(index + 1) % points.Count];
                // Translate first to avoid cancellation of large absolute coordinates.
                area += ((double)a.X - points[0].X) * ((double)b.Y - points[0].Y) - ((double)b.X - points[0].X) * ((double)a.Y - points[0].Y);
                double edge = Distance(a, b); minEdge = Math.Min(minEdge, edge); maxEdge = Math.Max(maxEdge, edge);
                minX = Math.Min(minX, a.X); maxX = Math.Max(maxX, a.X); minY = Math.Min(minY, a.Y); maxY = Math.Max(maxY, a.Y);
            }
            area = Math.Abs(area) * .5;
            double outside = minX >= 0 && minY >= 0 && maxX <= inputSize.Width && maxY <= inputSize.Height ? 0
                : maxX <= 0 || maxY <= 0 || minX >= inputSize.Width || minY >= inputSize.Height ? 1
                : Math.Max(0, Math.Min(1, 1 - InsideArea(points, inputSize, cancellationToken) / area));
            OcrGeometryRisk risks = OcrGeometryRisk.None;
            if (area < limits.MinimumArea) risks |= OcrGeometryRisk.SmallArea;
            if (minEdge < limits.MinimumEdgeLength) risks |= OcrGeometryRisk.ShortEdge;
            if (outside > limits.MaximumOutsideFraction) risks |= OcrGeometryRisk.OutsideImage;
            if (minX <= limits.EdgeMargin || minY <= limits.EdgeMargin || maxX >= inputSize.Width - limits.EdgeMargin || maxY >= inputSize.Height - limits.EdgeMargin) risks |= OcrGeometryRisk.EdgeContact;
            double? aspect = null, angle = null, condition = null, closure = null;
            TextQuadrilateral? quad = region.CropQuadrilateral;
            if (quad == null) risks |= OcrGeometryRisk.MissingCropCorners;
            else
            {
                double width = Math.Max(Distance(quad.TopLeft, quad.TopRight), Distance(quad.BottomLeft, quad.BottomRight));
                double height = Math.Max(Distance(quad.TopLeft, quad.BottomLeft), Distance(quad.TopRight, quad.BottomRight));
                aspect = Math.Max(width, height) / Math.Min(width, height);
                if (aspect > limits.MaximumAspectRatio) risks |= OcrGeometryRisk.ExtremeAspectRatio;
                angle = Math.Atan2((double)quad.TopRight.Y - quad.TopLeft.Y + quad.BottomRight.Y - quad.BottomLeft.Y,
                    (double)quad.TopRight.X - quad.TopLeft.X + quad.BottomRight.X - quad.BottomLeft.X);
                double dx = (double)quad.TopLeft.X + quad.BottomRight.X - quad.TopRight.X - quad.BottomLeft.X;
                double dy = (double)quad.TopLeft.Y + quad.BottomRight.Y - quad.TopRight.Y - quad.BottomLeft.Y;
                closure = Math.Sqrt(dx * dx + dy * dy) / maxEdge;
                var corners = new[] { quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft };
                for (int index = 0; index < 4; index++) corners[index] = new PointF((float)((corners[index].X - minX) / (maxX - minX)), (float)((corners[index].Y - minY) / (maxY - minY)));
                try
                {
                    condition = ImageTransform.Perspective(new VisualSize(1, 1), new VisualSize(1, 1), corners,
                        new[] { new PointF(0, 0), new PointF(1, 0), new PointF(1, 1), new PointF(0, 1) }).FrobeniusCondition;
                }
                catch (VisualException) { risks |= OcrGeometryRisk.IllConditionedPerspective; }
                if (!condition.HasValue || condition > limits.MaximumPerspectiveCondition) risks |= OcrGeometryRisk.IllConditionedPerspective;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new OcrGeometryDiagnostics(region.Polygon, inputSize, area, minEdge, aspect, outside, angle, condition, closure, risks);
        }

        private static double Distance(PointF a, PointF b)
        {
            double dx = (double)b.X - a.X, dy = (double)b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double InsideArea(IReadOnlyList<PointF> points, VisualSize size, CancellationToken token)
        {
            var input = new Coordinate[points.Count + 4];
            var output = new Coordinate[input.Length];
            int count = points.Count;
            for (int index = 0; index < count; index++) input[index] = new Coordinate(points[index].X, points[index].Y);
            for (int edge = 0; edge < 4 && count > 0; edge++)
            {
                token.ThrowIfCancellationRequested();
                int written = 0;
                Coordinate previous = input[count - 1];
                double previousDistance = BoundaryDistance(previous, edge, size);
                for (int index = 0; index < count; index++)
                {
                    Coordinate current = input[index];
                    double distance = BoundaryDistance(current, edge, size);
                    if ((distance >= 0) != (previousDistance >= 0))
                    {
                        double t = previousDistance / (previousDistance - distance);
                        Append(output, ref written, new Coordinate(previous.X + t * (current.X - previous.X), previous.Y + t * (current.Y - previous.Y)));
                    }
                    if (distance >= 0) Append(output, ref written, current);
                    previous = current; previousDistance = distance;
                }
                if (written > 1 && output[0].X == output[written - 1].X && output[0].Y == output[written - 1].Y) written--;
                Coordinate[] swap = input; input = output; output = swap; count = written;
            }
            double area = 0;
            for (int index = 1; index + 1 < count; index++) area += (input[index].X - input[0].X) * (input[index + 1].Y - input[0].Y) - (input[index + 1].X - input[0].X) * (input[index].Y - input[0].Y);
            return Math.Abs(area) * .5;
        }

        private static double BoundaryDistance(Coordinate p, int edge, VisualSize size)
            => edge == 0 ? p.X : edge == 1 ? size.Width - p.X : edge == 2 ? p.Y : size.Height - p.Y;

        private static void Append(Coordinate[] output, ref int count, Coordinate point)
        {
            if (count == 0 || output[count - 1].X != point.X || output[count - 1].Y != point.Y) output[count++] = point;
        }

        private readonly struct Coordinate
        {
            internal Coordinate(double x, double y) { X = x; Y = y; }
            internal double X { get; }
            internal double Y { get; }
        }
    }
}
