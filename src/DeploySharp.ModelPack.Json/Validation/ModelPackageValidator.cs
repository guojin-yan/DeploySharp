using System;
using System.Collections.Generic;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Validates and normalizes model-package manifests without accessing the file system. / 在不访问文件系统的情况下验证并规范化模型包清单。</summary>
    public static class ModelPackageValidator
    {
        /// <summary>Validates a document and returns an immutable normalized manifest. / 验证文档并返回不可变的规范化清单。</summary>
        public static ValidatedModelPackage Validate(ModelPackageDocument document, ModelPackageValidationOptions? options = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            ModelPackageValidationOptions limits = options ?? ModelPackageValidationOptions.Default;
            var diagnostics = new List<ModelPackageDiagnostic>();

            Version? schemaVersion = ValidateVersion(document.SchemaVersion, limits, diagnostics);
            ModelId? modelId = ValidateModelId(document.ModelId, diagnostics);
            RequiredString(document.Name, "$.name", limits, diagnostics);
            RequiredIdentifier(document.Family, "$.family", limits, diagnostics);
            RequiredIdentifier(document.Task, "$.task", limits, diagnostics);
            RequiredString(document.ModelVersion, "$.modelVersion", limits, diagnostics);
            OptionalIdentifier(document.ProfileId, "$.profileId", limits, diagnostics);
            ValidateExporter(document.Exporter, limits, diagnostics);
            ValidateSource(document.Source, limits, diagnostics);
            ValidateExtensions(document.Extensions, "$.extensions", limits, diagnostics);
            ValidateTensors(document.Inputs, "$.inputs", limits, diagnostics);
            ValidateTensors(document.Outputs, "$.outputs", limits, diagnostics);

            if (document.Artifacts.Count == 0)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "At least one artifact is required.", "$.artifacts"));
            }
            else if (document.Artifacts.Count > limits.MaximumArtifacts)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "Artifact count exceeds the configured limit.", "$.artifacts"));
            }

            var artifactIds = new HashSet<string>(StringComparer.Ordinal);
            var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int totalFiles = 0;
            for (int artifactIndex = 0; artifactIndex < document.Artifacts.Count; artifactIndex++)
            {
                ModelArtifactDocument artifact = document.Artifacts[artifactIndex];
                ValidateArtifact(artifact, artifactIndex, limits, diagnostics, artifactIds, allPaths, ref totalFiles);
            }

            if (totalFiles > limits.MaximumFiles)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "Total model-file count exceeds the configured limit.", "$.artifacts"));
            }

            if (document.Source != null && !string.IsNullOrWhiteSpace(document.Source.LicenseFile))
            {
                if (ModelPackagePath.TryNormalizeRelativePath(document.Source.LicenseFile, out string? licensePath, out _) && !allPaths.Contains(licensePath!))
                {
                    diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidPath, "The declared license file is not present in any artifact.", "$.source.licenseFile", filePath: licensePath));
                }
            }

            if (diagnostics.Count > 0 || schemaVersion == null || !modelId.HasValue)
            {
                throw new ModelPackageValidationException("The model-package manifest is invalid.", diagnostics.Count > 0 ? diagnostics : new[] { new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, "Manifest validation failed.") }, modelId: modelId);
            }

            ModelPackageDocument normalized = NormalizeDocument(document);
            return new ValidatedModelPackage(normalized, schemaVersion, modelId.Value);
        }

        private static Version? ValidateVersion(string? value, ModelPackageValidationOptions options, List<ModelPackageDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value) || !Version.TryParse(value, out Version? version) || version.Build >= 0 || version.Revision >= 0)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidVersion, "schemaVersion must use 'major.minor' form.", "$.schemaVersion"));
                return null;
            }

            if (version.Major != options.SupportedSchemaMajor)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidVersion, $"Schema major version {version.Major} is unsupported.", "$.schemaVersion"));
            }
            else if (version.Minor > options.SupportedSchemaMinor && !options.AllowNewerMinorVersions)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidVersion, $"Schema minor version {version.Minor} is newer than the configured implementation.", "$.schemaVersion"));
            }

            return version;
        }

        private static ModelId? ValidateModelId(string? value, List<ModelPackageDiagnostic> diagnostics)
        {
            try
            {
                return new ModelId(value!);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is ArgumentNullException)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidIdentifier, "modelId must be a normalized DeploySharp identifier.", "$.modelId"));
                return null;
            }
        }

        private static void ValidateExporter(ModelExporterDocument? exporter, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            if (exporter == null)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "Exporter metadata is required.", "$.exporter"));
                return;
            }

            RequiredString(exporter.Name, "$.exporter.name", limits, diagnostics);
            RequiredString(exporter.Version, "$.exporter.version", limits, diagnostics);
            OptionalString(exporter.SourceRevision, "$.exporter.sourceRevision", limits, diagnostics);
        }

        private static void ValidateSource(ModelSourceDocument? source, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            if (source == null)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "Source and license metadata is required.", "$.source"));
                return;
            }

            ValidateHttpUrl(source.SourceUrl, "$.source.sourceUrl", required: true, limits, diagnostics);
            ValidateHttpUrl(source.ProjectUrl, "$.source.projectUrl", required: false, limits, diagnostics);
            RequiredString(source.Revision, "$.source.revision", limits, diagnostics);
            RequiredString(source.Author, "$.source.author", limits, diagnostics);
            OptionalString(source.Copyright, "$.source.copyright", limits, diagnostics);
            OptionalString(source.LicenseExpression, "$.source.licenseExpression", limits, diagnostics);
            if (string.IsNullOrWhiteSpace(source.LicenseExpression) && string.IsNullOrWhiteSpace(source.LicenseFile))
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "A license expression or license file is required.", "$.source"));
            }

            if (!string.IsNullOrWhiteSpace(source.LicenseFile) && !ModelPackagePath.TryNormalizeRelativePath(source.LicenseFile, out _, out string? error))
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidPath, error!, "$.source.licenseFile", filePath: source.LicenseFile));
            }
        }

        private static void ValidateTensors(IReadOnlyList<ModelTensorSignatureDocument> tensors, string path, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            if (tensors.Count > limits.MaximumTensors)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "Tensor signature count exceeds the configured limit.", path));
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < tensors.Count; index++)
            {
                ModelTensorSignatureDocument tensor = tensors[index];
                string itemPath = path + "[" + index + "]";
                RequiredString(tensor.Name, itemPath + ".name", limits, diagnostics);
                RequiredIdentifier(tensor.ElementType, itemPath + ".elementType", limits, diagnostics);
                if (!string.IsNullOrWhiteSpace(tensor.Name) && !names.Add(tensor.Name!))
                {
                    diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Duplicate, "Tensor names must be unique within a signature list.", itemPath + ".name"));
                }

                if (tensor.Shape.Count > 32)
                {
                    diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "Tensor rank exceeds 32 dimensions.", itemPath + ".shape"));
                }

                for (int dimension = 0; dimension < tensor.Shape.Count; dimension++)
                {
                    if (tensor.Shape[dimension] != -1 && tensor.Shape[dimension] <= 0)
                    {
                        diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, "Tensor dimensions must be -1 or greater than zero.", itemPath + ".shape[" + dimension + "]"));
                    }
                }
            }
        }

        private static void ValidateArtifact(
            ModelArtifactDocument artifact,
            int artifactIndex,
            ModelPackageValidationOptions limits,
            List<ModelPackageDiagnostic> diagnostics,
            HashSet<string> artifactIds,
            HashSet<string> allPaths,
            ref int totalFiles)
        {
            string path = "$.artifacts[" + artifactIndex + "]";
            RequiredIdentifier(artifact.ArtifactId, path + ".artifactId", limits, diagnostics);
            RequiredIdentifier(artifact.Format, path + ".format", limits, diagnostics);
            if (!string.IsNullOrWhiteSpace(artifact.ArtifactId) && !artifactIds.Add(artifact.ArtifactId!))
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Duplicate, "Artifact identifiers must be unique.", path + ".artifactId", artifact.ArtifactId));
            }

            if (!Enum.IsDefined(typeof(ModelArtifactLocationKind), artifact.LocationKind))
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, "Artifact locationKind is invalid.", path + ".locationKind", artifact.ArtifactId));
            }

            string? normalizedEntrypoint = null;
            if (!ModelPackagePath.TryNormalizeRelativePath(artifact.Entrypoint, out normalizedEntrypoint, out string? entrypointError))
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidPath, entrypointError!, path + ".entrypoint", artifact.ArtifactId, artifact.Entrypoint));
            }

            if (artifact.CompatibleBackends.Count == 0)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "At least one compatible backend is required.", path + ".compatibleBackends", artifact.ArtifactId));
            }

            var backendIds = new HashSet<BackendId>();
            for (int backendIndex = 0; backendIndex < artifact.CompatibleBackends.Count; backendIndex++)
            {
                string backend = artifact.CompatibleBackends[backendIndex];
                try
                {
                    var backendId = new BackendId(backend);
                    if (!backendIds.Add(backendId)) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Duplicate, "Compatible backend identifiers must be unique.", path + ".compatibleBackends[" + backendIndex + "]", artifact.ArtifactId));
                }
                catch (ArgumentException)
                {
                    diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidIdentifier, "Compatible backend identifier is invalid.", path + ".compatibleBackends[" + backendIndex + "]", artifact.ArtifactId));
                }
            }

            if (artifact.Files.Count == 0)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "At least one artifact file is required.", path + ".files", artifact.ArtifactId));
            }

            totalFiles += artifact.Files.Count;
            bool entrypointMatched = false;
            for (int fileIndex = 0; fileIndex < artifact.Files.Count; fileIndex++)
            {
                ModelFileDocument file = artifact.Files[fileIndex];
                string fileJsonPath = path + ".files[" + fileIndex + "]";
                if (!ModelPackagePath.TryNormalizeRelativePath(file.RelativePath, out string? normalizedPath, out string? pathError))
                {
                    diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidPath, pathError!, fileJsonPath + ".relativePath", artifact.ArtifactId, file.RelativePath));
                }
                else
                {
                    if (!allPaths.Add(normalizedPath!)) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Duplicate, "Normalized file paths must be unique across the manifest.", fileJsonPath + ".relativePath", artifact.ArtifactId, normalizedPath));
                    if (artifact.LocationKind == ModelArtifactLocationKind.File && string.Equals(normalizedEntrypoint, normalizedPath, StringComparison.OrdinalIgnoreCase)) entrypointMatched = true;
                    if (artifact.LocationKind == ModelArtifactLocationKind.Directory && normalizedEntrypoint != null)
                    {
                        string prefix = normalizedEntrypoint + "/";
                        if (!normalizedPath!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidPath, "Directory artifact files must be below the entrypoint directory.", fileJsonPath + ".relativePath", artifact.ArtifactId, normalizedPath));
                    }
                }

                ValidateSha256(file.Sha256, fileJsonPath + ".sha256", artifact.ArtifactId, file.RelativePath, diagnostics);
                if (file.Size < 0) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, "File size cannot be negative.", fileJsonPath + ".size", artifact.ArtifactId, file.RelativePath));
                OptionalString(file.MediaType, fileJsonPath + ".mediaType", limits, diagnostics);
                if (!Enum.IsDefined(typeof(ModelFileRole), file.Role)) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, "File role is invalid.", fileJsonPath + ".role", artifact.ArtifactId, file.RelativePath));
            }

            if (artifact.LocationKind == ModelArtifactLocationKind.File && normalizedEntrypoint != null && !entrypointMatched)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidPath, "A file artifact entrypoint must match one declared file.", path + ".entrypoint", artifact.ArtifactId, normalizedEntrypoint));
            }

            OptionalIdentifier(artifact.Precision, path + ".precision", limits, diagnostics);
            OptionalIdentifier(artifact.Quantization, path + ".quantization", limits, diagnostics);
            if (artifact.Opset.HasValue && artifact.Opset.Value <= 0) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, "opset must be greater than zero.", path + ".opset", artifact.ArtifactId));
            OptionalString(artifact.MinimumBackendVersion, path + ".minimumBackendVersion", limits, diagnostics);
            OptionalString(artifact.MinimumRuntimeVersion, path + ".minimumRuntimeVersion", limits, diagnostics);
            ValidateExtensions(artifact.Extensions, path + ".extensions", limits, diagnostics);
        }

        private static void ValidateSha256(string? value, string path, string? artifactId, string? filePath, List<ModelPackageDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length != 64)
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidHash, "SHA256 must contain exactly 64 hexadecimal characters.", path, artifactId, filePath));
                return;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (!Uri.IsHexDigit(value[index]))
                {
                    diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidHash, "SHA256 contains a non-hexadecimal character.", path, artifactId, filePath));
                    return;
                }
            }
        }

        private static void ValidateExtensions(IReadOnlyDictionary<string, string> extensions, string path, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            if (extensions.Count > limits.MaximumExtensions) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "Extension count exceeds the configured limit.", path));
            foreach (KeyValuePair<string, string> extension in extensions)
            {
                RequiredIdentifier(extension.Key, path, limits, diagnostics);
                RequiredString(extension.Value, path + "." + extension.Key, limits, diagnostics);
            }
        }

        private static void ValidateHttpUrl(string? value, string path, bool required, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "An absolute HTTP or HTTPS URL is required.", path));
                return;
            }

            OptionalString(value, path, limits, diagnostics);
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, "The URL must be absolute HTTP or HTTPS.", path));
            }
        }

        private static void RequiredString(string? value, string path, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value)) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.Required, "A non-empty value is required.", path));
            else if (value!.Length > limits.MaximumStringLength) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "String length exceeds the configured limit.", path));
        }

        private static void OptionalString(string? value, string path, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            if (value != null && value.Length > limits.MaximumStringLength) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.LimitExceeded, "String length exceeds the configured limit.", path));
        }

        private static void RequiredIdentifier(string? value, string path, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            RequiredString(value, path, limits, diagnostics);
            if (!string.IsNullOrWhiteSpace(value) && !IsIdentifier(value!)) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidIdentifier, "Identifier contains unsupported characters.", path));
        }

        private static void OptionalIdentifier(string? value, string path, ModelPackageValidationOptions limits, List<ModelPackageDiagnostic> diagnostics)
        {
            OptionalString(value, path, limits, diagnostics);
            if (!string.IsNullOrWhiteSpace(value) && !IsIdentifier(value!)) diagnostics.Add(new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidIdentifier, "Identifier contains unsupported characters.", path));
        }

        private static bool IsIdentifier(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '.' || character == '-' || character == '_' || character == '/';
                if (!valid) return false;
            }

            return value.Length > 0;
        }

        private static ModelPackageDocument NormalizeDocument(ModelPackageDocument document)
        {
            var artifacts = new List<ModelArtifactDocument>(document.Artifacts.Count);
            for (int artifactIndex = 0; artifactIndex < document.Artifacts.Count; artifactIndex++)
            {
                ModelArtifactDocument artifact = document.Artifacts[artifactIndex];
                var files = new List<ModelFileDocument>(artifact.Files.Count);
                for (int fileIndex = 0; fileIndex < artifact.Files.Count; fileIndex++)
                {
                    ModelFileDocument file = artifact.Files[fileIndex];
                    files.Add(new ModelFileDocument(ModelPackagePath.NormalizeRelativePath(file.RelativePath!), file.Sha256!.ToLowerInvariant(), file.Size, file.MediaType, file.Role));
                }

                artifacts.Add(new ModelArtifactDocument(
                    artifact.ArtifactId,
                    artifact.Format,
                    artifact.LocationKind,
                    ModelPackagePath.NormalizeRelativePath(artifact.Entrypoint!),
                    artifact.CompatibleBackends,
                    files,
                    artifact.Precision,
                    artifact.Quantization,
                    artifact.Opset,
                    artifact.Portable,
                    artifact.MinimumBackendVersion,
                    artifact.MinimumRuntimeVersion,
                    artifact.Extensions));
            }

            ModelSourceDocument? source = document.Source == null ? null : new ModelSourceDocument(
                document.Source.SourceUrl,
                document.Source.ProjectUrl,
                document.Source.Revision,
                document.Source.Author,
                document.Source.Copyright,
                document.Source.LicenseExpression,
                string.IsNullOrWhiteSpace(document.Source.LicenseFile) ? null : ModelPackagePath.NormalizeRelativePath(document.Source.LicenseFile!),
                document.Source.RedistributionAllowed);

            return new ModelPackageDocument(
                document.SchemaVersion,
                document.ModelId,
                document.Name,
                document.Family,
                document.Task,
                document.ModelVersion,
                document.Exporter,
                source,
                document.GeneratedAt,
                document.ProfileId,
                document.Inputs,
                document.Outputs,
                artifacts,
                document.Extensions);
        }
    }
}
