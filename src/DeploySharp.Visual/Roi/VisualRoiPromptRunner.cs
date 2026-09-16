using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains one promptable-segmentation ROI result or its isolated failure. / 包含一个提示式分割 ROI 结果或其隔离失败。</summary>
    public sealed class VisualRoiPromptResultItem
    {
        internal VisualRoiPromptResultItem(VisualRoi roi, PromptableSegmentationPrompt? prompt, PromptableSegmentationResult? result, Exception? failure)
        {
            Roi = roi ?? throw new ArgumentNullException(nameof(roi));
            if (result == null && failure == null) throw new ArgumentException("A prompt ROI item requires either a result or a failure.");
            if (result != null && failure != null) throw new ArgumentException("A prompt ROI item cannot contain both a result and a failure.");
            Prompt = prompt;
            Result = result;
            Failure = failure;
        }

        /// <summary>Gets the immutable ROI definition. / 获取不可变 ROI 定义。</summary>
        public VisualRoi Roi { get; }
        /// <summary>Gets the generated prompt, or null when prompt construction failed. / 获取生成的 Prompt；构造失败时为空。</summary>
        public PromptableSegmentationPrompt? Prompt { get; }
        /// <summary>Gets the successful promptable result, or null when failed. / 获取成功的提示式分割结果；失败时为空。</summary>
        public PromptableSegmentationResult? Result { get; }
        /// <summary>Gets the isolated failure, or null when successful. / 获取隔离失败；成功时为空。</summary>
        public Exception? Failure { get; }
        /// <summary>Gets whether the prompt decode succeeded. / 获取 Prompt 解码是否成功。</summary>
        public bool Succeeded => Result != null;
    }

    /// <summary>Contains all promptable-segmentation results for one immutable ROI snapshot. / 包含一个不可变 ROI 快照的全部提示式分割结果。</summary>
    public sealed class VisualRoiPromptBatchResult
    {
        private readonly IReadOnlyList<VisualRoiPromptResultItem> _items;

        internal VisualRoiPromptBatchResult(VisualRoiSnapshot snapshot, IEnumerable<VisualRoiPromptResultItem> items, string? correlationId, TimeSpan elapsed, int inferenceCallCount)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (items == null) throw new ArgumentNullException(nameof(items));
            _items = new ReadOnlyCollection<VisualRoiPromptResultItem>(items.ToList());
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
            Elapsed = elapsed;
            if (inferenceCallCount < 0 || inferenceCallCount > _items.Count) throw new ArgumentOutOfRangeException(nameof(inferenceCallCount));
            InferenceCallCount = inferenceCallCount;
        }

        /// <summary>Gets the exact snapshot used by the operation. / 获取本次操作使用的确切快照。</summary>
        public VisualRoiSnapshot Snapshot { get; }
        /// <summary>Gets the snapshot version. / 获取快照版本。</summary>
        public long SnapshotVersion => Snapshot.Version;
        /// <summary>Gets prompt results in deterministic snapshot order. / 获取按快照确定性顺序排列的 Prompt 结果。</summary>
        public IReadOnlyList<VisualRoiPromptResultItem> Items => _items;
        /// <summary>Gets successful prompt results. / 获取成功的 Prompt 结果。</summary>
        public IReadOnlyList<VisualRoiPromptResultItem> Succeeded => _items.Where(value => value.Succeeded).ToList().AsReadOnly();
        /// <summary>Gets failed prompt results. / 获取失败的 Prompt 结果。</summary>
        public IReadOnlyList<VisualRoiPromptResultItem> Failed => _items.Where(value => !value.Succeeded).ToList().AsReadOnly();
        /// <summary>Gets the number of selected Include ROIs. / 获取选中的 Include ROI 数量。</summary>
        public int SelectedRoiCount => _items.Count;
        /// <summary>Gets the number of successful prompt decodes. / 获取成功 Prompt 解码数。</summary>
        public int SucceededResultCount => Succeeded.Count;
        /// <summary>Gets the number of failed prompt constructions or decodes. / 获取 Prompt 构造或解码失败数。</summary>
        public int FailedResultCount => Failed.Count;
        /// <summary>Gets the number of stateful decoder calls. / 获取有状态 Decoder 调用次数。</summary>
        public int InferenceCallCount { get; }
        /// <summary>Gets the elapsed wall-clock time for this operation. / 获取本次操作墙钟耗时。</summary>
        public TimeSpan Elapsed { get; }
        /// <summary>Gets the optional correlation identifier. / 获取可选关联标识。</summary>
        public string? CorrelationId { get; }
    }

    /// <summary>Executes Include ROIs as deterministic prompts against one cached SAM image embedding. / 将 Include ROI 作为确定性 Prompt 在一个已缓存 SAM 图像 Embedding 上执行。</summary>
    /// <remarks>The underlying image session is stateful and rejects concurrent prediction. This runner therefore executes prompts sequentially, preserving snapshot order while still exposing partial-failure semantics. / 底层图像 Session 有状态且拒绝并发预测，因此运行器按快照顺序串行执行，同时提供部分失败语义。</remarks>
    public sealed class VisualRoiPromptRunner
    {
        /// <summary>Runs all enabled Include ROIs applicable to promptable or instance segmentation. / 运行所有适用于提示式或实例分割的启用 Include ROI。</summary>
        public async Task<VisualRoiPromptBatchResult> RunAsync(
            PromptableSegmentationImageSession session,
            VisualRoiSnapshot snapshot,
            VisualRoiPromptOptions? promptOptions = null,
            RoiExecutionOptions? executionOptions = null,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            RoiExecutionOptions effective = executionOptions ?? RoiExecutionOptions.Default;
            if (coordinateContext != null && coordinateContext.SourceSize != snapshot.SourceSize) throw new ArgumentException("The ROI coordinate context source size must match the snapshot source size.", nameof(coordinateContext));
            PromptableImageIdentity? image = session.CurrentImage;
            if (image == null) throw new VisualException(VisualErrorCodes.PromptableSegmentationStateInvalid, "SetImage must succeed before running prompt ROIs.");
            if (image.SourceSize != snapshot.SourceSize) throw new VisualException(VisualErrorCodes.InputInvalid, "The prompt ROI snapshot source size must match the cached image embedding.", technicalDetails: "snapshot=" + snapshot.SourceSize.Width + "x" + snapshot.SourceSize.Height + ";image=" + image.SourceSize.Width + "x" + image.SourceSize.Height);

            var selected = new List<VisualRoi>();
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || roi.InclusionMode != RoiInclusionMode.Include) continue;
                if (!AppliesToPromptTask(roi)) continue;
                selected.Add(roi);
                if (selected.Count > effective.MaximumRois) throw new VisualException(VisualErrorCodes.InputInvalid, "The prompt ROI count exceeds the configured limit.", technicalDetails: "maximumRois=" + effective.MaximumRois);
                if (roi.ClassFilter.Count != 0) throw new VisualException(VisualErrorCodes.InputInvalid, "Promptable segmentation ROIs cannot use class filters because prompts have no class dimension.", technicalDetails: "roiId=" + roi.Id);
            }

            if (selected.Count == 0) return new VisualRoiPromptBatchResult(snapshot, Array.Empty<VisualRoiPromptResultItem>(), effective.CorrelationId, TimeSpan.Zero, 0);
            var items = new List<VisualRoiPromptResultItem>(selected.Count);
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

                foreach (VisualRoi roi in selected)
                {
                    operationToken.ThrowIfCancellationRequested();
                    PromptableSegmentationPrompt? prompt = null;
                    try
                    {
                        prompt = VisualRoiPromptFactory.Create(snapshot, roi, promptOptions, roi.Id, coordinateContext);
                        inferenceCallCount++;
                        PromptableSegmentationResult result = await session.PredictAsync(
                            prompt,
                            new VisualExecutionOptions(timeout: null, disposeOwnedInputOnCompletion: false, correlationId: effective.CorrelationId),
                            operationToken).ConfigureAwait(false);
                        items.Add(new VisualRoiPromptResultItem(roi, prompt, result, null));
                    }
                    catch (Exception exception) when (effective.FailureMode != RoiFailureMode.FailFast && !operationToken.IsCancellationRequested)
                    {
                        items.Add(new VisualRoiPromptResultItem(roi, prompt, null, exception));
                        if (effective.FailureMode == RoiFailureMode.CompleteThenFail) deferredFailure ??= exception;
                    }
                }
                stopwatch.Stop();
                if (deferredFailure != null) throw deferredFailure;
                return new VisualRoiPromptBatchResult(snapshot, items, effective.CorrelationId, stopwatch.Elapsed, inferenceCallCount);
            }
            finally
            {
                linkedSource?.Dispose();
                timeoutSource?.Dispose();
            }
        }

        private static bool AppliesToPromptTask(VisualRoi roi)
        {
            return roi.AppliesTo(VisualTaskId.PromptableSegmentation) || roi.AppliesTo(VisualTaskId.InstanceSegmentation);
        }
    }
}
