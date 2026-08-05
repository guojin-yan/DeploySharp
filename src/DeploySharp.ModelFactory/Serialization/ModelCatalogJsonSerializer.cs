using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelFactory.Serialization;
using JYPPX.DeploySharp.ModelPack.Json;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Reads and writes strict deterministic ModelFactory catalog JSON. / 读取和写入严格且确定性的 ModelFactory 目录 JSON。</summary>
    public static class ModelCatalogJsonSerializer
    {
        /// <summary>Deserializes and validates a catalog JSON string. / 反序列化并验证目录 JSON 字符串。</summary>
        public static ValidatedModelCatalog Deserialize(string json, ModelCatalogValidationOptions? options = null)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return DeserializeBytes(Encoding.UTF8.GetBytes(json), options ?? ModelCatalogValidationOptions.Default);
        }

        /// <summary>Deserializes and validates catalog JSON from a stream without taking ownership. / 从流反序列化并验证目录 JSON，且不接管流所有权。</summary>
        public static ValidatedModelCatalog Deserialize(Stream stream, ModelCatalogValidationOptions? options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            ModelCatalogValidationOptions limits = options ?? ModelCatalogValidationOptions.Default;
            return DeserializeBytes(ReadBounded(stream, limits.MaximumJsonBytes, CancellationToken.None), limits);
        }

        /// <summary>Asynchronously deserializes and validates catalog JSON without taking stream ownership. / 异步反序列化并验证目录 JSON，且不接管流所有权。</summary>
        public static async Task<ValidatedModelCatalog> DeserializeAsync(Stream stream, ModelCatalogValidationOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            ModelCatalogValidationOptions limits = options ?? ModelCatalogValidationOptions.Default;
            byte[] bytes = await ReadBoundedAsync(stream, limits.MaximumJsonBytes, cancellationToken).ConfigureAwait(false);
            return DeserializeBytes(bytes, limits);
        }

        /// <summary>Serializes a validated catalog with deterministic property order. / 以确定性属性顺序序列化已验证目录。</summary>
        public static string Serialize(ValidatedModelCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(ToDto(catalog.Document), CreateOptions(ModelCatalogValidationOptions.Default)));
        }

        /// <summary>Writes deterministic catalog JSON without taking stream ownership. / 写入确定性目录 JSON，且不接管流所有权。</summary>
        public static void Serialize(Stream stream, ValidatedModelCatalog catalog)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] bytes = Encoding.UTF8.GetBytes(Serialize(catalog ?? throw new ArgumentNullException(nameof(catalog))));
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>Asynchronously writes deterministic catalog JSON without taking stream ownership. / 异步写入确定性目录 JSON，且不接管流所有权。</summary>
        public static async Task SerializeAsync(Stream stream, ValidatedModelCatalog catalog, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] bytes = Encoding.UTF8.GetBytes(Serialize(catalog ?? throw new ArgumentNullException(nameof(catalog))));
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
        }

        private static ValidatedModelCatalog DeserializeBytes(byte[] bytes, ModelCatalogValidationOptions limits)
        {
            if (bytes.LongLength > limits.MaximumJsonBytes) Throw("Catalog JSON exceeds the configured byte limit.", new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.LimitExceeded, "Catalog JSON exceeds the configured byte limit.", "$"));
            ValidateRawJson(bytes, limits);
            CatalogDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<CatalogDto>(bytes, CreateOptions(limits));
            }
            catch (JsonException exception)
            {
                throw new ModelFactoryException("Catalog JSON cannot be deserialized.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, exception.Message, exception.Path) }, exception, exception.ToString());
            }

            if (dto == null) Throw("Catalog JSON root is null.", new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "A JSON object root is required.", "$"));
            ModelCatalogDocument document = FromDto(dto!);
            return ModelCatalogValidator.Validate(document, limits);
        }

        private static ModelCatalogDocument FromDto(CatalogDto dto)
        {
            var diagnostics = new List<ModelFactoryDiagnostic>();
            RejectUnknown(dto.Unknown, "$", diagnostics);
            if (dto.Entries == null) diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "entries property is required.", "$.entries"));
            Uri? repositoryUri = ParseUri(dto.SourceRepository, "$.sourceRepository", diagnostics);
            var entries = new List<ModelCatalogEntry>();
            if (dto.Entries != null)
            {
                for (int index = 0; index < dto.Entries.Count; index++)
                {
                    EntryDto? entry = dto.Entries[index];
                    string path = "$.entries[" + index + "]";
                    if (entry == null)
                    {
                        diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "Entry items cannot be null.", path));
                        continue;
                    }

                    RejectUnknown(entry.Unknown, path, diagnostics);
                    ModelCatalogStatus status = ParseStatus(entry.Status, path + ".status", diagnostics);
                    ModelSourceDocument? source = FromSource(entry.Source, path + ".source", diagnostics);
                    ModelCatalogRelease? release = FromRelease(entry.Release, path + ".release", diagnostics);
                    var artifacts = new List<ModelCatalogArtifact>();
                    if (entry.Artifacts == null) diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "artifacts property is required; use an empty array for External records.", path + ".artifacts", modelId: entry.ModelId));
                    else
                    {
                        for (int artifactIndex = 0; artifactIndex < entry.Artifacts.Count; artifactIndex++)
                        {
                            ArtifactDto? artifact = entry.Artifacts[artifactIndex];
                            string artifactPath = path + ".artifacts[" + artifactIndex + "]";
                            if (artifact == null)
                            {
                                diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "Artifact items cannot be null.", artifactPath, modelId: entry.ModelId));
                                continue;
                            }

                            RejectUnknown(artifact.Unknown, artifactPath, diagnostics);
                            if (!artifact.Portable.HasValue) diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "portable property is required.", artifactPath + ".portable", entry.ModelId, artifact.ArtifactId));
                            ModelCatalogConversion? conversion = FromConversion(artifact.Conversion, artifactPath + ".conversion", diagnostics);
                            List<ModelCatalogAsset> assets = FromAssets(artifact.Assets, artifactPath + ".assets", diagnostics, entry.ModelId, artifact.ArtifactId);
                            artifacts.Add(new ModelCatalogArtifact(artifact.ArtifactId, artifact.Format, artifact.CompatibleBackends, artifact.Precision, artifact.Quantization, artifact.Portable.GetValueOrDefault(), artifact.ManifestAssetId, assets, conversion));
                        }
                    }

                    List<ModelCatalogAsset> testInputs = FromAssets(entry.TestInputs, path + ".testInputs", diagnostics, entry.ModelId, null);
                    entries.Add(new ModelCatalogEntry(entry.ModelId, entry.Name, entry.Family, entry.Task, entry.ModelVersion, status, entry.Description, source, release, artifacts, testInputs, entry.ExpectedResultAssetId, entry.DocumentationPath));
                }
            }

            if (diagnostics.Count > 0) throw new ModelFactoryException("Catalog JSON contains unsupported or missing structure.", diagnostics);
            return new ModelCatalogDocument(dto.SchemaVersion, dto.GeneratedAt, dto.CatalogRevision, repositoryUri, entries);
        }

        private static ModelSourceDocument? FromSource(SourceDto? source, string path, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (source == null) return null;
            RejectUnknown(source.Unknown, path, diagnostics);
            if (!source.RedistributionAllowed.HasValue) diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "redistributionAllowed property is required.", path + ".redistributionAllowed"));
            return new ModelSourceDocument(source.SourceUrl, source.ProjectUrl, source.Revision, source.Author, source.Copyright, source.LicenseExpression, source.LicenseFile, source.RedistributionAllowed.GetValueOrDefault());
        }

        private static ModelCatalogRelease? FromRelease(ReleaseDto? release, string path, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (release == null) return null;
            RejectUnknown(release.Unknown, path, diagnostics);
            return new ModelCatalogRelease(release.Owner, release.Repository, release.Tag, release.Commit);
        }

        private static ModelCatalogConversion? FromConversion(ConversionDto? conversion, string path, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (conversion == null) return null;
            RejectUnknown(conversion.Unknown, path, diagnostics);
            return new ModelCatalogConversion(conversion.Exporter, conversion.ExporterVersion, conversion.SourceRevision, conversion.Notes);
        }

        private static List<ModelCatalogAsset> FromAssets(List<AssetDto?>? values, string path, List<ModelFactoryDiagnostic> diagnostics, string? modelId, string? artifactId)
        {
            var assets = new List<ModelCatalogAsset>();
            if (values == null)
            {
                diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "Asset collection property is required; use an empty array when none applies.", path, modelId, artifactId));
                return assets;
            }

            for (int index = 0; index < values.Count; index++)
            {
                AssetDto? asset = values[index];
                string assetPath = path + "[" + index + "]";
                if (asset == null)
                {
                    diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "Asset items cannot be null.", assetPath, modelId, artifactId));
                    continue;
                }

                RejectUnknown(asset.Unknown, assetPath, diagnostics);
                ModelCatalogAssetKind kind = ParseAssetKind(asset.Kind, assetPath + ".kind", diagnostics, modelId, artifactId, asset.AssetId);
                Uri? uri = ParseUri(asset.DownloadUrl, assetPath + ".downloadUrl", diagnostics, modelId, artifactId, asset.AssetId);
                assets.Add(new ModelCatalogAsset(asset.AssetId, kind, asset.ReleaseTag, uri, asset.RelativePath, asset.Size, asset.Sha256, asset.MediaType, asset.LicenseExpression));
            }

            return assets;
        }

        private static ModelCatalogStatus ParseStatus(string? value, string path, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (value == "supported") return ModelCatalogStatus.Supported;
            if (value == "preview") return ModelCatalogStatus.Preview;
            if (value == "external") return ModelCatalogStatus.External;
            diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "status must be supported, preview, or external.", path));
            return ModelCatalogStatus.External;
        }

        private static ModelCatalogAssetKind ParseAssetKind(string? value, string path, List<ModelFactoryDiagnostic> diagnostics, string? modelId, string? artifactId, string? assetId)
        {
            switch (value)
            {
                case "manifest": return ModelCatalogAssetKind.Manifest;
                case "model": return ModelCatalogAssetKind.Model;
                case "testInput": return ModelCatalogAssetKind.TestInput;
                case "testExpected": return ModelCatalogAssetKind.TestExpected;
                case "license": return ModelCatalogAssetKind.License;
                case "other": return ModelCatalogAssetKind.Other;
                default:
                    diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "Asset kind is invalid.", path, modelId, artifactId, assetId));
                    return ModelCatalogAssetKind.Other;
            }
        }

        private static Uri? ParseUri(string? value, string path, List<ModelFactoryDiagnostic> diagnostics, string? modelId = null, string? artifactId = null, string? assetId = null)
        {
            if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            {
                diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.AssetInvalid, "An absolute URI is required.", path, modelId, artifactId, assetId));
                return null;
            }

            return uri;
        }

        private static CatalogDto ToDto(ModelCatalogDocument document)
        {
            var dto = new CatalogDto
            {
                SchemaVersion = document.SchemaVersion,
                GeneratedAt = document.GeneratedAt,
                CatalogRevision = document.CatalogRevision,
                SourceRepository = document.SourceRepository?.AbsoluteUri,
                Entries = new List<EntryDto?>()
            };
            foreach (ModelCatalogEntry entry in document.Entries)
            {
                var entryDto = new EntryDto
                {
                    ModelId = entry.ModelId,
                    Name = entry.Name,
                    Family = entry.Family,
                    Task = entry.Task,
                    ModelVersion = entry.ModelVersion,
                    Status = FormatStatus(entry.Status),
                    Description = entry.Description,
                    Source = entry.Source == null ? null : new SourceDto { SourceUrl = entry.Source.SourceUrl, ProjectUrl = entry.Source.ProjectUrl, Revision = entry.Source.Revision, Author = entry.Source.Author, Copyright = entry.Source.Copyright, LicenseExpression = entry.Source.LicenseExpression, LicenseFile = entry.Source.LicenseFile, RedistributionAllowed = entry.Source.RedistributionAllowed },
                    Release = entry.Release == null ? null : new ReleaseDto { Owner = entry.Release.Owner, Repository = entry.Release.Repository, Tag = entry.Release.Tag, Commit = entry.Release.Commit },
                    Artifacts = new List<ArtifactDto?>(),
                    TestInputs = new List<AssetDto?>(),
                    ExpectedResultAssetId = entry.ExpectedResultAssetId,
                    DocumentationPath = entry.DocumentationPath
                };
                foreach (ModelCatalogArtifact artifact in entry.Artifacts)
                {
                    var artifactDto = new ArtifactDto
                    {
                        ArtifactId = artifact.ArtifactId,
                        Format = artifact.Format,
                        CompatibleBackends = new List<string>(artifact.CompatibleBackends),
                        Precision = artifact.Precision,
                        Quantization = artifact.Quantization,
                        Portable = artifact.Portable,
                        ManifestAssetId = artifact.ManifestAssetId,
                        Assets = new List<AssetDto?>(),
                        Conversion = artifact.Conversion == null ? null : new ConversionDto { Exporter = artifact.Conversion.Exporter, ExporterVersion = artifact.Conversion.ExporterVersion, SourceRevision = artifact.Conversion.SourceRevision, Notes = artifact.Conversion.Notes }
                    };
                    foreach (ModelCatalogAsset asset in artifact.Assets) artifactDto.Assets.Add(ToDto(asset));
                    entryDto.Artifacts.Add(artifactDto);
                }

                foreach (ModelCatalogAsset testInput in entry.TestInputs) entryDto.TestInputs.Add(ToDto(testInput));
                dto.Entries.Add(entryDto);
            }

            return dto;
        }

        private static AssetDto ToDto(ModelCatalogAsset asset)
        {
            return new AssetDto { AssetId = asset.AssetId, Kind = FormatAssetKind(asset.Kind), ReleaseTag = asset.ReleaseTag, DownloadUrl = asset.DownloadUri?.AbsoluteUri, RelativePath = asset.RelativePath, Size = asset.Size, Sha256 = asset.Sha256, MediaType = asset.MediaType, LicenseExpression = asset.LicenseExpression };
        }

        private static string FormatStatus(ModelCatalogStatus status)
        {
            return status == ModelCatalogStatus.Supported ? "supported" : status == ModelCatalogStatus.Preview ? "preview" : "external";
        }

        private static string FormatAssetKind(ModelCatalogAssetKind kind)
        {
            switch (kind)
            {
                case ModelCatalogAssetKind.Manifest: return "manifest";
                case ModelCatalogAssetKind.Model: return "model";
                case ModelCatalogAssetKind.TestInput: return "testInput";
                case ModelCatalogAssetKind.TestExpected: return "testExpected";
                case ModelCatalogAssetKind.License: return "license";
                default: return "other";
            }
        }

        private static JsonSerializerOptions CreateOptions(ModelCatalogValidationOptions options)
        {
            return new JsonSerializerOptions { AllowTrailingCommas = false, MaxDepth = options.MaximumJsonDepth, PropertyNameCaseInsensitive = false, ReadCommentHandling = JsonCommentHandling.Disallow, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true };
        }

        private static void ValidateRawJson(byte[] bytes, ModelCatalogValidationOptions options)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = options.MaximumJsonDepth }))
                {
                    var diagnostics = new List<ModelFactoryDiagnostic>();
                    ValidateElement(document.RootElement, "$", options, diagnostics);
                    if (diagnostics.Count > 0) throw new ModelFactoryException("Catalog JSON structure is invalid.", diagnostics);
                }
            }
            catch (ModelFactoryException) { throw; }
            catch (JsonException exception)
            {
                throw new ModelFactoryException("Catalog JSON syntax is invalid.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, exception.Message, exception.Path) }, exception, exception.ToString());
            }
        }

        private static void ValidateElement(JsonElement element, string path, ModelCatalogValidationOptions options, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string child = path + "." + property.Name;
                    if (!names.Add(property.Name)) diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "JSON property names must be unique.", child));
                    if (property.Name.Length > options.MaximumStringLength) diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.LimitExceeded, "JSON property name exceeds the configured limit.", child));
                    ValidateElement(property.Value, child, options, diagnostics);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement child in element.EnumerateArray()) ValidateElement(child, path + "[" + index++ + "]", options, diagnostics);
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                string? value = element.GetString();
                if (value != null && value.Length > options.MaximumStringLength) diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.LimitExceeded, "JSON string exceeds the configured limit.", path));
            }
        }

        private static void RejectUnknown(Dictionary<string, JsonElement>? values, string path, List<ModelFactoryDiagnostic> diagnostics)
        {
            if (values == null) return;
            foreach (string name in values.Keys) diagnostics.Add(new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CatalogInvalid, "Unknown property '" + name + "' is not allowed.", path + "." + name));
        }

        private static byte[] ReadBounded(Stream stream, long maximumBytes, CancellationToken cancellationToken)
        {
            using (var output = new MemoryStream())
            {
                var buffer = new byte[81920];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    if (output.Length + read > maximumBytes) Throw("Catalog JSON exceeds the configured byte limit.", new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.LimitExceeded, "Catalog JSON exceeds the configured byte limit.", "$"));
                    output.Write(buffer, 0, read);
                }

                return output.ToArray();
            }
        }

        private static async Task<byte[]> ReadBoundedAsync(Stream stream, long maximumBytes, CancellationToken cancellationToken)
        {
            using (var output = new MemoryStream())
            {
                var buffer = new byte[81920];
                while (true)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    if (output.Length + read > maximumBytes) Throw("Catalog JSON exceeds the configured byte limit.", new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.LimitExceeded, "Catalog JSON exceeds the configured byte limit.", "$"));
                    await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                }

                return output.ToArray();
            }
        }

        private static void Throw(string message, ModelFactoryDiagnostic diagnostic)
        {
            throw new ModelFactoryException(message, new[] { diagnostic });
        }
    }
}
