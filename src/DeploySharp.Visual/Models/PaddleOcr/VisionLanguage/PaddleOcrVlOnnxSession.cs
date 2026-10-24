#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Holds single-image RGB normalized patches in raster [grid_h,grid_w,3,14,14] order. / 持有按行排列的单图 RGB 归一化 Patch，形状为 [grid_h,grid_w,3,14,14]。</summary>
    public sealed class PaddleOcrVlPreparedInput
    {
        /// <summary>Binds exact processor geometry to caller-prepared patches. / 将精确 Processor 几何绑定到调用方已准备的 Patch。</summary>
        /// <remarks>Pixel values must match the pinned Pillow processor: RGB, bicubic, rescale 1/255, mean/std 0.5; spotting has a preceding Lanczos step. The caller must not mutate the tensor during inference. / 像素须匹配固定 Pillow Processor：RGB、Bicubic、1/255、Mean/Std=0.5；Spotting 另有前置 Lanczos 步骤。推理期间调用方不得修改 Tensor。</remarks>
        public PaddleOcrVlPreparedInput(PaddleOcrVlImagePlan imagePlan, Tensor<float> pixelPatches, string? sourceSha256 = null)
            : this(imagePlan, pixelPatches, sourceSha256, null) { }

        /// <summary>Binds prepared patches to an optional exact source-to-page crop frame without copying the tensor. / 将 Patch 绑定到可选精确源到页面裁剪坐标系，不复制张量。</summary>
        public PaddleOcrVlPreparedInput(PaddleOcrVlImagePlan imagePlan, Tensor<float> pixelPatches, string? sourceSha256, PaddleOcrVlSourceFrame? sourceFrame)
        {
            ImagePlan = imagePlan ?? throw new ArgumentNullException(nameof(imagePlan));
            PixelPatches = pixelPatches ?? throw new ArgumentNullException(nameof(pixelPatches));
            if (!pixelPatches.Shape.Equals(new TensorShape(imagePlan.GridHeight, imagePlan.GridWidth, 3, 14, 14)))
                throw new ArgumentException("PaddleOCR-VL patches must use [grid_h,grid_w,3,14,14] raster layout.", nameof(pixelPatches));
            if (sourceSha256 != null && (sourceSha256.Length != 64 || sourceSha256.Any(value => !Uri.IsHexDigit(value))))
                throw new ArgumentException("A source identity must be a SHA256 hex string.", nameof(sourceSha256));
            if (sourceFrame != null && sourceFrame.SourceSize != imagePlan.SourceSize)
                throw new ArgumentException("The source frame must match the original processor source size.", nameof(sourceFrame));
            SourceSha256 = sourceSha256;
            SourceFrame = sourceFrame;
        }
        /// <summary>Gets source, prepared and final image geometry. / 获取源图、准备图与最终图像几何。</summary>
        public PaddleOcrVlImagePlan ImagePlan { get; }
        /// <summary>Gets the prepared normalized patches; no copy is made here. / 获取已归一化 Patch，本类型不复制数据。</summary>
        public Tensor<float> PixelPatches { get; }
        /// <summary>Gets an optional source SHA256: encoded image identity, or the adapter's dimension-bound RGB crop identity. / 获取可选源 SHA256：编码图像身份，或适配器按尺寸绑定的 RGB 裁剪身份。</summary>
        public string? SourceSha256 { get; }
        /// <summary>Gets exact source-to-page translation, or null for caller-prepared input without page mapping. / 获取精确源到页面平移；调用方未提供页面映射时为空。</summary>
        public PaddleOcrVlSourceFrame? SourceFrame { get; }
        /// <summary>Attaches an exact crop frame without copying or modifying pixel patches. / 附加精确裁剪坐标系，不复制或修改像素 Patch。</summary>
        public PaddleOcrVlPreparedInput WithSourceFrame(PaddleOcrVlSourceFrame sourceFrame)
            => new PaddleOcrVlPreparedInput(ImagePlan, PixelPatches, SourceSha256, sourceFrame ?? throw new ArgumentNullException(nameof(sourceFrame)));
    }

    /// <summary>Defines bounded greedy PaddleOCR-VL generation. / 定义有界 PaddleOCR-VL Greedy 生成。</summary>
    public sealed class PaddleOcrVlGenerationOptions
    {
        /// <summary>Creates context, output, cache and optional operation-time limits. / 创建上下文、输出、缓存及可选操作时间限制。</summary>
        /// <remarks>Cache budget includes old and new managed KV buffers together, not graph weights or native runtime workspaces. / 缓存预算计入同时存活的新旧托管 KV 缓冲区，不包括图权重和原生工作区。</remarks>
        public PaddleOcrVlGenerationOptions(int maximumNewTokens = 512, int maximumContextTokens = 8192,
            long maximumCacheBytes = 512L * 1024L * 1024L, TimeSpan? timeout = null)
        {
            if (maximumNewTokens <= 0 || maximumNewTokens > 4096) throw new ArgumentOutOfRangeException(nameof(maximumNewTokens));
            if (maximumContextTokens <= maximumNewTokens || maximumContextTokens > 131072) throw new ArgumentOutOfRangeException(nameof(maximumContextTokens));
            if (maximumCacheBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCacheBytes));
            if (timeout.HasValue && (timeout.Value <= TimeSpan.Zero || timeout.Value.TotalMilliseconds > int.MaxValue)) throw new ArgumentOutOfRangeException(nameof(timeout));
            MaximumNewTokens = maximumNewTokens; MaximumContextTokens = maximumContextTokens;
            MaximumCacheBytes = maximumCacheBytes; Timeout = timeout;
        }
        /// <summary>Gets the maximum output tokens, including EOS. / 获取包含 EOS 的最大输出 Token 数。</summary>
        public int MaximumNewTokens { get; }
        /// <summary>Gets the complete prompt plus generation context budget. / 获取完整 Prompt 加生成 Token 的上下文预算。</summary>
        public int MaximumContextTokens { get; }
        /// <summary>Gets the conservative old-plus-new managed KV budget. / 获取保守的新旧托管 KV 总预算。</summary>
        public long MaximumCacheBytes { get; }
        /// <summary>Gets the optional timeout, including time spent waiting for the session. / 获取包含排队等待时间的可选超时。</summary>
        public TimeSpan? Timeout { get; }
    }

    /// <summary>Contains raw generated text/tokens and explicit termination, without inventing HTML or spotting coordinates. / 保存原始生成文本、Token 和明确终止原因，不虚构 HTML 或定位坐标。</summary>
    public sealed class PaddleOcrVlGenerationResult
    {
        /// <summary>Creates a caller-owned generation result for an adapter around another PaddleOCR-VL runtime. / 为其它 PaddleOCR-VL 运行时适配器创建调用方拥有的生成结果。</summary>
        /// <remarks>This constructor does not parse task text or invent spatial output. Use the document adapter only when the caller can supply the exact task, token sequence and finish reason. / 此构造函数不解析任务文本，也不虚构空间输出；只有调用方能够提供精确任务、Token 序列和结束原因时才应使用文档适配器。</remarks>
        public PaddleOcrVlGenerationResult(PaddleOcrVlTask task, string text, IEnumerable<int> tokens,
            GenerationFinishReason reason, int promptLength, int ropeDelta, string? sourceSha256, TimeSpan elapsed)
        {
            if (!Enum.IsDefined(typeof(PaddleOcrVlTask), task)) throw new ArgumentOutOfRangeException(nameof(task));
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            if (!Enum.IsDefined(typeof(GenerationFinishReason), reason)) throw new ArgumentOutOfRangeException(nameof(reason));
            if (promptLength < 0) throw new ArgumentOutOfRangeException(nameof(promptLength));
            // Spatial RoPE compresses image-token spans, so the request-local delta can be negative.
            if (sourceSha256 != null && (sourceSha256.Length != 64 || sourceSha256.Any(value => !Uri.IsHexDigit(value))))
                throw new ArgumentException("A source identity must be a SHA256 hex string.", nameof(sourceSha256));
            if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
            var copied = tokens.ToList();
            if (copied.Any(value => value < 0)) throw new ArgumentException("Generated token IDs cannot be negative.", nameof(tokens));
            Task = task; Text = text; TokenIds = copied.AsReadOnly(); FinishReason = reason;
            PromptTokenCount = promptLength; RopeDelta = ropeDelta; SourceSha256 = sourceSha256; Elapsed = elapsed;
        }

        internal PaddleOcrVlGenerationResult(PaddleOcrVlTask task, string text, List<int> tokens,
            GenerationFinishReason reason, int promptLength, int ropeDelta, string? sourceSha256, TimeSpan elapsed)
            : this(task, text, (IEnumerable<int>)tokens, reason, promptLength, ropeDelta, sourceSha256, elapsed) { }
        /// <summary>Gets the official prompt task. / 获取官方 Prompt 任务。</summary>
        public PaddleOcrVlTask Task { get; }
        /// <summary>Gets completion text, preserving whitespace and structural markers. / 获取保留空白和结构标记的生成文本。</summary>
        public string Text { get; }
        /// <summary>Gets the generated IDs, including EOS when emitted. / 获取生成 ID，输出 EOS 时保留该 ID。</summary>
        public IReadOnlyList<int> TokenIds { get; }
        /// <summary>Gets EOS or output-budget termination. / 获取 EOS 或输出预算终止原因。</summary>
        public GenerationFinishReason FinishReason { get; }
        /// <summary>Gets expanded image-prompt length. / 获取展开后的图像 Prompt 长度。</summary>
        public int PromptTokenCount { get; }
        /// <summary>Gets the request-local 3D RoPE delta, which can be negative. / 获取可为负值的请求独立三维 RoPE 偏移。</summary>
        public int RopeDelta { get; }
        /// <summary>Gets optional input image identity. / 获取可选输入图像标识。</summary>
        public string? SourceSha256 { get; }
        /// <summary>Gets this execution's elapsed time, excluding session queue and caller preprocessing. / 获取本次执行耗时，不含 Session 排队和调用方预处理。</summary>
        public TimeSpan Elapsed { get; }
    }

    /// <summary>Runs experimental PaddleOCR-VL-1.6 four-graph greedy generation from exact prepared patches. / 从精确已准备 Patch 执行实验性 PaddleOCR-VL-1.6 四图 Greedy 生成。</summary>
    /// <remarks>One unpadded image per request. Concurrent callers queue; each generation owns its positions and KV. The dynamic decoder must support zero-length KV on prefill. Cross-backend and complete image-processor validation remain separate requirements. / 每请求为一张无填充图像，并发调用排队；每次生成独立持有位置和 KV。动态 Decoder 必须支持预填充零长度 KV。跨后端与完整图像 Processor 验证须单独进行。</remarks>
    public sealed class PaddleOcrVlOnnxSession : IDisposable
    {
        private readonly IInferenceSession _vision, _embedding, _decoder, _head;
        private readonly bool _ownsSessions;
        private readonly object _lifecycle = new object();
        private readonly SemaphoreSlim _execution = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _dispose = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private bool _disposed;
        private int _operations;

        /// <summary>Binds and validates four distinct backend sessions; ownership transfers only after successful validation. / 绑定并验证四条独立后端 Session，仅在验证成功后转移所有权。</summary>
        public PaddleOcrVlOnnxSession(IInferenceSession vision, IInferenceSession embedding, IInferenceSession decoder,
            IInferenceSession languageHead, bool ownsSessions = false)
        {
            _vision = vision ?? throw new ArgumentNullException(nameof(vision));
            _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _head = languageHead ?? throw new ArgumentNullException(nameof(languageHead));
            var sessions = new[] { _vision, _embedding, _decoder, _head };
            if (sessions.Distinct().Count() != 4) throw new ArgumentException("Each PaddleOCR-VL graph requires its own session.");
            ValidatePorts(_vision.Metadata, new[] { Port("pixel_patches", TensorElementType.Float32, -1, -1, 3, 14, 14) },
                new[] { Port("image_features", TensorElementType.Float32, -1, 1024) });
            ValidatePorts(_embedding.Metadata, new[] { Port("input_ids", TensorElementType.Int64, 1, -1) },
                new[] { Port("inputs_embeds", TensorElementType.Float32, 1, -1, 1024) });
            var decoderInputs = new List<TensorDescriptor> { Port("inputs_embeds", TensorElementType.Float32, 1, -1, 1024), Port("position_ids", TensorElementType.Int64, 3, 1, -1) };
            var decoderOutputs = new List<TensorDescriptor> { Port("last_hidden_state", TensorElementType.Float32, 1, 1, 1024) };
            for (int layer = 0; layer < 18; layer++)
                foreach (string kind in new[] { "key", "value" })
                {
                    decoderInputs.Add(Port("past_" + kind + "_" + layer, TensorElementType.Float32, 1, 2, -1, 128));
                    decoderOutputs.Add(Port("present_" + kind + "_" + layer, TensorElementType.Float32, 1, 2, -1, 128));
                }
            ValidatePorts(_decoder.Metadata, decoderInputs, decoderOutputs);
            ValidatePorts(_head.Metadata, new[] { Port("last_hidden_state", TensorElementType.Float32, 1, 1, 1024) },
                new[] { Port("logits", TensorElementType.Float32, 1, 1, 103424) });
            _ownsSessions = ownsSessions;
        }

        /// <summary>Creates sessions for vision.onnx, embedding.onnx, decoder.onnx and head.onnx with explicit graph hashes. / 为四个具名 ONNX 图创建 Session，显式计算图 Hash。</summary>
        /// <remarks>Use only graphs produced and checked together by the pinned exporter. This factory does not promote failed component parity or verify the checkpoint provenance of arbitrary user graphs. / 仅使用固定导出器同时产出并验证的图。本工厂不会将失败的组件对照改为通过，也不验证任意用户图的 Checkpoint 来源。</remarks>
        public static PaddleOcrVlOnnxSession Create(BackendRegistry registry, string bundleDirectory, BackendRequest request, SessionOptions? options = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (string.IsNullOrWhiteSpace(bundleDirectory)) throw new ArgumentException("An exported bundle directory is required.", nameof(bundleDirectory));
            if (request == null) throw new ArgumentNullException(nameof(request));
            var created = new List<IInferenceSession>();
            try
            {
                foreach (string role in new[] { "vision", "embedding", "decoder", "head" })
                {
                    string path = Path.GetFullPath(Path.Combine(bundleDirectory, role + ".onnx"));
                    string hash;
                    using (FileStream stream = File.OpenRead(path)) hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                    var artifact = new ModelArtifact(new ModelId("paddleocr-vl/1.6/" + role), "onnx", path, hash, request.BackendId);
                    created.Add(registry.CreateSession(artifact, request, options ?? SessionOptions.Default));
                }
                return new PaddleOcrVlOnnxSession(created[0], created[1], created[2], created[3], true);
            }
            catch { foreach (IInferenceSession session in created) { try { session.Dispose(); } catch { } } throw; }
        }

        /// <summary>Loads a local Bundle only after validating its manifest, all four graph files, and all tokenizer sidecars. / 仅在校验 manifest、四张图和全部 tokenizer sidecar 后加载本地 Bundle。</summary>
        /// <remarks>This is the preferred factory for a downloadable or shared PaddleOCR-VL Bundle. It fails before creating any backend Session when one file is missing, has the wrong size/hash, or belongs to another checkpoint. / 这是可下载或共享 PaddleOCR-VL Bundle 的推荐工厂；任一文件缺失、大小/哈希错误或属于其它 checkpoint 时，会在创建后端 Session 前失败。</remarks>
        public static PaddleOcrVlOnnxSession CreateVerified(BackendRegistry registry, string bundleDirectory, BackendRequest request, SessionOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            PaddleOcrVlBundleManifest manifest = PaddleOcrVlBundleManifest.LoadFromDirectory(bundleDirectory);
            return CreateVerified(registry, manifest, bundleDirectory, request, options, cancellationToken);
        }

        /// <summary>Loads a caller-supplied, already parsed manifest and verifies its complete Bundle before Session creation. / 加载调用方解析的 manifest，并在创建 Session 前校验完整 Bundle。</summary>
        public static PaddleOcrVlOnnxSession CreateVerified(BackendRegistry registry, PaddleOcrVlBundleManifest manifest, string bundleDirectory,
            BackendRequest request, SessionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (string.IsNullOrWhiteSpace(bundleDirectory)) throw new ArgumentException("An exported Bundle directory is required.", nameof(bundleDirectory));
            if (request == null) throw new ArgumentNullException(nameof(request));
            IReadOnlyDictionary<string, string> paths = manifest.VerifyFiles(bundleDirectory, cancellationToken);
            var created = new List<IInferenceSession>();
            try
            {
                foreach (string role in new[] { "vision", "embedding", "decoder", "head" })
                {
                    PaddleOcrVlBundleArtifact descriptor = manifest.GetRequiredArtifact(role);
                    string path = paths[role];
                    var artifact = new ModelArtifact(new ModelId(manifest.BundleId + "/" + role), descriptor.Format, path, descriptor.Sha256, request.BackendId);
                    created.Add(registry.CreateSession(artifact, request, options ?? SessionOptions.Default));
                }
                return new PaddleOcrVlOnnxSession(created[0], created[1], created[2], created[3], true);
            }
            catch { foreach (IInferenceSession session in created) { try { session.Dispose(); } catch { } } throw; }
        }

        /// <summary>Runs bounded greedy generation synchronously. / 同步执行有界 Greedy 生成。</summary>
        public PaddleOcrVlGenerationResult Generate(PaddleOcrVlPreparedInput input, PaddleOcrVlTokenizer tokenizer,
            PaddleOcrVlGenerationOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
            => GenerateCoreAsync(input, tokenizer, options ?? new PaddleOcrVlGenerationOptions(), false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Queues and runs bounded greedy generation asynchronously. / 排队并异步执行有界 Greedy 生成。</summary>
        public Task<PaddleOcrVlGenerationResult> GenerateAsync(PaddleOcrVlPreparedInput input, PaddleOcrVlTokenizer tokenizer,
            PaddleOcrVlGenerationOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
            => GenerateCoreAsync(input, tokenizer, options ?? new PaddleOcrVlGenerationOptions(), true, cancellationToken);

        /// <summary>Cancels active/queued calls, waits for cleanup, then releases owned sessions. / 取消活动及排队调用，等待清理后释放自有 Session。</summary>
        public void Dispose()
        {
            lock (_lifecycle) { if (_disposed) return; _disposed = true; }
            _dispose.Cancel();
            _idle.Wait();
            try
            {
                if (_ownsSessions)
                    foreach (IInferenceSession session in new[] { _head, _decoder, _embedding, _vision })
                        try { session.Dispose(); } catch { /* Dispose all remaining owned sessions. */ }
            }
            finally { _execution.Dispose(); _dispose.Dispose(); _idle.Dispose(); }
        }

        private async Task<PaddleOcrVlGenerationResult> GenerateCoreAsync(PaddleOcrVlPreparedInput input, PaddleOcrVlTokenizer tokenizer,
            PaddleOcrVlGenerationOptions options, bool asynchronous, CancellationToken caller)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            CancellationToken disposeToken;
            lock (_lifecycle)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(PaddleOcrVlOnnxSession));
                disposeToken = _dispose.Token;
                _operations++; _idle.Reset();
            }
            bool acquired = false;
            try
            {
                using var timeout = options.Timeout.HasValue ? new CancellationTokenSource(options.Timeout.Value) : null;
                using var linked = timeout == null ? CancellationTokenSource.CreateLinkedTokenSource(caller, disposeToken)
                    : CancellationTokenSource.CreateLinkedTokenSource(caller, disposeToken, timeout.Token);
                CancellationToken token = linked.Token;
                if (asynchronous) await _execution.WaitAsync(token).ConfigureAwait(false); else _execution.Wait(token);
                acquired = true;
                token.ThrowIfCancellationRequested();
                PaddleOcrVlTokenSequence prompt = tokenizer.EncodePrompt(input.ImagePlan, options.MaximumNewTokens, options.MaximumContextTokens);
                long maximumPast = (long)prompt.Count + options.MaximumNewTokens - 1;
                const long pairBytesPerToken = 18L * 2 * 2 * 128 * sizeof(float);
                if (checked(maximumPast * pairBytesPerToken * 2) > options.MaximumCacheBytes)
                    throw new ArgumentOutOfRangeException(nameof(options), "The reserved generation exceeds the old-plus-new KV memory budget.");
                foreach (float pixel in (float[])input.PixelPatches.Buffer)
                    if (float.IsNaN(pixel) || float.IsInfinity(pixel) || pixel < -1.000001f || pixel > 1.000001f)
                        throw Invalid("PaddleOCR-VL patches must contain finite official [-1,1] normalization values.");
                PaddleOcrVlPositionPlan positionPlan = PaddleOcrVlPositionPlan.Create(prompt);
                var watch = Stopwatch.StartNew();
                InferenceOutputs vision = await Run(_vision, InferenceInputs.Create("pixel_patches", input.PixelPatches), asynchronous, token).ConfigureAwait(false);
                float[] features = Float(vision.GetRequired("image_features"), new TensorShape(input.ImagePlan.ImageTokenCount, 1024), "image_features");
                InferenceOutputs embedded = await Run(_embedding, InferenceInputs.Create("input_ids",
                    new Tensor<long>(new TensorShape(1, prompt.Count), prompt.CopyTokenIds(), TensorBufferOwnership.Transfer)), asynchronous, token).ConfigureAwait(false);
                float[] rawEmbeddings = Float(embedded.GetRequired("inputs_embeds"), new TensorShape(1, prompt.Count, 1024), "inputs_embeds");
                // Backend output buffers can be reused by an adapter: own the merged
                // prompt once, then overwrite the contiguous image span in one copy.
                float[] merged = (float[])rawEmbeddings.Clone();
                Array.Copy(features, 0, merged, checked(prompt.ImageTokenOffset * 1024), features.Length);
                var cache = new ITensor[36];
                for (int i = 0; i < cache.Length; i++) cache[i] = new Tensor<float>(new TensorShape(1, 2, 0, 128), Array.Empty<float>(), TensorBufferOwnership.Transfer);
                var completions = new List<int>(options.MaximumNewTokens);
                Tensor<float> currentEmbedding = new Tensor<float>(new TensorShape(1, prompt.Count, 1024), merged, TensorBufferOwnership.Transfer);
                long[] decodePositions = new long[3];
                var oneTokenIds = new long[1];
                GenerationFinishReason reason = GenerationFinishReason.MaxTokens;
                for (int step = 0; step < options.MaximumNewTokens; step++)
                {
                    token.ThrowIfCancellationRequested();
                    if (step > 0)
                    {
                        oneTokenIds[0] = completions[step - 1];
                        InferenceOutputs next = await Run(_embedding, InferenceInputs.Create("input_ids",
                            new Tensor<long>(new TensorShape(1, 1), oneTokenIds, TensorBufferOwnership.Borrow)), asynchronous, token).ConfigureAwait(false);
                        float[] value = Float(next.GetRequired("inputs_embeds"), new TensorShape(1, 1, 1024), "inputs_embeds");
                        currentEmbedding = new Tensor<float>(new TensorShape(1, 1, 1024), value, TensorBufferOwnership.Borrow);
                        positionPlan.WriteDecodePositionIds(prompt.Count + step - 1, 1, decodePositions);
                    }
                    var inputs = new List<NamedTensor>(38)
                    {
                        new NamedTensor("inputs_embeds", currentEmbedding),
                        new NamedTensor("position_ids", new Tensor<long>(new TensorShape(3, 1, step == 0 ? prompt.Count : 1),
                            step == 0 ? positionPlan.CopyPositionIds() : decodePositions, TensorBufferOwnership.Borrow))
                    };
                    for (int layer = 0; layer < 18; layer++)
                    {
                        inputs.Add(new NamedTensor("past_key_" + layer, cache[layer * 2]));
                        inputs.Add(new NamedTensor("past_value_" + layer, cache[layer * 2 + 1]));
                    }
                    InferenceOutputs decoded = await Run(_decoder, new InferenceInputs(inputs), asynchronous, token).ConfigureAwait(false);
                    for (int layer = 0; layer < 18; layer++)
                        foreach (int kind in new[] { 0, 1 })
                        {
                            ITensor tensor = decoded.GetRequired("present_" + (kind == 0 ? "key_" : "value_") + layer);
                            Float(tensor, new TensorShape(1, 2, prompt.Count + step, 128), "present KV");
                            cache[layer * 2 + kind] = tensor;
                        }
                    ITensor lastHidden = decoded.GetRequired("last_hidden_state");
                    Float(lastHidden, new TensorShape(1, 1, 1024), "last_hidden_state");
                    InferenceOutputs head = await Run(_head, InferenceInputs.Create("last_hidden_state", lastHidden), asynchronous, token).ConfigureAwait(false);
                    float[] logits = Float(head.GetRequired("logits"), new TensorShape(1, 1, 103424), "logits");
                    int best = 0;
                    for (int i = 0; i < logits.Length; i++)
                    {
                        if (float.IsNaN(logits[i]) || float.IsInfinity(logits[i])) throw Invalid("The language head produced non-finite logits.");
                        if (logits[i] > logits[best]) best = i; // Lowest ID wins an exact tie, like upstream argmax.
                    }
                    completions.Add(best);
                    if (best == tokenizer.EndOfSequenceTokenId) { reason = GenerationFinishReason.EndOfSequence; break; }
                }
                token.ThrowIfCancellationRequested();
                watch.Stop();
                return new PaddleOcrVlGenerationResult(input.ImagePlan.Task, tokenizer.DecodeCompletion(completions), completions,
                    reason, prompt.Count, positionPlan.RopeDelta, input.SourceSha256, watch.Elapsed);
            }
            finally
            {
                if (acquired) _execution.Release();
                lock (_lifecycle) { _operations--; if (_operations == 0) _idle.Set(); }
            }
        }

        private static Task<InferenceOutputs> Run(IInferenceSession session, InferenceInputs inputs, bool asynchronous, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return asynchronous ? session.RunAsync(inputs, token) : Task.FromResult(session.Run(inputs, token));
        }
        private static float[] Float(ITensor tensor, TensorShape shape, string name)
        {
            if (tensor.ElementType != TensorElementType.Float32 || !tensor.Shape.Equals(shape) || !(tensor.Buffer is float[] buffer))
                throw Invalid(name + " has an unexpected shape, dtype or buffer.");
            return buffer;
        }
        private static TensorDescriptor Port(string name, TensorElementType type, params long[] dimensions)
            => new TensorDescriptor(name, type, new TensorShape(dimensions));
        private static void ValidatePorts(ModelMetadata actual, IEnumerable<TensorDescriptor> inputs, IEnumerable<TensorDescriptor> outputs)
        {
            ValidatePortSet(actual.Inputs, inputs); ValidatePortSet(actual.Outputs, outputs);
        }
        private static void ValidatePortSet(IReadOnlyList<TensorDescriptor> actual, IEnumerable<TensorDescriptor> expected)
        {
            TensorDescriptor[] ports = expected.ToArray();
            if (actual.Count != ports.Length) throw Invalid("An ONNX component has an unexpected number of ports.");
            foreach (TensorDescriptor wanted in ports)
            {
                TensorDescriptor? found = actual.SingleOrDefault(value => value.Name == wanted.Name);
                if (found == null || found.ElementType != wanted.ElementType || found.Shape.Rank != wanted.Shape.Rank)
                    throw Invalid("An ONNX component has an invalid port: " + wanted.Name);
                for (int axis = 0; axis < found.Shape.Rank; axis++)
                    if (wanted.Shape[axis] < 0 ? found.Shape[axis] >= 0 : found.Shape[axis] >= 0 && wanted.Shape[axis] != found.Shape[axis])
                        throw Invalid("An ONNX component has an invalid static/dynamic axis: " + wanted.Name);
            }
        }
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, message, profileId: "paddleocr-vl-1.6.dynamic-kv");
    }
}
#endif
