#if NET8_0 || NET9_0 || NET10_0
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
    /// <summary>Creates the official PP-Chart2Table fixed-resize RGB bicubic preprocessing contract. / 创建官方 PP-Chart2Table 固定缩放、RGB、双三次预处理合同。</summary>
    public static class PaddleChart2TableOnnxPreprocessing
    {
        /// <summary>Returns Pillow-compatible bicubic resize to 1024x1024 and GOT RGB normalization. / 返回兼容 Pillow 的 1024x1024 双三次缩放与 GOT RGB 归一化。</summary>
        public static VisualPreprocessingOptions CreateOfficial() => new VisualPreprocessingOptions(
            new VisualSize(1024, 1024),
            VisualResizeMode.Resize,
            VisualColorOrder.Rgb,
            VisualNormalizationOptions.MeanStandardDeviation(
                new[] { .48145466f, .4578275f, .40821073f },
                new[] { .26862954f, .26130258f, .27577711f },
                new[] { 255f }),
            VisualTensorLayout.Nchw,
            1,
            interpolation: VisualInterpolationMode.PillowBicubic,
            contractId: "paddle-chart2table-got-rgb-bicubic-1024-v1");
    }

    /// <summary>Identifies one graph in the four-graph PP-Chart2Table ONNX bundle. / 标识 PP-Chart2Table 四图 ONNX Bundle 中的一个图。</summary>
    public enum PaddleChart2TableArtifactRole
    {
        /// <summary>Vision tower plus multimodal projector. / 视觉塔与多模态投影层。</summary>
        VisionProjector = 1,
        /// <summary>Qwen token embedding table. / Qwen Token Embedding 表。</summary>
        TokenEmbedding = 2,
        /// <summary>Fixed 286-token prompt prefill with logits and KV output. / 固定 286 Token Prompt Prefill，输出 Logits 与 KV。</summary>
        TextPrefill = 3,
        /// <summary>One-token decoder with dynamic past KV inputs. / 带动态 Past KV 输入的单 Token Decoder。</summary>
        TextDecodeWithPast = 4
    }

    /// <summary>Binds four ONNX files from the same Chart2Table export and hashes their contents. / 绑定同一 Chart2Table 导出中的四个 ONNX 文件并计算内容 Hash。</summary>
    public sealed class PaddleChart2TableOnnxBundle
    {
        private readonly IReadOnlyDictionary<PaddleChart2TableArtifactRole, ModelArtifact> _artifacts;

        /// <summary>Creates a bundle from the vision, embedding, prefill, and dynamic decode graph paths. / 使用 Vision、Embedding、Prefill 与动态 Decode 图路径创建 Bundle。</summary>
        public PaddleChart2TableOnnxBundle(string visionProjectorPath, string tokenEmbeddingPath, string textPrefillPath, string textDecodeWithPastPath, BackendId? preferredBackend = null)
        {
            var values = new Dictionary<PaddleChart2TableArtifactRole, ModelArtifact>();
            Add(values, PaddleChart2TableArtifactRole.VisionProjector, "paddle-chart/pp-chart2table/vision-projector", visionProjectorPath, preferredBackend);
            Add(values, PaddleChart2TableArtifactRole.TokenEmbedding, "paddle-chart/pp-chart2table/token-embedding", tokenEmbeddingPath, preferredBackend);
            Add(values, PaddleChart2TableArtifactRole.TextPrefill, "paddle-chart/pp-chart2table/text-prefill", textPrefillPath, preferredBackend);
            Add(values, PaddleChart2TableArtifactRole.TextDecodeWithPast, "paddle-chart/pp-chart2table/text-decode-with-past", textDecodeWithPastPath, preferredBackend);
            _artifacts = values;
        }

        /// <summary>Creates the same graph bundle from already-described ONNX or TensorRT engine artifacts. / 使用已有描述的 ONNX 或 TensorRT Engine 工件创建图 Bundle。</summary>
        public PaddleChart2TableOnnxBundle(ModelArtifact visionProjector, ModelArtifact tokenEmbedding, ModelArtifact textPrefill, ModelArtifact textDecodeWithPast)
        {
            var values = new Dictionary<PaddleChart2TableArtifactRole, ModelArtifact>();
            Add(values, PaddleChart2TableArtifactRole.VisionProjector, "paddle-chart/pp-chart2table/vision-projector", visionProjector);
            Add(values, PaddleChart2TableArtifactRole.TokenEmbedding, "paddle-chart/pp-chart2table/token-embedding", tokenEmbedding);
            Add(values, PaddleChart2TableArtifactRole.TextPrefill, "paddle-chart/pp-chart2table/text-prefill", textPrefill);
            Add(values, PaddleChart2TableArtifactRole.TextDecodeWithPast, "paddle-chart/pp-chart2table/text-decode-with-past", textDecodeWithPast);
            string format = values[PaddleChart2TableArtifactRole.VisionProjector].Format;
            if (values.Values.Any(value => !string.Equals(value.Format, format, StringComparison.Ordinal))) throw new ArgumentException("All four Chart2Table artifacts must use one backend format.");
            _artifacts = values;
        }

        /// <summary>Gets one graph artifact. / 获取一个图工件。</summary>
        public ModelArtifact GetArtifact(PaddleChart2TableArtifactRole role) => _artifacts.TryGetValue(role, out ModelArtifact? artifact) ? artifact : throw new ArgumentOutOfRangeException(nameof(role));

        private static void Add(IDictionary<PaddleChart2TableArtifactRole, ModelArtifact> values, PaddleChart2TableArtifactRole role, string modelId, string path, BackendId? backend)
        {
            if (!Enum.IsDefined(typeof(PaddleChart2TableArtifactRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("All four ONNX graph paths are required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("A PP-Chart2Table ONNX graph is missing.", fullPath);
            string hash;
            using (var stream = File.OpenRead(fullPath))
            using (SHA256 sha = SHA256.Create()) hash = string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
            values.Add(role, new ModelArtifact(new ModelId(modelId), "onnx", fullPath, hash, backend));
        }

        private static void Add(IDictionary<PaddleChart2TableArtifactRole, ModelArtifact> values, PaddleChart2TableArtifactRole role, string expectedModelId, ModelArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (artifact.ModelId.Value != expectedModelId || (artifact.Format != "onnx" && artifact.Format != "tensorrt-engine")) throw new ArgumentException("Chart2Table artifact role, model ID, or format is invalid.", nameof(artifact));
            values.Add(role, artifact);
        }
    }

    /// <summary>Reports text and stage timings from one Chart2Table greedy generation. / 报告一次 Chart2Table Greedy 生成的文本与分阶段耗时。</summary>
    public sealed class PaddleChart2TableGenerationResult
    {
        private readonly IReadOnlyList<int> _tokenIds;
        private readonly IReadOnlyList<TimeSpan> _decodeSteps;

        /// <summary>Creates a generation result from a specialized backend execution path. / 从专用后端执行路径创建生成结果。</summary>
        public PaddleChart2TableGenerationResult(string text, GenerationFinishReason finishReason, int promptTokenCount, IEnumerable<int> tokenIds, TimeSpan visionTime, TimeSpan embeddingTime, TimeSpan prefillTime, IEnumerable<TimeSpan> decodeSteps)
        {
            if (text == null || tokenIds == null || decodeSteps == null || promptTokenCount <= 0) throw new ArgumentNullException();
            Text = text;
            FinishReason = finishReason;
            PromptTokenCount = promptTokenCount;
            _tokenIds = tokenIds.ToList().AsReadOnly();
            _decodeSteps = decodeSteps.ToList().AsReadOnly();
            VisionTime = visionTime;
            EmbeddingTime = embeddingTime;
            PrefillTime = prefillTime;
        }

        /// <summary>Gets generated table text. / 获取生成的表格文本。</summary>
        public string Text { get; }
        /// <summary>Gets the terminal reason. / 获取生成结束原因。</summary>
        public GenerationFinishReason FinishReason { get; }
        /// <summary>Gets prompt token count. / 获取 Prompt Token 数。</summary>
        public int PromptTokenCount { get; }
        /// <summary>Gets owned completion token IDs, including a terminal token when emitted. / 获取自有 Completion Token ID；如有输出则包含终止 Token。</summary>
        public IReadOnlyList<int> TokenIds => _tokenIds;
        /// <summary>Gets vision/projector graph time. / 获取 Vision/Projector 图耗时。</summary>
        public TimeSpan VisionTime { get; }
        /// <summary>Gets all token embedding graph time. / 获取所有 Token Embedding 图耗时。</summary>
        public TimeSpan EmbeddingTime { get; }
        /// <summary>Gets first-token prefill graph time. / 获取首 Token Prefill 图耗时。</summary>
        public TimeSpan PrefillTime { get; }
        /// <summary>Gets each subsequent dynamic-KV decode duration. / 获取后续每步动态 KV Decode 耗时。</summary>
        public IReadOnlyList<TimeSpan> DecodeSteps => _decodeSteps;
    }

    /// <summary>Runs PP-Chart2Table through vision, token embedding, fixed prompt prefill, and dynamic KV decode ONNX graphs. / 使用 Vision、Token Embedding、固定 Prompt Prefill 与动态 KV Decode ONNX 图运行 PP-Chart2Table。</summary>
    /// <remarks>The caller prepares one RGB, bicubic-resized, normalized 1024x1024 pixel tensor and owns the tokenizer and registry. The session owns its four inference sessions. / 调用方准备一张 RGB、Bicubic 缩放并归一化到 1024x1024 的像素张量，并拥有 Tokenizer 与 Registry；本 Session 拥有四条推理 Session。</remarks>
    public sealed class PaddleChart2TableOnnxSession : IDisposable
    {
        private const int PromptLength = 286;
        private const int ImageTokenCount = 256;
        private const int Layers = 24;
        private const int Heads = 16;
        private const int HeadDimension = 64;
        private const int VocabularySize = 151860;
        private const int MaximumNewTokens = 2048;
        private const int NoRepeatNgramSize = 20;
        private readonly IInferenceSession _vision;
        private readonly IInferenceSession _embedding;
        private readonly IInferenceSession _prefill;
        private readonly IInferenceSession _decode;
        private readonly object _gate = new object();
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private bool _disposed;
        private int _active;

        /// <summary>Creates and validates all four named backend sessions. / 创建并验证四条具名 Backend Session。</summary>
        public PaddleChart2TableOnnxSession(BackendRegistry registry, PaddleChart2TableOnnxBundle bundle, BackendRequest request, SessionOptions? options = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            if (request == null) throw new ArgumentNullException(nameof(request));
            SessionOptions requested = options ?? SessionOptions.Default;
            var one = new SessionOptions(1, requested.EnableProfiling);
            var effective = new BackendRequest(request.RequiredCapabilities | BackendCapabilities.TensorInference, request.BackendId, request.Device);
            IInferenceSession? vision = null, embedding = null, prefill = null, decode = null;
            try
            {
                vision = registry.CreateSession(bundle.GetArtifact(PaddleChart2TableArtifactRole.VisionProjector), effective, one);
                embedding = registry.CreateSession(bundle.GetArtifact(PaddleChart2TableArtifactRole.TokenEmbedding), effective, one);
                prefill = registry.CreateSession(bundle.GetArtifact(PaddleChart2TableArtifactRole.TextPrefill), effective, one);
                decode = registry.CreateSession(bundle.GetArtifact(PaddleChart2TableArtifactRole.TextDecodeWithPast), effective, one);
                ValidateMetadata(vision.Metadata, VisionInputs(), VisionOutputs(), PaddleChart2TableArtifactRole.VisionProjector);
                ValidateMetadata(embedding.Metadata, EmbeddingInputs(), EmbeddingOutputs(), PaddleChart2TableArtifactRole.TokenEmbedding);
                ValidateMetadata(prefill.Metadata, PrefillInputs(), PrefillOutputs(), PaddleChart2TableArtifactRole.TextPrefill);
                ValidateMetadata(decode.Metadata, DecodeInputs(), DecodeOutputs(), PaddleChart2TableArtifactRole.TextDecodeWithPast);
                _vision = vision; _embedding = embedding; _prefill = prefill; _decode = decode;
            }
            catch
            {
                TryDispose(decode); TryDispose(prefill); TryDispose(embedding); TryDispose(vision);
                _disposeSource.Dispose(); _idle.Dispose();
                throw;
            }
        }

        /// <summary>Gets the immutable graph bundle. / 获取不可变图 Bundle。</summary>
        public PaddleChart2TableOnnxBundle Bundle { get; }

        /// <summary>Runs greedy chart-to-table generation synchronously. / 同步执行 Greedy 图表转表格生成。</summary>
        public PaddleChart2TableGenerationResult Generate(PreparedVisualInput input, PaddleChart2TableTokenizer tokenizer, int maximumNewTokens = 1024, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => GenerateCoreAsync(input, tokenizer, maximumNewTokens, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Runs greedy chart-to-table generation asynchronously. / 异步执行 Greedy 图表转表格生成。</summary>
        public Task<PaddleChart2TableGenerationResult> GenerateAsync(PreparedVisualInput input, PaddleChart2TableTokenizer tokenizer, int maximumNewTokens = 1024, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => GenerateCoreAsync(input, tokenizer, maximumNewTokens, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Cancels the current operation, waits for it to unwind, and disposes all four sessions. / 取消当前操作、等待退出并释放四条 Session。</summary>
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; _disposeSource.Cancel(); }
            _idle.Wait();
            try { _decode.Dispose(); _prefill.Dispose(); _embedding.Dispose(); _vision.Dispose(); }
            finally { _disposeSource.Dispose(); _idle.Dispose(); }
        }

        private async Task<PaddleChart2TableGenerationResult> GenerateCoreAsync(PreparedVisualInput input, PaddleChart2TableTokenizer tokenizer, int maximumNewTokens, VisualExecutionOptions options, bool asynchronous, CancellationToken caller)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            if (maximumNewTokens <= 0 || maximumNewTokens > MaximumNewTokens) throw new VisualException(VisualErrorCodes.DocumentUnderstandingLimitExceeded, "Chart2Table maximumNewTokens must be between 1 and 2048.");
            CancellationToken disposeToken = EnterOperation();
            CancellationTokenSource? timeout = null, linked = null;
            CancellationToken operation = disposeToken;
            try
            {
                if (options.Timeout.HasValue) timeout = new CancellationTokenSource(options.Timeout.Value);
                if (caller.CanBeCanceled || timeout != null)
                    linked = timeout == null ? CancellationTokenSource.CreateLinkedTokenSource(caller, disposeToken) : CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token, disposeToken);
                if (linked != null) operation = linked.Token;
                ValidateImage(input);
                GenerativeTokenSequence prompt = tokenizer.EncodeChartPrompt();
                long[] promptIds = prompt.CopyTokenIds();
                if (promptIds.Length != PromptLength || promptIds.Count(value => value == tokenizer.ImagePatchTokenId) != ImageTokenCount) throw Invalid("The official Chart2Table prompt must contain 286 tokens and 256 image placeholders.");

                var visionWatch = Stopwatch.StartNew();
                InferenceOutputs visionOutputs = await Run(_vision, InferenceInputs.Create("pixel_values", input.Tensor), asynchronous, operation).ConfigureAwait(false);
                visionWatch.Stop();
                float[] imageFeatures = GetFiniteFloat(visionOutputs.GetRequired("fetch_name_0"), new TensorShape(1, ImageTokenCount, 1024), "image_features");
                EnsureSignal(imageFeatures, "image_features");

                var embeddingWatch = Stopwatch.StartNew();
                InferenceOutputs tokenOutputs = await Run(_embedding, InferenceInputs.Create("input_ids", new Tensor<long>(new TensorShape(1, promptIds.Length), promptIds, TensorBufferOwnership.Transfer)), asynchronous, operation).ConfigureAwait(false);
                Tensor<float> rawPromptEmbedding = GetFloatTensor(tokenOutputs.GetRequired("fetch_name_0"), new TensorShape(1, PromptLength, 1024), "inputs_embeds");
                float[] promptEmbeddings = (float[])rawPromptEmbedding.Buffer;
                EnsureSignal(promptEmbeddings, "token_embeddings");
                float[] mergedPromptEmbeddings = (float[])promptEmbeddings.Clone();
                int firstImage = Array.FindIndex(promptIds, value => value == tokenizer.ImagePatchTokenId);
                for (int index = 0; index < ImageTokenCount; index++) Array.Copy(imageFeatures, index * 1024, mergedPromptEmbeddings, (firstImage + index) * 1024, 1024);
                var promptEmbeddingTensor = new Tensor<float>(new TensorShape(1, PromptLength, 1024), mergedPromptEmbeddings, TensorBufferOwnership.Transfer);
                embeddingWatch.Stop();

                var prefillWatch = Stopwatch.StartNew();
                var prefillInputs = new InferenceInputs(new[]
                {
                    new NamedTensor("inputs_embeds", promptEmbeddingTensor),
                    new NamedTensor("attention_mask", new Tensor<bool>(new TensorShape(1, PromptLength), OnesBool(PromptLength), TensorBufferOwnership.Transfer)),
                    new NamedTensor("position_ids", new Tensor<long>(new TensorShape(1, PromptLength), Range(PromptLength), TensorBufferOwnership.Transfer))
                });
                InferenceOutputs prefillOutputs = await Run(_prefill, prefillInputs, asynchronous, operation).ConfigureAwait(false);
                prefillWatch.Stop();
                ITensor logits = prefillOutputs.GetRequired("fetch_name_0");
                EnsureTensorSignal(logits, "prefill logits");
                List<Tensor<float>> cache = RetainCache(prefillOutputs, PromptLength);
                var completion = new List<int>(maximumNewTokens);
                var decodeTimes = new List<TimeSpan>(Math.Max(0, maximumNewTokens - 1));
                long[] history = (long[])promptIds.Clone();
                GenerationFinishReason finish = GenerationFinishReason.MaxTokens;
                for (int step = 0; step < maximumNewTokens; step++)
                {
                    operation.ThrowIfCancellationRequested();
                    int selected = SelectGreedy(logits, history);
                    completion.Add(selected);
                    history = Append(history, selected);
                    if (tokenizer.IsTerminalToken(selected)) { finish = GenerationFinishReason.EndOfSequence; break; }
                    if (step + 1 >= maximumNewTokens) break;

                    var stepWatch = Stopwatch.StartNew();
                    InferenceOutputs nextEmbedding = await Run(_embedding, InferenceInputs.Create("input_ids", new Tensor<long>(new TensorShape(1, 1), new long[] { selected }, TensorBufferOwnership.Transfer)), asynchronous, operation).ConfigureAwait(false);
                    ITensor nextTokenEmbedding = nextEmbedding.GetRequired("fetch_name_0");
                    if (nextTokenEmbedding.ElementType != TensorElementType.Float32 || nextTokenEmbedding.Shape.Rank != 3 || nextTokenEmbedding.Shape[0] != 1 || nextTokenEmbedding.Shape[1] != 1 || nextTokenEmbedding.Shape[2] != 1024) throw Invalid("Token embedding output must be Float32 [1,1,1024].");
                    int pastLength = checked((int)cache[0].Shape[1]);
                    var values = new List<NamedTensor>(3 + Layers * 2)
                    {
                        new NamedTensor("inputs_embeds", nextTokenEmbedding),
                        new NamedTensor("attention_mask", new Tensor<bool>(new TensorShape(1, pastLength + 1), OnesBool(pastLength + 1), TensorBufferOwnership.Transfer)),
                        new NamedTensor("position_ids", new Tensor<long>(new TensorShape(1, 1), new long[] { pastLength }, TensorBufferOwnership.Transfer))
                    };
                    for (int layer = 0; layer < Layers; layer++)
                    {
                        values.Add(new NamedTensor("past_key_" + layer, cache[layer * 2]));
                        values.Add(new NamedTensor("past_value_" + layer, cache[(layer * 2) + 1]));
                    }
                    InferenceOutputs decoded = await Run(_decode, new InferenceInputs(values), asynchronous, operation).ConfigureAwait(false);
                    stepWatch.Stop(); decodeTimes.Add(stepWatch.Elapsed);
                    logits = decoded.GetRequired("fetch_name_0");
                    EnsureTensorSignal(logits, "decode logits");
                    cache = RetainCache(decoded, pastLength + 1);
                }
                string text = tokenizer.DecodeCompletion(completion);
                return new PaddleChart2TableGenerationResult(text, finish, promptIds.Length, completion, visionWatch.Elapsed, embeddingWatch.Elapsed, prefillWatch.Elapsed, decodeTimes);
            }
            catch (OperationCanceledException exception) { throw MapCancellation(exception, caller, timeout?.IsCancellationRequested == true); }
            catch (DeploySharpException exception) when (operation.IsCancellationRequested) { throw MapCancellation(new OperationCanceledException(exception.Message, exception, operation), caller, timeout?.IsCancellationRequested == true); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw new VisualException(VisualErrorCodes.InferenceFailed, "PP-Chart2Table ONNX generation failed.", innerException: exception); }
            finally
            {
                linked?.Dispose(); timeout?.Dispose();
                if (options.DisposeOwnedInputOnCompletion && input.Ownership == PreparedInputOwnership.Owned) input.Dispose();
                ExitOperation();
            }
        }

        private static async Task<InferenceOutputs> Run(IInferenceSession session, InferenceInputs inputs, bool asynchronous, CancellationToken cancellationToken) => asynchronous ? await session.RunAsync(inputs, cancellationToken).ConfigureAwait(false) : session.Run(inputs, cancellationToken);

        private static int SelectGreedy(ITensor logits, IReadOnlyList<long> history)
        {
            if (logits.ElementType != TensorElementType.Float32 || logits.Shape.Rank != 3 || logits.Shape[0] != 1 || logits.Shape[1] != 1 || logits.Shape[2] != VocabularySize) throw Invalid("Chart2Table logits must be Float32 [1,1,151860].");
            float[] values = (float[])logits.Buffer;
            HashSet<int> forbidden = BuildNoRepeatNgram(history, NoRepeatNgramSize);
            int selected = -1; float maximum = float.NegativeInfinity;
            for (int token = 0; token < VocabularySize; token++)
            {
                float value = values[token];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw Invalid("Chart2Table logits contain NaN or Infinity.");
                if (forbidden.Contains(token)) continue;
                if (value > maximum) { selected = token; maximum = value; }
            }
            if (selected < 0) throw Invalid("No valid token remained after the no-repeat-ngram rule.");
            return selected;
        }

        private static HashSet<int> BuildNoRepeatNgram(IReadOnlyList<long> history, int size)
        {
            var result = new HashSet<int>();
            if (history.Count < size - 1) return result;
            int prefixStart = history.Count - (size - 1);
            for (int start = 0; start + size <= history.Count; start++)
            {
                bool matches = true;
                for (int offset = 0; offset < size - 1; offset++) if (history[start + offset] != history[prefixStart + offset]) { matches = false; break; }
                if (matches) result.Add(checked((int)history[start + size - 1]));
            }
            return result;
        }

        private static List<Tensor<float>> RetainCache(InferenceOutputs outputs, int expectedLength)
        {
            if (outputs.Count != 49) throw Invalid("Chart2Table KV graph must return logits and 48 KV tensors.");
            var cache = new List<Tensor<float>>(Layers * 2);
            for (int layer = 0; layer < Layers; layer++)
            {
                cache.Add(RetainCacheTensor(outputs.GetRequired("fetch_name_" + ((layer * 2) + 1)), expectedLength));
                cache.Add(RetainCacheTensor(outputs.GetRequired("fetch_name_" + ((layer * 2) + 2)), expectedLength));
            }
            return cache;
        }

        private static Tensor<float> RetainCacheTensor(ITensor tensor, int expectedLength)
        {
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 4 || tensor.Shape[0] != 1 || tensor.Shape[1] != expectedLength || tensor.Shape[2] != Heads || tensor.Shape[3] != HeadDimension) throw Invalid("A Chart2Table KV tensor has an invalid shape or type.");
            if (!(tensor.Buffer is float[] values)) throw Invalid("A Chart2Table KV tensor does not expose a Float32 managed buffer.");
            EnsureFinite(values, "present KV");
            // All built-in providers materialize each inference output as an independently
            // owned managed tensor. Retain that tensor for the next decode step instead of
            // cloning every layer's growing KV arrays a second time.
            if (tensor is Tensor<float> owned) return owned;
            return new Tensor<float>(new TensorShape(1, expectedLength, Heads, HeadDimension), values, TensorBufferOwnership.Copy);
        }

        private static float[] GetFiniteFloat(ITensor tensor, TensorShape expected, string name)
        {
            Tensor<float> value = GetFloatTensor(tensor, expected, name);
            float[] values = (float[])value.Buffer;
            EnsureFinite(values, name);
            return values;
        }

        private static Tensor<float> GetFloatTensor(ITensor tensor, TensorShape expected, string name)
        {
            if (tensor.ElementType != TensorElementType.Float32 || !tensor.Shape.Equals(expected)) throw Invalid(name + " tensor has an unexpected shape or element type.");
            return tensor as Tensor<float> ?? new Tensor<float>(new TensorShape(tensor.Shape.ToArray()), (float[])tensor.Buffer, TensorBufferOwnership.Copy);
        }

        private static void EnsureFinite(float[] values, string name)
        {
            for (int index = 0; index < values.Length; index++) if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) throw Invalid(name + " contains NaN or Infinity.");
        }

        private static void EnsureSignal(float[] values, string name)
        {
            float maximumAbsolute = 0;
            for (int index = 0; index < values.Length; index++)
            {
                float absolute = Math.Abs(values[index]);
                if (absolute > maximumAbsolute) maximumAbsolute = absolute;
            }
            if (maximumAbsolute == 0) throw Invalid(name + " is all zero; verify graph output binding order and the selected TensorRT plan.");
        }

        private static void EnsureTensorSignal(ITensor tensor, string name)
        {
            if (tensor.ElementType != TensorElementType.Float32 || !(tensor.Buffer is float[] values)) throw Invalid(name + " has an unexpected TensorRT output type.");
            EnsureFinite(values, name);
            EnsureSignal(values, name);
        }

        private static void ValidateImage(PreparedVisualInput input)
        {
            input.EnsureUsable();
            if (input.InputName != "pixel_values" || input.BatchSize != 1 || input.Layout != VisualTensorLayout.Nchw || input.ModelSize != new VisualSize(1024, 1024) || input.Tensor.ElementType != TensorElementType.Float32 || !input.Tensor.Shape.Equals(new TensorShape(1, 3, 1024, 1024)) || input.Preprocessing.ColorOrder != VisualColorOrder.Rgb || input.Preprocessing.Means.Count != 3 || input.Preprocessing.Scales.Count != 3) throw Invalid("Chart2Table requires one Float32 RGB NCHW 1024x1024 tensor with explicit processor metadata.");
            float[] means = { .48145466f, .4578275f, .40821073f };
            float[] scales = { 1f / .26862954f, 1f / .26130258f, 1f / .27577711f };
            for (int channel = 0; channel < 3; channel++)
            {
                if (Math.Abs(input.Preprocessing.Means[channel] - means[channel]) > 1e-6f || Math.Abs(input.Preprocessing.Scales[channel] - scales[channel]) > 1e-5f) throw Invalid("Chart2Table normalization differs from the official RGB mean/std contract.");
            }
            EnsureFinite((float[])input.Tensor.Buffer, "pixel_values");
        }

        private static void ValidateMetadata(ModelMetadata metadata, IReadOnlyList<TensorPort> inputs, IReadOnlyList<TensorPort> outputs, PaddleChart2TableArtifactRole role)
        {
            if (metadata.Inputs.Count != inputs.Count || metadata.Outputs.Count != outputs.Count) throw Invalid("Backend metadata port counts differ for " + role + ".");
            ValidatePorts(metadata.Inputs, inputs, role + " inputs");
            ValidatePorts(metadata.Outputs, outputs, role + " outputs");
        }

        private static void ValidatePorts(IReadOnlyList<TensorDescriptor> actual, IReadOnlyList<TensorPort> expected, string label)
        {
            for (int index = 0; index < expected.Count; index++)
            {
                TensorDescriptor port = actual[index]; TensorPort contract = expected[index];
                if (port.Name != contract.Name || port.ElementType != contract.ElementType || port.Shape.Rank != contract.Shape.Rank) throw Invalid(label + " differ from the PP-Chart2Table graph contract at port " + index + ".");
                for (int dimension = 0; dimension < port.Shape.Rank; dimension++) if (port.Shape[dimension] > 0 && contract.Shape[dimension] > 0 && port.Shape[dimension] != contract.Shape[dimension]) throw Invalid(label + " fixed dimensions differ for " + contract.Name + ".");
            }
        }

        private static IReadOnlyList<TensorPort> VisionInputs() => new[] { new TensorPort("pixel_values", TensorElementType.Float32, 1, 3, 1024, 1024) };
        private static IReadOnlyList<TensorPort> VisionOutputs() => new[] { new TensorPort("fetch_name_0", TensorElementType.Float32, 1, ImageTokenCount, 1024) };
        private static IReadOnlyList<TensorPort> EmbeddingInputs() => new[] { new TensorPort("input_ids", TensorElementType.Int64, 1, -1) };
        private static IReadOnlyList<TensorPort> EmbeddingOutputs() => new[] { new TensorPort("fetch_name_0", TensorElementType.Float32, 1, -1, 1024) };
        private static IReadOnlyList<TensorPort> PrefillInputs() => new[] { new TensorPort("inputs_embeds", TensorElementType.Float32, 1, PromptLength, 1024), new TensorPort("attention_mask", TensorElementType.Boolean, 1, PromptLength), new TensorPort("position_ids", TensorElementType.Int64, 1, PromptLength) };
        private static IReadOnlyList<TensorPort> PrefillOutputs() => BuildOutputs(PromptLength);
        private static IReadOnlyList<TensorPort> DecodeInputs()
        {
            var values = new List<TensorPort> { new TensorPort("inputs_embeds", TensorElementType.Float32, 1, 1, 1024), new TensorPort("attention_mask", TensorElementType.Boolean, 1, -1), new TensorPort("position_ids", TensorElementType.Int64, 1, 1) };
            for (int layer = 0; layer < Layers; layer++) { values.Add(new TensorPort("past_key_" + layer, TensorElementType.Float32, 1, -1, Heads, HeadDimension)); values.Add(new TensorPort("past_value_" + layer, TensorElementType.Float32, 1, -1, Heads, HeadDimension)); }
            return values;
        }
        private static IReadOnlyList<TensorPort> DecodeOutputs() => BuildOutputs(-1);
        private static IReadOnlyList<TensorPort> BuildOutputs(int sequence)
        {
            var values = new List<TensorPort> { new TensorPort("fetch_name_0", TensorElementType.Float32, 1, 1, VocabularySize) };
            for (int index = 1; index < 49; index++) values.Add(new TensorPort("fetch_name_" + index, TensorElementType.Float32, 1, sequence, Heads, HeadDimension));
            return values;
        }

        private static bool[] OnesBool(int length) { var values = new bool[length]; for (int index = 0; index < values.Length; index++) values[index] = true; return values; }
        private static long[] Range(int length) { var values = new long[length]; for (int index = 0; index < length; index++) values[index] = index; return values; }
        private static long[] Append(long[] values, int token) { var result = new long[values.Length + 1]; Array.Copy(values, result, values.Length); result[result.Length - 1] = token; return result; }
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, message, profileId: "paddle-chart2table.onnx.dynamic-kv");
        private static void TryDispose(IDisposable? session) { try { session?.Dispose(); } catch { } }

        private CancellationToken EnterOperation()
        {
            lock (_gate)
            {
                if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The Chart2Table ONNX session has been disposed.");
                if (Interlocked.Exchange(ref _active, 1) != 0) throw new VisualException(VisualErrorCodes.DocumentUnderstandingConcurrentOperation, "The Chart2Table ONNX session permits one active generation at a time.");
                _idle.Reset(); return _disposeSource.Token;
            }
        }
        private void ExitOperation() { Interlocked.Exchange(ref _active, 0); _idle.Set(); }
        private static VisualException MapCancellation(OperationCanceledException exception, CancellationToken caller, bool timedOut)
        {
            if (timedOut) return new VisualException(VisualErrorCodes.Timeout, "Chart2Table generation exceeded its configured timeout.", innerException: exception);
            if (caller.IsCancellationRequested) return new VisualException(VisualErrorCodes.Cancelled, "Chart2Table generation was cancelled by the caller.", innerException: exception);
            return new VisualException(VisualErrorCodes.Cancelled, "Chart2Table generation was cancelled by timeout or disposal.", innerException: exception);
        }

        private sealed class TensorPort
        {
            internal TensorPort(string name, TensorElementType elementType, params long[] shape) { Name = name; ElementType = elementType; Shape = new TensorShape(shape); }
            internal string Name { get; }
            internal TensorElementType ElementType { get; }
            internal TensorShape Shape { get; }
        }
    }
}
#endif
