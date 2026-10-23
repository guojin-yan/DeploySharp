using System;
using System.Collections.Generic;
#if NET8_0 || NET9_0 || NET10_0
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.ML.Tokenizers;
#endif

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Loads the exact official PaddleOCR-VL-1.6 SentencePiece BPE assets and single-image template. / 加载精确官方 PaddleOCR-VL-1.6 SentencePiece BPE 资产与单图模板。</summary>
    /// <remarks>This prepares tokens only; it does not load weights or run generation. Requires net8.0, net9.0 or net10.0. / 本类型仅准备 Token，不加载权重或执行生成；要求 net8.0、net9.0 或 net10.0。</remarks>
    public sealed class PaddleOcrVlTokenizer
    {
        private const string ModelHash = "34ef7db83df785924fb83d7b887b6e822a031c56e15cff40aaf9b982988180df";
        private const string JsonHash = "c8a215a59183d0d0781adc33bacd3ce6162716f7fd568fb30234a74d69803a7d";
        private const string ConfigHash = "1f979337347cc0cb72a6282d8a23ed183539aa81a87a906f022aee2bab83c7c5";
        private const string TemplateHash = "2f27812dab7f333e471884e0c803d807f11953d5453140dfb1aaba234f872bc8";
#if NET8_0 || NET9_0 || NET10_0
        private readonly object _gate = new object();
        private readonly SentencePieceTokenizer _tokenizer;
        private readonly HashSet<int> _specialIds = new HashSet<int>();
        private readonly string?[] _decodedPieces = new string?[103424];
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
#endif

        /// <summary>Verifies tokenizer.model, tokenizer.json, tokenizer_config.json and chat_template.jinja before loading. / 加载前校验 tokenizer.model、tokenizer.json、tokenizer_config.json 与 chat_template.jinja。</summary>
        public PaddleOcrVlTokenizer(string modelDirectory)
        {
            if (string.IsNullOrWhiteSpace(modelDirectory)) throw new ArgumentException("An official PaddleOCR-VL-1.6 asset directory is required.", nameof(modelDirectory));
#if NET8_0 || NET9_0 || NET10_0
            string root = Path.GetFullPath(modelDirectory);
            Verify(root, "tokenizer.model", ModelHash);
            Verify(root, "tokenizer.json", JsonHash);
            Verify(root, "tokenizer_config.json", ConfigHash);
            Verify(root, "chat_template.jinja", TemplateHash);
            using (JsonDocument config = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "tokenizer_config.json"))))
            {
                var added = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (JsonProperty entry in config.RootElement.GetProperty("added_tokens_decoder").EnumerateObject())
                {
                    int id = int.Parse(entry.Name, CultureInfo.InvariantCulture);
                    JsonElement token = entry.Value;
                    string piece = token.GetProperty("content").GetString()!;
                    added.Add(piece, id);
                    _decodedPieces[id] = piece.Replace('\u2581', ' ');
                    if (token.GetProperty("special").GetBoolean()) _specialIds.Add(id);
                }
                using (FileStream model = File.OpenRead(Path.Combine(root, "tokenizer.model")))
                    _tokenizer = SentencePieceTokenizer.Create(model, false, false, added);
            }
            using (JsonDocument vocabulary = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "tokenizer.json"))))
                foreach (JsonProperty piece in vocabulary.RootElement.GetProperty("model").GetProperty("vocab").EnumerateObject())
                    _decodedPieces[piece.Value.GetInt32()] = piece.Name.Replace('\u2581', ' ');
            using (SHA256 sha = SHA256.Create())
                Sha256 = Convert.ToHexString(sha.ComputeHash(Encoding.ASCII.GetBytes(string.Join("|", ModelHash, JsonHash, ConfigHash, TemplateHash)))).ToLowerInvariant();
#else
            throw Unavailable();
