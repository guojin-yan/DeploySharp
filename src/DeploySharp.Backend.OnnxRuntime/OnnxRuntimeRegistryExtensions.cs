using System;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime
{
    /// <summary>Provides explicit Core registry helpers for ONNX Runtime. / 提供 ONNX Runtime 的显式 Core 注册辅助方法。</summary>
    public static class OnnxRuntimeRegistryExtensions
    {
        /// <summary>Registers an ONNX Runtime provider owned by the Core registry. / 注册一个由 Core 注册中心持有的 ONNX Runtime Provider。</summary>
        public static BackendRegistry UseOnnxRuntime(this BackendRegistry registry, OnnxRuntimeOptions? options = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.Register(new OnnxRuntimeBackendProvider(options));
            return registry;
        }
    }
}
