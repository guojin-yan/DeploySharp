using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Owns exact vision-encoder/decoder sessions and one profile-bound cached image state. / 拥有精确 Vision Encoder/Decoder Session 与一个 Profile 绑定的缓存图像状态。</summary>
    /// <remarks>Set-image, generation, clear, and disposal are serialized; concurrent calls fail deterministically and the registry/tokenizer remain caller-owned. / Set-image、生成、清除与 Dispose 串行执行；并发调用确定失败，Registry/Tokenizer 仍由调用方拥有。</remarks>
    public sealed class GenerativeVisionLanguageSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IInferenceSession _visionEncoder;
        private readonly IInferenceSession _decoder;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private ImageState? _imageState;
        private bool _disposed;
        private int _active;

        /// <summary>Creates and validates every exact named backend session in an executable full-prefix bundle. / 创建并校验可执行全前缀 Bundle 中每个精确具名 Backend Session。</summary>
        public GenerativeVisionLanguageSession(BackendRegistry registry, GenerativeVisionLanguageArtifactBundle bundle, BackendRequest request, SessionOptions? sessionOptions = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (bundle.Profile.Artifacts.Count != 2 || bundle.Profile.Generation.Mode != GenerativeVisionLanguageGenerationMode.Greedy || bundle.Profile.Generation.CacheMode != GenerativeVisionLanguageCacheMode.NoneFullPrefix || bundle.Profile.Generation.NumberOfBeams != 1 || bundle.Profile.Generation.Temperature != 1f || bundle.Profile.Generation.TopP != 1f || bundle.Profile.Generation.RepetitionPenalty != 1f) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "This session executes only the audited greedy full-prefix no-KV two-graph contract.", profileId: bundle.Profile.ProfileId);
            SessionOptions requested = sessionOptions ?? SessionOptions.Default;
            var stateful = new SessionOptions(1, requested.EnableProfiling);
            var effectiveRequest = new BackendRequest(request.RequiredCapabilities | BackendCapabilities.TensorInference, request.BackendId, request.Device);
            IInferenceSession? vision = null;
            try
            {
                GenerativeVisionLanguageArtifactContract visionContract = bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder);
                GenerativeVisionLanguageArtifactContract decoderContract = bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder);
                vision = registry.CreateSession(bundle.GetArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder), effectiveRequest, stateful);
                ValidateMetadata(vision.Metadata, visionContract, bundle.Profile.ProfileId);
                _decoder = registry.CreateSession(bundle.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder), effectiveRequest, stateful);
                ValidateMetadata(_decoder.Metadata, decoderContract, bundle.Profile.ProfileId);
                _visionEncoder = vision;
            }
            catch (Exception exception)
            {
                vision?.Dispose();
                _disposeSource.Dispose();
                _idle.Dispose();
                if (exception is VisualException) throw;
                throw Failure("The generative vision-language backend sessions could not be created.", exception, bundle.Profile.ProfileId);
            }
        }

        /// <summary>Gets immutable artifact bundle. / 获取不可变工件 Bundle。</summary>
        public GenerativeVisionLanguageArtifactBundle Bundle { get; }
        /// <summary>Gets whether one exact image is cached. / 获取是否缓存了一个精确图像。</summary>
        public bool HasImage { get { lock (_gate) { EnsureUsableLocked(); return _imageState != null; } } }
        /// <summary>Gets current image identity, or null before set-image/after clear. / 获取当前图像 Identity；set-image 前或 clear 后为 null。</summary>
        public GenerativeVisionLanguageImageIdentity? CurrentImage { get { lock (_gate) { EnsureUsableLocked(); return _imageState?.PublicState.Identity; } } }

        /// <summary>Runs the vision encoder once and atomically replaces cached state only after complete validation. / 单次运行 Vision Encoder，并仅在完整校验后原子替换缓存状态。</summary>
        public GenerativeVisionLanguageImageState SetImage(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SetImageCoreAsync(input, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Asynchronously encodes one prepared image; cancellation never installs partial state and owned input disposal follows execution options. / 异步编码一个已准备图像；取消不会安装部分状态，自有输入释放遵循执行选项。</summary>
        public Task<GenerativeVisionLanguageImageState> SetImageAsync(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SetImageCoreAsync(input, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Generates one owned Caption/VQA result from cached image state using exact named full-prefix decoder steps. / 使用精确具名全前缀 Decoder Step 从缓存图像状态生成一个自有 Caption/VQA 结果。</summary>
        /// <remarks>The optional callback observes owned immutable chunks. Callback exceptions abort the operation and no reusable partial generation state is published. / 可选回调观察自有不可变 Chunk；回调异常会终止操作且不发布可复用的部分生成状态。</remarks>
        public GenerativeVisionLanguageResult Generate(GenerativeVisionLanguageRequest request, IGenerativeVisionLanguageTokenizer tokenizer, Action<GenerationChunk>? stream = null, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => GenerateCoreAsync(request, tokenizer, stream, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Asynchronously generates one owned result; cancellation/timeout never corrupts cached image state or exposes partial tokens as a result. / 异步生成一个自有结果；取消/超时不会污染缓存图像状态，也不会把部分 Token 作为结果公开。</summary>
        public Task<GenerativeVisionLanguageResult> GenerateAsync(GenerativeVisionLanguageRequest request, IGenerativeVisionLanguageTokenizer tokenizer, Action<GenerationChunk>? stream = null, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => GenerateCoreAsync(request, tokenizer, stream, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Clears cached image/encoder state; later generation fails until another successful set-image. / 清除缓存图像/Encoder State；之后生成在另一次成功 set-image 前失败。</summary>
        public void ClearImage()
        {
            EnterOperation();
            try { lock (_gate) { EnsureUsableLocked(); _imageState = null; } }
            finally { ExitOperation(); }
        }

        /// <summary>Disposes the owned vision/decoder sessions and synchronization resources exactly once. / 仅一次释放自有 Vision/Decoder Session 与同步资源。</summary>
        /// <remarks>Cancels an active operation, waits for unwind, clears image state, then disposes both owned sessions exactly once. / 取消活动操作并等待退出，清除图像状态，再仅一次释放两条自有 Session。</remarks>
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; _disposeSource.Cancel(); }
            _idle.Wait();
            try { _imageState = null; _decoder.Dispose(); _visionEncoder.Dispose(); }
            finally { _disposeSource.Dispose(); _idle.Dispose(); }
        }

        private async Task<GenerativeVisionLanguageImageState> SetImageCoreAsync(PreparedVisualInput input, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CancellationToken dispose = EnterOperation();
            CancellationTokenSource? timeoutSource = null;
            CancellationTokenSource? linked = null;
            CancellationToken operationToken = dispose;
            try
            {
                if (options.Timeout.HasValue)
                {
                    timeoutSource = new CancellationTokenSource(options.Timeout.Value);
                }
                if (caller.CanBeCanceled || timeoutSource != null)
                {
                    linked = timeoutSource == null
                        ? CancellationTokenSource.CreateLinkedTokenSource(caller, dispose)
                        : CancellationTokenSource.CreateLinkedTokenSource(caller, timeoutSource.Token, dispose);
                    operationToken = linked.Token;
                }
                input.EnsureUsable();
                GenerativeVisionLanguageArtifactContract contract = Bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder);
                ValidatePreparedInput(input, contract, Bundle.Profile);
                var watch = Stopwatch.StartNew();
                InferenceOutputs outputs = asynchronous ? await _visionEncoder.RunAsync(InferenceInputs.Create(contract.Inputs[0].Name, input.Tensor), operationToken).ConfigureAwait(false) : _visionEncoder.Run(InferenceInputs.Create(contract.Inputs[0].Name, input.Tensor), operationToken);
                watch.Stop();
                ValidateOutputs(outputs, contract, Bundle.Profile.ProfileId);
                GenerativeVisionLanguageTensorContract outputContract = contract.Outputs.Single();
                Tensor<float> encoderState = CopyFiniteFloatTensor(outputs.GetRequired(outputContract.Name), outputContract, Bundle.Profile.ProfileId);
                string sourceSha = input.InputId ?? throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "Set-image requires PreparedVisualInput.InputId to be the exact encoded-source SHA256.", profileId: Bundle.Profile.ProfileId);
                var identity = new GenerativeVisionLanguageImageIdentity(Bundle.Profile.ProfileId, Bundle.Profile.ArtifactIdentity, Bundle.Profile.Processor.Sha256, sourceSha, input.SourceSize, input.ModelSize);
                GenerativeVisionLanguageImageState publicState = Summarize(identity, outputContract.Name, encoderState, watch.Elapsed);
                var mask = new Tensor<long>(new TensorShape(encoderState.Shape[0], encoderState.Shape[1]), CreateOnes(checked((int)(encoderState.Shape[0] * encoderState.Shape[1]))), TensorBufferOwnership.Transfer);
                var newState = new ImageState(encoderState, mask, publicState);
                lock (_gate) { EnsureUsableLocked(); _imageState = newState; }
                return publicState;
            }
            catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
            catch (DeploySharpException exception) when (operationToken.IsCancellationRequested) { throw MapCancellation(exception, caller); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw Failure("Vision encoding failed.", exception, Bundle.Profile.ProfileId); }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
                if (options.DisposeOwnedInputOnCompletion && input.Ownership == PreparedInputOwnership.Owned) input.Dispose();
                ExitOperation();
            }
        }

        private async Task<GenerativeVisionLanguageResult> GenerateCoreAsync(GenerativeVisionLanguageRequest request, IGenerativeVisionLanguageTokenizer tokenizer, Action<GenerationChunk>? stream, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            CancellationToken dispose = EnterOperation();
            CancellationTokenSource? timeoutSource = null;
            CancellationTokenSource? linked = null;
            CancellationToken operationToken = dispose;
            try
            {
                if (options.Timeout.HasValue)
                {
                    timeoutSource = new CancellationTokenSource(options.Timeout.Value);
                }
                if (caller.CanBeCanceled || timeoutSource != null)
                {
                    linked = timeoutSource == null
                        ? CancellationTokenSource.CreateLinkedTokenSource(caller, dispose)
                        : CancellationTokenSource.CreateLinkedTokenSource(caller, timeoutSource.Token, dispose);
                    operationToken = linked.Token;
                }
                ImageState state;
                lock (_gate) { EnsureUsableLocked(); state = _imageState ?? throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageStateInvalid, "Set-image must succeed before generation.", profileId: Bundle.Profile.ProfileId); }
                operationToken.ThrowIfCancellationRequested();
                var tokenizeWatch = Stopwatch.StartNew();
                GenerativeTokenSequence prefix = tokenizer.EncodePrefix(Bundle.Profile, request);
                tokenizeWatch.Stop();
                ValidatePrefix(prefix, Bundle.Profile);
                var sequence = prefix.CopyTokenIds().ToList();
                var completion = new List<int>();
                var scores = new List<GenerativeTokenScore>();
                var stepTimes = new List<TimeSpan>();
                string streamedText = string.Empty;
                bool emittedEos = false;
                GenerativeVisionLanguageArtifactContract decoder = Bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder);
                while (sequence.Count < Bundle.Profile.Generation.MaximumTotalTokens)
                {
                    operationToken.ThrowIfCancellationRequested();
                    InferenceInputs inputs = CreateDecoderInputs(decoder, sequence, state);
                    var stepWatch = Stopwatch.StartNew();
                    InferenceOutputs outputs = asynchronous ? await _decoder.RunAsync(inputs, operationToken).ConfigureAwait(false) : _decoder.Run(inputs, operationToken);
                    stepWatch.Stop();
                    stepTimes.Add(stepWatch.Elapsed);
                    ValidateOutputs(outputs, decoder, Bundle.Profile.ProfileId);
                    SelectedToken selected = SelectToken(outputs.GetRequired(decoder.Outputs[0].Name), sequence.Count, Bundle.Profile);
                    sequence.Add(selected.TokenId);
                    completion.Add(selected.TokenId);
                    scores.Add(new GenerativeTokenScore(completion.Count - 1, selected.TokenId, selected.Logit, selected.LogProbability));
                    string cumulative = tokenizer.DecodeCompletion(completion);
                    string fragment = cumulative.StartsWith(streamedText, StringComparison.Ordinal) ? cumulative.Substring(streamedText.Length) : cumulative;
                    streamedText = cumulative;
                    emittedEos = selected.TokenId == Bundle.Profile.Tokenizer.EosTokenId;
                    stream?.Invoke(new GenerationChunk(completion.Count - 1, fragment, selected.TokenId, emittedEos ? GenerationFinishReason.EndOfSequence : GenerationFinishReason.None));
                    if (emittedEos) break;
                }
                var finalWatch = Stopwatch.StartNew();
                string text = tokenizer.DecodeCompletion(completion);
                finalWatch.Stop();
                GenerationFinishReason finish = emittedEos ? GenerationFinishReason.EndOfSequence : GenerationFinishReason.MaxTokens;
                if (!emittedEos) stream?.Invoke(new GenerationChunk(completion.Count, string.Empty, null, finish));
                var generation = new GenerationResult(text, finish, new TokenUsage(prefix.Count, completion.Count), completion);
                var identity = new GenerationIdentity(state.PublicState.Identity, prefix.ContentSha256, tokenizer.Sha256, Bundle.Profile.Generation.Identity, completion.Count);
                return new GenerativeVisionLanguageResult(generation, request, prefix.NormalizedPrompt, identity, scores, new GenerativeVisionLanguageTiming(tokenizeWatch.Elapsed, stepTimes, finalWatch.Elapsed));
            }
            catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
            catch (DeploySharpException exception) when (operationToken.IsCancellationRequested) { throw MapCancellation(exception, caller); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw Failure("Image-conditioned generation failed.", exception, Bundle.Profile.ProfileId); }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
                ExitOperation();
            }
        }

        private static InferenceInputs CreateDecoderInputs(GenerativeVisionLanguageArtifactContract contract, IReadOnlyList<long> sequence, ImageState state)
        {
            long[] ids = new long[sequence.Count];
            long[] mask = new long[sequence.Count];
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = sequence[index];
                mask[index] = 1L;
            }
            var tensors = new List<NamedTensor>
            {
                new NamedTensor(contract.Inputs[0].Name, new Tensor<long>(new TensorShape(1, ids.Length), ids, TensorBufferOwnership.Transfer)),
                new NamedTensor(contract.Inputs[1].Name, new Tensor<long>(new TensorShape(1, ids.Length), mask, TensorBufferOwnership.Transfer)),
                new NamedTensor(contract.Inputs[2].Name, state.EncoderHiddenStates),
                new NamedTensor(contract.Inputs[3].Name, state.EncoderAttentionMask)
            };
            return new InferenceInputs(tensors);
        }

        private static long[] CreateOnes(int length)
        {
            var values = new long[length];
            for (int index = 0; index < values.Length; index++) values[index] = 1L;
            return values;
        }

        private static SelectedToken SelectToken(ITensor tensor, int sequenceLength, GenerativeVisionLanguageProfile profile)
        {
            int vocabulary = profile.Tokenizer.VocabularySize;
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 3 || tensor.Shape[0] != 1 || tensor.Shape[1] != sequenceLength || tensor.Shape[2] != vocabulary) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageGenerationInvalid, "Decoder logits shape/type differs from the profile.", profileId: profile.ProfileId);
            float[] values = (float[])tensor.Buffer;
            int offset = checked((sequenceLength - 1) * vocabulary);
            int selected = -1;
            float maximum = float.NegativeInfinity;
            for (int token = 0; token < vocabulary; token++)
            {
                float value = values[offset + token];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageGenerationInvalid, "Decoder logits contain NaN or Infinity.", profileId: profile.ProfileId);
                if (sequenceLength < profile.Generation.MinimumTotalTokens && token == profile.Tokenizer.EosTokenId) continue;
                if (value > maximum) { maximum = value; selected = token; }
            }
            if (selected < 0) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageGenerationInvalid, "No selectable decoder token remained after stopping rules.", profileId: profile.ProfileId);
            double sum = 0;
            for (int token = 0; token < vocabulary; token++)
            {
                if (sequenceLength < profile.Generation.MinimumTotalTokens && token == profile.Tokenizer.EosTokenId) continue;
                sum += Math.Exp(values[offset + token] - maximum);
            }
            return new SelectedToken(selected, maximum, (float)(-Math.Log(sum)));
        }

        private static void ValidatePreparedInput(PreparedVisualInput input, GenerativeVisionLanguageArtifactContract contract, GenerativeVisionLanguageProfile profile)
        {
            if (contract.Inputs.Count != 1 || input.InputName != contract.Inputs[0].Name || input.Tensor.ElementType != TensorElementType.Float32 || !GenerativeVisionLanguageHash.ShapeMatches(contract.Inputs[0].ShapePattern, input.Tensor.Shape) || input.Tensor.Length > contract.Inputs[0].MaximumElements || input.ModelSize != profile.Processor.ImageSize) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Prepared image tensor differs from the exact processor/encoder contract.", profileId: profile.ProfileId, tensorName: contract.Inputs[0].Name);
            foreach (float value in (float[])input.Tensor.Buffer) if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Prepared image contains NaN or Infinity.", profileId: profile.ProfileId, tensorName: contract.Inputs[0].Name);
        }

        private static void ValidatePrefix(GenerativeTokenSequence prefix, GenerativeVisionLanguageProfile profile)
        {
            long[] ids = prefix.CopyTokenIds();
            if (!string.Equals(prefix.TokenizerId, profile.Tokenizer.TokenizerId, StringComparison.Ordinal) || !string.Equals(prefix.TokenizerSha256, profile.Tokenizer.Sha256, StringComparison.OrdinalIgnoreCase) || ids.Length == 0 || ids.Length > profile.Tokenizer.MaximumPromptTokens || ids[0] != profile.Tokenizer.BosTokenId || ids.Any(value => value < 0 || value >= profile.Tokenizer.VocabularySize)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "Prompt prefix differs from the profile tokenizer contract.", profileId: profile.ProfileId);
        }

        private static Tensor<float> CopyFiniteFloatTensor(ITensor tensor, GenerativeVisionLanguageTensorContract contract, string profileId)
        {
            if (tensor.ElementType != TensorElementType.Float32 || !GenerativeVisionLanguageHash.ShapeMatches(contract.ShapePattern, tensor.Shape) || tensor.Length > contract.MaximumElements) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Encoder output shape/type differs from the profile.", profileId: profileId, tensorName: contract.Name);
            float[] values = ((float[])tensor.Buffer).ToArray();
            for (int index = 0; index < values.Length; index++)
            {
                float value = values[index];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Encoder output contains NaN or Infinity.", profileId: profileId, tensorName: contract.Name);
            }
            return new Tensor<float>(new TensorShape(tensor.Shape.ToArray()), values, TensorBufferOwnership.Transfer);
        }

        private static GenerativeVisionLanguageImageState Summarize(GenerativeVisionLanguageImageIdentity identity, string name, Tensor<float> tensor, TimeSpan time)
        {
            float[] values = (float[])tensor.Buffer;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            double squared = 0;
            var bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            foreach (float value in values) { minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value); squared += (double)value * value; }
            using (SHA256 sha = SHA256.Create()) return new GenerativeVisionLanguageImageState(identity, name, tensor.Shape.ToArray(), string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2"))), minimum, maximum, Math.Sqrt(squared), time);
        }

        private static void ValidateMetadata(ModelMetadata metadata, GenerativeVisionLanguageArtifactContract expected, string profileId)
        {
            if (metadata.Inputs.Count != expected.Inputs.Count || metadata.Outputs.Count != expected.Outputs.Count) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Backend metadata exposes an unexpected port count.", profileId: profileId, modelId: expected.ModelId);
            foreach (GenerativeVisionLanguageTensorContract port in expected.Inputs) ValidateDescriptor(metadata.Inputs.SingleOrDefault(value => value.Name == port.Name), port, profileId, expected.ModelId);
            foreach (GenerativeVisionLanguageTensorContract port in expected.Outputs) ValidateDescriptor(metadata.Outputs.SingleOrDefault(value => value.Name == port.Name), port, profileId, expected.ModelId);
        }

        private static void ValidateDescriptor(TensorDescriptor? actual, GenerativeVisionLanguageTensorContract expected, string profileId, ModelId modelId)
        {
            if (actual == null) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Required named port '" + expected.Name + "' is missing.", profileId: profileId, modelId: modelId, tensorName: expected.Name);
            if (actual.ElementType != expected.ElementType || !MetadataShapeMatches(expected.ShapePattern, actual.Shape))
            {
                throw new VisualException(
                    VisualErrorCodes.GenerativeVisionLanguageContractInvalid,
                    "Named port '" + expected.Name + "' expected " + expected.ElementType + " " + expected.ShapePattern + " but backend metadata exposed " + actual.ElementType + " " + actual.Shape + ".",
                    profileId: profileId,
                    modelId: modelId,
                    tensorName: expected.Name);
            }
        }

        private static bool MetadataShapeMatches(TensorShape expected, TensorShape actual)
        {
            if (expected.Rank != actual.Rank) return false;
            for (int index = 0; index < expected.Rank; index++)
            {
                if (expected[index] >= 0 && actual[index] >= 0 && expected[index] != actual[index]) return false;
            }
            return true;
        }

        private static void ValidateOutputs(InferenceOutputs outputs, GenerativeVisionLanguageArtifactContract contract, string profileId)
        {
            if (outputs == null || outputs.Count != contract.Outputs.Count || contract.Outputs.Any(port => outputs.SingleOrDefault(value => value.Name == port.Name) == null)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Backend outputs do not match exact named ports.", profileId: profileId, modelId: contract.ModelId);
        }

        private CancellationToken EnterOperation()
        {
            lock (_gate) { EnsureUsableLocked(); if (Interlocked.Exchange(ref _active, 1) != 0) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageConcurrentOperation, "Only one stateful generation operation may execute at a time.", profileId: Bundle.Profile.ProfileId); _idle.Reset(); return _disposeSource.Token; }
        }

        private void ExitOperation() { Interlocked.Exchange(ref _active, 0); _idle.Set(); }
        private void EnsureUsableLocked() { if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The generative vision-language session has been disposed.", profileId: Bundle.Profile.ProfileId); }
        private static VisualException MapCancellation(Exception exception, CancellationToken caller) => new VisualException(caller.IsCancellationRequested ? VisualErrorCodes.Cancelled : VisualErrorCodes.Timeout, caller.IsCancellationRequested ? "The generation operation was cancelled." : "The generation operation exceeded its timeout.", exception);
        private static VisualException Failure(string message, Exception exception, string profileId) => new VisualException(VisualErrorCodes.InferenceFailed, message, exception, profileId, technicalDetails: exception.ToString());

        private sealed class ImageState
        {
            internal ImageState(Tensor<float> encoderHiddenStates, Tensor<long> encoderAttentionMask, GenerativeVisionLanguageImageState publicState) { EncoderHiddenStates = encoderHiddenStates; EncoderAttentionMask = encoderAttentionMask; PublicState = publicState; }
            internal Tensor<float> EncoderHiddenStates { get; }
            internal Tensor<long> EncoderAttentionMask { get; }
            internal GenerativeVisionLanguageImageState PublicState { get; }
        }

        private readonly struct SelectedToken
        {
            internal SelectedToken(int tokenId, float logit, float logProbability) { TokenId = tokenId; Logit = logit; LogProbability = logProbability; }
            internal int TokenId { get; }
            internal float Logit { get; }
            internal float LogProbability { get; }
        }
    }
}
