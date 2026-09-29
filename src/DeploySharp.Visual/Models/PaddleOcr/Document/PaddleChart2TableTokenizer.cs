#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML.Tokenizers;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>
    /// Loads the official PP-Chart2Table Qwen tiktoken vocabulary and applies the
    /// PaddleOCR GOT-OCR2 chart prompt. / 加载官方 PP-Chart2Table Qwen tiktoken
    /// 词表并应用 PaddleOCR GOT-OCR2 图表提示词。
    /// </summary>
    /// <remarks>
    /// This adapter deliberately consumes the original <c>qwen.tiktoken</c> and
    /// special-token sidecars. It does not convert the vocabulary to a guessed
    /// vocab.json/merges.txt pair, which would change Qwen token boundaries.
    /// / 此适配器直接消费原始 <c>qwen.tiktoken</c> 与特殊 Token sidecar，不把它
    /// 猜测转换成 vocab.json/merges.txt，避免改变 Qwen 的 Token 边界。
    /// </remarks>
    public sealed class PaddleChart2TableTokenizer : IGenerativeVisionLanguageTokenizer
    {
        private const string Pattern = @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+";
        private const string OfficialVocabularySha256 = "b2b1b8dfb5cc5f024bafc373121c6aba3f66f9a5a0269e243470a1de16a33186";
        private const string OfficialTokenizerConfigSha256 = "c167bad3fb717234222ea0fc401d797ca4f03c366f18c19c3026ed77777d0129";
        private const string OfficialAddedTokensSha256 = "4e0e1abe8efbae00a41ffb6275402dd0f9796f04398b4d129ad35310f76cb6cc";
        private const int DefaultImageTokenCount = 256;
        private readonly TiktokenTokenizer _tokenizer;
        private readonly IReadOnlyDictionary<int, string> _specialById;

        /// <summary>Loads and verifies one official Chart2Table tokenizer directory. / 加载并校验一个官方 Chart2Table Tokenizer 目录。</summary>
        public PaddleChart2TableTokenizer(string modelDirectory)
        {
            if (string.IsNullOrWhiteSpace(modelDirectory)) throw new ArgumentException("A Chart2Table model directory is required.", nameof(modelDirectory));
            string root = Path.GetFullPath(modelDirectory);
            string vocabulary = Path.Combine(root, "qwen.tiktoken");
            string config = Path.Combine(root, "tokenizer_config.json");
            string added = Path.Combine(root, "added_tokens.json");
            Verify(vocabulary, "qwen.tiktoken", OfficialVocabularySha256);
            Verify(config, "tokenizer_config.json", OfficialTokenizerConfigSha256);
            Verify(added, "added_tokens.json", OfficialAddedTokensSha256);

            var special = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["<|endoftext|>"] = 151643,
                ["<|im_start|>"] = 151644,
                ["<|im_end|>"] = 151645
            };
            for (int index = 0; index < 205; index++) special["<|extra_" + index.ToString(CultureInfo.InvariantCulture) + "|>"] = 151646 + index;
            Add(special, "<ref>", 151851);
            Add(special, "</ref>", 151852);
            Add(special, "<box>", 151853);
            Add(special, "</box>", 151854);
            Add(special, "<quad>", 151855);
            Add(special, "</quad>", 151856);
            Add(special, "<img>", 151857);
            Add(special, "</img>", 151858);
            Add(special, "<imgpad>", 151859);

            using (var stream = File.OpenRead(vocabulary))
            {
                _tokenizer = TiktokenTokenizer.Create(
                    stream,
                    new RegexPreTokenizer(new Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled), special),
                    null,
                    special,
                    cacheSize: 4096);
            }

            var byId = new Dictionary<int, string>();
            foreach (KeyValuePair<string, int> pair in special) byId[pair.Value] = pair.Key;
            _specialById = byId;
            ModelDirectory = root;
            VocabularySha256 = ComputeSha256(vocabulary);
            TokenizerConfigSha256 = ComputeSha256(config);
            AddedTokensSha256 = ComputeSha256(added);
            Sha256 = TextHash(string.Join("|", VocabularySha256, TokenizerConfigSha256, AddedTokensSha256));
        }

        /// <summary>Gets the bound model directory. / 获取绑定的模型目录。</summary>
        public string ModelDirectory { get; }
        /// <summary>Gets the qwen.tiktoken SHA256. / 获取 qwen.tiktoken SHA256。</summary>
        public string VocabularySha256 { get; }
        /// <summary>Gets tokenizer_config.json SHA256. / 获取 tokenizer_config.json SHA256。</summary>
        public string TokenizerConfigSha256 { get; }
        /// <summary>Gets added_tokens.json SHA256. / 获取 added_tokens.json SHA256。</summary>
        public string AddedTokensSha256 { get; }
        /// <inheritdoc />
        public string TokenizerId => "paddle-chart2table-qwen-tiktoken";
        /// <inheritdoc />
        public string Sha256 { get; }
        /// <summary>Gets the official model vocabulary size including added tokens. / 获取包含新增 Token 的官方模型词表大小。</summary>
        public int VocabularySize => 151860;
        /// <summary>Gets the image placeholder token ID. / 获取图像占位 Token ID。</summary>
        public int ImagePatchTokenId => 151859;
        /// <summary>Gets the model EOS token ID. / 获取模型 EOS Token ID。</summary>
        public int EndOfSequenceTokenId => 151645;
        /// <summary>Gets the model end-of-text token ID. / 获取模型 End-of-text Token ID。</summary>
        public int EndOfTextTokenId => 151643;
        /// <summary>Identifies either official terminal token used by Chart2Table configs. / 判断是否为 Chart2Table 配置中的任一官方终止 Token。</summary>
        public bool IsTerminalToken(int tokenId) => tokenId == EndOfSequenceTokenId || tokenId == EndOfTextTokenId;

        /// <summary>Encodes the exact PaddleOCR chart prompt with 256 image placeholders. / 编码含 256 个图像占位符的 PaddleOCR 精确图表 Prompt。</summary>
        public GenerativeTokenSequence EncodeChartPrompt(int imageTokenCount = DefaultImageTokenCount)
        {
            if (imageTokenCount <= 0) throw new ArgumentOutOfRangeException(nameof(imageTokenCount));
            string prompt = "<|im_start|>system\nYou should follow the instructions carefully and explain your answers in detail.<|im_end|><|im_start|>user\n<img>" + string.Concat(Enumerable.Repeat("<imgpad>", imageTokenCount)) + "</img>\nChart to table<|im_end|><|im_start|>assistant\n";
            IReadOnlyList<int> encoded = _tokenizer.EncodeToIds(prompt);
            if (encoded.Count(value => value == ImagePatchTokenId) != imageTokenCount) throw Invalid("The tokenizer did not preserve the expected image placeholder count.");
            long[] ids = encoded.Select(value => (long)value).ToArray();
            return new GenerativeTokenSequence(prompt, ids, TokenizerId, Sha256);
        }

        /// <inheritdoc />
        public GenerativeTokenSequence EncodePrefix(GenerativeVisionLanguageProfile profile, GenerativeVisionLanguageRequest request)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Task != GenerativeVisionLanguageTask.ConditionalTextGeneration) throw Invalid("Chart2Table accepts a conditional chart-to-table request.");
            return EncodeChartPrompt();
        }

        /// <inheritdoc />
        public string DecodeCompletion(IEnumerable<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            var values = new List<int>();
            foreach (int id in tokenIds)
            {
                if (id < 0 || id >= VocabularySize) throw Invalid("A completion token is outside the Chart2Table vocabulary.");
                if (IsTerminalToken(id) || id == 151644 || _specialById.ContainsKey(id)) continue;
                values.Add(id);
            }
            return values.Count == 0 ? string.Empty : _tokenizer.Decode(values);
        }

        private static void Add(IDictionary<string, int> map, string token, int id) => map[token] = id;
        private static void Verify(string path, string role, string expectedSha256)
        {
            if (!File.Exists(path)) throw Invalid("The official Chart2Table tokenizer asset is missing: " + role + "=" + path);
            string actual = ComputeSha256(path);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "A PP-Chart2Table tokenizer asset does not match the verified official file.", technicalDetails: role + ";expected=" + expectedSha256 + ";actual=" + actual);
        }
        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create()) return string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
        private static string TextHash(string value)
        {
            using (SHA256 algorithm = SHA256.Create()) return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.GenerativeVisionLanguageTokenizerInvalid, message);
    }
}
#endif
