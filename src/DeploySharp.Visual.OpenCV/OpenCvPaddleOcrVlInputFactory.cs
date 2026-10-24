#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Threading;
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

        /// <summary>Creates an OpenCV PaddleOCR-VL input factory. / 创建 OpenCV PaddleOCR-VL 输入工厂。</summary>
        public OpenCvPaddleOcrVlInputFactory(int maximumSourcePixels = 128 * 1024 * 1024)
        {
            _processor = new PaddleOcrVlImageProcessor(maximumSourcePixels);
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
                using (Mat decoded = OpenCvImageLoader.Decode(source))
                {
                    OpenCvImageLoader.Validate(decoded, source);
                    var sourceSize = new VisualSize(decoded.Cols, decoded.Rows);
                    var conversion = new OpenCvPreprocessOptions(sourceSize, OpenCvResizeMode.Resize,
                        VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, layout: VisualTensorLayout.Hwc,
                        outputType: OpenCvOutputType.UInt8);
                    byte[] rgb = OpenCvVisualInputFactory.CopyRowsAndConvertChannels(decoded, conversion);
                    return _processor.Create(rgb, sourceSize, task, minimumPixels, maximumPixels, source.Sha256, cancellationToken);
                }
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The PaddleOCR-VL OpenCV preprocessing operation was cancelled.", exception); }
            catch (OpenCvException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "OpenCV failed while preparing PaddleOCR-VL pixels.", exception); }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The PaddleOCR-VL image could not be prepared.", exception); }
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
