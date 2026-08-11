using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class NativeMultimodalInputTests
    {
        [TestMethod]
        public void FactoryDecodesFileAndBytesOnceAcrossRgbGrayAndAlpha()
        {
            NativeMultimodalProfile profile = NativeMultimodalProfiles.CreateLlavaOneVisionQwen2HalfB();
            var factory = new OpenCvNativeMultimodalInputFactory();
            string rgbPath = Fixture("rgb.png");
            using NativeMultimodalPreparedImage rgb = factory.CreateFromFile(rgbPath, profile);
            using NativeMultimodalPreparedImage bytes = factory.CreateFromBytes(File.ReadAllBytes(rgbPath), profile);
            using NativeMultimodalPreparedImage gray = factory.CreateFromBytes(File.ReadAllBytes(Fixture("gray.png")), profile);
            using NativeMultimodalPreparedImage alpha = factory.CreateFromBytes(File.ReadAllBytes(Fixture("alpha.png")), profile);

            Assert.AreEqual(new NativeMultimodalImageGrid(1, 1), rgb.Grid);
            CollectionAssert.AreEqual(new long[] { 2, 3, 384, 384 }, rgb.Input.Tensor.Shape.ToArray());
            Assert.AreEqual(profile.Processor.GetPackedTokenCount(rgb.Input.SourceSize, rgb.Grid), rgb.PackedImageTokens);
            Assert.AreEqual(rgb.Input.InputId, bytes.Input.InputId);
            Assert.AreEqual("pixel_values", alpha.Input.InputName);
            Assert.IsTrue(((float[])rgb.Input.Tensor.Buffer).All(float.IsFinite));
            Assert.IsTrue(((float[])gray.Input.Tensor.Buffer).All(float.IsFinite));
            Assert.IsTrue(((float[])alpha.Input.Tensor.Buffer).All(float.IsFinite));
        }

        [TestMethod]
        public void FactoryMapsCancellationAndEncodedCapacityToStableErrors()
        {
            NativeMultimodalProfile profile = NativeMultimodalProfiles.CreateLlavaOneVisionQwen2HalfB();
            var factory = new OpenCvNativeMultimodalInputFactory();
            using (var cancelled = new System.Threading.CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.AreEqual(OpenCvErrorCodes.Cancelled, Assert.ThrowsExactly<OpenCvVisualException>(() => factory.CreateFromFile(Fixture("rgb.png"), profile, cancelled.Token)).ErrorCode);
            }
            byte[] bytes = File.ReadAllBytes(Fixture("rgb.png"));
            Assert.AreEqual(OpenCvErrorCodes.InputBoundary, Assert.ThrowsExactly<OpenCvVisualException>(() => factory.Create(OpenCvImageSource.FromBytes(bytes, 1), profile)).ErrorCode);
        }

        private static string Fixture(string fileName) => Path.Combine(AppContext.BaseDirectory, fileName);
    }
}
