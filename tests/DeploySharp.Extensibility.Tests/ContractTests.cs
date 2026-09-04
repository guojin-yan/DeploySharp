using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Diagnostics;
using JYPPX.DeploySharp.Extensibility;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;

namespace DeploySharp.Extensibility.Tests
{
    [TestClass]
    public sealed class ContractTests
    {
        [TestMethod]
        public void DependencyRejectsInvalidAndDuplicateValues()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, packageId: "pkg", packageVersion: "bad version"));
            Assert.ThrowsExactly<ArgumentException>(() => new BackendRuntimeDependency(BackendRuntimeDependencyKind.Environment, environmentVariables: new[] { "PATH", "PATH" }));
            Assert.ThrowsExactly<ArgumentException>(() => new BackendOptionsSchema("schema", new[]
            {
                new BackendOptionDefinition("device", BackendOptionValueType.String),
                new BackendOptionDefinition("device", BackendOptionValueType.Boolean)
            }));
        }

        [TestMethod]
        public void SchemaAndStatusAreDefensiveAndStable()
        {
            var options = new List<BackendOptionDefinition> { new BackendOptionDefinition("device", BackendOptionValueType.Enum, "cpu", enumValues: new[] { "cpu", "cuda" }) };
            var schema = new BackendOptionsSchema("backend.options.v1", options);
            options.Clear();
            Assert.AreEqual(1, schema.Options.Count);

            var details = new Dictionary<string, string> { ["loaded"] = "native.dll" };
            var status = new BackendRuntimeStatus(BackendRuntimeState.Available, loadedPath: "C:\\runtime\\native.dll", version: "1.0", runtimeIdentifier: "win-x64", processArchitecture: "x64", details: details);
            details["mutated"] = "true";
            Assert.AreEqual(1, status.Details.Count);
            Assert.IsTrue(status.IsAvailable);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BackendRuntimeStatus((BackendRuntimeState)999));
        }

        [TestMethod]
        public void PluginDescriptorProvidesDeterministicContractSnapshot()
        {
            var backend = new BackendDescriptor(new BackendId("test"), "Test", "1.0.0", BackendCapabilities.TensorInference, new[] { "onnx" });
            var descriptor = new BackendPluginDescriptor("test.plugin", "Test", "1.0.0", backend, targetFrameworks: new[] { "net10.0" }, runtimeIdentifiers: new[] { "win-x64" }, formats: new[] { "onnx" });
            Assert.AreEqual("test.plugin", descriptor.PluginId);
            Assert.AreEqual("onnx", descriptor.Formats.Single());
            Assert.AreEqual(BackendCapabilities.TensorInference, descriptor.Capabilities);
        }

        [TestMethod]
        public void RuntimeDiagnosticValidatesCodeAndDetails()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new RuntimeDiagnostic("bad code", DiagnosticSeverity.Error, "Missing native runtime."));
            RuntimeDiagnostic diagnostic = new RuntimeDiagnostic("native-missing", DiagnosticSeverity.Error, "Missing native runtime.", backendId: new BackendId("onnxruntime"));
            Assert.AreEqual("native-missing", diagnostic.Code);
            Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        }

        [TestMethod]
        public void BackendDescriptorLegacyConstructorRemainsUsable()
        {
            var descriptor = new BackendDescriptor(new BackendId("legacy"), "Legacy", "1.0.0", BackendCapabilities.None);
            Assert.AreEqual("Legacy", descriptor.Description);
            Assert.AreEqual(BackendExecutionMode.InProcess, descriptor.PreferredExecutionMode);
            Assert.AreEqual(0, descriptor.RuntimeDependencies.Count);
        }

        [TestMethod]
        public void PluginCatalogAndRegistryAdapterKeepDefensiveAndLegacySemantics()
        {
            var backend = new BackendDescriptor(new BackendId("catalog-test"), "Catalog test", "1.0.0", BackendCapabilities.None);
            var descriptor = new BackendPluginDescriptor("catalog-test", "Catalog test", "1.0.0", backend);
            var catalog = new InMemoryBackendPluginCatalog();
            catalog.Add(descriptor);
            IReadOnlyList<BackendPluginDescriptor> snapshot = catalog.GetInstalled();
            Assert.AreEqual(1, snapshot.Count);
            Assert.ThrowsExactly<ArgumentException>(() => catalog.Add(descriptor));
            Assert.ThrowsExactly<OperationCanceledException>(() => catalog.RefreshAsync(new CancellationToken(true)));

            var factory = new TestPluginFactory(backend);
            using var registry = new BackendRegistry();
            registry.RegisterPlugin(factory);
            Assert.AreEqual("catalog-test", registry.GetDescriptors().Single().Id.Value);
            Assert.AreEqual(1, factory.CreateCount);
        }

        [TestMethod]
        public async Task FileSystemProbeReportsMissingNativeAndHonorsCancellation()
        {
            var backend = new BackendDescriptor(new BackendId("probe-test"), "Probe test", "1.0.0", BackendCapabilities.None);
            var descriptor = new BackendPluginDescriptor(
                "probe-test",
                "Probe test",
                "1.0.0",
                backend,
                targetFrameworks: new[] { "net10.0" },
                runtimeIdentifiers: new[] { "win-x64" },
                nativeRequirements: new[] { new NativeRuntimeRequirement(NativeRuntimeKind.OpenCV, environmentVariables: new[] { "DEPLOYSHARP_PROBE_ROOT" }) });
            var probe = new FileSystemBackendRuntimeProbe();
            BackendRuntimeStatus status = await probe.ProbeAsync(descriptor);
            Assert.AreEqual(BackendRuntimeState.MissingNative, status.State);
            CollectionAssert.Contains(status.MissingItems.ToArray(), "native.opencv");
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => probe.ProbeAsync(descriptor, new CancellationToken(true)));
        }

        private sealed class TestPluginFactory : IBackendPluginFactory
        {
            private readonly BackendDescriptor _backend;
            public TestPluginFactory(BackendDescriptor backend)
            {
                _backend = backend;
                Descriptor = new BackendPluginDescriptor("catalog-test", "Catalog test", "1.0.0", backend);
            }

            public int CreateCount { get; private set; }
            public BackendPluginDescriptor Descriptor { get; }
            public IDisposable Create(BackendPluginContext context)
            {
                CreateCount++;
                return new TestProvider(_backend);
            }
        }

        private sealed class TestProvider : IBackendProvider
        {
            public TestProvider(BackendDescriptor descriptor) { Descriptor = descriptor; }
            public BackendDescriptor Descriptor { get; }
            public bool CanCreate(ModelArtifact artifact, BackendRequest request) => false;
            public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options) => throw new NotSupportedException();
            public void Dispose() { }
        }

    }
}
