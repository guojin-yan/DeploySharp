using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    public sealed partial class OcrTests
    {
        private static OcrCropEnhancementOptions RetryEnhancement() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.ContrastNormalize);

        [TestMethod]
        public void EnhancementRetryOptionsCopyAndRejectAmbiguousOrUnboundedConfiguration()
        {
            var retry = new OcrEnhancementRetryOptions(RetryEnhancement());
            var original = new TextCropProfile("enhancement.retry", 8, OcrRecognitionWidthMode.Fixed, 16, 16);
            TextCropProfile profile = original.WithEnhancementRetry(retry).WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent)
                .WithOrientationRetry(new OcrOrientationRetryOptions()).WithRecognitionWindows(new OcrRecognitionWindowOptions());
            Assert.IsNull(original.EnhancementRetry); Assert.IsNull(original.CropProcessing);
            Assert.AreSame(retry, profile.EnhancementRetry); Assert.IsNotNull(profile.CropProcessing); Assert.IsNull(profile.CropProcessing.Enhancement);
            Assert.ThrowsExactly<ArgumentException>(() => profile.WithCropProcessing(new OcrCropProcessingOptions().WithEnhancement(RetryEnhancement())));
            Assert.ThrowsExactly<ArgumentException>(() => original.WithCropProcessing(new OcrCropProcessingOptions().WithEnhancement(RetryEnhancement())).WithEnhancementRetry(retry));
            Assert.ThrowsExactly<ArgumentNullException>(() => new OcrEnhancementRetryOptions(null!));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrEnhancementRetryOptions(RetryEnhancement(), float.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrEnhancementRetryOptions(RetryEnhancement(), minimumConfidenceGain: -1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrEnhancementRetryOptions(RetryEnhancement(), selectionPolicy: (OcrEnhancementSelectionPolicy)99));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrEnhancementRetryOptions(RetryEnhancement(), maximumCropsPerImage: 4097));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrEnhancementRetryOptions(RetryEnhancement(), maximumRegionsPerImage: 0));
        }

        [TestMethod]
        public void EnhancementRetryManyCopiesOrderedRecipesAndKeepsTheLegacyFirstRecipe()
        {
            OcrCropEnhancementOptions first = RetryEnhancement();
            OcrCropEnhancementOptions second = new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale, upscaleFactor: 2.5);
            var source = new[] { first, second };
            OcrEnhancementRetryOptions retry = OcrEnhancementRetryOptions.CreateMany(source, maximumRegionsPerImage: 3, maximumCropsPerImage: 12);

            Assert.AreSame(first, retry.Enhancement);
            Assert.AreEqual(2, retry.Enhancements.Count);
            Assert.AreSame(first, retry.Enhancements[0]);
            Assert.AreSame(second, retry.Enhancements[1]);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => OcrEnhancementRetryOptions.CreateMany(Array.Empty<OcrCropEnhancementOptions>()));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => OcrEnhancementRetryOptions.CreateMany(new[] { first, second, first, second, first }));
            Assert.ThrowsExactly<ArgumentException>(() => OcrEnhancementRetryOptions.CreateMany(new[] { first, (OcrCropEnhancementOptions)null! }));
            Assert.ThrowsExactly<ArgumentNullException>(() => OcrEnhancementRetryOptions.CreateMany(null!));
        }

        [TestMethod]
        public async Task EnhancementRetryManyRunsRecipesInOrderAndSelectsTheBestCandidate()
        {
            int calls = 0;
            OcrEnhancementRetryOptions retry = OcrEnhancementRetryOptions.CreateMany(
                new[]
                {
                    RetryEnhancement(),
                    new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale, upscaleFactor: 2)
                },
                confidenceThreshold: 1,
                selectionPolicy: OcrEnhancementSelectionPolicy.ConfidenceGain,
                minimumConfidenceGain: .05f,
                maximumRegionsPerImage: 2,
                maximumCropsPerImage: 8);
            using OcrFixture fixture = CreateOcrFixture(enhancementRetry: retry,
                recognitionFactory: _ => RetryOutputs(++calls == 1 ? .4f : calls == 2 ? .85f : .95f));
            using var input = new CropInput { QualityStep = 6 };

            OcrResult result = await fixture.Pipeline.RunAsync(input);

            Assert.AreEqual(3, calls, "The original pass and each ordered recipe should run once.");
            Assert.AreEqual(6, input.PhysicalCrops, "Two regions are processed by the original pass and two recipe candidates; each batch has no extra padding.");
            Assert.AreEqual(1, fixture.DetectionProvider.LastSession!.RunCount, "Recipe retries must not repeat detection.");
            Assert.AreEqual(3, result.Timing.Details!.RecognitionBatchCount);
            foreach (OcrRegionResult item in result.Regions)
            {
                OcrEnhancementRetryResult evidence = item.EnhancementRetry!;
                Assert.AreEqual(OcrEnhancementRetryDecision.CandidateSelected, evidence.Decision);
                Assert.AreEqual(2, evidence.Candidates.Count);
                CollectionAssert.AreEqual(new[] { 0, 1 }, evidence.CandidateRecipeIndices.ToArray());
                Assert.AreEqual(1, evidence.SelectedCandidateIndex);
                Assert.AreSame(evidence.Candidates[1], evidence.Candidate);
                Assert.AreEqual(.85f, evidence.Candidates[0].Recognition.Confidence, .00001);
                Assert.AreEqual(.95f, evidence.Candidates[1].Recognition.Confidence, .00001);
                Assert.AreSame(evidence.Candidates[1].Recognition, item.Recognition);
                Assert.IsNotNull(evidence.Candidates[0].CropDiagnostics[0].Enhanced);
                Assert.IsNotNull(evidence.Candidates[1].CropDiagnostics[0].Enhanced);
            }
            Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
        }

        [TestMethod]
        public async Task EnhancementRetryManyPreservesOriginalButRetainsAllCandidates()
        {
            int calls = 0;
            OcrEnhancementRetryOptions retry = OcrEnhancementRetryOptions.CreateMany(
                new[] { RetryEnhancement(), new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale) },
                confidenceThreshold: 1,
                selectionPolicy: OcrEnhancementSelectionPolicy.PreserveOriginal,
                maximumRegionsPerImage: 2,
                maximumCropsPerImage: 8);
            using OcrFixture fixture = CreateOcrFixture(enhancementRetry: retry,
                recognitionFactory: _ => RetryOutputs(++calls == 1 ? .4f : calls == 2 ? .9f : .95f));
            using var input = new CropInput { QualityStep = 6 };

            OcrResult result = await fixture.Pipeline.RunAsync(input);

            foreach (OcrRegionResult item in result.Regions)
            {
                OcrEnhancementRetryResult evidence = item.EnhancementRetry!;
                Assert.AreEqual(OcrEnhancementRetryDecision.PreservedByPolicy, evidence.Decision);
                Assert.IsNull(evidence.SelectedCandidateIndex);
                Assert.AreEqual(2, evidence.Candidates.Count);
                CollectionAssert.AreEqual(new[] { 0, 1 }, evidence.CandidateRecipeIndices.ToArray());
                Assert.AreSame(evidence.Candidates[0], evidence.Candidate);
                Assert.AreSame(evidence.Original.Recognition, item.Recognition);
            }
            Assert.AreEqual(3, calls);
            Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
        }

        [TestMethod]
        public async Task EnhancementRetryManyReportsRecipeIndexWhenAnEarlierRecipeIsIneligible()
        {
            int calls = 0;
            OcrEnhancementRetryOptions retry = OcrEnhancementRetryOptions.CreateMany(
                new[] { RetryEnhancement(), new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale) },
                confidenceThreshold: 1,
                maximumRegionsPerImage: 2,
                maximumCropsPerImage: 8);
            using OcrFixture fixture = CreateOcrFixture(enhancementRetry: retry,
                recognitionFactory: _ => RetryOutputs(++calls == 1 ? .4f : .9f));
            using var input = new CropInput { QualityStep = 0 };

            OcrResult result = await fixture.Pipeline.RunAsync(input);

            Assert.AreEqual(2, calls, "The ineligible contrast recipe must be skipped without a recognizer call.");
            Assert.AreEqual(4, input.PhysicalCrops);
            foreach (OcrRegionResult item in result.Regions)
            {
                OcrEnhancementRetryResult evidence = item.EnhancementRetry!;
                Assert.AreEqual(1, evidence.Candidates.Count);
                CollectionAssert.AreEqual(new[] { 1 }, evidence.CandidateRecipeIndices.ToArray());
                Assert.AreSame(evidence.Candidates[0], evidence.Candidate);
            }
            Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
        }

        [TestMethod]
        public async Task EnhancementSelectionPreservesEarlierWidthRetryEvidence()
        {
            int calls = 0;
            var widthRetry = new OcrWidthRetryOptions(32, confidenceThreshold: .8f,
                selectionPolicy: OcrWidthRetrySelectionPolicy.ConfidenceGain, minimumConfidenceGain: .01f,
                maximumRegionsPerImage: 2, maximumCropsPerImage: 8);
            var enhancementRetry = new OcrEnhancementRetryOptions(RetryEnhancement(), confidenceThreshold: 1,
                selectionPolicy: OcrEnhancementSelectionPolicy.ConfidenceGain, minimumConfidenceGain: .01f,
                maximumRegionsPerImage: 2, maximumCropsPerImage: 8);
            using OcrFixture fixture = CreateOcrFixture(dynamicWidth: true, widthRetry: widthRetry, enhancementRetry: enhancementRetry,
                recognitionFactory: _ => RetryOutputs(++calls == 1 ? .4f : calls == 2 ? .95f : .99f));
            using var input = new CropInput { QualityStep = 6 };

            OcrResult result = await fixture.Pipeline.RunAsync(input);

            Assert.AreEqual(3, calls);
            Assert.AreEqual(6, input.PhysicalCrops);
            foreach (OcrRegionResult item in result.Regions)
            {
                Assert.IsNotNull(item.WidthRetry, "An enhancement-selected result must retain the earlier width retry trace.");
                Assert.IsNotNull(item.WidthRetry!.Candidate);
                Assert.IsTrue(item.WidthRetry.Candidate!.RecognitionWidth!.Value.TargetWidth > 8);
                Assert.AreEqual(OcrEnhancementRetryDecision.CandidateSelected, item.EnhancementRetry!.Decision);
                Assert.AreSame(item.EnhancementRetry.Candidate!.Recognition, item.Recognition);
            }
            Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
        }

        [TestMethod]
        public async Task EnhancementRetryDefaultKeepsOriginalAndPreservesBetterCandidateWithoutRepeatingDetector()
        {
            int calls = 0;
            using OcrFixture baseline = CreateOcrFixture(recognitionFactory: _ => RetryOutputs(.4f));
            using OcrFixture enabled = CreateOcrFixture(enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement()), recognitionFactory: _ => RetryOutputs(++calls == 1 ? .4f : .95f));
            using var plain = new FakeOcrImageInput(); using var input = new CropInput { QualityStep = 6 };
            OcrResult expected = await baseline.Pipeline.RunAsync(plain);
            OcrResult actual = await enabled.Pipeline.RunAsync(input);
            Assert.AreEqual(expected.ComputeSha256(), actual.ComputeSha256()); Assert.AreEqual(2, calls);
            Assert.AreEqual(1, enabled.DetectionProvider.LastSession!.RunCount); Assert.AreEqual(4, input.PhysicalCrops);
            Assert.AreEqual(2, actual.Timing.Details!.RecognitionBatchCount); Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
            foreach (OcrRegionResult item in actual.Regions)
            {
                OcrEnhancementRetryResult retry = item.EnhancementRetry!;
                Assert.AreEqual(OcrEnhancementRetryDecision.PreservedByPolicy, retry.Decision);
                Assert.AreSame(item.Recognition, retry.Original.Recognition); Assert.IsNotNull(retry.Candidate);
                Assert.AreEqual(.95f, retry.Candidate.Recognition.Confidence, .00001);
                Assert.IsNull(retry.Original.CropDiagnostics[0].Enhanced); Assert.IsNotNull(retry.Candidate.CropDiagnostics[0].Enhanced);
            }
        }

        [TestMethod]
        public async Task EnhancementRetryExplicitSelectionRequiresStrictGainAndNeverSelectsEmptyCandidate()
        {
            foreach (int scenario in new[] { 0, 1, 2, 3, 4 })
            {
                int calls = 0;
                using OcrFixture fixture = CreateOcrFixture(enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement(), 1, OcrEnhancementSelectionPolicy.ConfidenceGain, .05f),
                    recognitionFactory: _ => RetryOutputs(++calls == 1 ? .4f : scenario == 1 ? .4f : scenario == 2 ? .42f : .95f,
                        blank: (scenario == 3 && calls > 1) || (scenario == 4 && calls == 1)));
                using var input = new CropInput { QualityStep = 6 };
                OcrResult result = await fixture.Pipeline.RunAsync(input);
                foreach (OcrRegionResult item in result.Regions)
                {
                    bool selected = scenario == 0 || scenario == 4;
                    Assert.AreEqual(selected ? OcrEnhancementRetryDecision.CandidateSelected : OcrEnhancementRetryDecision.InsufficientGain, item.EnhancementRetry!.Decision);
                    Assert.AreSame(selected ? item.EnhancementRetry.Candidate!.Recognition : item.EnhancementRetry.Original.Recognition, item.Recognition);
                }
            }
        }

        [TestMethod]
        public async Task EnhancementRetryQualityAndConfidenceGatesAvoidUnnecessaryRecognition()
        {
            foreach (int step in new[] { 0, 64, 6 })
            {
                using OcrFixture fixture = CreateOcrFixture(enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement()), recognitionFactory: _ => RetryOutputs(step == 6 ? .95f : .4f));
                using var input = new CropInput { QualityStep = step };
                OcrResult result = await fixture.Pipeline.RunAsync(input);
                Assert.AreEqual(2, input.PhysicalCrops); Assert.AreEqual(1, result.Timing.Details!.RecognitionBatchCount);
                foreach (OcrRegionResult item in result.Regions)
                    if (step == 6) Assert.IsNull(item.EnhancementRetry);
                    else { Assert.AreEqual(OcrEnhancementRetryDecision.NoEligibleCrop, item.EnhancementRetry!.Decision); Assert.IsNull(item.EnhancementRetry.Candidate); }
            }
        }

        [TestMethod]
        public async Task EnhancementRetryRegionLimitAndPhysicalPaddingAreExplicit()
        {
            foreach (int cropLimit in new[] { 1, 2 })
            {
                using OcrFixture fixture = CreateOcrFixture(enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement(), 1, maximumRegionsPerImage: 1, maximumCropsPerImage: cropLimit));
                using var input = new CropInput { QualityStep = 6 };
                if (cropLimit == 1)
                {
                    Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, (await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input))).ErrorCode);
                    Assert.AreEqual(2, input.PhysicalCrops, "One candidate row requires two physical rows for this model.");
                }
                else
                {
                    OcrResult result = await fixture.Pipeline.RunAsync(input);
                    Assert.AreEqual(OcrEnhancementRetryDecision.PreservedByPolicy, result.Regions[0].EnhancementRetry!.Decision);
                    Assert.AreEqual(OcrEnhancementRetryDecision.RegionLimit, result.Regions[1].EnhancementRetry!.Decision);
                    Assert.IsNull(result.Regions[1].EnhancementRetry!.Candidate); Assert.AreEqual(4, input.PhysicalCrops);
                }
                Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
            }
        }

        [TestMethod]
        public async Task EnhancementKeepsOriginalPhysicalWidthWhenOnlyPaddedLineIsRetried()
        {
            using OcrFixture fixture = CreateOcrFixture(dynamicWidth: true, enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement(), 1));
            using var input = new CropInput { RegionQualityStep = index => index == 2 ? 6 : 0 };
            OcrResult result = await fixture.Pipeline.RunAsync(input);
            OcrEnhancementRetryResult retry = result.Regions[1].EnhancementRetry!;
            Assert.AreEqual(12, retry.Original.RecognitionWidth!.Value.TargetWidth);
            Assert.AreEqual(16, retry.Original.RecognitionWidth.Value.TensorWidth);
            Assert.AreEqual(16, retry.Candidate!.RecognitionWidth!.Value.TensorWidth);
            Assert.AreEqual(retry.Original.CropDiagnostics[0].TensorSize, retry.Candidate.CropDiagnostics[0].TensorSize);
            Assert.AreEqual(OcrEnhancementRetryDecision.NoEligibleCrop, result.Regions[0].EnhancementRetry!.Decision);
            Assert.AreEqual(4, input.PhysicalCrops);
        }

        [TestMethod]
        public async Task EnhancementFollowsSelectedOrientationAndKeepsBothTraces()
        {
            int calls = 0;
            using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(),
                enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement(), .9f, OcrEnhancementSelectionPolicy.ConfidenceGain),
                recognitionFactory: _ => RetryOutputs(++calls == 1 ? .4f : calls == 2 ? .7f : .95f));
            using var input = new CropInput { QualityStep = 6 };
            OcrResult result = await fixture.Pipeline.RunAsync(input);
            Assert.AreEqual(3, calls); Assert.AreEqual(6, input.PhysicalCrops);
            foreach (OcrRegionResult item in result.Regions)
            {
                Assert.AreEqual(1, item.OrientationRetry!.SelectedIndex); Assert.AreEqual(2, item.OrientationRetry.Attempts.Count);
                Assert.AreSame(item.OrientationRetry.Attempts[1].Recognition, item.EnhancementRetry!.Original.Recognition);
                Assert.AreEqual(TextOrientation.Degrees180, item.Region.Orientation);
                Assert.AreEqual(TextOrientation.Degrees180, item.EnhancementRetry.Candidate!.Orientation);
                Assert.AreEqual(OcrEnhancementRetryDecision.CandidateSelected, item.EnhancementRetry.Decision);
            }
        }

        [TestMethod]
        public async Task EnhancementSharesCropAndRetainedByteBudgetsWithEarlierOrientationRetries()
        {
            foreach (bool pixelBudget in new[] { true, false })
            {
                using OcrFixture fixture = CreateOcrFixture(retryOptions: new OcrOrientationRetryOptions(1),
                    enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement(), 1),
                    cropProcessing: new OcrCropProcessingOptions(maximumCropsPerCall: pixelBudget ? 5 : 1024), maximumResultBytes: pixelBudget ? 16000000 : 8000);
                var input = new CropInput { QualityStep = 6 };
                OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, new OcrExecutionOptions(disposeInputOnCompletion: true)));
                Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, error.ErrorCode); Assert.AreEqual(4, input.PhysicalCrops);
                Assert.AreEqual(0, input.Inner.ActivePreparedBatches); Assert.AreEqual(1, input.Inner.DisposeCount);
            }
        }

        [TestMethod]
        public async Task EnhancementWindowsKeepOriginalAndCandidateCropEvidenceAndRoiReindexing()
        {
            using OcrFixture fixture = CreateOcrFixture(recognitionWidth: 8, windowOptions: new OcrRecognitionWindowOptions(),
                enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement(), 1));
            using var input = new CropInput { QualityStep = 6 };
            OcrResult result = await fixture.Pipeline.RunAsync(input);
            var size = new VisualSize(200, 200); var bounds = new RectangleF(50,50,100,100);
            var projection = new RoiProjection("enhance", size, input.SourceSize, ImageTransform.Crop(size, input.SourceSize, bounds), bounds);
            OcrResult projected = new ModelSpaceOcrRoiProjector().Project(result, projection);
            OcrResult merged = new RoiOcrResultMerger().Merge(new[] { new RoiProjectedResult<OcrResult>("enhance", 1, projected) }).Result;
            for (int i = 0; i < result.Regions.Count; i++)
            {
                OcrEnhancementRetryResult original = result.Regions[i].EnhancementRetry!, retry = merged.Regions[i].EnhancementRetry!;
                Assert.IsTrue(retry.Candidate!.RecognitionWindows.Count > 1);
                Assert.AreEqual(retry.Candidate.RecognitionWindows.Count, retry.Candidate.CropDiagnostics.Count);
                Assert.AreEqual(i, retry.Candidate.Recognition.SourceRegionIndex); Assert.AreEqual(i, retry.Original.Recognition.SourceRegionIndex);
                Assert.AreSame(original.Candidate!.CropDiagnostics[0], retry.Candidate.CropDiagnostics[0]);
                Assert.AreEqual(result.Regions[i].Region.SourceIndex, retry.Candidate.CropDiagnostics[0].InputRegionIndex);
            }
            Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
        }

        [TestMethod]
        public async Task EnhancementFailureCancellationAndConcurrentCallsPreserveOwnershipAndGate()
        {
            foreach (bool cancel in new[] { false, true })
            {
                int calls = 0; using var source = new CancellationTokenSource();
                using OcrFixture fixture = CreateOcrFixture(enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement(), 1), recognitionFactory: _ =>
                { if (++calls == 2) { if (cancel) source.Cancel(); else throw new InvalidOperationException("enhancement failure"); } return RetryOutputs(.4f); });
                var input = new CropInput { QualityStep = 6 };
                OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, new OcrExecutionOptions(disposeInputOnCompletion: true), source.Token));
                Assert.AreEqual(cancel ? VisualErrorCodes.Cancelled : VisualErrorCodes.OcrPipelineFailed, error.ErrorCode);
                Assert.AreEqual(1, input.Inner.DisposeCount); Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
                using var recovered = new CropInput { QualityStep = 6 }; Assert.AreEqual(2, (await fixture.Pipeline.RunAsync(recovered)).Regions.Count);
            }
            using OcrFixture concurrent = CreateOcrFixture(enhancementRetry: new OcrEnhancementRetryOptions(RetryEnhancement(), 1), maximumConcurrency: 2);
            concurrent.RecognitionProvider.Delay = TimeSpan.FromMilliseconds(5);
            using var first = new CropInput { QualityStep = 6 }; using var second = new CropInput { QualityStep = 6 };
            OcrResult[] results = await Task.WhenAll(concurrent.Pipeline.RunAsync(first), concurrent.Pipeline.RunAsync(second));
            Assert.AreNotSame(results[0].Regions[0].EnhancementRetry, results[1].Regions[0].EnhancementRetry);
            Assert.AreEqual(0, first.Inner.ActivePreparedBatches); Assert.AreEqual(0, second.Inner.ActivePreparedBatches);
            Assert.IsTrue(concurrent.RecognitionProvider.CreatedSessions.All(s => s.MaximumActive == 1));
        }
    }
}
