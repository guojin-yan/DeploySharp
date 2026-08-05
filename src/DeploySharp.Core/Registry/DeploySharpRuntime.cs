using System;
using JYPPX.DeploySharp.Diagnostics;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Registry
{
    /// <summary>
    /// Owns backend registrations and creates isolated inference sessions. / 拥有后端注册项并创建相互隔离的推理会话。
    /// </summary>
    public sealed class DeploySharpRuntime : IDisposable
    {
        private readonly BackendRegistry _registry;

        internal DeploySharpRuntime(BackendRegistry registry, IDeploySharpLogger logger)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Gets the configured framework-neutral logger. / 获取已配置的框架无关日志记录器。</summary>
        public IDeploySharpLogger Logger { get; }

        /// <summary>Creates a new empty runtime builder. / 创建新的空运行时生成器。</summary>
        public static DeploySharpRuntimeBuilder CreateBuilder()
        {
            return new DeploySharpRuntimeBuilder();
        }

        /// <summary>Gets a snapshot of registered backend descriptors. / 获取已注册后端描述信息的快照。</summary>
        public System.Collections.Generic.IReadOnlyList<BackendDescriptor> GetBackends()
        {
            return _registry.GetDescriptors();
        }

        /// <summary>Creates a model session using registered backend capabilities. / 使用已注册后端能力创建模型会话。</summary>
        public IInferenceSession CreateSession(
            ModelArtifact artifact,
            BackendRequest request,
            SessionOptions? options = null)
        {
            return _registry.CreateSession(artifact, request, options);
        }

        /// <inheritdoc />
        /// <remarks>Disposes all backend providers owned by this runtime. / 释放此运行时拥有的全部后端提供程序。</remarks>
        public void Dispose()
        {
            _registry.Dispose();
        }
    }
}
