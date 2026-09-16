using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains score and binary-mask statistics measured inside one anomaly ROI. / 包含一个异常 ROI 内测量得到的分数与二值掩码统计。</summary>
    public sealed class RoiAnomalyRegion
    {
        internal RoiAnomalyRegion(string roiId, int priority, RectangleF bounds, long coveredPixelCount, long anomalousPixelCount, double meanScore, float maximumScore, float percentile95Score)
        {
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            if (coveredPixelCount < 0 || anomalousPixelCount < 0 || anomalousPixelCount > coveredPixelCount) throw new ArgumentOutOfRangeException(nameof(coveredPixelCount));
            if (double.IsNaN(meanScore) || double.IsInfinity(meanScore)) throw new ArgumentOutOfRangeException(nameof(meanScore));
            if (float.IsNaN(maximumScore) || float.IsInfinity(maximumScore)) throw new ArgumentOutOfRangeException(nameof(maximumScore));
            if (float.IsNaN(percentile95Score) || float.IsInfinity(percentile95Score)) throw new ArgumentOutOfRangeException(nameof(percentile95Score));
            RoiId = roiId;
            Priority = priority;
            Bounds = bounds;
            CoveredPixelCount = coveredPixelCount;
            AnomalousPixelCount = anomalousPixelCount;
            MeanScore = meanScore;
            MaximumScore = maximumScore;
            Percentile95Score = percentile95Score;
            AnomalousPixelRatio = coveredPixelCount == 0 ? 0 : (double)anomalousPixelCount / coveredPixelCount;
        }

        /// <summary>Gets the ROI identifier. / 获取 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets the ROI priority. / 获取 ROI 优先级。</summary>
        public int Priority { get; }
        /// <summary>Gets the clipped source-space bounds. / 获取裁切后的源图边界。</summary>
        public RectangleF Bounds { get; }
        /// <summary>Gets the number of pixels covered after Exclude ROIs. / 获取排除 Exclude ROI 后覆盖的像素数。</summary>
        public long CoveredPixelCount { get; }
        /// <summary>Gets the number of thresholded anomalous pixels. / 获取阈值化异常像素数。</summary>
        public long AnomalousPixelCount { get; }
        /// <summary>Gets the arithmetic mean of the decoder-provided map values. / 获取 decoder 提供的分数图算术平均值。</summary>
        public double MeanScore { get; }
        /// <summary>Gets the maximum decoder-provided map value. / 获取 decoder 提供的分数图最大值。</summary>
        public float MaximumScore { get; }
        /// <summary>Gets the nearest-rank 95th percentile of decoder-provided map values. / 获取 decoder 提供的分数图近邻秩 95 分位数。</summary>
        public float Percentile95Score { get; }
        /// <summary>Gets the anomalous-pixel fraction in [0,1]. / 获取 [0,1] 内异常像素比例。</summary>
        public double AnomalousPixelRatio { get; }
    }

    /// <summary>Contains the original anomaly result and deterministic per-ROI statistics. / 包含原始异常结果及确定性的逐 ROI 统计。</summary>
    public sealed class RoiAnomalyResult
    {
        private readonly IReadOnlyList<RoiAnomalyRegion> _regions;

        internal RoiAnomalyResult(AnomalyDetectionResult source, VisualSize sourceSize, long snapshotVersion, IEnumerable<RoiAnomalyRegion> regions)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (snapshotVersion <= 0) throw new ArgumentOutOfRangeException(nameof(snapshotVersion));
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            _regions = new ReadOnlyCollection<RoiAnomalyRegion>(regions.ToList());
        }

        /// <summary>Gets the original source-space anomaly result. / 获取原始源图异常结果。</summary>
        public AnomalyDetectionResult Source { get; }
        /// <summary>Gets source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the ROI snapshot version used for sampling. / 获取采样所用 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets regions in deterministic snapshot order. / 获取按快照顺序排列的区域。</summary>
        public IReadOnlyList<RoiAnomalyRegion> Regions => _regions;
    }

    /// <summary>Computes source-pixel anomaly statistics inside Include ROIs. / 计算 Include ROI 内的源像素异常统计。</summary>
    public static class VisualRoiAnomalyFilter
    {
        /// <summary>Samples the restored decoder-provided score map and mask without mutating either input. / 采样恢复后的 decoder 分数图和掩码且不修改输入。</summary>
        /// <remarks>Both maps must be restored to source dimensions. Exclude ROIs are removed from every Include region. This API reports per-ROI statistics; it deliberately does not invent a fused anomaly map. / 两个图都必须恢复到源图尺寸。Exclude ROI 会从所有 Include 区域移除；此 API 报告逐 ROI 统计，不虚构融合异常图。</remarks>
        public static RoiAnomalyResult Filter(AnomalyDetectionResult anomaly, VisualRoiSnapshot snapshot, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (anomaly == null) throw new ArgumentNullException(nameof(anomaly));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            VisualSize sourceSize = snapshot.SourceSize;
            if (anomaly.NormalizedMap.Width != sourceSize.Width || anomaly.NormalizedMap.Height != sourceSize.Height || anomaly.Mask.Width != sourceSize.Width || anomaly.Mask.Height != sourceSize.Height)
            {
                throw new VisualException(VisualErrorCodes.InputInvalid, "Anomaly ROI filtering requires source-resolution normalized map and mask.", technicalDetails: "map=" + anomaly.NormalizedMap.Width + "x" + anomaly.NormalizedMap.Height + ";mask=" + anomaly.Mask.Width + "x" + anomaly.Mask.Height + ";source=" + sourceSize.Width + "x" + sourceSize.Height);
            }

            var applicable = new List<(VisualRoi Roi, IVisualRoiGeometry Geometry)>();
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || !roi.AppliesTo(VisualTaskId.AnomalyDetection)) continue;
                if (roi.ClassFilter.Count != 0) throw new VisualException(VisualErrorCodes.InputInvalid, "Anomaly ROIs cannot use a class filter because anomaly results have no class dimension.", technicalDetails: "roi=" + roi.Id);
                if (roi.ConfidenceOverride.HasValue) throw new VisualException(VisualErrorCodes.InputInvalid, "Anomaly ROIs cannot use confidenceOverride because anomaly statistics are map-based.", technicalDetails: "roi=" + roi.Id);
                applicable.Add((roi, VisualRoiResolution.Resolve(snapshot, roi, coordinateContext)));
            }

            var excludes = applicable.Where(value => value.Roi.InclusionMode == RoiInclusionMode.Exclude).ToList();
            var regions = new List<RoiAnomalyRegion>();
            foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in applicable)
            {
                if (entry.Roi.InclusionMode != RoiInclusionMode.Include) continue;
                regions.Add(Sample(entry.Roi, entry.Geometry, excludes, anomaly));
            }
            return new RoiAnomalyResult(anomaly, sourceSize, snapshot.Version, regions);
        }

        private static RoiAnomalyRegion Sample(VisualRoi roi, IVisualRoiGeometry geometry, IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> excludes, AnomalyDetectionResult anomaly)
        {
            RectangleF bounds = ClipBounds(geometry.Bounds, anomaly.NormalizedMap.Width, anomaly.NormalizedMap.Height);
            int left = Math.Max(0, (int)Math.Floor(bounds.X));
            int top = Math.Max(0, (int)Math.Floor(bounds.Y));
            int right = Math.Min(anomaly.NormalizedMap.Width, (int)Math.Ceiling(bounds.Right));
            int bottom = Math.Min(anomaly.NormalizedMap.Height, (int)Math.Ceiling(bounds.Bottom));
            var values = new List<float>();
            long anomalous = 0;
            float maximum = float.NegativeInfinity;
            double sum = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    PointF center = new PointF(x + .5f, y + .5f);
                    if (!geometry.Contains(center) || IsExcluded(center, excludes)) continue;
                    float value = anomaly.NormalizedMap.GetValue(x, y);
                    values.Add(value);
                    sum += value;
                    if (value > maximum) maximum = value;
                    if (anomaly.Mask.IsAnomalous(x, y)) anomalous++;
                }
            }

            if (values.Count == 0) maximum = 0;
            float percentile = values.Count == 0 ? 0 : SelectNearestRank(values, (int)Math.Ceiling(values.Count * .95d) - 1);
            return new RoiAnomalyRegion(roi.Id, roi.Priority, bounds, values.Count, anomalous, values.Count == 0 ? 0 : sum / values.Count, maximum, percentile);
        }

        private static bool IsExcluded(PointF point, IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> excludes)
        {
            foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) exclude in excludes)
            {
                if (exclude.Roi.AppliesTo(VisualTaskId.AnomalyDetection) && exclude.Geometry.Contains(point)) return true;
            }
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

    /// <summary>Defines deterministic fusion policies for source-resolution anomaly maps returned by ROI crops. / 定义 ROI 裁剪返回的源分辨率异常图确定性融合策略。</summary>
    public enum RoiAnomalyMergeMode
    {
        /// <summary>Keep the first candidate at every pixel. / 逐像素保留第一个候选。</summary>
        KeepFirst = 0,
        /// <summary>Keep the last candidate at every pixel. / 逐像素保留最后一个候选。</summary>
        KeepLast = 1,
        /// <summary>Keep the highest score at every pixel. / 逐像素保留最高分数。</summary>
        MaxScore = 2,
        /// <summary>Average all candidate scores at every pixel. / 逐像素平均所有候选分数。</summary>
        MeanScore = 3,
        /// <summary>Choose the candidate from the highest-priority ROI. / 逐像素选择最高优先级 ROI 候选。</summary>
        RoiPriority = 4
    }

    /// <summary>Fuses source-resolution anomaly maps from multiple ROI inferences without changing caller-owned results. / 合并多个 ROI 推理得到的源分辨率异常图且不修改调用方结果。</summary>
    public sealed class RoiAnomalyResultComposer
    {
        /// <summary>Composes normalized maps and rebuilds the thresholded mask using the shared threshold. / 合成归一化分数图，并使用统一阈值重建二值掩码。</summary>
        /// <remarks>Every candidate must already be projected to the same source dimensions. The composer does not infer crop coverage from zero-valued pixels; callers should use a merge mode such as MaxScore or RoiPriority when ROI maps contain untouched background. / 所有候选必须已经投影到相同源图尺寸；合并器不会从零值像素猜测裁剪覆盖范围，ROI 图包含未覆盖背景时应优先使用 MaxScore 或 RoiPriority。</remarks>
        public AnomalyDetectionResult Compose(IReadOnlyList<RoiProjectedResult<AnomalyDetectionResult>> results, RoiAnomalyMergeMode mode = RoiAnomalyMergeMode.MaxScore)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (results.Count == 0) throw new ArgumentException("At least one ROI anomaly result is required.", nameof(results));
            if (!Enum.IsDefined(typeof(RoiAnomalyMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));

            AnomalyDetectionResult first = results[0]?.Result ?? throw new ArgumentException("ROI anomaly results cannot contain null values.", nameof(results));
            VisualSize sourceSize = first.NormalizedMap.SourceSize;
            if (first.NormalizedMap.Width != sourceSize.Width || first.NormalizedMap.Height != sourceSize.Height || first.Mask.Width != sourceSize.Width || first.Mask.Height != sourceSize.Height)
            {
                throw new VisualException(VisualErrorCodes.InputInvalid, "ROI anomaly fusion requires source-resolution maps and masks.");
            }
            AnomalyMapValueMode valueMode = first.NormalizedMap.ValueMode;
            AnomalyNormalizationMode normalization = first.NormalizedMap.Normalization;
            float threshold = first.Threshold;
            var values = new float[checked(sourceSize.Width * sourceSize.Height)];
            var priorities = new int[values.Length];
            var counts = new int[values.Length];
            for (int index = 0; index < priorities.Length; index++) priorities[index] = int.MinValue;

            for (int candidateIndex = 0; candidateIndex < results.Count; candidateIndex++)
            {
                RoiProjectedResult<AnomalyDetectionResult> candidate = results[candidateIndex] ?? throw new ArgumentException("ROI anomaly results cannot contain null values.", nameof(results));
                AnomalyDetectionResult result = candidate.Result;
                if (result.NormalizedMap.SourceSize != sourceSize || result.NormalizedMap.Width != sourceSize.Width || result.NormalizedMap.Height != sourceSize.Height || result.Mask.Width != sourceSize.Width || result.Mask.Height != sourceSize.Height)
                {
                    throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI anomaly results must use the same source-resolution dimensions.");
                }
                if (result.NormalizedMap.ValueMode != valueMode || result.NormalizedMap.Normalization != normalization || Math.Abs(result.Threshold - threshold) > 0.000001f)
                {
                    throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI anomaly results must use the same map semantics and threshold.");
                }

                float[] candidateValues = result.NormalizedMap.DangerousGetReadOnlyBuffer();
                for (int offset = 0; offset < values.Length; offset++)
                {
                    float value = candidateValues[offset];
                    bool replace = mode == RoiAnomalyMergeMode.KeepLast || (mode == RoiAnomalyMergeMode.MaxScore && (candidateIndex == 0 || value > values[offset])) || (mode == RoiAnomalyMergeMode.RoiPriority && (candidate.Priority > priorities[offset] || (candidate.Priority == priorities[offset] && candidateIndex < counts[offset])));
                    if (mode == RoiAnomalyMergeMode.KeepFirst && candidateIndex != 0) replace = false;
                    if (mode == RoiAnomalyMergeMode.MeanScore)
                    {
                        values[offset] += value;
                        counts[offset]++;
                    }
                    else if (replace)
                    {
                        values[offset] = value;
                        priorities[offset] = candidate.Priority;
                        counts[offset] = candidateIndex;
                    }
                }
            }

            if (mode == RoiAnomalyMergeMode.MeanScore)
            {
                for (int offset = 0; offset < values.Length; offset++) values[offset] /= results.Count;
            }
            var maskValues = new byte[values.Length];
            int anomalousCount = 0;
            for (int offset = 0; offset < values.Length; offset++)
            {
                if (values[offset] >= threshold) { maskValues[offset] = 1; anomalousCount++; }
            }
            var normalized = new AnomalyScoreMap(sourceSize, sourceSize.Width, sourceSize.Height, values, valueMode, normalization, true);
            var mask = new AnomalyBinaryMask(sourceSize.Width, sourceSize.Height, maskValues, true);
            return new AnomalyDetectionResult(values.Length == 0 ? 0 : values.Max(), null, normalized, mask, threshold, first.Transform, anomalousCount, first.Timing, first.Warnings);
        }
    }
}
