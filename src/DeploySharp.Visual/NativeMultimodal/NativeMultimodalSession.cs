using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    /// <summary>Owns exact Vision/Projector, Token Embedding, and Prefill/KV Decode sessions plus one image state. / 拥有精确 Vision/Projector、Token Embedding、Prefill/KV Decode Session 与一个图像状态。</summary>
    /// <remarks>The session is single-writer. Cancellation, timeout, or callback failure never publishes partial image/KV state; Registry, tokenizer, and prepared input remain caller-owned. / Session 为 Single-writer；取消、超时或 Callback 失败不会发布部分图像/KV 状态；Registry、Tokenizer 与 Prepared Input 保持调用方所有。</remarks>
    public sealed class NativeMultimodalSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IInferenceSession _vision;
        private readonly IInferenceSession _embedding;
        private readonly IInferenceSession _decoder;
        private readonly float[] _imageNewline;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private ImageState? _imageState;
        private NativeMultimodalKvStateSummary? _lastKvState;
        private bool _disposed;
        private int _active;

        /// <summary>Creates and validates all exact named sessions and the external image-newline sidecar. / 创建并校验全部精确具名 Session 与外部 Image-newline Sidecar。</summary>
        public NativeMultimodalSession(BackendRegistry registry, NativeMultimodalArtifactBundle bundle, BackendRequest request, string imageNewlinePath, SessionOptions? options = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            if (request == null) throw new ArgumentNullException(nameof(request));
            _imageNewline = LoadImageNewline(imageNewlinePath, bundle.Profile);
            SessionOptions requested = options ?? SessionOptions.Default;
            var stateful = new SessionOptions(1, requested.EnableProfiling);
            var effectiveRequest = new BackendRequest(request.RequiredCapabilities | BackendCapabilities.TensorInference, request.BackendId, request.Device);
            IInferenceSession? vision = null;
            IInferenceSession? embedding = null;
            IInferenceSession? decoder = null;
            try
            {
                GenerativeVisionLanguageArtifactContract visionContract = bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder);
                GenerativeVisionLanguageArtifactContract embeddingContract = bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.TokenEmbedding);
                GenerativeVisionLanguageArtifactContract decoderContract = bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder);
                vision = registry.CreateSession(bundle.GetArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder), effectiveRequest, stateful);
                ValidateMetadata(vision.Metadata, visionContract, bundle.Profile.ProfileId);
                embedding = registry.CreateSession(bundle.GetArtifact(GenerativeVisionLanguageArtifactRole.TokenEmbedding), effectiveRequest, stateful);
                ValidateMetadata(embedding.Metadata, embeddingContract, bundle.Profile.ProfileId);
                decoder = registry.CreateSession(bundle.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder), effectiveRequest, stateful);
                ValidateMetadata(decoder.Metadata, decoderContract, bundle.Profile.ProfileId);
                _vision = vision;
                _embedding = embedding;
                _decoder = decoder;
            }
            catch (Exception exception)
            {
                TryDispose(decoder);
                TryDispose(embedding);
                TryDispose(vision);
                _disposeSource.Dispose();
                _idle.Dispose();
                if (exception is VisualException) throw;
                throw Failure("Native multimodal backend sessions could not be created.", exception, bundle.Profile.ProfileId);
            }
        }

        /// <summary>Gets immutable artifact bundle. / 获取不可变工件 Bundle。</summary>
        public NativeMultimodalArtifactBundle Bundle { get; }
        /// <summary>Gets whether one exact image state is cached. / 获取是否缓存一个精确图像状态。</summary>
        public bool HasImage { get { lock (_gate) { EnsureUsableLocked(); return _imageState != null; } } }
        /// <summary>Gets current image identity, or null before set-image/after clear. / 获取当前图像 Identity；Set-image 前或 Clear 后为 null。</summary>
        public GenerativeVisionLanguageImageIdentity? CurrentImage { get { lock (_gate) { EnsureUsableLocked(); return _imageState?.PublicState.FeatureState.Identity; } } }
        /// <summary>Gets the last successfully completed local KV summary; mutable KV tensors are never retained. / 获取最近一次成功完成的本地 KV 摘要；不保留可变 KV Tensor。</summary>
        public NativeMultimodalKvStateSummary? CurrentKvState { get { lock (_gate) { EnsureUsableLocked(); return _lastKvState; } } }

        /// <summary>Runs Vision/Projector once, packs anyres features once, and atomically replaces image state. / 单次运行 Vision/Projector 与 Anyres Feature 打包，并原子替换图像状态。</summary>
        public NativeMultimodalImageState SetImage(NativeMultimodalPreparedImage image, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SetImageCoreAsync(image, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Asynchronously prepares one image; cancellation never installs partial features and optional owned-input disposal follows execution options. / 异步准备一个图像；取消不会安装部分 Feature，可选自有输入释放遵循执行选项。</summary>
        public Task<NativeMultimodalImageState> SetImageAsync(NativeMultimodalPreparedImage image, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SetImageCoreAsync(image, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Runs exact embedding, empty-past Prefill, and named non-empty-past Decode steps. / 执行精确 Embedding、Empty-past Prefill 与具名 Non-empty-past Decode Step。</summary>
        /// <remarks>The callback observes immutable chunks. Callback failure aborts generation and does not publish a KV summary. / Callback 观察不可变 Chunk；Callback 失败会终止生成且不发布 KV 摘要。</remarks>
        public NativeMultimodalResult Generate(GenerativeVisionLanguageRequest request, INativeMultimodalTokenizer tokenizer, Action<GenerationChunk>? stream = null, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => GenerateCoreAsync(request, tokenizer, stream, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Asynchronously generates an owned result; cancellation/timeout exposes no partial result or KV state. / 异步生成自有结果；取消/超时不公开部分结果或 KV 状态。</summary>
        public Task<NativeMultimodalResult> GenerateAsync(GenerativeVisionLanguageRequest request, INativeMultimodalTokenizer tokenizer, Action<GenerationChunk>? stream = null, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => GenerateCoreAsync(request, tokenizer, stream, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Clears image and completed KV summary; generation then fails until another successful set-image. / 清除图像与已完成 KV 摘要；之后生成在另一次成功 Set-image 前失败。</summary>
        public void Clear()
        {
            EnterOperation();
            try { lock (_gate) { EnsureUsableLocked(); _imageState = null; _lastKvState = null; } }
            finally { ExitOperation(); }
        }

        /// <summary>Cancels an active call, waits for unwind, clears state, and disposes all three sessions exactly once. / 取消活动调用、等待回卷、清除状态，并 Exactly-once 释放三条 Session。</summary>
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; _disposeSource.Cancel(); }
            _idle.Wait();
            try { _imageState = null; _lastKvState = null; _decoder.Dispose(); _embedding.Dispose(); _vision.Dispose(); }
            finally { _disposeSource.Dispose(); _idle.Dispose(); }
        }

        private async Task<NativeMultimodalImageState> SetImageCoreAsync(NativeMultimodalPreparedImage image, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
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
                ValidatePreparedImage(image, Bundle.Profile);
                GenerativeVisionLanguageArtifactContract contract = Bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder);
                var visionWatch = Stopwatch.StartNew();
                InferenceOutputs outputs = asynchronous ? await _vision.RunAsync(InferenceInputs.Create(contract.Inputs[0].Name, image.Input.Tensor), operationToken).ConfigureAwait(false) : _vision.Run(InferenceInputs.Create(contract.Inputs[0].Name, image.Input.Tensor), operationToken);
                visionWatch.Stop();
                ValidateOutputs(outputs, contract, Bundle.Profile.ProfileId);
                Tensor<float> raw = CopyFiniteFloat(outputs.GetRequired("image_features"), contract.Outputs[0], Bundle.Profile.ProfileId);
                var packingWatch = Stopwatch.StartNew();
                Tensor<float> packed = NativeMultimodalImagePacker.Pack(raw, _imageNewline, image, Bundle.Profile.Processor);
                packingWatch.Stop();
                string sourceSha = image.Input.InputId ?? throw new VisualException(VisualErrorCodes.NativeMultimodalIdentityMismatch, "Set-image requires the exact encoded-source SHA256.", profileId: Bundle.Profile.ProfileId);
                var identity = new GenerativeVisionLanguageImageIdentity(Bundle.Profile.ProfileId, Bundle.Profile.ArtifactIdentity, Bundle.Profile.Processor.Identity, sourceSha, image.Input.SourceSize, new VisualSize(Bundle.Profile.Processor.PatchSize, Bundle.Profile.Processor.PatchSize));
                GenerativeVisionLanguageImageState featureState = Summarize(identity, "packed_image_features", packed, visionWatch.Elapsed);
                var publicState = new NativeMultimodalImageState(featureState, image.Grid, image.Input.BatchSize, image.PackedImageTokens, packingWatch.Elapsed);
                var state = new ImageState(packed, publicState);
                lock (_gate) { EnsureUsableLocked(); _imageState = state; _lastKvState = null; }
                return publicState;
            }
            catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
            catch (DeploySharpException exception) when (operationToken.IsCancellationRequested) { throw MapCancellation(exception, caller); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw Failure("Native multimodal image encoding failed.", exception, Bundle.Profile.ProfileId); }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
                if (options.DisposeOwnedInputOnCompletion && image.Input.Ownership == PreparedInputOwnership.Owned) image.Dispose();
                ExitOperation();
            }
        }

        private async Task<NativeMultimodalResult> GenerateCoreAsync(GenerativeVisionLanguageRequest request, INativeMultimodalTokenizer tokenizer, Action<GenerationChunk>? stream, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
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
                ImageState image;
                lock (_gate) { EnsureUsableLocked(); image = _imageState ?? throw new VisualException(VisualErrorCodes.NativeMultimodalStateInvalid, "Set-image must succeed before generation.", profileId: Bundle.Profile.ProfileId); }
                operationToken.ThrowIfCancellationRequested();
                var tokenizeWatch = Stopwatch.StartNew();
                NativeMultimodalTokenSequence prompt = tokenizer.Encode(Bundle.Profile, request, image.PublicState.ImageTokenCount);
                tokenizeWatch.Stop();
                ValidatePrompt(prompt, tokenizer, Bundle.Profile, image);
                long[] promptIds = prompt.CopyTokenIds();
                GenerativeVisionLanguageArtifactContract embeddingContract = Bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.TokenEmbedding);
                var embeddingWatch = Stopwatch.StartNew();
                Tensor<float> promptEmbeddings = await RunEmbeddingAsync(promptIds, embeddingContract, asynchronous, operationToken, true).ConfigureAwait(false);
                embeddingWatch.Stop();
                TimeSpan embeddingTime = embeddingWatch.Elapsed;
                ReplaceImageSentinels(promptIds, promptEmbeddings, image.Features, Bundle.Profile);

                List<Tensor<float>> past = CreateEmptyPast(Bundle.Profile.KvCache);
                long[] attentionMask = CreateOnes(promptIds.Length);
                long[] positionIds = CreateRange(promptIds.Length);
                    var completion = new List<int>();
                    var tokenScores = new List<GenerativeTokenScore>();
                    var decodeTimes = new List<TimeSpan>();
                    TimeSpan prefillTime = TimeSpan.Zero;
                    string streamed = string.Empty;
                    bool emittedEos = false;
                    for (int step = 0; step < Bundle.Profile.Generation.MaximumTotalTokens; step++)
                    {
                        operationToken.ThrowIfCancellationRequested();
                        GenerativeVisionLanguageArtifactContract decoderContract = Bundle.Profile.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder);
                        InferenceInputs decoderInputs = CreateDecoderInputs(attentionMask, positionIds, past, promptEmbeddings, Bundle.Profile);
                        var stepWatch = Stopwatch.StartNew();
                        InferenceOutputs outputs = asynchronous ? await _decoder.RunAsync(decoderInputs, operationToken).ConfigureAwait(false) : _decoder.Run(decoderInputs, operationToken);
                        stepWatch.Stop();
                        if (step == 0) prefillTime = stepWatch.Elapsed; else decodeTimes.Add(stepWatch.Elapsed);
                        ValidateOutputs(outputs, decoderContract, Bundle.Profile.ProfileId);
                        SelectedToken selected = SelectToken(outputs.GetRequired("logits"), Bundle.Profile, step);
                        List<Tensor<float>> nextPast = CopyPresent(outputs, Bundle.Profile);
                        past = nextPast;
                        completion.Add(selected.TokenId);
                        tokenScores.Add(new GenerativeTokenScore(step, selected.TokenId, selected.Logit, selected.LogProbability));
                        string cumulative = tokenizer.DecodeCompletion(completion);
                        string fragment = cumulative.StartsWith(streamed, StringComparison.Ordinal) ? cumulative.Substring(streamed.Length) : cumulative;
                        streamed = cumulative;
                        emittedEos = selected.TokenId == Bundle.Profile.Tokenizer.ImEndTokenId;
                        stream?.Invoke(new GenerationChunk(step, fragment, selected.TokenId, emittedEos ? GenerationFinishReason.EndOfSequence : GenerationFinishReason.None));
                        if (emittedEos) break;
                        var nextEmbeddingWatch = Stopwatch.StartNew();
                        promptEmbeddings = await RunEmbeddingAsync(new long[] { selected.TokenId }, embeddingContract, asynchronous, operationToken, false).ConfigureAwait(false);
                        nextEmbeddingWatch.Stop();
                        embeddingTime = embeddingTime.Add(nextEmbeddingWatch.Elapsed);
                        int position = checked(promptIds.Length + completion.Count - 1);
                        attentionMask = CreateOnes(position + 1);
                        positionIds = new long[] { position };
                    }
                    var finalWatch = Stopwatch.StartNew();
                    string text = tokenizer.DecodeCompletion(completion);
                    finalWatch.Stop();
                    GenerationFinishReason finish = emittedEos ? GenerationFinishReason.EndOfSequence : GenerationFinishReason.MaxTokens;
                    if (!emittedEos) stream?.Invoke(new GenerationChunk(completion.Count, string.Empty, null, finish));
                    var generation = new GenerationResult(text, finish, new TokenUsage(promptIds.Length, completion.Count), completion);
                    var identity = new GenerationIdentity(image.PublicState.FeatureState.Identity, prompt.ContentSha256, tokenizer.Sha256, Bundle.Profile.Generation.Identity, completion.Count);
                    var commonTiming = new GenerativeVisionLanguageTiming(tokenizeWatch.Elapsed, new[] { prefillTime }.Concat(decodeTimes), finalWatch.Elapsed);
                    var common = new GenerativeVisionLanguageResult(generation, request, prompt.NormalizedPrompt, identity, tokenScores, commonTiming);
                    NativeMultimodalKvStateSummary kvSummary = SummarizeKv(past, prompt.ContentSha256, Bundle.Profile.KvCache);
                    var nativeTiming = new NativeMultimodalExecutionTiming(tokenizeWatch.Elapsed, embeddingTime, prefillTime, decodeTimes, finalWatch.Elapsed);
                    var result = new NativeMultimodalResult(common, kvSummary, nativeTiming);
                    lock (_gate) { EnsureUsableLocked(); _lastKvState = kvSummary; }
                return result;
            }
            catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
            catch (DeploySharpException exception) when (operationToken.IsCancellationRequested) { throw MapCancellation(exception, caller); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw Failure("Native multimodal Prefill/KV generation failed.", exception, Bundle.Profile.ProfileId); }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
                ExitOperation();
            }
        }

        private static long[] CreateOnes(int length)
        {
            var values = new long[length];
            for (int index = 0; index < values.Length; index++) values[index] = 1L;
            return values;
        }

        private static long[] CreateRange(int length)
        {
            var values = new long[length];
            for (int index = 0; index < values.Length; index++) values[index] = index;
            return values;
        }

        private async Task<Tensor<float>> RunEmbeddingAsync(long[] ids, GenerativeVisionLanguageArtifactContract contract, bool asynchronous, CancellationToken token, bool copyInput)
        {
            var input = new Tensor<long>(new TensorShape(1, ids.Length), copyInput ? (long[])ids.Clone() : ids, TensorBufferOwnership.Transfer);
            InferenceOutputs outputs = asynchronous ? await _embedding.RunAsync(InferenceInputs.Create("input_ids", input), token).ConfigureAwait(false) : _embedding.Run(InferenceInputs.Create("input_ids", input), token);
            ValidateOutputs(outputs, contract, Bundle.Profile.ProfileId);
            return CopyFiniteFloat(outputs.GetRequired("inputs_embeds"), contract.Outputs[0], Bundle.Profile.ProfileId);
        }

        private static void ReplaceImageSentinels(long[] ids, Tensor<float> embeddings, Tensor<float> imageFeatures, NativeMultimodalProfile profile)
        {
            if (embeddings.Shape.Rank != 3 || embeddings.Shape[0] != 1 || embeddings.Shape[1] != ids.Length || embeddings.Shape[2] != profile.Processor.HiddenSize || imageFeatures.Shape.Rank != 2 || imageFeatures.Shape[1] != profile.Processor.HiddenSize) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Prompt or image embedding shape differs from the profile.", profileId: profile.ProfileId);
            float[] destination = (float[])embeddings.Buffer;
            float[] source = (float[])imageFeatures.Buffer;
            int imageIndex = 0;
            for (int token = 0; token < ids.Length; token++)
            {
                if (ids[token] != profile.Tokenizer.ImageTokenId) continue;
                if (imageIndex >= imageFeatures.Shape[0]) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Prompt contains more image sentinels than packed features.", profileId: profile.ProfileId);
                Array.Copy(source, imageIndex * profile.Processor.HiddenSize, destination, token * profile.Processor.HiddenSize, profile.Processor.HiddenSize);
                imageIndex++;
            }
            if (imageIndex != imageFeatures.Shape[0]) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Prompt image-sentinel count differs from packed features.", profileId: profile.ProfileId);
        }

        private static InferenceInputs CreateDecoderInputs(long[] attentionMask, long[] positionIds, IReadOnlyList<Tensor<float>> past, Tensor<float> embeddings, NativeMultimodalProfile profile)
        {
            if (past.Count != profile.KvCache.LayerCount * 2) throw new VisualException(VisualErrorCodes.NativeMultimodalStateInvalid, "KV tensor count differs from the profile.", profileId: profile.ProfileId);
            var values = new List<NamedTensor>
            {
                new NamedTensor("attention_mask", new Tensor<long>(new TensorShape(1, attentionMask.Length), attentionMask, TensorBufferOwnership.Transfer)),
                new NamedTensor("position_ids", new Tensor<long>(new TensorShape(1, positionIds.Length), positionIds, TensorBufferOwnership.Transfer))
            };
            for (int layer = 0; layer < profile.KvCache.LayerCount; layer++)
            {
                values.Add(new NamedTensor(profile.KvCache.PastKey(layer), past[layer * 2]));
                values.Add(new NamedTensor(profile.KvCache.PastValue(layer), past[(layer * 2) + 1]));
            }
            values.Add(new NamedTensor("inputs_embeds", embeddings));
            return new InferenceInputs(values);
        }

        private static List<Tensor<float>> CreateEmptyPast(NativeMultimodalKvCacheContract contract)
        {
            var result = new List<Tensor<float>>(contract.LayerCount * 2);
            for (int index = 0; index < contract.LayerCount * 2; index++) result.Add(new Tensor<float>(new TensorShape(1, contract.KeyValueHeads, 0, contract.HeadDimension), Array.Empty<float>()));
            return result;
        }

        private static List<Tensor<float>> CopyPresent(InferenceOutputs outputs, NativeMultimodalProfile profile)
        {
            var result = new List<Tensor<float>>(profile.KvCache.LayerCount * 2);
            long? past = null;
            for (int layer = 0; layer < profile.KvCache.LayerCount; layer++)
            {
                CopyPresentTensor(outputs, profile, profile.KvCache.PresentKey(layer), result, ref past);
                CopyPresentTensor(outputs, profile, profile.KvCache.PresentValue(layer), result, ref past);
            }
            return result;
        }

        private static void CopyPresentTensor(InferenceOutputs outputs, NativeMultimodalProfile profile, string name, List<Tensor<float>> result, ref long? past)
        {
            ITensor tensor = outputs.GetRequired(name);
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 4 || tensor.Shape[0] != 1 || tensor.Shape[1] != profile.KvCache.KeyValueHeads || tensor.Shape[3] != profile.KvCache.HeadDimension || tensor.Shape[2] <= 0 || tensor.Shape[2] > profile.KvCache.MaximumPastTokens) throw new VisualException(VisualErrorCodes.NativeMultimodalGenerationInvalid, "A present KV tensor differs from the exact profile axes or capacity.", profileId: profile.ProfileId, tensorName: name);
            if (past.HasValue && past.Value != tensor.Shape[2]) throw new VisualException(VisualErrorCodes.NativeMultimodalGenerationInvalid, "Present KV tensors have inconsistent sequence lengths.", profileId: profile.ProfileId, tensorName: name);
            past = tensor.Shape[2];
            float[] values = ((float[])tensor.Buffer).ToArray();
            for (int index = 0; index < values.Length; index++)
            {
                float value = values[index];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.NativeMultimodalGenerationInvalid, "Present KV contains NaN or Infinity.", profileId: profile.ProfileId, tensorName: name);
            }
            result.Add(new Tensor<float>(new TensorShape(tensor.Shape.ToArray()), values, TensorBufferOwnership.Transfer));
        }

        private static SelectedToken SelectToken(ITensor logits, NativeMultimodalProfile profile, int step)
        {
            if (logits.ElementType != TensorElementType.Float32 || logits.Shape.Rank != 3 || logits.Shape[0] != 1 || logits.Shape[1] <= 0 || logits.Shape[2] != profile.Tokenizer.VocabularySize) throw new VisualException(VisualErrorCodes.NativeMultimodalGenerationInvalid, "Decoder logits shape/type differs from the profile.", profileId: profile.ProfileId, tensorName: "logits");
            float[] values = (float[])logits.Buffer;
            int vocabulary = profile.Tokenizer.VocabularySize;
            int offset = checked(((int)logits.Shape[1] - 1) * vocabulary);
            int selected = -1;
            float maximum = float.NegativeInfinity;
            for (int token = 0; token < vocabulary; token++)
            {
                float value = values[offset + token];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.NativeMultimodalGenerationInvalid, "Decoder logits contain NaN or Infinity.", profileId: profile.ProfileId, tensorName: "logits");
                if (step + 1 < profile.Generation.MinimumTotalTokens && token == profile.Tokenizer.ImEndTokenId) continue;
                if (value > maximum) { maximum = value; selected = token; }
            }
            if (selected < 0) throw new VisualException(VisualErrorCodes.NativeMultimodalGenerationInvalid, "No selectable token remained.", profileId: profile.ProfileId);
            double sum = 0;
            for (int token = 0; token < vocabulary; token++)
            {
                if (step + 1 < profile.Generation.MinimumTotalTokens && token == profile.Tokenizer.ImEndTokenId) continue;
                sum += Math.Exp(values[offset + token] - maximum);
            }
            return new SelectedToken(selected, maximum, (float)(-Math.Log(sum)));
        }

        private static NativeMultimodalKvStateSummary SummarizeKv(IReadOnlyList<Tensor<float>> tensors, string promptSha, NativeMultimodalKvCacheContract contract)
        {
            if (tensors.Count != contract.LayerCount * 2) throw new VisualException(VisualErrorCodes.NativeMultimodalGenerationInvalid, "Final KV tensor count is invalid.");
            int past = checked((int)tensors[0].Shape[2]);
            using (SHA256 algorithm = SHA256.Create())
            {
                foreach (Tensor<float> tensor in tensors)
                {
                    float[] values = (float[])tensor.Buffer;
                    var bytes = new byte[checked(values.Length * sizeof(float))];
                    Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
                    algorithm.TransformBlock(bytes, 0, bytes.Length, null, 0);
                }
                algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string sha = string.Concat(algorithm.Hash!.Select(value => value.ToString("x2")));
                return new NativeMultimodalKvStateSummary(contract.SchemaId, contract.LayerCount, contract.KeyValueHeads, past, contract.HeadDimension, sha, promptSha);
            }
        }

        private static void ValidatePreparedImage(NativeMultimodalPreparedImage image, NativeMultimodalProfile profile)
        {
            image.Input.EnsureUsable();
            if (!string.Equals(image.ProfileId, profile.ProfileId, StringComparison.Ordinal) || image.Input.InputName != "pixel_values" || image.Input.Tensor.ElementType != TensorElementType.Float32 || image.Input.Tensor.Shape.Rank != 4 || image.Input.Tensor.Shape[0] != image.Input.BatchSize || image.Input.Tensor.Shape[1] != 3 || image.Input.Tensor.Shape[2] != profile.Processor.PatchSize || image.Input.Tensor.Shape[3] != profile.Processor.PatchSize || image.PackedImageTokens != profile.Processor.GetPackedTokenCount(image.Input.SourceSize, image.Grid) || image.Input.InputId == null || !GenerativeVisionLanguageHash.IsSha256(image.Input.InputId)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Prepared image differs from the profile, patch, source, or identity contract.", profileId: profile.ProfileId, tensorName: "pixel_values");
            foreach (float value in (float[])image.Input.Tensor.Buffer) if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Prepared image contains NaN or Infinity.", profileId: profile.ProfileId, tensorName: "pixel_values");
        }

        private static void ValidatePrompt(NativeMultimodalTokenSequence prompt, INativeMultimodalTokenizer tokenizer, NativeMultimodalProfile profile, ImageState image)
        {
            long[] ids = prompt.CopyTokenIds();
            if (!string.Equals(prompt.TokenizerId, profile.Tokenizer.TokenizerId, StringComparison.Ordinal) || !string.Equals(prompt.TokenizerSha256, profile.Tokenizer.Identity, StringComparison.OrdinalIgnoreCase) || !string.Equals(tokenizer.Sha256, profile.Tokenizer.Identity, StringComparison.OrdinalIgnoreCase) || prompt.ImageTokenCount != image.PublicState.ImageTokenCount || ids.Count(value => value == profile.Tokenizer.ImageTokenId) != image.PublicState.ImageTokenCount || ids.Length + profile.Generation.MaximumTotalTokens > profile.Tokenizer.MaximumContextTokens) throw new VisualException(VisualErrorCodes.NativeMultimodalIdentityMismatch, "Expanded prompt differs from the tokenizer, image, or context contract.", profileId: profile.ProfileId);
        }

        private static void ValidateMetadata(ModelMetadata metadata, GenerativeVisionLanguageArtifactContract contract, string profileId)
        {
            if (!metadata.Inputs.Select(value => value.Name).SequenceEqual(contract.Inputs.Select(value => value.Name), StringComparer.Ordinal) || !metadata.Outputs.Select(value => value.Name).SequenceEqual(contract.Outputs.Select(value => value.Name), StringComparer.Ordinal)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Backend metadata named-port order differs from the profile.", profileId: profileId, modelId: contract.ModelId);
            for (int index = 0; index < metadata.Inputs.Count; index++) ValidateDescriptor(metadata.Inputs[index], contract.Inputs[index], profileId);
            for (int index = 0; index < metadata.Outputs.Count; index++) ValidateDescriptor(metadata.Outputs[index], contract.Outputs[index], profileId);
        }

        private static void ValidateDescriptor(TensorDescriptor descriptor, GenerativeVisionLanguageTensorContract contract, string profileId)
        {
            if (descriptor.ElementType != contract.ElementType || descriptor.Shape.Rank != contract.ShapePattern.Rank) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Backend metadata type/rank differs from the profile.", profileId: profileId, tensorName: contract.Name);
            for (int index = 0; index < descriptor.Shape.Rank; index++) if (descriptor.Shape[index] > 0 && contract.ShapePattern[index] > 0 && descriptor.Shape[index] != contract.ShapePattern[index]) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Backend metadata fixed dimension differs from the profile.", profileId: profileId, tensorName: contract.Name);
        }

        private static void ValidateOutputs(InferenceOutputs outputs, GenerativeVisionLanguageArtifactContract contract, string profileId)
        {
            if (!outputs.Select(value => value.Name).SequenceEqual(contract.Outputs.Select(value => value.Name), StringComparer.Ordinal)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Runtime output names/order differ from the profile.", profileId: profileId, modelId: contract.ModelId);
        }

        private static Tensor<float> CopyFiniteFloat(ITensor tensor, GenerativeVisionLanguageTensorContract contract, string profileId)
        {
            if (tensor.ElementType != TensorElementType.Float32 || !GenerativeVisionLanguageHash.ShapeMatches(contract.ShapePattern, tensor.Shape) || tensor.Length > contract.MaximumElements) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Runtime tensor shape/type/capacity differs from the profile.", profileId: profileId, tensorName: contract.Name);
            float[] values = ((float[])tensor.Buffer).ToArray();
            for (int index = 0; index < values.Length; index++)
            {
                float value = values[index];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Runtime tensor contains NaN or Infinity.", profileId: profileId, tensorName: contract.Name);
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
            using (SHA256 algorithm = SHA256.Create())
            {
                string sha = string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
                return new GenerativeVisionLanguageImageState(identity, name, tensor.Shape.ToArray(), sha, minimum, maximum, Math.Sqrt(squared), time);
            }
        }

        private static float[] LoadImageNewline(string path, NativeMultimodalProfile profile)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "The external image-newline sidecar is missing.", profileId: profile.ProfileId, technicalDetails: path);
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length != profile.Processor.HiddenSize * sizeof(float)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Image-newline byte length differs from hidden size.", profileId: profile.ProfileId);
            using (SHA256 algorithm = SHA256.Create())
            {
                string actual = string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
                if (!string.Equals(actual, profile.Processor.ImageNewlineSha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.NativeMultimodalIdentityMismatch, "Image-newline SHA256 differs from the profile.", profileId: profile.ProfileId, technicalDetails: "expected=" + profile.Processor.ImageNewlineSha256 + ";actual=" + actual);
            }
            var values = new float[profile.Processor.HiddenSize];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            if (values.Any(value => float.IsNaN(value) || float.IsInfinity(value))) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Image-newline contains NaN or Infinity.", profileId: profile.ProfileId);
            return values;
        }

        private CancellationToken EnterOperation()
        {
            lock (_gate) EnsureUsableLocked();
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) throw new VisualException(VisualErrorCodes.NativeMultimodalConcurrentOperation, "The single-writer native multimodal session is already executing.", profileId: Bundle.Profile.ProfileId);
            _idle.Reset();
            lock (_gate) { if (_disposed) { ExitOperation(); throw new VisualException(VisualErrorCodes.ObjectDisposed, "The native multimodal session is disposed.", profileId: Bundle.Profile.ProfileId); } return _disposeSource.Token; }
        }

        private void ExitOperation() { Interlocked.Exchange(ref _active, 0); _idle.Set(); }
        private void EnsureUsableLocked() { if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The native multimodal session is disposed.", profileId: Bundle.Profile.ProfileId); }

        private VisualException MapCancellation(Exception exception, CancellationToken caller)
        {
            if (_disposeSource.IsCancellationRequested) return new VisualException(VisualErrorCodes.ObjectDisposed, "The native multimodal session was disposed during execution.", exception, profileId: Bundle.Profile.ProfileId);
            if (caller.IsCancellationRequested) return new VisualException(VisualErrorCodes.Cancelled, "The native multimodal operation was cancelled.", exception, profileId: Bundle.Profile.ProfileId);
            return new VisualException(VisualErrorCodes.Timeout, "The native multimodal operation timed out.", exception, profileId: Bundle.Profile.ProfileId);
        }

        private static VisualException Failure(string message, Exception exception, string profileId) => new VisualException(VisualErrorCodes.InferenceFailed, message, exception, profileId: profileId);
        private static void TryDispose(IDisposable? value) { try { value?.Dispose(); } catch { } }

        private sealed class ImageState
        {
            internal ImageState(Tensor<float> features, NativeMultimodalImageState publicState) { Features = features; PublicState = publicState; }
            internal Tensor<float> Features { get; }
            internal NativeMultimodalImageState PublicState { get; }
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
