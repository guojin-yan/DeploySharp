using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.LLM.Prompt;
using JYPPX.DeploySharp.Results.Language;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Represents a loaded, reusable language-model session. / 表示一个已加载且可复用的语言模型会话。</summary>
    public interface ILanguageModelSession : IDisposable
    {
        /// <summary>Gets immutable model metadata. / 获取不可变模型元数据。</summary>
        public LanguageModelMetadata Metadata { get; }

        /// <summary>Generates text synchronously and serializes access within this session. / 同步生成文本并串行化同一会话内的访问。</summary>
        public GenerationResult Generate(TextGenerationRequest request, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Generates text asynchronously. / 异步生成文本。</summary>
        public Task<GenerationResult> GenerateAsync(TextGenerationRequest request, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Streams ordered generation chunks; one terminal chunk is emitted on normal completion or cancellation. / 流式返回有序生成片段，正常完成或取消时均返回一个终止片段。</summary>
        public IAsyncEnumerable<GenerationChunk> StreamAsync(TextGenerationRequest request, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Embeds one text input when the session declares embedding capability. / 在会话声明嵌入能力时生成一条文本嵌入。</summary>
        public Task<EmbeddingResult> EmbedAsync(TextEmbeddingRequest request, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Gets the prompt formatter used by chat extension methods. / 获取聊天扩展方法使用的提示词格式化器。</summary>
        public IPromptFormatter PromptFormatter { get; }
    }
}
