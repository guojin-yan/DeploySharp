using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Provides a small host-neutral catalog for installed descriptors; transport remains application-owned. / 提供小型宿主无关已安装描述目录，传输仍由应用拥有。</summary>
    public sealed class InMemoryBackendPluginCatalog : IBackendPluginCatalog
    {
        private readonly object _sync = new object();
        private readonly List<BackendPluginDescriptor> _plugins = new List<BackendPluginDescriptor>();

        /// <summary>Adds one installed descriptor. / 添加一个已安装描述。</summary>
        public void Add(BackendPluginDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            lock (_sync)
            {
                for (int index = 0; index < _plugins.Count; index++) if (string.Equals(_plugins[index].PluginId, descriptor.PluginId, StringComparison.Ordinal)) throw new ArgumentException("A plugin with the same ID is already present.", nameof(descriptor));
                _plugins.Add(descriptor);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<BackendPluginDescriptor> GetInstalled()
        {
            lock (_sync) return new ReadOnlyCollection<BackendPluginDescriptor>(new List<BackendPluginDescriptor>(_plugins));
        }

        /// <inheritdoc />
        /// <remarks>This in-memory implementation has no transport to refresh. / 此内存实现没有可刷新的传输层。</remarks>
        public Task RefreshAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
