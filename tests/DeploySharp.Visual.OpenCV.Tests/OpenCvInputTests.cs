using System;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvInputTests
    {
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);

        [TestMethod]
        public void OptionsRejectContradictoryNormalizationAndAlpha()
        {
            Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvPreprocessOptions(new VisualSize(2, 2), means: new[] { 0f, 1f }, standardDeviations: new[] { 1f }));
            Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvPreprocessOptions(new VisualSize(2, 2), colorOrder: VisualColorOrder.Rgb, alphaMode: OpenCvAlphaMode.Preserve));
            Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvPreprocessOptions(new VisualSize(2, 2), outputType: OpenCvOutputType.UInt8, means: new[] { 0f }));
        }

        [TestMethod]
        public void SourceCopiesBytesAndComputesHash()
        {
            byte[] bytes = File.ReadAllBytes(Fixture("rgb.png"));
            OpenCvImageSource source = OpenCvImageSource.FromBytes(bytes);
            bytes[0] = 0;
            Assert.AreEqual(OpenCvImageSourceKind.Bytes, source.Kind);
            Assert.AreEqual(77L, source.Length);
            Assert.AreEqual("ecfec9e141ed0da523a03079244ecaee41da7f262d735c0bb11447f95119184d", source.Sha256);
        }

        [TestMethod]
        public void StreamSourceIsIndependentAndBoundariesAreStable()
        {
            byte[] bytes = File.ReadAllBytes(Fixture("rgb.png"));
            using var stream = new MemoryStream(bytes, writable: true);
            OpenCvImageSource source = OpenCvImageSource.FromStream(stream);
            stream.Position = 0;
            stream.WriteByte(0);

            Assert.AreEqual(OpenCvImageSourceKind.Stream, source.Kind);
            Assert.AreEqual(77L, source.Length);
            Assert.AreEqual("ecfec9e141ed0da523a03079244ecaee41da7f262d735c0bb11447f95119184d", source.Sha256);
            Assert.AreEqual(OpenCvErrorCodes.InputBoundary, Assert.ThrowsExactly<OpenCvVisualException>(() => OpenCvImageSource.FromFile("relative.png")).ErrorCode);
            Assert.AreEqual(OpenCvErrorCodes.InputBoundary, Assert.ThrowsExactly<OpenCvVisualException>(() => OpenCvImageSource.FromBytes(Array.Empty<byte>())).ErrorCode);
            Assert.AreEqual(OpenCvErrorCodes.InputBoundary, Assert.ThrowsExactly<OpenCvVisualException>(() => OpenCvImageSource.FromBytes(bytes, 16)).ErrorCode);
        }

        [TestMethod]
        public void RuntimePreflightReportsExactManagedNativePair()
        {
            OpenCvRuntimeInfo info = OpenCvRuntimePreflight.Check();
            Assert.AreEqual("5.0.0.0", info.ManagedPackageVersion);
            Assert.AreEqual("5.0.0", info.OpenCvVersion);
            Assert.IsTrue(info.IsCompatible);
            Assert.AreEqual("JYPPX.OpenCV.Native", info.NativeLibraryName);
        }

        [TestMethod]
        public void RgbInputProducesNchwFloatTensorAndResizeTransform()
        {
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), means: new[] { 1f, 2f, 3f }, standardDeviations: new[] { 1f, 2f, 4f });
            using (PreparedVisualInput input = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options))
            {
                Assert.AreEqual(new TensorShape(1, 3, 2, 2), input.Tensor.Shape);
                Assert.AreEqual(VisualTensorLayout.Nchw, input.Layout);
                Assert.AreEqual(ImageTransformKind.Resize, input.Transform.Kind);
                var tensor = (Tensor<float>)input.Tensor;
                float[] values = tensor.ToArray();
                Assert.AreEqual(12, values.Length);
                Assert.AreEqual(190f, values[0], 0.1f);
                Assert.AreEqual(31f, values[4], 0.1f);
            }
        }

        [TestMethod]
        public void GrayAndAlphaInputsCoverLayoutAndComposite()
        {
            var grayOptions = new OpenCvPreprocessOptions(new VisualSize(3, 2), colorOrder: VisualColorOrder.Gray, layout: VisualTensorLayout.Hwc, outputType: OpenCvOutputType.UInt8);
            using (PreparedVisualInput gray = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("gray.png")), "images", grayOptions))
            {
                Assert.AreEqual(new TensorShape(2, 3, 1), gray.Tensor.Shape);
                Assert.IsInstanceOfType(gray.Tensor, typeof(Tensor<byte>));
            }

            var alphaOptions = new OpenCvPreprocessOptions(new VisualSize(2, 2), colorOrder: VisualColorOrder.Rgb, alphaMode: OpenCvAlphaMode.Composite, layout: VisualTensorLayout.Nhwc, outputType: OpenCvOutputType.UInt8, alphaBackground: new OpenCvRgbColor(255, 255, 255));
            using (PreparedVisualInput alpha = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("alpha.png")), "images", alphaOptions))
            {
                Assert.AreEqual(new TensorShape(1, 2, 2, 3), alpha.Tensor.Shape);
                Assert.IsTrue(((Tensor<byte>)alpha.Tensor).ToArray().Any(value => value > 0));
            }
        }

        [TestMethod]
        public void RgbToGrayLetterboxCenterCropAndBatchHaveDeterministicShapes()
        {
            var grayOptions = new OpenCvPreprocessOptions(new VisualSize(3, 2), colorOrder: VisualColorOrder.Gray, layout: VisualTensorLayout.Hwc, outputType: OpenCvOutputType.UInt8);
            using (PreparedVisualInput gray = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", grayOptions))
            {
                Assert.AreEqual(new TensorShape(2, 3, 1), gray.Tensor.Shape);
                Assert.IsTrue(((Tensor<byte>)gray.Tensor).ToArray().Any(value => value > 0));
            }

            var letterboxOptions = new OpenCvPreprocessOptions(new VisualSize(6, 6), resizeMode: OpenCvResizeMode.Letterbox, layout: VisualTensorLayout.Nhwc, batchSize: 2, outputType: OpenCvOutputType.UInt8, paddingColor: new OpenCvRgbColor(7, 11, 13));
            using (PreparedVisualInput letterbox = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", letterboxOptions))
            {
                Assert.AreEqual(new TensorShape(2, 6, 6, 3), letterbox.Tensor.Shape);
                Assert.AreEqual(ImageTransformKind.Letterbox, letterbox.Transform.Kind);
                Assert.AreEqual(1f, letterbox.Transform.OffsetY);
            }

            var cropOptions = new OpenCvPreprocessOptions(new VisualSize(2, 2), resizeMode: OpenCvResizeMode.CenterCrop, outputType: OpenCvOutputType.UInt8);
            using (PreparedVisualInput crop = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", cropOptions))
            {
                Assert.AreEqual(ImageTransformKind.Crop, crop.Transform.Kind);
                Assert.AreEqual(new TensorShape(1, 3, 2, 2), crop.Tensor.Shape);
            }
        }

        [TestMethod]
        public void DecoderUsesContentNotExtensionAndRecoversAfterCorruptInput()
        {
            var factory = new OpenCvVisualInputFactory();
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), outputType: OpenCvOutputType.UInt8);
            Assert.AreEqual(
                OpenCvErrorCodes.DecodeFailed,
                Assert.ThrowsExactly<OpenCvVisualException>(() => factory.Create(OpenCvImageSource.FromBytes(new byte[] { 1, 2, 3, 4 }), "images", options)).ErrorCode);

            string disguised = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-" + Guid.NewGuid().ToString("N") + ".data");
            try
            {
                File.Copy(Fixture("rgb.png"), disguised);
                using PreparedVisualInput input = factory.Create(OpenCvImageSource.FromFile(disguised), "images", options);
                Assert.AreEqual(new TensorShape(1, 3, 2, 2), input.Tensor.Shape);
            }
            finally
            {
                if (File.Exists(disguised)) File.Delete(disguised);
            }
        }

        [TestMethod]
        public void CancellationIsObservedBeforeNativeDecode()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var options = new OpenCvPreprocessOptions(new VisualSize(2, 2));
                OpenCvVisualException exception = Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options, cancellationToken: cancellation.Token));
                Assert.AreEqual(OpenCvErrorCodes.Cancelled, exception.ErrorCode);
            }
        }
    }
}
