using System.Text.Json;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Web;

public sealed record BenchmarkHistoryItem(
    DateTimeOffset TimestampUtc,
    string ModelId,
    string BackendId,
    bool Available,
    string Message,
    double P50Ms,
    double P95Ms,
    double Throughput,
    string? ExecutionMode,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics);

/// <summary>Persists the last 50 verified benchmark reports in the application data directory.</summary>
public sealed class BenchmarkHistoryStore
{
    private const int MaxEntries = 50;
    private readonly object _gate = new();
    private readonly List<BenchmarkHistoryItem> _entries = new();
    private readonly string _path;

    public BenchmarkHistoryStore() : this(null) { }

    public BenchmarkHistoryStore(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
            path = Path.Combine(root, "DeploySharpApp", "benchmark-history.json");
        }
        _path = Path.GetFullPath(path);
        Load();
    }

    public IReadOnlyList<BenchmarkHistoryItem> Entries
    {
        get { lock (_gate) return _entries.ToList(); }
    }

    public void Add(BenchmarkReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));
        var item = new BenchmarkHistoryItem(DateTimeOffset.UtcNow, report.Request.ModelId, report.Request.BackendId, report.Available, report.Message, report.P50Ms, report.P95Ms, report.Throughput, report.ExecutionMode, report.Metadata, report.Diagnostics);
        lock (_gate)
        {
            _entries.Insert(0, item);
            if (_entries.Count > MaxEntries) _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            Save();
        }
    }

    public string ExportJson()
    {
        lock (_gate) return JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            BenchmarkHistoryItem[] items = JsonSerializer.Deserialize<BenchmarkHistoryItem[]>(File.ReadAllText(_path)) ?? Array.Empty<BenchmarkHistoryItem>();
            _entries.AddRange(items.Take(MaxEntries));
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException) { }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string tempPath = _path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, _path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
