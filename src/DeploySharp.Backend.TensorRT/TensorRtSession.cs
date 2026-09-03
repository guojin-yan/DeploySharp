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
    internal sealed class TensorRtSession : ITensorRtDeviceInferenceSession, ISequenceArgMaxInferenceSession
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
        private readonly List<NamedTensor> _namedOutputScratch = new List<NamedTensor>();
        private readonly string? _cudaTargetArchitecture;
        private readonly bool _cacheImmutableHostInputsOnDevice;
        private WeakReference<InferenceInputs>? _cachedHostInputs;
        private TensorRtCudaCompiledKernel? _ctcTraceKernel;
        private CudaMemory? _ctcClassIndices;
        private CudaMemory? _ctcConfidences;
        private CudaMemory? _ctcInvalidOffsets;
        private bool _disposed;
        private readonly int _deviceOrdinal;

        public TensorRtSession(
            ModelArtifact artifact,
            TensorRtLogger logger,
            TensorRtRuntime runtime,
            TensorRtEngine engine,
            TensorRtExecutionContext context,
            TensorRtInferenceBindings bindings,
            int maximumConcurrency,
            string? cudaTargetArchitecture,
            bool cacheImmutableHostInputsOnDevice)
        {
            _artifact = artifact;
            _logger = logger;
            _runtime = runtime;
            _engine = engine;
            _context = context;
            _bindings = bindings;
            Metadata = CreateMetadata(artifact, bindings.Report);
            _stream = new CudaStream();
            _deviceOrdinal = ResolveStreamDeviceOrdinal(_stream);
            _operationGate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            _disposeSource = new CancellationTokenSource();
            _cudaTargetArchitecture = cudaTargetArchitecture;
            _cacheImmutableHostInputsOnDevice = cacheImmutableHostInputsOnDevice;
        }

        public ModelMetadata Metadata { get; }

        public int DeviceOrdinal => _deviceOrdinal;

        public bool IsSequenceArgMaxSupported => _cudaTargetArchitecture != null;

        public TensorRtDeviceInferenceExecution RunDevice(
            IReadOnlyList<TensorRtDeviceTensor> inputs,
            IReadOnlyList<TensorRtDeviceTensor> outputs,
            CudaStream stream,
            CancellationToken cancellationToken = default)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (outputs == null) throw new ArgumentNullException(nameof(outputs));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            EnsureUsable();
            bool entered = false;
            bool enqueued = false;
            CancellationToken operationToken = _disposeSource.Token;
            CancellationTokenSource? linked = null;
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
                ValidateDeviceStream(stream);
                InvalidateHostInputCache();
                IReadOnlyList<TensorRtEngineTensorBinding> expectedInputs = _bindings.Report.GetInputs();
                IReadOnlyList<TensorRtEngineTensorBinding> expectedOutputs = _bindings.Report.GetOutputs();
                ValidateDeviceTensorCollection(inputs, expectedInputs, TensorRtIOMode.Input);
                ValidateDeviceTensorCollection(outputs, expectedOutputs, TensorRtIOMode.Output);

                foreach (TensorRtDeviceTensor input in inputs)
                {
                    TensorRtDims runtimeShape = ToTensorRtShape(input.Shape, input.Name);
                    TensorRtEngineTensorBinding binding = _bindings.Report.GetTensor(input.Name);
                    TensorRtBindingContract.ValidateInputShape(binding, runtimeShape, _artifact.ModelId);
                    ValidateDeviceType(binding, input);
                    if (binding.EngineShape.Values.Any(value => value < 0)) _bindings.SetInputShape(input.Name, runtimeShape);
                    _bindings.UseDeviceBuffer(input.Name, input.Memory, runtimeShape);
                }

                foreach (TensorRtDeviceTensor output in outputs)
                {
                    TensorRtEngineTensorBinding binding = _bindings.Report.GetTensor(output.Name);
                    ValidateDeviceType(binding, output);
                    _bindings.UseDeviceBuffer(output.Name, output.Memory, ToTensorRtShape(output.Shape, output.Name));
                }

                TensorRtExecutionContextReadiness shapeReadiness = _bindings.GetReadiness(runShapeInference: true);
                if (shapeReadiness.ShapeInferenceMissingTensorCount.GetValueOrDefault(0) != 0)
                {
                    throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "TensorRT could not infer all dynamic output shapes from the supplied device inputs.", modelId: _artifact.ModelId, operation: "shape-inference", technicalDetails: shapeReadiness.ToString());
                }

                _bindings.BindAll();
                TensorRtExecutionContextReadiness readiness = _bindings.GetReadiness(runShapeInference: true);
                if (!readiness.IsReadyForEnqueue)
                {
                    throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "TensorRT device bindings are not ready for enqueue.", modelId: _artifact.ModelId, operation: "bind", technicalDetails: readiness.ToString());
                }

                _bindings.EnqueueAsync(stream, synchronize: false, runShapeInference: false);
                enqueued = true;
                var resolvedOutputs = new List<TensorRtDeviceTensor>(outputs.Count);
                foreach (TensorRtDeviceTensor output in outputs)
                {
                    TensorRtEngineTensorBinding binding = _bindings.Report.GetTensor(output.Name);
                    TensorRtDims resolvedShape = ResolveOutputShapeAfterEnqueue(binding);
                    resolvedOutputs.Add(new TensorRtDeviceTensor(output.Name, output.ElementType, ToCoreShape(resolvedShape), output.Memory));
                }

                entered = false;
                return new TensorRtDeviceInferenceExecution(stream, resolvedOutputs, ReleaseDeviceOperation);
            }
            catch (TensorRtBackendException)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.InferenceFailed, "TensorRT device inference was cancelled.", exception, _artifact.ModelId, operation: "device-run");
            }
            catch (Exception exception)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.InferenceFailed, "TensorRT device inference failed.", exception, _artifact.ModelId, operation: "device-run", technicalDetails: exception.GetType().FullName);
            }
            finally
            {
                if (entered)
                {
                    if (enqueued)
                    {
                        try { stream.Synchronize(); }
                        catch { }
                    }
                    _operationGate.Release();
                }
                linked?.Dispose();
            }
        }

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            EnsureUsable();
            bool entered = false;
            CancellationToken operationToken = _disposeSource.Token;
            CancellationTokenSource? linked = null;
            // Pooled callers pass the disposal token directly. Linking an
            // identical token adds avoidable allocation to every enqueue.
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
                return RunCore(inputs, operationToken);
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
                linked?.Dispose();
            }
        }

        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            // Host output materialization requires stream synchronization; expose the Core fallback without fabricating a worker task.
            return Task.FromResult(Run(inputs, cancellationToken));
        }

        public SequenceArgMaxResult RunSequenceArgMax(InferenceInputs inputs, SequenceArgMaxRequest request, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!IsSequenceArgMaxSupported) throw new NotSupportedException("TensorRT CUDA sequence argmax requires an explicit CUDA target architecture.");
            EnsureUsable();
            bool entered = false;
            CancellationToken operationToken = _disposeSource.Token;
            CancellationTokenSource? linked = null;
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
                return RunSequenceArgMaxCore(inputs, request, operationToken);
            }
            catch (TensorRtBackendException)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.InferenceFailed, "TensorRT sequence argmax inference was cancelled.", exception, _artifact.ModelId, operation: "sequence-argmax");
            }
            catch (Exception exception)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.InferenceFailed, "TensorRT sequence argmax inference failed.", exception, _artifact.ModelId, request.OutputName, operation: "sequence-argmax", technicalDetails: exception.GetType().FullName);
            }
            finally
            {
                if (entered) _operationGate.Release();
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
                for (; acquired < 1; acquired++) _operationGate.Wait();
                Exception? disposeFailure = null;
                DisposeResource(_ctcInvalidOffsets, ref disposeFailure);
                DisposeResource(_ctcConfidences, ref disposeFailure);
                DisposeResource(_ctcClassIndices, ref disposeFailure);
                DisposeResource(_ctcTraceKernel, ref disposeFailure);
                InvalidateHostInputCache();
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
            PrepareHostInputs(inputs, cancellationToken);

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
                TensorRtDims runtimeShape = ResolveOutputShape(output, allowMaximumForDataDependent: true);
                _bindings.AllocateDeviceBuffer(output.Name, runtimeShape, output.EstimateByteSize(runtimeShape));
            }

            _bindings.BindAll();
            TensorRtExecutionContextReadiness readiness = _bindings.GetReadiness(runShapeInference: true);
            if (!readiness.IsReadyForEnqueue)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "TensorRT bindings are not ready for enqueue.", modelId: _artifact.ModelId, operation: "bind", technicalDetails: readiness.ToString());
            }

            _bindings.EnqueueAsync(_stream, synchronize: true, runShapeInference: false);
            _namedOutputScratch.Clear();
            try
            {
                foreach (TensorRtEngineTensorBinding output in outputs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TensorRtDims shape = ResolveOutputShapeAfterEnqueue(output);
                    _namedOutputScratch.Add(new NamedTensor(output.Name, ReadOutput(output, shape)));
                }

                // InferenceOutputs copies the ordered collection into its own immutable
                // lookup, so the session-owned list can be cleared and reused on the
                // next call without exposing mutable state to the caller.
                return new InferenceOutputs(_namedOutputScratch);
            }
            finally { _namedOutputScratch.Clear(); }
        }

        private SequenceArgMaxResult RunSequenceArgMaxCore(InferenceInputs inputs, SequenceArgMaxRequest request, CancellationToken cancellationToken)
        {
            PrepareHostInputs(inputs, cancellationToken);

            TensorRtExecutionContextReadiness shapeReadiness = _bindings.GetReadiness(runShapeInference: true);
            if (shapeReadiness.ShapeInferenceMissingTensorCount.GetValueOrDefault(0) != 0)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "TensorRT could not infer all dynamic output shapes for sequence argmax.", modelId: _artifact.ModelId, tensorName: request.OutputName, operation: "sequence-shape-inference", technicalDetails: shapeReadiness.ToString());
            }

            IReadOnlyList<TensorRtEngineTensorBinding> outputs = _bindings.Report.GetOutputs();
            TensorRtEngineTensorBinding? reducedOutput = outputs.FirstOrDefault(value => string.Equals(value.Name, request.OutputName, StringComparison.Ordinal));
            if (reducedOutput == null) throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "The requested sequence output is not present in the TensorRT engine.", modelId: _artifact.ModelId, tensorName: request.OutputName, operation: "sequence-output");
            if (reducedOutput.DataType != TensorRtDataType.Float) throw new TensorRtBackendException(TensorRtErrorCodes.ElementTypeUnsupported, "TensorRT sequence argmax currently requires a Float32 output.", modelId: _artifact.ModelId, tensorName: request.OutputName, operation: "sequence-output-type", technicalDetails: reducedOutput.DataType.ToString());
            foreach (TensorRtEngineTensorBinding output in outputs)
            {
                TensorRtDims runtimeShape = ResolveOutputShape(output, allowMaximumForDataDependent: true);
                _bindings.AllocateDeviceBuffer(output.Name, runtimeShape, output.EstimateByteSize(runtimeShape));
            }

            EnsureCtcTraceKernel(cancellationToken);
            _bindings.BindAll();
            TensorRtExecutionContextReadiness readiness = _bindings.GetReadiness(runShapeInference: true);
            if (!readiness.IsReadyForEnqueue)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "TensorRT bindings are not ready for sequence argmax enqueue.", modelId: _artifact.ModelId, tensorName: request.OutputName, operation: "sequence-bind", technicalDetails: readiness.ToString());
            }

            bool enqueued = false;
            try
            {
                _bindings.EnqueueAsync(_stream, synchronize: false, runShapeInference: false);
                enqueued = true;
                TensorRtDims shape = ResolveOutputShapeAfterEnqueue(reducedOutput);
                if (shape.Values.Length != 3) throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "Sequence argmax requires a rank-three output.", modelId: _artifact.ModelId, tensorName: request.OutputName, operation: "sequence-shape", technicalDetails: shape.ToString());
                int batch = request.Layout == SequenceTensorLayout.BatchTimeClasses ? shape.Values[0] : shape.Values[1];
                int time = request.Layout == SequenceTensorLayout.BatchTimeClasses ? shape.Values[1] : shape.Values[0];
                int classes = shape.Values[2];
                if (batch <= 0 || batch > request.MaximumBatch) throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "Sequence argmax batch exceeds its configured bound.", modelId: _artifact.ModelId, tensorName: request.OutputName, operation: "sequence-shape", technicalDetails: "batch=" + batch);
                if (time <= 0 || time > request.MaximumTime) throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "Sequence argmax time dimension exceeds its configured bound.", modelId: _artifact.ModelId, tensorName: request.OutputName, operation: "sequence-shape", technicalDetails: "time=" + time);
                if (classes != request.ExpectedClasses) throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "Sequence argmax class dimension does not match the requested contract.", modelId: _artifact.ModelId, tensorName: request.OutputName, operation: "sequence-shape", technicalDetails: "classes=" + classes + ";expected=" + request.ExpectedClasses);

                int traceLength = checked(batch * time);
                EnsureDeviceMemory(ref _ctcClassIndices, checked(traceLength * sizeof(int)));
                EnsureDeviceMemory(ref _ctcConfidences, checked(traceLength * sizeof(float)));
                EnsureDeviceMemory(ref _ctcInvalidOffsets, checked(batch * sizeof(int)));
                TensorRtInferenceBuffer logits = _bindings.Buffers[request.OutputName];
                var logitsBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor(request.OutputName, TensorElementType.Float32, ToCoreShape(shape), TensorRtCudaBufferAccess.Read), logits.Memory);
                var classBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("sequence.class-indices", TensorElementType.Int32, new TensorShape(batch, time), TensorRtCudaBufferAccess.Write), _ctcClassIndices!);
                var confidenceBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("sequence.confidences", TensorElementType.Float32, new TensorShape(batch, time), TensorRtCudaBufferAccess.Write), _ctcConfidences!);
                var invalidBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("sequence.invalid-offsets", TensorElementType.Int32, new TensorShape(batch), TensorRtCudaBufferAccess.Write), _ctcInvalidOffsets!);
                using (TensorRtCudaKernelLaunch launch = TensorRtCudaOcrKernels.LaunchCtcTrace(
                    _ctcTraceKernel!,
                    _stream,
                    logitsBuffer,
                    classBuffer,
                    confidenceBuffer,
                    invalidBuffer,
                    batch,
                    time,
                    classes,
                    request.Layout == SequenceTensorLayout.TimeBatchClasses,
                    request.ApplySoftmax,
                    request.RequireUnitInterval))
                {
                    launch.Synchronize();
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new SequenceArgMaxResult(
                    batch,
                    time,
                    classes,
                    ReadInt32(_ctcClassIndices!, traceLength),
                    _ctcConfidences!.ToSingleArray(traceLength),
                    ReadInt32(_ctcInvalidOffsets!, batch));
            }
            catch
            {
                if (enqueued)
                {
                    try { _stream.Synchronize(); }
                    catch { }
                }
                throw;
            }
        }

        private void EnsureCtcTraceKernel(CancellationToken cancellationToken)
        {
            if (_ctcTraceKernel != null) return;
            cancellationToken.ThrowIfCancellationRequested();
            string architecture = _cudaTargetArchitecture ?? throw new NotSupportedException("A CUDA target architecture is required for sequence argmax.");
            var options = new TensorRtCudaRtcCompileOptions(architecture, TensorRtCudaRtcArtifactKind.Ptx, useFastMath: false);
            TensorRtCudaRtcArtifact artifact = TensorRtCudaRtcCompiler.Compile(TensorRtCudaOcrKernels.CtcTraceDefinition, options);
            cancellationToken.ThrowIfCancellationRequested();
            _ctcTraceKernel = TensorRtCudaCompiledKernel.Load(artifact, _deviceOrdinal);
        }

        private static void EnsureDeviceMemory(ref CudaMemory? memory, int requiredBytes)
        {
            if (requiredBytes <= 0) throw new ArgumentOutOfRangeException(nameof(requiredBytes));
            if (memory != null && memory.SizeInBytes >= requiredBytes) return;
            memory?.Dispose();
            memory = new CudaMemory(requiredBytes);
        }

        private static int[] ReadInt32(CudaMemory memory, int count)
        {
            byte[] bytes = memory.ToArray(checked(count * sizeof(int)));
            var values = new int[count];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
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

        private void PrepareHostInputs(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            ValidateInputCollection(inputs);
            bool reuseDeviceCopy = false;
            if (_cacheImmutableHostInputsOnDevice &&
                _cachedHostInputs != null &&
                _cachedHostInputs.TryGetTarget(out InferenceInputs? cachedInputs) &&
                ReferenceEquals(cachedInputs, inputs))
            {
                reuseDeviceCopy = true;
            }

            if (!reuseDeviceCopy) InvalidateHostInputCache();
            foreach (NamedTensor input in inputs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TensorRtEngineTensorBinding binding = _bindings.Report.GetTensor(input.Name);
                EnsureMode(binding, TensorRtIOMode.Input, input.Name);
                TensorRtDims runtimeShape = ToTensorRtShape(input.Tensor.Shape, input.Name);
                TensorRtBindingContract.ValidateInputShape(binding, runtimeShape, _artifact.ModelId);
                if (binding.EngineShape.Values.Any(value => value < 0)) _bindings.SetInputShape(input.Name, runtimeShape);
                if (!reuseDeviceCopy) CopyInput(binding, input.Tensor, runtimeShape);
            }

            if (!reuseDeviceCopy && _cacheImmutableHostInputsOnDevice)
            {
                _cachedHostInputs = new WeakReference<InferenceInputs>(inputs);
            }
        }

        private void InvalidateHostInputCache()
        {
            _cachedHostInputs = null;
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
            int bytesToRead = checked(binding.EstimateByteSize(shape));
            if (binding.DataType == TensorRtDataType.Float)
            {
                // Use the typed bridge copy to materialize the managed tensor in
                // one device-to-host transfer. The previous byte[] plus
                // BlockCopy path performed two managed allocations/copies for
                // every OCR crop.
                float[] values = buffer.Memory.ToSingleArray(checked(bytesToRead / sizeof(float)));
                return new Tensor<float>(ToCoreShape(shape), values, TensorBufferOwnership.Transfer);
            }

            byte[] bytes = buffer.Memory.ToArray(bytesToRead);
            Array valuesArray = FromBytes(bytes, elementType);
            return CreateTensor(elementType, ToCoreShape(shape), valuesArray);
        }

        private TensorRtDims ResolveOutputShapeAfterEnqueue(TensorRtEngineTensorBinding output)
        {
            if (_bindings.Buffers.TryGetValue(output.Name, out TensorRtInferenceBuffer? buffer) &&
                buffer.RuntimeShape != null &&
                IsConcreteShape(buffer.RuntimeShape) &&
                output.EngineShape.Values.Any(value => value < 0))
            {
                // Data-dependent outputs may not emit a shape notification through every
                // bridge. The buffer was allocated from TensorRT's max-output-size bound,
                // so returning that bounded shape is safe and preserves the complete buffer.
                return buffer.RuntimeShape;
            }

            return ResolveOutputShape(output, allowMaximumForDataDependent: false);
        }

        private void ValidateDeviceTensorCollection(
            IReadOnlyList<TensorRtDeviceTensor> tensors,
            IReadOnlyList<TensorRtEngineTensorBinding> expected,
            TensorRtIOMode mode)
        {
            if (tensors.Count != expected.Count)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "The device tensor collection must contain every TensorRT binding exactly once.", modelId: _artifact.ModelId, operation: "validate-device-tensors", technicalDetails: "mode=" + mode + ";expected=" + expected.Count + ";actual=" + tensors.Count);
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (TensorRtDeviceTensor tensor in tensors)
            {
                if (tensor == null || !names.Add(tensor.Name))
                {
                    throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "The device tensor collection contains a null or duplicate tensor name.", modelId: _artifact.ModelId, operation: "validate-device-tensors");
                }
                TensorRtEngineTensorBinding binding = _bindings.Report.GetTensor(tensor.Name);
                EnsureMode(binding, mode, tensor.Name);
                if (tensor.DeviceOrdinal != _deviceOrdinal)
                {
                    throw new TensorRtBackendException(TensorRtErrorCodes.CudaContractInvalid, "Every device tensor must use the TensorRT context device.", modelId: _artifact.ModelId, tensorName: tensor.Name, operation: "validate-device-device", technicalDetails: "contextDevice=" + _deviceOrdinal + ";bufferDevice=" + tensor.DeviceOrdinal);
                }
            }

            if (expected.Any(binding => !names.Contains(binding.Name)))
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.TensorInvalid, "The device tensor collection is missing a TensorRT binding.", modelId: _artifact.ModelId, operation: "validate-device-tensors");
            }
        }

        private static void ValidateDeviceType(TensorRtEngineTensorBinding binding, TensorRtDeviceTensor tensor)
        {
            TensorElementType expected = ToCoreElementType(binding.DataType);
            if (expected == TensorElementType.Unknown || expected != tensor.ElementType)
            {
                throw UnsupportedType(tensor.Name, binding.DataType, tensor.ElementType);
            }
        }

        private void ValidateDeviceStream(CudaStream stream)
        {
            int streamDevice = ResolveStreamDeviceOrdinal(stream);
            if (streamDevice != _deviceOrdinal)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.CudaContractInvalid, "The caller-owned CUDA stream must use the TensorRT context device.", modelId: _artifact.ModelId, operation: "validate-device-stream", technicalDetails: "contextDevice=" + _deviceOrdinal + ";streamDevice=" + streamDevice);
            }
        }

        private void ReleaseDeviceOperation()
        {
            _operationGate.Release();
        }

        private TensorRtDims ResolveOutputShape(TensorRtEngineTensorBinding output, bool allowMaximumForDataDependent)
        {
            TensorRtDims shape;
            try { shape = _context.GetTensorShape(output.Name); }
            catch { shape = output.EngineShape; }
            if (IsConcreteShape(shape)) return shape;

            if (allowMaximumForDataDependent && output.EngineShape.Values.Any(value => value < 0))
            {
                TensorRtDims? maximumShape = TryResolveMaximumOutputShape(output);
                if (maximumShape != null) return maximumShape;
            }

            if (shape.Values.Length == 0 || shape.Values.Any(value => value <= 0))
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.TensorInvalid,
                    "TensorRT did not expose a concrete output shape after shape inference.",
                    modelId: _artifact.ModelId,
                    tensorName: output.Name,
                    operation: "shape",
                    technicalDetails: "engineShape=" + output.EngineShape + ";contextShape=" + shape);
            }
            return shape;
        }

        private TensorRtDims? TryResolveMaximumOutputShape(TensorRtEngineTensorBinding output)
        {
            long maximumBytes;
            try { maximumBytes = _context.GetMaxOutputSize(output.Name); }
            catch { return null; }
            if (maximumBytes <= 0) return null;

            long bytesPerElement = checked((long)output.EffectiveBytesPerComponent * output.EffectiveComponentsPerElement);
            if (bytesPerElement <= 0 || maximumBytes % bytesPerElement != 0) return null;
            long maximumElements = maximumBytes / bytesPerElement;
            int[] dimensions = output.EngineShape.Values.ToArray();
            int unresolvedIndex = -1;
            long fixedElements = 1;
            for (int index = 0; index < dimensions.Length; index++)
            {
                if (dimensions[index] == -1)
                {
                    if (unresolvedIndex >= 0) return null;
                    unresolvedIndex = index;
                }
                else
                {
                    fixedElements = checked(fixedElements * dimensions[index]);
                }
            }

            if (unresolvedIndex < 0 || fixedElements <= 0 || maximumElements % fixedElements != 0) return null;
            long unresolvedExtent = maximumElements / fixedElements;
            if (unresolvedExtent <= 0 || unresolvedExtent > int.MaxValue) return null;
            dimensions[unresolvedIndex] = checked((int)unresolvedExtent);
            return new TensorRtDims(dimensions);
        }

        private static bool IsConcreteShape(TensorRtDims shape)
        {
            return shape.Values.Length > 0 && shape.Values.All(value => value > 0);
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

        private static int ResolveStreamDeviceOrdinal(CudaStream stream)
        {
            try { return stream.DeviceOrdinal; }
            catch (CudaException exception) when (exception.StatusCode == TensorRtSharp.Shared.Interop.BridgeStatusCode.NotSupported && exception.ErrorCategory == TensorRtSharp.Shared.Interop.BridgeErrorCategory.Cuda)
            {
                return CudaDevice.Current;
            }
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

        private static void DisposeResource(IDisposable? resource, ref Exception? firstFailure)
        {
            if (resource == null) return;
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
