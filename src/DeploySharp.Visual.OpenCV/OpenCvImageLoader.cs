using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgCodecs;
using CoreOperations = JYPPX.OpenCvSharp.Core.Cv2;
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
                // IMREAD_UNCHANGED intentionally preserves the encoded pixels, which also
                // means OpenCV does not apply JPEG EXIF orientation. Normalize that metadata
                // here so detector coordinates, recognition crops and displayed source pixels
                // share one canonical top-left origin for both file and byte inputs.
                byte[] encoded = source.ReadEncodedBytes();
                Mat decoded = ImageCodecs.ImDecode(encoded, ImreadModes.Unchanged);
                try { return ApplyExifOrientation(decoded, ExifOrientationReader.Read(encoded)); }
                catch { decoded.Dispose(); throw; }
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

        private static Mat ApplyExifOrientation(Mat image, int orientation)
        {
            if (orientation == 1) return image;

            Mat oriented;
            switch (orientation)
            {
                case 2:
                    oriented = CoreOperations.Flip(image, 1);
                    break;
                case 3:
                    oriented = CoreOperations.Rotate(image, RotateFlags.Rotate180);
                    break;
                case 4:
                    oriented = CoreOperations.Flip(image, 0);
                    break;
                case 5:
                    oriented = CoreOperations.Transpose(image);
                    break;
                case 6:
                    oriented = CoreOperations.Rotate(image, RotateFlags.Rotate90Clockwise);
                    break;
                case 7:
                    using (Mat transposed = CoreOperations.Transpose(image)) oriented = CoreOperations.Flip(transposed, -1);
                    break;
                case 8:
                    oriented = CoreOperations.Rotate(image, RotateFlags.Rotate90Counterclockwise);
                    break;
                default:
                    return image;
            }

            image.Dispose();
            return oriented;
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

    // Small allocation-free-after-read JPEG APP1 parser. It deliberately ignores malformed
    // or unsupported metadata and leaves the pixels untouched, preserving decoder behavior for
    // PNG/BMP/WebP and for JPEGs without an orientation tag.
    internal static class ExifOrientationReader
    {
        internal static int Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12 || bytes[0] != 0xff || bytes[1] != 0xd8) return 1;
            int cursor = 2;
            while (cursor + 4 <= bytes.Length && bytes[cursor] == 0xff)
            {
                byte marker = bytes[cursor + 1];
                cursor += 2;
                if (marker == 0xd8 || marker == 0xd9) continue;
                if (marker == 0xda) break;
                int length = (bytes[cursor] << 8) | bytes[cursor + 1];
                if (length < 2 || cursor + length > bytes.Length) break;
                if (marker == 0xe1 && length >= 8 && bytes[cursor + 2] == (byte)'E' && bytes[cursor + 3] == (byte)'x' && bytes[cursor + 4] == (byte)'i' && bytes[cursor + 5] == (byte)'f' && bytes[cursor + 6] == 0 && bytes[cursor + 7] == 0)
                {
                    int tiff = cursor + 8;
                    return ReadTiffOrientation(bytes, tiff, cursor + length);
                }
                cursor += length;
            }
            return 1;
        }

        private static int ReadTiffOrientation(byte[] bytes, int tiff, int end)
        {
            if (tiff + 8 > end) return 1;
            bool littleEndian;
            if (bytes[tiff] == (byte)'I' && bytes[tiff + 1] == (byte)'I') littleEndian = true;
            else if (bytes[tiff] == (byte)'M' && bytes[tiff + 1] == (byte)'M') littleEndian = false;
            else return 1;
            if (ReadUInt16(bytes, tiff + 2, littleEndian) != 42) return 1;
            int ifd = checked(tiff + (int)ReadUInt32(bytes, tiff + 4, littleEndian));
            if (ifd < tiff || ifd + 2 > end) return 1;
            int count = ReadUInt16(bytes, ifd, littleEndian);
            for (int index = 0; index < count; index++)
            {
                int entry = checked(ifd + 2 + (index * 12));
                if (entry + 12 > end) return 1;
                if (ReadUInt16(bytes, entry, littleEndian) != 0x0112) continue;
                int type = ReadUInt16(bytes, entry + 2, littleEndian);
                int itemCount = checked((int)ReadUInt32(bytes, entry + 4, littleEndian));
                if (type != 3 || itemCount < 1) return 1;
                int value = ReadUInt16(bytes, entry + 8, littleEndian);
                return value >= 1 && value <= 8 ? value : 1;
            }
            return 1;
        }

        private static int ReadUInt16(byte[] bytes, int offset, bool littleEndian)
            => littleEndian ? bytes[offset] | (bytes[offset + 1] << 8) : (bytes[offset] << 8) | bytes[offset + 1];

        private static uint ReadUInt32(byte[] bytes, int offset, bool littleEndian)
            => littleEndian
                ? (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24))
                : (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
    }
}
