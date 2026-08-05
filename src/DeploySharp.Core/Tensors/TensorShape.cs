using System;
using System.Collections.Generic;
using System.Text;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Represents static or partially dynamic tensor dimensions. / 表示静态或部分动态的张量维度。
    /// </summary>
    public sealed class TensorShape : IEquatable<TensorShape>
    {
        private readonly long[] _dimensions;

        /// <summary>
        /// Initializes a shape. Use <c>-1</c> for an unresolved dynamic dimension. / 初始化形状；使用 <c>-1</c> 表示尚未解析的动态维度。
        /// </summary>
        public TensorShape(params long[] dimensions)
            : this((IEnumerable<long>)dimensions)
        {
        }

        /// <summary>
        /// Initializes a shape from dimensions. Use <c>-1</c> for an unresolved dynamic dimension. / 从维度初始化形状；使用 <c>-1</c> 表示尚未解析的动态维度。
        /// </summary>
        public TensorShape(IEnumerable<long> dimensions)
        {
            if (dimensions == null)
            {
                throw new ArgumentNullException(nameof(dimensions));
            }

            var values = new List<long>();
            foreach (long dimension in dimensions)
            {
                if (dimension < -1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(dimensions),
                        "A dimension must be non-negative or -1 for a dynamic dimension.");
                }

                values.Add(dimension);
            }

            _dimensions = values.ToArray();
        }

        /// <summary>Gets the scalar shape. / 获取标量形状。</summary>
        public static TensorShape Scalar { get; } = new TensorShape(new long[0]);

        /// <summary>Gets the number of dimensions. / 获取维度数量。</summary>
        public int Rank => _dimensions.Length;

        /// <summary>Gets a dimension by zero-based index. / 按从零开始的索引获取维度。</summary>
        public long this[int index] => _dimensions[index];

        /// <summary>Gets a value indicating whether at least one dimension is dynamic. / 获取一个值，指示是否至少有一个动态维度。</summary>
        public bool IsDynamic
        {
            get
            {
                for (int index = 0; index < _dimensions.Length; index++)
                {
                    if (_dimensions[index] < 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Calculates the element count for a fully static shape. / 计算完整静态形状的元素数量。
        /// </summary>
        public long GetElementCount()
        {
            if (IsDynamic)
            {
                throw new InvalidOperationException("A dynamic shape does not have a fixed element count.");
            }

            long count = 1;
            checked
            {
                for (int index = 0; index < _dimensions.Length; index++)
                {
                    count *= _dimensions[index];
                }
            }

            return count;
        }

        /// <summary>Returns a defensive copy of the dimensions. / 返回维度的防御性副本。</summary>
        public long[] ToArray()
        {
            return (long[])_dimensions.Clone();
        }

        /// <inheritdoc />
        /// <remarks>Compares dimensions in order. / 按顺序比较维度。</remarks>
        public bool Equals(TensorShape? other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null || Rank != other.Rank) return false;

            for (int index = 0; index < _dimensions.Length; index++)
            {
                if (_dimensions[index] != other._dimensions[index]) return false;
            }

            return true;
        }

        /// <inheritdoc />
        /// <remarks>Compares an object with this shape. / 将对象与此形状比较。</remarks>
        public override bool Equals(object? obj)
        {
            return Equals(obj as TensorShape);
        }

        /// <inheritdoc />
        /// <remarks>Computes a dimension-based hash code. / 根据维度计算哈希码。</remarks>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < _dimensions.Length; index++)
                {
                    hash = (hash * 31) + _dimensions[index].GetHashCode();
                }

                return hash;
            }
        }

        /// <inheritdoc />
        /// <remarks>Formats dimensions with brackets and commas. / 使用方括号和逗号格式化维度。</remarks>
        public override string ToString()
        {
            var builder = new StringBuilder("[");
            for (int index = 0; index < _dimensions.Length; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append(_dimensions[index]);
            }

            return builder.Append(']').ToString();
        }
    }
}
