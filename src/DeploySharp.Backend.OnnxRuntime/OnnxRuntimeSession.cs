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
        private bool _disposed;

        public OnnxRuntimeSession(ModelArtifact artifact, InferenceSession session, int maximumConcurrency, bool nativeAsyncEnabled)
        {
            _artifact = artifact;
            _session = session;
            _maximumConcurrency = maximumConcurrency;
            _nativeAsyncEnabled = nativeAsyncEnabled;
            _operationGate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            Metadata = OnnxTensorBridge.CreateMetadata(artifact, session);
            _inputs = Metadata.Inputs.ToDictionary(value => value.Name, StringComparer.Ordinal);
            _outputNames = Metadata.Outputs.Select(value => value.Name).ToList().AsReadOnly();
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
                    return RunCore(inputs, linked.Token);
                }
                catch (Exception exception) { throw OnnxRuntimeExceptionMapper.Map(exception, _artifact, "run", linked.Token); }
                finally { if (entered) _operationGate.Release(); }
            }
        }

        public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            EnsureUsable();
            bool entered = false;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeSource.Token))
            {
                try
                {
                    await _operationGate.WaitAsync(linked.Token).ConfigureAwait(false);
                    entered = true;
                    EnsureUsable();
                    // ORT 1.28 native RunAsync can fail to complete its callback when RunOptions.Terminate races the call. Cancellable requests therefore use the synchronous native path, which honors terminate; no worker task is fabricated. / ORT 1.28 原生 RunAsync 在 RunOptions.Terminate 与调用竞争时可能无法完成回调，因此可取消请求使用能够响应 terminate 的同步原生路径，且不伪造工作线程任务。
                    if (!_nativeAsyncEnabled || cancellationToken.CanBeCanceled || Metadata.Outputs.Any(value => value.Shape.IsDynamic)) return RunCore(inputs, linked.Token);
                    return await RunNativeAsync(inputs, linked.Token).ConfigureAwait(false);
                }
                catch (Exception exception) { throw OnnxRuntimeExceptionMapper.Map(exception, _artifact, "run-async", linked.Token); }
                finally { if (entered) _operationGate.Release(); }
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
            var inputNames = new List<string>(inputs.Count);
            var inputValues = new List<OrtValue>(inputs.Count);
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
                finally { foreach (OrtValue value in inputValues) value.Dispose(); }
            }
        }

        private async Task<InferenceOutputs> RunNativeAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            ValidateInputNames(inputs);
            var inputNames = new List<string>(inputs.Count);
            var inputValues = new List<OrtValue>(inputs.Count);
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
