using System.Collections.Generic;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.OpenVINO
{
    /// <summary>Provides the filesystem preflight for OpenVINO runtime and device plug-ins. / 提供 OpenVINO 运行时和设备插件的文件系统预检。</summary>
    public sealed class OpenVinoRuntimeProbe : IBackendRuntimeProbe
    {
        private readonly FileSystemBackendRuntimeProbe _probe;

        /// <summary>Initializes the probe with application-owned runtime paths. / 使用应用拥有的运行时路径初始化探针。</summary>
        public OpenVinoRuntimeProbe(BackendPluginContext? context = null)
        {
            _probe = new FileSystemBackendRuntimeProbe(context, new Dictionary<NativeRuntimeKind, IReadOnlyList<string>>
            {
                [NativeRuntimeKind.OpenVINO] = new[] { "openvino_c.dll", "libopenvino_c.so" }
            });
        }

        /// <inheritdoc />
        public System.Threading.Tasks.Task<BackendRuntimeStatus> ProbeAsync(BackendPluginDescriptor plugin, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) => _probe.ProbeAsync(plugin, cancellationToken);
    }
}
