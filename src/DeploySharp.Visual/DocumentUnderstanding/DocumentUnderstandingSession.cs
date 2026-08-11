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
    /// <summary>Owns exact document Encoder, Decoder Prefill, and KV Decode sessions plus one cached document state. / 拥有精确 Document Encoder、Decoder Prefill、KV Decode Session 与一个缓存 Document State。</summary>
    /// <remarks>The session is single-writer. Registry, tokenizer, and prepared documents remain caller-owned; cancellation, timeout, or callback failure never publishes partial document/KV state. / Session 为 Single-writer；Registry、Tokenizer 与 Prepared Document 保持调用方所有；取消、超时或 Callback 失败不会发布部分 Document/KV State。</remarks>
    public sealed class DocumentUnderstandingSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IInferenceSession _encoder;
        private readonly IInferenceSession _prefill;
        private readonly IInferenceSession _decode;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private EncodedState? _state;
        private DocumentKvStateSummary? _lastKv;
        private bool _disposed;
        private int _active;

        /// <summary>Creates all exact named sessions and rejects incomplete or mixed artifact metadata. / 创建全部精确具名 Session，并拒绝不完整或混合 Artifact Metadata。</summary>
        public DocumentUnderstandingSession(BackendRegistry registry, DocumentUnderstandingBundle bundle, BackendRequest request, SessionOptions? options = null)
        {
            if (registry == null || bundle == null || request == null) throw new ArgumentNullException(registry == null ? nameof(registry) : bundle == null ? nameof(bundle) : nameof(request));
            Bundle = bundle;
            SessionOptions requested = options ?? SessionOptions.Default;
            var stateful = new SessionOptions(1, requested.EnableProfiling);
            var effectiveRequest = new BackendRequest(request.RequiredCapabilities | BackendCapabilities.TensorInference, request.BackendId, request.Device);
            IInferenceSession? encoder = null; IInferenceSession? prefill = null; IInferenceSession? decode = null;
            try
            {
                encoder = registry.CreateSession(bundle.GetArtifact(DocumentArtifactRole.DocumentEncoder), effectiveRequest, stateful);
                ValidateMetadata(encoder.Metadata, bundle.Profile.GetArtifact(DocumentArtifactRole.DocumentEncoder), bundle.Profile.ProfileId);
                prefill = registry.CreateSession(bundle.GetArtifact(DocumentArtifactRole.DecoderPrefill), effectiveRequest, stateful);
                ValidateMetadata(prefill.Metadata, bundle.Profile.GetArtifact(DocumentArtifactRole.DecoderPrefill), bundle.Profile.ProfileId);
                decode = registry.CreateSession(bundle.GetArtifact(DocumentArtifactRole.DecoderWithPast), effectiveRequest, stateful);
                ValidateMetadata(decode.Metadata, bundle.Profile.GetArtifact(DocumentArtifactRole.DecoderWithPast), bundle.Profile.ProfileId);
                _encoder = encoder; _prefill = prefill; _decode = decode;
            }
            catch (Exception exception)
            {
                TryDispose(decode); TryDispose(prefill); TryDispose(encoder); _disposeSource.Dispose(); _idle.Dispose();
                if (exception is VisualException) throw;
                throw Failure("Document backend sessions could not be created.", exception, bundle.Profile.ProfileId);
            }
        }

        /// <summary>Gets immutable artifact bundle. / 获取不可变 Artifact Bundle。</summary>
        public DocumentUnderstandingBundle Bundle { get; }
        /// <summary>Gets whether one exact document state is cached. / 获取是否缓存一个精确 Document State。</summary>
        public bool HasDocument { get { lock (_gate) { EnsureUsableLocked(); return _state != null; } } }
        /// <summary>Gets current immutable encoded-state summary. / 获取当前不可变 Encoded-state Summary。</summary>
        public DocumentEncodedState? CurrentDocumentState { get { lock (_gate) { EnsureUsableLocked(); return _state?.PublicState; } } }
        /// <summary>Gets the last completely published KV summary; mutable KV is never exposed. / 获取最近一次完整发布的 KV Summary；永不公开可变 KV。</summary>
        public DocumentKvStateSummary? CurrentKvState { get { lock (_gate) { EnsureUsableLocked(); return _lastKv; } } }

        /// <summary>Encodes an ordered prepared document and atomically replaces the cached state. / 编码有序 Prepared Document 并原子替换缓存 State。</summary>
        public DocumentEncodedState SetDocument(PreparedDocument document, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SetDocumentCoreAsync(document, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();
        /// <summary>Asynchronously encodes a document; cancellation never installs partial features. / 异步编码文档；取消不会安装部分 Feature。</summary>
        public Task<DocumentEncodedState> SetDocumentAsync(PreparedDocument document, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SetDocumentCoreAsync(document, options ?? VisualExecutionOptions.Default, true, cancellationToken);
        /// <summary>Runs exact task Prompt, Prefill, named Past/Present Decode, and bounded schema parsing against the cached document. / 针对缓存文档执行精确 Task Prompt、Prefill、具名 Past/Present Decode 与受限 Schema Parse。</summary>
        public DocumentUnderstandingResult Generate(DocumentTaskRequest request, IDocumentUnderstandingTokenizer tokenizer, Action<GenerationChunk>? stream = null, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => GenerateCoreAsync(request, tokenizer, stream, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();
        /// <summary>Asynchronously generates one owned result; cancellation, timeout, or callback failure publishes no partial KV/result. / 异步生成一个自有 Result；取消、超时或 Callback 失败不发布部分 KV/Result。</summary>
        public Task<DocumentUnderstandingResult> GenerateAsync(DocumentTaskRequest request, IDocumentUnderstandingTokenizer tokenizer, Action<GenerationChunk>? stream = null, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => GenerateCoreAsync(request, tokenizer, stream, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Clears cached document and KV summaries; generation then requires another successful SetDocument. / 清除缓存 Document 与 KV Summary；之后生成要求再次成功 SetDocument。</summary>
        public void Clear()
        {
            EnterOperation();
            try { lock (_gate) { EnsureUsableLocked(); _state = null; _lastKv = null; } }
            finally { ExitOperation(); }
        }

        /// <summary>Cancels active work, waits for unwind, clears state, and disposes all child sessions exactly once. / 取消活动工作、等待回卷、清除 State，并 Exactly-once 释放全部子 Session。</summary>
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; _disposeSource.Cancel(); }
            _idle.Wait();
            try { _state = null; _lastKv = null; _decode.Dispose(); _prefill.Dispose(); _encoder.Dispose(); }
            finally { _disposeSource.Dispose(); _idle.Dispose(); }
        }

        private async Task<DocumentEncodedState> SetDocumentCoreAsync(PreparedDocument document, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            CancellationToken dispose = EnterOperation();
            using (var timeout = options.Timeout.HasValue ? new CancellationTokenSource(options.Timeout.Value) : new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token, dispose))
            {
                try
                {
                    ValidateDocument(document, Bundle.Profile);
                    PreparedDocumentPage page = document.Pages[0];
                    DocumentArtifactContract contract = Bundle.Profile.GetArtifact(DocumentArtifactRole.DocumentEncoder);
                    var watch = Stopwatch.StartNew();
                    InferenceOutputs outputs = asynchronous ? await _encoder.RunAsync(InferenceInputs.Create(contract.Inputs[0].Name, page.VisualInput.Tensor), linked.Token).ConfigureAwait(false) : _encoder.Run(InferenceInputs.Create(contract.Inputs[0].Name, page.VisualInput.Tensor), linked.Token);
                    watch.Stop();
                    ValidateOutputs(outputs, contract, Bundle.Profile.ProfileId);
                    Tensor<float> features = CopyFiniteFloat(outputs.GetRequired("last_hidden_state"), contract.Outputs[0], Bundle.Profile.ProfileId);
                    string featureSha = DocumentFeatureSummary.Sha((float[])features.Buffer);
                    string identity = DocumentUnderstandingHash.Text(document.Identity + "|" + Bundle.Identity + "|" + featureSha);
                    var publicState = new DocumentEncodedState(identity, document.Identity, page.PageIdentity, Bundle.Profile.ArtifactIdentity, Bundle.Profile.Processor.Identity, Bundle.Profile.Schema.Identity, features.Shape.ToArray(), featureSha, watch.Elapsed);
                    var state = new EncodedState(features, publicState, page.PreprocessTime);
                    lock (_gate) { EnsureUsableLocked(); _state = state; _lastKv = null; }
                    return publicState;
                }
                catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
                catch (DeploySharpException exception) when (linked.IsCancellationRequested) { throw MapCancellation(exception, caller); }
                catch (VisualException) { throw; }
                catch (Exception exception) { throw Failure("Document encoding failed.", exception, Bundle.Profile.ProfileId); }
                finally { if (options.DisposeOwnedInputOnCompletion) document.Dispose(); ExitOperation(); }
            }
        }

        private async Task<DocumentUnderstandingResult> GenerateCoreAsync(DocumentTaskRequest request, IDocumentUnderstandingTokenizer tokenizer, Action<GenerationChunk>? stream, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (request == null || tokenizer == null) throw new ArgumentNullException(request == null ? nameof(request) : nameof(tokenizer));
            CancellationToken dispose = EnterOperation();
            using (var timeout = options.Timeout.HasValue ? new CancellationTokenSource(options.Timeout.Value) : new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token, dispose))
            {
                try
                {
                    EncodedState state;
                    lock (_gate) { EnsureUsableLocked(); state = _state ?? throw new VisualException(VisualErrorCodes.DocumentUnderstandingStateInvalid, "SetDocument must succeed before generation.", profileId: Bundle.Profile.ProfileId); }
                    linked.Token.ThrowIfCancellationRequested();
                    ValidateRequest(request, tokenizer, Bundle.Profile);
                    var tokenizeWatch = Stopwatch.StartNew(); DocumentTokenSequence prompt = tokenizer.Encode(Bundle.Profile, request); tokenizeWatch.Stop();
                    ValidatePrompt(prompt, tokenizer, Bundle.Profile);
                    long[] promptIds = prompt.CopyTokenIds();
                    DocumentArtifactContract prefillContract = Bundle.Profile.GetArtifact(DocumentArtifactRole.DecoderPrefill);
                    var prefillInputs = new InferenceInputs(new[]
                    {
                        new NamedTensor("input_ids", new Tensor<long>(new TensorShape(1, promptIds.Length), promptIds, TensorBufferOwnership.Transfer)),
                        new NamedTensor("encoder_hidden_states", state.Features)
                    });
                    var prefillWatch = Stopwatch.StartNew();
                    InferenceOutputs current = asynchronous ? await _prefill.RunAsync(prefillInputs, linked.Token).ConfigureAwait(false) : _prefill.Run(prefillInputs, linked.Token);
                    prefillWatch.Stop(); ValidateOutputs(current, prefillContract, Bundle.Profile.ProfileId);
                    KvValues kv = CopyPrefillKv(current, Bundle.Profile);
                    var completion = new List<int>(); var decodeTimes = new List<TimeSpan>(); var tokenScores = new List<GenerativeTokenScore>(); string streamed = string.Empty; bool eos = false;
                    while (promptIds.Length + completion.Count < Bundle.Profile.Tokenizer.MaximumContextTokens)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        SelectedToken selected = SelectToken(current.GetRequired("logits"), Bundle.Profile, completion.Count);
                        completion.Add(selected.TokenId); tokenScores.Add(new GenerativeTokenScore(completion.Count - 1, selected.TokenId, selected.Logit, selected.LogProbability));
                        string cumulative = tokenizer.Decode(completion); string fragment = cumulative.StartsWith(streamed, StringComparison.Ordinal) ? cumulative.Substring(streamed.Length) : cumulative; streamed = cumulative;
                        eos = selected.TokenId == Bundle.Profile.Tokenizer.EosTokenId;
                        stream?.Invoke(new GenerationChunk(completion.Count - 1, fragment, selected.TokenId, eos ? GenerationFinishReason.EndOfSequence : GenerationFinishReason.None));
                        if (eos) break;
                        InferenceInputs decodeInputs = CreateDecodeInputs(selected.TokenId, kv, Bundle.Profile);
                        var stepWatch = Stopwatch.StartNew();
                        current = asynchronous ? await _decode.RunAsync(decodeInputs, linked.Token).ConfigureAwait(false) : _decode.Run(decodeInputs, linked.Token);
                        stepWatch.Stop(); decodeTimes.Add(stepWatch.Elapsed);
                        ValidateOutputs(current, Bundle.Profile.GetArtifact(DocumentArtifactRole.DecoderWithPast), Bundle.Profile.ProfileId);
                        kv = CopyDecodeKv(current, kv.Cross, Bundle.Profile);
                    }
                    GenerationFinishReason finish = eos ? GenerationFinishReason.EndOfSequence : GenerationFinishReason.MaxTokens;
                    if (!eos) stream?.Invoke(new GenerationChunk(completion.Count, string.Empty, null, finish));
                    var finalWatch = Stopwatch.StartNew(); string rawText = tokenizer.Decode(completion); finalWatch.Stop();
                    var parseWatch = Stopwatch.StartNew(); DocumentStructuredOutput structured = DocumentStructuredOutputParser.Parse(completion, rawText, Bundle.Profile.Schema, request.SchemaId, state.PublicState.PageIdentity, prompt.PromptSha256); parseWatch.Stop();
                    var generation = new GenerationResult(rawText, finish, new TokenUsage(promptIds.Length, completion.Count), completion);
                    DocumentKvStateSummary kvSummary = SummarizeKv(kv, prompt.PromptSha256, Bundle.Profile.KvCache!);
                    var timing = new DocumentExecutionTiming(state.PreprocessTime, state.PublicState.EncodeTime, tokenizeWatch.Elapsed, prefillWatch.Elapsed, decodeTimes, finalWatch.Elapsed, parseWatch.Elapsed);
                    var result = new DocumentUnderstandingResult(generation, request, structured, state.PublicState, kvSummary, timing);
                    lock (_gate) { EnsureUsableLocked(); _lastKv = kvSummary; }
                    return result;
                }
                catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
                catch (DeploySharpException exception) when (linked.IsCancellationRequested) { throw MapCancellation(exception, caller); }
                catch (VisualException) { throw; }
                catch (Exception exception) { throw Failure("Document Prefill/KV generation failed.", exception, Bundle.Profile.ProfileId); }
                finally { ExitOperation(); }
            }
        }

        private static InferenceInputs CreateDecodeInputs(int token, KvValues kv, DocumentUnderstandingProfile profile)
        {
            DocumentKvCacheContract contract = profile.KvCache!;
            var values = new List<NamedTensor> { new NamedTensor("input_ids", new Tensor<long>(new TensorShape(1, 1), new long[] { token }, TensorBufferOwnership.Transfer)) };
            for (int layer = 0; layer < contract.LayerCount; layer++)
            {
                values.Add(new NamedTensor(contract.Past(layer, true, true), kv.Self[(layer * 2)]));
                values.Add(new NamedTensor(contract.Past(layer, true, false), kv.Self[(layer * 2) + 1]));
                values.Add(new NamedTensor(contract.Past(layer, false, true), kv.Cross[(layer * 2)]));
                values.Add(new NamedTensor(contract.Past(layer, false, false), kv.Cross[(layer * 2) + 1]));
            }
            return new InferenceInputs(values);
        }

        private static KvValues CopyPrefillKv(InferenceOutputs outputs, DocumentUnderstandingProfile profile)
        {
            DocumentKvCacheContract contract = profile.KvCache!; var self = new List<Tensor<float>>(contract.LayerCount * 2); var cross = new List<Tensor<float>>(contract.LayerCount * 2);
            for (int layer = 0; layer < contract.LayerCount; layer++)
            {
                self.Add(CopyKv(outputs.GetRequired(contract.Present(layer, true, true)), contract, 1, contract.MaximumPastTokens, contract.Present(layer, true, true), profile.ProfileId));
                self.Add(CopyKv(outputs.GetRequired(contract.Present(layer, true, false)), contract, 1, contract.MaximumPastTokens, contract.Present(layer, true, false), profile.ProfileId));
                cross.Add(CopyKv(outputs.GetRequired(contract.Present(layer, false, true)), contract, contract.EncoderTokens, contract.EncoderTokens, contract.Present(layer, false, true), profile.ProfileId));
                cross.Add(CopyKv(outputs.GetRequired(contract.Present(layer, false, false)), contract, contract.EncoderTokens, contract.EncoderTokens, contract.Present(layer, false, false), profile.ProfileId));
            }
            return new KvValues(self, cross);
        }

        private static KvValues CopyDecodeKv(InferenceOutputs outputs, IReadOnlyList<Tensor<float>> cross, DocumentUnderstandingProfile profile)
        {
            DocumentKvCacheContract contract = profile.KvCache!; var self = new List<Tensor<float>>(contract.LayerCount * 2); long? length = null;
            for (int layer = 0; layer < contract.LayerCount; layer++)
            {
                Tensor<float> key = CopyKv(outputs.GetRequired(contract.Present(layer, true, true)), contract, 1, contract.MaximumPastTokens, contract.Present(layer, true, true), profile.ProfileId);
                Tensor<float> value = CopyKv(outputs.GetRequired(contract.Present(layer, true, false)), contract, 1, contract.MaximumPastTokens, contract.Present(layer, true, false), profile.ProfileId);
                if (key.Shape[2] != value.Shape[2] || (length.HasValue && length.Value != key.Shape[2])) throw new VisualException(VisualErrorCodes.DocumentUnderstandingGenerationInvalid, "Decode KV sequence lengths are inconsistent.", profileId: profile.ProfileId);
                length = key.Shape[2]; self.Add(key); self.Add(value);
            }
            return new KvValues(self, cross);
        }

        private static Tensor<float> CopyKv(ITensor tensor, DocumentKvCacheContract contract, int minimumTokens, int maximumTokens, string name, string profileId)
        {
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 4 || tensor.Shape[0] != 1 || tensor.Shape[1] != contract.Heads || tensor.Shape[2] < minimumTokens || tensor.Shape[2] > maximumTokens || tensor.Shape[3] != contract.HeadDimension) throw new VisualException(VisualErrorCodes.DocumentUnderstandingGenerationInvalid, "KV type or axes differ from the profile.", profileId: profileId, tensorName: name);
            float[] values = ((float[])tensor.Buffer).ToArray(); if (values.Any(value => float.IsNaN(value) || float.IsInfinity(value))) throw new VisualException(VisualErrorCodes.DocumentUnderstandingGenerationInvalid, "KV contains NaN or Infinity.", profileId: profileId, tensorName: name);
            return new Tensor<float>(new TensorShape(tensor.Shape.ToArray()), values, TensorBufferOwnership.Transfer);
        }

        private static SelectedToken SelectToken(ITensor tensor, DocumentUnderstandingProfile profile, int step)
        {
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 3 || tensor.Shape[0] != 1 || tensor.Shape[1] <= 0 || tensor.Shape[2] != profile.Tokenizer.VocabularySize) throw new VisualException(VisualErrorCodes.DocumentUnderstandingGenerationInvalid, "Decoder Logit type/shape differs from the profile.", profileId: profile.ProfileId, tensorName: "logits");
            float[] values = (float[])tensor.Buffer; int vocabulary = profile.Tokenizer.VocabularySize; int offset = checked(((int)tensor.Shape[1] - 1) * vocabulary); int selected = -1; float maximum = float.NegativeInfinity;
            for (int token = 0; token < vocabulary; token++)
            {
                float value = values[offset + token]; if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingGenerationInvalid, "Decoder Logits contain NaN or Infinity.", profileId: profile.ProfileId, tensorName: "logits");
                if (token == profile.Tokenizer.UnknownTokenId) continue;
                if (value > maximum) { maximum = value; selected = token; }
            }
            if (selected < 0) throw new VisualException(VisualErrorCodes.DocumentUnderstandingGenerationInvalid, "No selectable document token remained.", profileId: profile.ProfileId);
            double sum = 0; for (int token = 0; token < vocabulary; token++) if (token != profile.Tokenizer.UnknownTokenId) sum += Math.Exp(values[offset + token] - maximum);
            return new SelectedToken(selected, maximum, (float)(-Math.Log(sum)));
        }

        private static DocumentKvStateSummary SummarizeKv(KvValues kv, string promptSha, DocumentKvCacheContract contract)
        {
            using (SHA256 hash = SHA256.Create())
            {
                foreach (Tensor<float> tensor in kv.Self.Concat(kv.Cross))
                {
                    float[] values = (float[])tensor.Buffer; var bytes = new byte[checked(values.Length * sizeof(float))]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length); hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0); string sha = string.Concat(hash.Hash!.Select(value => value.ToString("x2")));
                return new DocumentKvStateSummary(contract.SchemaId, contract.LayerCount, contract.Heads, checked((int)kv.Self[0].Shape[2]), checked((int)kv.Cross[0].Shape[2]), contract.HeadDimension, sha, promptSha);
            }
        }

        private static void ValidateDocument(PreparedDocument document, DocumentUnderstandingProfile profile)
        {
            document.EnsureUsable();
            if (!string.Equals(document.ProfileId, profile.ProfileId, StringComparison.Ordinal) || document.Pages.Count != 1 || profile.OcrOwnership != DocumentOcrOwnership.NoneOcrFree) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "Prepared document differs from the executable single-page OCR-free profile.", profileId: profile.ProfileId);
            PreparedVisualInput input = document.Pages[0].VisualInput;
            if (input.InputName != "pixel_values" || input.Tensor.ElementType != TensorElementType.Float32 || input.Tensor.Shape.Rank != 4 || input.Tensor.Shape[0] != 1 || input.Tensor.Shape[1] != 3 || input.Tensor.Shape[2] != profile.Processor.ModelSize.Height || input.Tensor.Shape[3] != profile.Processor.ModelSize.Width || input.InputId == null || !DocumentUnderstandingHash.IsSha256(input.InputId)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Prepared page tensor differs from the processor/profile contract.", profileId: profile.ProfileId, tensorName: "pixel_values");
            foreach (float value in (float[])input.Tensor.Buffer) if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Prepared page contains NaN or Infinity.", profileId: profile.ProfileId, tensorName: "pixel_values");
        }

        private static void ValidateRequest(DocumentTaskRequest request, IDocumentUnderstandingTokenizer tokenizer, DocumentUnderstandingProfile profile)
        {
            if (!profile.Tasks.Contains(request.Task)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingCapabilityUnavailable, "The requested document task is not supported by the profile.", profileId: profile.ProfileId);
            if (!string.Equals(request.SchemaId, profile.Schema.SchemaId, StringComparison.Ordinal) || !string.Equals(tokenizer.TokenizerId, profile.Tokenizer.TokenizerId, StringComparison.Ordinal) || !string.Equals(tokenizer.Identity, profile.Tokenizer.Identity, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "Request schema or tokenizer differs from the document state/profile.", profileId: profile.ProfileId);
        }
        private static void ValidatePrompt(DocumentTokenSequence prompt, IDocumentUnderstandingTokenizer tokenizer, DocumentUnderstandingProfile profile)
        {
            long[] ids = prompt.CopyTokenIds();
            if (!string.Equals(prompt.TokenizerId, tokenizer.TokenizerId, StringComparison.Ordinal) || !string.Equals(prompt.TokenizerIdentity, profile.Tokenizer.Identity, StringComparison.Ordinal) || ids.Length == 0 || ids.Length >= profile.Tokenizer.MaximumContextTokens || ids.Any(value => value < 0 || value >= profile.Tokenizer.VocabularySize)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "Prompt tokens differ from tokenizer/profile/context identity.", profileId: profile.ProfileId);
        }

        private static void ValidateMetadata(ModelMetadata metadata, DocumentArtifactContract contract, string profileId)
        {
            if (!metadata.Inputs.Select(value => value.Name).SequenceEqual(contract.Inputs.Select(value => value.Name), StringComparer.Ordinal) || !metadata.Outputs.Select(value => value.Name).SequenceEqual(contract.Outputs.Select(value => value.Name), StringComparer.Ordinal)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Backend metadata named-port order differs from the document profile.", profileId: profileId, modelId: contract.ModelId);
            for (int index = 0; index < metadata.Inputs.Count; index++) ValidateDescriptor(metadata.Inputs[index], contract.Inputs[index], profileId);
            for (int index = 0; index < metadata.Outputs.Count; index++) ValidateDescriptor(metadata.Outputs[index], contract.Outputs[index], profileId);
        }
        private static void ValidateDescriptor(TensorDescriptor descriptor, GenerativeVisionLanguageTensorContract contract, string profileId)
        {
            if (descriptor.ElementType != contract.ElementType || descriptor.Shape.Rank != contract.ShapePattern.Rank) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Backend metadata type/rank differs from the document profile.", profileId: profileId, tensorName: contract.Name);
            for (int index = 0; index < descriptor.Shape.Rank; index++) if (descriptor.Shape[index] > 0 && contract.ShapePattern[index] > 0 && descriptor.Shape[index] != contract.ShapePattern[index]) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Backend metadata fixed dimension differs from the document profile.", profileId: profileId, tensorName: contract.Name);
        }
        private static void ValidateOutputs(InferenceOutputs outputs, DocumentArtifactContract contract, string profileId)
        {
            if (!outputs.Select(value => value.Name).SequenceEqual(contract.Outputs.Select(value => value.Name), StringComparer.Ordinal)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Runtime output names/order differ from the document profile.", profileId: profileId, modelId: contract.ModelId);
        }
        private static Tensor<float> CopyFiniteFloat(ITensor tensor, GenerativeVisionLanguageTensorContract contract, string profileId)
        {
            if (tensor.ElementType != TensorElementType.Float32 || !GenerativeVisionLanguageHash.ShapeMatches(contract.ShapePattern, tensor.Shape) || tensor.Length > contract.MaximumElements) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Runtime tensor type/shape/capacity differs from the document profile.", profileId: profileId, tensorName: contract.Name);
            float[] values = ((float[])tensor.Buffer).ToArray(); if (values.Any(value => float.IsNaN(value) || float.IsInfinity(value))) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Runtime document tensor contains NaN or Infinity.", profileId: profileId, tensorName: contract.Name);
            return new Tensor<float>(new TensorShape(tensor.Shape.ToArray()), values, TensorBufferOwnership.Transfer);
        }

        private CancellationToken EnterOperation()
        {
            lock (_gate) EnsureUsableLocked();
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) throw new VisualException(VisualErrorCodes.DocumentUnderstandingConcurrentOperation, "The single-writer document session is already executing.", profileId: Bundle.Profile.ProfileId);
            _idle.Reset(); lock (_gate) { if (_disposed) { ExitOperation(); throw new VisualException(VisualErrorCodes.ObjectDisposed, "The document session is disposed.", profileId: Bundle.Profile.ProfileId); } return _disposeSource.Token; }
        }
        private void ExitOperation() { Interlocked.Exchange(ref _active, 0); _idle.Set(); }
        private void EnsureUsableLocked() { if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The document session is disposed.", profileId: Bundle.Profile.ProfileId); }
        private VisualException MapCancellation(Exception exception, CancellationToken caller)
        {
            if (_disposeSource.IsCancellationRequested) return new VisualException(VisualErrorCodes.ObjectDisposed, "The document session was disposed during execution.", exception, profileId: Bundle.Profile.ProfileId);
            if (caller.IsCancellationRequested) return new VisualException(VisualErrorCodes.Cancelled, "The document operation was cancelled.", exception, profileId: Bundle.Profile.ProfileId);
            return new VisualException(VisualErrorCodes.Timeout, "The document operation timed out.", exception, profileId: Bundle.Profile.ProfileId);
        }
        private static VisualException Failure(string message, Exception exception, string profileId) => new VisualException(VisualErrorCodes.InferenceFailed, message, exception, profileId: profileId);
        private static void TryDispose(IDisposable? value) { try { value?.Dispose(); } catch { } }

        private sealed class EncodedState { internal EncodedState(Tensor<float> features, DocumentEncodedState state, TimeSpan preprocessTime) { Features = features; PublicState = state; PreprocessTime = preprocessTime; } internal Tensor<float> Features { get; } internal DocumentEncodedState PublicState { get; } internal TimeSpan PreprocessTime { get; } }
        private sealed class KvValues { internal KvValues(IReadOnlyList<Tensor<float>> self, IReadOnlyList<Tensor<float>> cross) { Self = self; Cross = cross; } internal IReadOnlyList<Tensor<float>> Self { get; } internal IReadOnlyList<Tensor<float>> Cross { get; } }
        private readonly struct SelectedToken { internal SelectedToken(int id, float logit, float probability) { TokenId = id; Logit = logit; LogProbability = probability; } internal int TokenId { get; } internal float Logit { get; } internal float LogProbability { get; } }
    }
}
