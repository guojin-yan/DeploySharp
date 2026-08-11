using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Runs prepared-tensor visual inference through a Core backend session and a reusable decoder. / 通过 Core 后端会话和可复用解码器运行已准备张量视觉推理。</summary>
    public sealed class VisualPipeline : IDisposable
    {
        private readonly object _lifetimeGate = new object();
        private readonly IInferenceSession _session;
        private readonly SemaphoreSlim _operationGate;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly int _maximumConcurrency;
        private bool _disposed;

        /// <summary>Initializes a pipeline and creates one backend session owned by the pipeline. The registry remains caller-owned. / 初始化 Pipeline 并创建一个由 Pipeline 拥有的后端会话；注册中心仍由调用方拥有。</summary>
        public VisualPipeline(BackendRegistry backendRegistry, VisualProfileSelection selection, BackendRequest request, SessionOptions? sessionOptions = null)
        {
            if (backendRegistry == null) throw new ArgumentNullException(nameof(backendRegistry));
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (selection.Profile.ModelId != selection.Artifact.ModelId || !string.Equals(selection.Profile.ModelFormat, selection.Artifact.Format, StringComparison.OrdinalIgnoreCase))
            {
                throw new VisualException(VisualErrorCodes.ProfileInvalid, "The selected profile and artifact are incompatible.", profileId: selection.Profile.ProfileId, backendId: selection.Backend.Id, modelId: selection.Artifact.ModelId);
            }
            SessionOptions effectiveOptions = sessionOptions ?? SessionOptions.Default;
            _maximumConcurrency = effectiveOptions.MaxConcurrency;
            _operationGate = new SemaphoreSlim(_maximumConcurrency, _maximumConcurrency);
            BackendCapabilities capabilities = request.RequiredCapabilities | selection.Profile.RequiredCapabilities | BackendCapabilities.TensorInference;
            var effectiveRequest = new BackendRequest(capabilities, selection.Backend.Id, request.Device);
            try
            {
                _session = backendRegistry.CreateSession(selection.Artifact, effectiveRequest, effectiveOptions);
                ValidateSessionMetadata(_session.Metadata, selection.Profile);
            }
            catch (Exception exception) when (!(exception is VisualException))
            {
                _operationGate.Dispose();
                _disposeSource.Dispose();
                throw new VisualException(VisualErrorCodes.InferenceFailed, "The visual backend session could not be created.", exception, selection.Profile.ProfileId, backendId: selection.Backend.Id, modelId: selection.Artifact.ModelId, technicalDetails: exception.ToString());
            }
        }

        /// <summary>Gets the immutable profile, artifact, and backend selection. / 获取不可变的 Profile、工件和后端选择。</summary>
        public VisualProfileSelection Selection { get; }

        /// <summary>Runs synchronous inference. The prepared input remains caller-owned unless disposal is explicitly requested. / 运行同步推理；除非显式请求释放，否则已准备输入仍由调用方拥有。</summary>
        public VisualInferenceResult Run(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecuteAsync(input, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Runs asynchronous inference or the backend's documented asynchronous fallback. / 运行异步推理或后端已记录的异步回退。</summary>
        public Task<VisualInferenceResult> RunAsync(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecuteAsync(input, options ?? VisualExecutionOptions.Default, true, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>Cancels active operations, waits for concurrency slots, and then releases the owned backend session exactly once. / 取消活动操作、等待并发槽位，然后仅一次释放拥有的后端会话。</remarks>
        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }

            // Waiting for every slot prevents session disposal while a backend call is still unwinding. / 等待所有槽位可防止后端调用仍在退出时释放会话。
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

        private async Task<VisualInferenceResult> ExecuteAsync(PreparedVisualInput input, VisualExecutionOptions options, bool asynchronous, CancellationToken callerToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CancellationToken disposeToken = CaptureDisposeToken();
            bool entered = false;
            using (var timeoutSource = options.Timeout.HasValue ? new CancellationTokenSource(options.Timeout.Value) : new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutSource.Token, disposeToken))
            {
                try
                {
                    input.EnsureUsable();
                    ValidateInput(input, Selection.Profile);
                    await _operationGate.WaitAsync(linked.Token).ConfigureAwait(false);
                    entered = true;
                    EnsureUsable();
                    var namedInputs = new List<NamedTensor>(input.AuxiliaryInputs.Count + 1) { new NamedTensor(Selection.Profile.Input.Name, input.Tensor) };
                    namedInputs.AddRange(input.AuxiliaryInputs);
                    var inputs = new InferenceInputs(namedInputs);
                    var inferenceWatch = Stopwatch.StartNew();
                    InferenceOutputs outputs = asynchronous
                        ? await _session.RunAsync(inputs, linked.Token).ConfigureAwait(false)
                        : _session.Run(inputs, linked.Token);
                    inferenceWatch.Stop();
                    ValidateOutputs(outputs, Selection.Profile);
                    var decodeWatch = Stopwatch.StartNew();
                    object decoded = Selection.Profile.Decoder.Decode(new VisualDecodeContext(input, Selection.Profile, outputs, linked.Token));
                    decodeWatch.Stop();
                    return new VisualInferenceResult(decoded, Selection.Profile.Task, Selection.Profile.ModelId, Selection.Backend.Id, new InferenceTiming(TimeSpan.Zero, inferenceWatch.Elapsed, decodeWatch.Elapsed), options.CorrelationId);
                }
                catch (OperationCanceledException exception)
                {
                    throw MapCancellation(exception, callerToken);
                }
                catch (DeploySharpException exception) when (linked.IsCancellationRequested)
                {
                    // Backends may preserve cancellation as a stable DeploySharp exception instead of leaking OperationCanceledException. / 后端可能将取消保留为稳定的 DeploySharp 异常，而不是泄漏 OperationCanceledException。
                    throw MapCancellation(exception, callerToken);
                }
                catch (VisualException) { throw; }
                catch (Exception exception)
                {
                    throw new VisualException(VisualErrorCodes.InferenceFailed, "Visual inference failed.", exception, Selection.Profile.ProfileId, backendId: Selection.Backend.Id, modelId: Selection.Profile.ModelId, technicalDetails: exception.ToString());
                }
                finally
                {
                    if (entered) _operationGate.Release();
                    if (options.DisposeOwnedInputOnCompletion && input.Ownership == PreparedInputOwnership.Owned) input.Dispose();
                }
            }
        }

        private VisualException MapCancellation(Exception exception, CancellationToken callerToken)
        {
            if (_disposed || _disposeSource.IsCancellationRequested) return new VisualException(VisualErrorCodes.ObjectDisposed, "The visual pipeline was disposed during inference.", exception, Selection.Profile.ProfileId, backendId: Selection.Backend.Id, modelId: Selection.Profile.ModelId, technicalDetails: exception.ToString());
            if (callerToken.IsCancellationRequested) return new VisualException(VisualErrorCodes.Cancelled, "Visual inference was cancelled by the caller.", exception, Selection.Profile.ProfileId, backendId: Selection.Backend.Id, modelId: Selection.Profile.ModelId, technicalDetails: exception.ToString());
            return new VisualException(VisualErrorCodes.Timeout, "Visual inference exceeded its configured timeout.", exception, Selection.Profile.ProfileId, backendId: Selection.Backend.Id, modelId: Selection.Profile.ModelId, technicalDetails: exception.ToString());
        }

        private void EnsureUsable()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The visual pipeline has been disposed.", profileId: Selection.Profile.ProfileId, backendId: Selection.Backend.Id, modelId: Selection.Profile.ModelId);
            }
        }

        private CancellationToken CaptureDisposeToken()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The visual pipeline has been disposed.", profileId: Selection.Profile.ProfileId, backendId: Selection.Backend.Id, modelId: Selection.Profile.ModelId);
                // Capture the token while the lifetime source is protected so Dispose cannot release it between the usability check and linked-token creation. / 在生命周期源受保护时捕获令牌，防止 Dispose 在可用性检查与创建链接令牌之间释放该源。
                return _disposeSource.Token;
            }
        }

        private static void ValidateInput(PreparedVisualInput input, VisualModelProfile profile)
        {
            VisualInputBinding binding = profile.Input;
            if (!string.Equals(input.InputName, binding.Name, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Prepared input tensor name does not match the profile.", profileId: profile.ProfileId, tensorName: input.InputName, modelId: profile.ModelId);
            if (input.Tensor.ElementType != binding.ElementType) throw new VisualException(VisualErrorCodes.TensorInvalid, "Prepared input element type does not match the profile.", profileId: profile.ProfileId, tensorName: input.InputName, modelId: profile.ModelId);
            if (input.Layout != binding.Layout) throw new VisualException(VisualErrorCodes.TensorInvalid, "Prepared input layout does not match the profile.", profileId: profile.ProfileId, tensorName: input.InputName, modelId: profile.ModelId);
            if (input.BatchSize < binding.MinimumBatch || input.BatchSize > binding.MaximumBatch) throw new VisualException(VisualErrorCodes.TensorInvalid, "Prepared input batch is outside profile bounds.", profileId: profile.ProfileId, tensorName: input.InputName, modelId: profile.ModelId);
            if (!TensorShapePattern.Matches(binding.ShapePattern, input.Tensor.Shape)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Prepared input shape does not match the profile pattern.", profileId: profile.ProfileId, tensorName: input.InputName, modelId: profile.ModelId, technicalDetails: input.Tensor.Shape.ToString());
            ValidateSpatialShape(input);
            if (input.AuxiliaryInputs.Count != profile.AuxiliaryInputs.Count) throw new VisualException(VisualErrorCodes.TensorInvalid, "Prepared auxiliary input count does not match the profile.", profileId: profile.ProfileId, modelId: profile.ModelId);
            foreach (VisualAuxiliaryInputBinding auxiliaryBinding in profile.AuxiliaryInputs)
            {
                NamedTensor? supplied = input.AuxiliaryInputs.FirstOrDefault(value => string.Equals(value.Name, auxiliaryBinding.Name, StringComparison.Ordinal));
                if (supplied == null) throw new VisualException(VisualErrorCodes.TensorInvalid, "A required auxiliary input tensor is missing.", profileId: profile.ProfileId, tensorName: auxiliaryBinding.Name, modelId: profile.ModelId);
                if (supplied.Tensor.ElementType != auxiliaryBinding.ElementType || !TensorShapePattern.Matches(auxiliaryBinding.ShapePattern, supplied.Tensor.Shape)) throw new VisualException(VisualErrorCodes.TensorInvalid, "An auxiliary input tensor is incompatible with the profile.", profileId: profile.ProfileId, tensorName: auxiliaryBinding.Name, modelId: profile.ModelId, technicalDetails: supplied.Tensor.Shape.ToString());
            }
        }

        private static void ValidateSpatialShape(PreparedVisualInput input)
        {
            TensorShape shape = input.Tensor.Shape;
            int heightIndex;
            int widthIndex;
            int batchIndex = -1;
            if (input.Layout == VisualTensorLayout.Nchw) { batchIndex = 0; heightIndex = 2; widthIndex = 3; }
            else if (input.Layout == VisualTensorLayout.Nhwc) { batchIndex = 0; heightIndex = 1; widthIndex = 2; }
            else if (input.Layout == VisualTensorLayout.Chw) { heightIndex = 1; widthIndex = 2; }
            else { heightIndex = 0; widthIndex = 1; }
            if (batchIndex >= 0 && shape[batchIndex] != input.BatchSize) throw new VisualException(VisualErrorCodes.TensorInvalid, "Tensor batch dimension does not match the declared batch size.", tensorName: input.InputName);
            if (shape[heightIndex] != input.ModelSize.Height || shape[widthIndex] != input.ModelSize.Width) throw new VisualException(VisualErrorCodes.TensorInvalid, "Tensor spatial dimensions do not match the model input size.", tensorName: input.InputName);
        }

        private static void ValidateOutputs(InferenceOutputs outputs, VisualModelProfile profile)
        {
            if (outputs == null) throw new VisualException(VisualErrorCodes.TensorInvalid, "Backend returned null outputs.", profileId: profile.ProfileId, modelId: profile.ModelId);
            foreach (VisualOutputBinding binding in profile.Outputs)
            {
                ITensor tensor;
                try { tensor = outputs.GetRequired(binding.Name); }
                catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "A required output tensor is missing.", exception, profile.ProfileId, binding.Name, modelId: profile.ModelId); }
                if (tensor.ElementType != binding.ElementType) throw new VisualException(VisualErrorCodes.TensorInvalid, "Output tensor element type does not match the profile.", profileId: profile.ProfileId, tensorName: binding.Name, modelId: profile.ModelId);
                if (!TensorShapePattern.Matches(binding.ShapePattern, tensor.Shape)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Output tensor shape does not match the profile pattern.", profileId: profile.ProfileId, tensorName: binding.Name, modelId: profile.ModelId, technicalDetails: tensor.Shape.ToString());
            }
        }

        private static void ValidateSessionMetadata(ModelMetadata metadata, VisualModelProfile profile)
        {
            if (metadata == null) throw new VisualException(VisualErrorCodes.TensorInvalid, "Backend session metadata is unavailable.", profileId: profile.ProfileId, modelId: profile.ModelId);
            if (metadata.ModelId != profile.ModelId) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Backend metadata model ID does not match the profile.", profileId: profile.ProfileId, modelId: profile.ModelId);
            TensorDescriptor? input = metadata.Inputs.FirstOrDefault(value => string.Equals(value.Name, profile.Input.Name, StringComparison.Ordinal));
            if (input == null || input.ElementType != profile.Input.ElementType || !PatternsCompatible(profile.Input.ShapePattern, input.Shape)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Backend input metadata is incompatible with the visual profile.", profileId: profile.ProfileId, tensorName: profile.Input.Name, modelId: profile.ModelId);
            foreach (VisualAuxiliaryInputBinding binding in profile.AuxiliaryInputs)
            {
                TensorDescriptor? auxiliary = metadata.Inputs.FirstOrDefault(value => string.Equals(value.Name, binding.Name, StringComparison.Ordinal));
                if (auxiliary == null || auxiliary.ElementType != binding.ElementType || !PatternsCompatible(binding.ShapePattern, auxiliary.Shape)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Backend auxiliary input metadata is incompatible with the visual profile.", profileId: profile.ProfileId, tensorName: binding.Name, modelId: profile.ModelId);
            }
            foreach (VisualOutputBinding binding in profile.Outputs)
            {
                TensorDescriptor? output = metadata.Outputs.FirstOrDefault(value => string.Equals(value.Name, binding.Name, StringComparison.Ordinal));
                if (output == null || output.ElementType != binding.ElementType || !PatternsCompatible(binding.ShapePattern, output.Shape)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Backend output metadata is incompatible with the visual profile.", profileId: profile.ProfileId, tensorName: binding.Name, modelId: profile.ModelId);
            }
        }

        private static bool PatternsCompatible(TensorShape first, TensorShape second)
        {
            if (first.Rank != second.Rank) return false;
            for (int index = 0; index < first.Rank; index++) if (first[index] >= 0 && second[index] >= 0 && first[index] != second[index]) return false;
            return true;
        }
    }
}
