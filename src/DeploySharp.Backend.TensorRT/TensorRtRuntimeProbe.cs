using System.Collections.Generic;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Provides a safe filesystem preflight for TensorRT native roots. / 提供 TensorRT 原生根目录的安全文件系统预检。</summary>
    public sealed class TensorRtRuntimeProbe : IBackendRuntimeProbe
    {
        private readonly FileSystemBackendRuntimeProbe _probe;

        /// <summary>Initializes the probe with application-owned roots. / 使用应用拥有的根目录初始化探针。</summary>
        public TensorRtRuntimeProbe(BackendPluginContext? context = null)
        {
            _probe = new FileSystemBackendRuntimeProbe(context, new Dictionary<NativeRuntimeKind, IReadOnlyList<string>>
            {
                [NativeRuntimeKind.CUDA] = new[] { "cudart64_12.dll", "cudart64_110.dll", "libcudart.so" },
                [NativeRuntimeKind.CuDNN] = new[] { "cudnn64_9.dll", "cudnn64_8.dll", "libcudnn.so" },
                [NativeRuntimeKind.TensorRT] = new[] { "nvinfer.dll", "nvinfer_10.dll", "nvinfer_11.dll", "libnvinfer.so" },
                [NativeRuntimeKind.NVRTC] = new[] { "nvrtc64_120_0.dll", "nvrtc64_110_0.dll", "libnvrtc.so" },
                [NativeRuntimeKind.Driver] = new[] { "nvcuda.dll", "libcuda.so" },
                [NativeRuntimeKind.Unknown] = new[] { "jyppxtrtbridge.dll", "libjyppxtrtbridge.so" }
            });
        }

        /// <inheritdoc />
        public System.Threading.Tasks.Task<BackendRuntimeStatus> ProbeAsync(BackendPluginDescriptor plugin, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) => _probe.ProbeAsync(plugin, cancellationToken);
    }
}
