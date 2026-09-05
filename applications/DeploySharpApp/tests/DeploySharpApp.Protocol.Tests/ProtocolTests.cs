using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DeploySharpApp.BackendHost.Protocol;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Protocol.Tests
{
    [TestClass]
    public class ProtocolTests
    {
        [TestMethod]
        public void JsonLinesRoundTripPreservesKindAndPayload()
        {
            var request = new WorkerRequest(WorkerMessageKind.Handshake, "req-1", payload: new Dictionary<string, string> { ["protocolVersion"] = "1" });
            var copy = WorkerProtocol.DeserializeRequest(WorkerProtocol.SerializeRequest(request));
            Assert.AreEqual(WorkerMessageKind.Handshake, copy.Kind);
            Assert.AreEqual("1", copy.Payload["protocolVersion"]);
        }

        [TestMethod]
        public void InvalidLineIsRejected()
        {
            Assert.ThrowsExactly<System.FormatException>(() => WorkerProtocol.DeserializeResponse("not-json"));
        }

        [TestMethod]
        public void ProgressAndLogEventsRoundTripPreservesPayload()
        {
            var progress = new WorkerResponse(WorkerResponseKind.Progress, "run-1", payload: new Dictionary<string, string> { ["value"] = "0.35", ["stage"] = "dispatch" });
            var log = new WorkerResponse(WorkerResponseKind.Log, "run-1", true, "adapter probe started", new Dictionary<string, string> { ["level"] = "Warning", ["diagnosticCode"] = "DSAPP-WORKER-ADAPTER-UNAVAILABLE" });

            var progressCopy = WorkerProtocol.DeserializeResponse(WorkerProtocol.SerializeResponse(progress));
            var logCopy = WorkerProtocol.DeserializeResponse(WorkerProtocol.SerializeResponse(log));

            Assert.AreEqual(WorkerResponseKind.Progress, progressCopy.Kind);
            Assert.AreEqual("0.35", progressCopy.Payload["value"]);
            Assert.AreEqual("dispatch", progressCopy.Payload["stage"]);
            Assert.AreEqual(WorkerResponseKind.Log, logCopy.Kind);
            Assert.AreEqual("Warning", logCopy.Payload["level"]);
            Assert.AreEqual("DSAPP-WORKER-ADAPTER-UNAVAILABLE", logCopy.Payload["diagnosticCode"]);
        }
    }
}
