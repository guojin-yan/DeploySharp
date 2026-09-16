using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains merged OCR regions with deterministic ROI provenance. / 包含带确定性 ROI 来源的合并 OCR 区域。</summary>
    public sealed class RoiMergedOcrResult
    {
        private readonly IReadOnlyList<RoiOcrRegion> _items;

        internal RoiMergedOcrResult(OcrResult result, IEnumerable<RoiOcrRegion> items)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            if (items == null) throw new ArgumentNullException(nameof(items));
            _items = new ReadOnlyCollection<RoiOcrRegion>(items.ToList());
        }

        /// <summary>Gets the canonical merged OCR result. / 获取规范合并 OCR 结果。</summary>
        public OcrResult Result { get; }
        /// <summary>Gets regions in deterministic source-image reading order. / 获取按源图确定性阅读顺序排列的区域。</summary>
        public IReadOnlyList<RoiOcrRegion> Items => _items;
        /// <summary>Gets the source-image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize => Result.SourceSize;
    }

    /// <summary>Merges OCR regions produced by overlapping ROI crops or sliding windows. / 合并重叠 ROI 裁剪或滑窗产生的 OCR 区域。</summary>
    public sealed class RoiOcrResultMerger
    {
        /// <summary>Merges OCR results using text identity and polygon IoU. / 使用文本身份和多边形 IoU 合并 OCR 结果。</summary>
        /// <remarks>Only non-empty normalized text with sufficient polygon overlap is deduplicated. Different text is retained even when geometry overlaps, preventing a geometry-only merge from silently losing recognition alternatives. / 仅对非空规范化文本且多边形重叠达到阈值的区域去重；不同文本即使几何重叠也会保留，避免仅凭几何静默丢失识别候选。</remarks>
        public RoiMergedOcrResult Merge(
            IReadOnlyList<RoiProjectedResult<OcrResult>> results,
            RoiResultMergeMode mode = RoiResultMergeMode.KeepAll,
            float polygonIouThreshold = .5f,
            bool ignoreCase = true,
            bool collapseWhitespace = true)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == RoiResultMergeMode.TaskSpecific) throw new NotSupportedException("TaskSpecific merge mode requires an application-provided OCR merger.");
            if (mode == RoiResultMergeMode.WeightedBoxFusion) throw new NotSupportedException("WeightedBoxFusion is defined for axis-aligned detections; use text polygon IoU for OCR results.");
            if (float.IsNaN(polygonIouThreshold) || float.IsInfinity(polygonIouThreshold) || polygonIouThreshold < 0 || polygonIouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(polygonIouThreshold));

            var candidates = new List<Candidate>();
            VisualSize? sourceSize = null;
            OcrResult? baseline = null;
            foreach (RoiProjectedResult<OcrResult> projected in results)
            {
                if (projected == null) throw new ArgumentException("OCR candidates cannot contain null values.", nameof(results));
                if (!sourceSize.HasValue) sourceSize = projected.Result.SourceSize;
                else if (sourceSize.Value != projected.Result.SourceSize) throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI OCR results must use the same source-image dimensions.");
                if (baseline == null) baseline = projected.Result;
                else
                {
                    if (!string.Equals(baseline.DetectionProfileId, projected.Result.DetectionProfileId, StringComparison.Ordinal) || baseline.DetectionModelId != projected.Result.DetectionModelId || !string.Equals(baseline.RecognitionProfileId, projected.Result.RecognitionProfileId, StringComparison.Ordinal) || baseline.RecognitionModelId != projected.Result.RecognitionModelId)
                    {
                        throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI OCR results must use the same detector and recognizer provenance.");
                    }
                }
                foreach (OcrRegionResult region in projected.Result.Regions)
                {
                    candidates.Add(new Candidate(projected.RoiId, projected.Priority, region, Normalize(region.Recognition.Text, ignoreCase, collapseWhitespace)));
                }
            }

            VisualSize effectiveSourceSize = sourceSize ?? throw new ArgumentException("At least one ROI OCR result is required.", nameof(results));
            candidates.Sort((left, right) => Compare(left, right, mode));
            var kept = new List<Candidate>(candidates.Count);
            foreach (Candidate candidate in candidates)
            {
                int overlapIndex = -1;
                if (mode != RoiResultMergeMode.KeepAll && candidate.NormalizedText.Length != 0)
                {
                    for (int index = 0; index < kept.Count; index++)
                    {
                        Candidate existing = kept[index];
                        if (!string.Equals(existing.NormalizedText, candidate.NormalizedText, StringComparison.Ordinal)) continue;
                        if (PolygonIntersectionOverUnion(existing.Region.Region.Polygon, candidate.Region.Region.Polygon) >= polygonIouThreshold)
                        {
                            overlapIndex = index;
                            break;
                        }
                    }
                }

                if (overlapIndex < 0) kept.Add(candidate);
                else
                {
                    Candidate winner = kept[overlapIndex];
                    kept[overlapIndex] = winner.WithAdditionalRoi(candidate.RoiId);
                }
            }

            kept.Sort(CompareReadingOrder);
            var canonicalRegions = new List<OcrRegionResult>(kept.Count);
            var wrappedRegions = new List<RoiOcrRegion>(kept.Count);
            for (int index = 0; index < kept.Count; index++)
            {
                Candidate candidate = kept[index];
                OcrRegionResult region = candidate.Region;
                TextRegion sourceRegion = region.Region;
                var canonicalTextRegion = new TextRegion(index, sourceRegion.Score, sourceRegion.Polygon, sourceRegion.CropQuadrilateral, sourceRegion.Orientation, sourceRegion.AngleRadians, sourceRegion.Language, sourceRegion.Script, sourceRegion.ExternalId, sourceRegion.Metadata);
                OcrRegionResult canonicalRegion = region.WithRegion(canonicalTextRegion);
                canonicalRegions.Add(canonicalRegion);
                wrappedRegions.Add(new RoiOcrRegion(canonicalRegion, candidate.ContributingRoiIds));
            }

            OcrResult first = baseline!;
            var merged = new OcrResult(canonicalRegions, effectiveSourceSize, first.DetectionProfileId, first.DetectionModelId, first.RecognitionProfileId, first.RecognitionModelId, first.Timing, first.Orientation);
            return new RoiMergedOcrResult(merged, wrappedRegions);
        }

        private static int Compare(Candidate left, Candidate right, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.RoiPriority)
            {
                int priority = right.Priority.CompareTo(left.Priority);
                if (priority != 0) return priority;
            }

            float leftConfidence = left.Region.Region.Score * left.Region.Recognition.Confidence;
            float rightConfidence = right.Region.Region.Score * right.Region.Recognition.Confidence;
            int score = rightConfidence.CompareTo(leftConfidence);
            if (score != 0) return score;
            int fallbackPriority = right.Priority.CompareTo(left.Priority);
            if (fallbackPriority != 0) return fallbackPriority;
            int roi = string.Compare(left.RoiId, right.RoiId, StringComparison.Ordinal);
            if (roi != 0) return roi;
            return left.Region.Region.SourceIndex.CompareTo(right.Region.Region.SourceIndex);
        }

        private static int CompareReadingOrder(Candidate left, Candidate right)
        {
            RectangleF leftBounds = left.Region.Region.AxisAlignedBounds;
            RectangleF rightBounds = right.Region.Region.AxisAlignedBounds;
            int y = leftBounds.Y.CompareTo(rightBounds.Y);
            if (y != 0) return y;
            int x = leftBounds.X.CompareTo(rightBounds.X);
            if (x != 0) return x;
            int score = right.Region.Region.Score.CompareTo(left.Region.Region.Score);
            if (score != 0) return score;
            int roi = string.Compare(left.RoiId, right.RoiId, StringComparison.Ordinal);
            if (roi != 0) return roi;
            return left.Region.Region.SourceIndex.CompareTo(right.Region.Region.SourceIndex);
        }

        private static float PolygonIntersectionOverUnion(TextPolygon first, TextPolygon second)
        {
            float intersection = VisualRoiTextDetectionFilter.CalculatePolygonIntersectionArea(first.Vertices, new PolygonRoiGeometry(second.Vertices));
            float union = first.Area + second.Area - intersection;
            return union <= 0 ? 0 : intersection / union;
        }

        private static string Normalize(string text, bool ignoreCase, bool collapseWhitespace)
        {
            string normalized = (text ?? string.Empty).Normalize(System.Text.NormalizationForm.FormKC).Trim();
            if (collapseWhitespace)
            {
                var builder = new System.Text.StringBuilder(normalized.Length);
                bool pendingSpace = false;
                foreach (char character in normalized)
                {
                    if (char.IsWhiteSpace(character)) pendingSpace = builder.Length > 0;
                    else
                    {
                        if (pendingSpace) builder.Append(' ');
                        builder.Append(character);
                        pendingSpace = false;
                    }
                }
                normalized = builder.ToString();
            }

            return ignoreCase ? normalized.ToUpperInvariant() : normalized;
        }

        private sealed class Candidate
        {
            internal Candidate(string roiId, int priority, OcrRegionResult region, string normalizedText, IEnumerable<string>? contributingRoiIds = null)
            {
                RoiId = roiId;
                Priority = priority;
                Region = region;
                NormalizedText = normalizedText;
                var ids = new List<string> { roiId };
                if (contributingRoiIds != null) ids.AddRange(contributingRoiIds);
                ContributingRoiIds = ids.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            }

            internal string RoiId { get; }
            internal int Priority { get; }
            internal OcrRegionResult Region { get; }
            internal string NormalizedText { get; }
            internal IReadOnlyList<string> ContributingRoiIds { get; }

            internal Candidate WithAdditionalRoi(string roiId) => new Candidate(RoiId, Priority, Region, NormalizedText, ContributingRoiIds.Concat(new[] { roiId }));
        }
    }
}
