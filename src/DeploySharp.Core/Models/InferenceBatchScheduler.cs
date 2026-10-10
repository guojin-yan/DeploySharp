using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Internal;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp
{
    /// <summary>Splits an ordered workload into model batches and dispatches them through an inference session pool. / 将有序工作负载拆分为模型批次，并通过推理 Session 池分派。</summary>
    /// <remarks>The scheduler does not own the session. Create the session through <see cref="Registry.BackendRegistry"/> with <see cref="Models.SessionOptions.MaxConcurrency"/> set to the desired independent session count. / 调度器不拥有 Session；请通过 BackendRegistry 创建 Session，并使用 SessionOptions.MaxConcurrency 指定独立 Session 数量。</remarks>
    public sealed class InferenceBatchScheduler<TInput, TOutput>
    {
        private readonly IInferenceSession _session;
        private readonly Func<IReadOnlyList<TInput>, InferenceInputs> _prepareBatch;
        private readonly Func<InferenceOutputs, int, IReadOnlyList<TOutput>> _decodeBatch;

        /// <summary>Initializes a reusable ordered batch scheduler. / 初始化可复用的有序批调度器。</summary>
        /// <param name="session">Inference session or session pool used to execute prepared batches. / 用于执行已准备批次的推理 Session 或 Session 池。</param>
        /// <param name="maximumBatchSize">Maximum number of items submitted in one model call. / 单次模型调用提交的最大项目数。</param>
        /// <param name="prepareBatch">Creates model inputs for one ordered batch. / 为一个有序批次创建模型输入。</param>
        /// <param name="decodeBatch">Decodes model outputs and returns one output for each input item. / 解码模型输出并为每个输入项目返回一个结果。</param>
        /// <param name="maximumInFlightBatches">Bounds prepared batches retained while earlier batches are executing; zero uses the registry session-pool capacity. / 限制执行中保留的已准备批次数；零使用注册表 Session 池容量。</param>
        public InferenceBatchScheduler(IInferenceSession session, int maximumBatchSize, Func<IReadOnlyList<TInput>, InferenceInputs> prepareBatch, Func<InferenceOutputs, int, IReadOnlyList<TOutput>> decodeBatch, int maximumInFlightBatches = 0)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (maximumBatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatchSize));
            if (maximumInFlightBatches < 0) throw new ArgumentOutOfRangeException(nameof(maximumInFlightBatches));
            MaximumBatchSize = maximumBatchSize;
            MaximumInFlightBatches = maximumInFlightBatches;
            _prepareBatch = prepareBatch ?? throw new ArgumentNullException(nameof(prepareBatch));
            _decodeBatch = decodeBatch ?? throw new ArgumentNullException(nameof(decodeBatch));
        }

        /// <summary>Gets the maximum number of items submitted in one model call. / 获取单次模型调用提交的最大项目数。</summary>
        public int MaximumBatchSize { get; }

        /// <summary>Gets the configured maximum number of prepared batches retained while execution is in flight; zero uses the session-pool capacity. / 获取执行中的已准备批次最大保留数；零使用 Session 池容量。</summary>
        public int MaximumInFlightBatches { get; }

        /// <summary>Runs all batches and returns outputs in original item order. / 运行全部批次并按原始项目顺序返回输出。</summary>
        public IReadOnlyList<TOutput> Run(IReadOnlyList<TInput> items, CancellationToken cancellationToken = default(CancellationToken))
            => RunAsync(items, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Runs independent batches concurrently up to the underlying session-pool capacity. / 在底层 Session 池容量范围内并发运行独立批次。</summary>
        public async Task<IReadOnlyList<TOutput>> RunAsync(IReadOnlyList<TInput> items, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count == 0) return Array.Empty<TOutput>();
            cancellationToken.ThrowIfCancellationRequested();
            // Snapshot the item references once. Each batch can then be exposed as
            // an ArraySegment without allocating a List plus ReadOnlyCollection;
            // this also preserves deterministic input order if the caller mutates
            // its collection while asynchronous batches are in flight.
            var snapshot = new TInput[items.Count];
            for (int index = 0; index < snapshot.Length; index++) snapshot[index] = items[index];
            int batchCount = snapshot.Length / MaximumBatchSize;
            if (snapshot.Length % MaximumBatchSize != 0) batchCount++;
            int workerCount = Math.Min(batchCount, ResolveMaximumInFlightBatches());
            // Use a fixed worker set instead of creating one Task per batch. This
            // keeps the scheduler bounded for large OCR/ROI workloads and lets a
            // preparation/decode failure cancel batches that have not started yet.
            using var stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var completed = new BatchResult?[batchCount];
            var nextBatch = new[] { -1 };
            var workers = new Task[workerCount];
            try
            {
                for (int worker = 0; worker < workerCount; worker++)
                {
                    workers[worker] = Task.Run(
                        () => RunWorkerAsync(snapshot, batchCount, completed, nextBatch, stopSource),
                        CancellationToken.None);
                }
                await Task.WhenAll(workers).ConfigureAwait(false);
                var results = new TOutput[items.Count];
                for (int batchIndex = 0; batchIndex < completed.Length; batchIndex++)
                {
                    BatchResult batch = completed[batchIndex] ?? throw new InvalidOperationException("The batch scheduler completed without a result for batch " + batchIndex + ".");
                    for (int index = 0; index < batch.Outputs.Count; index++) results[batch.Offset + index] = batch.Outputs[index];
                }
                return Array.AsReadOnly(results);
            }
            catch
            {
                CancelWorkers(stopSource);
                try { await Task.WhenAll(workers).ConfigureAwait(false); }
                catch { }
                throw;
            }
        }

        private int ResolveMaximumInFlightBatches()
        {
            if (MaximumInFlightBatches > 0) return MaximumInFlightBatches;
            if (_session is IInferenceSessionConcurrency pooled) return pooled.MaximumConcurrency;
            return 1;
        }

        private async Task RunWorkerAsync(
            TInput[] snapshot,
            int batchCount,
            BatchResult?[] completed,
            int[] nextBatch,
            CancellationTokenSource stopSource)
        {
            try
            {
                while (true)
                {
                    stopSource.Token.ThrowIfCancellationRequested();
                    int batchIndex = Interlocked.Increment(ref nextBatch[0]);
                    if (batchIndex >= batchCount) return;

                    int offset = checked(batchIndex * MaximumBatchSize);
                    int count = Math.Min(MaximumBatchSize, snapshot.Length - offset);
                    IReadOnlyList<TInput> batch = new ArraySegment<TInput>(snapshot, offset, count);
                    InferenceInputs prepared = _prepareBatch(batch) ?? throw new InvalidOperationException("The batch input factory returned null.");
                    InferenceOutputs outputs = await _session.RunAsync(prepared, stopSource.Token).ConfigureAwait(false);
                    IReadOnlyList<TOutput> decoded = _decodeBatch(outputs, count) ?? throw new InvalidOperationException("The batch output decoder returned null.");
                    if (decoded.Count != count) throw new InvalidOperationException("The decoded output count does not match the submitted batch size.");
                    completed[batchIndex] = new BatchResult(offset, decoded);
                }
            }
            catch
            {
                // Cancel other workers immediately. The caller still observes
                // the original failure after all workers have unwound.
                CancelWorkers(stopSource);
                throw;
            }
        }

        private static void CancelWorkers(CancellationTokenSource stopSource)
        {
            try { stopSource.Cancel(); }
            catch (AggregateException) { }
        }

        private sealed class BatchResult
        {
            public BatchResult(int offset, IReadOnlyList<TOutput> outputs) { Offset = offset; Outputs = outputs; }
            public int Offset { get; }
            public IReadOnlyList<TOutput> Outputs { get; }
        }
    }
}
