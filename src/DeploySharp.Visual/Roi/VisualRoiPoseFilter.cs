using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents one Pose instance retained by ROI filtering. / 表示一个经过 ROI 过滤后保留的姿态实例。</summary>
    public sealed class RoiPoseInstance
    {
        private readonly IReadOnlyList<string> _roiIds;

        /// <summary>Initializes a Pose instance with matching ROI identifiers. / 使用命中的 ROI 标识初始化姿态实例。</summary>
        public RoiPoseInstance(PoseInstance instance, IEnumerable<string> roiIds)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            if (roiIds == null) throw new ArgumentNullException(nameof(roiIds));
            _roiIds = new ReadOnlyCollection<string>(roiIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
        }

        /// <summary>Gets the original source-space Pose instance. / 获取原始源图空间姿态实例。</summary>
        public PoseInstance Instance { get; }
        /// <summary>Gets matching Include ROI identifiers. / 获取命中的 Include ROI 标识。</summary>
        public IReadOnlyList<string> RoiIds => _roiIds;
        /// <summary>Gets the deterministic primary ROI identifier. / 获取确定性的主 ROI 标识。</summary>
        public string? PrimaryRoiId => _roiIds.Count == 0 ? null : _roiIds[0];
    }

    /// <summary>Contains Pose instances retained by ROI filtering. / 包含 ROI 过滤后保留的姿态实例。</summary>
    public sealed class RoiPoseResult
    {
        private readonly IReadOnlyList<RoiPoseInstance> _instances;

        internal RoiPoseResult(VisualSize sourceSize, long snapshotVersion, PoseTopology topology, IEnumerable<RoiPoseInstance> instances)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _instances = new ReadOnlyCollection<RoiPoseInstance>(instances.ToList());
        }

        /// <summary>Gets source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the ROI snapshot version. / 获取 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets immutable Pose topology. / 获取不可变姿态拓扑。</summary>
        public PoseTopology Topology { get; }
        /// <summary>Gets retained instances in decoder order. / 获取按解码器顺序保留的姿态实例。</summary>
        public IReadOnlyList<RoiPoseInstance> Instances => _instances;
    }

    /// <summary>Filters source-space Pose instances against Include and Exclude ROIs. / 针对 Include 和 Exclude ROI 过滤源图姿态实例。</summary>
    public static class VisualRoiPoseFilter
    {
        /// <summary>Filters Pose instances by their declared or derived source-space bounds. / 按声明或推导的源图边界过滤姿态实例。</summary>
        /// <remarks>When a decoder provides no bounding box, the bounds of valid keypoints are used. This is a conservative fallback and does not mutate the Pose result. / decoder 未提供边界框时使用有效关键点边界；这是保守回退，不会修改 Pose 结果。</remarks>
        public static RoiPoseResult Filter(PoseEstimationResult poses, VisualRoiSnapshot snapshot, float? thresholdOverride = null, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (poses == null) throw new ArgumentNullException(nameof(poses));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (thresholdOverride.HasValue && (float.IsNaN(thresholdOverride.Value) || float.IsInfinity(thresholdOverride.Value) || thresholdOverride.Value < 0 || thresholdOverride.Value > 1)) throw new ArgumentOutOfRangeException(nameof(thresholdOverride));
            var rois = snapshot.Rois.Where(value => value.Enabled && value.AppliesTo(VisualTaskId.PoseEstimation)).Select(value => (Roi: value, Geometry: VisualRoiResolution.Resolve(snapshot, value, coordinateContext))).ToList();
            bool hasInclude = rois.Any(value => value.Roi.InclusionMode == RoiInclusionMode.Include);
            var retained = new List<RoiPoseInstance>();
            foreach (PoseInstance instance in poses.Instances)
            {
                RectangleF? bounds = GetBounds(instance);
                if (!bounds.HasValue) continue;
                var includeIds = new List<string>();
                bool excluded = false;
                foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in rois)
                {
                    if (!entry.Roi.AppliesTo(VisualTaskId.PoseEstimation, instance.ClassIndex) || !PassesConfidence(instance.Score, entry.Roi) || !IsHit(instance, bounds.Value, entry.Geometry, entry.Roi, thresholdOverride)) continue;
                    if (entry.Roi.InclusionMode == RoiInclusionMode.Exclude) excluded = true;
                    else includeIds.Add(entry.Roi.Id);
                }
                if (!excluded && (!hasInclude || includeIds.Count > 0)) retained.Add(new RoiPoseInstance(instance, hasInclude ? includeIds : Array.Empty<string>()));
            }
            return new RoiPoseResult(snapshot.SourceSize, snapshot.Version, poses.Topology, retained);
        }

        private static bool PassesConfidence(float score, VisualRoi roi) => !roi.ConfidenceOverride.HasValue || score >= roi.ConfidenceOverride.Value;

        private static RectangleF? GetBounds(PoseInstance instance)
        {
            if (instance.BoundingBox.HasValue) return instance.BoundingBox.Value;
            var valid = instance.Keypoints.Where(value => value.IsValid).Select(value => value.Point).ToList();
            if (valid.Count == 0) return null;
            float left = valid.Min(value => value.X);
            float top = valid.Min(value => value.Y);
            float right = valid.Max(value => value.X);
            float bottom = valid.Max(value => value.Y);
            return new RectangleF(left, top, Math.Max(0.0001f, right - left), Math.Max(0.0001f, bottom - top));
        }

        private static bool IsHit(PoseInstance instance, RectangleF result, IVisualRoiGeometry geometry, VisualRoi roi, float? thresholdOverride)
        {
            float threshold = thresholdOverride ?? roi.HitThreshold;
            if (roi.HitTestMode == RoiHitTestMode.KeypointCoverage || roi.HitTestMode == RoiHitTestMode.AllKeypoints || roi.HitTestMode == RoiHitTestMode.AnyKeypoint || roi.HitTestMode == RoiHitTestMode.VisibleKeypointRatio)
            {
                int eligible = 0;
                int inside = 0;
                foreach (PoseKeypoint keypoint in instance.Keypoints)
                {
                    bool visibleOnly = roi.HitTestMode == RoiHitTestMode.VisibleKeypointRatio;
                    if (!keypoint.IsValid || visibleOnly && keypoint.Visibility != PoseKeypointVisibility.Visible) continue;
                    eligible++;
                    if (geometry.Contains(keypoint.Point)) inside++;
                }
                if (eligible == 0) return false;
                if (roi.HitTestMode == RoiHitTestMode.AllKeypoints) return inside == eligible;
                if (roi.HitTestMode == RoiHitTestMode.AnyKeypoint) return inside > 0;
                return ((float)inside / eligible) >= threshold;
            }
            float intersection = VisualRoiTextDetectionFilter.CalculatePolygonIntersectionArea(new[]
            {
                new PointF(result.X, result.Y),
                new PointF(result.Right, result.Y),
                new PointF(result.Right, result.Bottom),
                new PointF(result.X, result.Bottom)
            }, geometry);
            float resultArea = Math.Max(0, result.Width) * Math.Max(0, result.Height);
            if (roi.HitTestMode == RoiHitTestMode.CenterPoint) return geometry.Contains(new PointF(result.X + result.Width / 2f, result.Y + result.Height / 2f));
            if (roi.HitTestMode == RoiHitTestMode.AnyIntersection) return intersection > 0;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverResult) return resultArea > 0 && intersection / resultArea >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverRoi) return geometry.Area > 0 && intersection / geometry.Area >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IoU)
            {
                float union = resultArea + geometry.Area - intersection;
                return union > 0 && intersection / union >= threshold;
            }
            return intersection > 0;
        }
    }
}
