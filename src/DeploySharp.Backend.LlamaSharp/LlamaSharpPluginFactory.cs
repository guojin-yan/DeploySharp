using System;
using JYPPX.DeploySharp.Extensibility;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.LLM.Prompt;

namespace JYPPX.DeploySharp.Backends.LlamaSharp
{
    /// <summary>Adapts LLamaSharp to the manifest-driven plugin contract. / 将 LLamaSharp 适配到清单驱动插件合同。</summary>
    public sealed class LlamaSharpPluginFactory : IBackendPluginFactory
    {
        private readonly LlamaSharpOptions _options;
        private readonly IPromptFormatter? _promptFormatter;

        /// <summary>Initializes a factory for local GGUF sessions. / 为本地 GGUF 会话初始化工厂。</summary>
        public LlamaSharpPluginFactory(LlamaSharpOptions? options = null, IPromptFormatter? promptFormatter = null)
        {
            _options = options ?? LlamaSharpOptions.Default;
            _promptFormatter = promptFormatter;
            using (var provider = new LlamaSharpBackendProvider(_options, _promptFormatter))
            {
                BackendDescriptor backend = provider.Descriptor;
                Descriptor = new BackendPluginDescriptor(
                    "llamasharp",
                    backend.DisplayName,
                    backend.Version,
                    backend,
                    targetFrameworks: new[] { "netstandard2.0", "net8.0" },
                    runtimeIdentifiers: new[] { "win-x64", "linux-x64", "linux-arm64" },
                    executionMode: backend.PreferredExecutionMode,
                    formats: backend.SupportedFormats,
                    runtimeDependencies: new[]
                    {
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "LLamaSharp", "0.27.0"),
                        new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "LLamaSharp.Backend.Cpu", "0.27.0", downloadable: true, licenseExpression: "MIT")
                    },
                    nativeRequirements: new[]
                    {
                        new NativeRuntimeRequirement(NativeRuntimeKind.LlamaSharpNative, runtimeIdentifiers: new[] { "win-x64", "linux-x64", "linux-arm64" }, environmentVariables: new[] { "LLAMASHARP_BACKEND_PATH" })
                    },
                    optionsSchema: backend.OptionsSchema as BackendOptionsSchema,
                    probeId: backend.NativeProbeId,
                    entryPoint: "JYPPX.DeploySharp.Backends.LlamaSharp.LlamaSharpBackendProvider");
            }
        }

        /// <inheritdoc />
        public BackendPluginDescriptor Descriptor { get; }
        /// <inheritdoc />
        public IDisposable Create(BackendPluginContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new LlamaSharpBackendProvider(_options, _promptFormatter);
        }

        /// <summary>Creates the strongly typed LLamaSharp language provider. / 创建强类型 LLamaSharp 语言 Provider。</summary>
        public LlamaSharpBackendProvider CreateLanguageProvider(BackendPluginContext context) => (LlamaSharpBackendProvider)Create(context);
    }
}
