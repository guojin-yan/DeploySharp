using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Diagnostics;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Contains structured TensorRT/CUDA probe output. / 包含结构化 TensorRT/CUDA 探针结果。</summary>
    public sealed class TensorRtProbeResult
    {
        /// <summary>Initializes a probe result. / 初始化探针结果。</summary>
        public TensorRtProbeResult(
            TensorRtApiVersion apiVersion,
            string? cudaVersion = null,
            string? nvrtcVersion = null,
            string? cudnnVersion = null,
            string? tensorRtVersion = null,
            string? driverVersion = null,
            IReadOnlyDictionary<string, string>? nativeLibraryPaths = null,
            IEnumerable<string>? devices = null,
            string? gpuComputeCapability = null,
            IEnumerable<string>? exportedSymbols = null,
            bool smokeTestPassed = false,
            string? runtimeIdentifier = null,
            string? processArchitecture = null,
            IEnumerable<RuntimeDiagnostic>? diagnostics = null)
        {
            if (!Enum.IsDefined(typeof(TensorRtApiVersion), apiVersion)) throw new ArgumentOutOfRangeException(nameof(apiVersion));
            ApiVersion = apiVersion;
            CudaVersion = Normalize(cudaVersion);
            NvrtcVersion = Normalize(nvrtcVersion);
            CudnnVersion = Normalize(cudnnVersion);
            TensorRtVersion = Normalize(tensorRtVersion);
            DriverVersion = Normalize(driverVersion);
            RuntimeIdentifier = string.IsNullOrWhiteSpace(runtimeIdentifier) ? null : ValidateIdentifier(runtimeIdentifier.ToLowerInvariant(), nameof(runtimeIdentifier));
            ProcessArchitecture = string.IsNullOrWhiteSpace(processArchitecture) ? null : ValidateIdentifier(processArchitecture.ToLowerInvariant(), nameof(processArchitecture));
            GpuComputeCapability = Normalize(gpuComputeCapability);
            NativeLibraryPaths = CopyDictionary(nativeLibraryPaths);
            Devices = CopyStrings(devices);
            ExportedSymbols = CopyStrings(exportedSymbols);
            Diagnostics = CopyDiagnostics(diagnostics);
            SmokeTestPassed = smokeTestPassed;
        }

        /// <summary>Gets TensorRT API line 8/10/11. / 获取 TensorRT API 8/10/11 线。</summary>
        public TensorRtApiVersion ApiVersion { get; }
        /// <summary>Gets CUDA runtime version. / 获取 CUDA 运行时版本。</summary>
        public string? CudaVersion { get; }
        /// <summary>Gets NVRTC version. / 获取 NVRTC 版本。</summary>
        public string? NvrtcVersion { get; }
        /// <summary>Gets cuDNN version. / 获取 cuDNN 版本。</summary>
        public string? CudnnVersion { get; }
        /// <summary>Gets TensorRT native version. / 获取 TensorRT 原生版本。</summary>
        public string? TensorRtVersion { get; }
        /// <summary>Gets NVIDIA driver version. / 获取 NVIDIA 驱动版本。</summary>
        public string? DriverVersion { get; }
        /// <summary>Gets actual loaded native library paths by logical name. / 获取按逻辑名称记录的实际原生库路径。</summary>
        public IReadOnlyDictionary<string, string> NativeLibraryPaths { get; }
        /// <summary>Gets discovered GPU devices. / 获取发现的 GPU 设备。</summary>
        public IReadOnlyList<string> Devices { get; }
        /// <summary>Gets the selected GPU compute capability. / 获取选定 GPU 的计算能力。</summary>
        public string? GpuComputeCapability { get; }
        /// <summary>Gets required exported symbols found by the probe. / 获取探针发现的导出符号。</summary>
        public IReadOnlyList<string> ExportedSymbols { get; }
        /// <summary>Gets whether the minimal native smoke test passed. / 获取最小原生冒烟测试是否通过。</summary>
        public bool SmokeTestPassed { get; }
        /// <summary>Gets the probed RID. / 获取探测的 RID。</summary>
        public string? RuntimeIdentifier { get; }
        /// <summary>Gets the probing process architecture. / 获取探测进程架构。</summary>
        public string? ProcessArchitecture { get; }
        /// <summary>Gets structured probe diagnostics. / 获取结构化探针诊断。</summary>
        public IReadOnlyList<RuntimeDiagnostic> Diagnostics { get; }

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : RequireText(value, nameof(value));
        private static IReadOnlyDictionary<string, string> CopyDictionary(IReadOnlyDictionary<string, string>? values)
        {
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (values != null) foreach (KeyValuePair<string, string> pair in values) copy.Add(ValidateIdentifier(pair.Key, nameof(values)), RequireText(pair.Value, nameof(values)));
            return new ReadOnlyDictionary<string, string>(copy);
        }
        private static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values)
        {
            var copy = new List<string>();
            if (values != null) foreach (string value in values) { string item = RequireText(value, nameof(values)); if (!copy.Contains(item)) copy.Add(item); }
            return new ReadOnlyCollection<string>(copy);
        }
        private static IReadOnlyList<RuntimeDiagnostic> CopyDiagnostics(IEnumerable<RuntimeDiagnostic>? values)
        {
            var copy = new List<RuntimeDiagnostic>();
            if (values != null) foreach (RuntimeDiagnostic value in values) copy.Add(value ?? throw new ArgumentException("Diagnostics cannot contain null entries.", nameof(values)));
            return new ReadOnlyCollection<RuntimeDiagnostic>(copy);
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
    }
}
