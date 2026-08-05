using System;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Describes a chat completion request before prompt formatting. / 描述格式化提示词之前的聊天补全请求。</summary>
    public sealed class ChatCompletionRequest
    {
        /// <summary>Initializes a chat request. / 初始化聊天请求。</summary>
        public ChatCompletionRequest(ChatHistory history, GenerationOptions? options = null)
        {
            History = history ?? throw new ArgumentNullException(nameof(history));
            if (history.Messages.Count == 0) throw new ArgumentException("Chat history cannot be empty.", nameof(history));
            Options = options ?? GenerationOptions.Default;
        }

        /// <summary>Gets the ordered chat history. / 获取有序聊天历史。</summary>
        public ChatHistory History { get; }
        /// <summary>Gets sampling and timeout options. / 获取采样和超时选项。</summary>
        public GenerationOptions Options { get; }
    }
}
