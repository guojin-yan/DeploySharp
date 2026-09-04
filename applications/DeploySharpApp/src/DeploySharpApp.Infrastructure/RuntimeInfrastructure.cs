using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeploySharpApp.Application;
using DeploySharpApp.Contracts;
using DeploySharpApp.Plugin.Abstractions;

namespace DeploySharpApp.Infrastructure
{
    public sealed class LocalRuntimeProbe : IAppRuntimeProbe
    {
        private readonly string _rid;
        public LocalRuntimeProbe(string? rid = null) => _rid = string.IsNullOrWhiteSpace(rid) ? (Environment.Is64BitProcess ? "win-x64" : "win-x86") : rid!.Trim();

        public BackendRuntimeStatus Probe(PluginManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var missing = new List<string>();
            var details = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["pluginId"] = manifest.PluginId ?? string.Empty, ["rid"] = _rid, ["processArchitecture"] = Environment.Is64BitProcess ? "x64" : "x86" };
            if (manifest.RuntimeIdentifiers == null || !manifest.RuntimeIdentifiers.Contains(_rid, StringComparer.OrdinalIgnoreCase))
            {
                return new BackendRuntimeStatus(manifest.PluginId!, AppRuntimeState.Unsupported, "当前进程架构/RID 不在插件支持范围内。", rid: _rid, processArchitecture: details["processArchitecture"], suggestedAction: "选择兼容的应用发布包。", details: details);
            }

            foreach (var dependency in manifest.NativeRequirements ?? new List<ManifestNativeRequirement>())
            {
                if (string.Equals(dependency.Kind, "driver", StringComparison.OrdinalIgnoreCase)) continue;
                IReadOnlyList<string> probedPaths;
                string? loadedPath = ResolveNativePath(dependency, out probedPaths);
                string detailKey = "native." + (dependency.Kind ?? "unknown");
                details[detailKey + ".probedPaths"] = string.Join(Path.PathSeparator.ToString(), probedPaths);
                if (loadedPath == null) missing.Add(dependency.Kind ?? "native-runtime");
                else details[detailKey + ".loadedPath"] = loadedPath;
            }
            if (missing.Count > 0)
            {
                var diagnostic = new RuntimeDiagnostic("DSAPP-NATIVE-MISSING", DiagnosticSeverity.Warning, "未找到后端所需的本机原生运行时。", manifest.PluginId, details: new Dictionary<string, string> { ["missing"] = string.Join(",", missing) });
                return new BackendRuntimeStatus(manifest.PluginId!, AppRuntimeState.MissingNative, "缺少原生运行时：" + string.Join(", ", missing), rid: _rid, processArchitecture: details["processArchitecture"], missingItems: missing, suggestedAction: "打开后端安装/探测向导并选择本机 runtime 根目录。", details: details, diagnostics: new[] { diagnostic });
            }

            if (string.Equals(manifest.PluginId, "deploysharp.backend.tensorrt", StringComparison.OrdinalIgnoreCase))
            {
                return new BackendRuntimeStatus(manifest.PluginId!, AppRuntimeState.Unavailable, "TensorRT 已识别为 Worker 后端；完成 CUDA/cuDNN/TensorRT/bridge 探测后才可用。", rid: _rid, processArchitecture: details["processArchitecture"], suggestedAction: "配置本机 NVIDIA runtime，随后启动 probe Worker。", details: details);
            }

            if ((manifest.NativeRequirements?.Count ?? 0) > 0)
            {
                var diagnostic = new RuntimeDiagnostic("DSAPP-NATIVE-SMOKE-PENDING", DiagnosticSeverity.Information, "已发现 native 候选，但尚无本次进程的 ABI/版本 smoke test 证据。", manifest.PluginId);
                return new BackendRuntimeStatus(manifest.PluginId!, AppRuntimeState.Unavailable, "已发现应用本地 native 候选；完成真实 Session smoke test 后才标记可用。", loadedPath: FirstLoadedPath(details), rid: _rid, processArchitecture: details["processArchitecture"], devices: new[] { "cpu" }, suggestedAction: "运行真实模型或后端 Doctor 以完成 ABI/版本校验。", details: details, diagnostics: new[] { diagnostic });
            }

