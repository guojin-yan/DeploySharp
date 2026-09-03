using System;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.OpenCvSharp.Core;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Stores one tightly packed, row-major BGR UInt8 image for zero-reformat backend interop. / 存储用于零重排后端互操作的紧凑行优先 BGR UInt8 图像。</summary>
    public sealed class OpenCvBgrImage
    {
        private readonly byte[] _pixels;

        /// <summary>Initializes a compact BGR image and applies the requested array ownership policy. / 初始化紧凑 BGR 图像并应用指定数组所有权策略。</summary>
        public OpenCvBgrImage(int width, int height, byte[] pixels, TensorBufferOwnership ownership = TensorBufferOwnership.Copy, string? inputId = null)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (pixels.LongLength != checked((long)width * height * 3)) throw new ArgumentException("The packed BGR byte count does not match width * height * 3.", nameof(pixels));
            Width = width;
            Height = height;
            Ownership = ownership;
            InputId = string.IsNullOrWhiteSpace(inputId) ? null : inputId;
            _pixels = ownership == TensorBufferOwnership.Copy ? (byte[])pixels.Clone() : pixels;
        }

        /// <summary>Gets the image width. / 获取图像宽度。</summary>
        public int Width { get; }
        /// <summary>Gets the image height. / 获取图像高度。</summary>
        public int Height { get; }
        /// <summary>Gets the byte-array ownership policy. / 获取字节数组所有权策略。</summary>
        public TensorBufferOwnership Ownership { get; }
        /// <summary>Gets an optional stable input identifier. / 获取可选稳定输入标识。</summary>
        public string? InputId { get; }
        /// <summary>Gets the packed BGR byte length. / 获取紧凑 BGR 字节长度。</summary>
        public int ByteLength => _pixels.Length;

        /// <summary>Returns the immutable backing array for explicit native upload; callers must not modify it while an inference call is active. / 返回用于显式原生上传的不可变底层数组；推理调用期间调用方不得修改。</summary>
        public byte[] GetReadOnlyInteropBuffer() => _pixels;

        /// <summary>Returns a defensive copy of the packed BGR pixels. / 返回紧凑 BGR 像素的防御性副本。</summary>
        public byte[] ToArray() => (byte[])_pixels.Clone();
    }

    /// <summary>Decodes encoded images into compact BGR bytes without CPU resize or normalization. / 将编码图像解码为紧凑 BGR 字节且不执行 CPU 缩放或归一化。</summary>
    public sealed class OpenCvBgrImageFactory
    {
        /// <summary>Decodes one bounded source into a compact BGR image. / 将一个有界输入解码为紧凑 BGR 图像。</summary>
        public OpenCvBgrImage Create(OpenCvImageSource source, string? inputId = null, CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            cancellationToken.ThrowIfCancellationRequested();
            OpenCvRuntimePreflight.Check();
            using Mat decoded = OpenCvImageLoader.Decode(source);
            OpenCvImageLoader.Validate(decoded, source);
            cancellationToken.ThrowIfCancellationRequested();
            return CopyPackedBgr(decoded, inputId ?? source.Sha256, cancellationToken);
        }

        /// <summary>Decodes one absolute image file into compact BGR bytes. / 将一个绝对图像文件解码为紧凑 BGR 字节。</summary>
        public OpenCvBgrImage CreateFromFile(string path, string? inputId = null, CancellationToken cancellationToken = default)
            => Create(OpenCvImageSource.FromFile(path), inputId, cancellationToken);

        private static unsafe OpenCvBgrImage CopyPackedBgr(Mat image, string? inputId, CancellationToken cancellationToken)
        {
            int width = image.Cols;
            int height = image.Rows;
            int channels = image.Channels;
            int sourceRowBytes = checked(width * channels);
            int destinationRowBytes = checked(width * 3);
            ulong nativeStep = image.Step.ToUInt64();
            if (nativeStep < (ulong)sourceRowBytes || nativeStep > int.MaxValue) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV reported an unsupported row stride.", technicalDetails: "step=" + nativeStep);
            IntPtr sourceData = image.Data;
            if (sourceData == IntPtr.Zero) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV returned a null image buffer.");
            var pixels = new byte[checked(destinationRowBytes * height)];
            fixed (byte* destinationBase = pixels)
            {
                for (int y = 0; y < height; y++)
                {
                    if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                    byte* sourceRow = (byte*)sourceData + checked(y * (int)nativeStep);
                    byte* destinationRow = destinationBase + checked(y * destinationRowBytes);
                    if (channels == 3)
                    {
                        Buffer.MemoryCopy(sourceRow, destinationRow, destinationRowBytes, destinationRowBytes);
                        continue;
                    }

                    for (int x = 0; x < width; x++)
                    {
                        int source = x * channels;
                        int destination = x * 3;
                        if (channels == 1)
                        {
                            byte value = sourceRow[source];
                            destinationRow[destination] = value;
                            destinationRow[destination + 1] = value;
                            destinationRow[destination + 2] = value;
                        }
                        else
                        {
                            destinationRow[destination] = sourceRow[source];
                            destinationRow[destination + 1] = sourceRow[source + 1];
                            destinationRow[destination + 2] = sourceRow[source + 2];
                        }
                    }
                }
            }
            return new OpenCvBgrImage(width, height, pixels, TensorBufferOwnership.Transfer, inputId);
        }
    }
}
