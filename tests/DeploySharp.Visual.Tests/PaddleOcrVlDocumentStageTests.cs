#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlDocumentStageTests
{
    [TestMethod]
    [DataRow("<fcel>A<nl>**B**", "~~~")]
    [DataRow("<table>A</table>\n~~~\n# literal heading", "~~~~")]
    public async Task MarkdownKeepsHtmlTokensAndModelEmittedFencesLiteral(string raw, string fence)
    {
        string sourceSha = new string('a', 64);
        var input = Input(PaddleOcrVlTask.TableRecognition, sourceSha);
        var stage = new PaddleOcrVlDocumentStage(Model(PaddleDocumentModule.TableStructureRecognition), "test-generator", PaddleOcrVlTask.TableRecognition,
            (context, token) => Task.FromResult(input),
            (context, prepared, token) => Task.FromResult(new PaddleOcrVlGenerationResult(PaddleOcrVlTask.TableRecognition,
                raw, new[] { 1 }, GenerationFinishReason.EndOfSequence, 4, 0, sourceSha, TimeSpan.Zero)));
        var page = await new PaddleDocumentPipeline(new[] { stage }).RunAsync(new PaddleDocumentPage("page", new VisualSize(336, 336)));
        StringAssert.Contains(PaddleDocumentPipelineExport.ToMarkdown(page), fence + "text" + Environment.NewLine + raw + Environment.NewLine + fence);
    }

    [TestMethod]
    public async Task MapsRawVlOutputToPageProvenanceWithoutInventingCoordinates()
    {
        const string sourceSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        PaddleOcrVlPreparedInput input = Input(PaddleOcrVlTask.TableRecognition, sourceSha);
        PaddleDocumentModelDescriptor model = Model(PaddleDocumentModule.TableStructureRecognition);
        var generation = new PaddleOcrVlGenerationResult(PaddleOcrVlTask.TableRecognition, "<table><tr><td>A</td></tr></table>",
            new[] { 11, 12, 13 }, GenerationFinishReason.EndOfSequence, promptLength: 9, ropeDelta: 3, sourceSha, TimeSpan.FromMilliseconds(4));
        var stage = new PaddleOcrVlDocumentStage(model, "onnxruntime-cpu", PaddleOcrVlTask.TableRecognition,
            (context, token) => Task.FromResult(input),
            (context, prepared, token) => Task.FromResult(generation));

        PaddleDocumentPipelineResult page = await new PaddleDocumentPipeline(new[] { stage })
            .RunAsync(new PaddleDocumentPage("page-4", new VisualSize(800, 600), pageIndex: 4));

        PaddleDocumentVisionLanguageResult result = page.GetRequired<PaddleDocumentVisionLanguageResult>(PaddleDocumentModule.TableStructureRecognition);
        Assert.AreEqual(PaddleOcrVlTask.TableRecognition, result.Task);
        Assert.AreEqual("<table><tr><td>A</td></tr></table>", result.Text);
        CollectionAssert.AreEqual(new[] { 11, 12, 13 }, result.TokenIds.ToArray());
        Assert.AreEqual(GenerationFinishReason.EndOfSequence, result.FinishReason);
        Assert.AreEqual(4, result.Metadata.PageIndex);
        Assert.AreEqual(sourceSha, result.Metadata.InputSha256);
        Assert.IsFalse(result.CoordinatesAvailable);
        StringAssert.Contains(string.Join(";", result.Warnings), "vl-raw-output-no-coordinates");
        StringAssert.Contains(PaddleDocumentPipelineExport.ToJson(page), "coordinatesAvailable");
    }

    [TestMethod]
    public async Task MultiPageConcurrentExecutionPreservesPageOrderAndTruncationWarning()
    {
        const string sourceSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        PaddleOcrVlPreparedInput input = Input(PaddleOcrVlTask.TextRecognition, sourceSha);
        PaddleDocumentModelDescriptor model = Model(PaddleDocumentModule.TextRecognition);
        var stage = new PaddleOcrVlDocumentStage(model, "onnxruntime-cpu", PaddleOcrVlTask.TextRecognition,
            async (context, token) =>
            {
                await Task.Delay(2, token).ConfigureAwait(false);
                return input;
            },
            async (context, prepared, token) =>
            {
                await Task.Delay(2, token).ConfigureAwait(false);
                return new PaddleOcrVlGenerationResult(PaddleOcrVlTask.TextRecognition, "page-" + context.Page.PageIndex,
                    new[] { context.Page.PageIndex }, GenerationFinishReason.MaxTokens, 5, 0, sourceSha, TimeSpan.Zero);
            });

        PaddleDocumentPage[] pages = Enumerable.Range(0, 5)
            .Select(index => new PaddleDocumentPage("page-" + index, new VisualSize(640, 480), index)).ToArray();
        IReadOnlyList<PaddleDocumentPipelineResult> results = await new PaddleDocumentPipeline(new[] { stage })
            .RunManyConcurrentAsync(pages, maxDegreeOfParallelism: 2);

        CollectionAssert.AreEqual(pages.Select(value => value.PageIndex).ToArray(), results.Select(value => value.Page.PageIndex).ToArray());
        for (int index = 0; index < results.Count; index++)
        {
            PaddleDocumentVisionLanguageResult result = results[index].GetRequired<PaddleDocumentVisionLanguageResult>(PaddleDocumentModule.TextRecognition);
            Assert.AreEqual("page-" + index, result.Text);
            StringAssert.Contains(string.Join(";", result.Warnings), "missing-eos:sequence-may-be-truncated");
        }
    }

    [TestMethod]
    public async Task RejectsGenerationForAnotherVlTask()
    {
        PaddleOcrVlPreparedInput input = Input(PaddleOcrVlTask.FormulaRecognition, new string('c', 64));
        PaddleDocumentModelDescriptor model = Model(PaddleDocumentModule.FormulaRecognition);
        var stage = new PaddleOcrVlDocumentStage(model, "onnxruntime-cpu", PaddleOcrVlTask.FormulaRecognition,
            (context, token) => Task.FromResult(input),
            (context, prepared, token) => Task.FromResult(new PaddleOcrVlGenerationResult(
                PaddleOcrVlTask.ChartRecognition, "wrong-task", new[] { 1 }, GenerationFinishReason.EndOfSequence, 2, 0, new string('c', 64), TimeSpan.Zero)));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new PaddleDocumentPipeline(new[] { stage })
            .RunAsync(new PaddleDocumentPage("wrong-task", new VisualSize(320, 240))));
    }

    [TestMethod]
    public async Task RequiresAnInputIdentityWhenPreparedInputHasNone()
    {
        PaddleOcrVlPreparedInput input = Input(PaddleOcrVlTask.SealRecognition, sourceSha256: null);
        PaddleDocumentModelDescriptor model = Model(PaddleDocumentModule.SealTextDetection);
        var stage = new PaddleOcrVlDocumentStage(model, "onnxruntime-cpu", PaddleOcrVlTask.SealRecognition,
            (context, token) => Task.FromResult(input),
            (context, prepared, token) => Task.FromResult(new PaddleOcrVlGenerationResult(
                PaddleOcrVlTask.SealRecognition, "seal", new[] { 1 }, GenerationFinishReason.EndOfSequence, 2, 0, null, TimeSpan.Zero)));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new PaddleDocumentPipeline(new[] { stage })
            .RunAsync(new PaddleDocumentPage("missing-sha", new VisualSize(320, 240))));
    }

    private static PaddleOcrVlPreparedInput Input(PaddleOcrVlTask task, string? sourceSha256)
    {
        PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336), task);
        int length = checked(plan.PatchCount * 3 * 14 * 14);
        return new PaddleOcrVlPreparedInput(plan,
            new Tensor<float>(new TensorShape(plan.GridHeight, plan.GridWidth, 3, 14, 14), new float[length], TensorBufferOwnership.Transfer),
            sourceSha256);
    }

    private static PaddleDocumentModelDescriptor Model(PaddleDocumentModule module)
        => new("test/paddleocr-vl/1.6/" + module, "PaddleOCR-VL test", module, "https://example.invalid/paddleocr-vl", "onnx");

}
#endif
