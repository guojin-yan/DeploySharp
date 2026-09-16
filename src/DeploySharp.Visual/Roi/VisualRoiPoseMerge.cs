using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Associates one source-space Pose instance with ROI/window provenance. / 将源图姿态实例与 ROI/窗口来源关联。</summary>
    public sealed class RoiProjectedPoseInstance
    {
        private readonly IReadOnlyList<string> _contributingRoiIds;

        /// <summary>Initializes a projected Pose candidate. / 初始化投影后的姿态候选。</summary>
        public RoiProjectedPoseInstance(string roiId, int priority, PoseInstance instance, int? windowIndex = null, IEnumerable<string>? contributingRoiIds = null)
        {
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
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
        /// <summary>Gets the ROI priority. / 获取 ROI 优先级。</summary>
        public int Priority { get; }
        /// <summary>Gets the source-space Pose instance. / 获取源图空间姿态实例。</summary>
        public PoseInstance Instance { get; }
        /// <summary>Gets the optional source window index. / 获取可选来源窗口索引。</summary>
        public int? WindowIndex { get; }
        /// <summary>Gets all ROI identifiers contributing the retained instance. / 获取贡献保留实例的全部 ROI 标识。</summary>
        public IReadOnlyList<string> ContributingRoiIds => _contributingRoiIds;
    }

    /// <summary>Merges projected Pose instances using explicit pairwise OKS. / 使用显式成对 OKS 合并投影后的姿态实例。</summary>
    public sealed class RoiPoseResultMerger
    {
        /// <summary>Applies deterministic OKS suppression to cross-ROI or cross-window candidates. / 对跨 ROI 或跨窗口候选执行确定性 OKS 抑制。</summary>
        public IReadOnlyList<RoiProjectedPoseInstance> Merge(IReadOnlyList<RoiProjectedPoseInstance> candidates, PoseTopology topology, PoseOksOptions? options = null, RoiResultMergeMode mode = RoiResultMergeMode.ClassAwareNms, int maximumInstances = 300)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (maximumInstances <= 0) throw new ArgumentOutOfRangeException(nameof(maximumInstances));
            if (mode == RoiResultMergeMode.TaskSpecific) throw new NotSupportedException("TaskSpecific Pose merging requires an application-provided merger.");
            if (mode == RoiResultMergeMode.WeightedBoxFusion) throw new NotSupportedException("WeightedBoxFusion is defined for axis-aligned detections; use OKS for Pose results.");
            PoseOksOptions effective = options ?? new PoseOksOptions();
            for (int index = 0; index < topology.Keypoints.Count; index++) if (!topology.Keypoints[index].OksSigma.HasValue) throw new ArgumentException("Pose ROI OKS merging requires a sigma for every keypoint.", nameof(topology));
            var ordered = candidates.Select(value => value ?? throw new ArgumentException("Pose candidates cannot contain null.", nameof(candidates))).ToList();
            ordered.Sort((left, right) => Compare(left, right, mode));
            if (mode == RoiResultMergeMode.KeepAll) return new ReadOnlyCollection<RoiProjectedPoseInstance>(ordered.Take(maximumInstances).ToList());

            bool classAgnostic = mode == RoiResultMergeMode.ClassAgnosticNms;
            var kept = new List<RoiProjectedPoseInstance>(Math.Min(ordered.Count, maximumInstances));
            for (int index = 0; index < ordered.Count && kept.Count < maximumInstances; index++)
            {
                RoiProjectedPoseInstance candidate = ordered[index];
                int overlapIndex = -1;
                for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                {
                    RoiProjectedPoseInstance existing = kept[keptIndex];
                    if (!classAgnostic && existing.Instance.ClassIndex != candidate.Instance.ClassIndex) continue;
                    RectangleF referenceBounds = RequireBounds(existing.Instance);
                    float similarity = PoseOks.CalculateSimilarity(existing.Instance, candidate.Instance, topology.Keypoints, referenceBounds.Width * referenceBounds.Height, effective.MinimumKeypointScore, effective.AreaEpsilon);
                    if (similarity > effective.SuppressionThreshold) { overlapIndex = keptIndex; break; }
                }
                if (overlapIndex < 0) kept.Add(candidate);
                else
                {
                    RoiProjectedPoseInstance winner = kept[overlapIndex];
                    kept[overlapIndex] = new RoiProjectedPoseInstance(winner.RoiId, winner.Priority, winner.Instance, winner.WindowIndex, winner.ContributingRoiIds.Concat(candidate.ContributingRoiIds));
                }
            }
            return new ReadOnlyCollection<RoiProjectedPoseInstance>(kept);
        }

        private static RectangleF RequireBounds(PoseInstance instance)
        {
            if (instance.BoundingBox.HasValue) return instance.BoundingBox.Value;
            var valid = instance.Keypoints.Where(value => value.IsValid).ToList();
            if (valid.Count < 2) throw new ArgumentException("Pose ROI OKS merging requires a bounding box or at least two valid keypoints.", nameof(instance));
            float left = valid.Min(value => value.Point.X);
            float top = valid.Min(value => value.Point.Y);
            float right = valid.Max(value => value.Point.X);
            float bottom = valid.Max(value => value.Point.Y);
            if (right <= left || bottom <= top) throw new ArgumentException("Pose ROI OKS merging requires a positive reference area.", nameof(instance));
            return new RectangleF(left, top, right - left, bottom - top);
        }

        private static int Compare(RoiProjectedPoseInstance left, RoiProjectedPoseInstance right, RoiResultMergeMode mode)
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
            int source = left.Instance.SourceIndex.CompareTo(right.Instance.SourceIndex);
            return source != 0 ? source : Nullable.Compare(left.WindowIndex, right.WindowIndex);
        }
    }
}
