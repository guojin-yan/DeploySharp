using System.Text.Json;
using DeploySharpApp.Contracts;
using DeploySharpApp.Engine;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Registry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharpApp.Engine.Tests;

[TestClass]
public sealed class EngineTests
{
    [TestMethod]
    public void OnnxRuntimeDescriptorCanBeCreated()
    {
        using var provider = new OnnxRuntimeBackendProvider();
        Assert.AreEqual("onnxruntime", provider.Descriptor.Id.Value);
        Assert.IsTrue(provider.Descriptor.Supports(BackendCapabilities.TensorInference));
        CollectionAssert.Contains(provider.Descriptor.SupportedFormats.ToList(), "onnx");
    }

    [TestMethod]
    public void ExplicitRegistryExtensionStillRegistersOnnxRuntime()
    {
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        Assert.AreEqual("onnxruntime", registry.GetDescriptors().Single().Id.Value);
    }

    [TestMethod]
    public async Task MissingModelPathReturnsStructuredModelUnavailable()
    {
        var engine = new DeploySharpEngine(new FixedRuntimeProbe(AvailableStatus()));
        ModelRunResult result = await engine.RunAsync(Request(Path.Combine(AppContext.BaseDirectory, "missing.onnx")), null, CancellationToken.None);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(AppErrorCode.ModelUnavailable, result.ErrorCode);
        Assert.AreEqual(ModelRunMode.RealOnnxRuntime, result.RunMode);
        Assert.AreEqual("DSAPP-MODEL-NOT-FOUND", result.Diagnostics.Single().Code);
    }

    [TestMethod]
    public async Task CudaRequestNeverFallsBackToCpu()
    {
        var engine = new DeploySharpEngine(new FixedRuntimeProbe(AvailableStatus()));
        var request = new ModelRunRequest(AppOperationKind.Vision, "tests/cuda", "deploysharp.backend.onnxruntime", "cuda", modelPath: Fixture(), modelFormat: "onnx", tensorInputs: Inputs());
        ModelRunResult result = await engine.RunAsync(request, null, CancellationToken.None);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(AppErrorCode.BackendUnavailable, result.ErrorCode);
        Assert.AreEqual(AppRuntimeState.Unavailable, result.RuntimeStatus?.State);
        CollectionAssert.Contains(result.RuntimeStatus!.MissingItems.ToList(), "onnxruntime-cuda-provider");
    }

    [TestMethod]
    public async Task MissingNativeRuntimeIsStructured()
    {
        var status = new BackendRuntimeStatus(
            "deploysharp.backend.onnxruntime",
            AppRuntimeState.MissingNative,
            "native missing",
            missingItems: new[] { "onnxruntime-native" },
            suggestedAction: "Install the official runtime.",
            details: new Dictionary<string, string> { ["probe.path.0"] = Path.Combine(AppContext.BaseDirectory, "onnxruntime.dll") },
            diagnostics: new[] { new RuntimeDiagnostic("DSAPP-ORT-NATIVE-MISSING", DiagnosticSeverity.Warning, "native missing", "deploysharp.backend.onnxruntime") });
        var engine = new DeploySharpEngine(new FixedRuntimeProbe(status));
        ModelRunResult result = await engine.RunAsync(Request(Fixture()), null, CancellationToken.None);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(AppErrorCode.NativeDependencyMissing, result.ErrorCode);
        Assert.AreEqual(AppRuntimeState.MissingNative, result.RuntimeStatus?.State);
        Assert.IsTrue(result.RuntimeStatus!.Details.ContainsKey("probe.path.0"));
    }

    [TestMethod]
    public async Task RealCpuInferenceRunsWhenNativeRuntimeIsAvailable()
    {
        var engine = new DeploySharpEngine();
        ModelRunResult result = await engine.RunAsync(Request(Fixture()), null, CancellationToken.None);
        if (result.ErrorCode == AppErrorCode.NativeDependencyMissing || result.ErrorCode == AppErrorCode.BackendUnavailable)
        {
            Assert.Inconclusive("ONNX Runtime native execution is unavailable: " + result.Message);
        }

        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.AreEqual(ModelRunMode.RealOnnxRuntime, result.RunMode);
        Assert.AreEqual(AppRuntimeState.Available, result.RuntimeStatus?.State);
        using JsonDocument output = JsonDocument.Parse(result.Output!);
        JsonElement values = output.RootElement.GetProperty("outputs")[0].GetProperty("values");
        CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, values.EnumerateArray().Select(item => item.GetSingle()).ToArray());
    }

    [TestMethod]
    public async Task ImageInputIsDecodedIntoTheRequestedTensor()
    {
        string imagePath = Path.Combine(Path.GetTempPath(), "deploysharp-app-test-" + Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllBytesAsync(imagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        try
        {
            var request = new ModelRunRequest(
                AppOperationKind.Vision,
                "tests/image-input",
                "deploysharp.backend.onnxruntime",
                inputPath: imagePath,
                modelPath: Fixture(),
                modelFormat: "onnx",
                tensorInputs: new[] { new ModelTensorInput("images", "float32", new long[] { 1, 3, 2, 2 }, imageInput: true) });
            ModelRunResult result = await new DeploySharpEngine(new FixedRuntimeProbe(AvailableStatus())).RunAsync(request, null, CancellationToken.None);
            if (result.ErrorCode == AppErrorCode.NativeDependencyMissing || result.ErrorCode == AppErrorCode.BackendUnavailable) Assert.Inconclusive(result.Message);
            Assert.IsTrue(result.Succeeded, result.Message);
        }
        finally { File.Delete(imagePath); }
    }

    private static ModelRunRequest Request(string path)
    {
        return new ModelRunRequest(AppOperationKind.Vision, "tests/classification", "deploysharp.backend.onnxruntime", modelPath: path, modelFormat: "onnx", tensorInputs: Inputs());
    }

    private static ModelTensorInput[] Inputs()
    {
        return new[]
        {
            new ModelTensorInput("images", "float32", new long[] { 1, 3, 2, 2 }, valuesJson: "[1,1,1,1,2,2,2,2,3,3,3,3]")
        };
    }

    private static string Fixture() => Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");

    private static BackendRuntimeStatus AvailableStatus()
    {
        return new BackendRuntimeStatus("deploysharp.backend.onnxruntime", AppRuntimeState.Available, "candidate found", devices: new[] { "cpu" });
    }

    private sealed class FixedRuntimeProbe : IOnnxRuntimeAvailabilityProbe
    {
        private readonly BackendRuntimeStatus _status;
        public FixedRuntimeProbe(BackendRuntimeStatus status) => _status = status;
        public Task<BackendRuntimeStatus> ProbeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_status);
        }
    }
}
