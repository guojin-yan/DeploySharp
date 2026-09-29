#if NET8_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.TensorRtSharp;

namespace JYPPX.DeploySharp.Visual.TensorRT
{
    /// <summary>
    /// Runs the PP-Chart2Table four-graph bundle with its autoregressive KV cache kept in CUDA memory.
    /// / 使用四图 Bundle 执行 PP-Chart2Table，并让自回归 KV 缓存驻留在 CUDA 显存中。
    /// </summary>
    /// <remarks>
    /// Vision, embedding, and prompt prefill use the regular inference contract. After prefill, only the
    /// selected token embedding and attention metadata cross the host boundary; the 48 present KV tensors
    /// ping-pong between two device banks, while only the vocabulary logits are copied back for greedy selection.
    /// / Vision、Embedding 和 Prompt Prefill 使用常规推理合同；Prefill 后仅 Token Embedding 与 Attention 元数据经过主机边界，
    /// 48 个 KV Tensor 在两组设备缓冲区间轮换，仅将词表 Logits 回传用于 Greedy 选择。
    /// </remarks>
    public sealed class PaddleChart2TableTensorRtDeviceSession : IDisposable
    {
        private const int PromptLength = 286;
        private const int ImageTokenCount = 256;
        private const int Layers = 24;
        private const int Heads = 16;
        private const int HeadDimension = 64;
        private const int VocabularySize = 151860;
        private const int MaximumNewTokens = 2048;
        private const int NoRepeatNgramSize = 20;
        private const int MaximumCacheLength = PromptLength + MaximumNewTokens - 1;

        private readonly IInferenceSession _vision;
        private readonly IInferenceSession _embedding;
        private readonly IInferenceSession _prefill;
        private readonly ITensorRtDeviceInferenceSession _decode;
        private readonly CudaStream _stream;
        private readonly CudaMemory _tokenEmbeddingMemory;
        private readonly CudaMemory _attentionMaskMemory;
        private readonly CudaMemory _positionMemory;
        private readonly CudaMemory _logitsMemory;
        private readonly SemaphoreSlim _operation = new SemaphoreSlim(1, 1);
        private bool _disposed;

        /// <summary>Creates a device-resident Chart2Table runner from a TensorRT provider. The provider must outlive this session. / 从 TensorRT Provider 创建显存驻留 Chart2Table Session；Provider 生命周期需长于本 Session。</summary>
        public PaddleChart2TableTensorRtDeviceSession(TensorRtBackendProvider provider, PaddleChart2TableOnnxBundle bundle, BackendRequest request, SessionOptions? options = null)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            if (request == null) throw new ArgumentNullException(nameof(request));
            foreach (PaddleChart2TableArtifactRole role in Enum.GetValues(typeof(PaddleChart2TableArtifactRole)))
            {
                if (bundle.GetArtifact(role).Format != "tensorrt-engine") throw new ArgumentException("The device-resident Chart2Table path requires four prebuilt TensorRT engine artifacts.", nameof(bundle));
            }

            SessionOptions requested = options ?? SessionOptions.Default;
            var one = new SessionOptions(1, requested.EnableProfiling);
            var effective = new BackendRequest(request.RequiredCapabilities | BackendCapabilities.TensorInference, request.BackendId, request.Device);
            IInferenceSession? vision = null, embedding = null, prefill = null, decode = null;
            CudaStream? stream = null;
            CudaMemory? tokenEmbedding = null, attentionMask = null, position = null, logits = null;
            try
            {
                vision = provider.CreateSession(bundle.GetArtifact(PaddleChart2TableArtifactRole.VisionProjector), effective, one);
                embedding = provider.CreateSession(bundle.GetArtifact(PaddleChart2TableArtifactRole.TokenEmbedding), effective, one);
                prefill = provider.CreateSession(bundle.GetArtifact(PaddleChart2TableArtifactRole.TextPrefill), effective, one);
                decode = provider.CreateSession(bundle.GetArtifact(PaddleChart2TableArtifactRole.TextDecodeWithPast), effective, one);
                if (!(decode is ITensorRtDeviceInferenceSession deviceDecode)) throw new NotSupportedException("The selected TensorRT provider does not expose its caller-owned device inference surface.");
                stream = new CudaStream();
                _ = deviceDecode.DeviceOrdinal;
                tokenEmbedding = new CudaMemory(1024 * sizeof(float));
                attentionMask = new CudaMemory(MaximumCacheLength);
                position = new CudaMemory(sizeof(long));
                logits = new CudaMemory(VocabularySize * sizeof(float));
                _vision = vision;
                _embedding = embedding;
                _prefill = prefill;
                _decode = deviceDecode;
                _stream = stream;
                _tokenEmbeddingMemory = tokenEmbedding;
                _attentionMaskMemory = attentionMask;
                _positionMemory = position;
                _logitsMemory = logits;
            }
            catch
            {
                TryDispose(logits); TryDispose(position); TryDispose(attentionMask); TryDispose(tokenEmbedding); TryDispose(stream);
                TryDispose(decode); TryDispose(prefill); TryDispose(embedding); TryDispose(vision);
                throw;
            }
        }

