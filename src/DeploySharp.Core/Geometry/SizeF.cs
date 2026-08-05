using System;

namespace JYPPX.DeploySharp.Geometry
{
    /// <summary>
    /// Represents a non-negative two-dimensional single-precision size. / 表示非负的二维单精度尺寸。
    /// </summary>
    public readonly struct SizeF : IEquatable<SizeF>
    {
        /// <summary>Initializes a size. / 初始化尺寸。</summary>
        public SizeF(float width, float height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }

        /// <summary>Gets the width. / 获取宽度。</summary>
        public float Width { get; }

        /// <summary>Gets the height. / 获取高度。</summary>
        public float Height { get; }

        /// <inheritdoc />
        /// <remarks>Compares size components exactly. / 精确比较尺寸分量。</remarks>
        public bool Equals(SizeF other)
        {
            return Width.Equals(other.Width) && Height.Equals(other.Height);
        }

        /// <inheritdoc />
        /// <remarks>Compares an object with this size. / 将对象与此尺寸比较。</remarks>
        public override bool Equals(object? obj)
        {
            return obj is SizeF other && Equals(other);
        }

        /// <inheritdoc />
        /// <remarks>Computes a hash from width and height. / 根据宽度和高度计算哈希码。</remarks>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Width.GetHashCode() * 397) ^ Height.GetHashCode();
            }
        }

        /// <summary>Compares two sizes for equality. / 比较两个尺寸是否相等。</summary>
        public static bool operator ==(SizeF left, SizeF right) => left.Equals(right);

        /// <summary>Compares two sizes for inequality. / 比较两个尺寸是否不相等。</summary>
        public static bool operator !=(SizeF left, SizeF right) => !left.Equals(right);
    }
}
