using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvOcrPixelQualityTests
    {
        [TestMethod]
        public void NonContinuousBgrBgraGrayViewsMatchTheirOwnedCopiesWithoutChangingPixels()
        {
            foreach (int channels in new[] { 1, 3, 4 })
            {
                int type = channels == 1 ? MatType.CV_8UC1 : channels == 3 ? MatType.CV_8UC3 : MatType.CV_8UC4;
                using var image = new Mat(9, 13, type);
                var bytes = new byte[9 * 13 * channels];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)((i * 37 + 23) % 256);
                Marshal.Copy(bytes, 0, image.Data, bytes.Length);
                using Mat view = image.SubMat(new Rect(2, 1, 7, 6));
                using Mat copy = view.Clone();
                Assert.IsTrue(view.Step.ToUInt64() > (ulong)(view.Cols * channels));
                OcrPixelQualityDiagnostics expected = OpenCvOcrPixelQuality.Analyze(copy);
                OcrPixelQualityDiagnostics actual = OpenCvOcrPixelQuality.Analyze(view);
                Assert.AreEqual(expected.MeanLuminance, actual.MeanLuminance);
                Assert.AreEqual(expected.LuminanceStandardDeviation, actual.LuminanceStandardDeviation);
                Assert.AreEqual(expected.LaplacianVariance, actual.LaplacianVariance);
                var after = new byte[bytes.Length]; Marshal.Copy(image.Data, after, 0, after.Length);
                CollectionAssert.AreEqual(bytes, after);
                Parallel.For(0, 8, _ => Assert.AreEqual(expected.LaplacianVariance, OpenCvOcrPixelQuality.Analyze(view).LaplacianVariance));
            }
        }

        [TestMethod]
        public void ColorWeightsIgnoreAlphaAndBoundsRejectInvalidDepthOrCancellation()
        {
            using var red = new Mat(4, 4, MatType.CV_8UC4, new Scalar(0, 0, 255, 0));
            Assert.AreEqual(77d, OpenCvOcrPixelQuality.Analyze(red).MeanLuminance);
            using var blue = new Mat(4, 4, MatType.CV_8UC3, new Scalar(255, 0, 0));
            Assert.AreEqual(29d, OpenCvOcrPixelQuality.Analyze(blue).MeanLuminance);
            using var gray = new Mat(4, 4, MatType.CV_8UC1, new Scalar(128));
            Assert.AreEqual(128d, OpenCvOcrPixelQuality.Analyze(gray).MeanLuminance);
            using var invalid = new Mat(4, 4, MatType.CV_32FC1);
            Assert.ThrowsExactly<OpenCvVisualException>(() => OpenCvOcrPixelQuality.Analyze(invalid));
            Assert.ThrowsExactly<OperationCanceledException>(() => OpenCvOcrPixelQuality.Analyze(gray, cancellationToken: new CancellationToken(true)));
        }
    }
}
