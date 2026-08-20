using System;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Provides explicit Core registry helpers for OpenCV DNN. / 提供 OpenCV DNN 的显式 Core 注册辅助方法。</summary>
    public static class OpenCvDnnRegistryExtensions
    {
        /// <summary>Registers a contract-bound OpenCV DNN provider owned by the Core registry. / 注册由 Core 注册中心持有且绑定合同的 OpenCV DNN Provider。</summary>
        public static BackendRegistry UseOpenCvDnn(this BackendRegistry registry, OpenCvDnnOptions options)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.Register(new OpenCvDnnBackendProvider(options));
            return registry;
        }
    }
}
