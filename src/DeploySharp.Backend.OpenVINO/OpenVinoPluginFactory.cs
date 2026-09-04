using System;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.OpenVINO
{
    /// <summary>Adapts OpenVINO to the manifest-driven plugin contract. / 将 OpenVINO 适配到清单驱动插件合同。</summary>
    public sealed class OpenVinoPluginFactory : IBackendPluginFactory
    {
        private readonly OpenVinoOptions _options;

        /// <summary>Initializes a factory for the configured OpenVINO device. / 为配置的 OpenVINO 设备初始化工厂。</summary>
        public OpenVinoPluginFactory(OpenVinoOptions? options = null)
        {
            _options = options ?? OpenVinoOptions.Default;
            using (var provider = new OpenVinoBackendProvider(_options))
            {
                BackendDescriptor backend = provider.Descriptor;
                Descriptor = new BackendPluginDescriptor(
                    "openvino",
                    backend.DisplayName,
                    backend.Version,
                    backend,
                    targetFrameworks: new[] { "net48", "net8.0", "net10.0" },
                    runtimeIdentifiers: new[] { "win-x64", "linux-x64" },
                    executionMode: backend.PreferredExecutionMode,
                    formats: backend.SupportedFormats,
                    runtimeDependencies: new[]
                    {
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "JYPPX.OpenVINO.CSharp.API", "3.3.0"),
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "OpenVINO.runtime.win", "2026.2.1", "win-x64", downloadable: true, licenseExpression: "Apache-2.0")
                    },
                    nativeRequirements: new[]
                    {
                        new NativeRuntimeRequirement(NativeRuntimeKind.OpenVINO, minimumVersion: "2026.2", runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "DEPLOYSHARP_OPENVINO_ROOT" })
                    },
                    optionsSchema: backend.OptionsSchema as BackendOptionsSchema,
                    probeId: backend.NativeProbeId,
                    entryPoint: "JYPPX.DeploySharp.Backends.OpenVINO.OpenVinoBackendProvider");
            }
        }

        /// <inheritdoc />
        public BackendPluginDescriptor Descriptor { get; }
        /// <inheritdoc />
        public IDisposable Create(BackendPluginContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new OpenVinoBackendProvider(_options);
        }

        /// <summary>Creates the strongly typed OpenVINO provider. / 创建强类型 OpenVINO Provider。</summary>
        public OpenVinoBackendProvider CreateBackendProvider(BackendPluginContext context) => (OpenVinoBackendProvider)Create(context);
    }
}
