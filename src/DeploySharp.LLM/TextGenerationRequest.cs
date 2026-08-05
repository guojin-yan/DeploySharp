using System;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Describes one text-completion request. / 描述一次文本补全请求。</summary>
    public sealed class TextGenerationRequest
    {
        /// <summary>Initializes a text request. / 初始化文本请求。</summary>
        public TextGenerationRequest(string prompt, GenerationOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
            Prompt = prompt;
            Options = options ?? GenerationOptions.Default;
        }

        /// <summary>Gets the backend prompt. / 获取后端提示词。</summary>
        public string Prompt { get; }
        /// <summary>Gets sampling and timeout options. / 获取采样和超时选项。</summary>
        public GenerationOptions Options { get; }
    }
}
