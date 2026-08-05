using System;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Stores a dense typed tensor in a managed one-dimensional array. / 使用托管一维数组存储密集类型化张量。
    /// </summary>
    public sealed class Tensor<T> : ITensor
    {
        private readonly T[] _buffer;

        /// <summary>
        /// Initializes a tensor and applies the requested array ownership policy. / 初始化张量并应用请求的数组所有权策略。
        /// </summary>
        public Tensor(TensorShape shape, T[] buffer, TensorBufferOwnership ownership = TensorBufferOwnership.Copy)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (shape.IsDynamic)
            {
                throw new ArgumentException("A runtime tensor requires a fully static shape.", nameof(shape));
            }

            long expectedLength = shape.GetElementCount();
            if (expectedLength != buffer.LongLength)
            {
                throw new ArgumentException(
                    $"The shape requires {expectedLength} elements, but the buffer contains {buffer.LongLength}.",
                    nameof(buffer));
            }

            ElementType = TensorElementTypes.FromType<T>();
            Ownership = ownership;
            _buffer = ownership == TensorBufferOwnership.Copy ? (T[])buffer.Clone() : buffer;
        }

        /// <inheritdoc />
        /// <remarks>Resolves the CLR element type. / 解析 CLR 元素类型。</remarks>
        public TensorElementType ElementType { get; }

        /// <inheritdoc />
        /// <remarks>Returns the validated static shape. / 返回已验证的静态形状。</remarks>
        public TensorShape Shape { get; }

        /// <inheritdoc />
        /// <remarks>Returns the backing array length. / 返回底层数组长度。</remarks>
        public long Length => _buffer.LongLength;

        /// <inheritdoc />
        /// <remarks>Returns the constructor ownership policy. / 返回构造时指定的所有权策略。</remarks>
        public TensorBufferOwnership Ownership { get; }

        /// <inheritdoc />
        /// <remarks>Exposes the backing array for explicit adapter use. / 为显式适配器使用公开底层数组。</remarks>
        public Array Buffer => _buffer;

        /// <summary>
        /// Returns a defensive typed copy of the tensor values. / 返回张量值的类型化防御性副本。
        /// </summary>
        public T[] ToArray()
        {
            return (T[])_buffer.Clone();
        }
    }
}
