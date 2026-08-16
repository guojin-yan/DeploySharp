using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp.Models;
using Microsoft.Win32.SafeHandles;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Stores opt-in PTX/CUBIN and engine/plan entries only below an explicit caller-owned root.</summary>
    public sealed class TensorRtExternalCacheStore
    {
        private const int SchemaVersion = 1;
        private const string LayoutDirectory = "deploysharp-tensorrt-cache-v1";
        private const string CurrentFileName = "current.json";
        private const string ManifestFileName = "manifest.json";
        private static readonly object GateSync = new object();
        private static readonly Dictionary<string, GateState> Gates = new Dictionary<string, GateState>(StringComparer.Ordinal);

        /// <summary>Initializes an opt-in store rooted at the exact absolute caller-selected directory.</summary>
        public TensorRtExternalCacheStore(string rootPath, TensorRtExternalCacheOptions? options = null)
        {
            Options = options ?? TensorRtExternalCacheOptions.Default;
            RootPath = ValidateAndPrepareRoot(rootPath, Options.CreateRootIfMissing);
            LayoutPath = CombineContained(RootPath, LayoutDirectory);
            EnsureSafeDirectory(LayoutPath, create: Options.CreateRootIfMissing);
        }

        /// <summary>Gets the normalized caller-owned cache root.</summary>
        public string RootPath { get; }
        /// <summary>Gets the bounded store behavior.</summary>
        public TensorRtExternalCacheOptions Options { get; }

        private string LayoutPath { get; }

        /// <summary>Looks up and reconstructs a copied CUDA artifact without invoking NVRTC.</summary>
        public TensorRtCudaCacheResult LookupCuda(TensorRtCudaKernelLookupIdentity identity, CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            using GateScope scope = Enter("cuda:" + identity.LookupKeySha256, cancellationToken);
            return LookupCudaCore(identity, cancellationToken);
        }

        /// <summary>Atomically stores a CUDA artifact after verifying every lookup and existing cache-identity field.</summary>
        public TensorRtCudaCacheResult StoreCuda(
            TensorRtCudaKernelLookupIdentity identity,
            TensorRtCudaRtcArtifact artifact,
            CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            using GateScope scope = Enter("cuda:" + identity.LookupKeySha256, cancellationToken);
            return StoreCudaCore(identity, artifact, cancellationToken);
        }

        /// <summary>Returns a hit without compiling, or executes the explicit factory once per store/key and atomically stores its result.</summary>
        public TensorRtCudaCacheResult GetOrCompileCuda(
            TensorRtCudaKernelLookupIdentity identity,
            Func<CancellationToken, TensorRtCudaRtcArtifact> compileFactory,
            CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (compileFactory == null) throw new ArgumentNullException(nameof(compileFactory));
            using GateScope scope = Enter("cuda:" + identity.LookupKeySha256, cancellationToken);
            scope.ThrowIfPriorFactoryFailed();
            TensorRtCudaCacheResult lookup = LookupCudaCore(identity, cancellationToken);
            if (lookup.Status != TensorRtExternalCacheStatus.Miss) return lookup;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                TensorRtCudaRtcArtifact artifact = compileFactory(cancellationToken) ?? throw new InvalidOperationException("The CUDA cache factory returned null.");
                TensorRtCudaCacheResult stored = StoreCudaCore(identity, artifact, cancellationToken);
                return new TensorRtCudaCacheResult(
                    stored.Status,
                    stored.Artifact,
                    stored.Metadata,
                    stored.RejectionReason,
                    stored.Remediation,
                    stored.RemediationPath,
                    factoryExecuted: true);
            }
            catch (Exception exception)
            {
                scope.RecordFactoryFailure(exception);
                throw;
            }
        }

        /// <summary>Opens a validated engine/plan stream without starting TensorRT.</summary>
        public TensorRtEngineCacheResult OpenEngine(TensorRtEngineCacheIdentity identity, CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            using GateScope scope = Enter("engine:" + identity.LookupKeySha256, cancellationToken);
            return OpenEngineCore(identity, cancellationToken);
        }

        /// <summary>Atomically stores engine/plan bytes from a caller-owned stream, which remains caller-owned.</summary>
        public TensorRtEngineCacheResult StoreEngine(
            TensorRtEngineCacheIdentity identity,
            Stream payload,
            CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (!payload.CanRead) throw new ArgumentException("The engine payload stream must be readable.", nameof(payload));
            using GateScope scope = Enter("engine:" + identity.LookupKeySha256, cancellationToken);
            return StoreEngineCore(identity, payload, cancellationToken);
        }

        /// <summary>Atomically stores a regular caller-owned .engine/.plan file whose extension matches the identity.</summary>
        public TensorRtEngineCacheResult StoreEngineFile(
            TensorRtEngineCacheIdentity identity,
            string enginePath,
            CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            string path = ValidateInputEnginePath(enginePath, identity.ArtifactExtension);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
            return StoreEngine(identity, stream, cancellationToken);
        }

        /// <summary>Returns a hit without building, or executes the explicit stream factory once per store/key and atomically stores its result.</summary>
        public TensorRtEngineCacheResult GetOrBuildEngine(
            TensorRtEngineCacheIdentity identity,
            Func<CancellationToken, Stream> buildFactory,
            CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (buildFactory == null) throw new ArgumentNullException(nameof(buildFactory));
            using GateScope scope = Enter("engine:" + identity.LookupKeySha256, cancellationToken);
            scope.ThrowIfPriorFactoryFailed();
            TensorRtEngineCacheResult lookup = OpenEngineCore(identity, cancellationToken);
            if (lookup.Status != TensorRtExternalCacheStatus.Miss) return lookup;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Stream built = buildFactory(cancellationToken) ?? throw new InvalidOperationException("The engine cache factory returned null.");
                if (!built.CanRead) throw new InvalidOperationException("The engine cache factory returned an unreadable stream.");
                TensorRtEngineCacheResult stored = StoreEngineCore(identity, built, cancellationToken);
                TensorRtEngineCacheResult opened = stored.Status == TensorRtExternalCacheStatus.Stored || stored.Status == TensorRtExternalCacheStatus.AlreadyPresent
                    ? OpenEngineCore(identity, cancellationToken)
                    : stored;
                if (!ReferenceEquals(opened, stored)) stored.Dispose();
                Stream? openedStream = opened.Stream == null ? null : opened.DetachStream();
                return new TensorRtEngineCacheResult(
                    opened.Status == TensorRtExternalCacheStatus.Hit ? TensorRtExternalCacheStatus.Stored : opened.Status,
                    openedStream,
                    opened.Metadata,
                    opened.RejectionReason,
                    opened.Remediation,
                    opened.RemediationPath,
                    factoryExecuted: true);
            }
            catch (Exception exception)
            {
                scope.RecordFactoryFailure(exception);
                throw;
            }
        }

        /// <summary>Returns a hit without building, or invokes the existing builder through an explicit caller path and stores its output.</summary>
        public TensorRtEngineCacheResult GetOrBuildEngine(
            TensorRtEngineCacheIdentity identity,
            ModelArtifact onnxArtifact,
            string callerOwnedEnginePath,
            TensorRtOnnxEngineBuilder builder,
            CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (onnxArtifact == null) throw new ArgumentNullException(nameof(onnxArtifact));
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            using GateScope scope = Enter("engine:" + identity.LookupKeySha256, cancellationToken);
            scope.ThrowIfPriorFactoryFailed();
            TensorRtEngineCacheResult lookup = OpenEngineCore(identity, cancellationToken);
            if (lookup.Status != TensorRtExternalCacheStatus.Miss) return lookup;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                TensorRtOnnxEngineBuildResult result = builder.Build(onnxArtifact, callerOwnedEnginePath, identity.BuildOptions, cancellationToken);
                if (!string.Equals(result.OnnxSha256, identity.OnnxSha256, StringComparison.Ordinal) ||
                    !string.Equals(result.BuildInputsSha256, identity.ManagedBuildInputsSha256, StringComparison.Ordinal))
                {
                    throw CacheFailure(TensorRtErrorCodes.ExternalCacheEntryRejected, "The completed engine build does not match the requested cache identity.", identity.LookupKeySha256);
                }
                using (var stream = new FileStream(result.EnginePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan))
                {
                    TensorRtEngineCacheResult stored = StoreEngineCore(identity, stream, cancellationToken);
                    if (stored.Status != TensorRtExternalCacheStatus.Stored && stored.Status != TensorRtExternalCacheStatus.AlreadyPresent) return stored;
                    stored.Dispose();
                }
                TensorRtEngineCacheResult opened = OpenEngineCore(identity, cancellationToken);
                if (opened.Status != TensorRtExternalCacheStatus.Hit) return opened;
                Stream openedStream = opened.DetachStream();
                return new TensorRtEngineCacheResult(TensorRtExternalCacheStatus.Stored, openedStream, opened.Metadata, factoryExecuted: true);
            }
            catch (Exception exception)
            {
                scope.RecordFactoryFailure(exception);
                throw;
            }
        }

        /// <summary>Invalidates the completed CUDA entry for one lookup identity.</summary>
        public TensorRtExternalCacheResult InvalidateCuda(TensorRtCudaKernelLookupIdentity identity, CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            return Invalidate("cuda", identity.LookupKeySha256, cancellationToken);
        }

        /// <summary>Invalidates the completed engine entry for one lookup identity.</summary>
        public TensorRtExternalCacheResult InvalidateEngine(TensorRtEngineCacheIdentity identity, CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            return Invalidate("engine", identity.LookupKeySha256, cancellationToken);
        }

        /// <summary>Deletes the completed CUDA entry; this is an explicit alias for invalidation.</summary>
        public TensorRtExternalCacheResult DeleteCuda(TensorRtCudaKernelLookupIdentity identity, CancellationToken cancellationToken = default)
        {
            return InvalidateCuda(identity, cancellationToken);
        }

        /// <summary>Deletes the completed engine entry; this is an explicit alias for invalidation.</summary>
        public TensorRtExternalCacheResult DeleteEngine(TensorRtEngineCacheIdentity identity, CancellationToken cancellationToken = default)
        {
            return InvalidateEngine(identity, cancellationToken);
        }

        private TensorRtCudaCacheResult LookupCudaCore(TensorRtCudaKernelLookupIdentity identity, CancellationToken cancellationToken)
        {
            EntryReadResult read = ReadEntry("cuda", identity.LookupKeySha256, Options.MaximumCudaArtifactBytes, cancellationToken, keepPayloadOpen: false);
            if (read.Status != TensorRtExternalCacheStatus.Hit)
            {
                return new TensorRtCudaCacheResult(read.Status, rejectionReason: read.RejectionReason, remediation: read.Remediation, remediationPath: read.RemediationPath);
            }

            try
            {
                ValidateCudaManifest(read.Manifest!, identity);
                byte[] bytes = read.PayloadBytes!;
                var artifact = new TensorRtCudaRtcArtifact(
                    bytes,
                    identity.ArtifactKind,
                    identity.Role,
                    identity.SourceSha256,
                    identity.HeadersSha256,
                    identity.OptionsSha256,
                    identity.CompilerVersion,
                    identity.TargetArchitecture,
                    identity.ProgramName,
                    GetRequiredString(read.Manifest!.RootElement.GetProperty("cuda"), "resolvedKernelName"),
                    identity.KernelNameExpression,
                    read.PayloadSha256);
                var completeIdentity = new TensorRtCudaKernelCacheIdentity(
                    artifact,
                    identity.CompilerIdentity,
                    identity.CudaRuntimeVersion,
                    identity.CudaRuntimeIdentity,
                    identity.CudaDriverVersion,
                    identity.CudaDriverIdentity,
                    identity.GpuArchitecture,
                    identity.GpuCompatibilityIdentity,
                    identity.NativeBridgeIdentity);
                string recordedCacheKey = GetRequiredSha256(read.Manifest.RootElement.GetProperty("cuda"), "cudaCacheKeySha256");
                if (!string.Equals(recordedCacheKey, completeIdentity.CacheKeySha256, StringComparison.Ordinal))
                {
                    return Reject("cuda", identity.LookupKeySha256, TensorRtExternalCacheRejectionReason.IdentityMismatch);
                }
                TensorRtExternalCacheEntryMetadata metadata = CreateMetadata(read, recordedCacheKey);
                return new TensorRtCudaCacheResult(TensorRtExternalCacheStatus.Hit, artifact, metadata);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return Reject("cuda", identity.LookupKeySha256, TensorRtExternalCacheRejectionReason.IdentityMismatch);
            }
            finally
            {
                read.Dispose();
            }
        }

        private TensorRtCudaCacheResult StoreCudaCore(TensorRtCudaKernelLookupIdentity identity, TensorRtCudaRtcArtifact artifact, CancellationToken cancellationToken)
        {
            ValidateCudaArtifact(identity, artifact);
            byte[] payload = artifact.ToArray();
            if (payload.LongLength > Options.MaximumCudaArtifactBytes) throw CacheFailure(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, "The CUDA artifact exceeds the configured cache limit.", "bytes=" + payload.LongLength);
            var completeIdentity = new TensorRtCudaKernelCacheIdentity(
                artifact,
                identity.CompilerIdentity,
                identity.CudaRuntimeVersion,
                identity.CudaRuntimeIdentity,
                identity.CudaDriverVersion,
                identity.CudaDriverIdentity,
                identity.GpuArchitecture,
                identity.GpuCompatibilityIdentity,
                identity.NativeBridgeIdentity);

            TensorRtCudaCacheResult existing = LookupCudaCore(identity, cancellationToken);
            if (existing.Status == TensorRtExternalCacheStatus.Hit)
            {
                if (string.Equals(existing.Metadata!.PayloadSha256, artifact.ArtifactSha256, StringComparison.Ordinal))
                {
                    return new TensorRtCudaCacheResult(TensorRtExternalCacheStatus.AlreadyPresent, existing.Artifact, existing.Metadata);
                }
                if (Options.ConflictPolicy == TensorRtExternalCacheConflictPolicy.Reject)
                {
                    return new TensorRtCudaCacheResult(TensorRtExternalCacheStatus.Conflict, rejectionReason: TensorRtExternalCacheRejectionReason.PayloadConflict);
                }
            }
            else if (existing.Status == TensorRtExternalCacheStatus.Rejected) return existing;

            EntryWriteResult written = PublishEntry(
                "cuda",
                identity.LookupKeySha256,
                identity.ArtifactKind == TensorRtCudaRtcArtifactKind.Ptx ? ".ptx" : ".cubin",
                stream => stream.Write(payload, 0, payload.Length),
                (writer, payloadLength, payloadSha256) => WriteCudaManifest(writer, identity, artifact, completeIdentity.CacheKeySha256, payloadLength, payloadSha256),
                Options.MaximumCudaArtifactBytes,
                cancellationToken);
            TensorRtExternalCacheEntryMetadata metadata = written.ToMetadata(completeIdentity.CacheKeySha256);
            return new TensorRtCudaCacheResult(TensorRtExternalCacheStatus.Stored, artifact, metadata);
        }

        private TensorRtEngineCacheResult OpenEngineCore(TensorRtEngineCacheIdentity identity, CancellationToken cancellationToken)
        {
            EntryReadResult read = ReadEntry("engine", identity.LookupKeySha256, Options.MaximumEngineBytes, cancellationToken, keepPayloadOpen: true);
            if (read.Status != TensorRtExternalCacheStatus.Hit)
            {
                read.Dispose();
                return new TensorRtEngineCacheResult(read.Status, rejectionReason: read.RejectionReason, remediation: read.Remediation, remediationPath: read.RemediationPath);
            }
            try
            {
                ValidateEngineManifest(read.Manifest!, identity);
                TensorRtExternalCacheEntryMetadata metadata = CreateMetadata(read, null);
                Stream stream = read.DetachPayloadStream();
                return new TensorRtEngineCacheResult(TensorRtExternalCacheStatus.Hit, stream, metadata);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                read.Dispose();
                TensorRtCudaCacheResult rejected = Reject("engine", identity.LookupKeySha256, TensorRtExternalCacheRejectionReason.IdentityMismatch);
                return new TensorRtEngineCacheResult(rejected.Status, rejectionReason: rejected.RejectionReason, remediation: rejected.Remediation, remediationPath: rejected.RemediationPath);
            }
            finally
            {
                read.Dispose();
            }
        }

        private TensorRtEngineCacheResult StoreEngineCore(TensorRtEngineCacheIdentity identity, Stream payload, CancellationToken cancellationToken)
        {
            TensorRtEngineCacheResult existing = OpenEngineCore(identity, cancellationToken);
            if (existing.Status == TensorRtExternalCacheStatus.Rejected) return existing;
            existing.Dispose();

            EntryWriteResult written = PublishEntry(
                "engine",
                identity.LookupKeySha256,
                identity.ArtifactExtension,
                stream => CopyBounded(payload, stream, Options.MaximumEngineBytes, cancellationToken),
                (writer, payloadLength, payloadSha256) => WriteEngineManifest(writer, identity, payloadLength, payloadSha256),
                Options.MaximumEngineBytes,
                cancellationToken,
                existing.Status == TensorRtExternalCacheStatus.Hit);

            if (written.ExistingPayloadSha256 != null)
            {
                if (string.Equals(written.ExistingPayloadSha256, written.PayloadSha256, StringComparison.Ordinal))
                {
                    TensorRtEngineCacheResult current = OpenEngineCore(identity, cancellationToken);
                    if (current.Status != TensorRtExternalCacheStatus.Hit) return current;
                    TensorRtExternalCacheEntryMetadata metadata = current.Metadata!;
                    current.Dispose();
                    return new TensorRtEngineCacheResult(TensorRtExternalCacheStatus.AlreadyPresent, metadata: metadata);
                }
                if (Options.ConflictPolicy == TensorRtExternalCacheConflictPolicy.Reject)
                {
                    return new TensorRtEngineCacheResult(TensorRtExternalCacheStatus.Conflict, rejectionReason: TensorRtExternalCacheRejectionReason.PayloadConflict);
                }
            }
            return new TensorRtEngineCacheResult(TensorRtExternalCacheStatus.Stored, metadata: written.ToMetadata(null));
        }

        private TensorRtExternalCacheResult Invalidate(string category, string lookupKeySha256, CancellationToken cancellationToken)
        {
            using GateScope scope = Enter(category + ":" + lookupKeySha256, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string entryPath = GetEntryPath(category, lookupKeySha256, createParents: false);
            if (!Directory.Exists(entryPath)) return new TensorRtExternalCacheResult(TensorRtExternalCacheStatus.NotFound);
            try
            {
                ValidateDirectoryPath(entryPath);
                string currentPath = CombineContained(entryPath, CurrentFileName);
                bool existed = File.Exists(currentPath);
                if (existed) File.Delete(currentPath);
                TryDeleteDirectory(entryPath);
                return new TensorRtExternalCacheResult(existed ? TensorRtExternalCacheStatus.Deleted : TensorRtExternalCacheStatus.NotFound);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                throw CacheFailure(TensorRtErrorCodes.ExternalCacheIoFailed, "The External cache entry could not be invalidated.", entryPath, exception);
            }
        }

        private EntryWriteResult PublishEntry(
            string category,
            string lookupKeySha256,
            string extension,
            Action<FileStream> writePayload,
            Action<Utf8JsonWriter, long, string> writeManifestFields,
            long maximumPayloadBytes,
            CancellationToken cancellationToken,
            bool hasExistingEntry = false)
        {
            string entryPath = GetEntryPath(category, lookupKeySha256, createParents: true);
            using EntryActivity writerActivity = EnterEntryActivity(entryPath, "writer", createEntry: true, cancellationToken)!;
            CleanupEntry(entryPath);
            string id = Guid.NewGuid().ToString("N");
            string temporaryDirectory = CombineContained(entryPath, "tmp-" + id);
            string generationName = "g-" + id;
            string generationPath = CombineContained(entryPath, generationName);
            string temporaryCurrentPath = CombineContained(entryPath, ".current-" + id + ".tmp");
            bool generationPublished = false;
            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                ValidateDirectoryPath(temporaryDirectory);
                string payloadFileName = "artifact" + extension;
                string payloadPath = CombineContained(temporaryDirectory, payloadFileName);
                long payloadLength;
                string payloadSha256;
                using (var payloadStream = new FileStream(payloadPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.SequentialScan))
                {
                    writePayload(payloadStream);
                    cancellationToken.ThrowIfCancellationRequested();
                    payloadStream.Flush(true);
                    payloadLength = payloadStream.Length;
                }
                if (payloadLength < 1 || payloadLength > maximumPayloadBytes)
                {
                    throw CacheFailure(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, "The cache payload is empty or exceeds the configured limit.", "bytes=" + payloadLength);
                }
                payloadSha256 = ComputeFileSha256(payloadPath, maximumPayloadBytes, cancellationToken);

                string? existingPayloadSha256 = null;
                if (hasExistingEntry)
                {
                    EntryReadResult existing = ReadEntry(category, lookupKeySha256, maximumPayloadBytes, cancellationToken, keepPayloadOpen: false);
                    try
                    {
                        if (existing.Status == TensorRtExternalCacheStatus.Hit) existingPayloadSha256 = existing.PayloadSha256;
                    }
                    finally { existing.Dispose(); }
                    if (existingPayloadSha256 != null && string.Equals(existingPayloadSha256, payloadSha256, StringComparison.Ordinal))
                    {
                        return EntryWriteResult.Existing(category, lookupKeySha256, extension, payloadLength, payloadSha256, existingPayloadSha256);
                    }
                    if (existingPayloadSha256 != null && Options.ConflictPolicy == TensorRtExternalCacheConflictPolicy.Reject)
                    {
                        return EntryWriteResult.Existing(category, lookupKeySha256, extension, payloadLength, payloadSha256, existingPayloadSha256);
                    }
                }

                byte[] manifestBytes;
                using (var memory = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();
                        writer.WriteNumber("schemaVersion", SchemaVersion);
                        writer.WriteString("entryKind", CategoryEntryKind(category, extension).ToString());
                        writer.WriteString("lookupKeySha256", lookupKeySha256);
                        writer.WriteString("artifactExtension", extension);
                        writer.WriteString("createdUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                        writer.WriteStartObject("payload");
                        writer.WriteString("fileName", payloadFileName);
                        writer.WriteNumber("length", payloadLength);
                        writer.WriteString("sha256", payloadSha256);
                        writer.WriteEndObject();
                        writeManifestFields(writer, payloadLength, payloadSha256);
                        writer.WriteEndObject();
                    }
                    manifestBytes = memory.ToArray();
                }
                if (manifestBytes.Length > Options.MaximumManifestBytes)
                {
                    throw CacheFailure(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, "The generated cache manifest exceeds the configured limit.", "bytes=" + manifestBytes.Length);
                }
                string manifestPath = CombineContained(temporaryDirectory, ManifestFileName);
                WriteAllBytesFlushed(manifestPath, manifestBytes);
                string manifestSha256 = HashBytes(manifestBytes);
                cancellationToken.ThrowIfCancellationRequested();

                Directory.Move(temporaryDirectory, generationPath);
                generationPublished = true;
                byte[] completionBytes = CreateCompletionBytes(generationName, manifestBytes.LongLength, manifestSha256, payloadLength, payloadSha256);
                WriteAllBytesFlushed(temporaryCurrentPath, completionBytes);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryCurrentPath, CombineContained(entryPath, CurrentFileName), true);
                generationPublished = false;
                CleanupEntry(entryPath);
                DateTimeOffset createdUtc;
                using (JsonDocument generatedManifest = JsonDocument.Parse(manifestBytes))
                {
                    createdUtc = DateTimeOffset.Parse(GetRequiredString(generatedManifest.RootElement, "createdUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
                return new EntryWriteResult(
                    CategoryEntryKind(category, extension),
                    lookupKeySha256,
                    extension,
                    payloadLength,
                    payloadSha256,
                    manifestBytes.LongLength,
                    manifestSha256,
                    createdUtc,
                    null);
            }
            catch (OperationCanceledException) { throw; }
            catch (TensorRtBackendException) { throw; }
            catch (Exception exception)
            {
                throw CacheFailure(TensorRtErrorCodes.ExternalCacheIoFailed, "The External cache entry could not be atomically published.", entryPath, exception);
            }
            finally
            {
                TryDeleteFile(temporaryCurrentPath);
                TryDeleteDirectory(temporaryDirectory);
                if (generationPublished) TryDeleteDirectory(generationPath);
            }
        }

        private EntryReadResult ReadEntry(
            string category,
            string lookupKeySha256,
            long maximumPayloadBytes,
            CancellationToken cancellationToken,
            bool keepPayloadOpen)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string entryPath = GetEntryPath(category, lookupKeySha256, createParents: false);
            string currentPath = CombineContained(entryPath, CurrentFileName);
            if (!Directory.Exists(entryPath)) return EntryReadResult.Miss();
            EntryActivity? readerActivity = EnterEntryActivity(entryPath, "reader", createEntry: false, cancellationToken);
            if (readerActivity == null) return EntryReadResult.Miss();
            bool activityTransferred = false;
            try
            {
            if (Directory.Exists(currentPath)) return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.UnsafePath);
            if (!File.Exists(currentPath)) return EntryReadResult.Miss();
            try
            {
                ValidateDirectoryPath(entryPath);
                ValidateRegularFile(currentPath);
                byte[] completionBytes = ReadBoundedFile(currentPath, Options.MaximumManifestBytes, cancellationToken);
                using JsonDocument completion = ParseStrict(completionBytes);
                JsonElement current = completion.RootElement;
                RequireExactProperties(current, "schemaVersion", "generation", "manifestFileName", "manifestLength", "manifestSha256", "payloadLength", "payloadSha256");
                if (GetRequiredInt32(current, "schemaVersion") != SchemaVersion) return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.ManifestInvalid);
                string generation = GetRequiredString(current, "generation");
                if (!IsGenerationName(generation)) return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.UnsafePath);
                if (!string.Equals(GetRequiredString(current, "manifestFileName"), ManifestFileName, StringComparison.Ordinal)) return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.ManifestInvalid);
                long expectedManifestLength = GetRequiredInt64(current, "manifestLength");
                string expectedManifestSha256 = GetRequiredSha256(current, "manifestSha256");
                long expectedPayloadLength = GetRequiredInt64(current, "payloadLength");
                string expectedPayloadSha256 = GetRequiredSha256(current, "payloadSha256");
                if (expectedManifestLength < 1 || expectedManifestLength > Options.MaximumManifestBytes || expectedPayloadLength < 1 || expectedPayloadLength > maximumPayloadBytes)
                {
                    return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.SizeLimitExceeded);
                }

                string generationPath = CombineContained(entryPath, generation);
                ValidateDirectoryPath(generationPath);
                string manifestPath = CombineContained(generationPath, ManifestFileName);
                ValidateRegularFile(manifestPath);
                if (new FileInfo(manifestPath).Length != expectedManifestLength)
                {
                    return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.IntegrityMismatch);
                }
                byte[] manifestBytes = ReadExactFile(manifestPath, expectedManifestLength, Options.MaximumManifestBytes, cancellationToken);
                if (!string.Equals(HashBytes(manifestBytes), expectedManifestSha256, StringComparison.Ordinal)) return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.IntegrityMismatch);
                JsonDocument manifest = ParseStrict(manifestBytes);
                ValidateCommonManifest(manifest.RootElement, category, lookupKeySha256, expectedPayloadLength, expectedPayloadSha256);
                JsonElement payload = manifest.RootElement.GetProperty("payload");
                string payloadFileName = GetRequiredString(payload, "fileName");
                if (!IsPayloadFileName(payloadFileName))
                {
                    manifest.Dispose();
                    return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.UnsafePath);
                }
                string payloadPath = CombineContained(generationPath, payloadFileName);
                ValidateRegularFile(payloadPath);
                var payloadStream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
                try
                {
                    if (payloadStream.Length != expectedPayloadLength)
                    {
                        payloadStream.Dispose();
                        manifest.Dispose();
                        return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.IntegrityMismatch);
                    }
                    string actualPayloadSha256 = ComputeStreamSha256(payloadStream, maximumPayloadBytes, cancellationToken);
                    if (!string.Equals(actualPayloadSha256, expectedPayloadSha256, StringComparison.Ordinal))
                    {
                        payloadStream.Dispose();
                        manifest.Dispose();
                        return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.IntegrityMismatch);
                    }
                    payloadStream.Position = 0;
                    if (keepPayloadOpen)
                    {
                        activityTransferred = true;
                        return EntryReadResult.Hit(manifest, payloadStream, null, generation, actualPayloadSha256, expectedPayloadLength, expectedManifestSha256, expectedManifestLength);
                    }
                    byte[] bytes = new byte[checked((int)expectedPayloadLength)];
                    ReadExactly(payloadStream, bytes, cancellationToken);
                    payloadStream.Dispose();
                    activityTransferred = true;
                    return EntryReadResult.Hit(manifest, null, bytes, generation, actualPayloadSha256, expectedPayloadLength, expectedManifestSha256, expectedManifestLength);
                }
                catch
                {
                    payloadStream.Dispose();
                    manifest.Dispose();
                    throw;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (CacheIdentityMismatchException) { return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.IdentityMismatch); }
            catch (JsonException) { return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.ManifestInvalid); }
            catch (InvalidDataException) { return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.ManifestInvalid); }
            catch (IOException) { return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.IntegrityMismatch); }
            catch (UnauthorizedAccessException) { return RejectReadAfterActivity(readerActivity, category, lookupKeySha256, TensorRtExternalCacheRejectionReason.UnsafePath); }
            }
            finally
            {
                if (!activityTransferred) readerActivity.Dispose();
            }
        }

        private EntryReadResult RejectReadAfterActivity(EntryActivity activity, string category, string key, TensorRtExternalCacheRejectionReason reason)
        {
            activity.Dispose();
            return RejectRead(category, key, reason);
        }

        private EntryActivity? EnterEntryActivity(string entryPath, string role, bool createEntry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(entryPath))
            {
                if (!createEntry) return null;
                Directory.CreateDirectory(entryPath);
            }
            ValidateDirectoryPath(entryPath);
            // Cross-process coordination is deliberately outside this library. Same-process
            // Cross-process coordination files are intentionally outside this single-process store.
            return new EntryActivity();
        }

        private EntryReadResult RejectRead(string category, string key, TensorRtExternalCacheRejectionReason reason)
        {
            TensorRtCudaCacheResult rejected = Reject(category, key, reason);
            return EntryReadResult.Rejected(reason, rejected.Remediation, rejected.RemediationPath);
        }

        private TensorRtCudaCacheResult Reject(string category, string key, TensorRtExternalCacheRejectionReason reason)
        {
            TensorRtExternalCacheRemediation remediation = TensorRtExternalCacheRemediation.None;
            string? remediationPath = null;
            string entryPath = GetEntryPath(category, key, createParents: false);
            if (Options.RejectedEntryPolicy == TensorRtExternalCacheRejectedEntryPolicy.Delete)
            {
                try
                {
                    if (Directory.Exists(entryPath))
                    {
                        ValidateTreeHasNoReparsePoints(entryPath);
                        string current = CombineContained(entryPath, CurrentFileName);
                        TryDeleteFile(current);
                        Directory.Delete(entryPath, true);
                    }
                    remediation = TensorRtExternalCacheRemediation.Deleted;
                }
                catch { remediation = TensorRtExternalCacheRemediation.Failed; }
            }
            else if (Options.RejectedEntryPolicy == TensorRtExternalCacheRejectedEntryPolicy.Quarantine)
            {
                try
                {
                    if (Directory.Exists(entryPath))
                    {
                        ValidateTreeHasNoReparsePoints(entryPath);
                        string quarantineRoot = CombineContained(LayoutPath, "quarantine");
                        EnsureSafeDirectory(quarantineRoot, create: true);
                        remediationPath = CombineContained(quarantineRoot, category + "-" + key + "-" + Guid.NewGuid().ToString("N"));
                        Directory.Move(entryPath, remediationPath);
                    }
                    remediation = TensorRtExternalCacheRemediation.Quarantined;
                }
                catch
                {
                    remediation = TensorRtExternalCacheRemediation.Failed;
                    remediationPath = null;
                }
            }
            return new TensorRtCudaCacheResult(TensorRtExternalCacheStatus.Rejected, rejectionReason: reason, remediation: remediation, remediationPath: remediationPath);
        }

        private static void ValidateCudaArtifact(TensorRtCudaKernelLookupIdentity identity, TensorRtCudaRtcArtifact artifact)
        {
            bool kernelMatches = identity.KernelNameExpression == null
                ? string.Equals(identity.KernelName, artifact.KernelName, StringComparison.Ordinal)
                : !string.IsNullOrWhiteSpace(artifact.KernelName);
            if (artifact.Role != identity.Role || artifact.Kind != identity.ArtifactKind ||
                !string.Equals(artifact.SourceSha256, identity.SourceSha256, StringComparison.Ordinal) ||
                !string.Equals(artifact.HeadersSha256, identity.HeadersSha256, StringComparison.Ordinal) ||
                !string.Equals(artifact.OptionsSha256, identity.OptionsSha256, StringComparison.Ordinal) ||
                !string.Equals(artifact.CompilerVersion, identity.CompilerVersion, StringComparison.Ordinal) ||
                !string.Equals(artifact.TargetArchitecture, identity.TargetArchitecture, StringComparison.Ordinal) ||
                !string.Equals(artifact.ProgramName, identity.ProgramName, StringComparison.Ordinal) ||
                !string.Equals(artifact.KernelNameExpression, identity.KernelNameExpression, StringComparison.Ordinal) || !kernelMatches)
            {
                throw CacheFailure(TensorRtErrorCodes.ExternalCacheEntryRejected, "The CUDA artifact does not match the requested cache lookup identity.", identity.LookupKeySha256);
            }
        }

        private static void ValidateCommonManifest(JsonElement root, string category, string key, long payloadLength, string payloadSha256)
        {
            string detailsProperty = category == "cuda" ? "cuda" : "engine";
            RequireExactProperties(root, "schemaVersion", "entryKind", "lookupKeySha256", "artifactExtension", "createdUtc", "payload", detailsProperty);
            if (GetRequiredInt32(root, "schemaVersion") != SchemaVersion) throw new InvalidDataException("Unsupported cache manifest schema.");
            if (!string.Equals(GetRequiredSha256(root, "lookupKeySha256"), key, StringComparison.Ordinal)) throw new CacheIdentityMismatchException("Cache manifest key mismatch.");
            _ = DateTimeOffset.ParseExact(GetRequiredString(root, "createdUtc"), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            JsonElement payload = root.GetProperty("payload");
            RequireExactProperties(payload, "fileName", "length", "sha256");
            string payloadFileName = GetRequiredString(payload, "fileName");
            if (!IsPayloadFileName(payloadFileName)) throw new UnauthorizedAccessException("The cache manifest contains an unsafe payload file name.");
            if (GetRequiredInt64(payload, "length") != payloadLength || !string.Equals(GetRequiredSha256(payload, "sha256"), payloadSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Cache payload integrity metadata mismatch.");
            }
            string extension = GetRequiredString(root, "artifactExtension");
            if (!string.Equals(Path.GetExtension(payloadFileName), extension, StringComparison.Ordinal)) throw new InvalidDataException("Cache payload extension mismatch.");
            if (!Enum.TryParse(GetRequiredString(root, "entryKind"), ignoreCase: false, out TensorRtExternalCacheEntryKind entryKind)) throw new InvalidDataException("Unknown cache entry kind.");
            if (entryKind != CategoryEntryKind(category, extension)) throw new InvalidDataException("Cache entry kind mismatch.");
        }

        private static void ValidateCudaManifest(JsonDocument manifest, TensorRtCudaKernelLookupIdentity identity)
        {
            JsonElement cuda = manifest.RootElement.GetProperty("cuda");
            RequireExactProperties(cuda,
                "role", "sourceSha256", "headersSha256", "optionsSha256", "artifactSha256", "compilerVersion", "compilerIdentity",
                "targetArchitecture", "artifactKind", "programName", "kernelName", "kernelNameExpression", "resolvedKernelName",
                "cudaRuntimeVersion", "cudaRuntimeIdentity", "cudaDriverVersion", "cudaDriverIdentity", "gpuArchitecture", "gpuCompatibilityIdentity",
                "nativeBridgeIdentity", "cudaCacheKeySha256");
            RequireEqual(GetRequiredInt32(cuda, "role"), (int)identity.Role);
            RequireEqual(GetRequiredSha256(cuda, "sourceSha256"), identity.SourceSha256);
            RequireEqual(GetRequiredSha256(cuda, "headersSha256"), identity.HeadersSha256);
            RequireEqual(GetRequiredSha256(cuda, "optionsSha256"), identity.OptionsSha256);
            RequireEqual(GetRequiredString(cuda, "compilerVersion"), identity.CompilerVersion);
            RequireEqual(GetRequiredString(cuda, "compilerIdentity"), identity.CompilerIdentity);
            RequireEqual(GetRequiredString(cuda, "targetArchitecture"), identity.TargetArchitecture);
            RequireEqual(GetRequiredInt32(cuda, "artifactKind"), (int)identity.ArtifactKind);
            RequireEqual(GetRequiredString(cuda, "programName"), identity.ProgramName);
            RequireEqual(GetRequiredString(cuda, "kernelName"), identity.KernelName);
            RequireEqual(GetOptionalString(cuda, "kernelNameExpression"), identity.KernelNameExpression);
            RequireEqual(GetRequiredString(cuda, "cudaRuntimeVersion"), identity.CudaRuntimeVersion);
            RequireEqual(GetRequiredString(cuda, "cudaRuntimeIdentity"), identity.CudaRuntimeIdentity);
            RequireEqual(GetRequiredString(cuda, "cudaDriverVersion"), identity.CudaDriverVersion);
            RequireEqual(GetRequiredString(cuda, "cudaDriverIdentity"), identity.CudaDriverIdentity);
            RequireEqual(GetRequiredString(cuda, "gpuArchitecture"), identity.GpuArchitecture);
            RequireEqual(GetRequiredString(cuda, "gpuCompatibilityIdentity"), identity.GpuCompatibilityIdentity);
            RequireEqual(GetRequiredString(cuda, "nativeBridgeIdentity"), identity.NativeBridgeIdentity);
            RequireEqual(GetRequiredSha256(cuda, "artifactSha256"), GetRequiredSha256(manifest.RootElement.GetProperty("payload"), "sha256"));
        }

        private static void ValidateEngineManifest(JsonDocument manifest, TensorRtEngineCacheIdentity identity)
        {
            JsonElement engine = manifest.RootElement.GetProperty("engine");
            RequireExactProperties(engine,
                "adapterSchemaVersion", "onnxSha256", "managedBuildInputsSha256", "managedPackageIdentity", "managedApiContractSha256",
                "tensorRtVersion", "tensorRtIdentity", "cudaRuntimeVersion", "cudaRuntimeIdentity", "cudnnVersion", "cudnnIdentity",
                "cudaDriverVersion", "cudaDriverIdentity", "nativeBridgeIdentity", "gpuCompatibilityIdentity", "gpuComputeCapability", "operatingSystem",
                "processArchitecture", "apiVersion", "precision", "workspaceBytes", "optimizationLevel", "stronglyTypedNetwork",
                "profilesSha256", "builderFlagsSha256", "profiles", "builderFlags");
            RequireEqual(GetRequiredString(engine, "adapterSchemaVersion"), identity.AdapterSchemaVersion);
            RequireEqual(GetRequiredSha256(engine, "onnxSha256"), identity.OnnxSha256);
            RequireEqual(GetRequiredSha256(engine, "managedBuildInputsSha256"), identity.ManagedBuildInputsSha256);
            RequireEqual(GetRequiredString(engine, "managedPackageIdentity"), identity.ManagedPackageIdentity);
            RequireEqual(GetRequiredSha256(engine, "managedApiContractSha256"), identity.ManagedApiContractSha256);
            RequireEqual(GetRequiredString(engine, "tensorRtVersion"), identity.TensorRtVersion);
            RequireEqual(GetRequiredString(engine, "tensorRtIdentity"), identity.TensorRtIdentity);
            RequireEqual(GetRequiredString(engine, "cudaRuntimeVersion"), identity.CudaRuntimeVersion);
            RequireEqual(GetRequiredString(engine, "cudaRuntimeIdentity"), identity.CudaRuntimeIdentity);
            RequireEqual(GetRequiredString(engine, "cudnnVersion"), identity.CudnnVersion);
            RequireEqual(GetRequiredString(engine, "cudnnIdentity"), identity.CudnnIdentity);
            RequireEqual(GetRequiredString(engine, "cudaDriverVersion"), identity.CudaDriverVersion);
            RequireEqual(GetRequiredString(engine, "cudaDriverIdentity"), identity.CudaDriverIdentity);
            RequireEqual(GetRequiredString(engine, "nativeBridgeIdentity"), identity.NativeBridgeIdentity);
            RequireEqual(GetRequiredString(engine, "gpuCompatibilityIdentity"), identity.GpuCompatibilityIdentity);
            RequireEqual(GetRequiredString(engine, "gpuComputeCapability"), identity.GpuComputeCapability);
            RequireEqual(GetRequiredString(engine, "operatingSystem"), identity.OperatingSystem);
            RequireEqual(GetRequiredString(engine, "processArchitecture"), identity.ProcessArchitecture);
            RequireEqual(GetRequiredInt32(engine, "apiVersion"), (int)identity.BuildOptions.ApiVersion);
            RequireEqual(GetRequiredInt32(engine, "precision"), (int)identity.BuildOptions.Precision);
            RequireEqual(GetRequiredUInt64(engine, "workspaceBytes"), identity.BuildOptions.WorkspaceBytes);
            RequireEqual(GetRequiredInt32(engine, "optimizationLevel"), identity.BuildOptions.OptimizationLevel);
            RequireEqual(GetRequiredBoolean(engine, "stronglyTypedNetwork"), identity.BuildOptions.StronglyTypedNetwork);
            RequireEqual(GetRequiredSha256(engine, "profilesSha256"), identity.ProfilesSha256);
            RequireEqual(GetRequiredSha256(engine, "builderFlagsSha256"), identity.BuilderFlagsSha256);
            ValidateProfiles(engine.GetProperty("profiles"), identity.InputProfiles);
            ValidateStrings(engine.GetProperty("builderFlags"), identity.BuilderFlags);
        }

        private static void WriteCudaManifest(
            Utf8JsonWriter writer,
            TensorRtCudaKernelLookupIdentity identity,
            TensorRtCudaRtcArtifact artifact,
            string cudaCacheKeySha256,
            long payloadLength,
            string payloadSha256)
        {
            writer.WriteStartObject("cuda");
            writer.WriteNumber("role", (int)identity.Role);
            writer.WriteString("sourceSha256", identity.SourceSha256);
            writer.WriteString("headersSha256", identity.HeadersSha256);
            writer.WriteString("optionsSha256", identity.OptionsSha256);
            writer.WriteString("artifactSha256", artifact.ArtifactSha256);
            writer.WriteString("compilerVersion", identity.CompilerVersion);
            writer.WriteString("compilerIdentity", identity.CompilerIdentity);
            writer.WriteString("targetArchitecture", identity.TargetArchitecture);
            writer.WriteNumber("artifactKind", (int)identity.ArtifactKind);
            writer.WriteString("programName", identity.ProgramName);
            writer.WriteString("kernelName", identity.KernelName);
            if (identity.KernelNameExpression == null) writer.WriteNull("kernelNameExpression"); else writer.WriteString("kernelNameExpression", identity.KernelNameExpression);
            writer.WriteString("resolvedKernelName", artifact.KernelName);
            writer.WriteString("cudaRuntimeVersion", identity.CudaRuntimeVersion);
            writer.WriteString("cudaRuntimeIdentity", identity.CudaRuntimeIdentity);
            writer.WriteString("cudaDriverVersion", identity.CudaDriverVersion);
            writer.WriteString("cudaDriverIdentity", identity.CudaDriverIdentity);
            writer.WriteString("gpuArchitecture", identity.GpuArchitecture);
            writer.WriteString("gpuCompatibilityIdentity", identity.GpuCompatibilityIdentity);
            writer.WriteString("nativeBridgeIdentity", identity.NativeBridgeIdentity);
            writer.WriteString("cudaCacheKeySha256", cudaCacheKeySha256);
            writer.WriteEndObject();
        }

        private static void WriteEngineManifest(Utf8JsonWriter writer, TensorRtEngineCacheIdentity identity, long payloadLength, string payloadSha256)
        {
            writer.WriteStartObject("engine");
            writer.WriteString("adapterSchemaVersion", identity.AdapterSchemaVersion);
            writer.WriteString("onnxSha256", identity.OnnxSha256);
            writer.WriteString("managedBuildInputsSha256", identity.ManagedBuildInputsSha256);
            writer.WriteString("managedPackageIdentity", identity.ManagedPackageIdentity);
            writer.WriteString("managedApiContractSha256", identity.ManagedApiContractSha256);
            writer.WriteString("tensorRtVersion", identity.TensorRtVersion);
            writer.WriteString("tensorRtIdentity", identity.TensorRtIdentity);
            writer.WriteString("cudaRuntimeVersion", identity.CudaRuntimeVersion);
            writer.WriteString("cudaRuntimeIdentity", identity.CudaRuntimeIdentity);
            writer.WriteString("cudnnVersion", identity.CudnnVersion);
            writer.WriteString("cudnnIdentity", identity.CudnnIdentity);
            writer.WriteString("cudaDriverVersion", identity.CudaDriverVersion);
            writer.WriteString("cudaDriverIdentity", identity.CudaDriverIdentity);
            writer.WriteString("nativeBridgeIdentity", identity.NativeBridgeIdentity);
            writer.WriteString("gpuCompatibilityIdentity", identity.GpuCompatibilityIdentity);
            writer.WriteString("gpuComputeCapability", identity.GpuComputeCapability);
            writer.WriteString("operatingSystem", identity.OperatingSystem);
            writer.WriteString("processArchitecture", identity.ProcessArchitecture);
            writer.WriteNumber("apiVersion", (int)identity.BuildOptions.ApiVersion);
            writer.WriteNumber("precision", (int)identity.BuildOptions.Precision);
            writer.WriteNumber("workspaceBytes", identity.BuildOptions.WorkspaceBytes);
            writer.WriteNumber("optimizationLevel", identity.BuildOptions.OptimizationLevel);
            writer.WriteBoolean("stronglyTypedNetwork", identity.BuildOptions.StronglyTypedNetwork);
            writer.WriteString("profilesSha256", identity.ProfilesSha256);
            writer.WriteString("builderFlagsSha256", identity.BuilderFlagsSha256);
            writer.WriteStartArray("profiles");
            foreach (TensorRtOnnxInputProfile profile in identity.InputProfiles.OrderBy(item => item.InputName, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("inputName", profile.InputName);
                WriteShape(writer, "minimum", profile.Minimum);
                WriteShape(writer, "optimum", profile.Optimum);
                WriteShape(writer, "maximum", profile.Maximum);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("builderFlags");
            foreach (string flag in identity.BuilderFlags) writer.WriteStringValue(flag);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private TensorRtExternalCacheEntryMetadata CreateMetadata(EntryReadResult read, string? cudaCacheKeySha256)
        {
            JsonElement root = read.Manifest!.RootElement;
            return new TensorRtExternalCacheEntryMetadata(
                Enum.Parse<TensorRtExternalCacheEntryKind>(GetRequiredString(root, "entryKind"), ignoreCase: false),
                GetRequiredSha256(root, "lookupKeySha256"),
                read.PayloadSha256!,
                read.PayloadLength,
                read.ManifestSha256!,
                read.ManifestLength,
                GetRequiredString(root, "artifactExtension"),
                DateTimeOffset.Parse(GetRequiredString(root, "createdUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                cudaCacheKeySha256);
        }

        private string GetEntryPath(string category, string lookupKeySha256, bool createParents)
        {
            TensorRtContractHash.ValidateSha256(lookupKeySha256, nameof(lookupKeySha256));
            if (category != "cuda" && category != "engine") throw new ArgumentException("Unknown cache category.", nameof(category));
            string categoryPath = CombineContained(LayoutPath, category);
            string shardPath = CombineContained(categoryPath, lookupKeySha256.Substring(0, 2));
            string entryPath = CombineContained(shardPath, lookupKeySha256);
            if (createParents)
            {
                EnsureSafeDirectory(categoryPath, create: true);
                EnsureSafeDirectory(shardPath, create: true);
                EnsureSafeDirectory(entryPath, create: true);
            }
            return entryPath;
        }

        private void CleanupEntry(string entryPath)
        {
            if (!Options.CleanupStaleTemporaryEntries || !Directory.Exists(entryPath)) return;
            string? currentGeneration = null;
            try
            {
                string currentPath = CombineContained(entryPath, CurrentFileName);
                if (File.Exists(currentPath))
                {
                    byte[] bytes = ReadBoundedFile(currentPath, Options.MaximumManifestBytes, CancellationToken.None);
                    using JsonDocument document = ParseStrict(bytes);
                    string candidate = GetRequiredString(document.RootElement, "generation");
                    if (IsGenerationName(candidate)) currentGeneration = candidate;
                }
            }
            catch { }

            DateTime threshold = DateTime.UtcNow - Options.TemporaryEntryRetention;
            foreach (string directory in Directory.EnumerateDirectories(entryPath))
            {
                string name = Path.GetFileName(directory);
                if ((!name.StartsWith("tmp-", StringComparison.Ordinal) && !name.StartsWith("g-", StringComparison.Ordinal)) || string.Equals(name, currentGeneration, StringComparison.Ordinal)) continue;
                try
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                    if (Directory.GetLastWriteTimeUtc(directory) <= threshold) Directory.Delete(directory, true);
                }
                catch { }
            }
            foreach (string file in Directory.EnumerateFiles(entryPath, ".current-*.tmp"))
            {
                try
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                    if (File.GetLastWriteTimeUtc(file) <= threshold) File.Delete(file);
                }
                catch { }
            }
        }

        private GateScope Enter(string key, CancellationToken cancellationToken)
        {
            GateState state;
            long observedFailureVersion;
            string gateRoot = OperatingSystem.IsWindows() ? RootPath.ToUpperInvariant() : RootPath;
            string gateKey = gateRoot + "\0" + key;
            lock (GateSync)
            {
                if (!Gates.TryGetValue(gateKey, out state!))
                {
                    state = new GateState();
                    Gates.Add(gateKey, state);
                }
                state.ReferenceCount++;
                observedFailureVersion = state.FactoryFailureVersion;
            }
            try
            {
                state.Semaphore.Wait(cancellationToken);
                return new GateScope(this, gateKey, state, observedFailureVersion);
            }
            catch
            {
                ReleaseReference(gateKey, state, releaseSemaphore: false);
                throw;
            }
        }

        private void ReleaseReference(string key, GateState state, bool releaseSemaphore)
        {
            if (releaseSemaphore) state.Semaphore.Release();
            bool dispose = false;
            lock (GateSync)
            {
                state.ReferenceCount--;
                if (state.ReferenceCount == 0 && Gates.TryGetValue(key, out GateState? current) && ReferenceEquals(current, state))
                {
                    Gates.Remove(key);
                    dispose = true;
                }
            }
            if (dispose) state.Semaphore.Dispose();
        }

        private static string ValidateAndPrepareRoot(string rootPath, bool create)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("An explicit caller-owned cache root is required.", nameof(rootPath));
            if (!Path.IsPathFullyQualified(rootPath)) throw CacheFailure(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, "The External cache root must be an absolute path.", rootPath);
            string fullPath;
            try { fullPath = Path.GetFullPath(rootPath); }
            catch (Exception exception) { throw CacheFailure(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, "The External cache root is invalid.", rootPath, exception); }
            if (File.Exists(fullPath)) throw CacheFailure(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, "The External cache root cannot be a file.", fullPath);
            if (!Directory.Exists(fullPath))
            {
                if (!create) throw CacheFailure(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, "The External cache root does not exist and creation is disabled.", fullPath);
                Directory.CreateDirectory(fullPath);
            }
            ValidateDirectoryPath(fullPath);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static void ValidateDirectoryPath(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            string? current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                FileAttributes attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new UnauthorizedAccessException("Cache directory paths cannot contain files or reparse points.");
                }
                DirectoryInfo? parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
        }

        private static void EnsureSafeDirectory(string path, bool create)
        {
            if (!Directory.Exists(path))
            {
                if (!create) throw new DirectoryNotFoundException(path);
                Directory.CreateDirectory(path);
            }
            ValidateDirectoryPath(path);
        }

        private static void ValidateRegularFile(string path)
        {
            if (Directory.Exists(path)) throw new UnauthorizedAccessException("Cache files cannot be directories.");
            if (!File.Exists(path)) throw new FileNotFoundException("A regular cache file is required.", path);
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new UnauthorizedAccessException("Cache files cannot be directories or reparse points.");
            ValidateSingleLink(path);
        }

        private static void ValidateSingleLink(string path)
        {
            if (!OperatingSystem.IsWindows()) return;
            using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
            {
                throw new IOException("The cache file link count could not be validated.", new Win32Exception(Marshal.GetLastWin32Error()));
            }
            if (information.NumberOfLinks != 1) throw new UnauthorizedAccessException("Cache payload and metadata files cannot be hard links.");
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        private static string ValidateInputEnginePath(string path, string expectedExtension)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A caller-owned engine path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            ValidateRegularFile(fullPath);
            if (!string.Equals(Path.GetExtension(fullPath), expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The caller-owned engine extension does not match the cache identity.", nameof(path));
            }
            return fullPath;
        }

        private static string CombineContained(string parent, string child)
        {
            if (string.IsNullOrEmpty(child) || Path.IsPathRooted(child) || child.IndexOf(Path.DirectorySeparatorChar) >= 0 || child.IndexOf(Path.AltDirectorySeparatorChar) >= 0 || child == "." || child == "..")
            {
                throw new UnauthorizedAccessException("Cache layout names must be single relative path segments.");
            }
            string fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string combined = Path.GetFullPath(Path.Combine(fullParent, child));
            if (!combined.StartsWith(fullParent + Path.DirectorySeparatorChar, PathComparison)) throw new UnauthorizedAccessException("Cache path escaped its parent.");
            return combined;
        }

        private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static byte[] CreateCompletionBytes(string generationName, long manifestLength, string manifestSha256, long payloadLength, string payloadSha256)
        {
            using var memory = new MemoryStream();
            using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", SchemaVersion);
                writer.WriteString("generation", generationName);
                writer.WriteString("manifestFileName", ManifestFileName);
                writer.WriteNumber("manifestLength", manifestLength);
                writer.WriteString("manifestSha256", manifestSha256);
                writer.WriteNumber("payloadLength", payloadLength);
                writer.WriteString("payloadSha256", payloadSha256);
                writer.WriteEndObject();
            }
            return memory.ToArray();
        }

        private static void WriteAllBytesFlushed(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.SequentialScan);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        private static byte[] ReadBoundedFile(string path, long maximumBytes, CancellationToken cancellationToken)
        {
            ValidateRegularFile(path);
            long length = new FileInfo(path).Length;
            if (length < 1 || length > maximumBytes || length > int.MaxValue) throw new InvalidDataException("The cache file exceeds its size limit.");
            return ReadExactFile(path, length, maximumBytes, cancellationToken);
        }

        private static byte[] ReadExactFile(string path, long expectedLength, long maximumBytes, CancellationToken cancellationToken)
        {
            if (expectedLength < 1 || expectedLength > maximumBytes || expectedLength > int.MaxValue) throw new InvalidDataException("The cache file exceeds its size limit.");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
            if (stream.Length != expectedLength) throw new InvalidDataException("The cache file length changed.");
            byte[] bytes = new byte[checked((int)expectedLength)];
            ReadExactly(stream, bytes, cancellationToken);
            return bytes;
        }

        private static void ReadExactly(Stream stream, byte[] bytes, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < bytes.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) throw new EndOfStreamException("The cache file was truncated.");
                offset += read;
            }
            if (stream.ReadByte() != -1) throw new InvalidDataException("The cache file grew while it was read.");
        }

        private static void CopyBounded(Stream source, Stream destination, long maximumBytes, CancellationToken cancellationToken)
        {
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = source.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                total = checked(total + read);
                if (total > maximumBytes) throw CacheFailure(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, "The engine payload exceeds the configured cache limit.", "bytes>" + maximumBytes);
                destination.Write(buffer, 0, read);
            }
        }

        private static string ComputeFileSha256(string path, long maximumBytes, CancellationToken cancellationToken)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
            return ComputeStreamSha256(stream, maximumBytes, cancellationToken);
        }

        private static string ComputeStreamSha256(Stream stream, long maximumBytes, CancellationToken cancellationToken)
        {
            using SHA256 algorithm = SHA256.Create();
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                total = checked(total + read);
                if (total > maximumBytes) throw new InvalidDataException("The cache payload exceeds its size limit.");
                algorithm.TransformBlock(buffer, 0, read, null, 0);
            }
            algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return ToLowerHex(algorithm.Hash!);
        }

        private static string HashBytes(byte[] bytes)
        {
            using SHA256 algorithm = SHA256.Create();
            return ToLowerHex(algorithm.ComputeHash(bytes));
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static JsonDocument ParseStrict(byte[] bytes)
        {
            return JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 16 });
        }

        private static void RequireExactProperties(JsonElement element, params string[] expected)
        {
            if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("A JSON object is required.");
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!actual.Add(property.Name)) throw new InvalidDataException("Duplicate manifest property: " + property.Name);
            }
            if (actual.Count != expected.Length || expected.Any(name => !actual.Contains(name))) throw new InvalidDataException("The cache manifest contains missing or unknown fields.");
        }

        private static string GetRequiredString(JsonElement element, string name)
        {
            JsonElement value = element.GetProperty(name);
            if (value.ValueKind != JsonValueKind.String) throw new InvalidDataException("A JSON string is required: " + name);
            string? result = value.GetString();
            if (string.IsNullOrEmpty(result) || result.IndexOf('\0') >= 0) throw new InvalidDataException("A non-empty JSON string is required: " + name);
            return result;
        }

        private static string? GetOptionalString(JsonElement element, string name)
        {
            JsonElement value = element.GetProperty(name);
            if (value.ValueKind == JsonValueKind.Null) return null;
            return GetRequiredString(element, name);
        }

        private static string GetRequiredSha256(JsonElement element, string name)
        {
            string value = GetRequiredString(element, name);
            try { return TensorRtContractHash.ValidateSha256(value, name); }
            catch (ArgumentException exception) { throw new InvalidDataException("An exact lowercase SHA256 is required: " + name, exception); }
        }

        private static int GetRequiredInt32(JsonElement element, string name)
        {
            if (!element.GetProperty(name).TryGetInt32(out int value)) throw new InvalidDataException("An Int32 is required: " + name);
            return value;
        }

        private static long GetRequiredInt64(JsonElement element, string name)
        {
            if (!element.GetProperty(name).TryGetInt64(out long value)) throw new InvalidDataException("An Int64 is required: " + name);
            return value;
        }

        private static ulong GetRequiredUInt64(JsonElement element, string name)
        {
            if (!element.GetProperty(name).TryGetUInt64(out ulong value)) throw new InvalidDataException("a UInt64 is required: " + name);
            return value;
        }

        private static bool GetRequiredBoolean(JsonElement element, string name)
        {
            JsonElement value = element.GetProperty(name);
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False) throw new InvalidDataException("A Boolean is required: " + name);
            return value.GetBoolean();
        }

        private static void ValidateProfiles(JsonElement element, IReadOnlyList<TensorRtOnnxInputProfile> expected)
        {
            if (element.ValueKind != JsonValueKind.Array) throw new InvalidDataException("A profile array is required.");
            TensorRtOnnxInputProfile[] sorted = expected.OrderBy(item => item.InputName, StringComparer.Ordinal).ToArray();
            JsonElement.ArrayEnumerator enumerator = element.EnumerateArray();
            int index = 0;
            foreach (JsonElement item in enumerator)
            {
                if (index >= sorted.Length) throw new InvalidDataException("Too many cache profiles.");
                RequireExactProperties(item, "inputName", "minimum", "optimum", "maximum");
                RequireEqual(GetRequiredString(item, "inputName"), sorted[index].InputName);
                ValidateShape(item.GetProperty("minimum"), sorted[index].Minimum);
                ValidateShape(item.GetProperty("optimum"), sorted[index].Optimum);
                ValidateShape(item.GetProperty("maximum"), sorted[index].Maximum);
                index++;
            }
            if (index != sorted.Length) throw new InvalidDataException("Missing cache profiles.");
        }

        private static void ValidateShape(JsonElement element, JYPPX.DeploySharp.Tensors.TensorShape expected)
        {
            if (element.ValueKind != JsonValueKind.Array) throw new InvalidDataException("A shape array is required.");
            long[] dimensions = expected.ToArray();
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (index >= dimensions.Length || !item.TryGetInt64(out long value) || value != dimensions[index]) throw new InvalidDataException("Cache shape mismatch.");
                index++;
            }
            if (index != dimensions.Length) throw new InvalidDataException("Cache shape rank mismatch.");
        }

        private static void ValidateStrings(JsonElement element, IReadOnlyList<string> expected)
        {
            if (element.ValueKind != JsonValueKind.Array) throw new InvalidDataException("A string array is required.");
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (index >= expected.Count || item.ValueKind != JsonValueKind.String || !string.Equals(item.GetString(), expected[index], StringComparison.Ordinal)) throw new InvalidDataException("Cache string array mismatch.");
                index++;
            }
            if (index != expected.Count) throw new InvalidDataException("Cache string array length mismatch.");
        }

        private static void WriteShape(Utf8JsonWriter writer, string name, JYPPX.DeploySharp.Tensors.TensorShape shape)
        {
            writer.WriteStartArray(name);
            foreach (long dimension in shape.ToArray()) writer.WriteNumberValue(dimension);
            writer.WriteEndArray();
        }

        private static TensorRtExternalCacheEntryKind CategoryEntryKind(string category, string extension)
        {
            if (category == "cuda")
            {
                if (string.Equals(extension, ".ptx", StringComparison.Ordinal)) return TensorRtExternalCacheEntryKind.CudaPtx;
                if (string.Equals(extension, ".cubin", StringComparison.Ordinal)) return TensorRtExternalCacheEntryKind.CudaCubin;
            }
            else if (category == "engine")
            {
                if (string.Equals(extension, ".engine", StringComparison.Ordinal)) return TensorRtExternalCacheEntryKind.TensorRtEngine;
                if (string.Equals(extension, ".plan", StringComparison.Ordinal)) return TensorRtExternalCacheEntryKind.TensorRtPlan;
            }
            throw new InvalidDataException("The cache category and artifact extension are not allowlisted.");
        }

        private static bool IsGenerationName(string value) => value.Length == 34 && value.StartsWith("g-", StringComparison.Ordinal) && value.Skip(2).All(IsLowerHex);
        private static bool IsPayloadFileName(string value) => value == "artifact.ptx" || value == "artifact.cubin" || value == "artifact.engine" || value == "artifact.plan";
        private static bool IsLowerHex(char value) => (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f');

        private static void RequireEqual<T>(T actual, T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected)) throw new InvalidDataException("Cache identity mismatch.");
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) Directory.Delete(path, true);
            }
            catch { }
        }

        private static void ValidateTreeHasNoReparsePoints(string root)
        {
            ValidateDirectoryPath(root);
            foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new UnauthorizedAccessException("Rejected cache entries containing reparse points are not automatically remediated.");
                }
            }
        }

        private static TensorRtBackendException CacheFailure(string errorCode, string message, string? details, Exception? exception = null)
        {
            return new TensorRtBackendException(errorCode, message, exception, operation: "external-cache", technicalDetails: details);
        }

        private sealed class GateState
        {
            public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);
            public int ReferenceCount { get; set; }
            public long FactoryFailureVersion { get; set; }
            public ExceptionDispatchInfo? FactoryFailure { get; set; }
        }

        private sealed class GateScope : IDisposable
        {
            private TensorRtExternalCacheStore? _owner;
            private readonly string _key;
            private readonly GateState _state;
            private readonly long _observedFailureVersion;

            public GateScope(TensorRtExternalCacheStore owner, string key, GateState state, long observedFailureVersion)
            {
                _owner = owner;
                _key = key;
                _state = state;
                _observedFailureVersion = observedFailureVersion;
            }

            public void ThrowIfPriorFactoryFailed()
            {
                if (_state.FactoryFailureVersion > _observedFailureVersion)
                {
                    _state.FactoryFailure!.Throw();
                }
            }

            public void RecordFactoryFailure(Exception exception)
            {
                _state.FactoryFailure = ExceptionDispatchInfo.Capture(exception);
                _state.FactoryFailureVersion++;
            }

            public void Dispose()
            {
                TensorRtExternalCacheStore? owner = Interlocked.Exchange(ref _owner, null);
                owner?.ReleaseReference(_key, _state, releaseSemaphore: true);
            }
        }

        private sealed class EntryReadResult : IDisposable
        {
            private Stream? _payloadStream;

            private EntryReadResult(TensorRtExternalCacheStatus status) { Status = status; }

            public TensorRtExternalCacheStatus Status { get; private set; }
            public TensorRtExternalCacheRejectionReason RejectionReason { get; private set; }
            public TensorRtExternalCacheRemediation Remediation { get; private set; }
            public string? RemediationPath { get; private set; }
            public JsonDocument? Manifest { get; private set; }
            public byte[]? PayloadBytes { get; private set; }
            public string? PayloadSha256 { get; private set; }
            public long PayloadLength { get; private set; }
            public string? ManifestSha256 { get; private set; }
            public long ManifestLength { get; private set; }
            public string? Generation { get; private set; }

            public static EntryReadResult Miss() => new EntryReadResult(TensorRtExternalCacheStatus.Miss);

            public static EntryReadResult Rejected(TensorRtExternalCacheRejectionReason reason, TensorRtExternalCacheRemediation remediation, string? path)
            {
                return new EntryReadResult(TensorRtExternalCacheStatus.Rejected) { RejectionReason = reason, Remediation = remediation, RemediationPath = path };
            }

            public static EntryReadResult Hit(JsonDocument manifest, Stream? stream, byte[]? bytes, string generation, string payloadSha256, long payloadLength, string manifestSha256, long manifestLength)
            {
                return new EntryReadResult(TensorRtExternalCacheStatus.Hit)
                {
                    Manifest = manifest,
                    Generation = generation,
                    _payloadStream = stream,
                    PayloadBytes = bytes,
                    PayloadSha256 = payloadSha256,
                    PayloadLength = payloadLength,
                    ManifestSha256 = manifestSha256,
                    ManifestLength = manifestLength
                };
            }

            public Stream DetachPayloadStream()
            {
                Stream stream = _payloadStream ?? throw new InvalidOperationException("No cache payload stream is available.");
                _payloadStream = null;
                return stream;
            }

            public void Dispose()
            {
                _payloadStream?.Dispose();
                _payloadStream = null;
                Manifest?.Dispose();
                Manifest = null;
            }
        }

        private sealed class EntryActivity : IDisposable
        {
            public void Dispose() { }
        }

        private sealed class EntryWriteResult
        {
            public EntryWriteResult(
                TensorRtExternalCacheEntryKind kind,
                string lookupKeySha256,
                string extension,
                long payloadLength,
                string payloadSha256,
                long manifestLength,
                string manifestSha256,
                DateTimeOffset createdUtc,
                string? existingPayloadSha256)
            {
                Kind = kind;
                LookupKeySha256 = lookupKeySha256;
                Extension = extension;
                PayloadLength = payloadLength;
                PayloadSha256 = payloadSha256;
                ManifestLength = manifestLength;
                ManifestSha256 = manifestSha256;
                CreatedUtc = createdUtc;
                ExistingPayloadSha256 = existingPayloadSha256;
            }

            public TensorRtExternalCacheEntryKind Kind { get; }
            public string LookupKeySha256 { get; }
            public string Extension { get; }
            public long PayloadLength { get; }
            public string PayloadSha256 { get; }
            public long ManifestLength { get; }
            public string ManifestSha256 { get; }
            public DateTimeOffset CreatedUtc { get; }
            public string? ExistingPayloadSha256 { get; }

            public static EntryWriteResult Existing(string category, string key, string extension, long length, string sha256, string existingSha256)
            {
                return new EntryWriteResult(CategoryEntryKind(category, extension), key, extension, length, sha256, 0, new string('0', 64), DateTimeOffset.MinValue, existingSha256);
            }

            public TensorRtExternalCacheEntryMetadata ToMetadata(string? cudaCacheKeySha256)
            {
                return new TensorRtExternalCacheEntryMetadata(Kind, LookupKeySha256, PayloadSha256, PayloadLength, ManifestSha256, ManifestLength, Extension, CreatedUtc, cudaCacheKeySha256);
            }
        }

        private sealed class CacheIdentityMismatchException : Exception
        {
            public CacheIdentityMismatchException(string message) : base(message) { }
        }
    }
}
