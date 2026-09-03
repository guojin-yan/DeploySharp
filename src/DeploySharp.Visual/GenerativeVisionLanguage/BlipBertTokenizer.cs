#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.ML.Tokenizers;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Adapts Microsoft's Hugging Face-derived BERT WordPiece implementation to an external BLIP vocabulary. / 将 Microsoft 基于 Hugging Face 的 BERT WordPiece 实现适配到外部 BLIP 词表。</summary>
    /// <remarks>The vocabulary file remains caller-owned and external; construction verifies its exact SHA256 before any tokenization. / 词表文件仍由调用方拥有并保持 External；构造会在任何 Tokenize 前校验精确 SHA256。</remarks>
    public sealed class BlipBertTokenizer : IGenerativeVisionLanguageTokenizer
    {
        private readonly BertTokenizer _tokenizer;

        /// <summary>Loads and verifies the profile-bound external <c>bert-base-uncased</c> vocabulary. / 加载并校验 Profile 绑定的外部 <c>bert-base-uncased</c> 词表。</summary>
        public BlipBertTokenizer(string vocabularyPath, GenerativeVisionLanguageTokenizerContract contract)
        {
            if (string.IsNullOrWhiteSpace(vocabularyPath)) throw new ArgumentException("A vocabulary path is required.", nameof(vocabularyPath));
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            string fullPath = Path.GetFullPath(vocabularyPath);
            if (!File.Exists(fullPath)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageTokenizerInvalid, "The external tokenizer vocabulary is missing.", technicalDetails: fullPath);
            string actualSha = ComputeSha256(fullPath);
            if (!string.Equals(actualSha, contract.Sha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "The tokenizer vocabulary SHA256 differs from the profile.", technicalDetails: "expected=" + contract.Sha256 + ";actual=" + actualSha);
            _tokenizer = BertTokenizer.Create(fullPath, new BertOptions
            {
                LowerCaseBeforeTokenization = true,
                ApplyBasicTokenization = true,
                SplitOnSpecialTokens = true,
                SeparatorToken = "[SEP]",
                PaddingToken = "[PAD]",
                ClassificationToken = "[CLS]",
                MaskingToken = "[MASK]",
                IndividuallyTokenizeCjk = true,
                RemoveNonSpacingMarks = true
            });
            TokenizerId = contract.TokenizerId;
            Sha256 = contract.Sha256;
        }

        /// <summary>Gets tokenizer contract. / 获取 Tokenizer 合同。</summary>
        public GenerativeVisionLanguageTokenizerContract Contract { get; }
        /// <summary>Gets the exact profile-bound tokenizer identifier. / 获取 Profile 绑定的精确 Tokenizer 标识。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets the verified external vocabulary SHA256. / 获取已校验外部词表的 SHA256。</summary>
        public string Sha256 { get; }

        /// <summary>Applies the exact Profile template and returns an owned BLIP decoder prefix. / 应用精确 Profile 模板并返回自有 BLIP Decoder Prefix。</summary>
        public GenerativeTokenSequence EncodePrefix(GenerativeVisionLanguageProfile profile, GenerativeVisionLanguageRequest request)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Task != profile.Task) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "The request task differs from the profile task.", profileId: profile.ProfileId);
            if (!string.Equals(TokenizerId, profile.Tokenizer.TokenizerId, StringComparison.Ordinal) || !string.Equals(Sha256, profile.Tokenizer.Sha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "The tokenizer instance differs from the profile-bound tokenizer.", profileId: profile.ProfileId);
            if (request.Text.Length > profile.MaximumRequestCharacters) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageLimitExceeded, "The question or instruction exceeds the profile character capacity.", profileId: profile.ProfileId);

            string prompt = profile.PromptTemplate.IndexOf("{0}", StringComparison.Ordinal) >= 0
                ? string.Format(CultureInfo.InvariantCulture, profile.PromptTemplate, request.Text)
                : profile.PromptTemplate;
            IReadOnlyList<int> encoded = _tokenizer.EncodeToIds(prompt, true, true, true);
            if (encoded.Count < 2 || encoded[0] != profile.Tokenizer.ClassificationTokenId || encoded[encoded.Count - 1] != profile.Tokenizer.EosTokenId) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageTokenizerInvalid, "The official BERT special-token layout did not match the BLIP prefix contract.", profileId: profile.ProfileId);
            var prefix = encoded.Select(value => (long)value).ToList();
            prefix[0] = profile.Tokenizer.BosTokenId;
            prefix.RemoveAt(prefix.Count - 1);
            if (prefix.Count == 0 || prefix.Count > profile.Tokenizer.MaximumPromptTokens || prefix.Count >= profile.Generation.MaximumTotalTokens) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageLimitExceeded, "The normalized prompt exceeds the profile token capacity.", profileId: profile.ProfileId);
            return new GenerativeTokenSequence(prompt, prefix.ToArray(), TokenizerId, Sha256);
        }

        /// <summary>Decodes owned completion IDs after excluding the Profile EOS and padding tokens. / 排除 Profile EOS 与 Padding Token 后解码自有 Completion ID。</summary>
        public string DecodeCompletion(IEnumerable<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            int capacity = tokenIds is ICollection<int> collection ? collection.Count : 8;
            var values = new List<int>(capacity);
            foreach (int value in tokenIds)
            {
                if (value < 0 || value >= Contract.VocabularySize) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageTokenizerInvalid, "A generated token ID is outside the tokenizer vocabulary.");
                if (value == Contract.EosTokenId || value == Contract.PadTokenId) continue;
                values.Add(value);
            }
            return values.Count == 0 ? string.Empty : _tokenizer.Decode(values);
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create()) return string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
    }
}
#endif
