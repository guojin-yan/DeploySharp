using System;
using System.Runtime.InteropServices;
using System.Threading;
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
        public PreparedVisualInput Create(OpenCvImageSource source, string inputName, OpenCvPreprocessOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
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
                    var sourceSize = new VisualSize(decoded.Cols, decoded.Rows);
                    Mat? convertedColor = null;
                    try
                    {
                        Mat geometrySource = PrepareColorForGeometry(decoded, options, out convertedColor);
                        using (Mat geometric = ApplyGeometry(geometrySource, sourceSize, options, out ImageTransform transform))
                        {
                            ObserveCancellation(cancellationToken);
                            byte[] nativeOrder = CopyRows(geometric);
                            byte[] requestedOrder = ConvertChannels(nativeOrder, geometric.Cols, geometric.Rows, geometric.Channels, options);
                            ITensor tensor = CreateTensor(requestedOrder, geometric.Cols, geometric.Rows, options, cancellationToken);
                            var scales = new float[options.ChannelCount];
                            for (int channel = 0; channel < scales.Length; channel++) scales[channel] = 1f / options.StandardDeviation(channel);
                            var descriptor = new VisualPreprocessingDescriptor(options.ColorOrder, options.Means, scales, "OpenCV 5 preview; pixels copied to managed tensor before Mat disposal.");
                            return new PreparedVisualInput(inputName, tensor, sourceSize, options.ModelSize, options.BatchSize, options.Layout, transform, descriptor, inputId);
                        }
                    }
                    finally
                    {
                        // The temporary conversion Mat is owned only by this call. The decoded Mat has a separate using scope.
                        // 临时颜色转换 Mat 仅由本次调用拥有；解码 Mat 由外层 using 独立管理。
                        convertedColor?.Dispose();
                    }
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
        public PreparedVisualInput CreateFromFile(string path, string inputName, OpenCvPreprocessOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Create(OpenCvImageSource.FromFile(path), inputName, options, inputId, cancellationToken);
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
                ImageProcessing.Resize(source, result, new Size(options.ModelSize.Width, options.ModelSize.Height));
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
                    ImageProcessing.Resize(crop, result, new Size(options.ModelSize.Width, options.ModelSize.Height));
                    transform = ImageTransform.Crop(sourceSize, options.ModelSize, new RectangleF(cropX, cropY, cropWidth, cropHeight));
                    return result;
                }
            }

            double scale = Math.Min((double)options.ModelSize.Width / sourceSize.Width, (double)options.ModelSize.Height / sourceSize.Height);
            int resizedWidth = Math.Max(1, Math.Min(options.ModelSize.Width, (int)Math.Round(sourceSize.Width * scale)));
            int resizedHeight = Math.Max(1, Math.Min(options.ModelSize.Height, (int)Math.Round(sourceSize.Height * scale)));
            int left = (options.ModelSize.Width - resizedWidth) / 2;
            int top = (options.ModelSize.Height - resizedHeight) / 2;
            using (var resized = new Mat())
            {
                ImageProcessing.Resize(source, resized, new Size(resizedWidth, resizedHeight));
                Scalar padding = PaddingScalar(options.PaddingColor, source.Channels);
                var result = new Mat(options.ModelSize.Height, options.ModelSize.Width, source.Type, padding);
                try
                {
                    using (Mat destination = result.SubMat(new Rect(left, top, resizedWidth, resizedHeight))) resized.CopyTo(destination);
                    transform = new ImageTransform(ImageTransformKind.Letterbox, sourceSize, options.ModelSize, (float)scale, (float)scale, left, top);
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
        }

        private static Scalar PaddingScalar(OpenCvRgbColor color, int channels)
        {
            if (channels == 1) return new Scalar((color.Red + color.Green + color.Blue) / 3.0);
            if (channels == 4) return new Scalar(color.Blue, color.Green, color.Red, 255);
            return new Scalar(color.Blue, color.Green, color.Red);
        }

        private static byte[] CopyRows(Mat image)
        {
            int rowBytes = checked(image.Cols * image.Channels);
            var result = new byte[checked(rowBytes * image.Rows)];
            ulong step = image.Step.ToUInt64();
            if (step < (ulong)rowBytes) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV reported a row stride smaller than the pixel row.", technicalDetails: "step=" + step + ";rowBytes=" + rowBytes);
            if (step > int.MaxValue) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV reported an unsupported row stride.", technicalDetails: "step=" + step);
            IntPtr data = image.Data;
            if (data == IntPtr.Zero) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV returned a null image buffer.");
            for (int row = 0; row < image.Rows; row++) Marshal.Copy(IntPtr.Add(data, checked(row * (int)step)), result, row * rowBytes, rowBytes);
            return result;
        }

        private static byte[] ConvertChannels(byte[] source, int width, int height, int sourceChannels, OpenCvPreprocessOptions options)
        {
            int targetChannels = options.ChannelCount;
            var result = new byte[checked(width * height * targetChannels)];
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

                int targetOffset = pixel * targetChannels;
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
            return result;
        }

        private static byte Composite(byte foreground, byte background, byte alpha) => checked((byte)(((foreground * alpha) + (background * (255 - alpha)) + 127) / 255));

        private static ITensor CreateTensor(byte[] pixels, int width, int height, OpenCvPreprocessOptions options, CancellationToken cancellationToken)
        {
            TensorShape shape = CreateShape(width, height, options);
            int channels = options.ChannelCount;
            int perImage = checked(width * height * channels);
            if (options.OutputType == OpenCvOutputType.UInt8)
            {
                var output = new byte[checked(perImage * options.BatchSize)];
                Rearrange(pixels, output, width, height, channels, options, cancellationToken, false);
                return new Tensor<byte>(shape, output, TensorBufferOwnership.Transfer);
            }

            var floats = new float[checked(perImage * options.BatchSize)];
            Rearrange(pixels, floats, width, height, channels, options, cancellationToken);
            return new Tensor<float>(shape, floats, TensorBufferOwnership.Transfer);
        }

        private static TensorShape CreateShape(int width, int height, OpenCvPreprocessOptions options)
        {
            int channels = options.ChannelCount;
            if (options.Layout == VisualTensorLayout.Nchw) return new TensorShape(options.BatchSize, channels, height, width);
            if (options.Layout == VisualTensorLayout.Nhwc) return new TensorShape(options.BatchSize, height, width, channels);
            if (options.BatchSize != 1) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Unbatched CHW/HWC layouts require batch size one.");
            return options.Layout == VisualTensorLayout.Chw ? new TensorShape(channels, height, width) : new TensorShape(height, width, channels);
        }

        private static void Rearrange(byte[] source, float[] destination, int width, int height, int channels, OpenCvPreprocessOptions options, CancellationToken cancellationToken)
        {
            for (int batch = 0; batch < options.BatchSize; batch++)
            {
                for (int y = 0; y < height; y++)
                {
                    if ((y & 31) == 0) ObserveCancellation(cancellationToken);
                    for (int x = 0; x < width; x++)
                    {
                        for (int channel = 0; channel < channels; channel++)
                        {
                            int sourceIndex = ((y * width + x) * channels) + channel;
                            int destinationIndex = DestinationIndex(batch, y, x, channel, width, height, channels, options);
                            destination[destinationIndex] = (source[sourceIndex] - options.Mean(channel)) / options.StandardDeviation(channel);
                        }
                    }
                }
            }
        }

        private static void Rearrange(byte[] source, byte[] destination, int width, int height, int channels, OpenCvPreprocessOptions options, CancellationToken cancellationToken, bool unused)
        {
            for (int batch = 0; batch < options.BatchSize; batch++)
            {
                for (int y = 0; y < height; y++)
                {
                    if ((y & 31) == 0) ObserveCancellation(cancellationToken);
                    for (int x = 0; x < width; x++)
                    {
                        for (int channel = 0; channel < channels; channel++)
                        {
                            int sourceIndex = ((y * width + x) * channels) + channel;
                            destination[DestinationIndex(batch, y, x, channel, width, height, channels, options)] = source[sourceIndex];
                        }
                    }
                }
            }
        }

        private static int DestinationIndex(int batch, int y, int x, int channel, int width, int height, int channels, OpenCvPreprocessOptions options)
        {
            if (options.Layout == VisualTensorLayout.Nchw || options.Layout == VisualTensorLayout.Chw) return (((batch * channels + channel) * height + y) * width) + x;
            return (((batch * height + y) * width + x) * channels) + channel;
        }

        private static void ObserveCancellation(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The OpenCV image operation was cancelled at a synchronous boundary.", new OperationCanceledException(cancellationToken));
        }
    }
}
