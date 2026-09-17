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
    [TestClass]
    public sealed class OcrPixelQualityTests
    {
        [TestMethod]
        public void ConstantAndCheckerPixelsHaveExplicitStatisticsWithoutInventingNoiseOrExposure()
        {
            var size = new VisualSize(8, 8);
            OcrPixelQualityDiagnostics black = OcrPixelQualityAnalyzer.Analyze(size, (_, _) => 0);
            Assert.AreEqual(64, black.SampleCount); Assert.AreEqual(36, black.NeighborhoodCount);
            Assert.AreEqual(0d, black.MeanLuminance); Assert.AreEqual(0d, black.LaplacianVariance);
            Assert.AreEqual(1d, black.DarkPixelFraction); Assert.AreEqual(0d, black.LightPixelFraction);
            OcrPixelQualityDiagnostics white = OcrPixelQualityAnalyzer.Analyze(size, (_, _) => 255);
            Assert.AreEqual(1d, white.LightPixelFraction); Assert.AreEqual(0d, white.LuminanceStandardDeviation);
            OcrPixelQualityDiagnostics checker = OcrPixelQualityAnalyzer.Analyze(size, (x, y) => (byte)((x + y) % 2 == 0 ? 0 : 255));
            Assert.AreEqual(127.5, checker.MeanLuminance!.Value, 1e-9); Assert.AreEqual(127.5, checker.LuminanceStandardDeviation!.Value, 1e-9);
            Assert.AreEqual(1040400d, checker.LaplacianVariance!.Value, 1e-8);
            Assert.AreEqual(0d, checker.MeanAbsoluteGradient, "A Nyquist pattern exposes the limitations of central differences; it is not a universal quality score.");
        }

        [TestMethod]
        public void GridIsBoundedDeterministicNativeScaleAndCancellationAware()
        {
            var size = new VisualSize(100000, 17);
            int reads = 0;
            var options = new OcrPixelQualityOptions(7);
            OcrPixelQualityDiagnostics report = OcrPixelQualityAnalyzer.Analyze(size, (x, y) =>
            { Assert.IsTrue(x >= 0 && x < size.Width && y >= 0 && y < size.Height); reads++; return 90; }, options);
            Assert.IsTrue(report.PlannedSampleCount <= 7); Assert.IsTrue(reads <= 35);
            Assert.AreEqual(90d, report.MeanLuminance);
            Parallel.For(0, 16, _ => Assert.AreEqual(report.SampleCount, OcrPixelQualityAnalyzer.Analyze(size, (_, _) => 90, options).SampleCount));
            using var cancellation = new CancellationTokenSource();
            Assert.ThrowsExactly<OperationCanceledException>(() => OcrPixelQualityAnalyzer.Analyze(new VisualSize(100, 100), (_, _) => { cancellation.Cancel(); return 10; }, cancellationToken: cancellation.Token));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrPixelQualityOptions(0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrPixelQualityOptions(maximumRegions: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrPixelQualityOptions(maximumSamplesPerCall: 4));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => OcrPixelQualityAnalyzer.Analyze(default, (_, _) => 0));
        }

        [TestMethod]
        public void PolygonExcludesOutsidePixelsAndMissingEvidenceIsNullNotGoodQuality()
        {
            var polygon = TextPolygon.Canonicalize(new[] { new PointF(0, 0), new PointF(8, 0), new PointF(0, 8) }, OrientedVertexOrder.CounterClockwise);
            var region = new TextRegion(4, .9f, polygon);
            OcrPixelQualityDiagnostics report = OcrPixelQualityAnalyzer.Analyze(new VisualSize(10, 10), (x, y) =>
            { Assert.IsTrue(x + y <= 7); return 100; }, region: region);
            Assert.AreEqual(36, report.SampleCount); Assert.AreSame(polygon, report.InputPolygon);
            Assert.AreEqual(4, report.InputRegionIndex); Assert.AreEqual(8d, report.RegionMinimumEdgePixels);
            var outside = TextPolygon.Canonicalize(new[] { new PointF(20, 20), new PointF(25, 20), new PointF(25, 25), new PointF(20, 25) }, OrientedVertexOrder.CounterClockwise);
            OcrPixelQualityDiagnostics empty = OcrPixelQualityAnalyzer.Analyze(new VisualSize(10, 10), (_, _) => throw new InvalidOperationException(), region: new TextRegion(2, 1, outside));
            Assert.AreEqual(0, empty.SampleCount); Assert.IsNull(empty.MeanLuminance); Assert.IsNull(empty.LaplacianVariance);
            OcrPixelQualityDiagnostics tiny = OcrPixelQualityAnalyzer.Analyze(new VisualSize(1, 1), (_, _) => 42);
            Assert.AreEqual(42d, tiny.MeanLuminance); Assert.IsNull(tiny.LaplacianVariance); Assert.IsNull(tiny.MeanAbsoluteGradient);
        }

        [TestMethod]
        public void ControlledBlurAndContrastReductionChangeMetricsWithoutAQualityClassifier()
        {
            var size = new VisualSize(32, 16);
            byte Edge(int x, int y) => x < 16 ? (byte)0 : (byte)255;
            byte Blur(int x, int y) => (byte)(Enumerable.Range(-2, 5).Sum(dx => Edge(x + dx, y)) / 5);
            OcrPixelQualityDiagnostics sharp = OcrPixelQualityAnalyzer.Analyze(size, Edge);
            OcrPixelQualityDiagnostics blur = OcrPixelQualityAnalyzer.Analyze(size, Blur);
            OcrPixelQualityDiagnostics lowContrast = OcrPixelQualityAnalyzer.Analyze(size, (x, y) => x < 16 ? (byte)110 : (byte)145);
            Assert.IsTrue(blur.LaplacianVariance < sharp.LaplacianVariance);
            Assert.IsTrue(lowContrast.LuminanceStandardDeviation < sharp.LuminanceStandardDeviation);
            Assert.AreEqual(sharp.SampleCount, blur.SampleCount);
        }

        [TestMethod]
        public void CombinedHalfPlaneNeighborhoodCheckMatchesFourIndependentTestsOnSubpixelQuad()
        {
            var quad = new TextQuadrilateral(new PointF(2.1f, 1.3f), new PointF(13.7f, 4.1f), new PointF(11.4f, 13.9f), new PointF(.2f, 9.8f), TextCornerOrder.TopLeftClockwise);
            var region = new TextRegion(0, 1, quad.Polygon, quad);
            bool Inside(int x, int y)
            {
                for (int i = 0; i < quad.Polygon.Vertices.Count; i++)
                {
                    PointF a = quad.Polygon.Vertices[i], b = quad.Polygon.Vertices[(i + 1) % quad.Polygon.Vertices.Count];
                    if (((double)b.X - a.X) * (y + .5 - a.Y) - ((double)b.Y - a.Y) * (x + .5 - a.X) < 0) return false;
                }
                return true;
            }
            int centers = 0, neighbors = 0;
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                if (!Inside(x, y)) continue;
                centers++;
                if (x > 0 && y > 0 && x < 15 && y < 15 && Inside(x - 1, y) && Inside(x + 1, y) && Inside(x, y - 1) && Inside(x, y + 1)) neighbors++;
            }
            OcrPixelQualityDiagnostics result = OcrPixelQualityAnalyzer.Analyze(new VisualSize(16, 16), (_, _) => 100, region: region);
            Assert.AreEqual(centers, result.SampleCount); Assert.AreEqual(neighbors, result.NeighborhoodCount);
        }
    }

    public sealed partial class OcrTests
    {
        [TestMethod]
        public async Task PixelQualityIsOptInPreservesTextHashAndBoundsEveryCall()
        {
            using OcrFixture fixture = CreateOcrFixture();
            using var input = new QualityInput();
            var originalOptions = new OcrExecutionOptions(correlationId: "quality");
            OcrExecutionOptions enabled = originalOptions.WithPixelQuality(new OcrPixelQualityOptions());
            Assert.IsNull(originalOptions.PixelQuality); Assert.AreEqual("quality", enabled.CorrelationId);
            OcrResult baseline = await fixture.Pipeline.RunAsync(input, originalOptions);
            Assert.AreEqual(0, input.ReadCount); Assert.IsNull(baseline.PixelQuality);
            OcrResult result = await fixture.Pipeline.RunAsync(input, enabled);
            Assert.AreEqual(baseline.ComputeSha256(), result.ComputeSha256());
            Assert.AreEqual(3, input.ReadCount); Assert.IsNotNull(result.PixelQuality);
            Assert.IsTrue(result.Regions.All(item => item.PixelQuality != null));
            Assert.AreEqual(input.SourceSize, result.PixelQuality.InputSize);
            using var missingCapability = new FakeOcrImageInput();
            OcrPipelineException missing = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(missingCapability, enabled));
            Assert.AreEqual(VisualErrorCodes.OcrPixelQualityUnavailable, missing.ErrorCode);
            foreach (OcrPixelQualityOptions bounds in new[] { new OcrPixelQualityOptions(maximumRegions: 1), new OcrPixelQualityOptions(maximumSamplesPerCall: 4096) })
            {
                int reads = input.ReadCount;
                OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, originalOptions.WithPixelQuality(bounds)));
                Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, error.ErrorCode); Assert.AreEqual(reads, input.ReadCount);
            }
            Assert.AreEqual(2, (await fixture.Pipeline.RunAsync(input, enabled)).Regions.Count);
        }

        [TestMethod]
        public async Task PixelQualityFailureAndCancellationReleaseInputAndGateAndBudgetPrecedesReads()
        {
            using OcrFixture fixture = CreateOcrFixture();
            var options = new OcrExecutionOptions(disposeInputOnCompletion: true).WithPixelQuality(new OcrPixelQualityOptions());
            foreach (bool cancel in new[] { false, true })
            {
                using var cancellation = new CancellationTokenSource();
                var input = new QualityInput { BeforeRead = () => { if (cancel) cancellation.Cancel(); else throw new InvalidOperationException("pixel failure"); } };
                await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, options, cancellation.Token));
                Assert.AreEqual(1, input.Inner.DisposeCount); Assert.AreEqual(0, input.Inner.LastBatchSize);
            }
            using OcrFixture bounded = CreateOcrFixture(maximumResultBytes: 64);
            using var limited = new QualityInput();
            OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => bounded.Pipeline.RunAsync(limited, options));
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, error.ErrorCode); Assert.AreEqual(0, limited.ReadCount);
            using var valid = new QualityInput();
            Assert.AreEqual(2, (await fixture.Pipeline.RunAsync(valid, options)).Regions.Count);
        }

        [TestMethod]
        public async Task PixelQualityInitialRegionEvidenceSurvivesWindowsAndOrientationRetries()
        {
            using OcrFixture fixture = CreateOcrFixture(recognitionWidth: 8,
                windowOptions: new OcrRecognitionWindowOptions(), retryOptions: new OcrOrientationRetryOptions(confidenceThreshold: 1, maximumRegionsPerImage: 1));
            using var input = new QualityInput();
            OcrResult result = await fixture.Pipeline.RunAsync(input, new OcrExecutionOptions().WithPixelQuality(new OcrPixelQualityOptions()));
            Assert.AreEqual(3, input.ReadCount, "Do not resample each window or retry.");
            foreach (OcrRegionResult item in result.Regions)
            { Assert.IsNotNull(item.PixelQuality); Assert.AreEqual(item.Region.SourceIndex, item.PixelQuality.InputRegionIndex); }
        }

        private sealed class QualityInput : IOcrPixelQualityInput
        {
            internal FakeOcrImageInput Inner { get; } = new FakeOcrImageInput();
            internal Action? BeforeRead { get; set; }
            internal bool WrongSize { get; set; }
            internal int ReadCount { get; private set; }
            public VisualSize SourceSize => Inner.SourceSize;
            public PreparedVisualInput DetectionInput => Inner.DetectionInput;
            public PreparedVisualInput PrepareRecognitionBatch(string name, IReadOnlyList<TextCropRequest> requests, CancellationToken token) => Inner.PrepareRecognitionBatch(name, requests, token);
            public OcrPixelQualityDiagnostics AssessPixelQuality(TextRegion? region, OcrPixelQualityOptions options, CancellationToken token)
            { ReadCount++; BeforeRead?.Invoke(); return OcrPixelQualityAnalyzer.Analyze(WrongSize ? new VisualSize(1, 1) : SourceSize, (_, _) => 128, options, region, token); }
            public void Dispose() => Inner.Dispose();
        }

        [TestMethod]
        public async Task PixelQualityRoiProjectionFilteringAndMergeKeepOriginalEvidenceAndDoNotInventWholeImageMetrics()
        {
            using OcrFixture fixture = CreateOcrFixture();
            using var input = new QualityInput();
            OcrResult original = await fixture.Pipeline.RunAsync(input, new OcrExecutionOptions().WithPixelQuality(new OcrPixelQualityOptions()));
            var size = new VisualSize(200, 200);
            var crop = new RectangleF(50, 50, 100, 100);
            var projection = new RoiProjection("quality", size, input.SourceSize, ImageTransform.Crop(size, input.SourceSize, crop), crop);
            OcrResult projected = new ModelSpaceOcrRoiProjector().Project(original, projection);
            Assert.AreSame(original.PixelQuality, projected.PixelQuality);
            var snapshot = new VisualRoiSnapshot(size, new[] { new VisualRoi("all", new RectangleRoiGeometry(new RectangleF(0, 0, 200, 200))) });
            RoiOcrResult filtered = VisualRoiOcrFilter.Filter(projected, snapshot);
            Assert.AreSame(original.PixelQuality, filtered.Result.PixelQuality);
            RoiMergedOcrResult merged = new RoiOcrResultMerger().Merge(new[] { new RoiProjectedResult<OcrResult>("quality", 1, projected) });
            Assert.IsNull(merged.Result.PixelQuality, "A merged image must not claim a crop's statistics are whole-image metrics.");
            for (int index = 0; index < original.Regions.Count; index++)
            {
                Assert.AreSame(original.Regions[index].PixelQuality, merged.Result.Regions[index].PixelQuality);
                Assert.AreEqual(original.Regions[index].Region.SourceIndex, merged.Result.Regions[index].PixelQuality!.InputRegionIndex);
                Assert.AreEqual(index, merged.Result.Regions[index].Region.SourceIndex);
            }
        }

        [TestMethod]
        public async Task PixelQualityRejectsMismatchedAdapterProvenanceBeforeRecognition()
        {
            using OcrFixture fixture = CreateOcrFixture();
            using var input = new QualityInput { WrongSize = true };
            OcrPipelineException error = await Assert.ThrowsExactlyAsync<OcrPipelineException>(() => fixture.Pipeline.RunAsync(input, new OcrExecutionOptions().WithPixelQuality(new OcrPixelQualityOptions())));
            Assert.AreEqual(VisualErrorCodes.OcrPipelineFailed, error.ErrorCode);
            Assert.AreEqual(OcrPipelineStage.CropAndBatch, error.Stage);
            Assert.AreEqual(0, input.Inner.LastBatchSize);
        }
    }
}
