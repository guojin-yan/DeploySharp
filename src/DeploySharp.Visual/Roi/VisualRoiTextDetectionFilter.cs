using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents one text region retained by ROI filtering. / 表示一个经过 ROI 过滤后保留的文本区域。</summary>
    public sealed class RoiTextRegion
    {
        private readonly IReadOnlyList<string> _roiIds;

        /// <summary>Initializes a text region with deterministic ROI provenance. / 使用确定性的 ROI 来源初始化文本区域。</summary>
        public RoiTextRegion(TextRegion region, IEnumerable<string> roiIds)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
            if (roiIds == null) throw new ArgumentNullException(nameof(roiIds));
            _roiIds = new ReadOnlyCollection<string>(roiIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
        }

        /// <summary>Gets the original source-space text region. / 获取原始源图空间文本区域。</summary>
        public TextRegion Region { get; }
        /// <summary>Gets matching Include ROI identifiers. / 获取命中的 Include ROI 标识。</summary>
        public IReadOnlyList<string> RoiIds => _roiIds;
        /// <summary>Gets the deterministic primary ROI identifier. / 获取确定性的主 ROI 标识。</summary>
        public string? PrimaryRoiId => _roiIds.Count == 0 ? null : _roiIds[0];
    }

    /// <summary>Contains text regions retained by ROI filtering. / 包含 ROI 过滤后保留的文本区域。</summary>
    public sealed class RoiTextDetectionResult
    {
        private readonly IReadOnlyList<RoiTextRegion> _regions;

        internal RoiTextDetectionResult(VisualSize sourceSize, long snapshotVersion, IEnumerable<RoiTextRegion> regions)
        {
            if (snapshotVersion <= 0) throw new ArgumentOutOfRangeException(nameof(snapshotVersion));
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            _regions = new ReadOnlyCollection<RoiTextRegion>(regions.ToList());
            Regions = new TextDetectionResult(_regions.Select(value => value.Region), sourceSize, "roi-filter", new ModelId("roi-filter"));
        }

        /// <summary>Gets source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the ROI snapshot version used for filtering. / 获取过滤所用 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets retained regions in original reading order. / 获取按原始阅读顺序保留的区域。</summary>
        public IReadOnlyList<RoiTextRegion> Items => _regions;
        /// <summary>Gets the canonical text-detection view without provenance wrappers. / 获取不带来源包装的规范文本检测视图。</summary>
        public TextDetectionResult Regions { get; }
    }

    /// <summary>Filters source-space OCR text polygons against Include and Exclude ROIs. / 针对 Include 和 Exclude ROI 过滤源图 OCR 文本多边形。</summary>
    public static class VisualRoiTextDetectionFilter
    {
        /// <summary>Filters text regions while preserving reading order and polygon ownership. / 过滤文本区域并保留阅读顺序和多边形所有权。</summary>
        /// <remarks>All non-center modes use the authoritative text polygon. Convex ROI geometries use polygon clipping; arbitrary simple polygons use ear-clipped triangles and masks use set-pixel squares. Cross-window text dedup remains task-specific because recognized text and reading order are application policy. / 除中心点模式外均使用权威文本多边形：凸 ROI 使用多边形裁剪，任意简单多边形使用耳切三角剖分，Mask 使用有效像素方格。跨窗口文本去重仍属于任务策略，因为识别文本和阅读顺序需要业务规则。</remarks>
        public static RoiTextDetectionResult Filter(TextDetectionResult detections, VisualRoiSnapshot snapshot, float? thresholdOverride = null, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (detections.SourceSize != snapshot.SourceSize) throw new VisualException(VisualErrorCodes.InputInvalid, "OCR ROI filtering requires matching source dimensions.");
            if (thresholdOverride.HasValue && (float.IsNaN(thresholdOverride.Value) || float.IsInfinity(thresholdOverride.Value) || thresholdOverride.Value < 0 || thresholdOverride.Value > 1)) throw new ArgumentOutOfRangeException(nameof(thresholdOverride));
            var rois = snapshot.Rois
                .Where(value => value.Enabled && (value.AppliesTo(VisualTaskId.OpticalCharacterRecognition) || value.AppliesTo(VisualTaskId.TextDetection)))
                .Select(value => (Roi: value, Geometry: VisualRoiResolution.Resolve(snapshot, value, coordinateContext)))
                .ToList();
            bool hasInclude = rois.Any(value => value.Roi.InclusionMode == RoiInclusionMode.Include);
            var retained = new List<RoiTextRegion>();
            foreach (TextRegion region in detections.Regions)
            {
                var includeIds = new List<string>();
                bool excluded = false;
                foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in rois)
                {
                    if (!PassesConfidence(region.Score, entry.Roi) || !IsHit(region, entry.Geometry, entry.Roi, thresholdOverride)) continue;
                    if (entry.Roi.InclusionMode == RoiInclusionMode.Exclude) excluded = true;
                    else includeIds.Add(entry.Roi.Id);
                }

                if (!excluded && (!hasInclude || includeIds.Count > 0)) retained.Add(new RoiTextRegion(region, hasInclude ? includeIds : Array.Empty<string>()));
            }
            return new RoiTextDetectionResult(snapshot.SourceSize, snapshot.Version, retained);
        }

        private static bool PassesConfidence(float score, VisualRoi roi) => !roi.ConfidenceOverride.HasValue || score >= roi.ConfidenceOverride.Value;

        private static bool IsHit(TextRegion region, IVisualRoiGeometry geometry, VisualRoi roi, float? thresholdOverride)
        {
            PointF center = Centroid(region.Polygon.Vertices);
            if (roi.HitTestMode == RoiHitTestMode.CenterPoint) return geometry.Contains(center);
            if (roi.HitTestMode == RoiHitTestMode.AnyIntersection) return CalculatePolygonIntersectionArea(region.Polygon.Vertices, geometry) > 0.000001f;

            float intersection = CalculatePolygonIntersectionArea(region.Polygon.Vertices, geometry);
            float resultArea = region.Polygon.Area;
            float threshold = thresholdOverride ?? roi.HitThreshold;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverResult) return resultArea > 0 && intersection / resultArea >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverRoi) return geometry.Area > 0 && intersection / geometry.Area >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IoU)
            {
                float union = resultArea + geometry.Area - intersection;
                return union > 0 && intersection / union >= threshold;
            }
            return intersection > 0.000001f;
        }

        internal static float CalculatePolygonIntersectionArea(IReadOnlyList<PointF> text, IVisualRoiGeometry geometry)
        {
            if (geometry is MaskRoiGeometry mask)
            {
                RectangleF bounds = Bounds(text);
                int left = Math.Max(0, (int)Math.Floor(bounds.X));
                int top = Math.Max(0, (int)Math.Floor(bounds.Y));
                int right = Math.Min(mask.SourceSize.Width, (int)Math.Ceiling(bounds.Right));
                int bottom = Math.Min(mask.SourceSize.Height, (int)Math.Ceiling(bounds.Bottom));
                float area = 0;
                for (int y = top; y < bottom; y++)
                {
                    for (int x = left; x < right; x++)
                    {
                        if (mask.IsSet(x, y)) area += ConvexIntersectionArea(text, new[]
                        {
                            new PointF(x, y), new PointF(x + 1, y), new PointF(x + 1, y + 1), new PointF(x, y + 1)
                        });
                    }
                }
                return area;
            }

            IReadOnlyList<PointF> roiPoints = geometry.Points;
            if (geometry.Kind == VisualRoiGeometryKind.Rectangle || geometry.Kind == VisualRoiGeometryKind.RotatedRectangle)
            {
                return ConvexIntersectionArea(text, roiPoints);
            }

            float total = 0;
            foreach (IReadOnlyList<PointF> triangle in Triangulate(roiPoints)) total += ConvexIntersectionArea(text, triangle);
            return total;
        }

        private static RectangleF Bounds(IReadOnlyList<PointF> points)
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

        private static PointF Centroid(IReadOnlyList<PointF> polygon)
        {
            double areaTwice = 0;
            double x = 0;
            double y = 0;
            for (int index = 0; index < polygon.Count; index++)
            {
                PointF first = polygon[index];
                PointF second = polygon[(index + 1) % polygon.Count];
                double cross = (first.X * second.Y) - (second.X * first.Y);
                areaTwice += cross;
                x += (first.X + second.X) * cross;
                y += (first.Y + second.Y) * cross;
            }
            if (Math.Abs(areaTwice) < 0.000001) return new PointF(polygon[0].X, polygon[0].Y);
            return new PointF((float)(x / (3 * areaTwice)), (float)(y / (3 * areaTwice)));
        }

        private static float ConvexIntersectionArea(IReadOnlyList<PointF> subject, IReadOnlyList<PointF> clipPolygon)
        {
            var clipped = subject.ToList();
            float orientation = SignedArea(clipPolygon) >= 0 ? 1f : -1f;
            for (int edge = 0; edge < clipPolygon.Count && clipped.Count >= 3; edge++)
            {
                PointF start = clipPolygon[edge];
                PointF end = clipPolygon[(edge + 1) % clipPolygon.Count];
                var output = new List<PointF>();
                PointF previous = clipped[clipped.Count - 1];
                bool previousInside = IsInside(start, end, previous, orientation);
                for (int index = 0; index < clipped.Count; index++)
                {
                    PointF current = clipped[index];
                    bool currentInside = IsInside(start, end, current, orientation);
                    if (currentInside != previousInside) output.Add(LineIntersection(previous, current, start, end));
                    if (currentInside) output.Add(current);
                    previous = current;
                    previousInside = currentInside;
                }
                clipped = output;
            }
            return clipped.Count < 3 ? 0 : Math.Abs(SignedArea(clipped));
        }

        private static IEnumerable<IReadOnlyList<PointF>> Triangulate(IReadOnlyList<PointF> polygon)
        {
            var remaining = Enumerable.Range(0, polygon.Count).ToList();
            float orientation = SignedArea(polygon) >= 0 ? 1f : -1f;
            int guard = polygon.Count * polygon.Count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int index = 0; index < remaining.Count; index++)
                {
                    int previous = remaining[(index + remaining.Count - 1) % remaining.Count];
                    int current = remaining[index];
                    int next = remaining[(index + 1) % remaining.Count];
                    if (orientation * Orientation(polygon[previous], polygon[current], polygon[next]) <= 0.000001f) continue;
                    var triangle = new[] { polygon[previous], polygon[current], polygon[next] };
                    bool containsVertex = false;
                    for (int other = 0; other < remaining.Count; other++)
                    {
                        int candidate = remaining[other];
                        if (candidate == previous || candidate == current || candidate == next) continue;
                        if (PointInTriangle(polygon[candidate], triangle)) { containsVertex = true; break; }
                    }
                    if (containsVertex) continue;
                    yield return triangle;
                    remaining.RemoveAt(index);
                    clipped = true;
                    break;
                }
                if (!clipped) yield break;
            }
            if (remaining.Count == 3) yield return new[] { polygon[remaining[0]], polygon[remaining[1]], polygon[remaining[2]] };
        }

        private static bool IsInside(PointF start, PointF end, PointF point, float orientation) => orientation * Orientation(start, end, point) >= -0.000001f;

        private static PointF LineIntersection(PointF first, PointF second, PointF clipStart, PointF clipEnd)
        {
            double lineX = second.X - first.X;
            double lineY = second.Y - first.Y;
            double clipX = clipEnd.X - clipStart.X;
            double clipY = clipEnd.Y - clipStart.Y;
            double denominator = (lineX * clipY) - (lineY * clipX);
            if (Math.Abs(denominator) < 0.0000001) return second;
            double offsetX = clipStart.X - first.X;
            double offsetY = clipStart.Y - first.Y;
            double t = ((offsetX * clipY) - (offsetY * clipX)) / denominator;
            return new PointF((float)(first.X + (t * lineX)), (float)(first.Y + (t * lineY)));
        }

        private static bool PointInTriangle(PointF point, IReadOnlyList<PointF> triangle)
        {
            float first = Orientation(triangle[0], triangle[1], point);
            float second = Orientation(triangle[1], triangle[2], point);
            float third = Orientation(triangle[2], triangle[0], point);
            return (first >= -0.000001f && second >= -0.000001f && third >= -0.000001f) || (first <= 0.000001f && second <= 0.000001f && third <= 0.000001f);
        }

        private static float SignedArea(IReadOnlyList<PointF> points)
        {
            double area = 0;
            for (int index = 0; index < points.Count; index++)
            {
                PointF current = points[index];
                PointF next = points[(index + 1) % points.Count];
                area += ((double)current.X * next.Y) - ((double)next.X * current.Y);
            }
            return (float)(area * .5);
        }

        private static float Orientation(PointF first, PointF second, PointF third) => ((second.X - first.X) * (third.Y - first.Y)) - ((second.Y - first.Y) * (third.X - first.X));
    }
}
