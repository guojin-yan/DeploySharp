using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Binds an ONNX model identity to exact named static or runtime-dynamic OpenCV DNN ports. / 将 ONNX 模型身份绑定到精确命名的静态或运行时动态 OpenCV DNN 端口。</summary>
    public sealed class OpenCvDnnModelContract
    {
        private readonly IReadOnlyList<TensorDescriptor> _inputs;
        private readonly IReadOnlyList<TensorDescriptor> _outputs;
        private readonly IReadOnlyList<string> _imageInputNames;

        /// <summary>Initializes and validates an admitted OpenCV DNN vision contract. / 初始化并校验已准入的 OpenCV DNN 视觉合同。</summary>
        public OpenCvDnnModelContract(ModelId modelId, IEnumerable<TensorDescriptor> inputs, IEnumerable<TensorDescriptor> outputs, IEnumerable<string>? imageInputNames = null)
        {
            if (modelId.IsEmpty) throw new ArgumentException("A model identifier is required.", nameof(modelId));
            ModelId = modelId;
            var rawInputs = inputs == null ? throw new ArgumentNullException(nameof(inputs)) : inputs.ToList();
            if (rawInputs.Any(value => value == null)) throw new ArgumentException("Tensor descriptors cannot contain null values.", nameof(inputs));
            var requestedImageNames = imageInputNames == null ? rawInputs.Select(value => value.Name).ToArray() : imageInputNames.ToArray();
            var requestedImageSet = new HashSet<string>(requestedImageNames, StringComparer.Ordinal);
            _inputs = Copy(rawInputs, nameof(inputs), input: true, requestedImageSet);
            _outputs = Copy(outputs, nameof(outputs), input: false);
            if (_inputs.Count == 0) throw new ArgumentException("At least one input is required.", nameof(inputs));
            if (_outputs.Count == 0) throw new ArgumentException("At least one output is required.", nameof(outputs));
            var imageNames = requestedImageNames;
            var inputNames = new HashSet<string>(_inputs.Select(value => value.Name), StringComparer.Ordinal);
            var uniqueImageNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (string imageName in imageNames)
            {
                if (string.IsNullOrWhiteSpace(imageName) || !inputNames.Contains(imageName) || !uniqueImageNames.Add(imageName)) throw new ArgumentException("Image input names must identify unique contract inputs.", nameof(imageInputNames));
            }
            _imageInputNames = imageNames;
        }

        /// <summary>Gets the exact logical model identity. / 获取精确的逻辑模型身份。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets ordered named input descriptors. / 获取有序命名输入描述符。</summary>
        public IReadOnlyList<TensorDescriptor> Inputs => _inputs;
        /// <summary>Gets ordered named output descriptors. / 获取有序命名输出描述符。</summary>
        public IReadOnlyList<TensorDescriptor> Outputs => _outputs;

        /// <summary>Gets the inputs treated as image tensors and passed through BlobFromImage(s). / 获取会经过 BlobFromImage(s) 的图像输入。</summary>
        public IReadOnlyList<string> ImageInputNames => _imageInputNames;

        /// <summary>Returns whether a named input uses image blob conversion. / 返回命名输入是否使用图像 blob 转换。</summary>
        public bool IsImageInput(string name) => _imageInputNames.Contains(name, StringComparer.Ordinal);

        private static IReadOnlyList<TensorDescriptor> Copy(IEnumerable<TensorDescriptor> descriptors, string parameterName, bool input, ISet<string>? imageInputNames = null)
        {
            if (descriptors == null) throw new ArgumentNullException(parameterName);
            var values = new List<TensorDescriptor>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (TensorDescriptor descriptor in descriptors)
            {
                if (descriptor == null) throw new ArgumentException("Tensor descriptors cannot contain null values.", parameterName);
                if (!names.Add(descriptor.Name)) throw new ArgumentException("Tensor names must be unique.", parameterName);
                bool isImageInput = input && imageInputNames != null && imageInputNames.Contains(descriptor.Name);
                // OpenCV exposes ONNX BOOL outputs as numeric Mats on the CPU path.
                // Keep image inputs restricted to float32 NCHW images. Explicit
                // auxiliary inputs may be numeric scalars, vectors, or matrices. Outputs may
                // contain one dynamic dimension (for example YOLO candidate
                // count); the session resolves it from the returned Mat element
                // count while preserving the profile's wildcard contract. Integer
                // outputs are admitted for decoded-detector count/label ports;
                // OpenCV exposes both ONNX int32 and int64 values as CV_32S Mats
                // on the CPU path, so the session widens them without a native
                // round-trip. Int64 inputs are narrowed only after checked range
                // validation because the OpenCV Mat bridge exposes integer inputs
                // as CV_32S.
                if ((isImageInput && descriptor.ElementType != TensorElementType.Float32) || (input && !isImageInput && descriptor.ElementType != TensorElementType.Float32 && descriptor.ElementType != TensorElementType.Float64 && descriptor.ElementType != TensorElementType.Int8 && descriptor.ElementType != TensorElementType.UInt8 && descriptor.ElementType != TensorElementType.Int32 && descriptor.ElementType != TensorElementType.Int64) || (!input && descriptor.ElementType != TensorElementType.Float32 && descriptor.ElementType != TensorElementType.Float64 && descriptor.ElementType != TensorElementType.Boolean && descriptor.ElementType != TensorElementType.Int8 && descriptor.ElementType != TensorElementType.UInt8 && descriptor.ElementType != TensorElementType.Int32 && descriptor.ElementType != TensorElementType.Int64)) throw new ArgumentException("OpenCV DNN admits float32 image inputs, numeric auxiliary inputs, and float32/float64/boolean/int8/uint8/int32/int64 outputs.", parameterName);
                if (descriptor.Shape.IsDynamic)
                {
                    int dynamicDimensions = 0;
                    foreach (long dimension in descriptor.Shape.ToArray())
                    {
                        if (dimension < 0) dynamicDimensions++;
                        else if (dimension == 0) throw new ArgumentException("OpenCV DNN dynamic outputs cannot contain zero-sized fixed dimensions.", parameterName);
                    }
                    if (!input && dynamicDimensions != 1) throw new ArgumentException("OpenCV DNN outputs must contain exactly one wildcard dimension and no non-positive fixed dimensions: " + descriptor.Name + " shape=" + descriptor.Shape, parameterName);
                }
                else if (descriptor.Shape.GetElementCount() <= 0 || descriptor.Shape.GetElementCount() > int.MaxValue) throw new ArgumentException("Tensor element count must fit a managed array.", parameterName);
                if (isImageInput && (descriptor.Shape.Rank != 4 || !IsPositiveOrDynamic(descriptor.Shape[0]) || (descriptor.Shape[1] != 1 && descriptor.Shape[1] != 3 && descriptor.Shape[1] != 4) || !IsPositiveOrDynamic(descriptor.Shape[2]) || !IsPositiveOrDynamic(descriptor.Shape[3])))
                {
                    throw new ArgumentException("OpenCV DNN inputs must be NCHW float32 image tensors with positive or dynamic batch/spatial dimensions and one, three, or four channels.", parameterName);
                }
                if (input && !isImageInput && (descriptor.Shape.Rank > 2 || descriptor.Shape.ToArray().Any(value => value == 0))) throw new ArgumentException("OpenCV DNN auxiliary inputs must be scalar, one- or two-dimensional tensors with positive or dynamic dimensions.", parameterName);
                values.Add(descriptor);
            }
            return values.AsReadOnly();
        }

        private static bool IsPositiveOrDynamic(long value) => value > 0 || value == -1;
    }

    /// <summary>Controls the admitted OpenCV DNN CPU execution path. / 控制已准入的 OpenCV DNN CPU 执行路径。</summary>
    public sealed class OpenCvDnnOptions
    {
        /// <summary>Initializes options bound to one exact model contract. / 初始化绑定到一个精确模型合同的选项。</summary>
        public OpenCvDnnOptions(OpenCvDnnModelContract contract, bool enableFusion = true, bool enableWinograd = true, bool specializeDynamicInputShapes = true, int? numThreads = null)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            EnableFusion = enableFusion;
            EnableWinograd = enableWinograd;
            SpecializeDynamicInputShapes = specializeDynamicInputShapes;
            if (numThreads.HasValue && numThreads.Value <= 0) throw new ArgumentOutOfRangeException(nameof(numThreads), "OpenCV thread count must be positive when specified.");
            NumThreads = numThreads;
        }

        /// <summary>Gets the exact named tensor contract. / 获取精确命名张量合同。</summary>
        public OpenCvDnnModelContract Contract { get; }
        /// <summary>Gets whether supported OpenCV graph fusion is enabled. / 获取是否启用 OpenCV 支持的图融合。</summary>
        public bool EnableFusion { get; }
        /// <summary>Gets whether supported Winograd convolution is enabled. / 获取是否启用支持的 Winograd 卷积。</summary>
        public bool EnableWinograd { get; }
        /// <summary>Gets whether symbolic ONNX input dimensions are specialized in memory from the concrete runtime shape before OpenCV imports the graph. / 获取是否在 OpenCV 导入计算图前，根据具体运行时形状在内存中专门化 ONNX 符号输入维度。</summary>
        public bool SpecializeDynamicInputShapes { get; }
        /// <summary>Gets the optional process-global OpenCV parallel-region thread count. Null preserves the native default. / 获取可选的进程级 OpenCV 并行区域线程数；为 null 时保留原生默认值。</summary>
        public int? NumThreads { get; }
    }
}
