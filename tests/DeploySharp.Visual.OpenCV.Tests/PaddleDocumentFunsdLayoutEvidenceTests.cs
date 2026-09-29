using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Collects real FUNSD page layout-model order evidence. / 收集真实 FUNSD 页面版面模型顺序证据。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentFunsdLayoutEvidenceTests
{
    private const string ModelPath = @"E:\Model\PaddleDocument\onnx\pp-doclayout-l.onnx";
    private static readonly string[] Images =
    {
        @"F:\OCRBenchmarkTesting\data\images\funsd\test\82504862.png",
        @"F:\OCRBenchmarkTesting\data\images\funsd\test\82562350.png",
        @"F:\OCRBenchmarkTesting\data\images\funsd\test\82573104.png"
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void DocLayoutRunsOnThreeRealFunsdPagesAndRetainsDeterministicRegions()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_FUNSD_LAYOUT") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_FUNSD_LAYOUT=1 to run the real FUNSD layout evidence.");
        if (!File.Exists(ModelPath)) Assert.Inconclusive("Missing PP-DocLayout-L model: " + ModelPath);
        if (Images.Any(path => !File.Exists(path))) Assert.Inconclusive("A FUNSD page is missing.");
        string modelSha = Sha256(ModelPath);
        var descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
        var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(descriptor, PaddleDocumentProfiles.Layout23Labels,
            new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: 0.3f);
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var pipeline = new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(ModelPath, OnnxRuntimeBackendProvider.BackendId), registry, request, VisualTaskId.LayoutDetection), request);
        var pages = new List<object>();
        foreach (string image in Images)
        {
            using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), image, profile, Sha256(image));
            VisualInferenceResult inference = pipeline.Run(input);
            DetectionResult result = inference.GetValue<DetectionResult>();
            Assert.IsTrue(result.Detections.Count > 0);
            var ordered = result.Detections.Select((d, index) => new { d, index })
                .OrderBy(item => item.d.Box.Y).ThenBy(item => item.d.Box.X).ThenBy(item => item.index)
                .Select((item, order) => new
                {
                    order,
                    modelIndex = item.index,
                    label = item.d.Label.Label,
                    score = item.d.Label.Score,
                    x = item.d.Box.X,
                    y = item.d.Box.Y,
                    width = item.d.Box.Width,
                    height = item.d.Box.Height
                }).ToArray();
            pages.Add(new
            {
                image = Path.GetFileName(image),
                imageSha256 = Sha256(image),
                detectionCount = result.Detections.Count,
                modelOrderIsTopLeft = result.Detections.Select((d, index) => new { d, index }).OrderBy(item => item.d.Box.Y).ThenBy(item => item.d.Box.X).Select(item => item.index).SequenceEqual(Enumerable.Range(0, result.Detections.Count)),
                regions = ordered
            });
        }
        string report = Path.Combine(TestContext.TestResultsDirectory!, "funsd-doclayout-l-ort-evidence.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            model = descriptor.ModelId,
            modelSha256 = modelSha,
            backend = "onnxruntime-cpu",
            labels = PaddleDocumentProfiles.Layout23Labels,
            pages,
            boundary = "Three real FUNSD pages; layout-model execution and deterministic regions only, not layout accuracy without aligned region labels."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
    }

    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
