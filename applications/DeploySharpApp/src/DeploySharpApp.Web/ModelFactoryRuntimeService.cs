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
        string[] modelFiles = artifact.Files.Where(file => file.Document.Role == ModelFileRole.Model).Select(file => file.FullPath).ToArray();
        string modelPath = ResolveExecutablePath(materialized.PackageRoot, artifact, coreArtifact, modelFiles);
        return new ModelFactoryMaterialization(
            materialized.Selection.Entry.ModelId!,
            materialized.CacheKey,
            materialized.PackageRoot,
            materialized.Package.ManifestPath,
            coreArtifact.ModelId.Value,
            coreArtifact.Format,
            modelPath,
            coreArtifact.Sha256)
        {
            ArtifactId = artifact.Document.ArtifactId,
            BundleRole = artifact.Document.Extensions.TryGetValue("deploysharp.bundle-role", out string? bundleRole) ? bundleRole : null,
            Opset = artifact.Document.Opset,
            Extensions = artifact.Document.Extensions,
            Assets = artifact.Files.Select(file => new ModelPackAsset(
                file.Document.RelativePath ?? Path.GetFileName(file.FullPath),
                file.FullPath,
                file.Document.Role,
                file.Document.Sha256,
                file.Document.Size)).ToArray()
        };
    }

    private static string ResolveExecutablePath(string packageRoot, ResolvedModelArtifact resolved, ModelArtifact artifact, IReadOnlyList<string> modelFiles)
    {
        if (resolved.Document.LocationKind == ModelArtifactLocationKind.File) return artifact.Location;
        if (!string.IsNullOrWhiteSpace(resolved.Document.Entrypoint))
        {
            string candidate = Path.GetFullPath(Path.Combine(packageRoot, resolved.Document.Entrypoint));
            if (File.Exists(candidate)) return candidate;
        }
        return modelFiles.FirstOrDefault() ?? artifact.Location;
    }

    public void Dispose() => _client.Dispose();
}

public sealed record ModelFactoryMaterialization(string ModelId, string CacheKey, string PackageRoot, string ManifestPath, string CoreModelId, string Format, string ModelPath, string? Sha256)
{
    public string? ArtifactId { get; init; }
    public string? BundleRole { get; init; }
    public int? Opset { get; init; }
    public IReadOnlyDictionary<string, string> Extensions { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ModelPackAsset> Assets { get; init; } = Array.Empty<ModelPackAsset>();
}
