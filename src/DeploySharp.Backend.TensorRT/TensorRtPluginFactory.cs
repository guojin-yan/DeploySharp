using System;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Adapts TensorRT to the manifest-driven plugin contract. / 将 TensorRT 适配到清单驱动插件合同。</summary>
    public sealed class TensorRtPluginFactory : IBackendPluginFactory
    {
        private readonly TensorRtBackendOptions _options;

        /// <summary>Initializes a factory for one TensorRT API line. / 为一个 TensorRT API 线初始化工厂。</summary>
        public TensorRtPluginFactory(TensorRtBackendOptions? options = null)
        {
            _options = options ?? TensorRtBackendOptions.Default;
            using (var provider = new TensorRtBackendProvider(_options))
            {
                BackendDescriptor backend = provider.Descriptor;
                Descriptor = new BackendPluginDescriptor(
                    "tensorrt",
                    backend.DisplayName,
                    backend.Version,
                    backend,
                    targetFrameworks: new[] { "net8.0" },
                    runtimeIdentifiers: new[] { "win-x64", "linux-x64" },
                    executionMode: BackendExecutionMode.Worker,
                    formats: backend.SupportedFormats,
                    runtimeDependencies: new[]
                    {
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "JYPPX.TensorRT.CSharp.API", "4.0.0"),
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.Environment, environmentVariables: new[] { "JYPPX_CUDA_ROOT", "CUDA_PATH", "JYPPX_CUDNN_ROOT", "JYPPX_TENSORRT_ROOT", "JYPPX_NATIVE_BRIDGE_PATH", "DEPLOYSHARP_TENSORRT_API_VERSION" }, requiresUserSelectedRoot: true)
                    },
                    nativeRequirements: new[]
                    {
                        new NativeRuntimeRequirement(NativeRuntimeKind.CUDA, runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "JYPPX_CUDA_ROOT", "CUDA_PATH" }),
                        new NativeRuntimeRequirement(NativeRuntimeKind.CuDNN, runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "JYPPX_CUDNN_ROOT" }),
                        new NativeRuntimeRequirement(NativeRuntimeKind.TensorRT, apiLine: ((int)_options.ApiVersion).ToString(), runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "JYPPX_TENSORRT_ROOT" }),
                        new NativeRuntimeRequirement(NativeRuntimeKind.NVRTC, runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true),
                        new NativeRuntimeRequirement(NativeRuntimeKind.Driver, runtimeIdentifiers: new[] { "win-x64", "linux-x64" }),
                        new NativeRuntimeRequirement(NativeRuntimeKind.Unknown, apiLine: "bridge", runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "JYPPX_NATIVE_BRIDGE_PATH" })
                    },
                    optionsSchema: backend.OptionsSchema as BackendOptionsSchema,
                    probeId: backend.NativeProbeId,
                    entryPoint: "JYPPX.DeploySharp.Backends.TensorRT.TensorRtBackendProvider");
            }
        }

        /// <inheritdoc />
        public BackendPluginDescriptor Descriptor { get; }
        /// <inheritdoc />
        public IDisposable Create(BackendPluginContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new TensorRtBackendProvider(_options);
        }

        /// <summary>Creates the strongly typed TensorRT provider. / 创建强类型 TensorRT Provider。</summary>
        public TensorRtBackendProvider CreateBackendProvider(BackendPluginContext context) => (TensorRtBackendProvider)Create(context);
    }
}
