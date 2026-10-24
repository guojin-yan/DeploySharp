using System;
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
public sealed class OpenCvPaddleOcrVlRegionStageTests
{
    [TestMethod]
    public async Task ConcurrentPagesReuseEachSourceAndKeepRegionAndPageProvenance()
    {
        int sourceCalls = 0;
        using var image = new Mat(20, 40, MatType.CV_8UC3, new Scalar(10, 20, 90));
        OpenCvImageSource source = OpenCvImageSource.FromBytes(ImageCodecs.ImEncode(".png", image));
        var stage = new OpenCvPaddleOcrVlRegionStage(Model(PaddleDocumentModule.TableStructureRecognition), "test-generator",
            PaddleOcrVlTask.TableRecognition, page => { Interlocked.Increment(ref sourceCalls); return (OpenCvImageSource)page.Source; },
            async (context, region, index, input, token) =>
            {
                await Task.Delay(context.Page.PageIndex == 7 ? 10 : 1, token);
                Assert.AreEqual(new VisualSize(10, 8), input.ImagePlan.SourceSize);
                return Generation(input, "page-" + context.Page.PageIndex + "-region-" + index);
            }, minimumPixels: 784, maximumPixels: 784);
        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { Layout(), stage });
        var pages = new[] { new PaddleDocumentPage(source, new VisualSize(40, 20), 7), new PaddleDocumentPage(source, new VisualSize(40, 20), 3) };
        var results = await pipeline.RunManyConcurrentAsync(pages, 2);
        Assert.AreEqual(2, sourceCalls);
        for (int index = 0; index < results.Count; index++)
        {
            PaddleDocumentVisionLanguagePageResult result = results[index].GetRequired<PaddleDocumentVisionLanguagePageResult>(PaddleDocumentModule.TableStructureRecognition);
            Assert.AreEqual(pages[index].PageIndex, result.Metadata.PageIndex);
            Assert.AreEqual(source.Sha256, result.Metadata.InputSha256);
            Assert.AreEqual(2, result.SucceededCount);
            Assert.IsFalse(result.CoordinatesAvailable);
            Assert.AreEqual(new RectangleF(2, 3, 10, 8), result.Executions[0].SourceRegion.Bounds);
            Assert.AreEqual("page-" + pages[index].PageIndex + "-region-1", result.Executions[1].Result!.Text);
            Assert.AreNotEqual(source.Sha256, result.Executions[0].Result!.Metadata.InputSha256);
        }
    }

    [TestMethod]
    public async Task DimensionMismatchFailsBeforeGenerationAndOneRegionFailureCanBeIsolated()
    {
        using var image = new Mat(20, 40, MatType.CV_8UC3, new Scalar(10, 20, 90));
        OpenCvImageSource source = OpenCvImageSource.FromBytes(ImageCodecs.ImEncode(".png", image));
        int calls = 0;
        var stage = new OpenCvPaddleOcrVlRegionStage(Model(PaddleDocumentModule.TableStructureRecognition), "test-generator", PaddleOcrVlTask.TableRecognition,
            page => source, (context, region, index, input, token) =>
            {
                calls++;
                if (index == 0) throw new InvalidOperationException("expected region failure");
                return Task.FromResult(Generation(input, "second-region"));
            }, minimumPixels: 784, maximumPixels: 784, continueOnRegionFailure: true);
        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { Layout(), stage });
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pipeline.RunAsync(new PaddleDocumentPage(source, new VisualSize(39, 20))));
        Assert.AreEqual(0, calls);
        PaddleDocumentVisionLanguagePageResult result = (await pipeline.RunAsync(new PaddleDocumentPage(source, new VisualSize(40, 20))))
            .GetRequired<PaddleDocumentVisionLanguagePageResult>(PaddleDocumentModule.TableStructureRecognition);
        Assert.AreEqual(1, result.FailedCount);
        Assert.AreEqual(1, result.SucceededCount);
        Assert.AreEqual("second-region", result.Executions[1].Result!.Text);
    }

    [TestMethod]
    public async Task MissingLayoutAndPreCancelledPagesDoNotOpenTheSource()
    {
        int calls = 0;
        var stage = new OpenCvPaddleOcrVlRegionStage(Model(PaddleDocumentModule.TableStructureRecognition), "test-generator", PaddleOcrVlTask.TableRecognition,
            page => { calls++; throw new InvalidOperationException("source should not be opened"); },
            (context, region, index, input, token) => Task.FromResult(Generation(input, "unused")));
        var pipeline = new PaddleDocumentPipeline(new[] { stage });
        var page = new PaddleDocumentPage("no-source", new VisualSize(40, 20));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pipeline.RunAsync(page));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.RunAsync(page, cancelled.Token));
        Assert.AreEqual(0, calls);
    }

    private static IPaddleDocumentPipelineStage Layout() => new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection,
        (context, token) => Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
            new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.LayoutDetection), "manual-layout", TimeSpan.Zero, ((OpenCvImageSource)context.Page.Source).Sha256, context.Page.PageIndex),
            new[] { new PaddleDocumentRegion("table", 1f, new RectangleF(2, 3, 10, 8)), new PaddleDocumentRegion("table", 1f, new RectangleF(20, 4, 10, 8)) })));

    private static PaddleOcrVlGenerationResult Generation(PaddleOcrVlPreparedInput input, string text)
        => new(input.ImagePlan.Task, text, new[] { 1 }, GenerationFinishReason.EndOfSequence, 4, 0, input.SourceSha256, TimeSpan.Zero);

    private static PaddleDocumentModelDescriptor Model(PaddleDocumentModule module)
        => new("test/vl/" + module, "VL test", module, "https://example.invalid/vl", "onnx");
}
