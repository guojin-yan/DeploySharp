using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Results.Language
{
    /// <summary>
    /// Contains completed generated text, usage, and terminal metadata. / 包含已完成生成文本、用量和终止元数据。
    /// </summary>
    public sealed class GenerationResult
    {
        private readonly IReadOnlyList<int> _tokenIds;

        /// <summary>Initializes a generation result. / 初始化生成结果。</summary>
        public GenerationResult(
            string text,
            GenerationFinishReason finishReason,
            TokenUsage usage,
            IEnumerable<int>? tokenIds = null)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            FinishReason = finishReason;
            Usage = usage ?? throw new ArgumentNullException(nameof(usage));
            _tokenIds = new List<int>(tokenIds ?? new int[0]).AsReadOnly();
        }

        /// <summary>Gets completed generated text. / 获取已完成的生成文本。</summary>
        public string Text { get; }

        /// <summary>Gets the terminal reason. / 获取终止原因。</summary>
        public GenerationFinishReason FinishReason { get; }

        /// <summary>Gets prompt and generated token counts. / 获取提示词和生成令牌数量。</summary>
        public TokenUsage Usage { get; }

        /// <summary>Gets emitted token identifiers when exposed by the backend. / 获取后端公开的输出令牌标识符。</summary>
        public IReadOnlyList<int> TokenIds => _tokenIds;
    }
}
