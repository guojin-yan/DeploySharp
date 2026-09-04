using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Describes one native runtime family required by a backend. / 描述后端所需的一个原生运行时族。</summary>
    public sealed class NativeRuntimeRequirement : IBackendRuntimeDependency
    {
        /// <summary>Initializes a native runtime requirement. / 初始化原生运行时需求。</summary>
        public NativeRuntimeRequirement(
            NativeRuntimeKind kind,
            string? minimumVersion = null,
            string? maximumVersion = null,
            string? apiLine = null,
            IEnumerable<string>? runtimeIdentifiers = null,
            bool requiresUserSelectedRoot = false,
            IEnumerable<string>? environmentVariables = null)
        {
            if (!Enum.IsDefined(typeof(NativeRuntimeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            MinimumVersion = string.IsNullOrWhiteSpace(minimumVersion) ? null : ValidateText(minimumVersion!, nameof(minimumVersion));
            MaximumVersion = string.IsNullOrWhiteSpace(maximumVersion) ? null : ValidateText(maximumVersion!, nameof(maximumVersion));
            ApiLine = string.IsNullOrWhiteSpace(apiLine) ? null : ExtGuard.Identifier(apiLine!.ToLowerInvariant(), nameof(apiLine));
            RuntimeIdentifiers = Normalize(runtimeIdentifiers, nameof(runtimeIdentifiers));
            EnvironmentVariables = Normalize(environmentVariables, nameof(environmentVariables), false);
            RequiresUserSelectedRoot = requiresUserSelectedRoot;
            Identity = string.Join("|", new[] { Kind.ToString(), MinimumVersion ?? string.Empty, MaximumVersion ?? string.Empty, ApiLine ?? string.Empty });
        }

        /// <summary>Gets the runtime family. / 获取运行时族。</summary>
        public NativeRuntimeKind Kind { get; }
        /// <summary>Gets an optional minimum runtime version. / 获取可选的最低运行时版本。</summary>
        public string? MinimumVersion { get; }
        /// <summary>Gets an optional maximum runtime version. / 获取可选的最高运行时版本。</summary>
        public string? MaximumVersion { get; }
        /// <summary>Gets an optional ABI/API line such as <c>10</c>. / 获取可选的 ABI/API 线，例如 <c>10</c>。</summary>
        public string? ApiLine { get; }
        /// <summary>Gets supported runtime identifiers. / 获取支持的运行时标识。</summary>
        public IReadOnlyList<string> RuntimeIdentifiers { get; }
        /// <summary>Gets whether the user must choose a root directory. / 获取是否必须由用户选择根目录。</summary>
        public bool RequiresUserSelectedRoot { get; }
        /// <summary>Gets environment variables used to locate the runtime. / 获取用于定位运行时的环境变量。</summary>
        public IReadOnlyList<string> EnvironmentVariables { get; }
        /// <inheritdoc />
        public string Identity { get; }

        private static string ValidateText(string value, string parameterName)
        {
            string normalized = ExtGuard.NotNullOrWhiteSpace(value, parameterName).Trim();
            if (normalized.Length > 128 || normalized.IndexOfAny(new[] { '\r', '\n', '\t', ' ' }) >= 0) throw new ArgumentException("Version values cannot contain whitespace.", parameterName);
            return normalized;
        }

        private static IReadOnlyList<string> Normalize(IEnumerable<string>? values, string parameterName, bool identifier = true)
        {
            var result = new List<string>();
            if (values != null)
            {
                foreach (string value in values)
                {
                    string normalized = ExtGuard.NotNullOrWhiteSpace(value, parameterName);
                    if (identifier) normalized = ExtGuard.Identifier(normalized.ToLowerInvariant(), parameterName);
                    for (int index = 0; index < result.Count; index++) if (StringComparer.Ordinal.Equals(result[index], normalized)) throw new ArgumentException("Values must be unique.", parameterName);
                    result.Add(normalized);
                }
            }
            return new ReadOnlyCollection<string>(result);
        }
    }
}
