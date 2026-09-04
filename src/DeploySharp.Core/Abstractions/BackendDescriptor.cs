using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp
{
    /// <summary>
    /// Provides immutable identity and capability metadata for a backend provider. / 提供后端提供程序的不可变标识与能力元数据。
    /// </summary>
    public sealed class BackendDescriptor
    {
        private readonly IReadOnlyList<string> _supportedFormats;
        private readonly IReadOnlyList<string> _supportedTargetFrameworks;
        private readonly IReadOnlyList<string> _supportedRuntimeIdentifiers;
        private readonly IReadOnlyList<string> _supportedDevices;
        private readonly IReadOnlyList<IBackendRuntimeDependency> _runtimeDependencies;

        /// <summary>
        /// Initializes backend metadata. / 初始化后端元数据。
        /// </summary>
        public BackendDescriptor(
            BackendId id,
            string displayName,
            string version,
            BackendCapabilities capabilities,
            IEnumerable<string>? supportedFormats = null)
            : this(id, displayName, version, capabilities, supportedFormats, null, null, null, null, null, null, null, BackendExecutionMode.InProcess, null, null, null, null)
        {
        }

        /// <summary>
        /// Initializes backend metadata with optional application-facing runtime contract fields. / 使用可选的应用侧运行时合同字段初始化后端元数据。
        /// </summary>
        public BackendDescriptor(
            BackendId id,
            string displayName,
            string version,
            BackendCapabilities capabilities,
            IEnumerable<string>? supportedFormats,
            string? description,
            string? iconKey,
            IEnumerable<string>? supportedTargetFrameworks,
            IEnumerable<string>? supportedRuntimeIdentifiers,
            IEnumerable<string>? supportedDevices,
            string? providerPackageId,
            string? providerPackageVersion,
            BackendExecutionMode preferredExecutionMode,
            IEnumerable<IBackendRuntimeDependency>? runtimeDependencies,
            string? nativeProbeId,
            IBackendOptionsSchema? optionsSchema,
            string? healthCheckId)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("A backend identifier is required.", nameof(id));
            }

            Id = id;
            DisplayName = Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
            Version = Guard.NotNullOrWhiteSpace(version, nameof(version));
            Capabilities = capabilities;
            Description = string.IsNullOrWhiteSpace(description) ? DisplayName : Guard.NotNullOrWhiteSpace(description, nameof(description)).Trim();
            IconKey = string.IsNullOrWhiteSpace(iconKey) ? null : Guard.Identifier(iconKey, nameof(iconKey));
            _supportedTargetFrameworks = NormalizeIdentifiers(supportedTargetFrameworks, nameof(supportedTargetFrameworks));
            _supportedRuntimeIdentifiers = NormalizeIdentifiers(supportedRuntimeIdentifiers, nameof(supportedRuntimeIdentifiers));
            _supportedDevices = NormalizeIdentifiers(supportedDevices, nameof(supportedDevices));
            ProviderPackageId = string.IsNullOrWhiteSpace(providerPackageId) ? null : PackageIdentifier(providerPackageId!, nameof(providerPackageId));
            ProviderPackageVersion = string.IsNullOrWhiteSpace(providerPackageVersion) ? null : Guard.NotNullOrWhiteSpace(providerPackageVersion, nameof(providerPackageVersion));
            if (!Enum.IsDefined(typeof(BackendExecutionMode), preferredExecutionMode)) throw new ArgumentOutOfRangeException(nameof(preferredExecutionMode));
            PreferredExecutionMode = preferredExecutionMode;
            NativeProbeId = string.IsNullOrWhiteSpace(nativeProbeId) ? null : Guard.Identifier(nativeProbeId, nameof(nativeProbeId));
            OptionsSchema = optionsSchema;
            HealthCheckId = string.IsNullOrWhiteSpace(healthCheckId) ? null : Guard.Identifier(healthCheckId, nameof(healthCheckId));

            var dependencies = new List<IBackendRuntimeDependency>();
            if (runtimeDependencies != null)
            {
                foreach (IBackendRuntimeDependency dependency in runtimeDependencies)
                {
                    if (dependency == null) throw new ArgumentException("Runtime dependencies cannot contain null entries.", nameof(runtimeDependencies));
                    for (int index = 0; index < dependencies.Count; index++)
                    {
                        if (string.Equals(dependencies[index].Identity, dependency.Identity, StringComparison.Ordinal))
                        {
                            throw new ArgumentException("Runtime dependencies must have unique identities.", nameof(runtimeDependencies));
                        }
                    }
                    dependencies.Add(dependency);
                }
            }
            _runtimeDependencies = dependencies.AsReadOnly();

            var formats = new List<string>();
            if (supportedFormats != null)
            {
                foreach (string format in supportedFormats)
                {
                    formats.Add(Guard.Identifier(format, nameof(supportedFormats)));
                }
            }

            _supportedFormats = formats.AsReadOnly();
        }

        /// <summary>Gets the stable backend identifier. / 获取稳定的后端标识符。</summary>
        public BackendId Id { get; }

        /// <summary>Gets the user-facing backend name. / 获取面向用户的后端名称。</summary>
        public string DisplayName { get; }

        /// <summary>Gets the managed backend adapter version. / 获取托管后端适配器版本。</summary>
        public string Version { get; }

        /// <summary>Gets the capabilities declared by this backend. / 获取此后端声明的能力。</summary>
        public BackendCapabilities Capabilities { get; }

        /// <summary>Gets a concise backend description. / 获取简要后端描述。</summary>
        public string Description { get; }

        /// <summary>Gets an optional application icon key. / 获取可选的应用图标键。</summary>
        public string? IconKey { get; }

        /// <summary>Gets target frameworks supported by this backend contract. / 获取此后端合同支持的目标框架。</summary>
        public IReadOnlyList<string> SupportedTargetFrameworks => _supportedTargetFrameworks;

        /// <summary>Gets runtime identifiers supported by this backend contract. / 获取此后端合同支持的运行时标识。</summary>
        public IReadOnlyList<string> SupportedRuntimeIdentifiers => _supportedRuntimeIdentifiers;

        /// <summary>Gets backend device names supported by this contract. / 获取此合同支持的设备名称。</summary>
        public IReadOnlyList<string> SupportedDevices => _supportedDevices;

        /// <summary>Gets the managed provider package identity, when known. / 获取已知的托管 Provider 包标识。</summary>
        public string? ProviderPackageId { get; }

        /// <summary>Gets the managed provider package version, when known. / 获取已知的托管 Provider 包版本。</summary>
        public string? ProviderPackageVersion { get; }

        /// <summary>Gets the preferred execution isolation mode. / 获取首选执行隔离模式。</summary>
        public BackendExecutionMode PreferredExecutionMode { get; }

        /// <summary>Gets declared runtime dependencies. / 获取声明的运行时依赖。</summary>
        public IReadOnlyList<IBackendRuntimeDependency> RuntimeDependencies => _runtimeDependencies;

        /// <summary>Gets the native runtime probe identifier, when known. / 获取已知的原生运行时探针标识。</summary>
        public string? NativeProbeId { get; }

        /// <summary>Gets the serializable options schema, when supplied by an extension package. / 获取扩展包提供的可序列化参数 schema。</summary>
        public IBackendOptionsSchema? OptionsSchema { get; }

        /// <summary>Gets the optional health-check identifier. / 获取可选的健康检查标识。</summary>
        public string? HealthCheckId { get; }

        /// <summary>Gets normalized model formats accepted by this backend. / 获取此后端接受的规范化模型格式。</summary>
        public IReadOnlyList<string> SupportedFormats => _supportedFormats;

        /// <summary>
        /// Determines whether the descriptor includes every requested capability. / 确定描述信息是否包含全部请求能力。
        /// </summary>
        public bool Supports(BackendCapabilities requiredCapabilities)
        {
            return (Capabilities & requiredCapabilities) == requiredCapabilities;
        }

        private static IReadOnlyList<string> NormalizeIdentifiers(IEnumerable<string>? values, string parameterName, bool lowerCase = true)
        {
            var result = new List<string>();
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (value == null) throw new ArgumentException("Identifier collections cannot contain null entries.", parameterName);
                    string normalized = lowerCase ? value.Trim().ToLowerInvariant() : value.Trim();
                    result.Add(Guard.Identifier(normalized, parameterName));
                }
            }
            return result.AsReadOnly();
        }

        private static string PackageIdentifier(string value, string parameterName)
        {
            string packageId = Guard.NotNullOrWhiteSpace(value, parameterName).Trim();
            for (int index = 0; index < packageId.Length; index++)
            {
                char c = packageId[index];
                bool valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_';
                if (!valid) throw new ArgumentException("Package IDs contain only letters, numbers, '.', '-', or '_'.", parameterName);
            }
            return packageId;
        }
    }
}
