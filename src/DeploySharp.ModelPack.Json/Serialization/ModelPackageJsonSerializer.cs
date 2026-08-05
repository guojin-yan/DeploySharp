using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelPack.Json.Serialization;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Reads and writes deterministic UTF-8 ModelPack JSON documents. / 读取和写入确定性的 UTF-8 ModelPack JSON 文档。</summary>
    public static class ModelPackageJsonSerializer
    {
        /// <summary>Deserializes and validates a JSON string. / 反序列化并验证 JSON 字符串。</summary>
        public static ValidatedModelPackage Deserialize(string json, ModelPackageValidationOptions? options = null)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            ModelPackageValidationOptions limits = options ?? ModelPackageValidationOptions.Default;
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            return DeserializeBytes(bytes, limits);
        }

        /// <summary>Deserializes and validates UTF-8 JSON from a stream without taking stream ownership. / 从流反序列化并验证 UTF-8 JSON，且不接管流所有权。</summary>
        public static ValidatedModelPackage Deserialize(Stream stream, ModelPackageValidationOptions? options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            ModelPackageValidationOptions limits = options ?? ModelPackageValidationOptions.Default;
            return DeserializeBytes(ReadBounded(stream, limits.MaximumJsonBytes, CancellationToken.None), limits);
        }

        /// <summary>Asynchronously deserializes and validates UTF-8 JSON without taking stream ownership. / 异步反序列化并验证 UTF-8 JSON，且不接管流所有权。</summary>
        public static async Task<ValidatedModelPackage> DeserializeAsync(Stream stream, ModelPackageValidationOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            ModelPackageValidationOptions limits = options ?? ModelPackageValidationOptions.Default;
            byte[] bytes = await ReadBoundedAsync(stream, limits.MaximumJsonBytes, cancellationToken).ConfigureAwait(false);
            return DeserializeBytes(bytes, limits);
        }

        /// <summary>Serializes a validated manifest using deterministic property and extension ordering. / 使用确定性的属性和扩展顺序序列化已验证清单。</summary>
        public static string Serialize(ValidatedModelPackage package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            return Encoding.UTF8.GetString(SerializeBytes(package));
        }

        /// <summary>Writes deterministic UTF-8 JSON without taking stream ownership. / 写入确定性 UTF-8 JSON，且不接管流所有权。</summary>
        public static void Serialize(Stream stream, ValidatedModelPackage package)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] bytes = SerializeBytes(package ?? throw new ArgumentNullException(nameof(package)));
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>Asynchronously writes deterministic UTF-8 JSON without taking stream ownership. / 异步写入确定性 UTF-8 JSON，且不接管流所有权。</summary>
        public static async Task SerializeAsync(Stream stream, ValidatedModelPackage package, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] bytes = SerializeBytes(package ?? throw new ArgumentNullException(nameof(package)));
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
        }

        private static ValidatedModelPackage DeserializeBytes(byte[] bytes, ModelPackageValidationOptions limits)
        {
            if (bytes.LongLength > limits.MaximumJsonBytes)
            {
                ThrowDiagnostics("The ModelPack JSON exceeds the configured byte limit.", new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "JSON byte length exceeds the configured limit.", "$"));
            }

            ValidateRawJson(bytes, limits);
            ManifestDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<ManifestDto>(bytes, CreateOptions(limits));
            }
            catch (JsonException exception)
            {
                throw new ModelPackageValidationException(
                    "The ModelPack JSON cannot be deserialized.",
                    new[] { new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidJson, exception.Message, exception.Path) },
                    exception,
                    technicalDetails: exception.ToString());
            }

            if (dto == null)
            {
                ThrowDiagnostics("The ModelPack JSON root is null.", new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "A JSON object root is required.", "$"));
            }

            ModelPackageDocument document = ConvertFromDto(dto!, limits);
            return ModelPackageValidator.Validate(document, limits);
        }

        private static byte[] SerializeBytes(ValidatedModelPackage package)
        {
            ManifestDto dto = ConvertToDto(package.Document);
            return JsonSerializer.SerializeToUtf8Bytes(dto, CreateOptions(ModelPackageValidationOptions.Default));
        }

        private static JsonSerializerOptions CreateOptions(ModelPackageValidationOptions limits)
        {
            return new JsonSerializerOptions
            {
                AllowTrailingCommas = false,
                MaxDepth = limits.MaximumJsonDepth,
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Disallow,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };
        }

        private static void ValidateRawJson(byte[] bytes, ModelPackageValidationOptions limits)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = limits.MaximumJsonDepth }))
                {
                    var diagnostics = new List<ModelPackageDiagnostic>();
                    ValidateElement(document.RootElement, "$", limits, diagnostics);
                    if (diagnostics.Count > 0) throw new ModelPackageValidationException("The ModelPack JSON structure is invalid.", diagnostics);
                }
            }
            catch (ModelPackageValidationException)
            {
                throw;
            }
            catch (JsonException exception)
            {
                throw new ModelPackageValidationException(
                    "The ModelPack JSON syntax is invalid.",
                    new[] { new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidJson, exception.Message, exception.Path) },
                    exception,
                    technicalDetails: exception.ToString());
            }
        }

        private static void ValidateElement(JsonElement element, string path, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string childPath = path + "." + property.Name;
                    if (!names.Add(property.Name)) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Duplicate, "JSON object property names must be unique.", childPath));
                    if (property.Name.Length > limits.MaximumStringLength) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "JSON property name exceeds the configured string limit.", childPath));
                    ValidateElement(property.Value, childPath, limits, diagnostics);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray()) ValidateElement(item, path + "[" + index++ + "]", limits, diagnostics);
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                string? value = element.GetString();
                if (value != null && value.Length > limits.MaximumStringLength) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "JSON string exceeds the configured string limit.", path));
            }
        }

        private static ModelPackageDocument ConvertFromDto(ManifestDto dto, ModelPackageValidationOptions limits)
        {
            var diagnostics = new List<ModelPackageDiagnostic>();
            RejectUnknown(dto.Unknown, "$", diagnostics);
            if (dto.Inputs == null) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "inputs property is required; use an empty array when no tensor signature applies.", "$.inputs"));
            if (dto.Outputs == null) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "outputs property is required; use an empty array when no tensor signature applies.", "$.outputs"));
            if (dto.Artifacts == null) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "artifacts property is required.", "$.artifacts"));

            ModelExporterDocument? exporter = null;
            if (dto.Exporter != null)
            {
                RejectUnknown(dto.Exporter.Unknown, "$.exporter", diagnostics);
                exporter = new ModelExporterDocument(dto.Exporter.Name, dto.Exporter.Version, dto.Exporter.SourceRevision);
            }

            ModelSourceDocument? source = null;
            if (dto.Source != null)
            {
                RejectUnknown(dto.Source.Unknown, "$.source", diagnostics);
                if (!dto.Source.RedistributionAllowed.HasValue) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "redistributionAllowed property is required.", "$.source.redistributionAllowed"));
                source = new ModelSourceDocument(dto.Source.SourceUrl, dto.Source.ProjectUrl, dto.Source.Revision, dto.Source.Author, dto.Source.Copyright, dto.Source.LicenseExpression, dto.Source.LicenseFile, dto.Source.RedistributionAllowed.GetValueOrDefault());
            }

            var inputs = ConvertTensors(dto.Inputs, "$.inputs", diagnostics);
            var outputs = ConvertTensors(dto.Outputs, "$.outputs", diagnostics);
            var artifacts = new List<ModelArtifactDocument>();
            if (dto.Artifacts != null)
            {
                for (int index = 0; index < dto.Artifacts.Count; index++)
                {
                    ArtifactDto? artifact = dto.Artifacts[index];
                    string path = "$.artifacts[" + index + "]";
                    if (artifact == null)
                    {
                        diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "Artifact items cannot be null.", path));
                        continue;
                    }

                    RejectUnknown(artifact.Unknown, path, diagnostics);
                    if (!artifact.Portable.HasValue) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "portable property is required.", path + ".portable", artifact.ArtifactId));
                    ModelArtifactLocationKind locationKind = ParseLocationKind(artifact.LocationKind, path + ".locationKind", diagnostics);
                    var files = new List<ModelFileDocument>();
                    if (artifact.Files == null)
                    {
                        diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "files property is required.", path + ".files", artifact.ArtifactId));
                    }
                    else
                    {
                        for (int fileIndex = 0; fileIndex < artifact.Files.Count; fileIndex++)
                        {
                            FileDto? file = artifact.Files[fileIndex];
                            string filePath = path + ".files[" + fileIndex + "]";
                            if (file == null)
                            {
                                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "File items cannot be null.", filePath, artifact.ArtifactId));
                                continue;
                            }

                            RejectUnknown(file.Unknown, filePath, diagnostics);
                            ModelFileRole role = ParseFileRole(file.Role, filePath + ".role", artifact.ArtifactId, file.RelativePath, diagnostics);
                            files.Add(new ModelFileDocument(file.RelativePath, file.Sha256, file.Size, file.MediaType, role));
                        }
                    }

                    artifacts.Add(new ModelArtifactDocument(
                        artifact.ArtifactId,
                        artifact.Format,
                        locationKind,
                        artifact.Entrypoint,
                        artifact.CompatibleBackends,
                        files,
                        artifact.Precision,
                        artifact.Quantization,
                        artifact.Opset,
                        artifact.Portable.GetValueOrDefault(),
                        artifact.MinimumBackendVersion,
                        artifact.MinimumRuntimeVersion,
                        artifact.Extensions));
                }
            }

            if (diagnostics.Count > 0) throw new ModelPackageValidationException("The ModelPack JSON contains unsupported or missing structure.", diagnostics);
            return new ModelPackageDocument(dto.SchemaVersion, dto.ModelId, dto.Name, dto.Family, dto.Task, dto.ModelVersion, exporter, source, dto.GeneratedAt, dto.ProfileId, inputs, outputs, artifacts, dto.Extensions);
        }

        private static List<ModelTensorSignatureDocument> ConvertTensors(List<TensorDto?>? values, string path, List<ModelPackageDiagnostic> diagnostics)
        {
            var tensors = new List<ModelTensorSignatureDocument>();
            if (values == null) return tensors;
            for (int index = 0; index < values.Count; index++)
            {
                TensorDto? tensor = values[index];
                string itemPath = path + "[" + index + "]";
                if (tensor == null)
                {
                    diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "Tensor items cannot be null.", itemPath));
                    continue;
                }

                RejectUnknown(tensor.Unknown, itemPath, diagnostics);
                if (tensor.Shape == null) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "shape property is required; use an empty array for a scalar.", itemPath + ".shape"));
                tensors.Add(new ModelTensorSignatureDocument(tensor.Name, tensor.ElementType, tensor.Shape));
            }

            return tensors;
        }

        private static ModelArtifactLocationKind ParseLocationKind(string? value, string path, List<ModelPackageDiagnostic> diagnostics)
        {
            if (value == "file") return ModelArtifactLocationKind.File;
            if (value == "directory") return ModelArtifactLocationKind.Directory;
            diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, "locationKind must be 'file' or 'directory'.", path));
            return ModelArtifactLocationKind.File;
        }

        private static ModelFileRole ParseFileRole(string? value, string path, string? artifactId, string? filePath, List<ModelPackageDiagnostic> diagnostics)
        {
            switch (value)
            {
                case "model": return ModelFileRole.Model;
                case "weights": return ModelFileRole.Weights;
                case "externalData": return ModelFileRole.ExternalData;
                case "labels": return ModelFileRole.Labels;
                case "vocabulary": return ModelFileRole.Vocabulary;
                case "tokenizer": return ModelFileRole.Tokenizer;
                case "chatTemplate": return ModelFileRole.ChatTemplate;
                case "configuration": return ModelFileRole.Configuration;
                case "license": return ModelFileRole.License;
                case "testInput": return ModelFileRole.TestInput;
                case "other": return ModelFileRole.Other;
                default:
                    diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, "File role is invalid.", path, artifactId, filePath));
                    return ModelFileRole.Other;
            }
        }

        private static void RejectUnknown(Dictionary<string, JsonElement>? values, string path, List<ModelPackageDiagnostic> diagnostics)
        {
            if (values == null) return;
            foreach (string name in values.Keys) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.UnknownProperty, $"Unknown property '{name}' is not allowed.", path + "." + name));
        }

        private static ManifestDto ConvertToDto(ModelPackageDocument document)
        {
            var dto = new ManifestDto
            {
                SchemaVersion = document.SchemaVersion,
                ModelId = document.ModelId,
                Name = document.Name,
                Family = document.Family,
                Task = document.Task,
                ModelVersion = document.ModelVersion,
                GeneratedAt = document.GeneratedAt,
                ProfileId = document.ProfileId,
                Extensions = CopyExtensions(document.Extensions),
                Inputs = new List<TensorDto?>(),
                Outputs = new List<TensorDto?>(),
                Artifacts = new List<ArtifactDto?>()
            };
            if (document.Exporter != null) dto.Exporter = new ExporterDto { Name = document.Exporter.Name, Version = document.Exporter.Version, SourceRevision = document.Exporter.SourceRevision };
            if (document.Source != null) dto.Source = new SourceDto { SourceUrl = document.Source.SourceUrl, ProjectUrl = document.Source.ProjectUrl, Revision = document.Source.Revision, Author = document.Source.Author, Copyright = document.Source.Copyright, LicenseExpression = document.Source.LicenseExpression, LicenseFile = document.Source.LicenseFile, RedistributionAllowed = document.Source.RedistributionAllowed };
            foreach (ModelTensorSignatureDocument tensor in document.Inputs) dto.Inputs.Add(new TensorDto { Name = tensor.Name, ElementType = tensor.ElementType, Shape = new List<long>(tensor.Shape) });
            foreach (ModelTensorSignatureDocument tensor in document.Outputs) dto.Outputs.Add(new TensorDto { Name = tensor.Name, ElementType = tensor.ElementType, Shape = new List<long>(tensor.Shape) });
            foreach (ModelArtifactDocument artifact in document.Artifacts)
            {
                var artifactDto = new ArtifactDto
                {
                    ArtifactId = artifact.ArtifactId,
                    Format = artifact.Format,
                    LocationKind = artifact.LocationKind == ModelArtifactLocationKind.File ? "file" : "directory",
                    Entrypoint = artifact.Entrypoint,
                    CompatibleBackends = new List<string>(artifact.CompatibleBackends),
                    Files = new List<FileDto?>(),
                    Precision = artifact.Precision,
                    Quantization = artifact.Quantization,
                    Opset = artifact.Opset,
                    Portable = artifact.Portable,
                    MinimumBackendVersion = artifact.MinimumBackendVersion,
                    MinimumRuntimeVersion = artifact.MinimumRuntimeVersion,
                    Extensions = CopyExtensions(artifact.Extensions)
                };
                foreach (ModelFileDocument file in artifact.Files) artifactDto.Files.Add(new FileDto { RelativePath = file.RelativePath, Sha256 = file.Sha256, Size = file.Size, MediaType = file.MediaType, Role = FormatFileRole(file.Role) });
                dto.Artifacts.Add(artifactDto);
            }

            return dto;
        }

        private static SortedDictionary<string, string> CopyExtensions(IReadOnlyDictionary<string, string> values)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> value in values) result.Add(value.Key, value.Value);
            return result;
        }

        private static string FormatFileRole(ModelFileRole role)
        {
            switch (role)
            {
                case ModelFileRole.Model: return "model";
                case ModelFileRole.Weights: return "weights";
                case ModelFileRole.ExternalData: return "externalData";
                case ModelFileRole.Labels: return "labels";
                case ModelFileRole.Vocabulary: return "vocabulary";
                case ModelFileRole.Tokenizer: return "tokenizer";
                case ModelFileRole.ChatTemplate: return "chatTemplate";
                case ModelFileRole.Configuration: return "configuration";
                case ModelFileRole.License: return "license";
                case ModelFileRole.TestInput: return "testInput";
                case ModelFileRole.Other: return "other";
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
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
                    if (output.Length + read > maximumBytes) ThrowDiagnostics("The ModelPack JSON exceeds the configured byte limit.", new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "JSON byte length exceeds the configured limit.", "$"));
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
                    if (output.Length + read > maximumBytes) ThrowDiagnostics("The ModelPack JSON exceeds the configured byte limit.", new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "JSON byte length exceeds the configured limit.", "$"));
                    await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                }

                return output.ToArray();
            }
        }

        private static void ThrowDiagnostics(string message, ModelPackageDiagnostic diagnostic)
        {
            throw new ModelPackageValidationException(message, new[] { diagnostic });
        }
    }
}
