#if NET8_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

namespace JYPPX.DeploySharp.Visual.TensorRT
{
    /// <summary>Runs PaddleOCR-VL-1.6 decoding with request-local KV state resident on CUDA. / 在 CUDA 上保留请求独立 KV 状态并执行 PaddleOCR-VL-1.6 解码。</summary>
    /// <remarks>The five TensorRT sessions remain caller-owned. Vision and prompt prefill use the host contract; only decode KV and the decoder-to-head hidden state remain on the device between tokens. / 五个 TensorRT Session 仍由调用方持有；视觉编码和首轮 Prefill 使用主机合同，后续 Token 的 KV 与 Decoder 到 Head 的隐藏状态留在设备端。</remarks>
    public sealed class PaddleOcrVlTensorRtDeviceSession : IDisposable
    {
        private const int Layers = 18;
        private const int KeyValueHeads = 2;
        private const int HeadDimension = 128;
        private const int HiddenSize = 1024;
        private const int VocabularySize = 103424;

        private readonly IInferenceSession _vision;
        private readonly IInferenceSession _embedding;
        private readonly IInferenceSession _prefill;
        private readonly ITensorRtDeviceInferenceSession _decoder;
        private readonly ITensorRtDeviceInferenceSession _head;
        private readonly CudaStream _stream;
        private readonly CudaMemory _embeddingMemory;
        private readonly CudaMemory _positionMemory;
        private readonly CudaMemory _hiddenMemory;
        private readonly CudaMemory _logitsMemory;
        private readonly SemaphoreSlim _operation = new SemaphoreSlim(1, 1);
        private bool _disposed;

        /// <summary>Binds five caller-owned sessions whose graph ports match the pinned 1.6 Bundle. / 绑定与固定 1.6 Bundle 端口匹配的五个调用方持有 Session。</summary>
        public PaddleOcrVlTensorRtDeviceSession(IInferenceSession vision, IInferenceSession embedding,
            IInferenceSession decoder, IInferenceSession head, IInferenceSession prefill)
        {
            _vision = vision ?? throw new ArgumentNullException(nameof(vision));
            _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
            _prefill = prefill ?? throw new ArgumentNullException(nameof(prefill));
            if (decoder is not ITensorRtDeviceInferenceSession deviceDecoder ||
                head is not ITensorRtDeviceInferenceSession deviceHead)
                throw new NotSupportedException("PaddleOCR-VL device decoding requires TensorRT device inference for decoder and head.");
            if (deviceDecoder.DeviceOrdinal != deviceHead.DeviceOrdinal)
                throw new ArgumentException("Decoder and head must use the same CUDA device.");
            using (var contract = new PaddleOcrVlOnnxSession(vision, embedding, decoder, head, prefill)) { }
            _decoder = deviceDecoder;
            _head = deviceHead;
            CudaStream? stream = null;
            CudaMemory? embeddingMemory = null, positionMemory = null, hiddenMemory = null, logitsMemory = null;
            try
            {
                stream = new CudaStream();
                if (CudaDevice.Current != deviceDecoder.DeviceOrdinal)
                    throw new ArgumentException("The current CUDA device must match the Decoder and Head TensorRT sessions.");
                embeddingMemory = new CudaMemory(HiddenSize * sizeof(float));
                positionMemory = new CudaMemory(3 * sizeof(long));
                hiddenMemory = new CudaMemory(HiddenSize * sizeof(float));
                logitsMemory = new CudaMemory(VocabularySize * sizeof(float));
                _stream = stream;
                _embeddingMemory = embeddingMemory;
                _positionMemory = positionMemory;
                _hiddenMemory = hiddenMemory;
                _logitsMemory = logitsMemory;
            }
            catch
            {
                logitsMemory?.Dispose(); hiddenMemory?.Dispose(); positionMemory?.Dispose(); embeddingMemory?.Dispose(); stream?.Dispose();
                throw;
            }
        }

        /// <summary>Runs bounded greedy generation; calls on this instance are serialized. / 执行有界 Greedy 生成，同一实例的调用串行化。</summary>
        public PaddleOcrVlGenerationResult Generate(PaddleOcrVlPreparedInput input, PaddleOcrVlTokenizer tokenizer,
            PaddleOcrVlGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            PaddleOcrVlGenerationOptions settings = options ?? new PaddleOcrVlGenerationOptions();
            using var timeout = settings.Timeout.HasValue ? new CancellationTokenSource(settings.Timeout.Value) : null;
            using var linked = timeout == null ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            _operation.Wait(linked.Token);
            try
            {
                if (_disposed) throw new ObjectDisposedException(nameof(PaddleOcrVlTensorRtDeviceSession));
                return GenerateCore(input, tokenizer, settings, linked.Token);
            }
            finally { _operation.Release(); }
        }

