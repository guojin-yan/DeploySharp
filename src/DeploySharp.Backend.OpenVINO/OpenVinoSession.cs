using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Backends.OpenVINO.Internal;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using OpenVinoSharp;
using CoreModelMetadata = JYPPX.DeploySharp.Models.ModelMetadata;
using OvTensor = OpenVinoSharp.Tensor;

namespace JYPPX.DeploySharp.Backends.OpenVINO
{
    internal sealed class OpenVinoSession : IInferenceSession
    {
        private readonly object _lifetimeGate = new object();
        private readonly ModelArtifact _artifact;
        private readonly Core _core;
        private readonly Model _model;
        private readonly CompiledModel _compiledModel;
        private readonly SemaphoreSlim _operationGate;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly IReadOnlyDictionary<string, TensorDescriptor> _inputs;
        private readonly IReadOnlyList<TensorDescriptor> _outputs;
        private readonly int _maximumConcurrency;
        private readonly string _device;
        private bool _disposed;

        public OpenVinoSession(ModelArtifact artifact, Core core, Model model, CompiledModel compiledModel, int maximumConcurrency, bool allowDynamicShapes, string device)
        {
            _artifact = artifact;
            _core = core;
            _model = model;
            _compiledModel = compiledModel;
            _maximumConcurrency = maximumConcurrency;
            _device = device;
            _operationGate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            Metadata = OpenVinoTensorBridge.CreateMetadata(artifact, model, compiledModel, allowDynamicShapes);
            _inputs = Metadata.Inputs.ToDictionary(value => value.Name, StringComparer.Ordinal);
            _outputs = Metadata.Outputs;
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
                    InferenceOutputs outputs = RunCore(inputs);
                    // Synchronous OpenVINO infer has no safe managed cancellation hook; cancellation is observed at native boundaries. / 同步 OpenVINO infer 没有安全的托管取消钩子，因此取消只在原生调用边界观察。
                    linked.Token.ThrowIfCancellationRequested();
                    return outputs;
                }
                catch (Exception exception) { throw OpenVinoExceptionMapper.Map(exception, _artifact, "run", _device, linked.Token); }
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
                    return await RunNativeAsync(inputs, linked.Token).ConfigureAwait(false);
                }
                catch (Exception exception) { throw OpenVinoExceptionMapper.Map(exception, _artifact, "run-async", _device, linked.Token); }
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
            int acquired = 0;
            try
            {
                // Acquiring every slot keeps compiled model/core handles alive until all independent requests exit. / 获取全部槽位可保证所有独立请求退出前编译模型和 Core 句柄仍然有效。
                for (; acquired < _maximumConcurrency; acquired++) _operationGate.Wait();
                _compiledModel.Dispose();
                _model.Dispose();
                _core.Dispose();
            }
            finally
            {
                for (int index = 0; index < acquired; index++) _operationGate.Release();
                _operationGate.Dispose();
                _disposeSource.Dispose();
            }
        }

        private InferenceOutputs RunCore(InferenceInputs inputs)
        {
            ValidateInputNames(inputs);
            using (InferRequest request = _compiledModel.CreateInferRequest())
            {
                List<OvTensor> nativeInputs = BindInputs(request, inputs);
                try
                {
                    request.Infer();
                    return OpenVinoTensorBridge.CopyOutputs(_artifact, request, _outputs);
                }
                finally { foreach (OvTensor tensor in nativeInputs) tensor.Dispose(); }
            }
        }

        private async Task<InferenceOutputs> RunNativeAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            ValidateInputNames(inputs);
            using (InferRequest request = _compiledModel.CreateInferRequest())
            {
                List<OvTensor> nativeInputs = BindInputs(request, inputs);
                bool started = false;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    request.StartAsync();
                    started = true;
                    while (!request.WaitFor(10))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            TryCancelAndDrain(request);
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        await Task.Delay(1).ConfigureAwait(false);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    return OpenVinoTensorBridge.CopyOutputs(_artifact, request, _outputs);
                }
                finally
                {
                    if (started && cancellationToken.IsCancellationRequested) TryCancelAndDrain(request);
                    foreach (OvTensor tensor in nativeInputs) tensor.Dispose();
                }
            }
        }

        private List<OvTensor> BindInputs(InferRequest request, InferenceInputs inputs)
        {
            var values = new List<OvTensor>(inputs.Count);
            try
            {
                foreach (NamedTensor input in inputs)
                {
                    OvTensor tensor = OpenVinoTensorBridge.CreateInput(_artifact, input.Name, input.Tensor, _inputs[input.Name]);
                    values.Add(tensor);
                    request.set_input_tensor(input.Name, tensor);
                }
                return values;
            }
            catch
            {
                foreach (OvTensor tensor in values) tensor.Dispose();
                throw;
            }
        }

        private void ValidateInputNames(InferenceInputs inputs)
        {
            if (inputs.Count != _inputs.Count) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "The input collection must contain every model input and no extras.", modelId: _artifact.ModelId, operation: "validate-inputs", device: _device, technicalDetails: "expected=" + _inputs.Count + ";actual=" + inputs.Count);
            foreach (NamedTensor input in inputs) if (!_inputs.ContainsKey(input.Name)) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "The input collection contains an unknown tensor name.", modelId: _artifact.ModelId, tensorName: input.Name, operation: "validate-inputs", device: _device);
        }

        private static void TryCancelAndDrain(InferRequest request)
        {
            try { request.Cancel(); } catch (Exception) { }
            try { request.Wait(); } catch (Exception) { }
        }

        private void EnsureUsable()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) throw new OpenVinoBackendException(OpenVinoErrorCodes.ObjectDisposed, "The OpenVINO session has been disposed.", modelId: _artifact.ModelId, operation: "session", device: _device);
            }
        }
    }
}
