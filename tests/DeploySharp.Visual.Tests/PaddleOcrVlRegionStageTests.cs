#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlRegionStageTests
{
    [TestMethod]
    public void AcceptsNegativeRopeDeltaFromOfficialPositionPlan()
    {
        var result = new PaddleOcrVlGenerationResult(
            PaddleOcrVlTask.TextRecognition,
            "text",
            new[] { 101 },
            GenerationFinishReason.EndOfSequence,
            promptLength: 157,
            ropeDelta: -132,
            sourceSha256: new string('a', 64),
            elapsed: TimeSpan.Zero);

        Assert.AreEqual(-132, result.RopeDelta);
    }

    [TestMethod]
    public async Task RoutesLayoutRegionsInOrderAndPreservesPageCoordinates()
    {
        const string pageSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        PaddleDocumentRegion[] regions =
        {
            new PaddleDocumentRegion("table", .95f, new RectangleF(10, 20, 100, 80)),
            new PaddleDocumentRegion("table", .90f, new RectangleF(200, 40, 120, 90))
        };
        PaddleDocumentRegionResult layout = LayoutResult(regions, pageIndex: 7);
        var stage = CreateStage(
            PaddleOcrVlTask.TableRecognition,
            pageSha,
            prepare: (context, region, index, token) => Task.FromResult(Input(PaddleOcrVlTask.TableRecognition, Hash(index))),
            generate: (context, region, index, prepared, token) => Task.FromResult(Generation(PaddleOcrVlTask.TableRecognition, "region-" + index, index, prepared.SourceSha256!)));

        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { LayoutStage(layout), stage });
        PaddleDocumentPipelineResult page = await pipeline.RunAsync(new PaddleDocumentPage("page-7", new VisualSize(640, 480), 7));
        PaddleDocumentVisionLanguagePageResult result = page.GetRequired<PaddleDocumentVisionLanguagePageResult>(PaddleDocumentModule.TableStructureRecognition);

        Assert.AreEqual(2, result.Executions.Count);
        Assert.AreEqual(2, result.SucceededCount);
        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual("region-0", result.Executions[0].Result!.Text);
        Assert.AreEqual("region-1", result.Executions[1].Result!.Text);
        Assert.AreEqual(regions[0].Bounds, result.Executions[0].SourceRegion.Bounds);
        Assert.AreEqual(regions[1].Bounds, result.Executions[1].Result!.SourceRegion!.Bounds);
        Assert.AreEqual(7, result.Metadata.PageIndex);
        Assert.AreEqual(pageSha, result.Metadata.InputSha256);
        Assert.IsFalse(result.CoordinatesAvailable);
        using JsonDocument exported = JsonDocument.Parse(PaddleDocumentPipelineExport.ToJson(page));
        JsonElement payload = exported.RootElement.GetProperty("results").EnumerateArray()
            .Single(value => value.GetProperty("module").GetString() == "TableStructureRecognition").GetProperty("payload");
        Assert.AreEqual(2, payload.GetProperty("succeededCount").GetInt32());
        JsonElement exportedRegion = payload.GetProperty("regions")[1];
        Assert.AreEqual(1, exportedRegion.GetProperty("regionIndex").GetInt32());
        Assert.AreEqual(200f, exportedRegion.GetProperty("source").GetProperty("bounds").GetProperty("x").GetSingle());
        Assert.AreEqual("region-1", exportedRegion.GetProperty("result").GetProperty("text").GetString());
        Assert.AreEqual("EndOfSequence", exportedRegion.GetProperty("result").GetProperty("finishReason").GetString());
        Assert.AreEqual(Hash(1), exportedRegion.GetProperty("result").GetProperty("inputSha256").GetString());
        StringAssert.Contains(PaddleDocumentPipelineExport.ToMarkdown(page), "regional routing");
    }

    [TestMethod]
    public async Task IsolatesOneRegionFailureAndRetainsRemainingResults()
    {
        const string pageSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        PaddleDocumentRegionResult layout = LayoutResult(new[]
        {
            new PaddleDocumentRegion("text", .9f, new RectangleF(0, 0, 80, 50)),
            new PaddleDocumentRegion("text", .8f, new RectangleF(90, 0, 80, 50)),
            new PaddleDocumentRegion("text", .7f, new RectangleF(180, 0, 80, 50))
        }, pageIndex: 2);
        var stage = CreateStage(
            PaddleOcrVlTask.TextRecognition,
            pageSha,
            prepare: (context, region, index, token) =>
            {
                if (index == 1) throw new InvalidOperationException("synthetic crop failure");
                return Task.FromResult(Input(PaddleOcrVlTask.TextRecognition, Hash(index)));
            },
            generate: (context, region, index, prepared, token) => Task.FromResult(Generation(PaddleOcrVlTask.TextRecognition, "ok-" + index, index, prepared.SourceSha256!)),
            continueOnRegionFailure: true);

        PaddleDocumentPipelineResult page = await new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { LayoutStage(layout), stage })
            .RunAsync(new PaddleDocumentPage("page-2", new VisualSize(320, 200), 2));
        PaddleDocumentVisionLanguagePageResult result = page.GetRequired<PaddleDocumentVisionLanguagePageResult>(PaddleDocumentModule.TextRecognition);

        Assert.AreEqual(2, result.SucceededCount);
        Assert.AreEqual(1, result.FailedCount);
        Assert.IsTrue(result.Executions[0].Succeeded);
        Assert.IsFalse(result.Executions[1].Succeeded);
        Assert.AreEqual("System.InvalidOperationException", result.Executions[1].FailureType);
        StringAssert.Contains(result.Executions[1].FailureMessage!, "synthetic crop failure");
        StringAssert.Contains(string.Join(";", result.Warnings), "vl-region-failure:1");
        Assert.AreEqual("ok-2", result.Executions[2].Result!.Text);
    }

    [TestMethod]
    public async Task UsesFallbackBeforeApplyingFailureIsolation()
    {
        const string pageSha = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        PaddleDocumentRegionResult layout = LayoutResult(new[]
        {
            new PaddleDocumentRegion("formula", .9f, new RectangleF(5, 5, 100, 40))
        }, pageIndex: 1);
        bool fallbackCalled = false;
        var stage = CreateStage(
            PaddleOcrVlTask.FormulaRecognition,
            pageSha,
            prepare: (context, region, index, token) => Task.FromResult(Input(PaddleOcrVlTask.FormulaRecognition, Hash(index))),
            generate: (context, region, index, prepared, token) => throw new InvalidOperationException("primary unavailable"),
            fallback: (context, region, index, exception, token) =>
            {
                fallbackCalled = true;
                Assert.AreEqual("primary unavailable", exception.Message);
                return Task.FromResult(Generation(PaddleOcrVlTask.FormulaRecognition, "fallback", index, Hash(index)));
            });

        PaddleDocumentPipelineResult page = await new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { LayoutStage(layout), stage })
            .RunAsync(new PaddleDocumentPage("page-1", new VisualSize(320, 200), 1));
        PaddleDocumentVisionLanguagePageResult result = page.GetRequired<PaddleDocumentVisionLanguagePageResult>(PaddleDocumentModule.FormulaRecognition);

        Assert.IsTrue(fallbackCalled);
        Assert.AreEqual("fallback", result.Executions[0].Result!.Text);
        StringAssert.Contains(string.Join(";", result.Executions[0].Result!.Warnings), "vl-region-fallback-used");
        Assert.AreEqual(0, result.FailedCount);
    }

    [TestMethod]
    public async Task FailsFastByDefaultWhenARegionCannotBePrepared()
    {
        PaddleDocumentRegionResult layout = LayoutResult(new[]
        {
            new PaddleDocumentRegion("text", .9f, new RectangleF(0, 0, 80, 50))
        }, pageIndex: 0);
        var stage = CreateStage(
            PaddleOcrVlTask.TextRecognition,
            new string('d', 64),
            prepare: (context, region, index, token) => throw new InvalidOperationException("required crop missing"),
            generate: (context, region, index, prepared, token) => Task.FromResult(Generation(PaddleOcrVlTask.TextRecognition, "unused", index, new string('e', 64))));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { LayoutStage(layout), stage })
            .RunAsync(new PaddleDocumentPage("page-0", new VisualSize(320, 200))));
    }

    [TestMethod]
    public async Task RejectsPreparedInputForAnotherTaskBeforeRunningGeneration()
    {
        PaddleDocumentRegionResult layout = LayoutResult(new[]
        {
            new PaddleDocumentRegion("text", .9f, new RectangleF(0, 0, 80, 50))
        }, pageIndex: 0);
        bool generated = false;
        var stage = CreateStage(PaddleOcrVlTask.TextRecognition, Hash(0),
            prepare: (context, region, index, token) => Task.FromResult(Input(PaddleOcrVlTask.TableRecognition, Hash(index))),
            generate: (context, region, index, prepared, token) =>
            {
                generated = true;
                return Task.FromResult(Generation(PaddleOcrVlTask.TextRecognition, "wrong-prompt", index, Hash(index)));
            });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { LayoutStage(layout), stage })
            .RunAsync(new PaddleDocumentPage("wrong-prompt", new VisualSize(320, 200))));
        Assert.IsFalse(generated);
    }

    [TestMethod]
    public async Task IsolatesAResultBelongingToAnotherRegion()
    {
        PaddleDocumentRegionResult layout = LayoutResult(new[]
        {
            new PaddleDocumentRegion("text", .9f, new RectangleF(0, 0, 80, 50)),
            new PaddleDocumentRegion("text", .8f, new RectangleF(90, 0, 80, 50))
        }, pageIndex: 0);
        var stage = CreateStage(PaddleOcrVlTask.TextRecognition, Hash(0),
            prepare: (context, region, index, token) => Task.FromResult(Input(PaddleOcrVlTask.TextRecognition, Hash(index))),
            generate: (context, region, index, prepared, token) => Task.FromResult(
                Generation(PaddleOcrVlTask.TextRecognition, "region-" + index, index, Hash(1))),
            continueOnRegionFailure: true);

        PaddleDocumentPipelineResult page = await new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { LayoutStage(layout), stage })
            .RunAsync(new PaddleDocumentPage("wrong-region", new VisualSize(320, 200)));
        PaddleDocumentVisionLanguagePageResult result = page.GetRequired<PaddleDocumentVisionLanguagePageResult>(PaddleDocumentModule.TextRecognition);

        Assert.AreEqual(1, result.FailedCount);
        StringAssert.Contains(result.Executions[0].FailureMessage!, "source does not match");
        Assert.AreEqual("region-1", result.Executions[1].Result!.Text);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationStopsCallbacksWithoutFallbackOrFailureIsolation(bool duringGeneration)
    {
        using var cancellation = new CancellationTokenSource();
        PaddleDocumentRegionResult layout = LayoutResult(new[]
        {
            new PaddleDocumentRegion("text", .9f, new RectangleF(0, 0, 80, 50))
        }, pageIndex: 0);
        bool generated = false;
        bool usedFallback = false;
        var stage = CreateStage(PaddleOcrVlTask.TextRecognition, Hash(0),
            prepare: (context, region, index, token) =>
            {
                if (!duringGeneration) cancellation.Cancel();
                return Task.FromResult(Input(PaddleOcrVlTask.TextRecognition, Hash(index)));
            },
            generate: (context, region, index, prepared, token) =>
            {
                generated = true;
                cancellation.Cancel();
                throw new InvalidOperationException("adapter failed after cancellation");
            },
            fallback: (context, region, index, exception, token) =>
            {
                usedFallback = true;
                return Task.FromResult(Generation(PaddleOcrVlTask.TextRecognition, "unused", index, Hash(index)));
            },
            continueOnRegionFailure: true);

        await Assert.ThrowsAsync<OperationCanceledException>(() => new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { LayoutStage(layout), stage })
            .RunAsync(new PaddleDocumentPage("cancel", new VisualSize(320, 200)), cancellation.Token));

        Assert.AreEqual(duringGeneration, generated);
        Assert.IsFalse(usedFallback);
    }

    [TestMethod]
    public void RejectsPageResultWithMismatchedModelModuleEvenWhenLayoutIsEmpty()
    {
        var metadata = new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.LayoutDetection), "onnxruntime-cpu", TimeSpan.Zero, Hash(0));
        Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentVisionLanguagePageResult(
            PaddleOcrVlTask.TextRecognition, metadata, Array.Empty<PaddleDocumentVisionLanguageRegionExecution>()));
    }

    private static PaddleOcrVlRegionStage CreateStage(
        PaddleOcrVlTask task,
        string pageSha,
        Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
        Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
        Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
        bool continueOnRegionFailure = false)
        => new(
            Model(PaddleDocumentVisionLanguageResult.ModuleForTask(task)),
            "onnxruntime-cpu",
            task,
            context => pageSha,
            prepare,
            generate,
            inputIdentity: (context, region, index, prepared) => prepared.SourceSha256!,
            fallback: fallback,
            continueOnRegionFailure: continueOnRegionFailure);

    private static PaddleDocumentPipelineStage LayoutStage(PaddleDocumentRegionResult result)
        => new(PaddleDocumentModule.LayoutDetection, (context, token) => Task.FromResult<PaddleDocumentModuleResult>(result));

    private static PaddleDocumentRegionResult LayoutResult(IEnumerable<PaddleDocumentRegion> regions, int pageIndex)
    {
        PaddleDocumentModelDescriptor model = Model(PaddleDocumentModule.LayoutDetection);
        return new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
            new PaddleDocumentResultMetadata(model, "onnxruntime-cpu", TimeSpan.Zero, new string('f', 64), pageIndex), regions);
    }

    private static PaddleOcrVlPreparedInput Input(PaddleOcrVlTask task, string sourceSha)
    {
        PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336), task);
        int length = checked(plan.PatchCount * 3 * 14 * 14);
        return new PaddleOcrVlPreparedInput(plan,
            new Tensor<float>(new TensorShape(plan.GridHeight, plan.GridWidth, 3, 14, 14), new float[length], TensorBufferOwnership.Transfer), sourceSha);
    }

    private static PaddleOcrVlGenerationResult Generation(PaddleOcrVlTask task, string text, int index, string sourceSha)
        => new(task, text, new[] { index + 1 }, GenerationFinishReason.EndOfSequence, 4, 0, sourceSha, TimeSpan.FromMilliseconds(1));

    private static PaddleDocumentModelDescriptor Model(PaddleDocumentModule module)
        => new("test/paddleocr-vl/region/" + module, "PaddleOCR-VL region test", module, "https://example.invalid/paddleocr-vl", "onnx");

    private static string Hash(int index) => new string((char)('a' + index), 64);
}
#endif
