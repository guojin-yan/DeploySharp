using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Associates a model input or output name with a runtime tensor. / 将模型输入或输出名称关联到运行时张量。
    /// </summary>
    public sealed class NamedTensor
    {
        /// <summary>Initializes a named tensor. / 初始化命名张量。</summary>
        public NamedTensor(string name, ITensor tensor)
        {
            Name = Guard.NotNullOrWhiteSpace(name, nameof(name));
            Tensor = Guard.NotNull(tensor, nameof(tensor));
        }

        /// <summary>Gets the model tensor name. / 获取模型张量名称。</summary>
        public string Name { get; }

        /// <summary>Gets the runtime tensor. / 获取运行时张量。</summary>
        public ITensor Tensor { get; }
    }
}
