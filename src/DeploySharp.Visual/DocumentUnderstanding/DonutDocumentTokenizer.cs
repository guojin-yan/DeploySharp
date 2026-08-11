#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.Tokenizers;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Adapts Microsoft SentencePiece Unigram to the exact official Donut XLM-R tokenizer and task tags. / 将 Microsoft SentencePiece Unigram 适配到精确官方 Donut XLM-R Tokenizer 与 Task Tag。</summary>
    /// <remarks>The tokenizer owns parsed vocabulary state only; external files and document/backend sessions remain caller-owned. / Tokenizer 仅拥有 Parsed Vocabulary State；外部文件与 Document/Backend Session 保持调用方所有。</remarks>
    public sealed class DonutDocumentTokenizer : IDocumentUnderstandingTokenizer
    {
        private readonly SentencePieceTokenizer _tokenizer;

        /// <summary>Loads and verifies sentencepiece, tokenizer.json, and added-token sidecars from one external checkpoint directory. / 从一个 External Checkpoint 目录加载并校验 SentencePiece、tokenizer.json 与 Added-token Sidecar。</summary>
        public DonutDocumentTokenizer(string checkpointDirectory, DocumentTokenizerContract contract)
        {
            if (string.IsNullOrWhiteSpace(checkpointDirectory)) throw new ArgumentException("A checkpoint directory is required.", nameof(checkpointDirectory));
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            string root = Path.GetFullPath(checkpointDirectory);
            string model = Path.Combine(root, "sentencepiece.bpe.model");
            string tokenizerJson = Path.Combine(root, "tokenizer.json");
            string addedTokens = Path.Combine(root, "added_tokens.json");
            Verify(model, contract.ModelSha256, "sentencepiece.bpe.model");
            Verify(tokenizerJson, contract.TokenizerJsonSha256, "tokenizer.json");
            Verify(addedTokens, contract.AddedTokensSha256, "added_tokens.json");
            Dictionary<string, int>? special = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllBytes(addedTokens));
            if (special == null || special.Count == 0 || special.Values.Distinct().Count() != special.Count || special.Any(value => value.Value < 0 || value.Value >= contract.VocabularySize) || !special.TryGetValue(contract.DefaultTaskPrompt, out int promptId) || promptId != 57579) throw new VisualException(VisualErrorCodes.DocumentUnderstandingTokenizerInvalid, "Donut added tokens differ from the profile prompt/vocabulary contract.");
            special.Add("<s>", contract.BosTokenId); special.Add("<pad>", contract.PadTokenId); special.Add("</s>", contract.EosTokenId); special.Add("<unk>", contract.UnknownTokenId); special.Add("<mask>", 57521);
            using (FileStream stream = File.OpenRead(model)) _tokenizer = SentencePieceTokenizer.Create(stream, false, false, special);
            TokenizerId = contract.TokenizerId; Identity = contract.Identity;
        }

        /// <summary>Gets exact tokenizer contract. / 获取精确 Tokenizer 合同。</summary>
        public DocumentTokenizerContract Contract { get; }
        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets verified tokenizer/template identity. / 获取已校验 Tokenizer/Template Identity。</summary>
        public string Identity { get; }

        /// <summary>Encodes the exact schema-bound Donut task prompt without adding substitute BOS/EOS tokens. / 编码精确 Schema-bound Donut Task Prompt，不添加替代 BOS/EOS Token。</summary>
        public DocumentTokenSequence Encode(DocumentUnderstandingProfile profile, DocumentTaskRequest request)
        {
            if (profile == null || request == null) throw new ArgumentNullException(profile == null ? nameof(profile) : nameof(request));
            if (profile.Family != DocumentUnderstandingFamily.Donut || !profile.Tasks.Contains(request.Task)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "The request task is not supported by this Donut profile.", profileId: profile.ProfileId);
            if (!string.Equals(profile.Tokenizer.Identity, Contract.Identity, StringComparison.Ordinal) || !string.Equals(profile.Tokenizer.TokenizerId, TokenizerId, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "The tokenizer differs from the profile-bound assets.", profileId: profile.ProfileId);
            if (!string.Equals(request.SchemaId, profile.Schema.SchemaId, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "The request schema differs from the profile-bound schema.", profileId: profile.ProfileId);
            string prompt = string.IsNullOrWhiteSpace(request.Prompt) ? Contract.DefaultTaskPrompt : request.Prompt;
            if (!string.Equals(prompt, Contract.DefaultTaskPrompt, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingCapabilityUnavailable, "This Donut checkpoint supports only its exact CORD-v2 task prompt.", profileId: profile.ProfileId);
            IReadOnlyList<int> ids = _tokenizer.EncodeToIds(prompt, false, false, true, true);
            if (ids.Count != 1 || ids[0] != 57579) throw new VisualException(VisualErrorCodes.DocumentUnderstandingTokenizerInvalid, "The exact CORD-v2 prompt did not encode to token 57579.", profileId: profile.ProfileId);
            return new DocumentTokenSequence(prompt, ids.Select(value => (long)value), TokenizerId, Identity);
        }

        /// <summary>Decodes generated IDs while retaining schema tags and excluding EOS/padding. / 解码生成 ID，保留 Schema Tag 并排除 EOS/Padding。</summary>
        public string Decode(IEnumerable<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            var values = tokenIds.Where(value => value != Contract.EosTokenId && value != Contract.PadTokenId).Select(value => value >= 4 && value <= 57520 ? value - 1 : value).ToList();
            if (values.Any(value => value < 0 || value >= Contract.VocabularySize)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingTokenizerInvalid, "A generated token ID is outside the Donut vocabulary.");
            return values.Count == 0 ? string.Empty : _tokenizer.Decode(values, true).Trim();
        }

        private static void Verify(string path, string expected, string role)
        {
            if (!File.Exists(path)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingTokenizerInvalid, "A required external tokenizer file is missing.", technicalDetails: role + "=" + path);
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
            {
                string actual = string.Concat(hash.ComputeHash(stream).Select(value => value.ToString("x2")));
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "An external document tokenizer SHA256 differs from the profile.", technicalDetails: role + ";expected=" + expected + ";actual=" + actual);
            }
        }
    }
}
#endif
