using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Internal;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Models
{
    /// <summary>
    /// Contains immutable model information discovered by an inference session. / 包含推理会话发现的不可变模型信息。
    /// </summary>
    public sealed class ModelMetadata
    {
        private readonly IReadOnlyList<TensorDescriptor> _inputs;
        private readonly IReadOnlyList<TensorDescriptor> _outputs;

        /// <summary>Initializes model metadata. / 初始化模型元数据。</summary>
        public ModelMetadata(
            ModelId modelId,
            string format,
            IEnumerable<TensorDescriptor> inputs,
            IEnumerable<TensorDescriptor> outputs)
        {
            if (modelId.IsEmpty)
            {
                throw new ArgumentException("A model identifier is required.", nameof(modelId));
            }

            ModelId = modelId;
            Format = Guard.Identifier(format, nameof(format));
            _inputs = CopyDescriptors(inputs, nameof(inputs));
            _outputs = CopyDescriptors(outputs, nameof(outputs));
        }

        /// <summary>Gets the logical model identifier. / 获取逻辑模型标识符。</summary>
        public ModelId ModelId { get; }

        /// <summary>Gets the normalized model format. / 获取规范化模型格式。</summary>
        public string Format { get; }

        /// <summary>Gets the ordered model inputs. / 获取有序模型输入。</summary>
        public IReadOnlyList<TensorDescriptor> Inputs => _inputs;

        /// <summary>Gets the ordered model outputs. / 获取有序模型输出。</summary>
        public IReadOnlyList<TensorDescriptor> Outputs => _outputs;

        private static IReadOnlyList<TensorDescriptor> CopyDescriptors(
            IEnumerable<TensorDescriptor> descriptors,
            string parameterName)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var values = new List<TensorDescriptor>();
            foreach (TensorDescriptor descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    throw new ArgumentException("Tensor descriptors cannot contain null values.", parameterName);
                }

                values.Add(descriptor);
            }

            return values.AsReadOnly();
        }
    }
}
