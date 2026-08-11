using System;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines stable Visual-domain diagnostic codes. / 定义稳定的 Visual 领域诊断码。</summary>
    public static class VisualErrorCodes
    {
        /// <summary>A prepared input is invalid. / 已准备输入无效。</summary>
        public const string InputInvalid = "DS-VISUAL-1001";
        /// <summary>A coordinate transform is invalid. / 坐标变换无效。</summary>
        public const string TransformInvalid = "DS-VISUAL-1002";
        /// <summary>A model profile is invalid or incompatible. / 模型 Profile 无效或不兼容。</summary>
        public const string ProfileInvalid = "DS-VISUAL-2001";
        /// <summary>A profile identifier was registered more than once. / Profile 标识符被重复注册。</summary>
        public const string ProfileAlreadyRegistered = "DS-VISUAL-2002";
        /// <summary>No registered profile matches the request. / 没有已注册 Profile 匹配请求。</summary>
        public const string ProfileNotFound = "DS-VISUAL-2003";
        /// <summary>An inference tensor binding or shape is invalid. / 推理张量绑定或形状无效。</summary>
        public const string TensorInvalid = "DS-VISUAL-3001";
        /// <summary>Visual result decoding failed. / Visual 结果解码失败。</summary>
        public const string DecodeFailed = "DS-VISUAL-3002";
        /// <summary>A requested Visual capability is not available. / 请求的 Visual 能力不可用。</summary>
        public const string CapabilityUnavailable = "DS-VISUAL-3003";
        /// <summary>A Visual inference operation failed. / Visual 推理操作失败。</summary>
        public const string InferenceFailed = "DS-VISUAL-4001";
        /// <summary>A Visual operation was cancelled by the caller. / Visual 操作被调用方取消。</summary>
        public const string Cancelled = "DS-VISUAL-4002";
        /// <summary>A Visual operation exceeded its configured timeout. / Visual 操作超过配置的超时时间。</summary>
        public const string Timeout = "DS-VISUAL-4003";
        /// <summary>An OCR pipeline stage failed. / OCR Pipeline 阶段失败。</summary>
        public const string OcrPipelineFailed = "DS-VISUAL-4101";
        /// <summary>An OCR input, batch, output, or workspace limit was exceeded. / OCR 输入、批次、输出或工作区超出限制。</summary>
        public const string OcrLimitExceeded = "DS-VISUAL-4102";
        /// <summary>An anomaly tensor or result contract is invalid. / 异常张量或结果契约无效。</summary>
        public const string AnomalyContractInvalid = "DS-VISUAL-4201";
        /// <summary>An anomaly map, workspace, or output limit was exceeded. / 异常图、工作区或输出超出限制。</summary>
        public const string AnomalyLimitExceeded = "DS-VISUAL-4202";
        /// <summary>A requested anomaly postprocessing capability is unavailable. / 请求的异常后处理能力不可用。</summary>
        public const string AnomalyCapabilityUnavailable = "DS-VISUAL-4203";
        /// <summary>The Visual object has already been disposed. / Visual 对象已被释放。</summary>
        public const string ObjectDisposed = "DS-VISUAL-5001";
        /// <summary>An OCR orientation contract is invalid. / OCR 方向契约无效。</summary>
        public const string OcrOrientationContractInvalid = "DS-VISUAL-4301";
        /// <summary>An OCR orientation limit was exceeded. / OCR 方向限制超出。</summary>
        public const string OcrOrientationLimitExceeded = "DS-VISUAL-4302";
        /// <summary>OCR orientation correction is unavailable. / OCR 方向纠正能力不可用。</summary>
        public const string OcrOrientationCapabilityUnavailable = "DS-VISUAL-4303";
        /// <summary>A YOLO model, export, tensor, or decoding contract is invalid. / YOLO 模型、导出、张量或解码合同无效。</summary>
        public const string YoloContractInvalid = "DS-VISUAL-4401";
        /// <summary>A YOLO candidate, result, or workspace limit was exceeded. / YOLO 候选、结果或工作区限制超出。</summary>
        public const string YoloLimitExceeded = "DS-VISUAL-4402";
        /// <summary>A promptable-segmentation profile, prompt, tensor, or result contract is invalid. / 可提示分割 Profile、提示、张量或结果合同无效。</summary>
        public const string PromptableSegmentationContractInvalid = "DS-VISUAL-4501";
        /// <summary>A prompt, embedding, mask, result, or workspace capacity was exceeded. / 提示、Embedding、掩码、结果或工作区容量超出限制。</summary>
        public const string PromptableSegmentationLimitExceeded = "DS-VISUAL-4502";
        /// <summary>The promptable image or video state does not permit the requested operation. / 可提示图像或视频状态不允许所请求的操作。</summary>
        public const string PromptableSegmentationStateInvalid = "DS-VISUAL-4503";
        /// <summary>An image embedding or mask-feedback identity does not match the active profile and artifacts. / 图像 Embedding 或掩码反馈 identity 与活动 Profile 和工件不匹配。</summary>
        public const string PromptableSegmentationIdentityMismatch = "DS-VISUAL-4504";
        /// <summary>A stateful promptable-segmentation session is already executing another operation. / 有状态可提示分割会话正在执行另一操作。</summary>
        public const string PromptableSegmentationConcurrentOperation = "DS-VISUAL-4505";
        /// <summary>An open-vocabulary artifact, prompt, tokenizer, tensor, or decoder contract is invalid. / 开放词汇工件、提示、Tokenizer、张量或 Decoder 合同无效。</summary>
        public const string OpenVocabularyContractInvalid = "DS-VISUAL-4601";
        /// <summary>An open-vocabulary prompt, candidate, result, or composition capacity was exceeded. / 开放词汇提示、候选、结果或组合容量超出限制。</summary>
        public const string OpenVocabularyLimitExceeded = "DS-VISUAL-4602";
        /// <summary>The open-vocabulary or Grounded-SAM state does not permit the requested operation. / 开放词汇或 Grounded-SAM 状态不允许所请求的操作。</summary>
        public const string OpenVocabularyStateInvalid = "DS-VISUAL-4603";
        /// <summary>A vocabulary, tokenizer, embedding, image, profile, or artifact identity does not match. / 词汇、Tokenizer、Embedding、图像、Profile 或工件 Identity 不匹配。</summary>
        public const string OpenVocabularyIdentityMismatch = "DS-VISUAL-4604";
        /// <summary>A stateful Grounded-SAM session is already executing another operation. / 有状态 Grounded-SAM 会话正在执行另一操作。</summary>
        public const string OpenVocabularyConcurrentOperation = "DS-VISUAL-4605";
        /// <summary>A vision-language profile, tokenizer, tensor, or scoring contract is invalid. / 视觉语言 Profile、Tokenizer、张量或评分合同无效。</summary>
        public const string VisionLanguageContractInvalid = "DS-VISUAL-4701";
        /// <summary>A vision-language batch, prompt, tensor, or workspace capacity was exceeded. / 视觉语言批次、提示、张量或工作区容量超出限制。</summary>
        public const string VisionLanguageLimitExceeded = "DS-VISUAL-4702";
        /// <summary>The vision-language session state does not permit the requested operation. / 视觉语言会话状态不允许所请求的操作。</summary>
        public const string VisionLanguageStateInvalid = "DS-VISUAL-4703";
        /// <summary>An image, text, profile, tokenizer, or artifact identity does not match. / 图像、文本、Profile、Tokenizer 或工件 Identity 不匹配。</summary>
        public const string VisionLanguageIdentityMismatch = "DS-VISUAL-4704";
        /// <summary>A vision-language session is already executing another operation. / 视觉语言会话正在执行另一操作。</summary>
        public const string VisionLanguageConcurrentOperation = "DS-VISUAL-4705";
        /// <summary>The generative vision-language profile or named tensor contract is invalid. / 生成式视觉语言 Profile 或具名张量合同无效。</summary>
        public const string GenerativeVisionLanguageContractInvalid = "DS-VISUAL-4801";
        /// <summary>A generative vision-language capacity was exceeded. / 超出生成式视觉语言容量。</summary>
        public const string GenerativeVisionLanguageLimitExceeded = "DS-VISUAL-4802";
        /// <summary>The image or generation state is unavailable. / 图像或生成状态不可用。</summary>
        public const string GenerativeVisionLanguageStateInvalid = "DS-VISUAL-4803";
        /// <summary>A processor, tokenizer, artifact, image, prompt, or generation identity mismatched. / Processor、Tokenizer、工件、图像、提示词或生成 Identity 不匹配。</summary>
        public const string GenerativeVisionLanguageIdentityMismatch = "DS-VISUAL-4804";
        /// <summary>A concurrent operation was rejected by a stateful generation session. / 有状态生成 Session 拒绝了并发操作。</summary>
        public const string GenerativeVisionLanguageConcurrentOperation = "DS-VISUAL-4805";
        /// <summary>A tokenizer sidecar is missing, altered, or unsupported. / Tokenizer sidecar 缺失、被修改或不受支持。</summary>
        public const string GenerativeVisionLanguageTokenizerInvalid = "DS-VISUAL-4806";
        /// <summary>A generation step returned invalid logits or a non-deterministic terminal state. / 生成步骤返回无效 Logit 或非确定终止状态。</summary>
        public const string GenerativeVisionLanguageGenerationInvalid = "DS-VISUAL-4807";
        /// <summary>A native multimodal profile, image-token layout, named port, or KV contract is invalid. / 原生多模态 Profile、图像 Token 布局、具名端口或 KV 合同无效。</summary>
        public const string NativeMultimodalContractInvalid = "DS-VISUAL-4901";
        /// <summary>A native multimodal image, prompt, context, KV, or workspace capacity was exceeded. / 原生多模态图像、Prompt、Context、KV 或工作区超出容量。</summary>
        public const string NativeMultimodalLimitExceeded = "DS-VISUAL-4902";
        /// <summary>The native multimodal image or KV state does not permit the operation. / 原生多模态图像或 KV 状态不允许该操作。</summary>
        public const string NativeMultimodalStateInvalid = "DS-VISUAL-4903";
        /// <summary>A profile, artifact, processor, tokenizer, prompt, image, or KV identity mismatched. / Profile、工件、Processor、Tokenizer、Prompt、图像或 KV Identity 不匹配。</summary>
        public const string NativeMultimodalIdentityMismatch = "DS-VISUAL-4904";
        /// <summary>A concurrent operation was rejected by the single-writer native multimodal session. / Single-writer 原生多模态 Session 拒绝了并发操作。</summary>
        public const string NativeMultimodalConcurrentOperation = "DS-VISUAL-4905";
        /// <summary>A native multimodal tokenizer asset or chat-template result is invalid. / 原生多模态 Tokenizer 资产或 Chat Template 结果无效。</summary>
        public const string NativeMultimodalTokenizerInvalid = "DS-VISUAL-4906";
        /// <summary>A native multimodal prefill/decode step returned invalid logits or KV tensors. / 原生多模态 Prefill/Decode 返回无效 Logit 或 KV 张量。</summary>
        public const string NativeMultimodalGenerationInvalid = "DS-VISUAL-4907";
        /// <summary>The requested native multimodal family or capability has no executable audited artifact bundle. / 请求的原生多模态模型族或能力没有可执行的已审计工件 Bundle。</summary>
        public const string NativeMultimodalCapabilityUnavailable = "DS-VISUAL-4908";
        /// <summary>A document profile, page, layout, named port, tokenizer, schema, or KV contract is invalid. / 文档 Profile、页面、版面、具名端口、Tokenizer、Schema 或 KV 合同无效。</summary>
        public const string DocumentUnderstandingContractInvalid = "DS-VISUAL-5002";
        /// <summary>A document page, word, box, patch, token, field, KV, or workspace capacity was exceeded. / 文档页面、词、Box、Patch、Token、字段、KV 或工作区超出容量。</summary>
        public const string DocumentUnderstandingLimitExceeded = "DS-VISUAL-5003";
        /// <summary>The document state does not permit the requested encode or generation operation. / 文档状态不允许请求的编码或生成操作。</summary>
        public const string DocumentUnderstandingStateInvalid = "DS-VISUAL-5004";
        /// <summary>A page, profile, processor, OCR, tokenizer, schema, artifact, prompt, or KV identity mismatched. / 页面、Profile、Processor、OCR、Tokenizer、Schema、工件、Prompt 或 KV Identity 不匹配。</summary>
        public const string DocumentUnderstandingIdentityMismatch = "DS-VISUAL-5005";
        /// <summary>A concurrent mutation was rejected by the single-writer document session. / Single-writer 文档 Session 拒绝了并发 Mutation。</summary>
        public const string DocumentUnderstandingConcurrentOperation = "DS-VISUAL-5006";
        /// <summary>A document tokenizer asset or prompt/template output is invalid. / 文档 Tokenizer 资产或 Prompt/Template 输出无效。</summary>
        public const string DocumentUnderstandingTokenizerInvalid = "DS-VISUAL-5007";
        /// <summary>A document Prefill/Decode step returned invalid logits or KV tensors. / 文档 Prefill/Decode 返回无效 Logit 或 KV Tensor。</summary>
        public const string DocumentUnderstandingGenerationInvalid = "DS-VISUAL-5008";
        /// <summary>A bounded structured-document result could not be parsed under the bound schema. / 受限结构化文档结果无法按绑定 Schema 解析。</summary>
        public const string DocumentUnderstandingSchemaInvalid = "DS-VISUAL-5009";
        /// <summary>The requested document family or task has no executable audited artifact bundle. / 请求的文档模型族或任务没有可执行的已审计工件 Bundle。</summary>
        public const string DocumentUnderstandingCapabilityUnavailable = "DS-VISUAL-5010";
        /// <summary>An audio profile, named port, waveform, feature, tokenizer, timestamp, speaker, or KV contract is invalid. / 音频 Profile、具名端口、波形、特征、Tokenizer、时间戳、说话人或 KV 合同无效。</summary>
        public const string AudioContractInvalid = "DS-AUDIO-6001";
        /// <summary>An audio file or byte stream is malformed, truncated, or unsupported. / 音频文件或字节流畸形、截断或不受支持。</summary>
        public const string AudioMalformed = "DS-AUDIO-6002";
        /// <summary>The sample rate differs from the artifact-bound native rate. / 采样率与工件绑定的原生采样率不一致。</summary>
        public const string AudioSampleRateMismatch = "DS-AUDIO-6003";
        /// <summary>The channel count or layout is unsupported or ambiguous. / 声道数或布局不受支持或含义不明确。</summary>
        public const string AudioChannelMismatch = "DS-AUDIO-6004";
        /// <summary>An audio sample, feature, logit, timestamp, or KV value is NaN or Infinity. / 音频样本、特征、Logit、时间戳或 KV 值为 NaN 或 Infinity。</summary>
        public const string AudioNonFinite = "DS-AUDIO-6005";
        /// <summary>An audio duration, sample, frame, token, segment, or workspace capacity was exceeded. / 音频时长、样本、帧、Token、片段或工作区超出容量。</summary>
        public const string AudioLimitExceeded = "DS-AUDIO-6006";
        /// <summary>An audio source, processor, vocabulary, feature, timestamp, speaker, artifact, or state identity mismatched. / 音频源、Processor、词表、特征、时间戳、说话人、工件或状态 Identity 不匹配。</summary>
        public const string AudioIdentityMismatch = "DS-AUDIO-6007";
        /// <summary>The requested language, task, timestamp, speaker, streaming, or generation capability is unavailable. / 请求的语言、任务、时间戳、说话人、流式或生成能力不可用。</summary>
        public const string AudioCapabilityUnavailable = "DS-AUDIO-6008";
        /// <summary>The audio session state does not permit the requested operation. / 音频 Session 状态不允许请求的操作。</summary>
        public const string AudioStateInvalid = "DS-AUDIO-6009";
        /// <summary>A concurrent operation was rejected by the single-writer audio session. / Single-writer 音频 Session 拒绝了并发操作。</summary>
        public const string AudioConcurrentOperation = "DS-AUDIO-6010";
        /// <summary>Deterministic CTC decoding failed its vocabulary, blank, tie, or alignment contract. / 确定性 CTC 解码违反词表、Blank、平局或对齐合同。</summary>
        public const string AudioCtcDecodeInvalid = "DS-AUDIO-6011";
        /// <summary>The audio operation was cancelled by the caller. / 音频操作被调用方取消。</summary>
        public const string AudioCancelled = "DS-AUDIO-6012";
        /// <summary>The audio operation exceeded its configured timeout. / 音频操作超过配置的超时时间。</summary>
        public const string AudioTimeout = "DS-AUDIO-6013";
        /// <summary>The audio session or prepared input has already been disposed. / 音频 Session 或 Prepared Input 已被释放。</summary>
        public const string AudioDisposed = "DS-AUDIO-6014";
        /// <summary>An executable audio backend call failed. / 可执行音频后端调用失败。</summary>
        public const string AudioInferenceFailed = "DS-AUDIO-6015";
    }

    /// <summary>Represents a diagnosable Visual-domain failure while preserving the original exception. / 表示可诊断的 Visual 领域故障，同时保留原始异常。</summary>
    public sealed class VisualException : DeploySharpException
    {
        /// <summary>Initializes a Visual exception. / 初始化 Visual 异常。</summary>
        public VisualException(
            string errorCode,
            string message,
            Exception? innerException = null,
            string? profileId = null,
            string? tensorName = null,
            BackendId? backendId = null,
            ModelId? modelId = null,
            string? technicalDetails = null)
            : base(errorCode, message, innerException, backendId, modelId, technicalDetails)
        {
            ProfileId = profileId;
            TensorName = tensorName;
        }

        /// <summary>Gets the associated Visual profile identifier. / 获取关联的 Visual Profile 标识符。</summary>
        public string? ProfileId { get; }

        /// <summary>Gets the associated tensor name. / 获取关联的张量名称。</summary>
        public string? TensorName { get; }
    }

    internal static class VisualGuard
    {
        public static string Identifier(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A stable identifier is required.", profileId: value);
            string normalized = value!.Trim().ToLowerInvariant();
            for (int index = 0; index < normalized.Length; index++)
            {
                char current = normalized[index];
                bool accepted = (current >= 'a' && current <= 'z') || (current >= '0' && current <= '9') || current == '-' || current == '_' || current == '.' || current == '/';
                if (!accepted) throw new VisualException(VisualErrorCodes.ProfileInvalid, "An identifier contains unsupported characters.", profileId: normalized, technicalDetails: parameterName);
            }

            return normalized;
        }

        public static void Finite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new VisualException(VisualErrorCodes.TransformInvalid, "A coordinate value must be finite.", technicalDetails: parameterName);
        }
    }
}
