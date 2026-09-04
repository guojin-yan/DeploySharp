using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Declares the exact native bridge compatibility contract. / 声明原生 bridge 的精确兼容合同。</summary>
    public sealed class TensorRtBridgeManifest
    {
        /// <summary>Initializes a bridge manifest. / 初始化 bridge 清单。</summary>
        public TensorRtBridgeManifest(string bridgeVersion, TensorRtApiVersion apiVersion, string cudaLine, int cudnnMajor, string runtimeIdentifier, string processArchitecture, IEnumerable<string> supportedComputeCapabilities, string entryPoint, string sha256)
        {
            BridgeVersion = RequireText(bridgeVersion, nameof(bridgeVersion));
            if (!Enum.IsDefined(typeof(TensorRtApiVersion), apiVersion)) throw new ArgumentOutOfRangeException(nameof(apiVersion));
            ApiVersion = apiVersion;
            CudaLine = RequireText(cudaLine, nameof(cudaLine));
            if (cudnnMajor <= 0) throw new ArgumentOutOfRangeException(nameof(cudnnMajor));
            CudnnMajor = cudnnMajor;
            RuntimeIdentifier = ValidateIdentifier(RequireText(runtimeIdentifier, nameof(runtimeIdentifier)).ToLowerInvariant(), nameof(runtimeIdentifier));
            ProcessArchitecture = ValidateIdentifier(RequireText(processArchitecture, nameof(processArchitecture)).ToLowerInvariant(), nameof(processArchitecture));
            var capabilities = new List<string>();
            foreach (string capability in supportedComputeCapabilities ?? throw new ArgumentNullException(nameof(supportedComputeCapabilities)))
            {
                string value = RequireText(capability, nameof(supportedComputeCapabilities));
                if (!capabilities.Contains(value)) capabilities.Add(value);
            }
            if (capabilities.Count == 0) throw new ArgumentException("At least one compute capability is required.", nameof(supportedComputeCapabilities));
            SupportedComputeCapabilities = new ReadOnlyCollection<string>(capabilities);
            EntryPoint = RequireText(entryPoint, nameof(entryPoint));
            Sha256 = ValidateSha256(sha256, nameof(sha256));
        }

        /// <summary>Gets bridge version. / 获取 bridge 版本。</summary>
        public string BridgeVersion { get; }
        /// <summary>Gets TensorRT API line. / 获取 TensorRT API 线。</summary>
        public TensorRtApiVersion ApiVersion { get; }
        /// <summary>Gets CUDA major/minor line. / 获取 CUDA 主次版本线。</summary>
        public string CudaLine { get; }
        /// <summary>Gets cuDNN major version. / 获取 cuDNN 主版本。</summary>
        public int CudnnMajor { get; }
        /// <summary>Gets bridge RID. / 获取 bridge RID。</summary>
        public string RuntimeIdentifier { get; }
        /// <summary>Gets bridge process architecture. / 获取 bridge 进程架构。</summary>
        public string ProcessArchitecture { get; }
        /// <summary>Gets supported GPU compute capabilities. / 获取支持的 GPU 计算能力。</summary>
        public IReadOnlyList<string> SupportedComputeCapabilities { get; }
        /// <summary>Gets bridge entry DLL. / 获取 bridge 入口 DLL。</summary>
        public string EntryPoint { get; }
        /// <summary>Gets bridge SHA-256. / 获取 bridge SHA-256。</summary>
        public string Sha256 { get; }

        /// <summary>Checks an observed probe against every bridge constraint. / 将观测探针结果与 bridge 的全部约束进行匹配。</summary>
        public bool Matches(TensorRtProbeResult probe)
        {
            if (probe == null) throw new ArgumentNullException(nameof(probe));
            if (!probe.SmokeTestPassed) return false;
            if (probe.ApiVersion != ApiVersion || !string.Equals(probe.RuntimeIdentifier, RuntimeIdentifier, StringComparison.OrdinalIgnoreCase) || !string.Equals(probe.ProcessArchitecture, ProcessArchitecture, StringComparison.OrdinalIgnoreCase)) return false;
            if (probe.CudaVersion == null || !IsSameCudaLine(probe.CudaVersion, CudaLine)) return false;
            if (probe.TensorRtVersion == null) return false;
            if (probe.CudnnVersion == null || !probe.CudnnVersion.StartsWith(CudnnMajor.ToString() + ".", StringComparison.Ordinal)) return false;
            if (probe.GpuComputeCapability == null) return false;
            for (int index = 0; index < SupportedComputeCapabilities.Count; index++) if (string.Equals(SupportedComputeCapabilities[index], probe.GpuComputeCapability, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsSameCudaLine(string version, string line)
        {
            string normalizedVersion = version.Trim();
            string normalizedLine = line.Trim();
            return string.Equals(normalizedVersion, normalizedLine, StringComparison.OrdinalIgnoreCase)
                || normalizedVersion.StartsWith(normalizedLine + ".", StringComparison.OrdinalIgnoreCase);
        }

        private static string RequireText(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName);
            return value!.Trim();
        }

        private static string ValidateIdentifier(string? value, string parameterName)
        {
            string identifier = RequireText(value, parameterName);
            for (int index = 0; index < identifier.Length; index++)
            {
                char c = identifier[index];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_' || c == '/'))
                    throw new ArgumentException("Identifiers contain only letters, numbers, '.', '-', '_', or '/'.", parameterName);
            }
            return identifier;
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
