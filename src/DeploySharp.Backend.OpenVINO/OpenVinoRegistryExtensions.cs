using System;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Backends.OpenVINO
{
    /// <summary>Provides explicit OpenVINO registration helpers. / 提供显式 OpenVINO 注册帮助程序。</summary>
    public static class OpenVinoRegistryExtensions
    {
        /// <summary>Registers one OpenVINO provider in a Core backend registry. / 在 Core 后端注册表中注册一个 OpenVINO provider。</summary>
        public static BackendRegistry UseOpenVino(this BackendRegistry registry, OpenVinoOptions? options = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.Register(new OpenVinoBackendProvider(options));
            return registry;
        }
    }
}
