using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains merged source-space detections with ROI provenance. / 包含带 ROI 来源追溯的源图合并检测。</summary>
    public sealed class RoiMergedDetectionResult
    {
        private readonly IReadOnlyList<RoiProjectedDetection> _items;

        internal RoiMergedDetectionResult(VisualSize sourceSize, long snapshotVersion, IEnumerable<RoiProjectedDetection> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            _items = new ReadOnlyCollection<RoiProjectedDetection>(items.ToList());
            Detections = new DetectionResult(_items.Select(value => value.Detection));
        }

        /// <summary>Gets the full source-image dimensions. / 获取完整源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the exact ROI snapshot version. / 获取确切 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets canonical detections without provenance wrappers. / 获取不带来源包装的规范检测。</summary>
        public DetectionResult Detections { get; }
        /// <summary>Gets detections with primary and contributing ROI identifiers. / 获取带主 ROI 和贡献 ROI 标识的检测。</summary>
        public IReadOnlyList<RoiProjectedDetection> Items => _items;
    }

    /// <summary>Merges Detection results produced by a crop-and-infer ROI batch. / 合并 CropAndInfer ROI 批次产生的 Detection 结果。</summary>
    public sealed class RoiDetectionResultMerger
    {
        /// <summary>Merges successful batch items in source coordinates. Failed ROI items remain available on the input batch. / 在源图坐标合并成功项；失败 ROI 仍保留在输入批次中。</summary>
        public RoiMergedDetectionResult Merge(RoiInferenceBatchResult batch, float iouThreshold = .45f, int maximumDetections = 300, RoiResultMergeMode? mode = null)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (maximumDetections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDetections));
            RoiResultMergeMode effective = mode ?? batch.MergeMode;
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), effective)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (effective == RoiResultMergeMode.TaskSpecific) throw new NotSupportedException("TaskSpecific merge mode requires an application-provided detection merger.");

            var candidates = new List<RoiProjectedDetection>();
            foreach (RoiInferenceItem item in batch.Succeeded)
            {
                if (item.Result!.Value is not DetectionResult result) throw new VisualException(VisualErrorCodes.DecodeFailed, "ROI detection merging requires DetectionResult payloads.", technicalDetails: "roiId=" + item.Roi.Id + ";actual=" + item.Result.Value.GetType().FullName);
                foreach (Detection detection in result.Detections)
                {
                    // Visual decoders already apply PreparedVisualInput.Transform and
                    // expose canonical source-space boxes. Do not apply the ROI
                    // projection a second time here; that would double the crop offset.
                    candidates.Add(new RoiProjectedDetection(item.Roi.Id, detection, priority: item.Roi.Priority));
                }
            }

            candidates.Sort((left, right) => Compare(left, right, effective));
            if (effective == RoiResultMergeMode.KeepAll)
            {
                return new RoiMergedDetectionResult(batch.Snapshot.SourceSize, batch.SnapshotVersion, candidates.Take(maximumDetections));
            }

            if (effective == RoiResultMergeMode.WeightedBoxFusion)
            {
                IReadOnlyList<RoiProjectedDetection> fused = RoiDetectionMergeMath.WeightedBoxFusion(candidates, iouThreshold, maximumDetections);
                return new RoiMergedDetectionResult(batch.Snapshot.SourceSize, batch.SnapshotVersion, fused);
            }

            bool classAgnostic = effective == RoiResultMergeMode.ClassAgnosticNms;
            var kept = new List<RoiProjectedDetection>(Math.Min(candidates.Count, maximumDetections));
            for (int index = 0; index < candidates.Count && kept.Count < maximumDetections; index++)
            {
                RoiProjectedDetection candidate = candidates[index];
                int overlapIndex = -1;
                for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                {
                    RoiProjectedDetection existing = kept[keptIndex];
                    if (!classAgnostic && existing.Detection.Label.Index != candidate.Detection.Label.Index) continue;
                    if (DetectionDecoder.IntersectionOverUnion(existing.Detection.Box, candidate.Detection.Box) > iouThreshold) { overlapIndex = keptIndex; break; }
                }
                if (overlapIndex < 0) kept.Add(candidate);
                else
                {
                    RoiProjectedDetection winner = kept[overlapIndex];
                    kept[overlapIndex] = new RoiProjectedDetection(winner.RoiId, winner.Detection, winner.WindowIndex, winner.Priority, winner.ContributingRoiIds.Concat(candidate.ContributingRoiIds));
                }
            }
            return new RoiMergedDetectionResult(batch.Snapshot.SourceSize, batch.SnapshotVersion, kept);
        }

        /// <summary>Provides the shared deterministic weighted-box fusion implementation used by batch and generic adapters. / 提供批次入口和通用适配器共用的确定性加权框融合实现。</summary>
        internal static class RoiDetectionMergeMath
        {
            internal static IReadOnlyList<RoiProjectedDetection> WeightedBoxFusion(IReadOnlyList<RoiProjectedDetection> candidates, float iouThreshold, int maximumDetections)
            {
                if (candidates == null) throw new ArgumentNullException(nameof(candidates));
                if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
                if (maximumDetections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDetections));

                var ordered = candidates.Select(value => value ?? throw new ArgumentException("Detection candidates cannot contain null values.", nameof(candidates))).ToList();
                ordered.Sort(CompareForFusion);
                var groups = new List<FusionGroup>();
                foreach (RoiProjectedDetection candidate in ordered)
                {
                    FusionGroup? selected = null;
                    float selectedIou = 0;
                    for (int index = 0; index < groups.Count; index++)
                    {
                        FusionGroup group = groups[index];
                        if (group.Representative.Detection.Label.Index != candidate.Detection.Label.Index) continue;
                        float iou = DetectionDecoder.IntersectionOverUnion(group.Box, candidate.Detection.Box);
                        if (iou < iouThreshold) continue;
                        if (selected == null || iou > selectedIou)
                        {
                            selected = group;
                            selectedIou = iou;
                        }
                    }

                    if (selected == null)
                    {
                        if (groups.Count >= maximumDetections) continue;
                        groups.Add(new FusionGroup(candidate));
                    }
                    else
                    {
                        selected.Add(candidate);
                    }
                }

                var result = groups.Select(value => value.ToResult()).ToList();
                result.Sort(CompareForFusion);
                return new ReadOnlyCollection<RoiProjectedDetection>(result.Take(maximumDetections).ToList());
            }

            private static int CompareForFusion(RoiProjectedDetection left, RoiProjectedDetection right)
            {
                int score = right.Detection.Label.Score.CompareTo(left.Detection.Label.Score);
                if (score != 0) return score;
                int priority = right.Priority.CompareTo(left.Priority);
                if (priority != 0) return priority;
                int roi = string.Compare(left.RoiId, right.RoiId, StringComparison.Ordinal);
                if (roi != 0) return roi;
                int label = left.Detection.Label.Index.CompareTo(right.Detection.Label.Index);
                if (label != 0) return label;
                int x = left.Detection.Box.X.CompareTo(right.Detection.Box.X);
                if (x != 0) return x;
                int y = left.Detection.Box.Y.CompareTo(right.Detection.Box.Y);
                return y != 0 ? y : Nullable.Compare(left.WindowIndex, right.WindowIndex);
            }

            private sealed class FusionGroup
            {
                private readonly List<RoiProjectedDetection> _members = new List<RoiProjectedDetection>();
                private float _weight;
                private float _scoreSum;
                private float _left;
                private float _top;
                private float _right;
                private float _bottom;

                internal FusionGroup(RoiProjectedDetection first)
                {
                    Representative = first;
                    Add(first);
                }

                internal RoiProjectedDetection Representative { get; private set; }
                internal RectangleF Box => new RectangleF(_left, _top, Math.Max(0, _right - _left), Math.Max(0, _bottom - _top));

                internal void Add(RoiProjectedDetection candidate)
                {
                    _members.Add(candidate);
                    float score = candidate.Detection.Label.Score;
                    float weight = score > 0 ? score : 1f;
                    if (_members.Count == 1)
                    {
                        _left = candidate.Detection.Box.X;
                        _top = candidate.Detection.Box.Y;
                        _right = candidate.Detection.Box.Right;
                        _bottom = candidate.Detection.Box.Bottom;
                    }
                    else
                    {
                        float total = _weight + weight;
                        _left = ((_left * _weight) + (candidate.Detection.Box.X * weight)) / total;
                        _top = ((_top * _weight) + (candidate.Detection.Box.Y * weight)) / total;
                        _right = ((_right * _weight) + (candidate.Detection.Box.Right * weight)) / total;
                        _bottom = ((_bottom * _weight) + (candidate.Detection.Box.Bottom * weight)) / total;
                    }
                    _weight += weight;
                    _scoreSum += score;
                    if (candidate.Detection.Label.Score > Representative.Detection.Label.Score) Representative = candidate;
                }

                internal RoiProjectedDetection ToResult()
                {
                    float score = _scoreSum / _members.Count;
                    var label = new LabelScore(Representative.Detection.Label.Index, Representative.Detection.Label.Label, score);
                    var detection = new Detection(Box, label);
                    return new RoiProjectedDetection(Representative.RoiId, detection, Representative.WindowIndex, Representative.Priority, _members.SelectMany(value => value.ContributingRoiIds));
                }
            }
        }

        private static int Compare(RoiProjectedDetection left, RoiProjectedDetection right, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.RoiPriority)
            {
                int priority = right.Priority.CompareTo(left.Priority);
                if (priority != 0) return priority;
            }
            int score = right.Detection.Label.Score.CompareTo(left.Detection.Label.Score);
            if (score != 0) return score;
            int fallbackPriority = right.Priority.CompareTo(left.Priority);
            if (fallbackPriority != 0) return fallbackPriority;
            int roi = string.Compare(left.RoiId, right.RoiId, StringComparison.Ordinal);
            if (roi != 0) return roi;
            int label = left.Detection.Label.Index.CompareTo(right.Detection.Label.Index);
            if (label != 0) return label;
            int x = left.Detection.Box.X.CompareTo(right.Detection.Box.X);
            if (x != 0) return x;
            int y = left.Detection.Box.Y.CompareTo(right.Detection.Box.Y);
            return y != 0 ? y : Nullable.Compare(left.WindowIndex, right.WindowIndex);
        }
    }
}
