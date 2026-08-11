using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Errors;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Groups profiles that must share one immutable model identity. / 将必须共享同一不可变模型身份的 Profile 组成 Bundle。</summary>
    public sealed class LanguageModelBundle
    {
        private readonly IReadOnlyList<LanguageModelProfile> _profiles;

        /// <summary>Initializes a bundle and rejects mixed model/runtime identities. / 初始化 Bundle 并拒绝混合模型或运行时身份。</summary>
        public LanguageModelBundle(IEnumerable<LanguageModelProfile> profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            var copied = new List<LanguageModelProfile>();
            foreach (LanguageModelProfile profile in profiles)
            {
                if (profile == null) throw new ArgumentException("A bundle cannot contain null profiles.", nameof(profiles));
                copied.Add(profile);
            }

            if (copied.Count == 0) throw new ArgumentException("A bundle requires at least one profile.", nameof(profiles));
            LanguageModelProfile first = copied[0];
            for (int index = 1; index < copied.Count; index++)
            {
                LanguageModelProfile current = copied[index];
                if (!string.Equals(first.Artifact.ModelId.Value, current.Artifact.ModelId.Value, StringComparison.Ordinal)
                    || !string.Equals(first.Artifact.Format, current.Artifact.Format, StringComparison.Ordinal)
                    || !string.Equals(first.Artifact.Sha256, current.Artifact.Sha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(first.ModelVersion, current.ModelVersion, StringComparison.Ordinal)
                    || !string.Equals(first.Quantization, current.Quantization, StringComparison.Ordinal)
                    || !string.Equals(first.TokenizerIdentity, current.TokenizerIdentity, StringComparison.Ordinal)
                    || !string.Equals(first.ChatTemplateIdentity, current.ChatTemplateIdentity, StringComparison.Ordinal)
                    || !string.Equals(first.GenerationIdentity, current.GenerationIdentity, StringComparison.Ordinal)
                    || first.ContextLength != current.ContextLength
                    || first.BackendId != current.BackendId)
                {
                    throw new DeploySharpException(
                        DeploySharpErrorCodes.LanguageModelBundleMismatch,
                        "Language-model bundle members must share model, version, quantization, tokenizer, chat-template, generation, context, and backend identities.",
                        backendId: first.BackendId,
                        modelId: first.Artifact.ModelId);
                }
            }

            _profiles = copied.AsReadOnly();
        }

        /// <summary>Gets an immutable snapshot of bundle profiles. / 获取 Bundle Profile 的不可变快照。</summary>
        public IReadOnlyList<LanguageModelProfile> Profiles => _profiles;

        /// <summary>Gets the shared model identity. / 获取共享模型身份。</summary>
        public LanguageModelProfile Identity => _profiles[0];
    }
}