#endif
        }

        /// <summary>Gets the exact tokenizer identity, including its checkpoint version. / 获取包含 Checkpoint 版本的精确 Tokenizer 标识。</summary>
        public string TokenizerId => "paddleocr-vl-1.6-sentencepiece-bpe";
        /// <summary>Gets the combined identity of all four verified assets. / 获取四份已校验资产的组合标识。</summary>
        public string Sha256 { get; } = string.Empty;
        /// <summary>Gets the model vocabulary size, including reserved and added tokens. / 获取包含保留与新增 Token 的模型词表大小。</summary>
        public int VocabularySize => 103424;
        /// <summary>Gets the official terminal token ID; this is not the CLS token. / 获取官方终止 Token ID，与 CLS Token 不同。</summary>
        public int EndOfSequenceTokenId => 2;
        /// <summary>Gets the official image-placeholder token ID. / 获取官方图像占位 Token ID。</summary>
        public int ImagePatchTokenId => 100295;

        /// <summary>Encodes text without adding BOS/EOS, preserving whitespace, byte fallback and added markers. / 编码文本且不添加 BOS/EOS，保留空白、字节回退及新增标记。</summary>
        public IReadOnlyList<int> EncodeText(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
#if NET8_0 || NET9_0 || NET10_0
            lock (_gate) return Array.AsReadOnly(_tokenizer.EncodeToIds(text, false, false, true, true).ToArray());
#else
            throw Unavailable();
#endif
        }

        /// <summary>Applies the official single-image task template and expands image tokens after checking the context plus output budget. / 应用官方单图任务模板，校验上下文及输出预算后展开图像 Token。</summary>
        /// <remarks>No repeated marker string is built. The returned PromptTemplate contains one unexpanded image marker. / 不构建重复占位符字符串，返回的 PromptTemplate 仅含一个未展开图像标记。</remarks>
        public PaddleOcrVlTokenSequence EncodePrompt(PaddleOcrVlImagePlan imagePlan, int maximumNewTokens = 512, int maximumContextTokens = 131072)
        {
            if (imagePlan == null) throw new ArgumentNullException(nameof(imagePlan));
            if (maximumContextTokens <= 0 || maximumContextTokens > 131072) throw new ArgumentOutOfRangeException(nameof(maximumContextTokens));
            if (maximumNewTokens <= 0 || maximumNewTokens >= maximumContextTokens) throw new ArgumentOutOfRangeException(nameof(maximumNewTokens));
#if NET8_0 || NET9_0 || NET10_0
            string template = "<|begin_of_sentence|>User: <|IMAGE_START|><|IMAGE_PLACEHOLDER|><|IMAGE_END|>"
                + PaddleOcrVlInputPlanning.GetOfficialV16Prompt(imagePlan.Task) + "\nAssistant:\n";
            IReadOnlyList<int> prefix = EncodeText(template);
            long count = (long)prefix.Count - 1 + imagePlan.ImageTokenCount;
            if (count + maximumNewTokens > maximumContextTokens)
                throw new ArgumentOutOfRangeException(nameof(imagePlan), "The expanded image prompt and reserved generation exceed the context budget.");
            var ids = new long[(int)count];
            int output = 0, imageOffset = -1;
            foreach (int id in prefix)
            {
                if (id != ImagePatchTokenId) { ids[output++] = id; continue; }
                if (imageOffset >= 0) throw Invalid("The official single-image template contains multiple image markers.");
                imageOffset = output;
                for (int index = 0; index < imagePlan.ImageTokenCount; index++) ids[output++] = id;
            }
            if (imageOffset < 0 || output != ids.Length) throw Invalid("The official image placeholder was not preserved.");
            return new PaddleOcrVlTokenSequence(template, ids, imagePlan, imageOffset, maximumNewTokens, Sha256);
#else
            throw Unavailable();
#endif
        }

        /// <summary>Decodes completion IDs, optionally removing only tokens marked special by the official assets. / 解码生成 ID，可选仅移除官方资产标记为 Special 的 Token。</summary>
        /// <remarks>Digits and table/spotting structural tokens marked special=false are retained. No trimming or terminal-token truncation is applied. / 保留 Special=false 的数字及表格/定位结构 Token，不裁剪空白或按终止 Token 截断。</remarks>
        public string DecodeCompletion(IEnumerable<int> tokenIds, bool skipSpecialTokens = true)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
#if NET8_0 || NET9_0 || NET10_0
            var result = new StringBuilder();
            var bytes = new List<byte>();
            foreach (int id in tokenIds)
            {
                if (id < 0 || id >= VocabularySize) throw Invalid("A completion token lies outside the PaddleOCR-VL-1.6 vocabulary.");
                if (skipSpecialTokens && _specialIds.Contains(id)) continue;
                string? piece = _decodedPieces[id];
                if (piece == null) continue; // Official tokenizer drops reserved, unmapped model slots.
                if (id >= 13 && id <= 268) { bytes.Add((byte)(id - 13)); continue; }
                FlushBytes(result, bytes);
                result.Append(piece);
            }
            FlushBytes(result, bytes);
            return result.ToString();
#else
            throw Unavailable();
#endif
        }

#if NET8_0 || NET9_0 || NET10_0
        private static void FlushBytes(StringBuilder result, List<byte> bytes)
        {
            if (bytes.Count == 0) return;
            // The official ByteFallback decoder replaces every byte of an invalid
            // run. Encoding.UTF8's usual maximal-subpart replacement differs.
            try { result.Append(StrictUtf8.GetString(CollectionsMarshal.AsSpan(bytes))); }
            catch (DecoderFallbackException) { result.Append('\ufffd', bytes.Count); }
            bytes.Clear();
        }

        private static void Verify(string root, string fileName, string expected)
        {
            string path = Path.Combine(root, fileName);
            if (!File.Exists(path)) throw Invalid("A required official PaddleOCR-VL tokenizer asset is missing: " + fileName);
            string actual;
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create()) actual = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "A PaddleOCR-VL tokenizer asset does not match the pinned official checkpoint.", technicalDetails: fileName + ";expected=" + expected + ";actual=" + actual);
        }
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.GenerativeVisionLanguageTokenizerInvalid, message);
#else
        private static VisualException Unavailable() => new VisualException(VisualErrorCodes.CapabilityUnavailable, "The built-in PaddleOCR-VL tokenizer requires net8.0, net9.0 or net10.0.");
#endif
    }
}
