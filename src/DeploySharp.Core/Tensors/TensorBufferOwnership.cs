namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Describes how a managed tensor uses an array supplied by the caller. / 描述托管张量如何使用调用方提供的数组。
    /// </summary>
    public enum TensorBufferOwnership
    {
        /// <summary>The tensor stores its own defensive copy. / 张量保存自己的防御性副本。</summary>
        Copy = 0,

        /// <summary>The caller transfers exclusive ownership of the array to the tensor. / 调用方将数组的独占所有权转移给张量。</summary>
        Transfer = 1,

        /// <summary>The tensor borrows the array and the caller must keep it unchanged and alive. / 张量借用数组，调用方必须保持数组不变且存活。</summary>
        Borrow = 2
    }
}
