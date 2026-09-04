using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelFactory.Runtime;
using JYPPX.DeploySharp.ModelPack.Json;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Implements catalog selection, versioned Release downloads, content-addressed caching, and offline reuse. / 实现目录选择、版本化 Release 下载、内容寻址缓存和离线复用。</summary>
    public sealed class ModelFactoryClient : IModelFactory
    {
        private const string RootMarker = ".deploysharp-model-factory-root";
        private const string CompleteMarker = ".complete";
        private readonly ModelFactoryOptions _options;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly SemaphoreSlim _downloadSlots;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly object _gate = new object();
        private readonly Dictionary<string, Task<MaterializedModel>> _modelOperations = new Dictionary<string, Task<MaterializedModel>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<MaterializedAsset>> _assetOperations = new Dictionary<string, Task<MaterializedAsset>>(StringComparer.Ordinal);
        private readonly string _managedRoot;
        private bool _disposed;

        /// <summary>Initializes a ModelFactory client. A supplied HttpClient remains application-owned and must disable automatic redirects for versioned Release assets. / 初始化 ModelFactory 客户端；传入的 HttpClient 仍由应用所有，且对版本化 Release 资产必须禁用自动重定向。</summary>
        public ModelFactoryClient(ValidatedModelCatalog catalog, ModelFactoryOptions options, HttpClient? httpClient = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _managedRoot = Path.Combine(_options.CacheRoot, ".deploysharp-model-factory", "v1");
            InitializeManagedRoot();
            _downloadSlots = new SemaphoreSlim(_options.MaximumConcurrentDownloads, _options.MaximumConcurrentDownloads);
            if (httpClient != null)
            {
                _httpClient = httpClient;
                _ownsHttpClient = false;
            }
            else
            {
                var handler = new HttpClientHandler { AllowAutoRedirect = false };
                if (_options.Proxy != null)
                {
                    handler.Proxy = _options.Proxy;
                    handler.UseProxy = true;
                }

                _httpClient = new HttpClient(handler, true) { Timeout = Timeout.InfiniteTimeSpan };
                _ownsHttpClient = true;
            }
        }

        /// <inheritdoc />
        /// <remarks>Returns the validated immutable snapshot. / 返回已验证的不可变快照。</remarks>
        public ValidatedModelCatalog Catalog { get; }

        /// <inheritdoc />
        /// <remarks>Uses deterministic query ordering. / 使用确定性查询顺序。</remarks>
        public ModelSelection Select(ModelQuery query)
        {
            EnsureUsable();
            IReadOnlyList<ModelSelection> matches = ModelCatalogQuery.Select(Catalog, query ?? throw new ArgumentNullException(nameof(query)));
            if (matches.Count > 0) return matches[0];
            string candidates = string.Join(", ", Catalog.Document.Entries.Select(entry => entry.ModelId).Where(value => value != null));
            throw new ModelFactoryException("No model artifact matched the query.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.NoMatch, "No model artifact matched the query. Candidates: " + candidates) }, technicalDetails: candidates);
        }

        /// <inheritdoc />
        /// <remarks>Shares concurrent work while caller cancellation remains isolated. / 共享并发工作，同时隔离调用方取消。</remarks>
        public Task<MaterializedModel> GetModelAsync(ModelSelection selection, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureUsable();
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            string key = ComputeSelectionKey(selection);
            Task<MaterializedModel> operation;
            lock (_gate)
            {
                if (_disposed) Throw(ModelFactoryDiagnosticCodes.ObjectDisposed, "The ModelFactory client has been disposed.");
                if (!_modelOperations.TryGetValue(key, out operation!))
                {
                    operation = MaterializeModelCoreAsync(selection, key, progress, _disposeSource.Token);
                    _modelOperations.Add(key, operation);
                    ObserveAndRemove(_modelOperations, key, operation);
                }
            }

            return WaitForCallerAsync(operation, cancellationToken, selection.Entry.ModelId, selection.Artifact.ArtifactId);
        }

        /// <inheritdoc />
        /// <remarks>Accepts only assets from this validated catalog snapshot. / 仅接受来自当前已验证目录快照的资产。</remarks>
        public Task<MaterializedAsset> GetTestInputAsync(ModelCatalogEntry entry, string assetId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureUsable();
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrWhiteSpace(assetId)) throw new ArgumentException("An asset identifier is required.", nameof(assetId));
            if (!_options.AllowTestInputs) Throw(ModelFactoryDiagnosticCodes.AdmissionRejected, "Test-input downloads are disabled.", modelId: entry.ModelId, assetId: assetId);
            if (!Catalog.Document.Entries.Any(catalogEntry => ReferenceEquals(catalogEntry, entry))) Throw(ModelFactoryDiagnosticCodes.AssetInvalid, "Test-input entry must come from this validated catalog snapshot.", modelId: entry.ModelId, assetId: assetId);
            ModelCatalogAsset? asset = entry.TestInputs.FirstOrDefault(value => string.Equals(value.AssetId, assetId, StringComparison.OrdinalIgnoreCase));
            if (asset == null) Throw(ModelFactoryDiagnosticCodes.NoMatch, "The requested test-input asset was not found.", modelId: entry.ModelId, assetId: assetId);
            string key = asset!.CacheKey ?? CatalogCacheKey.Compute(Catalog.CatalogRevision, asset.ReleaseTag!, asset.Sha256!, asset.RelativePath!);
            Task<MaterializedAsset> operation;
            lock (_gate)
            {
                if (_disposed) Throw(ModelFactoryDiagnosticCodes.ObjectDisposed, "The ModelFactory client has been disposed.");
                if (!_assetOperations.TryGetValue(key, out operation!))
                {
                    operation = MaterializeTestAssetCoreAsync(entry, asset, key, progress, _disposeSource.Token);
                    _assetOperations.Add(key, operation);
                    ObserveAndRemove(_assetOperations, key, operation);
                }
            }

            return WaitForCallerAsync(operation, cancellationToken, entry.ModelId, null, assetId);
        }

        /// <inheritdoc />
        /// <remarks>Performs size, SHA256, path, and ModelPack checks without network access. / 在不访问网络的情况下执行大小、SHA256、路径和 ModelPack 检查。</remarks>
        public async Task<bool> VerifyModelCacheAsync(ModelSelection selection, CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureUsable();
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            string key = ComputeSelectionKey(selection);
            string entryRoot = GetManagedEntryRoot("entries", key);
            MaterializedModel? materialized = await TryLoadModelCacheAsync(selection, key, entryRoot, cancellationToken).ConfigureAwait(false);
            return materialized != null;
        }

        /// <inheritdoc />
        /// <remarks>Never deletes outside the marker-owned ModelFactory namespace. / 绝不删除标记所有的 ModelFactory 命名空间以外的内容。</remarks>
        public Task<ModelCacheCleanupResult> CleanCacheAsync(ModelCacheCleanupOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureUsable();
            if (options == null) throw new ArgumentNullException(nameof(options));
            EnsureManagedRootMarker();
            int removed = 0;
            long bytesRemoved = 0;
            var entries = new List<CacheDirectory>();
            CollectCacheDirectories(Path.Combine(_managedRoot, "entries"), entries, cancellationToken);
            CollectCacheDirectories(Path.Combine(_managedRoot, "test-assets"), entries, cancellationToken);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            long total = entries.Sum(value => value.Size);
            foreach (CacheDirectory entry in entries.OrderBy(value => value.LastWriteUtc))
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool matchesFilters = (options.CatalogRevision == null || string.Equals(options.CatalogRevision, entry.Metadata?.CatalogRevision, StringComparison.Ordinal))
                    && (options.ReleaseTag == null || string.Equals(options.ReleaseTag, entry.Metadata?.ReleaseTag, StringComparison.Ordinal));
                if (!matchesFilters) continue;
                bool selected = options.OlderThan.HasValue && now - entry.LastWriteUtc >= options.OlderThan.Value;
                if (!selected && options.MaximumBytesToKeep.HasValue && total > options.MaximumBytesToKeep.Value) selected = true;
                if (!selected) continue;
                removed++;
                bytesRemoved += entry.Size;
                total -= entry.Size;
                if (!options.DryRun) DeleteManagedDirectory(entry.Path);
            }

            return Task.FromResult(new ModelCacheCleanupResult(removed, bytesRemoved, options.DryRun));
        }

        /// <inheritdoc />
        /// <remarks>Cancels owned work and releases owned HTTP resources after active operations settle. / 取消自有工作，并在活动操作结束后释放自有 HTTP 资源。</remarks>
        public void Dispose()
        {
            Task[] operations;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
                operations = _modelOperations.Values.Cast<Task>().Concat(_assetOperations.Values.Cast<Task>()).ToArray();
            }

            if (operations.Length == 0) DisposeResources();
            else _ = Task.WhenAll(operations).ContinueWith(_ => DisposeResources(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private async Task<MaterializedModel> MaterializeModelCoreAsync(ModelSelection selection, string key, IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken)
        {
            if (selection.Entry.Status == ModelCatalogStatus.External) Throw(ModelFactoryDiagnosticCodes.AdmissionRejected, "External catalog entries cannot be materialized as supported models.", modelId: selection.Entry.ModelId, artifactId: selection.Artifact.ArtifactId);
            long total = CheckedTotal(selection.Artifact.Assets, selection.Entry.ModelId, selection.Artifact.ArtifactId);
            if (total > _options.MaximumOperationBytes) Throw(ModelFactoryDiagnosticCodes.LimitExceeded, "Declared artifact bytes exceed the operation limit.", modelId: selection.Entry.ModelId, artifactId: selection.Artifact.ArtifactId);
            string entryRoot = GetManagedEntryRoot("entries", key);
            MaterializedModel? cached = await TryLoadModelCacheAsync(selection, key, entryRoot, cancellationToken).ConfigureAwait(false);
            if (cached != null) return cached;
            if (_options.Offline) Throw(ModelFactoryDiagnosticCodes.OfflineCacheMiss, "Offline mode requires a complete verified model cache entry.", modelId: selection.Entry.ModelId, artifactId: selection.Artifact.ArtifactId, filePath: entryRoot);
            string payloadRoot = Path.Combine(entryRoot, "payload");
            Directory.CreateDirectory(payloadRoot);
            using (var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var downloads = new List<Task<string>>();
                foreach (ModelCatalogAsset asset in selection.Artifact.Assets)
                {
                    Task<string> download = DownloadAssetAsync(asset, payloadRoot, selection.Entry.ModelId, selection.Artifact.ArtifactId, progress, operationSource.Token);
                    downloads.Add(download);
                    _ = download.ContinueWith(task =>
                    {
                        if (task.IsFaulted) operationSource.Cancel();
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }

                try { await Task.WhenAll(downloads).ConfigureAwait(false); }
                catch
                {
                    ModelFactoryException? structured = downloads.Where(task => task.IsFaulted).Select(task => task.Exception?.GetBaseException()).OfType<ModelFactoryException>().FirstOrDefault();
                    if (structured != null) throw structured;
                    throw;
                }
            }
            ModelCatalogAsset? manifestAsset = selection.Artifact.Assets.FirstOrDefault(asset => string.Equals(asset.AssetId, selection.Artifact.ManifestAssetId, StringComparison.OrdinalIgnoreCase));
            if (manifestAsset == null) Throw(ModelFactoryDiagnosticCodes.AssetInvalid, "The selected artifact does not contain its manifest asset.", modelId: selection.Entry.ModelId, artifactId: selection.Artifact.ArtifactId);
            string manifestPath = SafeCombine(payloadRoot, manifestAsset!.RelativePath!);
            try
            {
                LocalModelPackage package = await ModelPackageLoader.LoadAsync(manifestPath, new ModelPackageLoadOptions(maximumTotalFileBytes: _options.MaximumOperationBytes), cancellationToken).ConfigureAwait(false);
                if (!string.Equals(package.Manifest.ModelId.Value, selection.Entry.ModelId, StringComparison.OrdinalIgnoreCase)) Throw(ModelFactoryDiagnosticCodes.AssetInvalid, "ModelPack modelId does not match the catalog entry.", modelId: selection.Entry.ModelId, artifactId: selection.Artifact.ArtifactId, filePath: manifestPath);
                ModelArtifactDocument? manifestArtifact = package.Manifest.Document.Artifacts.FirstOrDefault(artifact => string.Equals(artifact.ArtifactId, selection.Artifact.ArtifactId, StringComparison.OrdinalIgnoreCase));
                if (manifestArtifact == null || !string.Equals(manifestArtifact.Format, selection.Artifact.Format, StringComparison.OrdinalIgnoreCase)) Throw(ModelFactoryDiagnosticCodes.AssetInvalid, "ModelPack artifact does not match the catalog selection.", modelId: selection.Entry.ModelId, artifactId: selection.Artifact.ArtifactId, filePath: manifestPath);
                WriteMetadata(entryRoot, key, selection.Entry, selection.Artifact, selection.Artifact.Assets);
                File.WriteAllText(Path.Combine(entryRoot, CompleteMarker), key, new UTF8Encoding(false));
                Touch(entryRoot);
                return new MaterializedModel(selection, key, payloadRoot, package);
            }
            catch (ModelPackageValidationException exception)
            {
                throw new ModelFactoryException("Downloaded ModelPack validation failed.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.AssetInvalid, exception.Message, modelId: selection.Entry.ModelId, artifactId: selection.Artifact.ArtifactId, filePath: manifestPath) }, exception, exception.ToString());
            }
        }

        private async Task<MaterializedAsset> MaterializeTestAssetCoreAsync(ModelCatalogEntry entry, ModelCatalogAsset asset, string key, IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken)
        {
            string entryRoot = GetManagedEntryRoot("test-assets", key);
            string payloadRoot = Path.Combine(entryRoot, "payload");
            string fullPath = SafeCombine(payloadRoot, asset.RelativePath!);
            progress?.Report(new ModelDownloadProgress(asset.AssetId!, ModelDownloadStage.CheckingCache, 0, asset.Size, 0, 0));
            if (ValidateCachedAsset(fullPath, asset, cancellationToken))
            {
                WriteMetadata(entryRoot, key, entry, null, new[] { asset });
                Touch(entryRoot);
                return new MaterializedAsset(asset, fullPath);
            }

            if (_options.Offline) Throw(ModelFactoryDiagnosticCodes.OfflineCacheMiss, "Offline mode requires a verified test-input cache entry.", modelId: entry.ModelId, assetId: asset.AssetId, filePath: fullPath);
            Directory.CreateDirectory(payloadRoot);
            fullPath = await DownloadAssetAsync(asset, payloadRoot, entry.ModelId, null, progress, cancellationToken).ConfigureAwait(false);
            WriteMetadata(entryRoot, key, entry, null, new[] { asset });
            File.WriteAllText(Path.Combine(entryRoot, CompleteMarker), key, new UTF8Encoding(false));
            Touch(entryRoot);
            return new MaterializedAsset(asset, fullPath);
        }

        private async Task<MaterializedModel?> TryLoadModelCacheAsync(ModelSelection selection, string key, string entryRoot, CancellationToken cancellationToken)
        {
            if (!File.Exists(Path.Combine(entryRoot, CompleteMarker))) return null;
            string payloadRoot = Path.Combine(entryRoot, "payload");
            foreach (ModelCatalogAsset asset in selection.Artifact.Assets)
            {
                string path = SafeCombine(payloadRoot, asset.RelativePath!);
                if (!ValidateCachedAsset(path, asset, cancellationToken)) return null;
            }

            ModelCatalogAsset? manifestAsset = selection.Artifact.Assets.FirstOrDefault(asset => string.Equals(asset.AssetId, selection.Artifact.ManifestAssetId, StringComparison.OrdinalIgnoreCase));
            if (manifestAsset == null) return null;
            try
            {
                LocalModelPackage package = await ModelPackageLoader.LoadAsync(SafeCombine(payloadRoot, manifestAsset.RelativePath!), new ModelPackageLoadOptions(maximumTotalFileBytes: _options.MaximumOperationBytes), cancellationToken).ConfigureAwait(false);
                WriteMetadata(entryRoot, key, selection.Entry, selection.Artifact, selection.Artifact.Assets);
                Touch(entryRoot);
                return new MaterializedModel(selection, key, payloadRoot, package);
            }
            catch (ModelPackageValidationException) { return null; }

        }

        private bool ValidateCachedAsset(string path, ModelCatalogAsset asset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path) || HasReparsePoint(path)) return false;
            var info = new FileInfo(path);
            if (info.Length != asset.Size) return false;
            return string.Equals(ModelFileIntegrity.ComputeSha256(path, cancellationToken), asset.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> DownloadAssetAsync(ModelCatalogAsset asset, string payloadRoot, string? modelId, string? artifactId, IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken)
        {
            if (asset.Size > _options.MaximumAssetBytes) Throw(ModelFactoryDiagnosticCodes.LimitExceeded, "Asset exceeds the configured per-file byte limit.", modelId, artifactId, asset.AssetId);
            if (!_options.AllowedSchemes.Contains(asset.DownloadUri!.Scheme, StringComparer.OrdinalIgnoreCase)) Throw(ModelFactoryDiagnosticCodes.AssetInvalid, "Asset URI scheme is not allowed.", modelId, artifactId, asset.AssetId, asset.DownloadUri);
            string finalPath = SafeCombine(payloadRoot, asset.RelativePath!);
            progress?.Report(new ModelDownloadProgress(asset.AssetId!, ModelDownloadStage.CheckingCache, 0, asset.Size, 0, 0));
            if (ValidateCachedAsset(finalPath, asset, cancellationToken)) return finalPath;
            if (_options.Offline) Throw(ModelFactoryDiagnosticCodes.OfflineCacheMiss, "Offline cache entry is missing or invalid.", modelId, artifactId, asset.AssetId, filePath: finalPath);
            await _downloadSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string? parent = Path.GetDirectoryName(finalPath);
                if (parent == null) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Asset cache parent path is unavailable.", modelId, artifactId, asset.AssetId, filePath: finalPath);
                Directory.CreateDirectory(parent!);
                if (HasReparsePoint(parent!)) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Asset cache path contains a reparse point.", modelId, artifactId, asset.AssetId, filePath: parent);
                for (int attempt = 1; attempt <= _options.MaximumRetries + 1; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        return await DownloadAttemptAsync(asset, finalPath, modelId, artifactId, attempt, progress, cancellationToken).ConfigureAwait(false);
                    }
                    catch (TransientHttpException exception)
                    {
                        if (attempt > _options.MaximumRetries) ThrowHttp("Asset HTTP request failed after retry attempts.", asset, modelId, artifactId, exception.StatusCode);
                        progress?.Report(new ModelDownloadProgress(asset.AssetId!, ModelDownloadStage.Retrying, 0, asset.Size, attempt, 0));
                        await Task.Delay(exception.Delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (AttemptTimeoutException exception)
                    {
                        if (attempt > _options.MaximumRetries) throw new ModelFactoryException("Asset request timed out after retry attempts.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.Timeout, "Asset request timed out after retry attempts.", modelId: modelId, artifactId: artifactId, assetId: asset.AssetId, uri: Sanitize(asset.DownloadUri)) }, exception.InnerException ?? exception, exception.ToString());
                        TimeSpan delay = GetRetryDelay(null, attempt);
                        progress?.Report(new ModelDownloadProgress(asset.AssetId!, ModelDownloadStage.Retrying, 0, asset.Size, attempt, 0));
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (ModelFactoryException) { throw; }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception exception) when (exception is HttpRequestException || exception is IOException || exception is TaskCanceledException)
                    {
                        if (attempt > _options.MaximumRetries) throw WrapNetwork("Asset download failed after retry attempts.", asset, modelId, artifactId, exception);
                        TimeSpan delay = GetRetryDelay(null, attempt);
                        progress?.Report(new ModelDownloadProgress(asset.AssetId!, ModelDownloadStage.Retrying, 0, asset.Size, attempt, 0));
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _downloadSlots.Release();
            }

            throw new InvalidOperationException("The download retry state was exhausted unexpectedly.");
        }

        private async Task<string> DownloadAttemptAsync(ModelCatalogAsset asset, string finalPath, string? modelId, string? artifactId, int attempt, IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken)
        {
            string temporaryPath = finalPath + ".download." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var timeoutSource = new CancellationTokenSource(_options.RequestTimeout))
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
                using (HttpResponseMessage response = await SendAssetRequestAsync(asset, modelId, artifactId, linked.Token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        if (IsTransient(response.StatusCode) && attempt <= _options.MaximumRetries)
                        {
                            throw new TransientHttpException(response.StatusCode, GetRetryDelay(response, attempt));
                        }

                        ThrowHttp("Asset HTTP request failed.", asset, modelId, artifactId, response.StatusCode);
                    }

                    long? contentLength = response.Content.Headers.ContentLength;
                    if (contentLength.HasValue && contentLength.Value != asset.Size) ThrowIntegrity("HTTP Content-Length does not match the catalog.", asset, modelId, artifactId, finalPath);
                    Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                    using (SHA256 sha = SHA256.Create())
                    {
                        var buffer = new byte[1024 * 1024];
                        long received = 0;
                        var stopwatch = Stopwatch.StartNew();
                        while (true)
                        {
                            int read = await input.ReadAsync(buffer, 0, buffer.Length, linked.Token).ConfigureAwait(false);
                            if (read == 0) break;
                            received = checked(received + read);
                            if (received > asset.Size || received > _options.MaximumAssetBytes) ThrowIntegrity("Downloaded bytes exceed the catalog or configured limit.", asset, modelId, artifactId, temporaryPath);
                            sha.TransformBlock(buffer, 0, read, buffer, 0);
                            await output.WriteAsync(buffer, 0, read, linked.Token).ConfigureAwait(false);
                            double speed = stopwatch.Elapsed.TotalSeconds <= 0 ? 0 : received / stopwatch.Elapsed.TotalSeconds;
                            progress?.Report(new ModelDownloadProgress(asset.AssetId!, ModelDownloadStage.Downloading, received, asset.Size, attempt, speed));
                        }

                        sha.TransformFinalBlock(new byte[0], 0, 0);
                        progress?.Report(new ModelDownloadProgress(asset.AssetId!, ModelDownloadStage.Verifying, received, asset.Size, attempt, 0));
                        if (received != asset.Size) ThrowIntegrity("Downloaded byte size does not match the catalog.", asset, modelId, artifactId, temporaryPath);
                        string actual = ToHex(sha.Hash!);
                        if (!string.Equals(actual, asset.Sha256, StringComparison.OrdinalIgnoreCase)) ThrowIntegrity("Downloaded SHA256 does not match the catalog.", asset, modelId, artifactId, temporaryPath);
#if NET8_0_OR_GREATER
                        output.Flush(true);
#else
                        output.Flush();
#endif
                    }
                }

                // Replace an invalid prior cache file atomically when it exists. / 当无效旧缓存存在时，以原子方式替换它。
                if (File.Exists(finalPath)) File.Replace(temporaryPath, finalPath, null);
                else File.Move(temporaryPath, finalPath);
                progress?.Report(new ModelDownloadProgress(asset.AssetId!, ModelDownloadStage.Completed, asset.Size, asset.Size, attempt, 0));
                return finalPath;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AttemptTimeoutException(exception);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private async Task<HttpResponseMessage> SendAssetRequestAsync(ModelCatalogAsset asset, string? modelId, string? artifactId, CancellationToken cancellationToken)
        {
            Uri origin = asset.DownloadUri!;
            Uri requestUri = origin;
            bool followedTrustedRedirect = false;
            while (true)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
                    HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    Uri? finalUri = response.RequestMessage?.RequestUri;
                    if (IsRedirect(response.StatusCode))
                    {
                        Uri? redirectUri = ResolveRedirectUri(requestUri, response.Headers.Location);
                        response.Dispose();
                        if (followedTrustedRedirect || redirectUri == null || !IsTrustedGitHubReleaseRedirect(origin, redirectUri)) ThrowHttp("Only one HTTPS redirect from a GitHub Release asset to release-assets.githubusercontent.com is allowed.", asset, modelId, artifactId, response.StatusCode);
                        requestUri = redirectUri!;
                        followedTrustedRedirect = true;
                        continue;
                    }

                    if (finalUri == null || finalUri != requestUri)
                    {
                        response.Dispose();
                        ThrowHttp("Asset HTTP request was automatically redirected; configure the supplied HttpClient with AllowAutoRedirect=false.", asset, modelId, artifactId, response.StatusCode);
                    }

                    return response;
                }
            }
        }

        private void InitializeManagedRoot()
        {
            if (Directory.Exists(_options.CacheRoot) && (File.GetAttributes(_options.CacheRoot) & FileAttributes.ReparsePoint) != 0) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Cache root cannot be a reparse point.", filePath: _options.CacheRoot);
            Directory.CreateDirectory(_managedRoot);
            if ((File.GetAttributes(_managedRoot) & FileAttributes.ReparsePoint) != 0) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Managed cache root cannot be a reparse point.", filePath: _managedRoot);
            string marker = Path.Combine(_managedRoot, RootMarker);
            if (!File.Exists(marker))
            {
                string[] existing = Directory.GetFileSystemEntries(_managedRoot);
                if (existing.Length > 0) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Existing managed cache root has no ownership marker.", filePath: _managedRoot);
                File.WriteAllText(marker, "DeploySharp ModelFactory managed cache v1", new UTF8Encoding(false));
            }

            Directory.CreateDirectory(Path.Combine(_managedRoot, "entries"));
            Directory.CreateDirectory(Path.Combine(_managedRoot, "test-assets"));
        }

        private void EnsureManagedRootMarker()
        {
            string marker = Path.Combine(_managedRoot, RootMarker);
            if (!File.Exists(marker) || (File.GetAttributes(_managedRoot) & FileAttributes.ReparsePoint) != 0) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Managed cache ownership marker is missing or unsafe.", filePath: _managedRoot);
        }

        private string GetManagedEntryRoot(string category, string key)
        {
            EnsureManagedRootMarker();
            if (key.Length != 64 || key.Any(character => !Uri.IsHexDigit(character))) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Cache key is invalid.");
            string value = Path.GetFullPath(Path.Combine(_managedRoot, category, key));
            if (!IsWithinRoot(_managedRoot, value)) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Cache entry escapes the managed root.", filePath: value);
            return value;
        }

        private string SafeCombine(string root, string relativePath)
        {
            string normalized = ModelPackagePath.NormalizeRelativePath(relativePath);
            string value = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinRoot(root, value)) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Resolved cache path escapes its root.", filePath: value);
            return value;
        }

        private static bool IsWithinRoot(string root, string candidate)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedCandidate = Path.GetFullPath(candidate);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return normalizedCandidate.StartsWith(normalizedRoot, comparison);
        }

        private static bool HasReparsePoint(string path)
        {
            string full = Path.GetFullPath(path);
            string? current = File.Exists(full) ? Path.GetDirectoryName(full) : full;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
                string? parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }

            return File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0;
        }

        private string ComputeSelectionKey(ModelSelection selection)
        {
            var builder = new StringBuilder();
            builder.Append(Catalog.CatalogRevision).Append('\n').Append(selection.Entry.Release?.Tag).Append('\n').Append(selection.Entry.ModelId).Append('\n').Append(selection.Artifact.ArtifactId).Append('\n');
            foreach (ModelCatalogAsset asset in selection.Artifact.Assets.OrderBy(value => value.RelativePath, StringComparer.Ordinal)) builder.Append(asset.CacheKey).Append('\n');
            return ComputeTextHash(builder.ToString());
        }

        private static string ComputeTextHash(string value)
        {
            using (SHA256 sha = SHA256.Create()) return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private long CheckedTotal(IEnumerable<ModelCatalogAsset> assets, string? modelId, string? artifactId)
        {
            long total = 0;
            try { foreach (ModelCatalogAsset asset in assets) total = checked(total + asset.Size); }
            catch (OverflowException) { Throw(ModelFactoryDiagnosticCodes.LimitExceeded, "Declared asset bytes overflow Int64.", modelId, artifactId); }
            return total;
        }

        private void WriteMetadata(string entryRoot, string cacheKey, ModelCatalogEntry entry, ModelCatalogArtifact? artifact, IEnumerable<ModelCatalogAsset> assets)
        {
            string now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            CacheMetadata? previous = ReadMetadata(entryRoot);
            var metadata = new CacheMetadata
            {
                CacheKey = cacheKey,
                CatalogRevision = Catalog.CatalogRevision,
                ReleaseTag = entry.Release?.Tag,
                ModelId = entry.ModelId,
                ArtifactId = artifact?.ArtifactId,
                DownloadedAt = previous?.DownloadedAt ?? now,
                VerifiedAt = now,
                LastAccessAt = now,
                VerificationStatus = "verified"
            };
            foreach (ModelCatalogAsset asset in assets) metadata.Assets.Add(new CacheAssetMetadata { AssetId = asset.AssetId, RelativePath = asset.RelativePath, Sha256 = asset.Sha256, Size = asset.Size, SourceUrl = Sanitize(asset.DownloadUri)?.AbsoluteUri, LicenseExpression = asset.LicenseExpression });
            Directory.CreateDirectory(entryRoot);
            string finalPath = Path.Combine(entryRoot, "entry.json");
            string temporary = finalPath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                if (File.Exists(finalPath)) File.Replace(temporary, finalPath, null);
                else File.Move(temporary, finalPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private CacheMetadata? ReadMetadata(string path)
        {
            string file = Path.Combine(path, "entry.json");
            if (!File.Exists(file)) return null;
            try { return JsonSerializer.Deserialize<CacheMetadata>(File.ReadAllText(file)); }
            catch (JsonException) { return null; }
            catch (IOException) { return null; }
        }

        private void CollectCacheDirectories(string root, List<CacheDirectory> output, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(root)) return;
            foreach (string path in Directory.EnumerateDirectories(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsWithinRoot(_managedRoot, path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                try { output.Add(new CacheDirectory(path, GetDirectorySize(path), new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero), ReadMetadata(path))); }
                catch (IOException exception)
                {
                    throw new ModelFactoryException("Managed cache inspection failed.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.CacheFailure, exception.Message, filePath: path) }, exception, exception.ToString());
                }
            }
        }

        private static long GetDirectorySize(string path)
        {
            long total = 0;
            var pending = new Stack<string>();
            pending.Push(path);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                foreach (string entry in Directory.EnumerateFileSystemEntries(current))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Managed cache contains an unsafe reparse point.");
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    else total = checked(total + new FileInfo(entry).Length);
                }
            }
            return total;
        }

        private void DeleteManagedDirectory(string path)
        {
            EnsureManagedRootMarker();
            string full = Path.GetFullPath(path);
            if (!IsWithinRoot(Path.Combine(_managedRoot, "entries"), full) && !IsWithinRoot(Path.Combine(_managedRoot, "test-assets"), full)) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Cleanup target escapes the managed cache namespace.", filePath: full);
            if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) Throw(ModelFactoryDiagnosticCodes.CacheFailure, "Cleanup refuses a reparse-point entry.", filePath: full);
            Directory.Delete(full, true);
        }

        private static void Touch(string path)
        {
            if (Directory.Exists(path)) Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }

        private TimeSpan GetRetryDelay(HttpResponseMessage? response, int attempt)
        {
            TimeSpan? retryAfter = response?.Headers.RetryAfter?.Delta;
            if (!retryAfter.HasValue && response?.Headers.RetryAfter?.Date != null) retryAfter = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
            double multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
            TimeSpan computed = retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero ? retryAfter.Value : TimeSpan.FromMilliseconds(_options.BaseRetryDelay.TotalMilliseconds * multiplier);
            return computed > _options.MaximumRetryDelay ? _options.MaximumRetryDelay : computed;
        }

        private static bool IsTransient(HttpStatusCode status)
        {
            int code = (int)status;
            return status == HttpStatusCode.RequestTimeout || code == 429 || code >= 500 && code <= 599;
        }

        private static bool IsRedirect(HttpStatusCode status)
        {
            int code = (int)status;
            return code >= 300 && code <= 399;
        }

        private static Uri? ResolveRedirectUri(Uri requestUri, Uri? location)
        {
            if (location == null) return null;
            return location.IsAbsoluteUri ? location : new Uri(requestUri, location);
        }

        private static bool IsTrustedGitHubReleaseRedirect(Uri origin, Uri redirectUri)
        {
            if (!string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(origin.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(redirectUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(redirectUri.Host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase)) return false;

            string[] segments = origin.AbsolutePath.Trim('/').Split('/');
            return segments.Length >= 5
                && string.Equals(segments[2], "releases", StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[3], "download", StringComparison.OrdinalIgnoreCase);
        }

        private static Uri? Sanitize(Uri? value)
        {
            if (value == null || !value.IsAbsoluteUri) return value;
            return new Uri(value.GetLeftPart(UriPartial.Path));
        }

        private static ModelFactoryException WrapNetwork(string message, ModelCatalogAsset asset, string? modelId, string? artifactId, Exception exception)
        {
            return new ModelFactoryException(message, new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.HttpFailure, exception.Message, modelId: modelId, artifactId: artifactId, assetId: asset.AssetId, uri: Sanitize(asset.DownloadUri)) }, exception, exception.ToString());
        }

        private static void ThrowHttp(string message, ModelCatalogAsset asset, string? modelId, string? artifactId, HttpStatusCode status)
        {
            throw new ModelFactoryException(message, new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.HttpFailure, message, modelId: modelId, artifactId: artifactId, assetId: asset.AssetId, uri: Sanitize(asset.DownloadUri), statusCode: status) }, technicalDetails: status.ToString());
        }

        private static void ThrowIntegrity(string message, ModelCatalogAsset asset, string? modelId, string? artifactId, string filePath)
        {
            throw new ModelFactoryException(message, new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.IntegrityMismatch, message, modelId: modelId, artifactId: artifactId, assetId: asset.AssetId, uri: Sanitize(asset.DownloadUri), filePath: filePath) }, technicalDetails: filePath);
        }

        private static void Throw(string code, string message, string? modelId = null, string? artifactId = null, string? assetId = null, Uri? uri = null, string? filePath = null)
        {
            throw new ModelFactoryException(message, new[] { new ModelFactoryDiagnostic(code, message, modelId: modelId, artifactId: artifactId, assetId: assetId, uri: Sanitize(uri), filePath: filePath) }, technicalDetails: filePath);
        }

        private void EnsureUsable()
        {
            if (_disposed) Throw(ModelFactoryDiagnosticCodes.ObjectDisposed, "The ModelFactory client has been disposed.");
        }

        private void DisposeResources()
        {
            _downloadSlots.Dispose();
            _disposeSource.Dispose();
            if (_ownsHttpClient) _httpClient.Dispose();
        }

        private static async Task<T> WaitForCallerAsync<T>(Task<T> operation, CancellationToken cancellationToken, string? modelId, string? artifactId, string? assetId = null)
        {
            if (!cancellationToken.CanBeCanceled) return await operation.ConfigureAwait(false);
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(operation, cancelled.Task).ConfigureAwait(false);
                if (completed == operation) return await operation.ConfigureAwait(false);
            }

            var inner = new OperationCanceledException(cancellationToken);
            throw new ModelFactoryException("ModelFactory operation was cancelled by the caller.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.Cancelled, "ModelFactory operation was cancelled by the caller.", modelId: modelId, artifactId: artifactId, assetId: assetId) }, inner, inner.ToString());
        }

        private void ObserveAndRemove<T>(Dictionary<string, Task<T>> operations, string key, Task<T> operation)
        {
            _ = operation.ContinueWith(_ =>
            {
                lock (_gate)
                {
                    if (operations.TryGetValue(key, out Task<T>? current) && ReferenceEquals(current, operation)) operations.Remove(key);
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private sealed class TransientHttpException : Exception
        {
            public TransientHttpException(HttpStatusCode statusCode, TimeSpan delay)
            {
                StatusCode = statusCode;
                Delay = delay;
            }

            public HttpStatusCode StatusCode { get; }
            public TimeSpan Delay { get; }
        }

        private sealed class AttemptTimeoutException : Exception
        {
            public AttemptTimeoutException(Exception innerException) : base("HTTP attempt timed out.", innerException) { }
        }

        private sealed class CacheDirectory
        {
            public CacheDirectory(string path, long size, DateTimeOffset lastWriteUtc, CacheMetadata? metadata)
            {
                Path = path;
                Size = size;
                LastWriteUtc = lastWriteUtc;
                Metadata = metadata;
            }

            public string Path { get; }
            public long Size { get; }
            public DateTimeOffset LastWriteUtc { get; }
            public CacheMetadata? Metadata { get; }
        }
    }
}
