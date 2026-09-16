using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Supplies the external coordinate context required to resolve TileLocal or World ROIs. / 提供解析 TileLocal 或 World ROI 所需的外部坐标上下文。</summary>
    public sealed class VisualRoiCoordinateContext
    {
        private VisualRoiCoordinateContext(VisualSize sourceSize, RectangleF? tileBounds, VisualRoiWorldTransform? worldTransform)
        {
            SourceSize = sourceSize;
            TileBounds = tileBounds;
            WorldTransform = worldTransform;
        }

        /// <summary>Creates a tile-local context whose tile is expressed in source pixels. / 创建以源像素表示切片区域的 TileLocal 上下文。</summary>
        public static VisualRoiCoordinateContext ForTile(VisualSize sourceSize, RectangleF tileBounds)
        {
            EnsureSourceSize(sourceSize);
            EnsureFinite(tileBounds);
            if (tileBounds.Width <= 0 || tileBounds.Height <= 0) throw new ArgumentOutOfRangeException(nameof(tileBounds));
            if (tileBounds.X < 0 || tileBounds.Y < 0 || tileBounds.Right > sourceSize.Width || tileBounds.Bottom > sourceSize.Height) throw new ArgumentException("Tile bounds must be inside the source image.", nameof(tileBounds));
            return new VisualRoiCoordinateContext(sourceSize, tileBounds, null);
        }

        /// <summary>Creates a world-coordinate context using a world-to-source homography. / 使用世界坐标到源像素的单应矩阵创建上下文。</summary>
        public static VisualRoiCoordinateContext ForWorld(VisualSize sourceSize, VisualRoiWorldTransform worldTransform)
        {
            EnsureSourceSize(sourceSize);
            if (worldTransform == null) throw new ArgumentNullException(nameof(worldTransform));
            return new VisualRoiCoordinateContext(sourceSize, null, worldTransform);
        }

        /// <summary>Gets the source image size expected by this context. / 获取此上下文对应的源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the source-pixel bounds of a tile, or null for a world context. / 获取切片的源像素边界；世界坐标上下文返回 null。</summary>
        public RectangleF? TileBounds { get; }
        /// <summary>Gets the world-to-source transform, or null for a tile context. / 获取世界坐标到源像素变换；切片上下文返回 null。</summary>
        public VisualRoiWorldTransform? WorldTransform { get; }

        /// <summary>Determines whether two external coordinate contexts describe the same projection. / 判断两个外部坐标上下文是否描述相同的投影。</summary>
        internal bool EquivalentTo(VisualRoiCoordinateContext? other)
        {
            if (other == null || SourceSize != other.SourceSize) return false;
            if (TileBounds.HasValue != other.TileBounds.HasValue) return false;
            if (TileBounds.HasValue && TileBounds.Value != other.TileBounds!.Value) return false;
            if ((WorldTransform == null) != (other.WorldTransform == null)) return false;
            return WorldTransform == null || WorldTransform.EquivalentTo(other.WorldTransform!);
        }

        internal IVisualRoiGeometry Resolve(VisualRoi roi)
        {
            if (roi == null) throw new ArgumentNullException(nameof(roi));
            if (roi.CoordinateSpace == RoiCoordinateSpace.TileLocal)
            {
                if (!TileBounds.HasValue) throw new InvalidOperationException("A TileLocal ROI requires a tile coordinate context.");
                return TranslateTile(roi.Geometry, TileBounds.Value, SourceSize);
            }

            if (roi.CoordinateSpace == RoiCoordinateSpace.World)
            {
                if (WorldTransform == null) throw new InvalidOperationException("A World ROI requires a world coordinate context.");
                if (roi.Geometry is MaskRoiGeometry worldMask) return WorldTransform.RasterizeWorldMask(worldMask, SourceSize);
                return WorldTransform.Transform(roi.Geometry);
            }

            throw new InvalidOperationException("The coordinate context does not resolve this ROI coordinate space.");
        }

        private static IVisualRoiGeometry TranslateTile(IVisualRoiGeometry geometry, RectangleF tileBounds, VisualSize sourceSize)
        {
            float offsetX = tileBounds.X;
            float offsetY = tileBounds.Y;
            if (geometry is MaskRoiGeometry mask)
            {
                if (Math.Abs(mask.SourceSize.Width - tileBounds.Width) > 0.0001f || Math.Abs(mask.SourceSize.Height - tileBounds.Height) > 0.0001f || tileBounds.X != (float)Math.Round(tileBounds.X) || tileBounds.Y != (float)Math.Round(tileBounds.Y))
                {
                    throw new NotSupportedException("A TileLocal mask requires an integer tile whose size matches the mask size.");
                }

                int width = checked((int)tileBounds.Width);
                int height = checked((int)tileBounds.Height);
                int offsetXInt = checked((int)tileBounds.X);
                int offsetYInt = checked((int)tileBounds.Y);
                var values = new byte[checked(sourceSize.Width * sourceSize.Height)];
                byte[] source = mask.ToArray();
                for (int y = 0; y < height; y++) Buffer.BlockCopy(source, y * width, values, (offsetYInt + y) * sourceSize.Width + offsetXInt, width);
                return new MaskRoiGeometry(sourceSize, values);
            }

            if (geometry is RectangleRoiGeometry rectangle)
            {
                return new RectangleRoiGeometry(new RectangleF(rectangle.Rectangle.X + offsetX, rectangle.Rectangle.Y + offsetY, rectangle.Rectangle.Width, rectangle.Rectangle.Height));
            }
            if (geometry is RotatedRectangleRoiGeometry rotated)
            {
                return new RotatedRectangleRoiGeometry(new PointF(rotated.Center.X + offsetX, rotated.Center.Y + offsetY), rotated.Size, rotated.AngleDegrees);
            }
            return new PolygonRoiGeometry(geometry.Points.Select(point => new PointF(point.X + offsetX, point.Y + offsetY)));
        }

        private static void EnsureSourceSize(VisualSize size)
        {
            if (size.Width <= 0 || size.Height <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        }

        private static void EnsureFinite(RectangleF value)
        {
            if (float.IsNaN(value.X) || float.IsInfinity(value.X) || float.IsNaN(value.Y) || float.IsInfinity(value.Y) || float.IsNaN(value.Width) || float.IsInfinity(value.Width) || float.IsNaN(value.Height) || float.IsInfinity(value.Height)) throw new ArgumentException("Tile bounds must be finite.", nameof(value));
        }
    }

    /// <summary>Represents a finite 3x3 world-to-source homography. / 表示有限的世界坐标到源像素 3×3 单应变换。</summary>
    public sealed class VisualRoiWorldTransform
    {
        /// <summary>Initializes a homography in row-major order. / 按行优先顺序初始化单应矩阵。</summary>
        public VisualRoiWorldTransform(float m11, float m12, float m13, float m21, float m22, float m23, float m31 = 0, float m32 = 0, float m33 = 1)
        {
            float[] values = { m11, m12, m13, m21, m22, m23, m31, m32, m33 };
            for (int index = 0; index < values.Length; index++) if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) throw new ArgumentOutOfRangeException(nameof(m11), "Homography coefficients must be finite.");
            double determinant = (m11 * ((double)m22 * m33 - (double)m23 * m32)) - (m12 * ((double)m21 * m33 - (double)m23 * m31)) + (m13 * ((double)m21 * m32 - (double)m22 * m31));
            if (Math.Abs(determinant) <= 1e-12) throw new ArgumentException("The world homography must be invertible.", nameof(m11));
            M11 = m11;
            M12 = m12;
            M13 = m13;
            M21 = m21;
            M22 = m22;
            M23 = m23;
            M31 = m31;
            M32 = m32;
            M33 = m33;
            double inverseDeterminant = 1d / determinant;
            _inverseM11 = (float)(((m22 * (double)m33) - (m23 * (double)m32)) * inverseDeterminant);
            _inverseM12 = (float)(((m13 * (double)m32) - (m12 * (double)m33)) * inverseDeterminant);
            _inverseM13 = (float)(((m12 * (double)m23) - (m13 * (double)m22)) * inverseDeterminant);
            _inverseM21 = (float)(((m23 * (double)m31) - (m21 * (double)m33)) * inverseDeterminant);
            _inverseM22 = (float)(((m11 * (double)m33) - (m13 * (double)m31)) * inverseDeterminant);
            _inverseM23 = (float)(((m13 * (double)m21) - (m11 * (double)m23)) * inverseDeterminant);
            _inverseM31 = (float)(((m21 * (double)m32) - (m22 * (double)m31)) * inverseDeterminant);
            _inverseM32 = (float)(((m12 * (double)m31) - (m11 * (double)m32)) * inverseDeterminant);
            _inverseM33 = (float)(((m11 * (double)m22) - (m12 * (double)m21)) * inverseDeterminant);
        }

        /// <summary>Creates a two-dimensional affine world-to-source transform. / 创建二维仿射世界坐标到源像素变换。</summary>
        public static VisualRoiWorldTransform CreateAffine(float scaleX, float shearX, float offsetX, float shearY, float scaleY, float offsetY)
        {
            return new VisualRoiWorldTransform(scaleX, shearX, offsetX, shearY, scaleY, offsetY);
        }

        /// <summary>Gets the first-row first coefficient. / 获取第一行第一系数。</summary>
        public float M11 { get; }
        /// <summary>Gets the first-row second coefficient. / 获取第一行第二系数。</summary>
        public float M12 { get; }
        /// <summary>Gets the first-row translation coefficient. / 获取第一行平移系数。</summary>
        public float M13 { get; }
        /// <summary>Gets the second-row first coefficient. / 获取第二行第一系数。</summary>
        public float M21 { get; }
        /// <summary>Gets the second-row second coefficient. / 获取第二行第二系数。</summary>
        public float M22 { get; }
        /// <summary>Gets the second-row translation coefficient. / 获取第二行平移系数。</summary>
        public float M23 { get; }
        /// <summary>Gets the third-row first coefficient. / 获取第三行第一系数。</summary>
        public float M31 { get; }
        /// <summary>Gets the third-row second coefficient. / 获取第三行第二系数。</summary>
        public float M32 { get; }
        /// <summary>Gets the third-row third coefficient. / 获取第三行第三系数。</summary>
        public float M33 { get; }

        private readonly float _inverseM11;
        private readonly float _inverseM12;
        private readonly float _inverseM13;
        private readonly float _inverseM21;
        private readonly float _inverseM22;
        private readonly float _inverseM23;
        private readonly float _inverseM31;
        private readonly float _inverseM32;
        private readonly float _inverseM33;

        /// <summary>Maps one world point to a source pixel. / 将一个世界坐标点映射到源像素。</summary>
        public PointF ToSource(PointF worldPoint)
        {
            double denominator = (M31 * worldPoint.X) + (M32 * worldPoint.Y) + M33;
            if (Math.Abs(denominator) <= 1e-12) throw new ArgumentException("The world point maps to a homography singularity.", nameof(worldPoint));
            float x = (float)(((M11 * worldPoint.X) + (M12 * worldPoint.Y) + M13) / denominator);
            float y = (float)(((M21 * worldPoint.X) + (M22 * worldPoint.Y) + M23) / denominator);
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)) throw new ArgumentException("The world point maps outside the finite source coordinate range.", nameof(worldPoint));
            return new PointF(x, y);
        }

        /// <summary>Maps one source pixel coordinate back to world space using the inverse homography. / 使用逆单应矩阵将源像素坐标映射回世界坐标。</summary>
        public PointF ToWorld(PointF sourcePoint)
        {
            if (!TryToWorld(sourcePoint, out PointF world)) throw new ArgumentException("The source point maps to a homography singularity.", nameof(sourcePoint));
            return world;
        }

        internal bool TryToWorld(PointF sourcePoint, out PointF world)
        {
            double denominator = (_inverseM31 * sourcePoint.X) + (_inverseM32 * sourcePoint.Y) + _inverseM33;
            if (Math.Abs(denominator) <= 1e-12)
            {
                world = default(PointF);
                return false;
            }
            float x = (float)(((_inverseM11 * sourcePoint.X) + (_inverseM12 * sourcePoint.Y) + _inverseM13) / denominator);
            float y = (float)(((_inverseM21 * sourcePoint.X) + (_inverseM22 * sourcePoint.Y) + _inverseM23) / denominator);
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
            {
                world = default(PointF);
                return false;
            }
            world = new PointF(x, y);
            return true;
        }

        internal bool EquivalentTo(VisualRoiWorldTransform other)
        {
            if (other == null) return false;
            return M11 == other.M11 && M12 == other.M12 && M13 == other.M13 &&
                   M21 == other.M21 && M22 == other.M22 && M23 == other.M23 &&
                   M31 == other.M31 && M32 == other.M32 && M33 == other.M33;
        }

        internal IVisualRoiGeometry Transform(IVisualRoiGeometry geometry)
        {
            if (geometry is MaskRoiGeometry) throw new NotSupportedException("A World mask requires a source-size rasterization context.");
            IReadOnlyList<PointF> points = geometry.Points.Select(ToSource).ToList();
            return new PolygonRoiGeometry(points);
        }

        internal MaskRoiGeometry RasterizeWorldMask(MaskRoiGeometry worldMask, VisualSize sourceSize)
        {
            long pixelCount = (long)sourceSize.Width * sourceSize.Height;
            if (pixelCount > 64L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(worldMask), "The rasterized source ROI mask is limited to 64 million pixels.");
            byte[] worldValues = worldMask.ToArray();
            var sourceValues = new byte[checked((int)pixelCount)];
            for (int y = 0; y < sourceSize.Height; y++)
            {
                for (int x = 0; x < sourceSize.Width; x++)
                {
                    if (!TryToWorld(new PointF(x + .5f, y + .5f), out PointF world)) continue;
                    int worldX = (int)Math.Floor(world.X);
                    int worldY = (int)Math.Floor(world.Y);
                    if (worldX < 0 || worldY < 0 || worldX >= worldMask.SourceSize.Width || worldY >= worldMask.SourceSize.Height) continue;
                    if (worldValues[(worldY * worldMask.SourceSize.Width) + worldX] != 0) sourceValues[(y * sourceSize.Width) + x] = 255;
                }
            }
            return new MaskRoiGeometry(sourceSize, sourceValues);
        }
    }
}
