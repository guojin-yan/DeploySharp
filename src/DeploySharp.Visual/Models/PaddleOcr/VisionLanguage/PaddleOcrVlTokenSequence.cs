using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Owns a single-image PaddleOCR-VL prompt after image-token expansion. / 持有完成图像 Token 展开的 PaddleOCR-VL 单图 Prompt。</summary>
    public sealed class PaddleOcrVlTokenSequence
    {
        private readonly long[] _tokenIds;

        internal PaddleOcrVlTokenSequence(string template, long[] tokenIds, PaddleOcrVlImagePlan imagePlan,
            int imageTokenOffset, int maximumNewTokens, string tokenizerSha256)
        {
            PromptTemplate = template;
            _tokenIds = tokenIds;
            TokenIds = Array.AsReadOnly(tokenIds);
            ImagePlan = imagePlan;
            ImageTokenOffset = imageTokenOffset;
            MaximumNewTokens = maximumNewTokens;
            TokenizerSha256 = tokenizerSha256;
        }

        /// <summary>Gets the exact chat template before image-marker repetition. / 获取图像占位符重复之前的精确 Chat Template。</summary>
        public string PromptTemplate { get; }
        /// <summary>Gets read-only request-owned token IDs; no state is shared with another request. / 获取请求独立持有的只读 Token ID，不与其它请求共享状态。</summary>
        public IReadOnlyList<long> TokenIds { get; }
        /// <summary>Gets the expanded prompt length. / 获取展开后的 Prompt 长度。</summary>
        public int Count => _tokenIds.Length;
        /// <summary>Gets the corresponding image geometry and token count. / 获取对应图像几何与 Token 数量。</summary>
        public PaddleOcrVlImagePlan ImagePlan { get; }
        /// <summary>Gets the first image token's zero-based offset. / 获取首个图像 Token 的零基偏移。</summary>
        public int ImageTokenOffset { get; }
        /// <summary>Gets the generation budget reserved during context validation. / 获取上下文校验时预留的生成 Token 预算。</summary>
        public int MaximumNewTokens { get; }
        /// <summary>Gets the verified tokenizer and chat-template identity. / 获取已校验的 Tokenizer 与 Chat Template 标识。</summary>
        public string TokenizerSha256 { get; }
        /// <summary>Returns an independent mutable copy for tensor creation. / 返回用于创建 Tensor 的独立可修改副本。</summary>
        public long[] CopyTokenIds() => (long[])_tokenIds.Clone();
    }
}
