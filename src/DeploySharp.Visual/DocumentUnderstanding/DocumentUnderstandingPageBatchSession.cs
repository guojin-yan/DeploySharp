using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Binds one independently prepared page to its task and tokenizer for ordered batch execution. / 将一个独立 Prepared Page 与其 Task、Tokenizer 绑定，用于有序批量执行。</summary>
    public sealed class DocumentPageInferenceRequest
    {
        /// <summary>Initializes one caller-owned page request. / 初始化一个由调用方拥有的页面请求。</summary>
        public DocumentPageInferenceRequest(PreparedDocument document, DocumentTaskRequest task, IDocumentUnderstandingTokenizer tokenizer)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Task = task ?? throw new ArgumentNullException(nameof(task));
            Tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        }

        /// <summary>Gets the caller-owned single-page document. / 获取由调用方拥有的单页 Document。</summary>
        public PreparedDocument Document { get; }
        /// <summary>Gets the task executed for this page. / 获取本页执行的 Task。</summary>
        public DocumentTaskRequest Task { get; }
        /// <summary>Gets the tokenizer used for this page. / 获取本页使用的 Tokenizer。</summary>
        public IDocumentUnderstandingTokenizer Tokenizer { get; }
    }

    /// <summary>Owns a bounded pool of independent document sessions for ordered multi-page inference. / 拥有受限的独立 Document Session 池，用于有序多页推理。</summary>
    /// <remarks>Each slot owns a complete Encoder, Prefill, and Decode session because <see cref="DocumentUnderstandingSession"/> is stateful and single-writer. The runner does not combine pages into a model batch and does not change the profile's single-page contract. Prepared documents and tokenizers remain caller-owned unless input disposal is explicitly requested. / 每个槽位拥有完整 Encoder、Prefill、Decode Session，因为 DocumentUnderstandingSession 有状态且 Single-writer。本运行器不会把页面合并为模型 Batch，也不会改变 Profile 的单页合同。除非显式请求释放输入，否则 Prepared Document 与 Tokenizer 仍由调用方拥有。</remarks>
    public sealed class DocumentUnderstandingPageBatchSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<DocumentUnderstandingSession> _available;
        private readonly IReadOnlyList<DocumentUnderstandingSession> _sessions;
        private readonly SemaphoreSlim _slots;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private bool _disposed;

        /// <summary>Creates the requested number of fully independent document inference channels. / 创建指定数量、完全独立的文档推理通道。</summary>
        public DocumentUnderstandingPageBatchSession(BackendRegistry registry, DocumentUnderstandingBundle bundle, BackendRequest request, int maximumConcurrency = 1, bool enableProfiling = false)
        {
            if (registry == null || bundle == null || request == null) throw new ArgumentNullException(registry == null ? nameof(registry) : bundle == null ? nameof(bundle) : nameof(request));
            if (maximumConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
            Bundle = bundle;
            MaximumConcurrency = maximumConcurrency;
            var sessions = new List<DocumentUnderstandingSession>(maximumConcurrency);
            try
            {
                for (int index = 0; index < maximumConcurrency; index++)
                {
                    sessions.Add(new DocumentUnderstandingSession(registry, bundle, request, new SessionOptions(1, enableProfiling)));
                }
            }
            catch
            {
                foreach (DocumentUnderstandingSession session in sessions) TryDispose(session);
                _disposeSource.Dispose();
                throw;
            }
            _sessions = new ReadOnlyCollection<DocumentUnderstandingSession>(sessions);
            _available = new Queue<DocumentUnderstandingSession>(sessions);
            _slots = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        }

        /// <summary>Gets the exact profile/artifact bundle shared by every independent channel. / 获取所有独立通道共享的精确 Profile/Artifact Bundle。</summary>
        public DocumentUnderstandingBundle Bundle { get; }
        /// <summary>Gets the number of complete document channels that can run concurrently. / 获取可并发运行的完整文档通道数。</summary>
        public int MaximumConcurrency { get; }

        /// <summary>Runs prepared pages through independent channels and returns results in input order. / 通过独立通道运行 Prepared Page，并按输入顺序返回结果。</summary>
        public IReadOnlyList<DocumentUnderstandingResult> Run(IReadOnlyList<DocumentPageInferenceRequest> pages, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return RunAsync(pages, options, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Asynchronously runs prepared pages up to <see cref="MaximumConcurrency"/> and returns results in input order. / 在不超过 MaximumConcurrency 的范围内异步运行 Prepared Page，并按输入顺序返回结果。</summary>
        /// <remarks>All page/profile/task/tokenizer identities are validated before any backend call starts. Timeout and input-disposal options apply independently to each page. / 在任何 Backend 调用开始前校验全部页面、Profile、Task、Tokenizer Identity。超时与输入释放选项分别应用于每一页。</remarks>
        public async Task<IReadOnlyList<DocumentUnderstandingResult>> RunAsync(IReadOnlyList<DocumentPageInferenceRequest> pages, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            EnsureUsable();
            for (int index = 0; index < pages.Count; index++) Validate(pages[index], index);
            if (pages.Count == 0) return Array.Empty<DocumentUnderstandingResult>();

            VisualExecutionOptions effective = options ?? VisualExecutionOptions.Default;
            var results = new DocumentUnderstandingResult[pages.Count];
            var nextIndex = new[] { -1 };
            int workerCount = Math.Min(pages.Count, MaximumConcurrency);
            var workers = new Task[workerCount];
            for (int worker = 0; worker < workerCount; worker++)
            {
                // OpenCV DNN may complete RunAsync synchronously. Moving only the
                // bounded workers keeps independent native channels concurrent while
                // avoiding one Task/continuation per page.
                workers[worker] = Task.Run(() => RunPagesWorkerAsync(pages, results, effective, cancellationToken, nextIndex), CancellationToken.None);
            }
            try
            {
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            catch
            {
                try { await Task.WhenAll(workers).ConfigureAwait(false); } catch { }
                throw;
            }
            return Array.AsReadOnly(results);
        }

        /// <summary>Cancels active work, waits for every channel to unwind, and releases all owned sessions exactly once. / 取消活动工作、等待所有通道回卷，并 Exactly-once 释放全部自有 Session。</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }

            int acquired = 0;
            try
            {
                for (; acquired < MaximumConcurrency; acquired++) _slots.Wait();
                foreach (DocumentUnderstandingSession session in _sessions) session.Dispose();
            }
            finally
            {
                for (int index = 0; index < acquired; index++) _slots.Release();
                _slots.Dispose();
                _disposeSource.Dispose();
            }
        }

        private async Task<DocumentUnderstandingResult> RunPageAsync(DocumentPageInferenceRequest page, VisualExecutionOptions options, CancellationToken caller)
        {
            CancellationToken dispose = CaptureDisposeToken();
            CancellationTokenSource? linked = caller.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(caller, dispose) : null;
            CancellationToken operationToken = linked?.Token ?? dispose;
            try
            {
                bool entered = false;
                DocumentUnderstandingSession? session = null;
                try
                {
                    await _slots.WaitAsync(operationToken).ConfigureAwait(false);
                    entered = true;
                    EnsureUsable();
                    lock (_gate) session = _available.Dequeue();
                    session.Clear();
                    // SetDocument owns the encoder input only for that call. The page batch owns the
                    // full page lifetime, so defer the requested disposal until Generate has completed.
                    VisualExecutionOptions setOptions = options.DisposeOwnedInputOnCompletion
                        ? new VisualExecutionOptions(options.Timeout, false, options.CorrelationId)
                        : options;
                    await session.SetDocumentAsync(page.Document, setOptions, operationToken).ConfigureAwait(false);
                    return await session.GenerateAsync(page.Task, page.Tokenizer, options: options, cancellationToken: operationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception)
                {
                    throw MapCancellation(exception, caller);
                }
                catch (VisualException exception) when (operationToken.IsCancellationRequested && _disposeSource.IsCancellationRequested)
                {
                    throw MapCancellation(exception, caller);
                }
                finally
                {
                    if (session != null)
                    {
                        TryClear(session);
                        lock (_gate) _available.Enqueue(session);
                    }
                    if (entered) _slots.Release();
                    if (options.DisposeOwnedInputOnCompletion) TryDispose(page.Document);
                }
            }
            finally { linked?.Dispose(); }
        }

        private void Validate(DocumentPageInferenceRequest? page, int index)
        {
            if (page == null) throw new ArgumentException("Page requests cannot contain null.", nameof(page));
            page.Document.EnsureUsable();
            DocumentUnderstandingProfile profile = Bundle.Profile;
            if (!string.Equals(page.Document.ProfileId, profile.ProfileId, StringComparison.Ordinal) || page.Document.Pages.Count != 1)
            {
                throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "Each page request must contain one document bound to the batch profile.", profileId: profile.ProfileId, technicalDetails: "pageIndex=" + index);
            }
            if (!profile.Tasks.Contains(page.Task.Task) || !string.Equals(page.Task.SchemaId, profile.Schema.SchemaId, StringComparison.Ordinal) || !string.Equals(page.Tokenizer.TokenizerId, profile.Tokenizer.TokenizerId, StringComparison.Ordinal) || !string.Equals(page.Tokenizer.Identity, profile.Tokenizer.Identity, StringComparison.Ordinal))
            {
                throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "A page task or tokenizer differs from the batch profile.", profileId: profile.ProfileId, technicalDetails: "pageIndex=" + index);
            }
        }

        private async Task RunPagesWorkerAsync(IReadOnlyList<DocumentPageInferenceRequest> pages, DocumentUnderstandingResult[] results, VisualExecutionOptions options, CancellationToken cancellationToken, int[] nextIndex)
        {
            while (true)
            {
                int index = Interlocked.Increment(ref nextIndex[0]);
                if (index >= pages.Count) return;
                results[index] = await RunPageAsync(pages[index], options, cancellationToken).ConfigureAwait(false);
            }
        }

        private void EnsureUsable()
        {
            lock (_gate) if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The document page batch session is disposed.", profileId: Bundle.Profile.ProfileId);
        }

        private CancellationToken CaptureDisposeToken()
        {
            lock (_gate)
            {
                if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The document page batch session is disposed.", profileId: Bundle.Profile.ProfileId);
                return _disposeSource.Token;
            }
        }

        private VisualException MapCancellation(Exception exception, CancellationToken caller)
        {
            if (_disposed || _disposeSource.IsCancellationRequested) return new VisualException(VisualErrorCodes.ObjectDisposed, "The document page batch session was disposed during execution.", exception, profileId: Bundle.Profile.ProfileId);
            if (caller.IsCancellationRequested) return new VisualException(VisualErrorCodes.Cancelled, "Document page batch inference was cancelled by the caller.", exception, profileId: Bundle.Profile.ProfileId);
            return new VisualException(VisualErrorCodes.Timeout, "Document page batch inference timed out.", exception, profileId: Bundle.Profile.ProfileId);
        }

        private static void TryClear(DocumentUnderstandingSession session) { try { session.Clear(); } catch { } }
        private static void TryDispose(IDisposable value) { try { value.Dispose(); } catch { } }
    }
}
