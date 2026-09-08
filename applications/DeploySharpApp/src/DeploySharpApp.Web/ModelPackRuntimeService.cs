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
        string executablePath = ResolveExecutablePath(package.PackageRoot, resolved, artifact, modelFiles);
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
            ArtifactId = resolved.Document.ArtifactId,
            BundleRole = resolved.Document.Extensions.TryGetValue("deploysharp.bundle-role", out string? bundleRole) ? bundleRole : null,
            Opset = resolved.Document.Opset,
            Extensions = resolved.Document.Extensions,
            VerifiedFiles = resolved.Files.Select(file => file.FullPath).ToArray(),
            Assets = resolved.Files.Select(file => new ModelPackAsset(
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
    public string? ArtifactId { get; init; }
    public string? BundleRole { get; init; }
    public int? Opset { get; init; }
    public IReadOnlyDictionary<string, string> Extensions { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> VerifiedFiles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ModelPackAsset> Assets { get; init; } = Array.Empty<ModelPackAsset>();
}

public sealed record ModelPackAsset(string RelativePath, string FullPath, ModelFileRole Role, string? Sha256, long Size);
