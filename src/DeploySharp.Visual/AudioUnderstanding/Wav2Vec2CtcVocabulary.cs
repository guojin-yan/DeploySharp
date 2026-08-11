using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
#if NET8_0 || NET9_0 || NET10_0
using System.Text.Json;
#endif

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Owns a verified fixed Wav2Vec2 CTC vocabulary. / 拥有已验证固定 Wav2Vec2 CTC 词表。</summary>
    public sealed class Wav2Vec2CtcVocabulary
    {
        private readonly IReadOnlyDictionary<int, string> _tokens;

        /// <summary>Initializes a vocabulary from explicit ID/token pairs and validates every bound special token. / 从显式 ID/Token 对初始化词表并验证全部绑定特殊 Token。</summary>
        public Wav2Vec2CtcVocabulary(AudioTokenizerContract contract, IReadOnlyDictionary<int, string> tokens)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            if (tokens == null || tokens.Count != contract.VocabularySize || tokens.Keys.OrderBy(value => value).Where((value, index) => value != index).Any() || tokens.Values.Any(string.IsNullOrEmpty)) throw new VisualException(VisualErrorCodes.AudioCtcDecodeInvalid, "CTC vocabulary IDs must be contiguous, unique, and non-empty.");
            var copy = new Dictionary<int, string>();
            foreach (KeyValuePair<int, string> token in tokens) copy.Add(token.Key, token.Value);
            if (!copy.TryGetValue(contract.BlankTokenId, out string? blank) || blank != "<pad>" || !copy.TryGetValue(contract.UnknownTokenId, out string? unknown) || unknown != "<unk>" || !copy.TryGetValue(contract.WordDelimiterTokenId, out string? delimiter) || delimiter != contract.WordDelimiterToken) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "CTC special tokens differ from the profile-bound vocabulary.");
            _tokens = new ReadOnlyDictionary<int, string>(copy); Identity = AudioUnderstandingHash.Text(contract.Identity + "|" + string.Join("|", copy.OrderBy(value => value.Key).Select(value => value.Key + ":" + value.Value)));
        }

        /// <summary>Gets tokenizer contract. / 获取 Tokenizer 合同。</summary>
        public AudioTokenizerContract Contract { get; }
        /// <summary>Gets verified vocabulary identity. / 获取已验证词表 Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets one token without fallback. / 获取一个 Token 且不回退。</summary>
        public string GetToken(int tokenId) { if (!_tokens.TryGetValue(tokenId, out string? token)) throw new VisualException(VisualErrorCodes.AudioCtcDecodeInvalid, "CTC token ID is outside the vocabulary.", technicalDetails: tokenId.ToString()); return token; }

        /// <summary>Loads and SHA-verifies official `vocab.json` on modern declared TFMs. / 在现代声明 TFM 加载并校验官方 `vocab.json` SHA。</summary>
        public static Wav2Vec2CtcVocabulary Load(string path, AudioTokenizerContract contract)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A vocabulary path is required.", nameof(path));
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (!File.Exists(path)) throw new VisualException(VisualErrorCodes.AudioCtcDecodeInvalid, "The external CTC vocabulary is missing.", technicalDetails: path);
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
            {
                string actual = string.Concat(hash.ComputeHash(stream).Select(value => value.ToString("x2")));
                if (!string.Equals(actual, contract.VocabularySha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "The CTC vocabulary SHA-256 differs from the profile.", technicalDetails: "expected=" + contract.VocabularySha256 + ";actual=" + actual);
            }
#if NET8_0 || NET9_0 || NET10_0
            Dictionary<string, int>? serialized = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllBytes(path));
            if (serialized == null) throw new VisualException(VisualErrorCodes.AudioCtcDecodeInvalid, "The CTC vocabulary JSON is invalid.");
            return new Wav2Vec2CtcVocabulary(contract, new Dictionary<int, string>(serialized.ToDictionary(value => value.Value, value => value.Key)));
#else
            throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "Built-in JSON loading for the audited CTC vocabulary is available on net8.0 and later declared targets; construct the vocabulary from a structured parser on older TFMs.");
#endif
        }
    }
}
