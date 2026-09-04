using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Web;

public sealed class VisualReleaseCatalogService
{
    public const string ReleaseTag = "models-visual.1";
    private const string Repository = "guojin-yan/DeploySharp";
    private readonly HttpClient _http;
    private readonly string _cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharpApp", "visual-models");
    private static readonly object SharedLoadGate = new();
    private static Task<IReadOnlyList<VisualReleaseModel>>? SharedLoadTask;

    public VisualReleaseCatalogService(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DeploySharpApp", "2.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public Task<IReadOnlyList<VisualReleaseModel>> GetModelsAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<VisualReleaseModel>> loadTask;
        lock (SharedLoadGate)
        {
            if (SharedLoadTask is null || SharedLoadTask.IsCanceled || SharedLoadTask.IsFaulted)
                SharedLoadTask = LoadCoreAsync(progress, CancellationToken.None);
            loadTask = SharedLoadTask;
        }
        return loadTask.WaitAsync(cancellationToken);
    }

    public string GetCachedPrimaryPath(VisualReleaseModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        return Path.Combine(_cacheRoot, CacheKey(model), SafeFileName(model.PrimaryFile.AssetName));
    }

    public async Task<VisualModelDownloadResult> DownloadAsync(VisualReleaseModel model, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        Directory.CreateDirectory(_cacheRoot);
        string modelDirectory = Path.Combine(_cacheRoot, CacheKey(model));
        Directory.CreateDirectory(modelDirectory);
        long total = model.Files.Sum(file => Math.Max(0, file.Size));
        long completed = 0;
        string? primaryPath = null;
        foreach (VisualReleaseFile file in model.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string targetPath = Path.Combine(modelDirectory, SafeFileName(file.AssetName));
            if (File.Exists(targetPath) && await VerifySha256Async(targetPath, file.Sha256, cancellationToken).ConfigureAwait(false))
            {
                completed += file.Size;
                progress?.Report(total == 0 ? 1 : completed / (double)total);
                if (file.IsPrimaryModel) primaryPath = targetPath;
                continue;
            }

            string temporaryPath = targetPath + ".partial";
            try
            {
                using HttpResponseMessage response = await _http.GetAsync(file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (var destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true))
                {
                    byte[] buffer = new byte[128 * 1024];
                    int read;
                    while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        completed += read;
                        progress?.Report(total == 0 ? 0 : Math.Min(1, completed / (double)total));
                    }
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                if (!await VerifySha256Async(temporaryPath, file.Sha256, cancellationToken).ConfigureAwait(false))
                    throw new InvalidDataException("SHA256 mismatch for release asset " + file.AssetName + ".");
                File.Move(temporaryPath, targetPath, overwrite: true);
                if (file.IsPrimaryModel) primaryPath = targetPath;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
        progress?.Report(1);
        return new VisualModelDownloadResult(model, primaryPath ?? throw new InvalidDataException("The release model has no downloadable ONNX entrypoint."));
    }

    private async Task<IReadOnlyList<VisualReleaseModel>> LoadCoreAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using JsonDocument release = await GetJsonAsync($"https://api.github.com/repos/{Repository}/releases/tags/{ReleaseTag}", cancellationToken).ConfigureAwait(false);
        var assets = new Dictionary<string, ReleaseAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement asset in release.RootElement.GetProperty("assets").EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString()!;
            assets[name] = new ReleaseAsset(name, asset.GetProperty("browser_download_url").GetString()!, asset.GetProperty("size").GetInt64());
        }
        if (!assets.TryGetValue("SHA256SUMS", out ReleaseAsset? sumsAsset)) throw new InvalidDataException("The visual model release does not contain SHA256SUMS.");
        string sums = await _http.GetStringAsync(sumsAsset.DownloadUrl, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> hashes = ParseHashes(sums);
        ReleaseAsset[] packs = assets.Values.Where(asset => asset.Name.EndsWith(".modelpack.json", StringComparison.OrdinalIgnoreCase)).OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var parsed = new ConcurrentBag<VisualReleaseModel>();
        int completed = 0;
        await Parallel.ForEachAsync(packs, new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = cancellationToken }, async (pack, token) =>
        {
            try
            {
                string json = await _http.GetStringAsync(pack.DownloadUrl, token).ConfigureAwait(false);
                VisualReleaseModel? model = ParseModel(pack, json, assets, hashes);
                if (model != null) parsed.Add(model);
            }
            finally { progress?.Report(Interlocked.Increment(ref completed) / (double)packs.Length); }
        }).ConfigureAwait(false);

        var grouped = parsed.GroupBy(model => model.LogicalModelId, StringComparer.OrdinalIgnoreCase);
        var result = new List<VisualReleaseModel>();
        foreach (IGrouping<string, VisualReleaseModel> group in grouped)
        {
            bool duplicate = group.Count() > 1;
            foreach (VisualReleaseModel model in group)
            {
                string id = duplicate ? model.LogicalModelId + "/" + Slug(model.ReleaseAssetName[..^".modelpack.json".Length]) : model.LogicalModelId;
                VisualReleaseModel identified = model with { Id = id };
                result.Add(identified with { Cached = IsCached(identified) });
            }
        }
        return result.OrderBy(model => model.Task, StringComparer.OrdinalIgnoreCase).ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private VisualReleaseModel? ParseModel(ReleaseAsset pack, string json, IReadOnlyDictionary<string, ReleaseAsset> assets, IReadOnlyDictionary<string, string> hashes)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string? modelId = String(root, "modelId");
        string? name = String(root, "name");
        string? task = String(root, "task");
        if (modelId == null || name == null || task == null || !root.TryGetProperty("artifacts", out JsonElement artifacts)) return null;
        JsonElement artifact = artifacts.EnumerateArray().FirstOrDefault();
        if (artifact.ValueKind != JsonValueKind.Object || !artifact.TryGetProperty("files", out JsonElement files)) return null;
        var modelFiles = new List<VisualReleaseFile>();
        foreach (JsonElement file in files.EnumerateArray())
        {
            if (!string.Equals(String(file, "role"), "model", StringComparison.OrdinalIgnoreCase)) continue;
            string? sha = String(file, "sha256");
            if (sha == null || !TryFindAsset(sha, assets, hashes, out ReleaseAsset? asset)) continue;
            string relativePath = String(file, "relativePath") ?? asset!.Name;
            modelFiles.Add(new VisualReleaseFile(asset!.Name, asset.DownloadUrl, asset.Size, sha, relativePath, modelFiles.Count == 0));
        }
        if (modelFiles.Count == 0) return null;
        var inputs = new List<VisualModelInput>();
        if (root.TryGetProperty("inputs", out JsonElement inputElements))
        {
            foreach (JsonElement input in inputElements.EnumerateArray())
            {
                if (String(input, "name") is not string inputName || String(input, "elementType") is not string elementType || !input.TryGetProperty("shape", out JsonElement shape)) continue;
                inputs.Add(new VisualModelInput(inputName, elementType, shape.EnumerateArray().Select(item => item.GetInt64()).ToArray()));
            }
        }
        string format = String(artifact, "format") ?? "onnx";
        string size = FormatSize(modelFiles.Sum(file => file.Size));
        string[] backends = artifact.TryGetProperty("compatibleBackends", out JsonElement backendElements)
            ? backendElements.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(value => value.Length > 0).Select(value => "deploysharp.backend." + value).ToArray()
            : new[] { "deploysharp.backend.onnxruntime" };
        string license = root.TryGetProperty("source", out JsonElement source) ? String(source, "licenseExpression") ?? "Unknown" : "Unknown";
        string precision = String(artifact, "precision") ?? "unspecified";
        string quantization = String(artifact, "quantization") ?? "none";
        string preprocessing = Extension(artifact, "deploysharp.preprocessing-version") ?? "not declared";
        string postprocessing = Extension(artifact, "deploysharp.postprocessing-version") ?? Extension(artifact, "deploysharp.postprocessing-contract") ?? "raw tensor output";
        string validation = Extension(artifact, "deploysharp.validation-status") ?? "not declared";
        int? opset = artifact.TryGetProperty("opset", out JsonElement opsetElement) && opsetElement.TryGetInt32(out int parsedOpset) ? parsedOpset : null;
        return new VisualReleaseModel(modelId, modelId, name, task, format, size, backends, license, String(root, "modelVersion") ?? "release", pack.Name, inputs, modelFiles, false)
        {
            Precision = precision,
            Quantization = quantization,
            Opset = opset,
            Preprocessing = preprocessing,
            Postprocessing = postprocessing,
            ValidationStatus = validation
        };
    }

