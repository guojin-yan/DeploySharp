using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrRecognitionWindowTests
    {
        public TestContext TestContext { get; set; } = null!;
        [TestMethod]
        public void WindowsCoverTheOriginalRegionWithoutHorizontalCompression()
        {
            TextRegion region = Region(1000, 40);
            TextCropProfile profile = Profile();
            IReadOnlyList<OcrRecognitionWindow> windows = OcrRecognitionWindowPlanner.Plan(region, profile);
            Assert.IsTrue(windows.Count > 2);
            Assert.AreEqual(0d, windows[0].Start);
            Assert.AreEqual(1d, windows[windows.Count - 1].End);
            for (int index = 0; index < windows.Count; index++)
            {
                Assert.AreEqual(index, windows[index].Index);
                Assert.AreEqual(region.SourceIndex, windows[index].Crop.Region.SourceIndex);
                Assert.IsFalse(windows[index].Crop.WidthInfo.WidthClamped);
                Assert.IsTrue(windows[index].Crop.TargetWidth <= 320);
                if (index != 0)
                {
                    Assert.IsTrue(windows[index].Start < windows[index - 1].End);
                    Assert.IsTrue(windows[index].End > windows[index - 1].End);
                }
            }
        }

        [TestMethod]
        public void UnslicedPathRetainsOriginalGeometryAndRejectCannotBeBypassed()
        {
            TextRegion region = Region(200, 40);
            IReadOnlyList<OcrRecognitionWindow> windows = OcrRecognitionWindowPlanner.Plan(region, Profile());
            Assert.AreEqual(1, windows.Count);
            Assert.AreSame(region, windows[0].Crop.Region);
            Assert.ThrowsExactly<OcrPipelineException>(() => OcrRecognitionWindowPlanner.Plan(Region(1000, 40), Profile().WithRecognitionOverflowMode(RecognitionOverflowMode.Reject)));
            Assert.ThrowsExactly<OcrPipelineException>(() => new TextCropRequest(Region(1000, 40), Profile()));
        }

        [TestMethod]
        public void OrientationChangesWindowDirectionAndNotTheOriginalRegion()
        {
            foreach (TextOrientation orientation in Enum.GetValues<TextOrientation>())
            {
                bool vertical = orientation == TextOrientation.Clockwise90 || orientation == TextOrientation.CounterClockwise90;
                TextRegion region = Region(vertical ? 40 : 1000, vertical ? 1000 : 40, orientation);
                IReadOnlyList<OcrRecognitionWindow> windows = OcrRecognitionWindowPlanner.Plan(region, Profile());
                Assert.AreEqual(orientation, region.Orientation);
                Assert.AreEqual(TextOrientation.Degrees0, windows[0].Crop.Region.Orientation);
                PointF start = windows[0].Crop.Quadrilateral.TopLeft;
                PointF expected = orientation == TextOrientation.Clockwise90 ? new PointF(0, 1000)
                    : orientation == TextOrientation.CounterClockwise90 ? new PointF(40, 0)
                    : orientation == TextOrientation.Degrees180 ? new PointF(1000, 40) : new PointF(0, 0);
                Assert.AreEqual(expected.X, start.X, .001f);
                Assert.AreEqual(expected.Y, start.Y, .001f);
            }
        }

        [TestMethod]
        public void PerspectiveWindowsUseRectifiedHomographyAndRespectLocalWidths()
        {
            var quad = new TextQuadrilateral(new PointF(0, 0), new PointF(1000, 70), new PointF(700, 120), new PointF(0, 40), TextCornerOrder.TopLeftClockwise);
            var region = new TextRegion(7, .9f, quad.Polygon, quad);
            var unit = new VisualSize(1, 1);
            ImageTransform transform = ImageTransform.Perspective(unit, unit,
                new[] { quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft },
                new[] { new PointF(0, 0), new PointF(1, 0), new PointF(1, 1), new PointF(0, 1) });
            foreach (OcrRecognitionWindow window in OcrRecognitionWindowPlanner.Plan(region, Profile()))
            {
                PointF rectified = transform.ToModel(window.Crop.Quadrilateral.TopRight);
                Assert.AreEqual(window.End, rectified.X, .00001);
                Assert.AreEqual(0, rectified.Y, .00001);
                Assert.IsFalse(window.Crop.WidthInfo.WidthClamped);
            }
        }

        [TestMethod]
        public void GeometryLimitsAndCancellationFailBeforeAllocatingImageInputs()
        {
            TextCropProfile profile = Profile(new OcrRecognitionWindowOptions(maximumWindowsPerRegion: 2));
            OcrPipelineException error = Assert.ThrowsExactly<OcrPipelineException>(() => OcrRecognitionWindowPlanner.Plan(Region(1000, 40), profile));
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, error.ErrorCode);
            Assert.AreEqual(7, error.RegionIndex);
            Assert.ThrowsExactly<OperationCanceledException>(() => OcrRecognitionWindowPlanner.Plan(Region(1000, 40), Profile(), new CancellationToken(true)));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrRecognitionWindowOptions(double.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrRecognitionWindowOptions(maximumWindowsPerRegion: 257));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrRecognitionWindowOptions(minimumOverlapTokens: 3, maximumOverlapTokens: 2));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrRecognitionWindowOptions().WithMaximumOverlapEditDistance(-1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrRecognitionWindowOptions().WithMaximumOverlapEditDistance(9));
        }

        [TestMethod]
        public void MatchingTokensMergeWithoutSplittingUnicodeOrChangingOriginalRegion()
        {
            TextRegion region = Region(600, 48);
            // The first seam shares one dictionary token containing two Unicode scalars.
            // Opt in to single-token matching here; the default requires two tokens.
            TextCropProfile profile = Profile(new OcrRecognitionWindowOptions(overlapRatio: .5, minimumOverlapTokens: 1));
            IReadOnlyList<OcrRecognitionWindow> plans = OcrRecognitionWindowPlanner.Plan(region, profile);
            OcrRecognitionWindowResult[] raw = Results(plans, profile, new[] { "A", "😀", "中文", "B", "C", "D" });
            OcrRegionResult merged = OcrRecognitionWindowMerger.Merge(region, profile, raw);
            Assert.AreSame(region, merged.Region);
            Assert.AreEqual("A😀中文BCD", merged.Recognition.Text);
            Assert.AreEqual(plans.Count, merged.RecognitionWidth!.Value.WindowCount);
            Assert.IsFalse(merged.RecognitionWidth.Value.WidthClamped);
            Assert.IsTrue(merged.RecognitionWindows.Skip(1).Any(window => window.RemovedPrefixTokens >= 2));
            Assert.AreEqual(merged.Recognition.Text, string.Concat(merged.Recognition.Tokens.Where(token => token.Emitted).Select(token => token.Text)));
            for (int index = 0; index < merged.Recognition.Tokens.Count; index++) Assert.AreEqual(index, merged.Recognition.Tokens[index].Timestep);
            Assert.AreEqual(.9f, merged.Recognition.Confidence, .0001f);
            Assert.AreEqual(raw[0].Recognition.Text, merged.RecognitionWindows[0].Recognition.Text);
        }

        [TestMethod]
        public void RepeatedTextOutsideOverlapIsNotDeletedAndUnmatchedSeamsRemainVisible()
        {
            TextRegion region = Region(600, 48);
            TextCropProfile profile = Profile(new OcrRecognitionWindowOptions(overlapRatio: .5));
            IReadOnlyList<OcrRecognitionWindow> plans = OcrRecognitionWindowPlanner.Plan(region, profile);
            OcrRecognitionWindowResult[] raw = plans.Select(window => new OcrRecognitionWindowResult(window,
                Trace(window, profile, new[] { (.5, "X", 1) }), window.Crop.WidthInfo)).ToArray();
            OcrRegionResult merged = OcrRecognitionWindowMerger.Merge(region, profile, raw);
            Assert.AreEqual(new string('X', plans.Count), merged.Recognition.Text);
            Assert.IsTrue(merged.RecognitionWindows.Skip(1).All(window => window.SeamUncertain && window.RemovedPrefixTokens == 0));
        }

        [TestMethod]
        public void OneTokenSeamErrorUsesBoundedGeometricFuzzyMatchAndKeepsTraceProvenance()
        {
            TextRegion region = Region(600, 48);
            TextCropProfile profile = Profile(new OcrRecognitionWindowOptions(overlapRatio: .5).WithMaximumOverlapEditDistance(1));
            IReadOnlyList<OcrRecognitionWindow> plans = OcrRecognitionWindowPlanner.Plan(region, profile);
            string[] expected = Enumerable.Range(0, 60).Select(index => ((char)('A' + index % 26)).ToString()).ToArray();
            OcrRecognitionWindowResult[] raw = Results(plans, profile, expected);

            List<OcrToken> noisyTrace = raw[1].Recognition.Tokens.ToList();
            int noisyIndex = noisyTrace.FindIndex(token => token.Emitted);
            Assert.IsTrue(noisyIndex >= 0);
            OcrToken original = noisyTrace[noisyIndex];
            noisyTrace[noisyIndex] = new OcrToken(original.Timestep, original.ClassIndex + 100, original.Confidence, "X",
                original.IsBlank, original.IsCollapsedRepeat, original.IsUnknown, original.Emitted);
            RecognizedText noisyText = new RecognizedText(7,
                string.Concat(noisyTrace.Where(token => token.Emitted).Select(token => token.Text)),
                raw[1].Recognition.Confidence, noisyTrace, raw[1].Recognition.CharacterSetId,
                raw[1].Recognition.CharacterSetVersion, raw[1].Recognition.CharacterSetSha256);
            raw[1] = new OcrRecognitionWindowResult(plans[1], noisyText, raw[1].Width);

            OcrRegionResult merged = OcrRecognitionWindowMerger.Merge(region, profile, raw);
            Assert.AreEqual(string.Concat(expected), merged.Recognition.Text);
            Assert.AreEqual(region.SourceIndex, merged.Recognition.SourceRegionIndex);
            OcrRecognitionWindowResult seam = merged.RecognitionWindows[1];
            Assert.IsTrue(seam.SeamUncertain, "A fuzzy seam must remain visible to callers.");
            Assert.AreEqual(1, seam.OverlapEditDistance);
            Assert.IsTrue(seam.RemovedPrefixTokens > 0);

            var size = new VisualSize(600, 48);
            var sourceResult = new OcrResult(new[] { merged }, size, "det", new JYPPX.DeploySharp.Models.ModelId("det"),
                "rec", new JYPPX.DeploySharp.Models.ModelId("rec"),
                new OcrStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));
            var projection = new RoiProjection("fuzzy-seam", size, size, ImageTransform.Resize(size, size));
            OcrResult projectedResult = new ModelSpaceOcrRoiProjector().Project(sourceResult, projection);
            Assert.AreEqual(seam.OverlapEditDistance, projectedResult.Regions[0].RecognitionWindows[1].OverlapEditDistance,
                "ROI projection must preserve accepted seam diagnostics.");

            int offset = 0;
            for (int windowIndex = 0; windowIndex < merged.RecognitionWindows.Count; windowIndex++)
            {
                OcrRecognitionWindowResult window = merged.RecognitionWindows[windowIndex];
                int emittedOrdinal = 0;
                for (int tokenIndex = 0; tokenIndex < window.Recognition.Tokens.Count; tokenIndex++)
                {
                    OcrToken source = window.Recognition.Tokens[tokenIndex];
                    OcrToken projected = merged.Recognition.Tokens[offset + tokenIndex];
                    bool expectedEmission = source.Emitted && emittedOrdinal++ >= window.RemovedPrefixTokens;
                    Assert.AreEqual(source.ClassIndex, projected.ClassIndex);
                    Assert.AreEqual(source.Confidence, projected.Confidence);
                    Assert.AreEqual(source.Text, projected.Text);
                    Assert.AreEqual(source.IsBlank, projected.IsBlank);
                    Assert.AreEqual(source.IsCollapsedRepeat, projected.IsCollapsedRepeat);
                    Assert.AreEqual(source.IsUnknown, projected.IsUnknown);
                    Assert.AreEqual(expectedEmission, projected.Emitted);
                    Assert.AreEqual(offset + tokenIndex, projected.Timestep);
                }
                offset += window.Recognition.Tokens.Count;
            }
        }

        [TestMethod]
        public void SeamNoiseBeyondConfiguredEditBudgetIsNotSilentlyDiscarded()
        {
            TextRegion region = Region(600, 48);
            TextCropProfile profile = Profile(new OcrRecognitionWindowOptions(overlapRatio: .5).WithMaximumOverlapEditDistance(1));
            IReadOnlyList<OcrRecognitionWindow> plans = OcrRecognitionWindowPlanner.Plan(region, profile);
            string[] expected = Enumerable.Range(0, 60).Select(index => ((char)('A' + index % 26)).ToString()).ToArray();
            OcrRecognitionWindowResult[] raw = Results(plans, profile, expected);

            List<OcrToken> noisyTrace = raw[1].Recognition.Tokens.ToList();
            int[] emitted = noisyTrace.Select((token, index) => (token, index)).Where(item => item.token.Emitted).Take(2).Select(item => item.index).ToArray();
            Assert.AreEqual(2, emitted.Length);
            for (int index = 0; index < emitted.Length; index++)
            {
                OcrToken original = noisyTrace[emitted[index]];
                string replacement = index == 0 ? "X" : "Y";
                noisyTrace[emitted[index]] = new OcrToken(original.Timestep, original.ClassIndex + 100, original.Confidence, replacement,
                    original.IsBlank, original.IsCollapsedRepeat, original.IsUnknown, original.Emitted);
            }
            RecognizedText noisyText = new RecognizedText(7,
                string.Concat(noisyTrace.Where(token => token.Emitted).Select(token => token.Text)),
                raw[1].Recognition.Confidence, noisyTrace, raw[1].Recognition.CharacterSetId,
                raw[1].Recognition.CharacterSetVersion, raw[1].Recognition.CharacterSetSha256);
            raw[1] = new OcrRecognitionWindowResult(plans[1], noisyText, raw[1].Width);

            OcrRegionResult merged = OcrRecognitionWindowMerger.Merge(region, profile, raw);
            OcrRecognitionWindowResult seam = merged.RecognitionWindows[1];
            Assert.IsTrue(seam.SeamUncertain);
            Assert.AreEqual(0, seam.RemovedPrefixTokens);
            Assert.AreEqual(0, seam.OverlapEditDistance);
            Assert.IsTrue(merged.Recognition.Text.Contains("XY", StringComparison.Ordinal), "Unmatched text must remain available for downstream review.");
        }

        [TestMethod]
        public void DefaultMinimumRetainsOneTokenSeamAndMarksItUncertain()
        {
            TextRegion region = Region(600, 48);
            TextCropProfile profile = Profile(new OcrRecognitionWindowOptions(overlapRatio: .5));
            IReadOnlyList<OcrRecognitionWindow> plans = OcrRecognitionWindowPlanner.Plan(region, profile);
            OcrRegionResult result = OcrRecognitionWindowMerger.Merge(region, profile, Results(plans, profile, new[] { "A", "😀", "中文", "B", "C", "D" }));
            Assert.AreEqual("A😀中文中文BCD", result.Recognition.Text);
            Assert.IsTrue(result.RecognitionWindows[1].SeamUncertain);
            Assert.AreEqual(0, result.RecognitionWindows[1].RemovedPrefixTokens);
            Assert.AreEqual(2, result.RecognitionWindows[2].RemovedPrefixTokens);
        }

        [TestMethod]
        public void FixedTensorPaddingIsRemovedBeforeEstimatingTokenPositions()
        {
            TextRegion region = Region(610, 48);
            var profile = new TextCropProfile("tests/windows-fixed", 48, OcrRecognitionWidthMode.Fixed, 320, 320)
                .WithRecognitionWindows(new OcrRecognitionWindowOptions(overlapRatio: .5, minimumOverlapTokens: 1));
            IReadOnlyList<OcrRecognitionWindow> plans = OcrRecognitionWindowPlanner.Plan(region, profile);
            // Dense unique tokens make every overlap observable, including the short last window.
            string[] text = Enumerable.Range(0, 24).Select(index => ((char)('A' + index)).ToString()).ToArray();
            OcrRegionResult result = OcrRecognitionWindowMerger.Merge(region, profile, Results(plans, profile, text));
            foreach (OcrRecognitionWindowResult window in result.RecognitionWindows)
                TestContext.WriteLine("index={0};span={1:R}-{2:R};natural={3};tensor={4};text={5};trace={6};remove={7}", window.Index, window.Start, window.End, window.Width.NaturalWidth, window.Width.TensorWidth, window.Recognition.Text,
                    string.Join(",", window.Recognition.Tokens.Where(token => token.Emitted).Select(token => token.Text + ":" + token.Timestep)), window.RemovedPrefixTokens);
            Assert.AreEqual(string.Concat(text), result.Recognition.Text);
            Assert.IsTrue(result.RecognitionWindows.Skip(1).All(window => !window.SeamUncertain));
        }

        [TestMethod]
        public void MergeRejectsMismatchedDictionaryIncompleteTraceAndResourceOverflow()
        {
            TextRegion region = Region(600, 48);
            TextCropProfile profile = Profile(new OcrRecognitionWindowOptions(overlapRatio: .5));
            IReadOnlyList<OcrRecognitionWindow> plans = OcrRecognitionWindowPlanner.Plan(region, profile);
            OcrRecognitionWindowResult[] raw = Results(plans, profile, new[] { "A", "B", "C", "D", "E", "F" });
            OcrRecognitionWindowResult saved = raw[1];
            raw[1] = new OcrRecognitionWindowResult(plans[1], new RecognizedText(7, saved.Recognition.Text, .9f, saved.Recognition.Tokens, "other", "1", new string('b', 64)), saved.Width);
            Assert.ThrowsExactly<ArgumentException>(() => OcrRecognitionWindowMerger.Merge(region, profile, raw));
            raw[1] = saved;
            Assert.ThrowsExactly<OcrPipelineException>(() => OcrRecognitionWindowMerger.Merge(region, profile.WithRecognitionWindows(new OcrRecognitionWindowOptions(maximumMergedCharacters: 2)), raw));
            Assert.ThrowsExactly<OcrPipelineException>(() => OcrRecognitionWindowMerger.Merge(region, profile.WithRecognitionWindows(new OcrRecognitionWindowOptions(maximumMergedTimesteps: 2)), raw));
            Assert.ThrowsExactly<OperationCanceledException>(() => OcrRecognitionWindowMerger.Merge(region, profile, raw, cancellationToken: new CancellationToken(true)));
            raw[1] = new OcrRecognitionWindowResult(plans[1], new RecognizedText(7, "A", .9f,
                new[] { new OcrToken(3, 1, .9f, "A", false, false, false, true) }, "tests.windows", "1", new string('a', 64)), saved.Width);
            Assert.ThrowsExactly<ArgumentException>(() => OcrRecognitionWindowMerger.Merge(region, profile, raw));
        }

        [TestMethod]
        public void RoiProjectionAndCanonicalMergeRetainWindowTraceAndReindexSources()
        {
            TextRegion region = Region(600, 48);
            TextCropProfile profile = Profile(new OcrRecognitionWindowOptions(overlapRatio: .5, minimumOverlapTokens: 1));
            IReadOnlyList<OcrRecognitionWindow> plans = OcrRecognitionWindowPlanner.Plan(region, profile);
            OcrRegionResult line = OcrRecognitionWindowMerger.Merge(region, profile, Results(plans, profile, new[] { "A", "B", "C", "D", "E", "F" }));
            var size = new VisualSize(600, 48);
            var original = new OcrResult(new[] { line }, size, "det", new JYPPX.DeploySharp.Models.ModelId("det"), "rec", new JYPPX.DeploySharp.Models.ModelId("rec"), new OcrStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));
            var projection = new RoiProjection("roi", size, size, ImageTransform.Resize(size, size));
            OcrResult projected = new ModelSpaceOcrRoiProjector().Project(original, projection);
            Assert.AreEqual(line.RecognitionWindows.Count, projected.Regions[0].RecognitionWindows.Count);
            RoiMergedOcrResult merged = new RoiOcrResultMerger().Merge(new[] { new RoiProjectedResult<OcrResult>("roi", 1, projected) }, RoiResultMergeMode.HighestConfidence, .5f);
            OcrRegionResult canonical = merged.Items[0].Region;
            Assert.AreEqual(0, canonical.Region.SourceIndex);
            Assert.AreEqual(line.RecognitionWindows.Count, canonical.RecognitionWindows.Count);
            Assert.AreEqual(line.RecognitionWidth, canonical.RecognitionWidth);
            for (int index = 0; index < canonical.RecognitionWindows.Count; index++)
            {
                Assert.AreEqual(0, canonical.RecognitionWindows[index].Recognition.SourceRegionIndex);
                Assert.AreEqual(line.RecognitionWindows[index].RemovedPrefixTokens, canonical.RecognitionWindows[index].RemovedPrefixTokens);
                Assert.AreEqual(line.RecognitionWindows[index].Recognition.Text, canonical.RecognitionWindows[index].Recognition.Text);
            }
        }

        private static OcrRecognitionWindowResult[] Results(IReadOnlyList<OcrRecognitionWindow> plans, TextCropProfile profile, string[] text)
        {
            var results = new List<OcrRecognitionWindowResult>();
            foreach (OcrRecognitionWindow window in plans)
            {
                var emissions = new List<(double, string, int)>();
                for (int index = 0; index < text.Length; index++)
                {
                    double source = (index + .5) / text.Length;
                    if (source >= window.Start && source < window.End) emissions.Add(((source - window.Start) / (window.End - window.Start), text[index], index + 1));
                }
                results.Add(new OcrRecognitionWindowResult(window, Trace(window, profile, emissions), window.Crop.WidthInfo));
            }
            return results.ToArray();
        }

        private static RecognizedText Trace(OcrRecognitionWindow window, TextCropProfile profile, IEnumerable<(double Position, string Text, int Class)> emissions)
        {
            const int time = 100;
            var tokens = Enumerable.Range(0, time).Select(index => new OcrToken(index, 0, 1, null, true, false, false, false)).ToArray();
            double content = Math.Min(window.Crop.TargetWidth, Math.Max(profile.WidthMode == OcrRecognitionWidthMode.Dynamic ? profile.MinimumWidth : 1, window.Crop.WidthInfo.NaturalWidth));
            foreach (var item in emissions)
            {
                int step = (int)(item.Position * content / window.Crop.TargetWidth * time);
                tokens[step] = new OcrToken(step, item.Class, .9f, item.Text, false, false, false, true);
            }
            return new RecognizedText(7, string.Concat(tokens.Where(token => token.Emitted).Select(token => token.Text)), .9f, tokens, "tests.windows", "1", new string('a', 64));
        }

        private static TextCropProfile Profile(OcrRecognitionWindowOptions? options = null)
            => new TextCropProfile("tests/windows", 48, OcrRecognitionWidthMode.Dynamic, 320, 320, minimumWidth: 48).WithRecognitionWindows(options ?? new OcrRecognitionWindowOptions());

        private static TextRegion Region(float width, float height, TextOrientation orientation = TextOrientation.Degrees0)
        {
            var quad = new TextQuadrilateral(new PointF(0, 0), new PointF(width, 0), new PointF(width, height), new PointF(0, height), TextCornerOrder.TopLeftClockwise);
            return new TextRegion(7, .9f, quad.Polygon, quad, orientation);
        }
    }
}
