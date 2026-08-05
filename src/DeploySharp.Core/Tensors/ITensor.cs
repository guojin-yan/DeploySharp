using System;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Provides a backend-neutral view of a dense managed tensor. / 提供密集托管张量的后端无关视图。
    /// </summary>
    public interface ITensor
    {
        /// <summary>Gets the tensor element type. / 获取张量元素类型。</summary>
        public TensorElementType ElementType { get; }

        /// <summary>Gets the tensor shape. / 获取张量形状。</summary>
        public TensorShape Shape { get; }

        /// <summary>Gets the number of elements in the tensor. / 获取张量元素数量。</summary>
        public long Length { get; }

        /// <summary>Gets how the managed array is owned. / 获取托管数组的所有权方式。</summary>
        public TensorBufferOwnership Ownership { get; }

        /// <summary>
        /// Gets the underlying managed array for backend adaptation. / 获取用于后端适配的底层托管数组。
        /// </summary>
        public Array Buffer { get; }
    }
}
