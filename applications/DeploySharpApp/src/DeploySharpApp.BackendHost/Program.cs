using System.Text.Json;
using DeploySharpApp.BackendHost;
using DeploySharpApp.BackendHost.Protocol;
using DeploySharpApp.Contracts;

Console.Error.WriteLine("DeploySharpApp BackendHost protocol v" + WorkerProtocol.ProtocolVersion);
while (true)
{
    var line = await Console.In.ReadLineAsync();
    if (line == null) break;
    WorkerRequest request;
    try { request = WorkerProtocol.DeserializeRequest(line); }
    catch (Exception ex)
    {
        Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Error, "unknown", false, ex.Message)));
        continue;
    }

    WorkerResponse response;
    switch (request.Kind)
    {
        case WorkerMessageKind.Handshake:
            string protocolVersion = request.Payload.TryGetValue("protocolVersion", out var requestedVersion) ? requestedVersion : string.Empty;
            bool protocolCompatible = string.Equals(protocolVersion, WorkerProtocol.ProtocolVersion.ToString(), StringComparison.Ordinal);
            response = new WorkerResponse(WorkerResponseKind.Handshake, request.RequestId, protocolCompatible, protocolCompatible ? "DeploySharpApp BackendHost ready" : "Worker protocol version mismatch.", new Dictionary<string, string> { ["protocolVersion"] = WorkerProtocol.ProtocolVersion.ToString(), ["execution"] = "worker", ["host"] = "DeploySharpApp.BackendHost" });
            break;
        case WorkerMessageKind.Capability:
            response = new WorkerResponse(WorkerResponseKind.Capability, request.RequestId, true, "Capabilities are manifest-driven; native adapters and probes are isolated in this Worker.", new Dictionary<string, string> { ["execution"] = "worker", ["backends"] = "deploysharp.backend.llamasharp,deploysharp.backend.tensorrt,deploysharp.backend.opencv,deploysharp.backend.openvino", ["inference"] = "adapter-required", ["probe"] = "filesystem-preflight" });
            break;
        case WorkerMessageKind.Probe:
            WorkerProbeResult probe = BackendRuntimeProbeCatalog.Probe(request.BackendId);
            response = new WorkerResponse(WorkerResponseKind.Probe, request.RequestId, probe.Succeeded, probe.Message, probe.Payload);
            break;
        case WorkerMessageKind.Inference:
            WorkerProbeResult inferenceProbe = BackendRuntimeProbeCatalog.Probe(request.BackendId);
            Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Progress, request.RequestId, true, "Worker request accepted.", new Dictionary<string, string> { ["value"] = "0.35", ["stage"] = "dispatch" })));
            if (inferenceProbe.Payload.TryGetValue("preflightState", out string? preflightState) && string.Equals(preflightState, AppRuntimeState.Available.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                WorkerResponse execution = await WorkerInferenceAdapter.RunAsync(request, value => Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Progress, request.RequestId, true, "Native Worker inference progress.", new Dictionary<string, string> { ["value"] = value.ToString(System.Globalization.CultureInfo.InvariantCulture), ["stage"] = "inference" }))), CancellationToken.None);
                response = execution;
            }
            else
            {
                Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Log, request.RequestId, true, inferenceProbe.Message, WithLogLevel(inferenceProbe.Payload))));
                response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "The selected native backend adapter cannot execute this request. " + inferenceProbe.Message, inferenceProbe.Payload);
            }
            break;
        case WorkerMessageKind.Benchmark:
            WorkerProbeResult benchmarkProbe = BackendRuntimeProbeCatalog.Probe(request.BackendId);
            Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Progress, request.RequestId, true, "Benchmark request accepted.", new Dictionary<string, string> { ["value"] = "0.35", ["stage"] = "dispatch" })));
            Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Log, request.RequestId, true, benchmarkProbe.Message, WithLogLevel(benchmarkProbe.Payload))));
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "The selected native backend adapter cannot benchmark this request. " + benchmarkProbe.Message, benchmarkProbe.Payload);
            break;
        case WorkerMessageKind.Cancel:
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "No matching Worker operation is active.", new Dictionary<string, string> { ["diagnosticCode"] = "DSAPP-WORKER-NO-ACTIVE-OPERATION" });
            break;
        case WorkerMessageKind.Shutdown:
            Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Shutdown, request.RequestId)));
            return;
        default:
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "Unsupported BackendHost Worker message kind.", new Dictionary<string, string> { ["diagnosticCode"] = "DSAPP-WORKER-UNSUPPORTED-MESSAGE" });
            break;
    }
    Console.WriteLine(WorkerProtocol.SerializeResponse(response));
}

static IReadOnlyDictionary<string, string> WithLogLevel(IReadOnlyDictionary<string, string> payload)
{
    var result = new Dictionary<string, string>(payload, StringComparer.Ordinal) { ["level"] = "Warning" };
    return result;
}
