using System;

namespace JYPPX.DeploySharp.Geometry
{
    /// <summary>
    /// Represents a rotated rectangle using center, size, and clockwise degrees. / 使用中心点、尺寸和顺时针角度表示旋转矩形。
    /// </summary>
    public readonly struct RotatedRectangleF : IEquatable<RotatedRectangleF>
    {
        /// <summary>Initializes a rotated rectangle. / 初始化旋转矩形。</summary>
        public RotatedRectangleF(PointF center, SizeF size, float angleDegrees)
        {
            Center = center;
            Size = size;
            AngleDegrees = angleDegrees;
        }

        /// <summary>Gets the center. / 获取中心点。</summary>
        public PointF Center { get; }

        /// <summary>Gets the unrotated size. / 获取未旋转尺寸。</summary>
        public SizeF Size { get; }

        /// <summary>Gets the clockwise angle in degrees. / 获取以度为单位的顺时针角度。</summary>
        public float AngleDegrees { get; }

        /// <inheritdoc />
        /// <remarks>Compares all rotated rectangle components exactly. / 精确比较旋转矩形的全部分量。</remarks>
        public bool Equals(RotatedRectangleF other)
        {
            return Center.Equals(other.Center)
                && Size.Equals(other.Size)
                && AngleDegrees.Equals(other.AngleDegrees);
        }

        /// <inheritdoc />
        /// <remarks>Compares an object with this rotated rectangle. / 将对象与此旋转矩形比较。</remarks>
        public override bool Equals(object? obj)
        {
            return obj is RotatedRectangleF other && Equals(other);
        }

        /// <inheritdoc />
        /// <remarks>Computes a hash from center, size, and angle. / 根据中心点、尺寸和角度计算哈希码。</remarks>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Center.GetHashCode();
                hash = (hash * 397) ^ Size.GetHashCode();
                return (hash * 397) ^ AngleDegrees.GetHashCode();
            }
        }

        /// <summary>Compares two rotated rectangles for equality. / 比较两个旋转矩形是否相等。</summary>
        public static bool operator ==(RotatedRectangleF left, RotatedRectangleF right) => left.Equals(right);

        /// <summary>Compares two rotated rectangles for inequality. / 比较两个旋转矩形是否不相等。</summary>
        public static bool operator !=(RotatedRectangleF left, RotatedRectangleF right) => !left.Equals(right);
    }
}
