using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents one OBB retained by ROI filtering. / 表示一个经过 ROI 过滤后保留的 OBB。</summary>
    public sealed class RoiOrientedDetection
    {
        private readonly IReadOnlyList<string> _roiIds;

        /// <summary>Initializes an OBB with matching ROI identifiers. / 使用命中的 ROI 标识初始化 OBB。</summary>
        public RoiOrientedDetection(OrientedDetection detection, IEnumerable<string> roiIds)
        {
            Detection = detection ?? throw new ArgumentNullException(nameof(detection));
            if (roiIds == null) throw new ArgumentNullException(nameof(roiIds));
            _roiIds = new ReadOnlyCollection<string>(roiIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
        }

        /// <summary>Gets the source-space OBB. / 获取源图空间 OBB。</summary>
        public OrientedDetection Detection { get; }
        /// <summary>Gets matching Include ROI identifiers. / 获取命中的 Include ROI 标识。</summary>
        public IReadOnlyList<string> RoiIds => _roiIds;
        /// <summary>Gets the deterministic primary ROI identifier. / 获取确定性的主 ROI 标识。</summary>
        public string? PrimaryRoiId => _roiIds.Count == 0 ? null : _roiIds[0];
    }

    /// <summary>Contains OBBs retained by ROI filtering. / 包含 ROI 过滤后保留的 OBB。</summary>
    public sealed class RoiOrientedDetectionResult
    {
        private readonly IReadOnlyList<RoiOrientedDetection> _detections;

        internal RoiOrientedDetectionResult(VisualSize sourceSize, long snapshotVersion, IEnumerable<RoiOrientedDetection> detections)
        {
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            _detections = new ReadOnlyCollection<RoiOrientedDetection>(detections.ToList());
        }

        /// <summary>Gets source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the ROI snapshot version. / 获取 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets retained OBBs in decoder order. / 获取按解码器顺序保留的 OBB。</summary>
        public IReadOnlyList<RoiOrientedDetection> Detections => _detections;
    }

    /// <summary>Filters source-space OBB results against Include and Exclude ROIs. / 针对 Include 和 Exclude ROI 过滤源图 OBB 结果。</summary>
    public static class VisualRoiOrientedDetectionFilter
    {
        /// <summary>Filters OBBs using their authoritative quadrilateral bounds. / 使用 OBB 权威四边形边界过滤结果。</summary>
        /// <remarks>All hit modes use the authoritative OBB quadrilateral. CenterPoint is exact; area modes use convex polygon intersection against the ROI and therefore do not substitute an axis-aligned box for rotated geometry. / 所有命中模式均使用权威 OBB 四边形：CenterPoint 精确判断，面积模式使用凸多边形与 ROI 的相交面积，不再用轴对齐外接框替代旋转几何。</remarks>
        public static RoiOrientedDetectionResult Filter(OrientedDetectionResult detections, VisualRoiSnapshot snapshot, float? thresholdOverride = null, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (thresholdOverride.HasValue && (float.IsNaN(thresholdOverride.Value) || float.IsInfinity(thresholdOverride.Value) || thresholdOverride.Value < 0 || thresholdOverride.Value > 1)) throw new ArgumentOutOfRangeException(nameof(thresholdOverride));
            var rois = snapshot.Rois.Where(value => value.Enabled && value.AppliesTo(VisualTaskId.OrientedObjectDetection)).Select(value => (Roi: value, Geometry: VisualRoiResolution.Resolve(snapshot, value, coordinateContext))).ToList();
            bool hasInclude = rois.Any(value => value.Roi.InclusionMode == RoiInclusionMode.Include);
            var retained = new List<RoiOrientedDetection>();
            foreach (OrientedDetection detection in detections.Detections)
            {
                var includeIds = new List<string>();
                bool excluded = false;
                foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in rois)
                {
                    if (!entry.Roi.AppliesTo(VisualTaskId.OrientedObjectDetection, detection.ClassIndex) || !PassesConfidence(detection.Score, entry.Roi) || !IsHit(detection, entry.Geometry, entry.Roi, thresholdOverride)) continue;
                    if (entry.Roi.InclusionMode == RoiInclusionMode.Exclude) excluded = true;
                    else includeIds.Add(entry.Roi.Id);
                }
                if (!excluded && (!hasInclude || includeIds.Count > 0)) retained.Add(new RoiOrientedDetection(detection, hasInclude ? includeIds : Array.Empty<string>()));
            }
            return new RoiOrientedDetectionResult(snapshot.SourceSize, snapshot.Version, retained);
        }

        private static bool PassesConfidence(float score, VisualRoi roi) => !roi.ConfidenceOverride.HasValue || score >= roi.ConfidenceOverride.Value;

        private static bool IsHit(OrientedDetection detection, IVisualRoiGeometry geometry, VisualRoi roi, float? thresholdOverride)
        {
            if (roi.HitTestMode == RoiHitTestMode.CenterPoint) return geometry.Contains(detection.Center);
            float intersection = VisualRoiTextDetectionFilter.CalculatePolygonIntersectionArea(detection.Quadrilateral.Vertices, geometry);
            float resultArea = detection.Quadrilateral.Area;
            float threshold = thresholdOverride ?? roi.HitThreshold;
            if (roi.HitTestMode == RoiHitTestMode.AnyIntersection) return intersection > 0.000001f;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverResult) return resultArea > 0 && intersection / resultArea >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverRoi) return geometry.Area > 0 && intersection / geometry.Area >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IoU)
            {
                float union = resultArea + geometry.Area - intersection;
                return union > 0 && intersection / union >= threshold;
            }
            return intersection > 0.000001f;
        }
    }
}
