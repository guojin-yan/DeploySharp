using System;

namespace JYPPX.DeploySharp.Geometry
{
    /// <summary>
    /// Represents an axis-aligned single-precision rectangle. / 表示轴对齐的单精度矩形。
    /// </summary>
    public readonly struct RectangleF : IEquatable<RectangleF>
    {
        /// <summary>Initializes a rectangle. / 初始化矩形。</summary>
        public RectangleF(float x, float y, float width, float height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>Gets the left coordinate. / 获取左侧坐标。</summary>
        public float X { get; }

        /// <summary>Gets the top coordinate. / 获取顶部坐标。</summary>
        public float Y { get; }

        /// <summary>Gets the width. / 获取宽度。</summary>
        public float Width { get; }

        /// <summary>Gets the height. / 获取高度。</summary>
        public float Height { get; }

        /// <summary>Gets the right coordinate. / 获取右侧坐标。</summary>
        public float Right => X + Width;

        /// <summary>Gets the bottom coordinate. / 获取底部坐标。</summary>
        public float Bottom => Y + Height;

        /// <inheritdoc />
        /// <remarks>Compares rectangle components exactly. / 精确比较矩形分量。</remarks>
        public bool Equals(RectangleF other)
        {
            return X.Equals(other.X)
                && Y.Equals(other.Y)
                && Width.Equals(other.Width)
                && Height.Equals(other.Height);
        }

        /// <inheritdoc />
        /// <remarks>Compares an object with this rectangle. / 将对象与此矩形比较。</remarks>
        public override bool Equals(object? obj)
        {
            return obj is RectangleF other && Equals(other);
        }

        /// <inheritdoc />
        /// <remarks>Computes a hash from all rectangle components. / 根据全部矩形分量计算哈希码。</remarks>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Width.GetHashCode();
                return (hash * 397) ^ Height.GetHashCode();
            }
        }

        /// <summary>Compares two rectangles for equality. / 比较两个矩形是否相等。</summary>
        public static bool operator ==(RectangleF left, RectangleF right) => left.Equals(right);

        /// <summary>Compares two rectangles for inequality. / 比较两个矩形是否不相等。</summary>
        public static bool operator !=(RectangleF left, RectangleF right) => !left.Equals(right);
    }
}
