using System.Collections.Generic;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Provides the filesystem preflight for the OpenCV native runtime. / 提供 OpenCV 原生运行时文件系统预检。</summary>
    public sealed class OpenCvRuntimeProbe : IBackendRuntimeProbe
    {
        private readonly FileSystemBackendRuntimeProbe _probe;

        /// <summary>Initializes the probe with application-owned runtime paths. / 使用应用拥有的运行时路径初始化探针。</summary>
        public OpenCvRuntimeProbe(BackendPluginContext? context = null)
        {
            _probe = new FileSystemBackendRuntimeProbe(context, new Dictionary<NativeRuntimeKind, IReadOnlyList<string>>
            {
                [NativeRuntimeKind.OpenCV] = new[] { "opencv_world500.dll", "opencv_world490.dll", "opencv_world480.dll", "libopencv_core.so" }
            });
        }

        /// <inheritdoc />
        public System.Threading.Tasks.Task<BackendRuntimeStatus> ProbeAsync(BackendPluginDescriptor plugin, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) => _probe.ProbeAsync(plugin, cancellationToken);
    }
}
