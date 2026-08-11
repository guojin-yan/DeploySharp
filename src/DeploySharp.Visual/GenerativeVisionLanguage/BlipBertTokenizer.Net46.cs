#if !(NET8_0 || NET9_0 || NET10_0)
using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Exposes a stable capability boundary for TFMs outside the audited Microsoft tokenizer matrix. / 为已审计 Microsoft Tokenizer 矩阵之外的 TFM 公开稳定 Capability 边界。</summary>
    /// <remarks>The built-in adapter is audited only on net8.0, net9.0, and net10.0; other TFMs may use a caller-owned verified implementation. / 内置 Adapter 仅在 net8.0、net9.0 与 net10.0 审计；其他 TFM 可使用调用方自有且已校验的实现。</remarks>
    public sealed class BlipBertTokenizer : IGenerativeVisionLanguageTokenizer
    {
        /// <summary>Fails with a stable capability error outside the audited dependency matrix. / 在已审计依赖矩阵之外以稳定 Capability 错误失败。</summary>
        public BlipBertTokenizer(string vocabularyPath, GenerativeVisionLanguageTokenizerContract contract)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The built-in Microsoft.ML.Tokenizers adapter is audited only on net8.0, net9.0, and net10.0.");
        }

        /// <summary>Gets tokenizer contract when construction is inspected by reflection. / 反射检查构造时获取 Tokenizer 合同。</summary>
        public GenerativeVisionLanguageTokenizerContract Contract { get; }
        /// <summary>Gets the requested tokenizer identifier on the unavailable-TFM capability boundary. / 在不可用 TFM 能力边界获取所请求的 Tokenizer 标识。</summary>
        public string TokenizerId => Contract.TokenizerId;
        /// <summary>Gets the requested external vocabulary SHA256 on the unavailable-TFM capability boundary. / 在不可用 TFM 能力边界获取所请求外部词表的 SHA256。</summary>
        public string Sha256 => Contract.Sha256;
        /// <summary>Fails deterministically because the audited tokenizer dependency is unavailable on this TFM. / 因本 TFM 不具备已审计 Tokenizer 依赖而确定失败。</summary>
        public GenerativeTokenSequence EncodePrefix(GenerativeVisionLanguageProfile profile, GenerativeVisionLanguageRequest request) => throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The built-in BLIP BERT adapter is unavailable on this TFM.");
        /// <summary>Fails deterministically because the audited tokenizer dependency is unavailable on this TFM. / 因本 TFM 不具备已审计 Tokenizer 依赖而确定失败。</summary>
        public string DecodeCompletion(IEnumerable<int> tokenIds) => throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The built-in BLIP BERT adapter is unavailable on this TFM.");
    }
}
#endif
