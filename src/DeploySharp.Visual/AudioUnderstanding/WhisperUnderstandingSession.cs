#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
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
    /// <summary>Owns Whisper Encoder, Prefill, and named Past/Present Decode sessions with one cached feature state. / 拥有 Whisper Encoder、Prefill 与具名 Past/Present Decode Session 以及一个缓存 Feature State。</summary>
    /// <remarks>The session is single-writer. Cross-attention KV is copied once during Prefill and reused during Decode; only self-attention KV is copied per token. / Session 为 Single-writer；Cross-attention KV 只在 Prefill 复制一次并在 Decode 复用，每个 Token 仅复制 Self-attention KV。</remarks>
    public sealed class WhisperUnderstandingSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IInferenceSession _encoder;
        private readonly IInferenceSession _prefill;
        private readonly IInferenceSession _decode;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private CachedState? _state;
        private bool _disposed;
        private int _active;

        /// <summary>Creates all exact named sessions and validates the three graph metadata contracts. / 创建全部精确具名 Session 并校验三张图的 Metadata 合同。</summary>
        public WhisperUnderstandingSession(BackendRegistry registry, AudioUnderstandingBundle bundle, BackendRequest request, SessionOptions? options = null)
        {
            if (registry == null || bundle == null || request == null) throw new ArgumentNullException(registry == null ? nameof(registry) : bundle == null ? nameof(bundle) : nameof(request));
            if (bundle.Profile.Family != AudioUnderstandingFamily.Whisper || bundle.Profile.Generation == null) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "The audio bundle is not an executable Whisper profile.", profileId: bundle.Profile.ProfileId);
            Bundle = bundle;
            SessionOptions requested = options ?? SessionOptions.Default;
            var effectiveRequest = new BackendRequest(request.RequiredCapabilities | BackendCapabilities.TensorInference, request.BackendId, request.Device);
            IInferenceSession? encoder = null; IInferenceSession? prefill = null; IInferenceSession? decode = null;
            try
            {
                encoder = registry.CreateSession(bundle.GetArtifact(AudioArtifactRole.WhisperEncoder), effectiveRequest, new SessionOptions(1, requested.EnableProfiling));
                ValidateMetadata(encoder.Metadata, bundle.Profile.GetArtifact(AudioArtifactRole.WhisperEncoder), bundle.Profile.ProfileId);
                prefill = registry.CreateSession(bundle.GetArtifact(AudioArtifactRole.WhisperDecoderPrefill), effectiveRequest, new SessionOptions(1, requested.EnableProfiling));
                ValidateMetadata(prefill.Metadata, bundle.Profile.GetArtifact(AudioArtifactRole.WhisperDecoderPrefill), bundle.Profile.ProfileId);
                decode = registry.CreateSession(bundle.GetArtifact(AudioArtifactRole.WhisperDecoderWithPast), effectiveRequest, new SessionOptions(1, requested.EnableProfiling));
                ValidateMetadata(decode.Metadata, bundle.Profile.GetArtifact(AudioArtifactRole.WhisperDecoderWithPast), bundle.Profile.ProfileId);
                _encoder = encoder; _prefill = prefill; _decode = decode;
            }
            catch (Exception exception)
            {
                TryDispose(decode); TryDispose(prefill); TryDispose(encoder); _disposeSource.Dispose(); _idle.Dispose();
                if (exception is VisualException) throw;
                throw Failure("Whisper backend sessions could not be created.", exception, bundle.Profile.ProfileId);
            }
        }

        /// <summary>Gets the exact executable bundle. / 获取精确可执行 Bundle。</summary>
        public AudioUnderstandingBundle Bundle { get; }
        /// <summary>Gets whether an Encoder feature state is cached. / 获取是否缓存 Encoder Feature State。</summary>
        public bool HasAudio { get { lock (_gate) { EnsureUsableLocked(); return _state != null; } } }
        /// <summary>Gets the current immutable Encoder state summary. / 获取当前不可变 Encoder State Summary。</summary>
        public WhisperEncodedState? CurrentState { get { lock (_gate) { EnsureUsableLocked(); return _state?.Summary; } } }

        /// <summary>Runs Whisper Encoder and atomically installs one prepared log-Mel input. / 执行 Whisper Encoder 并原子安装一个 Prepared log-Mel 输入。</summary>
        public WhisperEncodedState SetAudio(PreparedWhisperInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
            => SetAudioCoreAsync(input, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Asynchronously runs Whisper Encoder without transferring caller ownership. / 异步执行 Whisper Encoder 且不转移调用方所有权。</summary>
        public Task<WhisperEncodedState> SetAudioAsync(PreparedWhisperInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
            => SetAudioCoreAsync(input, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Runs bounded greedy Whisper Prefill/Decode against the cached feature state. / 针对缓存 Feature State 执行受限 Greedy Whisper Prefill/Decode。</summary>
        public WhisperTranscriptionResult Transcribe(WhisperTokenizer tokenizer, WhisperTranscriptionRequest request, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
            => TranscribeCoreAsync(tokenizer, request, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Asynchronously runs bounded greedy Whisper generation. / 异步执行受限 Greedy Whisper Generation。</summary>
        public Task<WhisperTranscriptionResult> TranscribeAsync(WhisperTokenizer tokenizer, WhisperTranscriptionRequest request, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
            => TranscribeCoreAsync(tokenizer, request, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Clears the cached Encoder state. / 清除缓存的 Encoder State。</summary>
        public void Clear()
        {
            EnterOperation();
            try { lock (_gate) { EnsureUsableLocked(); _state = null; } }
            finally { ExitOperation(); }
        }

        /// <summary>Cancels active work and disposes all three child sessions exactly once. / 取消活动工作并严格一次释放三张子图 Session。</summary>
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; _disposeSource.Cancel(); }
            _idle.Wait();
            try { _state = null; _decode.Dispose(); _prefill.Dispose(); _encoder.Dispose(); }
            finally { _disposeSource.Dispose(); _idle.Dispose(); }
        }

        private async Task<WhisperEncodedState> SetAudioCoreAsync(PreparedWhisperInput input, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CancellationToken dispose = EnterOperation();
            CancellationToken operationToken = dispose;
            CancellationTokenSource? timeout = null;
            CancellationTokenSource? linked = null;
            if (options.Timeout.HasValue) timeout = new CancellationTokenSource(options.Timeout.Value);
            if (caller.CanBeCanceled || timeout != null)
            {
                linked = timeout == null
                    ? CancellationTokenSource.CreateLinkedTokenSource(caller, dispose)
                    : CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token, dispose);
                operationToken = linked.Token;
            }
            try
            {
                operationToken.ThrowIfCancellationRequested(); ValidateInput(input, Bundle.Profile);
                AudioArtifactContract contract = Bundle.Profile.GetArtifact(AudioArtifactRole.WhisperEncoder);
                var watch = Stopwatch.StartNew();
                InferenceOutputs outputs = asynchronous ? await _encoder.RunAsync(InferenceInputs.Create(contract.Inputs[0].Name, input.Tensor), operationToken).ConfigureAwait(false) : _encoder.Run(InferenceInputs.Create(contract.Inputs[0].Name, input.Tensor), operationToken);
                watch.Stop(); ValidateOutputs(outputs, contract, Bundle.Profile.ProfileId);
                Tensor<float> features = CopyFloat(outputs.GetRequired("last_hidden_state"), contract.Outputs[0], Bundle.Profile.ProfileId, "last_hidden_state");
                string featureSha = AudioUnderstandingHash.Floats((float[])features.Buffer);
                string stateIdentity = AudioUnderstandingHash.Text(Bundle.Identity + "|" + input.Identity + "|" + featureSha);
                var summary = new WhisperEncodedState(stateIdentity, input.Identity, Bundle.Profile.ArtifactIdentity, Bundle.Profile.Processor.Identity, features.Shape.ToArray(), featureSha, watch.Elapsed);
                lock (_gate) { EnsureUsableLocked(); _state = new CachedState(features, summary, input.PreprocessTime); }
                return summary;
            }
            catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
            catch (DeploySharpException exception) when (operationToken.IsCancellationRequested) { throw MapCancellation(exception, caller); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw Failure("Whisper Encoder inference failed.", exception, Bundle.Profile.ProfileId); }
            finally { linked?.Dispose(); timeout?.Dispose(); if (options.DisposeOwnedInputOnCompletion) input.Dispose(); ExitOperation(); }
        }

        private async Task<WhisperTranscriptionResult> TranscribeCoreAsync(WhisperTokenizer tokenizer, WhisperTranscriptionRequest request, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (tokenizer == null || request == null) throw new ArgumentNullException(tokenizer == null ? nameof(tokenizer) : nameof(request));
            CancellationToken dispose = EnterOperation();
            CancellationToken operationToken = dispose;
            CancellationTokenSource? timeout = null;
            CancellationTokenSource? linked = null;
            if (options.Timeout.HasValue) timeout = new CancellationTokenSource(options.Timeout.Value);
            if (caller.CanBeCanceled || timeout != null)
            {
                linked = timeout == null
                    ? CancellationTokenSource.CreateLinkedTokenSource(caller, dispose)
                    : CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token, dispose);
                operationToken = linked.Token;
            }
            try
            {
                CachedState state; lock (_gate) { EnsureUsableLocked(); state = _state ?? throw new VisualException(VisualErrorCodes.AudioStateInvalid, "SetAudio must succeed before Whisper transcription.", profileId: Bundle.Profile.ProfileId); }
                ValidateRequest(tokenizer, request, Bundle.Profile); operationToken.ThrowIfCancellationRequested();
                var tokenizeWatch = Stopwatch.StartNew(); WhisperTokenSequence prompt = tokenizer.EncodePrompt(Bundle.Profile, request.IncludeNoTimestamps); tokenizeWatch.Stop();
                long[] promptIds = prompt.CopyTokenIds(); AudioGenerationContract generation = Bundle.Profile.Generation!;
                AudioArtifactContract prefillContract = Bundle.Profile.GetArtifact(AudioArtifactRole.WhisperDecoderPrefill);
                var prefillInputs = new InferenceInputs(new[]
                {
                    new NamedTensor("input_ids", new Tensor<long>(new TensorShape(1, promptIds.Length), promptIds, TensorBufferOwnership.Transfer)),
                    new NamedTensor("encoder_hidden_states", state.Features)
                });
                var prefillWatch = Stopwatch.StartNew();
                InferenceOutputs current = asynchronous ? await _prefill.RunAsync(prefillInputs, operationToken).ConfigureAwait(false) : _prefill.Run(prefillInputs, operationToken);
                prefillWatch.Stop(); ValidateOutputs(current, prefillContract, Bundle.Profile.ProfileId);
                KvValues kv = CopyPrefillKv(current, generation, Bundle.Profile.ProfileId);
                int maximumTokens = request.MaximumTokens ?? generation.MaximumTokens;
                if (maximumTokens > generation.MaximumTokens) maximumTokens = generation.MaximumTokens;
                var completion = new List<int>(Math.Min(maximumTokens, 64)); var decodeTimes = new List<TimeSpan>(Math.Min(maximumTokens, 64));
                while (completion.Count < maximumTokens)
                {
                    operationToken.ThrowIfCancellationRequested(); int selected = SelectToken(current.GetRequired("logits"), generation, request.IncludeNoTimestamps, Bundle.Profile.ProfileId); completion.Add(selected);
                    if (selected == generation.EosTokenId) break;
                    InferenceInputs decodeInputs = CreateDecodeInputs(selected, kv, generation);
                    var decodeWatch = Stopwatch.StartNew(); current = asynchronous ? await _decode.RunAsync(decodeInputs, operationToken).ConfigureAwait(false) : _decode.Run(decodeInputs, operationToken); decodeWatch.Stop(); decodeTimes.Add(decodeWatch.Elapsed);
                    AudioArtifactContract decodeContract = Bundle.Profile.GetArtifact(AudioArtifactRole.WhisperDecoderWithPast); ValidateOutputs(current, decodeContract, Bundle.Profile.ProfileId); kv = CopyDecodeKv(current, kv.Cross, generation, Bundle.Profile.ProfileId);
                }
                var finalWatch = Stopwatch.StartNew(); string text = tokenizer.DecodeText(completion); finalWatch.Stop();
                return new WhisperTranscriptionResult(text, completion, request, state.Summary, new WhisperExecutionTiming(state.PreprocessTime, state.Summary.EncodeTime, tokenizeWatch.Elapsed, prefillWatch.Elapsed, decodeTimes, finalWatch.Elapsed), Bundle.Profile.ProfileId, Bundle.Identity);
            }
            catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
            catch (DeploySharpException exception) when (operationToken.IsCancellationRequested) { throw MapCancellation(exception, caller); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw Failure("Whisper Prefill/Decode inference failed.", exception, Bundle.Profile.ProfileId); }
            finally { linked?.Dispose(); timeout?.Dispose(); ExitOperation(); }
        }

        private static void ValidateInput(PreparedWhisperInput input, AudioUnderstandingProfile profile)
        {
            input.EnsureUsable();
            if (!string.Equals(input.ProfileId, profile.ProfileId, StringComparison.Ordinal) || !string.Equals(input.ProfileIdentity, profile.Identity, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "Prepared Whisper features differ from the active profile.", profileId: profile.ProfileId);
            AudioArtifactContract contract = profile.GetArtifact(AudioArtifactRole.WhisperEncoder);
            if (!string.Equals(input.InputName, contract.Inputs[0].Name, StringComparison.Ordinal) || input.Tensor.ElementType != TensorElementType.Float32 || !contract.Inputs[0].Matches(input.Tensor.Shape) || input.Tensor.Length > contract.Inputs[0].MaximumElements) throw AudioFailure.Contract("Prepared Whisper features differ from the encoder contract.", profile.ProfileId, input.InputName);
        }

        private static void ValidateRequest(WhisperTokenizer tokenizer, WhisperTranscriptionRequest request, AudioUnderstandingProfile profile)
        {
            if (!profile.Tasks.Contains(AudioUnderstandingTask.AutomaticSpeechRecognition)) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "Whisper automatic speech recognition is unavailable.", profileId: profile.ProfileId);
            AudioGenerationContract generation = profile.Generation!;
            if (!string.Equals(tokenizer.Contract.TokenizerSha256, generation.TokenizerSha256, StringComparison.OrdinalIgnoreCase) || !string.Equals(tokenizer.Contract.GenerationConfigSha256, generation.GenerationConfigSha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "The Whisper tokenizer differs from the profile generation contract.", profileId: profile.ProfileId);
            if (request.MaximumTokens.HasValue && request.MaximumTokens.Value > generation.MaximumTokens) throw new VisualException(VisualErrorCodes.AudioLimitExceeded, "The Whisper token limit exceeds the profile contract.", profileId: profile.ProfileId);
        }

        private static InferenceInputs CreateDecodeInputs(int token, KvValues kv, AudioGenerationContract generation)
        {
            var values = new List<NamedTensor>(1 + generation.KvLayers * 4) { new NamedTensor("input_ids", new Tensor<long>(new TensorShape(1, 1), new[] { (long)token }, TensorBufferOwnership.Transfer)) };
            for (int layer = 0; layer < generation.KvLayers; layer++)
            {
                values.Add(new NamedTensor(generation.Past(layer, true, true), kv.Self[layer * 2])); values.Add(new NamedTensor(generation.Past(layer, true, false), kv.Self[(layer * 2) + 1]));
                values.Add(new NamedTensor(generation.Past(layer, false, true), kv.Cross[layer * 2])); values.Add(new NamedTensor(generation.Past(layer, false, false), kv.Cross[(layer * 2) + 1]));
            }
            return new InferenceInputs(values);
        }

        private static KvValues CopyPrefillKv(InferenceOutputs outputs, AudioGenerationContract generation, string profileId)
        {
            var self = new List<Tensor<float>>(generation.KvLayers * 2); var cross = new List<Tensor<float>>(generation.KvLayers * 2);
            for (int layer = 0; layer < generation.KvLayers; layer++)
            {
                self.Add(CopyKv(outputs.GetRequired(generation.Present(layer, true, true)), generation, true, generation.Present(layer, true, true), profileId)); self.Add(CopyKv(outputs.GetRequired(generation.Present(layer, true, false)), generation, true, generation.Present(layer, true, false), profileId));
                cross.Add(CopyKv(outputs.GetRequired(generation.Present(layer, false, true)), generation, false, generation.Present(layer, false, true), profileId)); cross.Add(CopyKv(outputs.GetRequired(generation.Present(layer, false, false)), generation, false, generation.Present(layer, false, false), profileId));
            }
            return new KvValues(self, cross);
        }

        private static KvValues CopyDecodeKv(InferenceOutputs outputs, IReadOnlyList<Tensor<float>> cross, AudioGenerationContract generation, string profileId)
        {
            var self = new List<Tensor<float>>(generation.KvLayers * 2);
            for (int layer = 0; layer < generation.KvLayers; layer++)
            {
                self.Add(CopyKv(outputs.GetRequired(generation.Present(layer, true, true)), generation, true, generation.Present(layer, true, true), profileId)); self.Add(CopyKv(outputs.GetRequired(generation.Present(layer, true, false)), generation, true, generation.Present(layer, true, false), profileId));
                ValidateKvShape(outputs.GetRequired(generation.Present(layer, false, true)), generation, false, generation.Present(layer, false, true), profileId); ValidateKvShape(outputs.GetRequired(generation.Present(layer, false, false)), generation, false, generation.Present(layer, false, false), profileId);
            }
            return new KvValues(self, cross);
        }

        private static Tensor<float> CopyKv(ITensor tensor, AudioGenerationContract generation, bool decoder, string name, string profileId)
        {
            ValidateKvShape(tensor, generation, decoder, name, profileId); float[] values = ((float[])tensor.Buffer).ToArray();
            for (int index = 0; index < values.Length; index++)
            {
                float value = values[index];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.AudioNonFinite, "Whisper KV contains NaN or Infinity.", profileId: profileId, tensorName: name);
            }
            return new Tensor<float>(new TensorShape(tensor.Shape.ToArray()), values, TensorBufferOwnership.Transfer);
        }

        private static void ValidateKvShape(ITensor tensor, AudioGenerationContract generation, bool decoder, string name, string profileId)
        {
            int maximum = decoder ? generation.MaximumTokens : generation.MaximumEncoderFrames;
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 4 || tensor.Shape[0] != 1 || tensor.Shape[1] != generation.KvHeads || tensor.Shape[2] <= 0 || tensor.Shape[2] > maximum || tensor.Shape[3] != generation.KvHeadDimension || tensor.Length > (long)generation.KvHeads * maximum * generation.KvHeadDimension) throw new VisualException(VisualErrorCodes.AudioContractInvalid, "Whisper KV type, axes, or capacity differs from the generation contract.", profileId: profileId, tensorName: name);
        }

        private static int SelectToken(ITensor tensor, AudioGenerationContract generation, bool noTimestamps, string profileId)
        {
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 3 || tensor.Shape[0] != 1 || tensor.Shape[1] <= 0 || tensor.Shape[2] != generation.VocabularySize) throw new VisualException(VisualErrorCodes.AudioContractInvalid, "Whisper logits type or shape differs from the generation contract.", profileId: profileId, tensorName: "logits");
            float[] values = (float[])tensor.Buffer; int offset = checked(((int)tensor.Shape[1] - 1) * generation.VocabularySize); int selected = -1; float maximum = float.NegativeInfinity;
            for (int token = 0; token < generation.VocabularySize; token++)
            {
                if (noTimestamps && token >= generation.TimestampBeginTokenId) continue;
                float value = values[offset + token]; if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.AudioNonFinite, "Whisper logits contain NaN or Infinity.", profileId: profileId, tensorName: "logits");
                if (value > maximum) { maximum = value; selected = token; }
            }
            if (selected < 0) throw new VisualException(VisualErrorCodes.AudioContractInvalid, "No selectable Whisper token remained.", profileId: profileId, tensorName: "logits");
            return selected;
        }

        private static Tensor<float> CopyFloat(ITensor tensor, AudioTensorContract contract, string profileId, string name)
        {
            if (tensor.ElementType != TensorElementType.Float32 || !contract.Matches(tensor.Shape) || tensor.Length > contract.MaximumElements) throw AudioFailure.Contract("Whisper runtime tensor differs from the profile.", profileId, name);
            float[] values = ((float[])tensor.Buffer).ToArray();
            for (int index = 0; index < values.Length; index++)
            {
                float value = values[index];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.AudioNonFinite, "Whisper runtime tensor contains NaN or Infinity.", profileId: profileId, tensorName: name);
            }
            return new Tensor<float>(new TensorShape(tensor.Shape.ToArray()), values, TensorBufferOwnership.Transfer);
        }

        private static void ValidateMetadata(ModelMetadata metadata, AudioArtifactContract contract, string profileId)
        {
            if (!metadata.Inputs.Select(value => value.Name).SequenceEqual(contract.Inputs.Select(value => value.Name), StringComparer.Ordinal) || !metadata.Outputs.Select(value => value.Name).SequenceEqual(contract.Outputs.Select(value => value.Name), StringComparer.Ordinal)) throw AudioFailure.Contract("Backend named-port order differs from the Whisper profile.", profileId, modelId: contract.ModelId);
            for (int index = 0; index < metadata.Inputs.Count; index++) ValidateDescriptor(metadata.Inputs[index], contract.Inputs[index], profileId);
            for (int index = 0; index < metadata.Outputs.Count; index++) ValidateDescriptor(metadata.Outputs[index], contract.Outputs[index], profileId);
        }

        private static void ValidateDescriptor(TensorDescriptor descriptor, AudioTensorContract contract, string profileId)
        {
            if (descriptor.ElementType != contract.ElementType || descriptor.Shape.Rank != contract.ShapePattern.Rank) throw AudioFailure.Contract("Backend tensor type or rank differs from the Whisper profile.", profileId, contract.Name);
            for (int index = 0; index < descriptor.Shape.Rank; index++) if (descriptor.Shape[index] > 0 && contract.ShapePattern[index] > 0 && descriptor.Shape[index] != contract.ShapePattern[index]) throw AudioFailure.Contract("Backend fixed dimension differs from the Whisper profile.", profileId, contract.Name);
        }

        private static void ValidateOutputs(InferenceOutputs outputs, AudioArtifactContract contract, string profileId)
        {
            if (!outputs.Select(value => value.Name).SequenceEqual(contract.Outputs.Select(value => value.Name), StringComparer.Ordinal)) throw AudioFailure.Contract("Runtime output names or order differ from the Whisper profile.", profileId);
            for (int index = 0; index < contract.Outputs.Count; index++)
            {
                ITensor tensor = outputs.GetRequired(contract.Outputs[index].Name); if (tensor.ElementType != contract.Outputs[index].ElementType || !contract.Outputs[index].Matches(tensor.Shape) || tensor.Length > contract.Outputs[index].MaximumElements) throw AudioFailure.Contract("Runtime output differs from the Whisper profile.", profileId, contract.Outputs[index].Name);
            }
        }

        private CancellationToken EnterOperation()
        {
            lock (_gate) EnsureUsableLocked();
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) throw new VisualException(VisualErrorCodes.AudioConcurrentOperation, "The single-writer Whisper session is already executing.", profileId: Bundle.Profile.ProfileId);
            _idle.Reset(); lock (_gate) { if (_disposed) { ExitOperation(); throw new VisualException(VisualErrorCodes.AudioDisposed, "The Whisper session is disposed.", profileId: Bundle.Profile.ProfileId); } return _disposeSource.Token; }
        }
        private void ExitOperation() { Interlocked.Exchange(ref _active, 0); _idle.Set(); }
        private void EnsureUsableLocked() { if (_disposed) throw new VisualException(VisualErrorCodes.AudioDisposed, "The Whisper session is disposed.", profileId: Bundle.Profile.ProfileId); }
        private VisualException MapCancellation(Exception exception, CancellationToken caller)
        {
            if (_disposeSource.IsCancellationRequested) return new VisualException(VisualErrorCodes.AudioDisposed, "The Whisper session was disposed during execution.", exception, profileId: Bundle.Profile.ProfileId);
            if (caller.IsCancellationRequested) return new VisualException(VisualErrorCodes.AudioCancelled, "The Whisper operation was cancelled.", exception, profileId: Bundle.Profile.ProfileId);
            return new VisualException(VisualErrorCodes.AudioTimeout, "The Whisper operation timed out.", exception, profileId: Bundle.Profile.ProfileId);
        }
        private static VisualException Failure(string message, Exception exception, string profileId) => new VisualException(VisualErrorCodes.AudioInferenceFailed, message, exception, profileId: profileId);
        private static void TryDispose(IDisposable? value) { try { value?.Dispose(); } catch { } }

        private sealed class CachedState
        {
            internal CachedState(Tensor<float> features, WhisperEncodedState summary, TimeSpan preprocessTime) { Features = features; Summary = summary; PreprocessTime = preprocessTime; }
            internal Tensor<float> Features { get; }
            internal WhisperEncodedState Summary { get; }
            internal TimeSpan PreprocessTime { get; }
        }

        private sealed class KvValues
        {
            internal KvValues(IReadOnlyList<Tensor<float>> self, IReadOnlyList<Tensor<float>> cross) { Self = self; Cross = cross; }
            internal IReadOnlyList<Tensor<float>> Self { get; }
            internal IReadOnlyList<Tensor<float>> Cross { get; }
        }
    }
}
#endif
