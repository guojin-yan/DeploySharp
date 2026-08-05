using System;
using JYPPX.DeploySharp;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Controls provider selection for a language-model session. / 控制语言模型会话的后端选择。</summary>
    public sealed class LanguageModelRequest
    {
        /// <summary>Initializes a language-model selection request. / 初始化语言模型选择请求。</summary>
        public LanguageModelRequest(
            LanguageModelCapabilities requiredCapabilities = LanguageModelCapabilities.TextGeneration,
            BackendId? backendId = null,
            string? device = null)
        {
            if (requiredCapabilities == LanguageModelCapabilities.None) throw new ArgumentOutOfRangeException(nameof(requiredCapabilities));
            RequiredCapabilities = requiredCapabilities;
            BackendId = backendId;
            Device = string.IsNullOrWhiteSpace(device) ? null : device;
        }

        /// <summary>Gets required language-model capabilities. / 获取所需语言模型能力。</summary>
        public LanguageModelCapabilities RequiredCapabilities { get; }
        /// <summary>Gets an optional explicit backend identifier. / 获取可选的明确后端标识。</summary>
        public BackendId? BackendId { get; }
        /// <summary>Gets an optional device selector. / 获取可选设备选择器。</summary>
        public string? Device { get; }
    }
}
