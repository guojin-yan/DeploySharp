using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeploySharpApp.Web;

/// <summary>Small local-only activity journal for diagnostics; it never uploads model or input content.</summary>
public sealed class WebActivityStore
{
    private const int MaxEntries = 100;
    private readonly object _gate = new();
    private readonly List<WebActivity> _entries = new();
    private readonly string _journalPath;

    public WebActivityStore()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
        _journalPath = Path.Combine(root, "DeploySharpApp", "web-activity.jsonl");
        Load();
    }

    public IReadOnlyList<WebActivity> Entries
    {
        get { lock (_gate) return _entries.AsEnumerable().Reverse().ToList(); }
    }

    public IReadOnlyList<WebActivity> Query(string? search = null, string? category = null)
    {
        lock (_gate)
        {
            IEnumerable<WebActivity> query = _entries;
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(item => string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(item => (item.Message + " " + item.Category + " " + item.Code).Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
            return query.Reverse().ToList();
        }
    }

    public IReadOnlyList<string> Categories
    {
        get { lock (_gate) return _entries.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(); }
    }

    public void Add(string category, string message, string? code = null)
    {
        var entry = new WebActivity(DateTimeOffset.Now, Sanitize(category), Sanitize(message), Sanitize(code));
        lock (_gate)
        {
            _entries.Add(entry);
            while (_entries.Count > MaxEntries) _entries.RemoveAt(0);
            Append(entry);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_journalPath)!);
                File.WriteAllText(_journalPath, string.Empty, Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public string ExportJson(IEnumerable<WebActivity>? entries = null)
        => JsonSerializer.Serialize((entries ?? Entries).ToArray(), new JsonSerializerOptions { WriteIndented = true });

    public string ExportText(IEnumerable<WebActivity>? entries = null)
    {
        IEnumerable<WebActivity> snapshot = entries ?? Entries;
        return string.Join(Environment.NewLine, snapshot.Select(item => $"[{item.Timestamp:O} {item.Category}] {item.Message}{(string.IsNullOrWhiteSpace(item.Code) ? string.Empty : " · " + item.Code)}"));
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_journalPath)) return;
            foreach (string line in File.ReadLines(_journalPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    WebActivity? item = JsonSerializer.Deserialize<WebActivity>(line);
                    if (item is not null) _entries.Add(item);
                }
                catch (JsonException) { }
            }
            if (_entries.Count > MaxEntries) _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void Append(WebActivity entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_journalPath)!);
            File.AppendAllText(_journalPath, JsonSerializer.Serialize(entry) + Environment.NewLine, Encoding.UTF8);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string result = value.Trim();
        result = Regex.Replace(result, "(?i)[a-z]:\\\\[^\\r\\n]+", "<path>");
        result = Regex.Replace(result, @"(?<![a-z0-9])/(?:[^\s]+/)+[^\s]+", "<path>", RegexOptions.IgnoreCase);
        return result;
    }
}

public sealed record WebActivity(DateTimeOffset Timestamp, string Category, string Message, string? Code);
