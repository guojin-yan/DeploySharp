using System.Globalization;
using DeploySharpApp.BackendHost;
using DeploySharpApp.BackendHost.Protocol;
using DeploySharpApp.Contracts;

Console.Error.WriteLine("DeploySharpApp BackendHost protocol v" + WorkerProtocol.ProtocolVersion);
using var outputGate = new SemaphoreSlim(1, 1);
CancellationTokenSource? activeCancellation = null;
Task? activeTask = null;
string? activeRequestId = null;

while (true)
{
    string? line = await Console.In.ReadLineAsync();
    if (line == null) break;
    WorkerRequest request;
    try { request = WorkerProtocol.DeserializeRequest(line); }
    catch (Exception exception)
    {
        await WriteResponseAsync(new WorkerResponse(WorkerResponseKind.Error, "unknown", false, exception.Message));
        continue;
    }

    if (activeTask?.IsCompleted == true)
    {
        await activeTask;
        activeTask = null;
        activeCancellation?.Dispose();
        activeCancellation = null;
        activeRequestId = null;
    }

    WorkerResponse response;
    switch (request.Kind)
    {
        case WorkerMessageKind.Handshake:
            string protocolVersion = request.Payload.TryGetValue("protocolVersion", out string? requestedVersion) ? requestedVersion : string.Empty;
            bool protocolCompatible = string.Equals(protocolVersion, WorkerProtocol.ProtocolVersion.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            response = new WorkerResponse(WorkerResponseKind.Handshake, request.RequestId, protocolCompatible, protocolCompatible ? "DeploySharpApp BackendHost ready" : "Worker protocol version mismatch.", new Dictionary<string, string> { ["protocolVersion"] = WorkerProtocol.ProtocolVersion.ToString(CultureInfo.InvariantCulture), ["execution"] = "worker", ["host"] = "DeploySharpApp.BackendHost" });
            break;
        case WorkerMessageKind.Capability:
            response = new WorkerResponse(WorkerResponseKind.Capability, request.RequestId, true, "Capabilities are manifest-driven; native adapters and probes are isolated in this Worker.", new Dictionary<string, string> { ["execution"] = "worker", ["backends"] = "deploysharp.backend.llamasharp,deploysharp.backend.tensorrt,deploysharp.backend.opencv,deploysharp.backend.openvino", ["inference"] = "native-adapter", ["cancel"] = "active-operation", ["probe"] = "filesystem-preflight" });
            break;
        case WorkerMessageKind.Probe:
            WorkerProbeResult probe = BackendRuntimeProbeCatalog.Probe(request.BackendId);
            response = new WorkerResponse(WorkerResponseKind.Probe, request.RequestId, probe.Succeeded, probe.Message, probe.Payload);
            break;
        case WorkerMessageKind.Inference:
            if (activeTask != null)
            {
                response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "A native Worker operation is already active.", new Dictionary<string, string> { ["diagnosticCode"] = "DSAPP-WORKER-BUSY", ["activeRequestId"] = activeRequestId ?? string.Empty });
                break;
            }
            activeCancellation = CreateOperationCancellation(request.Payload);
            activeRequestId = request.RequestId;
            activeTask = ExecuteInferenceAsync(request, activeCancellation.Token);
            continue;
        case WorkerMessageKind.Benchmark:
            WorkerProbeResult benchmarkProbe = BackendRuntimeProbeCatalog.Probe(request.BackendId);
            await WriteResponseAsync(Progress(request.RequestId, 0.35, "dispatch", "Benchmark request accepted."));
            await WriteResponseAsync(new WorkerResponse(WorkerResponseKind.Log, request.RequestId, true, benchmarkProbe.Message, WithLogLevel(benchmarkProbe.Payload)));
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "The selected native backend adapter cannot benchmark this request. " + benchmarkProbe.Message, benchmarkProbe.Payload);
            break;
        case WorkerMessageKind.Cancel:
            if (activeTask != null && activeCancellation != null)
            {
                activeCancellation.Cancel();
                response = new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, "Cancellation was requested for the active Worker operation.", new Dictionary<string, string> { ["diagnosticCode"] = "DSAPP-WORKER-CANCEL-REQUESTED", ["activeRequestId"] = activeRequestId ?? string.Empty });
            }
            else
            {
                response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "No matching Worker operation is active.", new Dictionary<string, string> { ["diagnosticCode"] = "DSAPP-WORKER-NO-ACTIVE-OPERATION" });
            }
            break;
        case WorkerMessageKind.Shutdown:
            activeCancellation?.Cancel();
            await WriteResponseAsync(new WorkerResponse(WorkerResponseKind.Shutdown, request.RequestId));
            if (activeTask != null) await Task.WhenAny(activeTask, Task.Delay(250));
            activeCancellation?.Dispose();
            return;
        default:
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "Unsupported BackendHost Worker message kind.", new Dictionary<string, string> { ["diagnosticCode"] = "DSAPP-WORKER-UNSUPPORTED-MESSAGE" });
            break;
    }
    await WriteResponseAsync(response);
}

