using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Describes an installable backend plugin and all host-visible runtime contracts. / 描述可安装后端插件及宿主可见的全部运行时合同。</summary>
    public sealed class BackendPluginDescriptor
    {
        /// <summary>Initializes a plugin descriptor. / 初始化插件描述。</summary>
        public BackendPluginDescriptor(
            string pluginId,
            string displayName,
            string version,
            BackendDescriptor backend,
            IEnumerable<string>? targetFrameworks = null,
            IEnumerable<string>? runtimeIdentifiers = null,
            BackendExecutionMode executionMode = BackendExecutionMode.InProcess,
            BackendCapabilities capabilities = BackendCapabilities.None,
            IEnumerable<string>? formats = null,
            string? providerPackageId = null,
            string? providerPackageVersion = null,
            IEnumerable<BackendRuntimeDependency>? runtimeDependencies = null,
            IEnumerable<NativeRuntimeRequirement>? nativeRequirements = null,
            BackendOptionsSchema? optionsSchema = null,
            string? probeId = null,
            string? entryPoint = null)
        {
            PluginId = ExtGuard.Identifier(pluginId, nameof(pluginId));
            DisplayName = ExtGuard.NotNullOrWhiteSpace(displayName, nameof(displayName));
            Version = ExtGuard.NotNullOrWhiteSpace(version, nameof(version));
            Backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (!Enum.IsDefined(typeof(BackendExecutionMode), executionMode)) throw new ArgumentOutOfRangeException(nameof(executionMode));
            ExecutionMode = executionMode;
            TargetFrameworks = ContractValidation.Identifiers(targetFrameworks, nameof(targetFrameworks));
            RuntimeIdentifiers = ContractValidation.Identifiers(runtimeIdentifiers, nameof(runtimeIdentifiers));
            Capabilities = capabilities == BackendCapabilities.None ? backend.Capabilities : capabilities;
            Formats = ContractValidation.Identifiers(formats ?? backend.SupportedFormats, nameof(formats));
            ProviderPackageId = string.IsNullOrWhiteSpace(providerPackageId) ? backend.ProviderPackageId : ExtGuard.Identifier(providerPackageId, nameof(providerPackageId));
            ProviderPackageVersion = string.IsNullOrWhiteSpace(providerPackageVersion) ? backend.ProviderPackageVersion : ExtGuard.NotNullOrWhiteSpace(providerPackageVersion, nameof(providerPackageVersion));
            RuntimeDependencies = ContractValidation.Items(runtimeDependencies, nameof(runtimeDependencies));
            NativeRequirements = ContractValidation.Items(nativeRequirements, nameof(nativeRequirements));
            OptionsSchema = optionsSchema;
            ProbeId = string.IsNullOrWhiteSpace(probeId) ? null : ExtGuard.Identifier(probeId, nameof(probeId));
            EntryPoint = ContractValidation.Path(entryPoint, nameof(entryPoint));
        }

        /// <summary>Gets the stable plugin identifier. / 获取稳定插件标识。</summary>
        public string PluginId { get; }
        /// <summary>Gets the user-facing plugin name. / 获取面向用户的插件名称。</summary>
        public string DisplayName { get; }
        /// <summary>Gets the plugin contract version. / 获取插件合同版本。</summary>
        public string Version { get; }
        /// <summary>Gets the compatible Core backend descriptor. / 获取兼容的 Core 后端描述。</summary>
        public BackendDescriptor Backend { get; }
        /// <summary>Gets target frameworks declared by the plugin. / 获取插件声明的目标框架。</summary>
        public IReadOnlyList<string> TargetFrameworks { get; }
        /// <summary>Gets runtime identifiers declared by the plugin. / 获取插件声明的运行时标识。</summary>
        public IReadOnlyList<string> RuntimeIdentifiers { get; }
        /// <summary>Gets the preferred execution isolation mode. / 获取首选执行隔离模式。</summary>
        public BackendExecutionMode ExecutionMode { get; }
        /// <summary>Gets plugin capabilities. / 获取插件能力。</summary>
        public BackendCapabilities Capabilities { get; }
        /// <summary>Gets model formats accepted by the plugin. / 获取插件接受的模型格式。</summary>
        public IReadOnlyList<string> Formats { get; }
        /// <summary>Gets the provider package ID. / 获取 Provider 包 ID。</summary>
        public string? ProviderPackageId { get; }
        /// <summary>Gets the provider package version. / 获取 Provider 包版本。</summary>
        public string? ProviderPackageVersion { get; }
        /// <summary>Gets managed and external runtime dependencies. / 获取托管和外部运行时依赖。</summary>
        public IReadOnlyList<BackendRuntimeDependency> RuntimeDependencies { get; }
        /// <summary>Gets native runtime requirements. / 获取原生运行时需求。</summary>
        public IReadOnlyList<NativeRuntimeRequirement> NativeRequirements { get; }
        /// <summary>Gets the host-generated options schema. / 获取供宿主生成界面的参数 schema。</summary>
        public BackendOptionsSchema? OptionsSchema { get; }
        /// <summary>Gets the native probe identifier. / 获取原生探针标识。</summary>
        public string? ProbeId { get; }
        /// <summary>Gets the optional plugin entry-point path or type name. / 获取可选的插件入口路径或类型名。</summary>
        public string? EntryPoint { get; }
    }
}
