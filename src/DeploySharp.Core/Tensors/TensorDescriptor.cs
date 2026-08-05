using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Describes a named model input or output without carrying runtime data. / 描述命名模型输入或输出，但不携带运行时数据。
    /// </summary>
    public sealed class TensorDescriptor
    {
        /// <summary>Initializes a tensor descriptor. / 初始化张量描述信息。</summary>
        public TensorDescriptor(string name, TensorElementType elementType, TensorShape shape)
        {
            Name = Guard.NotNullOrWhiteSpace(name, nameof(name));
            ElementType = elementType;
            Shape = Guard.NotNull(shape, nameof(shape));
        }

        /// <summary>Gets the model tensor name. / 获取模型张量名称。</summary>
        public string Name { get; }

        /// <summary>Gets the declared element type. / 获取声明的元素类型。</summary>
        public TensorElementType ElementType { get; }

        /// <summary>Gets the declared static or dynamic shape. / 获取声明的静态或动态形状。</summary>
        public TensorShape Shape { get; }
    }
}
