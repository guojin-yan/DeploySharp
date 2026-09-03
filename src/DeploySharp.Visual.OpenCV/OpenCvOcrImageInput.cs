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
            if (cancellationToken.IsCancellationRequested) throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "OpenCV OCR input creation was cancelled.", new OperationCanceledException(cancellationToken));
            OpenCvRuntimePreflight.Check();
            Mat? decoded = null;
            PreparedVisualInput? detectionInput = null;
            try
            {
                decoded = OpenCvImageLoader.Decode(source);
                OpenCvImageLoader.Validate(decoded, source);
                detectionInput = OpenCvVisualInputFactory.CreateFromDecoded(decoded, detectionInputName, detectionOptions, inputId, cancellationToken);
                var result = new OpenCvOcrImageInput(decoded, detectionInput, detectionInputName, detectionOptions, inputId);
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

        /// <summary>Creates a single-decode input for orientation classification and later OCR correction. / 创建单次解码的方向分类与后续 OCR 纠正输入。</summary>
        public OpenCvOcrImageInput CreateOrientationInput(OpenCvImageSource source, string orientationInputName, OpenCvPreprocessOptions orientationOptions, string detectionInputName, OpenCvPreprocessOptions detectionOptions, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (orientationOptions == null) throw new ArgumentNullException(nameof(orientationOptions));
            if (detectionOptions == null) throw new ArgumentNullException(nameof(detectionOptions));
            if (string.IsNullOrWhiteSpace(orientationInputName) || string.IsNullOrWhiteSpace(detectionInputName)) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Orientation and detector input names are required.");
            if (cancellationToken.IsCancellationRequested) throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "OpenCV orientation input creation was cancelled.", new OperationCanceledException(cancellationToken));
            OpenCvRuntimePreflight.Check();
            Mat? decoded = null;
            PreparedVisualInput? orientationInput = null;
            try
            {
                decoded = OpenCvImageLoader.Decode(source);
                OpenCvImageLoader.Validate(decoded, source);
                orientationInput = OpenCvVisualInputFactory.CreateFromDecoded(decoded, orientationInputName, orientationOptions, inputId, cancellationToken);
                var result = new OpenCvOcrImageInput(decoded, orientationInput, detectionInputName, detectionOptions, inputId);
                decoded = null;
                orientationInput = null;
                return result;
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "OpenCV orientation input creation was cancelled.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while creating the orientation input.", exception, "sourceKind=" + source.Kind); }
            catch (DllNotFoundException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime is unavailable.", exception); }
            catch (BadImageFormatException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime architecture is incompatible.", exception); }
            catch (EntryPointNotFoundException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime ABI is incompatible.", exception); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The OpenCV orientation input could not be created.", exception); }
            finally { orientationInput?.Dispose(); decoded?.Dispose(); }
        }

        private static void ObserveCancellation(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <summary>Owns a decoded OpenCV Mat and its prepared detector tensor for one OCR source image. / 为一张 OCR 源图拥有解码 OpenCV Mat 及其已准备检测张量。</summary>
    public sealed class OpenCvOcrImageInput : IOcrOrientationImageInput
    {
        // Recognition batches only read the decoded source Mat. A reader/writer lock
        // lets independent batches warp and normalize crops concurrently while keeping
        // orientation transfer and disposal exclusive.
        private readonly ReaderWriterLockSlim _gate = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private Mat? _source;
        private readonly string _correctedInputName;
        private readonly OpenCvPreprocessOptions _correctedInputOptions;
        private readonly string? _inputId;
        // Perspective point buffers are thread-local because recognition batches may
        // now run concurrently under the read lock. Each worker reuses its four-point
        // arrays instead of allocating them for every crop.
        private readonly ThreadLocal<Point2f[]> _sourcePoints = new ThreadLocal<Point2f[]>(() => new Point2f[4]);
        private readonly ThreadLocal<Point2f[]> _targetPoints = new ThreadLocal<Point2f[]>(() => new Point2f[4]);
        // Warp and resize destinations are scratch-only and never escape the worker.
        // Keeping one pair per worker avoids native Mat allocation/release for every
        // detected text region while preserving isolation between concurrent batches.
        private readonly ThreadLocal<CropScratch> _cropScratch = new ThreadLocal<CropScratch>(() => new CropScratch(), trackAllValues: true);
        // Recognition tensors are exact-sized because Core tensors expose their backing
        // arrays directly to backends. Keep a small bounded pool per OCR image so repeated
        // document calls do not allocate several megabytes for every cls/rec batch.
        private readonly ExactFloatArrayPool _recognitionTensorPool = new ExactFloatArrayPool(32 * 1024 * 1024, 8);
        private bool _disposed;

        internal OpenCvOcrImageInput(Mat source, PreparedVisualInput detectionInput, string correctedInputName, OpenCvPreprocessOptions correctedInputOptions, string? inputId)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            DetectionInput = detectionInput ?? throw new ArgumentNullException(nameof(detectionInput));
            _correctedInputName = string.IsNullOrWhiteSpace(correctedInputName) ? throw new ArgumentException("A corrected input name is required.", nameof(correctedInputName)) : correctedInputName;
            _correctedInputOptions = correctedInputOptions ?? throw new ArgumentNullException(nameof(correctedInputOptions));
            _inputId = inputId;
            SourceSize = new VisualSize(source.Cols, source.Rows);
        }

        /// <summary>Gets source image size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets prepared detector input. / 获取已准备检测器输入。</summary>
        public PreparedVisualInput DetectionInput { get; }

        /// <summary>Creates a new owned OCR input after one native right-angle rotation; zero degrees transfers the decoded Mat without a pixel copy. / 一次 native 直角旋转后创建新的自有 OCR 输入；零度直接转移已解码 Mat，不复制像素。</summary>
        public IOcrImageInput CreateOriented(OcrOrientationResult orientation, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (orientation == null) throw new ArgumentNullException(nameof(orientation));
            _gate.EnterWriteLock();
            try
            {
                EnsureUsable();
                ObserveCancellation(cancellationToken);
                if (orientation.Rejected) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "A rejected orientation result cannot be silently treated as zero degrees.");
                if (orientation.InputSize != SourceSize) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The orientation result belongs to a different source image size.", technicalDetails: "orientation=" + orientation.InputSize + ";source=" + SourceSize);
                Mat source = _source ?? throw new OpenCvVisualException(OpenCvErrorCodes.ObjectDisposed, "The decoded source Mat has already been transferred.");
                Mat? corrected = null;
                bool transferred = false;
                PreparedVisualInput? detection = null;
                try
                {
                    if (orientation.Orientation == TextOrientation.Degrees0)
                    {
                        corrected = source;
                        _source = null;
                        transferred = true;
                    }
                    else corrected = CoreOperations.Rotate(source, ToRotation(orientation.Orientation));
                    detection = OpenCvVisualInputFactory.CreateFromDecoded(corrected, _correctedInputName, _correctedInputOptions, _inputId, cancellationToken);
                    var result = new OpenCvOcrImageInput(corrected, detection, _correctedInputName, _correctedInputOptions, _inputId);
                    corrected = null;
                    detection = null;
                    return result;
                }
                catch (OpenCvVisualException) { throw; }
                catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while rotating the OCR source.", exception, "orientation=" + orientation.Orientation); }
                catch (DllNotFoundException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime is unavailable.", exception); }
                catch (BadImageFormatException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime architecture is incompatible.", exception); }
                catch (EntryPointNotFoundException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime ABI is incompatible.", exception); }
                finally
                {
                    detection?.Dispose();
                    if (corrected != null) { if (transferred) _source = corrected; else corrected.Dispose(); }
                }
            }
            finally { _gate.ExitWriteLock(); }
        }

        /// <summary>Creates an owned managed Float32 recognition tensor from explicit perspective crops. / 根据显式透视裁剪创建自有托管 Float32 识别张量。</summary>
        public PreparedVisualInput PrepareRecognitionBatch(string inputName, IReadOnlyList<TextCropRequest> requests, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputName)) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "A recognizer input name is required.");
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (requests.Count == 0 || requests.Count > 64) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Recognition batch must contain 1 through 64 requests.");
            _gate.EnterReadLock();
            try
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
                ExactFloatArrayPool.Lease? tensorLease = _recognitionTensorPool.Rent((int)elements);
                try
                {
                    float[] values = tensorLease.Buffer;
                    Mat source = _source ?? throw new OpenCvVisualException(OpenCvErrorCodes.ObjectDisposed, "The decoded source Mat is no longer available.");
                    // Crop pixels are read directly from native Mat rows. WriteTensor initializes
                    // only the trailing padding, avoiding a full tensor fill that would immediately
                    // be overwritten by valid content.
                    for (int batch = 0; batch < requests.Count; batch++)
                    {
                        ObserveCancellation(cancellationToken);
                        Mat crop = PrepareCropContent(requests[batch], cancellationToken);
                        WriteTensor(crop, values, batch, width, height, profile, cancellationToken);
                    }
                    TensorShape shape = profile.Layout == VisualTensorLayout.Nchw
                        ? new TensorShape(requests.Count, channels, height, width)
                        : new TensorShape(requests.Count, height, width, channels);
                    var tensor = new Tensor<float>(shape, values, TensorBufferOwnership.Borrow);
                    var descriptor = new VisualPreprocessingDescriptor(profile.ColorOrder, profile.Means, profile.Scales, "OpenCV 5 preview perspective warp; explicit corners and configured right-angle orientation; no automatic orientation classifier.");
                    var modelSize = new VisualSize(width, height);
                    var prepared = new PreparedVisualInput(inputName, tensor, modelSize, modelSize, requests.Count, profile.Layout, ImageTransform.Resize(modelSize, modelSize), descriptor, "ocr-recognition-batch", PreparedInputOwnership.Owned, tensorLease);
                    tensorLease = null;
                    return prepared;
                }
                finally { tensorLease?.Dispose(); }
            }
            finally { _gate.ExitReadLock(); }
        }

        /// <inheritdoc />
        /// <remarks>Idempotently releases detector input and the retained native source Mat. / 幂等释放检测器输入和保留的 native 源 Mat。</remarks>
        public void Dispose()
        {
            _gate.EnterWriteLock();
            try
            {
                if (_disposed) return;
                _disposed = true;
                DetectionInput.Dispose();
                _source?.Dispose();
                _source = null;
            }
            finally { _gate.ExitWriteLock(); }
            _sourcePoints.Dispose();
            _targetPoints.Dispose();
            foreach (CropScratch scratch in _cropScratch.Values) scratch.Dispose();
            _cropScratch.Dispose();
            _recognitionTensorPool.Dispose();
        }

        private Mat PrepareCropContent(TextCropRequest request, CancellationToken cancellationToken)
        {
            TextQuadrilateral corners = request.Quadrilateral;
            int naturalWidth = Math.Max(2, checked((int)Math.Ceiling(Math.Max(Distance(corners.TopLeft, corners.TopRight), Distance(corners.BottomLeft, corners.BottomRight)))));
            int naturalHeight = Math.Max(2, checked((int)Math.Ceiling(Math.Max(Distance(corners.TopLeft, corners.BottomLeft), Distance(corners.TopRight, corners.BottomRight)))));
            if (checked((long)naturalWidth * naturalHeight) > request.Profile.MaximumCropPixels) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Perspective crop exceeds its intermediate pixel limit.");
            Point2f[] sourcePoints = _sourcePoints.Value!;
            Point2f[] targetPoints = _targetPoints.Value!;
            sourcePoints[0] = ToNative(corners.TopLeft);
            sourcePoints[1] = ToNative(corners.TopRight);
            sourcePoints[2] = ToNative(corners.BottomRight);
            sourcePoints[3] = ToNative(corners.BottomLeft);
            targetPoints[0] = new Point2f(0, 0);
            targetPoints[1] = new Point2f(naturalWidth - 1, 0);
            targetPoints[2] = new Point2f(naturalWidth - 1, naturalHeight - 1);
            targetPoints[3] = new Point2f(0, naturalHeight - 1);
            using (Mat transform = ImageProcessing.GetPerspectiveTransform(sourcePoints, targetPoints, DecompTypes.LU))
            {
                ObserveCancellation(cancellationToken);
                Mat source = _source ?? throw new OpenCvVisualException(OpenCvErrorCodes.ObjectDisposed, "The decoded source Mat is no longer available.");
                CropScratch scratch = _cropScratch.Value!;
                ImageProcessing.WarpPerspective(source, scratch.Warped, transform, new Size(naturalWidth, naturalHeight), ToInterpolation(request.Profile.Interpolation), BorderTypes.Constant, PaddingScalar(request.Profile.PaddingColor, source.Channels));
                Mat oriented = scratch.Warped;
                if (request.Region.Orientation != TextOrientation.Degrees0)
                {
                    CoreOperations.Rotate(scratch.Warped, scratch.Rotated, ToRotation(request.Region.Orientation));
                    oriented = scratch.Rotated;
                }
                int contentWidth = CalculateContentWidth(oriented.Cols, oriented.Rows, request.TargetHeight, request.TargetWidth);
                ImageProcessing.Resize(oriented, scratch.Resized, new Size(contentWidth, request.TargetHeight), interpolation: ToInterpolation(request.Profile.Interpolation));
                return scratch.Resized;
            }
        }

        private sealed class CropScratch : IDisposable
        {
            internal readonly Mat Warped = new Mat();
            internal readonly Mat Rotated = new Mat();
            internal readonly Mat Resized = new Mat();

            public void Dispose()
            {
                Warped.Dispose();
                Rotated.Dispose();
                Resized.Dispose();
            }
        }

        private sealed class ExactFloatArrayPool : IDisposable
        {
            private readonly object _gate = new object();
            private readonly Dictionary<int, Stack<float[]>> _buffers = new Dictionary<int, Stack<float[]>>();
            private readonly int _maximumRetainedBytes;
            private readonly int _maximumPerLength;
            private int _retainedBytes;
            private bool _disposed;

            internal ExactFloatArrayPool(int maximumRetainedBytes, int maximumPerLength)
            {
                if (maximumRetainedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRetainedBytes));
                if (maximumPerLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPerLength));
                _maximumRetainedBytes = maximumRetainedBytes;
                _maximumPerLength = maximumPerLength;
            }

            internal Lease Rent(int length)
            {
                if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
                float[]? buffer = null;
                lock (_gate)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(ExactFloatArrayPool));
                    if (_buffers.TryGetValue(length, out Stack<float[]>? available) && available.Count != 0)
                    {
                        buffer = available.Pop();
                        _retainedBytes -= checked(length * sizeof(float));
                        if (available.Count == 0) _buffers.Remove(length);
                    }
                }
                return new Lease(this, buffer ?? new float[length]);
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _disposed = true;
                    _buffers.Clear();
                    _retainedBytes = 0;
                }
            }

            private void Return(float[] buffer)
            {
                int bytes = checked(buffer.Length * sizeof(float));
                lock (_gate)
                {
                    if (_disposed || bytes > _maximumRetainedBytes || _retainedBytes > _maximumRetainedBytes - bytes) return;
                    if (!_buffers.TryGetValue(buffer.Length, out Stack<float[]>? available))
                    {
                        available = new Stack<float[]>();
                        _buffers.Add(buffer.Length, available);
                    }
                    if (available.Count >= _maximumPerLength) return;
                    available.Push(buffer);
                    _retainedBytes += bytes;
                }
            }

            internal sealed class Lease : IDisposable
            {
                private ExactFloatArrayPool? _owner;
                private float[]? _buffer;

                internal Lease(ExactFloatArrayPool owner, float[] buffer)
                {
                    _owner = owner;
                    _buffer = buffer;
                }

                internal float[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(Lease));

                public void Dispose()
                {
                    ExactFloatArrayPool? owner = Interlocked.Exchange(ref _owner, null);
                    float[]? buffer = Interlocked.Exchange(ref _buffer, null);
                    if (owner != null && buffer != null) owner.Return(buffer);
                }
            }
        }

        private static unsafe void WriteTensor(Mat source, float[] destination, int batch, int width, int height, TextCropProfile profile, CancellationToken cancellationToken)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int sourceChannels = source.Channels;
            int contentWidth = source.Cols;
            if (sourceChannels != 1 && sourceChannels != 3 && sourceChannels != 4) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OCR crop has an unsupported channel count.", technicalDetails: "channels=" + sourceChannels);
            if (contentWidth <= 0 || contentWidth > width) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OCR crop content width is outside the target tensor bounds.", technicalDetails: "contentWidth=" + contentWidth + ";targetWidth=" + width);
            if (source.Rows != height) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OCR crop height does not match the target tensor.", technicalDetails: "cropHeight=" + source.Rows + ";targetHeight=" + height);
            int rowBytes = checked(contentWidth * sourceChannels);
            ulong nativeStep = source.Step.ToUInt64();
            if (nativeStep < (ulong)rowBytes || nativeStep > int.MaxValue) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV reported an unsupported OCR crop row stride.", technicalDetails: "step=" + nativeStep + ";rowBytes=" + rowBytes);
            IntPtr data = source.Data;
            if (data == IntPtr.Zero) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV returned a null OCR crop buffer.");
            int sourceStep = (int)nativeStep;
            byte* sourceBase = (byte*)data.ToPointer();
            int channels = ChannelCount(profile);
            bool isNchw = profile.Layout == VisualTensorLayout.Nchw;
            bool isGray = profile.ColorOrder == VisualColorOrder.Gray;
            bool isRgb = profile.ColorOrder == VisualColorOrder.Rgb;
            int plane = checked(width * height);
            int batchOffset = checked(batch * channels * plane);
            float mean0 = NormalizationValue(profile.Means, 0, 0);
            float mean1 = channels > 1 ? NormalizationValue(profile.Means, 1, 0) : 0;
            float mean2 = channels > 2 ? NormalizationValue(profile.Means, 2, 0) : 0;
            float scale0 = NormalizationValue(profile.Scales, 0, 1);
            float scale1 = channels > 1 ? NormalizationValue(profile.Scales, 1, 1) : 1;
            float scale2 = channels > 2 ? NormalizationValue(profile.Scales, 2, 1) : 1;
            byte paddingRed = profile.PaddingColor.Red;
            byte paddingGreen = profile.PaddingColor.Green;
            byte paddingBlue = profile.PaddingColor.Blue;
            byte paddingGray = checked((byte)((paddingRed * 77 + paddingGreen * 150 + paddingBlue * 29 + 128) >> 8));
            float padding0 = ((isGray ? paddingGray : isRgb ? paddingRed : paddingBlue) - mean0) * scale0;
            float padding1 = channels > 1 ? (paddingGreen - mean1) * scale1 : 0;
            float padding2 = channels > 2 ? ((isRgb ? paddingBlue : paddingRed) - mean2) * scale2 : 0;
            int paddingWidth = width - contentWidth;
            for (int y = 0; y < height; y++)
            {
                if ((y & 31) == 0) ObserveCancellation(cancellationToken);
                byte* sourceRow = sourceBase + checked(y * sourceStep);
                for (int x = 0; x < contentWidth; x++)
                {
                    int sourceOffset = x * sourceChannels;
                    byte blue;
                    byte green;
                    byte red;
                    if (sourceChannels == 1) blue = green = red = sourceRow[sourceOffset];
                    else { blue = sourceRow[sourceOffset]; green = sourceRow[sourceOffset + 1]; red = sourceRow[sourceOffset + 2]; }
                    int pixel = y * width + x;
                    if (isGray)
                    {
                        byte gray = checked((byte)((red * 77 + green * 150 + blue * 29 + 128) >> 8));
                        int index = batchOffset + pixel;
                        destination[index] = (gray - mean0) * scale0;
                    }
                    else if (isNchw)
                    {
                        int index = batchOffset + pixel;
                        byte first = isRgb ? red : blue;
                        byte second = green;
                        byte third = isRgb ? blue : red;
                        destination[index] = (first - mean0) * scale0;
                        destination[index + plane] = (second - mean1) * scale1;
                        destination[index + (plane * 2)] = (third - mean2) * scale2;
                    }
                    else
                    {
                        int index = batchOffset + (pixel * channels);
                        byte first = isRgb ? red : blue;
                        byte second = green;
                        byte third = isRgb ? blue : red;
                        destination[index] = (first - mean0) * scale0;
                        destination[index + 1] = (second - mean1) * scale1;
                        destination[index + 2] = (third - mean2) * scale2;
                    }
                }
                if (paddingWidth == 0) continue;
                if (isNchw)
                {
                    int paddingStart = batchOffset + (y * width) + contentWidth;
                    Fill(destination, padding0, paddingStart, paddingWidth);
                    if (channels > 1) Fill(destination, padding1, paddingStart + plane, paddingWidth);
                    if (channels > 2) Fill(destination, padding2, paddingStart + (plane * 2), paddingWidth);
                }
                else
                {
                    int paddingStart = batchOffset + (((y * width) + contentWidth) * channels);
                    for (int x = 0; x < paddingWidth; x++)
                    {
                        int index = paddingStart + (x * channels);
                        destination[index] = padding0;
                        if (channels > 1) destination[index + 1] = padding1;
                        if (channels > 2) destination[index + 2] = padding2;
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

        private static void Fill(float[] values, float value, int startIndex, int count)
        {
#if NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
            Array.Fill(values, value, startIndex, count);
#else
            int end = checked(startIndex + count);
            for (int index = startIndex; index < end; index++) values[index] = value;
#endif
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
