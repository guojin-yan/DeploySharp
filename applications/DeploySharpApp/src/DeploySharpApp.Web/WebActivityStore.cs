using System.Collections.Concurrent;

namespace DeploySharpApp.Web;

public sealed class WebActivityStore
{
    private readonly ConcurrentQueue<WebActivity> _entries = new();

    public IReadOnlyList<WebActivity> Entries => _entries.Reverse().Take(100).ToList();

    public void Add(string category, string message, string? code = null)
    {
        _entries.Enqueue(new WebActivity(DateTimeOffset.Now, category, message, code));
        while (_entries.Count > 100 && _entries.TryDequeue(out _)) { }
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}

public sealed record WebActivity(DateTimeOffset Timestamp, string Category, string Message, string? Code);
