using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.Dnn;
using DnnOperations = JYPPX.OpenCvSharp.Dnn.Cv2;
using CoreModelMetadata = JYPPX.DeploySharp.Models.ModelMetadata;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    internal sealed class OpenCvDnnSession : IInferenceSession
    {
        private readonly ModelArtifact _artifact;
        private readonly Net _network;
        private readonly OpenCvDnnModelContract _contract;
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly object _lifetimeGate = new object();
        private bool _disposed;

        internal OpenCvDnnSession(ModelArtifact artifact, Net network, OpenCvDnnModelContract contract)
        {
            _artifact = artifact;
            _network = network;
            _contract = contract;
            Metadata = new CoreModelMetadata(artifact.ModelId, artifact.Format, contract.Inputs, contract.Outputs);
        }

        public CoreModelMetadata Metadata { get; }

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            EnsureUsable();
            bool entered = false;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeSource.Token))
            {
                try
                {
                    _operationGate.Wait(linked.Token);
                    entered = true;
                    EnsureUsable();
                    linked.Token.ThrowIfCancellationRequested();
                    return RunCore(inputs, linked.Token);
                }
                catch (OperationCanceledException exception) { throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.Cancelled, "OpenCV DNN inference was cancelled at a managed boundary.", exception, _artifact.ModelId, operation: "run"); }
                catch (OpenCvDnnBackendException) { throw; }
                catch (Exception exception) { throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.InferenceFailed, "OpenCV DNN inference failed.", exception, _artifact.ModelId, operation: "run", technicalDetails: exception.Message); }
                finally { if (entered) _operationGate.Release(); }
            }
        }

        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
            => Task.FromResult(Run(inputs, cancellationToken));

        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }
            _operationGate.Wait();
            try { _network.Dispose(); }
            finally
            {
                _operationGate.Release();
                _operationGate.Dispose();
                _disposeSource.Dispose();
            }
        }

        private InferenceOutputs RunCore(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs.Count != _contract.Inputs.Count) throw TensorError("The input collection must contain every contract input and no extras.");
            var byName = inputs.ToDictionary(value => value.Name, StringComparer.Ordinal);
            foreach (TensorDescriptor descriptor in _contract.Inputs)
            {
                if (!byName.TryGetValue(descriptor.Name, out NamedTensor? input)) throw TensorError("A required named input is missing.", descriptor.Name);
                using (Mat blob = CreateBlob(input.Tensor, descriptor)) _network.SetInput(blob, descriptor.Name, 1d, null);
            }
            cancellationToken.ThrowIfCancellationRequested();
            string[] outputNames = _contract.Outputs.Select(value => value.Name).ToArray();
            Mat[] nativeOutputs = _network.Forward(outputNames);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (nativeOutputs.Length != _contract.Outputs.Count) throw TensorError("OpenCV DNN returned an unexpected output count.");
                var outputs = new List<NamedTensor>(nativeOutputs.Length);
                for (int index = 0; index < nativeOutputs.Length; index++)
                {
                    TensorDescriptor descriptor = _contract.Outputs[index];
                    Mat native = nativeOutputs[index];
                    int expected = checked((int)descriptor.Shape.GetElementCount());
                    if (native.Empty || !native.HasData || native.Depth != MatType.CV_32F || native.ValueCount != expected) throw TensorError("An OpenCV DNN output differs from the static float32 contract.", descriptor.Name, "expectedElements=" + expected + ";actualElements=" + native.ValueCount + ";depth=" + native.Depth);
                    var values = new float[expected];
                    Marshal.Copy(native.Data, values, 0, expected);
                    outputs.Add(new NamedTensor(descriptor.Name, new Tensor<float>(descriptor.Shape, values, TensorBufferOwnership.Transfer)));
                }
                return new InferenceOutputs(outputs);
            }
            finally { foreach (Mat output in nativeOutputs) output.Dispose(); }
        }

        private Mat CreateBlob(ITensor tensor, TensorDescriptor descriptor)
        {
            ValidateTensor(tensor, descriptor);
            float[] source = tensor.Buffer as float[] ?? throw TensorError("The float32 tensor has a mismatched CLR buffer type.", descriptor.Name);
            int channels = checked((int)descriptor.Shape[1]);
            int height = checked((int)descriptor.Shape[2]);
            int width = checked((int)descriptor.Shape[3]);
            int matType = channels == 1 ? MatType.CV_32FC1 : channels == 3 ? MatType.CV_32FC3 : MatType.CV_32FC4;
            var interleaved = new float[source.Length];
            for (int channel = 0; channel < channels; channel++)
            {
                int plane = channel * height * width;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++) interleaved[((y * width + x) * channels) + channel] = source[plane + (y * width) + x];
            }
            using (var image = new Mat(height, width, matType))
            {
                Marshal.Copy(interleaved, 0, image.Data, interleaved.Length);
                return DnnOperations.BlobFromImage(image, 1d, new Size(width, height), new Scalar(0d), false, false, MatType.CV_32F);
            }
        }

        private void ValidateTensor(ITensor tensor, TensorDescriptor descriptor)
        {
            if (tensor == null || tensor.ElementType != TensorElementType.Float32 || !tensor.Shape.Equals(descriptor.Shape) || tensor.Length != descriptor.Shape.GetElementCount()) throw TensorError("The input tensor does not match the exact static float32 NCHW contract.", descriptor.Name);
        }

        private OpenCvDnnBackendException TensorError(string message, string? name = null, string? details = null) => new OpenCvDnnBackendException(OpenCvDnnErrorCodes.TensorInvalid, message, modelId: _artifact.ModelId, tensorName: name, operation: "tensor-contract", technicalDetails: details);
        private void EnsureUsable() { lock (_lifetimeGate) { if (_disposed) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ObjectDisposed, "The OpenCV DNN session has been disposed.", modelId: _artifact.ModelId, operation: "session"); } }
    }
}
