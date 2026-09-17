using System;
using System.Threading;
using JYPPX.OpenCvSharp.Core;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Reads native-scale quality evidence from an 8-bit Gray/BGR/BGRA Mat, including non-contiguous views. / 从 8 位 Gray/BGR/BGRA Mat 读取原始尺度质量证据，支持非连续视图。</summary>
    public static class OpenCvOcrPixelQuality
    {
        /// <summary>Synchronously borrows the Mat without copying; callers must prevent mutation/disposal until return. Alpha is ignored like OCR preprocessing. / 同步借用 Mat 而不复制，返回前调用方须阻止修改或释放；与 OCR 预处理一致忽略 Alpha。</summary>
        public static OcrPixelQualityDiagnostics Analyze(Mat image, OcrPixelQualityOptions? options = null, TextRegion? region = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            cancellationToken.ThrowIfCancellationRequested();
            if (image.Empty || !image.HasData || image.Rows < 1 || image.Cols < 1 || image.Depth != MatType.CV_8U || (image.Channels != 1 && image.Channels != 3 && image.Channels != 4))
                throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Quality assessment requires a nonempty 8-bit Gray/BGR/BGRA Mat.");
            int channels = image.Channels;
            ulong step = image.Step.ToUInt64();
            if (step < (ulong)image.Cols * (ulong)channels || step > int.MaxValue || (ulong)(image.Rows - 1) * step + (ulong)image.Cols * (ulong)channels > int.MaxValue)
                throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Quality assessment Mat stride exceeds supported bounds.");
            var reader = new LuminanceReader(image.Data, (int)step, channels);
            try { return OcrPixelQualityAnalyzer.Analyze(new VisualSize(image.Cols, image.Rows), reader.Read, options, region, cancellationToken); }
            finally { GC.KeepAlive(image); }
        }
        private sealed class LuminanceReader
        {
            private readonly IntPtr _data;
            private readonly int _step, _channels;
            internal LuminanceReader(IntPtr data, int step, int channels) { _data = data; _step = step; _channels = channels; }
            internal unsafe byte Read(int x, int y)
            {
                byte* pixel = (byte*)_data.ToPointer() + y * _step + x * _channels;
                return _channels == 1 ? pixel[0] : (byte)((77 * pixel[2] + 150 * pixel[1] + 29 * pixel[0] + 128) >> 8);
            }
        }
    }
}
