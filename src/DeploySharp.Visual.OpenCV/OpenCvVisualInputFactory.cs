using System;
#if NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
using System.Buffers;
#endif
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgCodecs;
using JYPPX.OpenCvSharp.ImgProc;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using ImageProcessing = JYPPX.OpenCvSharp.ImgProc.Cv2;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Loads encoded images with OpenCV and creates backend-neutral prepared tensors. / 使用 OpenCV 加载编码图像并创建后端无关的已准备张量。</summary>
    public sealed class OpenCvVisualInputFactory
    {
        /// <summary>Creates a prepared tensor and releases every native Mat before returning. / 创建已准备张量，并在返回前释放所有 native Mat。</summary>
        public PreparedVisualInput Create(OpenCvImageSource source, string inputName, OpenCvPreprocessOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(inputName)) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "An input tensor name is required.");
            ObserveCancellation(cancellationToken);
            OpenCvRuntimePreflight.Check();
            ObserveCancellation(cancellationToken);

            try
            {
                using (Mat decoded = OpenCvImageLoader.Decode(source))
                {
                    OpenCvImageLoader.Validate(decoded, source);
                    return CreateFromDecoded(decoded, inputName, options, inputId, cancellationToken, auxiliaryInputs);
                }
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The OpenCV image operation was cancelled at a synchronous boundary.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while preparing the visual tensor.", exception, "sourceKind=" + source.Kind); }
            catch (DllNotFoundException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime is unavailable.", exception); }
            catch (BadImageFormatException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime architecture is incompatible.", exception, "processBits=" + (IntPtr.Size * 8)); }
            catch (EntryPointNotFoundException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime ABI is incompatible.", exception); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The visual tensor could not be prepared.", exception, "sourceKind=" + source.Kind); }
        }

        /// <summary>Creates a prepared tensor from an absolute local image path. / 从绝对本地图像路径创建已准备张量。</summary>
        public PreparedVisualInput CreateFromFile(string path, string inputName, OpenCvPreprocessOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            return Create(OpenCvImageSource.FromFile(path), inputName, options, inputId, cancellationToken, auxiliaryInputs);
        }

        internal static PreparedVisualInput CreateFromDecoded(Mat decoded, string inputName, OpenCvPreprocessOptions options, string? inputId, CancellationToken cancellationToken, IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (decoded == null) throw new ArgumentNullException(nameof(decoded));
            var sourceSize = new VisualSize(decoded.Cols, decoded.Rows);
            Mat? convertedColor = null;
            try
            {
                Mat geometrySource = PrepareColorForGeometry(decoded, options, out convertedColor);
                if (options.Interpolation == OpenCvInterpolation.PillowBicubic)
                {
                    byte[] resized = PillowBicubicResize(CopyRows(geometrySource), geometrySource.Cols, geometrySource.Rows, geometrySource.Channels, options.ModelSize.Width, options.ModelSize.Height, cancellationToken);
                    ITensor resizedTensor = CreateTensorFromPixels(resized, options.ModelSize.Width, options.ModelSize.Height, geometrySource.Channels, options, cancellationToken);
                    var pillowMeans = new float[options.ChannelCount];
                    var pillowScales = new float[options.ChannelCount];
                    for (int channel = 0; channel < pillowScales.Length; channel++)
                    {
                        pillowMeans[channel] = options.Mean(channel);
                        pillowScales[channel] = 1f / options.StandardDeviation(channel);
                    }
                    var pillowDescriptor = new VisualPreprocessingDescriptor(options.ColorOrder, pillowMeans, pillowScales, "OpenCV 5 preview decode plus managed Pillow-compatible antialiased bicubic resize; pixels copied before Mat disposal." + NormalizationNote(options));
                    return new PreparedVisualInput(inputName, resizedTensor, sourceSize, options.ModelSize, options.BatchSize, options.Layout, ImageTransform.Resize(sourceSize, options.ModelSize), pillowDescriptor, inputId, PreparedInputOwnership.Borrowed, null, auxiliaryInputs);
                }
                using (Mat geometric = ApplyGeometry(geometrySource, sourceSize, options, out ImageTransform transform))
                {
                    ObserveCancellation(cancellationToken);
                    // Stream rows directly from the native Mat into the final tensor.
                    // This avoids retaining a full-size managed pixel copy for the
                    // common OpenCV resize/letterbox paths.
                    ITensor tensor = CreateTensorFromMat(geometric, options, cancellationToken);
                    var means = new float[options.ChannelCount];
                    var scales = new float[options.ChannelCount];
                    for (int channel = 0; channel < scales.Length; channel++)
                    {
                        means[channel] = options.Mean(channel);
                        scales[channel] = 1f / options.StandardDeviation(channel);
                    }
                    var descriptor = new VisualPreprocessingDescriptor(options.ColorOrder, means, scales, "OpenCV 5 preview; pixels copied to managed tensor before Mat disposal." + NormalizationNote(options));
                    return new PreparedVisualInput(inputName, tensor, sourceSize, options.ModelSize, options.BatchSize, options.Layout, transform, descriptor, inputId, PreparedInputOwnership.Borrowed, null, auxiliaryInputs);
                }
            }
            finally
            {
                // The temporary conversion Mat is owned only by this call; the decoded Mat remains caller-owned.
                // 临时颜色转换 Mat 仅由本次调用拥有；解码 Mat 仍由调用方拥有。
                convertedColor?.Dispose();
            }
        }

        private static Mat PrepareColorForGeometry(Mat source, OpenCvPreprocessOptions options, out Mat? converted)
        {
            converted = null;
            if (source.Channels == 3 && options.ColorOrder == VisualColorOrder.Gray)
            {
                converted = new Mat();
                try
                {
                    ImageProcessing.CvtColor(source, converted, ColorConversionCodes.BGR2GRAY);
                    return converted;
                }
                catch
                {
                    converted.Dispose();
                    converted = null;
                    throw;
                }
            }

            // The preview wrapper exposes BGR2GRAY, but not verified BGR2RGB/BGRA conversion enum values.
            // 当前 preview wrapper 公开了 BGR2GRAY，但未提供已核验的 BGR2RGB/BGRA 转换枚举值。
            // Channel reorder and alpha handling therefore occur after the stride-safe managed copy.
            // 因此通道重排和 alpha 处理在安全处理 stride 的托管复制之后执行。
            return source;
        }

        private static Mat ApplyGeometry(Mat source, VisualSize sourceSize, OpenCvPreprocessOptions options, out ImageTransform transform)
        {
            if (options.ResizeMode == OpenCvResizeMode.Resize)
            {
                var result = new Mat();
                ImageProcessing.Resize(source, result, new Size(options.ModelSize.Width, options.ModelSize.Height), interpolation: ToInterpolation(options.Interpolation));
                transform = ImageTransform.Resize(sourceSize, options.ModelSize);
                return result;
            }

            if (options.ResizeMode == OpenCvResizeMode.CenterCrop)
            {
                double sourceAspect = (double)sourceSize.Width / sourceSize.Height;
                double targetAspect = (double)options.ModelSize.Width / options.ModelSize.Height;
                int cropWidth = sourceSize.Width;
                int cropHeight = sourceSize.Height;
                if (sourceAspect > targetAspect) cropWidth = Math.Max(1, (int)Math.Round(sourceSize.Height * targetAspect));
                else if (sourceAspect < targetAspect) cropHeight = Math.Max(1, (int)Math.Round(sourceSize.Width / targetAspect));
                int cropX = (sourceSize.Width - cropWidth) / 2;
                int cropY = (sourceSize.Height - cropHeight) / 2;
                using (Mat crop = source.SubMat(new Rect(cropX, cropY, cropWidth, cropHeight)))
                {
                    var result = new Mat();
                    ImageProcessing.Resize(crop, result, new Size(options.ModelSize.Width, options.ModelSize.Height), interpolation: ToInterpolation(options.Interpolation));
                    transform = ImageTransform.Crop(sourceSize, options.ModelSize, new RectangleF(cropX, cropY, cropWidth, cropHeight));
                    return result;
                }
            }

            if (options.ResizeMode == OpenCvResizeMode.ShortestEdgeCenterCrop)
            {
                double cropScale = Math.Max((double)options.ModelSize.Width / sourceSize.Width, (double)options.ModelSize.Height / sourceSize.Height);
                int cropResizedWidth = Math.Max(options.ModelSize.Width, checked((int)Math.Floor(sourceSize.Width * cropScale)));
                int cropResizedHeight = Math.Max(options.ModelSize.Height, checked((int)Math.Floor(sourceSize.Height * cropScale)));
                int cropX = Math.Max(0, (cropResizedWidth - options.ModelSize.Width) / 2);
                int cropY = Math.Max(0, (cropResizedHeight - options.ModelSize.Height) / 2);
                using (var resized = new Mat())
                {
                    ImageProcessing.Resize(source, resized, new Size(cropResizedWidth, cropResizedHeight), interpolation: ToInterpolation(options.Interpolation));
                    using (Mat crop = resized.SubMat(new Rect(cropX, cropY, options.ModelSize.Width, options.ModelSize.Height)))
                    {
                        var result = new Mat();
                        crop.CopyTo(result);
                        float sourceCropX = (float)(cropX * sourceSize.Width / (double)cropResizedWidth);
                        float sourceCropY = (float)(cropY * sourceSize.Height / (double)cropResizedHeight);
                        float sourceCropWidth = (float)(options.ModelSize.Width * sourceSize.Width / (double)cropResizedWidth);
                        float sourceCropHeight = (float)(options.ModelSize.Height * sourceSize.Height / (double)cropResizedHeight);
                        transform = ImageTransform.Crop(sourceSize, options.ModelSize, new RectangleF(sourceCropX, sourceCropY, sourceCropWidth, sourceCropHeight));
                        return result;
                    }
                }
            }

            double scale = Math.Min((double)options.ModelSize.Width / sourceSize.Width, (double)options.ModelSize.Height / sourceSize.Height);
            int resizedWidth = Math.Max(1, Math.Min(options.ModelSize.Width, RoundLetterboxDimension(sourceSize.Width * scale, options.LetterboxRounding)));
            int resizedHeight = Math.Max(1, Math.Min(options.ModelSize.Height, RoundLetterboxDimension(sourceSize.Height * scale, options.LetterboxRounding)));
            bool bottomRight = options.ResizeMode == OpenCvResizeMode.LongestSidePadBottomRight;
            int left = bottomRight ? 0 : (options.ModelSize.Width - resizedWidth) / 2;
            int top = bottomRight ? 0 : (options.ModelSize.Height - resizedHeight) / 2;
            using (var resized = new Mat())
            {
                ImageProcessing.Resize(source, resized, new Size(resizedWidth, resizedHeight), interpolation: ToInterpolation(options.Interpolation));
                Scalar padding = PaddingScalar(options.PaddingColor, source.Channels);
                var result = new Mat(options.ModelSize.Height, options.ModelSize.Width, source.Type, padding);
                try
                {
                    using (Mat destination = result.SubMat(new Rect(left, top, resizedWidth, resizedHeight))) resized.CopyTo(destination);
                    transform = new ImageTransform(ImageTransformKind.Letterbox, sourceSize, options.ModelSize, (float)resizedWidth / sourceSize.Width, (float)resizedHeight / sourceSize.Height, left, top);
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
        }

        private static int RoundLetterboxDimension(double value, OpenCvLetterboxRounding rounding)
        {
            if (rounding == OpenCvLetterboxRounding.Floor) return checked((int)Math.Floor(value));
            if (rounding == OpenCvLetterboxRounding.HalfUp) return checked((int)Math.Floor(value + 0.5));
            return checked((int)Math.Round(value));
        }

        private static InterpolationFlags ToInterpolation(OpenCvInterpolation interpolation)
        {
            if (interpolation == OpenCvInterpolation.Cubic) return InterpolationFlags.Cubic;
            if (interpolation == OpenCvInterpolation.Nearest) return InterpolationFlags.Nearest;
            return InterpolationFlags.Linear;
        }

        internal static byte[] PillowBicubicResize(byte[] source, int sourceWidth, int sourceHeight, int channels, int targetWidth, int targetHeight, CancellationToken cancellationToken)
        {
            ResampleCoefficient[] horizontal = CreatePillowBicubicCoefficients(sourceWidth, targetWidth);
            ResampleCoefficient[] vertical = CreatePillowBicubicCoefficients(sourceHeight, targetHeight);
            var intermediate = new byte[checked(sourceHeight * targetWidth * channels)];
            var destination = new byte[checked(targetHeight * targetWidth * channels)];

            // Pillow-compatible sampling is intentionally kept byte-for-byte identical, but the
            // two separable passes are independent by row.  Parallelizing only sufficiently large
            // images removes a major preprocessing bottleneck for BLIP/Donut/SAM without adding
            // thread-pool overhead to OCR-sized crops or unit-test fixtures.
            ParallelOptions parallel = CreateResizeParallelOptions(cancellationToken, sourceHeight, targetHeight, targetWidth, channels);
            Parallel.For(0, sourceHeight, parallel, y =>
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    ResampleCoefficient coefficient = horizontal[x];
                    for (int channel = 0; channel < channels; channel++)
                    {
                        double sum = 0;
                        for (int index = 0; index < coefficient.Weights.Length; index++) sum += source[((y * sourceWidth + coefficient.Start + index) * channels) + channel] * coefficient.Weights[index];
                        intermediate[((y * targetWidth + x) * channels) + channel] = ClipByte(sum);
                    }
                }
            });

            Parallel.For(0, targetHeight, parallel, y =>
            {
                ResampleCoefficient coefficient = vertical[y];
                for (int x = 0; x < targetWidth; x++)
                {
                    for (int channel = 0; channel < channels; channel++)
                    {
                        double sum = 0;
                        for (int index = 0; index < coefficient.Weights.Length; index++) sum += intermediate[(((coefficient.Start + index) * targetWidth + x) * channels) + channel] * coefficient.Weights[index];
                        destination[((y * targetWidth + x) * channels) + channel] = ClipByte(sum);
                    }
                }
            });

            return destination;
        }

        internal static byte[] PillowBilinearResize(byte[] source, int sourceWidth, int sourceHeight, int channels, int targetWidth, int targetHeight, CancellationToken cancellationToken)
        {
            ResampleCoefficient[] horizontal = CreatePillowBilinearCoefficients(sourceWidth, targetWidth);
            ResampleCoefficient[] vertical = CreatePillowBilinearCoefficients(sourceHeight, targetHeight);
            var intermediate = new byte[checked(sourceHeight * targetWidth * channels)];
            var destination = new byte[checked(targetHeight * targetWidth * channels)];
            ParallelOptions parallel = CreateResizeParallelOptions(cancellationToken, sourceHeight, targetHeight, targetWidth, channels);
            Parallel.For(0, sourceHeight, parallel, y =>
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    ResampleCoefficient coefficient = horizontal[x];
                    for (int channel = 0; channel < channels; channel++)
                    {
                        double sum = 0;
                        for (int index = 0; index < coefficient.Weights.Length; index++) sum += source[((y * sourceWidth + coefficient.Start + index) * channels) + channel] * coefficient.Weights[index];
                        intermediate[((y * targetWidth + x) * channels) + channel] = ClipByte(sum);
                    }
                }
            });
            Parallel.For(0, targetHeight, parallel, y =>
            {
                ResampleCoefficient coefficient = vertical[y];
                for (int x = 0; x < targetWidth; x++)
                {
                    for (int channel = 0; channel < channels; channel++)
                    {
                        double sum = 0;
                        for (int index = 0; index < coefficient.Weights.Length; index++) sum += intermediate[(((coefficient.Start + index) * targetWidth + x) * channels) + channel] * coefficient.Weights[index];
                        destination[((y * targetWidth + x) * channels) + channel] = ClipByte(sum);
                    }
                }
            });
            return destination;
        }

        private static ParallelOptions CreateResizeParallelOptions(CancellationToken cancellationToken, int sourceHeight, int targetHeight, int targetWidth, int channels)
        {
            // Keep tiny crops deterministic and allocation-free from the caller's perspective.
            // Large image transforms are where parallel row work amortizes scheduler overhead.
            int work = Math.Max(sourceHeight, targetHeight) * Math.Max(1, targetWidth) * Math.Max(1, channels);
            return new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = work >= 262144 ? Math.Max(1, Environment.ProcessorCount) : 1
            };
        }

        private static ResampleCoefficient[] CreatePillowBilinearCoefficients(int sourceSize, int targetSize)
        {
            double scale = sourceSize / (double)targetSize;
            double filterScale = Math.Max(scale, 1.0);
            double support = filterScale;
            var result = new ResampleCoefficient[targetSize];
            for (int output = 0; output < targetSize; output++)
            {
                double center = (output + .5) * scale;
                int start = Math.Max(0, (int)(center - support + .5));
                int end = Math.Min(sourceSize, (int)(center + support + .5));
                var weights = new double[end - start];
                double total = 0;
                for (int index = 0; index < weights.Length; index++)
                {
                    double distance = Math.Abs((start + index - center + .5) / filterScale);
                    double weight = distance < 1.0 ? 1.0 - distance : 0.0;
                    weights[index] = weight; total += weight;
                }
                for (int index = 0; index < weights.Length; index++) weights[index] /= total;
                result[output] = new ResampleCoefficient(start, weights);
            }
            return result;
        }

        private static ResampleCoefficient[] CreatePillowBicubicCoefficients(int sourceSize, int targetSize)
        {
            double scale = sourceSize / (double)targetSize;
            double filterScale = Math.Max(scale, 1.0);
            double support = 2.0 * filterScale;
            var result = new ResampleCoefficient[targetSize];
            for (int output = 0; output < targetSize; output++)
            {
                double center = (output + .5) * scale;
                int start = Math.Max(0, (int)(center - support + .5));
                int end = Math.Min(sourceSize, (int)(center + support + .5));
                var weights = new double[end - start];
                double total = 0;
                for (int index = 0; index < weights.Length; index++)
                {
                    double distance = Math.Abs((start + index - center + .5) / filterScale);
                    double weight = distance < 1.0
                        ? ((1.5 * distance - 2.5) * distance * distance) + 1.0
                        : distance < 2.0 ? (((-.5 * distance + 2.5) * distance - 4.0) * distance) + 2.0 : 0.0;
                    weights[index] = weight;
                    total += weight;
                }
                for (int index = 0; index < weights.Length; index++) weights[index] /= total;
                result[output] = new ResampleCoefficient(start, weights);
            }
            return result;
        }

        private static byte ClipByte(double value)
        {
            int rounded = checked((int)Math.Floor(value + .5));
            return (byte)Math.Max(0, Math.Min(255, rounded));
        }

        private readonly struct ResampleCoefficient
        {
            internal ResampleCoefficient(int start, double[] weights) { Start = start; Weights = weights; }
            internal int Start { get; }
            internal double[] Weights { get; }
        }

        private static Scalar PaddingScalar(OpenCvRgbColor color, int channels)
        {
            if (channels == 1) return new Scalar((color.Red + color.Green + color.Blue) / 3.0);
            if (channels == 4) return new Scalar(color.Blue, color.Green, color.Red, 255);
            return new Scalar(color.Blue, color.Green, color.Red);
        }

        private static string NormalizationNote(OpenCvPreprocessOptions options)
        {
            return options.InputDivisors.Count == 0 ? string.Empty : "; inputDivisors=" + string.Join(",", options.InputDivisors);
        }

        internal static byte[] CopyRows(Mat image)
        {
            int rowBytes = checked(image.Cols * image.Channels);
            var result = new byte[checked(rowBytes * image.Rows)];
            CopyRows(image, result);
            return result;
        }

        // Copies into caller-owned scratch storage to avoid one managed allocation per OCR crop.
        // The returned byte count covers only the active rows; callers may reuse the same buffer.
        internal static int CopyRows(Mat image, byte[] destination)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int rowBytes = checked(image.Cols * image.Channels);
            int required = checked(rowBytes * image.Rows);
            if (destination.Length < required) throw new ArgumentException("The destination scratch buffer is smaller than the image.", nameof(destination));
            ulong step = image.Step.ToUInt64();
            if (step < (ulong)rowBytes) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV reported a row stride smaller than the pixel row.", technicalDetails: "step=" + step + ";rowBytes=" + rowBytes);
            if (step > int.MaxValue) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV reported an unsupported row stride.", technicalDetails: "step=" + step);
            IntPtr data = image.Data;
            if (data == IntPtr.Zero) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV returned a null image buffer.");
            for (int row = 0; row < image.Rows; row++) Marshal.Copy(IntPtr.Add(data, checked(row * (int)step)), destination, row * rowBytes, rowBytes);
            return required;
        }

        internal static byte[] ConvertChannels(byte[] source, int width, int height, int sourceChannels, OpenCvPreprocessOptions options)
        {
            int targetChannels = options.ChannelCount;
            var result = new byte[checked(width * height * targetChannels)];
            ConvertChannelsInto(source, width, height, sourceChannels, options, result, 0);
            return result;
        }

        // Performs stride-safe native row copies and channel conversion directly into one
        // contiguous destination. This avoids retaining a full-size BGR scratch image.
        internal static byte[] CopyRowsAndConvertChannels(Mat image, OpenCvPreprocessOptions options)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (options == null) throw new ArgumentNullException(nameof(options));
            int sourceRowBytes = checked(image.Cols * image.Channels);
            int destinationRowBytes = checked(image.Cols * options.ChannelCount);
            var result = new byte[checked(destinationRowBytes * image.Rows)];
            ulong step = image.Step.ToUInt64();
            if (step < (ulong)sourceRowBytes || step > int.MaxValue) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV reported an unsupported row stride.", technicalDetails: "step=" + step + ";rowBytes=" + sourceRowBytes);
            IntPtr data = image.Data;
            if (data == IntPtr.Zero) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV returned a null image buffer.");
            Action<int, byte[]> processRow = (y, row) =>
            {
                Marshal.Copy(IntPtr.Add(data, checked(y * (int)step)), row, 0, sourceRowBytes);
                ConvertChannelsInto(row, image.Cols, 1, image.Channels, options, result, y * destinationRowBytes);
            };
            int work = checked(image.Cols * image.Rows * Math.Max(1, image.Channels));
            if (work >= 262144)
            {
#if NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
                ArrayPool<byte> pool = ArrayPool<byte>.Shared;
                Parallel.For(0, image.Rows,
                    () => pool.Rent(sourceRowBytes),
                    (y, _, row) => { processRow(y, row); return row; },
                    row => pool.Return(row));
#else
                Parallel.For(0, image.Rows,
                    () => new byte[sourceRowBytes],
                    (y, _, row) => { processRow(y, row); return row; },
                    _ => { });
#endif
            }
            else
            {
                var row = new byte[sourceRowBytes];
                for (int y = 0; y < image.Rows; y++) processRow(y, row);
            }
            return result;
        }

        private static void ConvertChannelsInto(byte[] source, int width, int height, int sourceChannels, OpenCvPreprocessOptions options, byte[] result, int resultOffset)
        {
            int targetChannels = options.ChannelCount;
            int pixels = checked(width * height);
            for (int pixel = 0; pixel < pixels; pixel++)
            {
                int sourceOffset = pixel * sourceChannels;
                byte blue;
                byte green;
                byte red;
                byte alpha = 255;
                if (sourceChannels == 1) blue = green = red = source[sourceOffset];
                else
                {
                    blue = source[sourceOffset];
                    green = source[sourceOffset + 1];
                    red = source[sourceOffset + 2];
                    if (sourceChannels == 4) alpha = source[sourceOffset + 3];
                }

                if (sourceChannels == 4 && options.AlphaMode == OpenCvAlphaMode.Composite)
                {
                    blue = Composite(blue, options.AlphaBackground.Blue, alpha);
                    green = Composite(green, options.AlphaBackground.Green, alpha);
                    red = Composite(red, options.AlphaBackground.Red, alpha);
                    alpha = 255;
                }

                int targetOffset = resultOffset + (pixel * targetChannels);
                if (options.ColorOrder == VisualColorOrder.Gray)
                {
                    result[targetOffset] = checked((byte)((red * 77 + green * 150 + blue * 29 + 128) >> 8));
                }
                else if (options.ColorOrder == VisualColorOrder.Rgb || options.ColorOrder == VisualColorOrder.Rgba)
                {
                    result[targetOffset] = red;
                    result[targetOffset + 1] = green;
                    result[targetOffset + 2] = blue;
                    if (targetChannels == 4) result[targetOffset + 3] = alpha;
                }
                else
                {
                    result[targetOffset] = blue;
                    result[targetOffset + 1] = green;
                    result[targetOffset + 2] = red;
                    if (targetChannels == 4) result[targetOffset + 3] = alpha;
                }
            }
        }

        private static byte Composite(byte foreground, byte background, byte alpha) => checked((byte)(((foreground * alpha) + (background * (255 - alpha)) + 127) / 255));

        // Converts channel order, alpha, layout, and normalization directly into the final
        // tensor buffer. This fuses the former ConvertChannels -> Rearrange sequence so large
        // model inputs do not allocate or copy a second full pixel buffer.
        private static ITensor CreateTensorFromPixels(byte[] source, int width, int height, int sourceChannels, OpenCvPreprocessOptions options, CancellationToken cancellationToken)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (sourceChannels != 1 && sourceChannels != 3 && sourceChannels != 4) throw new ArgumentOutOfRangeException(nameof(sourceChannels));
            TensorShape shape = CreateShape(width, height, options);
            int channels = options.ChannelCount;
            int perImage = checked(width * height * channels);
            int batches = options.BatchSize;
            byte[]? byteOutput = options.OutputType == OpenCvOutputType.UInt8 ? new byte[checked(perImage * batches)] : null;
            float[]? floatOutput = options.OutputType == OpenCvOutputType.UInt8 ? null : new float[checked(perImage * batches)];
            FillTensorImage(source, width, height, sourceChannels, options, byteOutput, floatOutput, cancellationToken);
            CopyTensorImageToBatches(byteOutput, floatOutput, perImage, batches);
            if (byteOutput != null) return new Tensor<byte>(shape, byteOutput, TensorBufferOwnership.Transfer);
            return new Tensor<float>(shape, floatOutput!, TensorBufferOwnership.Transfer);
        }

        private static ITensor CreateTensorFromMat(Mat image, OpenCvPreprocessOptions options, CancellationToken cancellationToken)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (image.Channels != 1 && image.Channels != 3 && image.Channels != 4) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "OpenCV returned an unsupported channel count.", technicalDetails: "channels=" + image.Channels);
            int width = image.Cols;
            int height = image.Rows;
            int sourceRowBytes = checked(width * image.Channels);
            TensorShape shape = CreateShape(width, height, options);
            int channels = options.ChannelCount;
            int perImage = checked(width * height * channels);
            int batches = options.BatchSize;
            byte[]? byteOutput = options.OutputType == OpenCvOutputType.UInt8 ? new byte[checked(perImage * batches)] : null;
            float[]? floatOutput = options.OutputType == OpenCvOutputType.UInt8 ? null : new float[checked(perImage * batches)];
            var row = new byte[sourceRowBytes];
            ulong step = image.Step.ToUInt64();
            if (step < (ulong)sourceRowBytes || step > int.MaxValue) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV reported an unsupported row stride.", technicalDetails: "step=" + step + ";rowBytes=" + sourceRowBytes);
            IntPtr data = image.Data;
            if (data == IntPtr.Zero) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV returned a null image buffer.");
            bool isNchw = options.Layout == VisualTensorLayout.Nchw || options.Layout == VisualTensorLayout.Chw;
            bool isGray = options.ColorOrder == VisualColorOrder.Gray;
            bool isRgb = options.ColorOrder == VisualColorOrder.Rgb || options.ColorOrder == VisualColorOrder.Rgba;
            int plane = checked(width * height);
            float mean0 = options.Mean(0);
            float deviation0 = options.StandardDeviation(0);
            float divisor0 = options.InputDivisor(0);
            float mean1 = channels > 1 ? options.Mean(1) : 0f;
            float deviation1 = channels > 1 ? options.StandardDeviation(1) : 1f;
            float divisor1 = channels > 1 ? options.InputDivisor(1) : 1f;
            float mean2 = channels > 2 ? options.Mean(2) : 0f;
            float deviation2 = channels > 2 ? options.StandardDeviation(2) : 1f;
            float divisor2 = channels > 2 ? options.InputDivisor(2) : 1f;
            float mean3 = channels > 3 ? options.Mean(3) : 0f;
            float deviation3 = channels > 3 ? options.StandardDeviation(3) : 1f;
            float divisor3 = channels > 3 ? options.InputDivisor(3) : 1f;
            Action<int, byte[]> processRow = (y, row) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Marshal.Copy(IntPtr.Add(data, checked(y * (int)step)), row, 0, sourceRowBytes);
                for (int x = 0; x < width; x++)
                {
                    int sourceOffset = x * image.Channels;
                    byte blue;
                    byte green;
                    byte red;
                    byte alpha = 255;
                    if (image.Channels == 1) blue = green = red = row[sourceOffset];
                    else
                    {
                        blue = row[sourceOffset];
                        green = row[sourceOffset + 1];
                        red = row[sourceOffset + 2];
                        if (image.Channels == 4) alpha = row[sourceOffset + 3];
                    }
                    if (image.Channels == 4 && options.AlphaMode == OpenCvAlphaMode.Composite)
                    {
                        blue = Composite(blue, options.AlphaBackground.Blue, alpha);
                        green = Composite(green, options.AlphaBackground.Green, alpha);
                        red = Composite(red, options.AlphaBackground.Red, alpha);
                        alpha = 255;
                    }
                    int pixel = y * width + x;
                    if (isGray)
                    {
                        byte value = checked((byte)((red * 77 + green * 150 + blue * 29 + 128) >> 8));
                        if (byteOutput != null) byteOutput[pixel] = value;
                        else floatOutput![pixel] = Normalize(value, mean0, deviation0, divisor0);
                    }
                    else if (isNchw)
                    {
                        int first = pixel;
                        int second = plane + pixel;
                        int third = (plane * 2) + pixel;
                        if (byteOutput != null)
                        {
                            byteOutput[first] = isRgb ? red : blue;
                            byteOutput[second] = green;
                            byteOutput[third] = isRgb ? blue : red;
                            if (channels == 4) byteOutput[(plane * 3) + pixel] = alpha;
                        }
                        else
                        {
                            floatOutput![first] = Normalize(isRgb ? red : blue, mean0, deviation0, divisor0);
                            floatOutput[second] = Normalize(green, mean1, deviation1, divisor1);
                            floatOutput[third] = Normalize(isRgb ? blue : red, mean2, deviation2, divisor2);
                            if (channels == 4) floatOutput[(plane * 3) + pixel] = Normalize(alpha, mean3, deviation3, divisor3);
                        }
                    }
                    else
                    {
                        int destination = pixel * channels;
                        if (byteOutput != null)
                        {
                            byteOutput[destination] = isRgb ? red : blue;
                            byteOutput[destination + 1] = green;
                            byteOutput[destination + 2] = isRgb ? blue : red;
                            if (channels == 4) byteOutput[destination + 3] = alpha;
                        }
                        else
                        {
                            floatOutput![destination] = Normalize(isRgb ? red : blue, mean0, deviation0, divisor0);
                            floatOutput[destination + 1] = Normalize(green, mean1, deviation1, divisor1);
                            floatOutput[destination + 2] = Normalize(isRgb ? blue : red, mean2, deviation2, divisor2);
                            if (channels == 4) floatOutput[destination + 3] = Normalize(alpha, mean3, deviation3, divisor3);
                        }
                    }
                }
            };
            int work = checked(width * height * Math.Max(1, image.Channels));
            if (work >= 262144)
            {
#if NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
                ArrayPool<byte> pool = ArrayPool<byte>.Shared;
                Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount) },
                    () => pool.Rent(sourceRowBytes),
                    (y, _, row) => { processRow(y, row); return row; },
                    row => pool.Return(row));
