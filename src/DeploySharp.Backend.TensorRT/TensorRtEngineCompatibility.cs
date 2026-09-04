using System;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Captures the device-bound identity required to reuse a TensorRT engine. / 记录复用 TensorRT 引擎所需的设备绑定身份。</summary>
    public sealed class TensorRtEngineCompatibility
    {
        /// <summary>Initializes an engine identity. / 初始化引擎身份。</summary>
        public TensorRtEngineCompatibility(string onnxSha256, string engineSerializationVersion, string tensorRtVersion, string cudaVersion, string cudnnVersion, string driverVersion, string gpuCompatibility, string bridgeIdentity)
        {
            OnnxSha256 = ValidateSha256(onnxSha256, nameof(onnxSha256));
            EngineSerializationVersion = RequireText(engineSerializationVersion, nameof(engineSerializationVersion));
            TensorRtVersion = RequireText(tensorRtVersion, nameof(tensorRtVersion));
            CudaVersion = RequireText(cudaVersion, nameof(cudaVersion));
            CudnnVersion = RequireText(cudnnVersion, nameof(cudnnVersion));
            DriverVersion = RequireText(driverVersion, nameof(driverVersion));
            GpuCompatibility = RequireText(gpuCompatibility, nameof(gpuCompatibility));
            BridgeIdentity = RequireText(bridgeIdentity, nameof(bridgeIdentity));
        }

        /// <summary>Gets ONNX model SHA-256. / 获取 ONNX 模型 SHA-256。</summary>
        public string OnnxSha256 { get; }
        /// <summary>Gets TensorRT engine serialization version. / 获取 TensorRT 引擎序列化版本。</summary>
        public string EngineSerializationVersion { get; }
        /// <summary>Gets TensorRT version. / 获取 TensorRT 版本。</summary>
        public string TensorRtVersion { get; }
        /// <summary>Gets CUDA version. / 获取 CUDA 版本。</summary>
        public string CudaVersion { get; }
        /// <summary>Gets cuDNN version. / 获取 cuDNN 版本。</summary>
        public string CudnnVersion { get; }
        /// <summary>Gets driver version. / 获取驱动版本。</summary>
        public string DriverVersion { get; }
        /// <summary>Gets GPU compatibility identity. / 获取 GPU 兼容身份。</summary>
        public string GpuCompatibility { get; }
        /// <summary>Gets exact native bridge identity or SHA. / 获取精确原生 bridge 身份或 SHA。</summary>
        public string BridgeIdentity { get; }

        /// <summary>Returns true only when every device-bound identity matches. / 仅当全部设备绑定身份一致时返回 true。</summary>
        public bool Matches(TensorRtEngineCompatibility actual)
        {
            if (actual == null) throw new ArgumentNullException(nameof(actual));
            return string.Equals(OnnxSha256, actual.OnnxSha256, StringComparison.Ordinal)
                && string.Equals(EngineSerializationVersion, actual.EngineSerializationVersion, StringComparison.Ordinal)
                && string.Equals(TensorRtVersion, actual.TensorRtVersion, StringComparison.Ordinal)
                && string.Equals(CudaVersion, actual.CudaVersion, StringComparison.Ordinal)
                && string.Equals(CudnnVersion, actual.CudnnVersion, StringComparison.Ordinal)
                && string.Equals(DriverVersion, actual.DriverVersion, StringComparison.Ordinal)
                && string.Equals(GpuCompatibility, actual.GpuCompatibility, StringComparison.Ordinal)
                && string.Equals(BridgeIdentity, actual.BridgeIdentity, StringComparison.Ordinal);
        }

        private static string RequireText(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName);
            return value!.Trim();
        }

        private static string ValidateSha256(string? value, string parameterName)
        {
            string sha = RequireText(value, parameterName);
            if (sha.Length != 64) throw new ArgumentException("SHA-256 values must contain exactly 64 hexadecimal characters.", parameterName);
            for (int index = 0; index < sha.Length; index++)
            {
                char c = sha[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) throw new ArgumentException("SHA-256 values must be hexadecimal.", parameterName);
            }
            return sha.ToLowerInvariant();
        }
    }
}
