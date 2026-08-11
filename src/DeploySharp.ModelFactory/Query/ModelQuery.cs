using System;
using System.Collections.Generic;
using System.Linq;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Defines deterministic catalog selection filters. / 定义确定性目录选择筛选条件。</summary>
    public sealed class ModelQuery
    {
        /// <summary>Initializes a query. / 初始化查询。</summary>
        public ModelQuery(string? modelId = null, string? task = null, string? family = null, string? format = null, string? backend = null, string? precision = null, string? quantization = null, bool? portable = null, bool includePreview = false, string? modelVersion = null, string? capability = null, string? tokenizerId = null, string? vocabularyMode = null, string? language = null, string? resolution = null, string? scoreSemantics = null, string? visionBackbone = null, string? qFormerId = null, string? languageModelId = null, string? promptTemplateId = null, string? generationConfigId = null, string? generationMode = null, string? kvCacheSchemaId = null, int? imageCount = null, int? contextLength = null, int? pageCount = null, string? schemaId = null, string? ocrOwnership = null, string? processorId = null, int? sampleRate = null, int? channelCount = null, string? timestampMode = null, string? speakerMode = null, string? audioFeatureId = null, string? vadId = null, string? speakerId = null)
        {
            ModelId = Normalize(modelId);
            Task = Normalize(task);
            Family = Normalize(family);
            Format = Normalize(format);
            Backend = Normalize(backend);
            Precision = Normalize(precision);
            Quantization = Normalize(quantization);
            Portable = portable;
            IncludePreview = includePreview;
            ModelVersion = Normalize(modelVersion);
            Capability = Normalize(capability);
            TokenizerId = Normalize(tokenizerId);
            VocabularyMode = Normalize(vocabularyMode);
            Language = Normalize(language);
            Resolution = Normalize(resolution);
            ScoreSemantics = Normalize(scoreSemantics);
            VisionBackbone = Normalize(visionBackbone);
            QFormerId = Normalize(qFormerId);
            LanguageModelId = Normalize(languageModelId);
            PromptTemplateId = Normalize(promptTemplateId);
            GenerationConfigId = Normalize(generationConfigId);
            GenerationMode = Normalize(generationMode);
            KvCacheSchemaId = Normalize(kvCacheSchemaId);
            ImageCount = imageCount;
            ContextLength = contextLength;
            PageCount = pageCount;
            SchemaId = Normalize(schemaId);
            OcrOwnership = Normalize(ocrOwnership);
            ProcessorId = Normalize(processorId);
            SampleRate = sampleRate;
            ChannelCount = channelCount;
            TimestampMode = Normalize(timestampMode);
            SpeakerMode = Normalize(speakerMode);
            AudioFeatureId = Normalize(audioFeatureId);
            VadId = Normalize(vadId);
            SpeakerId = Normalize(speakerId);
        }

        /// <summary>Gets the optional model identifier filter. / 获取可选模型标识筛选。</summary>
        public string? ModelId { get; }
        /// <summary>Gets the optional task filter. / 获取可选任务筛选。</summary>
        public string? Task { get; }
        /// <summary>Gets the optional family filter. / 获取可选模型族筛选。</summary>
        public string? Family { get; }
        /// <summary>Gets the optional format filter. / 获取可选格式筛选。</summary>
        public string? Format { get; }
        /// <summary>Gets the optional backend filter. / 获取可选后端筛选。</summary>
        public string? Backend { get; }
        /// <summary>Gets the optional precision filter. / 获取可选精度筛选。</summary>
        public string? Precision { get; }
        /// <summary>Gets the optional quantization filter. / 获取可选量化筛选。</summary>
        public string? Quantization { get; }
        /// <summary>Gets the optional portability filter. / 获取可选可移植性筛选。</summary>
        public bool? Portable { get; }
        /// <summary>Gets whether Preview entries are eligible. / 获取是否允许 Preview 条目。</summary>
        public bool IncludePreview { get; }
        /// <summary>Gets the optional exact model or bundle version filter. / 获取可选的精确模型或 bundle 版本筛选。</summary>
        public string? ModelVersion { get; }
        /// <summary>Gets the optional task or prompt capability filter. / 获取可选的任务或提示能力筛选。</summary>
        public string? Capability { get; }
        /// <summary>Gets the optional tokenizer identity filter. / 获取可选 Tokenizer Identity 筛选。</summary>
        public string? TokenizerId { get; }
        /// <summary>Gets the optional serialized vocabulary-mode filter. / 获取可选序列化词汇模式筛选。</summary>
        public string? VocabularyMode { get; }
        /// <summary>Gets the optional tokenizer/model language filter. / 获取可选 Tokenizer/模型语言筛选。</summary>
        public string? Language { get; }
        /// <summary>Gets the optional image-resolution filter. / 获取可选图像分辨率筛选。</summary>
        public string? Resolution { get; }
        /// <summary>Gets the optional score-semantics filter. / 获取可选评分语义筛选。</summary>
        public string? ScoreSemantics { get; }
        /// <summary>Gets the optional vision-backbone identity filter. / 获取可选视觉骨干 Identity 筛选。</summary>
        public string? VisionBackbone { get; }
        /// <summary>Gets the optional Q-Former identity filter. / 获取可选 Q-Former Identity 筛选。</summary>
        public string? QFormerId { get; }
        /// <summary>Gets the optional language-model identity filter. / 获取可选语言模型 Identity 筛选。</summary>
        public string? LanguageModelId { get; }
        /// <summary>Gets the optional prompt-template identity filter. / 获取可选提示模板 Identity 筛选。</summary>
        public string? PromptTemplateId { get; }
        /// <summary>Gets the optional generation-config identity filter. / 获取可选生成配置 Identity 筛选。</summary>
        public string? GenerationConfigId { get; }
        /// <summary>Gets the optional generation-mode filter. / 获取可选生成模式筛选。</summary>
        public string? GenerationMode { get; }
        /// <summary>Gets the optional KV-cache schema identity filter. / 获取可选 KV-cache Schema Identity 筛选。</summary>
        public string? KvCacheSchemaId { get; }
        /// <summary>Gets the optional exact maximum-image-count filter. / 获取可选精确最大图像数筛选。</summary>
        public int? ImageCount { get; }
        /// <summary>Gets the optional exact context-length filter. / 获取可选精确上下文长度筛选。</summary>
        public int? ContextLength { get; }
        /// <summary>Gets the optional exact maximum-page-count filter. / 获取可选精确最大页数筛选。</summary>
        public int? PageCount { get; }
        /// <summary>Gets the optional structured-output schema identity filter. / 获取可选结构化输出 Schema Identity 筛选。</summary>
        public string? SchemaId { get; }
        /// <summary>Gets the optional OCR ownership filter. / 获取可选 OCR 所有权筛选。</summary>
        public string? OcrOwnership { get; }
        /// <summary>Gets the optional document processor identity filter. / 获取可选文档 Processor Identity 筛选。</summary>
        public string? ProcessorId { get; }
        /// <summary>Gets the optional exact audio sample-rate filter. / 获取可选精确音频采样率筛选。</summary>
        public int? SampleRate { get; }
        /// <summary>Gets the optional exact channel-count filter. / 获取可选精确声道数筛选。</summary>
        public int? ChannelCount { get; }
        /// <summary>Gets the optional timestamp-mode filter. / 获取可选时间戳模式筛选。</summary>
        public string? TimestampMode { get; }
        /// <summary>Gets the optional speaker-ownership mode filter. / 获取可选说话人所有权模式筛选。</summary>
        public string? SpeakerMode { get; }
        /// <summary>Gets the optional audio-feature identity filter. / 获取可选音频 Feature Identity 筛选。</summary>
        public string? AudioFeatureId { get; }
        /// <summary>Gets the optional VAD identity filter. / 获取可选 VAD Identity 筛选。</summary>
        public string? VadId { get; }
        /// <summary>Gets the optional speaker identity filter. / 获取可选说话人 Identity 筛选。</summary>
        public string? SpeakerId { get; }

        internal bool Matches(ModelCatalogEntry entry, ModelCatalogArtifact artifact)
        {
            if (!IncludePreview && entry.Status != ModelCatalogStatus.Supported) return false;
            if (ModelId != null && !string.Equals(ModelId, entry.ModelId, StringComparison.OrdinalIgnoreCase)) return false;
            if (Task != null && !string.Equals(Task, entry.Task, StringComparison.OrdinalIgnoreCase)) return false;
            if (Family != null && !string.Equals(Family, entry.Family, StringComparison.OrdinalIgnoreCase)) return false;
            if (ModelVersion != null && !string.Equals(ModelVersion, entry.ModelVersion, StringComparison.OrdinalIgnoreCase)) return false;
            if (Format != null && !string.Equals(Format, artifact.Format, StringComparison.OrdinalIgnoreCase)) return false;
            if (Backend != null && !artifact.CompatibleBackends.Any(value => string.Equals(value, Backend, StringComparison.OrdinalIgnoreCase))) return false;
            if (Precision != null && !string.Equals(Precision, artifact.Precision, StringComparison.OrdinalIgnoreCase)) return false;
            if (Quantization != null && !string.Equals(Quantization, artifact.Quantization, StringComparison.OrdinalIgnoreCase)) return false;
            if (Portable.HasValue && artifact.Portable != Portable.Value) return false;
            if (Capability != null && !artifact.Capabilities.Any(value => string.Equals(value, Capability, StringComparison.OrdinalIgnoreCase))) return false;
            if (TokenizerId != null && !string.Equals(TokenizerId, artifact.TokenizerId, StringComparison.OrdinalIgnoreCase)) return false;
            if (VocabularyMode != null && !string.Equals(VocabularyMode, artifact.VocabularyMode, StringComparison.OrdinalIgnoreCase)) return false;
            if (Language != null && !string.Equals(Language, artifact.Language, StringComparison.OrdinalIgnoreCase)) return false;
            if (Resolution != null && !string.Equals(Resolution, artifact.Resolution, StringComparison.OrdinalIgnoreCase)) return false;
            if (ScoreSemantics != null && !string.Equals(ScoreSemantics, artifact.ScoreSemantics, StringComparison.OrdinalIgnoreCase)) return false;
            if (VisionBackbone != null && !string.Equals(VisionBackbone, artifact.VisionBackbone, StringComparison.OrdinalIgnoreCase)) return false;
            if (QFormerId != null && !string.Equals(QFormerId, artifact.QFormerId, StringComparison.OrdinalIgnoreCase)) return false;
            if (LanguageModelId != null && !string.Equals(LanguageModelId, artifact.LanguageModelId, StringComparison.OrdinalIgnoreCase)) return false;
            if (PromptTemplateId != null && !string.Equals(PromptTemplateId, artifact.PromptTemplateId, StringComparison.OrdinalIgnoreCase)) return false;
            if (GenerationConfigId != null && !string.Equals(GenerationConfigId, artifact.GenerationConfigId, StringComparison.OrdinalIgnoreCase)) return false;
            if (GenerationMode != null && !string.Equals(GenerationMode, artifact.GenerationMode, StringComparison.OrdinalIgnoreCase)) return false;
            if (KvCacheSchemaId != null && !string.Equals(KvCacheSchemaId, artifact.KvCacheSchemaId, StringComparison.OrdinalIgnoreCase)) return false;
            if (ImageCount.HasValue && ImageCount != artifact.ImageCount) return false;
            if (ContextLength.HasValue && ContextLength != artifact.ContextLength) return false;
            if (PageCount.HasValue && PageCount != artifact.PageCount) return false;
            if (SchemaId != null && !string.Equals(SchemaId, artifact.SchemaId, StringComparison.OrdinalIgnoreCase)) return false;
            if (OcrOwnership != null && !string.Equals(OcrOwnership, artifact.OcrOwnership, StringComparison.OrdinalIgnoreCase)) return false;
            if (ProcessorId != null && !string.Equals(ProcessorId, artifact.ProcessorId, StringComparison.OrdinalIgnoreCase)) return false;
            if (SampleRate.HasValue && SampleRate != artifact.SampleRate) return false;
            if (ChannelCount.HasValue && ChannelCount != artifact.ChannelCount) return false;
            if (TimestampMode != null && !string.Equals(TimestampMode, artifact.TimestampMode, StringComparison.OrdinalIgnoreCase)) return false;
            if (SpeakerMode != null && !string.Equals(SpeakerMode, artifact.SpeakerMode, StringComparison.OrdinalIgnoreCase)) return false;
            if (AudioFeatureId != null && !string.Equals(AudioFeatureId, artifact.AudioFeatureId, StringComparison.OrdinalIgnoreCase)) return false;
            if (VadId != null && !string.Equals(VadId, artifact.VadId, StringComparison.OrdinalIgnoreCase)) return false;
            if (SpeakerId != null && !string.Equals(SpeakerId, artifact.SpeakerId, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value!.Trim().ToLowerInvariant();
        }
    }

    /// <summary>Represents one deterministic model/artifact selection. / 表示一个确定性的模型/工件选择结果。</summary>
    public sealed class ModelSelection
    {
        internal ModelSelection(ModelCatalogEntry entry, ModelCatalogArtifact artifact, int score)
        {
            Entry = entry;
            Artifact = artifact;
            Score = score;
        }

        /// <summary>Gets the selected entry. / 获取选中的目录条目。</summary>
        public ModelCatalogEntry Entry { get; }
        /// <summary>Gets the selected artifact. / 获取选中的工件。</summary>
        public ModelCatalogArtifact Artifact { get; }
        /// <summary>Gets the deterministic match score. / 获取确定性匹配分数。</summary>
        public int Score { get; }
    }

    /// <summary>Provides deterministic selection over a validated catalog. / 提供已验证目录上的确定性选择。</summary>
    public static class ModelCatalogQuery
    {
        /// <summary>Returns matching artifacts sorted by score, model id, and artifact id. / 返回按分数、模型标识和工件标识排序的匹配工件。</summary>
        public static IReadOnlyList<ModelSelection> Select(ValidatedModelCatalog catalog, ModelQuery query)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (query == null) throw new ArgumentNullException(nameof(query));
            var selections = new List<ModelSelection>();
            foreach (ModelCatalogEntry entry in catalog.Document.Entries)
            {
                foreach (ModelCatalogArtifact artifact in entry.Artifacts)
                {
                    if (!query.Matches(entry, artifact)) continue;
                    int score = 0;
                    if (query.ModelId != null) score += 100;
                    if (query.Format != null) score += 20;
                    if (query.Backend != null) score += 20;
                    if (query.Precision != null) score += 10;
                    if (query.Quantization != null) score += 10;
                    if (query.ModelVersion != null) score += 30;
                    if (query.Capability != null) score += 15;
                    if (query.TokenizerId != null) score += 10;
                    if (query.Language != null) score += 5;
                    if (query.Resolution != null) score += 5;
                    if (query.ScoreSemantics != null) score += 10;
                    if (query.VisionBackbone != null) score += 10;
                    if (query.QFormerId != null) score += 10;
                    if (query.LanguageModelId != null) score += 10;
                    if (query.PromptTemplateId != null) score += 10;
                    if (query.GenerationConfigId != null) score += 10;
                    if (query.GenerationMode != null) score += 10;
                    if (query.KvCacheSchemaId != null) score += 10;
                    if (query.ImageCount.HasValue) score += 5;
                    if (query.ContextLength.HasValue) score += 5;
                    if (query.PageCount.HasValue) score += 5;
                    if (query.SchemaId != null) score += 10;
                    if (query.OcrOwnership != null) score += 10;
                    if (query.ProcessorId != null) score += 10;
                    if (query.SampleRate.HasValue) score += 5;
                    if (query.ChannelCount.HasValue) score += 5;
                    if (query.TimestampMode != null) score += 10;
                    if (query.SpeakerMode != null) score += 10;
                    if (query.AudioFeatureId != null) score += 10;
                    if (query.VadId != null) score += 10;
                    if (query.SpeakerId != null) score += 10;
                    selections.Add(new ModelSelection(entry, artifact, score));
                }
            }

            return selections
                .OrderByDescending(value => value.Score)
                .ThenBy(value => value.Entry.ModelId, StringComparer.Ordinal)
                .ThenBy(value => value.Artifact.ArtifactId, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>Returns complete multi-artifact bundles and rejects incomplete, mixed-version, or non-reproducible matches. / 返回完整多工件 bundle，并拒绝不完整、混版本或不可复现的匹配项。</summary>
        public static IReadOnlyList<ModelBundleSelection> SelectBundles(ValidatedModelCatalog catalog, ModelBundleQuery query)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (query == null) throw new ArgumentNullException(nameof(query));
            var selections = new List<ModelBundleSelection>();
            foreach (ModelCatalogEntry entry in catalog.Document.Entries)
            {
                if (!query.MatchesEntry(entry)) continue;
                List<ModelCatalogArtifact> artifacts = entry.Artifacts.Where(query.MatchesArtifact).ToList();
                if (artifacts.Count == 0) continue;
                ValidateBundle(entry, artifacts, query.RequiredRoles);
                selections.Add(new ModelBundleSelection(entry, artifacts));
            }
            return selections.OrderBy(value => value.Entry.ModelId, StringComparer.Ordinal).ToList().AsReadOnly();
        }

        private static void ValidateBundle(ModelCatalogEntry entry, IReadOnlyList<ModelCatalogArtifact> artifacts, IReadOnlyList<string> requiredRoles)
        {
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tokenizerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vocabularyModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var embeddingDimensions = new HashSet<int>();
            var imagePreprocessingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var projectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scoreSemantics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resolutions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visionBackbones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var qFormerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var languageModelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var promptTemplateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var generationConfigIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var generationModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var kvCacheSchemaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var imageCounts = new HashSet<int>();
            var contextLengths = new HashSet<int>();
            var pageCounts = new HashSet<int>();
            var schemaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ocrOwnerships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sampleRates = new HashSet<int>();
            var channelCounts = new HashSet<int>();
            var timestampModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var speakerModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var audioFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vadIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var speakerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModelCatalogArtifact artifact in artifacts)
            {
                if (string.IsNullOrWhiteSpace(artifact.BundleRole) || !roles.Add(artifact.BundleRole!)) ThrowBundle(entry, artifact, "Bundle roles must be present and unique.");
                if (string.IsNullOrWhiteSpace(artifact.BundleVersion) || !string.Equals(artifact.BundleVersion, entry.ModelVersion, StringComparison.OrdinalIgnoreCase)) ThrowBundle(entry, artifact, "Every artifact must use the exact catalog bundle version.");
                if (artifact.Conversion == null || string.IsNullOrWhiteSpace(artifact.Conversion.Exporter) || string.IsNullOrWhiteSpace(artifact.Conversion.ExporterVersion) || string.IsNullOrWhiteSpace(artifact.Conversion.SourceRevision)) ThrowBundle(entry, artifact, "Every artifact requires a reproducible conversion record.");
                foreach (string requiredAssetId in artifact.RequiredAssetIds) if (artifact.Assets.All(asset => !string.Equals(asset.AssetId, requiredAssetId, StringComparison.OrdinalIgnoreCase))) ThrowBundle(entry, artifact, "A required sidecar is missing: " + requiredAssetId + ".");
                if (!string.IsNullOrWhiteSpace(artifact.TokenizerId)) tokenizerIds.Add(artifact.TokenizerId!);
                if (!string.IsNullOrWhiteSpace(artifact.VocabularyMode)) vocabularyModes.Add(artifact.VocabularyMode!);
                if (artifact.EmbeddingDimension.HasValue) embeddingDimensions.Add(artifact.EmbeddingDimension.Value);
                if (!string.IsNullOrWhiteSpace(artifact.ImagePreprocessingId)) imagePreprocessingIds.Add(artifact.ImagePreprocessingId!);
                if (!string.IsNullOrWhiteSpace(artifact.ProjectionId)) projectionIds.Add(artifact.ProjectionId!);
                if (!string.IsNullOrWhiteSpace(artifact.NormalizationId)) normalizationIds.Add(artifact.NormalizationId!);
                if (!string.IsNullOrWhiteSpace(artifact.ScoreSemantics)) scoreSemantics.Add(artifact.ScoreSemantics!);
                if (!string.IsNullOrWhiteSpace(artifact.Language)) languages.Add(artifact.Language!);
                if (!string.IsNullOrWhiteSpace(artifact.Resolution)) resolutions.Add(artifact.Resolution!);
                if (!string.IsNullOrWhiteSpace(artifact.VisionBackbone)) visionBackbones.Add(artifact.VisionBackbone!);
                if (!string.IsNullOrWhiteSpace(artifact.QFormerId)) qFormerIds.Add(artifact.QFormerId!);
                if (!string.IsNullOrWhiteSpace(artifact.LanguageModelId)) languageModelIds.Add(artifact.LanguageModelId!);
                if (!string.IsNullOrWhiteSpace(artifact.PromptTemplateId)) promptTemplateIds.Add(artifact.PromptTemplateId!);
                if (!string.IsNullOrWhiteSpace(artifact.GenerationConfigId)) generationConfigIds.Add(artifact.GenerationConfigId!);
                if (!string.IsNullOrWhiteSpace(artifact.GenerationMode)) generationModes.Add(artifact.GenerationMode!);
                if (!string.IsNullOrWhiteSpace(artifact.KvCacheSchemaId)) kvCacheSchemaIds.Add(artifact.KvCacheSchemaId!);
                if (artifact.ImageCount.HasValue) imageCounts.Add(artifact.ImageCount.Value);
                if (artifact.ContextLength.HasValue) contextLengths.Add(artifact.ContextLength.Value);
                if (artifact.PageCount.HasValue) pageCounts.Add(artifact.PageCount.Value);
                if (!string.IsNullOrWhiteSpace(artifact.SchemaId)) schemaIds.Add(artifact.SchemaId!);
                if (!string.IsNullOrWhiteSpace(artifact.OcrOwnership)) ocrOwnerships.Add(artifact.OcrOwnership!);
                if (!string.IsNullOrWhiteSpace(artifact.ProcessorId)) processorIds.Add(artifact.ProcessorId!);
                if (artifact.SampleRate.HasValue) sampleRates.Add(artifact.SampleRate.Value);
                if (artifact.ChannelCount.HasValue) channelCounts.Add(artifact.ChannelCount.Value);
                if (!string.IsNullOrWhiteSpace(artifact.TimestampMode)) timestampModes.Add(artifact.TimestampMode!);
                if (!string.IsNullOrWhiteSpace(artifact.SpeakerMode)) speakerModes.Add(artifact.SpeakerMode!);
                if (!string.IsNullOrWhiteSpace(artifact.AudioFeatureId)) audioFeatureIds.Add(artifact.AudioFeatureId!);
                if (!string.IsNullOrWhiteSpace(artifact.VadId)) vadIds.Add(artifact.VadId!);
                if (!string.IsNullOrWhiteSpace(artifact.SpeakerId)) speakerIds.Add(artifact.SpeakerId!);
            }
            foreach (string role in requiredRoles) if (!roles.Contains(role)) ThrowBundle(entry, null, "A required bundle role is missing: " + role + ".");
            if (tokenizerIds.Count > 1) ThrowBundle(entry, null, "A bundle cannot mix tokenizer identities.");
            if (vocabularyModes.Count > 1) ThrowBundle(entry, null, "A bundle cannot mix vocabulary modes.");
            if (tokenizerIds.Count == 1 && artifacts.Any(value => string.IsNullOrWhiteSpace(value.TokenizerId))) ThrowBundle(entry, null, "Every artifact in a tokenizer-bound bundle must declare the tokenizer identity.");
            if (vocabularyModes.Count == 1 && artifacts.Any(value => string.IsNullOrWhiteSpace(value.VocabularyMode))) ThrowBundle(entry, null, "Every artifact in a vocabulary-bound bundle must declare the vocabulary mode.");
            ValidateShared(embeddingDimensions.Count, artifacts.Any(value => !value.EmbeddingDimension.HasValue), entry, "embedding dimension");
            ValidateShared(imagePreprocessingIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.ImagePreprocessingId)), entry, "image preprocessing identity");
            ValidateShared(projectionIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.ProjectionId)), entry, "projection identity");
            ValidateShared(normalizationIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.NormalizationId)), entry, "normalization identity");
            ValidateShared(scoreSemantics.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.ScoreSemantics)), entry, "score semantics");
            ValidateShared(languages.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.Language)), entry, "language identity");
            ValidateShared(resolutions.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.Resolution)), entry, "resolution identity");
            ValidateShared(visionBackbones.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.VisionBackbone)), entry, "vision-backbone identity");
            ValidateShared(qFormerIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.QFormerId)), entry, "Q-Former identity");
            ValidateShared(languageModelIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.LanguageModelId)), entry, "language-model identity");
            ValidateShared(promptTemplateIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.PromptTemplateId)), entry, "prompt-template identity");
            ValidateShared(generationConfigIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.GenerationConfigId)), entry, "generation-config identity");
            ValidateShared(generationModes.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.GenerationMode)), entry, "generation mode");
            ValidateShared(kvCacheSchemaIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.KvCacheSchemaId)), entry, "KV-cache schema identity");
            ValidateShared(imageCounts.Count, artifacts.Any(value => !value.ImageCount.HasValue), entry, "image-count identity");
            ValidateShared(contextLengths.Count, artifacts.Any(value => !value.ContextLength.HasValue), entry, "context-length identity");
            ValidateShared(pageCounts.Count, artifacts.Any(value => !value.PageCount.HasValue), entry, "page-count identity");
            ValidateShared(schemaIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.SchemaId)), entry, "schema identity");
            ValidateShared(ocrOwnerships.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.OcrOwnership)), entry, "OCR ownership");
            ValidateShared(processorIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.ProcessorId)), entry, "processor identity");
            ValidateShared(sampleRates.Count, artifacts.Any(value => !value.SampleRate.HasValue), entry, "sample-rate identity");
            ValidateShared(channelCounts.Count, artifacts.Any(value => !value.ChannelCount.HasValue), entry, "channel-count identity");
            ValidateShared(timestampModes.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.TimestampMode)), entry, "timestamp mode");
            ValidateShared(speakerModes.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.SpeakerMode)), entry, "speaker mode");
            ValidateShared(audioFeatureIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.AudioFeatureId)), entry, "audio-feature identity");
            ValidateShared(vadIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.VadId)), entry, "VAD identity");
            ValidateShared(speakerIds.Count, artifacts.Any(value => string.IsNullOrWhiteSpace(value.SpeakerId)), entry, "speaker identity");
        }

        private static void ValidateShared(int distinctCount, bool hasMissing, ModelCatalogEntry entry, string name)
        {
            if (distinctCount > 1) ThrowBundle(entry, null, "A bundle cannot mix " + name + ".");
            if (distinctCount == 1 && hasMissing) ThrowBundle(entry, null, "Every artifact in a bound bundle must declare the " + name + ".");
        }

        private static void ThrowBundle(ModelCatalogEntry entry, ModelCatalogArtifact? artifact, string message)
        {
            throw new ModelFactoryException("The selected multi-artifact bundle is invalid.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.BundleInvalid, message, modelId: entry.ModelId, artifactId: artifact?.ArtifactId) });
        }
    }

    /// <summary>Defines deterministic filters for a complete multi-artifact model bundle. / 定义完整多工件模型 bundle 的确定性筛选。</summary>
    public sealed class ModelBundleQuery
    {
        private readonly IReadOnlyList<string> _requiredRoles;

        /// <summary>Initializes a bundle query. / 初始化 bundle 查询。</summary>
        public ModelBundleQuery(string? task = null, string? family = null, string? modelVersion = null, string? capability = null, string? format = null, string? backend = null, string? precision = null, bool includePreview = false, IEnumerable<string>? requiredRoles = null, string? tokenizerId = null, string? vocabularyMode = null, string? language = null, string? resolution = null, string? scoreSemantics = null, string? visionBackbone = null, string? qFormerId = null, string? languageModelId = null, string? promptTemplateId = null, string? generationConfigId = null, string? generationMode = null, string? kvCacheSchemaId = null, int? imageCount = null, int? contextLength = null, int? pageCount = null, string? schemaId = null, string? ocrOwnership = null, string? processorId = null, int? sampleRate = null, int? channelCount = null, string? timestampMode = null, string? speakerMode = null, string? audioFeatureId = null, string? vadId = null, string? speakerId = null)
        {
            Task = Normalize(task); Family = Normalize(family); ModelVersion = Normalize(modelVersion); Capability = Normalize(capability); Format = Normalize(format); Backend = Normalize(backend); Precision = Normalize(precision); IncludePreview = includePreview; TokenizerId = Normalize(tokenizerId); VocabularyMode = Normalize(vocabularyMode); Language = Normalize(language); Resolution = Normalize(resolution); ScoreSemantics = Normalize(scoreSemantics); VisionBackbone = Normalize(visionBackbone); QFormerId = Normalize(qFormerId); LanguageModelId = Normalize(languageModelId); PromptTemplateId = Normalize(promptTemplateId); GenerationConfigId = Normalize(generationConfigId); GenerationMode = Normalize(generationMode); KvCacheSchemaId = Normalize(kvCacheSchemaId); ImageCount = imageCount; ContextLength = contextLength; PageCount = pageCount; SchemaId = Normalize(schemaId); OcrOwnership = Normalize(ocrOwnership); ProcessorId = Normalize(processorId); SampleRate = sampleRate; ChannelCount = channelCount; TimestampMode = Normalize(timestampMode); SpeakerMode = Normalize(speakerMode); AudioFeatureId = Normalize(audioFeatureId); VadId = Normalize(vadId); SpeakerId = Normalize(speakerId);
            _requiredRoles = new List<string>(requiredRoles ?? Array.Empty<string>()).Select(value => Normalize(value) ?? throw new ArgumentException("Required roles cannot be empty.", nameof(requiredRoles))).Distinct(StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
        }

        /// <summary>Gets the optional task filter. / 获取可选任务筛选。</summary>
        public string? Task { get; }
        /// <summary>Gets the optional family filter. / 获取可选模型族筛选。</summary>
        public string? Family { get; }
        /// <summary>Gets the optional exact bundle version filter. / 获取可选精确 bundle 版本筛选。</summary>
        public string? ModelVersion { get; }
        /// <summary>Gets the optional prompt capability filter. / 获取可选提示能力筛选。</summary>
        public string? Capability { get; }
        /// <summary>Gets the optional format filter. / 获取可选格式筛选。</summary>
        public string? Format { get; }
        /// <summary>Gets the optional backend filter. / 获取可选后端筛选。</summary>
        public string? Backend { get; }
        /// <summary>Gets the optional precision filter. / 获取可选精度筛选。</summary>
        public string? Precision { get; }
        /// <summary>Gets whether Preview and External entries may be selected. / 获取是否可选择 Preview 与 External 条目。</summary>
        public bool IncludePreview { get; }
        /// <summary>Gets roles that must all be present in the selected bundle. / 获取选中 bundle 必须全部具备的角色。</summary>
        public IReadOnlyList<string> RequiredRoles => _requiredRoles;
        /// <summary>Gets the optional tokenizer identity filter. / 获取可选 Tokenizer Identity 筛选。</summary>
        public string? TokenizerId { get; }
        /// <summary>Gets the optional serialized vocabulary-mode filter. / 获取可选序列化词汇模式筛选。</summary>
        public string? VocabularyMode { get; }
        /// <summary>Gets the optional tokenizer/model language filter. / 获取可选 Tokenizer/模型语言筛选。</summary>
        public string? Language { get; }
        /// <summary>Gets the optional image-resolution filter. / 获取可选图像分辨率筛选。</summary>
        public string? Resolution { get; }
        /// <summary>Gets the optional score-semantics filter. / 获取可选评分语义筛选。</summary>
        public string? ScoreSemantics { get; }
        /// <summary>Gets the optional vision-backbone identity filter. / 获取可选视觉骨干 Identity 筛选。</summary>
        public string? VisionBackbone { get; }
        /// <summary>Gets the optional Q-Former identity filter. / 获取可选 Q-Former Identity 筛选。</summary>
        public string? QFormerId { get; }
        /// <summary>Gets the optional language-model identity filter. / 获取可选语言模型 Identity 筛选。</summary>
        public string? LanguageModelId { get; }
        /// <summary>Gets the optional prompt-template identity filter. / 获取可选提示模板 Identity 筛选。</summary>
        public string? PromptTemplateId { get; }
        /// <summary>Gets the optional generation-config identity filter. / 获取可选生成配置 Identity 筛选。</summary>
        public string? GenerationConfigId { get; }
        /// <summary>Gets the optional generation-mode filter. / 获取可选生成模式筛选。</summary>
        public string? GenerationMode { get; }
        /// <summary>Gets the optional KV-cache schema identity filter. / 获取可选 KV-cache Schema Identity 筛选。</summary>
        public string? KvCacheSchemaId { get; }
        /// <summary>Gets the optional exact maximum-image-count filter. / 获取可选精确最大图像数筛选。</summary>
        public int? ImageCount { get; }
        /// <summary>Gets the optional exact context-length filter. / 获取可选精确上下文长度筛选。</summary>
        public int? ContextLength { get; }
        /// <summary>Gets the optional exact maximum-page-count filter. / 获取可选精确最大页数筛选。</summary>
        public int? PageCount { get; }
        /// <summary>Gets the optional structured-output schema identity filter. / 获取可选结构化输出 Schema Identity 筛选。</summary>
        public string? SchemaId { get; }
        /// <summary>Gets the optional OCR ownership filter. / 获取可选 OCR 所有权筛选。</summary>
        public string? OcrOwnership { get; }
        /// <summary>Gets the optional document processor identity filter. / 获取可选文档 Processor Identity 筛选。</summary>
        public string? ProcessorId { get; }
        /// <summary>Gets optional exact audio sample rate. / 获取可选精确音频采样率。</summary>
        public int? SampleRate { get; }
        /// <summary>Gets optional exact source channel count. / 获取可选精确源声道数。</summary>
        public int? ChannelCount { get; }
        /// <summary>Gets optional timestamp mode. / 获取可选时间戳模式。</summary>
        public string? TimestampMode { get; }
        /// <summary>Gets optional speaker mode. / 获取可选说话人模式。</summary>
        public string? SpeakerMode { get; }
        /// <summary>Gets optional audio-feature identity. / 获取可选音频 Feature Identity。</summary>
        public string? AudioFeatureId { get; }
        /// <summary>Gets optional VAD identity. / 获取可选 VAD Identity。</summary>
        public string? VadId { get; }
        /// <summary>Gets optional speaker identity. / 获取可选说话人 Identity。</summary>
        public string? SpeakerId { get; }

        internal bool MatchesEntry(ModelCatalogEntry entry) => (IncludePreview || entry.Status == ModelCatalogStatus.Supported) && (Task == null || string.Equals(Task, entry.Task, StringComparison.OrdinalIgnoreCase)) && (Family == null || string.Equals(Family, entry.Family, StringComparison.OrdinalIgnoreCase)) && (ModelVersion == null || string.Equals(ModelVersion, entry.ModelVersion, StringComparison.OrdinalIgnoreCase));
        internal bool MatchesArtifact(ModelCatalogArtifact artifact) => (Format == null || string.Equals(Format, artifact.Format, StringComparison.OrdinalIgnoreCase)) && (Backend == null || artifact.CompatibleBackends.Any(value => string.Equals(value, Backend, StringComparison.OrdinalIgnoreCase))) && (Precision == null || string.Equals(Precision, artifact.Precision, StringComparison.OrdinalIgnoreCase)) && (Capability == null || artifact.Capabilities.Any(value => string.Equals(value, Capability, StringComparison.OrdinalIgnoreCase))) && (TokenizerId == null || string.Equals(TokenizerId, artifact.TokenizerId, StringComparison.OrdinalIgnoreCase)) && (VocabularyMode == null || string.Equals(VocabularyMode, artifact.VocabularyMode, StringComparison.OrdinalIgnoreCase)) && (Language == null || string.Equals(Language, artifact.Language, StringComparison.OrdinalIgnoreCase)) && (Resolution == null || string.Equals(Resolution, artifact.Resolution, StringComparison.OrdinalIgnoreCase)) && (ScoreSemantics == null || string.Equals(ScoreSemantics, artifact.ScoreSemantics, StringComparison.OrdinalIgnoreCase)) && (VisionBackbone == null || string.Equals(VisionBackbone, artifact.VisionBackbone, StringComparison.OrdinalIgnoreCase)) && (QFormerId == null || string.Equals(QFormerId, artifact.QFormerId, StringComparison.OrdinalIgnoreCase)) && (LanguageModelId == null || string.Equals(LanguageModelId, artifact.LanguageModelId, StringComparison.OrdinalIgnoreCase)) && (PromptTemplateId == null || string.Equals(PromptTemplateId, artifact.PromptTemplateId, StringComparison.OrdinalIgnoreCase)) && (GenerationConfigId == null || string.Equals(GenerationConfigId, artifact.GenerationConfigId, StringComparison.OrdinalIgnoreCase)) && (GenerationMode == null || string.Equals(GenerationMode, artifact.GenerationMode, StringComparison.OrdinalIgnoreCase)) && (KvCacheSchemaId == null || string.Equals(KvCacheSchemaId, artifact.KvCacheSchemaId, StringComparison.OrdinalIgnoreCase)) && (!ImageCount.HasValue || ImageCount == artifact.ImageCount) && (!ContextLength.HasValue || ContextLength == artifact.ContextLength) && (!PageCount.HasValue || PageCount == artifact.PageCount) && (SchemaId == null || string.Equals(SchemaId, artifact.SchemaId, StringComparison.OrdinalIgnoreCase)) && (OcrOwnership == null || string.Equals(OcrOwnership, artifact.OcrOwnership, StringComparison.OrdinalIgnoreCase)) && (ProcessorId == null || string.Equals(ProcessorId, artifact.ProcessorId, StringComparison.OrdinalIgnoreCase)) && (!SampleRate.HasValue || SampleRate == artifact.SampleRate) && (!ChannelCount.HasValue || ChannelCount == artifact.ChannelCount) && (TimestampMode == null || string.Equals(TimestampMode, artifact.TimestampMode, StringComparison.OrdinalIgnoreCase)) && (SpeakerMode == null || string.Equals(SpeakerMode, artifact.SpeakerMode, StringComparison.OrdinalIgnoreCase)) && (AudioFeatureId == null || string.Equals(AudioFeatureId, artifact.AudioFeatureId, StringComparison.OrdinalIgnoreCase)) && (VadId == null || string.Equals(VadId, artifact.VadId, StringComparison.OrdinalIgnoreCase)) && (SpeakerId == null || string.Equals(SpeakerId, artifact.SpeakerId, StringComparison.OrdinalIgnoreCase));
        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value!.Trim().ToLowerInvariant();
    }

    /// <summary>Represents one complete selected multi-artifact bundle. / 表示一个完整选中的多工件 bundle。</summary>
    public sealed class ModelBundleSelection
    {
        private readonly IReadOnlyList<ModelCatalogArtifact> _artifacts;
        internal ModelBundleSelection(ModelCatalogEntry entry, IEnumerable<ModelCatalogArtifact> artifacts) { Entry = entry; _artifacts = new List<ModelCatalogArtifact>(artifacts).AsReadOnly(); }
        /// <summary>Gets the selected catalog entry. / 获取选中的目录条目。</summary>
        public ModelCatalogEntry Entry { get; }
        /// <summary>Gets every cooperating artifact in deterministic catalog order. / 获取按目录顺序排列的全部协作工件。</summary>
        public IReadOnlyList<ModelCatalogArtifact> Artifacts => _artifacts;
    }
}
