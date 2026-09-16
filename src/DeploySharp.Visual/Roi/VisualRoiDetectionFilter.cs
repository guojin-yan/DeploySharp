using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents one detection retained by ROI filtering. / 表示一个经过 ROI 过滤后保留的检测。</summary>
    public sealed class RoiDetection
    {
        private readonly IReadOnlyList<string> _roiIds;

        /// <summary>Initializes a detection with the matching ROI identifiers. / 使用命中的 ROI 标识初始化检测。</summary>
        public RoiDetection(Detection detection, IEnumerable<string> roiIds)
        {
            Detection = detection ?? throw new ArgumentNullException(nameof(detection));
            if (roiIds == null) throw new ArgumentNullException(nameof(roiIds));
            var ids = roiIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            _roiIds = new ReadOnlyCollection<string>(ids);
        }

        /// <summary>Gets the original source-space detection. / 获取原始源图空间检测。</summary>
        public Detection Detection { get; }
        /// <summary>Gets matching include ROI identifiers. / 获取命中的包含 ROI 标识。</summary>
        public IReadOnlyList<string> RoiIds => _roiIds;
        /// <summary>Gets the deterministic primary ROI identifier. / 获取确定性的主 ROI 标识。</summary>
        public string? PrimaryRoiId => _roiIds.Count == 0 ? null : _roiIds[0];
    }

    /// <summary>Contains source-image detections retained by a ROI filter. / 包含 ROI 过滤后保留的源图检测。</summary>
    public sealed class RoiDetectionResult
    {
        private readonly IReadOnlyList<RoiDetection> _detections;

        /// <summary>Initializes a ROI detection result. / 初始化 ROI 检测结果。</summary>
        public RoiDetectionResult(VisualSize sourceSize, long snapshotVersion, IEnumerable<RoiDetection> detections)
        {
            if (snapshotVersion <= 0) throw new ArgumentOutOfRangeException(nameof(snapshotVersion));
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            var copy = detections.ToList();
            if (copy.Any(value => value == null)) throw new ArgumentException("ROI detections cannot contain null values.", nameof(detections));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            _detections = new ReadOnlyCollection<RoiDetection>(copy);
        }

        /// <summary>Gets source image size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the ROI snapshot version used for filtering. / 获取过滤所用 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets retained detections in original decoder order. / 获取按解码器原始顺序保留的检测。</summary>
        public IReadOnlyList<RoiDetection> Detections => _detections;
    }

    /// <summary>Provides backend-neutral detection filtering against an immutable ROI snapshot. / 提供基于不可变 ROI 快照的后端无关检测过滤。</summary>
    public static class VisualRoiDetectionFilter
    {
        /// <summary>Filters detections using Include and Exclude ROIs. / 使用包含和排除 ROI 过滤检测。</summary>
        public static RoiDetectionResult Filter(DetectionResult detections, VisualRoiSnapshot snapshot, VisualTaskId task, float? thresholdOverride = null, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (task.IsEmpty) throw new ArgumentException("A visual task is required.", nameof(task));
            if (thresholdOverride.HasValue && (float.IsNaN(thresholdOverride.Value) || float.IsInfinity(thresholdOverride.Value) || thresholdOverride.Value < 0 || thresholdOverride.Value > 1)) throw new ArgumentOutOfRangeException(nameof(thresholdOverride));
            var sourceRois = new List<(VisualRoi Roi, IVisualRoiGeometry Geometry)>();
            foreach (VisualRoi roi in snapshot.Rois) if (roi.Enabled && roi.AppliesTo(task)) sourceRois.Add((roi, VisualRoiResolution.Resolve(snapshot, roi, coordinateContext)));
            bool hasInclude = sourceRois.Any(value => value.Roi.InclusionMode == RoiInclusionMode.Include);
            var retained = new List<RoiDetection>();
            for (int index = 0; index < detections.Detections.Count; index++)
            {
                Detection detection = detections.Detections[index];
                var includeIds = new List<string>();
                bool excluded = false;
                for (int roiIndex = 0; roiIndex < sourceRois.Count; roiIndex++)
                {
                    (VisualRoi roi, IVisualRoiGeometry geometry) = sourceRois[roiIndex];
                    if (!roi.AppliesTo(task, detection.Label.Index) || !PassesConfidence(detection.Label.Score, roi) || !IsHit(detection.Box, geometry, roi, thresholdOverride)) continue;
                    if (roi.InclusionMode == RoiInclusionMode.Exclude) excluded = true;
                    else includeIds.Add(roi.Id);
                }
                if (!excluded && (!hasInclude || includeIds.Count > 0)) retained.Add(new RoiDetection(detection, hasInclude ? includeIds : Array.Empty<string>()));
            }
            return new RoiDetectionResult(snapshot.SourceSize, snapshot.Version, retained);
        }

        private static bool IsHit(RectangleF result, IVisualRoiGeometry geometry, VisualRoi roi, float? thresholdOverride)
        {
            RectangleF bounds = geometry.Bounds;
            float intersection = CalculateIntersectionArea(result, geometry);
            float resultArea = Math.Max(0, result.Width) * Math.Max(0, result.Height);
            float roiArea = geometry.Area;
            float threshold = thresholdOverride ?? roi.HitThreshold;
            if (roi.HitTestMode == RoiHitTestMode.CenterPoint) return geometry.Contains(new PointF(result.X + result.Width / 2f, result.Y + result.Height / 2f));
            if (roi.HitTestMode == RoiHitTestMode.AnyIntersection) return intersection > 0;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverResult) return resultArea > 0 && intersection / resultArea >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverRoi) return roiArea > 0 && intersection / roiArea >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IoU)
            {
                float union = resultArea + roiArea - intersection;
                return union > 0 && intersection / union >= threshold;
            }
            return bounds.Width > 0 && bounds.Height > 0 && intersection > 0;
        }

        private static bool PassesConfidence(float score, VisualRoi roi) => !roi.ConfidenceOverride.HasValue || score >= roi.ConfidenceOverride.Value;

        internal static float CalculateIntersectionArea(RectangleF rectangle, IVisualRoiGeometry geometry)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            return geometry is MaskRoiGeometry mask
                ? IntersectionArea(rectangle, mask)
                : IntersectionArea(rectangle, geometry.Points);
        }

        private static float IntersectionArea(RectangleF rectangle, IReadOnlyList<PointF> polygon)
        {
            var clipped = polygon.ToList();
            clipped = Clip(clipped, point => point.X >= rectangle.X, (from, to) => IntersectVertical(from, to, rectangle.X));
            clipped = Clip(clipped, point => point.X <= rectangle.Right, (from, to) => IntersectVertical(from, to, rectangle.Right));
            clipped = Clip(clipped, point => point.Y >= rectangle.Y, (from, to) => IntersectHorizontal(from, to, rectangle.Y));
            clipped = Clip(clipped, point => point.Y <= rectangle.Bottom, (from, to) => IntersectHorizontal(from, to, rectangle.Bottom));
            return clipped.Count < 3 ? 0 : Math.Abs(VisualRoiGeometryMath.SignedArea(clipped));
        }

        private static float IntersectionArea(RectangleF rectangle, MaskRoiGeometry mask)
        {
            int left = Math.Max(0, (int)Math.Floor(rectangle.X));
            int top = Math.Max(0, (int)Math.Floor(rectangle.Y));
            int right = Math.Min(mask.SourceSize.Width, (int)Math.Ceiling(rectangle.Right));
            int bottom = Math.Min(mask.SourceSize.Height, (int)Math.Ceiling(rectangle.Bottom));
            if (right <= left || bottom <= top) return 0;
            int area = 0;
            for (int y = top; y < bottom; y++) for (int x = left; x < right; x++) if (mask.IsSet(x, y)) area++;
            return area;
        }

        private static List<PointF> Clip(List<PointF> input, Func<PointF, bool> inside, Func<PointF, PointF, PointF> intersection)
        {
            if (input.Count == 0) return input;
            var output = new List<PointF>();
            PointF previous = input[input.Count - 1];
            bool previousInside = inside(previous);
            for (int index = 0; index < input.Count; index++)
            {
                PointF current = input[index];
                bool currentInside = inside(current);
                if (currentInside != previousInside) output.Add(intersection(previous, current));
                if (currentInside) output.Add(current);
                previous = current;
                previousInside = currentInside;
            }
            return output;
        }

        private static PointF IntersectVertical(PointF first, PointF second, float x)
        {
            float denominator = second.X - first.X;
            if (Math.Abs(denominator) < 0.000001f) return new PointF(x, first.Y);
            float t = (x - first.X) / denominator;
            return new PointF(x, first.Y + ((second.Y - first.Y) * t));
        }

        private static PointF IntersectHorizontal(PointF first, PointF second, float y)
        {
            float denominator = second.Y - first.Y;
            if (Math.Abs(denominator) < 0.000001f) return new PointF(first.X, y);
            float t = (y - first.Y) / denominator;
            return new PointF(first.X + ((second.X - first.X) * t), y);
        }
    }
}
