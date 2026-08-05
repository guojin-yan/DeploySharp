using System.Collections.Generic;

namespace JYPPX.DeploySharp.ModelFactory.Runtime
{
    internal sealed class CacheMetadata
    {
        public string? CacheKey { get; set; }
        public string? CatalogRevision { get; set; }
        public string? ReleaseTag { get; set; }
        public string? ModelId { get; set; }
        public string? ArtifactId { get; set; }
        public string? DownloadedAt { get; set; }
        public string? VerifiedAt { get; set; }
        public string? LastAccessAt { get; set; }
        public string? VerificationStatus { get; set; }
        public List<CacheAssetMetadata> Assets { get; set; } = new List<CacheAssetMetadata>();
    }

    internal sealed class CacheAssetMetadata
    {
        public string? AssetId { get; set; }
        public string? RelativePath { get; set; }
        public string? Sha256 { get; set; }
        public long Size { get; set; }
        public string? SourceUrl { get; set; }
        public string? LicenseExpression { get; set; }
    }
}
