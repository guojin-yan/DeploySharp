using System;
using System.IO;
using System.Threading;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class RFDETRInputTests
    {
        private static VisualModelProfile Profile(VisualPreprocessingOptions options) => PortableDetectorProfiles.CreateRFDETRSeg(new ModelId("tests/rfdetr-input"),
            new PortableDetectorProfileOptions(17, options.ModelSize!.Value, new[] { "object" }, preprocessing: options)).VisualProfile;

        [TestMethod]
        public void FloatResizePreservesFractionalPixelsColorsAndOwnedTensor()
        {
            var pixels = new byte[] { 0, 0, 1, 2, 4, 2 };
            var image = new OpenCvBgrImage(2, 1, pixels, TensorBufferOwnership.Borrow, "frame");
            var options = new VisualPreprocessingOptions(new VisualSize(3, 1), VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.Scale());
            var factory = new RFDETRInputFactory();
            using PreparedVisualInput rgb = factory.Create(image, Profile(options));
            var tensor = (Tensor<float>)rgb.Tensor;
            float[] values = tensor.ToArray();
            Assert.AreEqual(1.5f / 255f, values[1], 1e-8f);
            Assert.AreEqual(2f / 255f, values[4], 1e-8f);
            Assert.AreEqual(1f / 255f, values[7], 1e-8f);
            Assert.AreEqual(TensorBufferOwnership.Transfer, tensor.Ownership);
            Assert.AreEqual(VisualColorOrder.Rgb, rgb.Preprocessing.ColorOrder);
            Assert.AreEqual("frame", rgb.InputId);
            pixels[2] = 99;
            Assert.AreEqual(1.5f / 255f, ((float[])rgb.Tensor.Buffer)[1], 1e-8f);
            using PreparedVisualInput bgr = factory.Create(image, Profile(options.WithColorOrder(VisualColorOrder.Bgr)));
            Assert.AreEqual(1f / 255f, ((float[])bgr.Tensor.Buffer)[1], 1e-8f);
        }

        [TestMethod]
        public void ImageNetNormalizationAndCancellationFollowProfile()
        {
            var options = new VisualPreprocessingOptions(new VisualSize(1, 1), VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.ImageNet);
            var image = new OpenCvBgrImage(1, 1, new byte[] { 0, 128, 255 });
            using PreparedVisualInput input = new RFDETRInputFactory().Create(image, Profile(options));
            float[] values = ((Tensor<float>)input.Tensor).ToArray();
            Assert.AreEqual((1f - .485f) / .229f, values[0], 1e-6f);
            Assert.AreEqual((128f / 255f - .456f) / .224f, values[1], 1e-6f);
            Assert.AreEqual(-.406f / .225f, values[2], 1e-6f);
            Assert.AreEqual(.485f, input.Preprocessing.Means[0]);
            Assert.AreEqual(1f / .229f, input.Preprocessing.Scales[0]);
            Assert.ThrowsExactly<OperationCanceledException>(() => new RFDETRInputFactory().Create(image, Profile(options), cancellationToken: new CancellationToken(true)));
            Assert.ThrowsExactly<VisualException>(() => Profile(options.WithBatchSize(2)));
        }

        [TestMethod]
        public void EncodedGeometryOverrideUsesDeclaredTransform()
        {
            var options = new VisualPreprocessingOptions(new VisualSize(6, 6), VisualResizeMode.Letterbox, VisualColorOrder.Rgb, VisualNormalizationOptions.ImageNet);
            var source = OpenCvImageSource.FromFile(Path.Combine(AppContext.BaseDirectory, "rgb.png"));
            using PreparedVisualInput input = new RFDETRInputFactory().Create(source, Profile(options));
            Assert.AreEqual(ImageTransformKind.Letterbox, input.Transform.Kind);
            Assert.AreEqual(new VisualSize(3, 2), input.SourceSize);
            Assert.ThrowsExactly<ArgumentException>(() => new RFDETRInputFactory().Create(new OpenCvBgrImageFactory().Create(source), Profile(options)));
        }
    }
}
