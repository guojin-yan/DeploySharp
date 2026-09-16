using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains merged source-space instance masks with ROI provenance. / 包含带 ROI 来源追溯的源图合并实例掩码。</summary>
    public sealed class RoiMergedInstanceSegmentationResult
    {
        private readonly IReadOnlyList<RoiInstanceSegmentationInstance> _items;

        internal RoiMergedInstanceSegmentationResult(InstanceSegmentationResult result, IEnumerable<RoiInstanceSegmentationInstance> items)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            if (items == null) throw new ArgumentNullException(nameof(items));
            _items = new ReadOnlyCollection<RoiInstanceSegmentationInstance>(items.ToList());
        }

        /// <summary>Gets the canonical merged instance-segmentation result. / 获取规范合并实例分割结果。</summary>
        public InstanceSegmentationResult Result { get; }
        /// <summary>Gets merged instances with primary and contributing ROI identifiers. / 获取带主 ROI 和全部贡献 ROI 标识的合并实例。</summary>
        public IReadOnlyList<RoiInstanceSegmentationInstance> Items => _items;
        /// <summary>Gets the source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize => Result.SourceSize;
    }

    /// <summary>Merges crop or sliding-window instance masks by pixel IoU without mutating caller-owned results. / 按像素 IoU 合并裁剪或滑窗实例掩码，且不修改调用方结果。</summary>
    public sealed class RoiInstanceSegmentationResultMerger
    {
        /// <summary>Merges projected instance-segmentation candidates. / 合并已投影的实例分割候选结果。</summary>
        /// <param name="results">Candidates in full source-image coordinates. / 完整源图坐标中的候选结果。</param>
        /// <param name="mode">Keep-all, class-aware/agnostic NMS, ROI priority, or highest-confidence policy. / 保留全部、按类别/忽略类别 NMS、ROI 优先级或最高置信度策略。</param>
        /// <param name="iouThreshold">Foreground-pixel IoU threshold used for suppression. / 用于抑制的前景像素 IoU 阈值。</param>
        /// <param name="overlapMode">Optional ownership-map representation for the retained masks. / 保留掩码的可选所有权图表示。</param>
        public RoiMergedInstanceSegmentationResult Merge(
            IReadOnlyList<RoiProjectedResult<InstanceSegmentationResult>> results,
            RoiResultMergeMode mode = RoiResultMergeMode.KeepAll,
            float iouThreshold = .5f,
            InstanceMaskOverlapMode overlapMode = InstanceMaskOverlapMode.Independent)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (!Enum.IsDefined(typeof(InstanceMaskOverlapMode), overlapMode)) throw new ArgumentOutOfRangeException(nameof(overlapMode));
            if (mode == RoiResultMergeMode.TaskSpecific) throw new NotSupportedException("TaskSpecific merge mode requires an application-provided instance-mask merger.");
            if (mode == RoiResultMergeMode.WeightedBoxFusion) throw new NotSupportedException("WeightedBoxFusion is defined for axis-aligned detections; use mask IoU for instance segmentation.");
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));

            var candidates = new List<Candidate>();
            VisualSize? sourceSize = null;
            InstanceSegmentationResult? baseline = null;
            foreach (RoiProjectedResult<InstanceSegmentationResult> projected in results)
            {
                if (projected == null) throw new ArgumentException("Instance-segmentation candidates cannot contain null values.", nameof(results));
                InstanceSegmentationResult result = projected.Result;
                if (!sourceSize.HasValue) sourceSize = result.SourceSize;
                else if (sourceSize.Value != result.SourceSize) throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI instance-segmentation results must use the same source-image dimensions.");
                if (baseline == null) baseline = result;
                else if (!string.Equals(baseline.ProfileId, result.ProfileId, StringComparison.Ordinal) || baseline.ModelId != result.ModelId) throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI instance-segmentation results must use the same profile and model provenance.");
                foreach (InstanceSegmentationInstance instance in result.Instances)
                {
                    candidates.Add(new Candidate(projected.RoiId, projected.Priority, instance));
                }
            }

            VisualSize effectiveSourceSize = sourceSize ?? throw new ArgumentException("At least one ROI result is required.", nameof(results));
            candidates.Sort((left, right) => Compare(left, right, mode));
            var kept = new List<Candidate>(candidates.Count);
            bool classAgnostic = mode == RoiResultMergeMode.ClassAgnosticNms;
            bool suppress = mode != RoiResultMergeMode.KeepAll;
            foreach (Candidate candidate in candidates)
            {
                int overlapIndex = -1;
                if (suppress)
                {
                    for (int index = 0; index < kept.Count; index++)
                    {
                        Candidate existing = kept[index];
                        if (!classAgnostic && existing.Instance.ClassIndex != candidate.Instance.ClassIndex) continue;
                        if (MaskIntersectionOverUnion(existing.Instance.Mask, candidate.Instance.Mask) > iouThreshold)
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
                    kept[overlapIndex] = winner.WithAdditionalRois(candidate.ContributingRoiIds);
                }
            }

            kept.Sort((left, right) => Compare(left, right, RoiResultMergeMode.KeepAll));
            var canonicalInstances = new List<InstanceSegmentationInstance>(kept.Count);
            var provenance = new List<RoiInstanceSegmentationInstance>(kept.Count);
            for (int index = 0; index < kept.Count; index++)
            {
                Candidate candidate = kept[index];
                InstanceSegmentationInstance instance = candidate.Instance;
                var canonical = new InstanceSegmentationInstance(index, instance.ClassIndex, instance.Label, instance.Score, instance.BoundingBox, instance.Mask, instance.Rle, instance.ExternalId, instance.Metadata);
                canonicalInstances.Add(canonical);
                provenance.Add(new RoiInstanceSegmentationInstance(canonical, candidate.ContributingRoiIds));
            }

            InstanceMaskOwnershipMap? ownership = overlapMode == InstanceMaskOverlapMode.ScorePriorityOwnership
                ? BuildOwnership(effectiveSourceSize, canonicalInstances)
                : null;
            var merged = new InstanceSegmentationResult(canonicalInstances, effectiveSourceSize, baseline!.ProfileId, baseline.ModelId, overlapMode, ownership);
            return new RoiMergedInstanceSegmentationResult(merged, provenance);
        }

        private static InstanceMaskOwnershipMap BuildOwnership(VisualSize sourceSize, IReadOnlyList<InstanceSegmentationInstance> instances)
        {
            int[] owners = Enumerable.Repeat(-1, checked(sourceSize.Width * sourceSize.Height)).ToArray();
            for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
            {
                InstanceBinaryMask mask = instances[instanceIndex].Mask;
                byte[] pixels = mask.GetPixelsUnsafe();
                int offset = mask.PixelOffset;
                for (int pixel = 0; pixel < owners.Length; pixel++)
                {
                    if (owners[pixel] < 0 && pixels[offset + pixel] != 0) owners[pixel] = instanceIndex;
                }
            }

            return new InstanceMaskOwnershipMap(sourceSize.Width, sourceSize.Height, instances.Count, owners);
        }

        private static int Compare(Candidate left, Candidate right, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.RoiPriority)
            {
                int priority = right.Priority.CompareTo(left.Priority);
                if (priority != 0) return priority;
            }

            int score = right.Instance.Score.CompareTo(left.Instance.Score);
            if (score != 0) return score;
            int fallbackPriority = right.Priority.CompareTo(left.Priority);
            if (fallbackPriority != 0) return fallbackPriority;
            int roi = string.Compare(left.RoiId, right.RoiId, StringComparison.Ordinal);
            if (roi != 0) return roi;
            int classIndex = left.Instance.ClassIndex.CompareTo(right.Instance.ClassIndex);
            if (classIndex != 0) return classIndex;
            int source = left.Instance.SourceIndex.CompareTo(right.Instance.SourceIndex);
            if (source != 0) return source;
            return left.Instance.BoundingBox.X.CompareTo(right.Instance.BoundingBox.X);
        }

        private static float MaskIntersectionOverUnion(InstanceBinaryMask first, InstanceBinaryMask second)
        {
            if (first.Width != second.Width || first.Height != second.Height || first.OriginX != second.OriginX || first.OriginY != second.OriginY)
            {
                throw new VisualException(VisualErrorCodes.InputInvalid, "Instance mask IoU requires equal source-space mask dimensions and origins.");
            }

            byte[] firstPixels = first.GetPixelsUnsafe();
            byte[] secondPixels = second.GetPixelsUnsafe();
            int firstOffset = first.PixelOffset;
            int secondOffset = second.PixelOffset;
            int intersection = 0;
            int union = 0;
            for (int index = 0; index < first.PixelCount; index++)
            {
                bool left = firstPixels[firstOffset + index] != 0;
                bool right = secondPixels[secondOffset + index] != 0;
                if (left && right) intersection++;
                if (left || right) union++;
            }

            return union == 0 ? 0 : (float)intersection / union;
        }

        private sealed class Candidate
        {
            internal Candidate(string roiId, int priority, InstanceSegmentationInstance instance, IEnumerable<string>? contributingRoiIds = null)
            {
                RoiId = roiId;
                Priority = priority;
                Instance = instance;
                var ids = new List<string> { roiId };
                if (contributingRoiIds != null) ids.AddRange(contributingRoiIds);
                ContributingRoiIds = ids.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            }

            internal string RoiId { get; }
            internal int Priority { get; }
            internal InstanceSegmentationInstance Instance { get; }
            internal IReadOnlyList<string> ContributingRoiIds { get; }

            internal Candidate WithAdditionalRois(IEnumerable<string> roiIds) => new Candidate(RoiId, Priority, Instance, ContributingRoiIds.Concat(roiIds));
        }
    }
}
