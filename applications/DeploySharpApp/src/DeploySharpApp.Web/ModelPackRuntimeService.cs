using JYPPX.DeploySharp;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.Models;

namespace DeploySharpApp.Web;

/// <summary>Loads a local ModelPack through the main library's strict validation boundary.</summary>
public sealed class ModelPackRuntimeService
{
    public async Task<ModelPackMaterialization> ValidateAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifestPath)) throw new ArgumentException("请输入 ModelPack manifest 路径。", nameof(manifestPath));
        LocalModelPackage package = await ModelPackageLoader.LoadAsync(manifestPath, ModelPackageLoadOptions.Default, cancellationToken).ConfigureAwait(false);
        ResolvedModelArtifact resolved = package.Artifacts.FirstOrDefault()
            ?? throw new InvalidOperationException("ModelPack 没有可运行的 artifact。");
        ModelArtifact artifact = resolved.ToCoreArtifact();
        return new ModelPackMaterialization(
            package.Manifest.ModelId.Value,
            package.ManifestPath,
            package.PackageRoot,
            artifact.Format,
            artifact.Location,
            artifact.Sha256,
            resolved.Document.CompatibleBackends.ToArray());
    }
}

public sealed record ModelPackMaterialization(
    string ModelId,
    string ManifestPath,
    string PackageRoot,
    string Format,
    string ModelPath,
    string? Sha256,
    IReadOnlyList<string> CompatibleBackends);
