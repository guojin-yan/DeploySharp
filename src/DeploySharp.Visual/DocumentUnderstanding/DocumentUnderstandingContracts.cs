using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies an audited document-understanding architecture family. / 标识已审计的文档理解架构族。</summary>
    public enum DocumentUnderstandingFamily
    {
        /// <summary>LayoutLMv3 text, image, bounding-box, and attention fusion. / LayoutLMv3 文本、图像、Box 与 Attention 融合。</summary>
        LayoutLmV3 = 1,
        /// <summary>OCR-free Donut Swin encoder and autoregressive decoder. / OCR-free Donut Swin Encoder 与自回归 Decoder。</summary>
        Donut = 2,
        /// <summary>Pix2Struct flattened patches, row/column positions, and T5 decoder. / Pix2Struct Flattened Patch、行列位置与 T5 Decoder。</summary>
        Pix2Struct = 3
    }

    /// <summary>Identifies a backend-independent document task. / 标识后端无关的文档任务。</summary>
    public enum DocumentUnderstandingTask
    {
        /// <summary>Layout-aware document classification. / 版面感知文档分类。</summary>
        LayoutClassification = 1,
        /// <summary>Layout-aware token/entity extraction. / 版面感知 Token/实体抽取。</summary>
        EntityExtraction = 2,
        /// <summary>OCR-free structured field extraction. / OCR-free 结构化字段抽取。</summary>
        StructuredExtraction = 3,
        /// <summary>Prompted document question answering. / 提示式文档问答。</summary>
        DocumentQuestionAnswering = 4
    }

    /// <summary>Defines which component owns OCR words, boxes, and token-word alignment. / 定义 OCR 词、Box 与 Token-word Alignment 的所有者。</summary>
    public enum DocumentOcrOwnership
    {
        /// <summary>The caller supplies audited words, boxes, and alignment exactly once. / 调用方只提供一次已审计词、Box 与 Alignment。</summary>
        Caller = 1,
        /// <summary>The bound processor supplies OCR exactly once. / 绑定 Processor 只执行一次 OCR。</summary>
        Processor = 2,
        /// <summary>The family is OCR-free and rejects OCR inputs. / 模型族为 OCR-free 并拒绝 OCR 输入。</summary>
        NoneOcrFree = 3
    }

    /// <summary>Identifies one exact role in a document artifact bundle. / 标识文档工件 Bundle 中的一个精确角色。</summary>
    public enum DocumentArtifactRole
    {
        /// <summary>Image/layout encoder or fused LayoutLM encoder. / 图像/版面 Encoder 或融合 LayoutLM Encoder。</summary>
        DocumentEncoder = 1,
        /// <summary>Autoregressive decoder Prefill graph. / 自回归 Decoder Prefill 图。</summary>
        DecoderPrefill = 2,
        /// <summary>Autoregressive one-token decoder with Past/Present KV. / 带 Past/Present KV 的单 Token Decoder。</summary>
        DecoderWithPast = 3,
        /// <summary>Optional separate text/layout encoder. / 可选的独立文本/版面 Encoder。</summary>
        TextLayoutEncoder = 4,
        /// <summary>Optional token embedding graph. / 可选 Token Embedding 图。</summary>
        TokenEmbedding = 5
    }

    /// <summary>Defines the single-source image or patch transformation. / 定义单一来源的图像或 Patch 变换。</summary>
    public enum DocumentProcessorMode
    {
        /// <summary>LayoutLMv3 fixed RGB image plus caller/processor OCR layout. / LayoutLMv3 固定 RGB 图像与 OCR 版面。</summary>
        LayoutLmV3ImageAndLayout = 1,
        /// <summary>Donut thumbnail, centered pad, and RGB normalization. / Donut Thumbnail、居中 Pad 与 RGB Normalize。</summary>
        DonutThumbnailPad = 2,
        /// <summary>Pix2Struct flattened RGB patches with row/column IDs. / Pix2Struct Flattened RGB Patch 与行列 ID。</summary>
        Pix2StructFlattenedPatches = 3
    }

    /// <summary>Represents a validated LayoutLM normalized box in the inclusive 0..1000 space. / 表示已校验的 LayoutLM 0..1000 归一化 Box。</summary>
    public readonly struct DocumentNormalizedBox : IEquatable<DocumentNormalizedBox>
    {
        /// <summary>Initializes a positive-area normalized OCR box. / 初始化正面积归一化 OCR Box。</summary>
        public DocumentNormalizedBox(int left, int top, int right, int bottom)
        {
            if (left < 0 || top < 0 || right > 1000 || bottom > 1000 || right <= left || bottom <= top) throw Invalid("A normalized OCR box must have positive area inside 0..1000.");
            Left = left; Top = top; Right = right; Bottom = bottom;
        }

        /// <summary>Gets left coordinate. / 获取左坐标。</summary>
        public int Left { get; }
        /// <summary>Gets top coordinate. / 获取上坐标。</summary>
        public int Top { get; }
        /// <summary>Gets right coordinate. / 获取右坐标。</summary>
        public int Right { get; }
        /// <summary>Gets bottom coordinate. / 获取下坐标。</summary>
        public int Bottom { get; }
        /// <summary>Compares normalized coordinates. / 比较归一化坐标。</summary>
        public bool Equals(DocumentNormalizedBox other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        /// <summary>Compares normalized coordinates. / 比较归一化坐标。</summary>
        public override bool Equals(object? obj) => obj is DocumentNormalizedBox other && Equals(other);
        /// <summary>Returns a stable coordinate hash. / 返回稳定坐标 Hash。</summary>
        public override int GetHashCode() => (((Left * 397) ^ Top) * 397 ^ Right) * 397 ^ Bottom;
        /// <summary>Formats left,top,right,bottom. / 格式化 left,top,right,bottom。</summary>
        public override string ToString() => Left + "," + Top + "," + Right + "," + Bottom;
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, message);
    }

    /// <summary>Owns one caller/processor OCR word and its source-page box. / 拥有一个调用方/Processor OCR 词及源页 Box。</summary>
    public sealed class DocumentWord
    {
        /// <summary>Initializes a non-empty OCR word with finite confidence. / 初始化非空 OCR 词与有限 Confidence。</summary>
        public DocumentWord(string text, DocumentNormalizedBox box, float confidence = 1f)
        {
            if (string.IsNullOrWhiteSpace(text) || float.IsNaN(confidence) || float.IsInfinity(confidence) || confidence < 0f || confidence > 1f) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "OCR word text or confidence is invalid.");
            Text = text; Box = box; Confidence = confidence;
        }
        /// <summary>Gets copied word text. / 获取复制的词文本。</summary>
        public string Text { get; }
        /// <summary>Gets normalized source-page box. / 获取归一化源页 Box。</summary>
        public DocumentNormalizedBox Box { get; }
        /// <summary>Gets OCR confidence. / 获取 OCR Confidence。</summary>
        public float Confidence { get; }
    }

    /// <summary>Owns page-size, OCR words, and token-to-word alignment computed by one owner. / 拥有由单一所有者计算的页尺寸、OCR 词与 Token-to-word Alignment。</summary>
    public sealed class DocumentLayoutInput
    {
        private readonly IReadOnlyList<DocumentWord> _words;
        private readonly IReadOnlyList<int> _alignment;

        /// <summary>Initializes bounded page layout; -1 alignment marks a special token and other values index words. / 初始化受限页面版面；-1 Alignment 表示 Special Token，其余值索引 Word。</summary>
        public DocumentLayoutInput(VisualSize pageSize, IEnumerable<DocumentWord> words, IEnumerable<int> tokenWordAlignment, int maximumWords = 512, int maximumTokens = 514)
        {
            if (words == null || tokenWordAlignment == null || maximumWords <= 0 || maximumTokens <= 0) throw Invalid("Layout collections or capacities are invalid.");
            var wordValues = words.ToList();
            var alignmentValues = tokenWordAlignment.ToList();
            if (wordValues.Count == 0 || wordValues.Count > maximumWords || alignmentValues.Count == 0 || alignmentValues.Count > maximumTokens) throw Limit("Layout word or token capacity was exceeded.");
            if (wordValues.Any(value => value == null) || alignmentValues.Any(value => value < -1 || value >= wordValues.Count)) throw Invalid("Token-word alignment references an absent word.");
            PageSize = pageSize;
            _words = new ReadOnlyCollection<DocumentWord>(wordValues);
            _alignment = new ReadOnlyCollection<int>(alignmentValues);
            Identity = DocumentUnderstandingHash.Text(pageSize + "|" + string.Join("|", wordValues.Select(value => value.Text + "@" + value.Box + "@" + value.Confidence)) + "|" + string.Join(",", alignmentValues));
        }

        /// <summary>Gets original page size used to normalize boxes. / 获取用于归一化 Box 的原始页尺寸。</summary>
        public VisualSize PageSize { get; }
        /// <summary>Gets immutable OCR words in reading order. / 获取按阅读顺序排列的不可变 OCR 词。</summary>
        public IReadOnlyList<DocumentWord> Words => _words;
        /// <summary>Gets token-to-word indexes; -1 is reserved for special tokens. / 获取 Token-to-word 索引；-1 保留给 Special Token。</summary>
        public IReadOnlyList<int> TokenWordAlignment => _alignment;
        /// <summary>Gets stable layout identity. / 获取稳定版面 Identity。</summary>
        public string Identity { get; }
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, message);
        private static VisualException Limit(string message) => new VisualException(VisualErrorCodes.DocumentUnderstandingLimitExceeded, message);
    }

    /// <summary>Binds the only component allowed to derive pixels, patches, masks, positions, and OCR geometry. / 绑定唯一允许派生 Pixel、Patch、Mask、位置与 OCR Geometry 的组件。</summary>
    public sealed class DocumentProcessorContract
    {
        private readonly IReadOnlyList<float> _mean;
        private readonly IReadOnlyList<float> _standardDeviation;

        /// <summary>Initializes an immutable, capacity-bounded document processor contract. / 初始化不可变且容量受限的文档 Processor 合同。</summary>
        public DocumentProcessorContract(string processorId, string configSha256, DocumentProcessorMode mode, VisualSize modelSize, IEnumerable<float> mean, IEnumerable<float> standardDeviation, string interpolation, int maximumPages, int maximumImageBytes, int maximumWords, int maximumPatches, int patchSize = 0)
        {
            if (string.IsNullOrWhiteSpace(processorId) || !DocumentUnderstandingHash.IsSha256(configSha256) || mean == null || standardDeviation == null || string.IsNullOrWhiteSpace(interpolation) || maximumPages <= 0 || maximumImageBytes <= 0 || maximumWords < 0 || maximumPatches < 0 || patchSize < 0) throw Invalid("Processor identity, dimensions, or capacities are invalid.");
            var meanValues = mean.ToList(); var stdValues = standardDeviation.ToList();
            if (meanValues.Count != 3 || stdValues.Count != 3 || meanValues.Concat(stdValues).Any(value => float.IsNaN(value) || float.IsInfinity(value)) || stdValues.Any(value => value <= 0f)) throw Invalid("Processor normalization must contain three finite channels and positive deviations.");
            ProcessorId = processorId.Trim(); ConfigSha256 = configSha256.ToLowerInvariant(); Mode = mode; ModelSize = modelSize; _mean = new ReadOnlyCollection<float>(meanValues); _standardDeviation = new ReadOnlyCollection<float>(stdValues); Interpolation = interpolation.Trim(); MaximumPages = maximumPages; MaximumImageBytes = maximumImageBytes; MaximumWords = maximumWords; MaximumPatches = maximumPatches; PatchSize = patchSize;
            Identity = DocumentUnderstandingHash.Text(string.Join("|", ProcessorId, ConfigSha256, mode, modelSize, string.Join(",", meanValues), string.Join(",", stdValues), Interpolation, maximumPages, maximumImageBytes, maximumWords, maximumPatches, patchSize));
        }

        /// <summary>Gets processor ID. / 获取 Processor ID。</summary>
        public string ProcessorId { get; }
        /// <summary>Gets official processor-config SHA256. / 获取官方 Processor 配置 SHA256。</summary>
        public string ConfigSha256 { get; }
        /// <summary>Gets processor mode. / 获取 Processor 模式。</summary>
        public DocumentProcessorMode Mode { get; }
        /// <summary>Gets exact model canvas size. / 获取精确模型 Canvas 尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets RGB normalization mean. / 获取 RGB Normalize Mean。</summary>
        public IReadOnlyList<float> Mean => _mean;
        /// <summary>Gets RGB normalization standard deviation. / 获取 RGB Normalize Standard Deviation。</summary>
        public IReadOnlyList<float> StandardDeviation => _standardDeviation;
        /// <summary>Gets official interpolation identity. / 获取官方 Interpolation Identity。</summary>
        public string Interpolation { get; }
        /// <summary>Gets maximum ordered pages per prepared document. / 获取每个 Prepared Document 的最大有序页数。</summary>
        public int MaximumPages { get; }
        /// <summary>Gets maximum encoded bytes per page. / 获取每页最大编码字节数。</summary>
        public int MaximumImageBytes { get; }
        /// <summary>Gets maximum OCR words; zero denotes OCR-free. / 获取最大 OCR 词数；零表示 OCR-free。</summary>
        public int MaximumWords { get; }
        /// <summary>Gets maximum flattened patches; zero when not applicable. / 获取最大 Flattened Patch 数；不适用时为零。</summary>
        public int MaximumPatches { get; }
        /// <summary>Gets patch side; zero when not applicable. / 获取 Patch 边长；不适用时为零。</summary>
        public int PatchSize { get; }
        /// <summary>Gets stable processor identity. / 获取稳定 Processor Identity。</summary>
        public string Identity { get; }
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, message);
    }

    /// <summary>Binds exact tokenizer assets, prompt template, special IDs, and bounded context. / 绑定精确 Tokenizer 资产、Prompt Template、Special ID 与受限 Context。</summary>
    public sealed class DocumentTokenizerContract
    {
        /// <summary>Initializes an immutable tokenizer contract. / 初始化不可变 Tokenizer 合同。</summary>
        public DocumentTokenizerContract(string tokenizerId, string modelSha256, string tokenizerJsonSha256, string addedTokensSha256, string tokenizerClass, string promptTemplateId, string defaultTaskPrompt, int vocabularySize, int bosTokenId, int padTokenId, int eosTokenId, int unknownTokenId, int maximumContextTokens)
        {
            if (string.IsNullOrWhiteSpace(tokenizerId) || !DocumentUnderstandingHash.IsSha256(modelSha256) || !DocumentUnderstandingHash.IsSha256(tokenizerJsonSha256) || !DocumentUnderstandingHash.IsSha256(addedTokensSha256) || string.IsNullOrWhiteSpace(tokenizerClass) || string.IsNullOrWhiteSpace(promptTemplateId) || string.IsNullOrWhiteSpace(defaultTaskPrompt) || vocabularySize <= 0 || new[] { bosTokenId, padTokenId, eosTokenId, unknownTokenId }.Any(value => value < 0 || value >= vocabularySize) || maximumContextTokens <= 0) throw Invalid("Tokenizer identity, assets, special tokens, prompt, or context are invalid.");
            TokenizerId = tokenizerId.Trim(); ModelSha256 = modelSha256.ToLowerInvariant(); TokenizerJsonSha256 = tokenizerJsonSha256.ToLowerInvariant(); AddedTokensSha256 = addedTokensSha256.ToLowerInvariant(); TokenizerClass = tokenizerClass.Trim(); PromptTemplateId = promptTemplateId.Trim(); DefaultTaskPrompt = defaultTaskPrompt; VocabularySize = vocabularySize; BosTokenId = bosTokenId; PadTokenId = padTokenId; EosTokenId = eosTokenId; UnknownTokenId = unknownTokenId; MaximumContextTokens = maximumContextTokens;
            Identity = DocumentUnderstandingHash.Text(string.Join("|", TokenizerId, ModelSha256, TokenizerJsonSha256, AddedTokensSha256, TokenizerClass, PromptTemplateId, DefaultTaskPrompt, vocabularySize, bosTokenId, padTokenId, eosTokenId, unknownTokenId, maximumContextTokens));
        }
        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets SentencePiece/vocabulary model SHA256. / 获取 SentencePiece/词表模型 SHA256。</summary>
        public string ModelSha256 { get; }
        /// <summary>Gets tokenizer.json SHA256. / 获取 tokenizer.json SHA256。</summary>
        public string TokenizerJsonSha256 { get; }
        /// <summary>Gets added/special-token mapping SHA256. / 获取 Added/Special Token 映射 SHA256。</summary>
        public string AddedTokensSha256 { get; }
        /// <summary>Gets official tokenizer class. / 获取官方 Tokenizer Class。</summary>
        public string TokenizerClass { get; }
        /// <summary>Gets prompt-template identity. / 获取 Prompt Template Identity。</summary>
        public string PromptTemplateId { get; }
        /// <summary>Gets exact default task prompt. / 获取精确默认 Task Prompt。</summary>
        public string DefaultTaskPrompt { get; }
        /// <summary>Gets vocabulary size. / 获取词表大小。</summary>
        public int VocabularySize { get; }
        /// <summary>Gets BOS token ID. / 获取 BOS Token ID。</summary>
        public int BosTokenId { get; }
        /// <summary>Gets padding token ID. / 获取 Padding Token ID。</summary>
        public int PadTokenId { get; }
        /// <summary>Gets EOS token ID. / 获取 EOS Token ID。</summary>
        public int EosTokenId { get; }
        /// <summary>Gets unknown token ID blocked during Donut generation. / 获取 Donut 生成时屏蔽的 Unknown Token ID。</summary>
        public int UnknownTokenId { get; }
        /// <summary>Gets maximum total decoder tokens. / 获取最大 Decoder 总 Token 数。</summary>
        public int MaximumContextTokens { get; }
        /// <summary>Gets stable tokenizer/template identity. / 获取稳定 Tokenizer/Template Identity。</summary>
        public string Identity { get; }
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, message);
    }

    /// <summary>Binds structured output grammar, provenance, and parse capacities. / 绑定结构化输出 Grammar、来源与 Parse 容量。</summary>
    public sealed class DocumentSchemaContract
    {
        /// <summary>Initializes a bounded schema contract. / 初始化受限 Schema 合同。</summary>
        public DocumentSchemaContract(string schemaId, string schemaSha256, string grammar, int maximumDepth, int maximumFields, int maximumTextCharacters)
        {
            if (string.IsNullOrWhiteSpace(schemaId) || !DocumentUnderstandingHash.IsSha256(schemaSha256) || string.IsNullOrWhiteSpace(grammar) || maximumDepth <= 0 || maximumFields <= 0 || maximumTextCharacters <= 0) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Schema identity, grammar, or capacity is invalid.");
            SchemaId = schemaId.Trim(); SchemaSha256 = schemaSha256.ToLowerInvariant(); Grammar = grammar.Trim(); MaximumDepth = maximumDepth; MaximumFields = maximumFields; MaximumTextCharacters = maximumTextCharacters;
            Identity = DocumentUnderstandingHash.Text(string.Join("|", SchemaId, SchemaSha256, Grammar, maximumDepth, maximumFields, maximumTextCharacters));
        }
        /// <summary>Gets schema ID. / 获取 Schema ID。</summary>
        public string SchemaId { get; }
        /// <summary>Gets schema sidecar SHA256. / 获取 Schema Sidecar SHA256。</summary>
        public string SchemaSha256 { get; }
        /// <summary>Gets grammar identity such as donut-tags-v1. / 获取 Grammar Identity，例如 donut-tags-v1。</summary>
        public string Grammar { get; }
        /// <summary>Gets maximum nesting depth. / 获取最大嵌套深度。</summary>
        public int MaximumDepth { get; }
        /// <summary>Gets maximum parsed fields. / 获取最大解析字段数。</summary>
        public int MaximumFields { get; }
        /// <summary>Gets maximum raw generated characters. / 获取最大原始生成字符数。</summary>
        public int MaximumTextCharacters { get; }
        /// <summary>Gets stable schema identity. / 获取稳定 Schema Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Binds Donut/Pix2Struct self/cross attention Past/Present port names and capacities. / 绑定 Donut/Pix2Struct Self/Cross Attention Past/Present 端口名与容量。</summary>
    public sealed class DocumentKvCacheContract
    {
        /// <summary>Initializes an exact decoder KV schema. / 初始化精确 Decoder KV Schema。</summary>
        public DocumentKvCacheContract(string schemaId, int layerCount, int heads, int headDimension, int encoderTokens, int maximumPastTokens)
        {
            if (string.IsNullOrWhiteSpace(schemaId) || layerCount <= 0 || heads <= 0 || headDimension <= 0 || encoderTokens <= 0 || maximumPastTokens <= 0) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Document KV schema or axes are invalid.");
            SchemaId = schemaId.Trim(); LayerCount = layerCount; Heads = heads; HeadDimension = headDimension; EncoderTokens = encoderTokens; MaximumPastTokens = maximumPastTokens;
            Identity = DocumentUnderstandingHash.Text(string.Join("|", SchemaId, layerCount, heads, headDimension, encoderTokens, maximumPastTokens));
        }
        /// <summary>Gets KV schema ID. / 获取 KV Schema ID。</summary>
        public string SchemaId { get; }
        /// <summary>Gets decoder layer count. / 获取 Decoder Layer 数。</summary>
        public int LayerCount { get; }
        /// <summary>Gets attention head count. / 获取 Attention Head 数。</summary>
        public int Heads { get; }
        /// <summary>Gets one head dimension. / 获取单个 Head Dimension。</summary>
        public int HeadDimension { get; }
        /// <summary>Gets fixed encoder sequence tokens. / 获取固定 Encoder Sequence Token 数。</summary>
        public int EncoderTokens { get; }
        /// <summary>Gets maximum self-attention Past tokens. / 获取最大 Self-attention Past Token 数。</summary>
        public int MaximumPastTokens { get; }
        /// <summary>Gets stable KV identity. / 获取稳定 KV Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets one exact Past input name. / 获取一个精确 Past 输入名。</summary>
        public string Past(int layer, bool decoder, bool key) => "past_key_values." + Layer(layer) + "." + (decoder ? "decoder" : "encoder") + "." + (key ? "key" : "value");
        /// <summary>Gets one exact Present output name. / 获取一个精确 Present 输出名。</summary>
        public string Present(int layer, bool decoder, bool key) => "present." + Layer(layer) + "." + (decoder ? "decoder" : "encoder") + "." + (key ? "key" : "value");
        private int Layer(int value) { if (value < 0 || value >= LayerCount) throw new ArgumentOutOfRangeException(nameof(value)); return value; }
    }

    /// <summary>Describes one immutable executable or blocked document subgraph. / 描述一个不可变的可执行或阻断文档子图。</summary>
    public sealed class DocumentArtifactContract
    {
        private readonly IReadOnlyList<GenerativeVisionLanguageTensorContract> _inputs;
        private readonly IReadOnlyList<GenerativeVisionLanguageTensorContract> _outputs;

        /// <summary>Initializes an exact named-port artifact contract. / 初始化精确具名端口 Artifact 合同。</summary>
        public DocumentArtifactContract(DocumentArtifactRole role, ModelId modelId, string format, string sha256, long size, int opset, IEnumerable<GenerativeVisionLanguageTensorContract> inputs, IEnumerable<GenerativeVisionLanguageTensorContract> outputs, string upstreamRevision, string exporter, string licenseExpression, string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(format) || !DocumentUnderstandingHash.IsSha256(sha256) || size <= 0 || opset <= 0 || inputs == null || outputs == null || string.IsNullOrWhiteSpace(upstreamRevision) || string.IsNullOrWhiteSpace(exporter) || string.IsNullOrWhiteSpace(licenseExpression) || string.IsNullOrWhiteSpace(sourceUrl)) throw Invalid("Document artifact provenance is invalid.");
            var inputValues = inputs.ToList(); var outputValues = outputs.ToList();
            if (inputValues.Count == 0 || outputValues.Count == 0 || inputValues.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != inputValues.Count || outputValues.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != outputValues.Count) throw Invalid("Document artifact ports are empty or duplicated.");
            Role = role; ModelId = modelId; Format = format.Trim().ToLowerInvariant(); Sha256 = sha256.ToLowerInvariant(); Size = size; Opset = opset; _inputs = new ReadOnlyCollection<GenerativeVisionLanguageTensorContract>(inputValues); _outputs = new ReadOnlyCollection<GenerativeVisionLanguageTensorContract>(outputValues); UpstreamRevision = upstreamRevision.Trim(); Exporter = exporter.Trim(); LicenseExpression = licenseExpression.Trim(); SourceUrl = sourceUrl.Trim();
        }
        /// <summary>Gets bundle role. / 获取 Bundle Role。</summary>
        public DocumentArtifactRole Role { get; }
        /// <summary>Gets model ID. / 获取模型 ID。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets artifact format. / 获取工件格式。</summary>
        public string Format { get; }
        /// <summary>Gets entrypoint SHA256. / 获取入口工件 SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets entrypoint byte size. / 获取入口工件字节数。</summary>
        public long Size { get; }
        /// <summary>Gets ONNX opset provenance. / 获取 ONNX Opset 来源。</summary>
        public int Opset { get; }
        /// <summary>Gets exact ordered inputs. / 获取精确有序输入。</summary>
        public IReadOnlyList<GenerativeVisionLanguageTensorContract> Inputs => _inputs;
        /// <summary>Gets exact ordered outputs. / 获取精确有序输出。</summary>
        public IReadOnlyList<GenerativeVisionLanguageTensorContract> Outputs => _outputs;
        /// <summary>Gets upstream revision. / 获取上游 Revision。</summary>
        public string UpstreamRevision { get; }
        /// <summary>Gets exporter identity. / 获取 Exporter Identity。</summary>
        public string Exporter { get; }
        /// <summary>Gets model/artifact license expression. / 获取模型/工件 License Expression。</summary>
        public string LicenseExpression { get; }
        /// <summary>Gets official source URL. / 获取官方 Source URL。</summary>
        public string SourceUrl { get; }
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, message);
    }

    /// <summary>Combines one immutable document family, processor, tokenizer, schema, KV, tasks, and artifact identities. / 组合不可变文档模型族、Processor、Tokenizer、Schema、KV、Task 与 Artifact Identity。</summary>
    public sealed class DocumentUnderstandingProfile
    {
        private readonly IReadOnlyList<DocumentUnderstandingTask> _tasks;
        private readonly IReadOnlyList<DocumentArtifactContract> _artifacts;

        /// <summary>Initializes one artifact-bound profile; blocked profiles remain queryable but cannot create sessions. / 初始化 Artifact-bound Profile；阻断 Profile 可查询但不能创建 Session。</summary>
        public DocumentUnderstandingProfile(string profileId, DocumentUnderstandingFamily family, string modelVersion, string upstreamRevision, DocumentOcrOwnership ocrOwnership, DocumentProcessorContract processor, DocumentTokenizerContract tokenizer, DocumentSchemaContract schema, DocumentKvCacheContract? kvCache, IEnumerable<DocumentUnderstandingTask> tasks, IEnumerable<DocumentArtifactContract> artifacts, bool executable, string? blocker = null)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(modelVersion) || string.IsNullOrWhiteSpace(upstreamRevision) || processor == null || tokenizer == null || schema == null || tasks == null || artifacts == null || (!executable && string.IsNullOrWhiteSpace(blocker))) throw Invalid("Document profile identity or capability boundary is invalid.", profileId);
            var taskValues = tasks.Distinct().OrderBy(value => (int)value).ToList(); var artifactValues = artifacts.OrderBy(value => (int)value.Role).ToList();
            if (taskValues.Count == 0 || artifactValues.Select(value => value.Role).Distinct().Count() != artifactValues.Count) throw Invalid("Document tasks are empty or artifact roles are duplicated.", profileId);
            if (ocrOwnership == DocumentOcrOwnership.NoneOcrFree && processor.MaximumWords != 0) throw Invalid("OCR-free profiles must expose zero OCR-word capacity.", profileId);
            if (executable && family == DocumentUnderstandingFamily.Donut && (kvCache == null || !new[] { DocumentArtifactRole.DocumentEncoder, DocumentArtifactRole.DecoderPrefill, DocumentArtifactRole.DecoderWithPast }.All(role => artifactValues.Any(value => value.Role == role)))) throw Invalid("Executable Donut requires Encoder, Prefill, Decode, and KV contracts.", profileId);
            ProfileId = profileId.Trim(); Family = family; ModelVersion = modelVersion.Trim(); UpstreamRevision = upstreamRevision.Trim(); OcrOwnership = ocrOwnership; Processor = processor; Tokenizer = tokenizer; Schema = schema; KvCache = kvCache; _tasks = new ReadOnlyCollection<DocumentUnderstandingTask>(taskValues); _artifacts = new ReadOnlyCollection<DocumentArtifactContract>(artifactValues); Executable = executable; Blocker = blocker?.Trim();
            ArtifactIdentity = artifactValues.Count == 0 ? "external-contract-only" : string.Join(";", artifactValues.Select(value => value.Role + "=" + value.Sha256));
            Identity = DocumentUnderstandingHash.Text(string.Join("|", ProfileId, family, ModelVersion, UpstreamRevision, ocrOwnership, Processor.Identity, Tokenizer.Identity, Schema.Identity, KvCache?.Identity ?? "none", ArtifactIdentity, executable, Blocker ?? string.Empty, string.Join(",", taskValues)));
        }
        /// <summary>Gets profile ID. / 获取 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets family. / 获取模型族。</summary>
        public DocumentUnderstandingFamily Family { get; }
        /// <summary>Gets model version. / 获取模型版本。</summary>
        public string ModelVersion { get; }
        /// <summary>Gets upstream model revision. / 获取上游模型 Revision。</summary>
        public string UpstreamRevision { get; }
        /// <summary>Gets explicit OCR ownership. / 获取显式 OCR Ownership。</summary>
        public DocumentOcrOwnership OcrOwnership { get; }
        /// <summary>Gets processor contract. / 获取 Processor 合同。</summary>
        public DocumentProcessorContract Processor { get; }
        /// <summary>Gets tokenizer/template contract. / 获取 Tokenizer/Template 合同。</summary>
        public DocumentTokenizerContract Tokenizer { get; }
        /// <summary>Gets schema contract. / 获取 Schema 合同。</summary>
        public DocumentSchemaContract Schema { get; }
        /// <summary>Gets KV contract, or null for non-generative profiles. / 获取 KV 合同；非生成 Profile 为 null。</summary>
        public DocumentKvCacheContract? KvCache { get; }
        /// <summary>Gets supported tasks. / 获取支持的任务。</summary>
        public IReadOnlyList<DocumentUnderstandingTask> Tasks => _tasks;
        /// <summary>Gets exact artifact contracts. / 获取精确 Artifact 合同。</summary>
        public IReadOnlyList<DocumentArtifactContract> Artifacts => _artifacts;
        /// <summary>Gets whether a complete audited native bundle is executable. / 获取完整已审计 Native Bundle 是否可执行。</summary>
        public bool Executable { get; }
        /// <summary>Gets reproducible blocker for a non-executable profile. / 获取非可执行 Profile 的可复现 Blocker。</summary>
        public string? Blocker { get; }
        /// <summary>Gets ordered artifact SHA identity. / 获取有序 Artifact SHA Identity。</summary>
        public string ArtifactIdentity { get; }
        /// <summary>Gets full profile identity. / 获取完整 Profile Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets an artifact contract by exact role. / 按精确角色获取 Artifact 合同。</summary>
        public DocumentArtifactContract GetArtifact(DocumentArtifactRole role) => _artifacts.SingleOrDefault(value => value.Role == role) ?? throw Invalid("The requested document artifact role is absent.", ProfileId);
        /// <summary>Creates a concrete external artifact after the application locates it. / 应用定位外部工件后创建具体 External Artifact。</summary>
        public ModelArtifact CreateArtifact(DocumentArtifactRole role, string path, BackendId backendId) { DocumentArtifactContract contract = GetArtifact(role); return new ModelArtifact(contract.ModelId, contract.Format, path, contract.Sha256, backendId); }
        private static VisualException Invalid(string message, string? profileId) => new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, message, profileId: profileId);
    }

    /// <summary>Binds one exact document artifact role to a located model entrypoint. / 将一个精确文档 Artifact Role 绑定到已定位的模型入口。</summary>
    public sealed class DocumentArtifactBinding
    {
        /// <summary>Initializes a role binding. / 初始化角色绑定。</summary>
        public DocumentArtifactBinding(DocumentArtifactRole role, ModelArtifact artifact) { Role = role; Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact)); }
        /// <summary>Gets role. / 获取角色。</summary>
        public DocumentArtifactRole Role { get; }
        /// <summary>Gets located artifact. / 获取已定位 Artifact。</summary>
        public ModelArtifact Artifact { get; }
    }

    /// <summary>Validates a complete multi-artifact bundle against one immutable profile. / 按一个不可变 Profile 校验完整多工件 Bundle。</summary>
    public sealed class DocumentUnderstandingBundle
    {
        private readonly IReadOnlyDictionary<DocumentArtifactRole, ModelArtifact> _artifacts;

        /// <summary>Initializes a complete executable bundle and rejects mixed revisions or SHA identities. / 初始化完整可执行 Bundle，并拒绝混合 Revision 或 SHA Identity。</summary>
        public DocumentUnderstandingBundle(DocumentUnderstandingProfile profile, IEnumerable<DocumentArtifactBinding> bindings)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (!profile.Executable) throw new VisualException(VisualErrorCodes.DocumentUnderstandingCapabilityUnavailable, profile.Blocker ?? "The document profile is not executable.", profileId: profile.ProfileId);
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            var values = bindings.ToList();
            if (values.Count != profile.Artifacts.Count || values.Select(value => value.Role).Distinct().Count() != values.Count) throw Invalid("Document artifact bundle is incomplete or duplicated.", profile.ProfileId);
            var dictionary = new Dictionary<DocumentArtifactRole, ModelArtifact>();
            foreach (DocumentArtifactContract contract in profile.Artifacts)
            {
                DocumentArtifactBinding binding = values.SingleOrDefault(value => value.Role == contract.Role) ?? throw Invalid("A required document artifact role is missing.", profile.ProfileId);
                if (binding.Artifact.ModelId != contract.ModelId || !string.Equals(binding.Artifact.Format, contract.Format, StringComparison.OrdinalIgnoreCase) || !string.Equals(binding.Artifact.Sha256, contract.Sha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "A document artifact differs from its profile-bound ID, format, or SHA256.", profileId: profile.ProfileId, modelId: contract.ModelId);
                dictionary.Add(contract.Role, binding.Artifact);
            }
            _artifacts = new ReadOnlyDictionary<DocumentArtifactRole, ModelArtifact>(dictionary);
            Identity = DocumentUnderstandingHash.Text(profile.Identity + "|" + string.Join("|", dictionary.OrderBy(value => (int)value.Key).Select(value => value.Key + "=" + value.Value.Sha256)));
        }
        /// <summary>Gets immutable profile. / 获取不可变 Profile。</summary>
        public DocumentUnderstandingProfile Profile { get; }
        /// <summary>Gets composite bundle identity. / 获取复合 Bundle Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets located artifact by exact role. / 按精确角色获取已定位 Artifact。</summary>
        public ModelArtifact GetArtifact(DocumentArtifactRole role) => _artifacts.TryGetValue(role, out ModelArtifact? value) ? value : throw Invalid("The requested document artifact binding is absent.", Profile.ProfileId);
        private static VisualException Invalid(string message, string profileId) => new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, message, profileId: profileId);
    }

    /// <summary>Owns one prepared page tensor, source identity, page order, and optional OCR layout. / 拥有一个 Prepared Page Tensor、源 Identity、页序与可选 OCR Layout。</summary>
    public sealed class PreparedDocumentPage : IDisposable
    {
        private bool _disposed;
        /// <summary>Initializes an owned prepared page. / 初始化自有 Prepared Page。</summary>
        public PreparedDocumentPage(string profileId, int pageIndex, PreparedVisualInput visualInput, DocumentLayoutInput? layout = null, TimeSpan preprocessTime = default(TimeSpan))
        {
            if (string.IsNullOrWhiteSpace(profileId) || pageIndex < 0) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Prepared page profile or index is invalid.");
            ProfileId = profileId.Trim(); PageIndex = pageIndex; VisualInput = visualInput ?? throw new ArgumentNullException(nameof(visualInput)); Layout = layout; PreprocessTime = preprocessTime;
            if (visualInput.InputId == null || !DocumentUnderstandingHash.IsSha256(visualInput.InputId)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "Prepared page requires the exact encoded-source SHA256.", profileId: ProfileId);
            if (layout != null && layout.PageSize != visualInput.SourceSize) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "OCR layout page size differs from decoded source size.", profileId: ProfileId);
            PageIdentity = DocumentUnderstandingHash.Text(ProfileId + "|" + pageIndex + "|" + visualInput.InputId + "|" + (layout?.Identity ?? "ocr-free"));
        }
        /// <summary>Gets bound profile ID. / 获取绑定 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets zero-based page order. / 获取从零开始的页序。</summary>
        public int PageIndex { get; }
        /// <summary>Gets prepared visual input owned by this page. / 获取本页拥有的 Prepared Visual Input。</summary>
        public PreparedVisualInput VisualInput { get; }
        /// <summary>Gets optional OCR layout. / 获取可选 OCR Layout。</summary>
        public DocumentLayoutInput? Layout { get; }
        /// <summary>Gets one-run decode/processor timing for diagnostics only. / 获取仅供诊断的单次 Decode/Processor Timing。</summary>
        public TimeSpan PreprocessTime { get; }
        /// <summary>Gets source/profile/layout-bound page identity. / 获取绑定源/Profile/Layout 的 Page Identity。</summary>
        public string PageIdentity { get; }
        /// <summary>Throws after disposal. / 释放后抛出异常。</summary>
        public void EnsureUsable() { if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The prepared document page is disposed.", profileId: ProfileId); VisualInput.EnsureUsable(); }
        /// <summary>Disposes the owned prepared visual input exactly once. / Exactly-once 释放自有 Prepared Visual Input。</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; VisualInput.Dispose(); }
    }

    /// <summary>Owns an ordered bounded set of prepared pages for one profile and source document. / 拥有一个 Profile 与源文档的有序受限 Prepared Page 集合。</summary>
    public sealed class PreparedDocument : IDisposable
    {
        private readonly IReadOnlyList<PreparedDocumentPage> _pages;
        private bool _disposed;

        /// <summary>Initializes a document and transfers ownership of ordered pages. / 初始化文档并转移有序 Page 所有权。</summary>
        public PreparedDocument(DocumentUnderstandingProfile profile, IEnumerable<PreparedDocumentPage> pages)
        {
            if (profile == null || pages == null) throw new ArgumentNullException(profile == null ? nameof(profile) : nameof(pages));
            var values = pages.ToList();
            if (values.Count == 0 || values.Count > profile.Processor.MaximumPages) throw new VisualException(VisualErrorCodes.DocumentUnderstandingLimitExceeded, "Prepared document page capacity was exceeded.", profileId: profile.ProfileId);
            for (int index = 0; index < values.Count; index++) if (values[index] == null || values[index].PageIndex != index || !string.Equals(values[index].ProfileId, profile.ProfileId, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingIdentityMismatch, "Prepared pages must be contiguous, ordered, and profile-bound.", profileId: profile.ProfileId);
            ProfileId = profile.ProfileId; _pages = new ReadOnlyCollection<PreparedDocumentPage>(values); Identity = DocumentUnderstandingHash.Text(profile.Identity + "|" + string.Join("|", values.Select(value => value.PageIdentity)));
        }
        /// <summary>Gets profile ID. / 获取 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets ordered pages. / 获取有序页面。</summary>
        public IReadOnlyList<PreparedDocumentPage> Pages => _pages;
        /// <summary>Gets profile/page/source composite identity. / 获取 Profile/Page/Source 复合 Identity。</summary>
        public string Identity { get; }
        /// <summary>Throws after disposal and validates every child page. / 释放后抛出异常并校验每个子 Page。</summary>
        public void EnsureUsable() { if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The prepared document is disposed.", profileId: ProfileId); foreach (PreparedDocumentPage page in _pages) page.EnsureUsable(); }
        /// <summary>Disposes all child pages exactly once. / Exactly-once 释放全部子 Page。</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; foreach (PreparedDocumentPage page in _pages) page.Dispose(); }
    }

    /// <summary>Describes one bounded document extraction or question prompt. / 描述一个受限文档抽取或问答 Prompt。</summary>
    public sealed class DocumentTaskRequest
    {
        private DocumentTaskRequest(DocumentUnderstandingTask task, string prompt, string schemaId) { Task = task; Prompt = prompt; SchemaId = schemaId; }
        /// <summary>Gets task. / 获取任务。</summary>
        public DocumentUnderstandingTask Task { get; }
        /// <summary>Gets caller prompt; empty uses the bound default template. / 获取调用方 Prompt；空值使用绑定默认 Template。</summary>
        public string Prompt { get; }
        /// <summary>Gets requested schema identity. / 获取请求的 Schema Identity。</summary>
        public string SchemaId { get; }
        /// <summary>Creates one schema-bound structured extraction request. / 创建 Schema-bound 结构化抽取请求。</summary>
        public static DocumentTaskRequest StructuredExtraction(string schemaId, string prompt = "") { if (string.IsNullOrWhiteSpace(schemaId)) throw new ArgumentException("Schema ID is required.", nameof(schemaId)); return new DocumentTaskRequest(DocumentUnderstandingTask.StructuredExtraction, prompt ?? string.Empty, schemaId.Trim()); }
        /// <summary>Creates one schema-bound document question. / 创建 Schema-bound 文档问题。</summary>
        public static DocumentTaskRequest Question(string question, string schemaId) { if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(schemaId)) throw new ArgumentException("Question and schema ID are required."); return new DocumentTaskRequest(DocumentUnderstandingTask.DocumentQuestionAnswering, question.Trim(), schemaId.Trim()); }
    }

    /// <summary>Owns one exact tokenizer/template output. / 拥有一个精确 Tokenizer/Template 输出。</summary>
    public sealed class DocumentTokenSequence
    {
        private readonly IReadOnlyList<long> _tokenIds;
        /// <summary>Initializes immutable bounded token IDs. / 初始化不可变受限 Token ID。</summary>
        public DocumentTokenSequence(string normalizedPrompt, IEnumerable<long> tokenIds, string tokenizerId, string tokenizerIdentity)
        {
            if (string.IsNullOrWhiteSpace(normalizedPrompt) || tokenIds == null || string.IsNullOrWhiteSpace(tokenizerId) || !DocumentUnderstandingHash.IsSha256(tokenizerIdentity)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingTokenizerInvalid, "Document token sequence identity is invalid.");
            var values = tokenIds.ToList(); if (values.Count == 0) throw new VisualException(VisualErrorCodes.DocumentUnderstandingTokenizerInvalid, "Document token sequence is empty.");
            NormalizedPrompt = normalizedPrompt; _tokenIds = new ReadOnlyCollection<long>(values); TokenizerId = tokenizerId.Trim(); TokenizerIdentity = tokenizerIdentity.ToLowerInvariant(); PromptSha256 = DocumentUnderstandingHash.Text(normalizedPrompt + "|" + string.Join(",", values));
        }
        /// <summary>Gets normalized/template-expanded prompt. / 获取 Normalize/Template-expanded Prompt。</summary>
        public string NormalizedPrompt { get; }
        /// <summary>Gets immutable token IDs. / 获取不可变 Token ID。</summary>
        public IReadOnlyList<long> TokenIds => _tokenIds;
        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets tokenizer/template identity. / 获取 Tokenizer/Template Identity。</summary>
        public string TokenizerIdentity { get; }
        /// <summary>Gets prompt and Token SHA256. / 获取 Prompt 与 Token SHA256。</summary>
        public string PromptSha256 { get; }
        /// <summary>Returns an owned token array. / 返回自有 Token 数组。</summary>
        public long[] CopyTokenIds() => _tokenIds.ToArray();
    }

    /// <summary>Encodes exact task prompts and decodes generated IDs without owning sessions or document state. / 编码精确 Task Prompt 并解码生成 ID，不拥有 Session 或 Document State。</summary>
    public interface IDocumentUnderstandingTokenizer
    {
        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets tokenizer/template identity. / 获取 Tokenizer/Template Identity。</summary>
        public string Identity { get; }
        /// <summary>Encodes one profile/schema-bound request. / 编码一个 Profile/Schema-bound 请求。</summary>
        public DocumentTokenSequence Encode(DocumentUnderstandingProfile profile, DocumentTaskRequest request);
        /// <summary>Decodes generated completion tokens including schema tags. / 解码包含 Schema Tag 的生成 Completion Token。</summary>
        public string Decode(IEnumerable<int> tokenIds);
    }

    internal static class DocumentUnderstandingHash
    {
        internal static bool IsSha256(string? value) => value != null && value.Length == 64 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') || (character >= 'A' && character <= 'F'));
        internal static string Text(string value) { using (SHA256 hash = SHA256.Create()) return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2"))); }
    }
}
