using System.Globalization;
using System.Diagnostics;
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
            response = new WorkerResponse(WorkerResponseKind.Capability, request.RequestId, true, "Capabilities are manifest-driven; native adapters and probes are isolated in this Worker.", new Dictionary<string, string> { ["execution"] = "worker", ["backends"] = "deploysharp.backend.onnxruntime,deploysharp.backend.llamasharp,deploysharp.backend.tensorrt,deploysharp.backend.opencv,deploysharp.backend.openvino", ["inference"] = "native-adapter", ["multimodal"] = "blip-caption,clip-image-embedding", ["cancel"] = "active-operation", ["probe"] = "filesystem-preflight,abi-smoke" });
            break;
        case WorkerMessageKind.Probe:
            using (NativeWorkerEnvironment.Apply(request.Payload))
            {
                WorkerProbeResult probe = BackendRuntimeProbeCatalog.Probe(request.BackendId, request.Payload);
                response = new WorkerResponse(WorkerResponseKind.Probe, request.RequestId, probe.Succeeded, probe.Message, probe.Payload);
            }
            break;
        case WorkerMessageKind.Inference:
            if (activeTask != null)
            {
                response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "A native Worker operation is already active.", new Dictionary<string, string> { ["diagnosticCode"] = "DSAPP-WORKER-BUSY", ["activeRequestId"] = activeRequestId ?? string.Empty });
                break;
            }
            activeCancellation = new CancellationTokenSource();
            activeRequestId = request.RequestId;
            activeTask = ExecuteInferenceAsync(request, activeCancellation);
            continue;
        case WorkerMessageKind.Benchmark:
            if (activeTask != null)
            {
                response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "A native Worker operation is already active.", new Dictionary<string, string> { ["diagnosticCode"] = "DSAPP-WORKER-BUSY", ["activeRequestId"] = activeRequestId ?? string.Empty });
                break;
            }
            activeCancellation = new CancellationTokenSource();
            activeRequestId = request.RequestId;
            activeTask = ExecuteBenchmarkAsync(request, activeCancellation);
            continue;
        case WorkerMessageKind.TensorRtBuild:
            if (activeTask != null)
            {
                response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "A native Worker operation is already active.", new Dictionary<string, string> { ["diagnosticCode"] = "DSAPP-WORKER-BUSY", ["activeRequestId"] = activeRequestId ?? string.Empty });
                break;
            }
            activeCancellation = new CancellationTokenSource();
            activeRequestId = request.RequestId;
            activeTask = ExecuteTensorRtBuildAsync(request, activeCancellation);
            continue;
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

