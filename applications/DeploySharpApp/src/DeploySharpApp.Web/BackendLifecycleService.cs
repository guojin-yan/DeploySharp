using System.Text.Json;
using DeploySharpApp.Application;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Web;

public enum BackendLifecycleState { NotInstalled, Installing, Staged, Active, Failed, RolledBack }

public sealed record BackendLifecycleRecord(string BackendId, string Version, BackendLifecycleState State, string Message, DateTimeOffset UpdatedUtc, string? PreviousVersion = null);

/// <summary>Persists manifest-driven backend lifecycle intent without claiming a native package is usable before a probe.</summary>
public sealed class BackendLifecycleService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, BackendLifecycleRecord> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    private readonly DeploySharpAppService _application;

    public BackendLifecycleService(DeploySharpAppService application)
    {
        _application = application;
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
        _path = Path.Combine(root, "DeploySharpApp", "backend-lifecycle.json");
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
        Set(new BackendLifecycleRecord(backend.Id, backend.Version, BackendLifecycleState.Installing, "正在根据 manifest 准备后端版本。", DateTimeOffset.UtcNow, previousVersion));
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        BackendRuntimeStatus? status = _application.RuntimeStatuses.FirstOrDefault(item => string.Equals(item.BackendId, backend.Id, StringComparison.OrdinalIgnoreCase));
        bool canActivate = status?.State == AppRuntimeState.Available;
        return Set(new BackendLifecycleRecord(backend.Id, backend.Version, canActivate ? BackendLifecycleState.Active : BackendLifecycleState.Staged,
            canActivate ? "版本已准备并通过当前应用探测，可作为活动版本使用。" : "版本已准备，但必须完成真实 native/ABI probe 后才能激活；未伪造可用状态。", DateTimeOffset.UtcNow, previousVersion));
    }

    public BackendLifecycleRecord Rollback(string backendId)
    {
        AppBackendInfo backend = Find(backendId);
        BackendLifecycleRecord current = Get(backend.Id, backend.Version);
        return Set(current with { State = string.IsNullOrWhiteSpace(current.PreviousVersion) ? BackendLifecycleState.NotInstalled : BackendLifecycleState.RolledBack, Message = string.IsNullOrWhiteSpace(current.PreviousVersion) ? "没有可回滚的历史版本。" : "已回滚活动版本指针；native runtime 仍需重新探测。", UpdatedUtc = DateTimeOffset.UtcNow });
    }

    private AppBackendInfo Find(string backendId) => _application.Backends.FirstOrDefault(item => string.Equals(item.Id, backendId, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("未知后端：" + backendId);

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
