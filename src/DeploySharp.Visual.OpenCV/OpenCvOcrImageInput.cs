using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgProc;
using CoreOperations = JYPPX.OpenCvSharp.Core.Cv2;
using ImageProcessing = JYPPX.OpenCvSharp.ImgProc.Cv2;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Decodes one image once and creates detector input plus perspective-warped recognition batches. / 单次解码一张图像，并创建检测器输入及透视变换识别批次。</summary>
    public sealed class OpenCvOcrImageInputFactory
    {
        /// <summary>Creates an owned OCR image input. The caller must dispose it. / 创建自有 OCR 图像输入；调用方必须释放它。</summary>
        public OpenCvOcrImageInput Create(OpenCvImageSource source, string detectionInputName, OpenCvPreprocessOptions detectionOptions, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (detectionOptions == null) throw new ArgumentNullException(nameof(detectionOptions));
            if (string.IsNullOrWhiteSpace(detectionInputName)) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "A detector input name is required.");
            ObserveCancellation(cancellationToken);
            OpenCvRuntimePreflight.Check();
            Mat? decoded = null;
            PreparedVisualInput? detectionInput = null;
            try
            {
                decoded = OpenCvImageLoader.Decode(source);
                OpenCvImageLoader.Validate(decoded, source);
                detectionInput = OpenCvVisualInputFactory.CreateFromDecoded(decoded, detectionInputName, detectionOptions, inputId, cancellationToken);
                var result = new OpenCvOcrImageInput(decoded, detectionInput);
                decoded = null;
                detectionInput = null;
                return result;
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "OpenCV OCR input creation was cancelled.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while creating OCR input.", exception, "sourceKind=" + source.Kind); }
            catch (DllNotFoundException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime is unavailable.", exception); }
            catch (BadImageFormatException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime architecture is incompatible.", exception); }
            catch (EntryPointNotFoundException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime ABI is incompatible.", exception); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The OCR image input could not be created.", exception); }
            finally
            {
                detectionInput?.Dispose();
                decoded?.Dispose();
            }
        }

        /// <summary>Creates an owned OCR image input from an absolute local file. / 从绝对本地文件创建自有 OCR 图像输入。</summary>
        public OpenCvOcrImageInput CreateFromFile(string path, string detectionInputName, OpenCvPreprocessOptions detectionOptions, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Create(OpenCvImageSource.FromFile(path), detectionInputName, detectionOptions, inputId, cancellationToken);
        }

        private static void ObserveCancellation(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <summary>Owns a decoded OpenCV Mat and its prepared detector tensor for one OCR source image. / 为一张 OCR 源图拥有解码 OpenCV Mat 及其已准备检测张量。</summary>
    public sealed class OpenCvOcrImageInput : IOcrImageInput
    {
        private readonly object _gate = new object();
        private readonly Mat _source;
        private bool _disposed;

        internal OpenCvOcrImageInput(Mat source, PreparedVisualInput detectionInput)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            DetectionInput = detectionInput ?? throw new ArgumentNullException(nameof(detectionInput));
            SourceSize = new VisualSize(source.Cols, source.Rows);
        }

        /// <summary>Gets source image size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets prepared detector input. / 获取已准备检测器输入。</summary>
        public PreparedVisualInput DetectionInput { get; }

        /// <summary>Creates an owned managed Float32 recognition tensor from explicit perspective crops. / 根据显式透视裁剪创建自有托管 Float32 识别张量。</summary>
        public PreparedVisualInput PrepareRecognitionBatch(string inputName, IReadOnlyList<TextCropRequest> requests, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputName)) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "A recognizer input name is required.");
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (requests.Count == 0 || requests.Count > 64) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Recognition batch must contain 1 through 64 requests.");
            lock (_gate)
            {
                EnsureUsable();
                ObserveCancellation(cancellationToken);
                TextCropProfile profile = requests[0].Profile;
                int width = requests[0].TargetWidth;
                int height = requests[0].TargetHeight;
                for (int index = 1; index < requests.Count; index++)
                {
                    if (requests[index].Profile.ProfileId != profile.ProfileId || requests[index].TargetWidth != width || requests[index].TargetHeight != height) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "A recognition batch must share crop profile and target dimensions.");
                }

                int channels = ChannelCount(profile);
                long elements = checked((long)requests.Count * channels * height * width);
                if (elements > int.MaxValue) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Recognition tensor exceeds managed array bounds.");
                var values = new float[(int)elements];
                for (int batch = 0; batch < requests.Count; batch++)
                {
                    ObserveCancellation(cancellationToken);
                    using (Mat crop = CreateCrop(requests[batch], cancellationToken))
                    {
                        byte[] native = OpenCvVisualInputFactory.CopyRows(crop);
                        WriteTensor(native, crop.Channels, values, batch, width, height, profile, cancellationToken);
                    }
                }
                TensorShape shape = profile.Layout == VisualTensorLayout.Nchw
                    ? new TensorShape(requests.Count, channels, height, width)
                    : new TensorShape(requests.Count, height, width, channels);
                var tensor = new Tensor<float>(shape, values, TensorBufferOwnership.Transfer);
                var descriptor = new VisualPreprocessingDescriptor(profile.ColorOrder, profile.Means, profile.Scales, "OpenCV 5 preview perspective warp; explicit corners and configured right-angle orientation; no automatic orientation classifier.");
                var modelSize = new VisualSize(width, height);
                return new PreparedVisualInput(inputName, tensor, modelSize, modelSize, requests.Count, profile.Layout, ImageTransform.Resize(modelSize, modelSize), descriptor, "ocr-recognition-batch");
            }
        }

        /// <inheritdoc />
        /// <remarks>Idempotently releases detector input and the retained native source Mat. / 幂等释放检测器输入和保留的 native 源 Mat。</remarks>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                DetectionInput.Dispose();
                _source.Dispose();
            }
        }

        private Mat CreateCrop(TextCropRequest request, CancellationToken cancellationToken)
        {
            TextQuadrilateral corners = request.Quadrilateral;
            int naturalWidth = Math.Max(2, checked((int)Math.Ceiling(Math.Max(Distance(corners.TopLeft, corners.TopRight), Distance(corners.BottomLeft, corners.BottomRight)))));
            int naturalHeight = Math.Max(2, checked((int)Math.Ceiling(Math.Max(Distance(corners.TopLeft, corners.BottomLeft), Distance(corners.TopRight, corners.BottomRight)))));
            if (checked((long)naturalWidth * naturalHeight) > request.Profile.MaximumCropPixels) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Perspective crop exceeds its intermediate pixel limit.");
            var sourcePoints = new[]
            {
                ToNative(corners.TopLeft), ToNative(corners.TopRight), ToNative(corners.BottomRight), ToNative(corners.BottomLeft)
            };
            var targetPoints = new[]
            {
                new Point2f(0, 0), new Point2f(naturalWidth - 1, 0), new Point2f(naturalWidth - 1, naturalHeight - 1), new Point2f(0, naturalHeight - 1)
            };
            using (Mat transform = ImageProcessing.GetPerspectiveTransform(sourcePoints, targetPoints, DecompTypes.LU))
            using (var warped = new Mat())
            {
                ObserveCancellation(cancellationToken);
                ImageProcessing.WarpPerspective(_source, warped, transform, new Size(naturalWidth, naturalHeight), ToInterpolation(request.Profile.Interpolation), BorderTypes.Constant, PaddingScalar(request.Profile.PaddingColor, _source.Channels));
                Mat? rotated = null;
                try
                {
                    Mat oriented = warped;
                    if (request.Region.Orientation != TextOrientation.Degrees0)
                    {
                        rotated = CoreOperations.Rotate(warped, ToRotation(request.Region.Orientation));
                        oriented = rotated;
                    }
                    int contentWidth = CalculateContentWidth(oriented.Cols, oriented.Rows, request.TargetHeight, request.TargetWidth);
                    using (var resized = new Mat())
                    {
                        ImageProcessing.Resize(oriented, resized, new Size(contentWidth, request.TargetHeight), interpolation: ToInterpolation(request.Profile.Interpolation));
                        var output = new Mat(request.TargetHeight, request.TargetWidth, resized.Type, PaddingScalar(request.Profile.PaddingColor, resized.Channels));
                        try
                        {
                            using (Mat destination = output.SubMat(new Rect(0, 0, contentWidth, request.TargetHeight))) resized.CopyTo(destination);
                            return output;
                        }
                        catch { output.Dispose(); throw; }
                    }
                }
                finally { rotated?.Dispose(); }
            }
        }

        private static void WriteTensor(byte[] source, int sourceChannels, float[] destination, int batch, int width, int height, TextCropProfile profile, CancellationToken cancellationToken)
        {
            if (sourceChannels != 1 && sourceChannels != 3 && sourceChannels != 4) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OCR crop has an unsupported channel count.", technicalDetails: "channels=" + sourceChannels);
            int channels = ChannelCount(profile);
            for (int y = 0; y < height; y++)
            {
                if ((y & 31) == 0) ObserveCancellation(cancellationToken);
                for (int x = 0; x < width; x++)
                {
                    int sourceOffset = ((y * width) + x) * sourceChannels;
                    byte blue;
                    byte green;
                    byte red;
                    if (sourceChannels == 1) blue = green = red = source[sourceOffset];
                    else { blue = source[sourceOffset]; green = source[sourceOffset + 1]; red = source[sourceOffset + 2]; }
                    for (int channel = 0; channel < channels; channel++)
                    {
                        byte pixel = profile.ColorOrder == VisualColorOrder.Gray
                            ? checked((byte)((red * 77 + green * 150 + blue * 29 + 128) >> 8))
                            : profile.ColorOrder == VisualColorOrder.Rgb
                                ? (channel == 0 ? red : channel == 1 ? green : blue)
                                : (channel == 0 ? blue : channel == 1 ? green : red);
                        int destinationIndex = profile.Layout == VisualTensorLayout.Nchw
                            ? checked((((batch * channels) + channel) * height + y) * width + x)
                            : checked((((batch * height) + y) * width + x) * channels + channel);
                        destination[destinationIndex] = (pixel - NormalizationValue(profile.Means, channel, 0)) * NormalizationValue(profile.Scales, channel, 1);
                    }
                }
            }
        }

        private static int CalculateContentWidth(int sourceWidth, int sourceHeight, int targetHeight, int targetWidth)
        {
            int scaled = Math.Max(1, checked((int)Math.Ceiling((double)sourceWidth * targetHeight / sourceHeight)));
            return Math.Min(targetWidth, scaled);
        }

        private static int ChannelCount(TextCropProfile profile) => profile.ColorOrder == VisualColorOrder.Gray ? 1 : 3;

        private static float NormalizationValue(IReadOnlyList<float> values, int channel, float fallback)
        {
            if (values.Count == 0) return fallback;
            return values[values.Count == 1 ? 0 : channel];
        }

        private static double Distance(PointF first, PointF second)
        {
            double x = second.X - first.X;
            double y = second.Y - first.Y;
            return Math.Sqrt((x * x) + (y * y));
        }

        private static Point2f ToNative(PointF point) => new Point2f(point.X, point.Y);

        private static InterpolationFlags ToInterpolation(TextCropInterpolation interpolation)
        {
            if (interpolation == TextCropInterpolation.Nearest) return InterpolationFlags.Nearest;
            if (interpolation == TextCropInterpolation.Cubic) return InterpolationFlags.Cubic;
            return InterpolationFlags.Linear;
        }

        private static RotateFlags ToRotation(TextOrientation orientation)
        {
            if (orientation == TextOrientation.Clockwise90) return RotateFlags.Rotate90Clockwise;
            if (orientation == TextOrientation.Degrees180) return RotateFlags.Rotate180;
            if (orientation == TextOrientation.CounterClockwise90) return RotateFlags.Rotate90Counterclockwise;
            throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "A no-op orientation must not request native rotation.");
        }

        private static Scalar PaddingScalar(TextCropColor color, int channels)
        {
            if (channels == 1) return new Scalar((color.Red + color.Green + color.Blue) / 3.0);
            if (channels == 4) return new Scalar(color.Blue, color.Green, color.Red, 255);
            return new Scalar(color.Blue, color.Green, color.Red);
        }

        private static void ObserveCancellation(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The OpenCV OCR crop operation was cancelled at a synchronous boundary.", new OperationCanceledException(cancellationToken));
        }

        private void EnsureUsable()
        {
            if (_disposed) throw new OpenCvVisualException(OpenCvErrorCodes.ObjectDisposed, "The OpenCV OCR image input has been disposed.");
        }
    }
}
