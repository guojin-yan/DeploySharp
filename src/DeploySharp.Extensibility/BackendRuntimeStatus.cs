using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Diagnostics;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Contains the complete structured result of a backend runtime probe. / 包含后端运行时探针的完整结构化结果。</summary>
    public sealed class BackendRuntimeStatus
    {
        /// <summary>Initializes a runtime status. / 初始化运行时状态。</summary>
        public BackendRuntimeStatus(
            BackendRuntimeState state,
            string? loadedPath = null,
            string? version = null,
            string? abiApiLine = null,
            string? runtimeIdentifier = null,
            string? processArchitecture = null,
            string? device = null,
            IEnumerable<string>? missingItems = null,
            string? suggestedAction = null,
            IReadOnlyDictionary<string, string>? details = null,
            IEnumerable<RuntimeDiagnostic>? diagnostics = null)
        {
            if (!Enum.IsDefined(typeof(BackendRuntimeState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            State = state;
            LoadedPath = ContractValidation.Path(loadedPath, nameof(loadedPath));
            Version = string.IsNullOrWhiteSpace(version) ? null : ExtGuard.NotNullOrWhiteSpace(version, nameof(version));
            AbiApiLine = string.IsNullOrWhiteSpace(abiApiLine) ? null : ExtGuard.NotNullOrWhiteSpace(abiApiLine, nameof(abiApiLine));
            RuntimeIdentifier = string.IsNullOrWhiteSpace(runtimeIdentifier) ? null : ExtGuard.Identifier(runtimeIdentifier!.ToLowerInvariant(), nameof(runtimeIdentifier));
            ProcessArchitecture = string.IsNullOrWhiteSpace(processArchitecture) ? null : ExtGuard.Identifier(processArchitecture!.ToLowerInvariant(), nameof(processArchitecture));
            Device = string.IsNullOrWhiteSpace(device) ? null : ExtGuard.NotNullOrWhiteSpace(device, nameof(device));
            SuggestedAction = string.IsNullOrWhiteSpace(suggestedAction) ? null : ExtGuard.NotNullOrWhiteSpace(suggestedAction, nameof(suggestedAction));
            MissingItems = NormalizeText(missingItems, nameof(missingItems));

            var detailCopy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (details != null)
            {
                foreach (KeyValuePair<string, string> pair in details) detailCopy.Add(ExtGuard.Identifier(pair.Key, nameof(details)), ExtGuard.NotNullOrWhiteSpace(pair.Value, nameof(details)));
            }
            Details = new ReadOnlyDictionary<string, string>(detailCopy);
            Diagnostics = RuntimeDiagnostics(diagnostics);
        }

        /// <summary>Gets the probe state. / 获取探针状态。</summary>
        public BackendRuntimeState State { get; }
        /// <summary>Gets the actual loaded native or managed path. / 获取实际加载的原生或托管路径。</summary>
        public string? LoadedPath { get; }
        /// <summary>Gets the discovered runtime version. / 获取发现的运行时版本。</summary>
        public string? Version { get; }
        /// <summary>Gets the discovered ABI/API line. / 获取发现的 ABI/API 线。</summary>
        public string? AbiApiLine { get; }
        /// <summary>Gets the discovered runtime identifier. / 获取发现的运行时标识。</summary>
        public string? RuntimeIdentifier { get; }
        /// <summary>Gets the process architecture used for probing. / 获取探测进程架构。</summary>
        public string? ProcessArchitecture { get; }
        /// <summary>Gets the discovered device identity. / 获取发现的设备标识。</summary>
        public string? Device { get; }
        /// <summary>Gets missing package, library, symbol, or device requirements. / 获取缺失的包、库、符号或设备需求。</summary>
        public IReadOnlyList<string> MissingItems { get; }
        /// <summary>Gets a user-actionable remediation suggestion. / 获取可执行的修复建议。</summary>
        public string? SuggestedAction { get; }
        /// <summary>Gets immutable structured probe details. / 获取不可变结构化探针详情。</summary>
        public IReadOnlyDictionary<string, string> Details { get; }
        /// <summary>Gets immutable diagnostics emitted during probing. / 获取探测过程中产生的不可变诊断。</summary>
        public IReadOnlyList<RuntimeDiagnostic> Diagnostics { get; }
        /// <summary>Gets whether the probe found an executable runtime. / 获取探针是否找到可执行运行时。</summary>
        public bool IsAvailable => State == BackendRuntimeState.Available;

        private static IReadOnlyList<string> NormalizeText(IEnumerable<string>? values, string parameterName)
        {
            var copy = new List<string>();
            if (values != null)
            {
                foreach (string value in values)
                {
                    string normalized = ExtGuard.NotNullOrWhiteSpace(value, parameterName);
                    if (copy.Contains(normalized)) throw new ArgumentException("Values must be unique.", parameterName);
                    copy.Add(normalized);
                }
            }
            return new ReadOnlyCollection<string>(copy);
        }

        private static IReadOnlyList<RuntimeDiagnostic> RuntimeDiagnostics(IEnumerable<RuntimeDiagnostic>? values)
        {
            var copy = new List<RuntimeDiagnostic>();
            if (values != null)
            {
                foreach (RuntimeDiagnostic diagnostic in values)
                {
                    if (diagnostic == null) throw new ArgumentException("Diagnostics cannot contain null entries.", nameof(values));
                    copy.Add(diagnostic);
                }
            }
            return new ReadOnlyCollection<RuntimeDiagnostic>(copy);
        }
    }
}
