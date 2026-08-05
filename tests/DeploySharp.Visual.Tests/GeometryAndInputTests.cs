using System;
using JYPPX.DeploySharp.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JYPPX.DeploySharp.Visual;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class GeometryAndInputTests
    {
        [TestMethod]
        public void ResizeAndLetterboxRoundTripPointsAndRectangles()
        {
            var resize = ImageTransform.Resize(new VisualSize(100, 50), new VisualSize(200, 200));
            PointF point = resize.ToSource(resize.ToModel(new PointF(25, 10)));
            Assert.AreEqual(25f, point.X, 0.0001f);
            Assert.AreEqual(10f, point.Y, 0.0001f);
            RectangleF rectangle = new RectangleF(10, 5, 30, 20);
            RectangleF roundTrip = resize.ToSource(resize.ToModel(rectangle));
            Assert.AreEqual(rectangle.X, roundTrip.X, 0.0001f);
            Assert.AreEqual(rectangle.Y, roundTrip.Y, 0.0001f);
            Assert.AreEqual(rectangle.Width, roundTrip.Width, 0.0001f);
            Assert.AreEqual(rectangle.Height, roundTrip.Height, 0.0001f);

            var letterbox = ImageTransform.Letterbox(new VisualSize(200, 100), new VisualSize(100, 100));
            Assert.AreEqual(0f, letterbox.OffsetX, 0.0001f);
            Assert.AreEqual(25f, letterbox.OffsetY, 0.0001f);
            RectangleF clipped = letterbox.ClipToSource(letterbox.ToSource(new RectangleF(-10, 20, 150, 100)));
            Assert.AreEqual(0f, clipped.X, 0.0001f);
            Assert.AreEqual(200f, clipped.Width, 0.0001f);
        }

        [TestMethod]
        public void CropValidatesBoundsAndMapsToModel()
        {
            var crop = ImageTransform.Crop(new VisualSize(100, 80), new VisualSize(200, 200), new RectangleF(10, 20, 50, 40));
            RectangleF model = crop.ToModel(new RectangleF(10, 20, 50, 40));
            Assert.AreEqual(0f, model.X, 0.0001f);
            Assert.AreEqual(0f, model.Y, 0.0001f);
            Assert.AreEqual(200f, model.Width, 0.0001f);
            Assert.ThrowsExactly<VisualException>(() => ImageTransform.Crop(new VisualSize(100, 80), new VisualSize(200, 200), new RectangleF(90, 0, 20, 20)));
        }

        [TestMethod]
        public void PreparedInputEnforcesTransformSizesAndOwnedResourceIsIdempotent()
        {
            var resource = new TrackingDisposable();
            using (PreparedVisualInput input = VisualTestData.ClassificationInput(PreparedInputOwnership.Owned, resource))
            {
                Assert.AreEqual(PreparedInputOwnership.Owned, input.Ownership);
                input.Dispose();
                Assert.AreEqual(1, resource.DisposeCount);
            }
            Assert.AreEqual(1, resource.DisposeCount);
            Assert.ThrowsExactly<VisualException>(() => new PreparedVisualInput("images", new JYPPX.DeploySharp.Tensors.Tensor<float>(new JYPPX.DeploySharp.Tensors.TensorShape(1, 3, 2, 2), new float[12]), new VisualSize(2, 2), new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(3, 3), new VisualSize(2, 2))));
        }

        [TestMethod]
        public void PreprocessingDescriptorRejectsNonFiniteValues()
        {
            Assert.ThrowsExactly<VisualException>(() => new VisualPreprocessingDescriptor(VisualColorOrder.Rgb, new[] { float.NaN }));
            Assert.ThrowsExactly<VisualException>(() => new VisualPreprocessingDescriptor(VisualColorOrder.Rgb, new[] { 0f }, new[] { 1f, 2f }));
            var descriptor = new VisualPreprocessingDescriptor(VisualColorOrder.Bgr, new[] { 1f, 2f, 3f }, new[] { 0.1f, 0.2f, 0.3f }, "adapter metadata only");
            Assert.AreEqual(3, descriptor.Means.Count);
        }
    }
}
