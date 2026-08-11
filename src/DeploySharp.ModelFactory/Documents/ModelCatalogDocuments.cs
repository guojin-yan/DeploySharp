using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.ModelPack.Json;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Identifies the publication maturity of a catalog entry. / 标识目录条目的发布成熟度。</summary>
    public enum ModelCatalogStatus
    {
        /// <summary>The model passed all supported-artifact admission checks. / 模型通过全部受支持工件准入检查。</summary>
        Supported = 0,
        /// <summary>The model is available for opt-in evaluation but is not a stable promise. / 模型可用于选择性评估，但不是稳定承诺。</summary>
        Preview = 1,
        /// <summary>The model is recorded for provenance or future work and is not downloadable as supported. / 模型仅用于来源记录或未来工作，不作为受支持模型下载。</summary>
        External = 2
    }

    /// <summary>Identifies the purpose of one catalog asset. / 标识目录资产的用途。</summary>
    public enum ModelCatalogAssetKind
    {
        /// <summary>A ModelPack JSON manifest. / ModelPack JSON 清单。</summary>
        Manifest = 0,
        /// <summary>A model or model sidecar file. / 模型或模型附属文件。</summary>
        Model = 1,
        /// <summary>A test input image or binary fixture. / 测试输入图片或二进制夹具。</summary>
        TestInput = 2,
        /// <summary>A golden expected-result fixture. / 黄金预期结果夹具。</summary>
        TestExpected = 3,
        /// <summary>A license or attribution file. / 许可证或归属文件。</summary>
        License = 4,
        /// <summary>Another non-executable catalog asset. / 其他非可执行目录资产。</summary>
        Other = 5
    }

    /// <summary>Describes the immutable GitHub Release that owns an entry. / 描述拥有目录条目的不可变 GitHub Release。</summary>
    public sealed class ModelCatalogRelease
    {
        /// <summary>Initializes release provenance. / 初始化 Release 来源信息。</summary>
        public ModelCatalogRelease(string? owner, string? repository, string? tag, string? commit)
        {
            Owner = owner;
            Repository = repository;
            Tag = tag;
            Commit = commit;
        }

        /// <summary>Gets the GitHub owner or organization. / 获取 GitHub 所有者或组织。</summary>
        public string? Owner { get; }
        /// <summary>Gets the GitHub repository name. / 获取 GitHub 仓库名称。</summary>
        public string? Repository { get; }
        /// <summary>Gets the immutable release tag. / 获取不可变 Release 标签。</summary>
        public string? Tag { get; }
        /// <summary>Gets the commit recorded when the release was created. / 获取创建 Release 时记录的提交。</summary>
        public string? Commit { get; }
    }

    /// <summary>Describes an auditable model conversion record. / 描述可审计的模型转换记录。</summary>
    public sealed class ModelCatalogConversion
    {
        /// <summary>Initializes conversion provenance. / 初始化转换来源信息。</summary>
        public ModelCatalogConversion(string? exporter, string? exporterVersion, string? sourceRevision, string? notes)
        {
            Exporter = exporter;
            ExporterVersion = exporterVersion;
            SourceRevision = sourceRevision;
            Notes = notes;
        }

        /// <summary>Gets the exporter name. / 获取导出器名称。</summary>
        public string? Exporter { get; }
        /// <summary>Gets the exporter version. / 获取导出器版本。</summary>
        public string? ExporterVersion { get; }
        /// <summary>Gets the source revision used for conversion. / 获取转换使用的源代码修订。</summary>
        public string? SourceRevision { get; }
        /// <summary>Gets reproducibility notes. / 获取可复现性说明。</summary>
        public string? Notes { get; }
    }

    /// <summary>Describes one downloadable, integrity-protected catalog asset. / 描述一个可下载且受完整性保护的目录资产。</summary>
    public sealed class ModelCatalogAsset
    {
        private readonly string? _cacheKey;

        /// <summary>Initializes an asset document. / 初始化资产文档。</summary>
        public ModelCatalogAsset(
            string? assetId,
            ModelCatalogAssetKind kind,
            string? releaseTag,
            Uri? downloadUri,
            string? relativePath,
            long size,
            string? sha256,
            string? mediaType,
            string? licenseExpression,
            string? cacheKey = null)
        {
            AssetId = assetId;
            Kind = kind;
            ReleaseTag = releaseTag;
            DownloadUri = downloadUri;
            RelativePath = relativePath;
            Size = size;
            Sha256 = sha256;
            MediaType = mediaType;
            LicenseExpression = licenseExpression;
            _cacheKey = cacheKey;
        }

        /// <summary>Gets the stable asset identifier. / 获取稳定资产标识。</summary>
        public string? AssetId { get; }
        /// <summary>Gets the asset purpose. / 获取资产用途。</summary>
        public ModelCatalogAssetKind Kind { get; }
        /// <summary>Gets the immutable release tag. / 获取不可变 Release 标签。</summary>
        public string? ReleaseTag { get; }
        /// <summary>Gets the immutable release download URI. / 获取不可变 Release 下载 URI。</summary>
        public Uri? DownloadUri { get; }
        /// <summary>Gets the safe path used inside a materialized package or cache entry. / 获取在物化模型包或缓存条目中使用的安全路径。</summary>
        public string? RelativePath { get; }
        /// <summary>Gets the expected byte size. / 获取预期字节大小。</summary>
        public long Size { get; }
        /// <summary>Gets the normalized lowercase SHA256. / 获取规范化的小写 SHA256。</summary>
        public string? Sha256 { get; }
        /// <summary>Gets the optional media type. / 获取可选媒体类型。</summary>
        public string? MediaType { get; }
        /// <summary>Gets the SPDX license expression applying to the asset. / 获取适用于资产的 SPDX 许可证表达式。</summary>
        public string? LicenseExpression { get; }
        /// <summary>Gets the runtime cache key, or null until a catalog is validated. / 获取运行时缓存键；目录验证前为 null。</summary>
        public string? CacheKey => _cacheKey;
    }

    /// <summary>Describes one backend-specific model artifact and its release assets. / 描述一个后端特定模型工件及其 Release 资产。</summary>
    public sealed class ModelCatalogArtifact
    {
        private readonly IReadOnlyList<string> _compatibleBackends;
        private readonly IReadOnlyList<ModelCatalogAsset> _assets;
        private readonly IReadOnlyList<string> _capabilities;
        private readonly IReadOnlyList<string> _requiredAssetIds;

        /// <summary>Initializes an artifact document. / 初始化工件文档。</summary>
        public ModelCatalogArtifact(
            string? artifactId,
            string? format,
            IEnumerable<string>? compatibleBackends,
            string? precision,
            string? quantization,
            bool portable,
            string? manifestAssetId,
            IEnumerable<ModelCatalogAsset>? assets,
            ModelCatalogConversion? conversion = null,
            string? bundleRole = null,
            string? bundleVersion = null,
            IEnumerable<string>? capabilities = null,
            IEnumerable<string>? requiredAssetIds = null,
            string? tokenizerId = null,
            string? vocabularyMode = null,
            int? embeddingDimension = null,
            string? imagePreprocessingId = null,
            string? projectionId = null,
            string? normalizationId = null,
            string? scoreSemantics = null,
            string? language = null,
            string? resolution = null,
            string? visionBackbone = null,
            string? qFormerId = null,
            string? languageModelId = null,
            string? promptTemplateId = null,
            string? generationConfigId = null,
            string? generationMode = null,
            string? kvCacheSchemaId = null,
            int? imageCount = null,
            int? contextLength = null,
            int? pageCount = null,
            string? schemaId = null,
            string? ocrOwnership = null,
            string? processorId = null,
            int? sampleRate = null,
            int? channelCount = null,
            string? timestampMode = null,
            string? speakerMode = null,
            string? audioFeatureId = null,
            string? vadId = null,
            string? speakerId = null)
        {
            ArtifactId = artifactId;
            Format = format;
            _compatibleBackends = CopyValues(compatibleBackends);
            Precision = precision;
            Quantization = quantization;
            Portable = portable;
            ManifestAssetId = manifestAssetId;
            _assets = CopyObjects(assets);
            Conversion = conversion;
            BundleRole = bundleRole;
            BundleVersion = bundleVersion;
            _capabilities = CopyValues(capabilities);
            _requiredAssetIds = CopyValues(requiredAssetIds);
            var normalizedTokenizerId = tokenizerId?.Trim();
            var normalizedVocabularyMode = vocabularyMode?.Trim();
            TokenizerId = string.IsNullOrEmpty(normalizedTokenizerId) ? null : normalizedTokenizerId;
            VocabularyMode = string.IsNullOrEmpty(normalizedVocabularyMode) ? null : normalizedVocabularyMode;
            EmbeddingDimension = embeddingDimension;
            ImagePreprocessingId = NormalizeOptional(imagePreprocessingId);
            ProjectionId = NormalizeOptional(projectionId);
            NormalizationId = NormalizeOptional(normalizationId);
            ScoreSemantics = NormalizeOptional(scoreSemantics);
            Language = NormalizeOptional(language);
            Resolution = NormalizeOptional(resolution);
            VisionBackbone = NormalizeOptional(visionBackbone);
            QFormerId = NormalizeOptional(qFormerId);
            LanguageModelId = NormalizeOptional(languageModelId);
            PromptTemplateId = NormalizeOptional(promptTemplateId);
            GenerationConfigId = NormalizeOptional(generationConfigId);
            GenerationMode = NormalizeOptional(generationMode);
            KvCacheSchemaId = NormalizeOptional(kvCacheSchemaId);
            ImageCount = imageCount;
            ContextLength = contextLength;
            PageCount = pageCount;
            SchemaId = NormalizeOptional(schemaId);
            OcrOwnership = NormalizeOptional(ocrOwnership);
            ProcessorId = NormalizeOptional(processorId);
            SampleRate = sampleRate;
            ChannelCount = channelCount;
            TimestampMode = NormalizeOptional(timestampMode);
            SpeakerMode = NormalizeOptional(speakerMode);
            AudioFeatureId = NormalizeOptional(audioFeatureId);
            VadId = NormalizeOptional(vadId);
            SpeakerId = NormalizeOptional(speakerId);
        }

        /// <summary>Gets the stable artifact identifier. / 获取稳定工件标识。</summary>
        public string? ArtifactId { get; }
        /// <summary>Gets the normalized model format. / 获取规范化模型格式。</summary>
        public string? Format { get; }
        /// <summary>Gets compatible backend identifiers. / 获取兼容后端标识。</summary>
        public IReadOnlyList<string> CompatibleBackends => _compatibleBackends;
        /// <summary>Gets precision metadata. / 获取精度元数据。</summary>
        public string? Precision { get; }
        /// <summary>Gets quantization metadata. / 获取量化元数据。</summary>
        public string? Quantization { get; }
        /// <summary>Gets whether the artifact is intended to be portable. / 获取工件是否设计为可移植。</summary>
        public bool Portable { get; }
        /// <summary>Gets the asset identifier of the ModelPack manifest. / 获取 ModelPack 清单资产标识。</summary>
        public string? ManifestAssetId { get; }
        /// <summary>Gets all release assets required by the artifact. / 获取工件所需的全部 Release 资产。</summary>
        public IReadOnlyList<ModelCatalogAsset> Assets => _assets;
        /// <summary>Gets the optional reproducible conversion record. / 获取可选的可复现转换记录。</summary>
        public ModelCatalogConversion? Conversion { get; }
        /// <summary>Gets the role of this artifact inside a multi-artifact bundle. / 获取此工件在多工件 bundle 中的角色。</summary>
        public string? BundleRole { get; }
        /// <summary>Gets the exact bundle version shared by all cooperating artifacts. / 获取协作工件共享的精确 bundle 版本。</summary>
        public string? BundleVersion { get; }
        /// <summary>Gets prompt or task capabilities supplied by the artifact. / 获取此工件提供的提示或任务能力。</summary>
        public IReadOnlyList<string> Capabilities => _capabilities;
        /// <summary>Gets asset identifiers that must accompany this artifact. / 获取必须随此工件提供的资产标识。</summary>
        public IReadOnlyList<string> RequiredAssetIds => _requiredAssetIds;
        /// <summary>Gets the exact tokenizer identity used by the prompt contract. / 获取提示合同使用的精确 Tokenizer Identity。</summary>
        public string? TokenizerId { get; }
        /// <summary>Gets the serialized vocabulary mode, such as fixed or runtime-text. / 获取序列化词汇模式，例如 fixed 或 runtime-text。</summary>
        public string? VocabularyMode { get; }
        /// <summary>Gets the optional projected embedding dimension. / 获取可选投影 Embedding 维度。</summary>
        public int? EmbeddingDimension { get; }
        /// <summary>Gets the exact profile-wide image preprocessing identity. / 获取精确的 Profile 级图像预处理 Identity。</summary>
        public string? ImagePreprocessingId { get; }
        /// <summary>Gets the exact projection-head identity. / 获取精确 Projection Head Identity。</summary>
        public string? ProjectionId { get; }
        /// <summary>Gets the exact embedding normalization identity. / 获取精确 Embedding 归一化 Identity。</summary>
        public string? NormalizationId { get; }
        /// <summary>Gets candidate-set score semantics. / 获取候选集评分语义。</summary>
        public string? ScoreSemantics { get; }
        /// <summary>Gets the tokenizer/model language capability identity. / 获取 Tokenizer/模型语言能力 Identity。</summary>
        public string? Language { get; }
        /// <summary>Gets the fixed or dynamic image-resolution identity. / 获取固定或动态图像分辨率 Identity。</summary>
        public string? Resolution { get; }
        /// <summary>Gets the exact vision-backbone identity. / 获取精确 Vision Backbone Identity。</summary>
        public string? VisionBackbone { get; }
        /// <summary>Gets the exact Q-Former/query-token identity. / 获取精确 Q-Former/Query Token Identity。</summary>
        public string? QFormerId { get; }
        /// <summary>Gets the exact language-model identity. / 获取精确 Language Model Identity。</summary>
        public string? LanguageModelId { get; }
        /// <summary>Gets the exact prompt-template identity. / 获取精确 Prompt Template Identity。</summary>
        public string? PromptTemplateId { get; }
        /// <summary>Gets the exact generation-config identity. / 获取精确 Generation Config Identity。</summary>
        public string? GenerationConfigId { get; }
        /// <summary>Gets generation-policy mode. / 获取生成策略模式。</summary>
        public string? GenerationMode { get; }
        /// <summary>Gets the exact KV-cache schema identity, including explicit no-cache schemas. / 获取精确 KV-cache Schema Identity，包括显式无缓存 Schema。</summary>
        public string? KvCacheSchemaId { get; }
        /// <summary>Gets the maximum image count accepted by this artifact-bound contract. / 获取此工件绑定合同允许的最大图像数量。</summary>
        public int? ImageCount { get; }
        /// <summary>Gets the maximum expanded text/image context length in tokens. / 获取展开后的图文上下文最大 Token 数。</summary>
        public int? ContextLength { get; }
        /// <summary>Gets the maximum document page count accepted by this artifact-bound contract. / 获取此工件绑定合同允许的最大文档页数。</summary>
        public int? PageCount { get; }
        /// <summary>Gets the exact structured-output schema identity. / 获取精确的结构化输出 Schema Identity。</summary>
        public string? SchemaId { get; }
        /// <summary>Gets the OCR ownership boundary: caller, processor, or ocr-free. / 获取 OCR 所有权边界：caller、processor 或 ocr-free。</summary>
        public string? OcrOwnership { get; }
        /// <summary>Gets the exact document processor identity shared by all cooperating artifacts. / 获取全部协作工件共享的精确文档 Processor Identity。</summary>
        public string? ProcessorId { get; }
        /// <summary>Gets the exact required audio sample rate. / 获取精确要求的音频采样率。</summary>
        public int? SampleRate { get; }
        /// <summary>Gets the exact accepted source channel count for this artifact record. / 获取此工件记录接受的精确源声道数。</summary>
        public int? ChannelCount { get; }
        /// <summary>Gets timestamp ownership/mode identity. / 获取时间戳所有权或模式 Identity。</summary>
        public string? TimestampMode { get; }
        /// <summary>Gets speaker/VAD/embedding/clustering ownership mode. / 获取说话人、VAD、Embedding、聚类所有权模式。</summary>
        public string? SpeakerMode { get; }
        /// <summary>Gets waveform, log-Mel, or representation feature identity. / 获取波形、log-Mel 或表征 Feature Identity。</summary>
        public string? AudioFeatureId { get; }
        /// <summary>Gets the exact VAD model or caller-segment identity. / 获取精确 VAD 模型或调用方分段 Identity。</summary>
        public string? VadId { get; }
        /// <summary>Gets the exact speaker embedding/label identity. / 获取精确说话人 Embedding/标签 Identity。</summary>
        public string? SpeakerId { get; }

        private static string? NormalizeOptional(string? value)
        {
            string? normalized = value?.Trim();
            return string.IsNullOrEmpty(normalized) ? null : normalized;
        }

        private static IReadOnlyList<T> CopyValues<T>(IEnumerable<T>? values)
        {
            return new List<T>(values ?? new T[0]).AsReadOnly();
        }

        private static IReadOnlyList<T> CopyObjects<T>(IEnumerable<T>? values) where T : class
        {
            var list = new List<T>();
            if (values != null)
            {
                foreach (T value in values)
                {
                    if (value == null) throw new ArgumentException("Catalog collections cannot contain null values.", nameof(values));
                    list.Add(value);
                }
            }

            return list.AsReadOnly();
        }
    }

    /// <summary>Describes one model catalog entry. / 描述一个模型目录条目。</summary>
    public sealed class ModelCatalogEntry
    {
        private readonly IReadOnlyList<ModelCatalogArtifact> _artifacts;
        private readonly IReadOnlyList<ModelCatalogAsset> _testInputs;

        /// <summary>Initializes a catalog entry. / 初始化目录条目。</summary>
        public ModelCatalogEntry(
            string? modelId,
            string? name,
            string? family,
            string? task,
            string? modelVersion,
            ModelCatalogStatus status,
            string? description,
            ModelSourceDocument? source,
            ModelCatalogRelease? release,
            IEnumerable<ModelCatalogArtifact>? artifacts,
            IEnumerable<ModelCatalogAsset>? testInputs,
            string? expectedResultAssetId = null,
            string? documentationPath = null)
        {
            ModelId = modelId;
            Name = name;
            Family = family;
            Task = task;
            ModelVersion = modelVersion;
            Status = status;
            Description = description;
            Source = source;
            Release = release;
            _artifacts = CopyObjects(artifacts);
            _testInputs = CopyObjects(testInputs);
            ExpectedResultAssetId = expectedResultAssetId;
            DocumentationPath = documentationPath;
        }

        /// <summary>Gets the stable DeploySharp model identifier. / 获取稳定 DeploySharp 模型标识。</summary>
        public string? ModelId { get; }
        /// <summary>Gets the display name. / 获取显示名称。</summary>
        public string? Name { get; }
        /// <summary>Gets the model family. / 获取模型族。</summary>
        public string? Family { get; }
        /// <summary>Gets the task identifier. / 获取任务标识。</summary>
        public string? Task { get; }
        /// <summary>Gets the model version. / 获取模型版本。</summary>
        public string? ModelVersion { get; }
        /// <summary>Gets the publication status. / 获取发布状态。</summary>
        public ModelCatalogStatus Status { get; }
        /// <summary>Gets the human-readable description. / 获取人类可读描述。</summary>
        public string? Description { get; }
        /// <summary>Gets source and license metadata. / 获取来源和许可证元数据。</summary>
        public ModelSourceDocument? Source { get; }
        /// <summary>Gets immutable Release provenance. / 获取不可变 Release 来源。</summary>
        public ModelCatalogRelease? Release { get; }
        /// <summary>Gets backend-specific artifacts. / 获取后端特定工件。</summary>
        public IReadOnlyList<ModelCatalogArtifact> Artifacts => _artifacts;
        /// <summary>Gets test-input assets. / 获取测试输入资产。</summary>
        public IReadOnlyList<ModelCatalogAsset> TestInputs => _testInputs;
        /// <summary>Gets the optional expected-result asset identifier. / 获取可选预期结果资产标识。</summary>
        public string? ExpectedResultAssetId { get; }
        /// <summary>Gets the generated documentation path. / 获取生成文档路径。</summary>
        public string? DocumentationPath { get; }

        private static IReadOnlyList<T> CopyObjects<T>(IEnumerable<T>? values) where T : class
        {
            var list = new List<T>();
            if (values != null)
            {
                foreach (T value in values)
                {
                    if (value == null) throw new ArgumentException("Catalog collections cannot contain null values.", nameof(values));
                    list.Add(value);
                }
            }

            return list.AsReadOnly();
        }
    }

    /// <summary>Represents an unvalidated catalog document. / 表示尚未验证的目录文档。</summary>
    public sealed class ModelCatalogDocument
    {
        private readonly IReadOnlyList<ModelCatalogEntry> _entries;

        /// <summary>Initializes a catalog document. / 初始化目录文档。</summary>
        public ModelCatalogDocument(string? schemaVersion, string? generatedAt, string? catalogRevision, Uri? sourceRepository, IEnumerable<ModelCatalogEntry>? entries)
        {
            SchemaVersion = schemaVersion;
            GeneratedAt = generatedAt;
            CatalogRevision = catalogRevision;
            SourceRepository = sourceRepository;
            var list = new List<ModelCatalogEntry>();
            if (entries != null)
            {
                foreach (ModelCatalogEntry entry in entries)
                {
                    if (entry == null) throw new ArgumentException("Catalog entries cannot contain null values.", nameof(entries));
                    list.Add(entry);
                }
            }

            _entries = new ReadOnlyCollection<ModelCatalogEntry>(list);
        }

        /// <summary>Gets the catalog schema version. / 获取目录 Schema 版本。</summary>
        public string? SchemaVersion { get; }
        /// <summary>Gets the ISO-8601 generation timestamp text. / 获取 ISO-8601 生成时间文本。</summary>
        public string? GeneratedAt { get; }
        /// <summary>Gets the immutable catalog revision. / 获取不可变目录修订。</summary>
        public string? CatalogRevision { get; }
        /// <summary>Gets the catalog source repository. / 获取目录来源仓库。</summary>
        public Uri? SourceRepository { get; }
        /// <summary>Gets catalog entries in document order. / 获取文档顺序的目录条目。</summary>
        public IReadOnlyList<ModelCatalogEntry> Entries => _entries;
    }
}
