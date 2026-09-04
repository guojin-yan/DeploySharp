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
            response = new WorkerResponse(WorkerResponseKind.Handshake, request.RequestId, true, "DeploySharpApp BackendHost ready", new Dictionary<string, string> { ["protocolVersion"] = WorkerProtocol.ProtocolVersion.ToString() });
            break;
        case WorkerMessageKind.Capability:
            response = new WorkerResponse(WorkerResponseKind.Capability, request.RequestId, true, "Capabilities are manifest-driven", new Dictionary<string, string> { ["execution"] = "worker" });
            break;
        case WorkerMessageKind.Probe:
            response = new WorkerResponse(WorkerResponseKind.Probe, request.RequestId, false, "No native backend is loaded by the minimal host.", new Dictionary<string, string> { ["state"] = AppRuntimeState.Unavailable.ToString(), ["reason"] = "probe adapter not installed" });
            break;
        case WorkerMessageKind.Shutdown:
            Console.WriteLine(WorkerProtocol.SerializeResponse(new WorkerResponse(WorkerResponseKind.Shutdown, request.RequestId)));
            return;
        default:
            response = new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, "The minimal BackendHost only implements handshake, capability, probe and shutdown.");
            break;
    }
    Console.WriteLine(WorkerProtocol.SerializeResponse(response));
}
