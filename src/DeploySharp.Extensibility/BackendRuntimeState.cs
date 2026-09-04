namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Describes the result of a runtime probe. / 描述运行时探测结果。</summary>
    public enum BackendRuntimeState
    {
        /// <summary>All declared requirements are available. / 所有声明的需求均可用。</summary>
        Available = 0,
        /// <summary>A managed package is missing. / 缺少托管包。</summary>
        MissingPackage = 1,
        /// <summary>A native library or runtime is missing. / 缺少原生库或运行时。</summary>
        MissingNative = 2,
        /// <summary>The installed runtime is incompatible. / 已安装运行时不兼容。</summary>
        Incompatible = 3,
        /// <summary>The runtime is present but cannot currently execute. / 运行时存在但当前不可执行。</summary>
        Unavailable = 4,
        /// <summary>The probe itself failed. / 探针自身失败。</summary>
        ProbeFailed = 5,
        /// <summary>The plugin does not support the requested host or platform. / 插件不支持请求的宿主或平台。</summary>
        Unsupported = 6
    }
}
