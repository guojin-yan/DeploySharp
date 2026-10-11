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
}
