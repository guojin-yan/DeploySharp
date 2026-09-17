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
    public sealed class OcrTextNormalizationTests
    {
        [TestMethod]
        public void NormalizationIsOrderedAndRetainsRawText()
        {
            string raw = "\uff21\uff22\r\n  cafe\u0301\t";
            var options = new OcrTextNormalizationOptions(
                OcrUnicodeNormalizationForm.Nfc,
                convertFullWidthAscii: true,
                normalizeLineEndings: true,
                trimWhitespace: true,
                collapseWhitespace: true,
                replacements: new[] { new OcrTextReplacementRule("accent", "café", "coffee") });

            OcrTextNormalizationResult result = OcrTextNormalizer.Normalize(raw, options);

            Assert.AreEqual(raw, result.RawText);
            Assert.AreEqual("AB coffee", result.NormalizedText);
            Assert.IsTrue(result.Changed);
            Assert.IsFalse(result.Truncated);
            CollectionAssert.AreEqual(new[] { "unicode:nfc", "width:fullwidth-ascii", "line-endings:lf", "whitespace:collapse", "whitespace:trim", "replacement:accent" }, new List<string>(result.AppliedRules));
            Assert.AreEqual(12, result.OriginalLength);
            Assert.AreEqual(9, result.NormalizedLength);
            Assert.AreEqual(64, result.ConfigurationSha256.Length);
            Assert.AreEqual(64, result.ComputeSha256().Length);
        }

        [TestMethod]
        public void BuiltInOperationsPreserveUnchangedPrefixAndUnicodeBoundaries()
        {
            OcrTextNormalizationResult width = OcrTextNormalizer.Normalize("prefix\uff21suffix", new OcrTextNormalizationOptions(convertFullWidthAscii: true));
            Assert.AreEqual("prefixAsuffix", width.NormalizedText);

            OcrTextNormalizationResult controls = OcrTextNormalizer.Normalize("a\0b\u000bc", new OcrTextNormalizationOptions(removeControlCharacters: true));
            Assert.AreEqual("abc", controls.NormalizedText);

            OcrTextNormalizationResult collapsed = OcrTextNormalizer.Normalize("left\t \r\nright", new OcrTextNormalizationOptions(normalizeLineEndings: true, collapseWhitespace: true));
            Assert.AreEqual("left right", collapsed.NormalizedText);

            string raw = "A😀BC";
            OcrTextNormalizationResult truncated = OcrTextNormalizer.Normalize(raw, new OcrTextNormalizationOptions(maximumLength: 2, overflowMode: OcrTextNormalizationOverflowMode.Truncate));
            Assert.AreEqual("A😀", truncated.NormalizedText);
            Assert.IsTrue(truncated.Truncated);
            Assert.AreEqual(2, truncated.NormalizedLength);
            Assert.AreEqual("length:truncate", truncated.AppliedRules[0]);
            Assert.IsFalse(char.IsHighSurrogate(truncated.NormalizedText[truncated.NormalizedText.Length - 1]));
        }

        [TestMethod]
        public void NoOpPolicyAvoidsTextAllocationAndRejectsOverLimit()
        {
            string raw = "already canonical";
            OcrTextNormalizationResult noOp = OcrTextNormalizer.Normalize(raw, new OcrTextNormalizationOptions());
            Assert.AreSame(raw, noOp.NormalizedText);
            Assert.IsFalse(noOp.Changed);
            Assert.AreEqual(0, noOp.AppliedRules.Count);

            VisualException exception = Assert.ThrowsExactly<VisualException>(() => OcrTextNormalizer.Normalize("12345", new OcrTextNormalizationOptions(maximumLength: 4)));
            Assert.AreEqual(VisualErrorCodes.OcrTextNormalizationLimitExceeded, exception.ErrorCode);
        }

        [TestMethod]
        public void NormalizedViewPreservesCanonicalOcrResult()
        {
            var quadrilateral = new TextQuadrilateral(new PointF(0, 0), new PointF(20, 0), new PointF(20, 8), new PointF(0, 8), TextCornerOrder.TopLeftClockwise);
            var region = new TextRegion(0, .9f, quadrilateral.Polygon, quadrilateral);
            var recognition = new RecognizedText(0, "  Ａ  ", .9f, Array.Empty<OcrToken>(), "latin", "1", new string('0', 64));
            var source = new OcrResult(
                new[] { new OcrRegionResult(region, recognition) },
                new VisualSize(20, 8),
                "det",
                new ModelId("det"),
                "rec",
                new ModelId("rec"),
                new OcrStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));
            string sourceHash = source.ComputeSha256();

            OcrNormalizedResult normalized = OcrTextNormalizer.Normalize(source, new OcrTextNormalizationOptions(convertFullWidthAscii: true, trimWhitespace: true));

            Assert.AreSame(source, normalized.Source);
            Assert.AreEqual(sourceHash, normalized.Source.ComputeSha256());
            Assert.AreEqual(1, normalized.Regions.Count);
            Assert.AreEqual(0, normalized.Regions[0].Text.SourceRegionIndex);
            Assert.AreEqual("A", normalized.Regions[0].Text.NormalizedText);
            Assert.AreEqual("  Ａ  ", source.Regions[0].Recognition.Text);
            Assert.AreEqual(64, normalized.ComputeSha256().Length);
        }

        [TestMethod]
        public void CancellationIsObservedBeforeWork()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() => OcrTextNormalizer.Normalize("text", new OcrTextNormalizationOptions(), cancellation.Token));
            }
        }
    }
}