        /// <summary>Waits for an active generation and releases owned CUDA scratch; the five graph sessions remain caller-owned. / 等待活动生成并释放 CUDA 临时内存，五个图 Session 仍由调用方持有。</summary>
        public void Dispose()
        {
            _operation.Wait();
            try
            {
                if (_disposed) return;
                _disposed = true;
                TryDispose(_logitsMemory); TryDispose(_hiddenMemory); TryDispose(_positionMemory);
                TryDispose(_embeddingMemory); TryDispose(_stream);
            }
            finally { _operation.Release(); }
        }

        private PaddleOcrVlGenerationResult GenerateCore(PaddleOcrVlPreparedInput input, PaddleOcrVlTokenizer tokenizer,
            PaddleOcrVlGenerationOptions options, CancellationToken cancellationToken)
        {
            PaddleOcrVlTokenSequence prompt = tokenizer.EncodePrompt(input.ImagePlan, options.MaximumNewTokens, options.MaximumContextTokens);
            PaddleOcrVlPositionPlan positions = PaddleOcrVlPositionPlan.Create(prompt);
            int maximumPast = checked(prompt.Count + options.MaximumNewTokens - 1);
            long bankBytes = checked((long)maximumPast * Layers * 2 * KeyValueHeads * HeadDimension * sizeof(float));
            if (checked(bankBytes * 2) > options.MaximumCacheBytes)
                throw new ArgumentOutOfRangeException(nameof(options), "The two CUDA KV banks exceed the configured cache budget.");
            foreach (float pixel in (float[])input.PixelPatches.Buffer)
                if (!float.IsFinite(pixel) || pixel < -1.000001f || pixel > 1.000001f)
                    throw new ArgumentException("PaddleOCR-VL patches must use finite official [-1,1] normalization.", nameof(input));
            TensorShape visionShape = _vision.Metadata.Inputs.Single(port => port.Name == "pixel_patches").Shape;
            if (visionShape[0] > 0 && visionShape[0] != input.ImagePlan.GridHeight
                || visionShape[1] > 0 && visionShape[1] != input.ImagePlan.GridWidth)
                throw new ArgumentException("The prepared image grid is outside the Vision Engine's fixed input shape.", nameof(input));

            var watch = Stopwatch.StartNew();
            InferenceOutputs vision = _vision.Run(InferenceInputs.Create("pixel_patches", input.PixelPatches), cancellationToken);
            float[] features = Float(vision.GetRequired("image_features"), new TensorShape(input.ImagePlan.ImageTokenCount, HiddenSize), "image_features");
            InferenceOutputs embedded = _embedding.Run(InferenceInputs.Create("input_ids",
                new Tensor<long>(new TensorShape(1, prompt.Count), prompt.CopyTokenIds(), TensorBufferOwnership.Transfer)), cancellationToken);
            float[] rawEmbeddings = Float(embedded.GetRequired("inputs_embeds"), new TensorShape(1, prompt.Count, HiddenSize), "inputs_embeds");
            float[] merged = (float[])rawEmbeddings.Clone();
            Array.Copy(features, 0, merged, checked(prompt.ImageTokenOffset * HiddenSize), features.Length);
            InferenceOutputs prefill = _prefill.Run(new InferenceInputs(new[]
            {
                new NamedTensor("inputs_embeds", new Tensor<float>(new TensorShape(1, prompt.Count, HiddenSize), merged, TensorBufferOwnership.Transfer)),
                new NamedTensor("position_ids", new Tensor<long>(new TensorShape(3, 1, prompt.Count), positions.CopyPositionIds(), TensorBufferOwnership.Transfer))
            }), cancellationToken);

            using var current = new KvBank(maximumPast);
            using var next = new KvBank(maximumPast);
            current.CopyFromPrefill(prefill, prompt.Count);
            InferenceOutputs firstHead = _head.Run(InferenceInputs.Create("last_hidden_state", prefill.GetRequired("last_hidden_state")), cancellationToken);
            float[] firstLogits = Float(firstHead.GetRequired("logits"), new TensorShape(1, 1, VocabularySize), "logits");
            var tokens = new List<int>(options.MaximumNewTokens);
            int selected = ArgMax(firstLogits);
            tokens.Add(selected);
            GenerationFinishReason reason = selected == tokenizer.EndOfSequenceTokenId
                ? GenerationFinishReason.EndOfSequence : GenerationFinishReason.MaxTokens;
            KvBank read = current, write = next;
            var positionIds = new long[3];
            byte[] positionBytes = new byte[3 * sizeof(long)];
            for (int step = 1; step < options.MaximumNewTokens && reason != GenerationFinishReason.EndOfSequence; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                InferenceOutputs nextEmbedding = _embedding.Run(InferenceInputs.Create("input_ids",
                    new Tensor<long>(new TensorShape(1, 1), new[] { (long)selected }, TensorBufferOwnership.Transfer)), cancellationToken);
                _embeddingMemory.CopyFrom(Float(nextEmbedding.GetRequired("inputs_embeds"), new TensorShape(1, 1, HiddenSize), "inputs_embeds"));
                positions.WriteDecodePositionIds(prompt.Count + step - 1, 1, positionIds);
                Buffer.BlockCopy(positionIds, 0, positionBytes, 0, positionBytes.Length);
                _positionMemory.CopyFrom(positionBytes);

                int pastLength = prompt.Count + step - 1;
                var decoderInputs = new List<TensorRtDeviceTensor>(2 + Layers * 2)
                {
                    new TensorRtDeviceTensor("inputs_embeds", TensorElementType.Float32, new TensorShape(1, 1, HiddenSize), _embeddingMemory),
                    new TensorRtDeviceTensor("position_ids", TensorElementType.Int64, new TensorShape(3, 1, 1), _positionMemory)
                };
                var decoderOutputs = new List<TensorRtDeviceTensor>(1 + Layers * 2)
                {
                    new TensorRtDeviceTensor("last_hidden_state", TensorElementType.Float32, new TensorShape(1, 1, HiddenSize), _hiddenMemory)
                };
                for (int layer = 0; layer < Layers; layer++)
                {
                    decoderInputs.Add(read.Tensor(layer * 2, "past_key_" + layer, pastLength));
                    decoderInputs.Add(read.Tensor(layer * 2 + 1, "past_value_" + layer, pastLength));
                    decoderOutputs.Add(write.Tensor(layer * 2, "present_key_" + layer, pastLength + 1));
                    decoderOutputs.Add(write.Tensor(layer * 2 + 1, "present_value_" + layer, pastLength + 1));
                }

                using (TensorRtDeviceInferenceExecution decode = _decoder.RunDevice(decoderInputs, decoderOutputs, _stream, cancellationToken))
                using (TensorRtDeviceInferenceExecution head = _head.RunDevice(
                    new[] { new TensorRtDeviceTensor("last_hidden_state", TensorElementType.Float32, new TensorShape(1, 1, HiddenSize), _hiddenMemory) },
                    new[] { new TensorRtDeviceTensor("logits", TensorElementType.Float32, new TensorShape(1, 1, VocabularySize), _logitsMemory) },
                    _stream, cancellationToken))
                {
                    head.Synchronize();
                    selected = ArgMax(_logitsMemory.ToSingleArray(VocabularySize));
                    head.ReleaseAfterEnqueue();
                    decode.ReleaseAfterEnqueue();
                }
                tokens.Add(selected);
                if (selected == tokenizer.EndOfSequenceTokenId) reason = GenerationFinishReason.EndOfSequence;
                KvBank previous = read; read = write; write = previous;
            }
            cancellationToken.ThrowIfCancellationRequested();
            watch.Stop();
            return new PaddleOcrVlGenerationResult(input.ImagePlan.Task, tokenizer.DecodeCompletion(tokens), tokens,
                reason, prompt.Count, positions.RopeDelta, input.SourceSha256, watch.Elapsed);
        }

