namespace JYPPX.DeploySharp.Diagnostics
{
    /// <summary>
    /// Provides a minimal logging abstraction without selecting an application logging framework. / 提供最小日志抽象，而不指定应用日志框架。
    /// </summary>
    public interface IDeploySharpLogger
    {
        /// <summary>Determines whether events at the specified level should be created. / 确定是否应创建指定级别的事件。</summary>
        public bool IsEnabled(DeploySharpLogLevel level);

        /// <summary>Writes one structured event. / 写入一个结构化事件。</summary>
        public void Log(DeploySharpLogEvent logEvent);
    }
}
