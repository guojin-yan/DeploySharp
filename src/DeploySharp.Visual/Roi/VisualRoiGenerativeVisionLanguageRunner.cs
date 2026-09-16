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
    /// <summary>Contains one VLM/VQA ROI generation or its isolated failure. / 包含一个 VLM/VQA ROI 生成结果或其隔离失败。</summary>
    public sealed class VisualRoiGenerativeVisionLanguageResultItem
    {
        internal VisualRoiGenerativeVisionLanguageResultItem(VisualRoi roi, IVisualRoiGeometry geometry, GenerativeVisionLanguageResult? result, Exception? failure)
        {
            Roi = roi ?? throw new ArgumentNullException(nameof(roi));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            if (result == null && failure == null) throw new ArgumentException("A VLM ROI item requires either a result or a failure.");
            if (result != null && failure != null) throw new ArgumentException("A VLM ROI item cannot contain both a result and a failure.");
            Result = result;
            Failure = failure;
        }

        /// <summary>Gets the immutable ROI definition. / 获取不可变 ROI 定义。</summary>
        public VisualRoi Roi { get; }
        /// <summary>Gets the resolved source-space geometry. / 获取解析后的源图空间几何。</summary>
        public IVisualRoiGeometry Geometry { get; }
        /// <summary>Gets the successful generated result. / 获取成功的生成结果。</summary>
        public GenerativeVisionLanguageResult? Result { get; }
        /// <summary>Gets the isolated preparation, encoder, or decoder failure. / 获取隔离的预处理、编码器或解码器失败。</summary>
        public Exception? Failure { get; }
        /// <summary>Gets whether this ROI completed successfully. / 获取此 ROI 是否成功完成。</summary>
        public bool Succeeded => Result != null;
    }

    /// <summary>Contains deterministic VLM/VQA results for one immutable ROI snapshot. / 包含一个不可变 ROI 快照的确定性 VLM/VQA 结果。</summary>
    public sealed class VisualRoiGenerativeVisionLanguageBatchResult
    {
        private readonly IReadOnlyList<VisualRoiGenerativeVisionLanguageResultItem> _items;

        internal VisualRoiGenerativeVisionLanguageBatchResult(
            VisualRoiSnapshot snapshot,
            GenerativeVisionLanguageRequest request,
            IEnumerable<VisualRoiGenerativeVisionLanguageResultItem> items,
            string? correlationId,
            TimeSpan elapsed,
            int inferenceCallCount,
            long preparedPixelCount)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            if (items == null) throw new ArgumentNullException(nameof(items));
            _items = new ReadOnlyCollection<VisualRoiGenerativeVisionLanguageResultItem>(items.ToList());
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
            Elapsed = elapsed;
            if (inferenceCallCount < 0 || inferenceCallCount > _items.Count) throw new ArgumentOutOfRangeException(nameof(inferenceCallCount));
            if (preparedPixelCount < 0) throw new ArgumentOutOfRangeException(nameof(preparedPixelCount));
            InferenceCallCount = inferenceCallCount;
            PreparedPixelCount = preparedPixelCount;
        }

        /// <summary>Gets the exact snapshot used by the operation. / 获取本次操作使用的确切快照。</summary>
        public VisualRoiSnapshot Snapshot { get; }
        /// <summary>Gets the common caption, VQA, or instruction request. / 获取公共 Caption、VQA 或指令请求。</summary>
        public GenerativeVisionLanguageRequest Request { get; }
        /// <summary>Gets the snapshot version. / 获取快照版本。</summary>
        public long SnapshotVersion => Snapshot.Version;
        /// <summary>Gets results in deterministic snapshot order. / 获取按快照确定性顺序排列的结果。</summary>
        public IReadOnlyList<VisualRoiGenerativeVisionLanguageResultItem> Items => _items;
        /// <summary>Gets successful ROI results. / 获取成功 ROI 结果。</summary>
        public IReadOnlyList<VisualRoiGenerativeVisionLanguageResultItem> Succeeded => _items.Where(value => value.Succeeded).ToList().AsReadOnly();
        /// <summary>Gets failed ROI results. / 获取失败 ROI 结果。</summary>
        public IReadOnlyList<VisualRoiGenerativeVisionLanguageResultItem> Failed => _items.Where(value => !value.Succeeded).ToList().AsReadOnly();
        /// <summary>Gets the number of selected ROIs. / 获取选中的 ROI 数量。</summary>
        public int SelectedRoiCount => _items.Count;
        /// <summary>Gets the number of successful results. / 获取成功结果数。</summary>
        public int SucceededResultCount => Succeeded.Count;
        /// <summary>Gets the number of failed results. / 获取失败结果数。</summary>
        public int FailedResultCount => Failed.Count;
        /// <summary>Gets logical per-ROI encoder/generation calls; decoder token steps are not counted separately. / 获取逐 ROI 逻辑编码/生成调用数；Decoder Token Step 不单独计数。</summary>
        public int InferenceCallCount { get; }
        /// <summary>Gets the sum of source ROI bounding-box pixels admitted by the budget. / 获取纳入预算的源 ROI 轴对齐边界像素总数。</summary>
        public long PreparedPixelCount { get; }
        /// <summary>Gets elapsed wall-clock time. / 获取墙钟耗时。</summary>
        public TimeSpan Elapsed { get; }
        /// <summary>Gets the optional correlation identifier. / 获取可选关联标识。</summary>
        public string? CorrelationId { get; }
    }

    /// <summary>Executes VLM/VQA requests on cropped ROIs using one stateful generation session. / 使用一个有状态生成 Session 在裁剪 ROI 上执行 VLM/VQA 请求。</summary>
    /// <remarks>
    /// A generative session caches exactly one image and rejects concurrent mutation. This runner therefore uses the industrially safe strategy of one crop, one image encode, and one generation sequence in snapshot order. It never pretends that text can be filtered from a full-image result. / 生成 Session 只缓存一张图且拒绝并发修改，因此本运行器采用工业上安全的“一 ROI 一裁剪、一图像编码、一生成序列”策略并按快照顺序执行；不会假装可以从整图文本结果中反向过滤 ROI。
    /// </remarks>
    public sealed class VisualRoiGenerativeVisionLanguageRunner
    {
        /// <summary>Runs a common Caption/VQA/instruction request for all applicable CropAndInfer ROIs. / 对所有适用的 CropAndInfer ROI 执行公共 Caption/VQA/指令请求。</summary>
        /// <param name="session">The stateful generative session. / 有状态生成 Session。</param>
        /// <param name="tokenizer">The profile-bound tokenizer. / Profile 绑定的 Tokenizer。</param>
        /// <param name="snapshot">The immutable ROI snapshot. / 不可变 ROI 快照。</param>
        /// <param name="request">The common generation request. / 公共生成请求。</param>
        /// <param name="prepareAsync">Prepares one crop while retaining the full source size and reversible geometry metadata. / 准备一个裁剪输入，同时保留完整源图尺寸和可逆几何元数据。</param>
        /// <param name="options">Bounded execution and failure options. / 有界执行和失败选项。</param>
        /// <param name="cancellationToken">Cancellation token. / 取消令牌。</param>
        /// <param name="coordinateContext">Optional TileLocal/World projection context. / 可选 TileLocal/World 投影上下文。</param>
        public async Task<VisualRoiGenerativeVisionLanguageBatchResult> RunAsync(
            GenerativeVisionLanguageSession session,
            IGenerativeVisionLanguageTokenizer tokenizer,
            VisualRoiSnapshot snapshot,
            GenerativeVisionLanguageRequest request,
            Func<VisualRoi, IVisualRoiGeometry, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            RoiExecutionOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (session.Bundle.Profile.Task != request.Task)
            {
                throw new VisualException(
                    VisualErrorCodes.GenerativeVisionLanguageContractInvalid,
                    "The ROI generation request task differs from the stateful session profile.",
                    profileId: session.Bundle.Profile.ProfileId,
                    technicalDetails: "profileTask=" + session.Bundle.Profile.Task + ";requestTask=" + request.Task);
            }
            RoiExecutionOptions effective = options ?? RoiExecutionOptions.Default;
            ValidateCoordinateContext(snapshot, coordinateContext);
            VisualTaskId task = ToVisualTask(request.Task);
            var selected = new List<(VisualRoi Roi, IVisualRoiGeometry Geometry)>();
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || roi.InclusionMode != RoiInclusionMode.Include || !roi.AppliesTo(task)) continue;
                if (roi.ExecutionMode != RoiExecutionMode.CropAndInfer) throw new VisualException(VisualErrorCodes.InputInvalid, "VLM/VQA ROI execution requires CropAndInfer; text results cannot be spatially filtered or merged from a full-image call.", technicalDetails: "roiId=" + roi.Id);
                if (roi.ClassFilter.Count != 0) throw new VisualException(VisualErrorCodes.InputInvalid, "VLM/VQA ROIs cannot use class filters.", technicalDetails: "roiId=" + roi.Id);
                if (roi.CoordinateSpace == RoiCoordinateSpace.ModelInput) throw new NotSupportedException("VLM/VQA ROI preparation requires SourcePixels, Normalized, TileLocal, or World coordinates; resolve ModelInput coordinates against a concrete frame first.");
                selected.Add((roi, ResolveGeometry(snapshot, roi, coordinateContext)));
                if (selected.Count > effective.MaximumRois) throw new VisualException(VisualErrorCodes.InputInvalid, "The ROI count exceeds the configured execution limit.", technicalDetails: "maximumRois=" + effective.MaximumRois);
            }

            long preparedPixelCount = EnsurePreparedPixelBudget(selected, effective.MaximumPreparedPixels);
            if (selected.Count == 0) return new VisualRoiGenerativeVisionLanguageBatchResult(snapshot, request, Array.Empty<VisualRoiGenerativeVisionLanguageResultItem>(), effective.CorrelationId, TimeSpan.Zero, 0, 0);

            var items = new List<VisualRoiGenerativeVisionLanguageResultItem>(selected.Count);
            Exception? deferredFailure = null;
            int inferenceCallCount = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            CancellationTokenSource? timeoutSource = null;
            CancellationTokenSource? linkedSource = null;
            CancellationToken operationToken = cancellationToken;
            try
            {
                if (effective.Timeout.HasValue) timeoutSource = new CancellationTokenSource(effective.Timeout.Value);
                if (timeoutSource != null && cancellationToken.CanBeCanceled)
                {
                    linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
                    operationToken = linkedSource.Token;
                }
                else if (timeoutSource != null)
                {
                    operationToken = timeoutSource.Token;
                }

                foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in selected)
                {
                    operationToken.ThrowIfCancellationRequested();
                    PreparedVisualInput? prepared = null;
                    bool handedToSession = false;
                    try
                    {
                        prepared = await prepareAsync(entry.Roi, entry.Geometry, operationToken).ConfigureAwait(false);
                        ValidatePreparedInput(prepared, snapshot.SourceSize, session);
                        handedToSession = true;
                        inferenceCallCount++;
                        await session.SetImageAsync(
                            prepared,
                            new VisualExecutionOptions(timeout: null, disposeOwnedInputOnCompletion: true, correlationId: effective.CorrelationId),
                            operationToken).ConfigureAwait(false);
                        GenerativeVisionLanguageResult result = await session.GenerateAsync(
                            request,
                            tokenizer,
                            options: new VisualExecutionOptions(timeout: null, disposeOwnedInputOnCompletion: false, correlationId: effective.CorrelationId),
                            cancellationToken: operationToken).ConfigureAwait(false);
                        items.Add(new VisualRoiGenerativeVisionLanguageResultItem(entry.Roi, entry.Geometry, result, null));
                    }
                    catch (Exception exception) when (effective.FailureMode != RoiFailureMode.FailFast && !operationToken.IsCancellationRequested)
                    {
                        items.Add(new VisualRoiGenerativeVisionLanguageResultItem(entry.Roi, entry.Geometry, null, exception));
                        if (effective.FailureMode == RoiFailureMode.CompleteThenFail) deferredFailure ??= exception;
                    }
                    finally
                    {
                        if (!handedToSession && prepared != null && prepared.Ownership == PreparedInputOwnership.Owned && !prepared.IsDisposed) prepared.Dispose();
                    }
                }
                stopwatch.Stop();
                if (deferredFailure != null) throw deferredFailure;
                return new VisualRoiGenerativeVisionLanguageBatchResult(snapshot, request, items, effective.CorrelationId, stopwatch.Elapsed, inferenceCallCount, preparedPixelCount);
            }
            finally
            {
                stopwatch.Stop();
                linkedSource?.Dispose();
                timeoutSource?.Dispose();
            }
        }

        private static VisualTaskId ToVisualTask(GenerativeVisionLanguageTask task)
        {
            switch (task)
            {
                case GenerativeVisionLanguageTask.ImageCaptioning: return VisualTaskId.ImageCaptioning;
                case GenerativeVisionLanguageTask.VisualQuestionAnswering: return VisualTaskId.VisualQuestionAnswering;
                case GenerativeVisionLanguageTask.ConditionalTextGeneration: return VisualTaskId.ConditionalTextGeneration;
                default: throw new ArgumentOutOfRangeException(nameof(task));
            }
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

        private static void ValidatePreparedInput(PreparedVisualInput input, VisualSize sourceSize, GenerativeVisionLanguageSession session)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.IsDisposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The ROI preparation callback returned a disposed input.", tensorName: input.InputName);
            if (input.BatchSize != 1 || input.BatchFrames.Count != 1) throw new VisualException(VisualErrorCodes.InputInvalid, "VLM/VQA CropAndInfer requires one prepared image per ROI.", tensorName: input.InputName);
            if (input.SourceSize != sourceSize || input.BatchFrames[0].SourceSize != sourceSize) throw new VisualException(VisualErrorCodes.InputInvalid, "VLM/VQA ROI inputs must retain the full source-image size for provenance.", tensorName: input.InputName);
            if (input.InputId == null) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "VLM/VQA ROI preparation requires a stable encoded-source InputId.", tensorName: input.InputName);
            if (input.ModelSize != session.Bundle.Profile.Processor.ImageSize) throw new VisualException(VisualErrorCodes.InputInvalid, "The prepared ROI model size differs from the selected VLM processor contract.", tensorName: input.InputName);
        }
    }
}
