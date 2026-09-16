using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;

namespace JYPPX.DeploySharp.Visual.Configuration.Json
{
    /// <summary>Identifies a semantic change between two ROI snapshots. / 标识两个 ROI 快照之间的语义变化。</summary>
    public enum VisualRoiChangeKind
    {
        /// <summary>A new ROI was added. / 新增 ROI。</summary>
        Added = 0,
        /// <summary>An existing ROI was removed. / 删除已有 ROI。</summary>
        Removed = 1,
        /// <summary>An existing ROI changed. / 修改已有 ROI。</summary>
        Changed = 2
    }

    /// <summary>Describes one deterministic ROI change and its affected fields. / 描述一项确定性的 ROI 变化及受影响字段。</summary>
    public sealed class VisualRoiChange
    {
        internal VisualRoiChange(string roiId, VisualRoiChangeKind kind, IEnumerable<string> paths)
        {
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            if (!Enum.IsDefined(typeof(VisualRoiChangeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            RoiId = roiId;
            Kind = kind;
            ChangedPaths = new ReadOnlyCollection<string>(paths.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
        }

        /// <summary>Gets the affected ROI identifier. / 获取受影响的 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets the change kind. / 获取变化类型。</summary>
        public VisualRoiChangeKind Kind { get; }
        /// <summary>Gets deterministic relative field paths; added/removed entries contain an empty list. / 获取确定性的相对字段路径；新增/删除项为空列表。</summary>
        public IReadOnlyList<string> ChangedPaths { get; }
    }

    /// <summary>Contains a semantic, deterministic diff between two ROI snapshots. / 包含两个 ROI 快照之间语义且确定性的差异。</summary>
    public sealed class VisualRoiDiff
    {
        internal VisualRoiDiff(VisualSize beforeSourceSize, VisualSize afterSourceSize, IEnumerable<VisualRoiChange> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));
            BeforeSourceSize = beforeSourceSize;
            AfterSourceSize = afterSourceSize;
            _changes = new ReadOnlyCollection<VisualRoiChange>(changes.ToList());
        }

        private readonly IReadOnlyList<VisualRoiChange> _changes;
        /// <summary>Gets the source size in the first snapshot. / 获取首个快照的源图尺寸。</summary>
        public VisualSize BeforeSourceSize { get; }
        /// <summary>Gets the source size in the second snapshot. / 获取第二个快照的源图尺寸。</summary>
        public VisualSize AfterSourceSize { get; }
        /// <summary>Gets whether source dimensions changed. / 获取源图尺寸是否变化。</summary>
        public bool SourceSizeChanged => BeforeSourceSize != AfterSourceSize;
        /// <summary>Gets all ROI changes in ordinal ID order. / 获取按序号 ID 排序的全部 ROI 变化。</summary>
        public IReadOnlyList<VisualRoiChange> Changes => _changes;
        /// <summary>Gets whether any source or ROI behavior changed. / 获取源图或 ROI 行为是否发生变化。</summary>
        public bool HasChanges => SourceSizeChanged || _changes.Count > 0;
    }

    /// <summary>Computes semantic and stable diffs for ROI snapshots. / 计算 ROI 快照的语义稳定差异。</summary>
    public static class VisualRoiJsonDiff
    {
        /// <summary>Compares two snapshots while ignoring their file/manager version metadata. / 比较两个快照并忽略文件或管理器版本元数据。</summary>
        public static VisualRoiDiff Compare(VisualRoiSnapshot before, VisualRoiSnapshot after)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            var beforeById = before.Rois.ToDictionary(value => value.Id, StringComparer.Ordinal);
            var afterById = after.Rois.ToDictionary(value => value.Id, StringComparer.Ordinal);
            var changes = new List<VisualRoiChange>();
            foreach (string id in beforeById.Keys.Concat(afterById.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                bool hasBefore = beforeById.TryGetValue(id, out VisualRoi? previous);
                bool hasAfter = afterById.TryGetValue(id, out VisualRoi? current);
                if (!hasBefore)
                {
                    changes.Add(new VisualRoiChange(id, VisualRoiChangeKind.Added, Array.Empty<string>()));
                    continue;
                }
                if (!hasAfter)
                {
                    changes.Add(new VisualRoiChange(id, VisualRoiChangeKind.Removed, Array.Empty<string>()));
                    continue;
                }
                IReadOnlyList<string> paths = CompareRoi(previous!, current!);
                if (paths.Count > 0) changes.Add(new VisualRoiChange(id, VisualRoiChangeKind.Changed, paths));
            }
            return new VisualRoiDiff(before.SourceSize, after.SourceSize, changes);
        }

        private static IReadOnlyList<string> CompareRoi(VisualRoi before, VisualRoi after)
        {
            var paths = new List<string>();
            if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal)) paths.Add("name");
            if (before.Enabled != after.Enabled) paths.Add("enabled");
            if (before.Priority != after.Priority) paths.Add("priority");
            if (before.CoordinateSpace != after.CoordinateSpace) paths.Add("coordinateSpace");
            if (before.InclusionMode != after.InclusionMode) paths.Add("inclusionMode");
            if (before.ExecutionMode != after.ExecutionMode) paths.Add("executionMode");
            if (before.HitTestMode != after.HitTestMode) paths.Add("hitTestMode");
            if (before.HitThreshold != after.HitThreshold) paths.Add("hitThreshold");
            if (before.Margin != after.Margin) paths.Add("margin");
            if (before.ConfidenceOverride != after.ConfidenceOverride) paths.Add("confidenceOverride");
            if (!before.TaskFilter.Select(value => value.Value).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(after.TaskFilter.Select(value => value.Value).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)) paths.Add("taskFilter");
            if (!before.ClassFilter.OrderBy(value => value).SequenceEqual(after.ClassFilter.OrderBy(value => value))) paths.Add("classFilter");
            if (!before.Tags.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(after.Tags.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)) paths.Add("tags");
            if (!MetadataEqual(before.Metadata, after.Metadata)) paths.Add("metadata");
            if (!GeometryEqual(before.Geometry, after.Geometry)) paths.Add("geometry");
            return new ReadOnlyCollection<string>(paths);
        }

        private static bool MetadataEqual(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
        {
            if (before.Count != after.Count) return false;
            foreach (KeyValuePair<string, string> entry in before)
            {
                if (!after.TryGetValue(entry.Key, out string? value) || !string.Equals(entry.Value, value, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static bool GeometryEqual(IVisualRoiGeometry before, IVisualRoiGeometry after)
        {
            if (before.Kind != after.Kind || before.Points.Count != after.Points.Count) return false;
            for (int index = 0; index < before.Points.Count; index++) if (before.Points[index] != after.Points[index]) return false;
            if (before is MaskRoiGeometry beforeMask && after is MaskRoiGeometry afterMask) return beforeMask.ToArray().SequenceEqual(afterMask.ToArray());
            return true;
        }
    }
}
