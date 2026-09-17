using System;
using System.Collections.Generic;
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
        [TestMethod]
        public void CropOptionsCopyWithoutMutatingDefaultsAndRejectInvalidBounds()
        {
            var original = new TextCropProfile("crop.test", 8, OcrRecognitionWidthMode.Fixed, 16, 16);
            var options = new OcrCropProcessingOptions(64, 4);
            TextCropProfile enabled = original.WithCropProcessing(options).WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent)
                .WithOrientationRetry(new OcrOrientationRetryOptions()).WithGeometryValidation(new OcrGeometryOptions());
            Assert.IsNull(original.CropProcessing); Assert.AreSame(options, enabled.CropProcessing);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropProcessingOptions(0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropProcessingOptions(65537));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropProcessingOptions(maximumCropsPerCall: 4097));
            Assert.ThrowsExactly<ArgumentNullException>(() => original.WithCropProcessing(null!));
        }

        [TestMethod]
        public async Task CropEvidenceKeepsWindowOrderAndHashesAndAllRetryAttempts()
        {
            foreach (bool windowed in new[] { false, true })
            {
                var retry = new OcrOrientationRetryOptions(confidenceThreshold: 1, maximumRegionsPerImage: 1);
                var windows = windowed ? new OcrRecognitionWindowOptions() : null;
                using OcrFixture baseline = CreateOcrFixture(recognitionWidth: 8, windowOptions: windows, retryOptions: retry);
                using OcrFixture enabled = CreateOcrFixture(recognitionWidth: 8, windowOptions: windows, retryOptions: retry, cropProcessing: new OcrCropProcessingOptions(32));
                using var plain = new FakeOcrImageInput(); using var input = new CropInput();
                OcrResult expected = await baseline.Pipeline.RunAsync(plain);
                OcrResult actual = await enabled.Pipeline.RunAsync(input);
                Assert.AreEqual(expected.ComputeSha256(), actual.ComputeSha256());
                Assert.IsTrue(input.PhysicalCrops > 2); Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
                foreach (OcrRegionResult region in actual.Regions)
                {
                    Assert.AreEqual(Math.Max(1, region.RecognitionWindows.Count), region.CropDiagnostics.Count);
                    foreach (OcrOrientationAttempt attempt in region.OrientationRetry!.Attempts)
                    {
                        Assert.AreEqual(Math.Max(1, attempt.RecognitionWindows.Count), attempt.CropDiagnostics.Count);
                        foreach (OcrCropDiagnostics diagnostic in attempt.CropDiagnostics)
                        {
                            Assert.AreEqual(region.Region.SourceIndex, diagnostic.InputRegionIndex);
                            Assert.AreEqual(attempt.RecognitionWindows.Count > 1 ? TextOrientation.Degrees0 : attempt.Orientation, diagnostic.Orientation,
                                "Window corner roles already encode the parent orientation; do not rotate them twice.");
                            Assert.IsTrue(diagnostic.Rectified.SampleCount <= 32);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public async Task CropBudgetIncludesPaddedRetryRowsAndStopsBeforePreparingExtraBatches()
        {
            using OcrFixture fixture = CreateOcrFixture(cropProcessing: new OcrCropProcessingOptions(maximumCropsPerCall: 3),
                retryOptions: new OcrOrientationRetryOptions(confidenceThreshold: 1, maximumRegionsPerImage: 1));
            using var input = new CropInput();
            OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input));
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, error.ErrorCode);
            Assert.AreEqual(2, input.PhysicalCrops, "One retry row needs two physical rows due to the model's minimum batch.");
            Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
            using OcrFixture small = CreateOcrFixture(cropProcessing: new OcrCropProcessingOptions(), maximumResultBytes: 1024);
            using var other = new CropInput();
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, (await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => small.Pipeline.RunAsync(other))).ErrorCode);
            Assert.AreEqual(0, other.PhysicalCrops);
        }

        [TestMethod]
        public async Task CropUnsupportedMalformedFailureAndCancellationReleaseInputAndGate()
        {
            using OcrFixture fixture = CreateOcrFixture(cropProcessing: new OcrCropProcessingOptions(16));
            using var unsupported = new FakeOcrImageInput();
            Assert.AreEqual(VisualErrorCodes.OcrCropProcessingUnavailable, (await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(unsupported))).ErrorCode);
            foreach (int mode in new[] { 0, 1, 2 })
            {
                using var cancellation = new CancellationTokenSource();
                var input = new CropInput { WrongRegion = mode == 0, BeforePrepare = () => { if (mode == 1) throw new InvalidOperationException("crop failure"); if (mode == 2) cancellation.Cancel(); } };
                await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, new OcrExecutionOptions(disposeInputOnCompletion: true), cancellation.Token));
                Assert.AreEqual(1, input.Inner.DisposeCount); Assert.AreEqual(0, input.Inner.ActivePreparedBatches);
            }
            using var valid = new CropInput(); Assert.AreEqual(2, (await fixture.Pipeline.RunAsync(valid)).Regions.Count);
        }

        [TestMethod]
        public async Task CropEvidenceSurvivesRoiProjectionReindexingAndConcurrentCalls()
        {
            using OcrFixture fixture = CreateOcrFixture(cropProcessing: new OcrCropProcessingOptions(), maximumConcurrency: 2);
            using var first = new CropInput(); using var second = new CropInput();
            OcrResult[] results = await Task.WhenAll(fixture.Pipeline.RunAsync(first), fixture.Pipeline.RunAsync(second));
            var size = new VisualSize(200, 200); var bounds = new RectangleF(50, 50, 100, 100);
            var projection = new RoiProjection("crop", size, first.SourceSize, ImageTransform.Crop(size, first.SourceSize, bounds), bounds);
            OcrResult projected = new ModelSpaceOcrRoiProjector().Project(results[0], projection);
            OcrResult merged = new RoiOcrResultMerger().Merge(new[] { new RoiProjectedResult<OcrResult>("crop", 1, projected) }).Result;
            for (int i = 0; i < merged.Regions.Count; i++)
            {
                Assert.AreSame(results[0].Regions[i].CropDiagnostics[0], merged.Regions[i].CropDiagnostics[0]);
                Assert.AreEqual(results[0].Regions[i].Region.SourceIndex, merged.Regions[i].CropDiagnostics[0].InputRegionIndex);
                Assert.AreEqual(i, merged.Regions[i].Region.SourceIndex);
            }
            Assert.AreEqual(0, first.Inner.ActivePreparedBatches); Assert.AreEqual(0, second.Inner.ActivePreparedBatches);
        }

        [TestMethod]
        public void ProcessedBatchTransfersOwnershipOnlyOnSuccessAndCopiesEvidence()
        {
            using var input = new CropInput();
            var quad = new TextQuadrilateral(new PointF(0,0), new PointF(16,0), new PointF(16,8), new PointF(0,8), TextCornerOrder.TopLeftClockwise);
            var request = new TextCropRequest(new TextRegion(0, 1, quad.Polygon, quad), new TextCropProfile("ownership.crop", 8, OcrRecognitionWidthMode.Fixed, 16, 16).WithCropProcessing(new OcrCropProcessingOptions()));
            using OcrPreparedCropBatch sample = input.PrepareProcessedRecognitionBatch("crops", new[] { request }, CancellationToken.None);
            PreparedVisualInput prepared = input.Inner.PrepareRecognitionBatch("crops", new[] { request }, CancellationToken.None);
            Assert.AreEqual(2, input.Inner.ActivePreparedBatches);
            Assert.ThrowsExactly<ArgumentException>(() => new OcrPreparedCropBatch(prepared, Array.Empty<OcrCropDiagnostics>()));
            Assert.AreEqual(2, input.Inner.ActivePreparedBatches, "Failed construction leaves ownership with the caller.");
            var mutable = new List<OcrCropDiagnostics>(sample.Diagnostics);
            using var owned = new OcrPreparedCropBatch(prepared, mutable); mutable.Clear();
            Assert.AreEqual(1, owned.Diagnostics.Count); owned.Dispose(); owned.Dispose();
            Assert.AreEqual(1, input.Inner.ActivePreparedBatches); Assert.AreSame(sample.Diagnostics[0], owned.Diagnostics[0]);
        }

        private sealed class CropInput : IOcrCropProcessingInput
        {
            internal FakeOcrImageInput Inner { get; } = new FakeOcrImageInput();
            internal Action? BeforePrepare { get; set; }
            internal bool WrongRegion { get; set; }
            private int _physicalCrops;
            internal int PhysicalCrops => Volatile.Read(ref _physicalCrops);
            internal int QualityStep { get; set; }
            internal Func<int, int>? RegionQualityStep { get; set; }
            public VisualSize SourceSize => Inner.SourceSize;
            public PreparedVisualInput DetectionInput => Inner.DetectionInput;
            public PreparedVisualInput PrepareRecognitionBatch(string name, IReadOnlyList<TextCropRequest> requests, CancellationToken token) => Inner.PrepareRecognitionBatch(name, requests, token);
            public OcrPreparedCropBatch PrepareProcessedRecognitionBatch(string name, IReadOnlyList<TextCropRequest> requests, CancellationToken token)
            {
                BeforePrepare?.Invoke(); token.ThrowIfCancellationRequested(); Interlocked.Add(ref _physicalCrops, requests.Count);
                PreparedVisualInput prepared = Inner.PrepareRecognitionBatch(name, requests, token);
                try
                {
                    var rows = new List<OcrCropDiagnostics>();
                    foreach (TextCropRequest request in requests)
                    {
                        TextCropRequest source = request;
                        if (WrongRegion) source = new TextCropRequest(new TextRegion(99, 1, request.Region.Polygon, request.Quadrilateral), request.Profile);
                        var quality = new OcrPixelQualityOptions(request.Profile.CropProcessing!.MaximumSamplesPerStage);
                        int qualityStep = RegionQualityStep?.Invoke(request.Region.SourceIndex) ?? QualityStep;
                        OcrPixelQualityDiagnostics rectified = OcrPixelQualityAnalyzer.Analyze(new VisualSize(40, 20), (x, _) => (byte)(100 + (x % 2 == 0 ? -qualityStep : qualityStep)), quality);
                        bool apply = OcrCropEnhancementPolicy.Decide(rectified, request.Profile.CropProcessing.Enhancement) == OcrCropEnhancementDecision.Applied;
                        rows.Add(new OcrCropDiagnostics(source, rectified,
                            OcrPixelQualityAnalyzer.Analyze(new VisualSize(request.TargetWidth, request.TargetHeight), (_, _) => 100, quality), apply ? rectified : null));
                    }
                    return new OcrPreparedCropBatch(prepared, rows);
                }
                catch { prepared.Dispose(); throw; }
            }
            public void Dispose() => Inner.Dispose();
        }
    }
}
