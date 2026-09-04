using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Builds and swaps runtime generations without unloading active native sessions. / 构建并切换运行时代，同时不卸载活动原生会话。</summary>
    public interface IDeploySharpRuntimeProvider
    {
        /// <summary>Gets the current runtime snapshot. / 获取当前运行时快照。</summary>
        public DeploySharpRuntimeSnapshot Current { get; }
        /// <summary>Builds an unregistered runtime snapshot from a composition. / 根据运行时组合构建未切换的运行时快照。</summary>
        public Task<DeploySharpRuntimeSnapshot> BuildAsync(RuntimeComposition composition, CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>Atomically swaps the current snapshot and retires the previous generation. / 原子切换当前快照并退役上一代。</summary>
        public Task SwapAsync(DeploySharpRuntimeSnapshot next, CancellationToken cancellationToken = default(CancellationToken));
    }
}
