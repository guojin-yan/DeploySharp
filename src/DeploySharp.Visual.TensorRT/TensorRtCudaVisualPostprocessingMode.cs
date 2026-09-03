namespace JYPPX.DeploySharp.Visual.TensorRT
{
    /// <summary>Controls optional CUDA postprocessing for compatible TensorRT visual profiles. / 控制兼容 TensorRT 视觉 Profile 的可选 CUDA 后处理。</summary>
    public enum TensorRtCudaVisualPostprocessingMode
    {
        /// <summary>Always copy engine outputs to managed memory and use the backend-neutral CPU decoder. / 始终将引擎输出复制到托管内存并使用后端无关 CPU 解码器。</summary>
        Disabled = 0,

        /// <summary>Use CUDA for admitted fixed-shape operations and fall back to the CPU decoder for other contracts. / 对已准入的固定形状操作使用 CUDA，其他合同回退到 CPU 解码器。</summary>
        WhenSupported = 1
    }
}
