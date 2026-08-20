using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Binds an ONNX model identity to exact named static OpenCV DNN ports. / 将 ONNX 模型身份绑定到精确命名的静态 OpenCV DNN 端口。</summary>
    public sealed class OpenCvDnnModelContract
    {
        private readonly IReadOnlyList<TensorDescriptor> _inputs;
        private readonly IReadOnlyList<TensorDescriptor> _outputs;

        /// <summary>Initializes and validates an admitted OpenCV DNN vision contract. / 初始化并校验已准入的 OpenCV DNN 视觉合同。</summary>
        public OpenCvDnnModelContract(ModelId modelId, IEnumerable<TensorDescriptor> inputs, IEnumerable<TensorDescriptor> outputs)
        {
            if (modelId.IsEmpty) throw new ArgumentException("A model identifier is required.", nameof(modelId));
            ModelId = modelId;
            _inputs = Copy(inputs, nameof(inputs), input: true);
            _outputs = Copy(outputs, nameof(outputs), input: false);
            if (_inputs.Count == 0) throw new ArgumentException("At least one input is required.", nameof(inputs));
            if (_outputs.Count == 0) throw new ArgumentException("At least one output is required.", nameof(outputs));
        }

        /// <summary>Gets the exact logical model identity. / 获取精确的逻辑模型身份。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets ordered named input descriptors. / 获取有序命名输入描述符。</summary>
        public IReadOnlyList<TensorDescriptor> Inputs => _inputs;
        /// <summary>Gets ordered named output descriptors. / 获取有序命名输出描述符。</summary>
        public IReadOnlyList<TensorDescriptor> Outputs => _outputs;

        private static IReadOnlyList<TensorDescriptor> Copy(IEnumerable<TensorDescriptor> descriptors, string parameterName, bool input)
        {
            if (descriptors == null) throw new ArgumentNullException(parameterName);
            var values = new List<TensorDescriptor>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (TensorDescriptor descriptor in descriptors)
            {
                if (descriptor == null) throw new ArgumentException("Tensor descriptors cannot contain null values.", parameterName);
                if (!names.Add(descriptor.Name)) throw new ArgumentException("Tensor names must be unique.", parameterName);
                if (descriptor.ElementType != TensorElementType.Float32 || descriptor.Shape.IsDynamic) throw new ArgumentException("OpenCV DNN v1 admits only static float32 tensors.", parameterName);
                if (descriptor.Shape.GetElementCount() <= 0 || descriptor.Shape.GetElementCount() > int.MaxValue) throw new ArgumentException("Tensor element count must fit a managed array.", parameterName);
                if (input && (descriptor.Shape.Rank != 4 || descriptor.Shape[0] != 1 || (descriptor.Shape[1] != 1 && descriptor.Shape[1] != 3 && descriptor.Shape[1] != 4) || descriptor.Shape[2] <= 0 || descriptor.Shape[3] <= 0))
                {
                    throw new ArgumentException("OpenCV DNN v1 inputs must be static NCHW float32 image tensors with batch one and one, three, or four channels.", parameterName);
                }
                values.Add(descriptor);
            }
            return values.AsReadOnly();
        }
    }

    /// <summary>Controls the admitted OpenCV DNN CPU execution path. / 控制已准入的 OpenCV DNN CPU 执行路径。</summary>
    public sealed class OpenCvDnnOptions
    {
        /// <summary>Initializes options bound to one exact model contract. / 初始化绑定到一个精确模型合同的选项。</summary>
        public OpenCvDnnOptions(OpenCvDnnModelContract contract, bool enableFusion = true, bool enableWinograd = true)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            EnableFusion = enableFusion;
            EnableWinograd = enableWinograd;
        }

        /// <summary>Gets the exact named tensor contract. / 获取精确命名张量合同。</summary>
        public OpenCvDnnModelContract Contract { get; }
        /// <summary>Gets whether supported OpenCV graph fusion is enabled. / 获取是否启用 OpenCV 支持的图融合。</summary>
        public bool EnableFusion { get; }
        /// <summary>Gets whether supported Winograd convolution is enabled. / 获取是否启用支持的 Winograd 卷积。</summary>
        public bool EnableWinograd { get; }
    }
}
