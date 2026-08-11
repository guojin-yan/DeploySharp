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
    /// <summary>Identifies an audited dual-encoder family. / 标识已审计的双编码器模型族。</summary>
    public enum VisionLanguageModelFamily
    {
        /// <summary>OpenAI CLIP family. / OpenAI CLIP 模型族。</summary>
        Clip = 1,
        /// <summary>Google SigLIP family. / Google SigLIP 模型族。</summary>
        SigLip = 2,
        /// <summary>Google SigLIP 2 family. / Google SigLIP 2 模型族。</summary>
        SigLip2 = 3
    }

    /// <summary>Identifies the pair-scoring contract. / 标识图文配对评分合同。</summary>
    public enum VisionLanguageScoreSemantics
    {
        /// <summary>Softmax is applied across the requested text candidates. / 在请求的文本候选维度执行 Softmax。</summary>
        ClipSoftmax = 1,
        /// <summary>Each image-text pair owns an independent sigmoid probability. / 每个图文对拥有独立 Sigmoid 概率。</summary>
        SigLipIndependentSigmoid = 2
    }

    /// <summary>Identifies an auditable VLM artifact role. / 标识可审计 VLM 工件角色。</summary>
    public enum VisionLanguageArtifactRole
    {
        /// <summary>Projected and normalized image encoder. / 投影并归一化的图像编码器。</summary>
        ImageEncoder = 1,
        /// <summary>Projected and normalized text encoder. / 投影并归一化的文本编码器。</summary>
        TextEncoder = 2
    }

    /// <summary>Identifies how text is pooled by the official encoder. / 标识官方编码器如何池化文本。</summary>
    public enum VisionLanguagePooling
    {
        /// <summary>Use the official end-of-text pooler. / 使用官方 End-of-Text 池化。</summary>
        EndOfText = 1,
        /// <summary>Use the model's configured text pooler. / 使用模型配置的文本池化器。</summary>
        ModelPooler = 2
    }

    /// <summary>Identifies the official image geometry before encoding. / 标识编码前的官方图像几何变换。</summary>
    public enum VisionLanguageImageResizeMode
    {
        /// <summary>Resize the shortest edge then take a center crop. / 缩放最短边后执行中心裁剪。</summary>
        ShortestEdgeCenterCrop = 1,
        /// <summary>Resize directly to the fixed canvas. / 直接缩放到固定画布。</summary>
        FixedResize = 2
    }

    /// <summary>Defines one exact named encoder port. / 定义一个精确具名编码器端口。</summary>
    public sealed class VisionLanguageTensorContract
    {
        /// <summary>Initializes a tensor contract. / 初始化张量合同。</summary>
        public VisionLanguageTensorContract(string name, TensorElementType elementType, TensorShape shapePattern, long maximumElements = 4_000_000)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A tensor name is required.", tensorName: name);
            if (elementType == TensorElementType.Unknown || elementType == TensorElementType.String) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The tensor element type is unsupported.", tensorName: name);
            if (shapePattern == null) throw new ArgumentNullException(nameof(shapePattern));
            if (maximumElements <= 0) throw new VisualException(VisualErrorCodes.VisionLanguageLimitExceeded, "Tensor capacity must be positive.", tensorName: name);
            Name = name;
            ElementType = elementType;
            ShapePattern = new TensorShape(shapePattern.ToArray());
            MaximumElements = maximumElements;
        }

        /// <summary>Gets the exact port name. / 获取精确端口名称。</summary>
        public string Name { get; }
        /// <summary>Gets the required element type. / 获取所需元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the static or dynamic shape pattern. / 获取静态或动态形状模式。</summary>
        public TensorShape ShapePattern { get; }
        /// <summary>Gets the maximum accepted element count. / 获取接受的最大元素数量。</summary>
        public long MaximumElements { get; }
    }

    /// <summary>Binds one exact encoder artifact to its provenance and ports. / 将一个精确编码器工件绑定到来源和端口。</summary>
    public sealed class VisionLanguageArtifactContract
    {
        private readonly IReadOnlyList<VisionLanguageTensorContract> _inputs;
        private readonly IReadOnlyList<VisionLanguageTensorContract> _outputs;

        /// <summary>Initializes an immutable artifact contract. / 初始化不可变工件合同。</summary>
        public VisionLanguageArtifactContract(
            VisionLanguageArtifactRole role,
            ModelId modelId,
            string format,
            string sha256,
            long size,
            int opset,
            IEnumerable<VisionLanguageTensorContract> inputs,
            IEnumerable<VisionLanguageTensorContract> outputs,
            string upstreamCommit,
            string exporter,
            string license,
            string? sourceUri = null,
            string? externalDataSha256 = null)
        {
            if (!Enum.IsDefined(typeof(VisionLanguageArtifactRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (modelId.IsEmpty) throw new ArgumentException("A model identifier is required.", nameof(modelId));
            if (string.IsNullOrWhiteSpace(format) || string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64 || size <= 0 || opset <= 0) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Artifact provenance is incomplete.", modelId: modelId);
            if (string.IsNullOrWhiteSpace(upstreamCommit) || string.IsNullOrWhiteSpace(exporter) || string.IsNullOrWhiteSpace(license)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Artifact source, exporter, and license are required.", modelId: modelId);
            Role = role;
            ModelId = modelId;
            Format = format.Trim().ToLowerInvariant();
            Sha256 = sha256.ToLowerInvariant();
            Size = size;
            Opset = opset;
            _inputs = Copy(inputs, nameof(inputs));
            _outputs = Copy(outputs, nameof(outputs));
            UpstreamCommit = upstreamCommit.Trim();
            Exporter = exporter.Trim();
            License = license.Trim();
            SourceUri = NormalizeOptional(sourceUri, false);
            ExternalDataSha256 = NormalizeOptional(externalDataSha256, true);
        }

        /// <summary>Gets the artifact role. / 获取工件角色。</summary>
        public VisionLanguageArtifactRole Role { get; }
        /// <summary>Gets the logical model identifier. / 获取逻辑模型标识符。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets the normalized format. / 获取规范化格式。</summary>
        public string Format { get; }
        /// <summary>Gets the exact SHA256. / 获取精确 SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets the artifact byte size. / 获取工件字节大小。</summary>
        public long Size { get; }
        /// <summary>Gets the ONNX opset. / 获取 ONNX Opset。</summary>
        public int Opset { get; }
        /// <summary>Gets exact named inputs. / 获取精确具名输入。</summary>
        public IReadOnlyList<VisionLanguageTensorContract> Inputs => _inputs;
        /// <summary>Gets exact named outputs. / 获取精确具名输出。</summary>
        public IReadOnlyList<VisionLanguageTensorContract> Outputs => _outputs;
        /// <summary>Gets the upstream commit or revision. / 获取上游 Commit 或 Revision。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets the reproducible exporter chain. / 获取可复现导出链。</summary>
        public string Exporter { get; }
        /// <summary>Gets the independent license. / 获取独立许可证。</summary>
        public string License { get; }
        /// <summary>Gets the source checkpoint/model URI. / 获取源 Checkpoint/模型 URI。</summary>
        public string? SourceUri { get; }
        /// <summary>Gets the optional external-data SHA256. / 获取可选 External-data SHA256。</summary>
        public string? ExternalDataSha256 { get; }

        private static IReadOnlyList<VisionLanguageTensorContract> Copy(IEnumerable<VisionLanguageTensorContract> values, string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            var list = new List<VisionLanguageTensorContract>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (VisionLanguageTensorContract value in values)
            {
                if (value == null || !names.Add(value.Name)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Tensor names must be unique.");
                list.Add(value);
            }
            if (list.Count == 0) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "An encoder must declare at least one tensor.");
            return new ReadOnlyCollection<VisionLanguageTensorContract>(list);
        }

        private static string? NormalizeOptional(string? value, bool lowerInvariant)
        {
            if (value == null) return null;
            string trimmed = value.Trim();
            if (trimmed.Length == 0) return null;
            return lowerInvariant ? trimmed.ToLowerInvariant() : trimmed;
        }
    }

    /// <summary>Describes the tokenizer and preprocessing identity used by one profile. / 描述一个 Profile 使用的 Tokenizer 与文本预处理 Identity。</summary>
    public sealed class VisionLanguageTokenizerContract
    {
        /// <summary>Initializes a tokenizer contract. / 初始化 Tokenizer 合同。</summary>
        public VisionLanguageTokenizerContract(string tokenizerId, string sha256, int maximumTokens, int bosTokenId, int eosTokenId, int padTokenId, bool attentionMaskRequired, string normalization, string tokenizerClass)
        {
            if (string.IsNullOrWhiteSpace(tokenizerId) || string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64 || maximumTokens <= 0 || bosTokenId < -1 || eosTokenId < 0 || padTokenId < 0 || string.IsNullOrWhiteSpace(tokenizerClass)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Tokenizer provenance or capacity is invalid.");
            TokenizerId = tokenizerId.Trim();
            Sha256 = sha256.Trim().ToLowerInvariant();
            MaximumTokens = maximumTokens;
            BosTokenId = bosTokenId;
            EosTokenId = eosTokenId;
            PadTokenId = padTokenId;
            AttentionMaskRequired = attentionMaskRequired;
            Normalization = string.IsNullOrWhiteSpace(normalization) ? "exact" : normalization.Trim();
            TokenizerClass = tokenizerClass.Trim();
        }

        /// <summary>Gets the stable tokenizer ID. / 获取稳定 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets tokenizer sidecar SHA256. / 获取 Tokenizer sidecar SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets maximum sequence length. / 获取最大序列长度。</summary>
        public int MaximumTokens { get; }
        /// <summary>Gets BOS ID, or -1 when the tokenizer has no BOS token. / 获取 BOS ID；Tokenizer 没有 BOS Token 时为 -1。</summary>
        public int BosTokenId { get; }
        /// <summary>Gets EOS ID. / 获取 EOS ID。</summary>
        public int EosTokenId { get; }
        /// <summary>Gets PAD ID. / 获取 PAD ID。</summary>
        public int PadTokenId { get; }
        /// <summary>Gets whether the official text port requires attention_mask. / 获取官方文本端口是否要求 attention_mask。</summary>
        public bool AttentionMaskRequired { get; }
        /// <summary>Gets the official normalization description. / 获取官方规范化说明。</summary>
        public string Normalization { get; }
        /// <summary>Gets the official tokenizer implementation class. / 获取官方 Tokenizer 实现类。</summary>
        public string TokenizerClass { get; }
    }

    /// <summary>Defines an immutable, artifact-bound image/text encoder family contract. / 定义不可变、工件绑定的图像/文本编码器模型族合同。</summary>
    public sealed class VisionLanguageEmbeddingProfile
    {
        private readonly IReadOnlyList<VisionLanguageArtifactContract> _artifacts;

        /// <summary>Initializes a complete or external-blocker profile. / 初始化完整 Profile 或 External Blocker Profile。</summary>
        public VisionLanguageEmbeddingProfile(
            string profileId,
            VisionLanguageModelFamily family,
            string variant,
            VisionLanguageTokenizerContract tokenizer,
            IEnumerable<VisionLanguageArtifactContract> artifacts,
            int embeddingDimension,
            VisionLanguagePooling pooling,
            VisionLanguageScoreSemantics scoreSemantics,
            float logitScale,
            float logitBias,
            VisualSize imageSize,
            IEnumerable<float> imageMean,
            IEnumerable<float> imageStandardDeviation,
            VisionLanguageImageResizeMode imageResizeMode,
            string version,
            bool executable,
            string? blocker = null,
            int maximumTextBatch = 64,
            int maximumImageBatch = 16)
        {
            ProfileId = VisualGuard.Identifier(profileId, nameof(profileId));
            if (!Enum.IsDefined(typeof(VisionLanguageModelFamily), family) || !Enum.IsDefined(typeof(VisionLanguagePooling), pooling) || !Enum.IsDefined(typeof(VisionLanguageScoreSemantics), scoreSemantics)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The VLM family or scoring enum is invalid.", profileId: ProfileId);
            if (tokenizer == null || artifacts == null || embeddingDimension <= 0 || logitScale <= 0 || imageSize.Width <= 0 || imageSize.Height <= 0 || maximumTextBatch <= 0 || maximumImageBatch <= 0) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The VLM profile capacity or preprocessing contract is invalid.", profileId: ProfileId);
            Family = family;
            Variant = string.IsNullOrWhiteSpace(variant) ? throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A VLM variant is required.", profileId: ProfileId) : variant.Trim();
            Tokenizer = tokenizer;
            var list = artifacts.ToList();
            if (list.Any(value => value == null) || list.Select(value => value.Role).Distinct().Count() != list.Count || (executable && (!list.Any(value => value.Role == VisionLanguageArtifactRole.ImageEncoder) || !list.Any(value => value.Role == VisionLanguageArtifactRole.TextEncoder)))) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Executable profiles require both unique image and text encoder artifacts.", profileId: ProfileId);
            _artifacts = new ReadOnlyCollection<VisionLanguageArtifactContract>(list);
            EmbeddingDimension = embeddingDimension;
            Pooling = pooling;
            ScoreSemantics = scoreSemantics;
            LogitScale = logitScale;
            LogitBias = logitBias;
            ImageSize = imageSize;
            ImageMean = CopyFinite(imageMean, 3, "imageMean");
            ImageStandardDeviation = CopyPositive(imageStandardDeviation, 3, "imageStandardDeviation");
            if (!Enum.IsDefined(typeof(VisionLanguageImageResizeMode), imageResizeMode)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "The VLM image resize mode is invalid.", profileId: ProfileId);
            ImageResizeMode = imageResizeMode;
            Version = string.IsNullOrWhiteSpace(version) ? throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A VLM version is required.", profileId: ProfileId) : version.Trim();
            Executable = executable;
            Blocker = NormalizeOptional(blocker);
            MaximumTextBatch = maximumTextBatch;
            MaximumImageBatch = maximumImageBatch;
            ArtifactIdentity = ComputeArtifactIdentity(_artifacts);
        }

        /// <summary>Gets the stable profile identifier. / 获取稳定 Profile 标识符。</summary>
        public string ProfileId { get; }
        /// <summary>Gets the model family. / 获取模型族。</summary>
        public VisionLanguageModelFamily Family { get; }
        /// <summary>Gets the exact upstream variant. / 获取精确上游变体。</summary>
        public string Variant { get; }
        /// <summary>Gets tokenizer identity. / 获取 Tokenizer Identity。</summary>
        public VisionLanguageTokenizerContract Tokenizer { get; }
        /// <summary>Gets image/text artifacts. / 获取图像/文本工件。</summary>
        public IReadOnlyList<VisionLanguageArtifactContract> Artifacts => _artifacts;
        /// <summary>Gets the projected embedding dimension. / 获取投影 Embedding 维度。</summary>
        public int EmbeddingDimension { get; }
        /// <summary>Gets official pooling semantics. / 获取官方池化语义。</summary>
        public VisionLanguagePooling Pooling { get; }
        /// <summary>Gets pair scoring semantics. / 获取配对评分语义。</summary>
        public VisionLanguageScoreSemantics ScoreSemantics { get; }
        /// <summary>Gets the official positive logit scale. / 获取官方正 Logit Scale。</summary>
        public float LogitScale { get; }
        /// <summary>Gets the official logit bias. / 获取官方 Logit Bias。</summary>
        public float LogitBias { get; }
        /// <summary>Gets the model image canvas. / 获取模型图像画布。</summary>
        public VisualSize ImageSize { get; }
        /// <summary>Gets RGB means in the 0..255 input domain. / 获取 0..255 输入域中的 RGB 均值。</summary>
        public IReadOnlyList<float> ImageMean { get; }
        /// <summary>Gets RGB standard deviations in the 0..255 input domain. / 获取 0..255 输入域中的 RGB 标准差。</summary>
        public IReadOnlyList<float> ImageStandardDeviation { get; }
        /// <summary>Gets official image resize mode. / 获取官方图像缩放模式。</summary>
        public VisionLanguageImageResizeMode ImageResizeMode { get; }
        /// <summary>Gets the profile version. / 获取 Profile 版本。</summary>
        public string Version { get; }
        /// <summary>Gets whether both encoder artifacts are locally executable. / 获取两条 Encoder 工件是否可本机执行。</summary>
        public bool Executable { get; }
        /// <summary>Gets a reproducible blocker when execution is unavailable. / 获取执行不可用时的可复现阻断原因。</summary>
        public string? Blocker { get; }
        /// <summary>Gets the maximum text batch. / 获取最大文本批次。</summary>
        public int MaximumTextBatch { get; }
        /// <summary>Gets the maximum image batch. / 获取最大图像批次。</summary>
        public int MaximumImageBatch { get; }
        /// <summary>Gets the deterministic identity of the encoder artifacts. / 获取 Encoder 工件的确定性 Identity。</summary>
        public string ArtifactIdentity { get; }

        /// <summary>Gets one artifact by role. / 按角色获取一个工件。</summary>
        public VisionLanguageArtifactContract GetArtifact(VisionLanguageArtifactRole role)
        {
            VisionLanguageArtifactContract? artifact = _artifacts.SingleOrDefault(value => value.Role == role);
            if (artifact == null) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, Blocker ?? "The requested VLM artifact is unavailable.", profileId: ProfileId);
            return artifact;
        }

        /// <summary>Creates a Core artifact bound to this profile. / 创建绑定到此 Profile 的 Core 工件。</summary>
        public ModelArtifact CreateArtifact(VisionLanguageArtifactRole role, string location, BackendId? backend = null)
        {
            if (!Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, Blocker ?? "The VLM profile is external-only.", profileId: ProfileId);
            VisionLanguageArtifactContract contract = GetArtifact(role);
            return new ModelArtifact(contract.ModelId, contract.Format, location, contract.Sha256, backend);
        }

        private static IReadOnlyList<float> CopyFinite(IEnumerable<float> values, int count, string name)
        {
            var list = values == null ? throw new ArgumentNullException(name) : values.ToList();
            if (list.Count != count || list.Any(value => float.IsNaN(value) || float.IsInfinity(value))) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Image normalization must contain three finite values.", technicalDetails: name);
            return new ReadOnlyCollection<float>(list);
        }

        private static string? NormalizeOptional(string? value)
        {
            if (value == null) return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static IReadOnlyList<float> CopyPositive(IEnumerable<float> values, int count, string name)
        {
            var list = CopyFinite(values, count, name).ToList();
            if (list.Any(value => value <= 0)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Image standard deviation must be positive.", technicalDetails: name);
            return new ReadOnlyCollection<float>(list);
        }

        private static string ComputeArtifactIdentity(IEnumerable<VisionLanguageArtifactContract> artifacts)
        {
            using (SHA256 sha = SHA256.Create())
            {
                string value = string.Join("|", artifacts.OrderBy(item => item.Role).Select(item => item.Role + ":" + item.ModelId + ":" + item.Sha256));
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2")));
            }
        }
    }

    /// <summary>Associates concrete image/text paths with one immutable VLM profile. / 将具体图像/文本路径关联到不可变 VLM Profile。</summary>
    public sealed class VisionLanguageArtifactBundle
    {
        /// <summary>Initializes and validates an encoder bundle. / 初始化并验证 Encoder Bundle。</summary>
        public VisionLanguageArtifactBundle(VisionLanguageEmbeddingProfile profile, ModelArtifact imageEncoder, ModelArtifact textEncoder)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            ImageEncoder = imageEncoder ?? throw new ArgumentNullException(nameof(imageEncoder));
            TextEncoder = textEncoder ?? throw new ArgumentNullException(nameof(textEncoder));
            Validate(profile.GetArtifact(VisionLanguageArtifactRole.ImageEncoder), imageEncoder);
            Validate(profile.GetArtifact(VisionLanguageArtifactRole.TextEncoder), textEncoder);
        }

        /// <summary>Gets the immutable profile. / 获取不可变 Profile。</summary>
        public VisionLanguageEmbeddingProfile Profile { get; }
        /// <summary>Gets the concrete image encoder artifact. / 获取具体图像 Encoder 工件。</summary>
        public ModelArtifact ImageEncoder { get; }
        /// <summary>Gets the concrete text encoder artifact. / 获取具体文本 Encoder 工件。</summary>
        public ModelArtifact TextEncoder { get; }

        private static void Validate(VisionLanguageArtifactContract expected, ModelArtifact actual)
        {
            if (expected.ModelId != actual.ModelId || !string.Equals(expected.Format, actual.Format, StringComparison.OrdinalIgnoreCase) || !string.Equals(expected.Sha256, actual.Sha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.VisionLanguageIdentityMismatch, "An encoder artifact does not match the profile-bound ID, format, and SHA256.", modelId: expected.ModelId);
        }
    }

    /// <summary>Owns official tokenizer output for one ordered text batch. / 拥有一个有序文本批次的官方 Tokenizer 输出。</summary>
    public sealed class TextTokenBatch
    {
        private readonly IReadOnlyList<string> _texts;
        private readonly long[] _inputIds;
        private readonly long[]? _attentionMask;

        /// <summary>Initializes a defensive, capacity-bounded token batch. / 初始化防御性、容量受限的 Token 批次。</summary>
        public TextTokenBatch(IEnumerable<string> texts, long[] inputIds, int batchSize, int sequenceLength, string tokenizerId, string tokenizerSha256, long[]? attentionMask = null)
        {
            if (texts == null || inputIds == null || string.IsNullOrWhiteSpace(tokenizerId) || string.IsNullOrWhiteSpace(tokenizerSha256) || tokenizerSha256.Length != 64 || batchSize <= 0 || sequenceLength <= 0 || inputIds.LongLength != (long)batchSize * sequenceLength || (attentionMask != null && attentionMask.LongLength != inputIds.LongLength)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Token batch shape, identity, or text values are invalid.");
            var textList = texts.Select(value => value ?? throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Text prompts cannot be null.")).ToList();
            if (textList.Count != batchSize || textList.Any(value => string.IsNullOrWhiteSpace(value))) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Text batch count and non-empty prompt values are required.");
            if (inputIds.Any(value => value < 0) || (attentionMask != null && attentionMask.Any(value => value != 0 && value != 1))) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Token IDs and attention mask values are invalid.");
            _texts = new ReadOnlyCollection<string>(textList);
            _inputIds = (long[])inputIds.Clone();
            _attentionMask = attentionMask == null ? null : (long[])attentionMask.Clone();
            BatchSize = batchSize;
            SequenceLength = sequenceLength;
            TokenizerId = tokenizerId.Trim();
            TokenizerSha256 = tokenizerSha256.Trim().ToLowerInvariant();
            ContentSha256 = HashIdentity();
        }

        /// <summary>Gets exact caller text in stable order. / 获取稳定顺序中的调用方原始文本。</summary>
        public IReadOnlyList<string> Texts => _texts;
        /// <summary>Gets token batch size. / 获取 Token 批次大小。</summary>
        public int BatchSize { get; }
        /// <summary>Gets fixed sequence length. / 获取固定序列长度。</summary>
        public int SequenceLength { get; }
        /// <summary>Gets tokenizer identity. / 获取 Tokenizer Identity。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets tokenizer sidecar SHA256. / 获取 Tokenizer sidecar SHA256。</summary>
        public string TokenizerSha256 { get; }
        /// <summary>Gets token content identity. / 获取 Token 内容 Identity。</summary>
        public string ContentSha256 { get; }
        /// <summary>Gets a defensive copy of input IDs. / 获取 input IDs 防御性副本。</summary>
        public long[] CopyInputIds() => (long[])_inputIds.Clone();
        /// <summary>Gets a defensive copy of attention mask or null when the official port omits it. / 获取 attention mask 防御性副本；官方端口省略时为 null。</summary>
        public long[]? CopyAttentionMask() => _attentionMask == null ? null : (long[])_attentionMask.Clone();

        private string HashIdentity()
        {
            using (SHA256 sha = SHA256.Create())
            {
                var bytes = new List<byte>(Encoding.UTF8.GetBytes(TokenizerId + "|" + TokenizerSha256 + "|" + SequenceLength));
                foreach (string text in _texts) bytes.AddRange(Encoding.UTF8.GetBytes("|" + text));
                foreach (long value in _inputIds) bytes.AddRange(BitConverter.GetBytes(value));
                if (_attentionMask != null) foreach (long value in _attentionMask) bytes.AddRange(BitConverter.GetBytes(value));
                return string.Concat(sha.ComputeHash(bytes.ToArray()).Select(item => item.ToString("x2")));
            }
        }
    }

    /// <summary>Groups text prompt indexes under one zero-shot class label. / 将文本提示索引归组到一个零样本类别标签。</summary>
    public sealed class ZeroShotLabelPrompt
    {
        private readonly IReadOnlyList<int> _promptIndexes;

        /// <summary>Initializes a label and one or more prompt indexes. / 初始化标签及一个或多个提示索引。</summary>
        public ZeroShotLabelPrompt(string label, IEnumerable<int> promptIndexes)
        {
            if (string.IsNullOrWhiteSpace(label) || promptIndexes == null) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A zero-shot label and prompt indexes are required.");
            var values = promptIndexes.ToList();
            if (values.Count == 0 || values.Any(value => value < 0) || values.Distinct().Count() != values.Count) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A zero-shot label must own unique non-negative prompt indexes.");
            Label = label.Trim();
            _promptIndexes = new ReadOnlyCollection<int>(values);
        }

        /// <summary>Gets the display label. / 获取显示标签。</summary>
        public string Label { get; }
        /// <summary>Gets owned prompt indexes. / 获取自有提示索引。</summary>
        public IReadOnlyList<int> PromptIndexes => _promptIndexes;
    }

    internal static class VisionLanguageHash
    {
        internal static string Floats(float[] values)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var bytes = new byte[checked(values.Length * sizeof(float))];
                Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
                return string.Concat(sha.ComputeHash(bytes).Select(item => item.ToString("x2")));
            }
        }

        internal static string Text(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2")));
            }
        }

        internal static bool ShapeMatches(TensorShape expected, TensorShape actual)
        {
            if (expected.Rank != actual.Rank) return false;
            for (int index = 0; index < expected.Rank; index++) if (expected[index] >= 0 && expected[index] != actual[index]) return false;
            return true;
        }
    }
}
