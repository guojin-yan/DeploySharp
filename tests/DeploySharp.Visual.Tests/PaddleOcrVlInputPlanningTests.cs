using System;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlInputPlanningTests
{
    // Expected values were produced by the pinned official smart_resize helper,
    // not a second copy of the managed implementation. See official asset report.
    [TestMethod]
    [DataRow(1000, 700, 1008, 700, 900)]
    [DataRow(700, 1000, 700, 1008, 900)]
    [DataRow(48, 3200, 56, 3192, 228)]
    [DataRow(10, 200, 84, 1512, 162)]
    [DataRow(4032, 3024, 1148, 840, 1230)]
    [DataRow(336, 336, 336, 336, 144)]
    public void GeometryMatchesPinnedOfficialScalarOutput(int height, int width, int expectedHeight, int expectedWidth, int imageTokens)
    {
        PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(width, height));
        Assert.AreEqual(new VisualSize(expectedWidth, expectedHeight), plan.ResizedSize);
        Assert.AreEqual(imageTokens, plan.ImageTokenCount);
        Assert.AreEqual(imageTokens * 4, plan.PatchCount);
        Assert.AreEqual(plan.SourceSize, plan.PreparedSize);
        Assert.IsFalse(plan.RequiresSpottingUpscale);
    }

    [TestMethod]
    [DataRow(PaddleOcrVlTask.TextRecognition, "OCR:")]
    [DataRow(PaddleOcrVlTask.TableRecognition, "Table Recognition:")]
    [DataRow(PaddleOcrVlTask.FormulaRecognition, "Formula Recognition:")]
    [DataRow(PaddleOcrVlTask.ChartRecognition, "Chart Recognition:")]
    [DataRow(PaddleOcrVlTask.TextSpotting, "Spotting:")]
    [DataRow(PaddleOcrVlTask.SealRecognition, "Seal Recognition:")]
    public void PromptsMatchOfficialUsage(PaddleOcrVlTask task, string prompt)
        => Assert.AreEqual(prompt, PaddleOcrVlInputPlanning.GetOfficialV16Prompt(task));

    [TestMethod]
    public void SpottingUsesTwoDistinctResizeStepsAndTaskBudget()
    {
        PaddleOcrVlImagePlan small = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(700, 1000), PaddleOcrVlTask.TextSpotting);
        Assert.IsTrue(small.RequiresSpottingUpscale);
        Assert.AreEqual(new VisualSize(1400, 2000), small.PreparedSize);
        Assert.AreEqual(new VisualSize(1036, 1512), small.ResizedSize);
        Assert.AreEqual(1998, small.ImageTokenCount);
        // One dimension at the 1500 threshold disables the optional 2x step.
        PaddleOcrVlImagePlan threshold = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(700, 1500), PaddleOcrVlTask.TextSpotting);
        Assert.IsFalse(threshold.RequiresSpottingUpscale);
        Assert.IsTrue(small.ImageTokenCount > PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(700, 1000)).ImageTokenCount);
        // Caller budgets remain usable without silently reverting to task defaults.
        PaddleOcrVlImagePlan custom = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(700, 1000), maximumPixels: 200000);
        Assert.IsTrue(custom.ImageTokenCount < small.ImageTokenCount);
    }

    [TestMethod]
    public void RejectsInvalidImageTaskBudgetAndExtremeAspectRatioBeforeAllocation()
    {
        Assert.ThrowsExactly<ArgumentException>(() => PaddleOcrVlInputPlanning.PlanOfficialV16(default));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336), (PaddleOcrVlTask)99));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336), minimumPixels: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336), maximumPixels: 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336), maximumSourcePixels: 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336), maximumSourcePixels: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(201, 1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(200, 1), minimumPixels: 784, maximumPixels: 784));
    }
}
