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
    /// <summary>Owns exact image/text encoder sessions and bounded embedding caches. / 拥有精确的图像/文本 Encoder Session 与受限 Embedding 缓存。</summary>
    /// <remarks>Calls are single-writer; embeddings are copied before publication and are valid only for the bound profile/artifacts. / 调用采用单写者；Embedding 发布前复制，且只对绑定 Profile/工件有效。</remarks>
    public sealed class VisionLanguageEmbeddingSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IInferenceSession _image;
        private readonly IInferenceSession _text;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private bool _disposed;
        private int _active;
        private VisionLanguageImageEmbedding? _imageCache;
        private VisionLanguageTextEmbedding? _textCache;

        /// <summary>Creates two exact named backend sessions and validates their metadata against the profile. / 创建两条精确具名 Backend Session，并按 Profile 验证元数据。</summary>
        public VisionLanguageEmbeddingSession(BackendRegistry backendRegistry, VisionLanguageArtifactBundle bundle, BackendRequest request, SessionOptions? sessionOptions = null)
        {
            if (backendRegistry == null) throw new ArgumentNullException(nameof(backendRegistry));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!bundle.Profile.Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, bundle.Profile.Blocker ?? "The VLM profile is external-only.", profileId: bundle.Profile.ProfileId);
            var effective = new BackendRequest(request.RequiredCapabilities | BackendCapabilities.TensorInference, request.BackendId, request.Device);
            IInferenceSession? image = null;
            IInferenceSession? text = null;
            try
            {
                image = backendRegistry.CreateSession(bundle.ImageEncoder, effective, sessionOptions ?? SessionOptions.Default);
                ValidateMetadata(image.Metadata, bundle.Profile.GetArtifact(VisionLanguageArtifactRole.ImageEncoder), bundle.Profile.ProfileId);
                text = backendRegistry.CreateSession(bundle.TextEncoder, effective, sessionOptions ?? SessionOptions.Default);
                ValidateMetadata(text.Metadata, bundle.Profile.GetArtifact(VisionLanguageArtifactRole.TextEncoder), bundle.Profile.ProfileId);
                _text = text;
                _image = image;
            }
            catch (Exception exception)
            {
                text?.Dispose();
                image?.Dispose();
                _disposeSource.Dispose();
                _idle.Dispose();
                if (exception is VisualException) throw;
                throw new VisualException(VisualErrorCodes.InferenceFailed, "Vision-language encoder sessions could not be created.", exception, bundle.Profile.ProfileId, technicalDetails: exception.ToString());
            }
        }

        /// <summary>Gets the immutable profile and concrete encoder paths. / 获取不可变 Profile 与具体 Encoder 路径。</summary>
        public VisionLanguageArtifactBundle Bundle { get; }
        /// <summary>Gets whether an image embedding is cached. / 获取是否缓存了图像 Embedding。</summary>
        public bool HasImage { get { lock (_gate) { EnsureUsableLocked(); return _imageCache != null; } } }
        /// <summary>Gets whether text embeddings are cached. / 获取是否缓存了文本 Embedding。</summary>
        public bool HasText { get { lock (_gate) { EnsureUsableLocked(); return _textCache != null; } } }
        /// <summary>Gets the current image identity, or null after clear. / 获取当前图像 Identity；clear 后为 null。</summary>
        public VisionLanguageEmbeddingIdentity? CurrentImage { get { lock (_gate) { EnsureUsableLocked(); return _imageCache?.Identity; } } }
        /// <summary>Gets the current text identity, or null after clear. / 获取当前文本 Identity；clear 后为 null。</summary>
        public VisionLanguageEmbeddingIdentity? CurrentText { get { lock (_gate) { EnsureUsableLocked(); return _textCache?.Identity; } } }

        /// <summary>Runs one image encoder call and atomically replaces the image cache. / 执行一次图像 Encoder 调用并原子替换图像缓存。</summary>
        public VisionLanguageImageEmbedding EncodeImage(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => EncodeImageCoreAsync(input, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();
        /// <summary>Asynchronously runs one image encoder call; cancellation publishes no partial cache. / 异步执行一次图像 Encoder；取消不会发布部分缓存。</summary>
        public Task<VisionLanguageImageEmbedding> EncodeImageAsync(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => EncodeImageCoreAsync(input, options ?? VisualExecutionOptions.Default, true, cancellationToken);
        /// <summary>Runs one text encoder call and atomically replaces the text cache. / 执行一次文本 Encoder 调用并原子替换文本缓存。</summary>
        public VisionLanguageTextEmbedding EncodeText(TextTokenBatch tokens, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => EncodeTextCoreAsync(tokens, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();
        /// <summary>Asynchronously runs one text encoder call; cancellation publishes no partial cache. / 异步执行一次文本 Encoder；取消不会发布部分缓存。</summary>
        public Task<VisionLanguageTextEmbedding> EncodeTextAsync(TextTokenBatch tokens, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => EncodeTextCoreAsync(tokens, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Clears both caches; results already returned remain caller-owned copies. / 清除两个缓存；已返回结果仍为调用方自有副本。</summary>
        public void ClearCache()
        {
            EnterOperation();
            try { lock (_gate) { EnsureUsableLocked(); _imageCache = null; _textCache = null; } }
            finally { ExitOperation(); }
        }

        /// <inheritdoc />
        /// <remarks>Cancellation, waits for the active operation, then disposes both sessions exactly once. / 取消、等待活动操作退出，再仅一次释放两条 Session。</remarks>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }
            _idle.Wait();
            try { _imageCache = null; _textCache = null; _text.Dispose(); _image.Dispose(); }
            finally { _disposeSource.Dispose(); _idle.Dispose(); }
        }

        private async Task<VisionLanguageImageEmbedding> EncodeImageCoreAsync(PreparedVisualInput input, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CancellationToken disposeToken = EnterOperation();
            using (var timeout = options.Timeout.HasValue ? new CancellationTokenSource(options.Timeout.Value) : new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token, disposeToken))
            {
                try
                {
                    input.EnsureUsable();
                    VisionLanguageArtifactContract contract = Bundle.Profile.GetArtifact(VisionLanguageArtifactRole.ImageEncoder);
                    ValidateInput(input, contract, Bundle.Profile.MaximumImageBatch, Bundle.Profile.ProfileId);
                    var watch = Stopwatch.StartNew();
                    InferenceOutputs outputs = asynchronous ? await _image.RunAsync(InferenceInputs.Create(contract.Inputs[0].Name, input.Tensor), linked.Token).ConfigureAwait(false) : _image.Run(InferenceInputs.Create(contract.Inputs[0].Name, input.Tensor), linked.Token);
                    watch.Stop();
                    float[] values = CopyEmbedding(outputs, contract, input.Tensor.Shape[0], Bundle.Profile.EmbeddingDimension, Bundle.Profile.ProfileId);
                    string content = input.InputId ?? throw new VisualException(VisualErrorCodes.VisionLanguageIdentityMismatch, "Image encoding requires PreparedVisualInput.InputId to be the exact source SHA256.", profileId: Bundle.Profile.ProfileId);
                    var result = new VisionLanguageImageEmbedding(new VisionLanguageEmbeddingIdentity(Bundle.Profile.ProfileId, Bundle.Profile.ArtifactIdentity, content, Bundle.Profile.EmbeddingDimension), input.Tensor.Shape[0] > int.MaxValue ? throw new VisualException(VisualErrorCodes.VisionLanguageLimitExceeded, "The image batch is too large.", profileId: Bundle.Profile.ProfileId) : (int)input.Tensor.Shape[0], values, watch.Elapsed);
                    lock (_gate) { EnsureUsableLocked(); _imageCache = result; }
                    return result;
                }
                catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
                catch (DeploySharpException exception) when (linked.IsCancellationRequested) { throw MapCancellation(exception, caller); }
                catch (VisualException) { throw; }
                catch (Exception exception) { throw new VisualException(VisualErrorCodes.InferenceFailed, "The image encoder failed.", exception, Bundle.Profile.ProfileId); }
                finally { if (options.DisposeOwnedInputOnCompletion && input.Ownership == PreparedInputOwnership.Owned) input.Dispose(); ExitOperation(); }
            }
        }

        private async Task<VisionLanguageTextEmbedding> EncodeTextCoreAsync(TextTokenBatch tokens, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            CancellationToken disposeToken = EnterOperation();
            using (var timeout = options.Timeout.HasValue ? new CancellationTokenSource(options.Timeout.Value) : new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token, disposeToken))
            {
                try
                {
                    VisionLanguageArtifactContract contract = Bundle.Profile.GetArtifact(VisionLanguageArtifactRole.TextEncoder);
                    ValidateTokens(tokens, contract, Bundle.Profile);
                    var tensors = new List<NamedTensor> { new NamedTensor(contract.Inputs[0].Name, new Tensor<long>(new TensorShape(tokens.BatchSize, tokens.SequenceLength), tokens.CopyInputIds(), TensorBufferOwnership.Transfer)) };
                    if (Bundle.Profile.Tokenizer.AttentionMaskRequired) tensors.Add(new NamedTensor(contract.Inputs[1].Name, new Tensor<long>(new TensorShape(tokens.BatchSize, tokens.SequenceLength), tokens.CopyAttentionMask()!, TensorBufferOwnership.Transfer)));
                    var watch = Stopwatch.StartNew();
                    InferenceOutputs outputs = asynchronous ? await _text.RunAsync(new InferenceInputs(tensors), linked.Token).ConfigureAwait(false) : _text.Run(new InferenceInputs(tensors), linked.Token);
                    watch.Stop();
                    float[] values = CopyEmbedding(outputs, contract, tokens.BatchSize, Bundle.Profile.EmbeddingDimension, Bundle.Profile.ProfileId);
                    var result = new VisionLanguageTextEmbedding(new VisionLanguageEmbeddingIdentity(Bundle.Profile.ProfileId, Bundle.Profile.ArtifactIdentity, tokens.ContentSha256, Bundle.Profile.EmbeddingDimension), tokens.Texts, values, watch.Elapsed);
                    lock (_gate) { EnsureUsableLocked(); _textCache = result; }
                    return result;
                }
                catch (OperationCanceledException exception) { throw MapCancellation(exception, caller); }
                catch (DeploySharpException exception) when (linked.IsCancellationRequested) { throw MapCancellation(exception, caller); }
                catch (VisualException) { throw; }
                catch (Exception exception) { throw new VisualException(VisualErrorCodes.InferenceFailed, "The text encoder failed.", exception, Bundle.Profile.ProfileId); }
                finally { ExitOperation(); }
            }
        }

        private static void ValidateInput(PreparedVisualInput input, VisionLanguageArtifactContract contract, int maxBatch, string profileId)
        {
            if (contract.Inputs.Count != 1 || input.InputName != contract.Inputs[0].Name || input.Tensor.ElementType != TensorElementType.Float32 || !VisionLanguageHash.ShapeMatches(contract.Inputs[0].ShapePattern, input.Tensor.Shape) || input.Tensor.Shape.Rank != 4 || input.Tensor.Shape[0] <= 0 || input.Tensor.Shape[0] > maxBatch || input.Tensor.Length > contract.Inputs[0].MaximumElements) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The prepared image tensor does not match the exact image encoder contract.", profileId: profileId, tensorName: contract.Inputs[0].Name);
            foreach (float value in (float[])input.Tensor.Buffer) if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The prepared image tensor contains NaN or Infinity.", profileId: profileId, tensorName: contract.Inputs[0].Name);
        }

        private static void ValidateTokens(TextTokenBatch tokens, VisionLanguageArtifactContract contract, VisionLanguageEmbeddingProfile profile)
        {
            long[] inputIds = tokens.CopyInputIds();
            long[]? attentionMask = tokens.CopyAttentionMask();
            bool identityMatches = string.Equals(tokens.TokenizerId, profile.Tokenizer.TokenizerId, StringComparison.Ordinal) && string.Equals(tokens.TokenizerSha256, profile.Tokenizer.Sha256, StringComparison.OrdinalIgnoreCase);
            bool maskMatches = profile.Tokenizer.AttentionMaskRequired ? attentionMask != null && contract.Inputs.Count == 2 : attentionMask == null && contract.Inputs.Count == 1;
            if (tokens.BatchSize <= 0 || tokens.SequenceLength != profile.Tokenizer.MaximumTokens || tokens.BatchSize > profile.MaximumTextBatch || tokens.SequenceLength != contract.Inputs[0].ShapePattern[1] || !identityMatches || !maskMatches) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The token batch does not match the exact tokenizer and text encoder contract.", profileId: profile.ProfileId, tensorName: contract.Inputs[0].Name);
            if (attentionMask != null && attentionMask.Length != inputIds.Length) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The attention mask shape does not match input_ids.", profileId: profile.ProfileId, tensorName: contract.Inputs[1].Name);
        }

        private static float[] CopyEmbedding(InferenceOutputs outputs, VisionLanguageArtifactContract contract, long batch, int dimension, string profileId)
        {
            if (outputs == null || outputs.Count != 1 || !string.Equals(outputs[0].Name, contract.Outputs[0].Name, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The encoder returned unexpected output names or count.", profileId: profileId);
            ITensor tensor = outputs.GetRequired(contract.Outputs[0].Name);
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 2 || tensor.Shape[0] != batch || tensor.Shape[1] != dimension || tensor.Length > contract.Outputs[0].MaximumElements) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The encoder output shape or type does not match the profile.", profileId: profileId, tensorName: contract.Outputs[0].Name);
            float[] values = ((float[])tensor.Buffer).ToArray();
            for (int index = 0; index < values.Length; index++) if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The encoder output contains NaN or Infinity.", profileId: profileId, tensorName: contract.Outputs[0].Name);
            for (int row = 0; row < batch; row++)
            {
                double sum = 0;
                for (int column = 0; column < dimension; column++) { float value = values[checked((int)(row * dimension) + column)]; sum += value * value; }
                double norm = Math.Sqrt(sum);
                if (norm < .95 || norm > 1.05) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The encoder output is not L2-normalized as required by the profile.", profileId: profileId, tensorName: contract.Outputs[0].Name, technicalDetails: "norm=" + norm.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
            return values;
        }

        private static void ValidateMetadata(ModelMetadata metadata, VisionLanguageArtifactContract expected, string profileId)
        {
            if (metadata == null || metadata.Inputs.Count != expected.Inputs.Count || metadata.Outputs.Count != expected.Outputs.Count) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Backend metadata does not expose the exact profile port count.", profileId: profileId, modelId: expected.ModelId);
            foreach (VisionLanguageTensorContract port in expected.Inputs) { TensorDescriptor actual = metadata.Inputs.SingleOrDefault(value => value.Name == port.Name) ?? throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A required named input is missing.", profileId: profileId, tensorName: port.Name); if (actual.ElementType != port.ElementType || !VisionLanguageHash.ShapeMatches(port.ShapePattern, actual.Shape)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A named input type or shape differs from the profile.", profileId: profileId, tensorName: port.Name); }
            foreach (VisionLanguageTensorContract port in expected.Outputs) { TensorDescriptor actual = metadata.Outputs.SingleOrDefault(value => value.Name == port.Name) ?? throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A required named output is missing.", profileId: profileId, tensorName: port.Name); if (actual.ElementType != port.ElementType || !VisionLanguageHash.ShapeMatches(port.ShapePattern, actual.Shape)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A named output type or shape differs from the profile.", profileId: profileId, tensorName: port.Name); }
        }

        private CancellationToken EnterOperation()
        {
            lock (_gate) { EnsureUsableLocked(); if (Interlocked.Exchange(ref _active, 1) != 0) throw new VisualException(VisualErrorCodes.VisionLanguageConcurrentOperation, "Only one embedding operation may execute at a time.", profileId: Bundle.Profile.ProfileId); _idle.Reset(); return _disposeSource.Token; }
        }
        private void ExitOperation() { Interlocked.Exchange(ref _active, 0); _idle.Set(); }
        private void EnsureUsableLocked() { if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The vision-language session has been disposed.", profileId: Bundle.Profile.ProfileId); }
        private static VisualException MapCancellation(Exception exception, CancellationToken caller) => new VisualException(caller.IsCancellationRequested ? VisualErrorCodes.Cancelled : VisualErrorCodes.Timeout, caller.IsCancellationRequested ? "The vision-language operation was cancelled." : "The vision-language operation exceeded its timeout.", exception);
    }
}
