using System;
using System.IO;
using System.Threading;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Reports how a local engine or CUDA kernel was resolved. / 报告 TensorRT 引擎状态。</summary>
    public enum TensorRtLocalCacheResolutionStatus
    {
        /// <summary>A complete validated local entry was reused. / 表示相关状态或选项。</summary>
        CacheHit = 1,
        /// <summary>An engine was built and published after a normal miss. / 表示 TensorRT 引擎状态或选项。</summary>
        Built = 2,
        /// <summary>A CUDA artifact was compiled and published after a normal miss. / 表示 CUDA状态或选项。</summary>
        Compiled = 3,
        /// <summary>One exact invalid entry was removed and rebuilt once. / 表示错误状态或选项。</summary>
        RebuiltAfterInvalidCache = 4
    }

    /// <summary>Configures the application-owned TensorRT local cache root. / 定义或说明缓存合同。</summary>
    public sealed class TensorRtLocalCacheOptions
    {
        /// <summary>Initializes a local cache configuration. / 初始化缓存对象。</summary>
        public TensorRtLocalCacheOptions(
            string? cacheRootPath = null,
            TensorRtExternalCacheOptions? storeOptions = null)
        {
            if (cacheRootPath != null && !Path.IsPathFullyQualified(cacheRootPath))
            {
                throw new ArgumentException("A custom TensorRT cache root must be an absolute path.", nameof(cacheRootPath));
            }

            CacheRootPath = Path.GetFullPath(cacheRootPath ?? GetDefaultCacheRootPath());
            StoreOptions = storeOptions ?? TensorRtExternalCacheOptions.Default;
        }

        /// <summary>Gets the normalized absolute application-owned cache root. / 获取缓存信息。</summary>
        public string CacheRootPath { get; }
        /// <summary>Gets the bounded manifest, payload, conflict and remediation settings. / 获取配置信息。</summary>
        public TensorRtExternalCacheOptions StoreOptions { get; }

        /// <summary>Gets the current user's default TensorRT cache root. / 获取缓存信息。</summary>
        public static string GetDefaultCacheRootPath()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local))
            {
                throw new InvalidOperationException("The current user does not expose a LocalApplicationData directory.");
            }
            return Path.GetFullPath(Path.Combine(local, "JYPPX", "DeploySharp", "TensorRT"));
        }
    }

    /// <summary>Owns one validated local engine stream. / 定义或说明 TensorRT 引擎合同。</summary>
    public sealed class TensorRtLocalEngineResult : IDisposable
    {
        private Stream? _stream;

        internal TensorRtLocalEngineResult(
            TensorRtLocalCacheResolutionStatus status,
            TensorRtEngineCacheIdentity identity,
            TensorRtExternalCacheEntryMetadata metadata,
            Stream stream)
        {
            Status = status;
            Identity = identity;
            Metadata = metadata;
            _stream = stream;
        }

        /// <summary>Gets how the engine was resolved. / 获取 TensorRT 引擎信息。</summary>
        public TensorRtLocalCacheResolutionStatus Status { get; }
        /// <summary>Gets the complete engine compatibility identity. / 获取 TensorRT 引擎信息。</summary>
        public TensorRtEngineCacheIdentity Identity { get; }
        /// <summary>Gets the validated local entry metadata. / 获取相关信息。</summary>
        public TensorRtExternalCacheEntryMetadata Metadata { get; }
        /// <summary>Gets the pre-build compatibility key. / 获取相关信息。</summary>
        public string CacheKeySha256 => Identity.LookupKeySha256;
        /// <summary>Gets the SHA256 of the validated serialized engine. / 获取 TensorRT 引擎信息。</summary>
        public string EngineSha256 => Metadata.PayloadSha256;
        /// <summary>Gets the readable engine stream owned by this result. / 获取 TensorRT 引擎信息。</summary>
        public Stream Stream => _stream ?? throw new ObjectDisposedException(nameof(TensorRtLocalEngineResult));

        /// <summary>Releases the owned engine stream. / 释放持有的引擎数据流。</summary>
        public void Dispose()
        {
            Stream? stream = Interlocked.Exchange(ref _stream, null);
            stream?.Dispose();
        }

        internal Stream DetachStream()
        {
            return Interlocked.Exchange(ref _stream, null) ?? throw new ObjectDisposedException(nameof(TensorRtLocalEngineResult));
        }
    }

    /// <summary>Owns a session created from a resolved local engine. / 定义或说明 TensorRT 引擎合同。</summary>
    public sealed class TensorRtLocalSessionResult : IDisposable
    {
        private IInferenceSession? _session;

        internal TensorRtLocalSessionResult(
            TensorRtLocalCacheResolutionStatus status,
            string cacheKeySha256,
            string engineSha256,
            IInferenceSession session)
        {
            Status = status;
            CacheKeySha256 = cacheKeySha256;
            EngineSha256 = engineSha256;
            _session = session;
        }

        /// <summary>Gets how the session's engine was resolved. / 获取 TensorRT 引擎信息。</summary>
        public TensorRtLocalCacheResolutionStatus Status { get; }
        /// <summary>Gets the engine compatibility key. / 获取 TensorRT 引擎信息。</summary>
        public string CacheKeySha256 { get; }
        /// <summary>Gets the SHA256 of the loaded serialized engine. / 获取 TensorRT 引擎信息。</summary>
        public string EngineSha256 { get; }
        /// <summary>Gets the inference session owned by this result. / 获取推理会话信息。</summary>
        public IInferenceSession Session => _session ?? throw new ObjectDisposedException(nameof(TensorRtLocalSessionResult));

        /// <summary>Releases the owned inference session. / 释放持有的推理会话。</summary>
        public void Dispose()
        {
            IInferenceSession? session = Interlocked.Exchange(ref _session, null);
            session?.Dispose();
        }
    }

    /// <summary>Owns a loaded CUDA kernel resolved through the local PTX/CUBIN cache. / 定义或说明缓存合同。</summary>
    public sealed class TensorRtLocalCudaKernelResult : IDisposable
    {
        private TensorRtCudaCompiledKernel? _kernel;

        internal TensorRtLocalCudaKernelResult(
            TensorRtLocalCacheResolutionStatus status,
            TensorRtCudaKernelLookupIdentity identity,
            TensorRtExternalCacheEntryMetadata metadata,
            TensorRtCudaCompiledKernel kernel)
        {
            Status = status;
            Identity = identity;
            Metadata = metadata;
            _kernel = kernel;
        }

        /// <summary>Gets how the CUDA artifact was resolved. / 获取 CUDA信息。</summary>
        public TensorRtLocalCacheResolutionStatus Status { get; }
        /// <summary>Gets the complete pre-compilation compatibility identity. / 获取相关信息。</summary>
        public TensorRtCudaKernelLookupIdentity Identity { get; }
        /// <summary>Gets the validated local entry metadata. / 获取相关信息。</summary>
        public TensorRtExternalCacheEntryMetadata Metadata { get; }
        /// <summary>Gets the pre-compilation compatibility key. / 获取相关信息。</summary>
        public string CacheKeySha256 => Identity.LookupKeySha256;
        /// <summary>Gets the loaded CUDA kernel owned by this result. / 获取 CUDA信息。</summary>
        public TensorRtCudaCompiledKernel Kernel => _kernel ?? throw new ObjectDisposedException(nameof(TensorRtLocalCudaKernelResult));

        /// <summary>Releases the owned CUDA kernel. / 释放持有的 CUDA 内核。</summary>
        public void Dispose()
        {
            TensorRtCudaCompiledKernel? kernel = Interlocked.Exchange(ref _kernel, null);
            kernel?.Dispose();
        }
    }

    /// <summary>Explicitly resolves local engines and CUDA kernels without changing provider behavior. / 说明 TensorRT 引擎公共 API。</summary>
    public sealed class TensorRtLocalSessionFactory : IDisposable
    {
        private readonly TensorRtExternalCacheStore _store;
        private readonly Func<TensorRtOnnxEngineBuilder> _builderFactory;
        private readonly Func<TensorRtBackendProvider> _providerFactory;
        private readonly Func<TensorRtCudaRtcArtifact, int, TensorRtCudaCompiledKernel> _kernelLoader;
        private readonly Func<ModelArtifact, Stream, string, BackendRequest, SessionOptions, CancellationToken, IInferenceSession>? _sessionLoader;
        private TensorRtBackendProvider? _ownedProvider;
        private bool _disposed;

        /// <summary>Initializes an explicit TensorRT local-cache facade. / 初始化缓存对象。</summary>
        public TensorRtLocalSessionFactory(TensorRtLocalCacheOptions? options = null)
            : this(
                options ?? new TensorRtLocalCacheOptions(),
                () => new TensorRtOnnxEngineBuilder(),
                () => new TensorRtBackendProvider(),
                TensorRtCudaCompiledKernel.Load,
                sessionLoader: null)
        {
        }

        internal TensorRtLocalSessionFactory(
            TensorRtLocalCacheOptions options,
            Func<TensorRtOnnxEngineBuilder> builderFactory,
            Func<TensorRtBackendProvider> providerFactory,
            Func<TensorRtCudaRtcArtifact, int, TensorRtCudaCompiledKernel> kernelLoader,
            Func<ModelArtifact, Stream, string, BackendRequest, SessionOptions, CancellationToken, IInferenceSession>? sessionLoader)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _builderFactory = builderFactory ?? throw new ArgumentNullException(nameof(builderFactory));
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            _kernelLoader = kernelLoader ?? throw new ArgumentNullException(nameof(kernelLoader));
            _sessionLoader = sessionLoader;
            CacheRootCreated = !Directory.Exists(options.CacheRootPath);
            _store = new TensorRtExternalCacheStore(options.CacheRootPath, options.StoreOptions);
            CacheRootPath = _store.RootPath;
        }

        /// <summary>Gets the normalized absolute local cache root. / 获取缓存信息。</summary>
        public string CacheRootPath { get; }
        /// <summary>Gets whether construction created the local cache root. / 获取缓存信息。</summary>
        public bool CacheRootCreated { get; }

        /// <summary>Resolves a validated engine stream, building only after a miss or exact-entry rejection. / 解析 TensorRT 引擎资源。</summary>
        public TensorRtLocalEngineResult ResolveOrBuildEngine(
            ModelArtifact onnxArtifact,
            TensorRtOnnxEngineBuildOptions buildOptions,
            TensorRtEngineCacheIdentity identity,
            CancellationToken cancellationToken = default)
        {
            return ResolveOrBuildEngine(
                onnxArtifact,
                buildOptions,
                identity,
                token => BuildEngineStream(onnxArtifact, buildOptions, identity, token),
                cancellationToken);
        }

        /// <summary>Resolves an engine through an explicit managed build seam. / 解析 TensorRT 引擎资源。</summary>
        public TensorRtLocalEngineResult ResolveOrBuildEngine(
            ModelArtifact onnxArtifact,
            TensorRtOnnxEngineBuildOptions buildOptions,
            TensorRtEngineCacheIdentity identity,
            Func<CancellationToken, Stream> buildFactory,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (onnxArtifact == null) throw new ArgumentNullException(nameof(onnxArtifact));
            if (buildOptions == null) throw new ArgumentNullException(nameof(buildOptions));
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (buildFactory == null) throw new ArgumentNullException(nameof(buildFactory));
            ValidateOnnxIdentity(onnxArtifact, buildOptions, identity);

            using TensorRtEngineCacheResult lookup = _store.OpenEngine(identity, cancellationToken);
            bool rejected = lookup.Status == TensorRtExternalCacheStatus.Rejected;
            if (lookup.Status == TensorRtExternalCacheStatus.Hit)
            {
                return CreateEngineResult(TensorRtLocalCacheResolutionStatus.CacheHit, identity, lookup);
            }
            if (lookup.Status != TensorRtExternalCacheStatus.Miss && !rejected)
            {
                throw CacheResultFailure("Engine cache lookup did not produce a reusable entry.", lookup);
            }
            if (rejected) _store.InvalidateEngine(identity, cancellationToken);

            using TensorRtEngineCacheResult resolved = _store.GetOrBuildEngine(identity, buildFactory, cancellationToken);
            if (resolved.Status != TensorRtExternalCacheStatus.Stored && resolved.Status != TensorRtExternalCacheStatus.Hit)
            {
                throw CacheResultFailure("Engine cache build did not publish a reusable entry.", resolved);
            }
            return CreateEngineResult(
                rejected ? TensorRtLocalCacheResolutionStatus.RebuiltAfterInvalidCache : TensorRtLocalCacheResolutionStatus.Built,
                identity,
                resolved);
        }

        /// <summary>Builds or reuses an engine and creates a TensorRT session from its validated bytes. / 构建 TensorRT 引擎。</summary>
        public TensorRtLocalSessionResult CreateSessionFromOnnx(
            ModelArtifact onnxArtifact,
            TensorRtOnnxEngineBuildOptions buildOptions,
            TensorRtEngineCacheIdentity identity,
            BackendRequest request,
            SessionOptions sessionOptions,
            CancellationToken cancellationToken = default)
        {
            return CreateSessionFromOnnx(
                onnxArtifact,
                buildOptions,
                identity,
                request,
                sessionOptions,
                token => BuildEngineStream(onnxArtifact, buildOptions, identity, token),
                cancellationToken);
        }

        internal TensorRtLocalSessionResult CreateSessionFromOnnx(
            ModelArtifact onnxArtifact,
            TensorRtOnnxEngineBuildOptions buildOptions,
            TensorRtEngineCacheIdentity identity,
            BackendRequest request,
            SessionOptions sessionOptions,
            Func<CancellationToken, Stream> buildFactory,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (sessionOptions == null) throw new ArgumentNullException(nameof(sessionOptions));
            TensorRtLocalEngineResult engine = ResolveOrBuildEngine(onnxArtifact, buildOptions, identity, buildFactory, cancellationToken);
            try
            {
                try
                {
                    IInferenceSession session = LoadResolvedSession(onnxArtifact, engine.Stream, engine.EngineSha256, identity.ArtifactExtension, request, sessionOptions, cancellationToken);
                    return new TensorRtLocalSessionResult(engine.Status, engine.CacheKeySha256, engine.EngineSha256, session);
                }
                catch (TensorRtBackendException exception) when (ShouldRetryEngineLoad(engine.Status, exception))
                {
                    engine.Dispose();
                    _store.InvalidateEngine(identity, cancellationToken);
                    using TensorRtLocalEngineResult rebuilt = ResolveOrBuildEngine(onnxArtifact, buildOptions, identity, buildFactory, cancellationToken);
                    IInferenceSession session = LoadResolvedSession(onnxArtifact, rebuilt.Stream, rebuilt.EngineSha256, identity.ArtifactExtension, request, sessionOptions, cancellationToken);
                    return new TensorRtLocalSessionResult(TensorRtLocalCacheResolutionStatus.RebuiltAfterInvalidCache, rebuilt.CacheKeySha256, rebuilt.EngineSha256, session);
                }
            }
            finally
            {
                engine.Dispose();
            }
        }

        /// <summary>Compiles or reuses PTX/CUBIN and loads the declared kernel on one CUDA device. / 说明 CUDA公共 API。</summary>
        public TensorRtLocalCudaKernelResult ResolveOrCompileCudaKernel(
            TensorRtCudaRtcKernelDefinition definition,
            TensorRtCudaRtcCompileOptions compileOptions,
            string compilerVersion,
            string compilerIdentity,
            string cudaRuntimeVersion,
            string cudaRuntimeIdentity,
            string cudaDriverVersion,
            string cudaDriverIdentity,
            string gpuArchitecture,
            string gpuCompatibilityIdentity,
            string nativeBridgeIdentity,
            int deviceOrdinal = 0,
            CancellationToken cancellationToken = default)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (compileOptions == null) throw new ArgumentNullException(nameof(compileOptions));
            var identity = new TensorRtCudaKernelLookupIdentity(
                definition,
                compileOptions,
                compilerVersion,
                compilerIdentity,
                cudaRuntimeVersion,
                cudaRuntimeIdentity,
                cudaDriverVersion,
                cudaDriverIdentity,
                gpuArchitecture,
                gpuCompatibilityIdentity,
                nativeBridgeIdentity);
            return ResolveOrCompileCudaKernel(
                identity,
                token =>
                {
                    token.ThrowIfCancellationRequested();
                    TensorRtCudaRtcArtifact artifact = TensorRtCudaRtcCompiler.Compile(definition, compileOptions);
                    token.ThrowIfCancellationRequested();
                    return artifact;
                },
                deviceOrdinal,
                cancellationToken);
        }

        /// <summary>Resolves and loads a CUDA kernel through an explicit managed compiler seam. / 解析 CUDA资源。</summary>
        public TensorRtLocalCudaKernelResult ResolveOrCompileCudaKernel(
            TensorRtCudaKernelLookupIdentity identity,
            Func<CancellationToken, TensorRtCudaRtcArtifact> compileFactory,
            int deviceOrdinal = 0,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (compileFactory == null) throw new ArgumentNullException(nameof(compileFactory));
            if (deviceOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(deviceOrdinal));

            TensorRtCudaCacheResult cache = ResolveCudaArtifact(identity, compileFactory, cancellationToken, out TensorRtLocalCacheResolutionStatus status);
            try
            {
                try
                {
                    TensorRtCudaCompiledKernel kernel = _kernelLoader(cache.Artifact!, deviceOrdinal);
                    return new TensorRtLocalCudaKernelResult(status, identity, cache.Metadata!, kernel);
                }
                catch (Exception exception) when (ShouldRetryCudaLoad(status, exception))
                {
                    _store.InvalidateCuda(identity, cancellationToken);
                    TensorRtCudaCacheResult rebuilt = ResolveCudaArtifact(identity, compileFactory, cancellationToken, out _);
                    TensorRtCudaCompiledKernel kernel = _kernelLoader(rebuilt.Artifact!, deviceOrdinal);
                    return new TensorRtLocalCudaKernelResult(TensorRtLocalCacheResolutionStatus.RebuiltAfterInvalidCache, identity, rebuilt.Metadata!, kernel);
                }
            }
            finally
            {
                // Artifacts and metadata are managed immutable values; the loaded kernel owns its own copied artifact.
            }
        }

        /// <summary>Invalidates one exact engine key. / 删除或失效 TensorRT 引擎资源。</summary>
        public TensorRtExternalCacheResult InvalidateEngine(TensorRtEngineCacheIdentity identity, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _store.InvalidateEngine(identity, cancellationToken);
        }

        /// <summary>Invalidates one exact CUDA key. / 删除或失效 CUDA资源。</summary>
        public TensorRtExternalCacheResult InvalidateCuda(TensorRtCudaKernelLookupIdentity identity, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _store.InvalidateCuda(identity, cancellationToken);
        }

        /// <summary>Releases facade-owned cache resources. / 释放门面持有的缓存资源。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ownedProvider?.Dispose();
            _ownedProvider = null;
        }

        private TensorRtCudaCacheResult ResolveCudaArtifact(
            TensorRtCudaKernelLookupIdentity identity,
            Func<CancellationToken, TensorRtCudaRtcArtifact> compileFactory,
            CancellationToken cancellationToken,
            out TensorRtLocalCacheResolutionStatus status)
        {
            TensorRtCudaCacheResult lookup = _store.LookupCuda(identity, cancellationToken);
            bool rejected = lookup.Status == TensorRtExternalCacheStatus.Rejected;
            if (lookup.Status == TensorRtExternalCacheStatus.Hit)
            {
                status = TensorRtLocalCacheResolutionStatus.CacheHit;
                return lookup;
            }
            if (lookup.Status != TensorRtExternalCacheStatus.Miss && !rejected)
            {
                throw CacheResultFailure("CUDA cache lookup did not produce a reusable entry.", lookup);
            }
            if (rejected) _store.InvalidateCuda(identity, cancellationToken);
            TensorRtCudaCacheResult resolved = _store.GetOrCompileCuda(identity, compileFactory, cancellationToken);
            if ((resolved.Status != TensorRtExternalCacheStatus.Stored && resolved.Status != TensorRtExternalCacheStatus.Hit) || resolved.Artifact == null || resolved.Metadata == null)
            {
                throw CacheResultFailure("CUDA cache compilation did not publish a reusable entry.", resolved);
            }
            status = rejected ? TensorRtLocalCacheResolutionStatus.RebuiltAfterInvalidCache : TensorRtLocalCacheResolutionStatus.Compiled;
            return resolved;
        }

        private Stream BuildEngineStream(
            ModelArtifact onnxArtifact,
            TensorRtOnnxEngineBuildOptions buildOptions,
            TensorRtEngineCacheIdentity identity,
            CancellationToken cancellationToken)
        {
            string directory = CreateContainedTemporaryDirectory("build");
            string enginePath = Path.Combine(directory, "artifact" + identity.ArtifactExtension);
            try
            {
                TensorRtOnnxEngineBuildResult result = _builderFactory().Build(onnxArtifact, enginePath, buildOptions, cancellationToken);
                if (!string.Equals(result.OnnxSha256, identity.OnnxSha256, StringComparison.Ordinal) ||
                    !string.Equals(result.BuildInputsSha256, identity.ManagedBuildInputsSha256, StringComparison.Ordinal))
                {
                    throw new TensorRtBackendException(TensorRtErrorCodes.ExternalCacheEntryRejected, "The built engine does not match the requested local cache identity.", modelId: onnxArtifact.ModelId, operation: "local-cache-build");
                }
                return ReadBoundedMemoryStream(result.EnginePath, buildOptions.MaximumEngineBytes, cancellationToken);
            }
            finally
            {
                DeleteContainedTemporaryDirectory(directory);
            }
        }

        private IInferenceSession LoadResolvedSession(
            ModelArtifact onnxArtifact,
            Stream engineStream,
            string engineSha256,
            string engineExtension,
            BackendRequest request,
            SessionOptions options,
            CancellationToken cancellationToken)
        {
            return _sessionLoader == null
                ? LoadSessionCore(onnxArtifact, engineStream, engineSha256, engineExtension, request, options, cancellationToken)
                : _sessionLoader(onnxArtifact, engineStream, engineSha256, request, options, cancellationToken);
        }

        private IInferenceSession LoadSessionCore(
            ModelArtifact onnxArtifact,
            Stream engineStream,
            string engineSha256,
            string engineExtension,
            BackendRequest request,
            SessionOptions options,
            CancellationToken cancellationToken)
        {
            string directory = CreateContainedTemporaryDirectory("load");
            string enginePath = Path.Combine(directory, "artifact" + engineExtension);
            try
            {
                using (var output = new FileStream(enginePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    CopyStream(engineStream, output, cancellationToken);
                    output.Flush(true);
                }
                var artifact = new ModelArtifact(onnxArtifact.ModelId, "tensorrt-engine", enginePath, engineSha256, TensorRtBackendProvider.BackendId);
                TensorRtBackendProvider provider = _ownedProvider ??= _providerFactory();
                return provider.CreateSession(artifact, request, options);
            }
            finally
            {
                DeleteContainedTemporaryDirectory(directory);
            }
        }

        private static TensorRtLocalEngineResult CreateEngineResult(
            TensorRtLocalCacheResolutionStatus status,
            TensorRtEngineCacheIdentity identity,
            TensorRtEngineCacheResult cache)
        {
            if (cache.Stream == null || cache.Metadata == null)
            {
                throw CacheResultFailure("The validated engine cache result has no payload.", cache);
            }
            return new TensorRtLocalEngineResult(status, identity, cache.Metadata, cache.DetachStream());
        }

        private static void ValidateOnnxIdentity(
            ModelArtifact artifact,
            TensorRtOnnxEngineBuildOptions options,
            TensorRtEngineCacheIdentity identity)
        {
            TensorRtOnnxModelArtifactValidator.ReadResult validated = TensorRtOnnxModelArtifactValidator.ReadValidated(artifact, options.MaximumOnnxBytes);
            string buildInputs = TensorRtOnnxEngineBuilder.GetBuildInputsSha256(validated.Sha256, options);
            if (!string.Equals(validated.Sha256, identity.OnnxSha256, StringComparison.Ordinal) ||
                !string.Equals(buildInputs, identity.ManagedBuildInputsSha256, StringComparison.Ordinal))
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.ExternalCacheConfigurationInvalid,
                    "The ONNX bytes or build options do not match the supplied engine cache identity.",
                    modelId: artifact.ModelId,
                    operation: "local-cache-identity");
            }
        }

        private static bool ShouldRetryEngineLoad(TensorRtLocalCacheResolutionStatus status, TensorRtBackendException exception)
        {
            return status != TensorRtLocalCacheResolutionStatus.RebuiltAfterInvalidCache &&
                (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable || exception.ErrorCode == TensorRtErrorCodes.ModelArtifactInvalid);
        }

        private static bool ShouldRetryCudaLoad(TensorRtLocalCacheResolutionStatus status, Exception exception)
        {
            return status != TensorRtLocalCacheResolutionStatus.RebuiltAfterInvalidCache &&
                exception is TensorRtBackendException backendException &&
                backendException.ErrorCode == TensorRtErrorCodes.CudaCompilationFailed &&
                string.Equals(backendException.Operation, "cuda-module-load", StringComparison.Ordinal);
        }

        private string CreateContainedTemporaryDirectory(string purpose)
        {
            string path = Path.GetFullPath(Path.Combine(CacheRootPath, ".local-" + purpose + "-" + Guid.NewGuid().ToString("N")));
            string prefix = CacheRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!path.StartsWith(prefix, comparison)) throw new UnauthorizedAccessException("The TensorRT temporary path escaped the cache root.");
            Directory.CreateDirectory(path);
            return path;
        }

        private void DeleteContainedTemporaryDirectory(string path)
        {
            string full = Path.GetFullPath(path);
            string prefix = CacheRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!full.StartsWith(prefix, comparison) || !Directory.Exists(full)) return;
            try
            {
                if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0) Directory.Delete(full, recursive: true);
            }
            catch { }
        }

        private static MemoryStream ReadBoundedMemoryStream(string path, long maximumBytes, CancellationToken cancellationToken)
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length < 1 || input.Length > maximumBytes || input.Length > int.MaxValue) throw new InvalidDataException("The built engine exceeds its configured size limit.");
            var memory = new MemoryStream(checked((int)input.Length));
            CopyStream(input, memory, cancellationToken);
            memory.Position = 0;
            return memory;
        }

        private static void CopyStream(Stream input, Stream output, CancellationToken cancellationToken)
        {
            var buffer = new byte[81920];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = input.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                output.Write(buffer, 0, read);
            }
        }

        private static TensorRtBackendException CacheResultFailure(string message, TensorRtExternalCacheResult result)
        {
            return new TensorRtBackendException(
                result.ErrorCode ?? TensorRtErrorCodes.ExternalCacheIoFailed,
                message,
                operation: "local-cache",
                technicalDetails: "status=" + result.Status + ";reason=" + result.RejectionReason);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TensorRtLocalSessionFactory));
        }
    }
}
