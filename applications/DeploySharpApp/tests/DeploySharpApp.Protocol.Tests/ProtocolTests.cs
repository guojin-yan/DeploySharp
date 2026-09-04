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
    }
}
