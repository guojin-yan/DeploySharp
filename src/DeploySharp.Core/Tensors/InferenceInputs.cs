using System.Collections.Generic;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Represents ordered, uniquely named inference inputs. / 表示有序且名称唯一的推理输入。
    /// </summary>
    public sealed class InferenceInputs : NamedTensorCollection
    {
        /// <summary>Initializes an input collection. / 初始化输入集合。</summary>
        public InferenceInputs(IEnumerable<NamedTensor> tensors)
            : base(tensors)
        {
        }

        private InferenceInputs(string name, ITensor tensor)
            : base(name, tensor)
        {
        }

        /// <summary>Creates a single-input collection. / 创建单输入集合。</summary>
        public static InferenceInputs Create(string name, ITensor tensor)
        {
            return new InferenceInputs(name, tensor);
        }
    }
}
