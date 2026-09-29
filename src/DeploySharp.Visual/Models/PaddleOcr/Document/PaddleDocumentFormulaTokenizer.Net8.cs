#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.Tokenizers;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Loads the official HuggingFace-compatible FormulaNet/UniMERNet BPE tokenizer. / 加载官方 HuggingFace 兼容的 FormulaNet/UniMERNet BPE tokenizer。</summary>
    /// <remarks>The loader accepts tokenizer.json directly, or the tokenizer block embedded in PaddleOCR inference.yml. It is read-only and never writes generated sidecar files. / 加载器既支持 tokenizer.json，也支持 PaddleOCR inference.yml 中嵌入的 tokenizer block；只读且不会生成旁车文件。</remarks>
    public sealed class PaddleDocumentFormulaTokenizer : IPaddleDocumentFormulaTokenizer
    {
        private readonly BpeTokenizer _tokenizer;
        private readonly IReadOnlyList<string> _tokens;

        private PaddleDocumentFormulaTokenizer(BpeTokenizer tokenizer, IReadOnlyList<string> tokens, string sourceIdentity)
        {
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            SourceIdentity = sourceIdentity;
        }

        /// <summary>Gets the vocabulary indexed by model token ID. / 获取按模型 token ID 索引的词表。</summary>
        public IReadOnlyList<string> Tokens => _tokens;

        /// <summary>Gets a SHA-256 identity for the loaded tokenizer source. / 获取已加载 tokenizer 源的 SHA-256 身份。</summary>
        public string SourceIdentity { get; }

        /// <summary>Loads a tokenizer.json containing model.vocab, model.merges and optional added_tokens. / 加载包含 model.vocab、model.merges 以及可选 added_tokens 的 tokenizer.json。</summary>
        public static PaddleDocumentFormulaTokenizer FromTokenizerJson(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A tokenizer.json path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Formula tokenizer.json was not found.", fullPath);
            byte[] bytes = File.ReadAllBytes(fullPath);
            using JsonDocument document = JsonDocument.Parse(bytes);
            return CreateFromJson(document.RootElement, Sha256(bytes), fullPath);
        }

        /// <summary>Loads the fast_tokenizer_file block embedded in a PaddleOCR inference.yml. / 加载 PaddleOCR inference.yml 中嵌入的 fast_tokenizer_file 区块。</summary>
        public static PaddleDocumentFormulaTokenizer FromPaddleInferenceYaml(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An inference.yml path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Paddle formula inference.yml was not found.", fullPath);
            byte[] bytes = File.ReadAllBytes(fullPath);
            var lines = File.ReadAllLines(fullPath);
            int fast = FindLine(lines, "fast_tokenizer_file:", 0);
            if (fast < 0) throw InvalidTokenizer("The inference.yml does not contain fast_tokenizer_file.");
            var added = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = fast + 1; index < lines.Length; index++)
            {
                if (Indent(lines[index]) <= Indent(lines[fast]) && !string.IsNullOrWhiteSpace(lines[index])) break;
                if (!lines[index].TrimStart().StartsWith("- content:", StringComparison.Ordinal)) continue;
                string content = ParseYamlScalar(lines[index].Substring(lines[index].IndexOf(':') + 1).Trim());
                int id = -1;
                for (int scan = index + 1; scan < Math.Min(lines.Length, index + 12); scan++)
                {
                    if (TryParseYamlInt(lines[scan], "id", out id)) break;
                }
                if (id < 0) throw InvalidTokenizer("An added formula token has no integer id.");
                added[content] = id;
            }

            int model = FindLine(lines, "model:", fast);
            if (model < 0) throw InvalidTokenizer("The tokenizer block does not contain a BPE model.");
            int mergesLine = FindLine(lines, "merges:", model);
            int vocabLine = FindLine(lines, "vocab:", model);
            if (mergesLine < 0 || vocabLine < 0 || vocabLine <= mergesLine) throw InvalidTokenizer("The tokenizer BPE model is missing merges or vocab.");
            var merges = new List<string>();
            for (int index = mergesLine + 1; index < vocabLine; index++)
            {
                string trimmed = lines[index].TrimStart();
                if (!trimmed.StartsWith("- ", StringComparison.Ordinal)) continue;
                string scalar = trimmed.Substring(2).Trim();
                // PyYAML may emit long merge pairs as a folded single-quoted scalar.
                // Preserve the fold as one space so entries such as "******** ****"
                // remain valid BPE pairs instead of becoming a one-token merge.
                while (IsUnterminatedQuotedScalar(scalar) && index + 1 < vocabLine)
                {
                    index++;
                    scalar += " " + lines[index].Trim();
                }
                merges.Add(ParseYamlScalar(scalar));
            }
            var vocabulary = new List<KeyValuePair<string, int>>();
            var byId = new Dictionary<int, string>();
            int vocabIndent = Indent(lines[vocabLine]);
            for (int index = vocabLine + 1; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index])) continue;
                int indent = Indent(lines[index]);
                if (indent <= vocabIndent) break;
                if (!TryParseYamlMapEntry(lines[index], out string key, out int id)) continue;
                vocabulary.Add(new KeyValuePair<string, int>(key, id));
                byId[id] = key;
            }
            foreach (KeyValuePair<string, int> pair in added)
            {
                if (!byId.ContainsKey(pair.Value)) { vocabulary.Add(new KeyValuePair<string, int>(pair.Key, pair.Value)); byId[pair.Value] = pair.Key; }
            }
            return Create(vocabulary, merges, added, Sha256(bytes), fullPath);
        }

        /// <summary>Creates a tokenizer from a vocabulary and BPE merge list. / 从词表和 BPE merge 列表创建 tokenizer。</summary>
        public static PaddleDocumentFormulaTokenizer FromVocabularyAndMerges(IEnumerable<KeyValuePair<string, int>> vocabulary, IEnumerable<string> merges, IReadOnlyDictionary<string, int>? specialTokens = null)
        {
            if (vocabulary == null) throw new ArgumentNullException(nameof(vocabulary));
            if (merges == null) throw new ArgumentNullException(nameof(merges));
            var vocab = vocabulary.ToArray();
            var mergeList = merges.ToArray();
            return Create(vocab, mergeList, specialTokens ?? new Dictionary<string, int>(), "", "in-memory");
        }

        public string Decode(IReadOnlyList<int> tokenIds)
        {
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            return tokenIds.Count == 0 ? string.Empty : _tokenizer.Decode(tokenIds);
        }

        private static PaddleDocumentFormulaTokenizer CreateFromJson(JsonElement root, string identity, string source)
        {
            JsonElement model = root.GetProperty("model");
            var vocabulary = new List<KeyValuePair<string, int>>();
            foreach (JsonProperty item in model.GetProperty("vocab").EnumerateObject()) vocabulary.Add(new KeyValuePair<string, int>(item.Name, item.Value.GetInt32()));
            var merges = model.GetProperty("merges").EnumerateArray().Select(value => value.GetString() ?? throw InvalidTokenizer("A BPE merge is not a string.")).ToArray();
            var specials = new Dictionary<string, int>(StringComparer.Ordinal);
            if (root.TryGetProperty("added_tokens", out JsonElement added) && added.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in added.EnumerateArray())
                {
                    string content = item.GetProperty("content").GetString() ?? throw InvalidTokenizer("An added token has no content.");
                    specials[content] = item.GetProperty("id").GetInt32();
                }
            }
            return Create(vocabulary, merges, specials, identity, source);
        }

        private static PaddleDocumentFormulaTokenizer Create(IEnumerable<KeyValuePair<string, int>> vocabulary, IEnumerable<string> merges, IReadOnlyDictionary<string, int> specials, string identity, string source)
        {
            var entries = vocabulary.ToArray();
            if (entries.Length == 0) throw InvalidTokenizer("Formula tokenizer vocabulary is empty.");
            var options = new BpeOptions(entries) { Merges = merges.ToArray(), ByteLevel = true, SpecialTokens = specials };
            BpeTokenizer tokenizer = BpeTokenizer.Create(options);
            int maximum = entries.Max(pair => pair.Value);
            var tokens = Enumerable.Repeat(string.Empty, maximum + 1).ToArray();
            foreach (KeyValuePair<string, int> pair in entries) if (pair.Value >= 0) tokens[pair.Value] = pair.Key;
            return new PaddleDocumentFormulaTokenizer(tokenizer, Array.AsReadOnly(tokens), identity);
        }

        private static int FindLine(string[] lines, string prefix, int start)
        {
            for (int index = start; index < lines.Length; index++) if (lines[index].TrimStart().StartsWith(prefix, StringComparison.Ordinal)) return index;
            return -1;
        }

        private static int Indent(string line) { int count = 0; while (count < line.Length && line[count] == ' ') count++; return count; }

        private static bool TryParseYamlInt(string line, string key, out int value)
        {
            value = 0; string trimmed = line.Trim(); string prefix = key + ":"; if (!trimmed.StartsWith(prefix, StringComparison.Ordinal)) return false;
            return int.TryParse(trimmed.Substring(prefix.Length).Trim(), out value);
        }

        private static bool TryParseYamlMapEntry(string line, out string key, out int value)
        {
            key = string.Empty; value = 0; int colon = line.LastIndexOf(':'); if (colon <= 0) return false;
            if (!int.TryParse(line.Substring(colon + 1).Trim(), out value)) return false;
            key = ParseYamlScalar(line.Substring(0, colon).Trim()); return key.Length > 0;
        }

        private static string ParseYamlScalar(string value)
        {
            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'') return value.Substring(1, value.Length - 2).Replace("''", "'");
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"') return JsonSerializer.Deserialize<string>(value) ?? string.Empty;
            return value;
        }

        private static bool IsUnterminatedQuotedScalar(string value)
        {
            if (value.Length == 0 || value[0] != '\'') return false;
            int quotes = 0;
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] != '\'') continue;
                if (index + 1 < value.Length && value[index + 1] == '\'') { index++; continue; }
                quotes++;
            }
            return (quotes & 1) != 0;
        }

        private static string Sha256(byte[] bytes) { using SHA256 sha = SHA256.Create(); return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2"))); }
        private static InvalidOperationException InvalidTokenizer(string message) => new InvalidOperationException(message);
    }
}

#pragma warning restore CS1591
#endif
