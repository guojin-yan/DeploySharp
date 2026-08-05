using System;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Combines Core session ownership options with LLM-specific session settings. / 将 Core 会话生命周期选项与 LLM 会话设置组合。</summary>
    public sealed class LanguageModelSessionOptions
    {
        /// <summary>Initializes language-model session options. / 初始化语言模型会话选项。</summary>
        public LanguageModelSessionOptions(SessionOptions? coreOptions = null)
        {
            CoreOptions = coreOptions ?? SessionOptions.Default;
        }

        /// <summary>Gets backend-neutral Core options. / 获取后端无关的 Core 选项。</summary>
        public SessionOptions CoreOptions { get; }

        /// <summary>Gets default language-model session options. / 获取默认语言模型会话选项。</summary>
        public static LanguageModelSessionOptions Default { get; } = new LanguageModelSessionOptions();
    }
}
