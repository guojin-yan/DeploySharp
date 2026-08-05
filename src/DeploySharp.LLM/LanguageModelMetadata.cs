using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Reports immutable model and backend capabilities. / 报告不可变模型和后端能力。</summary>
    public sealed class LanguageModelMetadata
    {
        private readonly IReadOnlyList<string> _tags;

        /// <summary>Initializes language-model metadata. / 初始化语言模型元数据。</summary>
        public LanguageModelMetadata(
            ModelArtifact artifact,
            BackendDescriptor backend,
            LanguageModelCapabilities capabilities,
            int? contextLength = null,
            int? embeddingDimensions = null,
            string? device = null,
            IEnumerable<string>? tags = null)
        {
            Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
            Backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (contextLength.HasValue && contextLength.Value <= 0) throw new ArgumentOutOfRangeException(nameof(contextLength));
            if (embeddingDimensions.HasValue && embeddingDimensions.Value <= 0) throw new ArgumentOutOfRangeException(nameof(embeddingDimensions));
            if ((capabilities & LanguageModelCapabilities.Embeddings) != 0 && !embeddingDimensions.HasValue)
            {
                throw new ArgumentException("Embedding capability requires embedding dimensions.", nameof(embeddingDimensions));
            }

            Capabilities = capabilities;
            ContextLength = contextLength;
            EmbeddingDimensions = embeddingDimensions;
            Device = string.IsNullOrWhiteSpace(device) ? null : device;
            var copiedTags = new List<string>();
            if (tags != null)
            {
                foreach (string tag in tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag) && !copiedTags.Contains(tag, StringComparer.Ordinal)) copiedTags.Add(tag);
                }
            }

            _tags = copiedTags.AsReadOnly();
        }

        /// <summary>Gets the loaded model artifact. / 获取已加载的模型工件。</summary>
        public ModelArtifact Artifact { get; }
        /// <summary>Gets backend descriptor. / 获取后端描述信息。</summary>
        public BackendDescriptor Backend { get; }
        /// <summary>Gets language-model capabilities. / 获取语言模型能力。</summary>
        public LanguageModelCapabilities Capabilities { get; }
        /// <summary>Gets context length when known. / 获取已知的上下文长度。</summary>
        public int? ContextLength { get; }
        /// <summary>Gets embedding dimensions when embeddings are supported. / 获取支持嵌入时的向量维度。</summary>
        public int? EmbeddingDimensions { get; }
        /// <summary>Gets the selected device label. / 获取选定的设备标签。</summary>
        public string? Device { get; }
        /// <summary>Gets model and backend tags. / 获取模型和后端标签。</summary>
        public IReadOnlyList<string> Tags => _tags;
    }
}
