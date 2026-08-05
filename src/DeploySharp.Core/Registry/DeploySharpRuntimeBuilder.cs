using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Diagnostics;

namespace JYPPX.DeploySharp.Registry
{
    /// <summary>
    /// Builds an isolated DeploySharp runtime using explicitly supplied backend providers. / 使用显式提供的后端提供程序构建隔离的 DeploySharp 运行时。
    /// </summary>
    public sealed class DeploySharpRuntimeBuilder
    {
        private readonly List<IBackendProvider> _providers = new List<IBackendProvider>();
        private IDeploySharpLogger _logger = NullDeploySharpLogger.Instance;
        private bool _built;

        /// <summary>Adds a backend provider to the runtime under construction. / 向正在构建的运行时添加后端提供程序。</summary>
        public DeploySharpRuntimeBuilder AddBackend(IBackendProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            ThrowIfBuilt();
            _providers.Add(provider);
            return this;
        }

        /// <summary>Configures the application-owned logging adapter. / 配置由应用拥有的日志适配器。</summary>
        public DeploySharpRuntimeBuilder UseLogger(IDeploySharpLogger logger)
        {
            ThrowIfBuilt();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        /// <summary>Builds the runtime and transfers provider lifetimes to it. / 构建运行时，并将提供程序生命周期移交给该运行时。</summary>
        public DeploySharpRuntime Build()
        {
            ThrowIfBuilt();
            _built = true;

            var registry = new BackendRegistry();
            try
            {
                for (int index = 0; index < _providers.Count; index++)
                {
                    registry.Register(_providers[index]);
                }

                return new DeploySharpRuntime(registry, _logger);
            }
            catch
            {
                registry.Dispose();
                throw;
            }
        }

        private void ThrowIfBuilt()
        {
            if (_built)
            {
                throw new InvalidOperationException("A DeploySharp runtime builder can only build one runtime.");
            }
        }
    }
}
