using System;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Adapts manifest plugin factories to the existing explicit Core registry. / 将清单插件工厂适配到现有显式 Core 注册表。</summary>
    public static class BackendPluginRegistryAdapter
    {
        /// <summary>Creates and registers a provider without changing legacy registration semantics. / 创建并注册 Provider，且不改变旧注册语义。</summary>
        public static BackendRegistry RegisterPlugin(this BackendRegistry registry, IBackendPluginFactory factory, BackendPluginContext? context = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            IDisposable created = factory.Create(context ?? BackendPluginContext.Empty);
            if (created is not IBackendProvider provider)
            {
                created.Dispose();
                throw new InvalidOperationException("The plugin does not expose a Core IBackendProvider and must be registered with its family-specific registry.");
            }
            registry.Register(provider);
            return registry;
        }
    }
}
