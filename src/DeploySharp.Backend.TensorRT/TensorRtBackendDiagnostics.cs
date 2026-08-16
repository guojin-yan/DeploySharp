using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Stable diagnostics emitted by the managed TensorRT adapter.</summary>
    public static class TensorRtErrorCodes
    {
        /// <summary>Indicates an invalid managed backend configuration.</summary>
        public const string ConfigurationInvalid = "DS-TRT-5001";
        /// <summary>Indicates an invalid serialized TensorRT engine artifact.</summary>
        public const string ModelArtifactInvalid = "DS-TRT-5002";
        /// <summary>Indicates an invalid tensor contract.</summary>
        public const string TensorInvalid = "DS-TRT-5003";
        /// <summary>Indicates an unsupported tensor element type.</summary>
        public const string ElementTypeUnsupported = "DS-TRT-5004";
        /// <summary>Indicates an unavailable consumer-owned native runtime.</summary>
        public const string NativeRuntimeUnavailable = "DS-TRT-5005";
        /// <summary>Indicates a native inference failure.</summary>
        public const string InferenceFailed = "DS-TRT-5006";
        /// <summary>Indicates use after disposal.</summary>
        public const string ObjectDisposed = "DS-TRT-5007";
        /// <summary>Indicates an invalid caller-owned ONNX artifact.</summary>
        public const string OnnxModelInvalid = "DS-TRT-5008";
        /// <summary>Indicates an ONNX parser failure.</summary>
        public const string OnnxParseFailed = "DS-TRT-5009";
        /// <summary>Indicates a TensorRT engine build failure.</summary>
        public const string EngineBuildFailed = "DS-TRT-5010";
        /// <summary>Indicates an invalid generated engine output.</summary>
        public const string EngineOutputInvalid = "DS-TRT-5011";
        /// <summary>Indicates an invalid managed CUDA execution contract.</summary>
        public const string CudaContractInvalid = "DS-TRT-5012";
        /// <summary>Indicates a CUDA runtime compilation or module-load failure.</summary>
        public const string CudaCompilationFailed = "DS-TRT-5013";
        /// <summary>Indicates a CUDA kernel launch failure.</summary>
        public const string CudaLaunchFailed = "DS-TRT-5014";
        /// <summary>Indicates an invalid local cache configuration or input.</summary>
        public const string ExternalCacheConfigurationInvalid = "DS-TRT-5015";
        /// <summary>Indicates a rejected local cache entry.</summary>
        public const string ExternalCacheEntryRejected = "DS-TRT-5016";
        /// <summary>Indicates conflicting valid bytes for one local cache key.</summary>
        public const string ExternalCacheConflict = "DS-TRT-5017";
        /// <summary>Indicates a local cache I/O failure.</summary>
        public const string ExternalCacheIoFailed = "DS-TRT-5018";
    }

    /// <summary>Represents a diagnosable TensorRT adapter failure without exposing native ownership to Core.</summary>
    public sealed class TensorRtBackendException : DeploySharpException
    {
        /// <summary>Initializes a diagnosable TensorRT adapter exception.</summary>
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

        /// <summary>Gets the affected tensor name, when known.</summary>
        public string? TensorName { get; }
        /// <summary>Gets the stable adapter operation name, when known.</summary>
        public string? Operation { get; }
    }
}
