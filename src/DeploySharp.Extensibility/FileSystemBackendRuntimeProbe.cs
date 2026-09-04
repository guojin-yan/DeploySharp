using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Diagnostics;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Runs a conservative filesystem/environment probe without loading native code. / 执行保守的文件系统和环境探测，不加载原生代码。</summary>
    /// <remarks>Hosts that need a true DLL ABI smoke test can run a provider-specific probe in their Worker and keep this result as the preflight stage. / 需要真实 DLL ABI 冒烟测试的宿主可在 Worker 中运行专用探针，并将本结果作为预检阶段。</remarks>
    public sealed class FileSystemBackendRuntimeProbe : IBackendRuntimeProbe
    {
        private readonly BackendPluginContext _context;
        private readonly IReadOnlyDictionary<NativeRuntimeKind, IReadOnlyList<string>> _libraryNames;

        /// <summary>Initializes a probe with application-owned paths and settings. / 使用应用拥有的路径和设置初始化探针。</summary>
        public FileSystemBackendRuntimeProbe(BackendPluginContext? context = null, IReadOnlyDictionary<NativeRuntimeKind, IReadOnlyList<string>>? libraryNames = null)
        {
            _context = context ?? BackendPluginContext.Empty;
            _libraryNames = libraryNames ?? new Dictionary<NativeRuntimeKind, IReadOnlyList<string>>();
        }

        /// <inheritdoc />
        public Task<BackendRuntimeStatus> ProbeAsync(BackendPluginDescriptor plugin, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            cancellationToken.ThrowIfCancellationRequested();

            string architecture = CurrentArchitecture();
            string rid = ResolveRid(plugin, architecture);
            var missing = new List<string>();
            var details = new Dictionary<string, string>(StringComparer.Ordinal);
            string? firstPath = null;

            if (plugin.TargetFrameworks.Count > 0 && !ContainsCurrentTarget(plugin.TargetFrameworks))
            {
                missing.Add("target-framework");
            }
            if (plugin.RuntimeIdentifiers.Count > 0 && !ContainsRuntime(plugin.RuntimeIdentifiers, rid))
            {
                missing.Add("runtime-identifier:" + rid);
            }

            for (int index = 0; index < plugin.NativeRequirements.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                NativeRuntimeRequirement requirement = plugin.NativeRequirements[index];
                string? path = ResolvePath(requirement, plugin, _context.RuntimeRoot, _libraryNames);
                string key = "native." + requirement.Kind.ToString().ToLowerInvariant();
                if (path == null)
                {
                    missing.Add(key);
                }
                else
                {
                    details[key + ".root"] = path;
                    if (firstPath == null) firstPath = path;
                }
            }

            BackendRuntimeState state = missing.Count == 0 ? BackendRuntimeState.Available : BackendRuntimeState.MissingNative;
            var diagnostics = new List<RuntimeDiagnostic>();
            if (missing.Count > 0)
            {
                diagnostics.Add(new RuntimeDiagnostic(
                    "runtime-missing",
                    DiagnosticSeverity.Warning,
                    "One or more declared backend runtime requirements are not discoverable from the supplied roots.",
                    backendId: plugin.Backend.Id,
                    details: new Dictionary<string, string> { ["missing"] = string.Join(",", missing) }));
            }

            details["pluginId"] = plugin.PluginId;
            details["processArchitecture"] = architecture;
            details["runtimeIdentifier"] = rid;
            return Task.FromResult(new BackendRuntimeStatus(
                state,
                loadedPath: firstPath,
                runtimeIdentifier: rid,
                processArchitecture: architecture,
                missingItems: missing,
                suggestedAction: missing.Count == 0 ? null : "Install the declared package or select its runtime root in the host.",
                details: details,
                diagnostics: diagnostics));
        }

        private static string ResolveRid(BackendPluginDescriptor plugin, string architecture)
        {
            string os = IsWindows() ? "win" : IsLinux() ? "linux" : "osx";
            string candidate = os + "-" + architecture;
            return candidate;
        }

        private static bool ContainsRuntime(IReadOnlyList<string> values, string rid)
        {
            for (int index = 0; index < values.Count; index++) if (string.Equals(values[index], rid, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool ContainsCurrentTarget(IReadOnlyList<string> values)
        {
            string? current = CurrentTargetFramework();
            if (string.IsNullOrWhiteSpace(current)) return true;
            for (int index = 0; index < values.Count; index++)
            {
                string value = values[index];
                string currentFramework = current!;
                if (currentFramework.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (MatchesFrameworkMoniker(currentFramework, value)) return true;
            }
            return false;
        }

        private static string CurrentArchitecture()
        {
#if DEPLOYSHARP_LEGACY_FRAMEWORK
            return Environment.Is64BitProcess ? "x64" : "x86";
#else
            return RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
#endif
        }

        private static bool IsWindows()
        {
#if DEPLOYSHARP_LEGACY_FRAMEWORK
            return true;
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        }

        private static bool IsLinux()
        {
#if DEPLOYSHARP_LEGACY_FRAMEWORK
            return false;
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#endif
        }

        private static string? CurrentTargetFramework()
        {
#if DEPLOYSHARP_LEGACY_FRAMEWORK
            // The legacy target can run only on Windows; its TFM is supplied by the host manifest.
            return null;
#else
            return GetTargetFrameworkName();
#endif
        }

        private static string? GetTargetFrameworkName()
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(FileSystemBackendRuntimeProbe).Assembly;
            object[] attributes = assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false);
            if (attributes.Length == 0) return null;
            return ((System.Runtime.Versioning.TargetFrameworkAttribute)attributes[0]).FrameworkName;
        }

        private static bool MatchesFrameworkMoniker(string current, string moniker)
        {
            if (!moniker.StartsWith("net", StringComparison.OrdinalIgnoreCase)) return false;
            string version = moniker.Substring(3);
            if (current.IndexOf(".NETCoreApp,Version=v", StringComparison.OrdinalIgnoreCase) >= 0 || current.IndexOf(".NETStandard,Version=v", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return current.EndsWith("v" + version, StringComparison.OrdinalIgnoreCase)
                    || current.EndsWith("v" + version + ".0", StringComparison.OrdinalIgnoreCase);
            }
            if (current.IndexOf(".NETFramework,Version=v", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string frameworkVersion = version.Length == 2 ? version[0] + "." + version[1] : version[0] + "." + version.Substring(1);
                return current.EndsWith("v" + frameworkVersion, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static string? ResolvePath(
            NativeRuntimeRequirement requirement,
            BackendPluginDescriptor plugin,
            string? runtimeRoot,
            IReadOnlyDictionary<NativeRuntimeKind, IReadOnlyList<string>> libraryNames)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(runtimeRoot)) candidates.Add(runtimeRoot!);
            for (int index = 0; index < requirement.EnvironmentVariables.Count; index++) AddEnvironmentCandidates(candidates, requirement.EnvironmentVariables[index]);
            for (int index = 0; index < plugin.RuntimeDependencies.Count; index++)
            {
                BackendRuntimeDependency dependency = plugin.RuntimeDependencies[index];
                for (int envIndex = 0; envIndex < dependency.EnvironmentVariables.Count; envIndex++) AddEnvironmentCandidates(candidates, dependency.EnvironmentVariables[envIndex]);
            }
            for (int index = 0; index < candidates.Count; index++)
            {
                string candidate = candidates[index];
                if (!Directory.Exists(candidate)) continue;
                if (!libraryNames.TryGetValue(requirement.Kind, out IReadOnlyList<string>? names) || names == null || names.Count == 0) return Path.GetFullPath(candidate);
                for (int nameIndex = 0; nameIndex < names.Count; nameIndex++)
                {
                    string[] locations =
                    {
                        Path.Combine(candidate, names[nameIndex]),
                        Path.Combine(candidate, "bin", names[nameIndex]),
                        Path.Combine(candidate, "lib", names[nameIndex]),
                        Path.Combine(candidate, "native", names[nameIndex]),
                        Path.Combine(candidate, "runtimes", "win-x64", "native", names[nameIndex]),
                        Path.Combine(candidate, "runtimes", "linux-x64", "native", names[nameIndex]),
                        Path.Combine(candidate, "runtimes", "linux-arm64", "native", names[nameIndex])
                    };
                    for (int locationIndex = 0; locationIndex < locations.Length; locationIndex++) if (File.Exists(locations[locationIndex])) return Path.GetFullPath(locations[locationIndex]);
                }
            }
            return null;
        }

        private static void AddEnvironmentCandidates(List<string> candidates, string variable)
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value)) return;
            string[] values = value.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < values.Length; index++) candidates.Add(values[index].Trim());
        }
    }
}
