using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
public sealed class PaddleOcrVlSpottingParserTests
{
    private const string Box = "<|LOC_0|><|LOC_0|><|LOC_1000|><|LOC_0|><|LOC_1000|><|LOC_1000|><|LOC_0|><|LOC_1000|>";
    private static readonly VisualSize Size = new(511, 333);

    [TestMethod]
    public void MatchesEveryUnmodifiedOfficialReferencePointAndTextIncludingRealEosOutput()
    {
        string path = Find("eng/models/paddle-ocr/verification/paddleocr-vl-spotting-official-reference-20261024.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement;
        Assert.AreEqual("deploysharp-paddleocr-vl-official-spotting-reference-v1", root.GetProperty("schemaVersion").GetString());
        Assert.AreEqual("63bf7d0ccf03225dd6be582e2b25628283e2eed0ad0b1c7864e086e3b95f923a", root.GetProperty("officialSource").GetProperty("sha256").GetString());
        Assert.AreEqual(Hash(File.ReadAllBytes(Find("eng/models/paddle-ocr/scripts/Export-PaddleOcrVlSpottingReference.py"))), root.GetProperty("generatorSha256").GetString());
        JsonElement provenance = root.GetProperty("generationReference");
        Assert.AreEqual(Hash(File.ReadAllBytes(Find(provenance.GetProperty("path").GetString()!))), provenance.GetProperty("sha256").GetString());
        int cases = 0, real = 0;
        foreach (JsonElement vector in root.GetProperty("cases").EnumerateArray())
        {
            string raw = vector.GetProperty("rawText").GetString()!;
            Assert.AreEqual(vector.GetProperty("rawTextSha256").GetString(), Hash(Encoding.UTF8.GetBytes(raw)));
            var size = new VisualSize(vector.GetProperty("sourceSize")[0].GetInt32(), vector.GetProperty("sourceSize")[1].GetInt32());
            PaddleOcrVlSpottingParseResult result = PaddleOcrVlSpottingParser.Parse(raw, GenerationFinishReason.EndOfSequence, size);
            Assert.IsTrue(result.Succeeded, vector.GetProperty("id").GetString() + ": " + string.Join(";", result.Diagnostics));
            Assert.AreEqual(raw, result.RawText);
            Assert.AreEqual(vector.GetProperty("text").GetString(), result.Text);
            Assert.IsFalse(result.PageCoordinatesAvailable);
            JsonElement[] texts = vector.GetProperty("texts").EnumerateArray().ToArray();
            JsonElement[] polygons = vector.GetProperty("sourcePolygons").EnumerateArray().ToArray();
            Assert.AreEqual(texts.Length, result.Regions.Count);
            for (int index = 0; index < texts.Length; index++)
            {
                Assert.AreEqual(texts[index].GetString(), result.Regions[index].Text);
                Assert.IsNull(result.Regions[index].PagePolygon);
                for (int point = 0; point < 4; point++)
                {
                    Assert.AreEqual(polygons[index][point][0].GetDouble(), result.Regions[index].SourcePolygon[point].X);
                    Assert.AreEqual(polygons[index][point][1].GetDouble(), result.Regions[index].SourcePolygon[point].Y);
                }
            }
            if (vector.GetProperty("origin").GetString() != "synthetic-parser-case") { real++; Assert.AreEqual(15, result.Regions.Count); }
            cases++;
        }
        Assert.AreEqual(9, cases); Assert.AreEqual(1, real);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("text without coordinates")]
    [DataRow("<|LOC_-1|>")]
    [DataRow("<|LOC_1001|>")]
    [DataRow("<|LOC_999999999999999999999999|>")]
    [DataRow("<|LOC_1.5|>")]
    [DataRow("<|LOC_١|>")]
    [DataRow("<|LOC_0|")]
    [DataRow("<|LOC_0|><|LOC_0|>")]
    [DataRow("<|UNKNOWN|>")]
    [DataRow("<|TEXT_START|>unclosed")]
    [DataRow("<|TEXT_START|><|TEXT_START|>nested<|TEXT_END|>")]
    [DataRow("<|TEXT_START|>text<|TEXT_END|>")]
    public void RejectsMalformedOutputWithoutPartialCoordinates(string invalid)
    {
        // Both a standalone failure and a malformed suffix after a valid region must be rejected.
        foreach (string raw in new[] { invalid, "valid" + Box + invalid + "unpaired" })
            AssertFailure(raw, PaddleOcrVlSpottingParseStatus.InvalidStructure);
    }

    [TestMethod]
    public void RejectsExtraBlockCoordinatesUnpairedBlocksAndUnexpectedInterleavedText()
    {
        foreach (string raw in new[]
        {
            "<|TEXT_START|>text<|TEXT_END|><|LOC_BEGIN|>" + Box + "<|LOC_1|><|LOC_END|>",
            "<|TEXT_START|>text<|TEXT_END|><|LOC_BEGIN|>" + Box,
            "<|LOC_BEGIN|>" + Box + "<|LOC_END|>",
            "<|TEXT_START|>a<|TEXT_END|><|TEXT_START|>b<|TEXT_END|><|LOC_BEGIN|>" + Box + "<|LOC_END|>",
            "<|TEXT_START|>a<|TEXT_END|>discarded<|LOC_BEGIN|>" + Box + "<|LOC_END|>",
            "a<|LOC_0|>discarded<|LOC_0|><|LOC_1000|><|LOC_0|><|LOC_1000|><|LOC_1000|><|LOC_0|><|LOC_1000|>",
            "a" + Box + "text without coordinates",
            "a" + Box + "<|LOC_1|>"
        }) AssertFailure(raw, PaddleOcrVlSpottingParseStatus.InvalidStructure);
    }

    [TestMethod]
    [DataRow("<|LOC_0|><|LOC_0|><|LOC_0|><|LOC_0|><|LOC_0|><|LOC_0|><|LOC_0|><|LOC_0|>")]
    [DataRow("<|LOC_0|><|LOC_0|><|LOC_1|><|LOC_1|><|LOC_2|><|LOC_2|><|LOC_3|><|LOC_3|>")]
    [DataRow("<|LOC_0|><|LOC_0|><|LOC_1000|><|LOC_1000|><|LOC_1000|><|LOC_0|><|LOC_0|><|LOC_1000|>")]
    [DataRow("<|LOC_0|><|LOC_0|><|LOC_1000|><|LOC_0|><|LOC_1000|><|LOC_1000|><|LOC_1000|><|LOC_0|>")]
    public void RejectsDegenerateDuplicateAndCrossingPolygons(string locations)
        => AssertFailure("text" + locations, PaddleOcrVlSpottingParseStatus.InvalidStructure);

    [TestMethod]
    public void NonEosGenerationNeverPublishesCompletedPolygons()
    {
        foreach (GenerationFinishReason reason in Enum.GetValues<GenerationFinishReason>().Where(value => value != GenerationFinishReason.EndOfSequence))
        {
            var result = PaddleOcrVlSpottingParser.Parse("text" + Box, reason, Size);
            Assert.AreEqual(PaddleOcrVlSpottingParseStatus.IncompleteGeneration, result.Status);
            Assert.AreEqual(0, result.Regions.Count); Assert.IsNull(result.Text);
        }
    }

    [TestMethod]
    public void EnforcesInputAndRegionBudgetsBeforePublishingOutputAndPropagatesCancellation()
    {
        var chars = PaddleOcrVlSpottingParser.Parse("text" + Box, GenerationFinishReason.EndOfSequence, Size, options: new(maximumInputCharacters: 4));
        Assert.AreEqual(PaddleOcrVlSpottingParseStatus.LimitExceeded, chars.Status);
        var regions = PaddleOcrVlSpottingParser.Parse("a" + Box + "b" + Box, GenerationFinishReason.EndOfSequence, Size, options: new(maximumRegions: 1));
        Assert.AreEqual(PaddleOcrVlSpottingParseStatus.LimitExceeded, regions.Status);
        Assert.AreEqual(0, regions.Regions.Count);
        var blocks = PaddleOcrVlSpottingParser.Parse("<|TEXT_START|>a<|TEXT_END|><|TEXT_START|>b<|TEXT_END|>", GenerationFinishReason.EndOfSequence, Size, options: new(maximumRegions: 1));
        Assert.AreEqual(PaddleOcrVlSpottingParseStatus.LimitExceeded, blocks.Status);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => PaddleOcrVlSpottingParser.Parse("a" + Box, GenerationFinishReason.EndOfSequence, Size, cancellationToken: cancellation.Token));
    }

