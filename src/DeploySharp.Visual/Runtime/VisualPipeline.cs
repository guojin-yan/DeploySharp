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

        /// <summary>Gets the number of independently-created backend sessions available to this pipeline. / 获取此 Pipeline 可用的独立后端 Session 数量。</summary>
        public int MaximumConcurrency => _maximumConcurrency;

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

        /// <summary>Overlaps bounded input preparation with inference and returns results in source order. / 将有界输入准备与推理重叠，并按源输入顺序返回结果。</summary>
        /// <remarks>The callback runs on the thread pool and must return an immutable prepared input. At most <c>MaximumConcurrency + prefetch</c> items are retained, allowing the requested number of upcoming frames to prepare while active backend calls run. This is a pipeline overlap API, not a model-batch API; use <see cref="InferenceBatchScheduler{TInput,TOutput}"/> for true tensor batches. / 回调在线程池执行且必须返回不可变已准备输入。最多保留 <c>MaximumConcurrency + prefetch</c> 个项目，因此可在活动后端调用期间准备所请求数量的后续帧。这是流水线重叠 API，不是真正模型 Batch API；真正张量 Batch 请使用 <see cref="InferenceBatchScheduler{TInput,TOutput}"/>。</remarks>
        public async Task<IReadOnlyList<VisualInferenceResult>> RunPrefetchedAsync<TInput>(
            IReadOnlyList<TInput> inputs,
            Func<TInput, CancellationToken, PreparedVisualInput> prepare,
            int prefetch = 1,
            VisualExecutionOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (prepare == null) throw new ArgumentNullException(nameof(prepare));
            if (prefetch <= 0) throw new ArgumentOutOfRangeException(nameof(prefetch));
            return await RunPrefetchedCoreAsync(
                inputs,
                (value, token) => Task.Factory.StartNew(
                    () => prepare(value, token),
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default),
                prefetch,
                options,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Overlaps asynchronous frame preparation with inference and returns results in source order. / 将异步帧准备与推理重叠，并按源输入顺序返回结果。</summary>
        /// <remarks>The callback is started on the thread pool, so synchronous work before its first await cannot block the caller. At most <c>MaximumConcurrency + prefetch</c> prepared items are retained. This is still a pipeline overlap API; it does not combine samples into a model batch. / 回调在线程池启动，因此其第一次 await 之前的同步工作不会阻塞调用方。最多保留 <c>MaximumConcurrency + prefetch</c> 个已准备项目。这仍是流水线重叠接口，不会把样本合并为模型 Batch。</remarks>
        public async Task<IReadOnlyList<VisualInferenceResult>> RunPrefetchedAsync<TInput>(
            IReadOnlyList<TInput> inputs,
            Func<TInput, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            int prefetch = 1,
            VisualExecutionOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (prefetch <= 0) throw new ArgumentOutOfRangeException(nameof(prefetch));
            return await RunPrefetchedCoreAsync(
                inputs,
                (value, token) => Task.Factory.StartNew(
                    () => prepareAsync(value, token),
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default).Unwrap(),
                prefetch,
                options,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Runs multiple prepared inputs through the independent session pool and returns results in input order. / 通过独立 Session 池运行多个已准备输入，并按输入顺序返回结果。</summary>
        /// <remarks>The timeout and owned-input disposal options apply independently to each input. This method schedules separate model calls; use <see cref="InferenceBatchScheduler{TInput,TOutput}"/> when a model accepts multiple samples in one tensor batch. / 超时与自有输入释放选项分别应用于每个输入。此方法调度独立模型调用；模型可在单个张量 Batch 中接收多个样本时，请使用 <see cref="InferenceBatchScheduler{TInput,TOutput}"/>。</remarks>
        public IReadOnlyList<VisualInferenceResult> RunMany(IReadOnlyList<PreparedVisualInput> inputs, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return RunManyAsync(inputs, options, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Asynchronously runs multiple prepared inputs up to <see cref="MaximumConcurrency"/> and returns results in input order. / 在不超过 <see cref="MaximumConcurrency"/> 的范围内异步运行多个已准备输入，并按输入顺序返回结果。</summary>
        /// <remarks>All inputs are validated before any model call starts. / 在开始任何模型调用前会先校验全部输入。</remarks>
        public async Task<IReadOnlyList<VisualInferenceResult>> RunManyAsync(IReadOnlyList<PreparedVisualInput> inputs, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            EnsureUsable();
            for (int index = 0; index < inputs.Count; index++)
            {
                PreparedVisualInput input = inputs[index] ?? throw new ArgumentException("Prepared visual inputs cannot contain null.", nameof(inputs));
                input.EnsureUsable();
                ValidateInput(input, Selection.Profile);
            }
            if (inputs.Count == 0) return Array.Empty<VisualInferenceResult>();

            VisualExecutionOptions effectiveOptions = options ?? VisualExecutionOptions.Default;
            var results = new VisualInferenceResult[inputs.Count];
            var nextIndex = new[] { -1 };
            int workerCount = Math.Min(inputs.Count, _maximumConcurrency);
            var workers = new Task[workerCount];
            for (int worker = 0; worker < workerCount; worker++)
            {
                // Some CPU backends expose RunAsync as a synchronous native call.
                // Run each bounded worker on the pool so those independent channels
                // can still overlap without creating one task per input.
                workers[worker] = Task.Run(() => RunManyWorkerAsync(inputs, results, effectiveOptions, cancellationToken, nextIndex), CancellationToken.None);
            }
            try
            {
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            catch
            {
                // Let already-started workers unwind before surfacing the first failure.
                try { await Task.WhenAll(workers).ConfigureAwait(false); } catch { }
                throw;
            }
            return Array.AsReadOnly(results);
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
            CancellationToken operationToken = disposeToken;
            CancellationTokenSource? timeoutSource = null;
            CancellationTokenSource? linked = null;
            try
            {
                // The default hot path only needs the pipeline lifetime token. Avoid allocating two linked CTS objects per call.
                // 默认热路径只需要 Pipeline 生命周期令牌，避免每次调用分配两个链接 CTS。
                if (options.Timeout.HasValue)
                {
                    timeoutSource = new CancellationTokenSource(options.Timeout.Value);
                }
                if (callerToken.CanBeCanceled || timeoutSource != null)
                {
                    linked = timeoutSource == null
                        ? CancellationTokenSource.CreateLinkedTokenSource(callerToken, disposeToken)
                        : CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutSource.Token, disposeToken);
                    operationToken = linked.Token;
                }

                input.EnsureUsable();
                ValidateInput(input, Selection.Profile);
                await _operationGate.WaitAsync(operationToken).ConfigureAwait(false);
                entered = true;
                EnsureUsable();
                // Prepared inputs are immutable and may be reused concurrently;
                // let the input cache avoid rebuilding the named collection on
                // every steady-state inference.
                InferenceInputs inputs = input.GetInferenceInputs();
                var inferenceWatch = Stopwatch.StartNew();
                object decoded;
                var decodeWatch = new Stopwatch();
                if (Selection.Profile.Decoder is ISequenceArgMaxVisualDecoder reducedDecoder &&
                    _session is ISequenceArgMaxInferenceSession reducedSession &&
                    reducedSession.IsSequenceArgMaxSupported)
                {
                    SequenceArgMaxRequest reducedRequest = reducedDecoder.CreateSequenceArgMaxRequest();
                    SequenceArgMaxResult reduced = asynchronous && _maximumConcurrency > 1
                        ? await Task.Run(() => reducedSession.RunSequenceArgMax(inputs, reducedRequest, operationToken), CancellationToken.None).ConfigureAwait(false)
                        : reducedSession.RunSequenceArgMax(inputs, reducedRequest, operationToken);
                    inferenceWatch.Stop();
                    decodeWatch.Start();
                    decoded = reducedDecoder.DecodeSequenceArgMax(reduced, input, Selection.Profile, operationToken);
                    decodeWatch.Stop();
                }
                else
                {
                    InferenceOutputs outputs = asynchronous
                        ? await _session.RunAsync(inputs, operationToken).ConfigureAwait(false)
                        : _session.Run(inputs, operationToken);
                    inferenceWatch.Stop();
                    ValidateOutputs(outputs, Selection.Profile);
                    decodeWatch.Start();
                    decoded = Selection.Profile.Decoder.Decode(new VisualDecodeContext(input, Selection.Profile, outputs, operationToken));
                    decodeWatch.Stop();
                }
                return new VisualInferenceResult(decoded, Selection.Profile.Task, Selection.Profile.ModelId, Selection.Backend.Id, new InferenceTiming(TimeSpan.Zero, inferenceWatch.Elapsed, decodeWatch.Elapsed), options.CorrelationId);
            }
            catch (OperationCanceledException exception)
            {
                throw MapCancellation(exception, callerToken);
            }
            catch (DeploySharpException exception) when (operationToken.IsCancellationRequested)
            {
                // Backends may preserve cancellation as a stable DeploySharp exception instead of leaking OperationCanceledException. / 后端可能将取消保留为稳定的 DeploySharp 异常。
                throw MapCancellation(exception, callerToken);
            }
            catch (VisualException) { throw; }
            catch (Exception exception)
            {
                throw new VisualException(VisualErrorCodes.InferenceFailed, "Visual inference failed.", exception, Selection.Profile.ProfileId, backendId: Selection.Backend.Id, modelId: Selection.Profile.ModelId, technicalDetails: exception.ToString());
            }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
                if (entered) _operationGate.Release();
                if (options.DisposeOwnedInputOnCompletion && input.Ownership == PreparedInputOwnership.Owned) input.Dispose();
            }
        }

        private async Task RunManyWorkerAsync(IReadOnlyList<PreparedVisualInput> inputs, VisualInferenceResult[] results, VisualExecutionOptions options, CancellationToken cancellationToken, int[] nextIndex)
        {
            while (true)
            {
                int index = Interlocked.Increment(ref nextIndex[0]);
                if (index >= inputs.Count) return;
                results[index] = await ExecuteAsync(inputs[index], options, true, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<IReadOnlyList<VisualInferenceResult>> RunPrefetchedCoreAsync<TInput>(
            IReadOnlyList<TInput> inputs,
            Func<TInput, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            int prefetch,
            VisualExecutionOptions? options,
            CancellationToken cancellationToken)
        {
            EnsureUsable();
            if (inputs.Count == 0) return Array.Empty<VisualInferenceResult>();

            VisualExecutionOptions effectiveOptions = options ?? VisualExecutionOptions.Default;
            // Prepared values are transient in this API. Borrowed values remain
            // caller-owned; owned resources are released after each execution.
            VisualExecutionOptions runOptions = new VisualExecutionOptions(effectiveOptions.Timeout, disposeOwnedInputOnCompletion: true, effectiveOptions.CorrelationId);
            int capacity = checked(_maximumConcurrency + prefetch);
            var pending = new Queue<Task<VisualInferenceResult>>(Math.Min(capacity, inputs.Count));
            var results = new VisualInferenceResult[inputs.Count];
            int next = 0;
            try
            {
                while (next < inputs.Count && pending.Count < capacity)
                {
                    pending.Enqueue(PrepareAndExecuteAsync(inputs[next], prepareAsync, runOptions, cancellationToken));
                    next++;
                }

                int completed = 0;
                while (pending.Count > 0)
                {
                    results[completed++] = await pending.Dequeue().ConfigureAwait(false);
                    if (next < inputs.Count)
                    {
                        pending.Enqueue(PrepareAndExecuteAsync(inputs[next], prepareAsync, runOptions, cancellationToken));
                        next++;
                    }
                }
                return Array.AsReadOnly(results);
            }
            catch
            {
                // Observe every already-started preparation/inference task so owned
                // inputs can finish their normal ExecuteAsync cleanup path.
                while (pending.Count > 0)
                {
                    try { await pending.Dequeue().ConfigureAwait(false); } catch { }
                }
                throw;
            }
        }

        private async Task<VisualInferenceResult> PrepareAndExecuteAsync<TInput>(TInput value, Func<TInput, CancellationToken, Task<PreparedVisualInput>> prepareAsync, VisualExecutionOptions options, CancellationToken cancellationToken)
        {
            PreparedVisualInput input = await prepareAsync(value, cancellationToken).ConfigureAwait(false);
            if (input == null) throw new ArgumentException("The prefetch callback returned null.", nameof(prepareAsync));
            return await ExecuteAsync(input, options, true, cancellationToken).ConfigureAwait(false);
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
                if (!input.TryGetAuxiliaryInput(auxiliaryBinding.Name, out NamedTensor? supplied) || supplied == null) throw new VisualException(VisualErrorCodes.TensorInvalid, "A required auxiliary input tensor is missing.", profileId: profile.ProfileId, tensorName: auxiliaryBinding.Name, modelId: profile.ModelId);
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
