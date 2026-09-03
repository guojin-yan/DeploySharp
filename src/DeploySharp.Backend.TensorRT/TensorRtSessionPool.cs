using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Dispatches concurrent calls to independently-created TensorRT runtimes, engines, contexts, and streams. / 将并发调用分发到彼此独立创建的 TensorRT Runtime、Engine、Context 与 Stream。</summary>
    internal sealed class TensorRtSessionPool : IInferenceSession, ITensorRtDeviceInferenceSession, ISequenceArgMaxInferenceSession
    {
        private readonly object _lifetimeGate = new object();
        private readonly object _slotGate = new object();
        private readonly TensorRtSession[] _sessions;
        private readonly Queue<int> _freeSlots;
        private readonly SemaphoreSlim _available;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private bool _disposed;

        public TensorRtSessionPool(IReadOnlyList<TensorRtSession> sessions)
        {
            if (sessions == null) throw new ArgumentNullException(nameof(sessions));
            if (sessions.Count < 2) throw new ArgumentException("A TensorRT session pool requires at least two independent sessions.", nameof(sessions));
            _sessions = sessions.ToArray();
            _freeSlots = new Queue<int>(_sessions.Length);
            for (int index = 0; index < _sessions.Length; index++) _freeSlots.Enqueue(index);
            _available = new SemaphoreSlim(_sessions.Length, _sessions.Length);
            Metadata = _sessions[0].Metadata;
        }

        public ModelMetadata Metadata { get; }

        public int DeviceOrdinal => _sessions[0].DeviceOrdinal;

        public bool IsSequenceArgMaxSupported => _sessions.All(session => session.IsSequenceArgMaxSupported);

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
            // Host output materialization synchronizes the CUDA stream; dispatch
            // the synchronous fallback on a worker so independent channels can run
            // concurrently when callers use the async Core contract.
            return Task.Run(() => Run(inputs, cancellationToken), CancellationToken.None);
        }

        public SequenceArgMaxResult RunSequenceArgMax(InferenceInputs inputs, SequenceArgMaxRequest request, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!IsSequenceArgMaxSupported) throw new NotSupportedException("Not every TensorRT session in the pool supports sequence argmax.");
            CancellationToken operationToken = CreateOperationToken(cancellationToken, out CancellationTokenSource? linked);
            int slot = -1;
            try
            {
                slot = AcquireSlot(operationToken);
                return _sessions[slot].RunSequenceArgMax(inputs, request, operationToken);
            }
            finally
            {
                if (slot >= 0) ReleaseSlot(slot);
                linked?.Dispose();
            }
        }

        public TensorRtDeviceInferenceExecution RunDevice(
            IReadOnlyList<TensorRtDeviceTensor> inputs,
            IReadOnlyList<TensorRtDeviceTensor> outputs,
            CudaStream stream,
            CancellationToken cancellationToken = default)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (outputs == null) throw new ArgumentNullException(nameof(outputs));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            CancellationToken operationToken = CreateOperationToken(cancellationToken, out CancellationTokenSource? linked);
            int slot = -1;
            TensorRtDeviceInferenceExecution? inner = null;
            try
            {
                slot = AcquireSlot(operationToken);
                inner = _sessions[slot].RunDevice(inputs, outputs, stream, operationToken);
                int released = 0;
                TensorRtDeviceInferenceExecution ownedInner = inner;
                int ownedSlot = slot;
                slot = -1;
                return new TensorRtDeviceInferenceExecution(stream, ownedInner.Outputs, () =>
                {
                    if (Interlocked.Exchange(ref released, 1) != 0) return;
                    try { ownedInner.ReleaseAfterEnqueue(); }
                    finally { ReleaseSlot(ownedSlot); }
                });
            }
            catch
            {
                // If the outer lease cannot be constructed, release the inner session lease before returning the pool slot.
                inner?.Dispose();
                throw;
            }
            finally
            {
                if (slot >= 0) ReleaseSlot(slot);
                linked?.Dispose();
            }
        }

        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }

            int acquired = 0;
            try
            {
                for (; acquired < _sessions.Length; acquired++) _available.Wait();
                foreach (TensorRtSession session in _sessions) session.Dispose();
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
                throw new TensorRtBackendException(TensorRtErrorCodes.InferenceFailed, "TensorRT session-pool acquisition was cancelled.", exception, operation: "pool-acquire");
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
                if (_disposed) throw new TensorRtBackendException(TensorRtErrorCodes.ObjectDisposed, "The TensorRT session pool has been disposed.", operation: "pool");
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
                if (_disposed) throw new TensorRtBackendException(TensorRtErrorCodes.ObjectDisposed, "The TensorRT session pool has been disposed.", operation: "pool");
            }
        }
    }
}
