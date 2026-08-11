using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Owns image-encoder and prompt/mask-decoder backend sessions plus one exact cached image embedding. / 拥有图像 Encoder 与 Prompt/Mask Decoder Backend Session 以及一个精确缓存图像 Embedding。</summary>
    /// <remarks>Operations mutate or consume shared image state and therefore reject concurrent calls; the registry remains caller-owned. / 操作会变更或使用共享图像状态，因此拒绝并发调用；Registry 仍由调用方拥有。</remarks>
    public sealed class PromptableSegmentationImageSession : IDisposable
    {
        private readonly object _lifetimeGate = new object();
        private readonly IInferenceSession _encoder;
        private readonly IInferenceSession _decoder;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private EmbeddingState? _state;
        private bool _disposed;
        private int _operationActive;

        /// <summary>Creates and validates every exact named backend session in a complete SAM v1 bundle. / 创建并验证完整 SAM v1 Bundle 中每条精确具名 Backend Session。</summary>
        public PromptableSegmentationImageSession(BackendRegistry backendRegistry, PromptableSegmentationArtifactBundle bundle, BackendRequest request, SessionOptions? sessionOptions = null)
        {
            if (backendRegistry == null) throw new ArgumentNullException(nameof(backendRegistry));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (bundle.Profile.ExecutionKind != PromptableSegmentationExecutionKind.SamV1ImageOnnx) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The profile has no complete supported native image pipeline.", profileId: bundle.Profile.ProfileId);
            SessionOptions requested = sessionOptions ?? SessionOptions.Default;
            var statefulOptions = new SessionOptions(1, requested.EnableProfiling);
            var effectiveRequest = new BackendRequest(request.RequiredCapabilities | BackendCapabilities.TensorInference, request.BackendId, request.Device);
            IInferenceSession? encoder = null;
            try
            {
                PromptableSegmentationArtifactContract encoderContract = bundle.Profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder);
                PromptableSegmentationArtifactContract decoderContract = bundle.Profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder);
                encoder = backendRegistry.CreateSession(bundle.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder), effectiveRequest, statefulOptions);
                ValidateMetadata(encoder.Metadata, encoderContract, bundle.Profile.ProfileId);
                _decoder = backendRegistry.CreateSession(bundle.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder), effectiveRequest, statefulOptions);
                ValidateMetadata(_decoder.Metadata, decoderContract, bundle.Profile.ProfileId);
                _encoder = encoder;
            }
            catch (Exception exception)
            {
                encoder?.Dispose();
                _disposeSource.Dispose();
                _idle.Dispose();
                if (exception is VisualException) throw;
                throw new VisualException(VisualErrorCodes.InferenceFailed, "Promptable segmentation backend sessions could not be created.", exception, bundle.Profile.ProfileId, modelId: bundle.Profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder).ModelId, technicalDetails: exception.ToString());
            }
        }

        /// <summary>Gets the immutable artifact bundle. / 获取不可变工件 Bundle。</summary>
        public PromptableSegmentationArtifactBundle Bundle { get; }
        /// <summary>Gets whether one exact image embedding is cached. / 获取是否缓存了一个精确图像 Embedding。</summary>
        public bool HasImage
        {
            get { lock (_lifetimeGate) { EnsureUsableLocked(); return _state != null; } }
        }
        /// <summary>Gets the current identity or null before set-image/after clear. / 获取当前 Identity；set-image 前或 clear 后为 null。</summary>
        public PromptableImageIdentity? CurrentImage
        {
            get { lock (_lifetimeGate) { EnsureUsableLocked(); return _state?.PublicEmbedding.Identity; } }
        }

        /// <summary>Runs the encoder once and atomically replaces the cached embedding only after success. / 仅在 Encoder 成功后原子替换缓存 Embedding，并保证一次图像解码。</summary>
        public PromptableImageEmbedding SetImage(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SetImageCoreAsync(input, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Asynchronously runs the encoder or its backend-documented fallback and atomically replaces state after success. / 异步运行 Encoder 或 Backend 已记录回退，并在成功后原子替换状态。</summary>
        public Task<PromptableImageEmbedding> SetImageAsync(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SetImageCoreAsync(input, options ?? VisualExecutionOptions.Default, true, cancellationToken);
        }

        /// <summary>Runs one prompt decode against the current embedding and returns fully owned source masks, RLE, quality, and feedback logits. / 针对当前 Embedding 运行一次提示解码，并返回完全自有的源图掩码、RLE、质量与反馈 Logit。</summary>
        public PromptableSegmentationResult Predict(PromptableSegmentationPrompt prompt, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return PredictCoreAsync(prompt, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Asynchronously runs one prompt decode; cancellation never installs partial image state or returns partial masks. / 异步运行一次提示解码；取消不会安装部分图像状态或返回部分掩码。</summary>
        public Task<PromptableSegmentationResult> PredictAsync(PromptableSegmentationPrompt prompt, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return PredictCoreAsync(prompt, options ?? VisualExecutionOptions.Default, true, cancellationToken);
        }

        /// <summary>Clears the cached embedding; later predictions fail until another successful set-image. / 清除缓存 Embedding；之后的预测在另一次成功 set-image 前失败。</summary>
        public void ClearImage()
        {
            EnterOperation();
            try { lock (_lifetimeGate) { EnsureUsableLocked(); _state = null; } }
            finally { ExitOperation(); }
        }

        /// <inheritdoc />
        /// <remarks>Cancels an active operation, waits for it to unwind, clears embedding state, and disposes both owned sessions exactly once. / 取消活动操作并等待退出，清除 Embedding 状态，再仅一次释放两条自有 Session。</remarks>
        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }
            _idle.Wait();
            try
            {
                _state = null;
                _decoder.Dispose();
                _encoder.Dispose();
            }
            finally
            {
                _disposeSource.Dispose();
                _idle.Dispose();
            }
        }

        private async Task<PromptableImageEmbedding> SetImageCoreAsync(PreparedVisualInput input, VisualExecutionOptions options, bool asynchronous, CancellationToken callerToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CancellationToken disposeToken = EnterOperation();
            using (var timeout = options.Timeout.HasValue ? new CancellationTokenSource(options.Timeout.Value) : new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeout.Token, disposeToken))
            {
                try
                {
                    ValidatePreparedImage(input);
                    SamV1TensorMap map = Bundle.Profile.SamV1TensorMap!;
                    var watch = Stopwatch.StartNew();
                    InferenceOutputs outputs = asynchronous
                        ? await _encoder.RunAsync(InferenceInputs.Create(map.ImageInput, input.Tensor), linked.Token).ConfigureAwait(false)
                        : _encoder.Run(InferenceInputs.Create(map.ImageInput, input.Tensor), linked.Token);
                    watch.Stop();
                    PromptableSegmentationArtifactContract encoderContract = Bundle.Profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder);
                    ValidateOutputs(outputs, encoderContract, Bundle.Profile.ProfileId);
                    var tensors = new Dictionary<string, Tensor<float>>(StringComparer.Ordinal);
                    var summaries = new List<PromptableImageEmbeddingSummary>();
                    foreach (PromptableTensorContract output in encoderContract.Outputs)
                    {
                        Tensor<float> owned = CopyFloatTensor(outputs.GetRequired(output.Name), output.Name, encoderContract.MaximumTensorElements);
                        tensors.Add(output.Name, owned);
                        summaries.Add(Summarize(output.Name, owned));
                    }
                    string contentSha = input.InputId ?? throw new VisualException(VisualErrorCodes.PromptableSegmentationIdentityMismatch, "Set-image requires the exact encoded-image SHA256 in PreparedVisualInput.InputId.", profileId: Bundle.Profile.ProfileId);
                    var identity = new PromptableImageIdentity(Bundle.Profile.ProfileId, Bundle.Profile.ArtifactIdentity, contentSha, input.SourceSize, input.ModelSize);
                    ImageTransform transform = CloneTransform(input.Transform);
                    var publicEmbedding = new PromptableImageEmbedding(identity, summaries, watch.Elapsed);
                    var state = new EmbeddingState(identity, tensors, transform, publicEmbedding);
                    lock (_lifetimeGate) { EnsureUsableLocked(); _state = state; }
                    return publicEmbedding;
                }
                catch (OperationCanceledException exception) { throw MapCancellation(exception, callerToken); }
                catch (DeploySharpException exception) when (linked.IsCancellationRequested) { throw MapCancellation(exception, callerToken); }
                catch (VisualException) { throw; }
                catch (Exception exception) { throw Failure("Image encoding failed.", exception); }
                finally
                {
                    if (options.DisposeOwnedInputOnCompletion && input.Ownership == PreparedInputOwnership.Owned) input.Dispose();
                    ExitOperation();
                }
            }
        }

        private async Task<PromptableSegmentationResult> PredictCoreAsync(PromptableSegmentationPrompt prompt, VisualExecutionOptions options, bool asynchronous, CancellationToken callerToken)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            CancellationToken disposeToken = EnterOperation();
            using (var timeout = options.Timeout.HasValue ? new CancellationTokenSource(options.Timeout.Value) : new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeout.Token, disposeToken))
            {
                try
                {
                    EmbeddingState state;
                    lock (_lifetimeGate)
                    {
                        EnsureUsableLocked();
                        state = _state ?? throw new VisualException(VisualErrorCodes.PromptableSegmentationStateInvalid, "Set-image must succeed before prompt decoding.", profileId: Bundle.Profile.ProfileId);
                    }
                    var prepareWatch = Stopwatch.StartNew();
                    InferenceInputs decoderInputs = CreateDecoderInputs(prompt, state);
                    prepareWatch.Stop();
                    var decodeWatch = Stopwatch.StartNew();
                    InferenceOutputs outputs = asynchronous
                        ? await _decoder.RunAsync(decoderInputs, linked.Token).ConfigureAwait(false)
                        : _decoder.Run(decoderInputs, linked.Token);
                    decodeWatch.Stop();
                    PromptableSegmentationArtifactContract decoderContract = Bundle.Profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder);
                    ValidateOutputs(outputs, decoderContract, Bundle.Profile.ProfileId);
                    var restoreWatch = Stopwatch.StartNew();
                    PromptableSegmentationResult result = Decode(outputs, prompt, state, prepareWatch.Elapsed, decodeWatch.Elapsed, linked.Token);
                    restoreWatch.Stop();
                    return WithRestoreTiming(result, restoreWatch.Elapsed);
                }
                catch (OperationCanceledException exception) { throw MapCancellation(exception, callerToken); }
                catch (DeploySharpException exception) when (linked.IsCancellationRequested) { throw MapCancellation(exception, callerToken); }
                catch (VisualException) { throw; }
                catch (Exception exception) { throw Failure("Prompt decoding failed.", exception); }
                finally { ExitOperation(); }
            }
        }

        private InferenceInputs CreateDecoderInputs(PromptableSegmentationPrompt prompt, EmbeddingState state)
        {
            PromptableSegmentationProfile profile = Bundle.Profile;
            SamV1TensorMap map = profile.SamV1TensorMap!;
            int coordinateCount = checked(prompt.Points.Count + (prompt.Box.HasValue ? 2 : 0));
            if (coordinateCount > profile.MaximumPromptPoints) throw new VisualException(VisualErrorCodes.PromptableSegmentationLimitExceeded, "The prompt point/box-corner capacity was exceeded.", profileId: profile.ProfileId, technicalDetails: "count=" + coordinateCount + ";limit=" + profile.MaximumPromptPoints);
            ValidatePromptBounds(prompt, state.Identity.SourceSize);
            if (prompt.MaskFeedback != null)
            {
                if (!prompt.MaskFeedback.ImageIdentity.Equals(state.Identity)) throw new VisualException(VisualErrorCodes.PromptableSegmentationIdentityMismatch, "Mask feedback belongs to another image, profile, or artifact bundle.", profileId: profile.ProfileId);
                if (prompt.MaskFeedback.Logits.Width != profile.LowResolutionMaskSize || prompt.MaskFeedback.Logits.Height != profile.LowResolutionMaskSize) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "Mask feedback dimensions do not match the profile.", profileId: profile.ProfileId);
            }

            int physicalCount = Math.Max(1, coordinateCount);
            var coordinates = new float[checked(physicalCount * 2)];
            var labels = new float[physicalCount];
            int offset = 0;
            foreach (PromptPoint point in prompt.Points)
            {
                PointF mapped = state.Transform.ToModel(new PointF(point.X, point.Y));
                coordinates[offset * 2] = mapped.X;
                coordinates[(offset * 2) + 1] = mapped.Y;
                labels[offset] = (float)point.Label;
                offset++;
            }
            if (prompt.Box.HasValue)
            {
                RectangleF mapped = state.Transform.ToModel(prompt.Box.Value);
                coordinates[offset * 2] = mapped.X;
                coordinates[(offset * 2) + 1] = mapped.Y;
                labels[offset] = 2f;
                offset++;
                coordinates[offset * 2] = mapped.Right;
                coordinates[(offset * 2) + 1] = mapped.Bottom;
                labels[offset] = 3f;
                offset++;
            }
            if (coordinateCount == 0) labels[0] = -1f;

            int low = profile.LowResolutionMaskSize;
            float[] feedback = prompt.MaskFeedback == null ? new float[checked(low * low)] : prompt.MaskFeedback.Logits.CopyValues();
            var inputs = new List<NamedTensor>
            {
                new NamedTensor(map.ImageEmbedding, state.Embeddings[map.ImageEmbedding]),
                new NamedTensor(map.PointCoordinates, new Tensor<float>(new TensorShape(1, physicalCount, 2), coordinates, TensorBufferOwnership.Transfer)),
                new NamedTensor(map.PointLabels, new Tensor<float>(new TensorShape(1, physicalCount), labels, TensorBufferOwnership.Transfer)),
                new NamedTensor(map.MaskInput, new Tensor<float>(new TensorShape(1, 1, low, low), feedback, TensorBufferOwnership.Transfer)),
                new NamedTensor(map.HasMaskInput, new Tensor<float>(new TensorShape(1), new[] { prompt.MaskFeedback == null ? 0f : 1f }, TensorBufferOwnership.Transfer)),
                new NamedTensor(map.OriginalImageSize, new Tensor<float>(new TensorShape(2), new[] { (float)state.Identity.SourceSize.Height, (float)state.Identity.SourceSize.Width }, TensorBufferOwnership.Transfer))
            };
            return new InferenceInputs(inputs);
        }

        private PromptableSegmentationResult Decode(InferenceOutputs outputs, PromptableSegmentationPrompt prompt, EmbeddingState state, TimeSpan prepareTime, TimeSpan decodeTime, CancellationToken token)
        {
            PromptableSegmentationProfile profile = Bundle.Profile;
            SamV1TensorMap map = profile.SamV1TensorMap!;
            Tensor<float> masks = RequireFloatTensor(outputs.GetRequired(map.Masks), map.Masks);
            Tensor<float> qualities = RequireFloatTensor(outputs.GetRequired(map.Quality), map.Quality);
            Tensor<float> lowResolution = RequireFloatTensor(outputs.GetRequired(map.LowResolutionMasks), map.LowResolutionMasks);
            if (masks.Shape.Rank != 4 || masks.Shape[0] != 1 || masks.Shape[2] != state.Identity.SourceSize.Height || masks.Shape[3] != state.Identity.SourceSize.Width) throw TensorError(map.Masks, "SAM masks must be [1,M,source_height,source_width].", masks.Shape);
            int count = checked((int)masks.Shape[1]);
            if (count <= 0 || count > profile.MaximumCandidates) throw new VisualException(VisualErrorCodes.PromptableSegmentationLimitExceeded, "The decoder candidate count is outside profile capacity.", profileId: profile.ProfileId, tensorName: map.Masks, technicalDetails: "count=" + count);
            if ((qualities.Shape.Rank != 2 && qualities.Shape.Rank != 1) || qualities.Length != count) throw TensorError(map.Quality, "Quality output must contain one value per mask.", qualities.Shape);
            int low = profile.LowResolutionMaskSize;
            if (lowResolution.Shape.Rank != 4 || lowResolution.Shape[0] != 1 || lowResolution.Shape[1] != count || lowResolution.Shape[2] != low || lowResolution.Shape[3] != low) throw TensorError(map.LowResolutionMasks, "Low-resolution masks do not match the profile feedback contract.", lowResolution.Shape);
            long totalPixels = checked((long)count * state.Identity.SourceSize.Width * state.Identity.SourceSize.Height);
            if (totalPixels > profile.MaximumSourceMaskPixels) throw new VisualException(VisualErrorCodes.PromptableSegmentationLimitExceeded, "The source-mask pixel capacity was exceeded.", profileId: profile.ProfileId, technicalDetails: "pixels=" + totalPixels + ";limit=" + profile.MaximumSourceMaskPixels);

            float[] maskValues = (float[])masks.Buffer;
            float[] qualityValues = (float[])qualities.Buffer;
            float[] lowValues = (float[])lowResolution.Buffer;
            // SAM token zero is the single-mask candidate; tokens one through three are the official multimask candidates.
            // SAM 的零号 Token 是单掩码候选；一至三号 Token 是官方多掩码候选。
            IEnumerable<int> selected = prompt.ReturnMultipleMasks && count > 1 ? Enumerable.Range(1, count - 1) : new[] { 0 };
            var indices = selected.OrderByDescending(index => CanonicalScore(qualityValues[index])).ThenBy(index => index).ToList();
            var instances = new List<InstanceSegmentationInstance>();
            var candidates = new List<PromptableMaskCandidate>();
            int sourcePixels = checked(state.Identity.SourceSize.Width * state.Identity.SourceSize.Height);
            int lowPixels = checked(low * low);
            foreach (int sourceIndex in indices)
            {
                token.ThrowIfCancellationRequested();
                float rawQuality = qualityValues[sourceIndex];
                if (float.IsNaN(rawQuality) || float.IsInfinity(rawQuality)) throw TensorError(map.Quality, "Quality output contains NaN or Infinity.", qualities.Shape);
                var binary = new byte[sourcePixels];
                int maskOffset = checked(sourceIndex * sourcePixels);
                for (int index = 0; index < binary.Length; index++)
                {
                    float value = maskValues[maskOffset + index];
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw TensorError(map.Masks, "Mask output contains NaN or Infinity.", masks.Shape);
                    binary[index] = value > profile.MaskThreshold ? (byte)1 : (byte)0;
                }
                var ownedMask = new InstanceBinaryMask(state.Identity.SourceSize.Width, state.Identity.SourceSize.Height, binary, InstanceMaskCoordinateSpace.SourceImage);
                RectangleF? bounds = ownedMask.GetForegroundBounds();
                if (!bounds.HasValue) continue;
                var lowCopy = new float[lowPixels];
                Array.Copy(lowValues, checked(sourceIndex * lowPixels), lowCopy, 0, lowPixels);
                var logits = new PromptableMaskLogits(low, low, lowCopy, state.Identity);
                var metadata = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("qualityKind", profile.QualityKind.ToString()),
                    new KeyValuePair<string, string>("maskThreshold", profile.MaskThreshold.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                };
                if (prompt.PromptId != null) metadata.Add(new KeyValuePair<string, string>("promptId", prompt.PromptId));
                var instance = new InstanceSegmentationInstance(sourceIndex, 0, "prompt", CanonicalScore(rawQuality), bounds.Value, ownedMask, InstanceMaskRle.Encode(ownedMask), prompt.PromptId, metadata);
                instances.Add(instance);
                candidates.Add(new PromptableMaskCandidate(sourceIndex, rawQuality, profile.QualityKind, logits));
            }
            var canonical = new InstanceSegmentationResult(instances, state.Identity.SourceSize, profile.ProfileId, profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder).ModelId);
            var provenance = new PromptablePromptProvenance(prompt.Points.Count, prompt.Box.HasValue, prompt.MaskFeedback != null, prompt.ReturnMultipleMasks, prompt.PromptId);
            return new PromptableSegmentationResult(canonical, candidates, state.Identity, provenance, new PromptableSegmentationTiming(prepareTime, decodeTime, TimeSpan.Zero));
        }

        private static PromptableSegmentationResult WithRestoreTiming(PromptableSegmentationResult result, TimeSpan restore)
        {
            return new PromptableSegmentationResult(result.Segmentation, result.Candidates, result.ImageIdentity, result.Prompt, new PromptableSegmentationTiming(result.Timing.PromptPreparation, result.Timing.PromptDecode, restore));
        }

        private void ValidatePreparedImage(PreparedVisualInput input)
        {
            input.EnsureUsable();
            PromptableSegmentationProfile profile = Bundle.Profile;
            PromptableSegmentationArtifactContract encoder = profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder);
            SamV1TensorMap map = profile.SamV1TensorMap!;
            PromptableTensorContract port = encoder.RequireInput(map.ImageInput);
            if (!string.Equals(input.InputName, map.ImageInput, StringComparison.Ordinal) || input.Tensor.ElementType != port.ElementType || !Matches(port.ShapePattern, input.Tensor.Shape)) throw TensorError(input.InputName, "Prepared image input does not match the encoder contract.", input.Tensor.Shape);
            if (input.ModelSize != profile.ImageInputSize || input.BatchSize != 1 || input.Layout != VisualTensorLayout.Nchw || input.AuxiliaryInputs.Count != 0) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "SAM set-image requires one NCHW image and no auxiliary tensors.", profileId: profile.ProfileId, tensorName: input.InputName);
            if (input.Transform.SourceSize != input.SourceSize || input.Transform.ModelSize != input.ModelSize || input.Transform.OffsetX != 0f || input.Transform.OffsetY != 0f) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "SAM preprocessing requires longest-side resize with padding only on bottom and right.", profileId: profile.ProfileId, tensorName: input.InputName);
            if (input.Preprocessing.ColorOrder != VisualColorOrder.Rgb || input.Preprocessing.Means.Count != 3 || input.Preprocessing.Scales.Count != 3) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "SAM preprocessing requires exact RGB mean/standard-deviation metadata.", profileId: profile.ProfileId, tensorName: input.InputName);
            float[] expectedMeans = { 123.675f, 116.28f, 103.53f };
            float[] expectedScales = { 1f / 58.395f, 1f / 57.12f, 1f / 57.375f };
            for (int index = 0; index < 3; index++) if (Math.Abs(input.Preprocessing.Means[index] - expectedMeans[index]) > 0.0001f || Math.Abs(input.Preprocessing.Scales[index] - expectedScales[index]) > 0.000001f) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "SAM normalization metadata does not match the official contract.", profileId: profile.ProfileId, tensorName: input.InputName);
            PromptableSegmentationArtifactContract.NormalizeSha256(input.InputId ?? string.Empty, nameof(input.InputId));
        }

        private static void ValidatePromptBounds(PromptableSegmentationPrompt prompt, VisualSize size)
        {
            foreach (PromptPoint point in prompt.Points) if (point.X < 0 || point.Y < 0 || point.X >= size.Width || point.Y >= size.Height) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "A point prompt lies outside the source image.");
            if (prompt.Box.HasValue)
            {
                RectangleF box = prompt.Box.Value;
                if (box.X < 0 || box.Y < 0 || box.Right > size.Width || box.Bottom > size.Height) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "A box prompt lies outside the source image.");
            }
        }

        private static PromptableImageEmbeddingSummary Summarize(string name, Tensor<float> tensor)
        {
            float[] values = (float[])tensor.Buffer;
            if (values.Length == 0) throw TensorError(name, "An embedding tensor cannot be empty.", tensor.Shape);
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            double sum = 0;
            for (int index = 0; index < values.Length; index++)
            {
                float value = values[index];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw TensorError(name, "An embedding contains NaN or Infinity.", tensor.Shape);
                if (value < minimum) minimum = value;
                if (value > maximum) maximum = value;
                sum += value;
            }
            return new PromptableImageEmbeddingSummary(name, tensor.Shape, tensor.Length, minimum, maximum, sum / values.Length, PromptableSegmentationHash.Floats(values));
        }

        private static Tensor<float> CopyFloatTensor(ITensor tensor, string name, long limit)
        {
            Tensor<float> typed = RequireFloatTensor(tensor, name);
            if (typed.Length <= 0 || typed.Length > limit) throw new VisualException(VisualErrorCodes.PromptableSegmentationLimitExceeded, "An embedding tensor exceeds its artifact capacity.", tensorName: name, technicalDetails: "elements=" + typed.Length + ";limit=" + limit);
            return new Tensor<float>(new TensorShape(typed.Shape.ToArray()), (float[])typed.Buffer, TensorBufferOwnership.Copy);
        }

        private static Tensor<float> RequireFloatTensor(ITensor tensor, string name)
        {
            Tensor<float>? typed = tensor as Tensor<float>;
            if (typed == null) throw new VisualException(VisualErrorCodes.TensorInvalid, "A promptable-segmentation tensor must be Float32.", tensorName: name, technicalDetails: tensor.ElementType.ToString());
            return typed;
        }

        private static void ValidateMetadata(ModelMetadata metadata, PromptableSegmentationArtifactContract contract, string profileId)
        {
            if (metadata == null || metadata.ModelId != contract.ModelId || !string.Equals(metadata.Format, contract.Format, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.PromptableSegmentationIdentityMismatch, "Backend metadata does not match the artifact contract.", profileId: profileId, modelId: contract.ModelId);
            ValidateDescriptors(metadata.Inputs, contract.Inputs, profileId, contract.ModelId, "input");
            ValidateDescriptors(metadata.Outputs, contract.Outputs, profileId, contract.ModelId, "output");
        }

        private static void ValidateDescriptors(IReadOnlyList<TensorDescriptor> actual, IReadOnlyList<PromptableTensorContract> expected, string profileId, ModelId modelId, string kind)
        {
            if (actual.Count != expected.Count) throw new VisualException(VisualErrorCodes.TensorInvalid, "Backend " + kind + " count does not match the artifact contract.", profileId: profileId, modelId: modelId, technicalDetails: "actual=" + string.Join(";", actual.Select(value => value.Name + ":" + value.ElementType + ":" + value.Shape)) + ";expected=" + string.Join(";", expected.Select(value => value.Name + ":" + value.ElementType + ":" + value.ShapePattern)));
            foreach (PromptableTensorContract port in expected)
            {
                TensorDescriptor? descriptor = actual.FirstOrDefault(value => string.Equals(value.Name, port.Name, StringComparison.Ordinal));
                if (descriptor == null || descriptor.ElementType != port.ElementType || !Compatible(port.ShapePattern, descriptor.Shape)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Backend " + kind + " metadata does not match the exact named port contract.", profileId: profileId, tensorName: port.Name, modelId: modelId, technicalDetails: "actual=" + (descriptor == null ? "missing" : descriptor.Name + ":" + descriptor.ElementType + ":" + descriptor.Shape) + ";actualAll=" + string.Join(";", actual.Select(value => value.Name + ":" + value.ElementType + ":" + value.Shape)) + ";expected=" + port.Name + ":" + port.ElementType + ":" + port.ShapePattern);
            }
        }

        private static void ValidateOutputs(InferenceOutputs outputs, PromptableSegmentationArtifactContract contract, string profileId)
        {
            if (outputs == null || outputs.Count != contract.Outputs.Count) throw new VisualException(VisualErrorCodes.TensorInvalid, "Backend output count does not match the artifact contract.", profileId: profileId, modelId: contract.ModelId);
            foreach (PromptableTensorContract port in contract.Outputs)
            {
                ITensor tensor;
                try { tensor = outputs.GetRequired(port.Name); }
                catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "A required exact named output is missing.", exception, profileId, port.Name, modelId: contract.ModelId); }
                if (tensor.ElementType != port.ElementType || !Matches(port.ShapePattern, tensor.Shape)) throw new VisualException(VisualErrorCodes.TensorInvalid, "A backend output does not match its exact type/shape contract.", profileId: profileId, tensorName: port.Name, modelId: contract.ModelId, technicalDetails: tensor.Shape.ToString());
                if (tensor.Length > contract.MaximumTensorElements) throw new VisualException(VisualErrorCodes.PromptableSegmentationLimitExceeded, "A backend output exceeds artifact capacity.", profileId: profileId, tensorName: port.Name, modelId: contract.ModelId);
            }
        }

        private static bool Matches(TensorShape pattern, TensorShape actual)
        {
            if (pattern.Rank != actual.Rank) return false;
            for (int index = 0; index < pattern.Rank; index++) if (pattern[index] >= 0 && pattern[index] != actual[index]) return false;
            return true;
        }

        private static bool Compatible(TensorShape expected, TensorShape actual)
        {
            if (expected.Rank != actual.Rank) return false;
            for (int index = 0; index < expected.Rank; index++) if (expected[index] >= 0 && actual[index] >= 0 && expected[index] != actual[index]) return false;
            return true;
        }

        private CancellationToken EnterOperation()
        {
            lock (_lifetimeGate)
            {
                EnsureUsableLocked();
                if (Interlocked.CompareExchange(ref _operationActive, 1, 0) != 0) throw new VisualException(VisualErrorCodes.PromptableSegmentationConcurrentOperation, "Promptable image sessions reject concurrent set-image, predict, and clear operations.", profileId: Bundle.Profile.ProfileId);
                _idle.Reset();
                return _disposeSource.Token;
            }
        }

        private void ExitOperation()
        {
            Interlocked.Exchange(ref _operationActive, 0);
            _idle.Set();
        }

        private void EnsureUsableLocked()
        {
            if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The promptable image session has been disposed.", profileId: Bundle.Profile.ProfileId);
        }

        private VisualException MapCancellation(Exception exception, CancellationToken callerToken)
        {
            if (_disposed || _disposeSource.IsCancellationRequested) return new VisualException(VisualErrorCodes.ObjectDisposed, "The promptable image session was disposed during execution.", exception, Bundle.Profile.ProfileId, technicalDetails: exception.ToString());
            if (callerToken.IsCancellationRequested) return new VisualException(VisualErrorCodes.Cancelled, "Promptable segmentation was cancelled by the caller.", exception, Bundle.Profile.ProfileId, technicalDetails: exception.ToString());
            return new VisualException(VisualErrorCodes.Timeout, "Promptable segmentation exceeded its configured timeout.", exception, Bundle.Profile.ProfileId, technicalDetails: exception.ToString());
        }

        private VisualException Failure(string message, Exception exception) => new VisualException(VisualErrorCodes.InferenceFailed, message, exception, Bundle.Profile.ProfileId, modelId: Bundle.Profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder).ModelId, technicalDetails: exception.ToString());

        private static VisualException TensorError(string name, string message, TensorShape shape) => new VisualException(VisualErrorCodes.TensorInvalid, message, tensorName: name, technicalDetails: shape.ToString());
        private static float CanonicalScore(float value) => value < 0f ? 0f : value;
        private static ImageTransform CloneTransform(ImageTransform value) => new ImageTransform(value.Kind, value.SourceSize, value.ModelSize, value.ScaleX, value.ScaleY, value.OffsetX, value.OffsetY);

        private sealed class EmbeddingState
        {
            public EmbeddingState(PromptableImageIdentity identity, Dictionary<string, Tensor<float>> embeddings, ImageTransform transform, PromptableImageEmbedding publicEmbedding)
            {
                Identity = identity;
                Embeddings = embeddings;
                Transform = transform;
                PublicEmbedding = publicEmbedding;
            }

            public PromptableImageIdentity Identity { get; }
            public Dictionary<string, Tensor<float>> Embeddings { get; }
            public ImageTransform Transform { get; }
            public PromptableImageEmbedding PublicEmbedding { get; }
        }
    }

}
