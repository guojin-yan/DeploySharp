using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using JYPPX.CudaSharp;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Identifies whether a CUDA kernel participates before or after TensorRT inference. / 定义或说明 CUDA合同。</summary>
    public enum TensorRtCudaKernelRole
    {
        /// <summary>The kernel prepares caller-owned device buffers before inference. / 表示 CUDA状态或选项。</summary>
        Preprocessing = 1,
        /// <summary>The kernel transforms caller-owned device buffers after inference. / 表示 CUDA状态或选项。</summary>
        Postprocessing = 2
    }

    /// <summary>Identifies a CUDA Driver-loadable artifact emitted by NVRTC. / 定义或说明 CUDA合同。</summary>
    public enum TensorRtCudaRtcArtifactKind
    {
        /// <summary>Null-terminated PTX bytes copied from NVRTC. / 表示 CUDA状态或选项。</summary>
        Ptx = 1,
        /// <summary>CUBIN bytes compiled for a real SM target. / 表示 CUDA状态或选项。</summary>
        Cubin = 2
    }

    /// <summary>Defines one immutable virtual header supplied to NVRTC. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaRtcHeader
    {
        /// <summary>Initializes a copied virtual header. / 初始化源码对象。</summary>
        public TensorRtCudaRtcHeader(string includeName, string source)
        {
            var validated = new CudaRtcHeader(includeName, source);
            IncludeName = validated.IncludeName;
            Source = validated.Source;
        }

        /// <summary>Gets the exact virtual include name. / 获取相关信息。</summary>
        public string IncludeName { get; }
        /// <summary>Gets the copied header source. / 获取源码信息。</summary>
        public string Source { get; }
    }

    /// <summary>Contains immutable CUDA C++ source and the exact kernel entry-point contract. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaRtcKernelDefinition
    {
        private readonly ReadOnlyCollection<TensorRtCudaRtcHeader> _headers;

        /// <summary>Initializes a copied CUDA/RTC kernel definition. / 初始化 CUDA对象。</summary>
        public TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole role,
            string source,
            string kernelName,
            string programName = "deploysharp-kernel.cu",
            IEnumerable<TensorRtCudaRtcHeader>? headers = null,
            string? kernelNameExpression = null)
        {
            if (!Enum.IsDefined(typeof(TensorRtCudaKernelRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            Role = role;
            KernelName = TensorRtContractHash.ValidateText(kernelName, nameof(kernelName), allowEmpty: false);
            KernelNameExpression = kernelNameExpression == null
                ? null
                : TensorRtContractHash.ValidateText(kernelNameExpression, nameof(kernelNameExpression), allowEmpty: false);

            var copiedHeaders = new List<TensorRtCudaRtcHeader>();
            var includeNames = new HashSet<string>(StringComparer.Ordinal);
            if (headers != null)
            {
                foreach (TensorRtCudaRtcHeader? header in headers)
                {
                    if (header == null) throw new ArgumentException("CUDA/RTC headers cannot contain null entries.", nameof(headers));
                    if (!includeNames.Add(header.IncludeName)) throw new ArgumentException("CUDA/RTC header include names must be unique.", nameof(headers));
                    copiedHeaders.Add(new TensorRtCudaRtcHeader(header.IncludeName, header.Source));
                }
            }
            copiedHeaders.Sort((left, right) => StringComparer.Ordinal.Compare(left.IncludeName, right.IncludeName));

            var nativeHeaders = new List<CudaRtcHeader>(copiedHeaders.Count);
            foreach (TensorRtCudaRtcHeader header in copiedHeaders) nativeHeaders.Add(new CudaRtcHeader(header.IncludeName, header.Source));
            IEnumerable<string>? nameExpressions = KernelNameExpression == null ? null : new[] { KernelNameExpression };
            var validatedSource = new CudaRtcProgramSource(source, programName, nativeHeaders, nameExpressions);
            Source = validatedSource.Source;
            ProgramName = validatedSource.ProgramName;
            _headers = new ReadOnlyCollection<TensorRtCudaRtcHeader>(copiedHeaders);
            SourceSha256 = TensorRtContractHash.Text(Source);
            var canonicalHeaders = new List<string>(copiedHeaders.Count * 2);
            foreach (TensorRtCudaRtcHeader header in copiedHeaders)
            {
                canonicalHeaders.Add(header.IncludeName);
                canonicalHeaders.Add(header.Source);
            }
            HeadersSha256 = TensorRtContractHash.Sequence(canonicalHeaders);
        }

        /// <summary>Gets whether the kernel is preprocessing or postprocessing. / 获取 CUDA信息。</summary>
        public TensorRtCudaKernelRole Role { get; }
        /// <summary>Gets the copied CUDA C++ source. / 获取 CUDA信息。</summary>
        public string Source { get; }
        /// <summary>Gets the exact unmangled entry point used when no name expression is supplied. / 获取相关信息。</summary>
        public string KernelName { get; }
        /// <summary>Gets the virtual program name supplied to NVRTC. / 获取 CUDA信息。</summary>
        public string ProgramName { get; }
        /// <summary>Gets the optional C++ name expression that must resolve to the launch entry point. / 获取配置信息。</summary>
        public string? KernelNameExpression { get; }
        /// <summary>Gets the copied virtual headers in canonical ordinal include-name order. / 获取源码信息。</summary>
        public IReadOnlyList<TensorRtCudaRtcHeader> Headers => _headers;
        /// <summary>Gets the exact UTF-8 source SHA256. / 获取哈希标识信息。</summary>
        public string SourceSha256 { get; }
        /// <summary>Gets the JYPPX.CudaSharp-compatible canonical header SHA256. / 获取 CUDA信息。</summary>
        public string HeadersSha256 { get; }
    }

    /// <summary>Contains the exact immutable NVRTC option list and requested output kind. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaRtcCompileOptions
    {
        private readonly CudaRtcCompileOptions _nativeOptions;

        /// <summary>Initializes explicit NVRTC compilation settings. / 初始化 CUDA对象。</summary>
        public TensorRtCudaRtcCompileOptions(
            string targetArchitecture,
            TensorRtCudaRtcArtifactKind artifactKind = TensorRtCudaRtcArtifactKind.Ptx,
            bool generateLineInfo = false,
            bool deviceDebug = false,
            bool useFastMath = false,
            bool relocatableDeviceCode = false,
            IEnumerable<string>? additionalOptions = null)
        {
            if (!Enum.IsDefined(typeof(TensorRtCudaRtcArtifactKind), artifactKind)) throw new ArgumentOutOfRangeException(nameof(artifactKind));
            if (string.IsNullOrWhiteSpace(targetArchitecture)) throw new ArgumentException("An explicit compute_XX or sm_XX target architecture is required.", nameof(targetArchitecture));
            if (artifactKind == TensorRtCudaRtcArtifactKind.Cubin && !targetArchitecture.StartsWith("sm_", StringComparison.Ordinal))
            {
                throw new ArgumentException("CUBIN output requires an explicit real sm_XX target architecture.", nameof(targetArchitecture));
            }

            _nativeOptions = new CudaRtcCompileOptions(
                targetArchitecture,
                generateLineInfo,
                deviceDebug,
                useFastMath,
                relocatableDeviceCode,
                emitLtoIr: false,
                additionalOptions);
            ArtifactKind = artifactKind;
            OptionsSha256 = TensorRtContractHash.Sequence(_nativeOptions.Options);
        }

        /// <summary>Gets the explicit compute_XX or sm_XX compiler target. / 获取相关信息。</summary>
        public string TargetArchitecture => _nativeOptions.TargetArchitecture;
        /// <summary>Gets the requested Driver-loadable artifact kind. / 获取模型工件信息。</summary>
        public TensorRtCudaRtcArtifactKind ArtifactKind { get; }
        /// <summary>Gets whether line information is requested. / 获取相关信息。</summary>
        public bool GenerateLineInfo => _nativeOptions.GenerateLineInfo;
        /// <summary>Gets whether device debug output is requested. / 获取设备信息。</summary>
        public bool DeviceDebug => _nativeOptions.DeviceDebug;
        /// <summary>Gets whether fast math is enabled. / 获取相关信息。</summary>
        public bool UseFastMath => _nativeOptions.UseFastMath;
        /// <summary>Gets whether relocatable device code is enabled. / 获取设备信息。</summary>
        public bool RelocatableDeviceCode => _nativeOptions.RelocatableDeviceCode;
        /// <summary>Gets the complete immutable option list in compiler order. / 获取配置信息。</summary>
        public IReadOnlyList<string> Options => _nativeOptions.Options;
        /// <summary>Gets the JYPPX.CudaSharp-compatible canonical option SHA256. / 获取 CUDA信息。</summary>
        public string OptionsSha256 { get; }

        internal CudaRtcCompileOptions NativeOptions => _nativeOptions;
    }

    /// <summary>Contains copied PTX/CUBIN bytes and the complete managed compilation identity. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaRtcArtifact
    {
        private readonly byte[] _code;

        /// <summary>Initializes a copied in-memory PTX/CUBIN artifact, including artifacts restored from caller-owned cache. / 初始化缓存对象。</summary>
        public TensorRtCudaRtcArtifact(
            byte[] code,
            TensorRtCudaRtcArtifactKind kind,
            TensorRtCudaKernelRole role,
            string sourceSha256,
            string headersSha256,
            string optionsSha256,
            string compilerVersion,
            string targetArchitecture,
            string programName,
            string kernelName,
            string? kernelNameExpression = null,
            string? expectedArtifactSha256 = null)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (code.Length == 0) throw new ArgumentException("A CUDA/RTC artifact cannot be empty.", nameof(code));
            if (!Enum.IsDefined(typeof(TensorRtCudaRtcArtifactKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(TensorRtCudaKernelRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            _code = (byte[])code.Clone();
            Kind = kind;
            Role = role;
            SourceSha256 = TensorRtContractHash.ValidateSha256(sourceSha256, nameof(sourceSha256));
            HeadersSha256 = TensorRtContractHash.ValidateSha256(headersSha256, nameof(headersSha256));
            OptionsSha256 = TensorRtContractHash.ValidateSha256(optionsSha256, nameof(optionsSha256));
            CompilerVersion = TensorRtContractHash.ValidateIdentity(compilerVersion, nameof(compilerVersion));
            TargetArchitecture = TensorRtContractHash.ValidateIdentity(targetArchitecture, nameof(targetArchitecture));
            ProgramName = TensorRtContractHash.ValidateText(programName, nameof(programName), allowEmpty: false);
            KernelName = TensorRtContractHash.ValidateText(kernelName, nameof(kernelName), allowEmpty: false);
            KernelNameExpression = kernelNameExpression == null
                ? null
                : TensorRtContractHash.ValidateText(kernelNameExpression, nameof(kernelNameExpression), allowEmpty: false);
            ArtifactSha256 = TensorRtContractHash.Bytes(_code);
            if (expectedArtifactSha256 != null &&
                !string.Equals(ArtifactSha256, TensorRtContractHash.ValidateSha256(expectedArtifactSha256, nameof(expectedArtifactSha256)), StringComparison.Ordinal))
            {
                throw new ArgumentException("The copied CUDA/RTC bytes do not match the expected artifact SHA256.", nameof(expectedArtifactSha256));
            }
            CompilationInputsSha256 = TensorRtContractHash.Sequence(new[]
            {
                "deploysharp-tensorrt-cuda-rtc-inputs-v1",
                ((int)Role).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ((int)Kind).ToString(System.Globalization.CultureInfo.InvariantCulture),
                SourceSha256,
                HeadersSha256,
                OptionsSha256,
                ProgramName,
                KernelName,
                KernelNameExpression ?? string.Empty,
                TargetArchitecture
            });
        }

        /// <summary>Gets the artifact kind. / 获取模型工件信息。</summary>
        public TensorRtCudaRtcArtifactKind Kind { get; }
        /// <summary>Gets the preprocessing or postprocessing role. / 获取相关信息。</summary>
        public TensorRtCudaKernelRole Role { get; }
        /// <summary>Gets the copied artifact byte length. / 获取模型工件信息。</summary>
        public int Length => _code.Length;
        /// <summary>Gets the artifact SHA256. / 获取模型工件信息。</summary>
        public string ArtifactSha256 { get; }
        /// <summary>Gets the source SHA256. / 获取哈希标识信息。</summary>
        public string SourceSha256 { get; }
        /// <summary>Gets the canonical header SHA256. / 获取哈希标识信息。</summary>
        public string HeadersSha256 { get; }
        /// <summary>Gets the canonical option SHA256. / 获取哈希标识信息。</summary>
        public string OptionsSha256 { get; }
        /// <summary>Gets the exact loaded compiler version. / 获取相关信息。</summary>
        public string CompilerVersion { get; }
        /// <summary>Gets the explicit compiler target architecture. / 获取相关信息。</summary>
        public string TargetArchitecture { get; }
        /// <summary>Gets the virtual program name. / 获取相关信息。</summary>
        public string ProgramName { get; }
        /// <summary>Gets the exact resolved Driver launch entry point. / 获取原生运行时信息。</summary>
        public string KernelName { get; }
        /// <summary>Gets the optional original C++ name expression. / 获取配置信息。</summary>
        public string? KernelNameExpression { get; }
        /// <summary>Gets a hash of all managed compilation inputs, excluding runtime/GPU compatibility identity. / 获取原生运行时信息。</summary>
        public string CompilationInputsSha256 { get; }
        /// <summary>Returns a new copy of the PTX/CUBIN bytes. / 返回 CUDA结果。</summary>
        public byte[] ToArray() => (byte[])_code.Clone();
    }

    /// <summary>Builds a complete caller-owned External/local cache key for one CUDA kernel artifact. / 构建缓存。</summary>
    public sealed class TensorRtCudaKernelCacheIdentity
    {
        /// <summary>Initializes a complete cache identity without writing a cache entry. / 初始化缓存对象。</summary>
        public TensorRtCudaKernelCacheIdentity(
            TensorRtCudaRtcArtifact artifact,
            string compilerIdentity,
            string cudaRuntimeVersion,
            string cudaRuntimeIdentity,
            string cudaDriverVersion,
            string cudaDriverIdentity,
            string gpuArchitecture,
            string gpuCompatibilityIdentity,
            string nativeBridgeIdentity)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            SourceSha256 = artifact.SourceSha256;
            HeadersSha256 = artifact.HeadersSha256;
            OptionsSha256 = artifact.OptionsSha256;
            ArtifactSha256 = artifact.ArtifactSha256;
            CompilerVersion = artifact.CompilerVersion;
            TargetArchitecture = artifact.TargetArchitecture;
            KernelName = artifact.KernelName;
            ArtifactKind = artifact.Kind;
            CompilerIdentity = TensorRtContractHash.ValidateIdentity(compilerIdentity, nameof(compilerIdentity));
            CudaRuntimeVersion = TensorRtContractHash.ValidateIdentity(cudaRuntimeVersion, nameof(cudaRuntimeVersion));
            CudaRuntimeIdentity = TensorRtContractHash.ValidateIdentity(cudaRuntimeIdentity, nameof(cudaRuntimeIdentity));
            CudaDriverVersion = TensorRtContractHash.ValidateIdentity(cudaDriverVersion, nameof(cudaDriverVersion));
            CudaDriverIdentity = TensorRtContractHash.ValidateIdentity(cudaDriverIdentity, nameof(cudaDriverIdentity));
            GpuArchitecture = TensorRtContractHash.ValidateIdentity(gpuArchitecture, nameof(gpuArchitecture));
            GpuCompatibilityIdentity = TensorRtContractHash.ValidateIdentity(gpuCompatibilityIdentity, nameof(gpuCompatibilityIdentity));
            NativeBridgeIdentity = TensorRtContractHash.ValidateIdentity(nativeBridgeIdentity, nameof(nativeBridgeIdentity));
            CacheKeySha256 = TensorRtContractHash.Sequence(new[]
            {
                "deploysharp-tensorrt-cuda-kernel-cache-v2",
                SourceSha256,
                HeadersSha256,
                OptionsSha256,
                ArtifactSha256,
                CompilerVersion,
                CompilerIdentity,
                TargetArchitecture,
                ((int)ArtifactKind).ToString(System.Globalization.CultureInfo.InvariantCulture),
                KernelName,
                CudaRuntimeVersion,
                CudaRuntimeIdentity,
                CudaDriverVersion,
                CudaDriverIdentity,
                GpuArchitecture,
                GpuCompatibilityIdentity,
                NativeBridgeIdentity
            });
        }

        /// <summary>Gets the source SHA256. / 获取哈希标识信息。</summary>
        public string SourceSha256 { get; }
        /// <summary>Gets the virtual-header SHA256. / 获取哈希标识信息。</summary>
        public string HeadersSha256 { get; }
        /// <summary>Gets the compiler-option SHA256. / 获取哈希标识信息。</summary>
        public string OptionsSha256 { get; }
        /// <summary>Gets the PTX/CUBIN artifact SHA256. / 获取 CUDA信息。</summary>
        public string ArtifactSha256 { get; }
        /// <summary>Gets the NVRTC compiler version. / 获取 CUDA信息。</summary>
        public string CompilerVersion { get; }
        /// <summary>Gets the caller-recorded exact NVRTC/compiler binary identity. / 获取 CUDA信息。</summary>
        public string CompilerIdentity { get; }
        /// <summary>Gets the requested target architecture. / 获取相关信息。</summary>
        public string TargetArchitecture { get; }
        /// <summary>Gets the artifact kind. / 获取模型工件信息。</summary>
        public TensorRtCudaRtcArtifactKind ArtifactKind { get; }
        /// <summary>Gets the resolved kernel entry point. / 获取 CUDA信息。</summary>
        public string KernelName { get; }
        /// <summary>Gets the caller-recorded CUDA runtime version. / 获取 CUDA信息。</summary>
        public string CudaRuntimeVersion { get; }
        /// <summary>Gets the caller-recorded exact CUDA runtime binary/package identity. / 获取 CUDA信息。</summary>
        public string CudaRuntimeIdentity { get; }
        /// <summary>Gets the caller-recorded CUDA driver version. / 获取 CUDA信息。</summary>
        public string CudaDriverVersion { get; }
        /// <summary>Gets the caller-recorded exact CUDA driver identity. / 获取 CUDA信息。</summary>
        public string CudaDriverIdentity { get; }
        /// <summary>Gets the caller-recorded GPU architecture. / 获取相关信息。</summary>
        public string GpuArchitecture { get; }
        /// <summary>Gets the caller-recorded GPU model or compatibility class; physical device UUIDs are intentionally excluded. / 获取模型工件信息。</summary>
        public string GpuCompatibilityIdentity { get; }
        /// <summary>Gets the caller-recorded native bridge package/binary identity. / 获取原生运行时信息。</summary>
        public string NativeBridgeIdentity { get; }
        /// <summary>Gets the complete cache-key SHA256. / 获取缓存信息。</summary>
        public string CacheKeySha256 { get; }
    }

    internal static class TensorRtContractHash
    {
        public static string Text(string value) => Bytes(Encoding.UTF8.GetBytes(value));

        public static string Sequence(IEnumerable<string> values)
        {
            var canonical = new StringBuilder();
            foreach (string value in values) canonical.Append(value.Length).Append(':').Append(value).Append(';');
            return Text(canonical.ToString());
        }

        public static string Bytes(byte[] bytes)
        {
            using SHA256 algorithm = SHA256.Create();
            byte[] hash = algorithm.ComputeHash(bytes);
            var result = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash) result.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return result.ToString();
        }

        public static string ValidateSha256(string value, string parameterName)
        {
            if (value == null || value.Length != 64) throw new ArgumentException("A lowercase 64-character SHA256 is required.", parameterName);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                {
                    throw new ArgumentException("A lowercase 64-character SHA256 is required.", parameterName);
                }
            }
            return value;
        }

        public static string ValidateIdentity(string value, string parameterName)
        {
            return ValidateText(value, parameterName, allowEmpty: false);
        }

        public static string ValidateText(string value, string parameterName, bool allowEmpty)
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            if (!allowEmpty && string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty identity value is required.", parameterName);
            if (value.IndexOf('\0') >= 0) throw new ArgumentException("Identity text cannot contain an embedded NUL.", parameterName);
            return value;
        }
    }
}
