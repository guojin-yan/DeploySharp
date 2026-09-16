using System;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrGeometryDiagnosticsTests
    {
        [TestMethod]
        public void RotatedRectanglesRetainSemanticBaselineAndInputPolygon()
        {
            foreach (int degrees in new[] { -45, -30, -15, -5, 0, 5, 15, 30, 45, 90, 180 })
            {
                double radians = degrees * Math.PI / 180;
                PointF Rotate(double x, double y) => new PointF((float)(100 + x * Math.Cos(radians) - y * Math.Sin(radians)), (float)(100 + x * Math.Sin(radians) + y * Math.Cos(radians)));
                TextRegion region = Region(Rotate(-40, -10), Rotate(40, -10), Rotate(40, 10), Rotate(-40, 10));
                OcrGeometryDiagnostics result = OcrGeometryAnalyzer.Analyze(region, new VisualSize(200, 200));
                Assert.AreSame(region.Polygon, result.InputPolygon);
                Assert.AreEqual(1600, result.Area, .002);
                Assert.AreEqual(20, result.MinimumEdgeLength, .0001);
                Assert.AreEqual(4, result.AspectRatio!.Value, .0001);
                Assert.AreEqual(radians, result.BaselineAngleRadians!.Value, .0001);
                Assert.AreEqual(-radians, result.RectificationAngleRadians!.Value, .0001);
                Assert.AreEqual(0, result.ParallelogramError!.Value, .000001);
                Assert.AreEqual(OcrGeometryRisk.None, result.Risks);
            }
        }

        [TestMethod]
        public void OutsideFractionUsesPolygonIntersectionNotItsBoundingBox()
        {
            TextRegion diamond = Region(new PointF(-10, 10), new PointF(10, -10), new PointF(30, 10), new PointF(10, 30));
            OcrGeometryDiagnostics result = OcrGeometryAnalyzer.Analyze(diamond, new VisualSize(20, 20));
            Assert.AreEqual(800, result.Area);
            Assert.AreEqual(.5, result.OutsideFraction, .000001); // Bounding-box fraction would be .75.
            Assert.IsTrue(result.Risks.HasFlag(OcrGeometryRisk.OutsideImage));
            Assert.IsTrue(result.Risks.HasFlag(OcrGeometryRisk.EdgeContact));
            TextRegion triangle = new TextRegion(0, 1, TextPolygon.Canonicalize(new[] { new PointF(-10, 0), new PointF(10, 0), new PointF(0, 10) }, OrientedVertexOrder.CounterClockwise));
            Assert.AreEqual(.5, OcrGeometryAnalyzer.Analyze(triangle, new VisualSize(20, 20)).OutsideFraction, .000001);
        }

        [TestMethod]
        public void BoundaryTouchOutsideAndFullyInsideHaveDistinctSemantics()
        {
            OcrGeometryDiagnostics touching = OcrGeometryAnalyzer.Analyze(Box(0, 0, 10, 10), new VisualSize(10, 10));
            Assert.AreEqual(0, touching.OutsideFraction);
            Assert.AreEqual(OcrGeometryRisk.EdgeContact, touching.Risks);
            Assert.AreEqual(OcrGeometryRisk.None, touching.Risks & new OcrGeometryOptions(OcrGeometryValidationMode.Reject).RejectedRisks);
            Assert.AreEqual(1, OcrGeometryAnalyzer.Analyze(Box(10, 0, 10, 10), new VisualSize(10, 10)).OutsideFraction);
            Assert.AreEqual(OcrGeometryRisk.None, OcrGeometryAnalyzer.Analyze(Box(2, 2, 10, 10), new VisualSize(20, 20)).Risks);
        }

        [TestMethod]
        public void ConditionIsTranslationScaleInvariantButDetectsNearDegeneratePerspective()
        {
            TextRegion trapezoid = Region(new PointF(10, 10), new PointF(90, 10), new PointF(60, 40), new PointF(40, 40));
            PointF[] enlarged = new[] { trapezoid.CropQuadrilateral!.TopLeft, trapezoid.CropQuadrilateral.TopRight, trapezoid.CropQuadrilateral.BottomRight, trapezoid.CropQuadrilateral.BottomLeft }
                .Select(p => new PointF(1000 + p.X * 2, 500 + p.Y * 2)).ToArray();
            OcrGeometryDiagnostics first = OcrGeometryAnalyzer.Analyze(trapezoid, new VisualSize(2000, 2000));
            OcrGeometryDiagnostics second = OcrGeometryAnalyzer.Analyze(Region(enlarged[0], enlarged[1], enlarged[2], enlarged[3]), new VisualSize(2000, 2000));
            Assert.AreEqual(first.PerspectiveCondition!.Value, second.PerspectiveCondition!.Value, .000001);
            Assert.IsTrue(first.ParallelogramError > .1);
            Assert.AreEqual(0, first.BaselineAngleRadians!.Value); // Small angle does not mean affine equivalence.
            Assert.AreEqual(3, OcrGeometryAnalyzer.Analyze(Box(1, 1, 100, 2), new VisualSize(200, 200)).PerspectiveCondition!.Value, .000001);
            TextRegion thinTip = Region(new PointF(10, 10), new PointF(110, 10), new PointF(60.001f, 30), new PointF(60, 30));
            Assert.IsTrue(OcrGeometryAnalyzer.Analyze(thinTip, new VisualSize(200, 200)).Risks.HasFlag(OcrGeometryRisk.IllConditionedPerspective));
        }

        [TestMethod]
        public void ThresholdsReportSmallThinAndExtremeRegionsWithoutModifyingThem()
        {
            TextRegion region = Box(5, 5, 1000, .25f);
            OcrGeometryDiagnostics result = OcrGeometryAnalyzer.Analyze(region, new VisualSize(2000, 2000), new OcrGeometryOptions(minimumArea: 300));
            Assert.AreEqual(OcrGeometryRisk.SmallArea | OcrGeometryRisk.ShortEdge | OcrGeometryRisk.ExtremeAspectRatio, result.Risks);
            Assert.AreSame(region.Polygon, result.InputPolygon);
        }

        [TestMethod]
        public void MissingCornerRolesAreNotGuessedAndInvalidGeometryFailsAtConstruction()
        {
            TextRegion rectangle = Box(2, 2, 8, 8);
            OcrGeometryDiagnostics result = OcrGeometryAnalyzer.Analyze(new TextRegion(0, 1, rectangle.Polygon), new VisualSize(20, 20));
            Assert.AreEqual(OcrGeometryRisk.MissingCropCorners, result.Risks);
            Assert.IsNull(result.BaselineAngleRadians);
            Assert.IsNull(result.PerspectiveCondition);
            Assert.ThrowsExactly<ArgumentException>(() => Region(new PointF(0, 0), new PointF(5, 5), new PointF(0, 5), new PointF(5, 0)));
            PointF[] ring = Enumerable.Range(0, 5).Select(index => new PointF((float)(20 + 10 * Math.Cos(index * 2 * Math.PI / 5)), (float)(20 + 10 * Math.Sin(index * 2 * Math.PI / 5)))).ToArray();
            PointF[] star = new[] { ring[0], ring[2], ring[4], ring[1], ring[3] };
            Assert.ThrowsExactly<ArgumentException>(() => TextPolygon.Canonicalize(star, OrientedVertexOrder.CounterClockwise));
            Assert.ThrowsExactly<ArgumentException>(() => Region(new PointF(float.NaN, 0), new PointF(5, 0), new PointF(5, 5), new PointF(0, 5)));
            Assert.ThrowsExactly<ArgumentException>(() => Region(new PointF(0, 0), new PointF(0, 5), new PointF(5, 5), new PointF(5, 0)));
            Assert.ThrowsExactly<ArgumentException>(() => Region(new PointF(0, 0), new PointF(5, 0), new PointF(5, 0), new PointF(0, 5)));
        }

        [TestMethod]
        public void OptionsValidateBoundsAndProfileCopiesPreserveEveryPolicy()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrGeometryOptions((OcrGeometryValidationMode)9));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrGeometryOptions(minimumArea: double.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrGeometryOptions(minimumEdgeLength: -1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrGeometryOptions(maximumAspectRatio: .5));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrGeometryOptions(maximumOutsideFraction: 1.1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrGeometryOptions(edgeMargin: double.PositiveInfinity));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrGeometryOptions(maximumPerspectiveCondition: 2.9));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrGeometryOptions(rejectedRisks: (OcrGeometryRisk)128));
            var profile = new TextCropProfile("tests/geometry", 48, OcrRecognitionWidthMode.Dynamic, 320, 3200);
            var options = new OcrGeometryOptions();
            TextCropProfile configured = profile.WithGeometryValidation(options).WithRecognitionWindows(new OcrRecognitionWindowOptions(overlapRatio: .35));
            Assert.AreSame(options, configured.WithRecognitionOverflowMode(RecognitionOverflowMode.Reject).Geometry);
            Assert.AreEqual(.35, configured.RecognitionWindows.OverlapRatio);
            Assert.AreEqual(OcrGeometryValidationMode.Disabled, profile.Geometry.Mode);
            Assert.AreEqual(RecognitionOverflowMode.SlidingWindow, configured.WithGeometryValidation(options).OverflowMode);
        }

        [TestMethod]
        public void CancellationAndUninitializedSizeAreRejected()
        {
            TextRegion region = Box(2, 2, 8, 8);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => OcrGeometryAnalyzer.Analyze(region, default));
            Assert.ThrowsExactly<ArgumentNullException>(() => OcrGeometryAnalyzer.Analyze(null!, new VisualSize(10, 10)));
            using var source = new CancellationTokenSource();
            source.Cancel();
            Assert.ThrowsExactly<OperationCanceledException>(() => OcrGeometryAnalyzer.Analyze(region, new VisualSize(10, 10), cancellationToken: source.Token));
        }

        private static TextRegion Box(float x, float y, float width, float height) => Region(new PointF(x, y), new PointF(x + width, y), new PointF(x + width, y + height), new PointF(x, y + height));
        private static TextRegion Region(PointF tl, PointF tr, PointF br, PointF bl)
        {
            var quad = new TextQuadrilateral(tl, tr, br, bl, TextCornerOrder.TopLeftClockwise);
            return new TextRegion(0, .9f, quad.Polygon, quad);
        }
    }
}
