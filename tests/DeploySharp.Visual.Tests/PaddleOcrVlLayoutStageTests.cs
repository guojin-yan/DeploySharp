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
public sealed class PaddleOcrVlLayoutStageTests
{
    [TestMethod]
    [DataRow("Text", PaddleOcrVlTask.TextRecognition)]
    [DataRow("Title", PaddleOcrVlTask.TextRecognition)]
    [DataRow("List", PaddleOcrVlTask.TextRecognition)]
    [DataRow("paragraph_title", PaddleOcrVlTask.TextRecognition)]
    [DataRow("reference_content", PaddleOcrVlTask.TextRecognition)]
    [DataRow("chart_title", PaddleOcrVlTask.TextRecognition)]
    [DataRow(" Table ", PaddleOcrVlTask.TableRecognition)]
    [DataRow("FORMULA", PaddleOcrVlTask.FormulaRecognition)]
    [DataRow("chart", PaddleOcrVlTask.ChartRecognition)]
    [DataRow("seal", PaddleOcrVlTask.SealRecognition)]
    public void ExistingLayoutLabelsSelectTheIntendedTask(string category, PaddleOcrVlTask expected)
    {
        Assert.IsTrue(PaddleOcrVlTaskRouter.CreatePpStructure().TryGetTask(category, out PaddleOcrVlTask actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void RouterCopiesMappingsAndKeepsExplicitSkipsSeparateFromUnknownFallback()
    {
        var mappings = new Dictionary<string, PaddleOcrVlTask?> { [" spotting "] = PaddleOcrVlTask.TextSpotting, ["table"] = null };
        var router = PaddleOcrVlTaskRouter.CreatePpStructure(mappings, PaddleOcrVlTask.TextRecognition);
        mappings[" spotting "] = null;
        Assert.IsTrue(router.TryGetTask("SPOTTING", out PaddleOcrVlTask task));
        Assert.AreEqual(PaddleOcrVlTask.TextSpotting, task);
        Assert.IsFalse(router.TryGetTask("table", out _));
        Assert.IsFalse(router.TryGetTask("image", out _));
        Assert.IsFalse(router.TryGetTask("header_image", out _));
        Assert.IsFalse(router.TryGetTask("figure", out _));
        Assert.IsTrue(router.TryGetTask("custom", out task));
        Assert.AreEqual(PaddleOcrVlTask.TextRecognition, task);
        Assert.IsFalse(PaddleOcrVlTaskRouter.CreatePpStructure().TryGetTask("custom", out _));
        Assert.ThrowsExactly<ArgumentException>(() => new PaddleOcrVlTaskRouter(new[] {
            new KeyValuePair<string, PaddleOcrVlTask?>("text", task), new KeyValuePair<string, PaddleOcrVlTask?>(" Text ", task) }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlTaskRouter(new Dictionary<string, PaddleOcrVlTask?> { ["bad"] = (PaddleOcrVlTask)999 }));
    }

    [TestMethod]
    public void ImageFallbackOptionsRejectInvalidBudgets()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlImageFallbackOptions((PaddleOcrVlTask)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlImageFallbackOptions(minimumUncoveredAreaRatio: double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlImageFallbackOptions(minimumUncoveredAreaRatio: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlImageFallbackOptions(minimumUncoveredAreaRatio: 1.1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlImageFallbackOptions(maximumRegionsPerPage: 0));
    }

    [TestMethod]
    public async Task ImageFallbackSelectsOnlyUncoveredImageAndRetainsSourceIndex()
    {
        var regions = new[]
        {
            new PaddleDocumentRegion("image", 1, new RectangleF(0, 0, 100, 100)),
            new PaddleDocumentRegion("text", 1, new RectangleF(0, 0, 10, 10)),
            new PaddleDocumentRegion("image", 1, new RectangleF(150, 0, 100, 100)),
            new PaddleDocumentRegion("table", 1, new RectangleF(150, 0, 100, 100))
        };
        var prepared = new List<int>();
        var stage = Stage((context, region, index, task, token) => { prepared.Add(index); return Task.FromResult(Input(task, index)); },
            (context, region, index, input, token) => Task.FromResult(Generation(input)),
            router: PaddleOcrVlTaskRouter.CreatePpStructure(imageFallback: new PaddleOcrVlImageFallbackOptions()));
        var result = (await Run(stage, regions)).GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        CollectionAssert.AreEqual(new[] { 0, 1, 3 }, prepared);
        Assert.AreEqual(PaddleOcrVlTask.TextRecognition, result.Executions[0].Task);
        Assert.IsTrue(result.Executions[2].Skipped);
        CollectionAssert.Contains(result.Warnings.ToArray(), "vl-image-fallback:0");
        CollectionAssert.DoesNotContain(result.Warnings.ToArray(), "vl-image-fallback:2");
    }

    [TestMethod]
    public async Task ImageFallbackUsesUnionCoverageAndStablePageBudget()
    {
        var regions = new[]
        {
            new PaddleDocumentRegion("image", 1, new RectangleF(0, 0, 100, 100)),
            new PaddleDocumentRegion("text", 1, new RectangleF(0, 0, 50, 100)),
            new PaddleDocumentRegion("table", 1, new RectangleF(50, 0, 50, 100)),
            new PaddleDocumentRegion("image", 1, new RectangleF(120, 0, 40, 40)),
            new PaddleDocumentRegion("image", 1, new RectangleF(170, 0, 40, 40)),
            new PaddleDocumentRegion("image", 1, new RectangleF(220, 0, 20, 20))
        };
        var stage = Stage((context, region, index, task, token) => Task.FromResult(Input(task, index)),
            (context, region, index, input, token) => Task.FromResult(Generation(input)),
            router: PaddleOcrVlTaskRouter.CreatePpStructure(imageFallback: new PaddleOcrVlImageFallbackOptions(maximumRegionsPerPage: 2)));
        var result = (await Run(stage, regions)).GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        Assert.IsTrue(result.Executions[0].Skipped);
        Assert.IsTrue(result.Executions[5].Skipped);
        Assert.AreEqual(PaddleOcrVlTask.TextRecognition, result.Executions[3].Task);
        Assert.AreEqual(PaddleOcrVlTask.TextRecognition, result.Executions[4].Task);
        CollectionAssert.Contains(result.Warnings.ToArray(), "vl-image-fallback:3");
        CollectionAssert.Contains(result.Warnings.ToArray(), "vl-image-fallback:4");
    }

    [TestMethod]
    public async Task ExplicitImageTaskTakesPriorityOverFallbackBudget()
    {
        var regions = new[]
        {
            new PaddleDocumentRegion("image", 1, new RectangleF(0, 0, 20, 20)),
            new PaddleDocumentRegion("image", 1, new RectangleF(30, 0, 20, 20))
        };
        var router = PaddleOcrVlTaskRouter.CreatePpStructure(
            new Dictionary<string, PaddleOcrVlTask?> { ["image"] = PaddleOcrVlTask.TextRecognition },
            imageFallback: new PaddleOcrVlImageFallbackOptions(maximumRegionsPerPage: 1));
        var stage = Stage((context, region, index, task, token) => Task.FromResult(Input(task, index)),
            (context, region, index, input, token) => Task.FromResult(Generation(input)), router: router);
        var result = (await Run(stage, regions)).GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        Assert.AreEqual(2, result.SucceededCount);
        Assert.IsFalse(result.Warnings.Any(warning => warning.StartsWith("vl-image-fallback:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RecordedFunsdDetectorBoxesSelectPageThreeButNotOverlappingPageSeven()
    {
        var router = PaddleOcrVlTaskRouter.CreatePpStructure(imageFallback: new PaddleOcrVlImageFallbackOptions());
        var stage = Stage((context, region, index, task, token) => Task.FromResult(Input(task, index)),
            (context, region, index, input, token) => Task.FromResult(Generation(input)), router: router);
        var pageThree = new[]
        {
            new PaddleDocumentRegion("image", 0.7381599f, new RectangleF(72.54758f, 152.612f, 590.8673f, 693.9916f)),
            new PaddleDocumentRegion("number", 0.4570042f, new RectangleF(43.389698f, 75.13815f, 8.4928055f, 8.508446f)),
            new PaddleDocumentRegion("text", 0.35140854f, new RectangleF(661.2267f, 710.3175f, 21.45459f, 103.797f)),
            new PaddleDocumentRegion("aside_text", 0.30272943f, new RectangleF(661.2267f, 710.3175f, 21.45459f, 103.797f))
        };
        var pageSeven = new[]
        {
            new PaddleDocumentRegion("table", 1, new RectangleF(15.869151f, 24.340034f, 722.5322f, 971.8575f)),
            new PaddleDocumentRegion("number", 1, new RectangleF(6.4226384f, 30.69006f, 5.6686864f, 6.2612f)),
            new PaddleDocumentRegion("image", 1, new RectangleF(15.869151f, 24.340034f, 722.5322f, 971.8575f)),
            new PaddleDocumentRegion("table_title", 1, new RectangleF(42.988083f, 167.6665f, 315.80798f, 15.162933f))
        };
        var three = (await Run(stage, pageThree, pageSize: new VisualSize(754, 1000))).GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        var seven = (await Run(stage, pageSeven, pageSize: new VisualSize(754, 1000))).GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        CollectionAssert.Contains(three.Warnings.ToArray(), "vl-image-fallback:0");
        Assert.AreEqual(PaddleOcrVlTask.TextRecognition, three.Executions[0].Task);
        Assert.IsTrue(seven.Executions[2].Skipped);
        Assert.IsFalse(seven.Warnings.Any(warning => warning.StartsWith("vl-image-fallback:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task MixedPagePreservesEveryIndexAndCorrectTaskSpecificProvenanceAndExports()
    {
        string[] categories = { "table", "figure", "formula", "text", "chart", "spotting", "seal" };
        var prepared = new List<int>();
        var stage = Stage((context, region, index, task, token) => { prepared.Add(index); return Task.FromResult(Input(task, index)); },
            (context, region, index, input, token) => Task.FromResult(Generation(input, "<fcel>region-" + index)));
        PaddleDocumentPipelineResult page = await Run(stage, categories);
        var result = page.GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        Assert.AreEqual(6, result.SucceededCount);
        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(1, result.SkippedCount);
        CollectionAssert.AreEqual(new[] { 0, 2, 3, 4, 5, 6 }, prepared);
        for (int index = 0; index < categories.Length; index++)
        {
            var execution = result.Executions[index];
            Assert.AreEqual(index, execution.RegionIndex);
            Assert.AreEqual(categories[index], execution.SourceRegion.Category);
            Assert.AreEqual(new RectangleF(index * 20, 4, 15, 12), execution.SourceRegion.Bounds);
            if (index == 1)
            {
                Assert.AreEqual(PaddleDocumentVisionLanguageRegionStatus.Skipped, execution.Status);
                Assert.IsNull(execution.Task);
                Assert.IsNull(execution.Result);
                continue;
            }
            Assert.AreEqual(PaddleDocumentVisionLanguageResult.ModuleForTask(execution.Task!.Value), execution.Result!.Metadata.Model.Module);
            Assert.AreEqual("test/vl", execution.Result.Metadata.Model.ModelId);
            Assert.AreEqual("bundle.onnx", execution.Result.Metadata.Model.ArtifactPath);
            Assert.AreEqual(Hash(15), execution.Result.Metadata.Model.Sha256);
            Assert.AreEqual(7, execution.Result.Metadata.PageIndex);
            Assert.AreEqual(Hash(index), execution.Result.Metadata.InputSha256);
        }
        Assert.AreEqual(Hash(14), result.Metadata.InputSha256);
        Assert.IsFalse(result.CoordinatesAvailable);
        using var json = JsonDocument.Parse(PaddleDocumentPipelineExport.ToJson(page));
        JsonElement payload = json.RootElement.GetProperty("results")[1].GetProperty("payload");
        Assert.AreEqual(1, payload.GetProperty("skippedCount").GetInt32());
        Assert.AreEqual("Skipped", payload.GetProperty("regions")[1].GetProperty("status").GetString());
        Assert.AreEqual("FormulaRecognition", payload.GetProperty("regions")[2].GetProperty("task").GetString());
        Assert.AreEqual("FormulaRecognition", payload.GetProperty("regions")[2].GetProperty("result").GetProperty("module").GetString());
        string markdown = PaddleDocumentPipelineExport.ToMarkdown(page);
        StringAssert.Contains(markdown, "success/failure/skipped: 6/0/1");
        StringAssert.Contains(markdown, "~~~text" + Environment.NewLine + "<fcel>region-2");
    }

    [TestMethod]
    public async Task FailureAndSkipRemainDistinctAndLaterRegionsContinue()
    {
        var stage = Stage((context, region, index, task, token) => index == 0
                ? throw new InvalidOperationException("crop failed") : Task.FromResult(Input(task, index)),
            (context, region, index, input, token) => Task.FromResult(Generation(input)), continueOnRegionFailure: true);
        var result = (await Run(stage, new[] { "table", "figure", "formula" }))
            .GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        Assert.AreEqual(1, result.SucceededCount);
        Assert.AreEqual(1, result.FailedCount);
        Assert.AreEqual(1, result.SkippedCount);
        Assert.AreEqual(PaddleOcrVlTask.TableRecognition, result.Executions[0].Task);
        Assert.AreEqual(PaddleDocumentVisionLanguageRegionStatus.Failed, result.Executions[0].Status);
        Assert.AreEqual(2, result.Executions[2].Result!.RegionIndex);
        StringAssert.Contains(result.Executions[0].FailureMessage!, "crop failed");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Run(Stage(
            (context, region, index, task, token) => throw new InvalidOperationException("fail fast"),
            (context, region, index, input, token) => Task.FromResult(Generation(input))), new[] { "text" }));
    }

    [TestMethod]
    public async Task TaskAwareFallbackRetainsTruncationWarning()
    {
        int calls = 0;
        var stage = Stage((context, region, index, task, token) => Task.FromResult(Input(task, index)),
            (context, region, index, input, token) => throw new InvalidOperationException("primary"),
            fallback: (context, region, index, task, error, token) =>
            {
                Assert.AreEqual(PaddleOcrVlTask.FormulaRecognition, task);
                calls++;
                return Task.FromResult(Generation(Input(task, index), "partial", GenerationFinishReason.MaxTokens));
            });
        var result = (await Run(stage, new[] { "formula" }))
            .GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing).Executions[0].Result!;
        Assert.AreEqual(1, calls);
        CollectionAssert.Contains(result.Warnings.ToArray(), "vl-region-fallback-used");
        CollectionAssert.Contains(result.Warnings.ToArray(), "missing-eos:sequence-may-be-truncated");
    }

    [TestMethod]
    [DataRow("prepared-task")]
    [DataRow("generated-task")]
    [DataRow("source")]
    public async Task WrongTaskOrRegionIdentityIsRejected(string mismatch)
    {
        bool generated = false;
        var stage = Stage((context, region, index, task, token) => Task.FromResult(Input(mismatch == "prepared-task" ? PaddleOcrVlTask.TableRecognition : task, index)),
            (context, region, index, input, token) =>
            {
                generated = true;
                var other = Input(mismatch == "generated-task" ? PaddleOcrVlTask.TableRecognition : input.ImagePlan.Task, mismatch == "source" ? 1 : index);
                return Task.FromResult(Generation(other));
            });
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Run(stage, new[] { "text" }));
        Assert.AreEqual(mismatch != "prepared-task", generated);
    }

    [TestMethod]
    [DataRow("prepare")]
    [DataRow("generate")]
    [DataRow("fallback")]
    [DataRow("identity")]
    public async Task IgnoredCancellationNeverBecomesAnIsolatedFailure(string point)
    {
        using var cancellation = new CancellationTokenSource();
        int fallbackCalls = 0;
        var stage = new PaddleOcrVlLayoutStage(Model(PaddleDocumentModule.VisionLanguageParsing), "test", Router(), _ => Hash(14),
            (context, region, index, task, token) => { if (point == "prepare") cancellation.Cancel(); return Task.FromResult(Input(task, index)); },
            (context, region, index, input, token) =>
            {
                if (point == "generate") cancellation.Cancel();
                if (point == "fallback" || point == "generate") throw new InvalidOperationException("primary failed");
                return Task.FromResult(Generation(input));
            }, inputIdentity: (context, region, index, input) => { if (point == "identity") cancellation.Cancel(); return input.SourceSha256!; },
            fallback: (context, region, index, task, error, token) => { fallbackCalls++; cancellation.Cancel(); return Task.FromResult(Generation(Input(task, index))); },
            continueOnRegionFailure: true);
        await Assert.ThrowsAsync<OperationCanceledException>(() => Run(stage, new[] { "figure", "text" }, cancellation.Token));
        Assert.AreEqual(point == "fallback" ? 1 : 0, fallbackCalls);
    }

    [TestMethod]
    public async Task EmptyAndUnmappedLayoutsNeverPrepareOrGenerate()
    {
        var stage = Stage((context, region, index, task, token) => throw new InvalidOperationException("not needed"),
            (context, region, index, input, token) => throw new InvalidOperationException("not needed"));
        foreach (string[] categories in new[] { Array.Empty<string>(), new[] { "image", "unmapped" } })
        {
            var result = (await Run(stage, categories)).GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
            Assert.AreEqual(categories.Length, result.SkippedCount);
            Assert.AreEqual(0, result.SucceededCount + result.FailedCount);
        }
    }

    [TestMethod]
    public void RoutedResultRejectsMissingTaskNoncontiguousIndicesAndWrongPage()
    {
        var region = new PaddleDocumentRegion("text", 1, new RectangleF(0, 0, 10, 10));
        var metadata = new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.VisionLanguageParsing), "test", TimeSpan.Zero, Hash(14), 7);
        Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentVisionLanguageRoutedPageResult(metadata,
            new[] { new PaddleDocumentVisionLanguageRegionExecution(0, region, "Error", "legacy missing task") }));
        Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentVisionLanguageRoutedPageResult(metadata,
            new[] { PaddleDocumentVisionLanguageRegionExecution.CreateSkipped(1, region, "skip") }));
        var wrongPage = new PaddleDocumentVisionLanguageResult(PaddleOcrVlTask.TextRecognition,
            new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.TextRecognition), "test", TimeSpan.Zero, Hash(0), 3),
            "wrong page", new[] { 1 }, GenerationFinishReason.EndOfSequence, regionIndex: 0, sourceRegion: region);
        Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentVisionLanguageRoutedPageResult(metadata,
            new[] { new PaddleDocumentVisionLanguageRegionExecution(0, region, wrongPage) }));
    }

    [TestMethod]
    public async Task ReadingOrderKeepsCallbacksAndSourceIndicesWhileExportsIncludeSkipsAndFailures()
    {
        var prepared = new List<int>();
        var stage = Stage((context, region, index, task, token) => { prepared.Add(index); return Task.FromResult(Input(task, index)); },
            (context, region, index, input, token) => index == 2 ? throw new InvalidOperationException("formula failed") : Task.FromResult(Generation(input, "region-" + index)),
            continueOnRegionFailure: true, readingOrderOptions: new(rightToLeft: true));
        PaddleDocumentPipelineResult page = await Run(stage, new[] { "text", "image", "formula", "text" });
        var result = page.GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
        CollectionAssert.AreEqual(new[] { 0, 2, 3 }, prepared);
        CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, result.ReadingOrder.ToArray());
        for (int index = 0; index < result.Executions.Count; index++)
        {
            Assert.AreEqual(index, result.Executions[index].RegionIndex);
            Assert.AreSame(result.Executions[3 - index], result.ReadingExecutions[index]);
        }
        Assert.IsTrue(result.ReadingExecutions[2].Skipped);
        Assert.AreEqual(PaddleDocumentVisionLanguageRegionStatus.Failed, result.ReadingExecutions[1].Status);
        using var json = JsonDocument.Parse(PaddleDocumentPipelineExport.ToJson(page));
        JsonElement payload = json.RootElement.GetProperty("results")[1].GetProperty("payload");
        CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, payload.GetProperty("readingOrder").EnumerateArray().Select(value => value.GetInt32()).ToArray());
        Assert.AreEqual(0, payload.GetProperty("regions")[0].GetProperty("regionIndex").GetInt32());
        string markdown = PaddleDocumentPipelineExport.ToMarkdown(page);
        StringAssert.Contains(markdown, "Reading order: 3, 2, 1, 0");
        Assert.IsTrue(markdown.IndexOf("- Region 3 /", StringComparison.Ordinal) < markdown.IndexOf("- Region 2 /", StringComparison.Ordinal));
        Assert.IsTrue(markdown.IndexOf("- Region 2 /", StringComparison.Ordinal) < markdown.IndexOf("- Region 1 /", StringComparison.Ordinal));
        Assert.IsTrue(markdown.IndexOf("- Region 1 /", StringComparison.Ordinal) < markdown.IndexOf("- Region 0 /", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CustomReadingOrderIsCopiedAndMustIncludeEveryRegionExactlyOnce()
    {
        var metadata = new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.VisionLanguageParsing), "test", TimeSpan.Zero, Hash(14));
        var executions = Enumerable.Range(0, 3).Select(index => PaddleDocumentVisionLanguageRegionExecution.CreateSkipped(index,
            new PaddleDocumentRegion("image", 1, new RectangleF(index * 20, 4, 15, 12)), "skip")).ToArray();
        int[] order = { 2, 0, 1 };
        var result = new PaddleDocumentVisionLanguageRoutedPageResult(metadata, executions, readingOrder: order);
        order[0] = 0;
        CollectionAssert.AreEqual(new[] { 2, 0, 1 }, result.ReadingOrder.ToArray());
        Assert.AreSame(executions[2], result.ReadingExecutions[0]);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<int>)result.ReadingOrder)[0] = 0);
        foreach (int[] invalid in new[] { new[] { 1, 0 }, new[] { 0, 0, 2 }, new[] { 0, 1, 3 }, new[] { -1, 1, 2 } })
            Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentVisionLanguageRoutedPageResult(metadata, executions, readingOrder: invalid));
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, new PaddleDocumentVisionLanguageRoutedPageResult(metadata, executions).ReadingOrder.ToArray());
    }

    private static PaddleOcrVlTaskRouter Router() => PaddleOcrVlTaskRouter.CreatePpStructure(
        new Dictionary<string, PaddleOcrVlTask?> { ["spotting"] = PaddleOcrVlTask.TextSpotting });
    private static PaddleOcrVlLayoutStage Stage(
        Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
        Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
        Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
        bool continueOnRegionFailure = false, PaddleDocumentReadingOrderOptions? readingOrderOptions = null,
        PaddleOcrVlTaskRouter? router = null)
        => new(Model(PaddleDocumentModule.VisionLanguageParsing), "test", router ?? Router(), _ => Hash(14), prepare, generate,
            fallback: fallback, continueOnRegionFailure: continueOnRegionFailure, readingOrderOptions: readingOrderOptions);

    private static Task<PaddleDocumentPipelineResult> Run(PaddleOcrVlLayoutStage stage, string[] categories, CancellationToken token = default)
        => Run(stage, categories.Select((category, index) => new PaddleDocumentRegion(category, 1,
            new RectangleF(index * 20, 4, 15, 12))).ToArray(), token);

    private static Task<PaddleDocumentPipelineResult> Run(PaddleOcrVlLayoutStage stage, PaddleDocumentRegion[] regions,
        CancellationToken token = default, VisualSize? pageSize = null)
    {
        var layout = new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection, (context, cancellation) => Task.FromResult<PaddleDocumentModuleResult>(
            new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
                new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.LayoutDetection), "manual", TimeSpan.Zero, Hash(14), context.Page.PageIndex),
                regions)));
        return new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { stage, layout }).RunAsync(
            new PaddleDocumentPage("page", pageSize ?? new VisualSize(300, 200), 7), token);
    }
    private static PaddleOcrVlPreparedInput Input(PaddleOcrVlTask task, int index)
    {
        var plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(28, 28), task, minimumPixels: 784, maximumPixels: 784);
        return new PaddleOcrVlPreparedInput(plan, new Tensor<float>(new TensorShape(plan.GridHeight, plan.GridWidth, 3, 14, 14),
            new float[plan.PatchCount * 3 * 14 * 14], TensorBufferOwnership.Transfer), Hash(index));
    }
    private static PaddleOcrVlGenerationResult Generation(PaddleOcrVlPreparedInput input, string text = "raw", GenerationFinishReason reason = GenerationFinishReason.EndOfSequence)
        => new(input.ImagePlan.Task, text, new[] { 1 }, reason, 4, 0, input.SourceSha256, TimeSpan.Zero);
    private static string Hash(int index) => index.ToString("x2", System.Globalization.CultureInfo.InvariantCulture).PadLeft(64, '0');
    private static PaddleDocumentModelDescriptor Model(PaddleDocumentModule module)
        => new("test/vl", "VL test", module, "https://example.invalid/vl", "onnx", PaddleDocumentArtifactStatus.OnnxConverted, "bundle.onnx", Hash(15));
}
