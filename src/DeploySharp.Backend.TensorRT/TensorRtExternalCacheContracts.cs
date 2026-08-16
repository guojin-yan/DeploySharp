using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Identifies the payload represented by an External TensorRT cache entry.</summary>
    public enum TensorRtExternalCacheEntryKind
    {
        /// <summary>An NVRTC PTX artifact.</summary>
        CudaPtx = 1,
        /// <summary>An NVRTC CUBIN artifact.</summary>
        CudaCubin = 2,
        /// <summary>A serialized TensorRT .engine artifact.</summary>
        TensorRtEngine = 3,
        /// <summary>A serialized TensorRT .plan artifact.</summary>
        TensorRtPlan = 4
    }

    /// <summary>Reports the observable outcome of one External cache operation.</summary>
    public enum TensorRtExternalCacheStatus
    {
        /// <summary>No completed entry exists for the requested identity.</summary>
        Miss = 1,
        /// <summary>A completed and fully validated entry was opened.</summary>
        Hit = 2,
        /// <summary>A new completed entry was atomically published.</summary>
        Stored = 3,
        /// <summary>The same identity and payload were already stored.</summary>
        AlreadyPresent = 4,
        /// <summary>The completed entry was explicitly invalidated or deleted.</summary>
        Deleted = 5,
        /// <summary>The requested entry did not exist when deletion was requested.</summary>
        NotFound = 6,
        /// <summary>An existing entry was rejected as unsafe, incompatible, incomplete, or corrupt.</summary>
        Rejected = 7,
        /// <summary>The key already names a different payload and replacement is disabled.</summary>
        Conflict = 8
    }

    /// <summary>Classifies why an existing External cache entry was rejected.</summary>
    public enum TensorRtExternalCacheRejectionReason
    {
        /// <summary>No rejection occurred.</summary>
        None = 0,
        /// <summary>A path component, file type, or reparse point was unsafe.</summary>
        UnsafePath = 1,
        /// <summary>The completion record or manifest was missing, malformed, or unsupported.</summary>
        ManifestInvalid = 2,
        /// <summary>The entry identity or key did not match the requested identity and directory.</summary>
        IdentityMismatch = 3,
        /// <summary>The payload or manifest length/SHA256 did not match its recorded integrity metadata.</summary>
        IntegrityMismatch = 4,
        /// <summary>The manifest or payload exceeded the configured limit before it was read.</summary>
        SizeLimitExceeded = 5,
        /// <summary>The requested key already names a different valid payload.</summary>
        PayloadConflict = 6
    }

    /// <summary>Controls what happens after a corrupt or incompatible entry is rejected.</summary>
    public enum TensorRtExternalCacheRejectedEntryPolicy
    {
        /// <summary>Leave the rejected entry in place for caller inspection.</summary>
        Keep = 0,
        /// <summary>Delete only the rejected entry from the caller-owned cache root.</summary>
        Delete = 1,
        /// <summary>Move the rejected entry under the cache root's quarantine directory.</summary>
        Quarantine = 2
    }

    /// <summary>Controls a store operation when the same lookup key names different valid bytes.</summary>
    public enum TensorRtExternalCacheConflictPolicy
    {
        /// <summary>Return a conflict result and preserve the completed entry.</summary>
        Reject = 0,
        /// <summary>Atomically publish a new immutable generation for the key.</summary>
        Replace = 1
    }

    /// <summary>Reports an explicit remediation performed for a rejected entry.</summary>
    public enum TensorRtExternalCacheRemediation
    {
        /// <summary>No remediation was requested or completed.</summary>
        None = 0,
        /// <summary>The rejected entry was deleted.</summary>
        Deleted = 1,
        /// <summary>The rejected entry was moved to quarantine.</summary>
        Quarantined = 2,
        /// <summary>The requested remediation could not be completed.</summary>
        Failed = 3
    }

    /// <summary>Configures one explicitly constructed, caller-path-only External cache store.</summary>
    public sealed class TensorRtExternalCacheOptions
    {
        /// <summary>Initializes bounded cache behavior without selecting a path.</summary>
        public TensorRtExternalCacheOptions(
            long maximumCudaArtifactBytes = 268435456,
            long maximumEngineBytes = int.MaxValue,
            int maximumManifestBytes = 1048576,
            TensorRtExternalCacheRejectedEntryPolicy rejectedEntryPolicy = TensorRtExternalCacheRejectedEntryPolicy.Keep,
            TensorRtExternalCacheConflictPolicy conflictPolicy = TensorRtExternalCacheConflictPolicy.Reject,
            bool createRootIfMissing = true,
            bool cleanupStaleTemporaryEntries = true,
            TimeSpan? temporaryEntryRetention = null)
        {
            if (maximumCudaArtifactBytes < 1 || maximumCudaArtifactBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumCudaArtifactBytes));
            if (maximumEngineBytes < 8 || maximumEngineBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumEngineBytes));
            if (maximumManifestBytes < 256 || maximumManifestBytes > 16777216) throw new ArgumentOutOfRangeException(nameof(maximumManifestBytes));
            if (!Enum.IsDefined(typeof(TensorRtExternalCacheRejectedEntryPolicy), rejectedEntryPolicy)) throw new ArgumentOutOfRangeException(nameof(rejectedEntryPolicy));
            if (!Enum.IsDefined(typeof(TensorRtExternalCacheConflictPolicy), conflictPolicy)) throw new ArgumentOutOfRangeException(nameof(conflictPolicy));
            TimeSpan retention = temporaryEntryRetention ?? TimeSpan.FromHours(24);
            if (retention < TimeSpan.Zero || retention > TimeSpan.FromDays(30)) throw new ArgumentOutOfRangeException(nameof(temporaryEntryRetention));

            MaximumCudaArtifactBytes = maximumCudaArtifactBytes;
            MaximumEngineBytes = maximumEngineBytes;
            MaximumManifestBytes = maximumManifestBytes;
            RejectedEntryPolicy = rejectedEntryPolicy;
            ConflictPolicy = conflictPolicy;
            CreateRootIfMissing = createRootIfMissing;
            CleanupStaleTemporaryEntries = cleanupStaleTemporaryEntries;
            TemporaryEntryRetention = retention;
        }

        /// <summary>Gets the maximum PTX/CUBIN bytes accepted before allocation or hashing.</summary>
        public long MaximumCudaArtifactBytes { get; }
        /// <summary>Gets the maximum engine/plan bytes accepted before reading or copying.</summary>
        public long MaximumEngineBytes { get; }
        /// <summary>Gets the maximum manifest bytes accepted before deserialization.</summary>
        public int MaximumManifestBytes { get; }
        /// <summary>Gets the explicit rejected-entry remediation policy.</summary>
        public TensorRtExternalCacheRejectedEntryPolicy RejectedEntryPolicy { get; }
        /// <summary>Gets the valid-payload conflict policy.</summary>
        public TensorRtExternalCacheConflictPolicy ConflictPolicy { get; }
        /// <summary>Gets whether construction may create the explicitly supplied root.</summary>
        public bool CreateRootIfMissing { get; }
        /// <summary>Gets whether operations remove stale temporary and unreachable generation directories.</summary>
        public bool CleanupStaleTemporaryEntries { get; }
        /// <summary>Gets the minimum age of a temporary or unreachable generation before cleanup.</summary>
        public TimeSpan TemporaryEntryRetention { get; }

        /// <summary>Gets default bounded behavior; no default cache path exists.</summary>
        public static TensorRtExternalCacheOptions Default { get; } = new TensorRtExternalCacheOptions();
    }

    /// <summary>Provides all deterministic inputs needed to find a CUDA artifact before starting NVRTC.</summary>
    public sealed class TensorRtCudaKernelLookupIdentity
    {
        /// <summary>Initializes a complete pre-compilation CUDA lookup identity.</summary>
        public TensorRtCudaKernelLookupIdentity(
            TensorRtCudaRtcKernelDefinition definition,
            TensorRtCudaRtcCompileOptions compileOptions,
            string compilerVersion,
            string compilerIdentity,
            string cudaRuntimeVersion,
            string cudaRuntimeIdentity,
            string cudaDriverVersion,
            string cudaDriverIdentity,
            string gpuArchitecture,
            string gpuCompatibilityIdentity,
            string nativeBridgeIdentity)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (compileOptions == null) throw new ArgumentNullException(nameof(compileOptions));
            Role = definition.Role;
            SourceSha256 = definition.SourceSha256;
            HeadersSha256 = definition.HeadersSha256;
            OptionsSha256 = compileOptions.OptionsSha256;
            CompilerVersion = TensorRtContractHash.ValidateIdentity(compilerVersion, nameof(compilerVersion));
            CompilerIdentity = TensorRtContractHash.ValidateIdentity(compilerIdentity, nameof(compilerIdentity));
            TargetArchitecture = compileOptions.TargetArchitecture;
            ArtifactKind = compileOptions.ArtifactKind;
            ProgramName = definition.ProgramName;
            KernelName = definition.KernelName;
            KernelNameExpression = definition.KernelNameExpression;
            CudaRuntimeVersion = TensorRtContractHash.ValidateIdentity(cudaRuntimeVersion, nameof(cudaRuntimeVersion));
            CudaRuntimeIdentity = TensorRtContractHash.ValidateIdentity(cudaRuntimeIdentity, nameof(cudaRuntimeIdentity));
            CudaDriverVersion = TensorRtContractHash.ValidateIdentity(cudaDriverVersion, nameof(cudaDriverVersion));
            CudaDriverIdentity = TensorRtContractHash.ValidateIdentity(cudaDriverIdentity, nameof(cudaDriverIdentity));
            GpuArchitecture = TensorRtContractHash.ValidateIdentity(gpuArchitecture, nameof(gpuArchitecture));
            GpuCompatibilityIdentity = TensorRtContractHash.ValidateIdentity(gpuCompatibilityIdentity, nameof(gpuCompatibilityIdentity));
            NativeBridgeIdentity = TensorRtContractHash.ValidateIdentity(nativeBridgeIdentity, nameof(nativeBridgeIdentity));
            LookupKeySha256 = TensorRtContractHash.Sequence(new[]
            {
                "deploysharp-tensorrt-cuda-cache-lookup-v2",
                ((int)Role).ToString(System.Globalization.CultureInfo.InvariantCulture),
                SourceSha256,
                HeadersSha256,
                OptionsSha256,
                CompilerVersion,
                CompilerIdentity,
                TargetArchitecture,
                ((int)ArtifactKind).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ProgramName,
                KernelName,
                KernelNameExpression ?? string.Empty,
                CudaRuntimeVersion,
                CudaRuntimeIdentity,
                CudaDriverVersion,
                CudaDriverIdentity,
                GpuArchitecture,
                GpuCompatibilityIdentity,
                NativeBridgeIdentity
            });
        }

        /// <summary>Gets the preprocessing or postprocessing role.</summary>
        public TensorRtCudaKernelRole Role { get; }
        /// <summary>Gets the exact source SHA256.</summary>
        public string SourceSha256 { get; }
        /// <summary>Gets the ordered virtual-header SHA256.</summary>
        public string HeadersSha256 { get; }
        /// <summary>Gets the complete compiler-option SHA256.</summary>
        public string OptionsSha256 { get; }
        /// <summary>Gets the expected NVRTC compiler version.</summary>
        public string CompilerVersion { get; }
        /// <summary>Gets the exact compiler binary/package identity.</summary>
        public string CompilerIdentity { get; }
        /// <summary>Gets the explicit compute_XX or sm_XX target.</summary>
        public string TargetArchitecture { get; }
        /// <summary>Gets the requested PTX or CUBIN kind.</summary>
        public TensorRtCudaRtcArtifactKind ArtifactKind { get; }
        /// <summary>Gets the virtual program name.</summary>
        public string ProgramName { get; }
        /// <summary>Gets the resolved launch entry point.</summary>
        public string KernelName { get; }
        /// <summary>Gets the optional original C++ name expression.</summary>
        public string? KernelNameExpression { get; }
        /// <summary>Gets the exact CUDA runtime version.</summary>
        public string CudaRuntimeVersion { get; }
        /// <summary>Gets the exact CUDA runtime identity.</summary>
        public string CudaRuntimeIdentity { get; }
        /// <summary>Gets the exact CUDA driver version.</summary>
        public string CudaDriverVersion { get; }
        /// <summary>Gets the exact CUDA driver identity.</summary>
        public string CudaDriverIdentity { get; }
        /// <summary>Gets the GPU architecture.</summary>
        public string GpuArchitecture { get; }
        /// <summary>Gets the GPU model or caller-defined compatibility class; physical device UUIDs are intentionally excluded.</summary>
        public string GpuCompatibilityIdentity { get; }
        /// <summary>Gets the exact native bridge identity.</summary>
        public string NativeBridgeIdentity { get; }
        /// <summary>Gets the build-before-output lookup key.</summary>
        public string LookupKeySha256 { get; }
    }

    /// <summary>Provides a complete deterministic TensorRT engine lookup identity before an engine is built.</summary>
    public sealed class TensorRtEngineCacheIdentity
    {
        private readonly ReadOnlyCollection<TensorRtOnnxInputProfile> _profiles;
        private readonly ReadOnlyCollection<string> _builderFlags;

        /// <summary>Gets the adapter cache identity schema accepted by this assembly.</summary>
        public const string CurrentAdapterSchemaVersion = "2";

        /// <summary>Initializes a device-, runtime-, build-, platform-, and package-bound engine identity.</summary>
        public TensorRtEngineCacheIdentity(
            string onnxSha256,
            TensorRtOnnxEngineBuildOptions buildOptions,
            string managedPackageIdentity,
            string managedApiContractSha256,
            string tensorRtVersion,
            string tensorRtIdentity,
            string cudaRuntimeVersion,
            string cudaRuntimeIdentity,
            string cudnnVersion,
            string cudnnIdentity,
            string cudaDriverVersion,
            string cudaDriverIdentity,
            string nativeBridgeIdentity,
            string gpuCompatibilityIdentity,
            string gpuComputeCapability,
            string operatingSystem,
            string processArchitecture,
            string artifactExtension = ".engine",
            IEnumerable<string>? builderFlags = null,
            string adapterSchemaVersion = CurrentAdapterSchemaVersion)
        {
            OnnxSha256 = TensorRtContractHash.ValidateSha256(onnxSha256, nameof(onnxSha256));
            BuildOptions = buildOptions ?? throw new ArgumentNullException(nameof(buildOptions));
            ManagedBuildInputsSha256 = TensorRtOnnxEngineBuilder.GetBuildInputsSha256(OnnxSha256, buildOptions);
            ManagedPackageIdentity = TensorRtContractHash.ValidateIdentity(managedPackageIdentity, nameof(managedPackageIdentity));
            ManagedApiContractSha256 = TensorRtContractHash.ValidateSha256(managedApiContractSha256, nameof(managedApiContractSha256));
            TensorRtVersion = TensorRtContractHash.ValidateIdentity(tensorRtVersion, nameof(tensorRtVersion));
            TensorRtIdentity = TensorRtContractHash.ValidateIdentity(tensorRtIdentity, nameof(tensorRtIdentity));
            CudaRuntimeVersion = TensorRtContractHash.ValidateIdentity(cudaRuntimeVersion, nameof(cudaRuntimeVersion));
            CudaRuntimeIdentity = TensorRtContractHash.ValidateIdentity(cudaRuntimeIdentity, nameof(cudaRuntimeIdentity));
            CudnnVersion = TensorRtContractHash.ValidateIdentity(cudnnVersion, nameof(cudnnVersion));
            CudnnIdentity = TensorRtContractHash.ValidateIdentity(cudnnIdentity, nameof(cudnnIdentity));
            CudaDriverVersion = TensorRtContractHash.ValidateIdentity(cudaDriverVersion, nameof(cudaDriverVersion));
            CudaDriverIdentity = TensorRtContractHash.ValidateIdentity(cudaDriverIdentity, nameof(cudaDriverIdentity));
            NativeBridgeIdentity = TensorRtContractHash.ValidateIdentity(nativeBridgeIdentity, nameof(nativeBridgeIdentity));
            GpuCompatibilityIdentity = TensorRtContractHash.ValidateIdentity(gpuCompatibilityIdentity, nameof(gpuCompatibilityIdentity));
            GpuComputeCapability = TensorRtContractHash.ValidateIdentity(gpuComputeCapability, nameof(gpuComputeCapability));
            OperatingSystem = TensorRtContractHash.ValidateIdentity(operatingSystem, nameof(operatingSystem));
            ProcessArchitecture = TensorRtContractHash.ValidateIdentity(processArchitecture, nameof(processArchitecture));
            if (!string.Equals(adapterSchemaVersion, CurrentAdapterSchemaVersion, StringComparison.Ordinal))
            {
                throw new ArgumentException("Only the current TensorRT engine cache adapter schema is accepted.", nameof(adapterSchemaVersion));
            }
            AdapterSchemaVersion = adapterSchemaVersion;
            ArtifactExtension = NormalizeEngineExtension(artifactExtension);

            var flags = new List<string>();
            if (builderFlags != null)
            {
                foreach (string? flag in builderFlags)
                {
                    if (flag == null) throw new ArgumentException("Builder flags cannot contain null entries.", nameof(builderFlags));
                    flags.Add(TensorRtContractHash.ValidateText(flag, nameof(builderFlags), allowEmpty: false));
                }
            }
            flags.Sort(StringComparer.Ordinal);
            for (int index = 1; index < flags.Count; index++)
            {
                if (string.Equals(flags[index - 1], flags[index], StringComparison.Ordinal)) throw new ArgumentException("Builder flags must be unique.", nameof(builderFlags));
            }
            _builderFlags = new ReadOnlyCollection<string>(flags);
            _profiles = new ReadOnlyCollection<TensorRtOnnxInputProfile>(buildOptions.InputProfiles.ToList());
            ProfilesSha256 = ComputeProfilesSha256(_profiles);
            BuilderFlagsSha256 = TensorRtContractHash.Sequence(_builderFlags);
            LookupKeySha256 = TensorRtContractHash.Sequence(new[]
            {
                "deploysharp-tensorrt-engine-cache-lookup-v2",
                AdapterSchemaVersion,
                OnnxSha256,
                ManagedBuildInputsSha256,
                ManagedPackageIdentity,
                ManagedApiContractSha256,
                TensorRtVersion,
                TensorRtIdentity,
                CudaRuntimeVersion,
                CudaRuntimeIdentity,
                CudnnVersion,
                CudnnIdentity,
                CudaDriverVersion,
                CudaDriverIdentity,
                NativeBridgeIdentity,
                GpuCompatibilityIdentity,
                GpuComputeCapability,
                OperatingSystem,
                ProcessArchitecture,
                ((int)buildOptions.ApiVersion).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ((int)buildOptions.Precision).ToString(System.Globalization.CultureInfo.InvariantCulture),
                buildOptions.WorkspaceBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                buildOptions.OptimizationLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                buildOptions.StronglyTypedNetwork ? "true" : "false",
                ProfilesSha256,
                BuilderFlagsSha256,
                ArtifactExtension
            });
        }

        /// <summary>Gets the ONNX content SHA256.</summary>
        public string OnnxSha256 { get; }
        /// <summary>Gets the existing managed builder-input SHA256.</summary>
        public string ManagedBuildInputsSha256 { get; }
        /// <summary>Gets the exact managed dependency/package identity.</summary>
        public string ManagedPackageIdentity { get; }
        /// <summary>Gets the exact managed API contract SHA256.</summary>
        public string ManagedApiContractSha256 { get; }
        /// <summary>Gets the native TensorRT version.</summary>
        public string TensorRtVersion { get; }
        /// <summary>Gets the exact native TensorRT identity.</summary>
        public string TensorRtIdentity { get; }
        /// <summary>Gets the CUDA runtime version.</summary>
        public string CudaRuntimeVersion { get; }
        /// <summary>Gets the exact CUDA runtime identity.</summary>
        public string CudaRuntimeIdentity { get; }
        /// <summary>Gets the cuDNN version.</summary>
        public string CudnnVersion { get; }
        /// <summary>Gets the exact cuDNN identity.</summary>
        public string CudnnIdentity { get; }
        /// <summary>Gets the CUDA driver version.</summary>
        public string CudaDriverVersion { get; }
        /// <summary>Gets the exact CUDA driver identity.</summary>
        public string CudaDriverIdentity { get; }
        /// <summary>Gets the exact native bridge identity.</summary>
        public string NativeBridgeIdentity { get; }
        /// <summary>Gets the GPU model or caller-defined compatibility class; physical device UUIDs are intentionally excluded.</summary>
        public string GpuCompatibilityIdentity { get; }
        /// <summary>Gets the GPU compute capability.</summary>
        public string GpuComputeCapability { get; }
        /// <summary>Gets the operating-system identity.</summary>
        public string OperatingSystem { get; }
        /// <summary>Gets the process architecture.</summary>
        public string ProcessArchitecture { get; }
        /// <summary>Gets the requested .engine or .plan extension in canonical lowercase form.</summary>
        public string ArtifactExtension { get; }
        /// <summary>Gets the adapter cache schema version.</summary>
        public string AdapterSchemaVersion { get; }
        /// <summary>Gets the immutable managed build options.</summary>
        public TensorRtOnnxEngineBuildOptions BuildOptions { get; }
        /// <summary>Gets the exact sorted dynamic profiles represented in the lookup key.</summary>
        public IReadOnlyList<TensorRtOnnxInputProfile> InputProfiles => _profiles;
        /// <summary>Gets the exact sorted caller-supplied builder flags.</summary>
        public IReadOnlyList<string> BuilderFlags => _builderFlags;
        /// <summary>Gets the canonical profile SHA256.</summary>
        public string ProfilesSha256 { get; }
        /// <summary>Gets the canonical builder-flag SHA256.</summary>
        public string BuilderFlagsSha256 { get; }
        /// <summary>Gets the build-before-output lookup key.</summary>
        public string LookupKeySha256 { get; }

        private static string NormalizeEngineExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentException("A .engine or .plan extension is required.", nameof(extension));
            string normalized = extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
            if (!string.Equals(normalized, ".engine", StringComparison.OrdinalIgnoreCase) && !string.Equals(normalized, ".plan", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only .engine and .plan cache artifacts are supported.", nameof(extension));
            }
            return normalized.ToLowerInvariant();
        }

        private static string ComputeProfilesSha256(IEnumerable<TensorRtOnnxInputProfile> profiles)
        {
            var values = new List<string> { "deploysharp-tensorrt-engine-profiles-v1" };
            foreach (TensorRtOnnxInputProfile profile in profiles.OrderBy(item => item.InputName, StringComparer.Ordinal))
            {
                values.Add(profile.InputName);
                values.Add(FormatShape(profile.Minimum));
                values.Add(FormatShape(profile.Optimum));
                values.Add(FormatShape(profile.Maximum));
            }
            return TensorRtContractHash.Sequence(values);
        }

        private static string FormatShape(JYPPX.DeploySharp.Tensors.TensorShape shape)
        {
            return string.Join(",", shape.ToArray().Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>Describes integrity and layout metadata for one validated completed entry.</summary>
    public sealed class TensorRtExternalCacheEntryMetadata
    {
        internal TensorRtExternalCacheEntryMetadata(
            TensorRtExternalCacheEntryKind kind,
            string lookupKeySha256,
            string payloadSha256,
            long payloadLength,
            string manifestSha256,
            long manifestLength,
            string artifactExtension,
            DateTimeOffset createdUtc,
            string? cudaCacheKeySha256)
        {
            Kind = kind;
            LookupKeySha256 = lookupKeySha256;
            PayloadSha256 = payloadSha256;
            PayloadLength = payloadLength;
            ManifestSha256 = manifestSha256;
            ManifestLength = manifestLength;
            ArtifactExtension = artifactExtension;
            CreatedUtc = createdUtc;
            CudaCacheKeySha256 = cudaCacheKeySha256;
        }

        /// <summary>Gets the payload kind.</summary>
        public TensorRtExternalCacheEntryKind Kind { get; }
        /// <summary>Gets the build-before-output lookup key.</summary>
        public string LookupKeySha256 { get; }
        /// <summary>Gets the payload SHA256 verified during lookup/open.</summary>
        public string PayloadSha256 { get; }
        /// <summary>Gets the payload length verified during lookup/open.</summary>
        public long PayloadLength { get; }
        /// <summary>Gets the manifest SHA256 verified from the completion record.</summary>
        public string ManifestSha256 { get; }
        /// <summary>Gets the manifest length verified from the completion record.</summary>
        public long ManifestLength { get; }
        /// <summary>Gets the canonical payload extension.</summary>
        public string ArtifactExtension { get; }
        /// <summary>Gets the manifest creation timestamp; it does not participate in lookup identity.</summary>
        public DateTimeOffset CreatedUtc { get; }
        /// <summary>Gets the reconstructed existing CUDA artifact cache key, when applicable.</summary>
        public string? CudaCacheKeySha256 { get; }
    }

    /// <summary>Reports a non-payload External cache operation.</summary>
    public class TensorRtExternalCacheResult
    {
        internal TensorRtExternalCacheResult(
            TensorRtExternalCacheStatus status,
            TensorRtExternalCacheEntryMetadata? metadata = null,
            TensorRtExternalCacheRejectionReason rejectionReason = TensorRtExternalCacheRejectionReason.None,
            TensorRtExternalCacheRemediation remediation = TensorRtExternalCacheRemediation.None,
            string? remediationPath = null,
            bool factoryExecuted = false)
        {
            Status = status;
            Metadata = metadata;
            RejectionReason = rejectionReason;
            Remediation = remediation;
            RemediationPath = remediationPath;
            FactoryExecuted = factoryExecuted;
        }

        /// <summary>Gets the operation status.</summary>
        public TensorRtExternalCacheStatus Status { get; }
        /// <summary>Gets validated entry metadata when available.</summary>
        public TensorRtExternalCacheEntryMetadata? Metadata { get; }
        /// <summary>Gets a stable rejection classification.</summary>
        public TensorRtExternalCacheRejectionReason RejectionReason { get; }
        /// <summary>Gets the remediation performed for a rejected entry.</summary>
        public TensorRtExternalCacheRemediation Remediation { get; }
        /// <summary>Gets the caller-root-contained quarantine path when an entry was quarantined.</summary>
        public string? RemediationPath { get; }
        /// <summary>Gets whether an explicit compile/build factory was executed.</summary>
        public bool FactoryExecuted { get; }
        /// <summary>Gets the stable cache error code, or null for a normal miss/hit/store/delete.</summary>
        public string? ErrorCode => Status == TensorRtExternalCacheStatus.Rejected
            ? TensorRtErrorCodes.ExternalCacheEntryRejected
            : Status == TensorRtExternalCacheStatus.Conflict
                ? TensorRtErrorCodes.ExternalCacheConflict
                : null;
    }

    /// <summary>Reports CUDA cache lookup/store state and a reconstructed copied artifact on hit.</summary>
    public sealed class TensorRtCudaCacheResult : TensorRtExternalCacheResult
    {
        internal TensorRtCudaCacheResult(
            TensorRtExternalCacheStatus status,
            TensorRtCudaRtcArtifact? artifact = null,
            TensorRtExternalCacheEntryMetadata? metadata = null,
            TensorRtExternalCacheRejectionReason rejectionReason = TensorRtExternalCacheRejectionReason.None,
            TensorRtExternalCacheRemediation remediation = TensorRtExternalCacheRemediation.None,
            string? remediationPath = null,
            bool factoryExecuted = false)
            : base(status, metadata, rejectionReason, remediation, remediationPath, factoryExecuted)
        {
            Artifact = artifact;
        }

        /// <summary>Gets the reconstructed copied PTX/CUBIN artifact on a hit or completed factory store.</summary>
        public TensorRtCudaRtcArtifact? Artifact { get; }
    }

    /// <summary>Reports engine cache state and owns a validated seekable read stream on hit.</summary>
    public sealed class TensorRtEngineCacheResult : TensorRtExternalCacheResult, IDisposable
    {
        private Stream? _stream;

        internal TensorRtEngineCacheResult(
            TensorRtExternalCacheStatus status,
            Stream? stream = null,
            TensorRtExternalCacheEntryMetadata? metadata = null,
            TensorRtExternalCacheRejectionReason rejectionReason = TensorRtExternalCacheRejectionReason.None,
            TensorRtExternalCacheRemediation remediation = TensorRtExternalCacheRemediation.None,
            string? remediationPath = null,
            bool factoryExecuted = false)
            : base(status, metadata, rejectionReason, remediation, remediationPath, factoryExecuted)
        {
            _stream = stream;
        }

        /// <summary>Gets the validated stream, positioned at zero, while this result remains undisposed.</summary>
        public Stream? Stream => _stream;

        /// <summary>Closes the validated engine/plan stream without modifying the cache entry.</summary>
        public void Dispose()
        {
            Stream? stream = Interlocked.Exchange(ref _stream, null);
            stream?.Dispose();
        }

        internal Stream DetachStream()
        {
            return Interlocked.Exchange(ref _stream, null) ?? throw new InvalidOperationException("No cache stream is available.");
        }
    }
}
