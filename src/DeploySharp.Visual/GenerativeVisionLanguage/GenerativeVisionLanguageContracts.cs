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
    /// <summary>Identifies an audited image-conditioned text generation family. / 标识已审计的图像条件文本生成模型族。</summary>
    public enum GenerativeVisionLanguageFamily
    {
        /// <summary>Salesforce BLIP. / Salesforce BLIP 模型族。</summary>
        Blip = 1,
        /// <summary>Salesforce BLIP-2. / Salesforce BLIP-2 模型族。</summary>
        Blip2 = 2,
        /// <summary>Salesforce InstructBLIP. / Salesforce InstructBLIP 模型族。</summary>
        InstructBlip = 3
    }

    /// <summary>Identifies an image-conditioned generation task. / 标识图像条件生成任务。</summary>
    public enum GenerativeVisionLanguageTask
    {
        /// <summary>Generate an image caption. / 生成图像描述。</summary>
        ImageCaptioning = 1,
        /// <summary>Answer a question about the current image. / 回答当前图像的问题。</summary>
        VisualQuestionAnswering = 2,
        /// <summary>Generate text from an image and instruction. / 根据图像与指令生成文本。</summary>
        ConditionalTextGeneration = 3
    }

    /// <summary>Identifies one exact sub-artifact in a generation bundle. / 标识生成 Bundle 中的精确子工件。</summary>
    public enum GenerativeVisionLanguageArtifactRole
    {
        /// <summary>Image vision encoder. / 图像 Vision Encoder。</summary>
        VisionEncoder = 1,
        /// <summary>BLIP-2 or InstructBLIP Q-Former. / BLIP-2 或 InstructBLIP Q-Former。</summary>
        QFormer = 2,
        /// <summary>Learned query-token sidecar. / 可学习 Query Token sidecar。</summary>
        QueryTokens = 3,
        /// <summary>Projection into the language-model embedding space. / 到语言模型 Embedding 空间的投影。</summary>
        LanguageProjection = 4,
        /// <summary>Autoregressive language decoder or encoder-decoder graph. / 自回归语言 Decoder 或 Encoder-Decoder 图。</summary>
        LanguageDecoder = 5,
        /// <summary>Token-ID to language-embedding graph. / Token ID 到语言 Embedding 的图。</summary>
        TokenEmbedding = 6
    }

    /// <summary>Identifies the bound generation algorithm. / 标识绑定的生成算法。</summary>
    public enum GenerativeVisionLanguageGenerationMode
    {
        /// <summary>Deterministic lowest-index argmax on every step. / 每步执行确定性的最小索引 Argmax。</summary>
        Greedy = 1,
        /// <summary>Official beam search; only executable when the profile supplies a supported implementation. / 官方 Beam Search；仅 Profile 提供受支持实现时可执行。</summary>
        BeamSearch = 2,
        /// <summary>Official sampling; only executable when the profile supplies a supported implementation. / 官方采样；仅 Profile 提供受支持实现时可执行。</summary>
        Sampling = 3
    }

    /// <summary>Identifies how decoder history is represented. / 标识 Decoder 历史的表示方式。</summary>
    public enum GenerativeVisionLanguageCacheMode
    {
        /// <summary>Each step submits the complete prefix and no reusable KV state exists. / 每步提交完整前缀且不存在可复用 KV 状态。</summary>
        NoneFullPrefix = 1,
        /// <summary>Exact named past/present KV ports are bound by the profile. / Profile 绑定精确具名 past/present KV 端口。</summary>
        PastPresent = 2
    }

    /// <summary>Identifies whether generation limits count the complete sequence or only newly emitted tokens. / 标识生成长度限制统计完整序列还是仅统计新生成 Token。</summary>
    public enum GenerativeVisionLanguageLengthMode
    {
        /// <summary>Limits include the prompt/prefix tokens. / 限制包含 Prompt/Prefix Token。</summary>
        TotalTokens = 1,
        /// <summary>Limits count only tokens emitted after the prompt. / 限制仅统计 Prompt 之后生成的 Token。</summary>
        NewTokens = 2
    }

    /// <summary>Defines one exact named tensor port and its capacity. / 定义一个精确具名张量端口及容量。</summary>
    public sealed class GenerativeVisionLanguageTensorContract
    {
        /// <summary>Initializes a tensor contract. / 初始化张量合同。</summary>
        public GenerativeVisionLanguageTensorContract(string name, TensorElementType elementType, TensorShape shapePattern, long maximumElements = 50_000_000)
        {
            if (string.IsNullOrWhiteSpace(name) || elementType == TensorElementType.Unknown || elementType == TensorElementType.String || shapePattern == null || maximumElements <= 0) throw Invalid("A named numeric tensor, shape, and positive capacity are required.", name);
            Name = name.Trim();
            ElementType = elementType;
            ShapePattern = new TensorShape(shapePattern.ToArray());
            MaximumElements = maximumElements;
        }

        /// <summary>Gets the exact port name. / 获取精确端口名称。</summary>
        public string Name { get; }
        /// <summary>Gets the element type. / 获取元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the fixed/dynamic shape pattern. / 获取固定/动态 Shape Pattern。</summary>
        public TensorShape ShapePattern { get; }
        /// <summary>Gets the maximum runtime element count. / 获取最大运行时元素数量。</summary>
        public long MaximumElements { get; }

        private static VisualException Invalid(string message, string? tensorName) => new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, message, tensorName: tensorName);
    }

    /// <summary>Binds an inference subgraph to exact named ports and supply-chain identity. / 将推理子图绑定到精确具名端口与供应链 Identity。</summary>
    public sealed class GenerativeVisionLanguageArtifactContract
    {
        private readonly IReadOnlyList<GenerativeVisionLanguageTensorContract> _inputs;
        private readonly IReadOnlyList<GenerativeVisionLanguageTensorContract> _outputs;

        /// <summary>Initializes an immutable subgraph contract. / 初始化不可变子图合同。</summary>
        public GenerativeVisionLanguageArtifactContract(GenerativeVisionLanguageArtifactRole role, ModelId modelId, string format, string sha256, long size, int opset, IEnumerable<GenerativeVisionLanguageTensorContract> inputs, IEnumerable<GenerativeVisionLanguageTensorContract> outputs, string upstreamCommit, string exporter, string license, string sourceUri, string? externalDataSha256 = null)
        {
            if (!Enum.IsDefined(typeof(GenerativeVisionLanguageArtifactRole), role) || modelId.IsEmpty || string.IsNullOrWhiteSpace(format) || !GenerativeVisionLanguageHash.IsSha256(sha256) || size <= 0 || opset <= 0 || string.IsNullOrWhiteSpace(upstreamCommit) || string.IsNullOrWhiteSpace(exporter) || string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(sourceUri) || (externalDataSha256 != null && !GenerativeVisionLanguageHash.IsSha256(externalDataSha256))) throw Invalid("Artifact provenance is incomplete.", modelId);
            Role = role;
            ModelId = modelId;
            Format = format.Trim().ToLowerInvariant();
            Sha256 = sha256.Trim().ToLowerInvariant();
            Size = size;
            Opset = opset;
            _inputs = CopyPorts(inputs, "input");
            _outputs = CopyPorts(outputs, "output");
            UpstreamCommit = upstreamCommit.Trim();
            Exporter = exporter.Trim();
            License = license.Trim();
            SourceUri = sourceUri.Trim();
            ExternalDataSha256 = externalDataSha256?.Trim().ToLowerInvariant();
        }

        /// <summary>Gets the artifact role. / 获取工件角色。</summary>
        public GenerativeVisionLanguageArtifactRole Role { get; }
        /// <summary>Gets the model identifier. / 获取模型标识符。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets the normalized format. / 获取规范化格式。</summary>
        public string Format { get; }
        /// <summary>Gets SHA256. / 获取 SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets byte size. / 获取字节大小。</summary>
        public long Size { get; }
        /// <summary>Gets ONNX opset. / 获取 ONNX Opset。</summary>
        public int Opset { get; }
        /// <summary>Gets exact named inputs. / 获取精确具名输入。</summary>
        public IReadOnlyList<GenerativeVisionLanguageTensorContract> Inputs => _inputs;
        /// <summary>Gets exact named outputs. / 获取精确具名输出。</summary>
        public IReadOnlyList<GenerativeVisionLanguageTensorContract> Outputs => _outputs;
        /// <summary>Gets upstream commit/revision. / 获取上游 Commit/Revision。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets exporter chain. / 获取 Exporter 链。</summary>
        public string Exporter { get; }
        /// <summary>Gets artifact license conclusion. / 获取工件许可证结论。</summary>
        public string License { get; }
        /// <summary>Gets immutable source URI. / 获取不可变来源 URI。</summary>
        public string SourceUri { get; }
        /// <summary>Gets optional external-data SHA256. / 获取可选 external-data SHA256。</summary>
        public string? ExternalDataSha256 { get; }

        private static IReadOnlyList<GenerativeVisionLanguageTensorContract> CopyPorts(IEnumerable<GenerativeVisionLanguageTensorContract> ports, string kind)
        {
            if (ports == null) throw new ArgumentNullException(nameof(ports));
            var result = ports.ToList();
            if (result.Count == 0 || result.Any(value => value == null) || result.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != result.Count) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Artifact " + kind + " ports must be non-empty and uniquely named.");
            return new ReadOnlyCollection<GenerativeVisionLanguageTensorContract>(result);
        }

        private static VisualException Invalid(string message, ModelId modelId) => new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, message, modelId: modelId);
    }

    /// <summary>Binds the official image processor and its exact sidecar identity. / 绑定官方图像 Processor 及精确 sidecar Identity。</summary>
    public sealed class GenerativeVisionLanguageProcessorContract
    {
        private readonly IReadOnlyList<float> _mean;
        private readonly IReadOnlyList<float> _standardDeviation;

        /// <summary>Initializes an immutable fixed-resize RGB processor. / 初始化不可变固定缩放 RGB Processor。</summary>
        public GenerativeVisionLanguageProcessorContract(string processorId, string sha256, VisualSize imageSize, IEnumerable<float> mean, IEnumerable<float> standardDeviation, string interpolation, string implementation, int maximumImageBytes = 64 * 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(processorId) || !GenerativeVisionLanguageHash.IsSha256(sha256) || imageSize.Width <= 0 || imageSize.Height <= 0 || string.IsNullOrWhiteSpace(interpolation) || string.IsNullOrWhiteSpace(implementation) || maximumImageBytes <= 0) throw Invalid("Processor provenance or capacity is invalid.");
            ProcessorId = processorId.Trim();
            Sha256 = sha256.Trim().ToLowerInvariant();
            ImageSize = imageSize;
            _mean = CopyFinite(mean, false);
            _standardDeviation = CopyFinite(standardDeviation, true);
            Interpolation = interpolation.Trim();
            Implementation = implementation.Trim();
            MaximumImageBytes = maximumImageBytes;
        }

        /// <summary>Gets processor ID. / 获取 Processor ID。</summary>
        public string ProcessorId { get; }
        /// <summary>Gets processor/config SHA256. / 获取 Processor/配置 SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets fixed RGB image size. / 获取固定 RGB 图像尺寸。</summary>
        public VisualSize ImageSize { get; }
        /// <summary>Gets RGB means in the 0..255 domain. / 获取 0..255 域 RGB 均值。</summary>
        public IReadOnlyList<float> Mean => _mean;
        /// <summary>Gets RGB standard deviations in the 0..255 domain. / 获取 0..255 域 RGB 标准差。</summary>
        public IReadOnlyList<float> StandardDeviation => _standardDeviation;
        /// <summary>Gets interpolation identity. / 获取插值 Identity。</summary>
        public string Interpolation { get; }
        /// <summary>Gets official implementation identity. / 获取官方实现 Identity。</summary>
        public string Implementation { get; }
        /// <summary>Gets maximum encoded-image bytes. / 获取最大编码图像字节数。</summary>
        public int MaximumImageBytes { get; }

        private static IReadOnlyList<float> CopyFinite(IEnumerable<float> values, bool positive)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var result = values.ToList();
            if (result.Count != 3 || result.Any(value => float.IsNaN(value) || float.IsInfinity(value) || (positive && value <= 0))) throw Invalid("Processor normalization must contain three finite RGB values and positive divisors.");
            return new ReadOnlyCollection<float>(result);
        }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, message);
    }

    /// <summary>Binds an official tokenizer vocabulary and special-token contract. / 绑定官方 Tokenizer 词表与特殊 Token 合同。</summary>
    public sealed class GenerativeVisionLanguageTokenizerContract
    {
        /// <summary>Initializes tokenizer identity and capacity. / 初始化 Tokenizer Identity 与容量。</summary>
        public GenerativeVisionLanguageTokenizerContract(string tokenizerId, string sha256, string tokenizerClass, int vocabularySize, int bosTokenId, int eosTokenId, int padTokenId, int classificationTokenId, int maximumPromptTokens, string normalization)
        {
            if (string.IsNullOrWhiteSpace(tokenizerId) || !GenerativeVisionLanguageHash.IsSha256(sha256) || string.IsNullOrWhiteSpace(tokenizerClass) || vocabularySize <= 0 || bosTokenId < 0 || eosTokenId < 0 || padTokenId < 0 || classificationTokenId < 0 || maximumPromptTokens <= 0 || string.IsNullOrWhiteSpace(normalization) || new[] { bosTokenId, eosTokenId, padTokenId, classificationTokenId }.Any(value => value >= vocabularySize)) throw Invalid("Tokenizer provenance, special tokens, or capacity are invalid.");
            TokenizerId = tokenizerId.Trim();
            Sha256 = sha256.Trim().ToLowerInvariant();
            TokenizerClass = tokenizerClass.Trim();
            VocabularySize = vocabularySize;
            BosTokenId = bosTokenId;
            EosTokenId = eosTokenId;
            PadTokenId = padTokenId;
            ClassificationTokenId = classificationTokenId;
            MaximumPromptTokens = maximumPromptTokens;
            Normalization = normalization.Trim();
            IsComplete = true;
        }

        /// <summary>Initializes an explicitly incomplete external tokenizer blocker without inventing vocabulary or token IDs. / 初始化显式不完整的 External Tokenizer blocker，不虚构词表或 Token ID。</summary>
        public GenerativeVisionLanguageTokenizerContract(string tokenizerId, string contractSha256, string tokenizerClass, string blocker)
        {
            if (string.IsNullOrWhiteSpace(tokenizerId) || !GenerativeVisionLanguageHash.IsSha256(contractSha256) || string.IsNullOrWhiteSpace(tokenizerClass) || string.IsNullOrWhiteSpace(blocker)) throw Invalid("Incomplete tokenizer provenance and blocker are required.");
            TokenizerId = tokenizerId.Trim();
            Sha256 = contractSha256.Trim().ToLowerInvariant();
            TokenizerClass = tokenizerClass.Trim();
            VocabularySize = 0;
            BosTokenId = -1;
            EosTokenId = -1;
            PadTokenId = -1;
            ClassificationTokenId = -1;
            MaximumPromptTokens = 0;
            Normalization = "unresolved-external-blocker";
            IsComplete = false;
            Blocker = blocker.Trim();
        }

        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets vocabulary/config SHA256. / 获取词表/配置 SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets official tokenizer class. / 获取官方 Tokenizer 类。</summary>
        public string TokenizerClass { get; }
        /// <summary>Gets total decoder vocabulary size, including added special tokens. / 获取包含新增特殊 Token 的 Decoder 总词表大小。</summary>
        public int VocabularySize { get; }
        /// <summary>Gets BOS/DEC token ID. / 获取 BOS/DEC Token ID。</summary>
        public int BosTokenId { get; }
        /// <summary>Gets EOS token ID. / 获取 EOS Token ID。</summary>
        public int EosTokenId { get; }
        /// <summary>Gets PAD token ID. / 获取 PAD Token ID。</summary>
        public int PadTokenId { get; }
        /// <summary>Gets base tokenizer classification token ID. / 获取基础 Tokenizer Classification Token ID。</summary>
        public int ClassificationTokenId { get; }
        /// <summary>Gets maximum prompt tokens. / 获取最大 Prompt Token 数。</summary>
        public int MaximumPromptTokens { get; }
        /// <summary>Gets Unicode/case normalization identity. / 获取 Unicode/大小写规范化 Identity。</summary>
        public string Normalization { get; }
        /// <summary>Gets whether exact vocabulary, special-token IDs, and capacity are available for execution. / 获取是否具备用于执行的精确词表、特殊 Token ID 与容量。</summary>
        public bool IsComplete { get; }
        /// <summary>Gets the reproducible tokenizer blocker when the exact asset contract is incomplete. / 获取精确资产合同不完整时的可复现 Tokenizer blocker。</summary>
        public string? Blocker { get; }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, message);
    }

    /// <summary>Binds stopping, selection, length, and KV-cache semantics. / 绑定停止、选择、长度与 KV-cache 语义。</summary>
    public sealed class GenerativeVisionLanguageGenerationContract
    {
        /// <summary>Initializes a deterministic generation contract. / 初始化确定性生成合同。</summary>
        public GenerativeVisionLanguageGenerationContract(string configId, string sha256, GenerativeVisionLanguageGenerationMode mode, GenerativeVisionLanguageCacheMode cacheMode, int minimumTotalTokens, int maximumTotalTokens, int numberOfBeams = 1, float temperature = 1f, float topP = 1f, float repetitionPenalty = 1f, GenerativeVisionLanguageLengthMode lengthMode = GenerativeVisionLanguageLengthMode.TotalTokens)
        {
            if (string.IsNullOrWhiteSpace(configId) || !GenerativeVisionLanguageHash.IsSha256(sha256) || !Enum.IsDefined(typeof(GenerativeVisionLanguageGenerationMode), mode) || !Enum.IsDefined(typeof(GenerativeVisionLanguageCacheMode), cacheMode) || !Enum.IsDefined(typeof(GenerativeVisionLanguageLengthMode), lengthMode) || minimumTotalTokens <= 0 || maximumTotalTokens < minimumTotalTokens || numberOfBeams <= 0 || !FinitePositive(temperature) || !FinitePositive(topP) || topP > 1 || !FinitePositive(repetitionPenalty)) throw Invalid("Generation configuration is invalid.");
            if (mode == GenerativeVisionLanguageGenerationMode.Greedy && numberOfBeams != 1) throw Invalid("Greedy generation requires exactly one beam.");
            ConfigId = configId.Trim();
            Sha256 = sha256.Trim().ToLowerInvariant();
            Mode = mode;
            CacheMode = cacheMode;
            MinimumTotalTokens = minimumTotalTokens;
            MaximumTotalTokens = maximumTotalTokens;
            NumberOfBeams = numberOfBeams;
            Temperature = temperature;
            TopP = topP;
            RepetitionPenalty = repetitionPenalty;
            LengthMode = lengthMode;
            Identity = GenerativeVisionLanguageHash.Text(string.Join("|", ConfigId, Sha256, mode, cacheMode, lengthMode, minimumTotalTokens, maximumTotalTokens, numberOfBeams, temperature.ToString("R", System.Globalization.CultureInfo.InvariantCulture), topP.ToString("R", System.Globalization.CultureInfo.InvariantCulture), repetitionPenalty.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        }

        /// <summary>Gets config sidecar ID. / 获取配置 sidecar ID。</summary>
        public string ConfigId { get; }
        /// <summary>Gets config sidecar SHA256. / 获取配置 sidecar SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets generation mode. / 获取生成模式。</summary>
        public GenerativeVisionLanguageGenerationMode Mode { get; }
        /// <summary>Gets KV-cache mode. / 获取 KV-cache 模式。</summary>
        public GenerativeVisionLanguageCacheMode CacheMode { get; }
        /// <summary>Gets minimum total sequence length, including prompt. / 获取包含 Prompt 的最小总序列长度。</summary>
        public int MinimumTotalTokens { get; }
        /// <summary>Gets maximum total sequence length, including prompt. / 获取包含 Prompt 的最大总序列长度。</summary>
        public int MaximumTotalTokens { get; }
        /// <summary>Gets beam count. / 获取 Beam 数量。</summary>
        public int NumberOfBeams { get; }
        /// <summary>Gets temperature. / 获取 Temperature。</summary>
        public float Temperature { get; }
        /// <summary>Gets top-p. / 获取 Top-p。</summary>
        public float TopP { get; }
        /// <summary>Gets repetition penalty. / 获取重复惩罚。</summary>
        public float RepetitionPenalty { get; }
        /// <summary>Gets whether the length bounds count total or newly generated tokens. / 获取长度边界统计总 Token 还是新生成 Token。</summary>
        public GenerativeVisionLanguageLengthMode LengthMode { get; }
        /// <summary>Gets stable generation-config identity. / 获取稳定 Generation Config Identity。</summary>
        public string Identity { get; }

        private static bool FinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, message);
    }

    /// <summary>Defines an immutable, artifact-bound image-conditioned generation profile. / 定义不可变、工件绑定的图像条件生成 Profile。</summary>
    public sealed class GenerativeVisionLanguageProfile
    {
        private readonly IReadOnlyList<GenerativeVisionLanguageArtifactContract> _artifacts;

        /// <summary>Initializes a complete executable profile or an explicit external blocker. / 初始化完整可执行 Profile 或显式 External blocker。</summary>
        public GenerativeVisionLanguageProfile(string profileId, GenerativeVisionLanguageFamily family, string variant, GenerativeVisionLanguageTask task, GenerativeVisionLanguageProcessorContract processor, GenerativeVisionLanguageTokenizerContract tokenizer, GenerativeVisionLanguageGenerationContract generation, string promptTemplate, IEnumerable<GenerativeVisionLanguageArtifactContract> artifacts, string version, bool executable, string? blocker = null, int maximumRequestCharacters = 4096)
        {
            ProfileId = VisualGuard.Identifier(profileId, nameof(profileId));
            if (!Enum.IsDefined(typeof(GenerativeVisionLanguageFamily), family) || !Enum.IsDefined(typeof(GenerativeVisionLanguageTask), task) || string.IsNullOrWhiteSpace(variant) || processor == null || tokenizer == null || generation == null || promptTemplate == null || artifacts == null || string.IsNullOrWhiteSpace(version) || maximumRequestCharacters <= 0) throw Invalid("Profile identity, contracts, or capacity are invalid.", ProfileId);
            Family = family;
            Variant = variant.Trim();
            Task = task;
            Processor = processor;
            Tokenizer = tokenizer;
            Generation = generation;
            PromptTemplate = promptTemplate;
            var list = artifacts.ToList();
            if (list.Any(value => value == null) || list.Select(value => value.Role).Distinct().Count() != list.Count) throw Invalid("Artifact roles must be unique.", ProfileId);
            if (executable && (!list.Any(value => value.Role == GenerativeVisionLanguageArtifactRole.VisionEncoder) || !list.Any(value => value.Role == GenerativeVisionLanguageArtifactRole.LanguageDecoder))) throw Invalid("Executable profiles require exact vision-encoder and language-decoder artifacts.", ProfileId);
            if (executable && !tokenizer.IsComplete) throw Invalid("Executable profiles require a complete tokenizer contract.", ProfileId);
            if (executable && generation.Mode != GenerativeVisionLanguageGenerationMode.Greedy) throw Invalid("The current managed loop executes only a profile-bound greedy policy.", ProfileId);
            if (executable && generation.CacheMode != GenerativeVisionLanguageCacheMode.NoneFullPrefix) throw Invalid("The current managed loop requires a full-prefix no-KV decoder contract.", ProfileId);
            if (executable && generation.LengthMode != GenerativeVisionLanguageLengthMode.TotalTokens) throw Invalid("The current managed loop requires total-token length semantics.", ProfileId);
            if (executable && (generation.NumberOfBeams != 1 || generation.Temperature != 1f || generation.TopP != 1f || generation.RepetitionPenalty != 1f)) throw Invalid("The current managed loop requires one beam, unit temperature/top-p, and unit repetition penalty.", ProfileId);
            _artifacts = new ReadOnlyCollection<GenerativeVisionLanguageArtifactContract>(list);
            Version = version.Trim();
            Executable = executable;
            string normalizedBlocker = blocker ?? string.Empty;
            Blocker = string.IsNullOrWhiteSpace(normalizedBlocker) ? null : normalizedBlocker.Trim();
            if (!executable && Blocker == null) throw Invalid("Non-executable profiles require a reproducible blocker.", ProfileId);
            MaximumRequestCharacters = maximumRequestCharacters;
            ArtifactIdentity = GenerativeVisionLanguageHash.Text(string.Join("|", _artifacts.OrderBy(value => value.Role).Select(value => value.Role + ":" + value.ModelId.Value + ":" + value.Sha256).Concat(new[] { processor.Sha256, tokenizer.Sha256, generation.Identity })));
        }

        /// <summary>Gets stable profile ID. / 获取稳定 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets family. / 获取模型族。</summary>
        public GenerativeVisionLanguageFamily Family { get; }
        /// <summary>Gets exact variant. / 获取精确变体。</summary>
        public string Variant { get; }
        /// <summary>Gets task. / 获取任务。</summary>
        public GenerativeVisionLanguageTask Task { get; }
        /// <summary>Gets processor contract. / 获取 Processor 合同。</summary>
        public GenerativeVisionLanguageProcessorContract Processor { get; }
        /// <summary>Gets tokenizer contract. / 获取 Tokenizer 合同。</summary>
        public GenerativeVisionLanguageTokenizerContract Tokenizer { get; }
        /// <summary>Gets generation/KV contract. / 获取生成/KV 合同。</summary>
        public GenerativeVisionLanguageGenerationContract Generation { get; }
        /// <summary>Gets exact prompt template. / 获取精确 Prompt Template。</summary>
        public string PromptTemplate { get; }
        /// <summary>Gets artifact contracts. / 获取工件合同。</summary>
        public IReadOnlyList<GenerativeVisionLanguageArtifactContract> Artifacts => _artifacts;
        /// <summary>Gets profile version. / 获取 Profile 版本。</summary>
        public string Version { get; }
        /// <summary>Gets whether the complete native pipeline is executable. / 获取完整 Native Pipeline 是否可执行。</summary>
        public bool Executable { get; }
        /// <summary>Gets blocker when execution is unavailable. / 获取执行不可用时的 blocker。</summary>
        public string? Blocker { get; }
        /// <summary>Gets maximum request characters. / 获取最大请求字符数。</summary>
        public int MaximumRequestCharacters { get; }
        /// <summary>Gets identity over every graph and processor/tokenizer/generation sidecar. / 获取覆盖全部图与 Processor/Tokenizer/Generation sidecar 的 Identity。</summary>
        public string ArtifactIdentity { get; }

        /// <summary>Gets one exact artifact contract. / 获取一个精确工件合同。</summary>
        public GenerativeVisionLanguageArtifactContract GetArtifact(GenerativeVisionLanguageArtifactRole role)
        {
            if (!Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, Blocker ?? "The profile is not executable.", profileId: ProfileId);
            return _artifacts.SingleOrDefault(value => value.Role == role) ?? throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "The requested artifact role is absent.", profileId: ProfileId);
        }

        /// <summary>Creates a concrete model artifact after the application locates an external file. / 应用定位外部文件后创建具体 ModelArtifact。</summary>
        public ModelArtifact CreateArtifact(GenerativeVisionLanguageArtifactRole role, string path, BackendId backendId) => new ModelArtifact(GetArtifact(role).ModelId, GetArtifact(role).Format, path, GetArtifact(role).Sha256, backendId);

        private static VisualException Invalid(string message, string profileId) => new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, message, profileId: profileId);
    }

    /// <summary>Associates one role with a concrete external backend artifact. / 将一个角色与具体外部 Backend 工件关联。</summary>
    public sealed class GenerativeVisionLanguageArtifactBinding
    {
        /// <summary>Initializes a role binding. / 初始化角色绑定。</summary>
        public GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole role, ModelArtifact artifact) { Role = role; Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact)); }
        /// <summary>Gets role. / 获取角色。</summary>
        public GenerativeVisionLanguageArtifactRole Role { get; }
        /// <summary>Gets concrete artifact. / 获取具体工件。</summary>
        public ModelArtifact Artifact { get; }
    }

    /// <summary>Validates and owns concrete paths for every inference subgraph in one profile. / 校验并拥有一个 Profile 中全部推理子图的具体路径。</summary>
    public sealed class GenerativeVisionLanguageArtifactBundle
    {
        private readonly IReadOnlyDictionary<GenerativeVisionLanguageArtifactRole, ModelArtifact> _artifacts;

        /// <summary>Initializes a complete exact-role bundle. / 初始化完整精确角色 Bundle。</summary>
        public GenerativeVisionLanguageArtifactBundle(GenerativeVisionLanguageProfile profile, IEnumerable<GenerativeVisionLanguageArtifactBinding> bindings)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (!profile.Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, profile.Blocker ?? "The profile is not executable.", profileId: profile.ProfileId);
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            var dictionary = new Dictionary<GenerativeVisionLanguageArtifactRole, ModelArtifact>();
            foreach (GenerativeVisionLanguageArtifactBinding binding in bindings)
            {
                if (binding == null) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Artifact bindings must be non-null and unique.", profileId: profile.ProfileId);
                if (dictionary.ContainsKey(binding.Role)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "Artifact bindings must be non-null and unique.", profileId: profile.ProfileId);
                dictionary.Add(binding.Role, binding.Artifact);
                GenerativeVisionLanguageArtifactContract expected = profile.GetArtifact(binding.Role);
                if (expected.ModelId != binding.Artifact.ModelId || !string.Equals(expected.Format, binding.Artifact.Format, StringComparison.OrdinalIgnoreCase) || !string.Equals(expected.Sha256, binding.Artifact.Sha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, "A concrete artifact differs from the profile-bound ID, format, or SHA256.", profileId: profile.ProfileId, modelId: expected.ModelId);
            }
            if (dictionary.Count != profile.Artifacts.Count || profile.Artifacts.Any(value => !dictionary.ContainsKey(value.Role))) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "The concrete bundle is incomplete or contains a mixed-version subgraph.", profileId: profile.ProfileId);
            _artifacts = new ReadOnlyDictionary<GenerativeVisionLanguageArtifactRole, ModelArtifact>(dictionary);
        }

        /// <summary>Gets profile. / 获取 Profile。</summary>
        public GenerativeVisionLanguageProfile Profile { get; }
        /// <summary>Gets one concrete artifact by role. / 按角色获取具体工件。</summary>
        public ModelArtifact GetArtifact(GenerativeVisionLanguageArtifactRole role) => _artifacts.TryGetValue(role, out ModelArtifact? artifact) ? artifact : throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "The concrete artifact role is missing.", profileId: Profile.ProfileId);
    }

    /// <summary>Describes one caption, question, or instruction without mutating shared image state. / 描述一次 Caption、问题或指令且不修改共享图像状态。</summary>
    public sealed class GenerativeVisionLanguageRequest
    {
        /// <summary>Initializes a task-bound request. / 初始化任务绑定请求。</summary>
        public GenerativeVisionLanguageRequest(GenerativeVisionLanguageTask task, string? text = null)
        {
            if (!Enum.IsDefined(typeof(GenerativeVisionLanguageTask), task)) throw new ArgumentOutOfRangeException(nameof(task));
            string value = text ?? string.Empty;
            if (task != GenerativeVisionLanguageTask.ImageCaptioning && string.IsNullOrWhiteSpace(value)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "VQA and conditional generation require non-empty text.");
            Task = task;
            Text = value;
        }

        /// <summary>Gets task. / 获取任务。</summary>
        public GenerativeVisionLanguageTask Task { get; }
        /// <summary>Gets exact original question/instruction; Caption uses an empty string. / 获取精确原始问题/指令；Caption 使用空字符串。</summary>
        public string Text { get; }
        /// <summary>Creates a Caption request. / 创建 Caption 请求。</summary>
        public static GenerativeVisionLanguageRequest Caption() => new GenerativeVisionLanguageRequest(GenerativeVisionLanguageTask.ImageCaptioning);
        /// <summary>Creates a VQA request. / 创建 VQA 请求。</summary>
        public static GenerativeVisionLanguageRequest Question(string question) => new GenerativeVisionLanguageRequest(GenerativeVisionLanguageTask.VisualQuestionAnswering, question);
    }

    /// <summary>Owns one validated prompt prefix and tokenizer identity. / 拥有一个已校验 Prompt 前缀与 Tokenizer Identity。</summary>
    public sealed class GenerativeTokenSequence
    {
        private readonly long[] _tokenIds;

        /// <summary>Initializes a defensive token prefix. / 初始化防御性 Token 前缀。</summary>
        public GenerativeTokenSequence(string normalizedPrompt, long[] tokenIds, string tokenizerId, string tokenizerSha256)
        {
            if (normalizedPrompt == null || tokenIds == null || tokenIds.Length == 0 || tokenIds.Any(value => value < 0) || string.IsNullOrWhiteSpace(tokenizerId) || !GenerativeVisionLanguageHash.IsSha256(tokenizerSha256)) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageTokenizerInvalid, "Prompt tokens or tokenizer identity are invalid.");
            NormalizedPrompt = normalizedPrompt;
            _tokenIds = (long[])tokenIds.Clone();
            TokenizerId = tokenizerId.Trim();
            TokenizerSha256 = tokenizerSha256.Trim().ToLowerInvariant();
            ContentSha256 = GenerativeVisionLanguageHash.Text(NormalizedPrompt + "|" + string.Join(",", _tokenIds));
        }

        /// <summary>Gets normalized/template-applied prompt. / 获取规范化/应用模板后的 Prompt。</summary>
        public string NormalizedPrompt { get; }
        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets tokenizer SHA256. / 获取 Tokenizer SHA256。</summary>
        public string TokenizerSha256 { get; }
        /// <summary>Gets prompt content SHA256. / 获取 Prompt 内容 SHA256。</summary>
        public string ContentSha256 { get; }
        /// <summary>Gets token count. / 获取 Token 数量。</summary>
        public int Count => _tokenIds.Length;
        /// <summary>Gets a defensive copy of token IDs. / 获取 Token ID 防御性副本。</summary>
        public long[] CopyTokenIds() => (long[])_tokenIds.Clone();
    }

    /// <summary>Defines a profile-verified tokenizer implementation; implementations remain caller-owned. / 定义经 Profile 校验的 Tokenizer 实现；实现仍由调用方拥有。</summary>
    public interface IGenerativeVisionLanguageTokenizer
    {
        /// <summary>Gets exact tokenizer ID. / 获取精确 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets exact tokenizer sidecar SHA256. / 获取精确 Tokenizer sidecar SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Applies the profile template and returns an owned prefix. / 应用 Profile 模板并返回自有前缀。</summary>
        public GenerativeTokenSequence EncodePrefix(GenerativeVisionLanguageProfile profile, GenerativeVisionLanguageRequest request);
        /// <summary>Decodes generated completion IDs without prompt/special tokens. / 解码不含 Prompt/特殊 Token 的生成 Completion ID。</summary>
        public string DecodeCompletion(IEnumerable<int> tokenIds);
    }

    internal static class GenerativeVisionLanguageHash
    {
        internal static bool IsSha256(string? value) => value != null && value.Length == 64 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') || (character >= 'A' && character <= 'F'));

        internal static string Text(string value)
        {
            using (SHA256 sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2")));
        }

        internal static bool ShapeMatches(TensorShape expected, TensorShape actual)
        {
            if (expected.Rank != actual.Rank) return false;
            for (int index = 0; index < expected.Rank; index++) if (expected[index] >= 0 && expected[index] != actual[index]) return false;
            return true;
        }
    }
}
