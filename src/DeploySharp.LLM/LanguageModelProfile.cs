using System;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>
    /// Binds a language model artifact to the identities required for reproducible generation. / 将语言模型工件绑定到可复现生成所需的身份。
    /// </summary>
    public sealed class LanguageModelProfile
    {
        /// <summary>Initializes an immutable language-model profile. / 初始化不可变语言模型 Profile。</summary>
        public LanguageModelProfile(
            ModelArtifact artifact,
            string modelVersion,
            string quantization,
            string tokenizerIdentity,
            string chatTemplateIdentity,
            string generationIdentity,
            int? contextLength,
            bool embeddingsSupported,
            BackendId backendId,
            string licenseStatus)
        {
            Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
            ModelVersion = Required(modelVersion, nameof(modelVersion));
            Quantization = Required(quantization, nameof(quantization));
            TokenizerIdentity = Required(tokenizerIdentity, nameof(tokenizerIdentity));
            ChatTemplateIdentity = Required(chatTemplateIdentity, nameof(chatTemplateIdentity));
            GenerationIdentity = Required(generationIdentity, nameof(generationIdentity));
            if (contextLength.HasValue && contextLength.Value <= 0) throw new ArgumentOutOfRangeException(nameof(contextLength));
            ContextLength = contextLength;
            EmbeddingsSupported = embeddingsSupported;
            if (backendId.IsEmpty) throw new ArgumentException("A backend identifier is required.", nameof(backendId));
            BackendId = backendId;
            LicenseStatus = Required(licenseStatus, nameof(licenseStatus));
        }

        /// <summary>Gets the bound Core model artifact, including path and optional hash. / 获取绑定的 Core 模型工件，包括路径和可选哈希。</summary>
        public ModelArtifact Artifact { get; }
        /// <summary>Gets the exact model revision or an explicit unverified marker. / 获取精确模型修订或明确的未验证标记。</summary>
        public string ModelVersion { get; }
        /// <summary>Gets the exact quantization identity. / 获取精确量化身份。</summary>
        public string Quantization { get; }
        /// <summary>Gets the tokenizer identity. / 获取 Tokenizer 身份。</summary>
        public string TokenizerIdentity { get; }
        /// <summary>Gets the chat-template identity. / 获取聊天模板身份。</summary>
        public string ChatTemplateIdentity { get; }
        /// <summary>Gets the generation configuration identity. / 获取生成配置身份。</summary>
        public string GenerationIdentity { get; }
        /// <summary>Gets the maximum context length when known. / 获取已知的最大上下文长度。</summary>
        public int? ContextLength { get; }
        /// <summary>Gets whether this profile has an embedding capability. / 获取此 Profile 是否具有嵌入能力。</summary>
        public bool EmbeddingsSupported { get; }
        /// <summary>Gets the backend identity that owns the runtime. / 获取拥有运行时的后端身份。</summary>
        public BackendId BackendId { get; }
        /// <summary>Gets the license/admission state, such as <c>caller-owned-unverified</c>. / 获取许可证或准入状态。</summary>
        public string LicenseStatus { get; }

        /// <summary>Creates an explicit unverified profile for a caller-owned GGUF. / 为调用方持有的 GGUF 创建明确的未验证 Profile。</summary>
        public static LanguageModelProfile CreateUnverified(ModelArtifact artifact, BackendId backendId, int? contextLength, bool embeddingsSupported)
        {
            return new LanguageModelProfile(
                artifact,
                "caller-supplied-unverified",
                "gguf-quantization-unverified",
                "gguf-embedded-tokenizer-unverified",
                "plain-text-prompt-unverified",
                "llamasharp-default-sampling-unverified",
                contextLength,
                embeddingsSupported,
                backendId,
                "caller-owned-unverified");
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty identity is required.", parameterName);
            return value.Trim();
        }
    }
}
