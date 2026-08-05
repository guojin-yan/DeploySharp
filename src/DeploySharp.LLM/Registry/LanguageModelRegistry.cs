using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.LLM.Registry
{
    /// <summary>Owns explicitly registered LLM providers and selects by Core capability descriptors. / 持有显式注册的 LLM 提供程序，并按 Core 能力描述进行选择。</summary>
    public sealed class LanguageModelRegistry : IDisposable
    {
        private readonly object _sync = new object();
        private readonly List<ILanguageModelProvider> _providers = new List<ILanguageModelProvider>();
        private bool _disposed;

        /// <summary>Registers a provider and transfers its lifetime to this registry. / 注册提供程序并将其生命周期转移给当前注册表。</summary>
        public void Register(ILanguageModelProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            lock (_sync)
            {
                ThrowIfDisposed();
                for (int index = 0; index < _providers.Count; index++)
                {
                    if (_providers[index].Descriptor.Id == provider.Descriptor.Id)
                    {
                        throw new DeploySharpException(DeploySharpErrorCodes.BackendAlreadyRegistered, $"The backend '{provider.Descriptor.Id}' is already registered.", backendId: provider.Descriptor.Id);
                    }
                }

                _providers.Add(provider);
            }
        }

        /// <summary>Gets registered Core backend descriptors. / 获取已注册的 Core 后端描述信息。</summary>
        public IReadOnlyList<BackendDescriptor> GetDescriptors()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                var values = new List<BackendDescriptor>(_providers.Count);
                for (int index = 0; index < _providers.Count; index++) values.Add(_providers[index].Descriptor);
                return values.AsReadOnly();
            }
        }

        /// <summary>Creates a session using an explicit backend or first compatible provider. / 使用明确后端或第一个兼容提供程序创建会话。</summary>
        public ILanguageModelSession CreateSession(ModelArtifact artifact, LanguageModelRequest request, LanguageModelSessionOptions? options = null)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ILanguageModelProvider[] providers;
            lock (_sync)
            {
                ThrowIfDisposed();
                providers = _providers.ToArray();
            }

            for (int index = 0; index < providers.Length; index++)
            {
                ILanguageModelProvider provider = providers[index];
                BackendId? selectedBackend = request.BackendId ?? artifact.PreferredBackend;
                if (selectedBackend.HasValue && provider.Descriptor.Id != selectedBackend.Value) continue;
                if (!provider.Descriptor.Supports(ToBackendCapabilities(request.RequiredCapabilities)) || !provider.CanCreate(artifact, request)) continue;
                return provider.CreateSession(artifact, request, options ?? LanguageModelSessionOptions.Default);
            }

            BackendId? requestedBackend = request.BackendId ?? artifact.PreferredBackend;
            if (requestedBackend.HasValue)
            {
                bool found = false;
                for (int index = 0; index < providers.Length; index++) found |= providers[index].Descriptor.Id == requestedBackend.Value;
                if (!found) throw new BackendNotFoundException(requestedBackend.Value, artifact.ModelId);
            }

            throw new BackendNotCompatibleException(artifact.ModelId, requestedBackend);
        }

        /// <inheritdoc />
        /// <remarks>Disposes providers in reverse registration order. / 按注册逆序释放提供程序。</remarks>
        public void Dispose()
        {
            ILanguageModelProvider[] providers;
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                providers = _providers.ToArray();
                _providers.Clear();
            }

            var failures = new List<Exception>();
            for (int index = providers.Length - 1; index >= 0; index--)
            {
                try { providers[index].Dispose(); }
                catch (Exception exception) { failures.Add(exception); }
            }

            if (failures.Count > 0) throw new AggregateException("One or more LLM providers failed to dispose.", failures);
        }

        private static BackendCapabilities ToBackendCapabilities(LanguageModelCapabilities capabilities)
        {
            BackendCapabilities result = BackendCapabilities.None;
            if ((capabilities & LanguageModelCapabilities.TextGeneration) != 0) result |= BackendCapabilities.TextGeneration;
            if ((capabilities & LanguageModelCapabilities.Embeddings) != 0) result |= BackendCapabilities.Embeddings;
            if ((capabilities & LanguageModelCapabilities.Streaming) != 0) result |= BackendCapabilities.AsynchronousExecution;
            return result;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LanguageModelRegistry));
        }
    }
}
