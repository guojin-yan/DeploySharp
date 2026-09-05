using System.Text.Json;
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
            response = new WorkerResponse(WorkerResponseKind.Capability, request.RequestId, true, "Capabilities are manifest-driven; native adapters are loaded only when installed in this Worker.", new Dictionary<string, string> { ["execution"] = "worker", ["backends"] = "deploysharp.backend.llamasharp,deploysharp.backend.tensorrt", ["inference"] = "adapter-required" });
            break;
        case WorkerMessageKind.Probe:
            response = new WorkerResponse(WorkerResponseKind.Probe, request.RequestId, false, "No native backend is loaded by BackendHost; install the selected adapter before probing.", new Dictionary<string, string> { ["state"] = AppRuntimeState.Unavailable.ToString(), ["reason"] = "native adapter not installed", ["backendId"] = request.BackendId ?? string.Empty, ["diagnosticCode"] = "DSAPP-WORKER-ADAPTER-UNAVAILABLE" });
            break;
        case WorkerMessageKind.Inference:
            Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Progress, request.RequestId, true, "Worker request accepted.", new Dictionary<string, string> { ["value"] = "0.35", ["stage"] = "dispatch" })));
            Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Log, request.RequestId, true, "No native adapter was selected for this Worker operation.", new Dictionary<string, string> { ["level"] = "Warning", ["diagnosticCode"] = "DSAPP-WORKER-ADAPTER-UNAVAILABLE" })));
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "The BackendHost Worker is reachable, but the selected native backend adapter is not installed.", new Dictionary<string, string> { ["state"] = AppRuntimeState.Unavailable.ToString(), ["backendId"] = request.BackendId ?? string.Empty, ["diagnosticCode"] = "DSAPP-WORKER-ADAPTER-UNAVAILABLE", ["action"] = "Install and probe the backend adapter in this Worker environment." });
            break;
        case WorkerMessageKind.Benchmark:
            Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Progress, request.RequestId, true, "Benchmark request accepted.", new Dictionary<string, string> { ["value"] = "0.35", ["stage"] = "dispatch" })));
            Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Log, request.RequestId, true, "Benchmark adapter is not installed in this Worker.", new Dictionary<string, string> { ["level"] = "Warning", ["diagnosticCode"] = "DSAPP-WORKER-BENCHMARK-UNAVAILABLE" })));
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "The BackendHost Worker is reachable, but the selected native backend adapter is not installed.", new Dictionary<string, string> { ["state"] = AppRuntimeState.Unavailable.ToString(), ["backendId"] = request.BackendId ?? string.Empty, ["diagnosticCode"] = "DSAPP-WORKER-ADAPTER-UNAVAILABLE", ["action"] = "Install and probe the backend adapter in this Worker environment." });
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
