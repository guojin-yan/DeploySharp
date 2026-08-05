using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Identifies the origin of encoded image bytes. / 标识编码图像字节的来源。</summary>
    public enum OpenCvImageSourceKind
    {
        /// <summary>A regular local file. / 本地普通文件。</summary>
        File = 0,
        /// <summary>A caller-owned stream copied during construction. / 构造时复制的调用方流。</summary>
        Stream = 1,
        /// <summary>A caller-provided byte buffer copied during construction. / 构造时复制的调用方字节缓冲区。</summary>
        Bytes = 2
    }

    /// <summary>Describes bounded encoded image input without exposing an OpenCV Mat. / 描述有边界的编码图像输入，不暴露 OpenCV Mat。</summary>
    public sealed class OpenCvImageSource
    {
        private const long DefaultMaximumBytes = 16L * 1024L * 1024L;
        private readonly string? _filePath;
        private readonly byte[]? _bytes;

        private OpenCvImageSource(OpenCvImageSourceKind kind, string? filePath, byte[]? bytes, long maximumBytes)
        {
            Kind = kind;
            _filePath = filePath;
            _bytes = bytes;
            MaximumBytes = maximumBytes;
            Length = kind == OpenCvImageSourceKind.File ? new FileInfo(filePath!).Length : bytes!.LongLength;
            Sha256 = kind == OpenCvImageSourceKind.File ? ComputeFileHash(filePath!) : ComputeHash(bytes!);
        }

        /// <summary>Creates a source from an absolute regular file path. / 从绝对普通文件路径创建输入源。</summary>
        public static OpenCvImageSource FromFile(string path, long maximumBytes = DefaultMaximumBytes)
        {
            if (string.IsNullOrWhiteSpace(path)) throw Boundary("An image file path is required.");
            if (!Path.IsPathRooted(path)) throw Boundary("The image file path must be absolute.");
            if (maximumBytes <= 0) throw Boundary("The image size limit must be positive.");
            string fullPath;
            try { fullPath = Path.GetFullPath(path); } catch (Exception exception) { throw Boundary("The image file path is invalid.", exception); }
            if (Directory.Exists(fullPath)) throw Boundary("The image path must identify a file.");
            if (!File.Exists(fullPath)) throw Boundary("The image file does not exist.");
            try
            {
                FileAttributes attributes = File.GetAttributes(fullPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw Boundary("Reparse-point image files are not accepted.");
                long length = new FileInfo(fullPath).Length;
                if (length <= 0 || length > maximumBytes) throw Boundary("The image file size is outside the accepted range.", technicalDetails: "length=" + length + ";limit=" + maximumBytes);
            }
            catch (OpenCvVisualException) { throw; }
            catch (Exception exception) { throw Boundary("The image file cannot be inspected.", exception); }

            return new OpenCvImageSource(OpenCvImageSourceKind.File, fullPath, null, maximumBytes);
        }

        /// <summary>Copies a bounded stream into an independent image source. / 将有边界的流复制到独立图像源。</summary>
        public static OpenCvImageSource FromStream(Stream stream, long maximumBytes = DefaultMaximumBytes)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw Boundary("The image stream must be readable.");
            if (maximumBytes <= 0) throw Boundary("The image size limit must be positive.");
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[81920];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + read > maximumBytes) throw Boundary("The encoded image exceeds the configured size limit.", technicalDetails: "limit=" + maximumBytes);
                    buffer.Write(chunk, 0, read);
                }

                if (buffer.Length == 0) throw Boundary("The encoded image is empty.");
                return new OpenCvImageSource(OpenCvImageSourceKind.Stream, null, buffer.ToArray(), maximumBytes);
            }
        }

        /// <summary>Copies an encoded image buffer into an independent image source. / 将编码图像缓冲区复制到独立图像源。</summary>
        public static OpenCvImageSource FromBytes(byte[] bytes, long maximumBytes = DefaultMaximumBytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length == 0 || bytes.LongLength > maximumBytes) throw Boundary("The encoded image buffer is empty or exceeds the configured size limit.", technicalDetails: "length=" + bytes.LongLength + ";limit=" + maximumBytes);
            return new OpenCvImageSource(OpenCvImageSourceKind.Bytes, null, (byte[])bytes.Clone(), maximumBytes);
        }

#if DEPLOYSHARP_HAS_SPAN
        /// <summary>Copies an encoded image memory block into an independent image source. / 将编码图像内存块复制到独立图像源。</summary>
        public static OpenCvImageSource FromBytes(ReadOnlyMemory<byte> bytes, long maximumBytes = DefaultMaximumBytes)
        {
            return FromBytes(bytes.ToArray(), maximumBytes);
        }
#endif

        /// <summary>Gets the source kind. / 获取源类型。</summary>
        public OpenCvImageSourceKind Kind { get; }
        /// <summary>Gets the encoded byte length. / 获取编码字节长度。</summary>
        public long Length { get; }
        /// <summary>Gets the lowercase SHA256 of the encoded bytes. / 获取编码字节的小写 SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets the configured maximum encoded size. / 获取配置的最大编码大小。</summary>
        public long MaximumBytes { get; }
        /// <summary>Gets the normalized source path for file inputs, or null for memory inputs. / 获取文件输入的规范路径；内存输入返回 null。</summary>
        public string? FilePath => _filePath;

        internal byte[] ReadEncodedBytes()
        {
            if (Kind == OpenCvImageSourceKind.File)
            {
                try { return File.ReadAllBytes(_filePath!); }
                catch (Exception exception) { throw Boundary("The image file could not be read.", exception); }
            }

            return (byte[])_bytes!.Clone();
        }

        private static string ComputeFileHash(string path)
        {
            using (var stream = File.OpenRead(path)) using (SHA256 algorithm = SHA256.Create()) return ToHex(algorithm.ComputeHash(stream));
        }

        private static string ComputeHash(byte[] bytes)
        {
            using (SHA256 algorithm = SHA256.Create()) return ToHex(algorithm.ComputeHash(bytes));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static OpenCvVisualException Boundary(string message, Exception? inner = null, string? technicalDetails = null)
        {
            return new OpenCvVisualException(OpenCvErrorCodes.InputBoundary, message, inner, technicalDetails);
        }
    }
}
