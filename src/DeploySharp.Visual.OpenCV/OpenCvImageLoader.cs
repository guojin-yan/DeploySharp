using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgCodecs;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    // The loader owns the returned Mat; callers must dispose it on every success path.
    // 加载器拥有返回的 Mat；调用方必须在每条成功路径上释放它。
    internal static class OpenCvImageLoader
    {
        internal static Mat Decode(OpenCvImageSource source)
        {
            try
            {
                return source.Kind == OpenCvImageSourceKind.File
                    ? ImageCodecs.ImRead(source.FilePath!, ImreadModes.Unchanged)
                    : ImageCodecs.ImDecode(source.ReadEncodedBytes(), ImreadModes.Unchanged);
            }
            catch (OpenCvException exception)
            {
                throw new OpenCvVisualException(
                    OpenCvErrorCodes.DecodeFailed,
                    "OpenCV could not decode the image.",
                    exception,
                    "sourceKind=" + source.Kind + ";length=" + source.Length);
            }
        }

        internal static void Validate(Mat image, OpenCvImageSource source)
        {
            if (image.Empty || !image.HasData || image.Rows <= 0 || image.Cols <= 0)
            {
                throw new OpenCvVisualException(
                    OpenCvErrorCodes.DecodeFailed,
                    "The encoded content did not produce a non-empty image.",
                    technicalDetails: "sourceKind=" + source.Kind);
            }

            if (image.Depth != MatType.CV_8U)
            {
                throw new OpenCvVisualException(
                    OpenCvErrorCodes.DecodeFailed,
                    "Only 8-bit decoded images are supported by this preview adapter.",
                    technicalDetails: "depth=" + image.Depth);
            }

            if (image.Channels != 1 && image.Channels != 3 && image.Channels != 4)
            {
                throw new OpenCvVisualException(
                    OpenCvErrorCodes.DecodeFailed,
                    "Only grayscale, BGR, and BGRA decoded images are supported.",
                    technicalDetails: "channels=" + image.Channels);
            }

            long pixels = checked((long)image.Rows * image.Cols);
            if (pixels > 100_000_000L)
            {
                throw new OpenCvVisualException(
                    OpenCvErrorCodes.InputBoundary,
                    "The decoded image exceeds the pixel limit.",
                    technicalDetails: "pixels=" + pixels);
            }
        }
    }
}
