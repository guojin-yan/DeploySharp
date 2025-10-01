using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Configuration interface for AI model deployment and inference parameters
    /// AI模型部署和推理参数的配置接口
    /// </summary>
    /// <remarks>
    /// <para>
    /// Centralizes all model-related configuration including paths, dimensions,
    /// hardware targets, and runtime parameters. Designed for flexibility across
    /// different model formats (ONNX/TensorFlow/etc) and inference backends.
    /// </para>
    /// <para>
    /// 集中所有与模型相关的配置，包括路径、维度、硬件目标和运行时参数。
    /// 设计用于跨不同模型格式（ONNX/TensorFlow等）和推理后端的灵活性。
    /// </para>
    /// <example>
    /// Basic YOLOv8 configuration:
    /// <code>
    /// var config = new IConfig()
    /// {
    ///     ModelPath = "Models/yolov8n.onnx",
    ///     ModelType = ModelType.ObjectDetection,
    ///     InputNames = new List<string> { "images" },
    ///     InputSizes = new List<int[]> { new[]{1, 3, 640, 640} },
    ///     OutputNames = new List<string> { "output0" },
    ///     CategoryDict = new Dictionary<int, string>
    ///     {
    ///         {0, "person"}, {1, "car"}
    ///     }
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    public class IConfig
    {
        /// <summary>
        /// Model file path (supports ONNX/TensorFlow/etc formats)
        /// 模型文件路径（支持ONNX/TensorFlow等格式）
        /// </summary>
        /// <value>
        /// Relative or absolute path to the model file.
        /// Example: @"Models/yolov8n.onnx"
        /// 模型文件的相对或绝对路径。
        /// 示例：@"Models/yolov8n.onnx"
        /// </value>
        public string ModelPath { get; set; }

        /// <summary>
        /// Whether the model expects dynamic input shapes
        /// 模型是否接受动态输入形状
        /// </summary>
        /// <value>
        /// True for models that can handle varying input dimensions.
        /// Default: false.
        /// 对于可以处理不同输入维度的模型为true。
        /// 默认值：false。
        /// </value>
        public bool DynamicInput { get; set; } = false;

        /// <summary>
        /// Whether the model produces dynamic output shapes
        /// 模型是否产生动态输出形状
        /// </summary>
        /// <value>
        /// True for models with variable-sized outputs.
        /// Default: false.
        /// 对于输出大小可变的模型为true。
        /// 默认值：false。
        /// </value>
        public bool DynamicOutput { get; set; } = false;

        /// <summary>
        /// Type of the AI model (classification/detection/etc)
        /// AI模型类型（分类/检测等）
        /// </summary>
        public ModelType ModelType { get; set; }

        /// <summary>
        /// Dictionary mapping class IDs to human-readable names
        /// 将类别ID映射到可读名称的字典
        /// </summary>
        /// <value>
        /// Key: class ID, Value: class name.
        /// Example: {{0,"person"}, {1,"car"}}
        /// 键：类别ID，值：类别名称。
        /// 示例：{{0,"人"}, {1,"汽车"}}
        /// </value>
        public Dictionary<int, string> CategoryDict { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// Input tensor names (ordered list for multi-input models)
        /// 输入张量名称（多输入模型的顺序列表）
        /// </summary>
        /// <value>
        /// Ordered list matching the model's expected inputs.
        /// Example: new List<string>{ "images" }
        /// 匹配模型预期输入的顺序列表。
        /// 示例：new List<string>{ "images" }
        /// </value>
        public List<string> InputNames { get; set; } = new List<string>();

        /// <summary>
        /// Default inference batch size (for dynamic input models)
        /// 默认推理批量大小（用于动态输入模型）
        /// </summary>
        /// <value>
        /// Batch size dimension value.
        /// Default: 1.
        /// 批量大小的维度值。
        /// 默认值：1。
        /// </value>
        public int InferBatch { get; set; } = 1;

        /// <summary>
        /// Input tensor dimensions (required for dynamic models)
        /// 输入张量维度（动态模型必填）
        /// </summary>
        /// <value>
        /// List of dimension arrays matching the input names.
        /// Example: new List<int[]>{ new[]{1, 3, 640, 640} }
        /// 匹配输入名称的维度数组列表。
        /// 示例：new List<int[]>{ new[]{1, 3, 640, 640} }
        /// </value>
        public List<int[]> InputSizes { get; set; } = new List<int[]>();
 
        /// <summary>
        /// Output tensor names
        /// 输出张量名称
        /// </summary>
        public List<string> OutputNames { get; set; } = new List<string>();

        /// <summary>
        /// Output tensor dimensions
        /// 输出张量维度
        /// </summary>
        /// <value>
        /// List of dimension arrays matching the output names.
        /// Example: new List<int[]>{ new[]{1, 84, 8400} }
        /// 匹配输出名称的维度数组列表。
        /// 示例：new List<int[]>{ new[]{1, 84, 8400} }
        /// </value>
        public List<int[]> OutputSizes { get; set; } = new List<int[]>();

        /// <summary>
        /// Target inference backend (OpenVINO/TensorRT/ONNXRuntime/etc)
        /// 目标推理后端（OpenVINO/TensorRT/ONNXRuntime等）
        /// </summary>
        /// <value>
        /// Default: OpenVINO
        /// 默认值：OpenVINO
        /// </value>
        public InferenceBackend TargetInferenceBackend { get; set; } = InferenceBackend.OpenVINO;

        /// <summary>
        /// Target execution device (CPU/GPU/NPU)
        /// 目标执行设备（CPU/GPU/NPU）
        /// </summary>
        /// <value>
        /// Default: CPU
        /// 默认值：CPU
        /// </value>
        public DeviceType TargetDeviceType { get; set; } = DeviceType.CPU;

        /// <summary>
        /// ONNXRuntime specific device type
        /// ONNXRuntime专用设备类型
        /// </summary>
        /// <value>
        /// Uses CUDA for GPU, default API for CPU by default
        /// GPU默认使用CUDA，CPU默认使用基础API
        /// </value>
        public OnnxRuntimeDeviceType TargetOnnxRuntimeDeviceType { get; set; } = OnnxRuntimeDeviceType.Default;

        /// <summary>
        /// Computation precision mode (FP32/FP16/INT8)
        /// 计算精度模式（FP32/FP16/INT8）
        /// </summary>
        /// <value>
        /// Affects model accuracy and inference speed.
        /// Default: "FP32".
        /// 影响模型精度和推理速度。
        /// 默认值："FP32"。
        /// </value>
        public string PrecisionMode { get; set; } = "FP32";

        /// <summary>
        /// Maximum batch size capacity
        /// 最大批处理大小
        /// </summary>
        /// <value>
        /// Impacts memory usage and throughput.
        /// Default: 1.
        /// 影响内存占用和吞吐量。
        /// 默认值：1。
        /// </value>
        public int MaxBatchSize { get; set; } = 1;

        /// <summary>
        /// Whether GPU acceleration is enabled
        /// 是否启用GPU加速
        /// </summary>
        /// <value>
        /// Derived from TargetDeviceType setting.
        /// Requires compatible backend support.
        /// 从TargetDeviceType设置派生。
        /// 需要兼容的后端支持。
        /// </value>
        public bool UseGPU
        {
            get { return TargetDeviceType == DeviceType.GPU1 || TargetDeviceType == DeviceType.GPU0; }
        }

        /// <summary>
        /// Number of CPU threads for inference
        /// CPU推理线程数
        /// </summary>
        /// <value>
        /// Uses all logical cores by default.
        /// 默认使用所有逻辑核心。
        /// </value>
        public int NumThreads { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Sets the model path using fluent interface pattern
        /// 使用流式接口模式设置模型路径
        /// </summary>
        /// <param name="path">Model file path 模型文件路径</param>
        /// <returns>Current config instance for chaining 当前配置实例用于链式调用</returns>
        public IConfig SetModelPath(string path)
        {
            ModelPath = path;
            return this;
        }

        /// <summary>
        /// Sets the target device type using fluent interface
        /// 使用流式接口设置目标设备类型
        /// </summary>
        /// <param name="targetDeviceType">Device type 设备类型</param>
        /// <returns>Current config instance 当前配置实例</returns>
        public IConfig SetTargetDeviceType(DeviceType targetDeviceType)
        {
            TargetDeviceType = targetDeviceType;
            return this;
        }

        /// <summary>
        /// Sets the inference backend using fluent interface
        /// 使用流式接口设置推理后端
        /// </summary>
        /// <param name="inferenceBackend">Inference backend 推理后端</param>
        /// <returns>Current config instance 当前配置实例</returns>
        public IConfig SetTargetInferenceBackend(InferenceBackend inferenceBackend)
        {
            TargetInferenceBackend = inferenceBackend;
            return this;
        }

        /// <summary>
        /// Sets ONNXRuntime-specific device type
        /// 设置ONNXRuntime专用设备类型
        /// </summary>
        /// <param name="onnxRuntimeDeviceType">Device type 设备类型</param>
        /// <returns>Current config instance 当前配置实例</returns>
        public IConfig SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType onnxRuntimeDeviceType)
        {
            TargetOnnxRuntimeDeviceType = onnxRuntimeDeviceType;
            return this;
        }

        /// <summary>
        /// Generates detailed configuration summary string
        /// 生成详细的配置摘要字符串
        /// </summary>
        /// <returns>Formatted configuration details 格式化的配置详情</returns>
        public override string ToString() 
        {
            var sb = new StringBuilder("Model Configuration:\n");
            AppendIfSet(sb, "ModelType", ModelType);
            
            // 只显示非空/非默认值的属性
            AppendIfSet(sb, "Path", ModelPath);

            AppendIfSet(sb, "TargetInferenceBackend", $"{TargetInferenceBackend.GetDisplayName()}");
            AppendIfSet(sb, "TargetDeviceType", $"{TargetDeviceType.GetDisplayName()} ({PrecisionMode})");
            AppendIfSet(sb, "TargetOnnxRuntimeDeviceType", $"{TargetOnnxRuntimeDeviceType.GetDisplayName()}");
            AppendIfSet(sb, "InferBatch", $"{InferBatch}");

            if (InputNames?.Count > 0)
            {
                sb.AppendLine("\n- Inputs:");
                for (int i = 0; i < InputNames.Count; i++)
                {
                    sb.AppendLine($"  {i + 1}. Name: {InputNames[i]}, " +
                        $"Size: {(InputSizes.Count > i ?
                            (InputSizes[i] != null ? string.Join(",", InputSizes[i]) : "Dynamic") : "NotSet")}");
                }
            }

            if (OutputNames?.Count > 0)
            {
                sb.AppendLine("- Outputs:");
                for (int i = 0; i < OutputNames.Count; i++)
                {
                    sb.AppendLine($"  {i + 1}. Name: {OutputNames[i]}, " +
                        $"Size: {(OutputSizes.Count > i ?
                            (OutputSizes[i] != null ? string.Join(",", OutputSizes[i]) : "Dynamic") : "NotSet")}");
                }
            }

            sb.AppendLine("Category Dict:" + $" {(CategoryDict.Count > 0 ? (string.Join(",", CategoryDict.Select(p => $"{p.Key}: '{p.Value}'"))) : "NotSet")}");
            AppendIfSet(sb, "Max Batch Size", MaxBatchSize, 1);
            AppendIfSet(sb, "GPU Enabled", UseGPU, false);
            AppendIfSet(sb, "Threads", NumThreads, Environment.ProcessorCount);

            return sb.ToString();
        }
        /// <summary>
        /// Helper method for conditional string building
        /// 用于条件字符串构建的辅助方法
        /// </summary>
        /// <typeparam name="T">Value type 值类型</typeparam>
        /// <param name="builder">StringBuilder instance StringBuilder实例</param>
        /// <param name="name">Property name 属性名称</param>
        /// <param name="value">Current value 当前值</param>
        /// <param name="defaultValue">Default value to compare against 用于比较的默认值</param>
        public void AppendIfSet<T>(StringBuilder builder, string name, T value, T defaultValue = default)
        {
            if (!EqualityComparer<T>.Default.Equals(value, defaultValue) && value != null)
            {
                builder.AppendLine($"- {name}: {value}");
            }
        }
    }
}
