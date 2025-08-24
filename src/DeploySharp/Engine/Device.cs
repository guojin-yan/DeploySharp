using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Engine
{

    /// <summary>
    /// Software reasoning equipment
    /// </summary>
    public enum InferenceBackend
    {
        /// <summary>
        /// The Intel's OpenVINO
        /// </summary>
        [DisplayName("OpenVINO")]  OpenVINO = 0,
        /// <summary>
        /// The Microsoft's OnnxRuntime
        /// </summary>
        [DisplayName("OnnxRuntime")]  OnnxRuntime = 1,
        /// <summary>
        /// The NVIDIA's TensorRT
        /// </summary>
        [DisplayName("TensorRT")]  TensorRT = 2,
    }




    /// <summary>
    /// Hardware reasoning equipment
    /// </summary>
    public enum DeviceType 
    {
        /// <summary>
        /// Only OpenVINO is available 
        /// </summary>
        [DisplayName("AUTO")] AUTO,
        /// <summary>
        /// OpenVINO, ONNX Runtime is available
        /// </summary>
        [DisplayName("CPU")] CPU,
        /// <summary>
        /// OpenVINO, ONNX Runtime(), TensorRT is available
        /// </summary>
        [DisplayName("GPU.0")] GPU0,
        /// <summary>
        /// OpenVINO, ONNX Runtime, TensorRT is available
        /// </summary>
        [DisplayName("GPU.1")] GPU1,
        /// <summary>
        /// Only OpenVINO is available 
        /// </summary>
        [DisplayName("NPU")] NPU,

    }



    /// <summary>
    /// Defines the hardware acceleration device types supported by ONNX Runtime
    /// </summary>
    public enum OnnxRuntimeDeviceType
    {
        /// <summary>
        /// Uses default acceleration device
        /// CPU: ONNX Runtime custom acceleration engine
        /// GPU: CUDA inference acceleration engine (default)
        /// </summary>
        Default,

        /// <summary>
        /// Intel OpenVINO inference engine
        /// Supports CPU/GPU/NPU devices with Intel hardware optimization
        /// </summary>
        OpenVINO,

        /// <summary>
        /// Intel oneDNN (formerly DNNL) acceleration
        /// Deep Neural Network Library for CPU optimization
        /// </summary>
        Dnnl,

        /// <summary>
        /// NVIDIA CUDA acceleration engine
        /// Requires NVIDIA GPU support
        /// Provides optimal CUDA core utilization
        /// </summary>
        Cuda,

        /// <summary>
        /// NVIDIA TensorRT acceleration engine
        /// Provides model optimization and quantization
        /// Requires model pre-compilation
        /// </summary>
        TensorRT,


        /// <summary>
        /// Microsoft DirectML execution provider
        /// Hardware-accelerated via DirectX 12
        /// Supports both AMD and NVIDIA GPUs
        /// </summary>
        DML,


        /// <summary>
        /// AMD ROCm acceleration platform
        /// Supports AMD GPU devices
        /// Open-source heterogeneous computing framework
        /// </summary>
        ROCm,

        /// <summary>
        /// AMD MIGraphX acceleration engine
        /// Optimized for AMD GPUs
        /// Supports model fusion optimization
        /// </summary>
        MIGraphX
    }


    /// <summary>
    /// Specifies that this attribute can only be applied to enum fields
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DisplayNameAttribute : Attribute
    {
        /// <summary>
        /// Gets the display name for the enum field
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the DisplayNameAttribute class
        /// </summary>
        /// <param name="name">The display name to associate with the enum field</param>
        public DisplayNameAttribute(string name) => Name = name;
    }

    /// <summary>
    /// Provides extension methods for working with enum display names
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Gets the display name for an enum value if specified via DisplayNameAttribute,
        /// otherwise returns the standard string representation
        /// </summary>
        /// <param name="value">The enum value to get display name for</param>
        /// <returns>The display name if specified, otherwise the enum value name</returns>
        public static string GetDisplayName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            return field?.GetCustomAttribute<DisplayNameAttribute>()?.Name ?? value.ToString();
        }
    }


    // 使用示例
    //string displayName = GpuDevice.GPU0.GetDisplayName(); // 输出"GPU.0"
}
