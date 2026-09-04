using System.Collections.Generic;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.LlamaSharp
{
    /// <summary>Provides a safe filesystem preflight for LLamaSharp CPU/GPU native backends. / 提供 LLamaSharp CPU/GPU 原生后端的安全文件系统预检。</summary>
    public sealed class LlamaSharpRuntimeProbe : IBackendRuntimeProbe
    {
        private readonly FileSystemBackendRuntimeProbe _probe;

        /// <summary>Initializes the probe with application-owned backend paths. / 使用应用拥有的后端路径初始化探针。</summary>
        public LlamaSharpRuntimeProbe(BackendPluginContext? context = null)
        {
            _probe = new FileSystemBackendRuntimeProbe(context, new Dictionary<NativeRuntimeKind, IReadOnlyList<string>>
            {
                [NativeRuntimeKind.LlamaSharpNative] = new[] { "llama.dll", "ggml.dll", "libllama.so", "libggml.so" }
            });
        }

        /// <inheritdoc />
        public System.Threading.Tasks.Task<BackendRuntimeStatus> ProbeAsync(BackendPluginDescriptor plugin, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) => _probe.ProbeAsync(plugin, cancellationToken);
    }
}
