using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrRecognitionWidthTests
    {
        [TestMethod]
        public void LongLineReportsCompressionAndRejectCarriesSourceContext()
        {
            var profile = new TextCropProfile("width/long", 48, OcrRecognitionWidthMode.Dynamic, 320, 320, minimumWidth: 48);
            TextQuadrilateral quad = Quad(500, 20);
            var request = new TextCropRequest(new TextRegion(7, .9f, quad.Polygon, quad), profile);
            Assert.AreEqual(1200L, request.WidthInfo.NaturalWidth);
            Assert.AreEqual(320, request.WidthInfo.TargetWidth);
            Assert.AreEqual(320, request.WidthInfo.TensorWidth);
            Assert.IsTrue(request.WidthInfo.WidthClamped);
            Assert.IsFalse(request.WidthInfo.BatchPadded);
            TextCropProfile reject = profile.WithRecognitionOverflowMode(RecognitionOverflowMode.Reject);
            Assert.AreEqual(RecognitionOverflowMode.Clamp, profile.OverflowMode);
            Assert.AreEqual(1200L, reject.DescribeWidth(quad, TextOrientation.Degrees0).NaturalWidth);
            OcrPipelineException error = Assert.ThrowsExactly<OcrPipelineException>(() => new TextCropRequest(request.Region, reject));
            Assert.AreEqual(VisualErrorCodes.OcrRecognitionWidthExceeded, error.ErrorCode);
            Assert.AreEqual(OcrPipelineStage.CropAndBatch, error.Stage);
            Assert.AreEqual(7, error.RegionIndex);
            Assert.AreEqual(profile.ProfileId, error.ProfileId);
            StringAssert.Contains(error.TechnicalDetails!, "naturalWidth=1200;targetWidth=320");
            Assert.ThrowsExactly<OcrPipelineException>(() => reject.CalculateWidth(quad, TextOrientation.Degrees0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => profile.WithRecognitionOverflowMode((RecognitionOverflowMode)99));
        }

        [TestMethod]
        public void MinimumAlignmentAndUpperBoundDoNotFalselyReportCompression()
        {
            var profile = new TextCropProfile("width/alignment", 48, OcrRecognitionWidthMode.Dynamic, 48, 321, 32, minimumWidth: 48)
                .WithRecognitionOverflowMode(RecognitionOverflowMode.Reject);
            OcrRecognitionWidthInfo narrow = profile.DescribeWidth(Quad(5, 20), TextOrientation.Degrees0);
            Assert.AreEqual(12L, narrow.NaturalWidth);
            Assert.AreEqual(64, narrow.TargetWidth);
            Assert.IsFalse(narrow.WidthClamped);
            // Alignment rounds to 352, but clamping padding back to 321 does not compress 321 pixels of content.
            Assert.AreEqual(321, profile.CalculateWidth(Quad(321, 48), TextOrientation.Degrees0));
            Assert.IsFalse(profile.DescribeWidth(Quad(321, 48), TextOrientation.Degrees0).WidthClamped);
        }

        [TestMethod]
        public void WidthIsCalculatedAfterOrientationAndUsesFixedWidthAsEffectiveLimit()
        {
            var profile = new TextCropProfile("width/fixed", 48, OcrRecognitionWidthMode.Fixed, 320, 3200)
                .WithRecognitionOverflowMode(RecognitionOverflowMode.Reject);
            TextQuadrilateral quad = Quad(20, 500);
            Assert.AreEqual(2L, profile.DescribeWidth(quad, TextOrientation.Degrees0).NaturalWidth);
            Assert.AreEqual(2L, profile.DescribeWidth(quad, TextOrientation.Degrees180).NaturalWidth);
            Assert.AreEqual(1200L, profile.DescribeWidth(quad, TextOrientation.Clockwise90).NaturalWidth);
            Assert.AreEqual(1200L, profile.DescribeWidth(quad, TextOrientation.CounterClockwise90).NaturalWidth);
            Assert.ThrowsExactly<OcrPipelineException>(() => profile.CalculateWidth(quad, TextOrientation.Clockwise90));
            Assert.AreEqual(320, profile.CalculateWidth(quad, TextOrientation.Degrees0));
        }

        [TestMethod]
        public void ClampHandlesNaturalWidthBeyondIntWithoutPrematureOverflow()
        {
            var profile = new TextCropProfile("width/large", 48, OcrRecognitionWidthMode.Dynamic, 3200, 3200, 32);
            OcrRecognitionWidthInfo width = profile.DescribeWidth(Quad(100000000, 1), TextOrientation.Degrees0);
            Assert.AreEqual(4800000000L, width.NaturalWidth);
            Assert.AreEqual(3200, width.TargetWidth);
            Assert.IsTrue(width.WidthClamped);
        }

        [TestMethod]
        public void FixedWidthCopyPreservesPreprocessingAndClearsDynamicWidthRetry()
        {
            var geometry = new OcrGeometryOptions(OcrGeometryValidationMode.Report);
            var source = new TextCropProfile(
                    "width/backend-static", 48, OcrRecognitionWidthMode.Dynamic, 320, 640, 32,
                    TextCropInterpolation.Cubic, VisualColorOrder.Bgr, VisualTensorLayout.Nchw,
                    new[] { .5f, .4f, .3f }, new[] { 2f, 3f, 4f }, new TextCropColor(1, 2, 3), 123456, 48)
                .WithRecognitionOverflowMode(RecognitionOverflowMode.Reject)
                .WithGeometryValidation(geometry)
                .WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent)
                .WithWidthRetry(new OcrWidthRetryOptions(960));

            TextCropProfile fixedProfile = source.WithFixedWidth(320);

            Assert.AreEqual(OcrRecognitionWidthMode.Dynamic, source.WidthMode);
            Assert.IsNotNull(source.WidthRetry);
            Assert.AreEqual(OcrRecognitionWidthMode.Fixed, fixedProfile.WidthMode);
            Assert.AreEqual(320, fixedProfile.FixedWidth);
            Assert.AreEqual(320, fixedProfile.MinimumWidth);
            Assert.AreEqual(640, fixedProfile.MaximumWidth);
            Assert.AreEqual(RecognitionOverflowMode.Reject, fixedProfile.OverflowMode);
            Assert.AreSame(geometry, fixedProfile.Geometry);
            Assert.AreEqual(OcrCropTransformMode.AffineWhenEquivalent, fixedProfile.TransformMode);
            Assert.AreEqual(TextCropInterpolation.Cubic, fixedProfile.Interpolation);
            Assert.AreEqual(VisualColorOrder.Bgr, fixedProfile.ColorOrder);
            CollectionAssert.AreEqual(new[] { .5f, .4f, .3f }, new System.Collections.Generic.List<float>(fixedProfile.Means));
            CollectionAssert.AreEqual(new[] { 2f, 3f, 4f }, new System.Collections.Generic.List<float>(fixedProfile.Scales));
            Assert.AreEqual(new TextCropColor(1, 2, 3), fixedProfile.PaddingColor);
            Assert.AreEqual(123456L, fixedProfile.MaximumCropPixels);
            Assert.IsNull(fixedProfile.WidthRetry);
            Assert.AreEqual(320, fixedProfile.CalculateWidth(Quad(100, 20), TextOrientation.Degrees0));
            Assert.AreSame(fixedProfile, fixedProfile.WithFixedWidth(320));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => source.WithFixedWidth(641));
        }

        [TestMethod]
        public void OptionalWidthBucketsPadWithoutChangingOverflowOrSourceProfile()
        {
            var source = new TextCropProfile("width/buckets", 48, OcrRecognitionWidthMode.Dynamic, 320, 640, 32, minimumWidth: 48);
            int[] values = { 64, 128, 256, 640 };
            TextCropProfile bucketed = source.WithWidthBuckets(values).WithRecognitionOverflowMode(RecognitionOverflowMode.Reject);
            values[0] = 96;

            Assert.IsNull(source.WidthBuckets);
            CollectionAssert.AreEqual(new[] { 64, 128, 256, 640 }, new List<int>(bucketed.WidthBuckets!));
            Assert.AreEqual(64, bucketed.CalculateWidth(Quad(20, 48), TextOrientation.Degrees0));
            Assert.AreEqual(128, bucketed.CalculateWidth(Quad(65, 48), TextOrientation.Degrees0));
            Assert.AreEqual(256, bucketed.CalculateWidth(Quad(129, 48), TextOrientation.Degrees0));
            Assert.AreEqual(640, bucketed.CalculateWidth(Quad(640, 48), TextOrientation.Degrees0));
            Assert.IsFalse(bucketed.DescribeWidth(Quad(129, 48), TextOrientation.Degrees0).WidthClamped);
            Assert.ThrowsExactly<OcrPipelineException>(() => bucketed.CalculateWidth(Quad(641, 48), TextOrientation.Degrees0));
            Assert.AreEqual(160, source.CalculateWidth(Quad(129, 48), TextOrientation.Degrees0));

            TextCropProfile fixedWidth = bucketed.WithFixedWidth(320);
            Assert.IsNull(fixedWidth.WidthBuckets);
            Assert.AreEqual(320, fixedWidth.CalculateWidth(Quad(129, 48), TextOrientation.Degrees0));
            TextCropProfile retry = bucketed.WithWidthRetry(new OcrWidthRetryOptions(960));
            Assert.IsNotNull(retry.WidthBuckets);
        }

        [TestMethod]
        public void WidthBucketsRejectInvalidContracts()
        {
            var dynamic = new TextCropProfile("width/invalid-buckets", 48, OcrRecognitionWidthMode.Dynamic, 320, 640, 32, minimumWidth: 48);
            Assert.ThrowsExactly<ArgumentNullException>(() => dynamic.WithWidthBuckets(null!));
            Assert.ThrowsExactly<ArgumentException>(() => dynamic.WithWidthBuckets(Array.Empty<int>()));
            Assert.ThrowsExactly<ArgumentException>(() => dynamic.WithWidthBuckets(new[] { 64, 128 }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => dynamic.WithWidthBuckets(new[] { 64, 64, 640 }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => dynamic.WithWidthBuckets(new[] { 65, 640 }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => dynamic.WithWidthBuckets(new[] { 32, 640 }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => dynamic.WithWidthBuckets(new[] { 64, 672 }));
            Assert.ThrowsExactly<InvalidOperationException>(() => dynamic.WithFixedWidth(320).WithWidthBuckets(new[] { 640 }));
        }

        private static TextQuadrilateral Quad(float width, float height) => new TextQuadrilateral(
            new PointF(0, 0), new PointF(width, 0), new PointF(width, height), new PointF(0, height), TextCornerOrder.TopLeftClockwise);

        [TestMethod]
        public void PaddleRecognitionDefaultRemains3200AndMinimumHeightWidth()
        {
            var artifact = new JYPPX.DeploySharp.Visual.Models.PaddleOcr.PaddleOcrArtifactContract(14, new string('a', 64), "test", "test", "test", "test", "test");
            var profile = JYPPX.DeploySharp.Visual.Models.PaddleOcr.PaddleOcrProfiles.CreateRecognition(
                new JYPPX.DeploySharp.Models.ModelId("test/width-default"), artifact, new OcrCharacterSet("test/width", "1", "AB"));
            Assert.AreEqual(3200, profile.CropProfile!.MaximumWidth);
            Assert.AreEqual(48, profile.CropProfile.MinimumWidth);
            Assert.AreEqual(RecognitionOverflowMode.Clamp, profile.CropProfile.OverflowMode);
        }
    }
}
