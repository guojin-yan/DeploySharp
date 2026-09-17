using System;
using System.Threading;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgProc;
using CoreOperations = JYPPX.OpenCvSharp.Core.Cv2;
using ImageProcessing = JYPPX.OpenCvSharp.ImgProc.Cv2;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    // One lazy instance per crop worker: no shared pixels, LUT or CLAHE state.
    // 每裁剪工作线程一个延迟创建实例：不共享像素、LUT或CLAHE状态。
    internal sealed class OpenCvOcrCropEnhancer : IDisposable
    {
        private readonly Mat _enhanced = new Mat();
        private readonly Mat _gray = new Mat();
        private readonly Mat _blurred = new Mat();
        private readonly byte[] _lookup = new byte[256];
        private CLAHE? _clahe;
        private OcrCropEnhancementOptions? _claheOptions;

        internal unsafe Mat Apply(Mat source, OcrPixelQualityDiagnostics quality, OcrCropEnhancementOptions options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (OcrCropEnhancementPolicy.Decide(quality, options) != OcrCropEnhancementDecision.Applied) return source;
            int channels = source.Channels;
            bool linear = options.Mode == OcrCropEnhancementMode.ContrastNormalize;
            bool denoise = options.Mode == OcrCropEnhancementMode.GaussianDenoise;
            bool adaptiveThreshold = options.Mode == OcrCropEnhancementMode.AdaptiveThreshold;
            bool unsharpMask = options.Mode == OcrCropEnhancementMode.UnsharpMask;
            bool requiresGray = adaptiveThreshold || (!linear && !denoise && !unsharpMask);
            if (linear)
            {
                double gain = Math.Min(options.MaximumGain, options.TargetStandardDeviation / quality.LuminanceStandardDeviation!.Value);
                double mean = quality.MeanLuminance!.Value;
                for (int i = 0; i < 256; i++) _lookup[i] = (byte)Math.Max(0, Math.Min(255, Math.Floor(mean + gain * (i - mean) + .5)));
                _enhanced.Create(source.Rows, source.Cols, source.Type);
            }
            else if (requiresGray) _gray.Create(source.Rows, source.Cols, MatType.CV_8UC1);
            Mat destination = linear || denoise || unsharpMask ? _enhanced : _gray;
            if (denoise)
            {
                _enhanced.Create(source.Rows, source.Cols, source.Type);
                ImageProcessing.GaussianBlur(source, _enhanced, new Size(options.DenoiseKernelSize, options.DenoiseKernelSize), options.DenoiseSigma, options.DenoiseSigma, BorderTypes.Reflect101);
                token.ThrowIfCancellationRequested();
                return _enhanced;
            }
            long sourceStep = checked((long)source.Step.ToUInt64()), destinationStep = checked((long)destination.Step.ToUInt64());
            byte* input = (byte*)source.Data.ToPointer(); byte* output = (byte*)destination.Data.ToPointer();
            try
            {
                for (int y = 0; y < source.Rows; y++)
                {
                    token.ThrowIfCancellationRequested();
                    byte* src = input + y * sourceStep; byte* dst = output + y * destinationStep;
                    for (int x = 0; x < source.Cols; x++)
                    {
                        if ((x & 1023) == 0) token.ThrowIfCancellationRequested();
                        int offset = x * channels;
                        if (linear)
                        {
                            dst[offset] = _lookup[src[offset]];
                            if (channels > 1) { dst[offset + 1] = _lookup[src[offset + 1]]; dst[offset + 2] = _lookup[src[offset + 2]]; }
                            if (channels == 4) dst[offset + 3] = src[offset + 3];
                        }
                        else if (requiresGray) dst[x] = channels == 1 ? src[x] : (byte)((77 * src[offset + 2] + 150 * src[offset + 1] + 29 * src[offset] + 128) >> 8);
                    }
                }
                if (unsharpMask)
                {
                    _blurred.Create(source.Rows, source.Cols, source.Type);
                    ImageProcessing.GaussianBlur(source, _blurred, new Size(options.SharpenKernelSize, options.SharpenKernelSize), options.SharpenSigma, options.SharpenSigma, BorderTypes.Reflect101);
                    CoreOperations.AddWeighted(source, 1 + options.SharpenAmount, _blurred, -options.SharpenAmount, 0, _enhanced);
                }
                else if (!linear)
                {
                    if (adaptiveThreshold)
                    {
                        ImageProcessing.AdaptiveThreshold(_gray, _enhanced, 255, AdaptiveThresholdTypes.GaussianC,
                            ThresholdTypes.Binary, options.AdaptiveBlockSize, options.AdaptiveConstant);
                    }
                    else if (unsharpMask)
                    {
                        _blurred.Create(source.Rows, source.Cols, source.Type);
                        ImageProcessing.GaussianBlur(source, _blurred, new Size(options.SharpenKernelSize, options.SharpenKernelSize), options.SharpenSigma, options.SharpenSigma, BorderTypes.Reflect101);
                        CoreOperations.AddWeighted(source, 1 + options.SharpenAmount, _blurred, -options.SharpenAmount, 0, _enhanced);
                    }
                    else
                    {
                        if (_clahe == null || _claheOptions != options)
                        {
                            _clahe?.Dispose(); _clahe = null;
                            _clahe = ImageProcessing.CreateCLAHE(options.ClaheClipLimit, new Size(options.ClaheGridSize, options.ClaheGridSize));
                            _claheOptions = options;
                        }
                        _clahe.Apply(_gray, _enhanced);
                    }
                }
                token.ThrowIfCancellationRequested();
                return _enhanced;
            }
            finally { GC.KeepAlive(source); GC.KeepAlive(destination); }
        }

        public void Dispose() { _clahe?.Dispose(); _blurred.Dispose(); _enhanced.Dispose(); _gray.Dispose(); }
    }
}
