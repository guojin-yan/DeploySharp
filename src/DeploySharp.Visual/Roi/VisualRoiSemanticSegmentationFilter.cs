using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains one semantic class count measured inside an ROI. / 包含 ROI 内测量得到的一个语义类别计数。</summary>
    public sealed class RoiSemanticSegmentationClassStatistics
    {
        /// <summary>Initializes ROI class statistics. / 初始化 ROI 类别统计。</summary>
        public RoiSemanticSegmentationClassStatistics(int classIndex, long pixelCount, double fraction)
        {
            if (classIndex < 0 || classIndex > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(classIndex));
            if (pixelCount < 0) throw new ArgumentOutOfRangeException(nameof(pixelCount));
            if (double.IsNaN(fraction) || double.IsInfinity(fraction) || fraction < 0 || fraction > 1) throw new ArgumentOutOfRangeException(nameof(fraction));
            ClassIndex = classIndex;
            PixelCount = pixelCount;
            Fraction = fraction;
        }

        /// <summary>Gets the zero-based semantic class index. / 获取从零开始的语义类别索引。</summary>
        public int ClassIndex { get; }
        /// <summary>Gets the number of eligible pixels assigned to the class. / 获取归属该类别的有效像素数。</summary>
        public long PixelCount { get; }
        /// <summary>Gets the fraction among eligible ROI pixels. / 获取该类别占 ROI 有效像素的比例。</summary>
        public double Fraction { get; }
    }

    /// <summary>Contains semantic segmentation statistics for one ROI. / 包含一个 ROI 的语义分割统计信息。</summary>
    public sealed class RoiSemanticSegmentationRegion
    {
        private readonly IReadOnlyList<RoiSemanticSegmentationClassStatistics> _statistics;

        internal RoiSemanticSegmentationRegion(string roiId, int priority, RectangleF bounds, long roiPixelCount, long classifiedPixelCount, int? dominantClassIndex, double dominantFraction, IEnumerable<RoiSemanticSegmentationClassStatistics> statistics)
        {
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            if (statistics == null) throw new ArgumentNullException(nameof(statistics));
            RoiId = roiId;
            Priority = priority;
            Bounds = bounds;
            RoiPixelCount = roiPixelCount;
            ClassifiedPixelCount = classifiedPixelCount;
            DominantClassIndex = dominantClassIndex;
            DominantFraction = dominantFraction;
            _statistics = new ReadOnlyCollection<RoiSemanticSegmentationClassStatistics>(statistics.ToList());
        }

        /// <summary>Gets the ROI identifier. / 获取 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets the ROI priority. / 获取 ROI 优先级。</summary>
        public int Priority { get; }
        /// <summary>Gets the clipped source-space ROI bounds. / 获取裁切后的源图 ROI 边界。</summary>
        public RectangleF Bounds { get; }
        /// <summary>Gets the number of source pixels spatially covered after Exclude ROIs. / 获取排除 Exclude ROI 后空间覆盖的源像素数。</summary>
        public long RoiPixelCount { get; }
        /// <summary>Gets the number of pixels included after the optional class filter. / 获取应用可选类别过滤后的有效像素数。</summary>
        public long ClassifiedPixelCount { get; }
        /// <summary>Gets the dominant class, or null when no pixel passed the class filter. / 获取主类别；没有像素通过类别过滤时为空。</summary>
        public int? DominantClassIndex { get; }
        /// <summary>Gets the dominant-class fraction among classified pixels. / 获取主类别占有效像素的比例。</summary>
        public double DominantFraction { get; }
        /// <summary>Gets deterministic per-class statistics ordered by class index. / 获取按类别索引确定性排序的统计信息。</summary>
        public IReadOnlyList<RoiSemanticSegmentationClassStatistics> Statistics => _statistics;
    }

    /// <summary>Contains per-ROI semantic segmentation statistics and snapshot provenance. / 包含逐 ROI 语义分割统计及快照来源信息。</summary>
    public sealed class RoiSemanticSegmentationResult
    {
        private readonly IReadOnlyList<RoiSemanticSegmentationRegion> _regions;

        internal RoiSemanticSegmentationResult(VisualSize sourceSize, long snapshotVersion, IEnumerable<RoiSemanticSegmentationRegion> regions)
        {
            if (snapshotVersion <= 0) throw new ArgumentOutOfRangeException(nameof(snapshotVersion));
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            _regions = new ReadOnlyCollection<RoiSemanticSegmentationRegion>(regions.ToList());
        }

        /// <summary>Gets source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the ROI snapshot version used for sampling. / 获取采样所用 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets regions in deterministic snapshot order. / 获取按快照顺序排列的区域。</summary>
        public IReadOnlyList<RoiSemanticSegmentationRegion> Regions => _regions;
    }

    /// <summary>Computes pixel-level semantic statistics inside Include ROIs without mutating the source mask. / 在 Include ROI 内计算像素级语义统计且不修改源掩码。</summary>
    public static class VisualRoiSemanticSegmentationFilter
    {
        /// <summary>Samples the source-resolution semantic mask for each applicable Include ROI. / 为每个适用的 Include ROI 采样源分辨率语义掩码。</summary>
        /// <remarks>The decoder must have restored the mask to source dimensions. Exclude ROIs remove covered pixels from every Include region. A class filter limits the denominator and statistics to selected classes. This is a statistics contract; it does not invent a cross-ROI fused mask. / decoder 必须已将掩码恢复到源图尺寸。Exclude ROI 会从所有 Include 区域移除覆盖像素；类别过滤器会将分母和统计限制为选中类别。这是统计合同，不会虚构跨 ROI 融合掩码。</remarks>
        public static RoiSemanticSegmentationResult Filter(SemanticSegmentationResult segmentation, VisualRoiSnapshot snapshot, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (segmentation == null) throw new ArgumentNullException(nameof(segmentation));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (segmentation.Mask.Width != snapshot.SourceSize.Width || segmentation.Mask.Height != snapshot.SourceSize.Height)
            {
                throw new VisualException(VisualErrorCodes.InputInvalid, "Semantic ROI filtering requires a source-resolution semantic mask.", technicalDetails: "mask=" + segmentation.Mask.Width + "x" + segmentation.Mask.Height + ";source=" + snapshot.SourceSize.Width + "x" + snapshot.SourceSize.Height);
            }

            var applicable = snapshot.Rois
                .Where(value => value.Enabled && value.AppliesTo(VisualTaskId.SemanticSegmentation))
                .Select(value => (Roi: value, Geometry: VisualRoiResolution.Resolve(snapshot, value, coordinateContext)))
                .ToList();
            foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in applicable)
            {
                if (entry.Roi.ConfidenceOverride.HasValue) throw new VisualException(VisualErrorCodes.InputInvalid, "Semantic segmentation ROIs cannot use confidenceOverride because the result is a label/probability map.", technicalDetails: "roi=" + entry.Roi.Id);
            }
            var excludes = applicable.Where(value => value.Roi.InclusionMode == RoiInclusionMode.Exclude).ToList();
            var includes = applicable.Where(value => value.Roi.InclusionMode == RoiInclusionMode.Include).ToList();
            var regions = new List<RoiSemanticSegmentationRegion>(includes.Count);
            foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in includes)
            {
                regions.Add(Sample(entry.Roi, entry.Geometry, excludes, segmentation));
            }

            return new RoiSemanticSegmentationResult(snapshot.SourceSize, snapshot.Version, regions);
        }

        private static RoiSemanticSegmentationRegion Sample(VisualRoi roi, IVisualRoiGeometry geometry, IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> excludes, SemanticSegmentationResult segmentation)
        {
            RectangleF bounds = ClipBounds(geometry.Bounds, segmentation.Mask.Width, segmentation.Mask.Height);
            int left = Math.Max(0, (int)Math.Floor(bounds.X));
            int top = Math.Max(0, (int)Math.Floor(bounds.Y));
            int right = Math.Min(segmentation.Mask.Width, (int)Math.Ceiling(bounds.Right));
            int bottom = Math.Min(segmentation.Mask.Height, (int)Math.Ceiling(bounds.Bottom));
            var counts = new SortedDictionary<int, long>();
            long roiPixels = 0;
            bool hasClassFilter = roi.ClassFilter.Count > 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    PointF center = new PointF(x + .5f, y + .5f);
                    if (!geometry.Contains(center)) continue;
                    int classIndex = segmentation.Mask.GetClassIndex(x, y);
                    if (IsExcluded(center, classIndex, excludes)) continue;
                    roiPixels++;
                    if (hasClassFilter && !roi.ClassFilter.Contains(classIndex)) continue;
                    counts[classIndex] = counts.TryGetValue(classIndex, out long current) ? current + 1 : 1;
                }
            }

            long classifiedPixels = counts.Values.Sum();
            var statistics = counts.Select(value => new RoiSemanticSegmentationClassStatistics(value.Key, value.Value, classifiedPixels == 0 ? 0 : (double)value.Value / classifiedPixels)).ToList();
            int? dominant = null;
            double dominantFraction = 0;
            if (statistics.Count > 0)
            {
                RoiSemanticSegmentationClassStatistics winner = statistics.OrderByDescending(value => value.PixelCount).ThenBy(value => value.ClassIndex).First();
                dominant = winner.ClassIndex;
                dominantFraction = winner.Fraction;
            }

            return new RoiSemanticSegmentationRegion(roi.Id, roi.Priority, bounds, roiPixels, classifiedPixels, dominant, dominantFraction, statistics);
        }

        private static bool IsExcluded(PointF point, int classIndex, IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> excludes)
        {
            foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) exclude in excludes)
            {
                if (exclude.Roi.AppliesTo(VisualTaskId.SemanticSegmentation, classIndex) && exclude.Geometry.Contains(point)) return true;
            }
            return false;
        }

        private static RectangleF ClipBounds(RectangleF bounds, int width, int height)
        {
            float left = Math.Max(0, Math.Min(width, bounds.X));
            float top = Math.Max(0, Math.Min(height, bounds.Y));
            float right = Math.Max(left, Math.Min(width, bounds.Right));
            float bottom = Math.Max(top, Math.Min(height, bounds.Bottom));
            return new RectangleF(left, top, right - left, bottom - top);
        }
    }
}
