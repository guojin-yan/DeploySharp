using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Internal
{
    /// <summary>Dispatches calls to independently-created single-channel backend sessions. / 将调用分派到独立创建的单通道后端 Session。</summary>
    internal sealed class PooledInferenceSession : IInferenceSession, IInferenceSessionConcurrency, ISequenceArgMaxInferenceSession
    {
        private readonly object _gate = new object();
        private readonly ConcurrentQueue<IInferenceSession> _availableSessions;
        private readonly IReadOnlyList<IInferenceSession> _sessions;
        private readonly SemaphoreSlim _available;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private bool _disposed;

        public PooledInferenceSession(IReadOnlyList<IInferenceSession> sessions)
        {
            if (sessions == null) throw new ArgumentNullException(nameof(sessions));
            if (sessions.Count == 0) throw new ArgumentException("An inference session pool requires at least one session.", nameof(sessions));
            var owned = new List<IInferenceSession>(sessions.Count);
            var available = new ConcurrentQueue<IInferenceSession>();
            for (int index = 0; index < sessions.Count; index++)
            {
                IInferenceSession session = sessions[index] ?? throw new ArgumentException("An inference session pool cannot contain null sessions.", nameof(sessions));
                owned.Add(session);
                available.Enqueue(session);
            }
            _sessions = owned.AsReadOnly();
            _availableSessions = available;
            _available = new SemaphoreSlim(sessions.Count, sessions.Count);
            Metadata = sessions[0].Metadata;
        }

        public ModelMetadata Metadata { get; }

        public int MaximumConcurrency => _sessions.Count;

        public bool IsSequenceArgMaxSupported
        {
            get
            {
                for (int index = 0; index < _sessions.Count; index++)
                {
                    if (!(_sessions[index] is ISequenceArgMaxInferenceSession sequence) || !sequence.IsSequenceArgMaxSupported) return false;
                }
                return true;
            }
        }

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            IInferenceSession? session = null;
            CancellationToken disposeToken = CaptureDisposeToken();
            CancellationToken operationToken = disposeToken;
            CancellationTokenSource? linked = null;
            try
            {
                // The pool lifetime token is already the operation token on the
                // normal pipeline path. Avoid allocating a linked CTS when the
                // caller passes that same token through the pool.
                if (cancellationToken.CanBeCanceled && cancellationToken != disposeToken)
                {
                    linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposeToken);
                    operationToken = linked.Token;
                }
                // A pre-cancelled request must never lease a channel or enter a
                // backend. This also keeps synchronous and asynchronous calls
                // consistent when an idle channel happens to be available.
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                _available.Wait(operationToken);
                session = TakeAvailable();
                return session.Run(inputs, operationToken);
            }
            finally
            {
                linked?.Dispose();
                Return(session);
            }
        }

        public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            IInferenceSession? session = null;
            CancellationToken disposeToken = CaptureDisposeToken();
            CancellationToken operationToken = disposeToken;
            CancellationTokenSource? linked = null;
            try
            {
                // Keep the common pooled hot path allocation-free when the
                // caller forwards the pool lifetime token unchanged.
                if (cancellationToken.CanBeCanceled && cancellationToken != disposeToken)
                {
                    linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposeToken);
                    operationToken = linked.Token;
                }
                await _available.WaitAsync(operationToken).ConfigureAwait(false);
                session = TakeAvailable();
                // A backend may document RunAsync as a synchronous fallback. Each leased session is independent,
                // so dispatching the call to a worker preserves real pool parallelism without sharing native state.
                return await Task.Run(() => session.RunAsync(inputs, operationToken), CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                linked?.Dispose();
                Return(session);
            }
        }

        public SequenceArgMaxResult RunSequenceArgMax(InferenceInputs inputs, SequenceArgMaxRequest request, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (request == null) throw new ArgumentNullException(nameof(request));
            IInferenceSession? session = null;
            CancellationToken disposeToken = CaptureDisposeToken();
            CancellationToken operationToken = disposeToken;
            CancellationTokenSource? linked = null;
            try
            {
                if (cancellationToken.CanBeCanceled && cancellationToken != disposeToken)
                {
                    linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposeToken);
                    operationToken = linked.Token;
                }
                _available.Wait(operationToken);
                session = TakeAvailable();
                if (!(session is ISequenceArgMaxInferenceSession sequence) || !sequence.IsSequenceArgMaxSupported)
                {
                    throw new NotSupportedException("The leased inference session does not support sequence argmax reduction.");
                }
                return sequence.RunSequenceArgMax(inputs, request, operationToken);
            }
            finally
            {
                linked?.Dispose();
                Return(session);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }
            int acquired = 0;
            var failures = new List<Exception>();
            try
            {
                for (; acquired < _sessions.Count; acquired++) _available.Wait();
                for (int index = _sessions.Count - 1; index >= 0; index--)
                {
                    try { _sessions[index].Dispose(); }
                    catch (Exception exception) { failures.Add(exception); }
                }
            }
            finally
            {
                _available.Dispose();
                _disposeSource.Dispose();
            }
            if (failures.Count > 0) throw new AggregateException("One or more pooled inference sessions failed to dispose.", failures);
        }

        private IInferenceSession TakeAvailable()
        {
            if (_availableSessions.TryDequeue(out IInferenceSession? session)) return session;
            // The semaphore and queue are updated together; reaching this branch
            // indicates an internal invariant violation rather than user input.
            throw new InvalidOperationException("The inference session pool lost an available session.");
        }

        private void Return(IInferenceSession? session)
        {
            if (session == null) return;
            _availableSessions.Enqueue(session);
            _available.Release();
        }

        private CancellationToken CaptureDisposeToken()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(PooledInferenceSession));
                return _disposeSource.Token;
            }
        }
    }
}
