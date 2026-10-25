using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests;

[TestClass]
public sealed class RunConfigurationTests
{
    [TestMethod]
    public void RunEntriesAreOptInImmutableCopiesThatPreserveSessionOptions()
    {
        var source = new OnnxRuntimeOptions(intraOpThreads: 2, interOpThreads: 1, enableMemoryPattern: false,
            cudaProviderOptions: new Dictionary<string, string> { ["use_tf32"] = "0" },
            sessionConfigEntries: new Dictionary<string, string> { ["session.logid"] = "test" },
            preferPreallocatedCpuOutputs: true);
        var entries = new Dictionary<string, string> { ["memory.enable_memory_arena_shrinkage"] = "cpu:0" };
        var copy = source.WithRunConfigEntries(entries);
        entries["memory.enable_memory_arena_shrinkage"] = "invalid:0";
        Assert.AreEqual(0, source.RunConfigEntries.Count);
        Assert.AreEqual("cpu:0", copy.RunConfigEntries["memory.enable_memory_arena_shrinkage"]);
        Assert.AreEqual(2, copy.IntraOpThreads);
        Assert.AreEqual(1, copy.InterOpThreads);
        Assert.IsFalse(copy.EnableMemoryPattern);
        Assert.IsTrue(copy.PreferPreallocatedCpuOutputs);
        Assert.AreEqual("0", copy.CudaProviderOptions["use_tf32"]);
        Assert.AreEqual("test", copy.SessionConfigEntries["session.logid"]);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IDictionary<string, string>)copy.RunConfigEntries).Clear());
        Assert.ThrowsExactly<ArgumentException>(() => source.WithRunConfigEntries(new Dictionary<string, string> { [" "] = "x" }));
        Assert.ThrowsExactly<ArgumentException>(() => source.WithRunConfigEntries(new Dictionary<string, string> { ["x"] = null! }));
        Assert.AreEqual(0, copy.WithRunConfigEntries(null).RunConfigEntries.Count);
    }

    [TestMethod]
    public async Task CpuArenaShrinkConfigurationExecutesOnSyncAndAsyncRuns()
    {
        var options = new OnnxRuntimeOptions(intraOpThreads: 1).WithRunConfigEntries(
            new Dictionary<string, string> { ["memory.enable_memory_arena_shrinkage"] = "cpu:0" });
        using var provider = new OnnxRuntimeBackendProvider(options);
        using var session = provider.CreateSession(OnnxRuntimeTestData.Artifact("classification.onnx"),
            new BackendRequest(BackendCapabilities.TensorInference, device: "cpu"), new SessionOptions());
        for (int i = 0; i < 4; i++)
        {
            var outputs = i % 2 == 0 ? session.Run(OnnxRuntimeTestData.ClassificationInputs(), CancellationToken.None)
                : await session.RunAsync(OnnxRuntimeTestData.ClassificationInputs(), CancellationToken.None);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, (float[])outputs.GetRequired("scores").Buffer);
        }
    }

    [TestMethod]
    public void NativeRunConfigurationErrorsAreMappedAndReleaseTheSessionGate()
    {
        using var provider = new OnnxRuntimeBackendProvider(new OnnxRuntimeOptions(intraOpThreads: 1).WithRunConfigEntries(
            new Dictionary<string, string> { ["memory.enable_memory_arena_shrinkage"] = "invalid:0" }));
        using var session = provider.CreateSession(OnnxRuntimeTestData.Artifact("classification.onnx"),
            new BackendRequest(BackendCapabilities.TensorInference, device: "cpu"), new SessionOptions());
        for (int i = 0; i < 2; i++)
        {
            var error = Assert.ThrowsExactly<OnnxRuntimeBackendException>(() => session.Run(OnnxRuntimeTestData.ClassificationInputs(), CancellationToken.None));
            Assert.IsNotNull(error.InnerException);
        }
    }
}
