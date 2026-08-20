using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Results.Multimodal;

namespace JYPPX.DeploySharp.Multimodal
{
    /// <summary>Provides validated, single-writer multimodal orchestration over one backend session. / 在一个后端会话上提供经校验的单写入多模态编排。</summary>
    public sealed class MultimodalSession : IDisposable
    {
        private readonly IMultimodalBackendSession _backend;
        private readonly bool _ownsBackend;
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly object _lifetimeGate = new object();
        private bool _disposed;

        /// <summary>Initializes an orchestrator and optionally owns the backend lifetime. / 初始化编排器并可选择持有后端生命周期。</summary>
        public MultimodalSession(IMultimodalBackendSession backend, bool ownsBackend = true)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _ownsBackend = ownsBackend;
        }

        /// <summary>Gets immutable backend metadata. / 获取不可变后端元数据。</summary>
        public MultimodalBackendDescriptor Descriptor => _backend.Descriptor;

        /// <summary>Generates a completed result synchronously. / 同步生成完整结果。</summary>
        public MultimodalTextResult Generate(MultimodalRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => GenerateAsync(request, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Generates a completed result while preserving request media order. / 生成完整结果并保留请求媒体顺序。</summary>
        public async Task<MultimodalTextResult> GenerateAsync(MultimodalRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateRequest(request, MultimodalCapabilities.TextGeneration);
            EnterOperation();
            using (CancellationTokenSource? timeout = CreateTimeout(request.Options.Timeout))
            using (CancellationTokenSource linked = CreateLinked(cancellationToken, timeout))
            {
                try
                {
                    GenerationResult result = await _backend.GenerateAsync(request, linked.Token).ConfigureAwait(false);
                    if (result == null) throw ContractError("The backend returned a null generation result.");
                    return new MultimodalTextResult(result, request.CreateReferences());
                }
                catch (OperationCanceledException exception)
                {
                    throw MapCancellation(exception, cancellationToken, timeout);
                }
                finally
                {
                    _operationGate.Release();
                }
            }
        }

        /// <summary>Streams validated, ordered generation chunks. / 流式返回经校验的有序生成片段。</summary>
        public IAsyncEnumerable<GenerationChunk> StreamAsync(MultimodalRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateRequest(request, MultimodalCapabilities.Streaming);
            return StreamCoreAsync(request, cancellationToken);
        }

        /// <summary>Cancels active work, waits for it to leave, and releases an owned backend. / 取消活动工作、等待其退出并释放持有的后端。</summary>
        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }

            _operationGate.Wait();
            try
            {
                if (_ownsBackend) _backend.Dispose();
                _disposeSource.Dispose();
            }
            finally
            {
                _operationGate.Release();
                _operationGate.Dispose();
            }
        }

        private async IAsyncEnumerable<GenerationChunk> StreamCoreAsync(
            MultimodalRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            EnterOperation();
            using (CancellationTokenSource? timeout = CreateTimeout(request.Options.Timeout))
            using (CancellationTokenSource linked = CreateLinked(cancellationToken, timeout))
            {
                int expectedIndex = 0;
                bool terminal = false;
                IAsyncEnumerator<GenerationChunk> enumerator = _backend.StreamAsync(request, linked.Token).GetAsyncEnumerator(linked.Token);
                try
                {
                    while (true)
                    {
                        GenerationChunk chunk;
                        try
                        {
                            if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) break;
                            chunk = enumerator.Current;
                        }
                        catch (OperationCanceledException exception)
                        {
                            throw MapCancellation(exception, cancellationToken, timeout);
                        }
                        if (chunk == null) throw ContractError("The backend stream returned a null chunk.");
                        if (terminal) throw ContractError("The backend emitted data after its terminal chunk.");
                        if (chunk.SequenceIndex != expectedIndex) throw ContractError("The backend stream sequence is not contiguous.");
                        expectedIndex++;
                        terminal = chunk.IsTerminal;
                        yield return chunk;
                    }

                    if (!terminal) throw ContractError("The backend stream ended without a terminal chunk.");
                }
                finally
                {
                    try { await enumerator.DisposeAsync().ConfigureAwait(false); }
                    finally { _operationGate.Release(); }
                }
            }
        }

        private void ValidateRequest(MultimodalRequest request, MultimodalCapabilities operation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            MultimodalBackendDescriptor descriptor = _backend.Descriptor ?? throw ContractError("The backend descriptor is null.");
            if (!descriptor.Availability.IsAvailable)
            {
                throw new MultimodalException(MultimodalErrorCodes.CapabilityUnavailable, descriptor.Availability.Reason, modelId: descriptor.ModelId, technicalDetails: descriptor.Availability.RuntimeIdentity);
            }
            if ((descriptor.Capabilities & operation) != operation) throw new MultimodalException(MultimodalErrorCodes.CapabilityUnavailable, "The adapter does not provide the requested multimodal operation.", modelId: descriptor.ModelId, technicalDetails: "required=" + operation);
            if (request.Media.Count > descriptor.MaximumMedia) throw new MultimodalException(MultimodalErrorCodes.CapabilityUnavailable, "The request exceeds the adapter media limit.", modelId: descriptor.ModelId, technicalDetails: "maximumMedia=" + descriptor.MaximumMedia);
            if (request.Media.Count > 1 && (descriptor.Capabilities & MultimodalCapabilities.MultipleMedia) == 0) throw new MultimodalException(MultimodalErrorCodes.CapabilityUnavailable, "The adapter does not support multiple media items.", modelId: descriptor.ModelId);
            for (int index = 0; index < request.Media.Count; index++)
            {
                if (request.Media[index].Region != null && (descriptor.Capabilities & MultimodalCapabilities.Regions) == 0) throw new MultimodalException(MultimodalErrorCodes.CapabilityUnavailable, "The adapter does not support region-bound media.", modelId: descriptor.ModelId, technicalDetails: "mediaId=" + request.Media[index].Id);
            }
        }

        private void EnterOperation()
        {
            ThrowIfDisposed();
            if (!_operationGate.Wait(0)) throw new MultimodalException(MultimodalErrorCodes.SessionBusy, "The multimodal session is single-writer and already has an active operation.", modelId: _backend.Descriptor.ModelId);
            try
            {
                ThrowIfDisposed();
            }
            catch
            {
                _operationGate.Release();
                throw;
            }
        }

        private CancellationTokenSource CreateLinked(CancellationToken caller, CancellationTokenSource? timeout)
        {
            return timeout == null
                ? CancellationTokenSource.CreateLinkedTokenSource(caller, _disposeSource.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token, _disposeSource.Token);
        }

        private static CancellationTokenSource? CreateTimeout(TimeSpan? value)
        {
            if (!value.HasValue) return null;
            var source = new CancellationTokenSource();
            source.CancelAfter(value.Value);
            return source;
        }

        private MultimodalException MapCancellation(OperationCanceledException exception, CancellationToken caller, CancellationTokenSource? timeout)
        {
            bool timedOut = timeout != null && timeout.IsCancellationRequested && !caller.IsCancellationRequested && !_disposeSource.IsCancellationRequested;
            return new MultimodalException(
                timedOut ? MultimodalErrorCodes.Timeout : MultimodalErrorCodes.Cancelled,
                timedOut ? "The multimodal operation exceeded its timeout." : "The multimodal operation was cancelled.",
                exception,
                _backend.Descriptor.ModelId);
        }

        private MultimodalException ContractError(string message) => new MultimodalException(MultimodalErrorCodes.BackendContractInvalid, message, modelId: _backend.Descriptor.ModelId);

        private void ThrowIfDisposed()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MultimodalSession));
            }
        }
    }
}
