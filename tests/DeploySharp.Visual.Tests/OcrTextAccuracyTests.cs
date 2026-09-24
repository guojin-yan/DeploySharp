using Microsoft.VisualStudio.TestTools.UnitTesting;
using JYPPX.DeploySharp.Visual;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class OcrTextAccuracyTests
{
    [TestMethod]
    public void ComputesCharacterAndWordErrorsWithoutImplicitNormalization()
    {
        OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare("A😀 B", "A😃 C");
        Assert.AreEqual(4, metrics.ReferenceCharacters);
        Assert.AreEqual(4, metrics.HypothesisCharacters);
        Assert.AreEqual(2, metrics.CharacterEditDistance);
        Assert.AreEqual(2, metrics.ReferenceWords);
        Assert.AreEqual(2, metrics.HypothesisWords);
        Assert.AreEqual(2, metrics.WordEditDistance);
        Assert.AreEqual(.5, metrics.CharacterErrorRate, .00001);
        Assert.AreEqual(1, metrics.WordErrorRate, .00001);
    }

    [TestMethod]
    public void EmptyReferenceHasExplicitInsertionRate()
    {
        OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(string.Empty, "text");
        Assert.AreEqual(1d, metrics.CharacterErrorRate);
        Assert.AreEqual(1d, metrics.WordErrorRate);
        Assert.AreEqual(4, metrics.CharacterEditDistance);
        Assert.AreEqual(1, metrics.WordEditDistance);
    }
}
