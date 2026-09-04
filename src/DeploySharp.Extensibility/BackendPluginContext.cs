using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Diagnostics;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Contains application-owned paths and settings supplied to a plugin factory. / 包含应用提供给插件工厂的路径和设置。</summary>
    public sealed class BackendPluginContext
    {
        /// <summary>Initializes a plugin context. / 初始化插件上下文。</summary>
        public BackendPluginContext(
            string? runtimeRoot = null,
            IReadOnlyDictionary<string, string>? settings = null,
            IDeploySharpDiagnosticSink? diagnosticSink = null)
        {
            RuntimeRoot = string.IsNullOrWhiteSpace(runtimeRoot) ? null : ExtGuard.NotNullOrWhiteSpace(runtimeRoot, nameof(runtimeRoot));
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (settings != null)
            {
                foreach (KeyValuePair<string, string> pair in settings)
                {
                    copy.Add(ExtGuard.Identifier(pair.Key, nameof(settings)), ExtGuard.NotNullOrWhiteSpace(pair.Value, nameof(settings)));
                }
            }
            Settings = new ReadOnlyDictionary<string, string>(copy);
            DiagnosticSink = diagnosticSink;
        }

        /// <summary>Gets the application-owned runtime root. / 获取应用拥有的运行时根目录。</summary>
        public string? RuntimeRoot { get; }
        /// <summary>Gets immutable plugin settings. / 获取不可变插件设置。</summary>
        public IReadOnlyDictionary<string, string> Settings { get; }
        /// <summary>Gets the optional structured diagnostic sink. / 获取可选的结构化诊断接收器。</summary>
        public IDeploySharpDiagnosticSink? DiagnosticSink { get; }
        /// <summary>Gets the default empty context. / 获取默认空上下文。</summary>
        public static BackendPluginContext Empty { get; } = new BackendPluginContext();
    }
}
