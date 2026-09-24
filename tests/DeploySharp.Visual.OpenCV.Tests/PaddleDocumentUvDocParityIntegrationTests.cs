using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentUvDocParityIntegrationTests
{
    private const string ModelPath = @"E:\Model\PaddleDocument\onnx\uvdoc.onnx";
    private const string ImagePath = @"E:\Data\image\bus.jpg";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void UvDocOrtAndOpenVinoMatchOnSamePreparedInput()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_UVDOC_PARITY") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_UVDOC_PARITY=1 to run UVDoc parity validation.");
        if (!File.Exists(ModelPath) || !File.Exists(ImagePath)) Assert.Inconclusive("Missing UVDoc model or image.");
        var descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/uvdoc");
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateUnwarping(descriptor, new VisualSize(640, 640));
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(ImagePath, profile.VisualProfile, "uvdoc-parity-" + Sha256(ImagePath));
        BackendRequest ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        BackendRequest ovRequest = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        using var ortRegistry = new BackendRegistry(); ortRegistry.UseOnnxRuntime();
        using var ovRegistry = new BackendRegistry(); ovRegistry.UseOpenVino();
        using IInferenceSession ort = ortRegistry.CreateSession(profile.CreateArtifact(ModelPath, OnnxRuntimeBackendProvider.BackendId), ortRequest);
        using IInferenceSession ov = ovRegistry.CreateSession(profile.CreateArtifact(ModelPath, OpenVinoBackendProvider.BackendId), ovRequest);
        InferenceInputs inputs = InferenceInputs.Create(input.InputName, input.Tensor);
        StopwatchScope ortWatch = new StopwatchScope();
        InferenceOutputs ortOutputs = ort.Run(inputs, CancellationToken.None); ortWatch.Stop();
        StopwatchScope ovWatch = new StopwatchScope();
        InferenceOutputs ovOutputs = ov.Run(inputs, CancellationToken.None); ovWatch.Stop();
        ITensor ortTensor = ortOutputs.GetRequired("fetch_name_0");
        ITensor ovTensor = ovOutputs.GetRequired("fetch_name_0");
        Assert.AreEqual(ortTensor.Shape, ovTensor.Shape);
        Assert.IsTrue(ortTensor.Buffer is float[] && ovTensor.Buffer is float[]);
        float[] ortValues = (float[])ortTensor.Buffer;
        float[] ovValues = (float[])ovTensor.Buffer;
        Assert.AreEqual(ortValues.Length, ovValues.Length);
        Stats ortStats = StatsOf(ortValues);
        Stats ovStats = StatsOf(ovValues);
        double maxAbs = 0, sumAbs = 0;
        for (int i = 0; i < ortValues.Length; i++) { double delta = Math.Abs(ortValues[i] - ovValues[i]); maxAbs = Math.Max(maxAbs, delta); sumAbs += delta; }
        double meanAbs = sumAbs / ortValues.Length;
        var ortResult = (PaddleDocumentUnwarpingResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, ortOutputs, CancellationToken.None));
        var ovResult = (PaddleDocumentUnwarpingResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, ovOutputs, CancellationToken.None));
        Assert.AreEqual(ortResult.Width, ovResult.Width); Assert.AreEqual(ortResult.Height, ovResult.Height); Assert.AreEqual(ortResult.Channels, ovResult.Channels);
        Assert.IsTrue(ortStats.Finite && ovStats.Finite);
        Assert.IsTrue(meanAbs < .01, "UVDoc backend mean divergence exceeded the diagnostic threshold: " + meanAbs.ToString("R"));
        string reportDirectory = Path.Combine(TestContext.TestResultsDirectory!, "paddle-document"); Directory.CreateDirectory(reportDirectory);
        string report = Path.Combine(reportDirectory, "uvdoc-ort-openvino-parity.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, model = descriptor.ModelId, modelSha256 = Sha256(ModelPath), image = ImagePath, imageSha256 = Sha256(ImagePath),
            inputShape = input.Tensor.Shape.ToString(), outputShape = ortTensor.Shape.ToString(),
            ort = new { elapsedMs = ortWatch.ElapsedMs, ortStats.Min, ortStats.Max, ortStats.Mean, ortStats.StdDev },
            openvino = new { elapsedMs = ovWatch.ElapsedMs, ovStats.Min, ovStats.Max, ovStats.Mean, ovStats.StdDev },
            parity = new { maxAbs, meanAbs, maxAbsDiagnosticThreshold = .1, meanAbsDiagnosticThreshold = .01, status = maxAbs < .02 ? "close" : "ranged-difference" }
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Console.WriteLine("PADDLE_DOCUMENT_UVDOC_PARITY " + File.ReadAllText(report));
    }

    private readonly struct Stats
    {
        public Stats(bool finite, float min, float max, double mean, double stdDev) { Finite = finite; Min = min; Max = max; Mean = mean; StdDev = stdDev; }
        public bool Finite { get; }
        public float Min { get; }
        public float Max { get; }
        public double Mean { get; }
        public double StdDev { get; }
    }

    private static Stats StatsOf(float[] values)
    {
        bool finite = values.All(float.IsFinite); float min = values.Min(); float max = values.Max(); double mean = values.Average(value => (double)value); double variance = values.Sum(value => Math.Pow(value - mean, 2)) / values.Length; return new Stats(finite, min, max, mean, Math.Sqrt(variance));
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed class StopwatchScope
    {
        private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
        public double ElapsedMs { get; private set; }
        public void Stop() { _watch.Stop(); ElapsedMs = _watch.Elapsed.TotalMilliseconds; }
    }
}
