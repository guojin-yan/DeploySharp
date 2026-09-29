using System;
using System.Collections.Generic;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Provides semantic detokenization for FormulaNet/UniMERNet token IDs. / 为 FormulaNet/UniMERNet token ID 提供语义反分词。</summary>
    public interface IPaddleDocumentFormulaTokenizer
    {
        /// <summary>Gets the vocabulary indexed by model token ID. / 获取按模型 token ID 索引的词表。</summary>
        public IReadOnlyList<string> Tokens { get; }

        /// <summary>Decodes one sequence after control tokens have been removed. / 在移除控制 token 后解码一个序列。</summary>
        public string Decode(IReadOnlyList<int> tokenIds);
    }

    /// <summary>Portable token-piece fallback used when a BPE tokenizer is not available. / 在没有 BPE tokenizer 时使用的可移植 token-piece fallback。</summary>
    public sealed class PaddleDocumentFormulaTokenPieceTokenizer : IPaddleDocumentFormulaTokenizer
    {
        private readonly IReadOnlyList<string> _tokens;

        public PaddleDocumentFormulaTokenPieceTokenizer(IEnumerable<string> tokens)
        {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            var values = new List<string>(tokens);
            if (values.Count == 0) throw new ArgumentException("Formula vocabulary cannot be empty.", nameof(tokens));
            for (int index = 0; index < values.Count; index++) values[index] ??= string.Empty;
            _tokens = values.AsReadOnly();
        }

        public IReadOnlyList<string> Tokens => _tokens;

        public string Decode(IReadOnlyList<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            var result = new System.Text.StringBuilder();
            foreach (int id in tokenIds)
            {
                if (id < 0 || id >= _tokens.Count) throw new ArgumentOutOfRangeException(nameof(tokenIds));
                result.Append(_tokens[id]);
            }
            return result.ToString();
        }
    }
}

#pragma warning restore CS1591
