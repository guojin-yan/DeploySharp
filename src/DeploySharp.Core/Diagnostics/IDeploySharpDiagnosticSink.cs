namespace JYPPX.DeploySharp.Diagnostics
{
    /// <summary>Receives structured diagnostics without selecting a logging framework. / 接收结构化诊断而不绑定日志框架。</summary>
    public interface IDeploySharpDiagnosticSink
    {
        /// <summary>Publishes one diagnostic. / 发布一条诊断。</summary>
        public void Publish(RuntimeDiagnostic diagnostic);
    }
}
