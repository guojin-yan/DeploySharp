using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleChart2TableQualityTests
{
    [TestMethod]
    public void AnalyzeCountsHeaderDataRowsSeparatorsAndEmptyCells()
    {
        PaddleChart2TableStructureMetrics metrics = PaddleChart2TableQualityEvaluator.Analyze(
            "| Name | Value |\n| --- | :---: |\n| A | 1 |\n| B |  |\n");

        Assert.AreEqual(3, metrics.RowCount);
        Assert.AreEqual(2, metrics.DataRowCount);
        Assert.AreEqual(2, metrics.ColumnCount);
        Assert.AreEqual(1, metrics.SeparatorRowCount);
        Assert.AreEqual(0, metrics.MalformedRowCount);
        Assert.AreEqual(0, metrics.InconsistentRowCount);
        Assert.AreEqual(1, metrics.EmptyCellCount);
        Assert.AreEqual(5, metrics.NonEmptyCellCount);
        Assert.IsTrue(metrics.IsStructurallyValid);
    }

    [TestMethod]
    public void CompareNormalizesWhitespaceAndSupportsEscapedPipes()
    {
        PaddleChart2TableQualityComparison comparison = PaddleChart2TableQualityEvaluator.Compare(
            "Header | Value\nA | 1",
            "| Header | Value |\r\n| A |  1  |");

        Assert.IsFalse(comparison.ExactTextMatch);
        Assert.IsTrue(comparison.StructureMatches);
        Assert.AreEqual(2, comparison.RowExactMatchCount);
        Assert.AreEqual(4, comparison.CellExactMatchCount);
        Assert.AreEqual(1d, comparison.RowAccuracy, .00001);
        Assert.AreEqual(1d, comparison.CellAccuracy, .00001);
        Assert.IsTrue(comparison.IsExactStructureAndContent);

        PaddleChart2TableStructureMetrics escaped = PaddleChart2TableQualityEvaluator.Analyze("| label | A\\|B |\n| x | y |");
        Assert.AreEqual(2, escaped.ColumnCount);
        Assert.AreEqual(2, escaped.RowCount);
        Assert.AreEqual(0, escaped.MalformedRowCount);
    }

    [TestMethod]
    public void CompareReportsMissingRowsAndMalformedStructureWithoutThrowing()
    {
        PaddleChart2TableQualityComparison comparison = PaddleChart2TableQualityEvaluator.Compare(
            "A | B\n1 | 2\n3 | 4",
            "A | B\n1 | 9\nnot-a-row");

        Assert.IsFalse(comparison.StructureMatches);
        Assert.AreEqual(3, comparison.Expected.RowCount);
        Assert.AreEqual(2, comparison.Actual.RowCount);
        Assert.AreEqual(1, comparison.Actual.MalformedRowCount);
        Assert.AreEqual(1, comparison.RowExactMatchCount);
        Assert.AreEqual(3, comparison.CellExactMatchCount);
        Assert.IsFalse(comparison.IsExactStructureAndContent);

        PaddleChart2TableStructureMetrics empty = PaddleChart2TableQualityEvaluator.Analyze(string.Empty);
        Assert.AreEqual(0, empty.RowCount);
        Assert.IsFalse(empty.IsStructurallyValid);
    }
}
