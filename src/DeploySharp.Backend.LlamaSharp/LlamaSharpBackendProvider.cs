using System;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.LlamaSharp.Internal;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.LLM.Prompt;
using JYPPX.DeploySharp.Models;
using LLama;
using LLama.Common;
using LLama.Native;

namespace JYPPX.DeploySharp.Backends.LlamaSharp
{
    /// <summary>Loads GGUF models through the managed LLamaSharp adapter. / 通过托管 LLamaSharp 适配器加载 GGUF 模型。</summary>
    public sealed class LlamaSharpBackendProvider : ILanguageModelProvider
    {
        /// <summary>Gets the stable backend identifier. / 获取稳定后端标识。</summary>
        public static BackendId BackendId { get; } = new BackendId("llamasharp");

        private readonly LlamaSharpOptions _options;
        private readonly IPromptFormatter _promptFormatter;
        private bool _disposed;

        /// <summary>Initializes a LLamaSharp provider. / 初始化 LLamaSharp 提供程序。</summary>
        public LlamaSharpBackendProvider(LlamaSharpOptions? options = null, IPromptFormatter? promptFormatter = null)
        {
            _options = options ?? LlamaSharpOptions.Default;
            _promptFormatter = promptFormatter ?? new PlainTextPromptFormatter();
            Descriptor = new BackendDescriptor(
                BackendId,
                "LLamaSharp",
                "0.27.0",
                BackendCapabilities.TextGeneration | BackendCapabilities.Embeddings | BackendCapabilities.AsynchronousExecution,
                new[] { "gguf" });
        }

        /// <summary>Gets the LLamaSharp backend descriptor and verified managed capabilities. / 获取 LLamaSharp 后端描述和已验证的托管能力。</summary>
        public BackendDescriptor Descriptor { get; }

        /// <summary>Determines whether this provider accepts the GGUF artifact and requested capabilities. / 判断当前提供程序是否接受 GGUF 工件和请求的能力。</summary>
        public bool CanCreate(ModelArtifact artifact, LanguageModelRequest request)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            if (request.BackendId.HasValue && request.BackendId.Value != BackendId) return false;
            if (!string.Equals(artifact.Format, "gguf", StringComparison.Ordinal)) return false;
            return Descriptor.Supports(ToBackendCapabilities(request.RequiredCapabilities));
        }

        /// <summary>Validates and loads a GGUF model into a caller-owned session. / 验证并加载 GGUF 模型，返回由调用方持有的会话。</summary>
        public ILanguageModelSession CreateSession(ModelArtifact artifact, LanguageModelRequest request, LanguageModelSessionOptions? options = null)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            if (!CanCreate(artifact, request)) throw new BackendNotCompatibleException(artifact.ModelId, request.BackendId ?? BackendId);
            LanguageModelSessionOptions sessionOptions = options ?? LanguageModelSessionOptions.Default;
            if (sessionOptions.CoreOptions.MaxConcurrency != 1)
            {
                throw new DeploySharpException(DeploySharpErrorCodes.BackendNotCompatible, "LLamaSharp sessions serialize access and require MaxConcurrency=1.", backendId: BackendId, modelId: artifact.ModelId);
            }

            string device;
            try
            {
                device = LlamaSharpOptions.NormalizeDevice(request.Device ?? _options.Device);
            }
            catch (ArgumentException exception)
            {
                throw new DeploySharpException(DeploySharpErrorCodes.LanguageModelFailed, "The LLamaSharp device configuration is invalid.", exception, BackendId, artifact.ModelId, exception.ToString());
            }

            GgufModelArtifactValidator.Validate(artifact);
            LLamaWeights? weights = null;
            try
            {
                var modelParams = CreateModelParams(artifact.Location, embeddings: false);
                // Native loading is intentionally delayed until session creation. / 原生加载被有意延迟到创建会话时。
                weights = LLamaWeights.LoadFromFile(modelParams);
                var session = new LlamaSharpSession(artifact, Descriptor, weights, modelParams, CreateModelParams(artifact.Location, embeddings: true), _promptFormatter, device);
                weights = null;
                if ((request.RequiredCapabilities & session.Metadata.Capabilities) != request.RequiredCapabilities)
                {
                    Exception? capabilityError = session.EmbeddingInitializationError;
                    session.Dispose();
                    throw new DeploySharpException(
                        DeploySharpErrorCodes.LanguageModelCapabilityUnavailable,
                        "The loaded GGUF model does not provide every requested LLM capability.",
                        capabilityError,
                        BackendId,
                        artifact.ModelId,
                        capabilityError?.ToString());
                }

                return session;
            }
            catch (Exception exception)
            {
                weights?.Dispose();
                throw LlamaSharpExceptionMapper.Map(exception, artifact, "load", loading: true);
            }
        }

        /// <summary>Disposes this provider without disposing sessions already returned to callers. / 释放当前提供程序，但不释放已返回给调用方的会话。</summary>
        /// <remarks>Provider disposal is idempotent. / 提供程序可重复释放。</remarks>
        public void Dispose()
        {
            _disposed = true;
        }

        private ModelParams CreateModelParams(string path, bool embeddings)
        {
            return new ModelParams(path)
            {
                ContextSize = _options.ContextSize,
                GpuLayerCount = _options.GpuLayerCount,
                MainGpu = _options.MainGpu,
                Threads = _options.Threads,
                BatchThreads = _options.BatchThreads,
                BatchSize = _options.BatchSize,
                UBatchSize = _options.BatchSize,
                SeqMax = _options.SequenceCount,
                UseMemorymap = _options.UseMemoryMap,
                UseMemoryLock = _options.UseMemoryLock,
                Embeddings = embeddings,
                PoolingType = embeddings ? ToPoolingType(_options.EmbeddingPooling) : LLamaPoolingType.Unspecified,
                AttentionType = embeddings ? LLamaAttentionType.NonCausal : LLamaAttentionType.Unspecified
            };
        }

        private static LLamaPoolingType ToPoolingType(LlamaEmbeddingPooling pooling)
        {
            switch (pooling)
            {
                case LlamaEmbeddingPooling.ModelDefault: return LLamaPoolingType.Unspecified;
                case LlamaEmbeddingPooling.Mean: return LLamaPoolingType.Mean;
                case LlamaEmbeddingPooling.ClassificationToken: return LLamaPoolingType.CLS;
                case LlamaEmbeddingPooling.LastToken: return LLamaPoolingType.Last;
                default: throw new ArgumentOutOfRangeException(nameof(pooling));
            }
        }

        private static BackendCapabilities ToBackendCapabilities(LanguageModelCapabilities capabilities)
        {
            BackendCapabilities value = BackendCapabilities.None;
            if ((capabilities & LanguageModelCapabilities.TextGeneration) != 0) value |= BackendCapabilities.TextGeneration;
            if ((capabilities & LanguageModelCapabilities.Streaming) != 0) value |= BackendCapabilities.AsynchronousExecution;
            if ((capabilities & LanguageModelCapabilities.Embeddings) != 0) value |= BackendCapabilities.Embeddings;
            return value;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LlamaSharpBackendProvider));
        }
    }
}
