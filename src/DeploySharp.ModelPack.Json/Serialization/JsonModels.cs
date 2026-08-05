using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JYPPX.DeploySharp.ModelPack.Json.Serialization
{
    internal sealed class ManifestDto
    {
        [JsonPropertyName("schemaVersion")] public string? SchemaVersion { get; set; }
        [JsonPropertyName("modelId")] public string? ModelId { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("family")] public string? Family { get; set; }
        [JsonPropertyName("task")] public string? Task { get; set; }
        [JsonPropertyName("modelVersion")] public string? ModelVersion { get; set; }
        [JsonPropertyName("exporter")] public ExporterDto? Exporter { get; set; }
        [JsonPropertyName("source")] public SourceDto? Source { get; set; }
        [JsonPropertyName("generatedAt")] public DateTimeOffset? GeneratedAt { get; set; }
        [JsonPropertyName("profileId")] public string? ProfileId { get; set; }
        [JsonPropertyName("inputs")] public List<TensorDto?>? Inputs { get; set; }
        [JsonPropertyName("outputs")] public List<TensorDto?>? Outputs { get; set; }
        [JsonPropertyName("artifacts")] public List<ArtifactDto?>? Artifacts { get; set; }
        [JsonPropertyName("extensions")] public SortedDictionary<string, string>? Extensions { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    internal sealed class ExporterDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("sourceRevision")] public string? SourceRevision { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    internal sealed class SourceDto
    {
        [JsonPropertyName("sourceUrl")] public string? SourceUrl { get; set; }
        [JsonPropertyName("projectUrl")] public string? ProjectUrl { get; set; }
        [JsonPropertyName("revision")] public string? Revision { get; set; }
        [JsonPropertyName("author")] public string? Author { get; set; }
        [JsonPropertyName("copyright")] public string? Copyright { get; set; }
        [JsonPropertyName("licenseExpression")] public string? LicenseExpression { get; set; }
        [JsonPropertyName("licenseFile")] public string? LicenseFile { get; set; }
        [JsonPropertyName("redistributionAllowed")] public bool? RedistributionAllowed { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    internal sealed class TensorDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("elementType")] public string? ElementType { get; set; }
        [JsonPropertyName("shape")] public List<long>? Shape { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    internal sealed class ArtifactDto
    {
        [JsonPropertyName("artifactId")] public string? ArtifactId { get; set; }
        [JsonPropertyName("format")] public string? Format { get; set; }
        [JsonPropertyName("locationKind")] public string? LocationKind { get; set; }
        [JsonPropertyName("entrypoint")] public string? Entrypoint { get; set; }
        [JsonPropertyName("compatibleBackends")] public List<string>? CompatibleBackends { get; set; }
        [JsonPropertyName("files")] public List<FileDto?>? Files { get; set; }
        [JsonPropertyName("precision")] public string? Precision { get; set; }
        [JsonPropertyName("quantization")] public string? Quantization { get; set; }
        [JsonPropertyName("opset")] public int? Opset { get; set; }
        [JsonPropertyName("portable")] public bool? Portable { get; set; }
        [JsonPropertyName("minimumBackendVersion")] public string? MinimumBackendVersion { get; set; }
        [JsonPropertyName("minimumRuntimeVersion")] public string? MinimumRuntimeVersion { get; set; }
        [JsonPropertyName("extensions")] public SortedDictionary<string, string>? Extensions { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    internal sealed class FileDto
    {
        [JsonPropertyName("relativePath")] public string? RelativePath { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; } = -1;
        [JsonPropertyName("mediaType")] public string? MediaType { get; set; }
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }
}
