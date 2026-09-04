using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Describes one installable or user-owned backend dependency. / 描述一个可安装或由用户拥有的后端依赖。</summary>
    public sealed class BackendRuntimeDependency : IBackendRuntimeDependency
    {
        /// <summary>Initializes a dependency description. / 初始化依赖描述。</summary>
        public BackendRuntimeDependency(
            BackendRuntimeDependencyKind kind,
            string? packageId = null,
            string? packageVersion = null,
            string? runtimeIdentifier = null,
            bool downloadable = false,
            string? licenseExpression = null,
            string? condition = null,
            IEnumerable<string>? environmentVariables = null,
            bool requiresUserSelectedRoot = false)
        {
            if (!Enum.IsDefined(typeof(BackendRuntimeDependencyKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (packageId == null && packageVersion != null) throw new ArgumentException("A package version requires a package ID.", nameof(packageVersion));
            Kind = kind;
            PackageId = string.IsNullOrWhiteSpace(packageId) ? null : ExtGuard.Identifier(packageId, nameof(packageId));
            PackageVersion = string.IsNullOrWhiteSpace(packageVersion) ? null : ValidateVersion(packageVersion!, nameof(packageVersion));
            RuntimeIdentifier = string.IsNullOrWhiteSpace(runtimeIdentifier) ? null : ExtGuard.Identifier(runtimeIdentifier!.ToLowerInvariant(), nameof(runtimeIdentifier));
            Downloadable = downloadable;
            LicenseExpression = string.IsNullOrWhiteSpace(licenseExpression) ? null : ExtGuard.NotNullOrWhiteSpace(licenseExpression, nameof(licenseExpression));
            Condition = string.IsNullOrWhiteSpace(condition) ? null : ExtGuard.NotNullOrWhiteSpace(condition, nameof(condition));
            RequiresUserSelectedRoot = requiresUserSelectedRoot;

            var variables = new List<string>();
            if (environmentVariables != null)
            {
                foreach (string variable in environmentVariables)
                {
                    string normalized = ExtGuard.NotNullOrWhiteSpace(variable, nameof(environmentVariables));
                    for (int index = 0; index < variables.Count; index++) if (StringComparer.Ordinal.Equals(variables[index], normalized)) throw new ArgumentException("Environment variables must be unique.", nameof(environmentVariables));
                    variables.Add(normalized);
                }
            }
            EnvironmentVariables = new ReadOnlyCollection<string>(variables);
            Identity = BuildIdentity();
        }

        /// <summary>Gets the dependency category. / 获取依赖类别。</summary>
        public BackendRuntimeDependencyKind Kind { get; }
        /// <summary>Gets the optional package ID. / 获取可选的包 ID。</summary>
        public string? PackageId { get; }
        /// <summary>Gets the optional exact package version. / 获取可选的精确包版本。</summary>
        public string? PackageVersion { get; }
        /// <summary>Gets the optional runtime identifier. / 获取可选的运行时标识。</summary>
        public string? RuntimeIdentifier { get; }
        /// <summary>Gets whether an application may download this dependency. / 获取应用是否可以下载此依赖。</summary>
        public bool Downloadable { get; }
        /// <summary>Gets the license identifier or expression supplied by the dependency owner. / 获取依赖所有者提供的许可证标识或表达式。</summary>
        public string? LicenseExpression { get; }
        /// <summary>Gets an optional host-evaluated condition. / 获取可由宿主评估的可选条件。</summary>
        public string? Condition { get; }
        /// <summary>Gets environment variables used during resolution. / 获取解析时使用的环境变量。</summary>
        public IReadOnlyList<string> EnvironmentVariables { get; }
        /// <summary>Gets whether the user must choose a root directory. / 获取是否必须由用户选择根目录。</summary>
        public bool RequiresUserSelectedRoot { get; }
        /// <inheritdoc />
        public string Identity { get; }

        private string BuildIdentity()
        {
            return string.Join("|", new[]
            {
                Kind.ToString(), PackageId ?? string.Empty, PackageVersion ?? string.Empty,
                RuntimeIdentifier ?? string.Empty, Condition ?? string.Empty
            });
        }

        private static string ValidateVersion(string value, string parameterName)
        {
            string version = ExtGuard.NotNullOrWhiteSpace(value, parameterName).Trim();
            if (version.Length > 128 || version.IndexOfAny(new[] { '\r', '\n', '\t', ' ' }) >= 0)
                throw new ArgumentException("Versions must be compact, non-whitespace package versions.", parameterName);
            return version;
        }
    }
}
