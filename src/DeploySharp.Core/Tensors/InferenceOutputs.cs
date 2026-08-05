using System.Collections.Generic;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Represents ordered, uniquely named inference outputs. / 表示有序且名称唯一的推理输出。
    /// </summary>
    public sealed class InferenceOutputs : NamedTensorCollection
    {
        /// <summary>Initializes an output collection. / 初始化输出集合。</summary>
        public InferenceOutputs(IEnumerable<NamedTensor> tensors)
            : base(tensors)
        {
        }

        /// <summary>Creates a single-output collection. / 创建单输出集合。</summary>
        public static InferenceOutputs Create(string name, ITensor tensor)
        {
            return new InferenceOutputs(new[] { new NamedTensor(name, tensor) });
        }
    }
}
