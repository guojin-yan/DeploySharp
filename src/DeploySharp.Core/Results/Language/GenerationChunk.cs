using System;

namespace JYPPX.DeploySharp.Results.Language
{
    /// <summary>
    /// Represents one ordered fragment emitted during text generation. / 表示文本生成期间输出的一个有序片段。
    /// </summary>
    public sealed class GenerationChunk
    {
        /// <summary>Initializes a generation fragment. / 初始化生成片段。</summary>
        public GenerationChunk(
            int sequenceIndex,
            string text,
            int? tokenId = null,
            GenerationFinishReason finishReason = GenerationFinishReason.None)
        {
            if (sequenceIndex < 0) throw new ArgumentOutOfRangeException(nameof(sequenceIndex));
            SequenceIndex = sequenceIndex;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            TokenId = tokenId;
            FinishReason = finishReason;
        }

        /// <summary>Gets the zero-based stream sequence index. / 获取从零开始的流序列索引。</summary>
        public int SequenceIndex { get; }

        /// <summary>Gets the emitted text fragment, which may be empty for a terminal chunk. / 获取输出的文本片段；终止片段可能为空。</summary>
        public string Text { get; }

        /// <summary>Gets the backend token identifier when exposed. / 获取后端公开的令牌标识符。</summary>
        public int? TokenId { get; }

        /// <summary>Gets the finish reason on a terminal chunk. / 获取终止片段的结束原因。</summary>
        public GenerationFinishReason FinishReason { get; }

        /// <summary>Gets whether this chunk terminates the generation stream. / 获取此片段是否终止生成流。</summary>
        public bool IsTerminal => FinishReason != GenerationFinishReason.None;
    }
}
