using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines deterministic conflict handling for ROI alpha composition. / 定义 ROI Alpha 合成的确定性冲突处理。</summary>
    public enum RoiAlphaMergeMode
    {
        /// <summary>Keep the first candidate in caller order. / 保留调用顺序中的第一个候选。</summary>
        KeepFirst = 0,
        /// <summary>Keep the last candidate in caller order. / 保留调用顺序中的最后一个候选。</summary>
        KeepLast = 1,
        /// <summary>Choose the highest alpha value at each pixel. / 逐像素选择最大 Alpha 值。</summary>
        MaxAlpha = 2,
        /// <summary>Choose the candidate with the highest ROI priority at each pixel. / 逐像素选择 ROI 优先级最高的候选。</summary>
        RoiPriority = 3
    }

    /// <summary>Contains alpha statistics measured inside one ROI. / 包含一个 ROI 内测量得到的 Alpha 统计。</summary>
    public sealed class RoiAlphaRegion
    {
        internal RoiAlphaRegion(string roiId, int priority, RectangleF bounds, long coveredPixelCount, long opaquePixelCount, double meanAlpha, float maximumAlpha, float percentile95Alpha, float opaqueThreshold)
        {
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            if (coveredPixelCount < 0 || opaquePixelCount < 0 || opaquePixelCount > coveredPixelCount) throw new ArgumentOutOfRangeException(nameof(coveredPixelCount));
            if (float.IsNaN(opaqueThreshold) || float.IsInfinity(opaqueThreshold) || opaqueThreshold < 0 || opaqueThreshold > 1) throw new ArgumentOutOfRangeException(nameof(opaqueThreshold));
            if (double.IsNaN(meanAlpha) || double.IsInfinity(meanAlpha) || meanAlpha < 0 || meanAlpha > 1) throw new ArgumentOutOfRangeException(nameof(meanAlpha));
            if (float.IsNaN(maximumAlpha) || float.IsInfinity(maximumAlpha) || maximumAlpha < 0 || maximumAlpha > 1) throw new ArgumentOutOfRangeException(nameof(maximumAlpha));
            if (float.IsNaN(percentile95Alpha) || float.IsInfinity(percentile95Alpha) || percentile95Alpha < 0 || percentile95Alpha > 1) throw new ArgumentOutOfRangeException(nameof(percentile95Alpha));
            RoiId = roiId;
            Priority = priority;
            Bounds = bounds;
            CoveredPixelCount = coveredPixelCount;
            OpaquePixelCount = opaquePixelCount;
            MeanAlpha = meanAlpha;
            MaximumAlpha = maximumAlpha;
            Percentile95Alpha = percentile95Alpha;
            OpaqueThreshold = opaqueThreshold;
            OpaqueRatio = coveredPixelCount == 0 ? 0 : (double)opaquePixelCount / coveredPixelCount;
        }

        /// <summary>Gets ROI identifier. / 获取 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets ROI priority. / 获取 ROI 优先级。</summary>
        public int Priority { get; }
        /// <summary>Gets clipped source-space bounds. / 获取裁切后的源图边界。</summary>
        public RectangleF Bounds { get; }
        /// <summary>Gets covered source pixels after excludes. / 获取排除区域后的覆盖源像素数。</summary>
        public long CoveredPixelCount { get; }
        /// <summary>Gets pixels at or above OpaqueThreshold. / 获取达到不透明阈值的像素数。</summary>
        public long OpaquePixelCount { get; }
        /// <summary>Gets arithmetic mean alpha. / 获取 Alpha 算术平均值。</summary>
        public double MeanAlpha { get; }
        /// <summary>Gets maximum alpha. / 获取最大 Alpha。</summary>
        public float MaximumAlpha { get; }
        /// <summary>Gets nearest-rank 95th percentile alpha. / 获取近邻秩 95 分位 Alpha。</summary>
        public float Percentile95Alpha { get; }
        /// <summary>Gets the threshold used for opaque counting. / 获取不透明像素计数所用阈值。</summary>
        public float OpaqueThreshold { get; }
        /// <summary>Gets opaque-pixel ratio in [0,1]. / 获取 [0,1] 内不透明像素比例。</summary>
        public double OpaqueRatio { get; }
    }

    /// <summary>Contains the original alpha result and deterministic per-ROI statistics. / 包含原始 Alpha 结果及确定性的逐 ROI 统计。</summary>
    public sealed class RoiAlphaResult
    {
        private readonly IReadOnlyList<RoiAlphaRegion> _regions;

        internal RoiAlphaResult(BackgroundRemovalResult source, VisualSize sourceSize, long snapshotVersion, IEnumerable<RoiAlphaRegion> regions)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (snapshotVersion <= 0) throw new ArgumentOutOfRangeException(nameof(snapshotVersion));
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            _regions = new ReadOnlyCollection<RoiAlphaRegion>(regions.ToList());
        }

        /// <summary>Gets original source-space alpha result. / 获取原始源图 Alpha 结果。</summary>
        public BackgroundRemovalResult Source { get; }
        /// <summary>Gets source dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets ROI snapshot version. / 获取 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets regions in snapshot order. / 获取按快照顺序排列的区域。</summary>
        public IReadOnlyList<RoiAlphaRegion> Regions => _regions;
    }

    /// <summary>Computes source-pixel Alpha statistics inside Include ROIs. / 计算 Include ROI 内的源像素 Alpha 统计。</summary>
    public static class VisualRoiAlphaFilter
    {
        /// <summary>Samples Alpha without mutating the source result. / 采样 Alpha 且不修改源结果。</summary>
        public static RoiAlphaResult Filter(BackgroundRemovalResult alpha, VisualRoiSnapshot snapshot, float opaqueThreshold = .5f, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (alpha == null) throw new ArgumentNullException(nameof(alpha));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (float.IsNaN(opaqueThreshold) || float.IsInfinity(opaqueThreshold) || opaqueThreshold < 0 || opaqueThreshold > 1) throw new ArgumentOutOfRangeException(nameof(opaqueThreshold));
            if (alpha.SourceSize != snapshot.SourceSize || alpha.Alpha.Width != snapshot.SourceSize.Width || alpha.Alpha.Height != snapshot.SourceSize.Height)
            {
                throw new VisualException(VisualErrorCodes.InputInvalid, "ROI alpha filtering requires a source-resolution alpha mask.", technicalDetails: "alpha=" + alpha.Alpha.Width + "x" + alpha.Alpha.Height + ";source=" + snapshot.SourceSize.Width + "x" + snapshot.SourceSize.Height);
            }

            var applicable = new List<(VisualRoi Roi, IVisualRoiGeometry Geometry)>();
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || !roi.AppliesTo(VisualTaskId.ForegroundMatting)) continue;
                if (roi.ClassFilter.Count != 0) throw new VisualException(VisualErrorCodes.InputInvalid, "Background-removal ROIs cannot use a class filter.", technicalDetails: "roi=" + roi.Id);
                if (roi.ConfidenceOverride.HasValue) throw new VisualException(VisualErrorCodes.InputInvalid, "Background-removal ROIs cannot use confidenceOverride because Alpha statistics are map-based.", technicalDetails: "roi=" + roi.Id);
                applicable.Add((roi, VisualRoiResolution.Resolve(snapshot, roi, coordinateContext)));
            }

            var excludes = applicable.Where(value => value.Roi.InclusionMode == RoiInclusionMode.Exclude).ToList();
            var regions = new List<RoiAlphaRegion>();
            foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in applicable)
            {
                if (entry.Roi.InclusionMode != RoiInclusionMode.Include) continue;
                regions.Add(Sample(entry.Roi, entry.Geometry, excludes, alpha, opaqueThreshold));
            }
            return new RoiAlphaResult(alpha, snapshot.SourceSize, snapshot.Version, regions);
        }

        private static RoiAlphaRegion Sample(VisualRoi roi, IVisualRoiGeometry geometry, IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> excludes, BackgroundRemovalResult alpha, float opaqueThreshold)
        {
            RectangleF bounds = ClipBounds(geometry.Bounds, alpha.Alpha.Width, alpha.Alpha.Height);
            int left = Math.Max(0, (int)Math.Floor(bounds.X));
            int top = Math.Max(0, (int)Math.Floor(bounds.Y));
            int right = Math.Min(alpha.Alpha.Width, (int)Math.Ceiling(bounds.Right));
            int bottom = Math.Min(alpha.Alpha.Height, (int)Math.Ceiling(bounds.Bottom));
            var values = new List<float>();
            long opaque = 0;
            float maximum = 0;
            double sum = 0;
            for (int y = top; y < bottom; y++) for (int x = left; x < right; x++)
            {
                PointF center = new PointF(x + .5f, y + .5f);
                if (!geometry.Contains(center) || IsExcluded(center, excludes)) continue;
                float value = alpha.Alpha.GetValue(x, y);
                values.Add(value);
                sum += value;
                if (value > maximum) maximum = value;
                if (value >= opaqueThreshold) opaque++;
            }
            float percentile = values.Count == 0 ? 0 : SelectNearestRank(values, (int)Math.Ceiling(values.Count * .95d) - 1);
            return new RoiAlphaRegion(roi.Id, roi.Priority, bounds, values.Count, opaque, values.Count == 0 ? 0 : sum / values.Count, maximum, percentile, opaqueThreshold);
        }

        private static bool IsExcluded(PointF point, IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> excludes)
        {
            foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) exclude in excludes) if (exclude.Roi.AppliesTo(VisualTaskId.ForegroundMatting) && exclude.Geometry.Contains(point)) return true;
            return false;
        }

        private static float SelectNearestRank(List<float> values, int rank)
        {
            values.Sort();
            return values[Math.Max(0, Math.Min(values.Count - 1, rank))];
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

    /// <summary>Composes source-space alpha masks returned by multiple ROI crops. / 合成多个 ROI 裁剪返回的源图 Alpha 掩码。</summary>
    public sealed class RoiAlphaResultComposer
    {
        /// <summary>Merges masks using a deterministic per-pixel policy. / 使用确定性的逐像素策略合并掩码。</summary>
        public BackgroundRemovalResult Compose(IReadOnlyList<RoiProjectedResult<BackgroundRemovalResult>> results, RoiAlphaMergeMode mode = RoiAlphaMergeMode.MaxAlpha)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (results.Count == 0) throw new ArgumentException("At least one ROI alpha result is required.", nameof(results));
            if (!Enum.IsDefined(typeof(RoiAlphaMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            BackgroundRemovalResult first = results[0]?.Result ?? throw new ArgumentException("ROI alpha results cannot contain null values.", nameof(results));
            VisualSize sourceSize = first.SourceSize;
            var values = new float[checked(sourceSize.Width * sourceSize.Height)];
            var priorities = new int[values.Length];
            for (int index = 0; index < priorities.Length; index++) priorities[index] = int.MinValue;
            for (int candidateIndex = 0; candidateIndex < results.Count; candidateIndex++)
            {
                RoiProjectedResult<BackgroundRemovalResult> candidate = results[candidateIndex] ?? throw new ArgumentException("ROI alpha results cannot contain null values.", nameof(results));
                BackgroundRemovalResult result = candidate.Result;
                if (result.SourceSize != sourceSize || result.Alpha.Width != sourceSize.Width || result.Alpha.Height != sourceSize.Height) throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI alpha results must use the same source-image dimensions.");
                if (!string.Equals(first.ProfileId, result.ProfileId, StringComparison.Ordinal) || first.ModelId != result.ModelId) throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI alpha results must use the same profile and model provenance.");
                for (int y = 0; y < sourceSize.Height; y++) for (int x = 0; x < sourceSize.Width; x++)
                {
                    int offset = (y * sourceSize.Width) + x;
                    float value = result.Alpha.GetValue(x, y);
                    bool replace = mode == RoiAlphaMergeMode.KeepLast || (mode == RoiAlphaMergeMode.MaxAlpha && value > values[offset]) || (mode == RoiAlphaMergeMode.RoiPriority && (candidate.Priority > priorities[offset] || (candidate.Priority == priorities[offset] && candidateIndex == 0)));
                    if (mode == RoiAlphaMergeMode.KeepFirst && candidateIndex != 0) replace = false;
                    if (replace) { values[offset] = value; priorities[offset] = candidate.Priority; }
                }
            }
            return new BackgroundRemovalResult(new AlphaMask(sourceSize.Width, sourceSize.Height, values), sourceSize, first.Transform, first.ProfileId, first.ModelId);
        }
    }
}
