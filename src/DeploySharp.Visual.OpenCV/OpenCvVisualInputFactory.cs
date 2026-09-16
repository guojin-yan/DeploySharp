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
using CoreOperations = JYPPX.OpenCvSharp.Core.Cv2;
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

        /// <summary>Creates a prepared tensor from one axis-aligned source ROI. The image is decoded once and the ROI is exposed as an OpenCV SubMat view. / 根据一个源图轴对齐 ROI 创建张量；图像只解码一次，ROI 使用 OpenCV SubMat 视图。</summary>
        /// <remarks>Rotated and polygon ROI input requires an explicit affine or perspective contract and is intentionally not approximated by this method. / 旋转和多边形 ROI 需要显式仿射或透视合同，本方法不会静默近似为外接矩形。</remarks>
        public PreparedVisualInput CreateRectangleRoi(OpenCvImageSource source, RectangleF crop, string inputName, OpenCvPreprocessOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(inputName)) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "An input tensor name is required.");
            ObserveCancellation(cancellationToken);
            OpenCvRuntimePreflight.Check();
            try
            {
                using (Mat decoded = OpenCvImageLoader.Decode(source))
                {
                    OpenCvImageLoader.Validate(decoded, source);
                    return CreateRectangleRoiFromDecoded(decoded, crop, inputName, options, inputId, cancellationToken, auxiliaryInputs);
                }
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The OpenCV ROI operation was cancelled.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while preparing the ROI tensor.", exception); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The ROI tensor could not be prepared.", exception); }
        }

 #if !DEPLOYSHARP_LEGACY_NO_ROI
        /// <summary>Creates a prepared tensor from a source-space rectangle, polygon, or binary-mask ROI. / 根据源图空间矩形、多边形或二值 Mask ROI 创建已准备张量。</summary>
        /// <remarks>Polygon and mask pixels outside the ROI are filled with zero before model preprocessing. Rotated rectangles require the perspective-aware input path and are rejected here instead of being approximated by their bounds. / 多边形和 Mask ROI 外的像素会在模型预处理前填零；旋转矩形需要支持透视的输入路径，本方法会拒绝该类型而不是以外接框近似。</remarks>
        public PreparedVisualInput CreateRoi(OpenCvImageSource source, IVisualRoiGeometry geometry, string inputName, OpenCvPreprocessOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            return CreateRoiCore(source, geometry, inputName, options, inputId, cancellationToken, auxiliaryInputs, false);
        }

        private PreparedVisualInput CreateRoiCore(OpenCvImageSource source, IVisualRoiGeometry geometry, string inputName, OpenCvPreprocessOptions options, string? inputId, CancellationToken cancellationToken, IEnumerable<NamedTensor>? auxiliaryInputs, bool usePerspective)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(inputName)) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "An input tensor name is required.");
            ObserveCancellation(cancellationToken);
            OpenCvRuntimePreflight.Check();
            try
            {
                using (Mat decoded = OpenCvImageLoader.Decode(source))
                {
                    OpenCvImageLoader.Validate(decoded, source);
                    return CreateRoiFromDecoded(decoded, geometry, inputName, options, inputId, cancellationToken, auxiliaryInputs, usePerspective);
                }
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The OpenCV ROI operation was cancelled.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while preparing the ROI tensor.", exception, "geometry=" + geometry.Kind); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The ROI tensor could not be prepared.", exception, "geometry=" + geometry.Kind); }
        }

        /// <summary>Creates a prepared tensor by perspective-warping four ordered source corners into a rectangular model input. / 将四个有序源图角点透视变换为矩形模型输入并创建张量。</summary>
        /// <remarks>The corner order must be clockwise or counter-clockwise without crossing edges. The returned transform is projective and preserves the exact source-to-model mapping for point, box, OBB, pose, and OCR projection. / 角点必须按顺时针或逆时针排列且边不相交；返回的变换为单应变换，可为点、框、OBB、Pose 和 OCR 投影保留精确源图到模型映射。</remarks>
        public PreparedVisualInput CreateQuadrilateralRoi(OpenCvImageSource source, IReadOnlyList<PointF> quadrilateral, string inputName, OpenCvPreprocessOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (quadrilateral == null) throw new ArgumentNullException(nameof(quadrilateral));
            if (quadrilateral.Count != 4) throw new ArgumentException("A quadrilateral ROI requires exactly four ordered points.", nameof(quadrilateral));
            return CreateRoiCore(source, new PolygonRoiGeometry(quadrilateral), inputName, options, inputId, cancellationToken, auxiliaryInputs, true);
        }

        /// <summary>Creates a prepared tensor from a rotated rectangle ROI using an exact perspective warp. / 使用精确透视变换根据旋转矩形 ROI 创建张量。</summary>
        public PreparedVisualInput CreateRotatedRectangleRoi(OpenCvImageSource source, RotatedRectangleRoiGeometry geometry, string inputName, OpenCvPreprocessOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            return CreateRoiCore(source, geometry, inputName, options, inputId, cancellationToken, auxiliaryInputs, true);
        }

        /// <summary>Creates one true NCHW or NHWC batch from multiple ROIs after decoding the source image once. / 在源图只解码一次后，将多个 ROI 直接打包为真正的 NCHW 或 NHWC Batch。</summary>
        /// <remarks>Every ROI is prepared independently so its geometry remains reversible in <see cref="PreparedVisualInput.BatchFrames"/>. The options batch size is ignored and the number of geometries determines the resulting batch dimension. CHW/HWC unbatched layouts are rejected. / 每个 ROI 独立准备，因此其几何会在 <see cref="PreparedVisualInput.BatchFrames"/> 中保持可逆；options 的 BatchSize 被忽略，由 geometry 数量决定 Batch 维度；CHW/HWC 无 Batch 布局会被拒绝。</remarks>
        public PreparedVisualInput CreateRoiBatch(OpenCvImageSource source, IReadOnlyList<IVisualRoiGeometry> geometries, string inputName, OpenCvPreprocessOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (geometries == null) throw new ArgumentNullException(nameof(geometries));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (geometries.Count == 0) throw new ArgumentException("At least one ROI geometry is required.", nameof(geometries));
            if (geometries.Count > 4096) throw new ArgumentOutOfRangeException(nameof(geometries));
            if (options.Layout != VisualTensorLayout.Nchw && options.Layout != VisualTensorLayout.Nhwc) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "ROI batches require NCHW or NHWC layout.");
            for (int index = 0; index < geometries.Count; index++) if (geometries[index] == null) throw new ArgumentException("ROI geometries cannot contain null values.", nameof(geometries));
            ObserveCancellation(cancellationToken);
            OpenCvRuntimePreflight.Check();
            try
            {
                using (Mat decoded = OpenCvImageLoader.Decode(source))
                {
                    OpenCvImageLoader.Validate(decoded, source);
                    return CreateRoiBatchFromDecodedPublic(decoded, geometries, inputName, options, inputId, cancellationToken, auxiliaryInputs);
                }
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The OpenCV ROI batch operation was cancelled.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while preparing the ROI batch.", exception); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The ROI batch could not be prepared.", exception); }
        }

 #endif

 #if !DEPLOYSHARP_LEGACY_NO_ROI

        /// <summary>Decodes a source once for repeated or concurrent ROI preparation. / 将源图解码一次，用于重复或并发 ROI 准备。</summary>
        public OpenCvDecodedRoiImage DecodeForRois(OpenCvImageSource source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ObserveCancellation(cancellationToken);
            OpenCvRuntimePreflight.Check();
            Mat? decoded = null;
            try
            {
                decoded = OpenCvImageLoader.Decode(source);
                OpenCvImageLoader.Validate(decoded, source);
                var result = new OpenCvDecodedRoiImage(decoded, source.Sha256);
                decoded = null;
                return result;
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The OpenCV ROI image decode was cancelled.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.DecodeFailed, "OpenCV failed to decode the ROI source image.", exception); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.DecodeFailed, "The ROI source image could not be decoded.", exception); }
            finally { decoded?.Dispose(); }
        }

 #endif

        /// <summary>Creates a prepared input from the shared backend-neutral preprocessing contract. / 根据共享的后端无关预处理合同创建已准备输入。</summary>
        public PreparedVisualInput Create(OpenCvImageSource source, string inputName, VisualPreprocessingOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            return Create(source, inputName, options.ToOpenCvOptions(), inputId, cancellationToken, auxiliaryInputs);
        }

        /// <summary>Creates a prepared input from a local file and the shared backend-neutral preprocessing contract. / 根据本地文件和共享的后端无关预处理合同创建已准备输入。</summary>
        public PreparedVisualInput CreateFromFile(string path, string inputName, VisualPreprocessingOptions options, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            return Create(OpenCvImageSource.FromFile(path), inputName, options, inputId, cancellationToken, auxiliaryInputs);
        }

        /// <summary>Creates an input directly from a profile's preprocessing contract. / 直接根据 Profile 的预处理合同创建输入。</summary>
        public PreparedVisualInput Create(OpenCvImageSource source, VisualModelProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            VisualPreprocessingOptions effective = profile.Preprocessing ?? throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The visual profile does not declare common image preprocessing; use WithPreprocessing or its specialized input factory.", technicalDetails: "profileId=" + profile.ProfileId);
            return Create(source, profile.Input.Name, effective, inputId, cancellationToken, auxiliaryInputs);
        }

        /// <summary>Creates an input from a local file and a profile's preprocessing contract. / 根据本地文件及 Profile 的预处理合同创建输入。</summary>
        public PreparedVisualInput CreateFromFile(string path, VisualModelProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken), IEnumerable<NamedTensor>? auxiliaryInputs = null)
            => Create(OpenCvImageSource.FromFile(path), profile, inputId, cancellationToken, auxiliaryInputs);

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

        internal static PreparedVisualInput CreateRectangleRoiFromDecoded(Mat decoded, RectangleF crop, string inputName, OpenCvPreprocessOptions options, string? inputId, CancellationToken cancellationToken, IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (decoded == null) throw new ArgumentNullException(nameof(decoded));
            Rect roi = ToClippedRect(crop, decoded.Cols, decoded.Rows);
            using (Mat view = decoded.SubMat(roi))
            using (PreparedVisualInput local = CreateFromDecoded(view, inputName, options, inputId, cancellationToken, auxiliaryInputs))
            {
                ImageTransform transform = ComposeCropTransform(new VisualSize(decoded.Cols, decoded.Rows), roi, local.Transform);
                return new PreparedVisualInput(inputName, local.Tensor, new VisualSize(decoded.Cols, decoded.Rows), local.ModelSize, local.BatchSize, local.Layout, transform, local.Preprocessing, inputId, PreparedInputOwnership.Borrowed, null, auxiliaryInputs);
            }
        }

 #if !DEPLOYSHARP_LEGACY_NO_ROI
        internal static PreparedVisualInput CreateRoiFromDecoded(Mat decoded, IVisualRoiGeometry geometry, string inputName, OpenCvPreprocessOptions options, string? inputId, CancellationToken cancellationToken, IEnumerable<NamedTensor>? auxiliaryInputs = null, bool usePerspective = false)
        {
            if (decoded == null) throw new ArgumentNullException(nameof(decoded));
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (geometry is RectangleRoiGeometry rectangle) return CreateRectangleRoiFromDecoded(decoded, rectangle.Rectangle, inputName, options, inputId, cancellationToken, auxiliaryInputs);
            if (geometry is PolygonRoiGeometry polygon)
            {
                if (usePerspective && polygon.Points.Count == 4) return CreatePerspectiveRoiFromDecoded(decoded, polygon.Points, inputName, options, inputId, cancellationToken, auxiliaryInputs);
                return CreateMaskedRoiFromDecoded(decoded, polygon, null, inputName, options, inputId, cancellationToken, auxiliaryInputs);
            }
            if (geometry is MaskRoiGeometry mask)
            {
                if (mask.SourceSize != new VisualSize(decoded.Cols, decoded.Rows)) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The ROI mask dimensions must match the decoded source image.");
                byte[] values = mask.ToArray();
                Rect crop = FindMaskBounds(values, mask.SourceSize);
                return CreateMaskedRoiFromDecoded(decoded, mask, new MaskRegion(values, mask.SourceSize, crop), inputName, options, inputId, cancellationToken, auxiliaryInputs);
            }
            if (geometry is RotatedRectangleRoiGeometry rotated && usePerspective) return CreatePerspectiveRoiFromDecoded(decoded, rotated.Points, inputName, options, inputId, cancellationToken, auxiliaryInputs);
            throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The ROI geometry requires a perspective-aware OpenCV input path.", technicalDetails: "geometry=" + geometry.Kind);
        }

        internal static PreparedVisualInput CreateRoiBatchFromDecodedPublic(Mat decoded, IReadOnlyList<IVisualRoiGeometry> geometries, string inputName, OpenCvPreprocessOptions options, string? inputId, CancellationToken cancellationToken, IEnumerable<NamedTensor>? auxiliaryInputs)
        {
            var singleOptions = new OpenCvPreprocessOptions(options.ModelSize, options.ResizeMode, options.ColorOrder, options.AlphaMode, options.Means, options.StandardDeviations, options.Layout, 1, options.OutputType, options.PaddingColor, options.AlphaBackground, options.LetterboxRounding, options.Interpolation, options.InputDivisors, options.ScaleUp);
            var prepared = new PreparedVisualInput[geometries.Count];
            try
            {
                for (int index = 0; index < geometries.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    prepared[index] = CreateRoiFromDecoded(decoded, geometries[index], inputName, singleOptions, inputId, cancellationToken, auxiliaryInputs, geometries[index] is RotatedRectangleRoiGeometry);
                    if (prepared[index].BatchSize != 1 || prepared[index].Layout != options.Layout) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Every ROI batch row must produce one image with the requested layout.");
                    if (prepared[index].Tensor.Shape.Rank != 4) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Every ROI batch row must produce a rank-4 tensor.");
                }

                int rowLength = checked((int)prepared[0].Tensor.Length);
                for (int index = 1; index < prepared.Length; index++) if (prepared[index].Tensor.Length != rowLength || !prepared[index].Tensor.Shape.Equals(prepared[0].Tensor.Shape)) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "ROI batch rows must have identical tensor shapes.");
                TensorShape rowShape = prepared[0].Tensor.Shape;
                long[] batchDimensions = rowShape.ToArray();
                batchDimensions[0] = geometries.Count;
                ITensor tensor;
                if (options.OutputType == OpenCvOutputType.UInt8)
                {
                    var values = new byte[checked(rowLength * geometries.Count)];
                    for (int index = 0; index < prepared.Length; index++) Array.Copy((byte[])prepared[index].Tensor.Buffer, 0, values, index * rowLength, rowLength);
                    tensor = new Tensor<byte>(new TensorShape(batchDimensions), values, TensorBufferOwnership.Transfer);
                }
                else
                {
                    var values = new float[checked(rowLength * geometries.Count)];
                    for (int index = 0; index < prepared.Length; index++) Array.Copy((float[])prepared[index].Tensor.Buffer, 0, values, index * rowLength, rowLength);
                    tensor = new Tensor<float>(new TensorShape(batchDimensions), values, TensorBufferOwnership.Transfer);
                }

                var frames = new List<VisualInputFrame>(prepared.Length);
                for (int index = 0; index < prepared.Length; index++) frames.Add(prepared[index].BatchFrames[0]);
                return new PreparedVisualInput(inputName, tensor, new VisualSize(decoded.Cols, decoded.Rows), options.ModelSize, geometries.Count, options.Layout, frames[0].Transform, prepared[0].Preprocessing, inputId, PreparedInputOwnership.Borrowed, null, auxiliaryInputs, frames);
            }
            finally
            {
                for (int index = 0; index < prepared.Length; index++) prepared[index]?.Dispose();
            }
        }

        private static PreparedVisualInput CreatePerspectiveRoiFromDecoded(Mat decoded, IReadOnlyList<PointF> points, string inputName, OpenCvPreprocessOptions options, string? inputId, CancellationToken cancellationToken, IEnumerable<NamedTensor>? auxiliaryInputs)
        {
            if (points == null || points.Count != 4) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "A perspective ROI requires four ordered points.");
            float widthTop = Distance(points[0], points[1]);
            float widthBottom = Distance(points[3], points[2]);
            float heightLeft = Distance(points[0], points[3]);
            float heightRight = Distance(points[1], points[2]);
            int cropWidth = Math.Max(2, checked((int)Math.Ceiling(Math.Max(widthTop, widthBottom))));
            int cropHeight = Math.Max(2, checked((int)Math.Ceiling(Math.Max(heightLeft, heightRight))));
            Point2f[] sourcePoints = { ToNative(points[0]), ToNative(points[1]), ToNative(points[2]), ToNative(points[3]) };
            Point2f[] targetPoints = { new Point2f(0, 0), new Point2f(cropWidth - 1, 0), new Point2f(cropWidth - 1, cropHeight - 1), new Point2f(0, cropHeight - 1) };
            using (Mat perspective = ImageProcessing.GetPerspectiveTransform(sourcePoints, targetPoints, DecompTypes.LU))
            using (var warped = new Mat())
            {
                ObserveCancellation(cancellationToken);
                ImageProcessing.WarpPerspective(decoded, warped, perspective, new Size(cropWidth, cropHeight), ToInterpolation(options.Interpolation == OpenCvInterpolation.PillowBicubic ? OpenCvInterpolation.Cubic : options.Interpolation), BorderTypes.Constant, PaddingScalar(options.PaddingColor, decoded.Channels));
                using (PreparedVisualInput local = CreateFromDecoded(warped, inputName, options, inputId, cancellationToken, auxiliaryInputs))
                {
                    ImageTransform roiTransform = ImageTransform.Perspective(new VisualSize(decoded.Cols, decoded.Rows), new VisualSize(cropWidth, cropHeight), points, new[] { new PointF(0, 0), new PointF(cropWidth, 0), new PointF(cropWidth, cropHeight), new PointF(0, cropHeight) });
                    ImageTransform transform = ImageTransform.Compose(roiTransform, local.Transform);
                    return new PreparedVisualInput(inputName, local.Tensor, new VisualSize(decoded.Cols, decoded.Rows), local.ModelSize, local.BatchSize, local.Layout, transform, local.Preprocessing, inputId, PreparedInputOwnership.Borrowed, null, auxiliaryInputs);
                }
            }
        }

        private static Point2f ToNative(PointF point) => new Point2f(point.X, point.Y);

        private static float Distance(PointF first, PointF second)
        {
            float x = second.X - first.X;
            float y = second.Y - first.Y;
            return (float)Math.Sqrt((x * x) + (y * y));
        }

        private static PreparedVisualInput CreateMaskedRoiFromDecoded(Mat decoded, IVisualRoiGeometry geometry, MaskRegion? maskRegion, string inputName, OpenCvPreprocessOptions options, string? inputId, CancellationToken cancellationToken, IEnumerable<NamedTensor>? auxiliaryInputs)
        {
            Rect roi = maskRegion.HasValue ? maskRegion.Value.Crop : ToClippedRect(geometry.Bounds, decoded.Cols, decoded.Rows);
            using (Mat view = decoded.SubMat(roi))
            using (var nativeMask = new Mat(roi.Height, roi.Width, MatType.CV_8UC1, new Scalar(0)))
            using (var masked = new Mat(roi.Height, roi.Width, decoded.Type, new Scalar(0)))
            {
                if (maskRegion.HasValue) CopyMaskRegion(nativeMask, maskRegion.Value, cancellationToken);
                else FillPolygonMask(nativeMask, geometry.Points, roi);
                ObserveCancellation(cancellationToken);
                CoreOperations.CopyTo(view, masked, nativeMask);
                using (PreparedVisualInput local = CreateFromDecoded(masked, inputName, options, inputId, cancellationToken, auxiliaryInputs))
                {
                    ImageTransform transform = ComposeCropTransform(new VisualSize(decoded.Cols, decoded.Rows), roi, local.Transform);
                    return new PreparedVisualInput(inputName, local.Tensor, new VisualSize(decoded.Cols, decoded.Rows), local.ModelSize, local.BatchSize, local.Layout, transform, local.Preprocessing, inputId, PreparedInputOwnership.Borrowed, null, auxiliaryInputs);
                }
            }
        }

        private static void FillPolygonMask(Mat mask, IReadOnlyList<PointF> points, Rect crop)
        {
            var nativePoints = new Point[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                int x = checked((int)Math.Round(points[index].X - crop.X));
                int y = checked((int)Math.Round(points[index].Y - crop.Y));
                nativePoints[index] = new Point(x, y);
            }
            ImageProcessing.FillPoly(mask, nativePoints, new Scalar(255), LineTypes.Line8, 0, null);
        }

        private static Rect FindMaskBounds(byte[] values, VisualSize size)
        {
            int left = size.Width;
            int top = size.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < size.Height; y++)
            {
                int row = y * size.Width;
                for (int x = 0; x < size.Width; x++)
                {
                    if (values[row + x] == 0) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }
            if (right < left || bottom < top) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The ROI mask must contain at least one selected pixel.");
            return new Rect(left, top, right - left + 1, bottom - top + 1);
        }

        private static void CopyMaskRegion(Mat destination, MaskRegion region, CancellationToken cancellationToken)
        {
            ulong step = destination.Step.ToUInt64();
            if (step < (ulong)region.Crop.Width || step > int.MaxValue) throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV reported an unsupported ROI mask stride.");
            var row = new byte[region.Crop.Width];
            for (int y = 0; y < region.Crop.Height; y++)
            {
                if ((y & 31) == 0) ObserveCancellation(cancellationToken);
                Buffer.BlockCopy(region.Values, ((region.Crop.Y + y) * region.SourceSize.Width) + region.Crop.X, row, 0, row.Length);
                Marshal.Copy(row, 0, IntPtr.Add(destination.Data, checked(y * (int)step)), row.Length);
            }
        }

        private readonly struct MaskRegion
        {
            internal MaskRegion(byte[] values, VisualSize sourceSize, Rect crop)
            {
                Values = values;
                SourceSize = sourceSize;
                Crop = crop;
            }

            internal byte[] Values { get; }
            internal VisualSize SourceSize { get; }
            internal Rect Crop { get; }
        }
 #endif

        private static Rect ToClippedRect(RectangleF crop, int width, int height)
        {
            if (float.IsNaN(crop.X) || float.IsNaN(crop.Y) || float.IsNaN(crop.Width) || float.IsNaN(crop.Height) || float.IsInfinity(crop.X) || float.IsInfinity(crop.Y) || float.IsInfinity(crop.Width) || float.IsInfinity(crop.Height) || crop.Width <= 0 || crop.Height <= 0)
            {
                throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The ROI rectangle must be finite and non-empty.");
            }
            if (crop.Right <= 0 || crop.Bottom <= 0 || crop.X >= width || crop.Y >= height) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The ROI rectangle does not intersect the source image.");

            int left = Math.Max(0, Math.Min(width - 1, (int)Math.Floor(crop.X)));
            int top = Math.Max(0, Math.Min(height - 1, (int)Math.Floor(crop.Y)));
            int right = Math.Max(left + 1, Math.Min(width, checked((int)Math.Ceiling(crop.Right))));
            int bottom = Math.Max(top + 1, Math.Min(height, checked((int)Math.Ceiling(crop.Bottom))));
            if (right <= left || bottom <= top) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The ROI rectangle does not intersect the source image.");
            return new Rect(left, top, right - left, bottom - top);
        }

        private static ImageTransform ComposeCropTransform(VisualSize sourceSize, Rect crop, ImageTransform local)
        {
            return new ImageTransform(
                ImageTransformKind.Custom,
                sourceSize,
                local.ModelSize,
                local.ScaleX,
                local.ScaleY,
                local.OffsetX - (crop.X * local.ScaleX),
                local.OffsetY - (crop.Y * local.ScaleY));
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
            if (!options.ScaleUp) scale = Math.Min(1d, scale);
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
