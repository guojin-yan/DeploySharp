using System;
using System.Threading;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Owns one runtime generation and delays disposal until all leases finish. / 拥有一个运行时代，并在所有租约结束后延迟释放。</summary>
    public sealed class DeploySharpRuntimeSnapshot : IDisposable
    {
        private readonly object _sync = new object();
        private int _leases;
        private bool _retired;
        private bool _disposed;

        internal DeploySharpRuntimeSnapshot(DeploySharpRuntime runtime, long generation)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Generation = generation;
            CreatedUtc = DateTimeOffset.UtcNow;
        }

        /// <summary>Gets the runtime generation number. / 获取运行时代编号。</summary>
        public long Generation { get; }
        /// <summary>Gets the UTC creation time. / 获取 UTC 创建时间。</summary>
        public DateTimeOffset CreatedUtc { get; }
        /// <summary>Gets the immutable runtime owned by this snapshot. / 获取此快照拥有的不可变运行时。</summary>
        public DeploySharpRuntime Runtime { get; }

        /// <summary>Acquires a lease that keeps this runtime generation alive. / 获取保持此运行时代存活的租约。</summary>
        public DeploySharpRuntimeLease AcquireLease()
        {
            lock (_sync)
            {
                if (_disposed || _retired) throw new ObjectDisposedException(nameof(DeploySharpRuntimeSnapshot));
                _leases++;
                return new DeploySharpRuntimeLease(this);
            }
        }

        internal void Retire()
        {
            lock (_sync)
            {
                if (_retired) return;
                _retired = true;
                DisposeIfUnused();
            }
        }

        /// <summary>Retires this runtime generation and releases it after active leases finish. / 退役此运行时代，并在活动租约结束后释放。</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                _retired = true;
                DisposeIfUnused();
            }
        }

        internal void ReleaseLease()
        {
            lock (_sync)
            {
                if (_leases <= 0) return;
                _leases--;
                DisposeIfUnused();
            }
        }

        private void DisposeIfUnused()
        {
            if (_retired && _leases == 0 && !_disposed)
            {
                _disposed = true;
                Runtime.Dispose();
            }
        }
    }

    /// <summary>Represents one disposable lease over a runtime snapshot. / 表示运行时快照上的一个可释放租约。</summary>
    public sealed class DeploySharpRuntimeLease : IDisposable
    {
        private DeploySharpRuntimeSnapshot? _snapshot;

        internal DeploySharpRuntimeLease(DeploySharpRuntimeSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        /// <summary>Gets the leased runtime. / 获取租约中的运行时。</summary>
        public DeploySharpRuntime Runtime => (_snapshot ?? throw new ObjectDisposedException(nameof(DeploySharpRuntimeLease))).Runtime;

        /// <summary>Releases this runtime lease. / 释放此运行时租约。</summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref _snapshot, null)?.ReleaseLease();
        }
    }
}
