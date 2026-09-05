using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.Models;

namespace DeploySharpApp.Web;

/// <summary>Connects the validated ModelFactory catalog to the server-side runtime cache and Core artifact contract.</summary>
public sealed class ModelFactoryRuntimeService : IDisposable
{
    private readonly ModelFactoryCatalogService _catalog;
    private readonly ModelFactoryClient _client;

    public ModelFactoryRuntimeService(ModelFactoryCatalogService catalog)
    {
        _catalog = catalog;
        string root = Environment.GetEnvironmentVariable("DEPLOYSHARPAPP_MODEL_CACHE")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharpApp", "models");
        bool offline = string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARPAPP_MODEL_OFFLINE"), "1", StringComparison.OrdinalIgnoreCase);
        _client = new ModelFactoryClient(_catalog.Catalog, new ModelFactoryOptions(root, offline: offline, allowTestInputs: true));
    }

    public async Task<ModelFactoryMaterialization> MaterializeAsync(string modelId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ModelSelection selection = _client.Select(new ModelQuery(modelId: modelId, includePreview: true));
        MaterializedModel materialized = await _client.GetModelAsync(selection, progress, cancellationToken).ConfigureAwait(false);
        ResolvedModelArtifact artifact = materialized.Package.Artifacts.FirstOrDefault()
            ?? throw new InvalidOperationException("The verified ModelPack contains no runnable artifact.");
        ModelArtifact coreArtifact = artifact.ToCoreArtifact();
        return new ModelFactoryMaterialization(materialized.Selection.Entry.ModelId!, materialized.CacheKey, materialized.PackageRoot, materialized.Package.ManifestPath, coreArtifact.ModelId.Value, coreArtifact.Format, coreArtifact.Location, coreArtifact.Sha256);
    }

    public void Dispose() => _client.Dispose();
}

public sealed record ModelFactoryMaterialization(string ModelId, string CacheKey, string PackageRoot, string ManifestPath, string CoreModelId, string Format, string ModelPath, string? Sha256);
