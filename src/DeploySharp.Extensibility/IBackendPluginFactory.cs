namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Creates a Core provider or an equivalent typed provider from a manifest descriptor. / 根据清单描述创建 Core Provider 或等价的强类型 Provider。</summary>
    public interface IBackendPluginFactory
    {
        /// <summary>Gets the plugin descriptor. / 获取插件描述。</summary>
        public BackendPluginDescriptor Descriptor { get; }
        /// <summary>Creates a disposable provider owned by the caller or host. Core backends return <see cref="IBackendProvider"/>; family-specific backends may return an equivalent provider contract. / 创建由调用方或宿主持有的可释放 Provider。Core 后端返回 <see cref="IBackendProvider"/>，特定模型族可返回等价 Provider 合同。</summary>
        public System.IDisposable Create(BackendPluginContext context);
    }
}
