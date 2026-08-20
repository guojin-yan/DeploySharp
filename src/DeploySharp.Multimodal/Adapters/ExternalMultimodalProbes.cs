using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Multimodal.Adapters
{
    /// <summary>Creates explicit blocker probes for optional external VLM runtimes. / 为可选外部 VLM 运行时创建明确的 blocker 探测结果。</summary>
    public static class ExternalMultimodalProbes
    {
        /// <summary>Reports the LLamaSharp mtmd path as unavailable until the caller supplies an audited native and model bundle. / 在调用方提供经审计的原生与模型 Bundle 前，将 LLamaSharp mtmd 路径报告为不可用。</summary>
        public static MultimodalBackendDescriptor LlamaSharpMtmd(ModelId modelId)
            => new MultimodalBackendDescriptor("llamasharp-mtmd", "external", modelId, MultimodalCapabilities.TextGeneration | MultimodalCapabilities.Streaming | MultimodalCapabilities.MultipleMedia | MultimodalCapabilities.Cancellation, 16, new MultimodalAvailability(MultimodalAvailabilityState.Unavailable, "An audited caller-owned mtmd native library, model, projector, tokenizer, and golden are required.", "not-recorded"));

        /// <summary>Reports OpenVINO GenAI VLM as unavailable until a device-specific bundle and runtime are audited. / 在审计设备专用 Bundle 与运行时前，将 OpenVINO GenAI VLM 报告为不可用。</summary>
        public static MultimodalBackendDescriptor OpenVinoGenAi(ModelId modelId, string device)
            => new MultimodalBackendDescriptor("openvino-genai-vlm", "external", modelId, MultimodalCapabilities.TextGeneration | MultimodalCapabilities.Streaming | MultimodalCapabilities.MultipleMedia | MultimodalCapabilities.Cancellation, 16, new MultimodalAvailability(MultimodalAvailabilityState.Unavailable, "An audited OpenVINO GenAI VLM bundle, native runtime, device identity, and golden are required.", "device=" + (string.IsNullOrWhiteSpace(device) ? "unspecified" : device.Trim())));
    }
}
