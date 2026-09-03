using System;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime
{
    /// <summary>Defines the verified ONNX Runtime graph-optimization levels without exposing vendor types. / 定义已验证的 ONNX Runtime 图优化级别，且不公开厂商类型。</summary>
    public enum OnnxRuntimeGraphOptimization
    {
        /// <summary>Disables graph optimization. / 禁用图优化。</summary>
        Disabled = 0,
        /// <summary>Enables basic safe rewrites. / 启用基础安全重写。</summary>
        Basic = 1,
        /// <summary>Enables extended rewrites. / 启用扩展重写。</summary>
        Extended = 2,
        /// <summary>Enables all available rewrites. / 启用全部可用重写。</summary>
        All = 3
    }

    /// <summary>Defines sequential or parallel graph execution. / 定义顺序或并行图执行。</summary>
    public enum OnnxRuntimeExecutionMode
    {
        /// <summary>Executes graph nodes sequentially. / 顺序执行图节点。</summary>
        Sequential = 0,
        /// <summary>Allows independent graph branches to execute in parallel. / 允许独立图分支并行执行。</summary>
        Parallel = 1
    }

    /// <summary>Defines the minimum ONNX Runtime native log severity. / 定义 ONNX Runtime 原生日志最低严重级别。</summary>
    public enum OnnxRuntimeLogSeverity
    {
        /// <summary>Verbose native diagnostics. / 详细原生诊断。</summary>
        Verbose = 0,
        /// <summary>Informational native diagnostics. / 信息级原生诊断。</summary>
        Information = 1,
        /// <summary>Warnings and errors. / 警告与错误。</summary>
        Warning = 2,
        /// <summary>Errors only. / 仅错误。</summary>
        Error = 3,
        /// <summary>Fatal errors only. / 仅致命错误。</summary>
        Fatal = 4
    }

    /// <summary>Defines the ONNX Runtime execution provider selected for a session. / 定义会话选择的 ONNX Runtime 执行提供程序。</summary>
    public enum OnnxRuntimeExecutionProvider
    {
        /// <summary>Uses the official CPU execution provider. / 使用官方 CPU 执行提供程序。</summary>
        Cpu = 0,
        /// <summary>Uses the CUDA execution provider on the selected GPU. / 使用所选 GPU 上的 CUDA 执行提供程序。</summary>
        Cuda = 1
    }

    /// <summary>Contains immutable session settings for the ONNX Runtime adapter. / 包含 ONNX Runtime 适配器不可变的会话设置。</summary>
    public sealed class OnnxRuntimeOptions
    {
        /// <summary>Initializes validated execution settings. / 初始化经过验证的执行设置。</summary>
        public OnnxRuntimeOptions(
            int intraOpThreads = 0,
            int interOpThreads = 0,
            OnnxRuntimeGraphOptimization graphOptimization = OnnxRuntimeGraphOptimization.All,
            OnnxRuntimeExecutionMode executionMode = OnnxRuntimeExecutionMode.Sequential,
            bool enableMemoryPattern = true,
            bool enableCpuMemoryArena = true,
            OnnxRuntimeLogSeverity logSeverity = OnnxRuntimeLogSeverity.Warning,
            string? logId = null,
            string? profilingOutputPathPrefix = null,
            OnnxRuntimeExecutionProvider executionProvider = OnnxRuntimeExecutionProvider.Cpu,
            int cudaDeviceId = 0)
        {
            if (intraOpThreads < 0) throw new ArgumentOutOfRangeException(nameof(intraOpThreads));
            if (interOpThreads < 0) throw new ArgumentOutOfRangeException(nameof(interOpThreads));
            if (!Enum.IsDefined(typeof(OnnxRuntimeGraphOptimization), graphOptimization)) throw new ArgumentOutOfRangeException(nameof(graphOptimization));
            if (!Enum.IsDefined(typeof(OnnxRuntimeExecutionMode), executionMode)) throw new ArgumentOutOfRangeException(nameof(executionMode));
            if (!Enum.IsDefined(typeof(OnnxRuntimeLogSeverity), logSeverity)) throw new ArgumentOutOfRangeException(nameof(logSeverity));
            if (!Enum.IsDefined(typeof(OnnxRuntimeExecutionProvider), executionProvider)) throw new ArgumentOutOfRangeException(nameof(executionProvider));
            if (cudaDeviceId < 0) throw new ArgumentOutOfRangeException(nameof(cudaDeviceId));
            IntraOpThreads = intraOpThreads;
            InterOpThreads = interOpThreads;
            GraphOptimization = graphOptimization;
            ExecutionMode = executionMode;
            EnableMemoryPattern = enableMemoryPattern;
            EnableCpuMemoryArena = enableCpuMemoryArena;
            LogSeverity = logSeverity;
            LogId = string.IsNullOrWhiteSpace(logId) ? null : logId;
            ProfilingOutputPathPrefix = string.IsNullOrWhiteSpace(profilingOutputPathPrefix) ? null : profilingOutputPathPrefix;
            ExecutionProvider = executionProvider;
            CudaDeviceId = cudaDeviceId;
        }

        /// <summary>Gets the intra-operation thread count, where zero lets ONNX Runtime choose. / 获取算子内线程数；零表示由 ONNX Runtime 选择。</summary>
        public int IntraOpThreads { get; }
        /// <summary>Gets the inter-operation thread count, where zero lets ONNX Runtime choose. / 获取算子间线程数；零表示由 ONNX Runtime 选择。</summary>
        public int InterOpThreads { get; }
        /// <summary>Gets the graph-optimization level. / 获取图优化级别。</summary>
        public OnnxRuntimeGraphOptimization GraphOptimization { get; }
        /// <summary>Gets the graph execution mode. / 获取图执行模式。</summary>
        public OnnxRuntimeExecutionMode ExecutionMode { get; }
        /// <summary>Gets whether memory-pattern optimization is enabled. / 获取是否启用内存模式优化。</summary>
        public bool EnableMemoryPattern { get; }
        /// <summary>Gets whether the CPU memory arena is enabled. / 获取是否启用 CPU 内存池。</summary>
        public bool EnableCpuMemoryArena { get; }
        /// <summary>Gets native log severity. / 获取原生日志严重级别。</summary>
        public OnnxRuntimeLogSeverity LogSeverity { get; }
        /// <summary>Gets the optional non-sensitive native log identifier. / 获取可选且非敏感的原生日志标识。</summary>
        public string? LogId { get; }
        /// <summary>Gets the optional profiling output path prefix used only when Core profiling is enabled. / 获取仅在启用 Core Profiling 时使用的可选性能分析输出路径前缀。</summary>
        public string? ProfilingOutputPathPrefix { get; }

        /// <summary>Gets the selected ONNX Runtime execution provider. / 获取所选 ONNX Runtime 执行提供程序。</summary>
        public OnnxRuntimeExecutionProvider ExecutionProvider { get; }

        /// <summary>Gets the CUDA device ordinal used when <see cref="ExecutionProvider"/> is <see cref="OnnxRuntimeExecutionProvider.Cuda"/>. / 获取使用 CUDA 时的设备序号。</summary>
        public int CudaDeviceId { get; }

        /// <summary>Gets default CPU settings. / 获取默认 CPU 设置。</summary>
        public static OnnxRuntimeOptions Default { get; } = new OnnxRuntimeOptions();
    }
}
