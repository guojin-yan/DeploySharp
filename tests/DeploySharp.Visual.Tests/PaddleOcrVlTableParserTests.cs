using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlTableParserTests
{
    private const string ReferencePath = "tests/assets/paddleocr-vl/table-reference.json";

    [TestMethod]
    public void LogicalCellsAndHtmlMatchPinnedUnmodifiedOfficialConverterIncludingTwoRealGenerations()
    {
        using JsonDocument report = JsonDocument.Parse(File.ReadAllBytes(Find(ReferencePath)));
        Assert.AreEqual("4c968d30b9d0d4a693d9eaaef0881c20f8d7d0e0", report.RootElement.GetProperty("upstream").GetProperty("revision").GetString());
        Assert.AreEqual("63bf7d0ccf03225dd6be582e2b25628283e2eed0ad0b1c7864e086e3b95f923a", report.RootElement.GetProperty("upstream").GetProperty("sourceSha256").GetString());
        Assert.AreEqual(report.RootElement.GetProperty("inputCorpusSha256").GetString(), Sha(File.ReadAllBytes(Find("tests/assets/paddleocr-vl/table-inputs.json"))));
        Assert.AreEqual(report.RootElement.GetProperty("scriptSha256").GetString(), Sha(File.ReadAllBytes(Find("eng/models/paddle-ocr/scripts/Export-PaddleOcrVlTableReference.py"))));
        foreach (JsonElement source in report.RootElement.GetProperty("generationReports").EnumerateArray())
            Assert.AreEqual(source.GetProperty("sha256").GetString(), Sha(File.ReadAllBytes(Find(source.GetProperty("path").GetString()!))));
        int count = 0, realCount = 0;
        foreach (JsonElement vector in report.RootElement.GetProperty("cases").EnumerateArray())
        {
            string id = vector.GetProperty("id").GetString()!;
            string raw = vector.GetProperty("rawText").GetString()!;
            Assert.AreEqual(vector.GetProperty("rawTextSha256").GetString(), Sha(Encoding.UTF8.GetBytes(raw)), id);
            bool repair = vector.GetProperty("requiresRepair").GetBoolean();
            PaddleOcrVlTableParseResult strict = PaddleOcrVlTableParser.Parse(raw, GenerationFinishReason.EndOfSequence);
            Assert.AreEqual(!repair, strict.Succeeded, id);
            if (repair)
            {
                Assert.AreEqual(PaddleOcrVlTableParseStatus.InvalidStructure, strict.Status, id);
                Assert.IsNull(strict.Html, id);
                Assert.AreEqual(0, strict.Cells.Count, id);
            }
            PaddleOcrVlTableParseResult result = PaddleOcrVlTableParser.Parse(raw, GenerationFinishReason.EndOfSequence,
                new PaddleOcrVlTableParseOptions(PaddleOcrVlTableRepairMode.OfficialRowWidth));
            Assert.IsTrue(result.Succeeded, id + ": " + string.Join(";", result.Diagnostics));
            Assert.AreEqual(repair ? PaddleOcrVlTableParseStatus.Repaired : PaddleOcrVlTableParseStatus.Succeeded, result.Status, id);
            Assert.AreSame(raw, result.RawText, id);
            Assert.AreEqual(vector.GetProperty("rowCount").GetInt32(), result.RowCount, id);
            Assert.AreEqual(vector.GetProperty("columnCount").GetInt32(), result.ColumnCount, id);
            Assert.AreEqual(vector.GetProperty("html").GetString(), result.Html, id);
            JsonElement[] expected = vector.GetProperty("cells").EnumerateArray().ToArray();
            Assert.AreEqual(expected.Length, result.Cells.Count, id);
            for (int index = 0; index < expected.Length; index++)
            {
                PaddleOcrVlTableCell cell = result.Cells[index];
                Assert.AreEqual(expected[index].GetProperty("row").GetInt32(), cell.Row, id);
                Assert.AreEqual(expected[index].GetProperty("column").GetInt32(), cell.Column, id);
                Assert.AreEqual(expected[index].GetProperty("rowSpan").GetInt32(), cell.RowSpan, id);
                Assert.AreEqual(expected[index].GetProperty("columnSpan").GetInt32(), cell.ColumnSpan, id);
                Assert.AreEqual(expected[index].GetProperty("text").GetString(), cell.Text, id);
                Assert.AreEqual(expected[index].GetProperty("isEmpty").GetBoolean(), cell.IsEmpty, id);
            }
            if (vector.GetProperty("origin").GetString() != "synthetic-parser-case") realCount++;
            count++;
        }
        Assert.AreEqual(14, count);
        Assert.AreEqual(2, realCount);
    }

    [TestMethod]
    [DataRow("", "empty-table-or-row")]
    [DataRow("<nl>", "empty-table-or-row")]
    [DataRow("<table><tr><td>A</td></tr></table>", "text-before-first-marker")]
    [DataRow("prefix<fcel>A<nl>", "text-before-first-marker")]
    [DataRow("<fcel><nl>", "filled-cell-without-text")]
    [DataRow("<ecel>not empty<nl>", "text-outside-filled-cell")]
    [DataRow("<fcel>A<lcel>discarded text<nl>", "text-outside-filled-cell")]
    [DataRow("<fcel>A<nl>outside", "text-outside-filled-cell")]
    [DataRow("<lcel><nl>", "orphan-continuation")]
    [DataRow("<ucel><nl>", "orphan-continuation")]
    [DataRow("<xcel><nl>", "orphan-continuation")]
    [DataRow("<fcel>A<xcel><nl>", "orphan-continuation")]
    [DataRow("<fcel>A<lcel><nl><ucel><fcel>B<nl>", "nonrectangular-or-overlapping-span")]
    [DataRow("<fcel>A<fcel>B<nl><ucel><xcel><nl>", "orphan-continuation")]
    [DataRow("<fcel>A<lcel><nl><ucel><lcel><nl>", "nonrectangular-or-overlapping-span")]
    public void RejectsInvalidDataWithoutPublishingPartialCellsOrHtmlEvenInRepairMode(string raw, string diagnostic)
    {
        foreach (PaddleOcrVlTableRepairMode mode in Enum.GetValues<PaddleOcrVlTableRepairMode>())
        {
            PaddleOcrVlTableParseResult result = PaddleOcrVlTableParser.Parse(raw, GenerationFinishReason.EndOfSequence, new(mode));
            Assert.AreEqual(PaddleOcrVlTableParseStatus.InvalidStructure, result.Status, mode.ToString());
            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Html);
            Assert.AreEqual(0, result.Cells.Count);
            Assert.AreEqual(raw, result.RawText);
            Assert.IsTrue(result.Diagnostics.Any(value => value.StartsWith(diagnostic, StringComparison.Ordinal)), string.Join(";", result.Diagnostics));
        }
    }

    [TestMethod]
    [DataRow(GenerationFinishReason.None)]
    [DataRow(GenerationFinishReason.MaxTokens)]
    [DataRow(GenerationFinishReason.StopSequence)]
    [DataRow(GenerationFinishReason.Cancelled)]
    [DataRow(GenerationFinishReason.Error)]
    public void NeverPublishesACompleteTableForNonEosGenerationEvenWhenTheGridLooksComplete(GenerationFinishReason reason)
    {
        const string raw = "<fcel>apparently complete<nl>";
        PaddleOcrVlTableParseResult result = PaddleOcrVlTableParser.Parse(raw, reason);
        Assert.AreEqual(PaddleOcrVlTableParseStatus.IncompleteGeneration, result.Status);
        Assert.AreEqual(reason, result.FinishReason);
        Assert.IsNull(result.Html);
        Assert.AreEqual(0, result.Cells.Count);
        Assert.AreEqual(raw, result.RawText);
    }

    [TestMethod]
    public void BudgetsApplyToRawInputAndRepairedRectangleBeforeGridAllocation()
    {
        Check("<fcel>A<nl>", new(maximumInputCharacters: 5), "input-character-limit");
        Check("<fcel>A<nl><fcel>B<nl>", new(maximumRows: 1), "row-limit");
        Check("<nl><nl><fcel>A<nl>", new(PaddleOcrVlTableRepairMode.OfficialRowWidth, maximumRows: 2), "row-limit");
        Check("<fcel>A<ecel><nl>", new(maximumColumns: 1), "column-limit");
        Check("<fcel>A<ecel><nl>", new(maximumGridCells: 1), "raw-cell-limit");
        Check("<fcel>A<fcel>B<fcel>C<nl><fcel>D<nl><fcel>E<nl>",
            new(PaddleOcrVlTableRepairMode.OfficialRowWidth, maximumGridCells: 6), "rectangular-grid-limit");

        static void Check(string raw, PaddleOcrVlTableParseOptions options, string expected)
        {
            PaddleOcrVlTableParseResult result = PaddleOcrVlTableParser.Parse(raw, GenerationFinishReason.EndOfSequence, options);
            Assert.AreEqual(PaddleOcrVlTableParseStatus.LimitExceeded, result.Status);
            Assert.IsNull(result.Html);
            CollectionAssert.Contains(result.Diagnostics.ToArray(), expected);
        }
    }

    [TestMethod]
    public void CancellationAndInvalidOptionsAreExplicitAndResultsCannotBeMutated()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => PaddleOcrVlTableParser.Parse("<fcel>A<nl>", GenerationFinishReason.EndOfSequence, cancellationToken: cancelled.Token));
        Assert.ThrowsExactly<ArgumentNullException>(() => PaddleOcrVlTableParser.Parse(null!, GenerationFinishReason.EndOfSequence));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PaddleOcrVlTableParser.Parse("", (GenerationFinishReason)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlTableParseOptions((PaddleOcrVlTableRepairMode)999));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlTableParseOptions(maximumInputCharacters: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlTableParseOptions(maximumRows: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlTableParseOptions(maximumColumns: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleOcrVlTableParseOptions(maximumGridCells: 0));
        PaddleOcrVlTableParseResult result = PaddleOcrVlTableParser.Parse("<fcel>A<nl>", GenerationFinishReason.EndOfSequence);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<PaddleOcrVlTableCell>)result.Cells).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<string>)result.Diagnostics).Add("modified"));
    }

    [TestMethod]
    public async Task ConcurrentParsersKeepIndependentStateAndEscapedPayloads()
    {
        Task<PaddleOcrVlTableParseResult>[] jobs = Enumerable.Range(0, 32).Select(index => Task.Run(() =>
            PaddleOcrVlTableParser.Parse("<fcel><img src=x onerror='" + index + "'> &amp;<lcel><nl><ucel><xcel><nl>", GenerationFinishReason.EndOfSequence))).ToArray();
        PaddleOcrVlTableParseResult[] results = await Task.WhenAll(jobs);
        for (int index = 0; index < results.Length; index++)
        {
            Assert.IsTrue(results[index].Succeeded);
            Assert.AreEqual(1, results[index].Cells.Count);
            Assert.AreEqual(2, results[index].Cells[0].RowSpan);
            Assert.AreEqual(2, results[index].Cells[0].ColumnSpan);
            Assert.AreEqual("<table><tr><td rowspan=\"2\" colspan=\"2\">&lt;img src=x onerror=&#x27;" + index + "&#x27;&gt; &amp;amp;</td></tr><tr></tr></table>", results[index].Html);
        }
    }

    [TestMethod]
    public void ResultKeepsDefaultStrictAnalysisAndProvenanceWhenCallerRequestsRepair()
    {
        const string raw = "<fcel>A<fcel>B<nl><fcel>C<nl>";
        PaddleDocumentVisionLanguageResult result = Result(raw);
        Assert.AreEqual(PaddleOcrVlTableParseStatus.InvalidStructure, result.Table!.Status);
        Assert.AreSame(result.Table, result.ParseTable());
        PaddleOcrVlTableParseResult repaired = result.ParseTable(new(PaddleOcrVlTableRepairMode.OfficialRowWidth));
        Assert.AreEqual(PaddleOcrVlTableParseStatus.Repaired, repaired.Status);
        CollectionAssert.Contains(repaired.Diagnostics.ToArray(), "padded-empty-cells:1");
        Assert.AreEqual(PaddleOcrVlTableParseStatus.InvalidStructure, result.Table.Status);
        Assert.AreEqual(raw, result.Text);
        CollectionAssert.AreEqual(new[] { 11, 12, 2 }, result.TokenIds.ToArray());
        Assert.AreEqual(7, result.Metadata.PageIndex);
        Assert.IsFalse(result.CoordinatesAvailable);
        PaddleDocumentVisionLanguageResult text = Result("plain OCR", PaddleOcrVlTask.TextRecognition);
        Assert.IsNull(text.Table);
        Assert.ThrowsExactly<InvalidOperationException>(() => text.ParseTable());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task SingleRegionalAndMixedPageExportsKeepRawTokensAndAddStructuredTable(int kind)
    {
        const string raw = "<fcel><script>alert('x')</script><lcel><nl><ucel><xcel><nl>";
        var region = new PaddleDocumentRegion("table", .9f, new RectangleF(10, 20, 200, 100));
        PaddleDocumentVisionLanguageResult result = Result(raw, region: kind == 0 ? null : region);
        PaddleDocumentModuleResult module = result;
        if (kind == 1)
            module = new PaddleDocumentVisionLanguagePageResult(result.Task, result.Metadata,
                new[] { new PaddleDocumentVisionLanguageRegionExecution(0, region, result) });
        else if (kind == 2)
            module = new PaddleDocumentVisionLanguageRoutedPageResult(Metadata(PaddleDocumentModule.VisionLanguageParsing),
                new[] { new PaddleDocumentVisionLanguageRegionExecution(0, region, result) });
        var stage = new PaddleDocumentPipelineStage(module.Module, (context, token) => Task.FromResult(module));
        PaddleDocumentPipelineResult page = await new PaddleDocumentPipeline(new[] { stage })
            .RunAsync(new PaddleDocumentPage("caller-owned", new VisualSize(800, 600), pageIndex: 7));
        using JsonDocument json = JsonDocument.Parse(PaddleDocumentPipelineExport.ToJson(page));
        JsonElement payload = json.RootElement.GetProperty("results")[0].GetProperty("payload");
        if (kind != 0) payload = payload.GetProperty("regions")[0].GetProperty("result");
        Assert.AreEqual(raw, payload.GetProperty("text").GetString());
        CollectionAssert.AreEqual(new[] { 11, 12, 2 }, payload.GetProperty("tokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray());
        JsonElement table = payload.GetProperty("table");
        Assert.AreEqual("Succeeded", table.GetProperty("status").GetString());
        Assert.AreEqual(2, table.GetProperty("rowCount").GetInt32());
        Assert.AreEqual(2, table.GetProperty("columnCount").GetInt32());
        Assert.AreEqual(result.Table!.Html, table.GetProperty("html").GetString());
        Assert.AreEqual(1, table.GetProperty("cells").GetArrayLength());
        Assert.IsFalse(table.GetProperty("cells")[0].TryGetProperty("bounds", out _));
        string markdown = PaddleDocumentPipelineExport.ToMarkdown(page);
        StringAssert.Contains(markdown, "~~~text" + Environment.NewLine + raw);
        StringAssert.Contains(markdown, result.Table.Html!);
        StringAssert.Contains(markdown, "OTSL table: Succeeded");
        Assert.AreEqual(1, markdown.Split("<script>", StringSplitOptions.None).Length - 1, "The only raw script text must stay inside the preserved raw-output fence.");
    }

    private static PaddleDocumentVisionLanguageResult Result(string raw, PaddleOcrVlTask task = PaddleOcrVlTask.TableRecognition, PaddleDocumentRegion? region = null)
        => new(task, Metadata(PaddleDocumentVisionLanguageResult.ModuleForTask(task)), raw, new[] { 11, 12, 2 }, GenerationFinishReason.EndOfSequence,
            regionIndex: region == null ? -1 : 0, sourceRegion: region);

    private static PaddleDocumentResultMetadata Metadata(PaddleDocumentModule module)
        => new(new PaddleDocumentModelDescriptor("paddleocr-vl/1.6", "PaddleOCR-VL-1.6", module, "https://example.invalid/model", "onnx"),
            "test-generator", TimeSpan.Zero, new string('a', 64), 7);

    private static string Find(string relative)
        => RepositoryTestFiles.Find(relative);

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
