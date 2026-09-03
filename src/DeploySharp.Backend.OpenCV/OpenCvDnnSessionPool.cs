using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Dispatches OpenCV DNN calls to independently-loaded Net instances. / 将 OpenCV DNN 调用分发到彼此独立加载的 Net 实例。</summary>
    internal sealed class OpenCvDnnSessionPool : IInferenceSession
    {
        private readonly object _lifetimeGate = new object();
        private readonly object _slotGate = new object();
        private readonly OpenCvDnnSession[] _sessions;
        private readonly Queue<int> _freeSlots;
        private readonly SemaphoreSlim _available;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private bool _disposed;

        public OpenCvDnnSessionPool(IReadOnlyList<OpenCvDnnSession> sessions)
        {
            if (sessions == null) throw new ArgumentNullException(nameof(sessions));
            if (sessions.Count < 2) throw new ArgumentException("An OpenCV DNN session pool requires at least two independent sessions.", nameof(sessions));
            _sessions = sessions.ToArray();
            _freeSlots = new Queue<int>(_sessions.Length);
            for (int index = 0; index < _sessions.Length; index++) _freeSlots.Enqueue(index);
            _available = new SemaphoreSlim(_sessions.Length, _sessions.Length);
            Metadata = _sessions[0].Metadata;
        }

        public ModelMetadata Metadata { get; }

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            CancellationToken operationToken = CreateOperationToken(cancellationToken, out CancellationTokenSource? linked);
            int slot = -1;
            try
            {
                slot = AcquireSlot(operationToken);
                return _sessions[slot].Run(inputs, operationToken);
            }
            finally
            {
                if (slot >= 0) ReleaseSlot(slot);
                linked?.Dispose();
            }
        }

        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            // OpenCV DNN's CPU wrapper is synchronous. A worker is required here
            // so callers can acquire multiple independent Net instances through
            // the asynchronous Core contract without blocking before a Task exists.
            return Task.Run(() => Run(inputs, cancellationToken), CancellationToken.None);
        }

        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }

            try
            {
                for (int acquired = 0; acquired < _sessions.Length; acquired++) _available.Wait();
                Exception? firstFailure = null;
                foreach (OpenCvDnnSession session in _sessions)
                {
                    try { session.Dispose(); }
                    catch (Exception exception) { firstFailure ??= exception; }
                }
                if (firstFailure != null) throw firstFailure;
            }
            finally
            {
                _available.Dispose();
                _disposeSource.Dispose();
            }
        }

        private int AcquireSlot(CancellationToken cancellationToken)
        {
            EnsureUsable();
            try { _available.Wait(cancellationToken); }
            catch (OperationCanceledException exception)
            {
                throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.Cancelled, "OpenCV DNN session-pool acquisition was cancelled.", exception, operation: "pool-acquire");
            }
            lock (_slotGate)
            {
                EnsureUsable();
                return _freeSlots.Dequeue();
            }
        }

        private void ReleaseSlot(int slot)
        {
            lock (_slotGate) _freeSlots.Enqueue(slot);
            try { _available.Release(); }
            catch (ObjectDisposedException) { }
        }

        private CancellationToken CreateOperationToken(CancellationToken callerToken, out CancellationTokenSource? linked)
        {
            lock (_lifetimeGate)
            {
                if (_disposed) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ObjectDisposed, "The OpenCV DNN session pool has been disposed.", operation: "pool");
                CancellationToken disposeToken = _disposeSource.Token;
                if (!callerToken.CanBeCanceled)
                {
                    linked = null;
                    return disposeToken;
                }
                linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, disposeToken);
                return linked.Token;
            }
        }

        private void EnsureUsable()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ObjectDisposed, "The OpenCV DNN session pool has been disposed.", operation: "pool");
            }
        }
    }
}
