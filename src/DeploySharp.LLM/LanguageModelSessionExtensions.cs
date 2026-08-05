using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Results.Language;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Provides chat and synchronous convenience methods over the stable session contract. / 在稳定会话契约之上提供聊天和同步便捷方法。</summary>
    public static class LanguageModelSessionExtensions
    {
        /// <summary>Generates a chat completion using the session formatter. / 使用会话格式化器生成聊天补全。</summary>
        public static GenerationResult Generate(this ILanguageModelSession session, ChatCompletionRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (request == null) throw new ArgumentNullException(nameof(request));
            return session.Generate(new TextGenerationRequest(session.PromptFormatter.Format(request.History), request.Options), cancellationToken);
        }

        /// <summary>Generates a chat completion asynchronously. / 异步生成聊天补全。</summary>
        public static Task<GenerationResult> GenerateAsync(this ILanguageModelSession session, ChatCompletionRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (request == null) throw new ArgumentNullException(nameof(request));
            return session.GenerateAsync(new TextGenerationRequest(session.PromptFormatter.Format(request.History), request.Options), cancellationToken);
        }

        /// <summary>Streams a chat completion asynchronously. / 异步流式生成聊天补全。</summary>
        public static IAsyncEnumerable<GenerationChunk> StreamAsync(this ILanguageModelSession session, ChatCompletionRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (request == null) throw new ArgumentNullException(nameof(request));
            return session.StreamAsync(new TextGenerationRequest(session.PromptFormatter.Format(request.History), request.Options), cancellationToken);
        }

        /// <summary>Runs embedding synchronously. / 同步执行文本嵌入。</summary>
        public static EmbeddingResult Embed(this ILanguageModelSession session, TextEmbeddingRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return session.EmbedAsync(request, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Aggregates a stream into one generation result. / 将流式片段聚合为一个生成结果。</summary>
        public static async Task<GenerationResult> AggregateAsync(this ILanguageModelSession session, TextGenerationRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (request == null) throw new ArgumentNullException(nameof(request));
            var text = new StringBuilder();
            GenerationFinishReason reason = GenerationFinishReason.None;
            var tokenIds = new List<int>();
            await foreach (GenerationChunk chunk in session.StreamAsync(request, cancellationToken).ConfigureAwait(false))
            {
                text.Append(chunk.Text);
                if (chunk.TokenId.HasValue) tokenIds.Add(chunk.TokenId.Value);
                if (chunk.IsTerminal) reason = chunk.FinishReason;
            }

            return new GenerationResult(text.ToString(), reason, new TokenUsage(0, tokenIds.Count), tokenIds);
        }
    }
}
