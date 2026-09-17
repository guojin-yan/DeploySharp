using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrTextLayoutTests
    {
        [TestMethod]
        public void LayoutGroupsRowsParagraphsAndColumnsDeterministically()
        {
            OcrResult source = CreateResult(new[]
            {
                Region(0, 0, 0, 40, 10, "A"), Region(1, 200, 0, 40, 10, "C"),
                Region(2, 0, 15, 40, 10, "B"), Region(3, 200, 15, 40, 10, "D")
            });

            OcrTextLayoutResult layout = OcrTextLayoutBuilder.Build(source);

            Assert.AreEqual(4, layout.Lines.Count);
            Assert.AreEqual("A", layout.Lines[0].Text);
            Assert.AreEqual("C", layout.Lines[1].Text);
            Assert.AreEqual("B", layout.Lines[2].Text);
            Assert.AreEqual("D", layout.Lines[3].Text);
            Assert.AreEqual(2, layout.Columns.Count);
            Assert.AreEqual(1, layout.Columns[0].Paragraphs.Count);
            Assert.AreEqual(1, layout.Columns[1].Paragraphs.Count);
            Assert.AreEqual("A\nB", layout.Columns[0].Text);
            Assert.AreEqual("C\nD", layout.Columns[1].Text);
            Assert.AreEqual("A\nB\n\nC\nD", layout.Text);
            Assert.AreEqual(2, layout.Paragraphs.Count);
            Assert.AreEqual(64, layout.ComputeSha256().Length);
        }

        [TestMethod]
        public void LayoutUsesNormalizedViewWithoutChangingSource()
        {
            OcrResult source = CreateResult(new[] { Region(0, 0, 0, 40, 10, "  Ａ  ") });
            OcrTextNormalizationOptions normalizationOptions = new OcrTextNormalizationOptions(convertFullWidthAscii: true, trimWhitespace: true);
            OcrNormalizedResult normalized = OcrTextNormalizer.Normalize(source, normalizationOptions);

            OcrTextLayoutResult layout = OcrTextLayoutBuilder.Build(source, normalized);

            Assert.AreEqual("A", layout.Lines[0].Text);
            Assert.AreEqual("  Ａ  ", source.Regions[0].Recognition.Text);
            Assert.AreSame(normalizationOptions, layout.Normalization);
            Assert.AreEqual(normalizationOptions.ConfigurationSha256, layout.Normalization!.ConfigurationSha256);
        }

        [TestMethod]
        public void InlineRegionsAreSortedAndParagraphGapSplits()
        {
            OcrResult source = CreateResult(new[]
            {
                Region(0, 30, 0, 10, 10, "B"), Region(1, 0, 0, 10, 10, "A"), Region(2, 0, 30, 10, 10, "C")
            });
            var options = new OcrTextLayoutOptions(paragraphGapRatio: .5f, regionSeparator: " ");

            OcrTextLayoutResult layout = OcrTextLayoutBuilder.Build(source, options: options);

            Assert.AreEqual(2, layout.Lines.Count);
            Assert.AreEqual("A B", layout.Lines[0].Text);
            Assert.AreEqual("C", layout.Lines[1].Text);
            Assert.AreEqual(2, layout.Paragraphs.Count);
            Assert.AreEqual("A B\n\nC", layout.Text);
        }

        [TestMethod]
        public void LayoutRejectsMismatchedNormalizedSourceAndInvalidLimits()
        {
            OcrResult first = CreateResult(new[] { Region(0, 0, 0, 10, 10, "A") });
            OcrResult second = CreateResult(new[] { Region(0, 0, 0, 10, 10, "B") });
            OcrNormalizedResult normalized = OcrTextNormalizer.Normalize(second, new OcrTextNormalizationOptions());
            Assert.ThrowsExactly<ArgumentException>(() => OcrTextLayoutBuilder.Build(first, normalized));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrTextLayoutOptions(maximumColumns: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrTextLayoutOptions(maximumInlineGapRatio: float.NaN));
        }

        [TestMethod]
        public void LayoutHonorsCancellationAndEmptyResults()
        {
            OcrResult empty = CreateResult(Array.Empty<OcrRegionResult>());
            OcrTextLayoutResult layout = OcrTextLayoutBuilder.Build(empty);
            Assert.AreEqual(0, layout.Lines.Count);
            Assert.AreEqual(string.Empty, layout.Text);

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() => OcrTextLayoutBuilder.Build(CreateResult(new[] { Region(0, 0, 0, 10, 10, "A") }), cancellationToken: cancellation.Token));
            }
        }

        private static OcrResult CreateResult(IReadOnlyList<OcrRegionResult> regions)
            => new OcrResult(regions, new VisualSize(320, 200), "det", new ModelId("det"), "rec", new ModelId("rec"), new OcrStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));

        private static OcrRegionResult Region(int index, float x, float y, float width, float height, string text)
        {
            var quad = new TextQuadrilateral(new PointF(x, y), new PointF(x + width, y), new PointF(x + width, y + height), new PointF(x, y + height), TextCornerOrder.TopLeftClockwise);
            var region = new TextRegion(index, .9f, quad.Polygon, quad);
            var recognition = new RecognizedText(index, text, .9f, Array.Empty<OcrToken>(), "latin", "1", new string('0', 64));
            return new OcrRegionResult(region, recognition);
        }
    }
}
