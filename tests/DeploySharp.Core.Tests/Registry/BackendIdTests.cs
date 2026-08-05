using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.DeploySharp.Core.Tests.Registry
{
    [TestClass]
    public sealed class BackendIdTests
    {
        [TestMethod]
        public void StableLowercaseIdentifierIsAccepted()
        {
            var id = new BackendId("onnx-runtime/cuda");

            Assert.AreEqual("onnx-runtime/cuda", id.Value);
        }

        [TestMethod]
        public void UppercaseIdentifierIsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new BackendId("OpenVINO"));
        }
    }
}
