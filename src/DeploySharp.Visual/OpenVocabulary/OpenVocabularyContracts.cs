using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies an audited open-vocabulary model family. / 标识已审计的开放词汇模型族。</summary>
    public enum OpenVocabularyModelFamily
    {
        /// <summary>Grounding DINO family. / Grounding DINO 模型族。</summary>
        GroundingDino = 1,
        /// <summary>YOLO-World family. / YOLO-World 模型族。</summary>
        YoloWorld = 2,
        /// <summary>YOLOE family. / YOLOE 模型族。</summary>
        YoloE = 3
    }

    /// <summary>Identifies how prompts reach an exact exported artifact. / 标识提示如何进入精确导出工件。</summary>
    public enum OpenVocabularyPromptMode
    {
        /// <summary>Text is a named runtime input. / 文本是具名运行时输入。</summary>
        RuntimeText = 1,
        /// <summary>Text embeddings were reparameterized before export and the vocabulary is fixed. / 文本 Embedding 在导出前已重参数化，词汇固定。</summary>
        FixedVocabulary = 2,
        /// <summary>Visual prompts are native model inputs. / 视觉提示是原生模型输入。</summary>
        VisualPrompt = 3,
        /// <summary>The artifact uses its upstream prompt-free vocabulary. / 工件使用上游无提示词汇。</summary>
        PromptFree = 4
    }

    /// <summary>Identifies prompt text normalization before identity and duplicate checks. / 标识 Identity 与重复检查前的提示文本规范化。</summary>
    public enum VocabularyNormalization
    {
        /// <summary>Preserve the exact text. / 保留精确文本。</summary>
        Exact = 0,
        /// <summary>Apply Unicode NFC. / 应用 Unicode NFC。</summary>
        Nfc = 1,
        /// <summary>Apply Unicode NFKC and invariant lowercase. / 应用 Unicode NFKC 与不变小写。</summary>
        NfkcLowerInvariant = 2
    }

    /// <summary>Identifies one auditable component in an open-vocabulary supply chain. / 标识开放词汇供应链中的一个可审计组件。</summary>
    public enum OpenVocabularyArtifactRole
    {
        /// <summary>Executable detector graph. / 可执行检测图。</summary>
        Detector = 1,
        /// <summary>Source training checkpoint. / 源训练 Checkpoint。</summary>
        SourceCheckpoint = 2,
        /// <summary>Text encoder weights. / 文本 Encoder 权重。</summary>
        TextEncoder = 3,
        /// <summary>Tokenizer vocabulary or merge data. / Tokenizer 词表或 Merge 数据。</summary>
        Tokenizer = 4,
        /// <summary>Serialized vocabulary or prompt embedding. / 序列化词汇或提示 Embedding。</summary>
        Vocabulary = 5,
        /// <summary>Prompt or image fusion component. / 提示或图像融合组件。</summary>
        Fusion = 6,
        /// <summary>Box or phrase decoder component. / 框或短语 Decoder 组件。</summary>
        Decoder = 7
    }

    /// <summary>Represents one immutable ordered vocabulary entry. / 表示一个不可变的有序词汇条目。</summary>
    public sealed class VocabularyPromptEntry
    {
        internal VocabularyPromptEntry(int index, string text, string normalizedText)
        {
            Index = index;
            Text = text;
            NormalizedText = normalizedText;
        }

        /// <summary>Gets the class index bound during export. / 获取导出时绑定的类别索引。</summary>
        public int Index { get; }
        /// <summary>Gets exact caller/export text. / 获取精确调用方/导出文本。</summary>
        public string Text { get; }
        /// <summary>Gets normalized identity text. / 获取规范化后的 Identity 文本。</summary>
        public string NormalizedText { get; }
    }

    /// <summary>Owns an ordered, normalized, capacity-bounded vocabulary and its deterministic identity. / 拥有有序、规范化、容量受限的词汇及其确定性 Identity。</summary>
    public sealed class VocabularyPrompt
    {
        private readonly IReadOnlyList<VocabularyPromptEntry> _entries;

        /// <summary>Creates a vocabulary; duplicate normalized entries and empty text are rejected. / 创建词汇；拒绝规范化后重复的条目与空文本。</summary>
        public VocabularyPrompt(IEnumerable<string> entries, VocabularyNormalization normalization = VocabularyNormalization.Nfc, int maximumEntries = 256, int maximumUtf8Bytes = 65536)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (!Enum.IsDefined(typeof(VocabularyNormalization), normalization)) throw Invalid("The vocabulary normalization is invalid.");
            if (maximumEntries <= 0 || maximumUtf8Bytes <= 0) throw Invalid("Vocabulary capacities must be positive.");
            var values = new List<VocabularyPromptEntry>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            int totalBytes = 0;
            foreach (string value in entries)
            {
                if (string.IsNullOrWhiteSpace(value)) throw Invalid("Vocabulary entries cannot be empty.");
                if (values.Count >= maximumEntries) throw Limit("The vocabulary entry capacity was exceeded.");
                string exact = value.Trim();
                string normalized = Normalize(exact, normalization);
                if (!unique.Add(normalized)) throw Invalid("Vocabulary entries must be unique after normalization: " + normalized + ".");
                totalBytes = checked(totalBytes + Encoding.UTF8.GetByteCount(normalized));
                if (totalBytes > maximumUtf8Bytes) throw Limit("The vocabulary UTF-8 capacity was exceeded.");
                values.Add(new VocabularyPromptEntry(values.Count, exact, normalized));
            }
            if (values.Count == 0) throw Invalid("At least one vocabulary entry is required.");
            _entries = new ReadOnlyCollection<VocabularyPromptEntry>(values);
            Normalization = normalization;
            MaximumEntries = maximumEntries;
            MaximumUtf8Bytes = maximumUtf8Bytes;
            Sha256 = ComputeIdentity(values);
        }

        /// <summary>Gets entries in exact exported class order. / 获取按精确导出类别顺序排列的条目。</summary>
        public IReadOnlyList<VocabularyPromptEntry> Entries => _entries;
        /// <summary>Gets the normalization policy. / 获取规范化策略。</summary>
        public VocabularyNormalization Normalization { get; }
        /// <summary>Gets entry capacity. / 获取条目容量。</summary>
        public int MaximumEntries { get; }
        /// <summary>Gets normalized UTF-8 capacity. / 获取规范化 UTF-8 容量。</summary>
        public int MaximumUtf8Bytes { get; }
        /// <summary>Gets SHA256 over length-prefixed normalized entries in order. / 获取按顺序对长度前缀规范化条目计算的 SHA256。</summary>
        public string Sha256 { get; }

        private static string Normalize(string value, VocabularyNormalization mode)
        {
            if (mode == VocabularyNormalization.Exact) return value;
            string result = value.Normalize(mode == VocabularyNormalization.Nfc ? NormalizationForm.FormC : NormalizationForm.FormKC);
            return mode == VocabularyNormalization.NfkcLowerInvariant ? result.ToLowerInvariant() : result;
        }

        private static string ComputeIdentity(IEnumerable<VocabularyPromptEntry> entries)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                foreach (VocabularyPromptEntry entry in entries)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(entry.NormalizedText);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
                writer.Flush();
                using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(stream.ToArray()));
            }
        }

        internal static string Hex(byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) result.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.OpenVocabularyContractInvalid, message);
        private static VisualException Limit(string message) => new VisualException(VisualErrorCodes.OpenVocabularyLimitExceeded, message);
    }

    /// <summary>Records official token IDs for one exact vocabulary entry. / 记录一个精确词汇条目的官方 Token ID。</summary>
    public sealed class OpenVocabularyTokenizationEntry
    {
        private readonly IReadOnlyList<int> _tokenIds;

        /// <summary>Initializes owned token evidence including special tokens and padding when exported that way. / 初始化自有 Token 证据，包括导出合同中的特殊 Token 与 Padding。</summary>
        public OpenVocabularyTokenizationEntry(int vocabularyIndex, IEnumerable<int> tokenIds)
        {
            if (vocabularyIndex < 0) throw new ArgumentOutOfRangeException(nameof(vocabularyIndex));
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            var values = new List<int>(tokenIds);
            if (values.Count == 0 || values.Any(value => value < 0)) throw Invalid("Token IDs must be non-empty and non-negative.");
            VocabularyIndex = vocabularyIndex;
            _tokenIds = new ReadOnlyCollection<int>(values);
        }

        /// <summary>Gets the vocabulary class index. / 获取词汇类别索引。</summary>
        public int VocabularyIndex { get; }
        /// <summary>Gets owned official token IDs. / 获取自有官方 Token ID。</summary>
        public IReadOnlyList<int> TokenIds => _tokenIds;
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.OpenVocabularyContractInvalid, message);
    }

    /// <summary>Binds fixed prompt embeddings to exact vocabulary, tokenizer, encoder, values, and shape. / 将固定提示 Embedding 绑定到精确词汇、Tokenizer、Encoder、数值与 Shape。</summary>
    public sealed class OpenVocabularyEmbeddingIdentity
    {
        /// <summary>Initializes an exact fixed-prompt embedding identity. / 初始化精确固定提示 Embedding Identity。</summary>
        public OpenVocabularyEmbeddingIdentity(string vocabularySha256, string tokenizerId, string tokenizerSha256, string textEncoderId, string textEncoderSha256, string embeddingSha256, int entryCount, int embeddingWidth)
        {
            VocabularySha256 = Sha(vocabularySha256, nameof(vocabularySha256));
            TokenizerId = Required(tokenizerId, nameof(tokenizerId));
            TokenizerSha256 = Sha(tokenizerSha256, nameof(tokenizerSha256));
            TextEncoderId = Required(textEncoderId, nameof(textEncoderId));
            TextEncoderSha256 = Sha(textEncoderSha256, nameof(textEncoderSha256));
            EmbeddingSha256 = Sha(embeddingSha256, nameof(embeddingSha256));
            if (entryCount <= 0 || embeddingWidth <= 0) throw Invalid("Embedding dimensions must be positive.");
            EntryCount = entryCount;
            EmbeddingWidth = embeddingWidth;
        }

        /// <summary>Gets vocabulary SHA256. / 获取词汇 SHA256。</summary>
        public string VocabularySha256 { get; }
        /// <summary>Gets tokenizer identity. / 获取 Tokenizer Identity。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets tokenizer data SHA256. / 获取 Tokenizer 数据 SHA256。</summary>
        public string TokenizerSha256 { get; }
        /// <summary>Gets text encoder identity. / 获取文本 Encoder Identity。</summary>
        public string TextEncoderId { get; }
        /// <summary>Gets text encoder weight SHA256. / 获取文本 Encoder 权重 SHA256。</summary>
        public string TextEncoderSha256 { get; }
        /// <summary>Gets embedding tensor SHA256. / 获取 Embedding 张量 SHA256。</summary>
        public string EmbeddingSha256 { get; }
        /// <summary>Gets embedding entry count. / 获取 Embedding 条目数。</summary>
        public int EntryCount { get; }
        /// <summary>Gets embedding width. / 获取 Embedding 宽度。</summary>
        public int EmbeddingWidth { get; }

        internal void Validate(VocabularyPrompt prompt)
        {
            if (!string.Equals(prompt.Sha256, VocabularySha256, StringComparison.Ordinal) || prompt.Entries.Count != EntryCount) throw new VisualException(VisualErrorCodes.OpenVocabularyIdentityMismatch, "The fixed vocabulary does not match its serialized embedding identity.");
        }

        internal static string Sha(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw Invalid("A SHA256 is required: " + parameterName + ".");
            string result = value.Trim().ToLowerInvariant();
            if (result.Length != 64 || result.Any(valueAt => !((valueAt >= '0' && valueAt <= '9') || (valueAt >= 'a' && valueAt <= 'f')))) throw Invalid("A 64-character hexadecimal SHA256 is required: " + parameterName + ".");
            return result;
        }

        private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw Invalid("A component identity is required: " + name + ".") : value.Trim();
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.OpenVocabularyContractInvalid, message);
    }

    /// <summary>Records one source or runtime artifact with exact provenance and blocker state. / 记录一个具有精确来源与 Blocker 状态的源工件或运行时工件。</summary>
    public sealed class OpenVocabularyArtifactContract
    {
        private readonly IReadOnlyList<string> _inputs;
        private readonly IReadOnlyList<string> _outputs;

        /// <summary>Initializes an immutable supply-chain artifact contract. / 初始化不可变供应链工件合同。</summary>
        public OpenVocabularyArtifactContract(OpenVocabularyArtifactRole role, ModelId modelId, string format, string sha256, long size, int opset, IEnumerable<string>? inputs, IEnumerable<string>? outputs, string upstreamRepository, string upstreamCommit, string exporter, string license, bool executable, string? blocker = null)
        {
            if (!Enum.IsDefined(typeof(OpenVocabularyArtifactRole), role) || modelId.IsEmpty) throw Invalid("The artifact role or model ID is invalid.");
            if (size <= 0 || opset < 0) throw Invalid("Artifact size must be positive and opset cannot be negative.");
            Role = role;
            ModelId = modelId;
            Format = Required(format, nameof(format)).ToLowerInvariant();
            Sha256 = OpenVocabularyEmbeddingIdentity.Sha(sha256, nameof(sha256));
            Size = size;
            Opset = opset;
            _inputs = CopyNames(inputs);
            _outputs = CopyNames(outputs);
            UpstreamRepository = Required(upstreamRepository, nameof(upstreamRepository));
            UpstreamCommit = Required(upstreamCommit, nameof(upstreamCommit));
            Exporter = Required(exporter, nameof(exporter));
            License = Required(license, nameof(license));
            Executable = executable;
            Blocker = string.IsNullOrWhiteSpace(blocker) ? null : blocker!.Trim();
            if (executable && role == OpenVocabularyArtifactRole.Detector && (_inputs.Count == 0 || _outputs.Count == 0)) throw Invalid("An executable detector requires exact named inputs and outputs.");
            if (!executable && Blocker == null) throw Invalid("A non-executable artifact requires a reproducible blocker.");
        }

        /// <summary>Gets artifact role. / 获取工件角色。</summary>
        public OpenVocabularyArtifactRole Role { get; }
        /// <summary>Gets logical model ID. / 获取逻辑模型 ID。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets normalized format. / 获取规范化格式。</summary>
        public string Format { get; }
        /// <summary>Gets exact SHA256. / 获取精确 SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets byte size. / 获取字节大小。</summary>
        public long Size { get; }
        /// <summary>Gets ONNX opset, or zero when not applicable. / 获取 ONNX Opset；不适用时为零。</summary>
        public int Opset { get; }
        /// <summary>Gets exact named inputs. / 获取精确具名输入。</summary>
        public IReadOnlyList<string> Inputs => _inputs;
        /// <summary>Gets exact named outputs. / 获取精确具名输出。</summary>
        public IReadOnlyList<string> Outputs => _outputs;
        /// <summary>Gets authoritative upstream repository. / 获取权威上游仓库。</summary>
        public string UpstreamRepository { get; }
        /// <summary>Gets pinned upstream commit or release. / 获取锁定的上游 Commit 或 Release。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets exporter and dependency identity. / 获取 Exporter 与依赖 Identity。</summary>
        public string Exporter { get; }
        /// <summary>Gets license evidence. / 获取许可证证据。</summary>
        public string License { get; }
        /// <summary>Gets whether DeploySharp can execute this exact artifact contract. / 获取 DeploySharp 是否可执行此精确工件合同。</summary>
        public bool Executable { get; }
        /// <summary>Gets a reproducible blocker. / 获取可复现 Blocker。</summary>
        public string? Blocker { get; }

        /// <summary>Creates the exact Core artifact; blocker-only components are rejected. / 创建精确 Core 工件；拒绝仅 Blocker 组件。</summary>
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null)
        {
            if (!Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The audited artifact is not executable: " + (Blocker ?? "unknown blocker") + ".", modelId: ModelId);
            return new ModelArtifact(ModelId, Format, path, Sha256, preferredBackend);
        }

        private static IReadOnlyList<string> CopyNames(IEnumerable<string>? names)
        {
            var values = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (names != null) foreach (string name in names)
            {
                string value = Required(name, nameof(names));
                if (!unique.Add(value)) throw Invalid("Artifact port names must be unique.");
                values.Add(value);
            }
            return new ReadOnlyCollection<string>(values);
        }

        private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw Invalid("A value is required: " + name + ".") : value.Trim();
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.OpenVocabularyContractInvalid, message);
    }
}
