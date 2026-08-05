using System;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OpenVINO
{
    /// <summary>Defines stable diagnostics emitted by the OpenVINO adapter. / 定义 OpenVINO 适配器发出的稳定诊断码。</summary>
    public static class OpenVinoErrorCodes
    {
        /// <summary>Backend configuration or device selection is invalid. / 后端配置或设备选择无效。</summary>
        public const string ConfigurationInvalid = "DS-OV-5101";
        /// <summary>The ONNX or IR model cannot be loaded or compiled. / 无法加载或编译 ONNX 或 IR 模型。</summary>
        public const string ModelLoadFailed = "DS-OV-5102";
        /// <summary>A named tensor is missing, extra, or incompatible. / 命名张量缺失、多余或不兼容。</summary>
        public const string TensorInvalid = "DS-OV-5103";
        /// <summary>A tensor element type is unsupported by this adapter. / 此适配器不支持该张量元素类型。</summary>
        public const string ElementTypeUnsupported = "DS-OV-5104";
        /// <summary>Native OpenVINO inference failed. / OpenVINO 原生推理失败。</summary>
        public const string InferenceFailed = "DS-OV-5105";
        /// <summary>The inference operation was cancelled. / 推理操作已取消。</summary>
        public const string Cancelled = "DS-OV-5106";
        /// <summary>The provider or session has already been disposed. / Provider 或会话已释放。</summary>
        public const string ObjectDisposed = "DS-OV-5107";
        /// <summary>The requested device or plug-in is unavailable. / 请求的设备或插件不可用。</summary>
        public const string DeviceUnavailable = "DS-OV-5108";
        /// <summary>An OpenVINO IR sidecar is missing or invalid. / OpenVINO IR 边文件缺失或无效。</summary>
        public const string IrSidecarInvalid = "DS-OV-5109";
    }

    /// <summary>Represents a diagnosable OpenVINO adapter failure without exposing vendor types. / 表示不暴露厂商类型的可诊断 OpenVINO 适配器故障。</summary>
    public sealed class OpenVinoBackendException : DeploySharpException
    {
        /// <summary>Initializes an OpenVINO backend exception. / 初始化 OpenVINO 后端异常。</summary>
        public OpenVinoBackendException(string errorCode, string message, Exception? innerException = null, ModelId? modelId = null, string? tensorName = null, string? operation = null, string? device = null, string? technicalDetails = null)
            : base(errorCode, message, innerException, OpenVinoBackendProvider.BackendId, modelId, technicalDetails)
        {
            TensorName = tensorName;
            Operation = operation;
            Device = device;
        }

        /// <summary>Gets the affected model tensor name when known. / 获取已知的受影响模型张量名称。</summary>
        public string? TensorName { get; }
        /// <summary>Gets the stable operation name when known. / 获取已知的稳定操作名称。</summary>
        public string? Operation { get; }
        /// <summary>Gets the requested OpenVINO device when known. / 获取已知的 OpenVINO 请求设备。</summary>
        public string? Device { get; }
    }
}
