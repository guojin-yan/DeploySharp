using System;

namespace JYPPX.DeploySharp.Backends.LlamaSharp
{
    /// <summary>Configures LLamaSharp model and context creation without leaking vendor types. / 配置 LLamaSharp 模型与上下文创建，且不泄漏厂商类型。</summary>
    public sealed class LlamaSharpOptions
    {
        /// <summary>Initializes backend-specific options. / 初始化后端专用选项。</summary>
        public LlamaSharpOptions(
            uint? contextSize = null,
            int gpuLayerCount = 0,
            int mainGpu = 0,
            int? threads = null,
            int? batchThreads = null,
            uint batchSize = 512,
            uint sequenceCount = 1,
            bool useMemoryMap = true,
            bool useMemoryLock = false,
            LlamaEmbeddingPooling embeddingPooling = LlamaEmbeddingPooling.Mean,
            string device = "cpu")
        {
            if (contextSize.HasValue && contextSize.Value == 0) throw new ArgumentOutOfRangeException(nameof(contextSize));
            if (gpuLayerCount < 0) throw new ArgumentOutOfRangeException(nameof(gpuLayerCount));
            if (mainGpu < 0) throw new ArgumentOutOfRangeException(nameof(mainGpu));
            if (threads.HasValue && threads.Value <= 0) throw new ArgumentOutOfRangeException(nameof(threads));
            if (batchThreads.HasValue && batchThreads.Value <= 0) throw new ArgumentOutOfRangeException(nameof(batchThreads));
            if (batchSize == 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
            if (sequenceCount == 0) throw new ArgumentOutOfRangeException(nameof(sequenceCount));
            Device = NormalizeDevice(device);
            ContextSize = contextSize;
            GpuLayerCount = gpuLayerCount;
            MainGpu = mainGpu;
            Threads = threads;
            BatchThreads = batchThreads;
            BatchSize = batchSize;
            SequenceCount = sequenceCount;
            UseMemoryMap = useMemoryMap;
            UseMemoryLock = useMemoryLock;
            EmbeddingPooling = embeddingPooling;
        }

        /// <summary>Gets requested context length or the model default. / 获取请求的上下文长度或模型默认值。</summary>
        public uint? ContextSize { get; }
        /// <summary>Gets the number of model layers offloaded to a GPU backend. / 获取卸载到 GPU 后端的模型层数。</summary>
        public int GpuLayerCount { get; }
        /// <summary>Gets the main GPU index. / 获取主 GPU 索引。</summary>
        public int MainGpu { get; }
        /// <summary>Gets optional inference thread count. / 获取可选推理线程数。</summary>
        public int? Threads { get; }
        /// <summary>Gets optional batch thread count. / 获取可选批处理线程数。</summary>
        public int? BatchThreads { get; }
        /// <summary>Gets logical and physical batch size. / 获取逻辑和物理批处理大小。</summary>
        public uint BatchSize { get; }
        /// <summary>Gets maximum sequence count. / 获取最大序列数。</summary>
        public uint SequenceCount { get; }
        /// <summary>Gets whether memory mapping is enabled. / 获取是否启用内存映射。</summary>
        public bool UseMemoryMap { get; }
        /// <summary>Gets whether model pages should be locked in memory. / 获取是否将模型页锁定在内存中。</summary>
        public bool UseMemoryLock { get; }
        /// <summary>Gets embedding pooling mode. / 获取嵌入池化方式。</summary>
        public LlamaEmbeddingPooling EmbeddingPooling { get; }
        /// <summary>Gets the diagnostic device label. / 获取用于诊断的设备标签。</summary>
        public string Device { get; }

        /// <summary>Gets CPU-safe defaults; GPU offload is opt-in. / 获取 CPU 安全默认值，GPU 卸载需要显式启用。</summary>
        public static LlamaSharpOptions Default { get; } = new LlamaSharpOptions();

        internal static string NormalizeDevice(string? device)
        {
            if (string.IsNullOrWhiteSpace(device)) return "auto";
            string normalized = device!.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "auto":
                case "cpu":
                case "gpu":
                case "cuda":
                case "vulkan":
                    return normalized;
                default:
                    throw new ArgumentException("Device must be auto, cpu, gpu, cuda, or vulkan.", nameof(device));
            }
        }
    }
}
