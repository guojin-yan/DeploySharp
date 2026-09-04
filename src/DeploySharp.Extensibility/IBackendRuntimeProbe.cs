using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Probes one plugin's managed and native runtime without installing anything. / 探测插件的托管和原生运行时，但不执行安装。</summary>
    public interface IBackendRuntimeProbe
    {
        /// <summary>Runs an asynchronous structured probe. / 异步执行结构化探测。</summary>
        public Task<BackendRuntimeStatus> ProbeAsync(BackendPluginDescriptor plugin, CancellationToken cancellationToken = default(CancellationToken));
    }
}
