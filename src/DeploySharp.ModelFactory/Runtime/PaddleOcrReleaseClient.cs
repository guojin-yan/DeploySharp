using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.Models;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Describes one independently downloadable PP-OCR or PP-Structure asset in the stable models-paddleocr release. / 描述稳定 models-paddleocr Release 中一个可独立下载的 PP-OCR 或 PP-Structure 资产。</summary>
    public sealed class PaddleOcrReleaseModel
    {
        internal PaddleOcrReleaseModel(string modelId, string assetName, string family, string? version, string? task, string? name, string? module, string? inputName, string? outputName, int? opset, long size, string sha256, string? dictionaryAssetName, string? profileFactory)
        {
            ModelId = modelId; AssetName = assetName; Family = family; Version = version; Task = task; Name = name; Module = module; InputName = inputName; OutputName = outputName; Opset = opset; Size = size; Sha256 = sha256; DictionaryAssetName = dictionaryAssetName; ProfileFactory = profileFactory;
        }

        /// <summary>Gets the release catalog model ID. Core IDs use ppocrv4/...; document IDs use paddle-doc/... and related namespaces. / 获取 Release 目录模型 ID。</summary>
        public string ModelId { get; }
        /// <summary>Gets the individual ONNX asset name. / 获取独立 ONNX 资产名称。</summary>
        public string AssetName { get; }
        /// <summary>Gets pp-ocr or pp-structure. / 获取 pp-ocr 或 pp-structure。</summary>
        public string Family { get; }
        /// <summary>Gets the model generation, when declared. / 获取模型代际（如果目录声明）。</summary>
        public string? Version { get; }
        /// <summary>Gets the task/family name, when declared. / 获取任务或模型族名称。</summary>
        public string? Task { get; }
        /// <summary>Gets the upstream display name. / 获取上游显示名称。</summary>
        public string? Name { get; }
        /// <summary>Gets the PP-Structure module, when declared. / 获取 PP-Structure 模块。</summary>
        public string? Module { get; }
        /// <summary>Gets the exact model input tensor name, when declared. / 获取模型输入张量名称。</summary>
        public string? InputName { get; }
        /// <summary>Gets the exact model output tensor name, when declared. / 获取模型输出张量名称。</summary>
        public string? OutputName { get; }
        /// <summary>Gets the exported ONNX opset, when declared. / 获取导出 ONNX opset。</summary>
        public int? Opset { get; }
        /// <summary>Gets the expected byte length. / 获取预期字节数。</summary>
        public long Size { get; }
        /// <summary>Gets the expected SHA-256. / 获取预期 SHA-256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets the optional recognition dictionary asset name. / 获取可选识别字典资产名称。</summary>
        public string? DictionaryAssetName { get; }
        /// <summary>Gets the DeploySharp profile factory hint for core OCR models. / 获取核心 OCR 模型的 DeploySharp Profile 工厂提示。</summary>
        public string? ProfileFactory { get; }
    }

    /// <summary>Describes a verified local model and its optional dictionary. / 描述已验证的本地模型及其可选字典。</summary>
    public sealed class PaddleOcrReleaseModelMaterialization
    {
        internal PaddleOcrReleaseModelMaterialization(PaddleOcrReleaseModel model, string modelPath, string? dictionaryPath)
        {
            Model = model; ModelPath = modelPath; DictionaryPath = dictionaryPath;
        }

        /// <summary>Gets the release model metadata. / 获取 Release 模型元数据。</summary>
        public PaddleOcrReleaseModel Model { get; }
        /// <summary>Gets the verified absolute ONNX path. / 获取已验证的绝对 ONNX 路径。</summary>
        public string ModelPath { get; }
        /// <summary>Gets the verified dictionary path, or null when the model does not require one. / 获取已验证字典路径。</summary>
        public string? DictionaryPath { get; }

        /// <summary>Creates a backend-neutral Core artifact for the downloaded file. Visual profiles should normally create the artifact themselves so their exact profile model ID is retained. / 为下载文件创建后端无关的 Core 工件；Visual Profile 通常应自行创建工件以保留精确模型 ID。</summary>
        public ModelArtifact CreateArtifact(ModelId modelId, BackendId? preferredBackend = null)
        {
            if (modelId.IsEmpty) throw new ArgumentException("A model ID is required.", nameof(modelId));
            return new ModelArtifact(modelId, "onnx", ModelPath, Model.Sha256, preferredBackend);
        }
    }

    /// <summary>Describes one independently downloadable artifact in a multi-file PaddleOCR generation bundle. / 描述 PaddleOCR 多文件生成 Bundle 中一个可独立下载的工件。</summary>
    public sealed class PaddleOcrReleaseBundleArtifact
    {
        internal PaddleOcrReleaseBundleArtifact(string role, string modelId, string assetName, string format, long size, string sha256)
        {
            Role = role; ModelId = modelId; AssetName = assetName; Format = format; Size = size; Sha256 = sha256;
        }

        /// <summary>Gets the bundle role, such as vision-projector or qwen-tokenizer. / 获取 Bundle 角色。</summary>
        public string Role { get; }
        /// <summary>Gets the stable code-facing model ID. / 获取稳定的代码侧模型 ID。</summary>
        public string ModelId { get; }
        /// <summary>Gets the simple GitHub Release asset filename. / 获取 GitHub Release 资产文件名。</summary>
        public string AssetName { get; }
        /// <summary>Gets the artifact format, such as onnx or tiktoken. / 获取工件格式。</summary>
        public string Format { get; }
        /// <summary>Gets the expected byte size. / 获取预期文件大小。</summary>
        public long Size { get; }
        /// <summary>Gets the expected SHA-256. / 获取预期 SHA-256。</summary>
        public string Sha256 { get; }
    }

    /// <summary>Describes a versioned multi-file model bundle in the PaddleOCR Release catalog. / 描述 PaddleOCR Release 目录中的版本化多文件模型 Bundle。</summary>
    public sealed class PaddleOcrReleaseBundle
    {
        private readonly IReadOnlyList<PaddleOcrReleaseBundleArtifact> _artifacts;
        internal PaddleOcrReleaseBundle(string bundleId, string modelId, IEnumerable<PaddleOcrReleaseBundleArtifact> artifacts)
        {
            BundleId = bundleId; ModelId = modelId; _artifacts = artifacts.ToList().AsReadOnly();
        }

        /// <summary>Gets the bundle ID used by GetBundleAsync. / 获取 GetBundleAsync 使用的 Bundle ID。</summary>
        public string BundleId { get; }
        /// <summary>Gets the primary DeploySharp model ID. / 获取 DeploySharp 主模型 ID。</summary>
        public string ModelId { get; }
        /// <summary>Gets all graph and tokenizer artifacts. / 获取全部计算图与 Tokenizer 工件。</summary>
        public IReadOnlyList<PaddleOcrReleaseBundleArtifact> Artifacts => _artifacts;
    }

    /// <summary>Describes verified local files from a downloaded generation bundle. / 描述已下载并校验的生成 Bundle 本地文件。</summary>
    public sealed class PaddleOcrReleaseBundleMaterialization
    {
        private readonly IReadOnlyDictionary<string, string> _paths;
        internal PaddleOcrReleaseBundleMaterialization(PaddleOcrReleaseBundle bundle, IDictionary<string, string> paths, string tokenizerDirectoryPath)
        {
            Bundle = bundle; _paths = new Dictionary<string, string>(paths, StringComparer.Ordinal); TokenizerDirectoryPath = tokenizerDirectoryPath;
        }

        /// <summary>Gets bundle metadata from the release catalog. / 获取 Release 目录中的 Bundle 元数据。</summary>
        public PaddleOcrReleaseBundle Bundle { get; }
        /// <summary>Gets the cache directory containing verified qwen.tiktoken, tokenizer_config.json, and added_tokens.json. / 获取包含已校验 Tokenizer 文件的缓存目录。</summary>
        public string TokenizerDirectoryPath { get; }
        /// <summary>Gets a verified local path by bundle role. / 按 Bundle 角色获取已校验的本地路径。</summary>
        public string GetRequiredPath(string role) => _paths.TryGetValue(role ?? string.Empty, out string? path) ? path : throw new KeyNotFoundException("The downloaded bundle does not contain artifact role: " + role);
        /// <summary>Creates an ONNX Core artifact for a graph role. / 按图角色创建 ONNX Core 工件。</summary>
        public ModelArtifact CreateOnnxArtifact(string role, BackendId? preferredBackend = null)
        {
            PaddleOcrReleaseBundleArtifact descriptor = Bundle.Artifacts.FirstOrDefault(value => string.Equals(value.Role, role, StringComparison.Ordinal)) ?? throw new KeyNotFoundException("The release bundle does not declare graph role: " + role);
            if (!string.Equals(descriptor.Format, "onnx", StringComparison.Ordinal)) throw new InvalidOperationException("Only ONNX graph roles can become Core model artifacts.");
            return new ModelArtifact(new ModelId(descriptor.ModelId), "onnx", GetRequiredPath(role), descriptor.Sha256, preferredBackend);
        }
    }

    /// <summary>Options for downloading individual assets from the stable PaddleOCR/PP-Structure release. / 从稳定 PaddleOCR/PP-Structure Release 下载独立资产的选项。</summary>
    public sealed class PaddleOcrReleaseClientOptions
    {
        public PaddleOcrReleaseClientOptions(string cacheRoot, string tag = "models-paddleocr", bool offline = false, long maximumAssetBytes = 20L * 1024L * 1024L * 1024L, TimeSpan? requestTimeout = null, string userAgent = "DeploySharp-PaddleOcrRelease/2.0")
        {
            if (string.IsNullOrWhiteSpace(cacheRoot)) throw new ArgumentException("A cache root is required.", nameof(cacheRoot));
            if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("A release tag is required.", nameof(tag));
            if (maximumAssetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumAssetBytes));
            if (string.IsNullOrWhiteSpace(userAgent)) throw new ArgumentException("A User-Agent is required.", nameof(userAgent));
            CacheRoot = Path.GetFullPath(cacheRoot); Tag = tag.Trim(); Offline = offline; MaximumAssetBytes = maximumAssetBytes; RequestTimeout = requestTimeout ?? TimeSpan.FromMinutes(20); UserAgent = userAgent;
            if (RequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }

        public string CacheRoot { get; }
        public string Tag { get; }
        public bool Offline { get; }
        public long MaximumAssetBytes { get; }
        public TimeSpan RequestTimeout { get; }
        public string UserAgent { get; }
    }

    /// <summary>Downloads and verifies only the PP-OCR/PP-Structure model selected by the caller. It does not create a backend session; pass the returned path to the matching Visual Profile factory. / 仅下载并校验调用方选择的 PP-OCR/PP-Structure 模型，不创建后端会话；返回路径可直接交给对应 Visual Profile 工厂。</summary>
    public sealed class PaddleOcrReleaseClient : IDisposable
    {
        public const string DefaultRepository = "guojin-yan/DeploySharp";
        public const string DefaultTag = "models-paddleocr";

        private readonly PaddleOcrReleaseClientOptions _options;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly string _root;
        private readonly object _gate = new object();
        private Task<IReadOnlyList<PaddleOcrReleaseModel>>? _catalogTask;
        private bool _disposed;

        public PaddleOcrReleaseClient(PaddleOcrReleaseClientOptions options, HttpClient? httpClient = null, string repository = DefaultRepository)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(repository) || repository.IndexOf('/') <= 0) throw new ArgumentException("A GitHub owner/repository is required.", nameof(repository));
            Repository = repository.Trim();
            _root = Path.Combine(_options.CacheRoot, ".deploysharp-paddleocr", _options.Tag);
            Directory.CreateDirectory(_root);
            if (httpClient != null) { _httpClient = httpClient; _ownsHttpClient = false; }
            else
            {
                var handler = new HttpClientHandler { AllowAutoRedirect = false };
                _httpClient = new HttpClient(handler, true) { Timeout = Timeout.InfiniteTimeSpan };
                _ownsHttpClient = true;
            }
        }

        public string Repository { get; }
        public Uri ReleaseBaseUri => new Uri("https://github.com/" + Repository + "/releases/download/" + Uri.EscapeDataString(_options.Tag) + "/", UriKind.Absolute);
        public Uri CatalogUri => new Uri(ReleaseBaseUri, "paddleocr-release-catalog.json");

        /// <summary>Loads the combined release catalog, cached only after valid JSON has been received. / 加载合并 Release 目录。</summary>
        public async Task<IReadOnlyList<PaddleOcrReleaseModel>> GetCatalogAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureUsable();
            Task<IReadOnlyList<PaddleOcrReleaseModel>> task;
            lock (_gate) { if (_catalogTask == null) _catalogTask = LoadCatalogAsync(); task = _catalogTask; }
            return await WaitForCatalogAsync(task, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Finds a model by release ID or by its core `paddleocr/` alias. / 按 Release ID 或核心 paddleocr/ 别名查找模型。</summary>
        public async Task<PaddleOcrReleaseModel> FindModelAsync(string modelId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(modelId)) throw new ArgumentException("A model ID is required.", nameof(modelId));
            string normalized = modelId.Trim();
            IReadOnlyList<PaddleOcrReleaseModel> models = await GetCatalogAsync(cancellationToken).ConfigureAwait(false);
            PaddleOcrReleaseModel? match = models.FirstOrDefault(value => string.Equals(value.ModelId, normalized, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                string[] prefixes = { "paddleocr/", "paddle-doc/", "paddle-table/", "paddle-formula/", "paddle-seal/", "paddle-chart/" };
                foreach (string prefix in prefixes)
                {
                    if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string alias = normalized.Substring(prefix.Length);
                    match = models.FirstOrDefault(value => string.Equals(value.ModelId, alias, StringComparison.OrdinalIgnoreCase));
                    if (match != null) break;
                }
            }
            if (match == null) throw new KeyNotFoundException("The models-paddleocr release does not contain model: " + modelId);
            return match;
        }

        /// <summary>Downloads and verifies one model plus its required recognition dictionary. / 下载并校验一个模型及其所需识别字典。</summary>
        public async Task<PaddleOcrReleaseModelMaterialization> GetModelAsync(string modelId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            PaddleOcrReleaseModel model = await FindModelAsync(modelId, cancellationToken).ConfigureAwait(false);
            string modelPath = await GetAssetAsync(model.AssetName, model.Size, model.Sha256, progress, cancellationToken).ConfigureAwait(false);
            string? dictionaryPath = null;
            if (!string.IsNullOrWhiteSpace(model.DictionaryAssetName)) dictionaryPath = await GetAssetAsync(model.DictionaryAssetName!, null, null, progress, cancellationToken).ConfigureAwait(false);
            return new PaddleOcrReleaseModelMaterialization(model, modelPath, dictionaryPath);
        }

        /// <summary>Downloads and verifies every asset in one declared multi-file bundle, without downloading unrelated models. / 仅下载并校验指定多文件 Bundle，不下载无关模型。</summary>
        public async Task<PaddleOcrReleaseBundleMaterialization> GetBundleAsync(string bundleId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(bundleId)) throw new ArgumentException("A release bundle ID is required.", nameof(bundleId));
            EnsureUsable();
            await GetCatalogAsync(cancellationToken).ConfigureAwait(false);
            string catalogPath = Path.Combine(_root, "paddleocr-release-catalog.json");
            PaddleOcrReleaseBundle bundle = ParseBundleCatalog(File.ReadAllText(catalogPath), bundleId.Trim());
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (PaddleOcrReleaseBundleArtifact artifact in bundle.Artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                paths.Add(artifact.Role, await GetAssetAsync(artifact.AssetName, artifact.Size, artifact.Sha256, progress, cancellationToken).ConfigureAwait(false));
            }
            ValidateChart2TableBundle(bundle);
            return new PaddleOcrReleaseBundleMaterialization(bundle, paths, _root);
        }

        /// <summary>Downloads an individual release asset. Expected size/hash are taken from the catalog for model assets and from SHA256SUMS when available for dictionaries. / 下载一个独立 Release 资产。</summary>
        public async Task<string> GetAssetAsync(string assetName, long? expectedSize = null, string? expectedSha256 = null, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureUsable();
            ValidateAssetName(assetName);
            if (!expectedSize.HasValue || string.IsNullOrWhiteSpace(expectedSha256))
            {
                IReadOnlyList<PaddleOcrReleaseModel> models = await GetCatalogAsync(cancellationToken).ConfigureAwait(false);
                PaddleOcrReleaseModel? row = models.FirstOrDefault(value => string.Equals(value.AssetName, assetName, StringComparison.OrdinalIgnoreCase));
                if (row != null) { expectedSize ??= row.Size; expectedSha256 ??= row.Sha256; }
                else
                {
                    Dictionary<string, string> checksums = await LoadChecksumsAsync(cancellationToken).ConfigureAwait(false);
                    if (checksums.TryGetValue(assetName, out string? hash)) expectedSha256 ??= hash;
                }
            }
            string destination = Path.Combine(_root, assetName);
            if (Validate(destination, expectedSize, expectedSha256, cancellationToken)) return destination;
            if (_options.Offline) throw new ModelFactoryException("Offline mode requires a verified PaddleOCR release asset cache entry.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.OfflineCacheMiss, "The requested PaddleOCR release asset is not cached.", assetId: assetName, filePath: destination) });
            string temporary = destination + ".download." + Guid.NewGuid().ToString("N") + ".tmp";
            Directory.CreateDirectory(_root);
            try
            {
                using (var timeout = new CancellationTokenSource(_options.RequestTimeout))
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
                using (HttpResponseMessage response = await SendAsync(new Uri(ReleaseBaseUri, Uri.EscapeDataString(assetName)), linked.Token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) throw new ModelFactoryException("PaddleOCR release asset download failed: " + response.StatusCode, new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.HttpFailure, "The release asset request failed.", assetId: assetName, statusCode: response.StatusCode) });
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > _options.MaximumAssetBytes) throw new ModelFactoryException("PaddleOCR release asset exceeds the configured byte limit.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.LimitExceeded, "The release asset is too large.", assetId: assetName) });
                    using (Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true))
                    {
                        byte[] buffer = new byte[128 * 1024]; long received = 0; int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, linked.Token).ConfigureAwait(false)) > 0)
                        {
                            received = checked(received + read); if (received > _options.MaximumAssetBytes) throw new ModelFactoryException("PaddleOCR release asset exceeds the configured byte limit.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.LimitExceeded, "The release asset is too large.", assetId: assetName) });
                            await output.WriteAsync(buffer, 0, read, linked.Token).ConfigureAwait(false);
                            progress?.Report(new ModelDownloadProgress(assetName, ModelDownloadStage.Downloading, received, expectedSize ?? response.Content.Headers.ContentLength ?? 0, 1, 0));
                        }
                    }
                }
                if (!Validate(temporary, expectedSize, expectedSha256, cancellationToken)) throw new ModelFactoryException("PaddleOCR release asset integrity validation failed.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.IntegrityMismatch, "The downloaded asset size or SHA-256 does not match the release catalog.", assetId: assetName, filePath: temporary) });
                if (File.Exists(destination)) File.Delete(destination); File.Move(temporary, destination);
                progress?.Report(new ModelDownloadProgress(assetName, ModelDownloadStage.Completed, new FileInfo(destination).Length, expectedSize ?? new FileInfo(destination).Length, 1, 0));
                return destination;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private async Task<IReadOnlyList<PaddleOcrReleaseModel>> LoadCatalogAsync()
        {
            string cachedCatalog = Path.Combine(_root, "paddleocr-release-catalog.json");
            if (_options.Offline)
            {
                if (!File.Exists(cachedCatalog)) throw new ModelFactoryException("Offline mode requires a cached PaddleOCR release catalog.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.OfflineCacheMiss, "The PaddleOCR release catalog is not cached.", filePath: cachedCatalog) });
                return ParseCatalog(File.ReadAllText(cachedCatalog));
            }
            using (var timeout = new CancellationTokenSource(_options.RequestTimeout))
            using (HttpResponseMessage response = await SendAsync(CatalogUri, timeout.Token).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw new ModelFactoryException("PaddleOCR release catalog download failed.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.HttpFailure, "The release catalog request failed.", statusCode: response.StatusCode) });
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                File.WriteAllText(cachedCatalog, json, new UTF8Encoding(false));
                return ParseCatalog(json);
            }
        }

        private async Task<Dictionary<string, string>> LoadChecksumsAsync(CancellationToken cancellationToken)
        {
            string checksumPath = Path.Combine(_root, "SHA256SUMS");
            string text;
            if (_options.Offline)
            {
                if (!File.Exists(checksumPath)) throw new ModelFactoryException("Offline mode requires cached PaddleOCR release checksums.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.OfflineCacheMiss, "SHA256SUMS is not cached.", filePath: checksumPath) });
                text = File.ReadAllText(checksumPath);
            }
            else
            {
                using (var timeout = new CancellationTokenSource(_options.RequestTimeout))
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
                    text = Encoding.UTF8.GetString(await DownloadBytesAsync(new Uri(ReleaseBaseUri, "SHA256SUMS"), linked.Token).ConfigureAwait(false));
                File.WriteAllText(checksumPath, text, new UTF8Encoding(false));
            }
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0].Length == 64) values[parts[1].TrimStart('*')] = parts[0].ToLowerInvariant();
            }
            return values;
        }

        private static IReadOnlyList<PaddleOcrReleaseModel> ParseCatalog(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                var result = new List<PaddleOcrReleaseModel>();
                AddRows(document.RootElement, "corePaddleOcr", "pp-ocr", result);
                AddRows(document.RootElement, "ppStructure", "pp-structure", result);
                return result.AsReadOnly();
            }
        }

        private static PaddleOcrReleaseBundle ParseBundleCatalog(string json, string bundleId)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("bundles", out JsonElement bundles) || bundles.ValueKind != JsonValueKind.Array) throw new KeyNotFoundException("The models-paddleocr release catalog has no multi-file bundles.");
                foreach (JsonElement row in bundles.EnumerateArray())
                {
                    if (!string.Equals(Required(row, "bundleId"), bundleId, StringComparison.OrdinalIgnoreCase)) continue;
                    string modelId = Required(row, "modelId");
                    if (!row.TryGetProperty("artifacts", out JsonElement artifacts) || artifacts.ValueKind != JsonValueKind.Array) throw new FormatException("A release bundle must declare its artifacts as an array.");
                    var parsed = new List<PaddleOcrReleaseBundleArtifact>();
                    var roles = new HashSet<string>(StringComparer.Ordinal);
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (JsonElement artifact in artifacts.EnumerateArray())
                    {
                        string role = Required(artifact, "role");
                        string assetName = Required(artifact, "assetName");
                        ValidateAssetName(assetName);
                        string assetModelId = Required(artifact, "modelId");
                        string format = Required(artifact, "format").ToLowerInvariant();
                        long size = artifact.GetProperty("size").GetInt64();
                        string sha = Required(artifact, "sha256").ToLowerInvariant();
                        if (!roles.Add(role) || !names.Add(assetName)) throw new FormatException("Bundle artifact roles and asset filenames must be unique.");
                        if (size <= 0 || sha.Length != 64 || !sha.All(Uri.IsHexDigit)) throw new FormatException("A bundle artifact must declare a positive size and valid SHA-256.");
                        if (format != "onnx" && format != "tiktoken" && format != "json") throw new FormatException("The release bundle declares an unsupported artifact format: " + format);
                        if (format == "onnx" && !assetModelId.StartsWith(modelId + "/", StringComparison.Ordinal)) throw new FormatException("ONNX graph model IDs must be nested below the bundle model ID.");
                        parsed.Add(new PaddleOcrReleaseBundleArtifact(role, assetModelId, assetName, format, size, sha));
                    }
                    var result = new PaddleOcrReleaseBundle(bundleId, modelId, parsed);
                    ValidateChart2TableBundle(result);
                    return result;
                }
            }
            throw new KeyNotFoundException("The models-paddleocr release does not contain bundle: " + bundleId);
        }

        private static void ValidateChart2TableBundle(PaddleOcrReleaseBundle bundle)
        {
            if (!string.Equals(bundle.ModelId, "paddle-chart/pp-chart2table", StringComparison.Ordinal)) return;
            string[] required = { "vision-projector", "token-embedding", "text-prefill", "text-decode-with-past", "qwen-tokenizer", "tokenizer-config", "added-tokens" };
            if (bundle.Artifacts.Count != required.Length || required.Any(role => !bundle.Artifacts.Any(value => string.Equals(value.Role, role, StringComparison.Ordinal)))) throw new FormatException("The PP-Chart2Table release bundle must contain four ONNX graphs and all three official tokenizer files.");
            string[] graphRoles = { "vision-projector", "token-embedding", "text-prefill", "text-decode-with-past" };
            if (graphRoles.Any(role => bundle.Artifacts.First(value => value.Role == role).Format != "onnx")) throw new FormatException("All PP-Chart2Table graph artifacts must use ONNX format.");
            if (bundle.Artifacts.First(value => value.Role == "qwen-tokenizer").Format != "tiktoken" || bundle.Artifacts.Where(value => value.Role == "tokenizer-config" || value.Role == "added-tokens").Any(value => value.Format != "json")) throw new FormatException("PP-Chart2Table tokenizer assets have invalid formats.");
        }

        private static void AddRows(JsonElement root, string property, string family, List<PaddleOcrReleaseModel> result)
        {
            if (!root.TryGetProperty(property, out JsonElement rows) || rows.ValueKind != JsonValueKind.Array) return;
            foreach (JsonElement row in rows.EnumerateArray())
            {
                string modelId = Required(row, "modelId"); string assetName = Required(row, "assetName"); string sha = Required(row, "sha256"); long size = row.GetProperty("size").GetInt64();
                int? opset = row.TryGetProperty("opset", out JsonElement opsetValue) && opsetValue.ValueKind == JsonValueKind.Number && opsetValue.TryGetInt32(out int parsedOpset) ? parsedOpset : (int?)null;
                result.Add(new PaddleOcrReleaseModel(modelId, assetName, family, Optional(row, "version"), Optional(row, "task"), Optional(row, "name"), Optional(row, "module"), Optional(row, "input"), Optional(row, "output"), opset, size, sha.ToLowerInvariant(), Optional(row, "dictionaryAssetName"), Optional(row, "profileFactory")));
            }
        }

        private async Task<byte[]> DownloadBytesAsync(Uri uri, CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await SendAsync(uri, cancellationToken).ConfigureAwait(false)) { if (!response.IsSuccessStatusCode) throw new ModelFactoryException("PaddleOCR release metadata download failed.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.HttpFailure, "Release metadata request failed.", uri: uri, statusCode: response.StatusCode) }); return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false); }
        }

        private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken cancellationToken)
        {
            Uri current = uri;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, current))
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
                    HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    if (!((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399)) return response;
                    Uri? location = response.Headers.Location; response.Dispose(); if (location == null) break; current = location.IsAbsoluteUri ? location : new Uri(current, location);
                    if (!IsTrustedRedirect(uri, current)) break;
                }
            }
            throw new ModelFactoryException("PaddleOCR release asset redirect was not trusted.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.HttpFailure, "Only GitHub release CDN redirects are accepted.", uri: uri) });
        }

        private static bool IsTrustedRedirect(Uri origin, Uri target) => origin.Scheme == Uri.UriSchemeHttps && target.Scheme == Uri.UriSchemeHttps && string.Equals(target.Host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) && string.Equals(origin.Host, "github.com", StringComparison.OrdinalIgnoreCase);

        private static bool Validate(string path, long? expectedSize, string? expectedSha256, CancellationToken cancellationToken)
        {
            if (!File.Exists(path)) return false; var info = new FileInfo(path); if (expectedSize.HasValue && info.Length != expectedSize.Value) return false; if (string.IsNullOrWhiteSpace(expectedSha256)) return true; return string.Equals(ModelFileIntegrity.ComputeSha256(path, cancellationToken), expectedSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static string Required(JsonElement row, string name) => row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new FormatException("PaddleOCR release catalog is missing " + name + ".");
        private static string? Optional(JsonElement row, string name) => row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
        private static void ValidateAssetName(string name) { if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0 || name == "." || name == "..") throw new ArgumentException("Release asset names must be simple file names.", nameof(name)); }
        private static async Task<T> WaitForCatalogAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return await task.ConfigureAwait(false);
            var cancelled = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false);
                if (completed == task) return await task.ConfigureAwait(false);
            }
            throw new OperationCanceledException(cancellationToken);
        }
        private void EnsureUsable() { if (_disposed) throw new ObjectDisposedException(nameof(PaddleOcrReleaseClient)); }
        public void Dispose() { if (_disposed) return; _disposed = true; if (_ownsHttpClient) _httpClient.Dispose(); }
    }
}
#pragma warning restore CS1591