        /// <summary>Gets the graph bundle used by this session. / 获取本 Session 使用的图 Bundle。</summary>
        public PaddleChart2TableOnnxBundle Bundle { get; }

        /// <summary>Runs greedy image-to-table generation while keeping KV state on the TensorRT device. / 执行 Greedy 图表转表格生成，并将 KV 状态保留在 TensorRT 设备端。</summary>
        public PaddleChart2TableGenerationResult Generate(PreparedVisualInput input, PaddleChart2TableTokenizer tokenizer, int maximumNewTokens = 1024, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            if (maximumNewTokens <= 0 || maximumNewTokens > MaximumNewTokens) throw new ArgumentOutOfRangeException(nameof(maximumNewTokens), "Chart2Table maximumNewTokens must be between 1 and 2048.");
            _operation.Wait(cancellationToken);
            try
            {
                ThrowIfDisposed();
                return GenerateCore(input, tokenizer, maximumNewTokens, cancellationToken);
            }
            finally { _operation.Release(); }
        }

        /// <summary>Cancels no in-flight native call; waits for the current generation and releases all sessions and scratch buffers. / 等待当前生成完成并释放 Session 与临时缓冲区。</summary>
        public void Dispose()
        {
            _operation.Wait();
            try
            {
                if (_disposed) return;
                _disposed = true;
                TryDispose(_logitsMemory); TryDispose(_positionMemory); TryDispose(_attentionMaskMemory); TryDispose(_tokenEmbeddingMemory);
                TryDispose(_stream);
                TryDispose(_decode); TryDispose(_prefill); TryDispose(_embedding); TryDispose(_vision);
            }
            finally
            {
                _operation.Release();
            }
        }

