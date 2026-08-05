using System;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime
{
    /// <summary>Defines stable diagnostics emitted by the ONNX Runtime adapter. / 定义 ONNX Runtime 适配器发出的稳定诊断码。</summary>
    public static class OnnxRuntimeErrorCodes
    {
        /// <summary>Backend configuration or device selection is invalid. / 后端配置或设备选择无效。</summary>
        public const string ConfigurationInvalid = "DS-ORT-5001";
        /// <summary>The ONNX model cannot be loaded. / 无法加载 ONNX 模型。</summary>
        public const string ModelLoadFailed = "DS-ORT-5002";
        /// <summary>A named tensor is missing, extra, or incompatible. / 命名张量缺失、多余或不兼容。</summary>
        public const string TensorInvalid = "DS-ORT-5003";
        /// <summary>A tensor element type is not supported by this adapter. / 此适配器不支持该张量元素类型。</summary>
        public const string ElementTypeUnsupported = "DS-ORT-5004";
        /// <summary>Native ONNX Runtime inference failed. / ONNX Runtime 原生推理失败。</summary>
        public const string InferenceFailed = "DS-ORT-5005";
        /// <summary>The inference operation was cancelled. / 推理操作已取消。</summary>
        public const string Cancelled = "DS-ORT-5006";
        /// <summary>The provider or session has already been disposed. / Provider 或会话已释放。</summary>
        public const string ObjectDisposed = "DS-ORT-5007";
        /// <summary>The requested execution provider is unavailable. / 请求的 Execution Provider 不可用。</summary>
        public const string ExecutionProviderUnavailable = "DS-ORT-5008";
    }

    /// <summary>Represents a diagnosable ONNX Runtime adapter failure without exposing vendor types. / 表示可诊断的 ONNX Runtime 适配器故障，且不公开厂商类型。</summary>
    public sealed class OnnxRuntimeBackendException : DeploySharpException
    {
        /// <summary>Initializes an ONNX Runtime backend exception. / 初始化 ONNX Runtime 后端异常。</summary>
        public OnnxRuntimeBackendException(string errorCode, string message, Exception? innerException = null, ModelId? modelId = null, string? tensorName = null, string? operation = null, string? technicalDetails = null)
            : base(errorCode, message, innerException, OnnxRuntimeBackendProvider.BackendId, modelId, technicalDetails)
        {
            TensorName = tensorName;
            Operation = operation;
        }

        /// <summary>Gets the affected model tensor name when known. / 获取已知的受影响模型张量名称。</summary>
        public string? TensorName { get; }
        /// <summary>Gets the stable operation name when known. / 获取已知的稳定操作名称。</summary>
        public string? Operation { get; }
    }
}
