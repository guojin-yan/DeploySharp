using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Engine
{

    /// <summary>
    /// Represents software inference backends supported by the system.
    /// 表示系统支持的软件推理后端
    /// </summary>
    /// <remarks>
    /// Each backend provides optimized execution for specific hardware configurations.
    /// 每种后端为特定的硬件配置提供优化执行
    /// </remarks>
    public enum InferenceBackend
    {
        /// <summary>
        /// Intel's OpenVINO toolkit
        /// Optimized for Intel CPUs, integrated GPUs and VPUs
        /// 英特尔OpenVINO工具套件
        /// 针对Intel CPU、集成GPU和VPU优化
        /// </summary>
        [DisplayName("OpenVINO")]
        OpenVINO = 0,

        /// <summary>
        /// Microsoft's ONNX Runtime
        /// Cross-platform inference accelerator with multiple execution providers
        /// 微软ONNX运行时
        /// 跨平台推理加速器，支持多执行提供程序
        /// </summary>
        [DisplayName("OnnxRuntime")]
        OnnxRuntime = 1,

        /// <summary>
        /// NVIDIA's TensorRT
        /// High-performance deep learning inference optimizer and runtime
        /// 英伟达TensorRT
        /// 高性能深度学习推理优化器和运行时
        /// </summary>
        [DisplayName("TensorRT")]
        TensorRT = 2,
    }

    /// <summary>
    /// Represents hardware device types available for inference operations.
    /// 表示可用于推理操作的硬件设备类型
    /// </summary>
    /// <remarks>
    /// Device selection affects which inference backends are available.
    /// 设备选择影响哪些推理后端可用
    /// </remarks>
    public enum DeviceType
    {
        /// <summary>
        /// Automatic device selection (OpenVINO only)
        /// 自动设备选择(仅OpenVINO)
        /// </summary>
        [DisplayName("AUTO")]
        AUTO,

        /// <summary>
        /// Central Processing Unit (CPU)
        /// Supports OpenVINO and ONNX Runtime
        /// 中央处理器(CPU)
        /// 支持OpenVINO和ONNX Runtime
        /// </summary>
        [DisplayName("CPU")]
        CPU,

        /// <summary>
        /// Primary Graphics Processing Unit
        /// Supports OpenVINO, ONNX Runtime, and TensorRT
        /// 主图形处理器
        /// 支持OpenVINO、ONNX Runtime和TensorRT
        /// </summary>
        [DisplayName("GPU.0")]
        GPU0,

        /// <summary>
        /// Secondary Graphics Processing Unit  
        /// Supports OpenVINO, ONNX Runtime, and TensorRT
        /// 副图形处理器
        /// 支持OpenVINO、ONNX Runtime和TensorRT
        /// </summary>
        [DisplayName("GPU.1")]
        GPU1,

        /// <summary>
        /// Neural Processing Unit (OpenVINO only)
        /// Supports Intel Neural Compute Stick and AI accelerators
        /// 神经处理单元(仅OpenVINO)
        /// 支持英特尔神经计算棒和AI加速器
        /// </summary>
        [DisplayName("NPU")]
        NPU,
    }

    /// <summary>
    /// Defines hardware acceleration device types supported by ONNX Runtime
    /// 定义ONNX Runtime支持的硬件加速设备类型
    /// </summary>
    /// <remarks>
    /// These correspond to different execution providers in ONNX Runtime.
    /// 这些对应ONNX Runtime中的不同执行提供程序
    /// </remarks>
    public enum OnnxRuntimeDeviceType
    {
        /// <summary>
        /// Uses default acceleration device:
        /// CPU - ONNX Runtime custom acceleration engine
        /// GPU - CUDA inference acceleration engine (default)
        /// 使用默认加速设备:
        /// CPU - ONNX Runtime自定义加速引擎
        /// GPU - CUDA推理加速引擎(默认)
        /// </summary>
        Default,

        /// <summary>
        /// Intel OpenVINO inference engine
        /// Supports CPU/GPU/NPU devices with Intel hardware optimization
        /// 英特尔OpenVINO推理引擎
        /// 支持具有Intel硬件优化的CPU/GPU/NPU设备
        /// </summary>
        OpenVINO,

        /// <summary>
        /// Intel oneDNN (formerly DNNL) acceleration
        /// Deep Neural Network Library for CPU optimization
        /// Intel oneDNN(原DNNL)加速
        /// 用于CPU优化的深度神经网络库
        /// </summary>
        Dnnl,

        /// <summary>
        /// NVIDIA CUDA acceleration engine
        /// Requires NVIDIA GPU support
        /// Provides optimal CUDA core utilization
        /// 英伟达CUDA加速引擎
        /// 需要NVIDIA GPU支持
        /// 提供最佳的CUDA核心利用率
        /// </summary>
        Cuda,

        /// <summary>
        /// NVIDIA TensorRT acceleration engine
        /// Provides model optimization and quantization
        /// Requires model pre-compilation
        /// 英伟达TensorRT加速引擎
        /// 提供模型优化和量化
        /// 需要模型预编译
        /// </summary>
        TensorRT,

        /// <summary>
        /// Microsoft DirectML execution provider
        /// Hardware-accelerated via DirectX 12
        /// Supports both AMD and NVIDIA GPUs
        /// 微软DirectML执行提供程序
        /// 通过DirectX 12硬件加速
        /// 支持AMD和NVIDIA GPU
        /// </summary>
        DML,

        /// <summary>
        /// AMD ROCm acceleration platform
        /// Supports AMD GPU devices
        /// Open-source heterogeneous computing framework
        /// AMD ROCm加速平台
        /// 支持AMD GPU设备
        /// 开源异构计算框架
        /// </summary>
        ROCm,

        /// <summary>
        /// AMD MIGraphX acceleration engine
        /// Optimized for AMD GPUs
        /// Supports model fusion optimization
        /// AMD MIGraphX加速引擎
        /// 针对AMD GPU优化
        /// 支持模型融合优化
        /// </summary>
        MIGraphX
    }

    /// <summary>
    /// Specifies a display name for enumeration fields
    /// 为枚举字段指定显示名称
    /// </summary>
    /// <remarks>
    /// This attribute can be used to provide user-friendly names for enum values.
    /// 此属性可用于为枚举值提供用户友好名称
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public class DisplayNameAttribute : Attribute
    {
        /// <summary>
        /// Gets the display name associated with the enum field
        /// 获取与枚举字段关联的显示名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the DisplayNameAttribute class
        /// 初始化DisplayNameAttribute类的新实例
        /// </summary>
        /// <param name="name">The display name to associate with the enum field 与枚举字段关联的显示名称</param>
        public DisplayNameAttribute(string name) => Name = name;
    }

    /// <summary>
    /// Provides extension methods for working with enums
    /// 提供处理枚举的扩展方法
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Gets the display name for an enum value as specified by DisplayNameAttribute
        /// 获取枚举值的显示名称(通过DisplayNameAttribute指定)
        /// </summary>
        /// <param name="value">The enum value to get the display name for 要获取显示名称的枚举值</param>
        /// <returns>
        /// The display name if specified by DisplayNameAttribute, otherwise the enum value's string representation
        /// 如果通过DisplayNameAttribute指定了显示名称则返回该名称，否则返回枚举值的字符串表示
        /// </returns>
        /// <example>
        /// <code>
        /// var name = DeviceType.CPU.GetDisplayName(); // Returns "CPU"
        /// var name = InferenceBackend.OpenVINO.GetDisplayName(); // Returns "OpenVINO"
        /// </code>
        /// </example>
        public static string GetDisplayName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            return field?.GetCustomAttribute<DisplayNameAttribute>()?.Name ?? value.ToString();
        }
    }

    // 使用示例
    //string displayName = GpuDevice.GPU0.GetDisplayName(); // 输出"GPU.0"
}
