using System;
using JYPPX.DeploySharp.LLM.Prompt;
using JYPPX.DeploySharp.LLM.Registry;

namespace JYPPX.DeploySharp.Backends.LlamaSharp
{
    /// <summary>Provides explicit LLamaSharp registration helpers. / 提供显式 LLamaSharp 注册辅助方法。</summary>
    public static class LlamaSharpRegistryExtensions
    {
        /// <summary>Registers a LLamaSharp provider owned by the registry. / 注册一个由注册表持有的 LLamaSharp 提供程序。</summary>
        public static LanguageModelRegistry UseLlamaSharp(this LanguageModelRegistry registry, LlamaSharpOptions? options = null, IPromptFormatter? promptFormatter = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.Register(new LlamaSharpBackendProvider(options, promptFormatter));
            return registry;
        }
    }
}
