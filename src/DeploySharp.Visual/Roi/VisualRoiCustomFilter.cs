using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents one custom result retained by ROI filtering. / 表示一个经过 ROI 过滤后保留的自定义结果。</summary>
    /// <typeparam name="TItem">The caller-owned result item type. / 调用方结果项类型。</typeparam>
    public sealed class RoiFilteredItem<TItem>
    {
        private readonly IReadOnlyList<string> _roiIds;

        /// <summary>Initializes a filtered item with deterministic ROI provenance. / 使用确定性的 ROI 来源初始化过滤结果。</summary>
        public RoiFilteredItem(TItem item, IEnumerable<string> roiIds)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (roiIds == null) throw new ArgumentNullException(nameof(roiIds));
            var ids = roiIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            Item = item;
            _roiIds = new ReadOnlyCollection<string>(ids);
        }

        /// <summary>Gets the original caller-owned item. / 获取调用方原始结果项。</summary>
        public TItem Item { get; }

        /// <summary>Gets matching Include ROI identifiers. / 获取命中的 Include ROI 标识。</summary>
        public IReadOnlyList<string> RoiIds => _roiIds;

        /// <summary>Gets the deterministic primary ROI identifier, or null when no Include ROI was configured. / 获取确定性的主 ROI 标识；未配置 Include ROI 时为空。</summary>
        public string? PrimaryRoiId => _roiIds.Count == 0 ? null : _roiIds[0];
    }

    /// <summary>Provides a task-agnostic FilterResults contract for custom model result types. / 为自定义模型结果类型提供与任务无关的 FilterResults 合同。</summary>
    public static class VisualRoiCustomFilter
    {
        /// <summary>
        /// Filters caller-owned result items against enabled FilterResults ROIs.
        /// The hit-test callback owns task geometry semantics; this method owns Include/Exclude,
        /// task/class filters, coordinate resolution and deterministic provenance.
        /// / 针对启用的 FilterResults ROI 过滤调用方结果项。命中回调负责任务几何语义；本方法负责 Include/Exclude、任务/类别过滤、坐标解析和确定性来源追溯。
        /// </summary>
        /// <typeparam name="TItem">The caller-owned result item type. / 调用方结果项类型。</typeparam>
        /// <param name="items">Decoded source-space result items in caller order. / 按调用方顺序排列的源图结果项。</param>
        /// <param name="snapshot">The immutable ROI snapshot. / 不可变 ROI 快照。</param>
        /// <param name="task">The task identity used by task filters. / 用于任务过滤的任务标识。</param>
        /// <param name="isHit">Returns whether one item matches one resolved ROI. / 返回一个结果项是否命中一个已解析 ROI。</param>
        /// <param name="classIndexSelector">Optional class selector. It is required when any matching ROI has a class filter. / 可选类别选择器；当匹配 ROI 含类别过滤时必须提供。</param>
        /// <param name="confidenceSelector">Optional result confidence selector. When a ROI has <see cref="VisualRoi.ConfidenceOverride"/>, it is required and the score must meet that override. / 可选结果置信度选择器；当 ROI 设置 <see cref="VisualRoi.ConfidenceOverride"/> 时必须提供且分数必须达到阈值。</param>
        /// <param name="coordinateContext">Optional TileLocal/World coordinate context. / 可选 TileLocal/World 坐标上下文。</param>
        public static IReadOnlyList<RoiFilteredItem<TItem>> Filter<TItem>(
            IReadOnlyList<TItem> items,
            VisualRoiSnapshot snapshot,
            VisualTaskId task,
            Func<TItem, VisualRoi, IVisualRoiGeometry, bool> isHit,
            Func<TItem, int?>? classIndexSelector = null,
            Func<TItem, float>? confidenceSelector = null,
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (task.IsEmpty) throw new ArgumentException("A visual task is required.", nameof(task));
            if (isHit == null) throw new ArgumentNullException(nameof(isHit));

            var sourceRois = new List<(VisualRoi Roi, IVisualRoiGeometry Geometry)>();
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || roi.ExecutionMode != RoiExecutionMode.FilterResults || !roi.AppliesTo(task)) continue;
                if (roi.ClassFilter.Count != 0 && classIndexSelector == null)
                {
                    throw new VisualException(
                        VisualErrorCodes.InputInvalid,
                        "A custom ROI item filter requires classIndexSelector when a matching ROI declares classFilter.",
                        technicalDetails: "roi=" + roi.Id);
                }
                if (roi.ConfidenceOverride.HasValue && confidenceSelector == null)
                {
                    throw new VisualException(
                        VisualErrorCodes.InputInvalid,
                        "A custom ROI item filter requires confidenceSelector when a matching ROI declares confidenceOverride.",
                        technicalDetails: "roi=" + roi.Id);
                }
                sourceRois.Add((roi, VisualRoiResolution.Resolve(snapshot, roi, coordinateContext)));
            }

            bool hasInclude = sourceRois.Any(value => value.Roi.InclusionMode == RoiInclusionMode.Include);
            bool requiresClassIndex = sourceRois.Any(value => value.Roi.ClassFilter.Count != 0);
            bool requiresConfidence = sourceRois.Any(value => value.Roi.ConfidenceOverride.HasValue);
            var retained = new List<RoiFilteredItem<TItem>>();
            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                TItem item = items[itemIndex];
                if (item == null) throw new ArgumentException("Custom ROI items cannot contain null values.", nameof(items));
                int? classIndex = requiresClassIndex ? classIndexSelector!(item) : null;
                float? confidence = requiresConfidence ? confidenceSelector!(item) : null;
                if (confidence.HasValue && (float.IsNaN(confidence.Value) || float.IsInfinity(confidence.Value) || confidence.Value < 0 || confidence.Value > 1)) throw new ArgumentException("Custom ROI confidence values must be finite and fall within [0,1].", nameof(confidenceSelector));
                var includeIds = new List<string>();
                bool excluded = false;
                for (int roiIndex = 0; roiIndex < sourceRois.Count; roiIndex++)
                {
                    (VisualRoi roi, IVisualRoiGeometry geometry) = sourceRois[roiIndex];
                    if (!roi.AppliesTo(task, classIndex) || (roi.ConfidenceOverride.HasValue && (!confidence.HasValue || confidence.Value < roi.ConfidenceOverride.Value)) || !isHit(item, roi, geometry)) continue;
                    if (roi.InclusionMode == RoiInclusionMode.Exclude) excluded = true;
                    else includeIds.Add(roi.Id);
                }
                if (!excluded && (!hasInclude || includeIds.Count > 0))
                {
                    retained.Add(new RoiFilteredItem<TItem>(item, hasInclude ? includeIds : Array.Empty<string>()));
                }
            }
            return new ReadOnlyCollection<RoiFilteredItem<TItem>>(retained);
        }
    }
}
