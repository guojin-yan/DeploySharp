using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
public sealed class OpenCvPaddleOcrVlLayoutStageTests
{
    [TestMethod]
    public async Task ConcurrentMixedPagesReuseOneSourcePerPageAndRetainSkippedIndices()
    {
        int sourceCalls = 0, generationCalls = 0;
        using var image = new Mat(20, 140, MatType.CV_8UC3, new Scalar(10, 20, 90));
        OpenCvImageSource source = OpenCvImageSource.FromBytes(ImageCodecs.ImEncode(".png", image));
        string[] categories = { "table", "image", "formula", "chart", "seal", "text", "spotting" };
        var router = PaddleOcrVlTaskRouter.CreatePpStructure(new Dictionary<string, PaddleOcrVlTask?> { ["spotting"] = PaddleOcrVlTask.TextSpotting });
        var stage = new OpenCvPaddleOcrVlLayoutStage(Model(PaddleDocumentModule.VisionLanguageParsing), "test", router,
            page => { Interlocked.Increment(ref sourceCalls); return (OpenCvImageSource)page.Source; },
            async (context, region, index, input, token) =>
            {
                Interlocked.Increment(ref generationCalls);
                await Task.Delay(1, token);
                Assert.AreNotEqual(1, index);
                Assert.AreEqual(new VisualSize(10, 8), input.ImagePlan.SourceSize);
                Assert.IsTrue(router.TryGetTask(categories[index], out var task));
                Assert.AreEqual(task, input.ImagePlan.Task);
                return Generation(input, "page-" + context.Page.PageIndex + "-region-" + index);
            }, minimumPixels: 784, maximumPixels: 784);
        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { Layout(source, categories), stage });
        var pages = new[] { new PaddleDocumentPage(source, new VisualSize(140, 20), 7), new PaddleDocumentPage(source, new VisualSize(140, 20), 3) };
        var results = await pipeline.RunManyConcurrentAsync(pages, 2);
        Assert.AreEqual(2, sourceCalls);
        Assert.AreEqual(12, generationCalls);
        for (int index = 0; index < results.Count; index++)
        {
            var result = results[index].GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
            Assert.AreEqual(pages[index].PageIndex, result.Metadata.PageIndex);
            Assert.AreEqual(source.Sha256, result.Metadata.InputSha256);
            Assert.AreEqual(6, result.SucceededCount);
            Assert.AreEqual(1, result.SkippedCount);
            Assert.AreEqual(0, result.FailedCount);
            Assert.IsTrue(result.Executions[1].Skipped);
            Assert.AreEqual(2, result.Executions[2].Result!.RegionIndex);
            Assert.AreEqual(PaddleOcrVlTask.FormulaRecognition, result.Executions[2].Result!.Task);
            Assert.AreEqual("page-" + pages[index].PageIndex + "-region-6", result.Executions[6].Result!.Text);
        }
    }

    [TestMethod]
    public async Task FailureAndDimensionChecksPreserveNativeAdapterBoundary()
    {
        using var image = new Mat(20, 60, MatType.CV_8UC3, new Scalar(10, 20, 90));
        OpenCvImageSource source = OpenCvImageSource.FromBytes(ImageCodecs.ImEncode(".png", image));
        int generations = 0;
        var stage = new OpenCvPaddleOcrVlLayoutStage(Model(PaddleDocumentModule.VisionLanguageParsing), "test", PaddleOcrVlTaskRouter.CreatePpStructure(),
            page => source, (context, region, index, input, token) =>
            {
                generations++;
                if (index == 0) throw new InvalidOperationException("expected table failure");
                return Task.FromResult(Generation(input, "formula"));
            }, minimumPixels: 784, maximumPixels: 784, continueOnRegionFailure: true);
        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { Layout(source, new[] { "table", "image", "formula" }), stage });
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pipeline.RunAsync(new PaddleDocumentPage(source, new VisualSize(59, 20))));
        Assert.AreEqual(0, generations);
        var result = (await pipeline.RunAsync(new PaddleDocumentPage(source, new VisualSize(60, 20))))
            .GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        Assert.AreEqual(1, result.FailedCount);
        Assert.AreEqual(1, result.SkippedCount);
        Assert.AreEqual(1, result.SucceededCount);
        Assert.AreEqual(PaddleOcrVlTask.TableRecognition, result.Executions[0].Task);
        Assert.AreEqual(PaddleOcrVlTask.FormulaRecognition, result.Executions[2].Task);
    }

    [TestMethod]
    public async Task MissingLayoutAndPreCancellationDoNotOpenAnImage()
    {
        int sources = 0;
        var stage = new OpenCvPaddleOcrVlLayoutStage(Model(PaddleDocumentModule.VisionLanguageParsing), "test", PaddleOcrVlTaskRouter.CreatePpStructure(),
            page => { sources++; throw new InvalidOperationException("should not open"); },
            (context, region, index, input, token) => Task.FromResult(Generation(input, "unused")));
        var pipeline = new PaddleDocumentPipeline(new[] { stage });
        var page = new PaddleDocumentPage("source", new VisualSize(40, 20));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pipeline.RunAsync(page));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.RunAsync(page, cancelled.Token));
        Assert.AreEqual(0, sources);
    }

    private static IPaddleDocumentPipelineStage Layout(OpenCvImageSource source, string[] categories)
        => new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection, (context, token) => Task.FromResult<PaddleDocumentModuleResult>(
            new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
                new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.LayoutDetection), "manual", TimeSpan.Zero, source.Sha256, context.Page.PageIndex),
                categories.Select((category, index) => new PaddleDocumentRegion(category, 1, new RectangleF((index * 20) + 2, 3, 10, 8))))));
    private static PaddleOcrVlGenerationResult Generation(PaddleOcrVlPreparedInput input, string text)
        => new(input.ImagePlan.Task, text, new[] { 1 }, GenerationFinishReason.EndOfSequence, 4, 0, input.SourceSha256, TimeSpan.Zero);
    private static PaddleDocumentModelDescriptor Model(PaddleDocumentModule module)
        => new("test/vl", "VL test", module, "https://example.invalid/vl", "onnx");
}
