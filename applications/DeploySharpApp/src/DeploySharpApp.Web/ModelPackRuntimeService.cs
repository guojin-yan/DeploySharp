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
        string[] modelFiles = resolved.Files
            .Where(file => file.Document.Role == ModelFileRole.Model)
            .Select(file => file.FullPath)
            .ToArray();
        bool bundle = resolved.Document.LocationKind == ModelArtifactLocationKind.Directory && modelFiles.Length > 1;
        string executablePath = resolved.Document.LocationKind == ModelArtifactLocationKind.File
            ? artifact.Location
            : modelFiles.Length == 1 ? modelFiles[0] : artifact.Location;
        return new ModelPackMaterialization(
            package.Manifest.ModelId.Value,
            package.ManifestPath,
            package.PackageRoot,
            artifact.Format,
            executablePath,
            artifact.Sha256,
            resolved.Document.CompatibleBackends.ToArray())
        {
            IsBundle = bundle,
            VerifiedFiles = resolved.Files.Select(file => file.FullPath).ToArray()
        };
    }
}

public sealed record ModelPackMaterialization(
    string ModelId,
    string ManifestPath,
    string PackageRoot,
    string Format,
    string ModelPath,
    string? Sha256,
    IReadOnlyList<string> CompatibleBackends)
{
    public bool IsBundle { get; init; }
    public IReadOnlyList<string> VerifiedFiles { get; init; } = Array.Empty<string>();
}