async Task ExecuteInferenceAsync(WorkerRequest request, CancellationTokenSource operationCancellation)
{
    WorkerResponse response;
    using var timeoutCancellation = new CancellationTokenSource();
    try
    {
        using NativeWorkerEnvironment nativeEnvironment = NativeWorkerEnvironment.Apply(request.Payload);
        // Native provider/session construction can be synchronous; keep stdin responsive for cancel/timeout messages.
        await Task.Yield();
        WorkerProbeResult inferenceProbe = BackendRuntimeProbeCatalog.Probe(request.BackendId);
        await WriteResponseAsync(Progress(request.RequestId, 0.35, "dispatch", "Worker request accepted."));
        if (inferenceProbe.Payload.TryGetValue("preflightState", out string? preflightState) && string.Equals(preflightState, AppRuntimeState.Available.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            Task<WorkerResponse> inferenceTask = Task.Run(() => WorkerInferenceAdapter.RunAsync(
                request,
                value => WriteResponseAsync(Progress(request.RequestId, value, "inference", "Native Worker inference progress.")).GetAwaiter().GetResult(),
                delta => WriteResponseAsync(TextDelta(request.RequestId, delta)).GetAwaiter().GetResult(),
                operationCancellation.Token));
            Task timeoutTask = CreateTimeoutTask(request.Payload, timeoutCancellation.Token);
            if (timeoutTask != Task.CompletedTask)
            {
                Task completed = await Task.WhenAny(inferenceTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    operationCancellation.Cancel();
                    try { await Task.WhenAny(inferenceTask, Task.Delay(250)); } catch (OperationCanceledException) { }
                    response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "Native Worker inference timed out.", new Dictionary<string, string> { ["state"] = AppRuntimeState.Unavailable.ToString(), ["backendId"] = request.BackendId ?? string.Empty, ["diagnosticCode"] = "DSAPP-WORKER-TIMED-OUT", ["execution"] = "worker" });
                }
                else
                {
                    timeoutCancellation.Cancel();
                    response = await inferenceTask;
                }
            }
            else
            {
                response = await inferenceTask;
            }
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

async Task ExecuteBenchmarkAsync(WorkerRequest request, CancellationTokenSource operationCancellation)
{
    WorkerResponse response;
    using var timeoutCancellation = new CancellationTokenSource();
    using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(operationCancellation.Token, timeoutCancellation.Token);
    try
    {
        using NativeWorkerEnvironment nativeEnvironment = NativeWorkerEnvironment.Apply(request.Payload);
        await Task.Yield();
        WorkerProbeResult probe = BackendRuntimeProbeCatalog.Probe(request.BackendId);
        await WriteResponseAsync(Progress(request.RequestId, 0.3, "dispatch", "Native benchmark request accepted."));
        if (!probe.Payload.TryGetValue("preflightState", out string? preflightState) || !string.Equals(preflightState, AppRuntimeState.Available.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(new WorkerResponse(WorkerResponseKind.Log, request.RequestId, true, probe.Message, WithLogLevel(probe.Payload)));
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "The selected native backend is unavailable for benchmark. " + probe.Message, probe.Payload);
        }
        else
        {
            Task timeoutTask = CreateTimeoutTask(request.Payload, timeoutCancellation.Token);
            if (timeoutTask != Task.CompletedTask)
            {
                _ = timeoutTask.ContinueWith(_ => operationCancellation.Cancel(), TaskScheduler.Default);
            }
            response = await Task.Run(() => WorkerInferenceAdapter.BenchmarkAsync(request, value => WriteResponseAsync(Progress(request.RequestId, value, "benchmark", "Native benchmark progress.")).GetAwaiter().GetResult(), linkedCancellation.Token), linkedCancellation.Token);
            timeoutCancellation.Cancel();
        }
    }
    catch (OperationCanceledException)
    {
        response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "Native Worker benchmark was cancelled or timed out.", new Dictionary<string, string> { ["state"] = AppRuntimeState.Unavailable.ToString(), ["backendId"] = request.BackendId ?? string.Empty, ["diagnosticCode"] = operationCancellation.IsCancellationRequested ? "DSAPP-WORKER-CANCELLED" : "DSAPP-WORKER-TIMED-OUT", ["execution"] = "worker" });
    }
    catch (Exception exception)
    {
        response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "Native Worker benchmark failed before producing a report.", new Dictionary<string, string> { ["state"] = AppRuntimeState.Unavailable.ToString(), ["backendId"] = request.BackendId ?? string.Empty, ["diagnosticCode"] = "DSAPP-WORKER-BENCHMARK-FAILED", ["technicalDetail"] = exception.GetType().FullName + ": " + exception.Message, ["execution"] = "worker" });
    }
    await WriteResponseAsync(response);
}

async Task ExecuteTensorRtBuildAsync(WorkerRequest request, CancellationTokenSource operationCancellation)
{
    WorkerResponse response;
    using var timeoutCancellation = new CancellationTokenSource();
    using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(operationCancellation.Token, timeoutCancellation.Token);
    bool timedOut = false;
    try
    {
        using NativeWorkerEnvironment nativeEnvironment = NativeWorkerEnvironment.Apply(request.Payload);
        await Task.Yield();
        await WriteResponseAsync(Progress(request.RequestId, 0.25, "dispatch", "TensorRT engine preparation request accepted."));
        string? modelPath = Value(request.Payload, "modelPath") ?? Value(request.Payload, "sourceOnnxPath");
        string format = Value(request.Payload, "modelFormat") ?? (modelPath is null ? string.Empty : Path.GetExtension(modelPath).TrimStart('.'));
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            response = TensorRtBuildError(request, "DSAPP-TENSORRT-ONNX-NOT-FOUND", "The ONNX source file does not exist; TensorRT conversion was not attempted.", AppRuntimeState.Unavailable, modelPath);
        }
        else if (!string.Equals(format, "onnx", StringComparison.OrdinalIgnoreCase))
        {
            response = TensorRtBuildError(request, "DSAPP-TENSORRT-BUILD-FORMAT-INVALID", "TensorRT engine preparation accepts an ONNX source only; choose an .onnx file or run an existing .engine/.plan artifact directly.", AppRuntimeState.Unsupported, format);
        }
        else
        {
            Task<TensorRtPreparedEngine> buildTask = Task.Run(
                () => TensorRtOnnxEngineAdapter.ResolveOrBuild(
                    request,
                    Path.GetFullPath(modelPath),
                    value => WriteResponseAsync(Progress(request.RequestId, value, "tensorrt-build", "TensorRT ONNX-to-engine conversion progress.")).GetAwaiter().GetResult(),
                    linkedCancellation.Token),
                linkedCancellation.Token);
            Task timeoutTask = CreateTimeoutTask(request.Payload, timeoutCancellation.Token);
            if (timeoutTask == Task.CompletedTask)
            {
                TensorRtPreparedEngine prepared = await buildTask.ConfigureAwait(false);
                response = TensorRtBuildResponse(request, prepared);
            }
            else
            {
                Task completed = await Task.WhenAny(buildTask, timeoutTask).ConfigureAwait(false);
                if (completed == timeoutTask)
                {
                    timedOut = true;
                    operationCancellation.Cancel();
                    await Task.WhenAny(buildTask, Task.Delay(250)).ConfigureAwait(false);
                    response = TensorRtBuildError(request, "DSAPP-WORKER-TIMED-OUT", "TensorRT engine preparation timed out.", AppRuntimeState.Unavailable, modelPath);
                }
                else
                {
                    timeoutCancellation.Cancel();
                    TensorRtPreparedEngine prepared = await buildTask.ConfigureAwait(false);
                    response = TensorRtBuildResponse(request, prepared);
                }
            }
        }
    }
    catch (OperationCanceledException)
    {
        response = TensorRtBuildError(request, timedOut ? "DSAPP-WORKER-TIMED-OUT" : "DSAPP-WORKER-CANCELLED", timedOut ? "TensorRT engine preparation timed out." : "TensorRT engine preparation was cancelled.", AppRuntimeState.Unavailable, null);
    }
    catch (Exception exception)
    {
        response = TensorRtBuildError(request, "DSAPP-TENSORRT-BUILD-FAILED", "TensorRT engine preparation failed before producing an engine.", AppRuntimeState.ProbeFailed, exception.GetType().FullName + ": " + exception.Message);
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

static WorkerResponse TextDelta(string requestId, string delta)
    => new(WorkerResponseKind.Progress, requestId, true, "Streaming text delta.", new Dictionary<string, string> { ["stage"] = "generation", ["textDelta"] = delta });

static WorkerResponse TensorRtBuildResponse(WorkerRequest request, TensorRtPreparedEngine prepared)
{
    var payload = new Dictionary<string, string>(prepared.Details, StringComparer.Ordinal)
    {
        ["backendId"] = request.BackendId ?? "deploysharp.backend.tensorrt",
        ["execution"] = "worker",
        ["diagnosticCode"] = prepared.Code,
        ["engineBuildState"] = prepared.Built ? "built" : "cache-hit",
        ["enginePath"] = prepared.EnginePath ?? string.Empty,
        ["engineIdentityPath"] = prepared.IdentityPath ?? string.Empty,
        ["engineSha256"] = prepared.EngineSha256 ?? string.Empty
    };
    if (prepared.Succeeded) return new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, prepared.Message, payload);
    payload["state"] = prepared.Code.IndexOf("NATIVE", StringComparison.OrdinalIgnoreCase) >= 0 || prepared.Code.IndexOf("RUNTIME", StringComparison.OrdinalIgnoreCase) >= 0
        ? AppRuntimeState.MissingNative.ToString()
        : AppRuntimeState.Unavailable.ToString();
    return new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, prepared.Message, payload);
}

static WorkerResponse TensorRtBuildError(WorkerRequest request, string diagnosticCode, string message, AppRuntimeState state, string? detail)
{
    var payload = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["state"] = state.ToString(),
        ["backendId"] = request.BackendId ?? "deploysharp.backend.tensorrt",
        ["diagnosticCode"] = diagnosticCode,
        ["execution"] = "worker"
    };
    if (!string.IsNullOrWhiteSpace(detail)) payload["detail"] = detail!;
    return new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, message, payload);
}

static string? Value(IReadOnlyDictionary<string, string> payload, string key)
    => payload.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

static Task CreateTimeoutTask(IReadOnlyDictionary<string, string> payload, CancellationToken cancellationToken)
{
    if (payload.TryGetValue("timeoutMs", out string? value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds) && milliseconds > 0 && milliseconds <= int.MaxValue)
        return Task.Delay(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);
    return Task.CompletedTask;
}

static IReadOnlyDictionary<string, string> WithLogLevel(IReadOnlyDictionary<string, string> payload)
{
    var result = new Dictionary<string, string>(payload, StringComparer.Ordinal) { ["level"] = "Warning" };
    return result;
}
