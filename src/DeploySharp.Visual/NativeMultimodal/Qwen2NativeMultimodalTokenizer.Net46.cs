#if !(NET8_0 || NET9_0 || NET10_0)
using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Exposes the audited Qwen2 tokenizer capability boundary on older TFMs. / 在旧 TFM 公开已审计 Qwen2 Tokenizer 能力边界。</summary>
    public sealed class Qwen2NativeMultimodalTokenizer : INativeMultimodalTokenizer
    {
        /// <summary>Fails because the audited Microsoft tokenizer dependency is available only on net8.0 and later targets declared here. / 因已审计 Microsoft Tokenizer 依赖仅用于此处声明的 net8.0 及更新 TFM 而失败。</summary>
        public Qwen2NativeMultimodalTokenizer(string modelDirectory, NativeMultimodalTokenizerContract contract)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            throw new VisualException(VisualErrorCodes.NativeMultimodalCapabilityUnavailable, "The managed Qwen2 ByteLevel BPE adapter is audited only on net8.0, net9.0, and net10.0.");
        }

        /// <summary>Gets requested tokenizer contract. / 获取请求的 Tokenizer 合同。</summary>
        public NativeMultimodalTokenizerContract Contract { get; }
        /// <summary>Gets requested tokenizer ID. / 获取请求的 Tokenizer ID。</summary>
        public string TokenizerId => Contract.TokenizerId;
        /// <summary>Gets requested tokenizer identity. / 获取请求的 Tokenizer Identity。</summary>
        public string Sha256 => Contract.Identity;
        /// <summary>Fails at the stable capability boundary. / 在稳定能力边界失败。</summary>
        public NativeMultimodalTokenSequence Encode(NativeMultimodalProfile profile, GenerativeVisionLanguageRequest request, int imageTokenCount) => throw new VisualException(VisualErrorCodes.NativeMultimodalCapabilityUnavailable, "The managed Qwen2 tokenizer is unavailable on this TFM.");
        /// <summary>Fails at the stable capability boundary. / 在稳定能力边界失败。</summary>
        public string DecodeCompletion(IEnumerable<int> tokenIds) => throw new VisualException(VisualErrorCodes.NativeMultimodalCapabilityUnavailable, "The managed Qwen2 tokenizer is unavailable on this TFM.");
    }
}
#endif
