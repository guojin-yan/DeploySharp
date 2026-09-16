using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    public sealed partial class OcrTests
    {
        [TestMethod]
        public void OrientationRetryOptionsAreBoundedAndPreservedByProfileCopies()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrOrientationRetryOptions(float.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrOrientationRetryOptions(minimumConfidenceGain: 1.1f));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrOrientationRetryOptions(maximumRegionsPerImage: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrOrientationRetryOptions(maximumCropsPerImage: 4097));
            Assert.ThrowsExactly<ArgumentException>(() => new OcrOrientationRetryOptions(rotations: Array.Empty<TextOrientation>()));
            Assert.ThrowsExactly<ArgumentException>(() => new OcrOrientationRetryOptions(rotations: new[] { TextOrientation.Degrees0 }));
            Assert.ThrowsExactly<ArgumentException>(() => new OcrOrientationRetryOptions(rotations: new[] { TextOrientation.Degrees180, TextOrientation.Degrees180 }));
            var rotations = new[] { TextOrientation.Degrees180 };
            var options = new OcrOrientationRetryOptions(rotations: rotations);
            rotations[0] = TextOrientation.Clockwise90;
            Assert.AreEqual(TextOrientation.Degrees180, options.Rotations[0]);
            var crop = new TextCropProfile("tests/retry", 8, OcrRecognitionWidthMode.Fixed, 16, 16);
            Assert.IsNull(crop.OrientationRetry);
            Assert.AreSame(options, crop.WithOrientationRetry(options).WithGeometryValidation(new OcrGeometryOptions())
                .WithRecognitionWindows(new OcrRecognitionWindowOptions()).WithRecognitionOverflowMode(RecognitionOverflowMode.Clamp).OrientationRetry);
        }

        [TestMethod]
        public async Task RetrySelectsBetterOrientationAndStopsAfterSuccessWithoutRerunningDetection()
        {
            int calls = 0;
            using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(rotations: new[] { TextOrientation.Degrees180, TextOrientation.Clockwise90 }),
                geometryOptions: new OcrGeometryOptions(), recognitionFactory: _ => RetryOutputs(++calls == 1 ? .4f : .95f));
            using var input = new FakeOcrImageInput();
            OcrResult result = await fixture.Pipeline.RunAsync(input);
            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, fixture.DetectionProvider.LastSession!.RunCount);
            Assert.AreEqual(2, result.Timing.Details!.RecognitionBatchCount);
            foreach (OcrRegionResult region in result.Regions)
            {
                Assert.AreEqual(TextOrientation.Degrees180, region.Region.Orientation);
                OcrOrientationRetryResult retry = region.OrientationRetry!;
                Assert.AreEqual(1, retry.SelectedIndex);
                Assert.AreEqual(2, retry.Attempts.Count);
                Assert.AreEqual(.4f, retry.Attempts[0].Recognition.Confidence, .00001);
                Assert.AreEqual(.95f, region.Recognition.Confidence, .00001);
                Assert.AreSame(region.Recognition, retry.Attempts[1].Recognition);
                Assert.AreSame(region.Region.Polygon, region.Geometry!.InputPolygon);
                Assert.IsFalse(retry.SkippedByRegionLimit);
            }
            Assert.AreEqual(0, input.ActivePreparedBatches);
        }

        [TestMethod]
        public async Task TiesKeepOriginalWhileRelativeAnglesDoNotAccumulate()
        {
            using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(1, 0,
                rotations: new[] { TextOrientation.Degrees180, TextOrientation.Clockwise90, TextOrientation.CounterClockwise90 }));
            using var input = new FakeOcrImageInput();
            OcrResult result = await fixture.Pipeline.RunAsync(input);
            foreach (OcrRegionResult region in result.Regions)
            {
                Assert.AreEqual(0, region.OrientationRetry!.SelectedIndex);
                CollectionAssert.AreEqual(new[] { TextOrientation.Degrees0, TextOrientation.Degrees180, TextOrientation.Clockwise90, TextOrientation.CounterClockwise90 }, region.OrientationRetry.Attempts.Select(a => a.Orientation).ToArray());
                Assert.AreEqual(TextOrientation.Degrees0, region.Region.Orientation);
            }
        }

        [TestMethod]
        public async Task RegionLimitIsExplicitAndHighConfidenceLinesNeverRetry()
        {
            using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(1, maximumRegionsPerImage: 1));
            using var input = new FakeOcrImageInput();
            OcrResult result = await fixture.Pipeline.RunAsync(input);
            Assert.AreEqual(2, result.Regions[0].OrientationRetry!.Attempts.Count);
            Assert.AreEqual(1, result.Regions[1].OrientationRetry!.Attempts.Count);
            Assert.IsTrue(result.Regions[1].OrientationRetry!.SkippedByRegionLimit);
            using OcrFixture noRetry = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(.8f));
            Assert.IsTrue((await noRetry.Pipeline.RunAsync(input)).Regions.All(r => r.OrientationRetry == null));
            Assert.AreEqual(1, noRetry.RecognitionProvider.LastSession!.RunCount + noRetry.RecognitionProvider.LastSession.SequenceArgMaxRunCount);
        }

        [TestMethod]
        public async Task EmptyOriginalQualifiesAndEmptyCandidatesNeverReplaceText()
        {
            int calls = 0;
            using OcrFixture empty = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(), recognitionFactory: _ => RetryOutputs(.95f, blank: ++calls == 1));
            using var input = new FakeOcrImageInput();
            Assert.IsTrue((await empty.Pipeline.RunAsync(input)).Regions.All(r => r.OrientationRetry!.SelectedIndex == 1 && r.OrientationRetry.Attempts[0].Recognition.Text == ""));
            calls = 0;
            using OcrFixture blankRetry = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(1), recognitionFactory: _ => RetryOutputs(.95f, blank: ++calls > 1));
            Assert.IsTrue((await blankRetry.Pipeline.RunAsync(input)).Regions.All(r => r.OrientationRetry!.SelectedIndex == 0 && r.Recognition.Text != ""));
        }

        [TestMethod]
        public async Task InsufficientConfidenceGainRetainsOriginalEvenAboveThreshold()
        {
            int calls = 0;
            using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(.8f, .2f), recognitionFactory: _ => RetryOutputs(++calls == 1 ? .7f : .85f));
            using var input = new FakeOcrImageInput();
            Assert.IsTrue((await fixture.Pipeline.RunAsync(input)).Regions.All(r => r.OrientationRetry!.SelectedIndex == 0));
        }

        [TestMethod]
        public async Task LaterCandidatesCompareAgainstCurrentWinnerWithoutAccumulatingRotation()
        {
            int calls = 0;
            float[] scores = { .3f, .5f, .45f, .9f };
            using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(.8f, .05f,
                rotations: new[] { TextOrientation.Degrees180, TextOrientation.Clockwise90, TextOrientation.CounterClockwise90 }), recognitionFactory: _ => RetryOutputs(scores[calls++]));
            using var input = new FakeOcrImageInput();
            OcrResult result = await fixture.Pipeline.RunAsync(input);
            Assert.AreEqual(4, calls);
            Assert.IsTrue(result.Regions.All(r => r.OrientationRetry!.SelectedIndex == 3 && r.Region.Orientation == TextOrientation.CounterClockwise90));
            CollectionAssert.AreEqual(new[] { TextOrientation.Degrees0, TextOrientation.Degrees180, TextOrientation.Clockwise90, TextOrientation.CounterClockwise90 }, result.Regions[0].OrientationRetry!.Attempts.Select(a => a.Orientation).ToArray());
        }

        [TestMethod]
        public async Task RightAngleRetryHonorsRejectWidthPolicyInsteadOfSilentlyCompressing()
        {
            using OcrFixture fixture = CreateOcrFixture(recognitionWidth: 16, overflowMode: RecognitionOverflowMode.Reject,
                retryOptions: new OcrOrientationRetryOptions(1, rotations: new[] { TextOrientation.Clockwise90 }), detectionFactory: _ => Outputs(
                    ("polygons", new Tensor<float>(new TensorShape(1, 3, 4, 2), Enumerable.Repeat(new[] { 5f, 5f, 7f, 5f, 7f, 85f, 5f, 85f }, 3).SelectMany(a => a).ToArray())),
                    ("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .95f, .9f, .8f }))));
            using var input = new FakeOcrImageInput();
            OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input));
            Assert.AreEqual(VisualErrorCodes.OcrRecognitionWidthExceeded, error.ErrorCode);
            Assert.AreEqual(0, error.RegionIndex);
            Assert.AreEqual(1, fixture.RecognitionProvider.LastSession!.RunCount + fixture.RecognitionProvider.LastSession.SequenceArgMaxRunCount);
            Assert.AreEqual(0, input.ActivePreparedBatches);
        }

        [TestMethod]
        public async Task RetryWindowsPreserveRawTracesAndOriginalSourceRegions()
        {
            using OcrFixture fixture = CreateOcrFixture(recognitionWidth: 8, overflowMode: RecognitionOverflowMode.SlidingWindow, retryOptions: new OcrOrientationRetryOptions(1));
            using var input = new FakeOcrImageInput();
            OcrResult result = await fixture.Pipeline.RunAsync(input);
            CollectionAssert.AreEqual(new[] { 0, 2 }, result.Regions.Select(r => r.Region.SourceIndex).ToArray());
            foreach (OcrRegionResult region in result.Regions)
            {
                Assert.AreEqual(2, region.OrientationRetry!.Attempts.Count);
                Assert.IsTrue(region.OrientationRetry.Attempts.All(a => a.RecognitionWindows.Count > 1));
                Assert.IsTrue(region.OrientationRetry.Attempts.SelectMany(a => a.RecognitionWindows).All(w => w.Recognition.SourceRegionIndex == region.Region.SourceIndex));
            }
            Assert.AreEqual(0, input.ActivePreparedBatches);
            var projection = new RoiProjection("zone", new VisualSize(200, 200), new VisualSize(100, 100),
                ImageTransform.Crop(new VisualSize(200, 200), new VisualSize(100, 100), new RectangleF(50, 50, 100, 100)), new RectangleF(50, 50, 100, 100));
            OcrResult projected = new ModelSpaceOcrRoiProjector().Project(result, projection);
            Assert.AreSame(result.Regions[1].OrientationRetry, projected.Regions[1].OrientationRetry);
            RoiMergedOcrResult merged = new RoiOcrResultMerger().Merge(new[] { new RoiProjectedResult<OcrResult>("zone", 1, projected) }, RoiResultMergeMode.HighestConfidence, .5f);
            foreach (var item in merged.Items)
            {
                int expected = item.Region.Region.SourceIndex;
                Assert.IsTrue(item.Region.OrientationRetry!.Attempts.All(a => a.Recognition.SourceRegionIndex == expected));
                Assert.IsTrue(item.Region.OrientationRetry.Attempts.SelectMany(a => a.RecognitionWindows).All(w => w.Recognition.SourceRegionIndex == expected));
            }
        }

        [TestMethod]
        public async Task RetryCropAndResultLimitsFailExplicitlyAndReleaseInputs()
        {
            foreach (bool cropLimit in new[] { true, false })
            {
                using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(1, maximumCropsPerImage: cropLimit ? 1 : 1024), maximumResultBytes: cropLimit ? 16000000 : 1100);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var input = new FakeOcrImageInput();
                    OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, new OcrExecutionOptions(disposeInputOnCompletion: true)));
                    Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, error.ErrorCode);
                    Assert.AreEqual(OcrPipelineStage.Recognition, error.Stage);
                    Assert.AreEqual(0, input.ActivePreparedBatches);
                    Assert.AreEqual(1, input.DisposeCount);
                }
            }
        }

        [TestMethod]
        public async Task RetryCropLimitAccumulatesAcrossRounds()
        {
            using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(1,
                rotations: new[] { TextOrientation.Degrees180, TextOrientation.Clockwise90 }, maximumCropsPerImage: 3));
            using var input = new FakeOcrImageInput();
            OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input));
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, error.ErrorCode);
            // Initial batch plus first retry; the second retry would raise the total crops from 2 to 4.
            Assert.AreEqual(2, fixture.RecognitionProvider.LastSession!.RunCount + fixture.RecognitionProvider.LastSession.SequenceArgMaxRunCount);
            Assert.AreEqual(0, input.ActivePreparedBatches);
        }

        [TestMethod]
        public async Task RetryFailureAndCancellationDoNotReturnPartialSuccessOrLeakGate()
        {
            foreach (bool cancel in new[] { false, true })
            {
                int calls = 0;
                using var source = new CancellationTokenSource();
                using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(), recognitionFactory: _ =>
                {
                    if (++calls == 2) { if (cancel) source.Cancel(); else throw new InvalidOperationException("retry failure"); }
                    return RetryOutputs(calls == 1 ? .4f : .95f);
                });
                var input = new FakeOcrImageInput();
                OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, new OcrExecutionOptions(disposeInputOnCompletion: true), source.Token));
                Assert.AreEqual(cancel ? VisualErrorCodes.Cancelled : VisualErrorCodes.OcrPipelineFailed, error.ErrorCode);
                Assert.AreEqual(OcrPipelineStage.Recognition, error.Stage);
                Assert.AreEqual(1, input.DisposeCount);
                Assert.AreEqual(0, input.ActivePreparedBatches);
                using var next = new FakeOcrImageInput();
                Assert.AreEqual(2, (await fixture.Pipeline.RunAsync(next)).Regions.Count);
            }
        }

        [TestMethod]
        public async Task ConcurrentRetryCallsIsolateEvidenceAndIndependentSessions()
        {
            using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(1), maximumConcurrency: 2);
            fixture.RecognitionProvider.Delay = TimeSpan.FromMilliseconds(5);
            using var first = new FakeOcrImageInput();
            using var second = new FakeOcrImageInput();
            OcrResult[] results = await Task.WhenAll(fixture.Pipeline.RunAsync(first), fixture.Pipeline.RunAsync(second));
            Assert.AreEqual(results[0].ComputeSha256(), results[1].ComputeSha256());
            Assert.AreNotSame(results[0].Regions[0].OrientationRetry, results[1].Regions[0].OrientationRetry);
            Assert.AreEqual(0, first.ActivePreparedBatches);
            Assert.AreEqual(0, second.ActivePreparedBatches);
            Assert.IsTrue(fixture.RecognitionProvider.CreatedSessions.All(s => s.MaximumActive == 1));
        }

        private static InferenceOutputs RetryOutputs(float score, bool blank = false)
        {
            float[] values = Probabilities(2, 6, 4, blank ? new int[12] : new[] { 0, 1, 1, 0, 2, 2, 3, 3, 0, 1, 1, 0 });
            for (int index = 0; index < values.Length; index++) if (values[index] > 0) values[index] = score;
            return InferenceOutputs.Create("logits", new Tensor<float>(new TensorShape(2, 6, 4), values));
        }
    }
}