    private bool IsCached(VisualReleaseModel model)
    {
        string directory = Path.Combine(_cacheRoot, CacheKey(model));
        return model.Files.All(file => File.Exists(Path.Combine(directory, SafeFileName(file.AssetName))));
    }

    private static string CacheKey(VisualReleaseModel model) => Slug(model.Id) + "-" + model.PrimaryFile.Sha256[..Math.Min(12, model.PrimaryFile.Sha256.Length)];
    private static string SafeFileName(string value) => Path.GetFileName(value.Replace('\\', '/'));
    private static string Slug(string value) => string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-')).Trim('-');
    private static string? String(JsonElement element, string property) => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? Extension(JsonElement element, string property)
    {
        return element.TryGetProperty("extensions", out JsonElement extensions) ? String(extensions, property) : null;
    }
    private static string FormatSize(long bytes) => bytes >= 1024 * 1024 * 1024 ? (bytes / 1024d / 1024d / 1024d).ToString("F1") + " GB" : (bytes / 1024d / 1024d).ToString("F1") + " MB";

    private static Dictionary<string, string> ParseHashes(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Length == 64) result[parts[1].TrimStart('*')] = parts[0].ToLowerInvariant();
        }
        return result;
    }

    private static bool TryFindAsset(string sha, IReadOnlyDictionary<string, ReleaseAsset> assets, IReadOnlyDictionary<string, string> hashes, out ReleaseAsset? asset)
    {
        foreach (KeyValuePair<string, string> pair in hashes)
        {
            if (!string.Equals(pair.Value, sha, StringComparison.OrdinalIgnoreCase)) continue;
            if (assets.TryGetValue(pair.Key, out ReleaseAsset? found)) { asset = found; return true; }
        }
        asset = null;
        return false;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> VerifySha256Async(string path, string expected, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, useAsync: true);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[128 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0) hash.AppendData(buffer, 0, read);
        return string.Equals(Convert.ToHexString(hash.GetHashAndReset()), expected, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ReleaseAsset(string Name, string DownloadUrl, long Size);
}

public sealed record VisualReleaseModel(
    string LogicalModelId,
    string Id,
    string DisplayName,
    string Task,
    string Format,
    string Size,
    IReadOnlyList<string> RecommendedBackends,
    string License,
    string Version,
    string ReleaseAssetName,
    IReadOnlyList<VisualModelInput> Inputs,
    IReadOnlyList<VisualReleaseFile> Files,
    bool Cached)
{
    public VisualReleaseFile PrimaryFile => Files.First(file => file.IsPrimaryModel);
    public string Precision { get; init; } = "unspecified";
    public string Quantization { get; init; } = "none";
    public int? Opset { get; init; }
    public string Preprocessing { get; init; } = "not declared";
    public string Postprocessing { get; init; } = "raw tensor output";
    public string ValidationStatus { get; init; } = "not declared";
    public AppModelInfo ToAppModelInfo() => new(Id, DisplayName, Task, Format, Size, RecommendedBackends, License, Cached, location: null, sha256: PrimaryFile.Sha256, externalArtifact: true);
}

public sealed record VisualModelInput(string Name, string ElementType, IReadOnlyList<long> Shape);
public sealed record VisualReleaseFile(string AssetName, string DownloadUrl, long Size, string Sha256, string RelativePath, bool IsPrimaryModel);
public sealed record VisualModelDownloadResult(VisualReleaseModel Model, string PrimaryModelPath);
