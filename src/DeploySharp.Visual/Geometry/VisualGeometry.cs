using System;
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
            return new PointF((sourcePoint.X * ScaleX) + OffsetX, (sourcePoint.Y * ScaleY) + OffsetY);
        }

        /// <summary>Maps a model-space point back to source space. / 将模型空间点逆向映射到源图空间。</summary>
        public PointF ToSource(PointF modelPoint)
        {
            EnsureFinite(modelPoint);
            return new PointF((modelPoint.X - OffsetX) / ScaleX, (modelPoint.Y - OffsetY) / ScaleY);
        }

        /// <summary>Maps a half-open source-space rectangle to model space. / 将半开区间源图空间矩形映射到模型空间。</summary>
        public RectangleF ToModel(RectangleF sourceRectangle)
        {
            EnsureFinite(sourceRectangle);
            PointF first = ToModel(new PointF(sourceRectangle.X, sourceRectangle.Y));
            PointF second = ToModel(new PointF(sourceRectangle.Right, sourceRectangle.Bottom));
            return FromCorners(first.X, first.Y, second.X, second.Y);
        }

        /// <summary>Maps a half-open model-space rectangle back to source space. / 将半开区间模型空间矩形逆向映射到源图空间。</summary>
        public RectangleF ToSource(RectangleF modelRectangle)
        {
            EnsureFinite(modelRectangle);
            PointF first = ToSource(new PointF(modelRectangle.X, modelRectangle.Y));
            PointF second = ToSource(new PointF(modelRectangle.Right, modelRectangle.Bottom));
            return FromCorners(first.X, first.Y, second.X, second.Y);
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
