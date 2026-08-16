using System;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Provides explicit Core registry helpers for the TensorRT adapter.</summary>
    public static class TensorRtRegistryExtensions
    {
        /// <summary>Registers a TensorRT provider owned by the Core registry.</summary>
        public static BackendRegistry UseTensorRT(this BackendRegistry registry, TensorRtBackendOptions? options = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.Register(new TensorRtBackendProvider(options));
            return registry;
        }
    }
}
