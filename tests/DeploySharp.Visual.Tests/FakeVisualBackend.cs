using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace DeploySharp.Visual.Tests
{
    internal sealed class FakeVisualBackendProvider : IBackendProvider
    {
        private readonly ModelMetadata _metadata;
        private readonly Func<InferenceInputs, InferenceOutputs> _outputFactory;
        private bool _disposed;

        public FakeVisualBackendProvider(ModelMetadata metadata, Func<InferenceInputs, InferenceOutputs> outputFactory, string format = "fake", BackendId? backendId = null)
        {
            _metadata = metadata;
            _outputFactory = outputFactory;
            Format = format;
            Descriptor = new BackendDescriptor(backendId ?? VisualTestData.BackendId, "Fake Visual", "1.0", BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes, new[] { format });
        }

        public BackendDescriptor Descriptor { get; }
        public string Format { get; }
        public TimeSpan Delay { get; set; }
        public Exception? Failure { get; set; }
        public SequenceArgMaxResult? SequenceArgMaxResult { get; set; }
        public FakeVisualSession? LastSession { get; private set; }
        public List<FakeVisualSession> CreatedSessions { get; } = new List<FakeVisualSession>();
        public int DisposeCount { get; private set; }

        public bool CanCreate(ModelArtifact artifact, BackendRequest request)
        {
            return !_disposed && artifact.ModelId == _metadata.ModelId && string.Equals(artifact.Format, Format, StringComparison.OrdinalIgnoreCase) && Descriptor.Supports(request.RequiredCapabilities);
        }

        public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FakeVisualBackendProvider));
            LastSession = new FakeVisualSession(_metadata, _outputFactory, () => Delay, () => Failure, () => SequenceArgMaxResult);
            CreatedSessions.Add(LastSession);
            return LastSession;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeCount++;
        }
    }

    internal sealed class FakeVisualSession : IInferenceSession, ISequenceArgMaxInferenceSession
    {
        private readonly Func<InferenceInputs, InferenceOutputs> _outputFactory;
        private readonly Func<TimeSpan> _delay;
        private readonly Func<Exception?> _failure;
        private readonly Func<SequenceArgMaxResult?> _sequenceArgMaxResult;
        private int _active;
        private bool _disposed;

        public FakeVisualSession(ModelMetadata metadata, Func<InferenceInputs, InferenceOutputs> outputFactory, Func<TimeSpan> delay, Func<Exception?> failure, Func<SequenceArgMaxResult?> sequenceArgMaxResult)
        {
            Metadata = metadata;
            _outputFactory = outputFactory;
            _delay = delay;
            _failure = failure;
            _sequenceArgMaxResult = sequenceArgMaxResult;
        }

        public ModelMetadata Metadata { get; }
        public int RunCount { get; private set; }
        public int MaximumActive { get; private set; }
        public int DisposeCount { get; private set; }
        public int SequenceArgMaxRunCount { get; private set; }
        public bool IsSequenceArgMaxSupported => _sequenceArgMaxResult() != null;

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            return RunCoreAsync(inputs, cancellationToken).GetAwaiter().GetResult();
        }

        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            return RunCoreAsync(inputs, cancellationToken);
        }

        public SequenceArgMaxResult RunSequenceArgMax(InferenceInputs inputs, SequenceArgMaxRequest request, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FakeVisualSession));
            int active = Interlocked.Increment(ref _active);
            if (active > MaximumActive) MaximumActive = active;
            SequenceArgMaxRunCount++;
            try
            {
                TimeSpan delay = _delay();
                if (delay > TimeSpan.Zero && cancellationToken.WaitHandle.WaitOne(delay)) cancellationToken.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                Exception? failure = _failure();
                if (failure != null) throw failure;
                return _sequenceArgMaxResult() ?? throw new NotSupportedException();
            }
            finally { Interlocked.Decrement(ref _active); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeCount++;
        }

        private async Task<InferenceOutputs> RunCoreAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FakeVisualSession));
            int active = Interlocked.Increment(ref _active);
            if (active > MaximumActive) MaximumActive = active;
            RunCount++;
            try
            {
                if (_delay() > TimeSpan.Zero) await Task.Delay(_delay(), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                Exception? failure = _failure();
                if (failure != null) throw failure;
                return _outputFactory(inputs);
            }
            finally { Interlocked.Decrement(ref _active); }
        }
    }

    internal sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
