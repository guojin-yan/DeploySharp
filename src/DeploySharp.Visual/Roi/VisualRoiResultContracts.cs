using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Projects one task result from ROI/model coordinates into source coordinates. / 将一个任务结果从 ROI/模型坐标投影回源图坐标。</summary>
    /// <typeparam name="TResult">The task result type. / 任务结果类型。</typeparam>
    public interface IRoiResultProjector<TResult>
    {
        /// <summary>Projects a result using the supplied reversible geometry. / 使用给定的可逆几何投影结果。</summary>
        public TResult Project(TResult result, RoiProjection projection);
    }

    /// <summary>Represents a projected result with deterministic ROI provenance. / 表示带有确定性 ROI 来源的投影结果。</summary>
    /// <typeparam name="TResult">The task result type. / 任务结果类型。</typeparam>
    public sealed class RoiProjectedResult<TResult>
    {
        /// <summary>Initializes a projected result. / 初始化投影结果。</summary>
        public RoiProjectedResult(string roiId, int priority, TResult result, int? windowIndex = null)
        {
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            if (result == null) throw new ArgumentNullException(nameof(result));
            RoiId = roiId;
            Priority = priority;
            Result = result;
            WindowIndex = windowIndex;
        }

        /// <summary>Gets the source ROI identifier. / 获取源 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets the ROI priority used for deterministic conflict resolution. / 获取用于确定性冲突处理的 ROI 优先级。</summary>
        public int Priority { get; }
        /// <summary>Gets the projected task result. / 获取投影后的任务结果。</summary>
        public TResult Result { get; }
        /// <summary>Gets the optional source window index. / 获取可选源窗口索引。</summary>
        public int? WindowIndex { get; }
    }

    /// <summary>Merges projected ROI results according to task-specific semantics. / 按任务特定语义合并 ROI 投影结果。</summary>
    /// <typeparam name="TResult">The task result type. / 任务结果类型。</typeparam>
    public interface IRoiResultMerger<TResult>
    {
        /// <summary>Merges results without changing the caller-owned inputs. / 合并结果且不修改调用方所有的输入。</summary>
        public IReadOnlyList<TResult> Merge(IReadOnlyList<RoiProjectedResult<TResult>> results, RoiResultMergeMode mode);
    }

    /// <summary>Provides a small reusable base for deterministic ROI result mergers. / 为确定性 ROI 结果合并器提供小型可复用基类。</summary>
    /// <typeparam name="TResult">The task result type. / 任务结果类型。</typeparam>
    public abstract class RoiResultMergerBase<TResult> : IRoiResultMerger<TResult>
    {
        /// <inheritdoc />
        public IReadOnlyList<TResult> Merge(IReadOnlyList<RoiProjectedResult<TResult>> results, RoiResultMergeMode mode)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            return new ReadOnlyCollection<TResult>(MergeCore(results, mode).ToList());
        }

        /// <summary>Implements task-specific merge behavior. / 实现任务特定的合并行为。</summary>
        protected abstract IEnumerable<TResult> MergeCore(IReadOnlyList<RoiProjectedResult<TResult>> results, RoiResultMergeMode mode);
    }
}
