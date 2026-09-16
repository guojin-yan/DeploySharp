using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains deterministic classification candidates and the selected ROI results. / 包含确定性的分类候选项及选中的 ROI 结果。</summary>
    public sealed class RoiMergedClassificationResult
    {
        private readonly IReadOnlyList<RoiProjectedResult<ClassificationResult>> _candidates;
        private readonly IReadOnlyList<RoiProjectedResult<ClassificationResult>> _items;

        internal RoiMergedClassificationResult(VisualSize sourceSize, long snapshotVersion, IEnumerable<RoiProjectedResult<ClassificationResult>> candidates, IEnumerable<RoiProjectedResult<ClassificationResult>> items)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (items == null) throw new ArgumentNullException(nameof(items));
            SourceSize = sourceSize;
            SnapshotVersion = snapshotVersion;
            _candidates = new ReadOnlyCollection<RoiProjectedResult<ClassificationResult>>(candidates.ToList());
            _items = new ReadOnlyCollection<RoiProjectedResult<ClassificationResult>>(items.ToList());
        }

        /// <summary>Gets the source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the immutable ROI snapshot version. / 获取不可变 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets every successful classification candidate before selection. / 获取选择前的全部成功分类候选项。</summary>
        public IReadOnlyList<RoiProjectedResult<ClassificationResult>> Candidates => _candidates;
        /// <summary>Gets the results selected by the merge policy. / 获取合并策略选中的结果。</summary>
        public IReadOnlyList<RoiProjectedResult<ClassificationResult>> Items => _items;
        /// <summary>Gets the primary selected result, or null when no ROI succeeded. / 获取主选结果；没有成功 ROI 时为空。</summary>
        public RoiProjectedResult<ClassificationResult>? Primary => _items.Count == 0 ? null : _items[0];
        /// <summary>Gets the primary classification payload, or null when no ROI succeeded. / 获取主分类载荷；没有成功 ROI 时为空。</summary>
        public ClassificationResult? Result => Primary?.Result;
    }

    /// <summary>Merges crop-and-infer classification results without inventing spatial semantics. / 合并 CropAndInfer 分类结果且不虚构空间语义。</summary>
    public sealed class RoiClassificationResultMerger
    {
        /// <summary>Merges successful classification items using a deterministic policy. / 使用确定性策略合并成功的分类项。</summary>
        /// <remarks><see cref="RoiResultMergeMode.KeepAll"/> preserves every ROI. <see cref="RoiResultMergeMode.HighestConfidence"/> selects the highest top score. <see cref="RoiResultMergeMode.RoiPriority"/> selects the highest-priority ROI, then score. Spatial NMS modes are intentionally rejected for classification. / KeepAll 保留全部 ROI；HighestConfidence 选择最高 Top 分数；RoiPriority 优先选择 ROI 优先级再比较分数。分类任务不支持空间 NMS，因此显式拒绝。</remarks>
        public RoiMergedClassificationResult Merge(RoiInferenceBatchResult batch, RoiResultMergeMode? mode = null)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            RoiResultMergeMode effective = mode ?? batch.MergeMode;
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), effective)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (effective == RoiResultMergeMode.ClassAwareNms || effective == RoiResultMergeMode.ClassAgnosticNms || effective == RoiResultMergeMode.TaskSpecific || effective == RoiResultMergeMode.WeightedBoxFusion)
            {
                throw new NotSupportedException("Classification ROI merging does not support spatial NMS; use KeepAll, HighestConfidence, or RoiPriority.");
            }

            var candidates = new List<RoiProjectedResult<ClassificationResult>>();
            foreach (RoiInferenceItem item in batch.Succeeded)
            {
                if (item.Result!.Value is not ClassificationResult result)
                {
                    throw new VisualException(VisualErrorCodes.DecodeFailed, "ROI classification merging requires ClassificationResult payloads.", technicalDetails: "roiId=" + item.Roi.Id + ";actual=" + item.Result.Value.GetType().FullName);
                }
                candidates.Add(new RoiProjectedResult<ClassificationResult>(item.Roi.Id, item.Roi.Priority, result));
            }

            candidates.Sort((left, right) => Compare(left, right, effective));
            IReadOnlyList<RoiProjectedResult<ClassificationResult>> selected = effective == RoiResultMergeMode.KeepAll
                ? candidates
                : candidates.Take(1).ToList();
            return new RoiMergedClassificationResult(batch.Snapshot.SourceSize, batch.SnapshotVersion, candidates, selected);
        }

        private static int Compare(RoiProjectedResult<ClassificationResult> left, RoiProjectedResult<ClassificationResult> right, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.RoiPriority)
            {
                int priority = right.Priority.CompareTo(left.Priority);
                if (priority != 0) return priority;
            }
            float leftScore = left.Result.TopPrediction?.Score ?? float.NegativeInfinity;
            float rightScore = right.Result.TopPrediction?.Score ?? float.NegativeInfinity;
            int score = rightScore.CompareTo(leftScore);
            if (score != 0) return score;
            int fallbackPriority = right.Priority.CompareTo(left.Priority);
            if (fallbackPriority != 0) return fallbackPriority;
            return string.Compare(left.RoiId, right.RoiId, StringComparison.Ordinal);
        }
    }
}
