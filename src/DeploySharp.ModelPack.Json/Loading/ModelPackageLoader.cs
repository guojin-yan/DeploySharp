using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Loads a local manifest and verifies every declared artifact file. / 加载本地清单并验证每个已声明工件文件。</summary>
    public static class ModelPackageLoader
    {
        /// <summary>Loads and verifies a local ModelPack manifest. / 加载并验证本地 ModelPack 清单。</summary>
        public static LocalModelPackage Load(string manifestPath, ModelPackageLoadOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (manifestPath == null) throw new ArgumentNullException(nameof(manifestPath));
            ModelPackageLoadOptions loadOptions = options ?? ModelPackageLoadOptions.Default;
            string fullManifestPath = Path.GetFullPath(manifestPath);
            if (!File.Exists(fullManifestPath)) ThrowLoad("The ModelPack manifest file does not exist.", ModelPackageDiagnosticCodes.FileNotFound, "$", filePath: fullManifestPath);
            string root = Path.GetDirectoryName(fullManifestPath)!;
            try
            {
                using (var stream = new FileStream(fullManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    ValidatedModelPackage manifest = ModelPackageJsonSerializer.Deserialize(stream, loadOptions.ValidationOptions);
                    return Resolve(fullManifestPath, root, manifest, loadOptions, cancellationToken);
                }
            }
            catch (ModelPackageValidationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw WrapIo("The local ModelPack could not be loaded.", fullManifestPath, exception);
            }
        }

        /// <summary>Asynchronously loads and verifies a local ModelPack manifest. / 异步加载并验证本地 ModelPack 清单。</summary>
        public static async Task<LocalModelPackage> LoadAsync(string manifestPath, ModelPackageLoadOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (manifestPath == null) throw new ArgumentNullException(nameof(manifestPath));
            ModelPackageLoadOptions loadOptions = options ?? ModelPackageLoadOptions.Default;
            string fullManifestPath = Path.GetFullPath(manifestPath);
            if (!File.Exists(fullManifestPath)) ThrowLoad("The ModelPack manifest file does not exist.", ModelPackageDiagnosticCodes.FileNotFound, "$", filePath: fullManifestPath);
            string root = Path.GetDirectoryName(fullManifestPath)!;
            try
            {
                using (var stream = new FileStream(fullManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                {
                    ValidatedModelPackage manifest = await ModelPackageJsonSerializer.DeserializeAsync(stream, loadOptions.ValidationOptions, cancellationToken).ConfigureAwait(false);
                    return await ResolveAsync(fullManifestPath, root, manifest, loadOptions, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ModelPackageValidationException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw WrapIo("The local ModelPack could not be loaded.", fullManifestPath, exception);
            }
        }

        private static LocalModelPackage Resolve(string manifestPath, string root, ValidatedModelPackage manifest, ModelPackageLoadOptions options, CancellationToken cancellationToken)
        {
            PrepareRoot(root, options, manifest);
            EnsureTotalSize(manifest, options);
            var artifacts = new List<ResolvedModelArtifact>();
            for (int artifactIndex = 0; artifactIndex < manifest.Document.Artifacts.Count; artifactIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ModelArtifactDocument artifact = manifest.Document.Artifacts[artifactIndex];
                var files = new List<ResolvedModelFile>();
                for (int fileIndex = 0; fileIndex < artifact.Files.Count; fileIndex++)
                {
                    ModelFileDocument file = artifact.Files[fileIndex];
                    string fullPath = ResolveDeclaredPath(root, file.RelativePath!, options, manifest, artifact.ArtifactId);
                    VerifyFile(fullPath, file, artifact.ArtifactId, options, manifest, cancellationToken);
                    files.Add(new ResolvedModelFile(file, fullPath));
                }

                string location = ResolveDeclaredPath(root, artifact.Entrypoint!, options, manifest, artifact.ArtifactId, artifact.LocationKind == ModelArtifactLocationKind.Directory);
                artifacts.Add(new ResolvedModelArtifact(manifest.ModelId, artifact, location, files));
            }

            return new LocalModelPackage(manifestPath, root, manifest, artifacts);
        }

        private static async Task<LocalModelPackage> ResolveAsync(string manifestPath, string root, ValidatedModelPackage manifest, ModelPackageLoadOptions options, CancellationToken cancellationToken)
        {
            PrepareRoot(root, options, manifest);
            EnsureTotalSize(manifest, options);
            var artifacts = new List<ResolvedModelArtifact>();
            for (int artifactIndex = 0; artifactIndex < manifest.Document.Artifacts.Count; artifactIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ModelArtifactDocument artifact = manifest.Document.Artifacts[artifactIndex];
                var files = new List<ResolvedModelFile>();
                for (int fileIndex = 0; fileIndex < artifact.Files.Count; fileIndex++)
                {
                    ModelFileDocument file = artifact.Files[fileIndex];
                    string fullPath = ResolveDeclaredPath(root, file.RelativePath!, options, manifest, artifact.ArtifactId);
                    await VerifyFileAsync(fullPath, file, artifact.ArtifactId, options, manifest, cancellationToken).ConfigureAwait(false);
                    files.Add(new ResolvedModelFile(file, fullPath));
                }

                string location = ResolveDeclaredPath(root, artifact.Entrypoint!, options, manifest, artifact.ArtifactId, artifact.LocationKind == ModelArtifactLocationKind.Directory);
                artifacts.Add(new ResolvedModelArtifact(manifest.ModelId, artifact, location, files));
            }

            return new LocalModelPackage(manifestPath, root, manifest, artifacts);
        }

        private static void PrepareRoot(string root, ModelPackageLoadOptions options, ValidatedModelPackage manifest)
        {
            if (!Directory.Exists(root)) ThrowLoad("The ModelPack root directory does not exist.", ModelPackageDiagnosticCodes.FileNotFound, "$", manifest.ModelId, filePath: root);
            if (options.RejectUnsafeLinks && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            {
                ThrowLoad("A ModelPack root cannot itself be a symbolic link or reparse point.", ModelPackageDiagnosticCodes.LinkBoundary, "$", manifest.ModelId, filePath: root);
            }
        }

        private static void EnsureTotalSize(ValidatedModelPackage manifest, ModelPackageLoadOptions options)
        {
            long total = 0;
            try
            {
                for (int artifactIndex = 0; artifactIndex < manifest.Document.Artifacts.Count; artifactIndex++)
                {
                    foreach (ModelFileDocument file in manifest.Document.Artifacts[artifactIndex].Files) total = checked(total + file.Size);
                }
            }
            catch (OverflowException)
            {
                ThrowLoad("Declared package size overflows Int64.", ModelPackageDiagnosticCodes.LimitExceeded, "$.artifacts", manifest.ModelId);
            }

            if (total > options.MaximumTotalFileBytes) ThrowLoad("Declared package size exceeds the configured loading limit.", ModelPackageDiagnosticCodes.LimitExceeded, "$.artifacts", manifest.ModelId);
        }

        private static string ResolveDeclaredPath(string root, string relativePath, ModelPackageLoadOptions options, ValidatedModelPackage manifest, string? artifactId, bool directory = false)
        {
            string fullPath = Path.GetFullPath(Path.Combine(root, ModelPackagePath.ToPlatformPath(relativePath)));
            if (!ModelPackagePath.IsWithinRoot(root, fullPath)) ThrowLoad("The resolved path escapes the package root.", ModelPackageDiagnosticCodes.InvalidPath, "$.artifacts", manifest.ModelId, artifactId, relativePath);
            if (directory)
            {
                if (!Directory.Exists(fullPath)) ThrowLoad("The declared artifact directory does not exist.", ModelPackageDiagnosticCodes.FileNotFound, "$.artifacts", manifest.ModelId, artifactId, relativePath);
            }
            else if (!File.Exists(fullPath))
            {
                ThrowLoad("The declared model file does not exist.", ModelPackageDiagnosticCodes.FileNotFound, "$.artifacts", manifest.ModelId, artifactId, relativePath);
            }

            if (options.RejectUnsafeLinks) VerifyLinkBoundary(root, fullPath, manifest, artifactId, relativePath);
            return fullPath;
        }

        private static void VerifyLinkBoundary(string root, string fullPath, ValidatedModelPackage manifest, string? artifactId, string relativePath)
        {
            // The boundary check above guarantees this substring is safe, and this implementation also works on netstandard2.0.
            // 上面的边界检查保证该子字符串安全，同时此实现也可用于 netstandard2.0。
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string platformRelative = Path.GetFullPath(fullPath).Substring(normalizedRoot.Length);
            string current = root;
            string[] segments = platformRelative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                FileAttributes attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) == 0) continue;
#if NET6_0_OR_GREATER
                FileSystemInfo info = (attributes & FileAttributes.Directory) != 0 ? (FileSystemInfo)new DirectoryInfo(current) : new FileInfo(current);
                FileSystemInfo? target = info.ResolveLinkTarget(true);
                if (target == null || !ModelPackagePath.IsWithinRoot(root, target.FullName))
                {
                    ThrowLoad("A symbolic link or reparse point resolves outside the package root.", ModelPackageDiagnosticCodes.LinkBoundary, "$.artifacts", manifest.ModelId, artifactId, relativePath);
                }
#else
                // Older TFMs cannot reliably resolve final link targets, so validation fails closed. / 旧 TFM 无法可靠解析最终链接目标，因此验证采用封闭失败。
                ThrowLoad("Symbolic links and reparse points are rejected on this target framework.", ModelPackageDiagnosticCodes.LinkBoundary, "$.artifacts", manifest.ModelId, artifactId, relativePath);
#endif
            }
        }

        private static void VerifyFile(string fullPath, ModelFileDocument file, string? artifactId, ModelPackageLoadOptions options, ValidatedModelPackage manifest, CancellationToken cancellationToken)
        {
            FileInfo info = GetVerificationFileInfo(fullPath);
            if (options.VerifyFileSize && info.Length != file.Size) ThrowIntegrity("File size does not match the manifest.", file, artifactId, manifest);
            if (options.VerifySha256)
            {
                string actual = ModelFileIntegrity.ComputeSha256(fullPath, cancellationToken);
                if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase)) ThrowIntegrity("File SHA256 does not match the manifest.", file, artifactId, manifest);
            }
        }

        private static async Task VerifyFileAsync(string fullPath, ModelFileDocument file, string? artifactId, ModelPackageLoadOptions options, ValidatedModelPackage manifest, CancellationToken cancellationToken)
        {
            FileInfo info = GetVerificationFileInfo(fullPath);
            if (options.VerifyFileSize && info.Length != file.Size) ThrowIntegrity("File size does not match the manifest.", file, artifactId, manifest);
            if (options.VerifySha256)
            {
                string actual = await ModelFileIntegrity.ComputeSha256Async(fullPath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase)) ThrowIntegrity("File SHA256 does not match the manifest.", file, artifactId, manifest);
            }
        }

        private static FileInfo GetVerificationFileInfo(string fullPath)
        {
            var info = new FileInfo(fullPath);
#if NET6_0_OR_GREATER
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0 && info.ResolveLinkTarget(true) is FileInfo target)
            {
                return target;
            }
#endif
            return info;
        }

        private static void ThrowIntegrity(string message, ModelFileDocument file, string? artifactId, ValidatedModelPackage manifest)
        {
            ThrowLoad(message, ModelPackageDiagnosticCodes.IntegrityMismatch, "$.artifacts", manifest.ModelId, artifactId, file.RelativePath);
        }

        private static ModelPackageValidationException WrapIo(string message, string path, Exception exception)
        {
            return new ModelPackageValidationException(message, new[] { new ModelPackageDiagnostic(ModelPackageDiagnosticCodes.InvalidValue, exception.Message, filePath: path) }, exception, technicalDetails: exception.ToString());
        }

        private static void ThrowLoad(string message, string code, string jsonPath, JYPPX.DeploySharp.Models.ModelId? modelId = null, string? artifactId = null, string? filePath = null)
        {
            throw new ModelPackageValidationException(message, new[] { new ModelPackageDiagnostic(code, message, jsonPath, artifactId, filePath) }, modelId: modelId, technicalDetails: filePath);
        }
    }
}