    [TestMethod]
    public void UsesOriginalPixelsAndExplicitFrameWithoutReorderingOrRoundingPoints()
    {
        var plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(500, 500), PaddleOcrVlTask.TextSpotting);
        Assert.AreEqual(new VisualSize(1000, 1000), plan.PreparedSize);
        var frame = new PaddleOcrVlSourceFrame(new VisualSize(1000, 900), plan.SourceSize, 17, 29);
        string raw = "skew<|LOC_948|><|LOC_929|><|LOC_390|><|LOC_929|><|LOC_390|><|LOC_846|><|LOC_948|><|LOC_846|>";
        var result = PaddleOcrVlSpottingParser.Parse(raw, GenerationFinishReason.EndOfSequence, plan.SourceSize, frame);
        Assert.IsTrue(result.PageCoordinatesAvailable);
        Assert.AreEqual(474d, result.Regions[0].SourcePolygon[0].X);
        Assert.AreEqual(464.5d, result.Regions[0].SourcePolygon[0].Y);
        Assert.AreEqual(491d, result.Regions[0].PagePolygon![0].X);
        Assert.AreEqual(493.5d, result.Regions[0].PagePolygon![0].Y);
        CollectionAssert.AreEqual(new[] { 948, 929, 390, 929, 390, 846, 948, 846 }, result.Regions[0].QuantizedCoordinates.ToArray());
    }

    [TestMethod]
    public void ValidatesFrameDimensionsAndAttachesItWithoutCopyingPatchTensor()
    {
        var input = Input();
        var frame = new PaddleOcrVlSourceFrame(new VisualSize(700, 600), Size, 17, 29);
        var mapped = input.WithSourceFrame(frame);
        Assert.AreSame(input.PixelPatches, mapped.PixelPatches); Assert.AreSame(input.ImagePlan, mapped.ImagePlan);
        Assert.IsNull(input.SourceFrame); Assert.AreSame(frame, mapped.SourceFrame);
        Assert.ThrowsExactly<ArgumentException>(() => input.WithSourceFrame(new(new VisualSize(1000, 1000), new VisualSize(500, 500))));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlSourceFrame(Size, Size, 1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlSourceFrame(new VisualSize(int.MaxValue, int.MaxValue), Size, int.MaxValue, 0));
        Assert.ThrowsExactly<ArgumentException>(() => PaddleOcrVlSpottingParser.Parse("a" + Box, GenerationFinishReason.EndOfSequence, new VisualSize(500, 500), frame));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlSpottingParseOptions(maximumRegions: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlSpottingParseOptions(maximumInputCharacters: 0));
    }

    [TestMethod]
    public void ConcurrentParsingNeverSharesCoordinatesBetweenFrames()
    {
        Parallel.For(0, 32, index =>
        {
            var frame = new PaddleOcrVlSourceFrame(new VisualSize(1000, 1000), Size, index, index * 2);
            var result = PaddleOcrVlSpottingParser.Parse("text" + Box, GenerationFinishReason.EndOfSequence, Size, frame);
            Assert.AreEqual((double)index, result.Regions[0].PagePolygon![0].X);
            Assert.AreEqual((double)index * 2, result.Regions[0].PagePolygon![0].Y);
            Assert.AreEqual(0d, result.Regions[0].SourcePolygon[0].X);
        });
    }

    [TestMethod]
    public async Task DocumentStageExportsBothFramesAndKeepsSourceOnlyWhenMappingIsUnknown()
    {
        foreach (bool mapping in new[] { false, true })
        {
            PaddleOcrVlPreparedInput input = Input();
            if (mapping) input = input.WithSourceFrame(new(new VisualSize(1000, 900), Size, 17, 29));
            var stage = Stage(input, "<script>not markup</script>" + Box);
            var page = await new PaddleDocumentPipeline(new[] { stage }).RunAsync(new PaddleDocumentPage("page", new VisualSize(1000, 900), 7));
            var result = page.GetRequired<PaddleDocumentVisionLanguageResult>(PaddleDocumentModule.TextRecognition);
            Assert.IsTrue(result.CoordinatesAvailable); Assert.AreEqual(mapping, result.PageCoordinatesAvailable);
            Assert.AreSame(result.Spotting, result.ParseSpotting());
            var limited = result.ParseSpotting(new(maximumInputCharacters: 1));
            Assert.AreEqual(PaddleOcrVlSpottingParseStatus.LimitExceeded, limited.Status); Assert.IsTrue(result.Spotting!.Succeeded);
            using var json = JsonDocument.Parse(PaddleDocumentPipelineExport.ToJson(page));
            JsonElement payload = json.RootElement.GetProperty("results")[0].GetProperty("payload");
            Assert.IsTrue(payload.GetProperty("coordinatesAvailable").GetBoolean());
            Assert.AreEqual(mapping, payload.GetProperty("pageCoordinatesAvailable").GetBoolean());
            JsonElement parsed = payload.GetProperty("spotting");
            Assert.AreEqual(4, parsed.GetProperty("regions")[0].GetProperty("sourcePolygon").GetArrayLength());
            Assert.AreEqual(8, parsed.GetProperty("regions")[0].GetProperty("quantizedCoordinates").GetArrayLength());
            Assert.AreEqual(mapping ? JsonValueKind.Array : JsonValueKind.Null, parsed.GetProperty("regions")[0].GetProperty("pagePolygon").ValueKind);
            string markdown = PaddleDocumentPipelineExport.ToMarkdown(page);
            StringAssert.Contains(markdown, "Spotting: Succeeded"); StringAssert.Contains(markdown, "~~~text");
            Assert.AreEqual(mapping, markdown.Contains("page pixels:", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void RawAndIncompleteResultsNeverClaimPageCoordinates()
    {
        var metadata = Metadata(PaddleDocumentModule.TextRecognition);
        var raw = new PaddleDocumentVisionLanguageResult(PaddleOcrVlTask.TextSpotting, metadata, "text" + Box, new[] { 1 }, GenerationFinishReason.EndOfSequence);
        Assert.IsFalse(raw.CoordinatesAvailable); Assert.IsNull(raw.Spotting);
        Assert.AreEqual("spotting-source-geometry-unavailable", raw.SpatialOutputStatus);
        Assert.ThrowsExactly<InvalidOperationException>(() => raw.ParseSpotting());
        var input = Input().WithSourceFrame(new(new VisualSize(1000, 900), Size, 17, 29));
        var truncated = new PaddleDocumentVisionLanguageResult(PaddleOcrVlTask.TextSpotting, metadata, "text" + Box,
            new[] { 1 }, GenerationFinishReason.MaxTokens, null, -1, null, input.ImagePlan, input.SourceFrame);
        Assert.AreEqual(PaddleOcrVlSpottingParseStatus.IncompleteGeneration, truncated.Spotting!.Status);
        Assert.IsFalse(truncated.CoordinatesAvailable); Assert.IsFalse(truncated.PageCoordinatesAvailable);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DocumentStageObservesCancellationEvenWhenCallbacksIgnoreIt(bool duringGeneration)
    {
        using var cancellation = new CancellationTokenSource();
        bool generated = false;
        var stage = new PaddleOcrVlDocumentStage(Model(PaddleDocumentModule.TextRecognition), "test-generator", PaddleOcrVlTask.TextSpotting,
            (context, token) => { if (!duringGeneration) cancellation.Cancel(); return Task.FromResult(Input()); },
            (context, prepared, token) => { generated = true; cancellation.Cancel(); return Task.FromResult(Generation("text" + Box)); });
        await Assert.ThrowsAsync<OperationCanceledException>(() => new PaddleDocumentPipeline(new[] { stage }).RunAsync(new PaddleDocumentPage("page", Size), cancellation.Token));
        Assert.AreEqual(duringGeneration, generated);
    }

    [TestMethod]
    public async Task RegionalStageDoesNotGuessPageMappingFromRequestedBounds()
    {
        var metadata = Metadata(PaddleDocumentModule.LayoutDetection);
        var requested = new PaddleDocumentRegion("spotting", 1f, new RectangleF(17.2f, 29.7f, 510.5f, 332.1f));
        var layout = new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection, (context, token) => Task.FromResult<PaddleDocumentModuleResult>(
            new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection, metadata, new[] { requested })));
        foreach (bool mapping in new[] { false, true })
        {
            var input = Input();
            if (mapping) input = input.WithSourceFrame(new(new VisualSize(1000, 900), Size, 17, 29));
            var stage = new PaddleOcrVlRegionStage(Model(PaddleDocumentModule.TextRecognition), "test-generator", PaddleOcrVlTask.TextSpotting,
                context => new string('b', 64), (context, region, index, token) => Task.FromResult(input),
                (context, region, index, prepared, token) => Task.FromResult(Generation("text" + Box)));
            var page = await new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { stage, layout })
                .RunAsync(new PaddleDocumentPage("page", new VisualSize(1000, 900), 7));
            var result = page.GetRequired<PaddleDocumentVisionLanguagePageResult>(PaddleDocumentModule.TextRecognition);
            Assert.IsTrue(result.CoordinatesAvailable); Assert.AreEqual(mapping, result.PageCoordinatesAvailable);
            Assert.AreSame(requested, result.Executions[0].Result!.SourceRegion);
            Assert.AreEqual(mapping, result.Executions[0].Result!.Spotting!.PageCoordinatesAvailable);
            var routed = new PaddleDocumentVisionLanguageRoutedPageResult(Metadata(PaddleDocumentModule.VisionLanguageParsing), result.Executions);
            Assert.IsTrue(routed.CoordinatesAvailable); Assert.AreEqual(mapping, routed.PageCoordinatesAvailable);
        }
    }

    [TestMethod]
    public async Task RejectsMismatchedPreparationBeforeGenerationAndCrossImageGenerationAfterward()
    {
        foreach (bool taskMismatch in new[] { false, true })
        {
            var input = Input(taskMismatch ? PaddleOcrVlTask.TextRecognition : PaddleOcrVlTask.TextSpotting);
            if (!taskMismatch) input = input.WithSourceFrame(new(new VisualSize(700, 600), Size));
            bool generated = false;
            var stage = new PaddleOcrVlDocumentStage(Model(PaddleDocumentModule.TextRecognition), "test-generator", PaddleOcrVlTask.TextSpotting,
                (context, token) => Task.FromResult(input), (context, prepared, token) => { generated = true; return Task.FromResult(Generation("a" + Box)); });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new PaddleDocumentPipeline(new[] { stage }).RunAsync(new PaddleDocumentPage("page", new VisualSize(1000, 900), 7)));
            Assert.IsFalse(generated);
        }
        var mismatch = new PaddleOcrVlDocumentStage(Model(PaddleDocumentModule.TextRecognition), "test-generator", PaddleOcrVlTask.TextSpotting,
            (context, token) => Task.FromResult(Input()), (context, prepared, token) => Task.FromResult(new PaddleOcrVlGenerationResult(
                PaddleOcrVlTask.TextSpotting, "a" + Box, new[] { 2 }, GenerationFinishReason.EndOfSequence, 4, 0, new string('f', 64), TimeSpan.Zero)));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new PaddleDocumentPipeline(new[] { mismatch }).RunAsync(new PaddleDocumentPage("page", Size)));
    }

    private static PaddleOcrVlPreparedInput Input(PaddleOcrVlTask task = PaddleOcrVlTask.TextSpotting)
    {
        var plan = PaddleOcrVlInputPlanning.PlanOfficialV16(Size, task, minimumPixels: 784, maximumPixels: 3136);
        return new(plan, new Tensor<float>(new TensorShape(plan.GridHeight, plan.GridWidth, 3, 14, 14), new float[plan.PatchCount * 3 * 14 * 14], TensorBufferOwnership.Transfer), new string('a', 64));
    }

    private static PaddleOcrVlDocumentStage Stage(PaddleOcrVlPreparedInput input, string text)
        => new(Model(PaddleDocumentModule.TextRecognition), "test-generator", PaddleOcrVlTask.TextSpotting,
            (context, token) => Task.FromResult(input), (context, prepared, token) => Task.FromResult(Generation(text)));

    private static PaddleOcrVlGenerationResult Generation(string text)
        => new(PaddleOcrVlTask.TextSpotting, text, new[] { 1, 2 }, GenerationFinishReason.EndOfSequence, 4, 0, new string('a', 64), TimeSpan.Zero);

    private static PaddleDocumentModelDescriptor Model(PaddleDocumentModule module)
        => new("paddleocr-vl/1.6", "PaddleOCR-VL", module, "https://example.invalid/model", "onnx");
    private static PaddleDocumentResultMetadata Metadata(PaddleDocumentModule module)
        => new(Model(module), "test-generator", TimeSpan.Zero, new string('b', 64), 7);
    private static void AssertFailure(string raw, PaddleOcrVlSpottingParseStatus status)
    {
        var result = PaddleOcrVlSpottingParser.Parse(raw, GenerationFinishReason.EndOfSequence, Size);
        Assert.AreEqual(status, result.Status, raw); Assert.IsFalse(result.Succeeded); Assert.IsFalse(result.PageCoordinatesAvailable);
        Assert.AreEqual(0, result.Regions.Count); Assert.IsNull(result.Text); Assert.AreEqual(raw, result.RawText);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Find(string relative)
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current != null; current = current.Parent)
        {
            string path = Path.Combine(current.FullName, relative);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Pinned Spotting reference is missing.", relative);
    }
}
