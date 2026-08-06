using System;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines stable Visual-domain diagnostic codes. / 定义稳定的 Visual 领域诊断码。</summary>
    public static class VisualErrorCodes
    {
        /// <summary>A prepared input is invalid. / 已准备输入无效。</summary>
        public const string InputInvalid = "DS-VISUAL-1001";
        /// <summary>A coordinate transform is invalid. / 坐标变换无效。</summary>
        public const string TransformInvalid = "DS-VISUAL-1002";
        /// <summary>A model profile is invalid or incompatible. / 模型 Profile 无效或不兼容。</summary>
        public const string ProfileInvalid = "DS-VISUAL-2001";
        /// <summary>A profile identifier was registered more than once. / Profile 标识符被重复注册。</summary>
        public const string ProfileAlreadyRegistered = "DS-VISUAL-2002";
        /// <summary>No registered profile matches the request. / 没有已注册 Profile 匹配请求。</summary>
        public const string ProfileNotFound = "DS-VISUAL-2003";
        /// <summary>An inference tensor binding or shape is invalid. / 推理张量绑定或形状无效。</summary>
        public const string TensorInvalid = "DS-VISUAL-3001";
        /// <summary>Visual result decoding failed. / Visual 结果解码失败。</summary>
        public const string DecodeFailed = "DS-VISUAL-3002";
        /// <summary>A requested Visual capability is not available. / 请求的 Visual 能力不可用。</summary>
        public const string CapabilityUnavailable = "DS-VISUAL-3003";
        /// <summary>A Visual inference operation failed. / Visual 推理操作失败。</summary>
        public const string InferenceFailed = "DS-VISUAL-4001";
        /// <summary>A Visual operation was cancelled by the caller. / Visual 操作被调用方取消。</summary>
        public const string Cancelled = "DS-VISUAL-4002";
        /// <summary>A Visual operation exceeded its configured timeout. / Visual 操作超过配置的超时时间。</summary>
        public const string Timeout = "DS-VISUAL-4003";
        /// <summary>An OCR pipeline stage failed. / OCR Pipeline 阶段失败。</summary>
        public const string OcrPipelineFailed = "DS-VISUAL-4101";
        /// <summary>An OCR input, batch, output, or workspace limit was exceeded. / OCR 输入、批次、输出或工作区超出限制。</summary>
        public const string OcrLimitExceeded = "DS-VISUAL-4102";
        /// <summary>An anomaly tensor or result contract is invalid. / 异常张量或结果契约无效。</summary>
        public const string AnomalyContractInvalid = "DS-VISUAL-4201";
        /// <summary>An anomaly map, workspace, or output limit was exceeded. / 异常图、工作区或输出超出限制。</summary>
        public const string AnomalyLimitExceeded = "DS-VISUAL-4202";
        /// <summary>A requested anomaly postprocessing capability is unavailable. / 请求的异常后处理能力不可用。</summary>
        public const string AnomalyCapabilityUnavailable = "DS-VISUAL-4203";
        /// <summary>The Visual object has already been disposed. / Visual 对象已被释放。</summary>
        public const string ObjectDisposed = "DS-VISUAL-5001";
        /// <summary>An OCR orientation contract is invalid. / OCR 方向契约无效。</summary>
        public const string OcrOrientationContractInvalid = "DS-VISUAL-4301";
        /// <summary>An OCR orientation limit was exceeded. / OCR 方向限制超出。</summary>
        public const string OcrOrientationLimitExceeded = "DS-VISUAL-4302";
        /// <summary>OCR orientation correction is unavailable. / OCR 方向纠正能力不可用。</summary>
        public const string OcrOrientationCapabilityUnavailable = "DS-VISUAL-4303";
        /// <summary>A YOLO model, export, tensor, or decoding contract is invalid. / YOLO 模型、导出、张量或解码合同无效。</summary>
        public const string YoloContractInvalid = "DS-VISUAL-4401";
        /// <summary>A YOLO candidate, result, or workspace limit was exceeded. / YOLO 候选、结果或工作区限制超出。</summary>
        public const string YoloLimitExceeded = "DS-VISUAL-4402";
    }

    /// <summary>Represents a diagnosable Visual-domain failure while preserving the original exception. / 表示可诊断的 Visual 领域故障，同时保留原始异常。</summary>
    public sealed class VisualException : DeploySharpException
    {
        /// <summary>Initializes a Visual exception. / 初始化 Visual 异常。</summary>
        public VisualException(
            string errorCode,
            string message,
            Exception? innerException = null,
            string? profileId = null,
            string? tensorName = null,
            BackendId? backendId = null,
            ModelId? modelId = null,
            string? technicalDetails = null)
            : base(errorCode, message, innerException, backendId, modelId, technicalDetails)
        {
            ProfileId = profileId;
            TensorName = tensorName;
        }

        /// <summary>Gets the associated Visual profile identifier. / 获取关联的 Visual Profile 标识符。</summary>
        public string? ProfileId { get; }

        /// <summary>Gets the associated tensor name. / 获取关联的张量名称。</summary>
        public string? TensorName { get; }
    }

    internal static class VisualGuard
    {
        public static string Identifier(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A stable identifier is required.", profileId: value);
            string normalized = value!.Trim().ToLowerInvariant();
            for (int index = 0; index < normalized.Length; index++)
            {
                char current = normalized[index];
                bool accepted = (current >= 'a' && current <= 'z') || (current >= '0' && current <= '9') || current == '-' || current == '_' || current == '.' || current == '/';
                if (!accepted) throw new VisualException(VisualErrorCodes.ProfileInvalid, "An identifier contains unsupported characters.", profileId: normalized, technicalDetails: parameterName);
            }

            return normalized;
        }

        public static void Finite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.TransformInvalid, "A coordinate value must be finite.", technicalDetails: parameterName);
        }
    }
}
