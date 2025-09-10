using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public class IConfig
    {

        // 模型文件路径
        /// <summary>
        /// 模型文件路径（支持ONNX/TensorFlow等格式）
        /// 示例：@"Models/yolov8n.onnx"
        /// </summary>
        public string ModelPath { get; set; }

        /// <summary>
        /// 是否为动态输入模型，根据具体模型配置
        /// </summary>
        public bool DynamicInput { get; set; } = false;

        /// <summary>
        /// 是否为动态输出模型，根据具体模型配置
        /// </summary>
        public bool DynamicOutput { get; set; } = false;

        // 输入参数配置
        /// <summary>
        /// 输入张量名称列表（多输入模型需要按顺序指定）
        /// 示例：new List<string>{ "images" }
        /// </summary>
        public List<string> InputNames { get; set; } = new List<string>();


        /// <summary>
        /// 模型默认推理Batch，适用于动态输入模型
        /// 示例：new List<int[]>{ new[]{1, 3, 640, 640} }
        /// </summary>
        public int InferBatch { get; set; } = 1;


        /// <summary>
        /// 输入张量维度（动态模型此项必填）
        /// 示例：new List<int[]>{ new[]{1, 3, 640, 640} }
        /// </summary>
        public List<int[]> InputSizes { get; set; } = new List<int[]>();

        // 输出参数配置
        /// <summary>
        /// 输出张量名称列表
        /// 示例：new List<string>{ "output0" }
        /// </summary>
        public List<string> OutputNames { get; set; } = new List<string>();

        /// <summary>
        /// 输出张量维度
        /// 示例：new List<int[]>{ new[]{1, 84, 8400} }
        /// </summary>
        public List<int[]> OutputSizes { get; set; } = new List<int[]>();

        // 硬件配置
        /// <summary>
        /// 目标推理后端
        /// 默认使用OpenVINO
        /// </summary>
        public InferenceBackend TargetInferenceBackend { get; set; } = InferenceBackend.OpenVINO;
        /// <summary>
        /// 目标推理平台（CPU/GPU/NPU）
        /// 默认使用CPU
        /// </summary>
        public DeviceType TargetDeviceType { get; set; } = DeviceType.CPU;

        /// <summary>
        ///  OnnxRuntime目标推理平台
        /// CPU默认使用基础API，GPU默认使用Cuda
        /// </summary>
        public OnnxRuntimeDeviceType TargetOnnxRuntimeDeviceType { get; set; } = OnnxRuntimeDeviceType.Default;

        /// <summary>
        /// 计算精度模式（FP32/FP16/INT8）
        /// 影响模型精度和推理速度
        /// </summary>
        public string PrecisionMode { get; set; } = "FP32";

        /// <summary>
        /// 最大批处理大小（默认1）
        /// 影响内存占用和吞吐量
        /// </summary>
        public int MaxBatchSize { get; set; } = 1;

        /// <summary>
        /// 是否启用GPU加速（默认false）
        /// 需要对应后端支持
        /// </summary>
        public bool UseGPU { get; set; } = false;

        /// <summary>
        /// CPU推理线程数（默认使用全部逻辑核心）
        /// </summary>
        public int NumThreads { get; set; } = Environment.ProcessorCount;

        public void SetModelPath(string path)
        {
            ModelPath = path;
        }

        public void SetTargetDeviceType(DeviceType targetDeviceType)
        {
            TargetDeviceType = targetDeviceType;
        }

        public void SetTargetInferenceBackend(InferenceBackend inferenceBackend)
        {
            TargetInferenceBackend = inferenceBackend;
        }
        public void SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType onnxRuntimeDeviceType)
        {
            TargetOnnxRuntimeDeviceType = onnxRuntimeDeviceType;
        }
        public override string ToString() 
        {
            var sb = new StringBuilder("Model Configuration:");

            // 只显示非空/非默认值的属性
            AppendIfSet(sb, "Path", ModelPath);

            AppendIfSet(sb, "TargetDeviceType", $"{TargetInferenceBackend.GetDisplayName()}");
            AppendIfSet(sb, "TargetDeviceType", $"{TargetDeviceType.GetDisplayName()} ({PrecisionMode})");

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


            AppendIfSet(sb, "Max Batch Size", MaxBatchSize, 1);
            AppendIfSet(sb, "GPU Enabled", UseGPU);
            AppendIfSet(sb, "Threads", NumThreads, Environment.ProcessorCount);

            return sb.ToString();
        }

        public void AppendIfSet<T>(StringBuilder builder, string name, T value, T defaultValue = default)
        {
            if (!EqualityComparer<T>.Default.Equals(value, defaultValue) && value != null)
            {
                builder.AppendLine($"- {name}: {value}");
            }
        }
    }
}
