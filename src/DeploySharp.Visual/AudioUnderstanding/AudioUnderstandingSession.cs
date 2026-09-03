using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Owns one exact CTC backend session and one atomically cached waveform state. / 拥有一个精确 CTC 后端 Session 与一个原子缓存波形 State。</summary>
    /// <remarks>The session is single-writer. Registry, vocabulary, prepared inputs, and source files remain caller-owned; cancellation and timeout publish no partial result. / Session 为 Single-writer；Registry、词表、Prepared Input 与源文件保持调用方所有；取消和超时不发布部分结果。</remarks>
    public sealed class AudioUnderstandingSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IInferenceSession _session;
        private readonly AudioCtcDecoder _decoder;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private CachedAudio? _state;
        private bool _disposed;
        private int _active;

        /// <summary>Creates one exact CTC session and rejects source-only, mixed, or incorrectly named artifacts. / 创建一个精确 CTC Session，并拒绝仅源、混合或名称错误的工件。</summary>
        public AudioUnderstandingSession(BackendRegistry registry, AudioUnderstandingBundle bundle, Wav2Vec2CtcVocabulary vocabulary, BackendRequest request, SessionOptions? options = null)
        {
            if (registry == null || bundle == null || vocabulary == null || request == null) throw new ArgumentNullException(registry == null ? nameof(registry) : bundle == null ? nameof(bundle) : vocabulary == null ? nameof(vocabulary) : nameof(request));
            if (bundle.Profile.Family != AudioUnderstandingFamily.Wav2Vec2 || bundle.Profile.Tokenizer == null || !string.Equals(bundle.Profile.Tokenizer.Identity, vocabulary.Contract.Identity, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "The CTC vocabulary differs from the executable audio profile.", profileId: bundle.Profile.ProfileId);
            Bundle = bundle; Vocabulary = vocabulary; _decoder = new AudioCtcDecoder(vocabulary, bundle.Profile.Timestamps);
            AudioArtifactContract contract = bundle.Profile.GetArtifact(AudioArtifactRole.CtcEncoderHead);
            var effectiveRequest = new BackendRequest(request.RequiredCapabilities | BackendCapabilities.TensorInference, request.BackendId, request.Device);
            try
            {
                _session = registry.CreateSession(bundle.GetArtifact(AudioArtifactRole.CtcEncoderHead), effectiveRequest, options ?? new SessionOptions(1, false));
                ValidateMetadata(_session.Metadata, contract, bundle.Profile.ProfileId);
            }
            catch (Exception exception)
            {
                _disposeSource.Dispose(); _idle.Dispose();
                if (exception is VisualException) throw;
                throw Failure("Audio backend session could not be created.", exception, bundle.Profile.ProfileId);
            }
        }

        /// <summary>Gets exact executable bundle. / 获取精确可执行 Bundle。</summary>
        public AudioUnderstandingBundle Bundle { get; }
        /// <summary>Gets verified CTC vocabulary. / 获取已验证 CTC 词表。</summary>
        public Wav2Vec2CtcVocabulary Vocabulary { get; }
        /// <summary>Gets whether one waveform is cached. / 获取是否缓存一个波形。</summary>
        public bool HasAudio { get { lock (_gate) { EnsureUsableLocked(); return _state != null; } } }
        /// <summary>Gets immutable cached state summary. / 获取不可变缓存 State Summary。</summary>
        public AudioStateSummary? CurrentState { get { lock (_gate) { EnsureUsableLocked(); return _state?.Summary; } } }

        /// <summary>Copies and atomically installs one prepared waveform; no backend inference occurs. / 复制并原子安装一个 Prepared 波形；此时不执行后端推理。</summary>
        public AudioStateSummary SetAudio(PreparedAudioInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
            => SetAudioCore(input, options ?? VisualExecutionOptions.Default, cancellationToken);

        /// <summary>Asynchronously exposes the same atomic SetAudio contract without transferring caller ownership. / 异步公开同一原子 SetAudio 合同且不转移调用方所有权。</summary>
        public Task<AudioStateSummary> SetAudioAsync(PreparedAudioInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            try { return Task.FromResult(SetAudioCore(input, options ?? VisualExecutionOptions.Default, cancellationToken)); }
            catch (Exception exception) { return Task.FromException<AudioStateSummary>(exception); }
        }

        /// <summary>Runs one complete CTC graph and deterministic decode against the cached waveform. / 针对缓存波形执行一次完整 CTC 图与确定性解码。</summary>
        public AudioTranscriptionResult Transcribe(AudioTranscriptionRequest request, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
            => TranscribeCoreAsync(request, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Asynchronously runs one complete CTC graph; cancellation publishes no partial result. / 异步执行一次完整 CTC 图；取消不发布部分结果。</summary>
        public Task<AudioTranscriptionResult> TranscribeAsync(AudioTranscriptionRequest request, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
            => TranscribeCoreAsync(request, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Clears cached audio; transcription then requires another successful SetAudio. / 清除缓存音频；之后转录要求再次成功 SetAudio。</summary>
        public void Clear()
        {
            EnterOperation();
            try { lock (_gate) { EnsureUsableLocked(); _state = null; } }
            finally { ExitOperation(); }
        }

        /// <summary>Resets all mutable waveform state; equivalent to Clear for this stateless CTC graph. / 重置全部可变波形 State；对此无状态 CTC 图等同于 Clear。</summary>
        public void Reset() => Clear();

        /// <summary>Cancels active work, waits for unwind, clears state, and disposes the child session exactly once. / 取消活动工作、等待回卷、清除 State 并严格一次释放子 Session。</summary>
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; _disposeSource.Cancel(); }
            _idle.Wait();
            try { _state = null; _session.Dispose(); }
            finally { _disposeSource.Dispose(); _idle.Dispose(); }
        }

        private AudioStateSummary SetAudioCore(PreparedAudioInput input, VisualExecutionOptions options, CancellationToken caller)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CancellationToken dispose = EnterOperation();
            CancellationTokenSource? timeoutSource = null;
            CancellationTokenSource? linked = null;
            CancellationToken operationToken = dispose;
            try
            {
                if (options.Timeout.HasValue) timeoutSource = new CancellationTokenSource(options.Timeout.Value);
                if (caller.CanBeCanceled || timeoutSource != null)
                {
                    linked = timeoutSource == null
                        ? CancellationTokenSource.CreateLinkedTokenSource(caller, dispose)
                        : CancellationTokenSource.CreateLinkedTokenSource(caller, timeoutSource.Token, dispose);
                    operationToken = linked.Token;
                }
                operationToken.ThrowIfCancellationRequested(); ValidateInput(input, Bundle.Profile);
                float[] copy = ((float[])input.Tensor.Buffer).ToArray(); operationToken.ThrowIfCancellationRequested();
                var tensor = new Tensor<float>(new TensorShape(input.Tensor.Shape.ToArray()), copy, TensorBufferOwnership.Transfer);
                string stateIdentity = AudioUnderstandingHash.Text(Bundle.Identity + "|" + input.Identity + "|" + input.FeatureSha256);
                var summary = new AudioStateSummary(stateIdentity, input); var state = new CachedAudio(tensor, summary);
                lock (_gate) { EnsureUsableLocked(); _state = state; }
                return summary;
            }
            catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
            catch (DeploySharpException exception) when (operationToken.IsCancellationRequested) { throw MapCancellation(exception, caller); }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
                if (options.DisposeOwnedInputOnCompletion) input.Dispose();
                ExitOperation();
            }
        }

        private async Task<AudioTranscriptionResult> TranscribeCoreAsync(AudioTranscriptionRequest request, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            CancellationToken dispose = EnterOperation();
            CancellationTokenSource? timeoutSource = null;
            CancellationTokenSource? linked = null;
            CancellationToken operationToken = dispose;
            try
            {
                if (options.Timeout.HasValue) timeoutSource = new CancellationTokenSource(options.Timeout.Value);
                if (caller.CanBeCanceled || timeoutSource != null)
                {
                    linked = timeoutSource == null
                        ? CancellationTokenSource.CreateLinkedTokenSource(caller, dispose)
                        : CancellationTokenSource.CreateLinkedTokenSource(caller, timeoutSource.Token, dispose);
                    operationToken = linked.Token;
                }
                ValidateRequest(request, Bundle.Profile); CachedAudio state;
                lock (_gate) { EnsureUsableLocked(); state = _state ?? throw new VisualException(VisualErrorCodes.AudioStateInvalid, "SetAudio must succeed before transcription.", profileId: Bundle.Profile.ProfileId); }
                operationToken.ThrowIfCancellationRequested(); AudioArtifactContract contract = Bundle.Profile.GetArtifact(AudioArtifactRole.CtcEncoderHead);
                var inferenceWatch = Stopwatch.StartNew();
                InferenceOutputs outputs = asynchronous
                    ? await _session.RunAsync(InferenceInputs.Create(contract.Inputs[0].Name, state.InputValues), operationToken).ConfigureAwait(false)
                    : _session.Run(InferenceInputs.Create(contract.Inputs[0].Name, state.InputValues), operationToken);
                inferenceWatch.Stop(); ValidateOutputs(outputs, contract, Bundle.Profile.ProfileId);
                var decodeWatch = Stopwatch.StartNew(); AudioCtcDecodedResult decoded = _decoder.Decode(outputs.GetRequired("logits"), request.IncludeCtcTokenTimestamps); decodeWatch.Stop();
                return new AudioTranscriptionResult(request, decoded, state.Summary, Bundle.Profile, new AudioExecutionTiming(state.Summary.PreprocessTime, inferenceWatch.Elapsed, decodeWatch.Elapsed));
            }
            catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
            catch (DeploySharpException exception) when (operationToken.IsCancellationRequested) { throw MapCancellation(exception, caller); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw Failure("Audio CTC inference failed.", exception, Bundle.Profile.ProfileId); }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
                ExitOperation();
            }
        }

        private static void ValidateInput(PreparedAudioInput input, AudioUnderstandingProfile profile)
        {
            input.EnsureUsable();
            if (input.Chunk != null) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "Chunked or streaming CTC state is not verified in Stage 28.", profileId: profile.ProfileId);
            if (!string.Equals(input.ProfileId, profile.ProfileId, StringComparison.Ordinal) || !string.Equals(input.ProfileIdentity, profile.Identity, StringComparison.Ordinal) || !string.Equals(input.ProcessorIdentity, profile.Processor.Identity, StringComparison.Ordinal) || !string.Equals(input.FeatureIdentity, profile.Processor.FeatureIdentity, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "Prepared audio differs from the active profile.", profileId: profile.ProfileId);
            if (input.Tensor.ElementType != TensorElementType.Float32 || input.Tensor.Shape.Rank != 2 || input.Tensor.Shape[0] != 1 || input.Tensor.Shape[1] <= 0 || input.Tensor.Shape[1] > profile.Processor.MaximumSamples) throw AudioFailure.Contract("Prepared audio tensor differs from the profile.", profile.ProfileId, input.InputName);
            foreach (float value in (float[])input.Tensor.Buffer) if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.AudioNonFinite, "Prepared audio contains NaN or Infinity.", profileId: profile.ProfileId, tensorName: input.InputName);
        }

        private static void ValidateRequest(AudioTranscriptionRequest request, AudioUnderstandingProfile profile)
        {
            if (!profile.Tasks.Contains(request.Task)) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "The requested audio task is unavailable.", profileId: profile.ProfileId);
            if (profile.Tokenizer == null || !string.Equals(request.Language, profile.Tokenizer.Language, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "The requested language differs from this English-only CTC profile.", profileId: profile.ProfileId);
            if (request.IncludeCtcTokenTimestamps && profile.Timestamps.Ownership != AudioTimestampOwnership.CtcFrameStride) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "CTC token timestamps are unavailable.", profileId: profile.ProfileId);
        }

        private static void ValidateMetadata(ModelMetadata metadata, AudioArtifactContract contract, string profileId)
        {
            if (!metadata.Inputs.Select(value => value.Name).SequenceEqual(contract.Inputs.Select(value => value.Name), StringComparer.Ordinal) || !metadata.Outputs.Select(value => value.Name).SequenceEqual(contract.Outputs.Select(value => value.Name), StringComparer.Ordinal)) throw AudioFailure.Contract("Backend named-port order differs from the audio profile.", profileId, modelId: contract.ModelId);
            for (int index = 0; index < metadata.Inputs.Count; index++) ValidateDescriptor(metadata.Inputs[index], contract.Inputs[index], profileId);
            for (int index = 0; index < metadata.Outputs.Count; index++) ValidateDescriptor(metadata.Outputs[index], contract.Outputs[index], profileId);
        }

        private static void ValidateDescriptor(TensorDescriptor descriptor, AudioTensorContract contract, string profileId)
        {
            if (descriptor.ElementType != contract.ElementType || descriptor.Shape.Rank != contract.ShapePattern.Rank) throw AudioFailure.Contract("Backend tensor type or rank differs from the audio profile.", profileId, contract.Name);
            for (int index = 0; index < descriptor.Shape.Rank; index++) if (descriptor.Shape[index] > 0 && contract.ShapePattern[index] > 0 && descriptor.Shape[index] != contract.ShapePattern[index]) throw AudioFailure.Contract("Backend fixed dimension differs from the audio profile.", profileId, contract.Name);
        }

        private static void ValidateOutputs(InferenceOutputs outputs, AudioArtifactContract contract, string profileId)
        {
            if (!outputs.Select(value => value.Name).SequenceEqual(contract.Outputs.Select(value => value.Name), StringComparer.Ordinal)) throw AudioFailure.Contract("Runtime output names or order differ from the audio profile.", profileId);
            ITensor logits = outputs.GetRequired("logits"); AudioTensorContract expected = contract.Outputs[0];
            if (logits.ElementType != expected.ElementType || !expected.Matches(logits.Shape) || logits.Length > expected.MaximumElements) throw AudioFailure.Contract("Runtime logits differ from the audio profile.", profileId, "logits");
        }

        private CancellationToken EnterOperation()
        {
            lock (_gate) EnsureUsableLocked();
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) throw new VisualException(VisualErrorCodes.AudioConcurrentOperation, "The single-writer audio session is already executing.", profileId: Bundle.Profile.ProfileId);
            _idle.Reset(); lock (_gate) { if (_disposed) { ExitOperation(); throw new VisualException(VisualErrorCodes.AudioDisposed, "The audio session is disposed.", profileId: Bundle.Profile.ProfileId); } return _disposeSource.Token; }
        }
        private void ExitOperation() { Interlocked.Exchange(ref _active, 0); _idle.Set(); }
        private void EnsureUsableLocked() { if (_disposed) throw new VisualException(VisualErrorCodes.AudioDisposed, "The audio session is disposed.", profileId: Bundle.Profile.ProfileId); }
        private VisualException MapCancellation(Exception exception, CancellationToken caller)
        {
            if (_disposeSource.IsCancellationRequested) return new VisualException(VisualErrorCodes.AudioDisposed, "The audio session was disposed during execution.", exception, profileId: Bundle.Profile.ProfileId);
            if (caller.IsCancellationRequested) return new VisualException(VisualErrorCodes.AudioCancelled, "The audio operation was cancelled.", exception, profileId: Bundle.Profile.ProfileId);
            return new VisualException(VisualErrorCodes.AudioTimeout, "The audio operation timed out.", exception, profileId: Bundle.Profile.ProfileId);
        }
        private static VisualException Failure(string message, Exception exception, string profileId) => new VisualException(VisualErrorCodes.AudioInferenceFailed, message, exception, profileId: profileId);

        private sealed class CachedAudio
        {
            internal CachedAudio(Tensor<float> inputValues, AudioStateSummary summary) { InputValues = inputValues; Summary = summary; }
            internal Tensor<float> InputValues { get; } internal AudioStateSummary Summary { get; }
        }
    }
}
