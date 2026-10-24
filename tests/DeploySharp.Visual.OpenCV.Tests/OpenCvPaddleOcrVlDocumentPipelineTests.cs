using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

namespace DeploySharp.Visual.OpenCV.Tests;

// Tiny ONNX detector plus injected generation: native ownership/scheduling contracts,
// not evidence of real VL generation or layout-model quality.
[TestClass]
public sealed class OpenCvPaddleOcrVlDocumentPipelineTests
{
    [TestMethod]
    public void DecodedLayoutPreparationMatchesFileTensorAndAuxiliaryGeometryAfterFileRemoval()
    {
        string path = TemporaryImage();
        try
        {
            var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l"),
                PaddleDocumentProfiles.Layout23Labels, new VisualSize(32, 32), includeGeometryInputs: true,
                preprocessing: new VisualPreprocessingOptions(new VisualSize(32, 32), VisualResizeMode.Letterbox,
                    VisualColorOrder.Rgb, VisualNormalizationOptions.Scale(1f / 255f), VisualTensorLayout.Nchw, 1));
            var factory = new OpenCvVisualInputFactory();
            OpenCvImageSource source = OpenCvImageSource.FromFile(path);
            using var expected = OpenCvPaddleDocumentPreprocessing.Create(factory, source, profile, "page-id");
            using var decoded = factory.DecodeForRois(source);
            File.Delete(path);
            using var actual = OpenCvPaddleDocumentPreprocessing.CreateFromPage(decoded, profile, "page-id");
            CollectionAssert.AreEqual(((Tensor<float>)expected.Tensor).ToArray(), ((Tensor<float>)actual.Tensor).ToArray());
            Assert.AreEqual(expected.SourceSize, actual.SourceSize);
            Assert.AreEqual(expected.ModelSize, actual.ModelSize);
            Assert.AreEqual("page-id", actual.InputId);
            Assert.AreEqual(2, actual.AuxiliaryInputs.Count);
            for (int index = 0; index < 2; index++)
            {
                Assert.AreEqual(expected.AuxiliaryInputs[index].Name, actual.AuxiliaryInputs[index].Name);
                CollectionAssert.AreEqual(((Tensor<float>)expected.AuxiliaryInputs[index].Tensor).ToArray(),
                    ((Tensor<float>)actual.AuxiliaryInputs[index].Tensor).ToArray());
            }
            actual.Dispose();
            // Disposing a prepared tensor never disposes the shared decoded owner.
            Assert.AreEqual(new VisualSize(80, 40), decoded.SourceSize);
            using var again = OpenCvPaddleDocumentPreprocessing.CreateFromPage(decoded, profile);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.ThrowsExactly<OpenCvVisualException>(() => OpenCvPaddleDocumentPreprocessing.CreateFromPage(decoded, profile, cancellationToken: cancelled.Token));
            decoded.Dispose();
            var disposed = Assert.ThrowsExactly<OpenCvVisualException>(() => OpenCvPaddleDocumentPreprocessing.CreateFromPage(decoded, profile));
            Assert.AreEqual(OpenCvErrorCodes.ObjectDisposed, disposed.ErrorCode);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [TestMethod]
    public async Task DetectorAndAllCropsShareOneDecodedPageAndRetainCallerSource()
    {
        string path = TemporaryImage();
        try
        {
            OpenCvImageSource source = OpenCvImageSource.FromFile(path);
            using var registry = Registry();
            using var layout = Layout(registry);
            int calls = 0;
            var pipeline = new OpenCvPaddleOcrVlDocumentPipeline(layout, LayoutModel(), VlModel(), "injected-test",
                (context, region, index, input, token) =>
                {
                    Assert.AreSame(source, context.Page.Source);
                    Assert.AreEqual(calls++, index);
                    Assert.IsTrue(input.ImagePlan.SourceSize.Width > 0);
                    File.Delete(path); // Every later crop must still work without reopening the file.
                    return Task.FromResult(Generation(input, region.Category));
                }, minimumPixels: 784, maximumPixels: 12544);
            var result = await pipeline.RunAsync(source, 7);
            Assert.AreSame(source, result.Page.Source);
            Assert.AreEqual(new VisualSize(80, 40), result.Page.SourceSize);
            Assert.AreEqual(7, result.Page.PageIndex);
            var detected = result.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection);
            var generated = result.GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
            Assert.AreEqual(2, calls);
            Assert.AreEqual(detected.Regions.Count, generated.Executions.Count);
            Assert.AreEqual(2, generated.SucceededCount);
            Assert.AreEqual(source.Sha256, detected.Metadata.InputSha256);
            Assert.AreEqual(source.Sha256, generated.Metadata.InputSha256);
            for (int index = 0; index < generated.Executions.Count; index++)
            {
                Assert.AreEqual(detected.Regions[index].Bounds, generated.Executions[index].SourceRegion.Bounds);
                Assert.AreEqual(index.ToString(), detected.Regions[index].Metadata["layoutModelRegionIndex"]);
            }
            // The wrapper owns neither detector nor its Session.
            using var input = new OpenCvVisualInputFactory().Create(Source(), layout.Selection.Profile);
            Assert.IsNotNull(layout.Run(input));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [TestMethod]
    public async Task RegionBudgetPageBudgetAndDimensionsFailBeforeGeneration()
    {
        using var registry = Registry();
        using var layout = Layout(registry);
        int generations = 0;
        OpenCvPaddleOcrVlDocumentPipeline Pipeline(int regions = 64, int pixels = 10000) => new(layout, LayoutModel(), VlModel(), "test",
            (context, region, index, input, token) => { generations++; return Task.FromResult(Generation(input)); },
            maximumRegions: regions, maximumPagePixels: pixels, minimumPixels: 784, maximumPixels: 12544);
        OpenCvImageSource source = Source();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Pipeline(regions: 1).RunAsync(source));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Pipeline(pixels: 3199).RunAsync(source));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Pipeline().RunAsync(new PaddleDocumentPage(source, new VisualSize(79, 40))));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Pipeline().RunAsync(source, cancellationToken: cancelled.Token));
        Assert.AreEqual(0, generations);
        Assert.AreEqual(2, (await Pipeline().RunAsync(source)).GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing).SucceededCount);
    }

    [TestMethod]
    public async Task SkipsKeepDetectorIndicesAndRegionFailureCanBeIsolated()
    {
        using var registry = Registry();
        using var layout = Layout(registry, secondLabel: "image");
        int calls = 0;
        var pipeline = new OpenCvPaddleOcrVlDocumentPipeline(layout, LayoutModel(), VlModel(), "test",
            (context, region, index, input, token) => { calls++; throw new InvalidOperationException("injected region failure"); },
            minimumPixels: 784, maximumPixels: 12544, continueOnRegionFailure: true);
        var result = (await pipeline.RunAsync(Source())).GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(1, result.SkippedCount); Assert.AreEqual(1, result.FailedCount);
        Assert.AreEqual(0, result.Executions[0].RegionIndex); Assert.IsTrue(result.Executions[0].Skipped);
        Assert.AreEqual(1, result.Executions[1].RegionIndex); Assert.AreEqual(PaddleOcrVlTask.TextRecognition, result.Executions[1].Task);
    }

    [TestMethod]
    public async Task BoundedWorkersKeepPageOrderAndOriginalSources()
    {
        using var registry = Registry();
        using var layout = Layout(registry);
        int active = 0, peak = 0;
        var pipeline = new OpenCvPaddleOcrVlDocumentPipeline(layout, LayoutModel(), VlModel(), "test",
            async (context, region, index, input, token) =>
            {
                int current = Interlocked.Increment(ref active);
                int observed;
                do { observed = Volatile.Read(ref peak); } while (current > observed && Interlocked.CompareExchange(ref peak, current, observed) != observed);
                try { await Task.Delay(context.Page.PageIndex == 7 ? 40 : 5, token); return Generation(input, context.Page.PageIndex.ToString()); }
                finally { Interlocked.Decrement(ref active); }
            }, minimumPixels: 784, maximumPixels: 12544);
        var pages = new[] { 7, 3, 9 }.Select(index => new PaddleDocumentPage(Source(), new VisualSize(80, 40), index)).ToArray();
        var results = await pipeline.RunManyAsync(pages, maximumConcurrency: 2);
        Assert.AreEqual(2, peak); Assert.AreEqual(0, active);
        for (int index = 0; index < pages.Length; index++)
        {
            Assert.AreSame(pages[index].Source, results[index].Page.Source);
            Assert.AreEqual(pages[index].PageIndex, results[index].Page.PageIndex);
        }
        Assert.AreEqual(0, (await pipeline.RunManyAsync(Array.Empty<PaddleDocumentPage>())).Count);
    }

    [TestMethod]
    public async Task CancellationStopsGenerationAndSameCallerOwnedDetectorRemainsUsable()
    {
        using var registry = Registry();
        using var layout = Layout(registry);
        using var cancelled = new CancellationTokenSource();
        int calls = 0;
        var pipeline = new OpenCvPaddleOcrVlDocumentPipeline(layout, LayoutModel(), VlModel(), "test",
            async (context, region, index, input, token) =>
            {
                if (Interlocked.Increment(ref calls) == 1) { cancelled.Cancel(); await Task.Delay(1, token); }
                return Generation(input);
            }, minimumPixels: 784, maximumPixels: 12544);
        await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.RunAsync(Source(), cancellationToken: cancelled.Token));
        Assert.AreEqual(1, calls);
        Assert.AreEqual(2, (await pipeline.RunAsync(Source())).GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing).SucceededCount);
    }

    [TestMethod]
    public async Task PageFailureCancelsAndDrainsSiblingBeforeReturningWithoutHidingOriginalError()
    {
        using var registry = Registry();
        using var layout = Layout(registry);
        var bothStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0, active = 0;
        var pipeline = new OpenCvPaddleOcrVlDocumentPipeline(layout, LayoutModel(), VlModel(), "test",
            async (context, region, index, input, token) =>
            {
                Interlocked.Increment(ref active);
                using var registration = token.Register(() => { if (context.Page.PageIndex == 7) throw new InvalidOperationException("injected cancellation callback error"); });
                try
                {
                    if (Interlocked.Increment(ref calls) == 2) bothStarted.TrySetResult(true);
                    await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
                    if (context.Page.PageIndex == 3) throw new InvalidOperationException("original page error");
                    await Task.Delay(Timeout.Infinite, token);
                    return Generation(input);
                }
                finally { Interlocked.Decrement(ref active); }
            }, minimumPixels: 784, maximumPixels: 12544);
        var pages = new[] { 7, 3, 9 }.Select(index => new PaddleDocumentPage(Source(), new VisualSize(80, 40), index)).ToArray();
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pipeline.RunManyAsync(pages, 2));
        Assert.AreEqual("original page error", error.Message);
        Assert.AreEqual(0, active); Assert.AreEqual(2, calls);
    }

    private static BackendRegistry Registry() { var registry = new BackendRegistry(); registry.UseOnnxRuntime(); return registry; }
    private static VisualPipeline Layout(BackendRegistry registry, string secondLabel = "table")
    {
        var profile = PaddleDocumentProfiles.CreateRegionDetection(LayoutModel(), new[] { "text", secondLabel }, VisualTaskId.LayoutDetection,
            "images", "detections", new VisualSize(100, 100), normalizedCoordinates: false, classScoreOffset: 5,
            scoreMode: DetectionScoreMode.ObjectnessTimesClassScore, objectnessIndex: 4,
            decoderOptions: new DetectionDecoderOptions(scoreThreshold: .25f, iouThreshold: .45f),
            preprocessing: new VisualPreprocessingOptions(new VisualSize(100, 100), VisualResizeMode.Letterbox,
                VisualColorOrder.Rgb, VisualNormalizationOptions.None, VisualTensorLayout.Nchw, 1));
        var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var artifact = profile.CreateArtifact(Path.Combine(AppContext.BaseDirectory, "fixtures", "onnx", "detection.onnx"));
        return new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.LayoutDetection), request);
    }
    private static PaddleDocumentModelDescriptor LayoutModel() => new("test/vl-layout", "Tiny test layout", PaddleDocumentModule.LayoutDetection, "https://example.invalid/layout", "onnx");
    private static PaddleDocumentModelDescriptor VlModel() => new("test/vl", "Injected generator", PaddleDocumentModule.VisionLanguageParsing, "https://example.invalid/vl", "onnx");
    private static PaddleOcrVlGenerationResult Generation(PaddleOcrVlPreparedInput input, string text = "test")
        => new(input.ImagePlan.Task, text, new[] { 1 }, GenerationFinishReason.EndOfSequence, 4, 0, input.SourceSha256, TimeSpan.Zero);
    private static OpenCvImageSource Source() { using var image = new Mat(40, 80, MatType.CV_8UC3, new Scalar(10, 40, 90)); return OpenCvImageSource.FromBytes(ImageCodecs.ImEncode(".png", image)); }
    private static string TemporaryImage()
    {
        string path = Path.Combine(Path.GetTempPath(), "deploysharp-vl-detector-" + Guid.NewGuid().ToString("N") + ".png");
        using var image = new Mat(40, 80, MatType.CV_8UC3, new Scalar(10, 40, 90));
        File.WriteAllBytes(path, ImageCodecs.ImEncode(".png", image));
        return path;
    }
}