            return new BackendRuntimeStatus(manifest.PluginId!, AppRuntimeState.Available, "托管后端合同可用。", rid: _rid, processArchitecture: details["processArchitecture"], devices: new[] { "cpu" }, details: details);
        }

        private static string? ResolveNativePath(ManifestNativeRequirement requirement, out IReadOnlyList<string> probedPaths)
        {
            var candidates = new List<string>();
            foreach (var variable in requirement.EnvironmentVariables ?? new List<string>())
            {
                var value = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrWhiteSpace(value)) continue;
                foreach (string item in value.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)) candidates.Add(item.Trim());
            }

            bool onnxRuntime = string.Equals(requirement.Kind, "onnxruntime-native", StringComparison.OrdinalIgnoreCase);
            if (onnxRuntime) candidates.Add(AppContext.BaseDirectory);

            string[] libraryNames = onnxRuntime
                ? new[] { "onnxruntime.dll", "libonnxruntime.so" }
                : Array.Empty<string>();
            var probes = new List<string>();
            foreach (string candidate in candidates)
            {
                string fullPath;
                try { fullPath = Path.GetFullPath(candidate); }
                catch (Exception) { continue; }
                if (File.Exists(fullPath))
                {
                    probes.Add(fullPath);
                    if (libraryNames.Length == 0 || libraryNames.Contains(Path.GetFileName(fullPath), StringComparer.OrdinalIgnoreCase))
                    {
                        probedPaths = probes.AsReadOnly();
                        return fullPath;
                    }
                    continue;
                }

                if (libraryNames.Length == 0)
                {
                    probes.Add(fullPath);
                    if (Directory.Exists(fullPath))
                    {
                        probedPaths = probes.AsReadOnly();
                        return fullPath;
                    }
                    continue;
                }

                foreach (string library in libraryNames)
                {
                    string[] locations =
                    {
                        Path.Combine(fullPath, library),
                        Path.Combine(fullPath, "runtimes", "win-x64", "native", library),
                        Path.Combine(fullPath, "runtimes", "linux-x64", "native", library),
                        Path.Combine(fullPath, "runtimes", "linux-arm64", "native", library)
                    };
                    foreach (string location in locations)
                    {
                        string probe = Path.GetFullPath(location);
                        probes.Add(probe);
                        if (File.Exists(probe))
                        {
                            probedPaths = probes.AsReadOnly();
                            return probe;
                        }
                    }
                }
            }
            probedPaths = probes.AsReadOnly();
            return null;
        }

        private static string? FirstLoadedPath(IReadOnlyDictionary<string, string> details)
        {
            foreach (var pair in details) if (pair.Key.EndsWith(".loadedPath", StringComparison.OrdinalIgnoreCase)) return pair.Value;
            return null;
        }
    }

    public interface IContentAddressedCache
    {
        string RootPath { get; }
        string GetPath(string name, string sha256);
        bool Contains(string name, string sha256);
        Stream OpenRead(string name, string sha256);
        Stream OpenWrite(string name, string sha256);
    }

    public sealed class FileContentAddressedCache : IContentAddressedCache
    {
        public FileContentAddressedCache(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("A cache root is required.", nameof(rootPath));
            RootPath = Path.GetFullPath(rootPath); Directory.CreateDirectory(RootPath);
        }
        public string RootPath { get; }
        public string GetPath(string name, string sha256) { if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A cache name is required.", nameof(name)); if (string.IsNullOrWhiteSpace(sha256)) throw new ArgumentException("A SHA256 is required.", nameof(sha256)); return Path.Combine(RootPath, sha256.ToLowerInvariant(), name); }
        public bool Contains(string name, string sha256) => File.Exists(GetPath(name, sha256));
        public Stream OpenRead(string name, string sha256) => File.OpenRead(GetPath(name, sha256));
        public Stream OpenWrite(string name, string sha256) { var path = GetPath(name, sha256); Directory.CreateDirectory(Path.GetDirectoryName(path)!); return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None); }
    }
}
