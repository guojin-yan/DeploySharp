using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.OpenCvSharp.Core;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Owns one decoded OpenCV image for repeated and concurrent ROI preprocessing. / 拥有一张已解码 OpenCV 图像，用于重复和并发 ROI 预处理。</summary>
    public sealed class OpenCvDecodedRoiImage : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Mat _decoded;
        private int _activePreparations;
        private bool _disposeRequested;
        private bool _disposed;

        internal OpenCvDecodedRoiImage(Mat decoded, string inputId)
        {
            _decoded = decoded ?? throw new ArgumentNullException(nameof(decoded));
            InputId = string.IsNullOrWhiteSpace(inputId) ? null : inputId;
            SourceSize = new VisualSize(decoded.Cols, decoded.Rows);
        }

        /// <summary>Gets the decoded source dimensions. / 获取已解码源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the encoded-source identity when available. / 获取可用的编码源标识。</summary>
        public string? InputId { get; }

        /// <summary>Prepares one axis-aligned ROI while retaining original-source geometry. / 准备一个轴对齐 ROI，同时保留原始源图几何。</summary>
        public PreparedVisualInput PrepareRectangle(
            IVisualRoiGeometry geometry,
            string inputName,
            OpenCvPreprocessOptions options,
            CancellationToken cancellationToken = default(CancellationToken),
            IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (geometry is not RectangleRoiGeometry rectangle) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "This input path accepts only an axis-aligned rectangle ROI.", technicalDetails: "geometry=" + geometry.Kind);
            EnterPreparation();
            try
            {
                OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                return OpenCvVisualInputFactory.CreateRectangleRoiFromDecoded(_decoded, rectangle.Rectangle, inputName, options, InputId, cancellationToken, auxiliaryInputs);
            }
            finally { ExitPreparation(); }
        }

        /// <summary>Prepares one source-space rectangle, polygon, or binary-mask ROI while retaining original-source geometry. / 准备一个源图空间矩形、多边形或二值 Mask ROI，同时保留原始源图几何。</summary>
        public PreparedVisualInput Prepare(
            IVisualRoiGeometry geometry,
            string inputName,
            OpenCvPreprocessOptions options,
            CancellationToken cancellationToken = default(CancellationToken),
            IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (options == null) throw new ArgumentNullException(nameof(options));
            EnterPreparation();
            try
            {
                OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                return OpenCvVisualInputFactory.CreateRoiFromDecoded(_decoded, geometry, inputName, options, InputId, cancellationToken, auxiliaryInputs, geometry is RotatedRectangleRoiGeometry);
            }
            finally { ExitPreparation(); }
        }

        /// <summary>Prepares an ordered quadrilateral or rotated-rectangle ROI with an exact perspective transform. / 使用精确透视变换准备有序四边形或旋转矩形 ROI。</summary>
        public PreparedVisualInput PreparePerspective(
            IReadOnlyList<PointF> quadrilateral,
            string inputName,
            OpenCvPreprocessOptions options,
            CancellationToken cancellationToken = default(CancellationToken),
            IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (quadrilateral == null) throw new ArgumentNullException(nameof(quadrilateral));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (quadrilateral.Count != 4) throw new ArgumentException("A quadrilateral ROI requires exactly four ordered points.", nameof(quadrilateral));
            EnterPreparation();
            try
            {
                OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                return OpenCvVisualInputFactory.CreateRoiFromDecoded(_decoded, new PolygonRoiGeometry(quadrilateral), inputName, options, InputId, cancellationToken, auxiliaryInputs, true);
            }
            finally { ExitPreparation(); }
        }

        /// <summary>Prepares multiple ROIs as one true NCHW or NHWC batch while reusing the decoded source image. / 复用已解码源图，将多个 ROI 准备为真正的 NCHW 或 NHWC Batch。</summary>
        public PreparedVisualInput PrepareBatch(
            IReadOnlyList<IVisualRoiGeometry> geometries,
            string inputName,
            OpenCvPreprocessOptions options,
            CancellationToken cancellationToken = default(CancellationToken),
            IEnumerable<NamedTensor>? auxiliaryInputs = null)
        {
            if (geometries == null) throw new ArgumentNullException(nameof(geometries));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (geometries.Count == 0) throw new ArgumentException("At least one ROI geometry is required.", nameof(geometries));
            if (geometries.Count > 4096) throw new ArgumentOutOfRangeException(nameof(geometries));
            if (options.Layout != VisualTensorLayout.Nchw && options.Layout != VisualTensorLayout.Nhwc) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "ROI batches require NCHW or NHWC layout.");
            for (int index = 0; index < geometries.Count; index++) if (geometries[index] == null) throw new ArgumentException("ROI geometries cannot contain null values.", nameof(geometries));
            EnterPreparation();
            try
            {
                OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                return OpenCvVisualInputFactory.CreateRoiBatchFromDecodedPublic(_decoded, geometries, inputName, options, InputId, cancellationToken, auxiliaryInputs);
            }
            finally { ExitPreparation(); }
        }

        /// <inheritdoc />
        /// <remarks>Waits for in-flight preparation calls before releasing the native image. / 在释放 native 图像前等待正在进行的准备调用结束。</remarks>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposeRequested = true;
                while (_activePreparations > 0) Monitor.Wait(_gate);
                if (_disposed) return;
                _disposed = true;
            }
            _decoded.Dispose();
        }

        private void EnterPreparation()
        {
            lock (_gate)
            {
                if (_disposeRequested || _disposed) throw new OpenCvVisualException(OpenCvErrorCodes.ObjectDisposed, "The decoded ROI image has been disposed.");
                _activePreparations = checked(_activePreparations + 1);
            }
        }

        private void ExitPreparation()
        {
            lock (_gate)
            {
                _activePreparations--;
                if (_activePreparations == 0) Monitor.PulseAll(_gate);
            }
        }
    }
}
