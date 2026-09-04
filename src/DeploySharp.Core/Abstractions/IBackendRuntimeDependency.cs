namespace JYPPX.DeploySharp
{
    /// <summary>Provides the minimal identity needed by a backend runtime dependency. / 提供后端运行时依赖所需的最小标识。</summary>
    public interface IBackendRuntimeDependency
    {
        /// <summary>Gets a stable dependency identity. / 获取稳定的依赖标识。</summary>
        public string Identity { get; }
    }
}
