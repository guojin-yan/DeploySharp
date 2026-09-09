using System.Text.Json;
using System.Net.Http;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Runtime.InteropServices;
using DeploySharpApp.Application;
using DeploySharpApp.Contracts;
using DeploySharpApp.Plugin.Abstractions;

namespace DeploySharpApp.Web;

public enum BackendLifecycleState { NotInstalled, Installing, Staged, Active, Failed, RolledBack }

public sealed record BackendLifecycleRecord(string BackendId, string Version, BackendLifecycleState State, string Message, DateTimeOffset UpdatedUtc, string? PreviousVersion = null, string? InstallRoot = null, IReadOnlyList<string>? Packages = null);

/// <summary>Persists manifest-driven backend lifecycle intent without claiming a native package is usable before a probe.</summary>
public sealed class BackendLifecycleService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, BackendLifecycleRecord> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    private readonly string _installRoot;
    private readonly DeploySharpAppService _application;
    private readonly HttpClient _http;

    public BackendLifecycleService(DeploySharpAppService application, HttpClient http, string? statePath = null, string? installRoot = null)
    {
        _application = application;
        _http = http;
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
        _path = statePath ?? Path.Combine(root, "DeploySharpApp", "backend-lifecycle.json");
        _installRoot = installRoot ?? Path.Combine(root, "DeploySharpApp", "backends");
        Load();
    }

    public BackendLifecycleRecord Get(string backendId, string version)
    {
        lock (_gate) return _records.TryGetValue(backendId, out BackendLifecycleRecord? value) ? value : new BackendLifecycleRecord(backendId, version, BackendLifecycleState.NotInstalled, "尚未准备此版本。", DateTimeOffset.UtcNow);
    }

    public async Task<BackendLifecycleRecord> InstallOrUpgradeAsync(string backendId, CancellationToken cancellationToken = default)
    {
        AppBackendInfo backend = Find(backendId);
        BackendLifecycleRecord previous = Get(backend.Id, backend.Version);
        string? previousVersion = previous.State == BackendLifecycleState.NotInstalled || string.Equals(previous.Version, backend.Version, StringComparison.OrdinalIgnoreCase) ? previous.PreviousVersion : previous.Version;
        string installRoot = GetInstallRoot(backend.Id, backend.Version);
        var packages = new List<string>();
        Set(new BackendLifecycleRecord(backend.Id, backend.Version, BackendLifecycleState.Installing, "正在根据 manifest 下载并校验可下载依赖。", DateTimeOffset.UtcNow, previousVersion, installRoot, packages));
        try
        {
            PluginManifest manifest = DefaultManifests.Create().First(item => string.Equals(item.PluginId, backend.Id, StringComparison.OrdinalIgnoreCase));
            foreach (ManifestRuntimeDependency dependency in manifest.RuntimeDependencies ?? new List<ManifestRuntimeDependency>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!dependency.Downloadable || string.IsNullOrWhiteSpace(dependency.PackageId) || string.IsNullOrWhiteSpace(dependency.Version) || !IsRuntimeCompatible(dependency.Rid) || !IsConditionEnabled(dependency.Condition)) continue;
                await DownloadPackageAsync(dependency.PackageId!, dependency.Version!, installRoot, cancellationToken).ConfigureAwait(false);
                packages.Add(dependency.PackageId + "/" + dependency.Version);
            }
            BackendRuntimeStatus? status = _application.RuntimeStatuses.FirstOrDefault(item => string.Equals(item.BackendId, backend.Id, StringComparison.OrdinalIgnoreCase));
            bool canActivate = status?.State == AppRuntimeState.Available;
            return Set(new BackendLifecycleRecord(backend.Id, backend.Version, canActivate ? BackendLifecycleState.Active : BackendLifecycleState.Staged,
                canActivate ? "依赖已下载并通过当前应用探测，可作为活动版本使用。" : "依赖已下载并完成 SHA512 校验，仍需使用 staged 根目录执行真实 native/ABI probe；未伪造可用状态。", DateTimeOffset.UtcNow, previousVersion, installRoot, packages));
        }
        catch (OperationCanceledException)
        {
            return Set(new BackendLifecycleRecord(backend.Id, backend.Version, BackendLifecycleState.Failed, "后端依赖下载已取消。", DateTimeOffset.UtcNow, previousVersion, installRoot, packages));
        }
        catch (Exception exception) when (exception is HttpRequestException || exception is IOException || exception is InvalidDataException || exception is CryptographicException)
        {
            return Set(new BackendLifecycleRecord(backend.Id, backend.Version, BackendLifecycleState.Failed, "后端依赖下载或 SHA512 校验失败：" + exception.Message, DateTimeOffset.UtcNow, previousVersion, installRoot, packages));
        }
    }

    public BackendLifecycleRecord Rollback(string backendId)
    {
        AppBackendInfo backend = Find(backendId);
        BackendLifecycleRecord current = Get(backend.Id, backend.Version);
        return Set(current with { State = string.IsNullOrWhiteSpace(current.PreviousVersion) ? BackendLifecycleState.NotInstalled : BackendLifecycleState.RolledBack, Message = string.IsNullOrWhiteSpace(current.PreviousVersion) ? "没有可回滚的历史版本。" : "已回滚活动版本指针；native runtime 仍需重新探测。", UpdatedUtc = DateTimeOffset.UtcNow });
    }

    public BackendLifecycleRecord ActivateAfterProbe(string backendId, RuntimeProbeEvidence evidence)
    {
        if (evidence is null) throw new ArgumentNullException(nameof(evidence));
        BackendLifecycleRecord current = _records.TryGetValue(backendId, out BackendLifecycleRecord? value)
            ? value
            : throw new InvalidOperationException("后端尚未下载，无法激活。");
        if (!evidence.Succeeded)
            return Set(current with { State = BackendLifecycleState.Staged, Message = "staged 根目录的真实 ABI smoke 未通过，保持 Staged；" + evidence.Message, UpdatedUtc = DateTimeOffset.UtcNow });
        return Set(current with { State = BackendLifecycleState.Active, Message = "staged 根目录已通过真实 Worker ABI smoke，可作为当前活动版本；" + evidence.Message, UpdatedUtc = DateTimeOffset.UtcNow });
    }

    private AppBackendInfo Find(string backendId) => _application.Backends.FirstOrDefault(item => string.Equals(item.Id, backendId, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("未知后端：" + backendId);

    private string GetInstallRoot(string backendId, string version)
    {
        string safeId = string.Join('_', backendId.Split(Path.GetInvalidFileNameChars()));
        string safeVersion = string.Join('_', version.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(_installRoot, safeId, safeVersion);
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool IsRuntimeCompatible(string? rid)
    {
        if (string.IsNullOrWhiteSpace(rid)) return true;
        string current = RuntimeInformation.RuntimeIdentifier;
        if (string.Equals(rid, current, StringComparison.OrdinalIgnoreCase)) return true;
        return rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && current.StartsWith("win-", StringComparison.OrdinalIgnoreCase)
            || rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase) && current.StartsWith("linux-", StringComparison.OrdinalIgnoreCase)
            || rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase) && current.StartsWith("osx-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConditionEnabled(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        // The current Web UI supports audited CPU execution only. GPU packages remain
        // discoverable in the manifest but are not downloaded unless a future CUDA flow opts in.
        if (condition.Contains("device == cuda", StringComparison.OrdinalIgnoreCase)) return false;
        if (condition.Contains("device == cpu", StringComparison.OrdinalIgnoreCase)) return true;
        if (condition.Contains("runtimeIdentifier ==", StringComparison.OrdinalIgnoreCase))
            return condition.Contains(RuntimeInformation.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase)
                || condition.Contains(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-" : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux-" : "osx-", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private async Task DownloadPackageAsync(string packageId, string version, string installRoot, CancellationToken cancellationToken)
    {
        string id = packageId.ToLowerInvariant();
        string normalizedVersion = version.ToLowerInvariant();
        string packageRoot = Path.Combine(installRoot, id, normalizedVersion);
        Directory.CreateDirectory(packageRoot);
        string packagePath = Path.Combine(packageRoot, id + "." + normalizedVersion + ".nupkg");
        string url = $"https://api.nuget.org/v3-flatcontainer/{id}/{normalizedVersion}/{id}.{normalizedVersion}.nupkg";
        if (!File.Exists(packagePath))
        {
            using HttpResponseMessage response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string tempPath = packagePath + ".download";
            await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (FileStream target = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, packagePath, true);
        }
        string hashText = (await _http.GetStringAsync(url + ".sha512", cancellationToken).ConfigureAwait(false)).Trim();
        byte[] expected = Convert.FromBase64String(hashText);
        await using FileStream stream = new(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] actual = await SHA512.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected)) throw new CryptographicException("NuGet package SHA512 mismatch for " + packageId + " " + version + ".");
        string extractedMarker = Path.Combine(packageRoot, ".extracted");
        if (!File.Exists(extractedMarker))
        {
            ZipFile.ExtractToDirectory(packagePath, packageRoot, true);
            File.WriteAllText(extractedMarker, DateTimeOffset.UtcNow.ToString("O"));
        }
    }

    private BackendLifecycleRecord Set(BackendLifecycleRecord record)
    {
        lock (_gate) { _records[record.BackendId] = record; Save(); }
        return record;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            BackendLifecycleRecord[] records = JsonSerializer.Deserialize<BackendLifecycleRecord[]>(File.ReadAllText(_path)) ?? Array.Empty<BackendLifecycleRecord>();
            foreach (BackendLifecycleRecord record in records) _records[record.BackendId] = record;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException) { }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_records.Values.OrderBy(item => item.BackendId), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
