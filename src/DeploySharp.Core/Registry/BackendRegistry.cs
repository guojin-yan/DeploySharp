using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Internal;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Registry
{
    /// <summary>
    /// Owns explicitly registered backend providers and selects sessions by capability. / 拥有显式注册的后端提供程序，并按能力选择会话。
    /// </summary>
    public sealed class BackendRegistry : IDisposable
    {
        private readonly object _sync = new object();
        private readonly Dictionary<BackendId, IBackendProvider> _providers =
            new Dictionary<BackendId, IBackendProvider>();
        private readonly List<BackendId> _registrationOrder = new List<BackendId>();
        private bool _disposed;

        /// <summary>
        /// Registers a backend provider and transfers its lifetime to this registry. / 注册后端提供程序，并将其生命周期移交给此注册中心。
        /// </summary>
        public void Register(IBackendProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            BackendId id = provider.Descriptor.Id;
            lock (_sync)
            {
                ThrowIfDisposed();
                if (_providers.ContainsKey(id))
                {
                    throw new DeploySharpException(
                        DeploySharpErrorCodes.BackendAlreadyRegistered,
                        $"The backend '{id}' is already registered.",
                        backendId: id);
                }

                _providers.Add(id, provider);
                _registrationOrder.Add(id);
            }
        }

        /// <summary>
        /// Gets a snapshot of registered backend descriptors in registration order. / 按注册顺序获取后端描述信息快照。
        /// </summary>
        public IReadOnlyList<BackendDescriptor> GetDescriptors()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                var descriptors = new List<BackendDescriptor>(_registrationOrder.Count);
                for (int index = 0; index < _registrationOrder.Count; index++)
                {
                    descriptors.Add(_providers[_registrationOrder[index]].Descriptor);
                }

                return descriptors.AsReadOnly();
            }
        }

        /// <summary>
        /// Creates a session using an explicitly selected or first compatible registered backend. / 使用显式选择的后端或首个兼容的已注册后端创建会话。
        /// </summary>
        public IInferenceSession CreateSession(
            ModelArtifact artifact,
            BackendRequest request,
            SessionOptions? options = null)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));

            IBackendProvider[] providers = GetProviderCandidates(request.BackendId, artifact.ModelId);
            for (int index = 0; index < providers.Length; index++)
            {
                IBackendProvider provider = providers[index];
                if (!provider.Descriptor.Supports(request.RequiredCapabilities))
                {
                    continue;
                }

                if (provider.CanCreate(artifact, request))
                {
                    SessionOptions requested = options ?? SessionOptions.Default;
                    var sessions = new List<IInferenceSession>(requested.MaxConcurrency);
                    try
                    {
                        var singleChannel = new SessionOptions(1, requested.EnableProfiling);
                        for (int sessionIndex = 0; sessionIndex < requested.MaxConcurrency; sessionIndex++) sessions.Add(provider.CreateSession(artifact, request, singleChannel));
                        return new PooledInferenceSession(sessions.AsReadOnly());
                    }
                    catch
                    {
                        for (int sessionIndex = sessions.Count - 1; sessionIndex >= 0; sessionIndex--) sessions[sessionIndex].Dispose();
                        throw;
                    }
                }
            }

            throw new BackendNotCompatibleException(artifact.ModelId, request.BackendId);
        }

        /// <inheritdoc />
        /// <remarks>Disposes owned providers in reverse registration order. / 按注册顺序的逆序释放所拥有的提供程序。</remarks>
        public void Dispose()
        {
            IBackendProvider[] providers;
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                providers = new IBackendProvider[_registrationOrder.Count];
                for (int index = 0; index < _registrationOrder.Count; index++)
                {
                    providers[index] = _providers[_registrationOrder[index]];
                }

                _providers.Clear();
                _registrationOrder.Clear();
            }

            var failures = new List<Exception>();
            for (int index = providers.Length - 1; index >= 0; index--)
            {
                try
                {
                    providers[index].Dispose();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("One or more backend providers failed to dispose.", failures);
            }
        }

        private IBackendProvider[] GetProviderCandidates(BackendId? selectedBackend, ModelId modelId)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                if (selectedBackend.HasValue)
                {
                    if (!_providers.TryGetValue(selectedBackend.Value, out IBackendProvider? selected))
                    {
                        throw new BackendNotFoundException(selectedBackend.Value, modelId);
                    }

                    return new[] { selected };
                }

                var providers = new IBackendProvider[_registrationOrder.Count];
                for (int index = 0; index < _registrationOrder.Count; index++)
                {
                    providers[index] = _providers[_registrationOrder[index]];
                }

                return providers;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BackendRegistry));
            }
        }
    }
}