        private PaddleChart2TableGenerationResult GenerateCore(PreparedVisualInput input, PaddleChart2TableTokenizer tokenizer, int maximumNewTokens, CancellationToken cancellationToken)
        {
            ValidateImage(input);
            GenerativeTokenSequence prompt = tokenizer.EncodeChartPrompt();
            long[] promptIds = prompt.CopyTokenIds();
            if (promptIds.Length != PromptLength || promptIds.Count(value => value == tokenizer.ImagePatchTokenId) != ImageTokenCount) throw new InvalidOperationException("The official Chart2Table prompt must contain 286 tokens and 256 image placeholders.");

            var visionWatch = Stopwatch.StartNew();
            InferenceOutputs visionOutputs = _vision.Run(InferenceInputs.Create("pixel_values", input.Tensor), cancellationToken);
            visionWatch.Stop();
            float[] imageFeatures = GetFiniteFloat(visionOutputs.GetRequired("fetch_name_0"), new TensorShape(1, ImageTokenCount, 1024), "image_features");
            EnsureSignal(imageFeatures, "image_features");

            var embeddingWatch = Stopwatch.StartNew();
            InferenceOutputs tokenOutputs = _embedding.Run(InferenceInputs.Create("input_ids", new Tensor<long>(new TensorShape(1, PromptLength), promptIds, TensorBufferOwnership.Transfer)), cancellationToken);
            Tensor<float> rawPromptEmbedding = GetFloatTensor(tokenOutputs.GetRequired("fetch_name_0"), new TensorShape(1, PromptLength, 1024), "inputs_embeds");
            float[] promptEmbeddings = (float[])rawPromptEmbedding.Buffer;
            EnsureSignal(promptEmbeddings, "token_embeddings");
            float[] mergedPromptEmbeddings = (float[])promptEmbeddings.Clone();
            int firstImage = Array.FindIndex(promptIds, value => value == tokenizer.ImagePatchTokenId);
            for (int index = 0; index < ImageTokenCount; index++) Array.Copy(imageFeatures, index * 1024, mergedPromptEmbeddings, (firstImage + index) * 1024, 1024);
            embeddingWatch.Stop();

            var prefillWatch = Stopwatch.StartNew();
            var prefillInputs = new InferenceInputs(new[]
            {
                new NamedTensor("inputs_embeds", new Tensor<float>(new TensorShape(1, PromptLength, 1024), mergedPromptEmbeddings, TensorBufferOwnership.Transfer)),
                new NamedTensor("attention_mask", new Tensor<bool>(new TensorShape(1, PromptLength), Ones(PromptLength), TensorBufferOwnership.Transfer)),
                new NamedTensor("position_ids", new Tensor<long>(new TensorShape(1, PromptLength), Range(PromptLength), TensorBufferOwnership.Transfer))
            });
            InferenceOutputs prefillOutputs = _prefill.Run(prefillInputs, cancellationToken);
            ITensor logits = prefillOutputs.GetRequired("fetch_name_0");
            ValidateLogits(logits);
            int cacheCapacity = Math.Min(PromptLength + maximumNewTokens - 1, MaximumCacheLength);
            using var cacheA = new KvCacheBank(cacheCapacity);
            using var cacheB = new KvCacheBank(cacheCapacity);
            cacheA.CopyFromPrefill(prefillOutputs, PromptLength);
            prefillWatch.Stop();

            var completion = new List<int>(maximumNewTokens);
            var decodeTimes = new List<TimeSpan>(Math.Max(0, maximumNewTokens - 1));
            long[] history = (long[])promptIds.Clone();
            int pastLength = PromptLength;
            KvCacheBank currentCache = cacheA;
            KvCacheBank nextCache = cacheB;
            GenerationFinishReason finish = GenerationFinishReason.MaxTokens;

            for (int step = 0; step < maximumNewTokens; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int selected = SelectGreedy(logits, history);
                completion.Add(selected);
                history = Append(history, selected);
                if (tokenizer.IsTerminalToken(selected)) { finish = GenerationFinishReason.EndOfSequence; break; }
                if (step + 1 >= maximumNewTokens) break;

                var decodeWatch = Stopwatch.StartNew();
                embeddingWatch.Start();
                InferenceOutputs nextEmbedding = _embedding.Run(
                    InferenceInputs.Create("input_ids", new Tensor<long>(new TensorShape(1, 1), new[] { (long)selected }, TensorBufferOwnership.Transfer)),
                    cancellationToken);
                float[] nextTokenEmbedding = GetFiniteFloat(nextEmbedding.GetRequired("fetch_name_0"), new TensorShape(1, 1, 1024), "next_token_embedding");
                _tokenEmbeddingMemory.CopyFrom(nextTokenEmbedding);
                embeddingWatch.Stop();

                byte[] mask = OnesBytes(pastLength + 1);
                _attentionMaskMemory.CopyFrom(mask);
                byte[] positionBytes = new byte[sizeof(long)];
                Buffer.BlockCopy(new[] { (long)pastLength }, 0, positionBytes, 0, positionBytes.Length);
                _positionMemory.CopyFrom(positionBytes);

                var deviceInputs = new List<TensorRtDeviceTensor>(3 + Layers * 2)
                {
                    new TensorRtDeviceTensor("inputs_embeds", TensorElementType.Float32, new TensorShape(1, 1, 1024), _tokenEmbeddingMemory),
                    new TensorRtDeviceTensor("attention_mask", TensorElementType.Boolean, new TensorShape(1, pastLength + 1), _attentionMaskMemory),
                    new TensorRtDeviceTensor("position_ids", TensorElementType.Int64, new TensorShape(1, 1), _positionMemory)
                };
                for (int layer = 0; layer < Layers; layer++)
                {
                    deviceInputs.Add(currentCache.CreateTensor(layer * 2, "past_key_" + layer, pastLength));
                    deviceInputs.Add(currentCache.CreateTensor((layer * 2) + 1, "past_value_" + layer, pastLength));
                }

                int presentLength = pastLength + 1;
                var deviceOutputs = new List<TensorRtDeviceTensor>(1 + Layers * 2)
                {
                    new TensorRtDeviceTensor("fetch_name_0", TensorElementType.Float32, new TensorShape(1, 1, VocabularySize), _logitsMemory)
                };
                for (int layer = 0; layer < Layers; layer++)
                {
                    deviceOutputs.Add(nextCache.CreateTensor(layer * 2, "fetch_name_" + ((layer * 2) + 1), presentLength));
                    deviceOutputs.Add(nextCache.CreateTensor((layer * 2) + 1, "fetch_name_" + ((layer * 2) + 2), presentLength));
                }

                using (TensorRtDeviceInferenceExecution execution = _decode.RunDevice(deviceInputs, deviceOutputs, _stream, cancellationToken))
                {
                    execution.Synchronize();
                    logits = new Tensor<float>(new TensorShape(1, 1, VocabularySize), _logitsMemory.ToSingleArray(VocabularySize), TensorBufferOwnership.Transfer);
                    execution.ReleaseAfterEnqueue();
                }
                decodeWatch.Stop();
                decodeTimes.Add(decodeWatch.Elapsed);
                pastLength = presentLength;
                KvCacheBank previous = currentCache;
                currentCache = nextCache;
                nextCache = previous;
            }

            string text = tokenizer.DecodeCompletion(completion);
            return new PaddleChart2TableGenerationResult(text, finish, promptIds.Length, completion, visionWatch.Elapsed, embeddingWatch.Elapsed, prefillWatch.Elapsed, decodeTimes);
        }

