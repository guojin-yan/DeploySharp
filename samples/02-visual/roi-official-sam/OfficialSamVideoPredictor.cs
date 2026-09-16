using System.Diagnostics;
using System.Text;
using System.Text.Json;
using JYPPX.DeploySharp.Visual;

// Application-owned adapter: Python/PyTorch do not become dependencies of the
// Visual NuGet package. One worker owns one video, model and predictor state.
internal sealed class OfficialSamVideoPredictor : IVisualRoiVideoPromptPredictor<int, JsonElement>, IAsyncDisposable
{
    private readonly Process _worker;
    private readonly Task _errorPump;
    private readonly StringBuilder _errors = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _faulted;
    private bool _disposed;

    internal OfficialSamVideoPredictor(PromptableSegmentationProfile profile, string python, IEnumerable<string> arguments)
    {
        Profile = profile;
        var start = new ProcessStartInfo(python)
        {
            UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        start.ArgumentList.Add("-u");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "official_sam_worker.py"));
        foreach (string value in arguments) start.ArgumentList.Add(value);
        _worker = Process.Start(start) ?? throw new InvalidOperationException("Cannot start official SAM worker.");
        _errorPump = Task.Run(async () =>
        {
            while (await _worker.StandardError.ReadLineAsync() is string line)
            {
                lock (_errors)
                {
                    _errors.AppendLine(line);
                    if (_errors.Length > 16000) _errors.Remove(0, _errors.Length - 16000);
                }
            }
        });
    }

    public PromptableSegmentationProfile Profile { get; }

    internal async Task<JsonElement> InitializeAsync(CancellationToken token)
    {
        try { return await ReadAsync(token); }
        catch { StopAfterFailure(); throw; }
    }

    public Task<JsonElement> ProcessFrameAsync(int frame, VisualRoiVideoPromptPlan plan, CancellationToken cancellationToken)
    {
        if (frame != plan.FrameIndex) throw new ArgumentException("Frame and ROI plan index differ.", nameof(frame));
        var items = plan.Items.Select(item => new
        {
            roi_id = item.Roi.Id,
            object_id = item.ObjectIndex + 1,
            points = item.Prompt?.Points.Select(point => new[] { point.X, point.Y }).ToArray(),
            labels = item.Prompt?.Points.Select(point => (int)point.Label).ToArray(),
            box = item.Prompt?.Box is { } box ? new[] { box.X, box.Y, box.Right, box.Bottom } : null
        }).ToArray();
        return ExchangeAsync(new { operation = "frame", frame_index = frame, mode = plan.Mode.ToString(), items }, cancellationToken);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await ExchangeAsync(new { operation = "reset" }, cancellationToken);
    }

    private async Task<JsonElement> ExchangeAsync(object request, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_faulted) throw new InvalidOperationException("Predictor state is uncertain after failure; create a new worker and planner.");
            token.ThrowIfCancellationRequested();
            await _worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), token);
            await _worker.StandardInput.FlushAsync(token);
            return await ReadAsync(token);
        }
        catch { StopAfterFailure(); throw; }
        finally { _gate.Release(); }
    }

    private async Task<JsonElement> ReadAsync(CancellationToken token)
    {
        string? line = await _worker.StandardOutput.ReadLineAsync(token);
        if (line == null)
        {
            await _errorPump.WaitAsync(token);
            string detail;
            lock (_errors) detail = _errors.ToString();
            throw new InvalidOperationException("Official SAM worker exited: " + detail);
        }
        using JsonDocument document = JsonDocument.Parse(line);
        if (document.RootElement.TryGetProperty("error", out JsonElement error))
            throw new InvalidOperationException("Official SAM predictor: " + error.GetString());
        return document.RootElement.Clone();
    }

    private void StopAfterFailure()
    {
        _faulted = true;
        try { if (!_worker.HasExited) _worker.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            StopAfterFailure();
            await _worker.WaitForExitAsync();
            await _errorPump;
            _worker.Dispose();
        }
        finally { _gate.Release(); }
    }
}
