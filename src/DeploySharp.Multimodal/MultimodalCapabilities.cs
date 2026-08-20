using System;

namespace JYPPX.DeploySharp.Multimodal
{
    /// <summary>Describes operations supported by a multimodal backend session. / 描述多模态后端会话支持的操作。</summary>
    [Flags]
    public enum MultimodalCapabilities
    {
        /// <summary>No capability is available. / 没有可用能力。</summary>
        None = 0,
        /// <summary>Completed text generation from media and a prompt. / 根据媒体与提示词生成完整文本。</summary>
        TextGeneration = 1,
        /// <summary>Ordered streaming text generation. / 有序流式文本生成。</summary>
        Streaming = 1 << 1,
        /// <summary>More than one media item in a request. / 单个请求可包含多个媒体项。</summary>
        MultipleMedia = 1 << 2,
        /// <summary>Region-bound media inputs. / 带区域身份的媒体输入。</summary>
        Regions = 1 << 3,
        /// <summary>Cooperative cancellation. / 协作式取消。</summary>
        Cancellation = 1 << 4
    }

    /// <summary>Identifies the intent of a multimodal generation request. / 标识多模态生成请求的意图。</summary>
    public enum MultimodalTask
    {
        /// <summary>Answer a question about the supplied media. / 回答与所提供媒体有关的问题。</summary>
        QuestionAnswering = 0,
        /// <summary>Describe the supplied media. / 描述所提供的媒体。</summary>
        Captioning = 1,
        /// <summary>Follow a general media-grounded instruction. / 执行一般的媒体条件指令。</summary>
        Instruction = 2
    }

    /// <summary>Reports whether an adapter can execute in the current process. / 报告适配器能否在当前进程中执行。</summary>
    public enum MultimodalAvailabilityState
    {
        /// <summary>The adapter and required runtime are available. / 适配器及所需运行时可用。</summary>
        Available = 0,
        /// <summary>The adapter is supported but a required external asset or runtime is unavailable. / 支持该适配器，但缺少所需外部资产或运行时。</summary>
        Unavailable = 1,
        /// <summary>The requested adapter or platform is unsupported. / 不支持请求的适配器或平台。</summary>
        Unsupported = 2
    }
}
