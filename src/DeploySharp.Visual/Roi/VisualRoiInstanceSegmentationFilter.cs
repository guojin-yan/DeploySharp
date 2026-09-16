using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents one instance mask retained by ROI filtering. / 表示一个经过 ROI 过滤后保留的实例掩码。</summary>
    public sealed class RoiInstanceSegmentationInstance
    {
        private readonly IReadOnlyList<string> _roiIds;

        /// <summary>Initializes an instance with matching ROI identifiers. / 使用命中的 ROI 标识初始化实例。</summary>
        public RoiInstanceSegmentationInstance(InstanceSegmentationInstance instance, IEnumerable<string> roiIds)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            if (roiIds == null) throw new ArgumentNullException(nameof(roiIds));
            _roiIds = new ReadOnlyCollection<string>(roiIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
        }

        /// <summary>Gets the original source-space instance and owned mask. / 获取原始源图实例及其自有掩码。</summary>
        public InstanceSegmentationInstance Instance { get; }
        /// <summary>Gets matching Include ROI identifiers. / 获取命中的 Include ROI 标识。</summary>
        public IReadOnlyList<string> RoiIds => _roiIds;
        /// <summary>Gets the deterministic primary ROI identifier. / 获取确定性的主 ROI 标识。</summary>
        public string? PrimaryRoiId => _roiIds.Count == 0 ? null : _roiIds[0];
    }

    /// <summary>Contains instance masks retained by ROI filtering. / 包含 ROI 过滤后保留的实例掩码。</summary>
    public sealed class RoiInstanceSegmentationResult
    {
        private readonly IReadOnlyList<RoiInstanceSegmentationInstance> _instances;

        internal RoiInstanceSegmentationResult(VisualSize sourceSize, long snapshotVersion, IEnumerable<RoiInstanceSegmentationInstance> instances)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            _instances = new ReadOnlyCollection<RoiInstanceSegmentationInstance>(instances.ToList());
        }

        /// <summary>Gets source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the ROI snapshot version. / 获取 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets retained instances in decoder order. / 获取按 decoder 顺序保留的实例。</summary>
        public IReadOnlyList<RoiInstanceSegmentationInstance> Instances => _instances;
    }

    /// <summary>Filters source-space instance masks using pixel-level ROI intersection. / 使用像素级 ROI 相交过滤源图实例掩码。</summary>
    public static class VisualRoiInstanceSegmentationFilter
    {
        /// <summary>Filters instances by their dense source-space masks, preserving mask ownership and provenance. / 按稠密源图掩码过滤实例，同时保留掩码所有权和来源追溯。</summary>
        public static RoiInstanceSegmentationResult Filter(InstanceSegmentationResult instances, VisualRoiSnapshot snapshot, float? thresholdOverride = null, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (thresholdOverride.HasValue && (float.IsNaN(thresholdOverride.Value) || float.IsInfinity(thresholdOverride.Value) || thresholdOverride.Value < 0 || thresholdOverride.Value > 1)) throw new ArgumentOutOfRangeException(nameof(thresholdOverride));
            var rois = snapshot.Rois.Where(value => value.Enabled && value.AppliesTo(VisualTaskId.InstanceSegmentation)).Select(value => (Roi: value, Geometry: VisualRoiResolution.Resolve(snapshot, value, coordinateContext))).ToList();
            bool hasInclude = rois.Any(value => value.Roi.InclusionMode == RoiInclusionMode.Include);
            var retained = new List<RoiInstanceSegmentationInstance>();
            foreach (InstanceSegmentationInstance instance in instances.Instances)
            {
                var includeIds = new List<string>();
                bool excluded = false;
                foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in rois)
                {
                    if (!entry.Roi.AppliesTo(VisualTaskId.InstanceSegmentation, instance.ClassIndex) || !PassesConfidence(instance.Score, entry.Roi) || !IsHit(instance, entry.Geometry, entry.Roi, thresholdOverride)) continue;
                    if (entry.Roi.InclusionMode == RoiInclusionMode.Exclude) excluded = true;
                    else includeIds.Add(entry.Roi.Id);
                }
                if (!excluded && (!hasInclude || includeIds.Count > 0)) retained.Add(new RoiInstanceSegmentationInstance(instance, hasInclude ? includeIds : Array.Empty<string>()));
            }
            return new RoiInstanceSegmentationResult(snapshot.SourceSize, snapshot.Version, retained);
        }

        private static bool PassesConfidence(float score, VisualRoi roi) => !roi.ConfidenceOverride.HasValue || score >= roi.ConfidenceOverride.Value;

        private static bool IsHit(InstanceSegmentationInstance instance, IVisualRoiGeometry geometry, VisualRoi roi, float? thresholdOverride)
        {
            InstanceBinaryMask mask = instance.Mask;
            RectangleF? foregroundBounds = mask.GetForegroundBounds();
            if (!foregroundBounds.HasValue) return false;
            if (roi.HitTestMode == RoiHitTestMode.CenterPoint) return geometry.Contains(new PointF(foregroundBounds.Value.X + foregroundBounds.Value.Width / 2f, foregroundBounds.Value.Y + foregroundBounds.Value.Height / 2f));
            int intersection = CountIntersection(mask, geometry, foregroundBounds.Value);
            if (roi.HitTestMode == RoiHitTestMode.AnyIntersection) return intersection > 0;
            float foreground = mask.ForegroundPixelCount;
            float threshold = thresholdOverride ?? roi.HitThreshold;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverResult || roi.HitTestMode == RoiHitTestMode.MaskIntersectionRatio) return foreground > 0 && intersection / foreground >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IntersectionOverRoi) return geometry.Area > 0 && intersection / geometry.Area >= threshold;
            if (roi.HitTestMode == RoiHitTestMode.IoU)
            {
                float union = foreground + geometry.Area - intersection;
                return union > 0 && intersection / union >= threshold;
            }
            return intersection > 0;
        }

        private static int CountIntersection(InstanceBinaryMask mask, IVisualRoiGeometry geometry, RectangleF bounds)
        {
            int left = Math.Max(0, (int)Math.Floor(bounds.X - mask.OriginX));
            int top = Math.Max(0, (int)Math.Floor(bounds.Y - mask.OriginY));
            int right = Math.Min(mask.Width, (int)Math.Ceiling(bounds.Right - mask.OriginX));
            int bottom = Math.Min(mask.Height, (int)Math.Ceiling(bounds.Bottom - mask.OriginY));
            if (right <= left || bottom <= top) return 0;
            byte[] pixels = mask.GetPixelsUnsafe();
            int offset = mask.PixelOffset;
            int intersection = 0;
            for (int y = top; y < bottom; y++)
            {
                int row = offset + (y * mask.Width);
                for (int x = left; x < right; x++) if (pixels[row + x] != 0 && geometry.Contains(new PointF(mask.OriginX + x + .5f, mask.OriginY + y + .5f))) intersection++;
            }
            return intersection;
        }
    }
}
