using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JYPPX.DeploySharp.ModelFactory.Serialization
{
    internal sealed class CatalogDto
    {
        [JsonPropertyName("schemaVersion")] public string? SchemaVersion { get; set; }
        [JsonPropertyName("generatedAt")] public string? GeneratedAt { get; set; }
        [JsonPropertyName("catalogRevision")] public string? CatalogRevision { get; set; }
        [JsonPropertyName("sourceRepository")] public string? SourceRepository { get; set; }
        [JsonPropertyName("entries")] public List<EntryDto?>? Entries { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    internal sealed class EntryDto
    {
        [JsonPropertyName("modelId")] public string? ModelId { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("family")] public string? Family { get; set; }
        [JsonPropertyName("task")] public string? Task { get; set; }
        [JsonPropertyName("modelVersion")] public string? ModelVersion { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("source")] public SourceDto? Source { get; set; }
        [JsonPropertyName("release")] public ReleaseDto? Release { get; set; }
        [JsonPropertyName("artifacts")] public List<ArtifactDto?>? Artifacts { get; set; }
        [JsonPropertyName("testInputs")] public List<AssetDto?>? TestInputs { get; set; }
        [JsonPropertyName("expectedResultAssetId")] public string? ExpectedResultAssetId { get; set; }
        [JsonPropertyName("documentationPath")] public string? DocumentationPath { get; set; }
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

    internal sealed class ReleaseDto
    {
        [JsonPropertyName("owner")] public string? Owner { get; set; }
        [JsonPropertyName("repository")] public string? Repository { get; set; }
        [JsonPropertyName("tag")] public string? Tag { get; set; }
        [JsonPropertyName("commit")] public string? Commit { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    internal sealed class ArtifactDto
    {
        [JsonPropertyName("artifactId")] public string? ArtifactId { get; set; }
        [JsonPropertyName("format")] public string? Format { get; set; }
        [JsonPropertyName("compatibleBackends")] public List<string>? CompatibleBackends { get; set; }
        [JsonPropertyName("precision")] public string? Precision { get; set; }
        [JsonPropertyName("quantization")] public string? Quantization { get; set; }
        [JsonPropertyName("portable")] public bool? Portable { get; set; }
        [JsonPropertyName("manifestAssetId")] public string? ManifestAssetId { get; set; }
        [JsonPropertyName("assets")] public List<AssetDto?>? Assets { get; set; }
        [JsonPropertyName("conversion")] public ConversionDto? Conversion { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    internal sealed class AssetDto
    {
        [JsonPropertyName("assetId")] public string? AssetId { get; set; }
        [JsonPropertyName("kind")] public string? Kind { get; set; }
        [JsonPropertyName("releaseTag")] public string? ReleaseTag { get; set; }
        [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; set; }
        [JsonPropertyName("relativePath")] public string? RelativePath { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; } = -1;
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
        [JsonPropertyName("mediaType")] public string? MediaType { get; set; }
        [JsonPropertyName("licenseExpression")] public string? LicenseExpression { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    internal sealed class ConversionDto
    {
        [JsonPropertyName("exporter")] public string? Exporter { get; set; }
        [JsonPropertyName("exporterVersion")] public string? ExporterVersion { get; set; }
        [JsonPropertyName("sourceRevision")] public string? SourceRevision { get; set; }
        [JsonPropertyName("notes")] public string? Notes { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }
    }
}
