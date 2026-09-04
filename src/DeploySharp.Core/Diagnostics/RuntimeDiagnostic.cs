using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Internal;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Diagnostics
{
    /// <summary>Represents one immutable, structured runtime diagnostic. / 表示一个不可变的结构化运行时诊断。</summary>
    public sealed class RuntimeDiagnostic
    {
        /// <summary>Initializes a runtime diagnostic. / 初始化运行时诊断。</summary>
        public RuntimeDiagnostic(
            string code,
            DiagnosticSeverity severity,
            string message,
            BackendId? backendId = null,
            ModelId? modelId = null,
            IReadOnlyDictionary<string, string>? details = null)
        {
            Code = Guard.Identifier(code, nameof(code));
            if (!Enum.IsDefined(typeof(DiagnosticSeverity), severity)) throw new ArgumentOutOfRangeException(nameof(severity));
            Severity = severity;
            Message = Guard.NotNullOrWhiteSpace(message, nameof(message));
            BackendId = backendId;
            ModelId = modelId;

            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (details != null)
            {
                foreach (KeyValuePair<string, string> pair in details)
                {
                    string key = Guard.Identifier(pair.Key, nameof(details));
                    copy.Add(key, Guard.NotNullOrWhiteSpace(pair.Value, nameof(details)));
                }
            }

            Details = new ReadOnlyDictionary<string, string>(copy);
        }

        /// <summary>Gets the stable diagnostic code. / 获取稳定的诊断代码。</summary>
        public string Code { get; }
        /// <summary>Gets the diagnostic severity. / 获取诊断级别。</summary>
        public DiagnosticSeverity Severity { get; }
        /// <summary>Gets the human-readable message. / 获取可读消息。</summary>
        public string Message { get; }
        /// <summary>Gets the optional backend identity. / 获取可选的后端标识。</summary>
        public BackendId? BackendId { get; }
        /// <summary>Gets the optional model identity. / 获取可选的模型标识。</summary>
        public ModelId? ModelId { get; }
        /// <summary>Gets immutable diagnostic details. / 获取不可变诊断详情。</summary>
        public IReadOnlyDictionary<string, string> Details { get; }
    }
}
