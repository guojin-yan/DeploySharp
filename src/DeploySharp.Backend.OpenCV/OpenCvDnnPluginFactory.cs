using System;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Adapts OpenCV DNN to the manifest-driven plugin contract. / 将 OpenCV DNN 适配到清单驱动插件合同。</summary>
    public sealed class OpenCvDnnPluginFactory : IBackendPluginFactory
    {
        private readonly OpenCvDnnOptions _options;

        /// <summary>Initializes a factory bound to one OpenCV model contract. / 初始化绑定到一个 OpenCV 模型合同的工厂。</summary>
        public OpenCvDnnPluginFactory(OpenCvDnnOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            using (var provider = new OpenCvDnnBackendProvider(_options))
            {
                BackendDescriptor backend = provider.Descriptor;
                Descriptor = new BackendPluginDescriptor(
                    "opencv-dnn",
                    backend.DisplayName,
                    backend.Version,
                    backend,
                    targetFrameworks: new[] { "net48", "net8.0", "net10.0" },
                    runtimeIdentifiers: new[] { "win-x64" },
                    executionMode: backend.PreferredExecutionMode,
                    formats: backend.SupportedFormats,
                    runtimeDependencies: new[]
                    {
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "JYPPX.OpenCV.CSharp.API", "5.0.0-preview.1"),
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "JYPPX.OpenCV.runtime.win-x64", "5.0.0-preview.1", "win-x64", downloadable: true, licenseExpression: "Apache-2.0")
                    },
                    nativeRequirements: new[]
                    {
                        new NativeRuntimeRequirement(NativeRuntimeKind.OpenCV, minimumVersion: "5.0.0-preview.1", runtimeIdentifiers: new[] { "win-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "DEPLOYSHARP_OPENCV_ROOT" })
                    },
                    optionsSchema: backend.OptionsSchema as BackendOptionsSchema,
                    probeId: backend.NativeProbeId,
                    entryPoint: "JYPPX.DeploySharp.Backends.OpenCV.OpenCvDnnBackendProvider");
            }
        }

        /// <inheritdoc />
        public BackendPluginDescriptor Descriptor { get; }
        /// <inheritdoc />
        public IDisposable Create(BackendPluginContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new OpenCvDnnBackendProvider(_options);
        }

        /// <summary>Creates the strongly typed OpenCV DNN provider. / 创建强类型 OpenCV DNN Provider。</summary>
        public OpenCvDnnBackendProvider CreateBackendProvider(BackendPluginContext context) => (OpenCvDnnBackendProvider)Create(context);
    }
}
