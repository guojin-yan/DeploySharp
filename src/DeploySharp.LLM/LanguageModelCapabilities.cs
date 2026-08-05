using System;
using JYPPX.DeploySharp;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Describes language-model operations available from a session. / 描述语言模型会话可用的操作能力。</summary>
    [Flags]
    public enum LanguageModelCapabilities
    {
        /// <summary>No capability. / 无能力。</summary>
        None = 0,
        /// <summary>Synchronous or asynchronous text generation. / 同步或异步文本生成。</summary>
        TextGeneration = (int)BackendCapabilities.TextGeneration,
        /// <summary>Streaming text generation. / 流式文本生成。</summary>
        Streaming = 1 << 8,
        /// <summary>Text embeddings. / 文本嵌入。</summary>
        Embeddings = (int)BackendCapabilities.Embeddings
    }
}
