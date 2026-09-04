using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Abstracts installed-plugin enumeration and remote manifest refresh. / 抽象已安装插件枚举和远程清单刷新。</summary>
    public interface IBackendPluginCatalog
    {
        /// <summary>Gets a defensive snapshot of installed plugin descriptors. / 获取已安装插件描述的防御性快照。</summary>
        public IReadOnlyList<BackendPluginDescriptor> GetInstalled();
        /// <summary>Refreshes metadata through an application-owned transport. / 通过应用拥有的传输层刷新元数据。</summary>
        public Task RefreshAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
