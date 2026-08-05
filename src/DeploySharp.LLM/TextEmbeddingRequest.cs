using System;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Describes one embedding request. / 描述一次嵌入请求。</summary>
    public sealed class TextEmbeddingRequest
    {
        /// <summary>Initializes an embedding request. / 初始化嵌入请求。</summary>
        public TextEmbeddingRequest(string text, bool normalize = true, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Embedding text cannot be empty.", nameof(text));
            if (timeout.HasValue && (timeout.Value <= TimeSpan.Zero || timeout.Value == System.Threading.Timeout.InfiniteTimeSpan)) throw new ArgumentOutOfRangeException(nameof(timeout));
            Text = text;
            Normalize = normalize;
            Timeout = timeout;
        }

        /// <summary>Gets the text to embed. / 获取待嵌入文本。</summary>
        public string Text { get; }
        /// <summary>Gets whether the result must be L2-normalized. / 获取是否要求结果进行 L2 归一化。</summary>
        public bool Normalize { get; }
        /// <summary>Gets an optional operation timeout. / 获取可选操作超时。</summary>
        public TimeSpan? Timeout { get; }
    }
}
