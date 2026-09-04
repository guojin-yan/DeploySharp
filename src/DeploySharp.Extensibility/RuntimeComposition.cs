using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Diagnostics;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Describes application-selected plugin factories used to build a runtime snapshot. / 描述应用选择的插件工厂集合，用于构建运行时快照。</summary>
    public sealed class RuntimeComposition
    {
        /// <summary>Initializes a runtime composition. / 初始化运行时组合。</summary>
        public RuntimeComposition(IEnumerable<IBackendPluginFactory>? plugins = null, BackendPluginContext? context = null, IDeploySharpLogger? logger = null)
        {
            var copy = new List<IBackendPluginFactory>();
            if (plugins != null)
            {
                foreach (IBackendPluginFactory plugin in plugins)
                {
                    if (plugin == null) throw new ArgumentException("Plugins cannot contain null entries.", nameof(plugins));
                    copy.Add(plugin);
                }
            }
            Plugins = copy.AsReadOnly();
            Context = context ?? BackendPluginContext.Empty;
            Logger = logger ?? NullDeploySharpLogger.Instance;
        }

        /// <summary>Gets immutable plugin factories in registration order. / 获取按注册顺序排列的不可变插件工厂。</summary>
        public IReadOnlyList<IBackendPluginFactory> Plugins { get; }
        /// <summary>Gets the application-owned plugin context. / 获取应用拥有的插件上下文。</summary>
        public BackendPluginContext Context { get; }
        /// <summary>Gets the runtime logger. / 获取运行时日志记录器。</summary>
        public IDeploySharpLogger Logger { get; }
    }
}
