using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Backends.OnnxRuntime.Internal;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.ML.OnnxRuntime;
using CoreModelMetadata = JYPPX.DeploySharp.Models.ModelMetadata;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime
{
    internal sealed class OnnxRuntimeSession : IInferenceSession
    {
        private readonly object _lifetimeGate = new object();
        private readonly ModelArtifact _artifact;
        private readonly InferenceSession _session;
        private readonly SemaphoreSlim _operationGate;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly IReadOnlyDictionary<string, TensorDescriptor> _inputs;
        private readonly IReadOnlyList<string> _outputNames;
        private readonly int _maximumConcurrency;
        private readonly bool _nativeAsyncEnabled;
        private readonly bool _hasDynamicOutputs;
        private readonly bool _reuseInputCollections;
        private List<string>? _singleInputNames;
        private List<OrtValue>? _singleInputValues;
        private bool _disposed;

        public OnnxRuntimeSession(ModelArtifact artifact, InferenceSession session, int maximumConcurrency, bool nativeAsyncEnabled)
        {
            _artifact = artifact;
            _session = session;
            _maximumConcurrency = maximumConcurrency;
            _nativeAsyncEnabled = nativeAsyncEnabled;
            _operationGate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            // BackendRegistry normally supplies one independent channel per pool slot.
            // Reusing the short-lived input collection in that common case avoids two
            // managed list allocations on every inference without changing the
            // thread-safety contract of direct multi-channel sessions.
            _reuseInputCollections = maximumConcurrency == 1;
            Metadata = OnnxTensorBridge.CreateMetadata(artifact, session);
            _inputs = Metadata.Inputs.ToDictionary(value => value.Name, StringComparer.Ordinal);
            _outputNames = Metadata.Outputs.Select(value => value.Name).ToList().AsReadOnly();
            _hasDynamicOutputs = false;
            for (int index = 0; index < Metadata.Outputs.Count; index++)
            {
                if (Metadata.Outputs[index].Shape.IsDynamic)
                {
                    _hasDynamicOutputs = true;
                    break;
                }
            }
        }

        public CoreModelMetadata Metadata { get; }

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            EnsureUsable();
            bool entered = false;
            CancellationToken operationToken = _disposeSource.Token;
            CancellationTokenSource? linked = null;
            // Pooled callers pass this session's disposal token directly. It is
            // already linked to the session lifetime, so creating a one-shot
            // linked CTS would only add an allocation on every inference.
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
            catch (Exception exception) { throw OnnxRuntimeExceptionMapper.Map(exception, _artifact, "run", operationToken); }
            finally
            {
                if (entered) _operationGate.Release();
                linked?.Dispose();
            }
        }

        public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
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
                await _operationGate.WaitAsync(operationToken).ConfigureAwait(false);
                entered = true;
                EnsureUsable();
                // ORT 1.28 native RunAsync can fail to complete its callback when RunOptions.Terminate races the call. Cancellable requests therefore use the synchronous native path, which honors terminate; no worker task is fabricated. / ORT 1.28 原生 RunAsync 在 RunOptions.Terminate 与调用竞争时可能无法完成回调，因此可取消请求使用能够响应 terminate 的同步原生路径，且不伪造工作线程任务。
                if (!_nativeAsyncEnabled || cancellationToken.CanBeCanceled || _hasDynamicOutputs) return RunCore(inputs, operationToken);
                return await RunNativeAsync(inputs, operationToken).ConfigureAwait(false);
            }
            catch (Exception exception) { throw OnnxRuntimeExceptionMapper.Map(exception, _artifact, "run-async", operationToken); }
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
            // Waiting for every slot prevents native session disposal while calls are unwinding. / 等待所有槽位可避免调用尚未退出时释放原生会话。
            int acquired = 0;
            try
            {
                for (; acquired < _maximumConcurrency; acquired++) _operationGate.Wait();
                _session.Dispose();
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
            ValidateInputNames(inputs);
            List<string> inputNames = _reuseInputCollections
                ? (_singleInputNames ??= new List<string>(inputs.Count))
                : new List<string>(inputs.Count);
            List<OrtValue> inputValues = _reuseInputCollections
                ? (_singleInputValues ??= new List<OrtValue>(inputs.Count))
                : new List<OrtValue>(inputs.Count);
            using (var runOptions = new RunOptions())
            using (CancellationTokenRegistration registration = cancellationToken.Register(() => TryTerminate(runOptions)))
            {
                try
                {
                    foreach (NamedTensor input in inputs)
                    {
                        inputNames.Add(input.Name);
                        inputValues.Add(OnnxTensorBridge.CreateInput(_artifact, input.Name, input.Tensor, _inputs[input.Name]));
                    }
                    using (IDisposableReadOnlyCollection<OrtValue> outputs = _session.Run(runOptions, inputNames, inputValues, _outputNames))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return OnnxTensorBridge.CopyOutputs(_artifact, _outputNames, outputs);
                    }
                }
                finally
                {
                    foreach (OrtValue value in inputValues) value.Dispose();
                    inputValues.Clear();
                    inputNames.Clear();
                }
            }
        }

        private async Task<InferenceOutputs> RunNativeAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            ValidateInputNames(inputs);
            List<string> inputNames = _reuseInputCollections
                ? (_singleInputNames ??= new List<string>(inputs.Count))
                : new List<string>(inputs.Count);
            List<OrtValue> inputValues = _reuseInputCollections
                ? (_singleInputValues ??= new List<OrtValue>(inputs.Count))
                : new List<OrtValue>(inputs.Count);
            var outputValues = new List<OrtValue>(_outputNames.Count);
            using (var runOptions = new RunOptions())
            {
                try
                {
                    foreach (NamedTensor input in inputs)
                    {
                        inputNames.Add(input.Name);
                        inputValues.Add(OnnxTensorBridge.CreateInput(_artifact, input.Name, input.Tensor, _inputs[input.Name]));
                    }
                    foreach (TensorDescriptor output in Metadata.Outputs) outputValues.Add(OnnxTensorBridge.AllocateOutput(_artifact, output));
                    IReadOnlyCollection<OrtValue> outputs = await _session.RunAsync(runOptions, inputNames, inputValues, _outputNames, outputValues).ConfigureAwait(false);
                    // Native async is entered only for a non-cancellable caller token. Disposal is observed after native completion so the ORT callback is never raced with terminate. / 仅在调用方 token 不可取消时进入原生异步；释放请求在原生完成后观察，避免 terminate 与 ORT 回调竞争。
                    cancellationToken.ThrowIfCancellationRequested();
                    return OnnxTensorBridge.CopyOutputs(_artifact, _outputNames, outputs);
                }
                finally
                {
                    foreach (OrtValue value in outputValues) value.Dispose();
                    foreach (OrtValue value in inputValues) value.Dispose();
                    inputValues.Clear();
                    inputNames.Clear();
                }
            }
        }

        private void ValidateInputNames(InferenceInputs inputs)
        {
            if (inputs.Count != _inputs.Count) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "The input collection must contain every model input and no extras.", modelId: _artifact.ModelId, operation: "validate-inputs", technicalDetails: "expected=" + _inputs.Count + ";actual=" + inputs.Count);
            foreach (NamedTensor input in inputs) if (!_inputs.ContainsKey(input.Name)) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "The input collection contains an unknown tensor name.", modelId: _artifact.ModelId, tensorName: input.Name, operation: "validate-inputs");
        }

        private static void TryTerminate(RunOptions options)
        {
            try { options.Terminate = true; }
            catch (ObjectDisposedException) { }
        }

        private void EnsureUsable()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.ObjectDisposed, "The ONNX Runtime session has been disposed.", modelId: _artifact.ModelId, operation: "session");
            }
        }
    }
}
