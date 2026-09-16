using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Associates one source-space OBB with its ROI/window provenance. / 将一个源图 OBB 与其 ROI/窗口来源关联。</summary>
    public sealed class RoiProjectedOrientedDetection
    {
        private readonly IReadOnlyList<string> _contributingRoiIds;

        /// <summary>Initializes a projected OBB candidate. / 初始化投影后的 OBB 候选。</summary>
        public RoiProjectedOrientedDetection(string roiId, int priority, OrientedDetection detection, int? windowIndex = null, IEnumerable<string>? contributingRoiIds = null)
        {
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            Detection = detection ?? throw new ArgumentNullException(nameof(detection));
            if (windowIndex.HasValue && windowIndex.Value < 0) throw new ArgumentOutOfRangeException(nameof(windowIndex));
            RoiId = roiId;
            Priority = priority;
            WindowIndex = windowIndex;
            var ids = contributingRoiIds == null ? new List<string>() : contributingRoiIds.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
            ids.Add(roiId);
            _contributingRoiIds = new ReadOnlyCollection<string>(ids.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
        }

        /// <summary>Gets the primary ROI identifier. / 获取主 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets the ROI priority used for deterministic conflict resolution. / 获取用于确定性冲突处理的 ROI 优先级。</summary>
        public int Priority { get; }
        /// <summary>Gets the source-space OBB. / 获取源图空间 OBB。</summary>
        public OrientedDetection Detection { get; }
        /// <summary>Gets the optional source window index. / 获取可选来源窗口索引。</summary>
        public int? WindowIndex { get; }
        /// <summary>Gets every ROI that contributed an overlapping OBB. / 获取贡献重叠 OBB 的全部 ROI。</summary>
        public IReadOnlyList<string> ContributingRoiIds => _contributingRoiIds;
    }

    /// <summary>Merges projected OBB candidates using exact quadrilateral IoU. / 使用精确四边形 IoU 合并投影后的 OBB 候选。</summary>
    public sealed class RoiOrientedDetectionResultMerger
    {
        /// <summary>Applies deterministic rotated NMS while preserving all contributing ROI identifiers. / 执行确定性旋转 NMS，并保留所有贡献 ROI 标识。</summary>
        /// <remarks>KeepAll performs no suppression. ClassAwareNms, RoiPriority, and HighestConfidence suppress only within a class; ClassAgnosticNms suppresses across classes. TaskSpecific requires an application merger. / KeepAll 不抑制；ClassAwareNms、RoiPriority 和 HighestConfidence 仅在类内抑制；ClassAgnosticNms 跨类别抑制；TaskSpecific 需应用自定义合并器。</remarks>
        public IReadOnlyList<RoiProjectedOrientedDetection> Merge(IReadOnlyList<RoiProjectedOrientedDetection> candidates, RoiResultMergeMode mode = RoiResultMergeMode.ClassAwareNms, float iouThreshold = .45f, int maximumDetections = 300)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (maximumDetections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDetections));
            if (mode == RoiResultMergeMode.TaskSpecific) throw new NotSupportedException("TaskSpecific OBB merging requires an application-provided merger.");
            if (mode == RoiResultMergeMode.WeightedBoxFusion) throw new NotSupportedException("WeightedBoxFusion is defined for axis-aligned detections; use rotated IoU NMS for OBB results.");
            var ordered = candidates.Select(value => value ?? throw new ArgumentException("OBB candidates cannot contain null.", nameof(candidates))).ToList();
            ordered.Sort((left, right) => Compare(left, right, mode));
            if (mode == RoiResultMergeMode.KeepAll) return new ReadOnlyCollection<RoiProjectedOrientedDetection>(ordered.Take(maximumDetections).ToList());

            bool classAgnostic = mode == RoiResultMergeMode.ClassAgnosticNms;
            var kept = new List<RoiProjectedOrientedDetection>(Math.Min(ordered.Count, maximumDetections));
            for (int index = 0; index < ordered.Count && kept.Count < maximumDetections; index++)
            {
                RoiProjectedOrientedDetection candidate = ordered[index];
                int overlapIndex = -1;
                for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                {
                    RoiProjectedOrientedDetection existing = kept[keptIndex];
                    if (!classAgnostic && existing.Detection.ClassIndex != candidate.Detection.ClassIndex) continue;
                    if (OrientedQuadrilateral.IntersectionOverUnion(existing.Detection.Quadrilateral, candidate.Detection.Quadrilateral) > iouThreshold) { overlapIndex = keptIndex; break; }
                }
                if (overlapIndex < 0) kept.Add(candidate);
                else
                {
                    RoiProjectedOrientedDetection winner = kept[overlapIndex];
                    kept[overlapIndex] = new RoiProjectedOrientedDetection(winner.RoiId, winner.Priority, winner.Detection, winner.WindowIndex, winner.ContributingRoiIds.Concat(candidate.ContributingRoiIds));
                }
            }
            return new ReadOnlyCollection<RoiProjectedOrientedDetection>(kept);
        }

        private static int Compare(RoiProjectedOrientedDetection left, RoiProjectedOrientedDetection right, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.RoiPriority)
            {
                int priority = right.Priority.CompareTo(left.Priority);
                if (priority != 0) return priority;
            }
            int score = right.Detection.Score.CompareTo(left.Detection.Score);
            if (score != 0) return score;
            int fallbackPriority = right.Priority.CompareTo(left.Priority);
            if (fallbackPriority != 0) return fallbackPriority;
            int roi = string.Compare(left.RoiId, right.RoiId, StringComparison.Ordinal);
            if (roi != 0) return roi;
            int source = left.Detection.SourceIndex.CompareTo(right.Detection.SourceIndex);
            return source != 0 ? source : Nullable.Compare(left.WindowIndex, right.WindowIndex);
        }
    }
}