activeCancellation?.Cancel();
if (activeTask != null) await Task.WhenAny(activeTask, Task.Delay(250));
activeCancellation?.Dispose();

async Task ExecuteInferenceAsync(WorkerRequest request, CancellationToken cancellationToken)
{
    WorkerResponse response;
    try
    {
        WorkerProbeResult inferenceProbe = BackendRuntimeProbeCatalog.Probe(request.BackendId);
        await WriteResponseAsync(Progress(request.RequestId, 0.35, "dispatch", "Worker request accepted."));
        if (inferenceProbe.Payload.TryGetValue("preflightState", out string? preflightState) && string.Equals(preflightState, AppRuntimeState.Available.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            response = await WorkerInferenceAdapter.RunAsync(request, value => WriteResponseAsync(Progress(request.RequestId, value, "inference", "Native Worker inference progress.")).GetAwaiter().GetResult(), cancellationToken);
        }
        else
        {
            await WriteResponseAsync(new WorkerResponse(WorkerResponseKind.Log, request.RequestId, true, inferenceProbe.Message, WithLogLevel(inferenceProbe.Payload)));
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "The selected native backend adapter cannot execute this request. " + inferenceProbe.Message, inferenceProbe.Payload);
        }
    }
    catch (Exception exception)
    {
        response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "The Worker operation failed outside the backend adapter.", new Dictionary<string, string> { ["state"] = AppRuntimeState.ProbeFailed.ToString(), ["diagnosticCode"] = "DSAPP-WORKER-OPERATION-FAILED", ["technicalDetail"] = exception.ToString() });
    }
    await WriteResponseAsync(response);
}

async Task WriteResponseAsync(WorkerResponse response)
{
    await outputGate.WaitAsync();
    try { Console.WriteLine(WorkerProtocol.SerializeResponse(response)); }
    finally { outputGate.Release(); }
}

static WorkerResponse Progress(string requestId, double value, string stage, string message)
    => new(WorkerResponseKind.Progress, requestId, true, message, new Dictionary<string, string> { ["value"] = value.ToString(CultureInfo.InvariantCulture), ["stage"] = stage });

static CancellationTokenSource CreateOperationCancellation(IReadOnlyDictionary<string, string> payload)
{
    var cancellation = new CancellationTokenSource();
    if (payload.TryGetValue("timeoutMs", out string? value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds) && milliseconds > 0 && milliseconds <= int.MaxValue)
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(milliseconds));
    return cancellation;
}

static IReadOnlyDictionary<string, string> WithLogLevel(IReadOnlyDictionary<string, string> payload)
{
    var result = new Dictionary<string, string>(payload, StringComparer.Ordinal) { ["level"] = "Warning" };
    return result;
}
