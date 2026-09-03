#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.ML.Tokenizers;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Loads the pinned Whisper ByteLevel BPE tokenizer without writing generated sidecar files. / 加载固定 Whisper ByteLevel BPE Tokenizer，且不写入生成的旁车文件。</summary>
    /// <remarks>The tokenizer.json remains caller-owned. The adapter parses its embedded vocabulary and merges directly into BpeOptions, so construction is read-only and deterministic. / tokenizer.json 保持调用方所有；Adapter 直接将内嵌词表与 Merge 解析到 BpeOptions，因此构造过程只读且确定。</remarks>
    public sealed class WhisperTokenizer
    {
        private const string StartOfTranscript = "<|startoftranscript|>";
        private const string EndOfText = "<|endoftext|>";
        private const string NoTimestamps = "<|notimestamps|>";
        private const string WhisperEnglish = "<|en|>";
        private const string WhisperTranscribe = "<|transcribe|>";
        private const string WhisperRegex = @"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+";

        private readonly BpeTokenizer _tokenizer;
        private readonly IReadOnlyDictionary<string, int> _specialTokens;

        /// <summary>Loads and verifies the exact Whisper tokenizer and generation config. / 加载并校验精确 Whisper Tokenizer 与 Generation Config。</summary>
        public WhisperTokenizer(string checkpointDirectory, AudioGenerationContract contract)
        {
            if (string.IsNullOrWhiteSpace(checkpointDirectory)) throw new ArgumentException("A checkpoint directory is required.", nameof(checkpointDirectory));
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            string root = Path.GetFullPath(checkpointDirectory);
            string tokenizerPath = Path.Combine(root, "tokenizer.json");
            string generationConfigPath = Path.Combine(root, "generation_config.json");
            Verify(tokenizerPath, contract.TokenizerSha256, "tokenizer.json");
            Verify(generationConfigPath, contract.GenerationConfigSha256, "generation_config.json");

            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(tokenizerPath));
            JsonElement model = document.RootElement.GetProperty("model");
            var vocabulary = new List<KeyValuePair<string, int>>();
            foreach (JsonProperty item in model.GetProperty("vocab").EnumerateObject()) vocabulary.Add(new KeyValuePair<string, int>(item.Name, item.Value.GetInt32()));
            var merges = model.GetProperty("merges").EnumerateArray().Select(value => value.GetString() ?? throw InvalidTokenizer("A Whisper merge entry is not a string.")).ToArray();
            var special = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (JsonElement item in document.RootElement.GetProperty("added_tokens").EnumerateArray())
            {
                string content = item.GetProperty("content").GetString() ?? throw InvalidTokenizer("A Whisper added token has no content.");
                int id = item.GetProperty("id").GetInt32();
                if (id < 0 || id >= contract.VocabularySize || (special.TryGetValue(content, out int previous) && previous != id)) throw InvalidTokenizer("Whisper added-token IDs are inconsistent.");
                special[content] = id;
                if (!vocabulary.Any(value => string.Equals(value.Key, content, StringComparison.Ordinal))) vocabulary.Add(new KeyValuePair<string, int>(content, id));
            }
            RequireSpecial(special, StartOfTranscript, contract.DecoderStartTokenId);
            RequireSpecial(special, EndOfText, contract.EosTokenId);
            RequireSpecial(special, NoTimestamps, contract.NoTimestampsTokenId);
            if (contract.LanguageTokenId.HasValue) RequireSpecial(special, WhisperEnglish, contract.LanguageTokenId.Value);
            if (contract.TaskTokenId.HasValue) RequireSpecial(special, WhisperTranscribe, contract.TaskTokenId.Value);
            if (vocabulary.Select(value => value.Value).Distinct().Count() != vocabulary.Count) throw InvalidTokenizer("Whisper vocabulary contains duplicate token IDs.");

            _specialTokens = special;
            var options = new BpeOptions(vocabulary)
            {
                Merges = merges,
                ByteLevel = true,
                PreTokenizer = new RegexPreTokenizer(new Regex(WhisperRegex, RegexOptions.CultureInvariant), special),
                SpecialTokens = special
            };
            _tokenizer = BpeTokenizer.Create(options);
            TokenizerId = "openai-whisper-tokenizer-json";
            Identity = AudioUnderstandingHash.Text(contract.TokenizerSha256 + "|" + contract.GenerationConfigSha256 + "|whisper-bytelevel-bpe-v1");
        }

        /// <summary>Gets the exact generation contract bound to this tokenizer. / 获取此 Tokenizer 绑定的精确 Generation 合同。</summary>
        public AudioGenerationContract Contract { get; }
        /// <summary>Gets the immutable tokenizer adapter ID. / 获取不可变 Tokenizer Adapter ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets the composite tokenizer identity. / 获取复合 Tokenizer Identity。</summary>
        public string Identity { get; }

        /// <summary>Encodes the fixed English transcribe prompt without adding implicit BOS/EOS tokens. / 编码固定 English Transcribe Prompt，且不添加隐式 BOS/EOS Token。</summary>
        public WhisperTokenSequence EncodePrompt(AudioUnderstandingProfile profile, bool includeNoTimestamps = true)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Family != AudioUnderstandingFamily.Whisper || profile.Generation == null || !string.Equals(profile.Generation.TokenizerSha256, Contract.TokenizerSha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "The Whisper tokenizer differs from the profile-bound generation contract.", profileId: profile.ProfileId);
            string prompt = StartOfTranscript + (Contract.LanguageTokenId.HasValue ? WhisperEnglish : string.Empty) + (Contract.TaskTokenId.HasValue ? WhisperTranscribe : string.Empty) + (includeNoTimestamps ? NoTimestamps : string.Empty);
            IReadOnlyList<int> ids = _tokenizer.EncodeToIds(prompt);
            var expected = new List<int> { Contract.DecoderStartTokenId };
            if (Contract.LanguageTokenId.HasValue) expected.Add(Contract.LanguageTokenId.Value);
            if (Contract.TaskTokenId.HasValue) expected.Add(Contract.TaskTokenId.Value);
            if (includeNoTimestamps) expected.Add(Contract.NoTimestampsTokenId);
            if (ids.Count != expected.Count || ids.Where((value, index) => value != expected[index]).Any()) throw new VisualException(VisualErrorCodes.AudioCtcDecodeInvalid, "The Whisper prompt did not encode to the exact special-token sequence.", profileId: profile.ProfileId);
            return new WhisperTokenSequence(prompt, ids.Select(value => (long)value), TokenizerId, Identity);
        }

        /// <summary>Encodes ordinary transcript text without Whisper control tokens. / 编码普通转录文本且不添加 Whisper 控制 Token。</summary>
        public IReadOnlyList<int> EncodeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Transcript text is required.", nameof(text));
            IReadOnlyList<int> ids = _tokenizer.EncodeToIds(text);
            if (ids.Count == 0 || ids.Any(value => value < 0 || value >= Contract.VocabularySize)) throw new VisualException(VisualErrorCodes.AudioCtcDecodeInvalid, "Whisper text encoding produced an invalid token sequence.");
            return ids;
        }

        /// <summary>Decodes generated text tokens while excluding Whisper control, EOS, and timestamp tokens. / 解码生成文本 Token，并排除 Whisper 控制、EOS 与时间戳 Token。</summary>
        public string DecodeText(IEnumerable<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            int capacity = tokenIds is ICollection<int> collection ? collection.Count : 8;
            var values = new List<int>(capacity);
            foreach (int value in tokenIds)
            {
                if (value < 0 || value >= Contract.VocabularySize) throw new VisualException(VisualErrorCodes.AudioCtcDecodeInvalid, "A Whisper token is outside the vocabulary.");
                if (value == Contract.EosTokenId || value == Contract.PadTokenId || value == Contract.DecoderStartTokenId || value == Contract.NoTimestampsTokenId || (Contract.LanguageTokenId.HasValue && value == Contract.LanguageTokenId.Value) || (Contract.TaskTokenId.HasValue && value == Contract.TaskTokenId.Value) || value >= Contract.TimestampBeginTokenId) continue;
                values.Add(value);
            }
            return values.Count == 0 ? string.Empty : _tokenizer.Decode(values).Trim();
        }

        private static void RequireSpecial(IReadOnlyDictionary<string, int> tokens, string text, int expected)
        {
            if (!tokens.TryGetValue(text, out int actual) || actual != expected) throw InvalidTokenizer("Whisper special-token ID differs from the generation contract: " + text + ".");
        }

        private static VisualException InvalidTokenizer(string message) => new VisualException(VisualErrorCodes.AudioContractInvalid, message);

        private static void Verify(string path, string expected, string role)
        {
            if (!File.Exists(path)) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "A required Whisper tokenizer file is missing.", technicalDetails: role + "=" + path);
            using FileStream stream = File.OpenRead(path);
            using SHA256 hash = SHA256.Create();
            string actual = string.Concat(hash.ComputeHash(stream).Select(value => value.ToString("x2")));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "A Whisper tokenizer file SHA-256 differs from the generation contract.", technicalDetails: role + ";expected=" + expected + ";actual=" + actual);
        }
    }

    /// <summary>Owns one exact Whisper decoder prompt. / 拥有一个精确 Whisper Decoder Prompt。</summary>
    public sealed class WhisperTokenSequence
    {
        private readonly IReadOnlyList<long> _tokenIds;

        /// <summary>Initializes an immutable Whisper prompt sequence. / 初始化不可变 Whisper Prompt 序列。</summary>
        public WhisperTokenSequence(string normalizedPrompt, IEnumerable<long> tokenIds, string tokenizerId, string tokenizerIdentity)
        {
            if (string.IsNullOrWhiteSpace(normalizedPrompt) || tokenIds == null || string.IsNullOrWhiteSpace(tokenizerId) || !AudioUnderstandingHash.IsSha256(tokenizerIdentity)) throw AudioFailure.Contract("Whisper token sequence identity is invalid.");
            var values = tokenIds.ToList();
            if (values.Count == 0 || values.Any(value => value < 0)) throw AudioFailure.Contract("Whisper token sequence is empty or contains a negative token.");
            NormalizedPrompt = normalizedPrompt; _tokenIds = new System.Collections.ObjectModel.ReadOnlyCollection<long>(values); TokenizerId = tokenizerId.Trim(); TokenizerIdentity = tokenizerIdentity.ToLowerInvariant(); PromptSha256 = AudioUnderstandingHash.Text(normalizedPrompt + "|" + string.Join(",", values));
        }

        /// <summary>Gets normalized prompt text. / 获取归一化 Prompt 文本。</summary>
        public string NormalizedPrompt { get; }
        /// <summary>Gets immutable prompt IDs. / 获取不可变 Prompt ID。</summary>
        public IReadOnlyList<long> TokenIds => _tokenIds;
        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets tokenizer identity. / 获取 Tokenizer Identity。</summary>
        public string TokenizerIdentity { get; }
        /// <summary>Gets prompt hash. / 获取 Prompt 哈希。</summary>
        public string PromptSha256 { get; }
        /// <summary>Returns an owned token array. / 返回自有 Token 数组。</summary>
        public long[] CopyTokenIds() => _tokenIds.ToArray();
    }
}
#endif
