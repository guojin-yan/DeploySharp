using System;
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
            return new FakeInferenceSession(Descriptor.Id, artifact);
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    internal sealed class FakeInferenceSession : IInferenceSession
    {
        public FakeInferenceSession(BackendId backendId, ModelArtifact artifact)
        {
            BackendId = backendId;
            Metadata = new ModelMetadata(
                artifact.ModelId,
                artifact.Format,
                new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(-1)) },
                new[] { new TensorDescriptor("output", TensorElementType.Float32, new TensorShape(-1)) });
        }

        public BackendId BackendId { get; }

        public ModelMetadata Metadata { get; }

        public bool IsDisposed { get; private set; }

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(FakeInferenceSession));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            cancellationToken.ThrowIfCancellationRequested();
            return InferenceOutputs.Create("output", inputs[0].Tensor);
        }

        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            return Task.FromResult(Run(inputs, cancellationToken));
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
