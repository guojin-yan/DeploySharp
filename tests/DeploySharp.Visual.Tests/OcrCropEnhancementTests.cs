using System;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrCropEnhancementTests
    {
        [TestMethod]
        public void EnhancementGatesConstantLowAndSufficientContrastAndBoundsWork()
        {
            var options = new OcrCropEnhancementOptions(OcrCropEnhancementMode.ContrastNormalize);
            OcrPixelQualityDiagnostics Analyze(int step) => OcrPixelQualityAnalyzer.Analyze(new VisualSize(20,20), (x, _) => (byte)(128 + (x % 2 == 0 ? -step : step)));
            Assert.AreEqual(OcrCropEnhancementDecision.NotConfigured, OcrCropEnhancementPolicy.Decide(Analyze(4), null));
            Assert.AreEqual(OcrCropEnhancementDecision.InsufficientVariation, OcrCropEnhancementPolicy.Decide(Analyze(0), options));
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, OcrCropEnhancementPolicy.Decide(Analyze(4), options));
            Assert.AreEqual(OcrCropEnhancementDecision.SufficientContrast, OcrCropEnhancementPolicy.Decide(Analyze(32), options));
            var denoise = new OcrCropEnhancementOptions(OcrCropEnhancementMode.GaussianDenoise, noiseThreshold: 1);
            Assert.AreEqual(OcrCropEnhancementDecision.SufficientQuality, OcrCropEnhancementPolicy.Decide(Analyze(0), denoise));
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, OcrCropEnhancementPolicy.Decide(Analyze(4), denoise));
            var adaptive = new OcrCropEnhancementOptions(OcrCropEnhancementMode.AdaptiveThreshold, adaptiveBlockSize: 3);
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, OcrCropEnhancementPolicy.Decide(Analyze(4), adaptive));
            Assert.AreEqual(OcrCropEnhancementDecision.SufficientContrast, OcrCropEnhancementPolicy.Decide(Analyze(64), adaptive));
            Assert.AreEqual(OcrCropEnhancementDecision.SufficientQuality, OcrCropEnhancementPolicy.Decide(OcrPixelQualityAnalyzer.Analyze(new VisualSize(2, 2), (_, _) => 128), adaptive));
            var sharpen = new OcrCropEnhancementOptions(OcrCropEnhancementMode.UnsharpMask, sharpnessThreshold: 1000000);
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, OcrCropEnhancementPolicy.Decide(OcrPixelQualityAnalyzer.Analyze(new VisualSize(20, 20), (x, _) => (byte)(100 + x)), sharpen));
            Assert.AreEqual(OcrCropEnhancementDecision.SufficientQuality, OcrCropEnhancementPolicy.Decide(Analyze(64), new OcrCropEnhancementOptions(OcrCropEnhancementMode.UnsharpMask)));
            var upscale = new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale, upscaleFactor: 2);
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, OcrCropEnhancementPolicy.Decide(Analyze(4), upscale));
            Assert.AreEqual(new VisualSize(40, 40), upscale.CalculateUpscaledSize(new VisualSize(20, 20)));
            var upscaleLimited = new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale, maximumPixelsPerCrop: 399);
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, Assert.ThrowsExactly<OcrPipelineException>(() => OcrCropEnhancementPolicy.Decide(Analyze(4), upscaleLimited)).ErrorCode);
            var shadow = new OcrCropEnhancementOptions(OcrCropEnhancementMode.ShadowNormalize, shadowVariationThreshold: 3);
            Assert.AreEqual(OcrCropEnhancementDecision.SufficientQuality, OcrCropEnhancementPolicy.Decide(Analyze(0), shadow));
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, OcrCropEnhancementPolicy.Decide(Analyze(4), shadow));
            var shadowLimited = new OcrCropEnhancementOptions(OcrCropEnhancementMode.ShadowNormalize, shadowVariationThreshold: 3, maximumPixelsPerCrop: 399);
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, Assert.ThrowsExactly<OcrPipelineException>(() => OcrCropEnhancementPolicy.Decide(Analyze(4), shadowLimited)).ErrorCode);
            var jpeg = new OcrCropEnhancementOptions(OcrCropEnhancementMode.JpegArtifactSuppress, jpegArtifactThreshold: 1);
            Assert.AreEqual(OcrCropEnhancementDecision.SufficientQuality, OcrCropEnhancementPolicy.Decide(Analyze(0), jpeg));
            Assert.AreEqual(OcrCropEnhancementDecision.Applied, OcrCropEnhancementPolicy.Decide(Analyze(4), jpeg));
            var jpegLimited = new OcrCropEnhancementOptions(OcrCropEnhancementMode.JpegArtifactSuppress, jpegArtifactThreshold: 1, maximumPixelsPerCrop: 399);
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, Assert.ThrowsExactly<OcrPipelineException>(() => OcrCropEnhancementPolicy.Decide(Analyze(4), jpegLimited)).ErrorCode);
            var limited = new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe, maximumPixelsPerCrop: 399);
            Assert.AreEqual(VisualErrorCodes.OcrLimitExceeded, Assert.ThrowsExactly<OcrPipelineException>(() => OcrCropEnhancementPolicy.Decide(Analyze(4), limited)).ErrorCode);
            Assert.AreEqual(OcrCropEnhancementDecision.SufficientContrast, OcrCropEnhancementPolicy.Decide(Analyze(64), limited));
        }

        [TestMethod]
        public void EnhancementOptionsAreOptInImmutableAndRejectUnsafeValues()
        {
            var initial = new OcrCropProcessingOptions();
            var enhanced = initial.WithEnhancement(new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe));
            Assert.IsNull(initial.Enhancement); Assert.AreEqual(OcrCropEnhancementMode.GrayClahe, enhanced.Enhancement!.Mode);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions((OcrCropEnhancementMode)8));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe, lowContrastThreshold: double.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe, minimumContrast: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.ContrastNormalize, targetStandardDeviation: 8));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.ContrastNormalize, maximumGain: double.PositiveInfinity));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe, claheClipLimit: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe, claheGridSize: 32));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe, maximumPixelsPerCrop: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.GaussianDenoise, noiseThreshold: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.GaussianDenoise, denoiseKernelSize: 4));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.GaussianDenoise, denoiseKernelSize: 11));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.GaussianDenoise, denoiseSigma: double.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.AdaptiveThreshold, adaptiveBlockSize: 4));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.AdaptiveThreshold, adaptiveBlockSize: 33));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.AdaptiveThreshold, adaptiveConstant: double.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.UnsharpMask, sharpnessThreshold: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.UnsharpMask, sharpenKernelSize: 4));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.UnsharpMask, sharpenAmount: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.UnsharpMask, sharpenSigma: double.PositiveInfinity));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale, upscaleFactor: 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale, upscaleFactor: 4.1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale, upscaleInterpolation: (TextCropInterpolation)8));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.ShadowNormalize, shadowVariationThreshold: double.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.ShadowNormalize, shadowKernelSize: 4));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.JpegArtifactSuppress, jpegArtifactThreshold: 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCropEnhancementOptions(OcrCropEnhancementMode.JpegArtifactSuppress, jpegKernelSize: 9));
        }

        [TestMethod]
        public void EvidenceRejectsMissingUnexpectedOrWrongSizedEnhancement()
        {
            var profile = new TextCropProfile("enhance.test", 8, OcrRecognitionWidthMode.Fixed, 16, 16);
            var quad = new TextQuadrilateral(new PointF(0,0), new PointF(16,0), new PointF(16,8), new PointF(0,8), TextCornerOrder.TopLeftClockwise);
            var region = new TextRegion(0, 1, quad.Polygon, quad);
            var low = OcrPixelQualityAnalyzer.Analyze(new VisualSize(16,8), (x, _) => (byte)(120 + x));
            var enabled = profile.WithCropProcessing(new OcrCropProcessingOptions().WithEnhancement(new OcrCropEnhancementOptions(OcrCropEnhancementMode.ContrastNormalize)));
            Assert.ThrowsExactly<ArgumentException>(() => new OcrCropDiagnostics(new TextCropRequest(region, enabled), low, low));
            Assert.ThrowsExactly<ArgumentException>(() => new OcrCropDiagnostics(new TextCropRequest(region, profile), low, low, low));
            var wrong = OcrPixelQualityAnalyzer.Analyze(new VisualSize(4,4), (_, _) => 100);
            Assert.ThrowsExactly<ArgumentException>(() => new OcrCropDiagnostics(new TextCropRequest(region, enabled), low, low, wrong));
            var valid = new OcrCropDiagnostics(new TextCropRequest(region, enabled), low, low, low);
            Assert.AreEqual(3d, valid.AppliedGain); Assert.AreEqual(OcrCropEnhancementDecision.Applied, valid.EnhancementDecision);
        }
    }
}
