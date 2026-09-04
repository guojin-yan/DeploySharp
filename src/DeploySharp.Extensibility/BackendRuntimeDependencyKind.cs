namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Classifies a backend dependency without selecting an installer. / 对后端依赖分类，但不指定安装器。</summary>
    public enum BackendRuntimeDependencyKind
    {
        /// <summary>A managed package dependency. / 托管包依赖。</summary>
        ManagedPackage = 0,
        /// <summary>A native runtime directory or library. / 原生运行时目录或库。</summary>
        NativeRuntime = 1,
        /// <summary>A device driver dependency. / 设备驱动依赖。</summary>
        Driver = 2,
        /// <summary>An environment variable used to locate a dependency. / 用于定位依赖的环境变量。</summary>
        Environment = 3,
        /// <summary>A model or external artifact supplied by the user. / 由用户提供的模型或外部工件。</summary>
        ExternalArtifact = 4,
        /// <summary>An application-owned executable or tool. / 由应用拥有的可执行工具。</summary>
        Tool = 5
    }
}
