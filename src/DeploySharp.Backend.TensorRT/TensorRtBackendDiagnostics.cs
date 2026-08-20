using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Stable diagnostics emitted by the managed TensorRT adapter. / 托管 TensorRT 适配器发出的稳定诊断代码。</summary>
    public static class TensorRtErrorCodes
    {
        /// <summary>Indicates an invalid managed backend configuration. / 表示托管后端配置无效。</summary>
        public const string ConfigurationInvalid = "DS-TRT-5001";
        /// <summary>Indicates an invalid serialized TensorRT engine artifact. / 表示序列化 TensorRT 引擎工件无效。</summary>
        public const string ModelArtifactInvalid = "DS-TRT-5002";
        /// <summary>Indicates an invalid tensor contract. / 表示张量合同无效。</summary>
        public const string TensorInvalid = "DS-TRT-5003";
        /// <summary>Indicates an unsupported tensor element type. / 表示张量元素类型不受支持。</summary>
        public const string ElementTypeUnsupported = "DS-TRT-5004";
        /// <summary>Indicates an unavailable consumer-owned native runtime. / 表示调用方持有的原生运行时不可用。</summary>
        public const string NativeRuntimeUnavailable = "DS-TRT-5005";
        /// <summary>Indicates a native inference failure. / 表示原生推理失败。</summary>
        public const string InferenceFailed = "DS-TRT-5006";
        /// <summary>Indicates use after disposal. / 表示对象释放后仍被使用。</summary>
        public const string ObjectDisposed = "DS-TRT-5007";
        /// <summary>Indicates an invalid caller-owned ONNX artifact. / 表示调用方持有的 ONNX 工件无效。</summary>
        public const string OnnxModelInvalid = "DS-TRT-5008";
        /// <summary>Indicates an ONNX parser failure. / 表示 ONNX 解析失败。</summary>
        public const string OnnxParseFailed = "DS-TRT-5009";
        /// <summary>Indicates a TensorRT engine build failure. / 表示 TensorRT 引擎构建失败。</summary>
        public const string EngineBuildFailed = "DS-TRT-5010";
        /// <summary>Indicates an invalid generated engine output. / 表示生成的引擎输出无效。</summary>
        public const string EngineOutputInvalid = "DS-TRT-5011";
        /// <summary>Indicates an invalid managed CUDA execution contract. / 表示托管 CUDA 执行合同无效。</summary>
        public const string CudaContractInvalid = "DS-TRT-5012";
        /// <summary>Indicates a CUDA runtime compilation or module-load failure. / 表示 CUDA 运行时编译或模块加载失败。</summary>
        public const string CudaCompilationFailed = "DS-TRT-5013";
        /// <summary>Indicates a CUDA kernel launch failure. / 表示 CUDA 内核启动失败。</summary>
        public const string CudaLaunchFailed = "DS-TRT-5014";
        /// <summary>Indicates an invalid local cache configuration or input. / 表示本地缓存配置或输入无效。</summary>
        public const string ExternalCacheConfigurationInvalid = "DS-TRT-5015";
        /// <summary>Indicates a rejected local cache entry. / 表示本地缓存条目被拒绝。</summary>
        public const string ExternalCacheEntryRejected = "DS-TRT-5016";
        /// <summary>Indicates conflicting valid bytes for one local cache key. / 表示同一本地缓存键出现冲突的有效字节。</summary>
        public const string ExternalCacheConflict = "DS-TRT-5017";
        /// <summary>Indicates a local cache I/O failure. / 表示本地缓存 I/O 失败。</summary>
        public const string ExternalCacheIoFailed = "DS-TRT-5018";
    }

    /// <summary>Represents a diagnosable TensorRT adapter failure without exposing native ownership to Core. / 表示不向 Core 暴露原生所有权的可诊断 TensorRT 适配器故障。</summary>
    public sealed class TensorRtBackendException : DeploySharpException
    {
        /// <summary>Initializes a diagnosable TensorRT adapter exception. / 初始化可诊断的 TensorRT 适配器异常。</summary>
        public TensorRtBackendException(
            string errorCode,
            string message,
            System.Exception? innerException = null,
            ModelId? modelId = null,
            string? tensorName = null,
            string? operation = null,
            string? technicalDetails = null)
            : base(errorCode, message, innerException, TensorRtBackendProvider.BackendId, modelId, technicalDetails)
        {
            TensorName = tensorName;
            Operation = operation;
        }

        /// <summary>Gets the affected tensor name, when known. / 获取已知的受影响张量名称。</summary>
        public string? TensorName { get; }
        /// <summary>Gets the stable adapter operation name, when known. / 获取已知的稳定适配器操作名称。</summary>
        public string? Operation { get; }
    }
}
