#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.ML.Tokenizers;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Adapts Microsoft ByteLevel BPE to the exact external Qwen2 tokenizer and LLaVA chat template. / 将 Microsoft ByteLevel BPE 适配到精确外部 Qwen2 Tokenizer 与 LLaVA Chat Template。</summary>
    /// <remarks>All tokenizer files remain caller-owned; construction verifies tokenizer.json, vocab.json, and merges.txt before use. / 全部 Tokenizer 文件保持调用方所有；构造会在使用前校验三个资产。</remarks>
    public sealed class Qwen2NativeMultimodalTokenizer : INativeMultimodalTokenizer
    {
        private readonly BpeTokenizer _tokenizer;

        /// <summary>Loads and verifies exact tokenizer assets from one external model directory. / 从一个外部模型目录加载并校验精确 Tokenizer 资产。</summary>
        public Qwen2NativeMultimodalTokenizer(string modelDirectory, NativeMultimodalTokenizerContract contract)
        {
            if (string.IsNullOrWhiteSpace(modelDirectory)) throw new ArgumentException("A model directory is required.", nameof(modelDirectory));
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            string root = Path.GetFullPath(modelDirectory);
            string tokenizerJson = Path.Combine(root, "tokenizer.json");
            string vocabulary = Path.Combine(root, "vocab.json");
            string merges = Path.Combine(root, "merges.txt");
            Verify(tokenizerJson, contract.TokenizerJsonSha256, "tokenizer.json");
            Verify(vocabulary, contract.VocabularySha256, "vocab.json");
            Verify(merges, contract.MergesSha256, "merges.txt");
            var special = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["<|endoftext|>"] = contract.EndOfTextTokenId,
                ["<|im_start|>"] = contract.ImStartTokenId,
                ["<|im_end|>"] = contract.ImEndTokenId,
                ["<image>"] = contract.ImageTokenId,
                ["<video>"] = 151647
            };
            var options = new BpeOptions(vocabulary, merges)
            {
                ByteLevel = true,
                PreTokenizer = new RegexPreTokenizer(new Regex(contract.RegexPattern, RegexOptions.CultureInvariant), special),
                SpecialTokens = special
            };
            _tokenizer = BpeTokenizer.Create(options);
            TokenizerId = contract.TokenizerId;
            Sha256 = contract.Identity;
        }

        /// <summary>Gets exact tokenizer contract. / 获取精确 Tokenizer 合同。</summary>
        public NativeMultimodalTokenizerContract Contract { get; }
        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets composite verified tokenizer identity. / 获取复合已校验 Tokenizer Identity。</summary>
        public string Sha256 { get; }

        /// <summary>Applies the bound single-image chat template and expands its one image sentinel. / 应用绑定的单图 Chat Template 并展开其中唯一图像 Sentinel。</summary>
        public NativeMultimodalTokenSequence Encode(NativeMultimodalProfile profile, GenerativeVisionLanguageRequest request, int imageTokenCount)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!profile.Tasks.Contains(request.Task)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "The request task is not supported by this profile.", profileId: profile.ProfileId);
            if (!string.Equals(profile.Tokenizer.Identity, Contract.Identity, StringComparison.Ordinal) || !string.Equals(profile.Tokenizer.TokenizerId, TokenizerId, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.NativeMultimodalIdentityMismatch, "The tokenizer differs from the profile-bound assets.", profileId: profile.ProfileId);
            if (request.Text.Length > profile.MaximumRequestCharacters || imageTokenCount <= 0) throw new VisualException(VisualErrorCodes.NativeMultimodalLimitExceeded, "The request text or image-token count exceeds capacity.", profileId: profile.ProfileId);
            string requestText = request.Task == GenerativeVisionLanguageTask.ImageCaptioning && string.IsNullOrEmpty(request.Text) ? Contract.DefaultCaptionPrompt : request.Text;
            string prompt = string.Format(CultureInfo.InvariantCulture, Contract.ChatTemplate, requestText);
            IReadOnlyList<int> baseIds = _tokenizer.EncodeToIds(prompt);
            int sentinelCount = baseIds.Count(value => value == Contract.ImageTokenId);
            if (sentinelCount != 1) throw new VisualException(VisualErrorCodes.NativeMultimodalTokenizerInvalid, "The exact chat template must produce one image sentinel before expansion.", profileId: profile.ProfileId);
            var expanded = new List<long>(checked(baseIds.Count + imageTokenCount - 1));
            foreach (int value in baseIds)
            {
                if (value == Contract.ImageTokenId) for (int index = 0; index < imageTokenCount; index++) expanded.Add(value);
                else expanded.Add(value);
            }
            if (expanded.Count + profile.Generation.MaximumTotalTokens > Contract.MaximumContextTokens) throw new VisualException(VisualErrorCodes.NativeMultimodalLimitExceeded, "Expanded prompt plus completion exceeds the profile context capacity.", profileId: profile.ProfileId);
            if (expanded.Count(value => value == Contract.ImageTokenId) != imageTokenCount || expanded.Any(value => value < 0 || value >= Contract.VocabularySize)) throw new VisualException(VisualErrorCodes.NativeMultimodalTokenizerInvalid, "Expanded token IDs differ from the image-sentinel or vocabulary contract.", profileId: profile.ProfileId);
            return new NativeMultimodalTokenSequence(prompt, expanded, imageTokenCount, TokenizerId, Sha256);
        }

        /// <summary>Decodes completion IDs after excluding EOS and end-of-text padding. / 排除 EOS 与 End-of-text Padding 后解码 Completion ID。</summary>
        public string DecodeCompletion(IEnumerable<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            int capacity = tokenIds is ICollection<int> collection ? collection.Count : 8;
            var values = new List<int>(capacity);
            foreach (int value in tokenIds)
            {
                if (value < 0 || value >= Contract.VocabularySize) throw new VisualException(VisualErrorCodes.NativeMultimodalTokenizerInvalid, "A completion token is outside the tokenizer vocabulary.");
                if (value == Contract.ImEndTokenId || value == Contract.EndOfTextTokenId) continue;
                values.Add(value);
            }
            return values.Count == 0 ? string.Empty : _tokenizer.Decode(values);
        }

        private static void Verify(string path, string expected, string role)
        {
            if (!File.Exists(path)) throw new VisualException(VisualErrorCodes.NativeMultimodalTokenizerInvalid, "A required external tokenizer file is missing.", technicalDetails: role + "=" + path);
            using (var stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create())
            {
                string actual = string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2")));
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.NativeMultimodalIdentityMismatch, "An external tokenizer file SHA256 differs from the profile.", technicalDetails: role + ";expected=" + expected + ";actual=" + actual);
            }
        }
    }
}
#endif
