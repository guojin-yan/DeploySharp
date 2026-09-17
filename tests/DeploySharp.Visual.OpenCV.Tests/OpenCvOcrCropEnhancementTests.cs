using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvOcrCropEnhancementTests
    {
        [TestMethod]
        [DataRow(OcrCropEnhancementMode.ContrastNormalize)]
        [DataRow(OcrCropEnhancementMode.GrayClahe)]
        public void LowContrastEnhancementChangesContentButNotSourceOrPadding(OcrCropEnhancementMode mode)
        {
            using OpenCvOcrImageInput input = Image(6, colored: true);
            var profile = Profile();
            using PreparedVisualInput plain = input.PrepareRecognitionBatch("crops", new[] { Request(profile) }, CancellationToken.None);
            var enabled = profile.WithCropProcessing(new OcrCropProcessingOptions(1024).WithEnhancement(new OcrCropEnhancementOptions(mode)));
            using OcrPreparedCropBatch actual = input.PrepareProcessedRecognitionBatch("crops", new[] { Request(enabled) }, CancellationToken.None);
            OcrCropDiagnostics row = actual.Diagnostics[0];
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, row.EnhancementDecision); Assert.IsNotNull(row.Enhanced);
            Assert.AreEqual(row.Rectified.InputSize, row.Enhanced.InputSize);
            Assert.IsFalse(((float[])plain.Tensor.Buffer).SequenceEqual((float[])actual.Input.Tensor.Buffer));
            if (mode == OcrCropEnhancementMode.ContrastNormalize)
            {
                Assert.AreEqual(3d, row.AppliedGain);
                Assert.AreEqual(row.Rectified.LuminanceStandardDeviation!.Value * 3, row.Enhanced.LuminanceStandardDeviation!.Value, .7);
            }
            else Assert.IsNull(row.AppliedGain);
            float[] values = (float[])actual.Input.Tensor.Buffer;
            for (int y = 0; y < 16; y++) for (int x = 0; x < row.Content.InputSize.Width; x++)
            {
                int i = y * 64 + x;
                float difference = mode == OcrCropEnhancementMode.GrayClahe ? 0 : 36;
                Assert.AreEqual(difference, values[i] - values[1024 + i]);
                Assert.AreEqual(difference, values[1024 + i] - values[2048 + i]);
            }
            for (int y = 0; y < 16; y++) for (int x = row.Content.InputSize.Width; x < 64; x++)
            {
                Assert.AreEqual(11f, values[y * 64 + x]); Assert.AreEqual(22f, values[1024 + y * 64 + x]); Assert.AreEqual(33f, values[2048 + y * 64 + x]);
            }
            using PreparedVisualInput after = input.PrepareRecognitionBatch("crops", new[] { Request(profile) }, CancellationToken.None);
            CollectionAssert.AreEqual((float[])plain.Tensor.Buffer, (float[])after.Tensor.Buffer);
            // Shared input, isolated thread-local enhancement state and owned tensor leases.
            Parallel.For(0, 8, _ =>
            {
                using OcrPreparedCropBatch other = input.PrepareProcessedRecognitionBatch("crops", new[] { Request(enabled) }, CancellationToken.None);
                CollectionAssert.AreEqual(values, (float[])other.Input.Tensor.Buffer);
            });
        }

        [TestMethod]
        public void AdaptiveThresholdProducesBinaryGrayContentAndPreservesSource()
        {
            using OpenCvOcrImageInput input = AdaptiveImage();
            TextCropProfile profile = Profile().WithCropProcessing(new OcrCropProcessingOptions(1024)
                .WithEnhancement(new OcrCropEnhancementOptions(OcrCropEnhancementMode.AdaptiveThreshold, adaptiveBlockSize: 15, adaptiveConstant: 5)));
            using PreparedVisualInput plain = input.PrepareRecognitionBatch("crops", new[] { Request(Profile()) }, CancellationToken.None);
            using OcrPreparedCropBatch actual = input.PrepareProcessedRecognitionBatch("crops", new[] { Request(profile) }, CancellationToken.None);
            OcrCropDiagnostics row = actual.Diagnostics[0];
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, row.EnhancementDecision);
            Assert.IsNotNull(row.Enhanced);
            Assert.IsNull(row.AppliedGain);
            float[] values = (float[])actual.Input.Tensor.Buffer;
            bool sawLow = false, sawHigh = false;
            int contentWidth = row.Content.InputSize.Width;
            for (int y = 0; y < row.Content.InputSize.Height; y++) for (int x = 0; x < contentWidth; x++)
            {
                int offset = y * 64 + x;
                Assert.AreEqual(values[offset], values[1024 + offset]);
                Assert.AreEqual(values[offset], values[2048 + offset]);
                sawLow |= values[offset] <= 0;
                sawHigh |= values[offset] > 0;
            }
            Assert.IsTrue(sawLow && sawHigh, "Adaptive threshold should retain both binary levels in the fixture.");
            using PreparedVisualInput after = input.PrepareRecognitionBatch("crops", new[] { Request(Profile()) }, CancellationToken.None);
            CollectionAssert.AreEqual((float[])plain.Tensor.Buffer, (float[])after.Tensor.Buffer);
        }

        [TestMethod]
        public void UnsharpMaskUsesLowSharpnessGateAndLeavesSourceUnchanged()
        {
            using OpenCvOcrImageInput input = Image(6, colored: true);
            TextCropProfile profile = Profile().WithCropProcessing(new OcrCropProcessingOptions(1024)
                .WithEnhancement(new OcrCropEnhancementOptions(OcrCropEnhancementMode.UnsharpMask,
                    sharpnessThreshold: 1000000, sharpenKernelSize: 3, sharpenAmount: 1, sharpenSigma: 1)));
            using PreparedVisualInput plain = input.PrepareRecognitionBatch("crops", new[] { Request(Profile()) }, CancellationToken.None);
            using OcrPreparedCropBatch actual = input.PrepareProcessedRecognitionBatch("crops", new[] { Request(profile) }, CancellationToken.None);
            OcrCropDiagnostics row = actual.Diagnostics[0];
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, row.EnhancementDecision);
            Assert.IsNotNull(row.Enhanced);
            Assert.IsNull(row.AppliedGain);
            Assert.IsFalse(((float[])plain.Tensor.Buffer).SequenceEqual((float[])actual.Input.Tensor.Buffer));
            using PreparedVisualInput after = input.PrepareRecognitionBatch("crops", new[] { Request(Profile()) }, CancellationToken.None);
            CollectionAssert.AreEqual((float[])plain.Tensor.Buffer, (float[])after.Tensor.Buffer);
        }

        [TestMethod]
        public void FlatAndHighContrastAreExactNoOpsAndLimitsAndCancellationRecover()
        {
            foreach (int step in new[] { 0, 64 })
            {
                using OpenCvOcrImageInput input = Image(step);
                using PreparedVisualInput plain = input.PrepareRecognitionBatch("crops", new[] { Request(Profile()) }, CancellationToken.None);
                foreach (OcrCropEnhancementMode mode in Enum.GetValues<OcrCropEnhancementMode>())
                {
                    var configured = Profile().WithCropProcessing(new OcrCropProcessingOptions().WithEnhancement(new OcrCropEnhancementOptions(mode)));
                    using OcrPreparedCropBatch actual = input.PrepareProcessedRecognitionBatch("crops", new[] { Request(configured) }, CancellationToken.None);
                    if (mode == OcrCropEnhancementMode.LocalUpscale)
                    {
                        Assert.AreEqual(OcrCropEnhancementDecision.Applied, actual.Diagnostics[0].EnhancementDecision);
                        Assert.IsNotNull(actual.Diagnostics[0].Enhanced);
                        Assert.AreEqual(actual.Diagnostics[0].Rectified.InputSize.Width * 2, actual.Diagnostics[0].Enhanced!.InputSize.Width);
                        Assert.AreEqual(actual.Diagnostics[0].Rectified.InputSize.Height * 2, actual.Diagnostics[0].Enhanced!.InputSize.Height);
                        continue;
                    }
                    OcrCropEnhancementDecision expected = mode == OcrCropEnhancementMode.GaussianDenoise
                        ? (step == 0 ? OcrCropEnhancementDecision.SufficientQuality : OcrCropEnhancementDecision.Applied)
                        : mode == OcrCropEnhancementMode.UnsharpMask
                            ? OcrCropEnhancementDecision.SufficientQuality
                            : (step == 0 ? OcrCropEnhancementDecision.InsufficientVariation : OcrCropEnhancementDecision.SufficientContrast);
                    Assert.AreEqual(expected, actual.Diagnostics[0].EnhancementDecision);
                    Assert.AreEqual(step == 0 || mode != OcrCropEnhancementMode.GaussianDenoise, actual.Diagnostics[0].Enhanced == null);
                    if (step == 0 || mode != OcrCropEnhancementMode.GaussianDenoise)
                        CollectionAssert.AreEqual((float[])plain.Tensor.Buffer, (float[])actual.Input.Tensor.Buffer);
                    else
                        Assert.IsFalse(((float[])plain.Tensor.Buffer).SequenceEqual((float[])actual.Input.Tensor.Buffer), "Gaussian denoise should change the high-frequency fixture.");
                }
            }
            using OpenCvOcrImageInput low = Image(6);
            var limited = Profile().WithCropProcessing(new OcrCropProcessingOptions().WithEnhancement(new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe, maximumPixelsPerCrop: 2)));
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, Assert.ThrowsExactly<OcrPipelineException>(() => low.PrepareProcessedRecognitionBatch("crops", new[] { Request(limited) }, CancellationToken.None)).ErrorCode);
            var valid = Profile().WithCropProcessing(new OcrCropProcessingOptions().WithEnhancement(new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe)));
            Assert.ThrowsExactly<OpenCvVisualException>(() => low.PrepareProcessedRecognitionBatch("crops", new[] { Request(valid) }, new CancellationToken(true)));
            using OcrPreparedCropBatch recovered = low.PrepareProcessedRecognitionBatch("crops", new[] { Request(valid) }, CancellationToken.None);
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, recovered.Diagnostics[0].EnhancementDecision);
        }

        private static TextCropProfile Profile() => new TextCropProfile("enhance.native", 16, OcrRecognitionWidthMode.Fixed, 64, 64,
            colorOrder: VisualColorOrder.Rgb, paddingColor: new TextCropColor(11,22,33), interpolation: TextCropInterpolation.Nearest);

        private static TextCropRequest Request(TextCropProfile profile)
        {
            var quad = new TextQuadrilateral(new PointF(0,0), new PointF(63,0), new PointF(63,31), new PointF(0,31), TextCornerOrder.TopLeftClockwise);
            return new TextCropRequest(new TextRegion(0, 1, quad.Polygon, quad), profile);
        }

        private static OpenCvOcrImageInput Image(int step, bool colored = false)
        {
            byte[] header = Encoding.ASCII.GetBytes("P6\n64 32\n255\n");
            var bytes = new byte[header.Length + 64 * 32 * 3]; Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++)
            {
                byte value = (byte)(128 + ((x / 4 + y / 4) % 2 == 0 ? -step : step));
                int offset = header.Length + (y * 64 + x) * 3;
                bytes[offset] = (byte)(value + (colored ? 12 : 0)); bytes[offset + 1] = value; bytes[offset + 2] = (byte)(value - (colored ? 12 : 0));
            }
            return new OpenCvOcrImageInputFactory().Create(OpenCvImageSource.FromBytes(bytes), "images", new OpenCvPreprocessOptions(new VisualSize(32,16)));
        }

        private static OpenCvOcrImageInput AdaptiveImage()
        {
            byte[] header = Encoding.ASCII.GetBytes("P6\n64 32\n255\n");
            var bytes = new byte[header.Length + 64 * 32 * 3]; Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++)
            {
                bool darkStroke = (x >= 8 && x < 16) || (y >= 12 && y < 20);
                byte value = darkStroke ? (byte)80 : (byte)128;
                int offset = header.Length + (y * 64 + x) * 3;
                bytes[offset] = value; bytes[offset + 1] = value; bytes[offset + 2] = value;
            }
            return new OpenCvOcrImageInputFactory().Create(OpenCvImageSource.FromBytes(bytes), "images", new OpenCvPreprocessOptions(new VisualSize(32,16)));
        }
    }
}
