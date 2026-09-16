using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Results.Vision;
using System.Collections.ObjectModel;
using System.Linq;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Provides deterministic extraction of one row from built-in true-batch visual results. / 从内置真正 Batch 视觉结果中确定性提取一行结果。</summary>
    public static class VisualRoiBatchResultSelectors
    {
        /// <summary>Extracts a known batch row and preserves model/backend/timing metadata. / 提取已知 Batch 行并保留模型、后端和时序元数据。</summary>
        public static VisualInferenceResult SelectKnownBatchRow(VisualInferenceResult batchResult, int batchIndex)
        {
            if (batchResult == null) throw new ArgumentNullException(nameof(batchResult));
            if (batchIndex < 0) throw new ArgumentOutOfRangeException(nameof(batchIndex));
            object row = batchResult.Value switch
            {
                DetectionBatchResult value => value[batchIndex],
                ClassificationBatchResult value => value.Results[batchIndex],
                OrientedDetectionBatchResult value => value[batchIndex],
                PoseEstimationBatchResult value => value[batchIndex],
                InstanceSegmentationBatchResult value => value[batchIndex],
                SemanticSegmentationBatchResult value => value[batchIndex],
                AnomalyDetectionBatchResult value => value[batchIndex],
                BackgroundRemovalBatchResult value => value[batchIndex],
                TextRecognitionBatchResult value => value.Items[batchIndex],
                OcrOrientationBatchResult value => value.Items[batchIndex],
                _ => throw new VisualException(VisualErrorCodes.DecodeFailed, "The decoded value does not expose a built-in batch row selector.", technicalDetails: "type=" + batchResult.Value.GetType().FullName)
            };
            return new VisualInferenceResult(row, batchResult.Task, batchResult.ModelId, batchResult.BackendId, batchResult.Timing, batchResult.CorrelationId);
        }
    }

    /// <summary>Projects a result that is already expressed in source-image coordinates. / 投影已经处于源图坐标的结果。</summary>
    /// <typeparam name="TResult">The task result type. / 任务结果类型。</typeparam>
    public sealed class IdentityRoiResultProjector<TResult> : IRoiResultProjector<TResult>
    {
        /// <inheritdoc />
        public TResult Project(TResult result, RoiProjection projection)
        {
            if (result is null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            return result;
        }
    }

    /// <summary>Keeps every projected ROI result in caller order without inventing task semantics. / 按调用顺序保留全部投影 ROI 结果且不虚构任务语义。</summary>
    /// <typeparam name="TResult">The task result type. / 任务结果类型。</typeparam>
    public sealed class KeepAllRoiResultMerger<TResult> : IRoiResultMerger<TResult>
    {
        /// <inheritdoc />
        public IReadOnlyList<TResult> Merge(IReadOnlyList<RoiProjectedResult<TResult>> results, RoiResultMergeMode mode)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode != RoiResultMergeMode.KeepAll) throw new NotSupportedException("KeepAllRoiResultMerger only supports RoiResultMergeMode.KeepAll; use a task-specific merger for other policies.");
            var values = new List<TResult>(results.Count);
            foreach (RoiProjectedResult<TResult> result in results) values.Add((result ?? throw new ArgumentException("ROI results cannot contain null values.", nameof(results))).Result);
            return new ReadOnlyCollection<TResult>(values);
        }
    }

    /// <summary>Adapts a caller delegate into a reusable ROI result projector. / 将调用方委托适配为可复用 ROI 结果投影器。</summary>
    /// <typeparam name="TResult">The task result type. / 任务结果类型。</typeparam>
    public sealed class DelegateRoiResultProjector<TResult> : IRoiResultProjector<TResult>
    {
        private readonly Func<TResult, RoiProjection, TResult> _project;

        /// <summary>Initializes a delegate-backed projector. / 初始化委托投影器。</summary>
        public DelegateRoiResultProjector(Func<TResult, RoiProjection, TResult> project)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        /// <inheritdoc />
        public TResult Project(TResult result, RoiProjection projection)
        {
            if (result is null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            TResult projected = _project(result, projection);
            if (projected is null) throw new VisualException(VisualErrorCodes.DecodeFailed, "The delegate ROI projector returned null.");
            return projected;
        }
    }

    /// <summary>Adapts a caller delegate into a reusable ROI result merger. / 将调用方委托适配为可复用 ROI 结果合并器。</summary>
    /// <typeparam name="TResult">The task result type. / 任务结果类型。</typeparam>
    public sealed class DelegateRoiResultMerger<TResult> : IRoiResultMerger<TResult>
    {
        private readonly Func<IReadOnlyList<RoiProjectedResult<TResult>>, RoiResultMergeMode, IReadOnlyList<TResult>> _merge;

        /// <summary>Initializes a delegate-backed merger. / 初始化委托合并器。</summary>
        public DelegateRoiResultMerger(Func<IReadOnlyList<RoiProjectedResult<TResult>>, RoiResultMergeMode, IReadOnlyList<TResult>> merge)
        {
            _merge = merge ?? throw new ArgumentNullException(nameof(merge));
        }

        /// <inheritdoc />
        public IReadOnlyList<TResult> Merge(IReadOnlyList<RoiProjectedResult<TResult>> results, RoiResultMergeMode mode)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            IReadOnlyList<TResult> merged = _merge(results, mode);
            if (merged == null) throw new VisualException(VisualErrorCodes.DecodeFailed, "The delegate ROI merger returned null.");
            return new ReadOnlyCollection<TResult>(merged.Select(value => value is null ? throw new ArgumentException("A delegate ROI merger returned a null result.", nameof(merged)) : value).ToList());
        }
    }
}
