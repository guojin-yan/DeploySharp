using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents a positive integer image or tensor spatial size. / 表示正整数图像或张量空间尺寸。</summary>
    public readonly struct VisualSize : IEquatable<VisualSize>
    {
        /// <summary>Initializes a visual size. / 初始化视觉尺寸。</summary>
        public VisualSize(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }

        /// <summary>Gets the width in pixels or tensor positions. / 获取以像素或张量位置计量的宽度。</summary>
        public int Width { get; }

        /// <summary>Gets the height in pixels or tensor positions. / 获取以像素或张量位置计量的高度。</summary>
        public int Height { get; }

        /// <inheritdoc />
        /// <remarks>Compares width and height exactly. / 精确比较宽度和高度。</remarks>
        public bool Equals(VisualSize other) => Width == other.Width && Height == other.Height;

        /// <inheritdoc />
        /// <remarks>Compares an object with this size. / 将对象与此尺寸比较。</remarks>
        public override bool Equals(object? obj) => obj is VisualSize other && Equals(other);

        /// <inheritdoc />
        /// <remarks>Computes a component-based hash code. / 根据尺寸分量计算哈希码。</remarks>
        public override int GetHashCode() => unchecked((Width * 397) ^ Height);

        /// <summary>Compares two sizes for equality. / 比较两个尺寸是否相等。</summary>
        public static bool operator ==(VisualSize left, VisualSize right) => left.Equals(right);

        /// <summary>Compares two sizes for inequality. / 比较两个尺寸是否不相等。</summary>
        public static bool operator !=(VisualSize left, VisualSize right) => !left.Equals(right);
    }

    /// <summary>Identifies the spatial operation represented by an image transform. / 标识图像变换所表示的空间操作。</summary>
    public enum ImageTransformKind
    {
        /// <summary>Independent horizontal and vertical resize. / 水平和垂直方向独立缩放。</summary>
        Resize = 0,
        /// <summary>Aspect-preserving resize with padding. / 保持宽高比缩放并填充。</summary>
        Letterbox = 1,
        /// <summary>Crop followed by resize. / 裁剪后缩放。</summary>
        Crop = 2,
        /// <summary>An explicitly supplied affine scale and offset. / 显式提供的仿射缩放和偏移。</summary>
        Custom = 3
    }

    /// <summary>Maps half-open source-image coordinates to model-input coordinates using scale and offset. / 使用缩放和偏移将半开区间源图坐标映射到模型输入坐标。</summary>
    public sealed class ImageTransform
    {
        private readonly bool _isProjective;
        private readonly double _m11;
        private readonly double _m12;
        private readonly double _m13;
        private readonly double _m21;
        private readonly double _m22;
        private readonly double _m23;
        private readonly double _m31;
        private readonly double _m32;
        private readonly double _m33;
        private readonly double _i11;
        private readonly double _i12;
        private readonly double _i13;
        private readonly double _i21;
        private readonly double _i22;
        private readonly double _i23;
        private readonly double _i31;
        private readonly double _i32;
        private readonly double _i33;

        /// <summary>Initializes an invertible axis-aligned transform. / 初始化可逆的轴对齐变换。</summary>
        public ImageTransform(ImageTransformKind kind, VisualSize sourceSize, VisualSize modelSize, float scaleX, float scaleY, float offsetX, float offsetY)
        {
            VisualGuard.Finite(scaleX, nameof(scaleX));
            VisualGuard.Finite(scaleY, nameof(scaleY));
            VisualGuard.Finite(offsetX, nameof(offsetX));
            VisualGuard.Finite(offsetY, nameof(offsetY));
            if (scaleX <= 0) throw new VisualException(VisualErrorCodes.TransformInvalid, "Horizontal scale must be positive.");
            if (scaleY <= 0) throw new VisualException(VisualErrorCodes.TransformInvalid, "Vertical scale must be positive.");
            if (!Enum.IsDefined(typeof(ImageTransformKind), kind)) throw new VisualException(VisualErrorCodes.TransformInvalid, "Transform kind is invalid.");
            Kind = kind;
            SourceSize = sourceSize;
            ModelSize = modelSize;
            ScaleX = scaleX;
            ScaleY = scaleY;
            OffsetX = offsetX;
            OffsetY = offsetY;
            _isProjective = false;
            _m11 = scaleX;
            _m12 = 0;
            _m13 = offsetX;
            _m21 = 0;
            _m22 = scaleY;
            _m23 = offsetY;
            _m31 = 0;
            _m32 = 0;
            _m33 = 1;
            _i11 = 1d / scaleX;
            _i12 = 0;
            _i13 = -offsetX / scaleX;
            _i21 = 0;
            _i22 = 1d / scaleY;
            _i23 = -offsetY / scaleY;
            _i31 = 0;
            _i32 = 0;
            _i33 = 1;
        }

        private ImageTransform(VisualSize sourceSize, VisualSize modelSize, double[] matrix, double[] inverse, ImageTransformKind kind = ImageTransformKind.Custom)
        {
            SourceSize = sourceSize;
            ModelSize = modelSize;
            Kind = kind;
            _isProjective = true;
            _m11 = matrix[0];
            _m12 = matrix[1];
            _m13 = matrix[2];
            _m21 = matrix[3];
            _m22 = matrix[4];
            _m23 = matrix[5];
            _m31 = matrix[6];
            _m32 = matrix[7];
            _m33 = matrix[8];
            _i11 = inverse[0];
            _i12 = inverse[1];
            _i13 = inverse[2];
            _i21 = inverse[3];
            _i22 = inverse[4];
            _i23 = inverse[5];
            _i31 = inverse[6];
            _i32 = inverse[7];
            _i33 = inverse[8];
            ScaleX = (float)_m11;
            ScaleY = (float)_m22;
            OffsetX = (float)_m13;
            OffsetY = (float)_m23;
        }

        /// <summary>Gets the transform kind. / 获取变换类型。</summary>
        public ImageTransformKind Kind { get; }
        /// <summary>Gets the source image size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the model input size. / 获取模型输入尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets the horizontal scale. / 获取水平缩放比例。</summary>
        public float ScaleX { get; }
        /// <summary>Gets the vertical scale. / 获取垂直缩放比例。</summary>
        public float ScaleY { get; }
        /// <summary>Gets the horizontal model-space offset. / 获取模型空间水平偏移。</summary>
        public float OffsetX { get; }
        /// <summary>Gets the vertical model-space offset. / 获取模型空间垂直偏移。</summary>
        public float OffsetY { get; }

        /// <summary>Gets whether the transform is the legacy axis-aligned scale and offset form. / 获取变换是否为传统轴对齐缩放和偏移形式。</summary>
        public bool IsAxisAligned => !_isProjective;

        /// <summary>Gets whether the transform contains rotation, shear, or perspective terms. / 获取变换是否包含旋转、剪切或透视项。</summary>
        public bool IsProjective => _isProjective;

        internal double FrobeniusCondition
        {
            get
            {
                double norm, inverseNorm;
                if (_isProjective)
                {
                    norm = _m11 * _m11 + _m12 * _m12 + _m13 * _m13 + _m21 * _m21 + _m22 * _m22 + _m23 * _m23 + _m31 * _m31 + _m32 * _m32 + _m33 * _m33;
                    inverseNorm = _i11 * _i11 + _i12 * _i12 + _i13 * _i13 + _i21 * _i21 + _i22 * _i22 + _i23 * _i23 + _i31 * _i31 + _i32 * _i32 + _i33 * _i33;
                }
                else
                {
                    double x = ScaleX, y = ScaleY, dx = OffsetX, dy = OffsetY;
                    norm = x * x + y * y + dx * dx + dy * dy + 1;
                    inverseNorm = (1 + dx * dx) / (x * x) + (1 + dy * dy) / (y * y) + 1;
                }
                return Math.Sqrt(norm * inverseNorm);
            }
        }

        /// <summary>Creates an invertible projective transform from four corresponding source/model corners. / 根据四组对应的源图和模型角点创建可逆透视变换。</summary>
        /// <remarks>Points must be supplied in the same clockwise or counter-clockwise order and form non-degenerate quadrilaterals. Rectangles are mapped by all four corners, so rotated and perspective crops retain exact point geometry. / 两组点必须按相同顺时针或逆时针顺序提供并构成非退化四边形；矩形的四个角点均参与映射，因此旋转和透视裁剪可以保留精确点几何。</remarks>
        public static ImageTransform Perspective(VisualSize sourceSize, VisualSize modelSize, IReadOnlyList<PointF> sourcePoints, IReadOnlyList<PointF> modelPoints)
        {
            if (sourcePoints == null) throw new ArgumentNullException(nameof(sourcePoints));
            if (modelPoints == null) throw new ArgumentNullException(nameof(modelPoints));
            if (sourcePoints.Count != 4 || modelPoints.Count != 4) throw new ArgumentException("Perspective transforms require exactly four source and four model points.");
            double[] matrix = SolveHomography(sourcePoints, modelPoints);
            double[] inverse = Invert3x3(matrix);
            return new ImageTransform(sourceSize, modelSize, matrix, inverse);
        }

        /// <summary>Composes two transforms in source-to-intermediate then intermediate-to-model order. / 按源图到中间空间再到模型空间的顺序组合两个变换。</summary>
        public static ImageTransform Compose(ImageTransform sourceToIntermediate, ImageTransform intermediateToModel)
        {
            if (sourceToIntermediate == null) throw new ArgumentNullException(nameof(sourceToIntermediate));
            if (intermediateToModel == null) throw new ArgumentNullException(nameof(intermediateToModel));
            if (sourceToIntermediate.ModelSize != intermediateToModel.SourceSize) throw new ArgumentException("The intermediate transform sizes must match.", nameof(intermediateToModel));
            double[] product = Multiply(intermediateToModel.Matrix(), sourceToIntermediate.Matrix());
            return new ImageTransform(sourceToIntermediate.SourceSize, intermediateToModel.ModelSize, product, Invert3x3(product));
        }

        /// <summary>Creates a direct resize transform. / 创建直接缩放变换。</summary>
        public static ImageTransform Resize(VisualSize sourceSize, VisualSize modelSize)
        {
            return new ImageTransform(ImageTransformKind.Resize, sourceSize, modelSize, (float)modelSize.Width / sourceSize.Width, (float)modelSize.Height / sourceSize.Height, 0, 0);
        }

        /// <summary>Creates a centered aspect-preserving letterbox transform. / 创建居中的保持宽高比 letterbox 变换。</summary>
        public static ImageTransform Letterbox(VisualSize sourceSize, VisualSize modelSize)
        {
            float scale = Math.Min((float)modelSize.Width / sourceSize.Width, (float)modelSize.Height / sourceSize.Height);
            float offsetX = (modelSize.Width - (sourceSize.Width * scale)) / 2f;
            float offsetY = (modelSize.Height - (sourceSize.Height * scale)) / 2f;
            return new ImageTransform(ImageTransformKind.Letterbox, sourceSize, modelSize, scale, scale, offsetX, offsetY);
        }

        /// <summary>Creates a crop-to-model transform from a source-space crop rectangle. / 根据源图空间裁剪矩形创建裁剪到模型的变换。</summary>
        public static ImageTransform Crop(VisualSize sourceSize, VisualSize modelSize, RectangleF crop)
        {
            EnsureFinite(crop);
            if (crop.Width <= 0 || crop.Height <= 0 || crop.X < 0 || crop.Y < 0 || crop.Right > sourceSize.Width || crop.Bottom > sourceSize.Height)
            {
                throw new VisualException(VisualErrorCodes.TransformInvalid, "Crop rectangle must be non-empty and remain inside the source image.");
            }

            float scaleX = modelSize.Width / crop.Width;
            float scaleY = modelSize.Height / crop.Height;
            return new ImageTransform(ImageTransformKind.Crop, sourceSize, modelSize, scaleX, scaleY, -crop.X * scaleX, -crop.Y * scaleY);
        }

        /// <summary>Maps a source-space point to model space. / 将源图空间点映射到模型空间。</summary>
        public PointF ToModel(PointF sourcePoint)
        {
            EnsureFinite(sourcePoint);
            return _isProjective ? Map(sourcePoint, _m11, _m12, _m13, _m21, _m22, _m23, _m31, _m32, _m33) : new PointF((sourcePoint.X * ScaleX) + OffsetX, (sourcePoint.Y * ScaleY) + OffsetY);
        }

        /// <summary>Maps a model-space point back to source space. / 将模型空间点逆向映射到源图空间。</summary>
        public PointF ToSource(PointF modelPoint)
        {
            EnsureFinite(modelPoint);
            return _isProjective ? Map(modelPoint, _i11, _i12, _i13, _i21, _i22, _i23, _i31, _i32, _i33) : new PointF((modelPoint.X - OffsetX) / ScaleX, (modelPoint.Y - OffsetY) / ScaleY);
        }

        /// <summary>Maps a half-open source-space rectangle to model space. / 将半开区间源图空间矩形映射到模型空间。</summary>
        public RectangleF ToModel(RectangleF sourceRectangle)
        {
            EnsureFinite(sourceRectangle);
            return Bounds(ToModelPoints(sourceRectangle));
        }

        /// <summary>Maps a half-open model-space rectangle back to source space. / 将半开区间模型空间矩形逆向映射到源图空间。</summary>
        public RectangleF ToSource(RectangleF modelRectangle)
        {
            EnsureFinite(modelRectangle);
            return Bounds(ToSourcePoints(modelRectangle));
        }

        /// <summary>Clips a source-space rectangle to half-open source image bounds. / 将源图空间矩形裁剪到半开区间源图边界。</summary>
        public RectangleF ClipToSource(RectangleF rectangle)
        {
            EnsureFinite(rectangle);
            float left = Math.Max(0, Math.Min(SourceSize.Width, rectangle.X));
            float top = Math.Max(0, Math.Min(SourceSize.Height, rectangle.Y));
            float right = Math.Max(left, Math.Min(SourceSize.Width, rectangle.Right));
            float bottom = Math.Max(top, Math.Min(SourceSize.Height, rectangle.Bottom));
            return new RectangleF(left, top, right - left, bottom - top);
        }

        private static RectangleF FromCorners(float x1, float y1, float x2, float y2)
        {
            float left = Math.Min(x1, x2);
            float top = Math.Min(y1, y2);
            float right = Math.Max(x1, x2);
            float bottom = Math.Max(y1, y2);
            return new RectangleF(left, top, right - left, bottom - top);
        }

        private IReadOnlyList<PointF> ToModelPoints(RectangleF rectangle)
        {
            return new[]
            {
                ToModel(new PointF(rectangle.X, rectangle.Y)),
                ToModel(new PointF(rectangle.Right, rectangle.Y)),
                ToModel(new PointF(rectangle.Right, rectangle.Bottom)),
                ToModel(new PointF(rectangle.X, rectangle.Bottom))
            };
        }

        private IReadOnlyList<PointF> ToSourcePoints(RectangleF rectangle)
        {
            return new[]
            {
                ToSource(new PointF(rectangle.X, rectangle.Y)),
                ToSource(new PointF(rectangle.Right, rectangle.Y)),
                ToSource(new PointF(rectangle.Right, rectangle.Bottom)),
                ToSource(new PointF(rectangle.X, rectangle.Bottom))
            };
        }

        private static RectangleF Bounds(IReadOnlyList<PointF> points)
        {
            float minX = points[0].X;
            float minY = points[0].Y;
            float maxX = points[0].X;
            float maxY = points[0].Y;
            for (int index = 1; index < points.Count; index++)
            {
                minX = Math.Min(minX, points[index].X);
                minY = Math.Min(minY, points[index].Y);
                maxX = Math.Max(maxX, points[index].X);
                maxY = Math.Max(maxY, points[index].Y);
            }
            return FromCorners(minX, minY, maxX, maxY);
        }

        private static PointF Map(PointF point, double m11, double m12, double m13, double m21, double m22, double m23, double m31, double m32, double m33)
        {
            double denominator = (m31 * point.X) + (m32 * point.Y) + m33;
            if (Math.Abs(denominator) <= 1e-12) throw new VisualException(VisualErrorCodes.TransformInvalid, "A projective transform maps a point to infinity.");
            float x = (float)(((m11 * point.X) + (m12 * point.Y) + m13) / denominator);
            float y = (float)(((m21 * point.X) + (m22 * point.Y) + m23) / denominator);
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)) throw new VisualException(VisualErrorCodes.TransformInvalid, "A projective transform produced a non-finite point.");
            return new PointF(x, y);
        }

        private double[] Matrix()
        {
            return new[] { _m11, _m12, _m13, _m21, _m22, _m23, _m31, _m32, _m33 };
        }

        private static double[] Multiply(double[] left, double[] right)
        {
            var result = new double[9];
            for (int row = 0; row < 3; row++) for (int column = 0; column < 3; column++)
            {
                result[(row * 3) + column] = (left[row * 3] * right[column]) + (left[(row * 3) + 1] * right[3 + column]) + (left[(row * 3) + 2] * right[6 + column]);
            }
            return result;
        }

        private static double[] SolveHomography(IReadOnlyList<PointF> source, IReadOnlyList<PointF> model)
        {
            var augmented = new double[8, 9];
            for (int index = 0; index < 4; index++)
            {
                double x = source[index].X;
                double y = source[index].Y;
                double u = model[index].X;
                double v = model[index].Y;
                EnsureFinite(source[index]);
                EnsureFinite(model[index]);
                int row = index * 2;
                augmented[row, 0] = x;
                augmented[row, 1] = y;
                augmented[row, 2] = 1;
                augmented[row, 6] = -u * x;
                augmented[row, 7] = -u * y;
                augmented[row, 8] = u;
                augmented[row + 1, 3] = x;
                augmented[row + 1, 4] = y;
                augmented[row + 1, 5] = 1;
                augmented[row + 1, 6] = -v * x;
                augmented[row + 1, 7] = -v * y;
                augmented[row + 1, 8] = v;
            }

            for (int column = 0; column < 8; column++)
            {
                int pivot = column;
                double largest = Math.Abs(augmented[pivot, column]);
                for (int row = column + 1; row < 8; row++)
                {
                    double candidate = Math.Abs(augmented[row, column]);
                    if (candidate > largest) { largest = candidate; pivot = row; }
                }
                if (largest <= 1e-12) throw new VisualException(VisualErrorCodes.TransformInvalid, "Perspective point correspondences are degenerate.");
                if (pivot != column) for (int value = column; value <= 8; value++)
                {
                    double temporary = augmented[column, value];
                    augmented[column, value] = augmented[pivot, value];
                    augmented[pivot, value] = temporary;
                }
                double divisor = augmented[column, column];
                for (int value = column; value <= 8; value++) augmented[column, value] /= divisor;
                for (int row = 0; row < 8; row++)
                {
                    if (row == column) continue;
                    double factor = augmented[row, column];
                    if (Math.Abs(factor) <= 1e-15) continue;
                    for (int value = column; value <= 8; value++) augmented[row, value] -= factor * augmented[column, value];
                }
            }

            return new[] { augmented[0, 8], augmented[1, 8], augmented[2, 8], augmented[3, 8], augmented[4, 8], augmented[5, 8], augmented[6, 8], augmented[7, 8], 1d };
        }

        private static double[] Invert3x3(double[] matrix)
        {
            double a = matrix[0], b = matrix[1], c = matrix[2], d = matrix[3], e = matrix[4], f = matrix[5], g = matrix[6], h = matrix[7], i = matrix[8];
            double determinant = a * ((e * i) - (f * h)) - b * ((d * i) - (f * g)) + c * ((d * h) - (e * g));
            if (Math.Abs(determinant) <= 1e-12) throw new VisualException(VisualErrorCodes.TransformInvalid, "Perspective transform is not invertible.");
            return new[]
            {
                ((e * i) - (f * h)) / determinant, ((c * h) - (b * i)) / determinant, ((b * f) - (c * e)) / determinant,
                ((f * g) - (d * i)) / determinant, ((a * i) - (c * g)) / determinant, ((c * d) - (a * f)) / determinant,
                ((d * h) - (e * g)) / determinant, ((b * g) - (a * h)) / determinant, ((a * e) - (b * d)) / determinant
            };
        }

        private static void EnsureFinite(PointF point)
        {
            VisualGuard.Finite(point.X, nameof(point.X));
            VisualGuard.Finite(point.Y, nameof(point.Y));
        }

        private static void EnsureFinite(RectangleF rectangle)
        {
            VisualGuard.Finite(rectangle.X, nameof(rectangle.X));
            VisualGuard.Finite(rectangle.Y, nameof(rectangle.Y));
            VisualGuard.Finite(rectangle.Width, nameof(rectangle.Width));
            VisualGuard.Finite(rectangle.Height, nameof(rectangle.Height));
        }
    }
}
