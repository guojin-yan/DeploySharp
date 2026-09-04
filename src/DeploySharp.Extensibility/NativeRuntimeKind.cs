namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Identifies a native runtime family that a probe may inspect. / 标识探针可以检查的原生运行时族。</summary>
    public enum NativeRuntimeKind
    {
        /// <summary>Unspecified native runtime. / 未指定的原生运行时。</summary>
        Unknown = 0,
        /// <summary>NVIDIA CUDA runtime. / NVIDIA CUDA 运行时。</summary>
        CUDA = 1,
        /// <summary>NVIDIA cuDNN runtime. / NVIDIA cuDNN 运行时。</summary>
        CuDNN = 2,
        /// <summary>NVIDIA TensorRT runtime. / NVIDIA TensorRT 运行时。</summary>
        TensorRT = 3,
        /// <summary>NVIDIA NVRTC compiler runtime. / NVIDIA NVRTC 编译器运行时。</summary>
        NVRTC = 4,
        /// <summary>NVIDIA driver. / NVIDIA 驱动。</summary>
        Driver = 5,
        /// <summary>OpenVINO runtime and device plug-ins. / OpenVINO 运行时及设备插件。</summary>
        OpenVINO = 6,
        /// <summary>OpenCV native runtime. / OpenCV 原生运行时。</summary>
        OpenCV = 7,
        /// <summary>ONNX Runtime native library. / ONNX Runtime 原生库。</summary>
        OnnxRuntimeNative = 8,
        /// <summary>LLamaSharp/llama.cpp native backend. / LLamaSharp/llama.cpp 原生后端。</summary>
        LlamaSharpNative = 9
    }
}
