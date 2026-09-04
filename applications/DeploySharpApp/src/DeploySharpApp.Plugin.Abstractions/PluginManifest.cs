using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Plugin.Abstractions
{
    public sealed class PluginManifest
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonPropertyName("pluginId")] public string? PluginId { get; set; }
        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("packageId")] public string? PackageId { get; set; }
        [JsonPropertyName("targetFrameworks")] public List<string>? TargetFrameworks { get; set; }
        [JsonPropertyName("runtimeIdentifiers")] public List<string>? RuntimeIdentifiers { get; set; }
        [JsonPropertyName("execution")] public string? Execution { get; set; }
        [JsonPropertyName("capabilities")] public List<string>? Capabilities { get; set; }
        [JsonPropertyName("formats")] public List<string>? Formats { get; set; }
        [JsonPropertyName("providerPackageId")] public string? ProviderPackageId { get; set; }
        [JsonPropertyName("providerPackageVersion")] public string? ProviderPackageVersion { get; set; }
        [JsonPropertyName("runtimeDependencies")] public List<ManifestRuntimeDependency>? RuntimeDependencies { get; set; }
        [JsonPropertyName("nativeRequirements")] public List<ManifestNativeRequirement>? NativeRequirements { get; set; }
        [JsonPropertyName("optionsSchema")] public Dictionary<string, object>? OptionsSchema { get; set; }
        [JsonPropertyName("probeId")] public string? ProbeId { get; set; }
        [JsonPropertyName("license")] public string? License { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
        [JsonPropertyName("minimumVersion")] public string? MinimumVersion { get; set; }

        public AppBackendInfo ToBackendInfo(AppRuntimeState state = AppRuntimeState.Unavailable, string? detail = null)
        {
            var capabilities = AppBackendCapability.None;
            foreach (var capability in Capabilities ?? new List<string>())
            {
                if (string.Equals(capability, "tensor-inference", StringComparison.OrdinalIgnoreCase) || string.Equals(capability, "vision", StringComparison.OrdinalIgnoreCase)) capabilities |= AppBackendCapability.Vision;
                if (string.Equals(capability, "text-generation", StringComparison.OrdinalIgnoreCase) || string.Equals(capability, "llm", StringComparison.OrdinalIgnoreCase)) capabilities |= AppBackendCapability.TextGeneration;
                if (string.Equals(capability, "embedding", StringComparison.OrdinalIgnoreCase)) capabilities |= AppBackendCapability.Embedding;
                if (string.Equals(capability, "multimodal", StringComparison.OrdinalIgnoreCase)) capabilities |= AppBackendCapability.Multimodal;
            }
            return new AppBackendInfo(PluginId!, DisplayName!, Version!, capabilities, Formats, state, ParseExecution(Execution), devices: null, detail: detail, probeId: ProbeId, providerPackageId: ProviderPackageId ?? PackageId, providerPackageVersion: ProviderPackageVersion ?? Version);
        }

        private static AppExecutionMode ParseExecution(string? value)
        {
            if (string.Equals(value, "worker", StringComparison.OrdinalIgnoreCase)) return AppExecutionMode.Worker;
            if (string.Equals(value, "inprocess", StringComparison.OrdinalIgnoreCase)) return AppExecutionMode.InProcess;
            return AppExecutionMode.InProcessOrWorker;
        }
    }

    public sealed class ManifestRuntimeDependency
    {
        [JsonPropertyName("kind")] public string? Kind { get; set; }
        [JsonPropertyName("packageId")] public string? PackageId { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("rid")] public string? Rid { get; set; }
        [JsonPropertyName("downloadable")] public bool Downloadable { get; set; }
        [JsonPropertyName("license")] public string? License { get; set; }
        [JsonPropertyName("condition")] public string? Condition { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
    }

    public sealed class ManifestNativeRequirement
    {
        [JsonPropertyName("kind")] public string? Kind { get; set; }
        [JsonPropertyName("rootSelection")] public string? RootSelection { get; set; }
        [JsonPropertyName("environmentVariables")] public List<string>? EnvironmentVariables { get; set; }
        [JsonPropertyName("minimumVersion")] public string? MinimumVersion { get; set; }
        [JsonPropertyName("apiLines")] public List<string>? ApiLines { get; set; }
    }

    public static class PluginManifestParser
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        public static PluginManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Manifest JSON is required.", nameof(json));
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, Options) ?? throw new FormatException("Manifest JSON is empty.");
            Validate(manifest);
            return manifest;
        }

        public static PluginManifest ParseFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Manifest path is required.", nameof(path));
            return Parse(File.ReadAllText(path));
        }

        public static string Serialize(PluginManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            Validate(manifest);
            return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        }

        public static void Validate(PluginManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (manifest.SchemaVersion != 1) throw new FormatException("Unsupported plugin manifest schema version.");
            RequireId(manifest.PluginId, nameof(manifest.PluginId)); RequireText(manifest.DisplayName, nameof(manifest.DisplayName)); RequireText(manifest.Version, nameof(manifest.Version)); RequireId(manifest.PackageId, nameof(manifest.PackageId));
            RequireList(manifest.TargetFrameworks, "targetFrameworks"); RequireList(manifest.RuntimeIdentifiers, "runtimeIdentifiers"); RequireList(manifest.Capabilities, "capabilities"); RequireList(manifest.Formats, "formats");
            if (manifest.RuntimeDependencies != null && manifest.RuntimeDependencies.Any(item => item == null || string.IsNullOrWhiteSpace(item.Kind))) throw new FormatException("Every runtime dependency needs a kind.");
            if (manifest.NativeRequirements != null && manifest.NativeRequirements.Any(item => item == null || string.IsNullOrWhiteSpace(item.Kind))) throw new FormatException("Every native requirement needs a kind.");
            var duplicate = (manifest.RuntimeDependencies ?? new List<ManifestRuntimeDependency>()).Where(item => item != null).GroupBy(item => (item.Kind ?? string.Empty) + "|" + (item.PackageId ?? string.Empty) + "|" + (item.Version ?? string.Empty), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null) throw new FormatException("Runtime dependencies must have unique identities.");
            if (!string.IsNullOrWhiteSpace(manifest.Sha256) && (manifest.Sha256!.Length != 64 || manifest.Sha256.Any(c => !Uri.IsHexDigit(c)))) throw new FormatException("sha256 must be a 64 character hexadecimal value.");
        }

        private static void RequireId(string? value, string name) { RequireText(value, name); if (value!.Any(c => !(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '/'))) throw new FormatException(name + " contains an invalid identifier."); }
        private static void RequireText(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new FormatException(name + " is required."); }
        private static void RequireList(List<string>? values, string name) { if (values == null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace)) throw new FormatException(name + " must contain at least one value."); }
    }

    public sealed class PluginCatalog
    {
        private readonly List<PluginManifest> _manifests = new List<PluginManifest>();
        public IReadOnlyList<PluginManifest> Manifests => _manifests.AsReadOnly();
        public void Add(PluginManifest manifest)
        {
            PluginManifestParser.Validate(manifest);
            if (_manifests.Any(item => string.Equals(item.PluginId, manifest.PluginId, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("A plugin with the same id is already in the catalog.");
            _manifests.Add(manifest);
        }
        public void AddDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)) Add(PluginManifestParser.ParseFile(path));
        }
    }
}
