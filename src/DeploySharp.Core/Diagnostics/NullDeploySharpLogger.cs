namespace JYPPX.DeploySharp.Diagnostics
{
    /// <summary>
    /// Discards all events and is used when an application does not configure logging. / 丢弃所有事件，供未配置日志的应用使用。
    /// </summary>
    public sealed class NullDeploySharpLogger : IDeploySharpLogger
    {
        private NullDeploySharpLogger()
        {
        }

        /// <summary>Gets the shared no-op logger. / 获取共享的空操作日志记录器。</summary>
        public static NullDeploySharpLogger Instance { get; } = new NullDeploySharpLogger();

        /// <inheritdoc />
        /// <remarks>Always returns <see langword="false" />. / 始终返回 <see langword="false" />。</remarks>
        public bool IsEnabled(DeploySharpLogLevel level)
        {
            return false;
        }

        /// <inheritdoc />
        /// <remarks>Intentionally discards the event. / 有意丢弃该事件。</remarks>
        public void Log(DeploySharpLogEvent logEvent)
        {
        }
    }
}
