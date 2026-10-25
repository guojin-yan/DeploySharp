#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Core;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Decodes an image once with OpenCV and prepares an official PaddleOCR-VL-1.6 input. / 使用 OpenCV 单次解码并准备官方 PaddleOCR-VL-1.6 输入。</summary>
    /// <remarks>The adapter owns no native image after returning. It preserves the encoded-source SHA256 as the input identity and delegates geometry, resampling and patch packing to the managed VL processor. / 返回后不保留 native 图像；保留编码源 SHA256 作为输入身份，并将几何、重采样和 Patch 打包交给托管 VL Processor。</remarks>
    public sealed class OpenCvPaddleOcrVlInputFactory
    {
        private readonly PaddleOcrVlImageProcessor _processor;
        private readonly bool _normalizeExifOrientation;

        /// <summary>Creates an OpenCV PaddleOCR-VL input factory. / 创建 OpenCV PaddleOCR-VL 输入工厂。</summary>
        /// <param name="maximumSourcePixels">Maximum decoded source pixels. / 解码源图的最大像素数。</param>
        public OpenCvPaddleOcrVlInputFactory(int maximumSourcePixels = 128 * 1024 * 1024)
            : this(maximumSourcePixels, normalizeExifOrientation: true)
        {
        }

        /// <summary>Creates an OpenCV PaddleOCR-VL input factory with explicit EXIF handling. / 创建显式指定 EXIF 处理方式的 OpenCV PaddleOCR-VL 输入工厂。</summary>
        /// <param name="maximumSourcePixels">Maximum decoded source pixels. / 解码源图的最大像素数。</param>
        /// <param name="normalizeExifOrientation">True keeps page/display coordinates upright (the default); false preserves encoded JPEG pixels to match the official Pillow processor, which does not apply EXIF orientation. This option affects encoded-image entrypoints only; an already decoded page retains its owner's orientation. / true（默认）按 EXIF 统一页面/显示坐标；false 保留 JPEG 编码像素，以匹配不自动应用 EXIF 方向的官方 Pillow Processor。该选项仅影响编码图入口；已解码页面保留所有者的方向。</param>
        public OpenCvPaddleOcrVlInputFactory(int maximumSourcePixels, bool normalizeExifOrientation)
        {
            _processor = new PaddleOcrVlImageProcessor(maximumSourcePixels);
            _normalizeExifOrientation = normalizeExifOrientation;
        }

        /// <summary>Decodes and prepares one official task input. / 解码并准备一个官方任务输入。</summary>
        public PaddleOcrVlPreparedInput Create(OpenCvImageSource source,
            PaddleOcrVlTask task = PaddleOcrVlTask.TextRecognition, int? minimumPixels = null,
            int? maximumPixels = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
            OpenCvRuntimePreflight.Check();
            try
            {
                using (Mat decoded = OpenCvImageLoader.Decode(source, _normalizeExifOrientation))
                {
                    OpenCvImageLoader.Validate(decoded, source);
                    return CreateDecoded(decoded, task, minimumPixels, maximumPixels, source.Sha256, cancellationToken);
                }
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The PaddleOCR-VL OpenCV preprocessing operation was cancelled.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while preparing PaddleOCR-VL pixels.", exception); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The PaddleOCR-VL image could not be prepared.", exception); }
        }

        /// <summary>Prepares the full page from an existing decoded ROI owner, without decoding again. / 从已有 ROI 图像所有者准备整页，不重复解码。</summary>
        public PaddleOcrVlPreparedInput CreateFromPage(OpenCvDecodedRoiImage page,
            PaddleOcrVlTask task = PaddleOcrVlTask.TextRecognition, int? minimumPixels = null,
            int? maximumPixels = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
            return page.PrepareFromDecoded(decoded => CreateDecoded(decoded, task, minimumPixels, maximumPixels, page.InputId, cancellationToken));
        }

        /// <summary>Prepares a page-space rectangle using a stride-safe view of the decoded page. / 使用已解码页面的 stride-safe 视图准备页面坐标矩形。</summary>
        /// <remarks>Bounds use floor(left/top), ceiling(right/bottom), then clipping to the page. No image encoding, page-sized clone or OpenCV resize is performed. SourceSha256 identifies the crop's dimensions and packed RGB pixels, not the encoded page. / 边界先左上向下、右下向上取整，再裁至页面；不重新编码、不克隆整页、不使用 OpenCV 缩放。SourceSha256 标识裁剪尺寸和紧密 RGB 像素，而非页面编码文件。</remarks>
        public PaddleOcrVlPreparedInput CreateRegion(OpenCvDecodedRoiImage page, RectangleF bounds,
            PaddleOcrVlTask task = PaddleOcrVlTask.TextRecognition, int? minimumPixels = null,
            int? maximumPixels = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
            return page.PrepareFromDecoded(decoded =>
            {
                try
                {
                    Rect crop = OpenCvVisualInputFactory.ToClippedRect(bounds, decoded.Cols, decoded.Rows);
                    using (Mat view = decoded.SubMat(crop))
                        return CreateDecoded(view, task, minimumPixels, maximumPixels, null, cancellationToken,
                            new PaddleOcrVlSourceFrame(new VisualSize(decoded.Cols, decoded.Rows), new VisualSize(crop.Width, crop.Height), crop.X, crop.Y));
                }
                catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while creating the PaddleOCR-VL region view.", exception); }
            });
        }

        private PaddleOcrVlPreparedInput CreateDecoded(Mat decoded, PaddleOcrVlTask task, int? minimumPixels,
            int? maximumPixels, string? sourceSha256, CancellationToken cancellationToken, PaddleOcrVlSourceFrame? sourceFrame = null)
        {
            OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
            try
            {
                var sourceSize = new VisualSize(decoded.Cols, decoded.Rows);
                // Check the source budget and task geometry before allocating the RGB crop.
                PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(sourceSize, task,
                    minimumPixels, maximumPixels, _processor.MaximumSourcePixels);
                var conversion = new OpenCvPreprocessOptions(sourceSize, OpenCvResizeMode.Resize,
                    VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, layout: VisualTensorLayout.Hwc,
                    outputType: OpenCvOutputType.UInt8);
                byte[] rgb = OpenCvVisualInputFactory.CopyRowsAndConvertChannels(decoded, conversion);
                OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                return _processor.Create(plan, rgb, sourceSha256 ?? CropIdentity(sourceSize, rgb), cancellationToken)
                    .WithSourceFrame(sourceFrame ?? new PaddleOcrVlSourceFrame(sourceSize, sourceSize));
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The PaddleOCR-VL OpenCV preprocessing operation was cancelled.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while preparing PaddleOCR-VL pixels.", exception); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The PaddleOCR-VL image could not be prepared.", exception); }
        }

        private static string CropIdentity(VisualSize size, byte[] rgb)
        {
            using (IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                hash.AppendData(Encoding.UTF8.GetBytes("deploysharp-paddleocr-vl-rgb8-v1\n"
                    + size.Width.ToString(CultureInfo.InvariantCulture) + "x"
                    + size.Height.ToString(CultureInfo.InvariantCulture) + "\n"));
                hash.AppendData(rgb);
                return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }
        }

        /// <summary>Creates an input from an absolute image path. / 从绝对图像路径创建输入。</summary>
        public PaddleOcrVlPreparedInput CreateFromFile(string path,
            PaddleOcrVlTask task = PaddleOcrVlTask.TextRecognition, int? minimumPixels = null,
            int? maximumPixels = null, CancellationToken cancellationToken = default(CancellationToken))
            => Create(OpenCvImageSource.FromFile(path), task, minimumPixels, maximumPixels, cancellationToken);

        /// <summary>Creates an input from copied encoded image bytes. / 从复制的编码图像字节创建输入。</summary>
        public PaddleOcrVlPreparedInput CreateFromBytes(byte[] bytes,
            PaddleOcrVlTask task = PaddleOcrVlTask.TextRecognition, int? minimumPixels = null,
            int? maximumPixels = null, CancellationToken cancellationToken = default(CancellationToken))
            => Create(OpenCvImageSource.FromBytes(bytes), task, minimumPixels, maximumPixels, cancellationToken);
    }
}
#endif
