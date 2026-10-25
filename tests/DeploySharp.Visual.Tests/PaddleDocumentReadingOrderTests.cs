using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleDocumentReadingOrderTests
{
    private static readonly VisualSize Page = new(1000, 1000);

    [TestMethod]
    public void ColumnsReadDownEachColumnEvenWithStaggeredRowsAndShuffledDetectorOrder()
    {
        var regions = new[] { Region(550, 240, 400, 60), Region(40, 100, 400, 80), Region(550, 90, 400, 60), Region(40, 220, 400, 80) };
        CollectionAssert.AreEqual(new[] { 1, 3, 2, 0 }, Order(regions));
        CollectionAssert.AreEqual(new[] { 2, 0, 1, 3 }, Order(regions, new(rightToLeft: true)));
        CollectionAssert.AreEqual(new[] { 2, 1, 3, 0 }, Order(regions, new(PaddleDocumentReadingOrderMode.TopToBottom)));
    }

    [TestMethod]
    public void SpanningTitleTableAndFooterSeparateColumnSections()
    {
        var regions = new[]
        {
            Region(550, 590, 400, 60), // 0 lower right
            Region(40, 240, 400, 60), // 1 upper left bottom
            Region(40, 20, 910, 50), // 2 full-width title
            Region(550, 140, 400, 60), // 3 upper right top
            Region(40, 400, 910, 100), // 4 spanning table
            Region(40, 830, 910, 40), // 5 full-width footer
            Region(40, 570, 400, 60), // 6 lower left
            Region(550, 250, 400, 60), // 7 upper right bottom
            Region(40, 130, 400, 60) // 8 upper left top
        };
        CollectionAssert.AreEqual(new[] { 2, 8, 1, 3, 7, 4, 6, 0, 5 }, Order(regions));
        CollectionAssert.AreEqual(new[] { 2, 3, 7, 8, 1, 4, 0, 6, 5 }, Order(regions, new(rightToLeft: true)));
    }

    [TestMethod]
    public void FullyOverlappingBlocksFallBackDeterministicallyAndKeepSourceTies()
    {
        var regions = new[] { Region(20, 30, 800, 500), Region(10, 20, 900, 500), Region(10, 20, 900, 500), Region(30, 20, 900, 500) };
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 0 }, Order(regions));
        CollectionAssert.AreEqual(new[] { 3, 1, 2, 0 }, Order(regions, new(rightToLeft: true)));
    }

    [TestMethod]
    public void PageRelativeGuttersPreventTinyHorizontalGapsFromInventingColumns()
    {
        var regions = new[] { Region(10, 200, 400, 40), Region(415, 100, 400, 40) };
        CollectionAssert.AreEqual(new[] { 1, 0 }, Order(regions));
        CollectionAssert.AreEqual(new[] { 0, 1 }, Order(regions, new(minimumColumnGapRatio: .005)));
        CollectionAssert.AreEqual(new[] { 1, 0 }, Order(regions, new(minimumColumnGapRatio: .006)));
    }

    [TestMethod]
    public void SortingGeometryClipsToPageWithoutMutatingOriginalBoundsOrMetadata()
    {
        var first = new PaddleDocumentRegion("text", 1, new RectangleF(-20, -10, 440, 100), new Dictionary<string, string> { ["id"] = "source" });
        var regions = new[] { Region(550, 100, 500, 80), first };
        RectangleF before = first.Bounds;
        CollectionAssert.AreEqual(new[] { 1, 0 }, Order(regions));
        Assert.AreEqual(before, first.Bounds); Assert.AreEqual("source", first.Metadata["id"]);
        Assert.AreSame(first, regions[1]);
        var order = PaddleDocumentReadingOrder.GetIndices(regions, Page, new());
        regions[1] = Region(5, 800, 50, 50);
        CollectionAssert.AreEqual(new[] { 1, 0 }, order.ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<int>)order)[0] = 0);
    }

    [TestMethod]
    public void SourceModeEmptySingleAndDepthLimitRemainTotalPermutations()
    {
        var regions = new[] { Region(800, 20, 50, 50), Region(400, 10, 50, 50), Region(20, 30, 50, 50) };
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, PaddleDocumentReadingOrder.GetIndices(regions, Page).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Order(regions, new(PaddleDocumentReadingOrderMode.SourceOrder)));
        CollectionAssert.AreEqual(new[] { 1, 2, 0 }, Order(regions, new(maximumDepth: 1)));
        CollectionAssert.AreEqual(new[] { 2, 1, 0 }, Order(regions));
        Assert.AreEqual(0, Order(Array.Empty<PaddleDocumentRegion>()).Length);
        CollectionAssert.AreEqual(new[] { 0 }, Order(new[] { regions[0] }));
    }

    [TestMethod]
    public void InvalidGeometryAndCancelledWorkFailBeforeReturningAnOrder()
    {
        foreach (var region in new[] { Region(0, 0, 0, 5), Region(2000, 10, 5, 5), Region(float.NaN, 0, 5, 5),
            Region(0, float.NegativeInfinity, 5, 5), Region(0, 0, float.PositiveInfinity, 5), null! })
            Assert.ThrowsExactly<ArgumentException>(() => Order(new[] { region }));
        Assert.ThrowsExactly<ArgumentNullException>(() => PaddleDocumentReadingOrder.GetIndices(null!, Page));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => PaddleDocumentReadingOrder.GetIndices(Array.Empty<PaddleDocumentRegion>(), Page, cancellationToken: cancelled.Token));
    }

    [TestMethod]
    public void OptionsRejectUndefinedModesNonfiniteRatiosAndUnboundedRecursion()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleDocumentReadingOrderOptions((PaddleDocumentReadingOrderMode)999));
        foreach (double ratio in new[] { -.01, 1, double.NaN, double.PositiveInfinity })
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleDocumentReadingOrderOptions(minimumColumnGapRatio: ratio));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleDocumentReadingOrderOptions(minimumRowGapRatio: ratio));
        }
        foreach (int depth in new[] { 0, 65 }) Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleDocumentReadingOrderOptions(maximumDepth: depth));
    }

    private static int[] Order(PaddleDocumentRegion[] regions, PaddleDocumentReadingOrderOptions? options = null)
        => PaddleDocumentReadingOrder.GetIndices(regions, Page, options ?? new()).ToArray();
    private static PaddleDocumentRegion Region(float x, float y, float width, float height) => new("text", 1, new RectangleF(x, y, width, height));
}
