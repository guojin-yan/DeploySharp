namespace JYPPX.DeploySharp.Diagnostics
{
    /// <summary>Classifies a runtime diagnostic. / 对运行时诊断进行分级。</summary>
    public enum DiagnosticSeverity
    {
        /// <summary>Informational diagnostic. / 信息诊断。</summary>
        Information = 0,
        /// <summary>Warning diagnostic. / 警告诊断。</summary>
        Warning = 1,
        /// <summary>Error diagnostic. / 错误诊断。</summary>
        Error = 2,
        /// <summary>Fatal diagnostic. / 致命诊断。</summary>
        Critical = 3
    }
}
