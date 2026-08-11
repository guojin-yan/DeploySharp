using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.LlamaSharp.Internal;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.LLM.Prompt;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace JYPPX.DeploySharp.Backends.LlamaSharp
{
    internal sealed class LlamaSharpSession : ILanguageModelSession
    {
        private readonly ModelArtifact _artifact;
        private readonly LLamaWeights _weights;
        private readonly ModelParams _embeddingParams;
        private readonly StatelessExecutor _executor;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly object _disposeSync = new object();
        private LLamaEmbedder? _embedder;
        private volatile bool _disposed;

        public LlamaSharpSession(
            ModelArtifact artifact,
            BackendDescriptor descriptor,
            LLamaWeights weights,
            ModelParams generationParams,
            ModelParams embeddingParams,
            IPromptFormatter promptFormatter,
            string device)
        {
            _artifact = artifact;
            _weights = weights;
            _embeddingParams = embeddingParams;
            PromptFormatter = promptFormatter;
            _executor = new StatelessExecutor(weights, generationParams);
            LanguageModelCapabilities capabilities = LanguageModelCapabilities.TextGeneration | LanguageModelCapabilities.Streaming;
            int? embeddingDimensions = null;
            if (weights.EmbeddingSize > 0)
            {
                try
                {
                    // Capability is declared only after LLamaSharp creates a real embedding context for this model. / 只有 LLamaSharp 为当前模型成功创建真实嵌入上下文后才声明能力。
                    _embedder = new LLamaEmbedder(_weights, _embeddingParams);
                    capabilities |= LanguageModelCapabilities.Embeddings;
                    embeddingDimensions = weights.EmbeddingSize;
                }
                catch (Exception exception)
                {
                    EmbeddingInitializationError = exception;
                }
            }

            Metadata = new LanguageModelMetadata(
                artifact,
                descriptor,
                capabilities,
                generationParams.ContextSize.HasValue ? checked((int)generationParams.ContextSize.Value) : weights.ContextSize,
                embeddingDimensions,
                device,
                new[] { "local", "gguf", "llama.cpp" },
                LanguageModelProfile.CreateUnverified(
                    artifact,
                    descriptor.Id,
                    generationParams.ContextSize.HasValue ? checked((int)generationParams.ContextSize.Value) : weights.ContextSize,
                    (capabilities & LanguageModelCapabilities.Embeddings) != 0));
        }

        public LanguageModelMetadata Metadata { get; }

        public IPromptFormatter PromptFormatter { get; }

        internal Exception? EmbeddingInitializationError { get; }

        internal IReadOnlyDictionary<string, string> ModelMetadata => _weights.Metadata;

        internal int ModelContextSize => _weights.ContextSize;

        internal ulong ModelSizeInBytes => _weights.SizeInBytes;

        internal ulong ModelParameterCount => _weights.ParameterCount;

        internal int ModelEmbeddingSize => _weights.EmbeddingSize;

        internal int ModelVocabularySize => _weights.Vocab.Count;

        internal int? ModelBosTokenId => _weights.Vocab.BOS.HasValue ? (int)_weights.Vocab.BOS.Value : (int?)null;

        internal int? ModelEosTokenId => _weights.Vocab.EOS.HasValue ? (int)_weights.Vocab.EOS.Value : (int?)null;

        internal int? ModelPadTokenId => _weights.Vocab.Pad.HasValue ? (int)_weights.Vocab.Pad.Value : (int?)null;

        internal string ModelDescription => _weights.NativeHandle.Description;

        public GenerationResult Generate(TextGenerationRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GenerateAsync(request, cancellationToken).GetAwaiter().GetResult();
        }

        public async Task<GenerationResult> GenerateAsync(TextGenerationRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var builder = new StringBuilder();
            GenerationFinishReason finishReason = GenerationFinishReason.None;
            await foreach (GenerationChunk chunk in StreamAsync(request, cancellationToken).ConfigureAwait(false))
            {
                builder.Append(chunk.Text);
                if (chunk.IsTerminal) finishReason = chunk.FinishReason;
            }

            int promptTokens;
            int generatedTokens;
            try
            {
                promptTokens = _weights.Tokenize(request.Prompt, true, true, Encoding.UTF8).Length;
                generatedTokens = _weights.Tokenize(builder.ToString(), false, true, Encoding.UTF8).Length;
            }
            catch (Exception exception)
            {
                throw LlamaSharpExceptionMapper.Map(exception, _artifact, "token usage");
            }

            return new GenerationResult(builder.ToString(), finishReason, new TokenUsage(promptTokens, generatedTokens));
        }

        public async IAsyncEnumerable<GenerationChunk> StreamAsync(
            TextGenerationRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            using (CancellationTokenSource? timeoutSource = CreateTimeoutSource(request.Options.Timeout, cancellationToken))
            {
                CancellationToken operationToken = timeoutSource?.Token ?? cancellationToken;
                if (operationToken.IsCancellationRequested)
                {
                    yield return new GenerationChunk(0, string.Empty, finishReason: GenerationFinishReason.Cancelled);
                    yield break;
                }

                if (!await _gate.WaitAsync(0).ConfigureAwait(false))
                {
                    throw new DeploySharpException(
                        DeploySharpErrorCodes.LanguageModelSessionBusy,
                        "The language-model session is single-writer and already has an active operation.",
                        backendId: Metadata.Backend.Id,
                        modelId: _artifact.ModelId);
                }

                bool entered = true;
                GenerationFinishReason? terminalReason = null;
                int sequenceIndex = 0;
                try
                {
                    ThrowIfDisposed();
                    var generatedText = new StringBuilder();
                    var inferenceParams = CreateInferenceParams(request.Options);
                    IAsyncEnumerator<string> enumerator = _executor.InferAsync(request.Prompt, inferenceParams, operationToken).GetAsyncEnumerator(operationToken);
                    try
                    {
                        while (true)
                        {
                            bool moved;
                            try
                            {
                                moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
                            {
                                terminalReason = GenerationFinishReason.Cancelled;
                                break;
                            }
                            catch (Exception exception) when (operationToken.IsCancellationRequested && ContainsCancellation(exception))
                            {
                                terminalReason = GenerationFinishReason.Cancelled;
                                break;
                            }
                            catch (Exception exception)
                            {
                                throw LlamaSharpExceptionMapper.Map(exception, _artifact, "stream generation");
                            }

                            if (!moved) break;
                            string text = enumerator.Current ?? string.Empty;
                            generatedText.Append(text);
                            yield return new GenerationChunk(sequenceIndex++, text);
                        }
                    }
                    finally
                    {
                        try
                        {
                            await enumerator.DisposeAsync().ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
                        {
                            terminalReason = GenerationFinishReason.Cancelled;
                        }
                        catch (Exception exception) when (operationToken.IsCancellationRequested && ContainsCancellation(exception))
                        {
                            terminalReason = GenerationFinishReason.Cancelled;
                        }
                    }

                    if (!terminalReason.HasValue)
                    {
                        try
                        {
                            terminalReason = DetermineFinishReason(generatedText.ToString(), request.Options, operationToken);
                        }
                        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
                        {
                            terminalReason = GenerationFinishReason.Cancelled;
                        }
                        catch (Exception exception) when (operationToken.IsCancellationRequested && ContainsCancellation(exception))
                        {
                            terminalReason = GenerationFinishReason.Cancelled;
                        }
                        catch (Exception exception)
                        {
                            throw LlamaSharpExceptionMapper.Map(exception, _artifact, "finish reason");
                        }
                    }
                }
                finally
                {
                    if (entered) _gate.Release();
                }

                yield return new GenerationChunk(sequenceIndex, string.Empty, finishReason: terminalReason ?? GenerationFinishReason.Error);
            }
        }

        public async Task<EmbeddingResult> EmbedAsync(TextEmbeddingRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if ((Metadata.Capabilities & LanguageModelCapabilities.Embeddings) == 0)
            {
                throw new DeploySharpException(DeploySharpErrorCodes.LanguageModelCapabilityUnavailable, "The loaded GGUF model does not expose embeddings.", backendId: Metadata.Backend.Id, modelId: _artifact.ModelId);
            }

            ThrowIfDisposed();
            using (CancellationTokenSource? timeoutSource = CreateTimeoutSource(request.Timeout, cancellationToken))
            {
                CancellationToken operationToken = timeoutSource?.Token ?? cancellationToken;
                operationToken.ThrowIfCancellationRequested();
                if (!await _gate.WaitAsync(0).ConfigureAwait(false))
                {
                    throw new DeploySharpException(
                        DeploySharpErrorCodes.LanguageModelSessionBusy,
                        "The language-model session is single-writer and already has an active operation.",
                        backendId: Metadata.Backend.Id,
                        modelId: _artifact.ModelId);
                }

                bool entered = true;
                try
                {
                    ThrowIfDisposed();
                    try
                    {
                        // LLamaEmbedder owns a mutable context, so the same session gate protects all calls. / LLamaEmbedder 持有可变上下文，因此所有调用共享同一个会话锁。
                        IReadOnlyList<float[]> values = await _embedder!.GetEmbeddings(request.Text, operationToken).ConfigureAwait(false);
                        if (values.Count != 1)
                        {
                            throw new DeploySharpException(DeploySharpErrorCodes.LanguageModelCapabilityUnavailable, "The configured pooling mode did not produce exactly one embedding vector.", backendId: Metadata.Backend.Id, modelId: _artifact.ModelId);
                        }

                        float[] vector = (float[])values[0].Clone();
                        if (request.Normalize) Normalize(vector);
                        return new EmbeddingResult(vector, IsUnitLength(vector));
                    }
                    catch (Exception exception)
                    {
                        throw LlamaSharpExceptionMapper.Map(exception, _artifact, "embedding");
                    }
                }
                finally
                {
                    if (entered) _gate.Release();
                }
            }
        }

        public void Dispose()
        {
            lock (_disposeSync)
            {
                if (_disposed) return;
                // Disposal waits for the active operation because every native context belongs to this session. / 释放会等待活动操作，因为所有原生上下文都属于当前会话。
                _gate.Wait();
                try
                {
                    _disposed = true;
                    try
                    {
                        _embedder?.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            _executor.Context.Dispose();
                        }
                        finally
                        {
                            _weights.Dispose();
                        }
                    }
                }
                finally
                {
                    _gate.Release();
                    _gate.Dispose();
                }
            }
        }

        private static InferenceParams CreateInferenceParams(GenerationOptions options)
        {
            var pipeline = new DefaultSamplingPipeline
            {
                Temperature = options.Temperature,
                TopP = options.TopP,
                TopK = options.TopK
            };
            if (options.Seed.HasValue) pipeline.Seed = unchecked((uint)options.Seed.Value);
            return new InferenceParams
            {
                MaxTokens = options.MaxTokens,
                AntiPrompts = options.StopSequences,
                SamplingPipeline = pipeline
            };
        }

        private GenerationFinishReason DetermineFinishReason(string generatedText, GenerationOptions options, CancellationToken operationToken)
        {
            if (operationToken.IsCancellationRequested) return GenerationFinishReason.Cancelled;
            for (int index = 0; index < options.StopSequences.Count; index++)
            {
                if (generatedText.IndexOf(options.StopSequences[index], StringComparison.Ordinal) >= 0) return GenerationFinishReason.StopSequence;
            }

            int tokenCount = _weights.Tokenize(generatedText, false, true, Encoding.UTF8).Length;
            return tokenCount >= options.MaxTokens ? GenerationFinishReason.MaxTokens : GenerationFinishReason.EndOfSequence;
        }

        private static CancellationTokenSource? CreateTimeoutSource(TimeSpan? timeout, CancellationToken cancellationToken)
        {
            if (!timeout.HasValue) return null;
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            source.CancelAfter(timeout.Value);
            return source;
        }

        private static void Normalize(float[] values)
        {
            double sum = 0;
            for (int index = 0; index < values.Length; index++) sum += values[index] * values[index];
            double length = Math.Sqrt(sum);
            if (length <= double.Epsilon) return;
            for (int index = 0; index < values.Length; index++) values[index] = (float)(values[index] / length);
        }

        private static bool IsUnitLength(float[] values)
        {
            double sum = 0;
            for (int index = 0; index < values.Length; index++) sum += values[index] * values[index];
            return Math.Abs(Math.Sqrt(sum) - 1d) <= 0.0001d;
        }

        private static bool ContainsCancellation(Exception exception)
        {
            Exception? current = exception;
            while (current != null)
            {
                if (current is OperationCanceledException) return true;
                current = current.InnerException;
            }

            return false;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LlamaSharpSession));
        }
    }
}
