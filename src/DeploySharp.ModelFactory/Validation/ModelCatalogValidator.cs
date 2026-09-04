using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using JYPPX.DeploySharp.ModelPack.Json;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Validates catalog structure, provenance, versioned Release URLs, and model admission rules. / 验证目录结构、来源、版本化 Release URL 和模型准入规则。</summary>
    public static class ModelCatalogValidator
    {
        // Release collections may be maintained over time (for example models-visual.1 or models-llm.1).
        // Keep a required namespace/version suffix while rejecting floating aliases such as "latest".
        private static readonly Regex ReleaseTagPattern = new Regex("^models-[A-Za-z0-9][A-Za-z0-9._-]*\\.[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant);

        /// <summary>Validates and normalizes a catalog document. / 验证并规范化目录文档。</summary>
        public static ValidatedModelCatalog Validate(ModelCatalogDocument document, ModelCatalogValidationOptions? options = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            ModelCatalogValidationOptions limits = options ?? ModelCatalogValidationOptions.Default;
            var diagnostics = new List<ModelFactoryDiagnostic>();
            Version? version = ValidateVersion(document.SchemaVersion, limits, diagnostics);
            string? revision = RequiredIdentifier(document.CatalogRevision, "$.catalogRevision", limits, diagnostics);
            string? generatedAt = ValidateTimestamp(document.GeneratedAt, diagnostics);
            Uri? sourceRepository = ValidateRepositoryUri(document.SourceRepository, "$.sourceRepository", diagnostics);
            if (document.Entries.Count > limits.MaximumEntries) Add(diagnostics, ModelFactoryDiagnosticCodes.LimitExceeded, "Catalog entry count exceeds the configured limit.", "$.entries");

            var modelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedEntries = new List<ModelCatalogEntry>();
            for (int index = 0; index < document.Entries.Count; index++)
            {
                ModelCatalogEntry entry = document.Entries[index];
                string path = "$.entries[" + index + "]";
                string? modelId = RequiredIdentifier(entry.ModelId, path + ".modelId", limits, diagnostics);
                if (modelId != null && !modelIds.Add(modelId)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Model identifiers must be unique.", path + ".modelId", modelId: modelId);
                RequiredString(entry.Name, path + ".name", limits, diagnostics);
                string? family = RequiredIdentifier(entry.Family, path + ".family", limits, diagnostics);
                string? task = RequiredIdentifier(entry.Task, path + ".task", limits, diagnostics);
                RequiredString(entry.ModelVersion, path + ".modelVersion", limits, diagnostics);
                ValidateStatus(entry.Status, path + ".status", limits, diagnostics);
                OptionalString(entry.Description, path + ".description", limits, diagnostics);
                ValidateSource(entry.Source, path + ".source", entry.Status, limits, diagnostics);
                ValidateRelease(entry.Release, path + ".release", entry.Status, diagnostics);
                if (entry.Artifacts.Count > limits.MaximumArtifactsPerEntry) Add(diagnostics, ModelFactoryDiagnosticCodes.LimitExceeded, "Artifact count exceeds the configured limit.", path + ".artifacts", modelId: modelId);
                if (entry.Status == ModelCatalogStatus.Supported && entry.Artifacts.Count == 0) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported entries require at least one artifact.", path + ".artifacts", modelId: modelId);
                var artifactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var normalizedArtifacts = new List<ModelCatalogArtifact>();
                for (int artifactIndex = 0; artifactIndex < entry.Artifacts.Count; artifactIndex++)
                {
                    ModelCatalogArtifact artifact = entry.Artifacts[artifactIndex];
                    string artifactPath = path + ".artifacts[" + artifactIndex + "]";
                    string? artifactId = RequiredIdentifier(artifact.ArtifactId, artifactPath + ".artifactId", limits, diagnostics);
                    if (artifactId != null && !artifactIds.Add(artifactId)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Artifact identifiers must be unique within an entry.", artifactPath + ".artifactId", modelId: modelId, artifactId: artifactId);
                    string? format = RequiredIdentifier(artifact.Format, artifactPath + ".format", limits, diagnostics);
                    ValidateBackends(artifact.CompatibleBackends, artifactPath + ".compatibleBackends", limits, diagnostics, modelId, artifactId);
                    if (entry.Status == ModelCatalogStatus.Supported && (format == null || !limits.AdmittedFormats.Contains(format, StringComparer.OrdinalIgnoreCase))) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported artifact format has no current DeploySharp deployment evidence.", artifactPath + ".format", modelId: modelId, artifactId: artifactId);
                    if (entry.Status == ModelCatalogStatus.Supported && !artifact.CompatibleBackends.Any(backend => limits.AdmittedBackends.Contains(backend, StringComparer.OrdinalIgnoreCase))) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported artifact backend has no current DeploySharp deployment evidence.", artifactPath + ".compatibleBackends", modelId: modelId, artifactId: artifactId);
                    OptionalIdentifier(artifact.Precision, artifactPath + ".precision", limits, diagnostics);
                    OptionalIdentifier(artifact.Quantization, artifactPath + ".quantization", limits, diagnostics);
                    OptionalIdentifier(artifact.BundleRole, artifactPath + ".bundleRole", limits, diagnostics);
                    OptionalString(artifact.BundleVersion, artifactPath + ".bundleVersion", limits, diagnostics);
                    string? normalizedTokenizerId = artifact.TokenizerId == null ? null : RequiredIdentifier(artifact.TokenizerId, artifactPath + ".tokenizerId", limits, diagnostics, modelId);
                    string? normalizedVocabularyMode = artifact.VocabularyMode == null ? null : RequiredIdentifier(artifact.VocabularyMode, artifactPath + ".vocabularyMode", limits, diagnostics, modelId);
                    if (artifact.EmbeddingDimension.HasValue && artifact.EmbeddingDimension.Value <= 0) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "embeddingDimension must be positive.", artifactPath + ".embeddingDimension", modelId, artifactId);
                    string? normalizedImagePreprocessingId = artifact.ImagePreprocessingId == null ? null : RequiredIdentifier(artifact.ImagePreprocessingId, artifactPath + ".imagePreprocessingId", limits, diagnostics, modelId);
                    string? normalizedProjectionId = artifact.ProjectionId == null ? null : RequiredIdentifier(artifact.ProjectionId, artifactPath + ".projectionId", limits, diagnostics, modelId);
                    string? normalizedNormalizationId = artifact.NormalizationId == null ? null : RequiredIdentifier(artifact.NormalizationId, artifactPath + ".normalizationId", limits, diagnostics, modelId);
                    string? normalizedScoreSemantics = artifact.ScoreSemantics == null ? null : RequiredIdentifier(artifact.ScoreSemantics, artifactPath + ".scoreSemantics", limits, diagnostics, modelId);
                    string? normalizedLanguage = artifact.Language == null ? null : RequiredIdentifier(artifact.Language, artifactPath + ".language", limits, diagnostics, modelId);
                    string? normalizedResolution = artifact.Resolution == null ? null : RequiredIdentifier(artifact.Resolution, artifactPath + ".resolution", limits, diagnostics, modelId);
                    string? normalizedVisionBackbone = artifact.VisionBackbone == null ? null : RequiredIdentifier(artifact.VisionBackbone, artifactPath + ".visionBackbone", limits, diagnostics, modelId);
                    string? normalizedQFormerId = artifact.QFormerId == null ? null : RequiredIdentifier(artifact.QFormerId, artifactPath + ".qFormerId", limits, diagnostics, modelId);
                    string? normalizedLanguageModelId = artifact.LanguageModelId == null ? null : RequiredIdentifier(artifact.LanguageModelId, artifactPath + ".languageModelId", limits, diagnostics, modelId);
                    string? normalizedPromptTemplateId = artifact.PromptTemplateId == null ? null : RequiredIdentifier(artifact.PromptTemplateId, artifactPath + ".promptTemplateId", limits, diagnostics, modelId);
                    string? normalizedGenerationConfigId = artifact.GenerationConfigId == null ? null : RequiredIdentifier(artifact.GenerationConfigId, artifactPath + ".generationConfigId", limits, diagnostics, modelId);
                    string? normalizedGenerationMode = artifact.GenerationMode == null ? null : RequiredIdentifier(artifact.GenerationMode, artifactPath + ".generationMode", limits, diagnostics, modelId);
                    string? normalizedKvCacheSchemaId = artifact.KvCacheSchemaId == null ? null : RequiredIdentifier(artifact.KvCacheSchemaId, artifactPath + ".kvCacheSchemaId", limits, diagnostics, modelId);
                    if (artifact.ImageCount.HasValue && artifact.ImageCount.Value <= 0) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "imageCount must be positive.", artifactPath + ".imageCount", modelId, artifactId);
                    if (artifact.ContextLength.HasValue && artifact.ContextLength.Value <= 0) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "contextLength must be positive.", artifactPath + ".contextLength", modelId, artifactId);
                    if (artifact.PageCount.HasValue && artifact.PageCount.Value <= 0) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "pageCount must be positive.", artifactPath + ".pageCount", modelId, artifactId);
                    string? normalizedSchemaId = artifact.SchemaId == null ? null : RequiredIdentifier(artifact.SchemaId, artifactPath + ".schemaId", limits, diagnostics, modelId);
                    string? normalizedOcrOwnership = artifact.OcrOwnership == null ? null : RequiredIdentifier(artifact.OcrOwnership, artifactPath + ".ocrOwnership", limits, diagnostics, modelId);
                    if (normalizedOcrOwnership != null && normalizedOcrOwnership != "caller" && normalizedOcrOwnership != "processor" && normalizedOcrOwnership != "ocr-free") Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "ocrOwnership must be caller, processor, or ocr-free.", artifactPath + ".ocrOwnership", modelId, artifactId);
                    string? normalizedProcessorId = artifact.ProcessorId == null ? null : RequiredIdentifier(artifact.ProcessorId, artifactPath + ".processorId", limits, diagnostics, modelId);
                    if (artifact.SampleRate.HasValue && artifact.SampleRate.Value <= 0) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "sampleRate must be positive.", artifactPath + ".sampleRate", modelId, artifactId);
                    if (artifact.ChannelCount.HasValue && artifact.ChannelCount.Value <= 0) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "channelCount must be positive.", artifactPath + ".channelCount", modelId, artifactId);
                    string? normalizedTimestampMode = artifact.TimestampMode == null ? null : RequiredIdentifier(artifact.TimestampMode, artifactPath + ".timestampMode", limits, diagnostics, modelId);
                    string? normalizedSpeakerMode = artifact.SpeakerMode == null ? null : RequiredIdentifier(artifact.SpeakerMode, artifactPath + ".speakerMode", limits, diagnostics, modelId);
                    string? normalizedAudioFeatureId = artifact.AudioFeatureId == null ? null : RequiredIdentifier(artifact.AudioFeatureId, artifactPath + ".audioFeatureId", limits, diagnostics, modelId);
                    string? normalizedVadId = artifact.VadId == null ? null : RequiredIdentifier(artifact.VadId, artifactPath + ".vadId", limits, diagnostics, modelId);
                    string? normalizedSpeakerId = artifact.SpeakerId == null ? null : RequiredIdentifier(artifact.SpeakerId, artifactPath + ".speakerId", limits, diagnostics, modelId);
                    var normalizedCapabilities = new List<string>();
                    var capabilitySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string capability in artifact.Capabilities)
                    {
                        string? normalizedCapability = RequiredIdentifier(capability, artifactPath + ".capabilities", limits, diagnostics, modelId);
                        if (normalizedCapability != null && !capabilitySet.Add(normalizedCapability)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Artifact capabilities must be unique.", artifactPath + ".capabilities", modelId, artifactId);
                        else if (normalizedCapability != null) normalizedCapabilities.Add(normalizedCapability);
                    }
                    if (entry.Status == ModelCatalogStatus.Supported && !artifact.Portable) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported artifacts must be portable.", artifactPath + ".portable", modelId: modelId, artifactId: artifactId);
                    if (IsTensorRtEngine(artifact) && (artifact.Portable || entry.Status != ModelCatalogStatus.External)) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "TensorRT engine/plan assets are allowed only as non-portable External records.", artifactPath, modelId: modelId, artifactId: artifactId);
                    if (artifact.Assets.Count > limits.MaximumAssetsPerArtifact) Add(diagnostics, ModelFactoryDiagnosticCodes.LimitExceeded, "Asset count exceeds the configured limit.", artifactPath + ".assets", modelId: modelId, artifactId: artifactId);
                    var normalizedAssets = new List<ModelCatalogAsset>();
                    var artifactPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int assetIndex = 0; assetIndex < artifact.Assets.Count; assetIndex++)
                    {
                        ModelCatalogAsset asset = artifact.Assets[assetIndex];
                        string assetPath = artifactPath + ".assets[" + assetIndex + "]";
                        string? assetId = RequiredIdentifier(asset.AssetId, assetPath + ".assetId", limits, diagnostics);
                        if (assetId != null && !allAssetIds.Add(assetId)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Asset identifiers must be unique within an entry.", assetPath + ".assetId", modelId: modelId, artifactId: artifactId, assetId: assetId);
                        ValidateAsset(asset, assetPath, entry, limits, diagnostics, modelId, artifactId);
                        string? normalizedPath = NormalizePath(asset.RelativePath, assetPath + ".relativePath", diagnostics, modelId, artifactId, assetId);
                        if (normalizedPath != null && !artifactPaths.Add(normalizedPath)) Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "Normalized asset paths must be unique within an artifact.", assetPath + ".relativePath", modelId: modelId, artifactId: artifactId, assetId: assetId);
                        string? normalizedHash = NormalizeHash(asset.Sha256, assetPath + ".sha256", diagnostics, modelId, artifactId, assetId);
                        string? tag = RequiredString(asset.ReleaseTag, assetPath + ".releaseTag", limits, diagnostics);
                        Uri? uri = ValidateAssetUri(asset.DownloadUri, assetPath + ".downloadUrl", entry.Release, tag, diagnostics, modelId, artifactId, assetId);
                        if (asset.Size < 0) Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "Asset size must be non-negative.", assetPath + ".size", modelId: modelId, artifactId: artifactId, assetId: assetId);
                        if (entry.Status == ModelCatalogStatus.Supported && string.IsNullOrWhiteSpace(asset.LicenseExpression) && string.IsNullOrWhiteSpace(entry.Source?.LicenseExpression)) Add(diagnostics, ModelFactoryDiagnosticCodes.LicenseRejected, "Supported assets require an SPDX license expression.", assetPath + ".licenseExpression", modelId: modelId, artifactId: artifactId, assetId: assetId);
                        string? cacheKey = null;
                        if (revision != null && tag != null && normalizedHash != null && normalizedPath != null) cacheKey = CatalogCacheKey.Compute(revision, tag, normalizedHash, normalizedPath);
                        normalizedAssets.Add(new ModelCatalogAsset(assetId, asset.Kind, tag, uri, normalizedPath, asset.Size, normalizedHash, asset.MediaType, asset.LicenseExpression ?? entry.Source?.LicenseExpression, cacheKey));
                    }

                    if (artifact.ManifestAssetId != null && normalizedAssets.All(asset => !string.Equals(asset.AssetId, artifact.ManifestAssetId, StringComparison.OrdinalIgnoreCase))) Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "manifestAssetId must reference an asset in the same artifact.", artifactPath + ".manifestAssetId", modelId: modelId, artifactId: artifactId);
                    var normalizedRequiredAssetIds = new List<string>();
                    var requiredAssetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string requiredAssetIdValue in artifact.RequiredAssetIds)
                    {
                        string? requiredAssetId = RequiredIdentifier(requiredAssetIdValue, artifactPath + ".requiredAssetIds", limits, diagnostics, modelId);
                        if (requiredAssetId != null && !requiredAssetSet.Add(requiredAssetId)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Required asset identifiers must be unique.", artifactPath + ".requiredAssetIds", modelId, artifactId);
                        else if (requiredAssetId != null)
                        {
                            normalizedRequiredAssetIds.Add(requiredAssetId);
                            if (normalizedAssets.All(asset => !string.Equals(asset.AssetId, requiredAssetId, StringComparison.OrdinalIgnoreCase))) Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "A required bundle sidecar is missing from this artifact.", artifactPath + ".requiredAssetIds", modelId, artifactId, requiredAssetId);
                        }
                    }
                    if (entry.Status == ModelCatalogStatus.Supported)
                    {
                        if (string.IsNullOrWhiteSpace(artifact.ManifestAssetId)) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported artifacts require manifestAssetId.", artifactPath + ".manifestAssetId", modelId: modelId, artifactId: artifactId);
                        if (normalizedAssets.All(asset => asset.Kind != ModelCatalogAssetKind.Manifest)) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported artifacts require a ModelPack manifest asset.", artifactPath + ".assets", modelId: modelId, artifactId: artifactId);
                        if (normalizedAssets.All(asset => asset.Kind != ModelCatalogAssetKind.Model)) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported artifacts require at least one model asset.", artifactPath + ".assets", modelId: modelId, artifactId: artifactId);
                        if (artifact.Conversion == null || string.IsNullOrWhiteSpace(artifact.Conversion.Exporter) || string.IsNullOrWhiteSpace(artifact.Conversion.ExporterVersion) || string.IsNullOrWhiteSpace(artifact.Conversion.SourceRevision)) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported artifacts require reproducible conversion provenance.", artifactPath + ".conversion", modelId: modelId, artifactId: artifactId);
                    }

                normalizedArtifacts.Add(new ModelCatalogArtifact(artifactId, format, artifact.CompatibleBackends, artifact.Precision, artifact.Quantization, artifact.Portable, artifact.ManifestAssetId, normalizedAssets, artifact.Conversion, artifact.BundleRole, artifact.BundleVersion, normalizedCapabilities, normalizedRequiredAssetIds, normalizedTokenizerId, normalizedVocabularyMode, artifact.EmbeddingDimension, normalizedImagePreprocessingId, normalizedProjectionId, normalizedNormalizationId, normalizedScoreSemantics, normalizedLanguage, normalizedResolution, normalizedVisionBackbone, normalizedQFormerId, normalizedLanguageModelId, normalizedPromptTemplateId, normalizedGenerationConfigId, normalizedGenerationMode, normalizedKvCacheSchemaId, artifact.ImageCount, artifact.ContextLength, artifact.PageCount, normalizedSchemaId, normalizedOcrOwnership, normalizedProcessorId, artifact.SampleRate, artifact.ChannelCount, normalizedTimestampMode, normalizedSpeakerMode, normalizedAudioFeatureId, normalizedVadId, normalizedSpeakerId));
                }

                var normalizedTests = new List<ModelCatalogAsset>();
                foreach (ModelCatalogAsset testInput in entry.TestInputs)
                {
                    if (!allAssetIds.Add(testInput.AssetId ?? string.Empty)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Test-input asset identifiers must be unique within an entry.", path + ".testInputs", modelId: modelId, assetId: testInput.AssetId);
                    if (testInput.Kind != ModelCatalogAssetKind.TestInput && testInput.Kind != ModelCatalogAssetKind.TestExpected) Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "Entry test assets must use testInput or testExpected kind.", path + ".testInputs.kind", modelId: modelId, assetId: testInput.AssetId);
                    ValidateAsset(testInput, path + ".testInputs", entry, limits, diagnostics, modelId, assetId: testInput.AssetId);
                    string? normalizedPath = NormalizePath(testInput.RelativePath, path + ".testInputs.relativePath", diagnostics, modelId, assetId: testInput.AssetId);
                    string? normalizedHash = NormalizeHash(testInput.Sha256, path + ".testInputs.sha256", diagnostics, modelId, assetId: testInput.AssetId);
                    string? tag = RequiredString(testInput.ReleaseTag, path + ".testInputs.releaseTag", limits, diagnostics);
                    Uri? uri = ValidateAssetUri(testInput.DownloadUri, path + ".testInputs.downloadUrl", entry.Release, tag, diagnostics, modelId, assetId: testInput.AssetId);
                    string? cacheKey = revision != null && tag != null && normalizedHash != null && normalizedPath != null ? CatalogCacheKey.Compute(revision, tag, normalizedHash, normalizedPath) : null;
                    normalizedTests.Add(new ModelCatalogAsset(testInput.AssetId, testInput.Kind, tag, uri, normalizedPath, testInput.Size, normalizedHash, testInput.MediaType, testInput.LicenseExpression ?? entry.Source?.LicenseExpression, cacheKey));
                }

                if (entry.Status == ModelCatalogStatus.Supported && normalizedTests.All(asset => asset.Kind != ModelCatalogAssetKind.TestInput)) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported entries require at least one test input asset.", path + ".testInputs", modelId: modelId);
                if (entry.Status == ModelCatalogStatus.Supported && string.IsNullOrWhiteSpace(entry.ExpectedResultAssetId)) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported entries require a reproducible expected-result asset.", path + ".expectedResultAssetId", modelId: modelId);
                if (!string.IsNullOrWhiteSpace(entry.ExpectedResultAssetId) && normalizedTests.All(asset => !string.Equals(asset.AssetId, entry.ExpectedResultAssetId, StringComparison.OrdinalIgnoreCase) || asset.Kind != ModelCatalogAssetKind.TestExpected)) Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "expectedResultAssetId must reference a testExpected asset.", path + ".expectedResultAssetId", modelId: modelId, assetId: entry.ExpectedResultAssetId);
                if (entry.Status == ModelCatalogStatus.Supported && string.IsNullOrWhiteSpace(entry.DocumentationPath)) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Supported entries require a generated documentation-table source path.", path + ".documentationPath", modelId: modelId);
                OptionalPath(entry.DocumentationPath, path + ".documentationPath", diagnostics, modelId);
                normalizedEntries.Add(new ModelCatalogEntry(modelId, entry.Name, family, task, entry.ModelVersion, entry.Status, entry.Description, entry.Source, entry.Release, normalizedArtifacts, normalizedTests, entry.ExpectedResultAssetId, entry.DocumentationPath));
            }

            if (diagnostics.Count > 0 || version == null || revision == null || generatedAt == null || sourceRepository == null)
            {
                Throw("The model catalog is invalid.", diagnostics.Count == 0 ? new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "Catalog validation failed.", "$") } : diagnostics);
            }

            var normalizedDocument = new ModelCatalogDocument(version!.ToString(2), generatedAt, revision, sourceRepository, normalizedEntries);
            return new ValidatedModelCatalog(normalizedDocument, version);
        }

        private static Version? ValidateVersion(string? value, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value) || !Version.TryParse(value, out Version? version) || version.Build >= 0 || version.Revision >= 0)
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "schemaVersion must use major.minor form.", "$.schemaVersion");
                return null;
            }

            if (version.Major != options.SupportedSchemaMajor || (version.Minor > options.SupportedSchemaMinor && !options.AllowNewerMinorVersions)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "The catalog schema version is unsupported.", "$.schemaVersion");
            return version;
        }

        private static string? ValidateTimestamp(string? value, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset timestamp))
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "generatedAt must be an ISO-8601 timestamp.", "$.generatedAt");
                return null;
            }

            return timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static Uri? ValidateRepositoryUri(Uri? value, string path, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (value == null || !value.IsAbsoluteUri || value.Scheme != Uri.UriSchemeHttps || !string.Equals(value.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "The source repository must be an absolute HTTPS GitHub URI.", path, uri: value);
                return null;
            }

            return value;
        }

        private static void ValidateSource(ModelSourceDocument? source, string path, ModelCatalogStatus status, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (source == null)
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.LicenseRejected, "Source and license metadata are required.", path);
                return;
            }

            ValidateHttp(source.SourceUrl, path + ".sourceUrl", diagnostics);
            if (!string.IsNullOrWhiteSpace(source.ProjectUrl)) ValidateHttp(source.ProjectUrl, path + ".projectUrl", diagnostics);
            RequiredString(source.Revision, path + ".revision", options, diagnostics);
            RequiredString(source.Author, path + ".author", options, diagnostics);
            if (string.IsNullOrWhiteSpace(source.LicenseExpression) && string.IsNullOrWhiteSpace(source.LicenseFile)) Add(diagnostics, ModelFactoryDiagnosticCodes.LicenseRejected, "A license expression or license file is required.", path);
            if (status != ModelCatalogStatus.External && !source.RedistributionAllowed) Add(diagnostics, ModelFactoryDiagnosticCodes.LicenseRejected, "Redistribution must be explicitly allowed for downloadable catalog publication.", path + ".redistributionAllowed");
            if (!string.IsNullOrWhiteSpace(source.LicenseFile)) NormalizePath(source.LicenseFile, path + ".licenseFile", diagnostics);
        }

        private static void ValidateRelease(ModelCatalogRelease? release, string path, ModelCatalogStatus status, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (release == null)
            {
                if (status != ModelCatalogStatus.External) Add(diagnostics, ModelFactoryDiagnosticCodes.MutableReleaseTag, "Supported and Preview entries require versioned Release metadata.", path);
                return;
            }

            RequiredPlain(release.Owner, path + ".owner", diagnostics);
            RequiredPlain(release.Repository, path + ".repository", diagnostics);
            if (!string.IsNullOrWhiteSpace(release.Owner) && !IsGitHubSegment(release.Owner!)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "GitHub owner contains unsupported characters.", path + ".owner");
            if (!string.IsNullOrWhiteSpace(release.Repository) && !IsGitHubSegment(release.Repository!)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "GitHub repository contains unsupported characters.", path + ".repository");
            if (string.IsNullOrWhiteSpace(release.Tag) || !ReleaseTagPattern.IsMatch(release.Tag) || string.Equals(release.Tag, "latest", StringComparison.OrdinalIgnoreCase)) Add(diagnostics, ModelFactoryDiagnosticCodes.MutableReleaseTag, "Release tag must match a models-collection.revision form and must not be a floating alias.", path + ".tag");
            RequiredPlain(release.Commit, path + ".commit", diagnostics);
        }

        private static void ValidateStatus(ModelCatalogStatus status, string path, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (!Enum.IsDefined(typeof(ModelCatalogStatus), status)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Catalog status is invalid.", path);
            if (!options.AllowPreviewAndExternal && status != ModelCatalogStatus.Supported) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "Preview and External entries are disabled by validation options.", path);
        }

        private static void ValidateBackends(IReadOnlyList<string> values, string path, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics, string? modelId, string? artifactId)
        {
            if (values.Count == 0) Add(diagnostics, ModelFactoryDiagnosticCodes.AdmissionRejected, "At least one compatible backend is required.", path, modelId: modelId, artifactId: artifactId);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values)
            {
                RequiredIdentifier(value, path, options, diagnostics);
                if (!seen.Add(value)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Compatible backend identifiers must be unique.", path, modelId: modelId, artifactId: artifactId);
            }
        }

        private static void ValidateAsset(ModelCatalogAsset asset, string path, ModelCatalogEntry entry, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics, string? modelId, string? artifactId = null, string? assetId = null)
        {
            if (!Enum.IsDefined(typeof(ModelCatalogAssetKind), asset.Kind)) Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "Asset kind is invalid.", path + ".kind", modelId: modelId, artifactId: artifactId, assetId: assetId);
            if (asset.Size < 0) Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "Asset size must be non-negative.", path + ".size", modelId: modelId, artifactId: artifactId, assetId: assetId);
            OptionalString(asset.MediaType, path + ".mediaType", options, diagnostics);
        }

        private static Uri? ValidateAssetUri(Uri? value, string path, ModelCatalogRelease? release, string? tag, List<ModelFactoryDiagnostic> diagnostics, string? modelId, string? artifactId = null, string? assetId = null)
        {
            if (value == null || !value.IsAbsoluteUri || value.Scheme != Uri.UriSchemeHttps || !string.Equals(value.Host, "github.com", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(value.Query) || !string.IsNullOrEmpty(value.Fragment) || value.UserInfo.Length != 0)
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "Asset downloadUrl must be an absolute HTTPS GitHub Release URL without query, fragment, or credentials.", path, modelId: modelId, artifactId: artifactId, assetId: assetId, uri: value);
                return null;
            }

            if (release == null || string.IsNullOrWhiteSpace(tag)) return value;
            string expectedPrefix = "/" + release.Owner + "/" + release.Repository + "/releases/download/" + tag + "/";
            string actual = Uri.UnescapeDataString(value.AbsolutePath);
            if (!actual.StartsWith(expectedPrefix, StringComparison.Ordinal) || actual.Substring(expectedPrefix.Length).Length == 0)
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.MutableReleaseTag, "Asset URL does not point to the recorded versioned GitHub Release tag.", path, modelId: modelId, artifactId: artifactId, assetId: assetId, uri: value);
            }

            return value;
        }

        private static string? NormalizePath(string? value, string path, List<ModelFactoryDiagnostic> diagnostics, string? modelId = null, string? artifactId = null, string? assetId = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "A safe relative path is required.", path, modelId: modelId, artifactId: artifactId, assetId: assetId);
                return null;
            }

            try { return ModelPackagePath.NormalizeRelativePath(value!); }
            catch (ArgumentException exception)
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, exception.Message, path, modelId: modelId, artifactId: artifactId, assetId: assetId);
                return null;
            }
        }

        private static string? NormalizeHash(string? value, string path, List<ModelFactoryDiagnostic> diagnostics, string? modelId = null, string? artifactId = null, string? assetId = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "A SHA256 value is required.", path, modelId: modelId, artifactId: artifactId, assetId: assetId);
                return null;
            }

            try { return ModelFileIntegrity.NormalizeSha256(value!); }
            catch (ArgumentException exception)
            {
                Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, exception.Message, path, modelId: modelId, artifactId: artifactId, assetId: assetId);
                return null;
            }
        }

        private static void OptionalPath(string? value, string path, List<ModelFactoryDiagnostic> diagnostics, string? modelId)
        {
            if (!string.IsNullOrWhiteSpace(value)) NormalizePath(value, path, diagnostics, modelId);
        }

        private static bool IsTensorRtEngine(ModelCatalogArtifact artifact)
        {
            if (string.Equals(artifact.Format, "tensorrt-engine", StringComparison.OrdinalIgnoreCase) || string.Equals(artifact.Format, "tensorrt", StringComparison.OrdinalIgnoreCase)) return true;
            return artifact.Assets.Any(asset => asset.RelativePath != null && (asset.RelativePath.EndsWith(".engine", StringComparison.OrdinalIgnoreCase) || asset.RelativePath.EndsWith(".plan", StringComparison.OrdinalIgnoreCase)));
        }

        private static string? RequiredIdentifier(string? value, string path, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics, string? modelId = null)
        {
            RequiredString(value, path, options, diagnostics, modelId);
            if (!string.IsNullOrWhiteSpace(value) && !IsIdentifier(value!)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Identifier contains unsupported characters.", path, modelId: modelId);
            return string.IsNullOrWhiteSpace(value) ? null : value!.ToLowerInvariant();
        }

        private static void OptionalIdentifier(string? value, string path, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                OptionalString(value, path, options, diagnostics);
                if (!IsIdentifier(value!)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "Identifier contains unsupported characters.", path);
            }
        }

        private static bool IsIdentifier(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '.' || character == '-' || character == '_' || character == '/')) return false;
            }

            return value.Length > 0;
        }

        private static string? RequiredString(string? value, string path, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics, string? modelId = null)
        {
            if (string.IsNullOrWhiteSpace(value)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "A non-empty value is required.", path, modelId: modelId);
            else if (value!.Length > options.MaximumStringLength) Add(diagnostics, ModelFactoryDiagnosticCodes.LimitExceeded, "String length exceeds the configured limit.", path, modelId: modelId);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static void OptionalString(string? value, string path, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (value != null && value.Length > options.MaximumStringLength) Add(diagnostics, ModelFactoryDiagnosticCodes.LimitExceeded, "String length exceeds the configured limit.", path);
        }

        private static void RequiredPlain(string? value, string path, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value)) Add(diagnostics, ModelFactoryDiagnosticCodes.CatalogInvalid, "A non-empty value is required.", path);
        }

        private static bool IsGitHubSegment(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '.' || character == '-' || character == '_')) return false;
            }

            return value.Length > 0;
        }

        private static void ValidateHttp(string? value, string path, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) Add(diagnostics, ModelFactoryDiagnosticCodes.AssetInvalid, "The URL must be absolute HTTP or HTTPS.", path, uri: uri);
        }

        private static void Add(List<ModelFactoryDiagnostic> diagnostics, string code, string message, string? path = null, string? modelId = null, string? artifactId = null, string? assetId = null, Uri? uri = null, string? filePath = null)
        {
            diagnostics.Add(new ModelFactoryDiagnostic(code, message, path, modelId, artifactId, assetId, uri, filePath));
        }

        private static void Throw(string message, IEnumerable<ModelFactoryDiagnostic> diagnostics)
        {
            throw new ModelFactoryException(message, diagnostics);
        }
    }
}
