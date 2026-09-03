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
        private Net? _network;
        private readonly Func<IReadOnlyList<TensorDescriptor>, Net>? _networkFactory;
        private TensorShape[]? _loadedInputShapes;
        private readonly OpenCvDnnModelContract _contract;
        private readonly string[] _outputNames;
        private float[]? _interleavedScratch;
        private int[]? _int64AuxiliaryScratch;
        private byte[]? _byteAuxiliaryScratch;
        private float[]? _floatOutputScratch;
        private int[]? _intOutputScratch;
        private byte[]? _byteOutputScratch;
        private Mat? _singleImageScratch;
        private Mat[]? _batchImageScratch;
        private Mat? _singleBlobScratch;
        private Mat? _batchBlobScratch;
        private readonly Dictionary<string, Mat> _auxiliaryScratch = new Dictionary<string, Mat>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly object _lifetimeGate = new object();
        private bool _disposed;

        internal OpenCvDnnSession(ModelArtifact artifact, Net network, OpenCvDnnModelContract contract)
        {
            _artifact = artifact;
            _network = network;
            _contract = contract;
            _outputNames = contract.Outputs.Select(value => value.Name).ToArray();
            Metadata = new CoreModelMetadata(artifact.ModelId, artifact.Format, contract.Inputs, contract.Outputs);
        }

        internal OpenCvDnnSession(ModelArtifact artifact, OpenCvDnnModelContract contract, Func<IReadOnlyList<TensorDescriptor>, Net> networkFactory)
        {
            _artifact = artifact;
            _contract = contract;
            _networkFactory = networkFactory ?? throw new ArgumentNullException(nameof(networkFactory));
            _outputNames = contract.Outputs.Select(value => value.Name).ToArray();
            Metadata = new CoreModelMetadata(artifact.ModelId, artifact.Format, contract.Inputs, contract.Outputs);
        }

        public CoreModelMetadata Metadata { get; }

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            EnsureUsable();
            bool entered = false;
            CancellationToken operationToken = _disposeSource.Token;
            CancellationTokenSource? linked = null;
            // The session disposal token is already the operation token on the
            // pooled path; don't allocate an equivalent linked CTS per call.
            if (cancellationToken.CanBeCanceled && cancellationToken != operationToken)
            {
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationToken);
                operationToken = linked.Token;
            }
            try
            {
                _operationGate.Wait(operationToken);
                entered = true;
                EnsureUsable();
                operationToken.ThrowIfCancellationRequested();
                return RunCore(inputs, operationToken);
            }
            catch (OperationCanceledException exception) { throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.Cancelled, "OpenCV DNN inference was cancelled at a managed boundary.", exception, _artifact.ModelId, operation: "run"); }
            catch (OpenCvDnnBackendException) { throw; }
            catch (Exception exception) { throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.InferenceFailed, "OpenCV DNN inference failed.", exception, _artifact.ModelId, operation: "run", technicalDetails: exception.Message); }
            finally
            {
                linked?.Dispose();
                if (entered) _operationGate.Release();
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
            try
            {
                try { _network?.Dispose(); }
                finally
                {
                    _singleImageScratch?.Dispose();
                    if (_batchImageScratch != null) foreach (Mat image in _batchImageScratch) image.Dispose();
                    _singleBlobScratch?.Dispose();
                    _batchBlobScratch?.Dispose();
                    foreach (Mat auxiliary in _auxiliaryScratch.Values) auxiliary.Dispose();
                    _auxiliaryScratch.Clear();
                }
            }
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
            var runtimeDescriptors = new List<TensorDescriptor>(_contract.Inputs.Count);
            foreach (TensorDescriptor descriptor in _contract.Inputs)
            {
                if (!inputs.TryGet(descriptor.Name, out ITensor? tensor)) throw TensorError("A required named input is missing.", descriptor.Name);
                ValidateTensor(tensor!, descriptor);
                runtimeDescriptors.Add(new TensorDescriptor(descriptor.Name, descriptor.ElementType, tensor!.Shape));
            }
            EnsureNetwork(runtimeDescriptors);
            foreach (TensorDescriptor descriptor in runtimeDescriptors)
            {
                ITensor tensor = inputs.GetRequired(descriptor.Name);
                Mat blob = CreateBlob(tensor, descriptor, out bool disposeBlob);
                try { _network!.SetInput(blob, descriptor.Name, 1d, null); }
                finally { if (disposeBlob) blob.Dispose(); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            // OCR det/rec/cls graphs expose one output. Use the single-output
            // native overload to avoid allocating a Mat[] and invoking the
            // multi-output dispatcher on every call.
            if (_outputNames.Length == 1)
            {
                Mat nativeOutput = _network!.Forward(_outputNames[0]);
                try { return new InferenceOutputs(new[] { ReadNativeOutput(nativeOutput, _contract.Outputs[0]) }); }
                finally { nativeOutput.Dispose(); }
            }

            Mat[] nativeOutputs = _network!.Forward(_outputNames);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (nativeOutputs.Length != _contract.Outputs.Count) throw TensorError("OpenCV DNN returned an unexpected output count.");
                var outputs = new List<NamedTensor>(nativeOutputs.Length);
                for (int index = 0; index < nativeOutputs.Length; index++) outputs.Add(ReadNativeOutput(nativeOutputs[index], _contract.Outputs[index]));
                return new InferenceOutputs(outputs);
            }
            finally { foreach (Mat output in nativeOutputs) output.Dispose(); }
        }

        private void EnsureNetwork(IReadOnlyList<TensorDescriptor> runtimeDescriptors)
        {
            if (_networkFactory == null)
            {
                if (_network != null) return;
                throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ModelLoadFailed, "The OpenCV DNN session has no loaded network.", modelId: _artifact.ModelId, operation: "load");
            }
            if (_network != null && _loadedInputShapes != null && _loadedInputShapes.Length == runtimeDescriptors.Count)
            {
                bool sameShape = true;
                for (int index = 0; index < runtimeDescriptors.Count; index++)
                {
                    if (!_loadedInputShapes[index].Equals(runtimeDescriptors[index].Shape)) { sameShape = false; break; }
                }
                if (sameShape) return;
            }
            try
            {
                Net replacement = _networkFactory(runtimeDescriptors);
                Net? previous = _network;
                _network = replacement;
                _loadedInputShapes = runtimeDescriptors.Select(value => value.Shape).ToArray();
                previous?.Dispose();
            }
            catch (OpenCvDnnBackendException) { throw; }
            catch (Exception exception) { throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ModelLoadFailed, "OpenCV DNN could not load the specialized dynamic-input network.", exception, _artifact.ModelId, operation: "specialize-input", technicalDetails: exception.Message); }
        }

        private NamedTensor ReadNativeOutput(Mat native, TensorDescriptor descriptor)
        {
            TensorShape outputShape = ResolveOutputShape(descriptor.Shape, native.ValueCount, descriptor.Name);
            int expected = checked((int)outputShape.GetElementCount());
            if (native.Empty || !native.HasData || native.ValueCount != expected) throw TensorError("An OpenCV DNN output differs from the contract.", descriptor.Name, "expectedElements=" + expected + ";actualElements=" + native.ValueCount + ";depth=" + native.Depth);
            if (descriptor.ElementType == TensorElementType.Boolean)
            {
                if (native.Depth != MatType.CV_32F && native.Depth != MatType.CV_8U && native.Depth != MatType.CV_32S && native.Depth != MatType.CV_Bool) throw TensorError("OpenCV returned an unsupported depth for a boolean output.", descriptor.Name, "depth=" + native.Depth);
                var values = new bool[expected];
                if (native.Depth == MatType.CV_32F)
                {
                    float[] numeric = GetFloatOutputScratch(expected);
                    Marshal.Copy(native.Data, numeric, 0, expected);
                    for (int valueIndex = 0; valueIndex < expected; valueIndex++) values[valueIndex] = numeric[valueIndex] != 0f;
                }
                else if (native.Depth == MatType.CV_32S)
                {
                    int[] numeric = GetIntOutputScratch(expected);
                    Marshal.Copy(native.Data, numeric, 0, expected);
                    for (int valueIndex = 0; valueIndex < expected; valueIndex++) values[valueIndex] = numeric[valueIndex] != 0;
                }
                else
                {
                    byte[] numeric = GetByteOutputScratch(expected);
                    Marshal.Copy(native.Data, numeric, 0, expected);
                    for (int valueIndex = 0; valueIndex < expected; valueIndex++) values[valueIndex] = numeric[valueIndex] != 0;
                }
                return new NamedTensor(descriptor.Name, new Tensor<bool>(outputShape, values, TensorBufferOwnership.Transfer));
            }

            if (descriptor.ElementType == TensorElementType.Int32)
            {
                if (native.Depth != MatType.CV_32S) throw TensorError("OpenCV returned a non-int32 output for an int32 contract.", descriptor.Name, "depth=" + native.Depth);
                int[] values = new int[expected];
                Marshal.Copy(native.Data, values, 0, expected);
                return new NamedTensor(descriptor.Name, new Tensor<int>(outputShape, values, TensorBufferOwnership.Transfer));
            }

            if (descriptor.ElementType == TensorElementType.Float64)
            {
                if (native.Depth != MatType.CV_64F) throw TensorError("OpenCV returned a non-float64 output for a float64 contract.", descriptor.Name, "depth=" + native.Depth);
                var values = new double[expected];
                Marshal.Copy(native.Data, values, 0, expected);
                return new NamedTensor(descriptor.Name, new Tensor<double>(outputShape, values, TensorBufferOwnership.Transfer));
            }

            if (descriptor.ElementType == TensorElementType.UInt8 || descriptor.ElementType == TensorElementType.Int8)
            {
                if (native.Depth != MatType.CV_8U && native.Depth != MatType.CV_8S) throw TensorError("OpenCV returned a non-8-bit output for an int8/uint8 contract.", descriptor.Name, "depth=" + native.Depth);
                if (descriptor.ElementType == TensorElementType.UInt8)
                {
                    var values = new byte[expected];
                    Marshal.Copy(native.Data, values, 0, expected);
                    return new NamedTensor(descriptor.Name, new Tensor<byte>(outputShape, values, TensorBufferOwnership.Transfer));
                }
                byte[] numeric = GetByteOutputScratch(expected);
                Marshal.Copy(native.Data, numeric, 0, expected);
                var signed = new sbyte[expected];
                for (int valueIndex = 0; valueIndex < expected; valueIndex++) signed[valueIndex] = unchecked((sbyte)numeric[valueIndex]);
                return new NamedTensor(descriptor.Name, new Tensor<sbyte>(outputShape, signed, TensorBufferOwnership.Transfer));
            }

            if (descriptor.ElementType == TensorElementType.Int64)
            {
                if (native.Depth != MatType.CV_32S) throw TensorError("OpenCV returned a non-int32 output for an int64 contract.", descriptor.Name, "depth=" + native.Depth);
                int[] numeric = GetIntOutputScratch(expected);
                Marshal.Copy(native.Data, numeric, 0, expected);
                var values = new long[expected];
                for (int valueIndex = 0; valueIndex < expected; valueIndex++) values[valueIndex] = numeric[valueIndex];
                return new NamedTensor(descriptor.Name, new Tensor<long>(outputShape, values, TensorBufferOwnership.Transfer));
            }

            if (native.Depth != MatType.CV_32F) throw TensorError("OpenCV returned a non-float output for a float32 contract.", descriptor.Name, "depth=" + native.Depth);
            var floatValues = new float[expected];
            Marshal.Copy(native.Data, floatValues, 0, expected);
            return new NamedTensor(descriptor.Name, new Tensor<float>(outputShape, floatValues, TensorBufferOwnership.Transfer));
        }

        private static TensorShape ResolveOutputShape(TensorShape pattern, long elementCount, string tensorName)
        {
            if (!pattern.IsDynamic) return pattern;
            if (elementCount <= 0 || elementCount > int.MaxValue) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.TensorInvalid, "OpenCV returned a dynamic output whose element count cannot fit a managed tensor.", tensorName: tensorName, operation: "output-shape", technicalDetails: "elements=" + elementCount);
            long[] dimensions = pattern.ToArray();
            int wildcard = -1;
            long fixedElements = 1;
            for (int index = 0; index < dimensions.Length; index++)
            {
                long dimension = dimensions[index];
                if (dimension < 0)
                {
                    if (wildcard >= 0) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.TensorInvalid, "OpenCV dynamic output resolution requires exactly one wildcard dimension.", tensorName: tensorName, operation: "output-shape");
                    wildcard = index;
                }
                else fixedElements = checked(fixedElements * dimension);
            }
            if (wildcard < 0 || fixedElements <= 0 || elementCount % fixedElements != 0) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.TensorInvalid, "OpenCV returned an output whose element count does not match the dynamic contract.", tensorName: tensorName, operation: "output-shape", technicalDetails: "pattern=" + pattern + ";elements=" + elementCount);
            long resolved = elementCount / fixedElements;
            if (resolved <= 0) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.TensorInvalid, "OpenCV resolved a dynamic output dimension to zero.", tensorName: tensorName, operation: "output-shape");
            dimensions[wildcard] = resolved;
            return new TensorShape(dimensions);
        }

        private Mat CreateBlob(ITensor tensor, TensorDescriptor descriptor, out bool disposeBlob)
        {
            disposeBlob = false;
            ValidateTensor(tensor, descriptor);
            if (!_contract.IsImageInput(descriptor.Name)) return CreateAuxiliaryBlob(tensor, descriptor);
            float[] source = tensor.Buffer as float[] ?? throw TensorError("The float32 tensor has a mismatched CLR buffer type.", descriptor.Name);
            int channels = checked((int)descriptor.Shape[1]);
            int height = checked((int)descriptor.Shape[2]);
            int width = checked((int)descriptor.Shape[3]);
            int batch = checked((int)descriptor.Shape[0]);
            int matType = channels == 1 ? MatType.CV_32FC1 : channels == 3 ? MatType.CV_32FC3 : MatType.CV_32FC4;
            // The wrapper exposes image-to-blob conversion rather than an N-D Mat
            // constructor. Reuse one interleaved scratch buffer, then let OpenCV build
            // the NCHW blob for the complete batch.
            int imageElements = checked(channels * height * width);
            int planeElements = checked(height * width);
            // A single-channel NCHW image is already contiguous HWC data, so
            // avoid the interleave buffer and its full-tensor copy entirely.
            float[] interleaved = source;
            if (channels != 1)
            {
                interleaved = _interleavedScratch != null && _interleavedScratch.Length == source.Length
                    ? _interleavedScratch
                    : (_interleavedScratch = new float[source.Length]);
                for (int batchIndex = 0; batchIndex < batch; batchIndex++)
                {
                    int batchOffset = batchIndex * imageElements;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int plane = batchOffset + (channel * planeElements);
                        for (int pixel = 0; pixel < planeElements; pixel++) interleaved[batchOffset + (pixel * channels) + channel] = source[plane + pixel];
                    }
                }
            }

            if (batch == 1)
            {
                // A session normally receives one static input contract, but a provider
                // can still be reused across compatible contracts with different shapes.
                // Recreate the native scratch image when its geometry/type changes;
                // writing a larger tensor into an old Mat would otherwise overrun native
                // storage while silently corrupting the next inference.
                if (_singleImageScratch == null || _singleImageScratch.Rows != height || _singleImageScratch.Cols != width || _singleImageScratch.Type != matType)
                {
                    _singleImageScratch?.Dispose();
                    _singleImageScratch = new Mat(height, width, matType);
                }
                Marshal.Copy(interleaved, 0, _singleImageScratch.Data, imageElements);
                _singleBlobScratch ??= new Mat();
                DnnOperations.BlobFromImage(_singleImageScratch, _singleBlobScratch, 1d, new Size(width, height), new Scalar(0d), false, false, MatType.CV_32F);
                return _singleBlobScratch;
            }

            bool recreateBatchScratch = _batchImageScratch == null || _batchImageScratch.Length != batch;
            if (!recreateBatchScratch)
            {
                for (int batchIndex = 0; batchIndex < _batchImageScratch!.Length; batchIndex++)
                {
                    Mat image = _batchImageScratch[batchIndex];
                    if (image.Rows != height || image.Cols != width || image.Type != matType)
                    {
                        recreateBatchScratch = true;
                        break;
                    }
                }
            }
            if (recreateBatchScratch)
            {
                if (_batchImageScratch != null) foreach (Mat image in _batchImageScratch) image.Dispose();
                _batchImageScratch = new Mat[batch];
                for (int batchIndex = 0; batchIndex < batch; batchIndex++) _batchImageScratch[batchIndex] = new Mat(height, width, matType);
            }
            Mat[] batchImages = _batchImageScratch!;
            for (int batchIndex = 0; batchIndex < batch; batchIndex++) Marshal.Copy(interleaved, batchIndex * imageElements, batchImages[batchIndex].Data, imageElements);
            _batchBlobScratch ??= new Mat();
            DnnOperations.BlobFromImages(batchImages, _batchBlobScratch, 1d, new Size(width, height), new Scalar(0d), false, false, MatType.CV_32F);
            return _batchBlobScratch;
        }

        private Mat CreateAuxiliaryBlob(ITensor tensor, TensorDescriptor descriptor)
        {
            long elements = descriptor.Shape.GetElementCount();
            int rows = descriptor.Shape.Rank == 0 ? 1 : descriptor.Shape.Rank == 1 ? 1 : checked((int)descriptor.Shape[0]);
            int cols = descriptor.Shape.Rank == 0 ? 1 : checked((int)(elements / rows));
            int type = descriptor.ElementType == TensorElementType.Int8 ? MatType.CV_8S : descriptor.ElementType == TensorElementType.UInt8 ? MatType.CV_8U : descriptor.ElementType == TensorElementType.Int32 || descriptor.ElementType == TensorElementType.Int64 ? MatType.CV_32S : descriptor.ElementType == TensorElementType.Float64 ? MatType.CV_64F : MatType.CV_32F;
            if (!_auxiliaryScratch.TryGetValue(descriptor.Name, out Mat? mat) || mat.Rows != rows || mat.Cols != cols || mat.Type != type)
            {
                mat?.Dispose();
                mat = new Mat(rows, cols, type);
                _auxiliaryScratch[descriptor.Name] = mat;
            }
            if (descriptor.ElementType == TensorElementType.UInt8)
            {
                if (tensor.Buffer is not byte[] values) throw TensorError("The uint8 tensor has a mismatched CLR buffer type.", descriptor.Name);
                Marshal.Copy(values, 0, mat.Data, values.Length);
            }
            else if (descriptor.ElementType == TensorElementType.Int8)
            {
                if (tensor.Buffer is not sbyte[] values) throw TensorError("The int8 tensor has a mismatched CLR buffer type.", descriptor.Name);
                byte[] bytes = _byteAuxiliaryScratch != null && _byteAuxiliaryScratch.Length >= values.Length ? _byteAuxiliaryScratch : (_byteAuxiliaryScratch = new byte[values.Length]);
                for (int index = 0; index < values.Length; index++) bytes[index] = unchecked((byte)values[index]);
                Marshal.Copy(bytes, 0, mat.Data, values.Length);
            }
            else if (descriptor.ElementType == TensorElementType.Float64)
            {
                if (tensor.Buffer is not double[] values) throw TensorError("The float64 tensor has a mismatched CLR buffer type.", descriptor.Name);
                Marshal.Copy(values, 0, mat.Data, values.Length);
            }
            else if (descriptor.ElementType == TensorElementType.Int32)
            {
                if (tensor.Buffer is not int[] values) throw TensorError("The int32 tensor has a mismatched CLR buffer type.", descriptor.Name);
                Marshal.Copy(values, 0, mat.Data, values.Length);
            }
            else if (descriptor.ElementType == TensorElementType.Int64)
            {
                if (tensor.Buffer is not long[] values) throw TensorError("The int64 tensor has a mismatched CLR buffer type.", descriptor.Name);
                int[] narrowed = _int64AuxiliaryScratch != null && _int64AuxiliaryScratch.Length >= values.Length
                    ? _int64AuxiliaryScratch
                    : (_int64AuxiliaryScratch = new int[values.Length]);
                for (int index = 0; index < values.Length; index++)
                {
                    long value = values[index];
                    if (value < int.MinValue || value > int.MaxValue) throw TensorError("The int64 auxiliary tensor contains a value outside OpenCV CV_32S range.", descriptor.Name, "index=" + index + ";value=" + value);
                    narrowed[index] = (int)value;
                }
                Marshal.Copy(narrowed, 0, mat.Data, values.Length);
            }
            else
            {
                if (tensor.Buffer is not float[] values) throw TensorError("The float32 tensor has a mismatched CLR buffer type.", descriptor.Name);
                Marshal.Copy(values, 0, mat.Data, values.Length);
            }
            return mat;
        }

        private void ValidateTensor(ITensor tensor, TensorDescriptor descriptor)
        {
            if (tensor == null || tensor.ElementType != descriptor.ElementType || tensor.Shape.Rank != descriptor.Shape.Rank) throw TensorError("The input tensor does not match the OpenCV DNN input contract.", descriptor.Name);
            for (int index = 0; index < descriptor.Shape.Rank; index++)
            {
                long expected = descriptor.Shape[index];
                if (expected > 0 && tensor.Shape[index] != expected) throw TensorError("The input tensor does not match the fixed OpenCV DNN input dimensions.", descriptor.Name, "dimension=" + index + ";expected=" + expected + ";actual=" + tensor.Shape[index]);
                if (tensor.Shape[index] <= 0) throw TensorError("The runtime input tensor must have positive dimensions.", descriptor.Name);
            }
            if (tensor.Length != tensor.Shape.GetElementCount()) throw TensorError("The input tensor element count is inconsistent with its runtime shape.", descriptor.Name);
            if (_contract.IsImageInput(descriptor.Name) && tensor.Buffer is not float[]) throw TensorError("The image input tensor has a mismatched CLR buffer type.", descriptor.Name);
            if (!_contract.IsImageInput(descriptor.Name) && descriptor.ElementType == TensorElementType.Float32 && tensor.Buffer is not float[]) throw TensorError("The auxiliary float32 tensor has a mismatched CLR buffer type.", descriptor.Name);
            if (!_contract.IsImageInput(descriptor.Name) && descriptor.ElementType == TensorElementType.Int8 && tensor.Buffer is not sbyte[]) throw TensorError("The auxiliary int8 tensor has a mismatched CLR buffer type.", descriptor.Name);
            if (!_contract.IsImageInput(descriptor.Name) && descriptor.ElementType == TensorElementType.UInt8 && tensor.Buffer is not byte[]) throw TensorError("The auxiliary uint8 tensor has a mismatched CLR buffer type.", descriptor.Name);
            if (!_contract.IsImageInput(descriptor.Name) && descriptor.ElementType == TensorElementType.Float64 && tensor.Buffer is not double[]) throw TensorError("The auxiliary float64 tensor has a mismatched CLR buffer type.", descriptor.Name);
            if (!_contract.IsImageInput(descriptor.Name) && descriptor.ElementType == TensorElementType.Int32 && tensor.Buffer is not int[]) throw TensorError("The auxiliary int32 tensor has a mismatched CLR buffer type.", descriptor.Name);
            if (!_contract.IsImageInput(descriptor.Name) && descriptor.ElementType == TensorElementType.Int64 && tensor.Buffer is not long[]) throw TensorError("The auxiliary int64 tensor has a mismatched CLR buffer type.", descriptor.Name);
        }

        private float[] GetFloatOutputScratch(int length)
            => _floatOutputScratch != null && _floatOutputScratch.Length >= length ? _floatOutputScratch : (_floatOutputScratch = new float[length]);

        private int[] GetIntOutputScratch(int length)
            => _intOutputScratch != null && _intOutputScratch.Length >= length ? _intOutputScratch : (_intOutputScratch = new int[length]);

        private byte[] GetByteOutputScratch(int length)
            => _byteOutputScratch != null && _byteOutputScratch.Length >= length ? _byteOutputScratch : (_byteOutputScratch = new byte[length]);

        private OpenCvDnnBackendException TensorError(string message, string? name = null, string? details = null) => new OpenCvDnnBackendException(OpenCvDnnErrorCodes.TensorInvalid, message, modelId: _artifact.ModelId, tensorName: name, operation: "tensor-contract", technicalDetails: details);
        private void EnsureUsable() { lock (_lifetimeGate) { if (_disposed) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ObjectDisposed, "The OpenCV DNN session has been disposed.", modelId: _artifact.ModelId, operation: "session"); } }
    }
}
