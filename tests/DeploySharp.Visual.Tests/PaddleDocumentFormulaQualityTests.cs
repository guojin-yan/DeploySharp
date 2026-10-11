using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleDocumentFormulaQualityTests
{
    [TestMethod]
    public void CompareSeparatesRawAndWhitespaceNormalizedEquality()
    {
        PaddleDocumentFormulaQualityMetrics metrics = PaddleDocumentFormulaQualityEvaluator.Compare(
            "\\frac { a }\n{ b }", "\\frac { a } { b }");

        Assert.IsFalse(metrics.ExactTextMatch);
        Assert.IsTrue(metrics.NormalizedTextMatch);
        Assert.AreEqual(0d, metrics.NormalizedTextAccuracy.CharacterErrorRate, .00001);
        Assert.IsTrue(metrics.ExpectedBalancedDelimiters);
        Assert.IsTrue(metrics.ActualBalancedDelimiters);
    }

    [TestMethod]
    public void CompareComputesCommandMultisetAndF1()
    {
        PaddleDocumentFormulaQualityMetrics metrics = PaddleDocumentFormulaQualityEvaluator.Compare(
            "\\frac{a}{b}+\\sqrt{x}", "\\frac{a}{b}+\\sqrt{y}+\\sqrt{z}");

        Assert.AreEqual(2, metrics.ExpectedCommandCount);
        Assert.AreEqual(3, metrics.ActualCommandCount);
        Assert.AreEqual(2, metrics.MatchedCommandCount);
        Assert.AreEqual(2d / 3d, metrics.CommandPrecision, .00001);
        Assert.AreEqual(1d, metrics.CommandRecall, .00001);
        Assert.AreEqual(0.8d, metrics.CommandF1, .00001);
    }

    [TestMethod]
    public void CompareDetectsUnbalancedPredictionWithoutCallingItSemanticFailure()
    {
        PaddleDocumentFormulaQualityMetrics metrics = PaddleDocumentFormulaQualityEvaluator.Compare(
            "\\left( x \\right)", "\\left( x");

        Assert.IsTrue(metrics.ExpectedBalancedDelimiters);
        Assert.IsFalse(metrics.ActualBalancedDelimiters);
        Assert.IsTrue(metrics.MatchedCommandCount > 0);
        Assert.IsFalse(metrics.NormalizedTextMatch);
    }

    [TestMethod]
    public void CompareChecksNestedDelimitersAndIgnoresEscapedBraces()
    {
        PaddleDocumentFormulaQualityMetrics valid = PaddleDocumentFormulaQualityEvaluator.Compare("\\{ [x] \\}", "\\{ [x] \\}");
        PaddleDocumentFormulaQualityMetrics invalid = PaddleDocumentFormulaQualityEvaluator.Compare("([x])", "([x)]");

        Assert.IsTrue(valid.ExpectedBalancedDelimiters);
        Assert.IsTrue(valid.ActualBalancedDelimiters);
        Assert.IsFalse(invalid.ActualBalancedDelimiters);
    }

    [TestMethod]
    public void CompareChecksBeginEndEnvironmentBalance()
    {
        PaddleDocumentFormulaQualityMetrics valid = PaddleDocumentFormulaQualityEvaluator.Compare(
            "\\begin{aligned}x&=1\\end{aligned}",
            "\\begin{aligned}x&=2\\end{aligned}");
        PaddleDocumentFormulaQualityMetrics invalid = PaddleDocumentFormulaQualityEvaluator.Compare(
            "\\begin{matrix}x\\end{matrix}",
            "\\begin{matrix}x\\end{aligned}");

        Assert.IsTrue(valid.ExpectedBalancedEnvironments);
        Assert.IsTrue(valid.ActualBalancedEnvironments);
        Assert.IsTrue(invalid.ExpectedBalancedEnvironments);
        Assert.IsFalse(invalid.ActualBalancedEnvironments);
    }
}
