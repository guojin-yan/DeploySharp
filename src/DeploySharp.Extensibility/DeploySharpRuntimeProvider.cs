using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Provides application-owned runtime generation management. / 提供由应用拥有的运行时代管理。</summary>
    public sealed class DeploySharpRuntimeProvider : IDeploySharpRuntimeProvider, IDisposable
    {
        private readonly object _sync = new object();
        private long _generation;
        private DeploySharpRuntimeSnapshot _current;
        private bool _disposed;

        /// <summary>Initializes a provider with an existing runtime snapshot. / 使用已有运行时快照初始化提供程序。</summary>
        public DeploySharpRuntimeProvider(DeploySharpRuntimeSnapshot initial)
        {
            _current = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        /// <summary>Gets the current runtime snapshot. / 获取当前运行时快照。</summary>
        public DeploySharpRuntimeSnapshot Current
        {
            get { lock (_sync) { ThrowIfDisposed(); return _current; } }
        }

        /// <summary>Builds an unregistered runtime snapshot from a composition. / 根据运行时组合构建未注册的运行时快照。</summary>
        public Task<DeploySharpRuntimeSnapshot> BuildAsync(RuntimeComposition composition, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync) { ThrowIfDisposed(); }

            var builder = DeploySharpRuntime.CreateBuilder().UseLogger(composition.Logger);
            var createdProviders = new List<IBackendProvider>();
            bool ownershipTransferred = false;
            try
            {
                for (int index = 0; index < composition.Plugins.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IDisposable created = composition.Plugins[index].Create(composition.Context);
                    if (created == null) throw new InvalidOperationException("A plugin factory returned a null provider.");
                    if (created is not IBackendProvider provider)
                    {
                        created.Dispose();
                        throw new InvalidOperationException("Runtime composition accepts Core IBackendProvider instances only; use the family-specific registry for equivalent providers.");
                    }
                    createdProviders.Add(provider);
                    builder.AddBackend(provider);
                }
                DeploySharpRuntime runtime = builder.Build();
                ownershipTransferred = true;
                return Task.FromResult(new DeploySharpRuntimeSnapshot(runtime, Interlocked.Increment(ref _generation)));
            }
            catch
            {
                if (!ownershipTransferred)
                {
                    for (int index = createdProviders.Count - 1; index >= 0; index--)
                    {
                        try { createdProviders[index].Dispose(); }
                        catch { /* Preserve the original build failure. */ }
                    }
                }
                throw;
            }
        }

        /// <summary>Atomically swaps the current snapshot and retires the previous generation. / 原子切换当前快照并退役上一代。</summary>
        public Task SwapAsync(DeploySharpRuntimeSnapshot next, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                ThrowIfDisposed();
                DeploySharpRuntimeSnapshot previous = _current;
                _current = next;
                previous.Retire();
            }
            return Task.CompletedTask;
        }

        /// <summary>Retires the current runtime snapshot and releases this provider. / 退役当前运行时快照并释放此提供程序。</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _current.Retire();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DeploySharpRuntimeProvider));
        }
    }
}
