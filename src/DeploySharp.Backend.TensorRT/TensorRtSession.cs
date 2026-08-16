using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.TensorRtSharp;
using JYPPX.CudaSharp;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    internal sealed class TensorRtSession : IInferenceSession
    {
        private readonly object _lifetimeGate = new object();
        private readonly ModelArtifact _artifact;
        private readonly TensorRtLogger _logger;
        private readonly TensorRtRuntime _runtime;
        private readonly TensorRtEngine _engine;
        private readonly TensorRtExecutionContext _context;
        private readonly TensorRtInferenceBindings _bindings;
        private readonly CudaStream _stream;
        private readonly SemaphoreSlim _operationGate;
        private readonly CancellationTokenSource _disposeSource;
        private bool _disposed;

        public TensorRtSession(
            ModelArtifact artifact,
            TensorRtLogger logger,
            TensorRtRuntime runtime,
            TensorRtEngine engine,
            TensorRtExecutionContext context,
            TensorRtInferenceBindings bindings,
            int maximumConcurrency)
        {
            _artifact = artifact;
            _logger = logger;
            _runtime = runtime;
            _engine = engine;
            _context = context;
            _bindings = bindings;
            Metadata = CreateMetadata(artifact, bindings.Report);
            _stream = new CudaStream();
            _operationGate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            _disposeSource = new CancellationTokenSource();
        }

        public ModelMetadata Metadata { get; }

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            EnsureUsable();
            bool entered = false;
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeSource.Token))
            {
                try
                {
                    _operationGate.Wait(linked.Token);
                    entered = true;
                    EnsureUsable();
                    return RunCore(inputs, linked.Token);
                }
                catch (TensorRtBackendException)
                {
                    throw;
                }
                catch (OperationCanceledException exception)
                {
                    throw new TensorRtBackendException(TensorRtErrorCodes.InferenceFailed, "TensorRT inference was cancelled.", exception, _artifact.ModelId, operation: "run");
                }
                catch (Exception exception)
                {
                    throw new TensorRtBackendException(TensorRtErrorCodes.InferenceFailed, "TensorRT inference failed.", exception, _artifact.ModelId, operation: "run", technicalDetails: exception.GetType().FullName);
                }
                finally
                {
                    if (entered) _operationGate.Release();
                }
            }
        }

        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            // Host output materialization requires stream synchronization; expose the Core fallback without fabricating a worker task.
            return Task.FromResult(Run(inputs, cancellationToken));
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
                for (; acquired < 1; acquired++) _operationGate.Wait();
                Exception? disposeFailure = null;
                DisposeResource(_bindings, ref disposeFailure);
                DisposeResource(_stream, ref disposeFailure);
                DisposeResource(_context, ref disposeFailure);
                DisposeResource(_engine, ref disposeFailure);
                DisposeResource(_runtime, ref disposeFailure);
                DisposeResource(_logger, ref disposeFailure);
                if (disposeFailure != null) throw disposeFailure;
            }
            finally
            {
                for (int index = 0; index < acquired; index++) _operationGate.Release();
                _operationGate.Dispose();
                _disposeSource.Dispose();
            }
        }

        private InferenceOutputs RunCore(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            ValidateInputCollection(inputs);
            foreach (NamedTensor input in inputs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TensorRtEngineTensorBinding binding = _bindings.Report.GetTensor(input.Name);
                EnsureMode(binding, TensorRtIOMode.Input, input.Name);
                TensorRtDims runtimeShape = ToTensorRtShape(input.Tensor.Shape, input.Name);
                TensorRtBindingContract.ValidateInputShape(binding, runtimeShape, _artifact.ModelId);
                if (binding.EngineShape.Values.Any(value => value < 0)) _bindings.SetInputShape(input.Name, runtimeShape);
                CopyInput(binding, input.Tensor, runtimeShape);
            }

            TensorRtExecutionContextReadiness shapeReadiness = _bindings.GetReadiness(runShapeInference: true);
            if (shapeReadiness.ShapeInferenceMissingTensorCount.GetValueOrDefault(0) != 0)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.TensorInvalid,
                    "TensorRT could not infer all dynamic output shapes from the supplied inputs.",
                    modelId: _artifact.ModelId,
                    operation: "shape-inference",
                    technicalDetails: shapeReadiness.ToString());
            }

            IReadOnlyList<TensorRtEngineTensorBinding> outputs = _bindings.Report.GetOutputs();
            foreach (TensorRtEngineTensorBinding output in outputs)
            {
                TensorRtDims runtimeShape = ResolveOutputShape(output);
                _bindings.AllocateDeviceBuffer(output.Name, runtimeShape, output.EstimateByteSize(runtimeShape));
            }

            _bindings.BindAll();
            TensorRtExecutionContextReadiness readiness = _bindings.GetReadiness(runShapeInference: true);
            if (!readiness.IsReadyForEnqueue)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "TensorRT bindings are not ready for enqueue.", modelId: _artifact.ModelId, operation: "bind", technicalDetails: readiness.ToString());
            }

            _bindings.EnqueueAsync(_stream, synchronize: true, runShapeInference: false);
            var namedOutputs = new List<NamedTensor>(outputs.Count);
            foreach (TensorRtEngineTensorBinding output in outputs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TensorRtDims shape = _bindings.Buffers[output.Name].RuntimeShape ?? ResolveOutputShape(output);
                namedOutputs.Add(new NamedTensor(output.Name, ReadOutput(output, shape)));
            }

            return new InferenceOutputs(namedOutputs);
        }

        private void ValidateInputCollection(InferenceInputs inputs)
        {
            IReadOnlyList<TensorRtEngineTensorBinding> expected = _bindings.Report.GetInputs();
            if (inputs.Count != expected.Count)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "The input collection must contain every TensorRT input and no extras.", modelId: _artifact.ModelId, operation: "validate-inputs", technicalDetails: "expected=" + expected.Count + ";actual=" + inputs.Count);
            }

            foreach (NamedTensor input in inputs)
            {
                if (expected.All(value => !string.Equals(value.Name, input.Name, StringComparison.Ordinal)))
                {
                    throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "The input collection contains an unknown tensor name.", modelId: _artifact.ModelId, tensorName: input.Name, operation: "validate-inputs");
                }
            }
        }

        private void CopyInput(TensorRtEngineTensorBinding binding, ITensor tensor, TensorRtDims runtimeShape)
        {
            TensorElementType expected = ToCoreElementType(binding.DataType);
            if (expected == TensorElementType.Unknown || tensor.ElementType != expected)
            {
                throw UnsupportedType(binding.Name, binding.DataType, tensor.ElementType);
            }

            if (binding.DataType == TensorRtDataType.Float)
            {
                _bindings.CopyInputFromHost(binding.Name, (float[])tensor.Buffer, runtimeShape);
            }
            else
            {
                _bindings.CopyInputFromHost(binding.Name, ToBytes(tensor.Buffer, tensor.ElementType), runtimeShape);
            }
        }

        private ITensor ReadOutput(TensorRtEngineTensorBinding binding, TensorRtDims shape)
        {
            TensorElementType elementType = ToCoreElementType(binding.DataType);
            if (elementType == TensorElementType.Unknown) throw UnsupportedType(binding.Name, binding.DataType, TensorElementType.Unknown);
            TensorRtInferenceBuffer buffer = _bindings.Buffers[binding.Name];
            TensorRtBindingContract.ValidateOutputBuffer(binding, shape, buffer.SizeInBytes, _artifact.ModelId);
            byte[] bytes = buffer.Memory.ToArray(buffer.SizeInBytes);
            if (binding.DataType == TensorRtDataType.Float)
            {
                float[] values = new float[checked(bytes.Length / sizeof(float))];
                Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
                return new Tensor<float>(ToCoreShape(shape), values, TensorBufferOwnership.Transfer);
            }

            Array valuesArray = FromBytes(bytes, elementType);
            return CreateTensor(elementType, ToCoreShape(shape), valuesArray);
        }

        private TensorRtDims ResolveOutputShape(TensorRtEngineTensorBinding output)
        {
            TensorRtDims shape;
            try { shape = _context.GetTensorShape(output.Name); }
            catch { shape = output.EngineShape; }
            if (shape.Values.Length == 0 || shape.Values.Any(value => value <= 0))
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "TensorRT did not expose a concrete output shape after shape inference.", modelId: _artifact.ModelId, tensorName: output.Name, operation: "shape");
            }
            return shape;
        }

        private static TensorRtDims ToTensorRtShape(TensorShape shape, string tensorName)
        {
            if (shape == null || shape.IsDynamic) throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "Runtime tensors must use a fully static shape.", tensorName: tensorName, operation: "shape");
            long[] dimensions = shape.ToArray();
            var values = new int[dimensions.Length];
            for (int index = 0; index < dimensions.Length; index++)
            {
                if (dimensions[index] <= 0 || dimensions[index] > int.MaxValue) throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "Tensor dimensions must be positive and fit TensorRT's managed dimension range.", tensorName: tensorName, operation: "shape");
                values[index] = checked((int)dimensions[index]);
            }
            return new TensorRtDims(values);
        }

        private static TensorShape ToCoreShape(TensorRtDims shape)
        {
            return new TensorShape(shape.Values.Select(value => (long)value));
        }

        private static void EnsureMode(TensorRtEngineTensorBinding binding, TensorRtIOMode expected, string tensorName)
        {
            if (binding.IOMode != expected) throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "The TensorRT tensor is not an input.", tensorName: tensorName, operation: "validate-inputs");
        }

        private static TensorElementType ToCoreElementType(TensorRtDataType dataType)
        {
            return dataType switch
            {
                TensorRtDataType.Float => TensorElementType.Float32,
                TensorRtDataType.Int8 => TensorElementType.Int8,
                TensorRtDataType.UInt8 => TensorElementType.UInt8,
                TensorRtDataType.Bool => TensorElementType.Boolean,
                TensorRtDataType.Int32 => TensorElementType.Int32,
                TensorRtDataType.Int64 => TensorElementType.Int64,
                _ => TensorElementType.Unknown
            };
        }

        private static TensorRtBackendException UnsupportedType(string tensorName, TensorRtDataType nativeType, TensorElementType coreType)
        {
            return new TensorRtBackendException(TensorRtErrorCodes.ElementTypeUnsupported, "The TensorRT tensor element type is not supported by this managed adapter.", tensorName: tensorName, operation: "tensor-type", technicalDetails: "native=" + nativeType + ";core=" + coreType);
        }

        private static byte[] ToBytes(Array buffer, TensorElementType elementType)
        {
            if (elementType == TensorElementType.Boolean)
            {
                bool[] values = (bool[])buffer;
                var bytes = new byte[values.Length];
                for (int index = 0; index < values.Length; index++) bytes[index] = values[index] ? (byte)1 : (byte)0;
                return bytes;
            }

            int size = Buffer.ByteLength(buffer);
            var result = new byte[size];
            Buffer.BlockCopy(buffer, 0, result, 0, size);
            return result;
        }

        private static Array FromBytes(byte[] bytes, TensorElementType elementType)
        {
            Type elementTypeClr = elementType switch
            {
                TensorElementType.Int8 => typeof(sbyte),
                TensorElementType.UInt8 or TensorElementType.Boolean => typeof(byte),
                TensorElementType.Int32 => typeof(int),
                TensorElementType.Int64 => typeof(long),
                _ => throw new NotSupportedException("Tensor element type cannot be decoded from a TensorRT byte buffer.")
            };
            int elementSize = System.Runtime.InteropServices.Marshal.SizeOf(elementTypeClr);
            Array result = Array.CreateInstance(elementTypeClr, checked(bytes.Length / elementSize));
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            if (elementType == TensorElementType.Boolean)
            {
                bool[] booleans = new bool[result.Length];
                for (int index = 0; index < booleans.Length; index++) booleans[index] = ((byte[])result)[index] != 0;
                return booleans;
            }
            return result;
        }

        private static ITensor CreateTensor(TensorElementType elementType, TensorShape shape, Array values)
        {
            return elementType switch
            {
                TensorElementType.Int8 => new Tensor<sbyte>(shape, (sbyte[])values, TensorBufferOwnership.Transfer),
                TensorElementType.UInt8 => new Tensor<byte>(shape, (byte[])values, TensorBufferOwnership.Transfer),
                TensorElementType.Boolean => new Tensor<bool>(shape, (bool[])values, TensorBufferOwnership.Transfer),
                TensorElementType.Int32 => new Tensor<int>(shape, (int[])values, TensorBufferOwnership.Transfer),
                TensorElementType.Int64 => new Tensor<long>(shape, (long[])values, TensorBufferOwnership.Transfer),
                _ => throw new NotSupportedException("Tensor element type cannot be materialized by this managed adapter.")
            };
        }

        private static ModelMetadata CreateMetadata(ModelArtifact artifact, TensorRtEngineBindingReport report)
        {
            var inputs = new List<TensorDescriptor>();
            var outputs = new List<TensorDescriptor>();
            foreach (TensorRtEngineTensorBinding binding in report.Tensors)
            {
                TensorRtBindingContract.ValidateForSession(binding, artifact.ModelId);
                TensorElementType elementType = ToCoreElementType(binding.DataType);
                if (elementType == TensorElementType.Unknown)
                {
                    throw UnsupportedType(binding.Name, binding.DataType, elementType);
                }

                var descriptor = new TensorDescriptor(binding.Name, elementType, ToCoreShape(binding.EngineShape));
                if (binding.IOMode == TensorRtIOMode.Input) inputs.Add(descriptor); else if (binding.IOMode == TensorRtIOMode.Output) outputs.Add(descriptor);
            }
            return new ModelMetadata(artifact.ModelId, "tensorrt-engine", inputs, outputs);
        }

        private static void DisposeResource(IDisposable resource, ref Exception? firstFailure)
        {
            try { resource.Dispose(); }
            catch (Exception exception) when (firstFailure == null) { firstFailure = exception; }
            catch { }
        }

        private void EnsureUsable()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) throw new TensorRtBackendException(TensorRtErrorCodes.ObjectDisposed, "The TensorRT session has been disposed.", modelId: _artifact.ModelId, operation: "session");
            }
        }
    }
}