#else
                Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount) },
                    () => new byte[sourceRowBytes],
                    (y, _, row) => { processRow(y, row); return row; },
                    _ => { });
#endif
            }
            else
            {
                var serialRow = new byte[sourceRowBytes];
                for (int y = 0; y < height; y++) processRow(y, serialRow);
            }
            CopyTensorImageToBatches(byteOutput, floatOutput, perImage, batches);
            if (byteOutput != null) return new Tensor<byte>(shape, byteOutput, TensorBufferOwnership.Transfer);
            return new Tensor<float>(shape, floatOutput!, TensorBufferOwnership.Transfer);
        }

        private static void FillTensorImage(byte[] source, int width, int height, int sourceChannels, OpenCvPreprocessOptions options, byte[]? byteOutput, float[]? floatOutput, CancellationToken cancellationToken)
        {
            bool isNchw = options.Layout == VisualTensorLayout.Nchw || options.Layout == VisualTensorLayout.Chw;
            bool isGray = options.ColorOrder == VisualColorOrder.Gray;
            bool isRgb = options.ColorOrder == VisualColorOrder.Rgb || options.ColorOrder == VisualColorOrder.Rgba;
            int channels = options.ChannelCount;
            int plane = checked(width * height);
            float mean0 = options.Mean(0);
            float deviation0 = options.StandardDeviation(0);
            float divisor0 = options.InputDivisor(0);
            float mean1 = channels > 1 ? options.Mean(1) : 0f;
            float deviation1 = channels > 1 ? options.StandardDeviation(1) : 1f;
            float divisor1 = channels > 1 ? options.InputDivisor(1) : 1f;
            float mean2 = channels > 2 ? options.Mean(2) : 0f;
            float deviation2 = channels > 2 ? options.StandardDeviation(2) : 1f;
            float divisor2 = channels > 2 ? options.InputDivisor(2) : 1f;
            float mean3 = channels > 3 ? options.Mean(3) : 0f;
            float deviation3 = channels > 3 ? options.StandardDeviation(3) : 1f;
            float divisor3 = channels > 3 ? options.InputDivisor(3) : 1f;
            for (int y = 0; y < height; y++)
            {
                if ((y & 31) == 0) ObserveCancellation(cancellationToken);
                for (int x = 0; x < width; x++)
                {
                    int sourceOffset = (y * width + x) * sourceChannels;
                    byte blue;
                    byte green;
                    byte red;
                    byte alpha = 255;
                    if (sourceChannels == 1) blue = green = red = source[sourceOffset];
                    else
                    {
                        blue = source[sourceOffset];
                        green = source[sourceOffset + 1];
                        red = source[sourceOffset + 2];
                        if (sourceChannels == 4) alpha = source[sourceOffset + 3];
                    }
                    if (sourceChannels == 4 && options.AlphaMode == OpenCvAlphaMode.Composite)
                    {
                        blue = Composite(blue, options.AlphaBackground.Blue, alpha);
                        green = Composite(green, options.AlphaBackground.Green, alpha);
                        red = Composite(red, options.AlphaBackground.Red, alpha);
                        alpha = 255;
                    }
                    int pixel = y * width + x;
                    if (isGray)
                    {
                        byte value = checked((byte)((red * 77 + green * 150 + blue * 29 + 128) >> 8));
                        if (byteOutput != null) byteOutput[pixel] = value;
                        else floatOutput![pixel] = Normalize(value, mean0, deviation0, divisor0);
                    }
                    else if (isNchw)
                    {
                        int first = pixel;
                        int second = plane + pixel;
                        int third = (plane * 2) + pixel;
                        if (byteOutput != null)
                        {
                            byteOutput[first] = isRgb ? red : blue;
                            byteOutput[second] = green;
                            byteOutput[third] = isRgb ? blue : red;
                            if (channels == 4) byteOutput[(plane * 3) + pixel] = alpha;
                        }
                        else
                        {
                            floatOutput![first] = Normalize(isRgb ? red : blue, mean0, deviation0, divisor0);
                            floatOutput[second] = Normalize(green, mean1, deviation1, divisor1);
                            floatOutput[third] = Normalize(isRgb ? blue : red, mean2, deviation2, divisor2);
                            if (channels == 4) floatOutput[(plane * 3) + pixel] = Normalize(alpha, mean3, deviation3, divisor3);
                        }
                    }
                    else
                    {
                        int destination = pixel * channels;
                        if (byteOutput != null)
                        {
                            byteOutput[destination] = isRgb ? red : blue;
                            byteOutput[destination + 1] = green;
                            byteOutput[destination + 2] = isRgb ? blue : red;
                            if (channels == 4) byteOutput[destination + 3] = alpha;
                        }
                        else
                        {
                            floatOutput![destination] = Normalize(isRgb ? red : blue, mean0, deviation0, divisor0);
                            floatOutput[destination + 1] = Normalize(green, mean1, deviation1, divisor1);
                            floatOutput[destination + 2] = Normalize(isRgb ? blue : red, mean2, deviation2, divisor2);
                            if (channels == 4) floatOutput[destination + 3] = Normalize(alpha, mean3, deviation3, divisor3);
                        }
                    }
                }
            }
        }

        private static void CopyTensorImageToBatches(byte[]? byteOutput, float[]? floatOutput, int perImage, int batches)
        {
            if (batches <= 1) return;
            for (int batch = 1; batch < batches; batch++)
            {
                if (byteOutput != null) Buffer.BlockCopy(byteOutput, 0, byteOutput, checked(batch * perImage), perImage);
                else Buffer.BlockCopy(floatOutput!, 0, floatOutput!, checked(batch * perImage * sizeof(float)), checked(perImage * sizeof(float)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Normalize(byte value, float mean, float standardDeviation, float divisor)
        {
            float scaled = divisor == 1f ? value : value / divisor;
            return (scaled - mean) / standardDeviation;
        }

        private static TensorShape CreateShape(int width, int height, OpenCvPreprocessOptions options)
        {
            int channels = options.ChannelCount;
            if (options.Layout == VisualTensorLayout.Nchw) return new TensorShape(options.BatchSize, channels, height, width);
            if (options.Layout == VisualTensorLayout.Nhwc) return new TensorShape(options.BatchSize, height, width, channels);
            if (options.BatchSize != 1) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Unbatched CHW/HWC layouts require batch size one.");
            return options.Layout == VisualTensorLayout.Chw ? new TensorShape(channels, height, width) : new TensorShape(height, width, channels);
        }

        internal static void ObserveCancellation(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The OpenCV image operation was cancelled at a synchronous boundary.", new OperationCanceledException(cancellationToken));
        }
    }
}
