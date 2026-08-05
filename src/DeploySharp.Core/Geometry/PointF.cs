using System;

namespace JYPPX.DeploySharp.Geometry
{
    /// <summary>
    /// Represents a two-dimensional single-precision point without an imaging dependency. / 表示不依赖图像库的二维单精度点。
    /// </summary>
    public readonly struct PointF : IEquatable<PointF>
    {
        /// <summary>Initializes a point. / 初始化点。</summary>
        public PointF(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Gets the horizontal coordinate. / 获取横坐标。</summary>
        public float X { get; }

        /// <summary>Gets the vertical coordinate. / 获取纵坐标。</summary>
        public float Y { get; }

        /// <inheritdoc />
        /// <remarks>Compares point coordinates exactly. / 精确比较点坐标。</remarks>
        public bool Equals(PointF other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        /// <inheritdoc />
        /// <remarks>Compares an object with this point. / 将对象与此点比较。</remarks>
        public override bool Equals(object? obj)
        {
            return obj is PointF other && Equals(other);
        }

        /// <inheritdoc />
        /// <remarks>Computes a hash from both coordinates. / 根据两个坐标计算哈希码。</remarks>
        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        /// <summary>Compares two points for equality. / 比较两个点是否相等。</summary>
        public static bool operator ==(PointF left, PointF right) => left.Equals(right);

        /// <summary>Compares two points for inequality. / 比较两个点是否不相等。</summary>
        public static bool operator !=(PointF left, PointF right) => !left.Equals(right);
    }
}
