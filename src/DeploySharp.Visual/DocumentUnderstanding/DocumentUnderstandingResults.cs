using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Results.Language;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Describes the exact outcome of bounded structured-output parsing. / 描述受限结构化输出 Parse 的精确结果。</summary>
    public enum DocumentParseStatus
    {
        /// <summary>All tags were balanced and parsed under the bound schema. / 全部 Tag 平衡并按绑定 Schema Parse。</summary>
        Success = 1,
        /// <summary>Generated syntax was invalid and was not repaired. / 生成语法无效且未被修复。</summary>
        InvalidSyntax = 2,
        /// <summary>Depth, field, or text capacity was exceeded. / 超出深度、字段或文本容量。</summary>
        LimitExceeded = 3,
        /// <summary>The requested schema identity differed from the profile. / 请求 Schema Identity 与 Profile 不同。</summary>
        SchemaMismatch = 4
    }

    /// <summary>Identifies the source page and generated character span for one structured field. / 标识一个结构化字段的源页与生成字符 Span。</summary>
    public sealed class DocumentFieldProvenance
    {
        /// <summary>Initializes immutable field provenance. / 初始化不可变字段 Provenance。</summary>
        public DocumentFieldProvenance(int pageIndex, string pageIdentity, string schemaIdentity, string promptSha256, int characterStart, int characterEnd)
        {
            if (pageIndex < 0 || !DocumentUnderstandingHash.IsSha256(pageIdentity) || !DocumentUnderstandingHash.IsSha256(schemaIdentity) || !DocumentUnderstandingHash.IsSha256(promptSha256) || characterStart < 0 || characterEnd < characterStart) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Document field provenance is invalid.");
            PageIndex = pageIndex; PageIdentity = pageIdentity; SchemaIdentity = schemaIdentity; PromptSha256 = promptSha256; CharacterStart = characterStart; CharacterEnd = characterEnd;
        }
        /// <summary>Gets source page index. / 获取源页索引。</summary>
        public int PageIndex { get; }
        /// <summary>Gets exact source-page identity. / 获取精确源页 Identity。</summary>
        public string PageIdentity { get; }
        /// <summary>Gets bound schema identity. / 获取绑定 Schema Identity。</summary>
        public string SchemaIdentity { get; }
        /// <summary>Gets prompt/token SHA256. / 获取 Prompt/Token SHA256。</summary>
        public string PromptSha256 { get; }
        /// <summary>Gets inclusive generated-text start character. / 获取生成文本起始字符（含）。</summary>
        public int CharacterStart { get; }
        /// <summary>Gets exclusive generated-text end character. / 获取生成文本结束字符（不含）。</summary>
        public int CharacterEnd { get; }
    }

    /// <summary>Represents one immutable object, array occurrence, or scalar in structured document output. / 表示结构化文档输出中的一个不可变 Object、Array Occurrence 或 Scalar。</summary>
    public sealed class DocumentStructuredNode
    {
        private readonly IReadOnlyList<DocumentStructuredNode> _children;
        /// <summary>Initializes one named structured node. / 初始化一个具名结构化 Node。</summary>
        public DocumentStructuredNode(string name, string? value, IEnumerable<DocumentStructuredNode> children, DocumentFieldProvenance provenance)
        {
            if (string.IsNullOrWhiteSpace(name) || children == null) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Structured node name or children are invalid.");
            var values = children.ToList();
            if (value != null && values.Count != 0) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "A structured node cannot contain both scalar value and children.");
            Name = name; Value = value; _children = new ReadOnlyCollection<DocumentStructuredNode>(values); Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        }
        /// <summary>Gets schema field name. / 获取 Schema 字段名。</summary>
        public string Name { get; }
        /// <summary>Gets scalar value, or null for an object. / 获取 Scalar Value；Object 为 null。</summary>
        public string? Value { get; }
        /// <summary>Gets ordered child occurrences; repeated names are preserved. / 获取有序子 Occurrence；保留重复名称。</summary>
        public IReadOnlyList<DocumentStructuredNode> Children => _children;
        /// <summary>Gets source/schema/prompt provenance. / 获取 Source/Schema/Prompt Provenance。</summary>
        public DocumentFieldProvenance Provenance { get; }
    }

    /// <summary>Owns raw tokens/text, exact parse status, deterministic JSON, schema identity, and field provenance. / 拥有原始 Token/Text、精确 Parse Status、确定性 JSON、Schema Identity 与字段 Provenance。</summary>
    public sealed class DocumentStructuredOutput
    {
        private readonly IReadOnlyList<int> _tokenIds;
        private readonly IReadOnlyList<DocumentStructuredNode> _nodes;
        /// <summary>Initializes an immutable parse result; invalid output retains raw evidence and has no successful JSON. / 初始化不可变 Parse Result；无效输出保留原始证据且没有成功 JSON。</summary>
        public DocumentStructuredOutput(IEnumerable<int> tokenIds, string rawText, DocumentParseStatus status, string schemaId, string schemaIdentity, IEnumerable<DocumentStructuredNode> nodes, string? json, string? diagnostic)
        {
            if (tokenIds == null || rawText == null || string.IsNullOrWhiteSpace(schemaId) || !DocumentUnderstandingHash.IsSha256(schemaIdentity) || nodes == null || (status == DocumentParseStatus.Success) != (json != null)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Structured output ownership or parse status is invalid.");
            _tokenIds = new ReadOnlyCollection<int>(tokenIds.ToList()); _nodes = new ReadOnlyCollection<DocumentStructuredNode>(nodes.ToList()); RawText = rawText; Status = status; SchemaId = schemaId; SchemaIdentity = schemaIdentity; Json = json; Diagnostic = diagnostic;
        }
        /// <summary>Gets copied generated token IDs. / 获取复制的生成 Token ID。</summary>
        public IReadOnlyList<int> TokenIds => _tokenIds;
        /// <summary>Gets unmodified tokenizer text before parsing. / 获取 Parse 前未修改的 Tokenizer 文本。</summary>
        public string RawText { get; }
        /// <summary>Gets parse status. / 获取 Parse Status。</summary>
        public DocumentParseStatus Status { get; }
        /// <summary>Gets schema ID. / 获取 Schema ID。</summary>
        public string SchemaId { get; }
        /// <summary>Gets profile-bound schema identity. / 获取 Profile-bound Schema Identity。</summary>
        public string SchemaIdentity { get; }
        /// <summary>Gets ordered root fields, including repeated occurrences. / 获取有序根字段，包括重复 Occurrence。</summary>
        public IReadOnlyList<DocumentStructuredNode> Nodes => _nodes;
        /// <summary>Gets deterministic JSON only after successful exact parsing. / 仅在精确 Parse 成功后获取确定性 JSON。</summary>
        public string? Json { get; }
        /// <summary>Gets stable failure diagnostic without repaired output. / 获取稳定失败 Diagnostic，不包含修复后输出。</summary>
        public string? Diagnostic { get; }
    }

    /// <summary>Summarizes one cached encoded page without exposing mutable feature tensors. / 汇总一个缓存的编码页面，不公开可变 Feature Tensor。</summary>
    public sealed class DocumentEncodedState
    {
        /// <summary>Initializes one immutable encoded-state summary. / 初始化不可变 Encoded State Summary。</summary>
        public DocumentEncodedState(string identity, string documentIdentity, string pageIdentity, string artifactIdentity, string processorIdentity, string schemaIdentity, long[] shape, string featureSha256, TimeSpan encodeTime)
        {
            if (!DocumentUnderstandingHash.IsSha256(identity) || !DocumentUnderstandingHash.IsSha256(documentIdentity) || !DocumentUnderstandingHash.IsSha256(pageIdentity) || string.IsNullOrWhiteSpace(artifactIdentity) || !DocumentUnderstandingHash.IsSha256(processorIdentity) || !DocumentUnderstandingHash.IsSha256(schemaIdentity) || shape == null || !DocumentUnderstandingHash.IsSha256(featureSha256)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Encoded document state is invalid.");
            Identity = identity; DocumentIdentity = documentIdentity; PageIdentity = pageIdentity; ArtifactIdentity = artifactIdentity; ProcessorIdentity = processorIdentity; SchemaIdentity = schemaIdentity; Shape = (long[])shape.Clone(); FeatureSha256 = featureSha256; EncodeTime = encodeTime;
        }
        /// <summary>Gets composite state identity. / 获取复合 State Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets prepared-document identity. / 获取 Prepared Document Identity。</summary>
        public string DocumentIdentity { get; }
        /// <summary>Gets encoded source-page identity. / 获取已编码源页 Identity。</summary>
        public string PageIdentity { get; }
        /// <summary>Gets ordered artifact identity. / 获取有序 Artifact Identity。</summary>
        public string ArtifactIdentity { get; }
        /// <summary>Gets processor identity. / 获取 Processor Identity。</summary>
        public string ProcessorIdentity { get; }
        /// <summary>Gets schema identity. / 获取 Schema Identity。</summary>
        public string SchemaIdentity { get; }
        /// <summary>Gets owned feature shape. / 获取自有 Feature Shape。</summary>
        public long[] Shape { get; }
        /// <summary>Gets feature SHA256. / 获取 Feature SHA256。</summary>
        public string FeatureSha256 { get; }
        /// <summary>Gets one-run Encoder time. / 获取单次 Encoder 时间。</summary>
        public TimeSpan EncodeTime { get; }
    }

    /// <summary>Summarizes final self/cross KV without exposing mutable tensors. / 汇总最终 Self/Cross KV，不公开可变 Tensor。</summary>
    public sealed class DocumentKvStateSummary
    {
        /// <summary>Initializes immutable KV summary. / 初始化不可变 KV Summary。</summary>
        public DocumentKvStateSummary(string schemaId, int layers, int heads, int selfTokens, int crossTokens, int headDimension, string sha256, string promptSha256)
        {
            if (string.IsNullOrWhiteSpace(schemaId) || layers <= 0 || heads <= 0 || selfTokens <= 0 || crossTokens <= 0 || headDimension <= 0 || !DocumentUnderstandingHash.IsSha256(sha256) || !DocumentUnderstandingHash.IsSha256(promptSha256)) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Document KV summary is invalid.");
            SchemaId = schemaId; Layers = layers; Heads = heads; SelfTokens = selfTokens; CrossTokens = crossTokens; HeadDimension = headDimension; Sha256 = sha256; PromptSha256 = promptSha256; Identity = DocumentUnderstandingHash.Text(string.Join("|", schemaId, layers, heads, selfTokens, crossTokens, headDimension, sha256, promptSha256));
        }
        /// <summary>Gets KV schema ID. / 获取 KV Schema ID。</summary>
        public string SchemaId { get; }
        /// <summary>Gets layer count. / 获取 Layer 数。</summary>
        public int Layers { get; }
        /// <summary>Gets head count. / 获取 Head 数。</summary>
        public int Heads { get; }
        /// <summary>Gets final self-attention token count. / 获取最终 Self-attention Token 数。</summary>
        public int SelfTokens { get; }
        /// <summary>Gets cross-attention encoder token count. / 获取 Cross-attention Encoder Token 数。</summary>
        public int CrossTokens { get; }
        /// <summary>Gets head dimension. / 获取 Head Dimension。</summary>
        public int HeadDimension { get; }
        /// <summary>Gets ordered KV value SHA256. / 获取有序 KV Value SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets prompt SHA256. / 获取 Prompt SHA256。</summary>
        public string PromptSha256 { get; }
        /// <summary>Gets composite KV identity. / 获取复合 KV Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Contains one-run processor, encoder, tokenizer, Prefill, Decode, and parse timings for diagnostics only. / 包含仅供诊断的单次 Processor、Encoder、Tokenizer、Prefill、Decode 与 Parse Timing。</summary>
    public sealed class DocumentExecutionTiming
    {
        private readonly IReadOnlyList<TimeSpan> _decodeSteps;
        /// <summary>Initializes one-run stage timings. / 初始化单次分阶段 Timing。</summary>
        public DocumentExecutionTiming(TimeSpan preprocess, TimeSpan encode, TimeSpan tokenize, TimeSpan prefill, IEnumerable<TimeSpan> decodeSteps, TimeSpan finalDecode, TimeSpan parse)
        {
            if (decodeSteps == null) throw new ArgumentNullException(nameof(decodeSteps)); Preprocess = preprocess; Encode = encode; Tokenize = tokenize; Prefill = prefill; _decodeSteps = new ReadOnlyCollection<TimeSpan>(decodeSteps.ToList()); FinalDecode = finalDecode; Parse = parse; DecodeTotal = TimeSpan.FromTicks(_decodeSteps.Sum(value => value.Ticks));
        }
        /// <summary>Gets OpenCV processor time when supplied by the prepared page, otherwise zero. / 获取 Prepared Page 提供的 OpenCV Processor 时间，否则为零。</summary>
        public TimeSpan Preprocess { get; }
        /// <summary>Gets document Encoder time. / 获取 Document Encoder 时间。</summary>
        public TimeSpan Encode { get; }
        /// <summary>Gets prompt tokenizer time. / 获取 Prompt Tokenizer 时间。</summary>
        public TimeSpan Tokenize { get; }
        /// <summary>Gets Prefill time. / 获取 Prefill 时间。</summary>
        public TimeSpan Prefill { get; }
        /// <summary>Gets per-token Decode times after Prefill. / 获取 Prefill 后逐 Token Decode 时间。</summary>
        public IReadOnlyList<TimeSpan> DecodeSteps => _decodeSteps;
        /// <summary>Gets total per-token Decode time. / 获取逐 Token Decode 总时间。</summary>
        public TimeSpan DecodeTotal { get; }
        /// <summary>Gets final tokenizer decode time. / 获取最终 Tokenizer Decode 时间。</summary>
        public TimeSpan FinalDecode { get; }
        /// <summary>Gets structured parse time. / 获取结构化 Parse 时间。</summary>
        public TimeSpan Parse { get; }
    }

    /// <summary>Contains common generation ownership plus structured output, encoded state, KV, and timing. / 包含通用 Generation Ownership、结构化输出、Encoded State、KV 与 Timing。</summary>
    public sealed class DocumentUnderstandingResult
    {
        /// <summary>Initializes one complete owned result. / 初始化一个完整自有 Result。</summary>
        public DocumentUnderstandingResult(GenerationResult generation, DocumentTaskRequest request, DocumentStructuredOutput structuredOutput, DocumentEncodedState documentState, DocumentKvStateSummary kvState, DocumentExecutionTiming timing)
        {
            Generation = generation ?? throw new ArgumentNullException(nameof(generation)); Request = request ?? throw new ArgumentNullException(nameof(request)); StructuredOutput = structuredOutput ?? throw new ArgumentNullException(nameof(structuredOutput)); DocumentState = documentState ?? throw new ArgumentNullException(nameof(documentState)); KvState = kvState ?? throw new ArgumentNullException(nameof(kvState)); Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        }
        /// <summary>Gets common owned generation result. / 获取通用自有 Generation Result。</summary>
        public GenerationResult Generation { get; }
        /// <summary>Gets immutable task request. / 获取不可变 Task Request。</summary>
        public DocumentTaskRequest Request { get; }
        /// <summary>Gets raw and parsed structured output. / 获取原始与 Parsed 结构化输出。</summary>
        public DocumentStructuredOutput StructuredOutput { get; }
        /// <summary>Gets encoded source state. / 获取已编码 Source State。</summary>
        public DocumentEncodedState DocumentState { get; }
        /// <summary>Gets final KV summary. / 获取最终 KV Summary。</summary>
        public DocumentKvStateSummary KvState { get; }
        /// <summary>Gets one-run diagnostic timings. / 获取单次诊断 Timing。</summary>
        public DocumentExecutionTiming Timing { get; }
    }

    internal static class DocumentFeatureSummary
    {
        internal static string Sha(float[] values)
        {
            var bytes = new byte[checked(values.Length * sizeof(float))]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using (SHA256 hash = SHA256.Create()) return string.Concat(hash.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }
    }
}
