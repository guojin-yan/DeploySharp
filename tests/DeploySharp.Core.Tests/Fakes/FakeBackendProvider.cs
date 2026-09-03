using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Core.Tests.Fakes
{
    internal sealed class FakeBackendProvider : IBackendProvider
    {
        private readonly string _acceptedFormat;

        public FakeBackendProvider(
            string id,
            string acceptedFormat = "onnx",
            BackendCapabilities capabilities = BackendCapabilities.TensorInference)
        {
            _acceptedFormat = acceptedFormat;
            Descriptor = new BackendDescriptor(
                new BackendId(id),
                id,
                "1.0.0",
                capabilities,
                new[] { acceptedFormat });
        }

        public BackendDescriptor Descriptor { get; }

        public bool IsDisposed { get; private set; }

        public int CreatedSessionCount { get; private set; }

        public List<FakeInferenceSession> CreatedSessions { get; } = new List<FakeInferenceSession>();

        public List<SessionOptions> CreatedSessionOptions { get; } = new List<SessionOptions>();

        public TimeSpan RunDelay { get; set; }

        public bool SynchronousAsyncFallback { get; set; }

        public bool CanCreate(ModelArtifact artifact, BackendRequest request)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(FakeBackendProvider));
            return string.Equals(artifact.Format, _acceptedFormat, StringComparison.Ordinal);
        }

        public IInferenceSession CreateSession(
            ModelArtifact artifact,
            BackendRequest request,
            SessionOptions options)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(FakeBackendProvider));
            CreatedSessionCount++;
            var session = new FakeInferenceSession(Descriptor.Id, artifact, () => RunDelay, () => SynchronousAsyncFallback);
            CreatedSessions.Add(session);
            CreatedSessionOptions.Add(options);
            return session;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    internal sealed class FakeInferenceSession : IInferenceSession
    {
        private readonly Func<TimeSpan> _runDelay;
        private readonly Func<bool> _synchronousAsyncFallback;
        private int _runCount;

        public FakeInferenceSession(BackendId backendId, ModelArtifact artifact, Func<TimeSpan>? runDelay = null, Func<bool>? synchronousAsyncFallback = null)
        {
            BackendId = backendId;
            _runDelay = runDelay ?? (() => TimeSpan.Zero);
            _synchronousAsyncFallback = synchronousAsyncFallback ?? (() => false);
            Metadata = new ModelMetadata(
                artifact.ModelId,
                artifact.Format,
                new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(-1)) },
                new[] { new TensorDescriptor("output", TensorElementType.Float32, new TensorShape(-1)) });
        }

        public BackendId BackendId { get; }

        public ModelMetadata Metadata { get; }

        public bool IsDisposed { get; private set; }

        public int RunCount => Volatile.Read(ref _runCount);

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(FakeInferenceSession));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _runCount);
            TimeSpan delay = _runDelay();
            if (delay > TimeSpan.Zero) Thread.Sleep(delay);
            cancellationToken.ThrowIfCancellationRequested();
            return InferenceOutputs.Create("output", inputs[0].Tensor);
        }

        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            return _synchronousAsyncFallback() ? Task.FromResult(Run(inputs, cancellationToken)) : RunCoreAsync(inputs, cancellationToken);
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        private async Task<InferenceOutputs> RunCoreAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(FakeInferenceSession));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            Interlocked.Increment(ref _runCount);
            TimeSpan delay = _runDelay();
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return InferenceOutputs.Create("output", inputs[0].Tensor);
        }
    }
}
