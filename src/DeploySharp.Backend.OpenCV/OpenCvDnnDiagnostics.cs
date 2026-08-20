using System;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Defines stable OpenCV DNN backend error codes. / 定义稳定的 OpenCV DNN 后端错误码。</summary>
    public static class OpenCvDnnErrorCodes
    {
        /// <summary>The model contract or provider configuration is invalid. / 模型合同或 Provider 配置无效。</summary>
        public const string ConfigurationInvalid = "DS-OCV-8001";
        /// <summary>The model could not be loaded by OpenCV DNN. / OpenCV DNN 无法加载模型。</summary>
        public const string ModelLoadFailed = "DS-OCV-8002";
        /// <summary>A tensor violates the admitted static float32 image contract. / 张量违反已准入的静态 float32 图像合同。</summary>
        public const string TensorInvalid = "DS-OCV-8003";
        /// <summary>OpenCV DNN inference failed. / OpenCV DNN 推理失败。</summary>
        public const string InferenceFailed = "DS-OCV-8004";
        /// <summary>The operation was cancelled at a managed boundary. / 操作在托管边界被取消。</summary>
        public const string Cancelled = "DS-OCV-8005";
        /// <summary>The provider or session was disposed. / Provider 或 Session 已释放。</summary>
        public const string ObjectDisposed = "DS-OCV-8006";
    }

    /// <summary>Represents an OpenCV DNN adapter failure with stable diagnostics. / 表示带稳定诊断信息的 OpenCV DNN 适配器故障。</summary>
    public sealed class OpenCvDnnBackendException : DeploySharpException
    {
        /// <summary>Initializes an OpenCV DNN exception without logging. / 初始化 OpenCV DNN 异常且不写日志。</summary>
        public OpenCvDnnBackendException(string errorCode, string message, Exception? innerException = null, ModelId? modelId = null, string? tensorName = null, string? operation = null, string? technicalDetails = null)
            : base(errorCode, message, innerException, OpenCvDnnBackendProvider.BackendId, modelId, BuildDetails(tensorName, operation, technicalDetails))
        {
        }

        private static string? BuildDetails(string? tensorName, string? operation, string? details)
        {
            string value = string.Empty;
            if (!string.IsNullOrWhiteSpace(operation)) value = "operation=" + operation;
            if (!string.IsNullOrWhiteSpace(tensorName)) value += (value.Length == 0 ? string.Empty : ";") + "tensor=" + tensorName;
            if (!string.IsNullOrWhiteSpace(details)) value += (value.Length == 0 ? string.Empty : ";") + details;
            return value.Length == 0 ? null : value;
        }
    }
}
