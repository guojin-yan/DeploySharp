using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace DeploySharpApp.Web;

public sealed class VisualTestImageCatalogService
{
    public const string ReleaseTag = "test-assets.1";
    private const string Repository = "guojin-yan/DeploySharp";
    private readonly HttpClient _http;
    private readonly string _cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharpApp", "test-images");
    private static readonly object SharedLoadGate = new();
    private static Task<IReadOnlyList<VisualTestImage>>? SharedLoadTask;

    public VisualTestImageCatalogService(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DeploySharpApp", "2.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public Task<IReadOnlyList<VisualTestImage>> GetImagesAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<VisualTestImage>> loadTask;
        lock (SharedLoadGate)
        {
            if (SharedLoadTask is null || SharedLoadTask.IsCanceled || SharedLoadTask.IsFaulted)
                SharedLoadTask = LoadCoreAsync(progress, CancellationToken.None);
            loadTask = SharedLoadTask;
        }
        return loadTask.WaitAsync(cancellationToken);
    }

    public string GetCachedPath(VisualTestImage image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        return Path.Combine(_cacheRoot, image.Sha256[..Math.Min(12, image.Sha256.Length)], SafeFileName(image.FileName));
    }

    public VisualTestImage? FindDefault(IEnumerable<VisualTestImage> images, string task, string? displayName = null)
    {
        if (images == null) throw new ArgumentNullException(nameof(images));
        string key = TaskKey(task, displayName);
        return images.FirstOrDefault(image => image.Tasks.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<VisualTestImageDownloadResult> DownloadAsync(VisualTestImage image, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        string directory = Path.Combine(_cacheRoot, image.Sha256[..Math.Min(12, image.Sha256.Length)]);
        Directory.CreateDirectory(directory);
        string targetPath = Path.Combine(directory, SafeFileName(image.FileName));
        if (File.Exists(targetPath) && await VerifySha256Async(targetPath, image.Sha256, cancellationToken).ConfigureAwait(false))
        {
            progress?.Report(1);
            return new VisualTestImageDownloadResult(image with { Cached = true }, targetPath);
        }

        string temporaryPath = targetPath + ".partial";
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(image.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true);
            byte[] buffer = new byte[128 * 1024];
            long completed = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                completed += read;
                progress?.Report(image.SizeBytes <= 0 ? 0 : Math.Min(1, completed / (double)image.SizeBytes));
            }
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (!await VerifySha256Async(temporaryPath, image.Sha256, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("SHA256 mismatch for test image " + image.FileName + ".");
            File.Move(temporaryPath, targetPath, overwrite: true);
            progress?.Report(1);
            return new VisualTestImageDownloadResult(image with { Cached = true }, targetPath);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private async Task<IReadOnlyList<VisualTestImage>> LoadCoreAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using JsonDocument release = await GetJsonAsync($"https://api.github.com/repos/{Repository}/releases/tags/{ReleaseTag}", cancellationToken).ConfigureAwait(false);
        var assets = new Dictionary<string, ReleaseAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement asset in release.RootElement.GetProperty("assets").EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString()!;
            assets[name] = new ReleaseAsset(name, asset.GetProperty("browser_download_url").GetString()!, asset.GetProperty("size").GetInt64());
        }
        if (!assets.TryGetValue("test-image-catalog.json", out ReleaseAsset? catalogAsset) || !assets.TryGetValue("SHA256SUMS", out ReleaseAsset? sumsAsset))
            throw new InvalidDataException("The test image release must contain test-image-catalog.json and SHA256SUMS.");

        string catalogJson = await _http.GetStringAsync(catalogAsset.DownloadUrl, cancellationToken).ConfigureAwait(false);
        string sums = await _http.GetStringAsync(sumsAsset.DownloadUrl, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> hashes = ParseHashes(sums);
        using JsonDocument catalog = JsonDocument.Parse(catalogJson);
        var result = new List<VisualTestImage>();
        foreach (JsonElement item in catalog.RootElement.GetProperty("assets").EnumerateArray())
        {
            string? fileName = String(item, "fileName");
            string? id = String(item, "id");
            string? sha = String(item, "sha256");
            if (fileName == null || id == null || sha == null || !assets.TryGetValue(fileName, out ReleaseAsset? asset)) continue;
            if (!hashes.TryGetValue(fileName, out string? releaseSha) || !string.Equals(sha, releaseSha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The test image catalog and SHA256SUMS disagree for " + fileName + ".");
            string mediaType = String(item, "mediaType") ?? "application/octet-stream";
            string[] tasks = item.TryGetProperty("tasks", out JsonElement taskValues)
                ? taskValues.EnumerateArray().Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                : Array.Empty<string>();
            result.Add(new VisualTestImage(id, fileName, mediaType, asset.Size, sha, tasks, asset.DownloadUrl, IsCached(fileName, sha)));
        }
        progress?.Report(1);
        return result.OrderBy(image => image.FileName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private bool IsCached(string fileName, string sha256)
    {
        string path = Path.Combine(_cacheRoot, sha256[..Math.Min(12, sha256.Length)], SafeFileName(fileName));
        return File.Exists(path);
    }

    private static string TaskKey(string task, string? displayName)
    {
        string value = ((task ?? string.Empty) + " " + (displayName ?? string.Empty)).ToLowerInvariant();
        if (value.Contains("ocr")) return "ocr";
        if (value.Contains("pose") || value.Contains("keypoint")) return "pose";
        if (value.Contains("obb") || value.Contains("oriented")) return "oriented-detection";
        if (value.Contains("classif")) return "classification";
        if (value.Contains("anomal")) return "anomaly";
        if (value.Contains("matting") || value.Contains("rmbg") || value.Contains("background")) return "background-removal";
        if (value.Contains("prompt") || value.Contains("sam")) return "promptable-segmentation";
        if (value.Contains("clip") || value.Contains("blip") || value.Contains("language") || value.Contains("caption")) return "visual-language";
        if (value.Contains("segment")) return "segmentation";
        return "detection";
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

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

    private static string? String(JsonElement element, string property) => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string SafeFileName(string value) => Path.GetFileName(value.Replace('\\', '/'));

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

public sealed record VisualTestImage(string Id, string FileName, string MediaType, long SizeBytes, string Sha256, IReadOnlyList<string> Tasks, string DownloadUrl, bool Cached);
public sealed record VisualTestImageDownloadResult(VisualTestImage Image, string Path);
