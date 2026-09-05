using JYPPX.DeploySharp.ModelFactory;

namespace DeploySharpApp.Web;

/// <summary>Exposes the validated, embedded ModelFactory catalog to the Web UI without implying that assets are downloaded.</summary>
public sealed class ModelFactoryCatalogService
{
    private readonly Lazy<ValidatedModelCatalog> _catalog = new(OfficialModelCatalog.Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public ValidatedModelCatalog Catalog => _catalog.Value;

    public IReadOnlyList<ModelFactoryCatalogItem> Items => Catalog.Document.Entries
        .Select(entry => new ModelFactoryCatalogItem(
            entry.ModelId ?? "unknown",
            entry.Name ?? entry.ModelId ?? "Unnamed model",
            entry.Task ?? "unspecified",
            entry.Family ?? "unspecified",
            entry.Status.ToString(),
            entry.ModelVersion ?? "unspecified",
            entry.Description ?? string.Empty,
            entry.Source?.LicenseExpression ?? "Unknown",
            entry.Artifacts.Select(artifact => new ModelFactoryArtifactItem(
                artifact.ArtifactId ?? "unknown",
                artifact.Format ?? "unspecified",
                artifact.Precision ?? "unspecified",
                artifact.Quantization ?? "none",
                artifact.Portable,
                artifact.CompatibleBackends,
                artifact.Assets.Count,
                artifact.Assets.Count(asset => asset.Kind == ModelCatalogAssetKind.TestInput))).ToArray(),
            entry.TestInputs.Count))
        .OrderBy(item => item.Task, StringComparer.OrdinalIgnoreCase)
        .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public sealed record ModelFactoryCatalogItem(
    string Id,
    string Name,
    string Task,
    string Family,
    string Status,
    string Version,
    string Description,
    string License,
    IReadOnlyList<ModelFactoryArtifactItem> Artifacts,
    int TestInputCount);

public sealed record ModelFactoryArtifactItem(
    string Id,
    string Format,
    string Precision,
    string Quantization,
    bool Portable,
    IReadOnlyList<string> CompatibleBackends,
    int AssetCount,
    int TestInputCount);
