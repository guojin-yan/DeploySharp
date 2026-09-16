using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains task-specific results produced by one bounded ROI sliding-window run. / 包含一次有界 ROI 滑窗运行产生的任务专用结果。</summary>
    /// <typeparam name="TResult">The task result type. / 任务结果类型。</typeparam>
    public sealed class VisualRoiSlidingWindowResult<TResult>
    {
        internal VisualRoiSlidingWindowResult(IReadOnlyList<SlidingWindow> windows, IReadOnlyList<RoiProjectedResult<TResult>> results, RoiExecutionDiagnostics? diagnostics = null)
        {
            Windows = windows ?? throw new ArgumentNullException(nameof(windows));
            Results = results ?? throw new ArgumentNullException(nameof(results));
            Diagnostics = diagnostics ?? new RoiExecutionDiagnostics(RoiExecutionMode.SlidingWindow, 1, Windows.Count, 0, 0, Results.Count, Windows.Count - Results.Count, 0, TimeSpan.Zero, false);
        }

        /// <summary>Gets windows in deterministic preparation order. / 获取按确定性准备顺序排列的窗口。</summary>
        public IReadOnlyList<SlidingWindow> Windows { get; }
        /// <summary>Gets one projected result for each successfully evaluated window. / 获取每个成功评估窗口对应的一个投影结果。</summary>
        public IReadOnlyList<RoiProjectedResult<TResult>> Results { get; }
        /// <summary>Gets the number of evaluated windows. / 获取评估窗口数。</summary>
        public int WindowCount => Windows.Count;
        /// <summary>Gets bounded execution counters and elapsed time. / 获取有界执行计数和耗时。</summary>
        public RoiExecutionDiagnostics Diagnostics { get; }
    }

    /// <summary>Contains raw projected window results and their task-specific merged output. / 包含窗口投影结果及任务专用合并输出。</summary>
    /// <typeparam name="TResult">The decoded task result type. / 解码后的任务结果类型。</typeparam>
    public sealed class VisualRoiSlidingWindowMergedResult<TResult>
        where TResult : class
    {
        internal VisualRoiSlidingWindowMergedResult(VisualRoiSlidingWindowResult<TResult> windows, IReadOnlyList<TResult> merged)
        {
            Windows = windows ?? throw new ArgumentNullException(nameof(windows));
            Merged = merged ?? throw new ArgumentNullException(nameof(merged));
        }

        /// <summary>Gets every successfully projected window result before deduplication. / 获取去重前每个成功投影的窗口结果。</summary>
        public VisualRoiSlidingWindowResult<TResult> Windows { get; }
        /// <summary>Gets the task-specific merged results. / 获取任务专用合并结果。</summary>
        public IReadOnlyList<TResult> Merged { get; }
        /// <summary>Gets the underlying sliding-window execution diagnostics. / 获取底层滑窗执行诊断。</summary>
        public RoiExecutionDiagnostics Diagnostics => Windows.Diagnostics;
    }

    /// <summary>Runs any Visual task over bounded overlapping ROI windows and leaves projection/merge semantics to task contracts. / 在有界重叠 ROI 窗口上运行任意 Visual 任务，并将投影/合并语义交给任务合同。</summary>
    /// <typeparam name="TResult">The decoded task result type. / 解码后的任务结果类型。</typeparam>
    public sealed class VisualRoiSlidingWindowRunner<TResult>
        where TResult : class
    {
        private readonly VisualPipeline _pipeline;

        /// <summary>Initializes a task-neutral runner over a Visual pipeline. / 使用 Visual Pipeline 初始化任务无关运行器。</summary>
        public VisualRoiSlidingWindowRunner(VisualPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        /// <summary>Runs one ROI and returns one caller-projected result per evaluated window. / 运行一个 ROI，并为每个窗口返回一个调用方投影结果。</summary>
        public Task<VisualRoiSlidingWindowResult<TResult>> RunRoiAsync(
            VisualSize sourceSize,
            VisualRoi roi,
            IVisualRoiGeometry geometry,
            SlidingWindowDetectionOptions options,
            Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, SlidingWindow, PreparedVisualInput, RoiProjectedResult<TResult>> materialize,
            VisualExecutionOptions? executionOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (roi == null) throw new ArgumentNullException(nameof(roi));
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (materialize == null) throw new ArgumentNullException(nameof(materialize));
            return RunCoreAsync(sourceSize, roi, geometry, options, prepareAsync, materialize, executionOptions, cancellationToken);
        }

        /// <summary>Runs one ROI, projects each decoded result, and applies a task-specific merger. / 运行一个 ROI、投影每个解码结果并应用任务专用合并器。</summary>
        public async Task<VisualRoiSlidingWindowMergedResult<TResult>> RunRoiAndMergeAsync(
            VisualSize sourceSize,
            VisualRoi roi,
            IVisualRoiGeometry geometry,
            SlidingWindowDetectionOptions options,
            Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, TResult> decode,
            IRoiResultProjector<TResult> projector,
            IRoiResultMerger<TResult> merger,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.KeepAll,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            if (projector == null) throw new ArgumentNullException(nameof(projector));
            if (merger == null) throw new ArgumentNullException(nameof(merger));
            VisualRoiSlidingWindowResult<TResult> windows = await RunRoiAsync(
                sourceSize,
                roi,
                geometry,
                options,
                prepareAsync,
                (inference, window, prepared) =>
                {
                    TResult decoded = decode(inference) ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The sliding-window decode callback returned null.");
                    RoiProjection projection = CreateProjection(roi, prepared, coordinateContext);
                    TResult projected = projector.Project(decoded, projection) ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The sliding-window ROI projector returned null.", technicalDetails: "roiId=" + roi.Id);
                    return new RoiProjectedResult<TResult>(roi.Id, roi.Priority, projected, window.Index);
                },
                executionOptions,
                cancellationToken).ConfigureAwait(false);
            IReadOnlyList<TResult> merged = windows.Results.Count == 0
                ? Array.Empty<TResult>()
                : merger.Merge(windows.Results, mergeMode);
            return new VisualRoiSlidingWindowMergedResult<TResult>(windows, merged);
        }

        /// <summary>Runs all enabled Include SlidingWindow ROIs, projects each window, and merges the complete source result set. / 运行快照中所有启用的 Include SlidingWindow ROI，投影每个窗口并合并完整源图结果集。</summary>
        /// <remarks>TileLocal and World ROIs require a matching explicit coordinate context. The same task-specific projector and merger are used for every ROI, while ROI provenance and window indices remain attached to every candidate. / TileLocal 和 World ROI 必须提供匹配的显式坐标上下文；所有 ROI 使用同一任务专用投影器和合并器，但每个候选仍保留 ROI 来源和窗口索引。</remarks>
        public async Task<VisualRoiSlidingWindowMergedResult<TResult>> RunRoisAndMergeAsync(
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, TResult> decode,
            IRoiResultProjector<TResult> projector,
            IRoiResultMerger<TResult> merger,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.KeepAll,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            if (projector == null) throw new ArgumentNullException(nameof(projector));
            if (merger == null) throw new ArgumentNullException(nameof(merger));
            VisualRoiSlidingWindowResult<TResult> windows = await RunRoisAsync(
                snapshot,
                options,
                prepareAsync,
                (roi, inference, window, prepared) =>
                {
                    TResult decoded = decode(inference) ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The sliding-window decode callback returned null.");
                    RoiProjection projection = CreateProjection(roi, prepared, coordinateContext);
                    TResult projected = projector.Project(decoded, projection) ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The sliding-window ROI projector returned null.", technicalDetails: "roiId=" + roi.Id);
                    return new RoiProjectedResult<TResult>(roi.Id, roi.Priority, projected, window.Index);
                },
                executionOptions,
                maximumRois,
                cancellationToken,
                coordinateContext).ConfigureAwait(false);
            IReadOnlyList<TResult> merged = windows.Results.Count == 0
                ? Array.Empty<TResult>()
                : merger.Merge(windows.Results, mergeMode);
            return new VisualRoiSlidingWindowMergedResult<TResult>(windows, merged);
        }

        /// <summary>Runs every enabled Include SlidingWindow ROI in one snapshot. / 运行快照中所有启用的 Include SlidingWindow ROI。</summary>
        public async Task<VisualRoiSlidingWindowResult<TResult>> RunRoisAsync(
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualRoi, VisualInferenceResult, SlidingWindow, PreparedVisualInput, RoiProjectedResult<TResult>> materialize,
            VisualExecutionOptions? executionOptions = null,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (materialize == null) throw new ArgumentNullException(nameof(materialize));
            if (maximumRois <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRois));
            if (coordinateContext != null && coordinateContext.SourceSize != snapshot.SourceSize) throw new ArgumentException("The ROI coordinate context source size must match the snapshot source size.", nameof(coordinateContext));
            if (snapshot.Rois.Any(value => value.Enabled && value.InclusionMode == RoiInclusionMode.Include && value.ExecutionMode == RoiExecutionMode.SlidingWindow && value.AppliesTo(_pipeline.Selection.Profile.Task) && (value.CoordinateSpace == RoiCoordinateSpace.TileLocal || value.CoordinateSpace == RoiCoordinateSpace.World)) && coordinateContext == null)
            {
                throw new NotSupportedException("TileLocal and World sliding-window ROIs require an explicit projection context.");
            }
            var allWindows = new List<SlidingWindow>();
            var allResults = new List<RoiProjectedResult<TResult>>();
            var selectedRois = new List<VisualRoi>();
            var selectedGeometries = new List<IVisualRoiGeometry>();
            int selectedCount = 0;
            int totalWindows = 0;
            long totalPreparedPixels = 0;
            int preparedInputCount = 0;
            int inferenceCallCount = 0;
            int succeededResultCount = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || roi.InclusionMode != RoiInclusionMode.Include || roi.ExecutionMode != RoiExecutionMode.SlidingWindow || !roi.AppliesTo(_pipeline.Selection.Profile.Task)) continue;
                if (++selectedCount > maximumRois) throw new VisualException(VisualErrorCodes.InputInvalid, "The sliding-window ROI count exceeds the configured limit.", technicalDetails: "maximumRois=" + maximumRois);
                IVisualRoiGeometry geometry = roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World
                    ? snapshot.Resolve(roi, coordinateContext ?? throw new NotSupportedException("TileLocal and World sliding-window ROIs require an explicit projection context."))
                    : snapshot.Resolve(roi);
                IReadOnlyList<SlidingWindow> planned = SlidingWindowDetectionRunner.PlanWindows(snapshot.SourceSize, options, geometry, roi.Id);
                totalWindows = checked(totalWindows + planned.Count);
                if (totalWindows > options.MaximumWindows) throw new VisualException(VisualErrorCodes.DecodeFailed, "Sliding-window generation exceeded the configured cross-ROI window limit.", technicalDetails: "maximumWindows=" + options.MaximumWindows);
                totalPreparedPixels = checked(totalPreparedPixels + SlidingWindowDetectionRunner.EstimatePreparedPixels(planned));
                if (totalPreparedPixels > options.MaximumPreparedPixels) throw new VisualException(VisualErrorCodes.InputInvalid, "Sliding-window preparation exceeds the configured cross-ROI pixel budget.", technicalDetails: "pixels=" + totalPreparedPixels + ";limit=" + options.MaximumPreparedPixels);
                selectedRois.Add(roi);
                selectedGeometries.Add(geometry);
            }
            for (int index = 0; index < selectedRois.Count; index++)
            {
                VisualRoi roi = selectedRois[index];
                IVisualRoiGeometry geometry = selectedGeometries[index];
                VisualRoiSlidingWindowResult<TResult> result = await RunRoiAsync(snapshot.SourceSize, roi, geometry, options, (window, token) => prepareAsync(roi, geometry, window, token), (inference, window, prepared) => materialize(roi, inference, window, prepared), executionOptions, cancellationToken).ConfigureAwait(false);
                allWindows.AddRange(result.Windows);
                allResults.AddRange(result.Results);
                preparedInputCount = checked(preparedInputCount + result.Diagnostics.PreparedInputCount);
                inferenceCallCount = checked(inferenceCallCount + result.Diagnostics.InferenceCallCount);
                succeededResultCount = checked(succeededResultCount + result.Diagnostics.SucceededResultCount);
            }
            stopwatch.Stop();
            return new VisualRoiSlidingWindowResult<TResult>(new ReadOnlyCollection<SlidingWindow>(allWindows), new ReadOnlyCollection<RoiProjectedResult<TResult>>(allResults),
                new RoiExecutionDiagnostics(RoiExecutionMode.SlidingWindow, selectedCount, totalWindows, preparedInputCount, inferenceCallCount, succeededResultCount, totalWindows - succeededResultCount, totalPreparedPixels, stopwatch.Elapsed, false));
        }

        private async Task<VisualRoiSlidingWindowResult<TResult>> RunCoreAsync(
            VisualSize sourceSize,
            VisualRoi roi,
            IVisualRoiGeometry geometry,
            SlidingWindowDetectionOptions options,
            Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, SlidingWindow, PreparedVisualInput, RoiProjectedResult<TResult>> materialize,
            VisualExecutionOptions? executionOptions,
            CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            IReadOnlyList<SlidingWindow> windows = SlidingWindowDetectionRunner.PlanWindows(sourceSize, options, geometry, roi.Id);
            int chunkSize = Math.Max(1, _pipeline.MaximumConcurrency * 2);
            var results = new List<RoiProjectedResult<TResult>>(windows.Count);
            VisualExecutionOptions requested = executionOptions ?? VisualExecutionOptions.Default;
            var runOptions = new VisualExecutionOptions(requested.Timeout, disposeOwnedInputOnCompletion: true, requested.CorrelationId);
            int preparedInputCount = 0;
            int inferenceCallCount = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int offset = 0; offset < windows.Count; offset += chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(chunkSize, windows.Count - offset);
                PreparedVisualInput[] prepared = await PrepareChunkAsync(windows, offset, count, prepareAsync, cancellationToken).ConfigureAwait(false);
                preparedInputCount = checked(preparedInputCount + prepared.Length);
                    try
                    {
                        IReadOnlyList<VisualInferenceResult> inferences = await _pipeline.RunManyAsync(prepared, runOptions, cancellationToken).ConfigureAwait(false);
                        inferenceCallCount++;
                        if (inferences.Count != count) throw new VisualException(VisualErrorCodes.DecodeFailed, "The Visual pipeline returned a result count different from the sliding-window batch.");
                    for (int index = 0; index < count; index++)
                    {
                        VisualInferenceResult inference = inferences[index] ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The Visual pipeline returned a null sliding-window result.");
                        RoiProjectedResult<TResult> projected = materialize(inference, windows[offset + index], prepared[index]);
                        if (projected == null) throw new ArgumentException("The sliding-window materialize callback returned null.", nameof(materialize));
                        if (!string.Equals(projected.RoiId, roi.Id, StringComparison.Ordinal) || projected.Priority != roi.Priority)
                        {
                            throw new VisualException(VisualErrorCodes.InputInvalid, "A sliding-window materialize callback returned ROI provenance different from the scheduled ROI.", technicalDetails: "expected=" + roi.Id + ";actual=" + projected.RoiId);
                        }
                        if (projected.WindowIndex.HasValue && projected.WindowIndex.Value != windows[offset + index].Index)
                        {
                            throw new VisualException(VisualErrorCodes.InputInvalid, "A sliding-window materialize callback returned a window index different from the scheduled window.", technicalDetails: "expected=" + windows[offset + index].Index + ";actual=" + projected.WindowIndex.Value);
                        }
                        results.Add(projected);
                    }
                }
                finally
                {
                    DisposeOwned(prepared);
                }
            }
            stopwatch.Stop();
            return new VisualRoiSlidingWindowResult<TResult>(windows, new ReadOnlyCollection<RoiProjectedResult<TResult>>(results),
                new RoiExecutionDiagnostics(RoiExecutionMode.SlidingWindow, 1, windows.Count, preparedInputCount, inferenceCallCount, results.Count, windows.Count - results.Count, SlidingWindowDetectionRunner.EstimatePreparedPixels(windows), stopwatch.Elapsed, false, requested.CorrelationId));
        }

        private static Task<PreparedVisualInput[]> PrepareChunkAsync(IReadOnlyList<SlidingWindow> windows, int offset, int count, Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() => PrepareChunkCoreAsync(windows, offset, count, prepareAsync, cancellationToken), CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();
        }

        private static async Task<PreparedVisualInput[]> PrepareChunkCoreAsync(IReadOnlyList<SlidingWindow> windows, int offset, int count, Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync, CancellationToken cancellationToken)
        {
            var tasks = new Task<PreparedVisualInput>[count];
            int started = 0;
            try
            {
                for (int index = 0; index < count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tasks[index] = prepareAsync(windows[offset + index], cancellationToken) ?? throw new ArgumentException("The sliding-window prepare callback returned a null task.", nameof(prepareAsync));
                    started++;
                }
                PreparedVisualInput[] prepared = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (int index = 0; index < prepared.Length; index++)
                {
                    if (prepared[index] == null) throw new ArgumentException("The sliding-window prepare callback returned null.", nameof(prepareAsync));
                    if (prepared[index].BatchSize != 1) throw new VisualException(VisualErrorCodes.InputInvalid, "Task-specific ROI sliding-window execution requires one image per prepared window.", tensorName: prepared[index].InputName);
                }
                return prepared;
            }
            catch
            {
                for (int index = 0; index < started; index++)
                {
                    try
                    {
                        PreparedVisualInput prepared = await tasks[index].ConfigureAwait(false);
                        if (prepared?.Ownership == PreparedInputOwnership.Owned) prepared.Dispose();
                    }
                    catch { }
                }
                throw;
            }
        }

        private static void DisposeOwned(IReadOnlyList<PreparedVisualInput> prepared)
        {
            for (int index = 0; index < prepared.Count; index++) if (prepared[index]?.Ownership == PreparedInputOwnership.Owned) prepared[index].Dispose();
        }

        private static RoiProjection CreateProjection(VisualRoi roi, PreparedVisualInput prepared, VisualRoiCoordinateContext? coordinateContext)
        {
            if (roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World)
            {
                return prepared.CreateRoiProjection(roi, coordinateContext ?? throw new NotSupportedException("TileLocal and World sliding-window projection requires an explicit projection context."));
            }
            return prepared.CreateRoiProjection(roi);
        }
    }
}