        private static float[] Float(ITensor tensor, TensorShape expected, string name)
        {
            if (tensor.ElementType != TensorElementType.Float32 || !tensor.Shape.Equals(expected) || tensor.Buffer is not float[] values)
                throw new InvalidOperationException("PaddleOCR-VL " + name + " has an unexpected type or shape.");
            return values;
        }

        private static int ArgMax(float[] logits)
        {
            int best = 0;
            for (int index = 0; index < logits.Length; index++)
            {
                if (!float.IsFinite(logits[index])) throw new InvalidOperationException("PaddleOCR-VL logits contain non-finite values.");
                if (logits[index] > logits[best]) best = index;
            }
            return best;
        }

        private static void TryDispose(IDisposable resource)
        {
            try { resource.Dispose(); } catch { /* Release the remaining CUDA resources. */ }
        }

        private sealed class KvBank : IDisposable
        {
            private readonly CudaMemory[] _buffers = new CudaMemory[Layers * 2];

            internal KvBank(int maximumLength)
            {
                int bytes = checked(maximumLength * KeyValueHeads * HeadDimension * sizeof(float));
                try { for (int index = 0; index < _buffers.Length; index++) _buffers[index] = new CudaMemory(bytes); }
                catch { Dispose(); throw; }
            }

            internal TensorRtDeviceTensor Tensor(int index, string name, int length) =>
                new TensorRtDeviceTensor(name, TensorElementType.Float32,
                    new TensorShape(1, KeyValueHeads, length, HeadDimension), _buffers[index]);

            internal void CopyFromPrefill(InferenceOutputs outputs, int promptLength)
            {
                for (int layer = 0; layer < Layers; layer++)
                    for (int kind = 0; kind < 2; kind++)
                    {
                        string name = "present_" + (kind == 0 ? "key_" : "value_") + layer;
                        float[] values = Float(outputs.GetRequired(name),
                            new TensorShape(1, KeyValueHeads, promptLength, HeadDimension), name);
                        _buffers[layer * 2 + kind].CopyFrom(values);
                    }
            }

            public void Dispose()
            {
                foreach (CudaMemory? memory in _buffers)
                    try { memory?.Dispose(); } catch { /* Release all remaining allocations. */ }
            }
        }
    }
}
#endif
