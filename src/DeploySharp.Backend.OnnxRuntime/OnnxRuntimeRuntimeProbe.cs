using System.Collections.Generic;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime
{
    /// <summary>Provides the filesystem preflight for ONNX Runtime native assets. / 提供 ONNX Runtime 原生资产的文件系统预检。</summary>
    public sealed class OnnxRuntimeRuntimeProbe : IBackendRuntimeProbe
    {
        private readonly FileSystemBackendRuntimeProbe _probe;

        /// <summary>Initializes the probe with application-owned runtime paths. / 使用应用拥有的运行时路径初始化探针。</summary>
        public OnnxRuntimeRuntimeProbe(BackendPluginContext? context = null)
        {
            _probe = new FileSystemBackendRuntimeProbe(context, new Dictionary<NativeRuntimeKind, IReadOnlyList<string>>
            {
                [NativeRuntimeKind.OnnxRuntimeNative] = new[] { "onnxruntime.dll", "libonnxruntime.so" }
            });
        }

        /// <inheritdoc />
        public System.Threading.Tasks.Task<BackendRuntimeStatus> ProbeAsync(BackendPluginDescriptor plugin, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) => _probe.ProbeAsync(plugin, cancellationToken);
    }
}