        private static int SelectGreedy(ITensor logits, IReadOnlyList<long> history)
        {
            ValidateLogits(logits);
            float[] values = (float[])logits.Buffer;
            HashSet<int> forbidden = BuildNoRepeatNgram(history, NoRepeatNgramSize);
            int selected = -1;
            float maximum = float.NegativeInfinity;
            for (int token = 0; token < VocabularySize; token++)
            {
                float value = values[token];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Chart2Table logits contain NaN or Infinity.");
                if (forbidden.Contains(token)) continue;
                if (value > maximum) { selected = token; maximum = value; }
            }
            if (selected < 0) throw new InvalidOperationException("No valid token remained after the no-repeat-ngram rule.");
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

        private static void ValidateLogits(ITensor tensor)
        {
            if (tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 3 || tensor.Shape[0] != 1 || tensor.Shape[1] != 1 || tensor.Shape[2] != VocabularySize) throw new InvalidOperationException("Chart2Table logits must be Float32 [1,1,151860].");
        }

        private static float[] GetFiniteFloat(ITensor tensor, TensorShape expected, string name)
        {
            Tensor<float> value = GetFloatTensor(tensor, expected, name);
            float[] values = (float[])value.Buffer;
            for (int index = 0; index < values.Length; index++) if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) throw new InvalidOperationException(name + " contains NaN or Infinity.");
            return values;
        }

        private static Tensor<float> GetFloatTensor(ITensor tensor, TensorShape expected, string name)
        {
            if (tensor.ElementType != TensorElementType.Float32 || !tensor.Shape.Equals(expected)) throw new InvalidOperationException(name + " tensor has an unexpected shape or element type.");
            return tensor as Tensor<float> ?? new Tensor<float>(new TensorShape(tensor.Shape.ToArray()), (float[])tensor.Buffer, TensorBufferOwnership.Copy);
        }

