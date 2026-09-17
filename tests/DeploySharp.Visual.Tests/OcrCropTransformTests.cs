using System;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrCropTransformTests
    {
        [TestMethod]
        public void AffinePolicyAcceptsTranslatedRotatedParallelogramsAndIsScaleInvariant()
        {
            var quad = new TextQuadrilateral(new PointF(100, 80), new PointF(160, 20), new PointF(175, 35), new PointF(115, 95), TextCornerOrder.TopLeftClockwise);
            Assert.AreEqual(0, OcrCropTransformPolicy.GetParallelogramError(quad), .000001);
            Assert.IsTrue(OcrCropTransformPolicy.IsAffineEquivalent(quad));
            var scaled = new TextQuadrilateral(new PointF(-10000, 5000), new PointF(-10000 + 6000, 5000 - 6000), new PointF(-10000 + 7500, 5000 - 4500), new PointF(-10000 + 1500, 5000 + 1500), TextCornerOrder.TopLeftClockwise);
            Assert.IsTrue(OcrCropTransformPolicy.IsAffineEquivalent(scaled));
        }

        [TestMethod]
        public void AffinePolicyRejectsAnyPerspectiveClosureAndBadBases()
        {
            var trapezoid = new TextQuadrilateral(new PointF(10, 10), new PointF(90, 10), new PointF(70, 40), new PointF(30, 40), TextCornerOrder.TopLeftClockwise);
            Assert.IsTrue(OcrCropTransformPolicy.GetParallelogramError(trapezoid) > .1);
            Assert.IsFalse(OcrCropTransformPolicy.IsAffineEquivalent(trapezoid));
            var nearly = new TextQuadrilateral(new PointF(10, 10), new PointF(90, 10), new PointF(90.01f, 40), new PointF(10, 40), TextCornerOrder.TopLeftClockwise);
            Assert.IsFalse(OcrCropTransformPolicy.IsAffineEquivalent(nearly));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => OcrCropTransformPolicy.IsAffineEquivalent(trapezoid, maximumParallelogramError: -1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => OcrCropTransformPolicy.IsAffineEquivalent(trapezoid, maximumAffineCondition: 1.9));
        }

        [TestMethod]
        public void TransformModeIsImmutableAndProfileCopiesDoNotEnableItByAccident()
        {
            var profile = new TextCropProfile("tests/transform", 32, OcrRecognitionWidthMode.Fixed, 128, 128);
            Assert.AreEqual(OcrCropTransformMode.Perspective, profile.TransformMode);
            Assert.AreEqual(OcrCropTransformMode.AffineWhenEquivalent, profile.WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent).TransformMode);
            Assert.AreEqual(OcrCropTransformMode.Perspective, profile.TransformMode);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => profile.WithTransformMode((OcrCropTransformMode)99));
            TextCropProfile copy = profile.WithRecognitionOverflowMode(RecognitionOverflowMode.Reject).WithGeometryValidation(new OcrGeometryOptions()).WithOrientationRetry(new OcrOrientationRetryOptions());
            Assert.AreEqual(OcrCropTransformMode.Perspective, copy.TransformMode);
        }
    }
}
