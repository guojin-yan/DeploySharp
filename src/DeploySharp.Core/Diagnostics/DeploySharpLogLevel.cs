namespace JYPPX.DeploySharp.Diagnostics
{
    /// <summary>
    /// Defines framework-neutral log severity levels. / 定义框架无关的日志严重级别。
    /// </summary>
    public enum DeploySharpLogLevel
    {
        /// <summary>Detailed diagnostic information. / 详细诊断信息。</summary>
        Trace = 0,
        /// <summary>Developer-focused diagnostic information. / 面向开发者的诊断信息。</summary>
        Debug = 1,
        /// <summary>Normal operational information. / 正常运行信息。</summary>
        Information = 2,
        /// <summary>A recoverable or unexpected condition. / 可恢复或非预期状况。</summary>
        Warning = 3,
        /// <summary>An operation failed. / 操作失败。</summary>
        Error = 4,
        /// <summary>A process-level or unrecoverable failure. / 进程级或不可恢复故障。</summary>
        Critical = 5,
        /// <summary>Logging is disabled. / 日志已禁用。</summary>
        None = 6
    }
}
