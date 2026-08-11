#if !NET8_0 && !NET9_0 && !NET10_0
using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Exposes the audited Donut tokenizer capability boundary on older TFMs. / 在旧 TFM 公开已审计 Donut Tokenizer 能力边界。</summary>
    public sealed class DonutDocumentTokenizer : IDocumentUnderstandingTokenizer
    {
        /// <summary>Fails because the audited Microsoft SentencePiece dependency is enabled only on net8.0 and later declared targets. / 因已审计 Microsoft SentencePiece 依赖仅在声明的 net8.0 及更新目标启用而失败。</summary>
        public DonutDocumentTokenizer(string checkpointDirectory, DocumentTokenizerContract contract)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The built-in Donut SentencePiece tokenizer is audited only on net8.0, net9.0, and net10.0.");
        }
        /// <summary>Gets requested tokenizer contract. / 获取请求的 Tokenizer 合同。</summary>
        public DocumentTokenizerContract Contract { get; }
        /// <summary>Gets requested tokenizer ID. / 获取请求的 Tokenizer ID。</summary>
        public string TokenizerId => Contract.TokenizerId;
        /// <summary>Gets requested tokenizer identity. / 获取请求的 Tokenizer Identity。</summary>
        public string Identity => Contract.Identity;
        /// <summary>Fails deterministically because this TFM lacks the audited tokenizer. / 因本 TFM 缺少已审计 Tokenizer 而确定失败。</summary>
        public DocumentTokenSequence Encode(DocumentUnderstandingProfile profile, DocumentTaskRequest request) => throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "Donut tokenizer encoding is unavailable on this TFM.");
        /// <summary>Fails deterministically because this TFM lacks the audited tokenizer. / 因本 TFM 缺少已审计 Tokenizer 而确定失败。</summary>
        public string Decode(IEnumerable<int> tokenIds) => throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "Donut tokenizer decoding is unavailable on this TFM.");
    }
}
#endif
