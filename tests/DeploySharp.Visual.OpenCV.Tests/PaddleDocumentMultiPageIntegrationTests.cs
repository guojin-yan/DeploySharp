using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentMultiPageIntegrationTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string ImagePath = @"E:\Data\image\bus.jpg";

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task TwoPageOrientationLayoutBookPreservesPageOrderAndExports()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL") != "1") Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL=1 to run the multi-page PP-Structure case.");
        if (!File.Exists(ImagePath)) Assert.Inconclusive("Missing multi-page input: " + ImagePath);
        string orientationPath = RequireModel("pp-lcnet-x1-0-doc-ori.onnx");
        string layoutPath = RequireModel("pp-doclayout-l.onnx");
        var orientationDescriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-lcnet-x1-0-doc-ori");
        var orientationProfile = PaddleDocumentProfiles.CreateClassification(orientationDescriptor, PaddleDocumentProfiles.DocumentOrientationLabels, VisualTaskId.DocumentOrientation, modelSize: new VisualSize(224, 224));
        var layoutDescriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
        var layoutProfile = PaddleDocumentProfiles.CreatePaddleNmsRegions(layoutDescriptor, PaddleDocumentProfiles.Layout23Labels, new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: 0);
        using var orientationRegistry = new BackendRegistry(); orientationRegistry.UseOnnxRuntime();
        using var layoutRegistry = new BackendRegistry(); layoutRegistry.UseOnnxRuntime();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using VisualPipeline orientation = CreatePipeline(orientationRegistry, orientationProfile, orientationPath, request);
        using VisualPipeline layout = CreatePipeline(layoutRegistry, layoutProfile, layoutPath, request);
        string sourceSha = Sha256(ImagePath);
        var stages = new IPaddleDocumentPipelineStage[]
        {
            PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(PaddleDocumentModule.DocumentOrientation, orientation,
                (context, token) => new OpenCvVisualInputFactory().CreateFromFile(ImagePath, orientationProfile.VisualProfile, sourceSha, token),
                (context, inference) =>
                {
                    LabelScore top = inference.GetValue<ClassificationResult>().TopPrediction ?? throw new AssertFailedException("Orientation prediction missing.");
                    return new PaddleDocumentOrientationResult(new PaddleDocumentResultMetadata(orientationDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex), top.Label, top.Index * 90);
                }),
            PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(PaddleDocumentModule.LayoutDetection, layout,
                (context, token) => OpenCvPaddleDocumentPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), ImagePath, layoutProfile, sourceSha, token),
                (context, inference) =>
                {
                    DetectionResult value = inference.GetValue<DetectionResult>();
                    var regions = value.Detections.Select(item => new PaddleDocumentRegion(item.Label.Label, item.Label.Score, item.Box)).ToArray();
                    return new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection, new PaddleDocumentResultMetadata(layoutDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex), regions);
                })
        };
        var pipeline = new PaddleDocumentPipeline(stages);
        IReadOnlyList<PaddleDocumentPipelineResult> pages = await pipeline.RunManyAsync(new[]
        {
            new PaddleDocumentPage(ImagePath, new VisualSize(810, 1080), 0),
            new PaddleDocumentPage(ImagePath, new VisualSize(810, 1080), 1)
        }, CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(2, pages.Count);
        Assert.AreEqual(0, pages[0].Page.PageIndex); Assert.AreEqual(1, pages[1].Page.PageIndex);
        Assert.AreEqual(sourceSha, pages[1].GetRequired<PaddleDocumentOrientationResult>(PaddleDocumentModule.DocumentOrientation).Metadata.InputSha256);
        Assert.IsTrue(pages.All(page => page.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection).Regions.Count > 0));
        Assert.IsTrue(PaddleDocumentPipelineExport.ToJson(pages).Contains("\"pageIndex\"", StringComparison.Ordinal));
        Assert.IsTrue(PaddleDocumentPipelineExport.ToMarkdown(pages).Contains("PP-Structure page 1", StringComparison.Ordinal));
    }

    private static VisualPipeline CreatePipeline(BackendRegistry registry, PaddleDocumentProfile profile, string path, BackendRequest request)
    {
        var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
        return new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), registry, request, profile.VisualProfile.Task), request);
    }

    private static string RequireModel(string name)
    {
        string path = Path.Combine(ModelRoot, name);
        if (!File.Exists(path)) Assert.Inconclusive("Missing multi-page model: " + path);
        return path;
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
