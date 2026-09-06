using DeploySharpApp.BackendHost.Protocol;
using DeploySharpApp.Contracts;
using DeploySharpApp.Infrastructure;

namespace DeploySharpApp.Web;

/// <summary>Executes native ABI smoke probes only in the short-lived BackendHost process.</summary>
public sealed class RuntimeProbeService
{
    private readonly IBackendHostWorkerClient _worker;

    public RuntimeProbeService(IBackendHostWorkerClient worker) => _worker = worker;

    public async Task<RuntimeProbeEvidence> ProbeAsync(string backendId, string smokeModelPath, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["smokeModelPath"] = smokeModelPath
        };
        WorkerResponse response = await _worker.SendAsync(
            new WorkerRequest(WorkerMessageKind.Probe, "probe-" + Guid.NewGuid().ToString("N"), backendId, payload: payload),
            TimeSpan.FromMinutes(2),
            cancellationToken).ConfigureAwait(false);
        string state = Value(response.Payload, "state") ?? AppRuntimeState.ProbeFailed.ToString();
        string code = Value(response.Payload, "diagnosticCode") ?? "DSAPP-WORKER-PROBE-FAILED";
        return new RuntimeProbeEvidence(response.Succeeded, backendId, state, code, response.Message ?? "Worker probe completed.", response.Payload);
    }

    private static string? Value(IReadOnlyDictionary<string, string> payload, string key)
        => payload.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

public sealed record RuntimeProbeEvidence(
    bool Succeeded,
    string BackendId,
    string State,
    string DiagnosticCode,
    string Message,
    IReadOnlyDictionary<string, string> Details);