        private static void EnsureSignal(float[] values, string name)
        {
            float maximumAbsolute = 0;
            for (int index = 0; index < values.Length; index++) maximumAbsolute = Math.Max(maximumAbsolute, Math.Abs(values[index]));
            if (maximumAbsolute == 0) throw new InvalidOperationException(name + " is all zero; verify the TensorRT output binding and precision.");
        }

        private static void ValidateImage(PreparedVisualInput input)
        {
            float[] means = { .48145466f, .4578275f, .40821073f };
            float[] scales = { 1f / .26862954f, 1f / .26130258f, 1f / .27577711f };
            if (input.InputName != "pixel_values" || input.BatchSize != 1 || input.Layout != VisualTensorLayout.Nchw || input.ModelSize != new VisualSize(1024, 1024) || input.Tensor.ElementType != TensorElementType.Float32 || !input.Tensor.Shape.Equals(new TensorShape(1, 3, 1024, 1024)) || input.Preprocessing.ColorOrder != VisualColorOrder.Rgb || input.Preprocessing.Means.Count != 3 || input.Preprocessing.Scales.Count != 3) throw new InvalidOperationException("Chart2Table requires one Float32 RGB NCHW 1024x1024 tensor with official processor metadata.");
            for (int channel = 0; channel < 3; channel++) if (Math.Abs(input.Preprocessing.Means[channel] - means[channel]) > 1e-6f || Math.Abs(input.Preprocessing.Scales[channel] - scales[channel]) > 1e-5f) throw new InvalidOperationException("Chart2Table normalization differs from the official RGB mean/std contract.");
        }

        private static long[] Append(IReadOnlyList<long> values, int token)
        {
            var result = new long[values.Count + 1];
            for (int index = 0; index < values.Count; index++) result[index] = values[index];
            result[result.Length - 1] = token;
            return result;
        }

        private static bool[] Ones(int length) { var values = new bool[length]; Array.Fill(values, true); return values; }
        private static byte[] OnesBytes(int length) { var values = new byte[length]; Array.Fill(values, (byte)1); return values; }
        private static long[] Range(int length) { var values = new long[length]; for (int index = 0; index < values.Length; index++) values[index] = index; return values; }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(PaddleChart2TableTensorRtDeviceSession)); }
        private static void TryDispose(IDisposable? value) { try { value?.Dispose(); } catch { } }

        private sealed class KvCacheBank : IDisposable
        {
            private readonly CudaMemory[] _buffers = new CudaMemory[Layers * 2];

            internal KvCacheBank(int maximumSequenceLength)
            {
                int bytes = checked(maximumSequenceLength * Heads * HeadDimension * sizeof(float));
                try { for (int index = 0; index < _buffers.Length; index++) _buffers[index] = new CudaMemory(bytes); }
                catch { Dispose(); throw; }
            }

            internal TensorRtDeviceTensor CreateTensor(int index, string name, int sequenceLength) =>
                new TensorRtDeviceTensor(name, TensorElementType.Float32, new TensorShape(1, sequenceLength, Heads, HeadDimension), _buffers[index]);

            internal void CopyFromPrefill(InferenceOutputs outputs, int sequenceLength)
            {
                for (int index = 0; index < _buffers.Length; index++)
                {
                    ITensor tensor = outputs.GetRequired("fetch_name_" + (index + 1));
                    var expected = new TensorShape(1, sequenceLength, Heads, HeadDimension);
                    if (tensor.ElementType != TensorElementType.Float32 || !tensor.Shape.Equals(expected) || !(tensor.Buffer is float[] values)) throw new InvalidOperationException("A Chart2Table prefill KV tensor has an invalid type, shape, or host buffer.");
                    for (int valueIndex = 0; valueIndex < values.Length; valueIndex++) if (float.IsNaN(values[valueIndex]) || float.IsInfinity(values[valueIndex])) throw new InvalidOperationException("A Chart2Table prefill KV tensor contains NaN or Infinity.");
                    _buffers[index].CopyFrom(values);
                }
            }

            public void Dispose()
            {
                for (int index = 0; index < _buffers.Length; index++) { try { _buffers[index]?.Dispose(); } catch { } _buffers[index] = null!; }
            }
        }
    }
}
#endif
