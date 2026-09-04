using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DeploySharpApp.Plugin.Abstractions;

namespace DeploySharpApp.Plugin.Tests
{
    [TestClass]
    public class ManifestTests
    {
        [TestMethod]
        public void ParsesRequiredManifestAndMapsCapability()
        {
            var manifest = PluginManifestParser.Parse("{\"schemaVersion\":1,\"pluginId\":\"demo.backend\",\"displayName\":\"Demo\",\"version\":\"1.0.0\",\"packageId\":\"Demo.Package\",\"targetFrameworks\":[\"net10.0\"],\"runtimeIdentifiers\":[\"win-x64\"],\"execution\":\"worker\",\"capabilities\":[\"tensor-inference\"],\"formats\":[\"onnx\"]}");
            Assert.AreEqual("demo.backend", manifest.PluginId);
            Assert.IsTrue(manifest.ToBackendInfo().Capabilities.HasFlag(DeploySharpApp.Contracts.AppBackendCapability.Vision));
            Assert.AreEqual(DeploySharpApp.Contracts.AppExecutionMode.Worker, manifest.ToBackendInfo().ExecutionMode);
        }

        [TestMethod]
        public void RejectsUnknownSchemaAndDuplicateRuntimeDependency()
        {
            Assert.ThrowsExactly<FormatException>(() => PluginManifestParser.Parse("{\"schemaVersion\":2}"));
            var json = "{\"schemaVersion\":1,\"pluginId\":\"demo.backend\",\"displayName\":\"Demo\",\"version\":\"1.0.0\",\"packageId\":\"Demo.Package\",\"targetFrameworks\":[\"net10.0\"],\"runtimeIdentifiers\":[\"win-x64\"],\"capabilities\":[\"vision\"],\"formats\":[\"onnx\"],\"runtimeDependencies\":[{\"kind\":\"managed\",\"packageId\":\"A\",\"version\":\"1\"},{\"kind\":\"managed\",\"packageId\":\"A\",\"version\":\"1\"}]}";
            Assert.ThrowsExactly<FormatException>(() => PluginManifestParser.Parse(json));
        }
    }
}
