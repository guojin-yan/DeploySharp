using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Controls one bounded ROI execution. / 控制一次有界 ROI 执行。</summary>
    public sealed class RoiExecutionOptions
    {
        /// <summary>Initializes execution options. / 初始化执行选项。</summary>
        public RoiExecutionOptions(int maximumRois = 4096, int prefetch = 1, RoiFailureMode failureMode = RoiFailureMode.FailFast, bool preserveIndividualResults = true, RoiResultMergeMode mergeMode = RoiResultMergeMode.KeepAll, TimeSpan? timeout = null, string? correlationId = null, int maximumBatchSize = 64, long maximumPreparedPixels = 1L * 1024 * 1024 * 1024)
        {
            if (maximumRois <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRois));
            if (prefetch <= 0) throw new ArgumentOutOfRangeException(nameof(prefetch));
            if (!Enum.IsDefined(typeof(RoiFailureMode), failureMode)) throw new ArgumentOutOfRangeException(nameof(failureMode));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mergeMode)) throw new ArgumentOutOfRangeException(nameof(mergeMode));
            if (timeout.HasValue && timeout.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            if (maximumBatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatchSize));
            if (maximumPreparedPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPreparedPixels));
            MaximumRois = maximumRois;
            Prefetch = prefetch;
            FailureMode = failureMode;
            PreserveIndividualResults = preserveIndividualResults;
            MergeMode = mergeMode;
            Timeout = timeout;
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
            MaximumBatchSize = maximumBatchSize;
            MaximumPreparedPixels = maximumPreparedPixels;
        }

        /// <summary>Gets the maximum ROI count accepted by one call. / 获取一次调用接受的最大 ROI 数。</summary>
        public int MaximumRois { get; }
        /// <summary>Gets the bounded preparation look-ahead. / 获取有界准备预取数。</summary>
        public int Prefetch { get; }
        /// <summary>Gets failure behavior. / 获取失败行为。</summary>
        public RoiFailureMode FailureMode { get; }
        /// <summary>Gets whether per-ROI results are retained. / 获取是否保留逐 ROI 结果。</summary>
        public bool PreserveIndividualResults { get; }
        /// <summary>Gets the requested merge mode for a later task-specific merger. / 获取后续任务合并器使用的模式。</summary>
        public RoiResultMergeMode MergeMode { get; }
        /// <summary>Gets the optional total timeout. / 获取可选总超时。</summary>
        public TimeSpan? Timeout { get; }
        /// <summary>Gets the optional correlation identifier. / 获取可选关联标识。</summary>
        public string? CorrelationId { get; }
        /// <summary>Gets the maximum number of ROI rows allowed in one true Batch call. / 获取一次真正 Batch 调用允许的最大 ROI 行数。</summary>
        public int MaximumBatchSize { get; }
        /// <summary>Gets the maximum sum of source ROI pixels prepared by one call. / 获取一次调用准备的源图 ROI 像素面积之和上限。</summary>
        /// <remarks>The bound is checked before the first prepare callback; it limits source-side preprocessing work and is independent of model tensor dimensions. / 该上限在第一个准备回调之前检查，约束源图预处理工作量，与模型张量尺寸相互独立。</remarks>
        public long MaximumPreparedPixels { get; }
        /// <summary>Gets default options. / 获取默认选项。</summary>
        public static RoiExecutionOptions Default { get; } = new RoiExecutionOptions();
    }

    /// <summary>Contains one ROI inference item, including a per-ROI failure when partial execution is enabled. / 包含一个 ROI 推理项及部分执行时的逐 ROI 失败。</summary>
    public sealed class RoiInferenceItem
    {
        internal RoiInferenceItem(VisualRoi roi, IVisualRoiGeometry geometry, VisualInferenceResult? result, Exception? failure)
            : this(roi, geometry, null, result, failure)
        {
        }

        internal RoiInferenceItem(VisualRoi roi, IVisualRoiGeometry geometry, RoiProjection? projection, VisualInferenceResult? result, Exception? failure)
        {
            Roi = roi ?? throw new ArgumentNullException(nameof(roi));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            if (result == null && failure == null) throw new ArgumentException("An ROI item requires either a result or a failure.");
            if (result != null && failure != null) throw new ArgumentException("An ROI item cannot contain both a result and a failure.");
            Result = result;
            Failure = failure;
            Projection = projection;
        }

        /// <summary>Gets the immutable ROI definition. / 获取不可变 ROI 定义。</summary>
        public VisualRoi Roi { get; }
        /// <summary>Gets the resolved source-space geometry. / 获取解析后的源空间几何。</summary>
        public IVisualRoiGeometry Geometry { get; }
        /// <summary>Gets the reversible prepared-input projection, or null when preparation failed. / 获取已准备输入的可逆投影；准备失败时为空。</summary>
        public RoiProjection? Projection { get; }
        /// <summary>Gets the successful visual result, or null when failed. / 获取成功视觉结果，失败时为空。</summary>
        public VisualInferenceResult? Result { get; }
        /// <summary>Gets the captured failure, or null when successful. / 获取捕获失败，成功时为空。</summary>
        public Exception? Failure { get; }
        /// <summary>Gets whether the item succeeded. / 获取是否成功。</summary>
        public bool Succeeded => Result != null;
    }

    /// <summary>Contains all ROI crop-and-infer items for one immutable snapshot. / 包含一个不可变快照的全部 ROI 裁剪推理项。</summary>
    public sealed class RoiInferenceBatchResult
    {
        private readonly IReadOnlyList<RoiInferenceItem> _items;

        internal RoiInferenceBatchResult(VisualRoiSnapshot snapshot, IEnumerable<RoiInferenceItem> items, RoiResultMergeMode mergeMode, string? correlationId, RoiExecutionDiagnostics? diagnostics = null)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (items == null) throw new ArgumentNullException(nameof(items));
            _items = new ReadOnlyCollection<RoiInferenceItem>(items.ToList());
            MergeMode = mergeMode;
            CorrelationId = correlationId;
            Diagnostics = diagnostics ?? new RoiExecutionDiagnostics(RoiExecutionMode.CropAndInfer, _items.Count, 0, 0, 0, Succeeded.Count, Failed.Count, 0, TimeSpan.Zero, false, correlationId);
        }

        /// <summary>Gets the exact snapshot used by this call. / 获取本次调用使用的确切快照。</summary>
        public VisualRoiSnapshot Snapshot { get; }
        /// <summary>Gets the snapshot version. / 获取快照版本。</summary>
        public long SnapshotVersion => Snapshot.Version;
        /// <summary>Gets per-ROI items in deterministic snapshot order. / 获取按快照确定性顺序排列的逐 ROI 项。</summary>
        public IReadOnlyList<RoiInferenceItem> Items => _items;
        /// <summary>Gets successful items. / 获取成功项。</summary>
        public IReadOnlyList<RoiInferenceItem> Succeeded => _items.Where(value => value.Succeeded).ToList().AsReadOnly();
        /// <summary>Gets failed items. / 获取失败项。</summary>
        public IReadOnlyList<RoiInferenceItem> Failed => _items.Where(value => !value.Succeeded).ToList().AsReadOnly();
        /// <summary>Gets the requested merge mode. / 获取请求的合并模式。</summary>
        public RoiResultMergeMode MergeMode { get; }
        /// <summary>Gets the optional correlation identifier. / 获取可选关联标识。</summary>
        public string? CorrelationId { get; }
        /// <summary>Gets bounded execution counters and elapsed time. / 获取有界执行计数和耗时。</summary>
        public RoiExecutionDiagnostics Diagnostics { get; }

        /// <summary>Returns successful items as a strongly typed result collection. / 将成功项作为强类型结果集合返回。</summary>
        public IReadOnlyList<RoiTypedInferenceItem<TResult>> GetSuccessful<TResult>() where TResult : class
        {
            var typed = new List<RoiTypedInferenceItem<TResult>>();
            foreach (RoiInferenceItem item in _items)
            {
                if (!item.Succeeded) continue;
                if (item.Result!.Value is not TResult value) throw new VisualException(VisualErrorCodes.DecodeFailed, "An ROI result payload does not match the requested type.", technicalDetails: "roiId=" + item.Roi.Id + ";actual=" + item.Result.Value.GetType().FullName + ";requested=" + typeof(TResult).FullName);
                typed.Add(new RoiTypedInferenceItem<TResult>(item, value));
            }
            return new ReadOnlyCollection<RoiTypedInferenceItem<TResult>>(typed);
        }
    }

    /// <summary>Contains the raw ROI crop batch, projected candidates, and task-specific merged output. / 包含 ROI 裁剪批次、投影候选项及任务专用合并结果。</summary>
    /// <typeparam name="TResult">The decoded task result type. / 解码后的任务结果类型。</typeparam>
    public sealed class VisualRoiCropMergedResult<TResult> where TResult : class
    {
        internal VisualRoiCropMergedResult(RoiInferenceBatchResult batch, IReadOnlyList<RoiProjectedResult<TResult>> candidates, IReadOnlyList<TResult> merged)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            Merged = merged ?? throw new ArgumentNullException(nameof(merged));
        }

        /// <summary>Gets the complete per-ROI execution batch, including failures. / 获取包含失败项的完整逐 ROI 执行批次。</summary>
        public RoiInferenceBatchResult Batch { get; }
        /// <summary>Gets successful results after explicit model-space projection. / 获取显式模型空间投影后的成功结果。</summary>
        public IReadOnlyList<RoiProjectedResult<TResult>> Candidates { get; }
        /// <summary>Gets task-specific merged results. / 获取任务专用合并结果。</summary>
        public IReadOnlyList<TResult> Merged { get; }
        /// <summary>Gets the underlying ROI execution diagnostics. / 获取底层 ROI 执行诊断。</summary>
        public RoiExecutionDiagnostics Diagnostics => Batch.Diagnostics;
    }

    /// <summary>Contains one successful strongly typed ROI result and its execution metadata. / 包含一个成功的强类型 ROI 结果及其执行元数据。</summary>
    /// <typeparam name="TResult">The canonical decoded result type. / 规范解码结果类型。</typeparam>
    public sealed class RoiTypedInferenceItem<TResult> where TResult : class
    {
        internal RoiTypedInferenceItem(RoiInferenceItem item, TResult value)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets the untyped item including ROI, geometry, projection, and inference metadata. / 获取包含 ROI、几何、投影和推理元数据的非类型化项。</summary>
        public RoiInferenceItem Item { get; }
        /// <summary>Gets the ROI definition. / 获取 ROI 定义。</summary>
        public VisualRoi Roi => Item.Roi;
        /// <summary>Gets the resolved source-space geometry. / 获取解析后的源图空间几何。</summary>
        public IVisualRoiGeometry Geometry => Item.Geometry;
        /// <summary>Gets the reversible projection. / 获取可逆投影。</summary>
        public RoiProjection? Projection => Item.Projection;
        /// <summary>Gets the canonical typed result. / 获取规范强类型结果。</summary>
        public TResult Value { get; }
        /// <summary>Gets backend/model/timing metadata. / 获取后端、模型和耗时元数据。</summary>
        public VisualInferenceResult Inference => Item.Result!;
    }

    /// <summary>Runs crop-and-infer ROI work on an existing VisualPipeline. / 在现有 VisualPipeline 上运行 ROI 裁剪推理。</summary>
    public sealed class VisualRoiRunner
    {
        private readonly VisualPipeline _pipeline;

        /// <summary>Initializes an ROI runner. / 初始化 ROI 运行器。</summary>
        public VisualRoiRunner(VisualPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        /// <summary>Runs enabled ROIs by preparing each ROI through the supplied source adapter. / 使用调用方输入适配器准备每个 ROI 并执行推理。</summary>
        public async Task<RoiInferenceBatchResult> RunCropAsync(
            VisualRoiSnapshot snapshot,
            Func<VisualRoi, IVisualRoiGeometry, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            RoiExecutionOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            RoiExecutionOptions effective = options ?? RoiExecutionOptions.Default;
            ValidateCoordinateContext(snapshot, coordinateContext);
            IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> selected = ResolveSelected(snapshot, effective.MaximumRois, coordinateContext, _pipeline.Selection.Profile.Task);
            if (selected.Count == 0)
            {
                return new RoiInferenceBatchResult(snapshot, Array.Empty<RoiInferenceItem>(), effective.MergeMode, effective.CorrelationId,
                    new RoiExecutionDiagnostics(RoiExecutionMode.CropAndInfer, 0, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, false, effective.CorrelationId));
            }
            long preparedPixelCount = EnsurePreparedPixelBudget(selected, effective.MaximumPreparedPixels);

            var items = new RoiInferenceItem[selected.Count];
            Exception? deferredFailure = null;
            int preparedInputCount = 0;
            int inferenceCallCount = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            int chunkSize = Math.Max(1, _pipeline.MaximumConcurrency * Math.Max(1, effective.Prefetch));
            for (int offset = 0; offset < selected.Count; offset += chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(chunkSize, selected.Count - offset);
                var prepared = new PreparedVisualInput[count];
                try
                {
                    var tasks = new Task<PreparedVisualInput>[count];
                    for (int index = 0; index < count; index++)
                    {
                        int captured = index;
                        (VisualRoi Roi, IVisualRoiGeometry Geometry) entry = selected[offset + captured];
                        tasks[captured] = Task.Factory.StartNew(
                            () => prepareAsync(entry.Roi, entry.Geometry, cancellationToken),
                            CancellationToken.None,
                            TaskCreationOptions.DenyChildAttach,
                            TaskScheduler.Default).Unwrap();
                    }

                    try
                    {
                        prepared = await Task.WhenAll(tasks).ConfigureAwait(false);
                        preparedInputCount += prepared.Count(value => value != null);
                        if (effective.FailureMode == RoiFailureMode.ReturnPartialResults)
                        {
                            await RunPartialAsync(prepared, selected, offset, snapshot, items, effective, cancellationToken, coordinateContext, () => inferenceCallCount++).ConfigureAwait(false);
                        }
                        else
                        {
                            RoiProjection[] projections = ValidatePreparedInputs(prepared, selected, offset, snapshot, coordinateContext);
                            IReadOnlyList<VisualInferenceResult> results = await _pipeline.RunManyAsync(
                                prepared,
                                new VisualExecutionOptions(effective.Timeout, disposeOwnedInputOnCompletion: false, effective.CorrelationId),
                                cancellationToken).ConfigureAwait(false);
                            inferenceCallCount++;
                            for (int index = 0; index < count; index++)
                            {
                                IVisualRoiGeometry geometry = ResolvePreparedGeometry(snapshot, selected[offset + index].Roi, prepared[index], coordinateContext);
                                items[offset + index] = new RoiInferenceItem(selected[offset + index].Roi, geometry, projections[index], results[index], null);
                            }
                        }
                    }
                    catch when (effective.FailureMode == RoiFailureMode.ReturnPartialResults && !cancellationToken.IsCancellationRequested)
                    {
                        // A preparation failure must not discard successfully prepared inputs.
                        // 预处理失败时不能丢弃已经成功准备的输入。
                        for (int index = 0; index < count; index++)
                        {
                            try { prepared[index] = await tasks[index].ConfigureAwait(false); }
                            catch (Exception preparationException) { items[offset + index] = new RoiInferenceItem(selected[offset + index].Roi, selected[offset + index].Geometry, null, preparationException); }
                        }
                        preparedInputCount += prepared.Count(value => value != null);
                        await RunPartialAsync(prepared, selected, offset, snapshot, items, effective, cancellationToken, coordinateContext, () => inferenceCallCount++).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        // Task.WhenAll has completed at this point, but its exception prevents
                        // assignment of the successful results to the prepared array. Capture
                        // every successful owned input before surfacing the failure so the
                        // finally block can release it deterministically.
                        for (int index = 0; index < count; index++)
                        {
                            try { prepared[index] = await tasks[index].ConfigureAwait(false); }
                            catch { }
                        }
                        if (effective.FailureMode == RoiFailureMode.CompleteThenFail && !cancellationToken.IsCancellationRequested)
                        {
                            deferredFailure ??= exception;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                finally
                {
                    for (int index = 0; index < prepared.Length; index++) if (prepared[index] != null && prepared[index].Ownership == PreparedInputOwnership.Owned && !prepared[index].IsDisposed) prepared[index].Dispose();
                }
            }
            if (deferredFailure != null) throw deferredFailure;
            stopwatch.Stop();
            int succeededResultCount = items.Count(value => value != null && value.Succeeded);
            return new RoiInferenceBatchResult(snapshot, items, effective.MergeMode, effective.CorrelationId,
                new RoiExecutionDiagnostics(RoiExecutionMode.CropAndInfer, selected.Count, 0, preparedInputCount, inferenceCallCount, succeededResultCount, selected.Count - succeededResultCount, preparedPixelCount, stopwatch.Elapsed, false, effective.CorrelationId));
        }

        /// <summary>Prepares all selected CropAndInfer ROIs as one true model batch and splits the decoded batch back to ROI rows. / 将所有选中的 CropAndInfer ROI 准备为一个真正模型 Batch，再将解码后的 Batch 拆回 ROI 行。</summary>
        /// <remarks>The preparation callback must preserve the supplied ROI order in <see cref="PreparedVisualInput.BatchFrames"/>. The row selector is responsible for extracting one canonical result from the decoder's batch payload; <see cref="VisualRoiBatchResultSelectors.SelectKnownBatchRow"/> covers the built-in batch result types. / 预处理回调必须按传入 ROI 顺序保留 <see cref="PreparedVisualInput.BatchFrames"/>；行选择器负责从解码器 Batch 载荷中取出一行规范结果；内置 Batch 结果可使用 <see cref="VisualRoiBatchResultSelectors.SelectKnownBatchRow"/>。</remarks>
        public async Task<RoiInferenceBatchResult> RunCropBatchAsync(
            VisualRoiSnapshot snapshot,
            Func<IReadOnlyList<VisualRoi>, IReadOnlyList<IVisualRoiGeometry>, CancellationToken, Task<PreparedVisualInput>> prepareBatchAsync,
            Func<VisualInferenceResult, int, VisualInferenceResult> selectRow,
            RoiExecutionOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (prepareBatchAsync == null) throw new ArgumentNullException(nameof(prepareBatchAsync));
            if (selectRow == null) throw new ArgumentNullException(nameof(selectRow));
            RoiExecutionOptions effective = options ?? RoiExecutionOptions.Default;
            ValidateCoordinateContext(snapshot, coordinateContext);
            IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> selected = ResolveSelected(snapshot, effective.MaximumRois, coordinateContext, _pipeline.Selection.Profile.Task);
            if (selected.Count == 0)
            {
                return new RoiInferenceBatchResult(snapshot, Array.Empty<RoiInferenceItem>(), effective.MergeMode, effective.CorrelationId,
                    new RoiExecutionDiagnostics(RoiExecutionMode.CropAndInfer, 0, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, true, effective.CorrelationId));
            }
            if (selected.Count > effective.MaximumBatchSize) throw new VisualException(VisualErrorCodes.InputInvalid, "The selected ROI count exceeds the configured true-batch limit.", technicalDetails: "count=" + selected.Count + ";maximumBatchSize=" + effective.MaximumBatchSize);
            long preparedPixelCount = EnsurePreparedPixelBudget(selected, effective.MaximumPreparedPixels);
            Stopwatch stopwatch = Stopwatch.StartNew();

            var rois = new ReadOnlyCollection<VisualRoi>(selected.Select(value => value.Roi).ToList());
            var geometries = new ReadOnlyCollection<IVisualRoiGeometry>(selected.Select(value => value.Geometry).ToList());
            PreparedVisualInput? prepared = null;
            var items = new RoiInferenceItem[selected.Count];
            try
            {
                try
                {
                    prepared = await prepareBatchAsync(rois, geometries, cancellationToken).ConfigureAwait(false);
                    ValidatePreparedBatch(prepared, selected, snapshot.SourceSize);
                    VisualInferenceResult batchResult = await _pipeline.RunAsync(
                        prepared,
                        new VisualExecutionOptions(effective.Timeout, disposeOwnedInputOnCompletion: false, effective.CorrelationId),
                        cancellationToken).ConfigureAwait(false);
                    for (int index = 0; index < selected.Count; index++)
                    {
                        try
                        {
                            VisualInferenceResult row = selectRow(batchResult, index) ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The ROI batch row selector returned null.", technicalDetails: "batchIndex=" + index);
                            RoiProjection projection = CreateProjection(prepared, selected[index].Roi, coordinateContext, index);
                            IVisualRoiGeometry geometry = ResolvePreparedGeometry(snapshot, selected[index].Roi, prepared, coordinateContext, index);
                            items[index] = new RoiInferenceItem(selected[index].Roi, geometry, projection, row, null);
                        }
                        catch (Exception exception) when (effective.FailureMode == RoiFailureMode.ReturnPartialResults && !cancellationToken.IsCancellationRequested)
                        {
                            items[index] = new RoiInferenceItem(selected[index].Roi, selected[index].Geometry, null, exception);
                        }
                    }
                }
                catch (Exception exception) when (effective.FailureMode == RoiFailureMode.ReturnPartialResults && !cancellationToken.IsCancellationRequested)
                {
                    for (int index = 0; index < items.Length; index++) if (items[index] == null) items[index] = new RoiInferenceItem(selected[index].Roi, selected[index].Geometry, null, exception);
                }
                stopwatch.Stop();
                int succeededResultCount = items.Count(value => value != null && value.Succeeded);
                return new RoiInferenceBatchResult(snapshot, items, effective.MergeMode, effective.CorrelationId,
                    new RoiExecutionDiagnostics(RoiExecutionMode.CropAndInfer, selected.Count, 0, prepared == null ? 0 : 1, prepared == null ? 0 : 1, succeededResultCount, selected.Count - succeededResultCount, preparedPixelCount, stopwatch.Elapsed, true, effective.CorrelationId));
            }
            finally
            {
                if (prepared?.Ownership == PreparedInputOwnership.Owned && !prepared.IsDisposed) prepared.Dispose();
            }
        }

        /// <summary>Runs one true ROI batch, decodes each selected row, projects it to source coordinates, and applies a task-specific merger. / 执行一次真正的 ROI Batch，逐行解码、投影到源图并应用任务专用合并器。</summary>
        /// <remarks>The batch selector must return one canonical row result for each ROI. Use <see cref="VisualRoiBatchResultSelectors.SelectKnownBatchRow"/> for built-in batch payloads or provide a selector for a custom decoder. / Batch 行选择器必须为每个 ROI 返回一个规范行结果；内置 Batch 载荷可使用 <see cref="VisualRoiBatchResultSelectors.SelectKnownBatchRow"/>，自定义 decoder 请提供自己的选择器。</remarks>
        public async Task<VisualRoiCropMergedResult<TResult>> RunCropBatchAndMergeAsync<TResult>(
            VisualRoiSnapshot snapshot,
            Func<IReadOnlyList<VisualRoi>, IReadOnlyList<IVisualRoiGeometry>, CancellationToken, Task<PreparedVisualInput>> prepareBatchAsync,
            Func<VisualInferenceResult, int, VisualInferenceResult> selectRow,
            Func<VisualInferenceResult, TResult> decode,
            IRoiResultProjector<TResult> projector,
            IRoiResultMerger<TResult> merger,
            RoiExecutionOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null) where TResult : class
        {
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            if (projector == null) throw new ArgumentNullException(nameof(projector));
            if (merger == null) throw new ArgumentNullException(nameof(merger));
            RoiInferenceBatchResult batch = await RunCropBatchAsync(
                snapshot,
                prepareBatchAsync,
                selectRow,
                options,
                cancellationToken,
                coordinateContext).ConfigureAwait(false);
            var candidates = new List<RoiProjectedResult<TResult>>(batch.Succeeded.Count);
            foreach (RoiInferenceItem item in batch.Succeeded)
            {
                if (item.Projection == null) throw new VisualException(VisualErrorCodes.DecodeFailed, "A successful ROI batch item has no reversible projection.", technicalDetails: "roiId=" + item.Roi.Id);
                TResult decoded = decode(item.Result!) ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The ROI batch decode callback returned null.", technicalDetails: "roiId=" + item.Roi.Id);
                TResult projected = projector.Project(decoded, item.Projection) ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The ROI batch projector returned null.", technicalDetails: "roiId=" + item.Roi.Id);
                candidates.Add(new RoiProjectedResult<TResult>(item.Roi.Id, item.Roi.Priority, projected));
            }
            IReadOnlyList<TResult> merged = merger.Merge(candidates, options?.MergeMode ?? batch.MergeMode);
            IReadOnlyList<RoiProjectedResult<TResult>> retainedCandidates = options?.PreserveIndividualResults == false
                ? Array.Empty<RoiProjectedResult<TResult>>()
                : new ReadOnlyCollection<RoiProjectedResult<TResult>>(candidates);
            return new VisualRoiCropMergedResult<TResult>(batch, retainedCandidates, merged);
        }

        /// <summary>Runs CropAndInfer ROIs, decodes, projects model-space results, and applies a task-specific merger. / 执行 CropAndInfer ROI、解码、投影模型空间结果并应用任务专用合并器。</summary>
        /// <remarks>The complete batch is retained so preparation or inference failures remain observable. The projector is called only for successful items with a concrete reversible projection. / 保留完整批次，因此预处理或推理失败仍可观测；仅对具有具体可逆投影的成功项调用投影器。</remarks>
        public async Task<VisualRoiCropMergedResult<TResult>> RunCropAndMergeAsync<TResult>(
            VisualRoiSnapshot snapshot,
            Func<VisualRoi, IVisualRoiGeometry, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, TResult> decode,
            IRoiResultProjector<TResult> projector,
            IRoiResultMerger<TResult> merger,
            RoiExecutionOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null) where TResult : class
        {
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            if (projector == null) throw new ArgumentNullException(nameof(projector));
            if (merger == null) throw new ArgumentNullException(nameof(merger));
            RoiInferenceBatchResult batch = await RunCropAsync(snapshot, prepareAsync, options, cancellationToken, coordinateContext).ConfigureAwait(false);
            var candidates = new List<RoiProjectedResult<TResult>>(batch.Succeeded.Count);
            foreach (RoiInferenceItem item in batch.Succeeded)
            {
                if (item.Projection == null) throw new VisualException(VisualErrorCodes.DecodeFailed, "A successful ROI item has no reversible projection.", technicalDetails: "roiId=" + item.Roi.Id);
                TResult decoded = decode(item.Result!) ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The ROI decode callback returned null.", technicalDetails: "roiId=" + item.Roi.Id);
                TResult projected = projector.Project(decoded, item.Projection) ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The ROI projector returned null.", technicalDetails: "roiId=" + item.Roi.Id);
                candidates.Add(new RoiProjectedResult<TResult>(item.Roi.Id, item.Roi.Priority, projected));
            }
            IReadOnlyList<TResult> merged = merger.Merge(candidates, options?.MergeMode ?? batch.MergeMode);
            IReadOnlyList<RoiProjectedResult<TResult>> retainedCandidates = options?.PreserveIndividualResults == false
                ? Array.Empty<RoiProjectedResult<TResult>>()
                : new ReadOnlyCollection<RoiProjectedResult<TResult>>(candidates);
            return new VisualRoiCropMergedResult<TResult>(batch, retainedCandidates, merged);
        }

        private async Task RunPartialAsync(
            IReadOnlyList<PreparedVisualInput> prepared,
            IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> selected,
            int offset,
            VisualRoiSnapshot snapshot,
            RoiInferenceItem[] items,
            RoiExecutionOptions options,
            CancellationToken cancellationToken,
            VisualRoiCoordinateContext? coordinateContext,
            Action inferenceCallStarted)
        {
            if (inferenceCallStarted == null) throw new ArgumentNullException(nameof(inferenceCallStarted));
            var runs = new Task<PartialRun>[prepared.Count];
            for (int index = 0; index < prepared.Count; index++)
            {
                int captured = index;
                runs[captured] = prepared[captured] == null
                    ? Task.FromResult(new PartialRun(null, new InvalidOperationException("ROI preparation returned no input.")))
                    : RunOnePartialAsync(prepared[captured], selected[offset + captured].Roi, snapshot, options, cancellationToken, coordinateContext, inferenceCallStarted);
            }

            PartialRun[] completed = await Task.WhenAll(runs).ConfigureAwait(false);
            for (int index = 0; index < completed.Length; index++)
            {
                PartialRun run = completed[index];
                items[offset + index] = run.Result != null
                    ? new RoiInferenceItem(selected[offset + index].Roi, ResolvePreparedGeometry(snapshot, selected[offset + index].Roi, prepared[index], coordinateContext), run.Projection, run.Result, null)
                    : new RoiInferenceItem(selected[offset + index].Roi, selected[offset + index].Geometry, null, run.Failure!);
            }
        }

        private async Task<PartialRun> RunOnePartialAsync(PreparedVisualInput input, VisualRoi roi, VisualRoiSnapshot snapshot, RoiExecutionOptions options, CancellationToken cancellationToken, VisualRoiCoordinateContext? coordinateContext, Action inferenceCallStarted)
        {
            try
            {
                ValidatePreparedSource(input, snapshot.SourceSize);
                RoiProjection projection = CreateProjection(input, roi, coordinateContext);
                inferenceCallStarted();
                VisualInferenceResult result = await _pipeline.RunAsync(
                    input,
                    new VisualExecutionOptions(options.Timeout, disposeOwnedInputOnCompletion: false, options.CorrelationId),
                    cancellationToken).ConfigureAwait(false);
                return new PartialRun(result, null, projection);
            }
            catch (Exception exception)
            {
                return new PartialRun(null, exception);
            }
        }

        private sealed class PartialRun
        {
            internal PartialRun(VisualInferenceResult? result, Exception? failure, RoiProjection? projection = null)
            {
                Result = result;
                Failure = failure;
                Projection = projection;
            }

            internal VisualInferenceResult? Result { get; }
            internal Exception? Failure { get; }
            internal RoiProjection? Projection { get; }
        }

        private static RoiProjection[] ValidatePreparedInputs(IReadOnlyList<PreparedVisualInput> prepared, IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> selected, int offset, VisualRoiSnapshot snapshot, VisualRoiCoordinateContext? coordinateContext)
        {
            var projections = new RoiProjection[prepared.Count];
            for (int index = 0; index < prepared.Count; index++)
            {
                ValidatePreparedSource(prepared[index], snapshot.SourceSize);
                projections[index] = CreateProjection(prepared[index], selected[offset + index].Roi, coordinateContext);
            }
            return projections;
        }

        private static IVisualRoiGeometry ResolvePreparedGeometry(VisualRoiSnapshot snapshot, VisualRoi roi, PreparedVisualInput input, VisualRoiCoordinateContext? coordinateContext, int batchIndex = 0)
        {
            if (roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World)
            {
                if (coordinateContext == null) throw new InvalidOperationException("TileLocal and World ROI coordinates require an explicit projection context.");
                return snapshot.Resolve(roi, coordinateContext);
            }
            return snapshot.Resolve(roi, input.BatchFrames[batchIndex]);
        }

        private static RoiProjection CreateProjection(PreparedVisualInput input, VisualRoi roi, VisualRoiCoordinateContext? coordinateContext, int batchIndex = 0)
        {
            return roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World
                ? input.CreateRoiProjection(roi, coordinateContext ?? throw new InvalidOperationException("TileLocal and World ROI coordinates require an explicit projection context."), batchIndex)
                : input.CreateRoiProjection(roi, batchIndex);
        }

        private static void ValidatePreparedBatch(PreparedVisualInput input, IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> selected, VisualSize sourceSize)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.BatchSize != selected.Count) throw new VisualException(VisualErrorCodes.InputInvalid, "The prepared ROI batch size must equal the selected ROI count.", tensorName: input.InputName, technicalDetails: "expected=" + selected.Count + ";actual=" + input.BatchSize);
            if (input.BatchFrames.Count != selected.Count) throw new VisualException(VisualErrorCodes.InputInvalid, "The prepared ROI batch must expose one frame descriptor for every ROI row.", tensorName: input.InputName);
            for (int index = 0; index < input.BatchFrames.Count; index++)
            {
                if (input.BatchFrames[index].SourceSize != sourceSize) throw new VisualException(VisualErrorCodes.InputInvalid, "ROI prepared batch rows must retain the full source-image size and a reversible source-to-model transform.", tensorName: input.InputName, technicalDetails: "expected=" + sourceSize.Width + "x" + sourceSize.Height + ";actual=" + input.BatchFrames[index].SourceSize.Width + "x" + input.BatchFrames[index].SourceSize.Height + ";batchIndex=" + index);
            }
        }

        private static void ValidatePreparedSource(PreparedVisualInput input, VisualSize sourceSize)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.BatchSize != 1) throw new VisualException(VisualErrorCodes.InputInvalid, "CropAndInfer ROI execution requires one prepared image per ROI; use CreateRoiBatch and a true batch decoder when one input contains multiple ROI rows.", tensorName: input.InputName, technicalDetails: "batchSize=" + input.BatchSize);
            for (int index = 0; index < input.BatchFrames.Count; index++)
            {
                if (input.BatchFrames[index].SourceSize != sourceSize) throw new VisualException(VisualErrorCodes.InputInvalid, "ROI prepared inputs must retain the full source-image size and a reversible source-to-model transform.", tensorName: input.InputName, technicalDetails: "expected=" + sourceSize.Width + "x" + sourceSize.Height + ";actual=" + input.BatchFrames[index].SourceSize.Width + "x" + input.BatchFrames[index].SourceSize.Height + ";batchIndex=" + index);
            }
        }

        private static IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> ResolveSelected(VisualRoiSnapshot snapshot, int maximumRois, VisualRoiCoordinateContext? coordinateContext, VisualTaskId task)
        {
            var selected = new List<(VisualRoi Roi, IVisualRoiGeometry Geometry)>();
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || roi.InclusionMode != RoiInclusionMode.Include || roi.ExecutionMode != RoiExecutionMode.CropAndInfer) continue;
                if (!roi.AppliesTo(task)) continue;
                if (selected.Count >= maximumRois) throw new VisualException(VisualErrorCodes.InputInvalid, "The ROI count exceeds the configured execution limit.");
                if (roi.CoordinateSpace == RoiCoordinateSpace.ModelInput) throw new NotSupportedException("RunCropAsync requires SourcePixels, Normalized, TileLocal or World ROI coordinates; resolve ModelInput coordinates against a concrete VisualInputFrame before preparing a crop.");
                selected.Add((roi, ResolveGeometry(snapshot, roi, coordinateContext)));
            }
            return selected.AsReadOnly();
        }

        private static long EnsurePreparedPixelBudget(IReadOnlyList<(VisualRoi Roi, IVisualRoiGeometry Geometry)> selected, long maximumPreparedPixels)
        {
            long total = 0;
            for (int index = 0; index < selected.Count; index++)
            {
                RectangleF bounds = selected[index].Geometry.Bounds;
                long width = checked((long)Math.Ceiling(Math.Max(0, bounds.Width)));
                long height = checked((long)Math.Ceiling(Math.Max(0, bounds.Height)));
                total = checked(total + checked(width * height));
            }
            if (total > maximumPreparedPixels) throw new VisualException(VisualErrorCodes.InputInvalid, "ROI preparation exceeds the configured pixel budget.", technicalDetails: "pixels=" + total + ";limit=" + maximumPreparedPixels);
            return total;
        }

        private static IVisualRoiGeometry ResolveGeometry(VisualRoiSnapshot snapshot, VisualRoi roi, VisualRoiCoordinateContext? coordinateContext)
        {
            if (roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World)
            {
                if (coordinateContext == null) throw new NotSupportedException("TileLocal and World ROI coordinates require an explicit projection context.");
                return snapshot.Resolve(roi, coordinateContext);
            }
            return snapshot.Resolve(roi);
        }

        private static void ValidateCoordinateContext(VisualRoiSnapshot snapshot, VisualRoiCoordinateContext? coordinateContext)
        {
            if (coordinateContext != null && coordinateContext.SourceSize != snapshot.SourceSize) throw new ArgumentException("The ROI coordinate context source size must match the snapshot source size.", nameof(coordinateContext));
        }
    }
}
