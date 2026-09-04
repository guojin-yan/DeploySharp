using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime
{
    /// <summary>Adapts the ONNX Runtime provider to the manifest-driven plugin contract. / 将 ONNX Runtime Provider 适配到清单驱动插件合同。</summary>
    public sealed class OnnxRuntimePluginFactory : IBackendPluginFactory
    {
        private readonly OnnxRuntimeOptions _options;

        /// <summary>Initializes a factory for one CPU or CUDA execution-provider choice. / 为 CPU 或 CUDA 执行提供程序选择初始化工厂。</summary>
        public OnnxRuntimePluginFactory(OnnxRuntimeOptions? options = null)
        {
            _options = options ?? OnnxRuntimeOptions.Default;
            using (var provider = new OnnxRuntimeBackendProvider(_options))
            {
                BackendDescriptor backend = provider.Descriptor;
                Descriptor = new BackendPluginDescriptor(
                    "onnxruntime",
                    backend.DisplayName,
                    backend.Version,
                    backend,
                    targetFrameworks: new[] { "netstandard2.0", "net8.0" },
                    runtimeIdentifiers: new[] { "win-x64", "linux-x64", "linux-arm64" },
                    executionMode: backend.PreferredExecutionMode,
                    formats: backend.SupportedFormats,
                    runtimeDependencies: new[]
                    {
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "Microsoft.ML.OnnxRuntime.Managed", "1.28.0"),
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "Microsoft.ML.OnnxRuntime", "1.28.0", downloadable: true, licenseExpression: "MIT"),
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "Microsoft.ML.OnnxRuntime.Gpu.Windows", "1.28.0", "win-x64", downloadable: true, licenseExpression: "MIT", condition: "executionProvider == cuda")
                    },
                    nativeRequirements: new[]
                    {
                        new NativeRuntimeRequirement(NativeRuntimeKind.OnnxRuntimeNative, minimumVersion: "1.28.0", runtimeIdentifiers: new[] { "win-x64", "linux-x64", "linux-arm64" })
                    },
                    optionsSchema: backend.OptionsSchema as BackendOptionsSchema,
                    probeId: backend.NativeProbeId,
                    entryPoint: "JYPPX.DeploySharp.Backends.OnnxRuntime.OnnxRuntimeBackendProvider");
            }
        }

        /// <inheritdoc />
        public BackendPluginDescriptor Descriptor { get; }
        /// <inheritdoc />
        public IDisposable Create(BackendPluginContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new OnnxRuntimeBackendProvider(_options);
        }

        /// <summary>Creates the strongly typed ONNX Runtime provider. / 创建强类型 ONNX Runtime Provider。</summary>
        public OnnxRuntimeBackendProvider CreateBackendProvider(BackendPluginContext context) => (OnnxRuntimeBackendProvider)Create(context);
    }
}
