using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelPack.Json;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Identifies the current download/cache operation stage. / 标识当前下载/缓存操作阶段。</summary>
    public enum ModelDownloadStage
    {
        /// <summary>The cache is being checked. / 正在检查缓存。</summary>
        CheckingCache = 0,
        /// <summary>The asset is being downloaded. / 正在下载资产。</summary>
        Downloading = 1,
        /// <summary>The asset hash and size are being verified. / 正在验证资产 hash 和大小。</summary>
        Verifying = 2,
        /// <summary>The asset was materialized successfully. / 资产已成功物化。</summary>
        Completed = 3,
        /// <summary>A retry delay is in progress. / 正在等待重试。</summary>
        Retrying = 4
    }

    /// <summary>Reports bounded progress for one asset. / 报告单个资产的有界进度。</summary>
    public sealed class ModelDownloadProgress
    {
        /// <summary>Initializes a progress update. / 初始化进度更新。</summary>
        public ModelDownloadProgress(string assetId, ModelDownloadStage stage, long receivedBytes, long totalBytes, int attempt, double bytesPerSecond)
        {
            AssetId = assetId ?? throw new ArgumentNullException(nameof(assetId));
            Stage = stage;
            ReceivedBytes = receivedBytes;
            TotalBytes = totalBytes;
            Attempt = attempt;
            BytesPerSecond = bytesPerSecond;
        }

        /// <summary>Gets the asset identifier. / 获取资产标识。</summary>
        public string AssetId { get; }
        /// <summary>Gets the operation stage. / 获取操作阶段。</summary>
        public ModelDownloadStage Stage { get; }
        /// <summary>Gets received bytes. / 获取已接收字节数。</summary>
        public long ReceivedBytes { get; }
        /// <summary>Gets expected total bytes. / 获取预期总字节数。</summary>
        public long TotalBytes { get; }
        /// <summary>Gets the one-based HTTP attempt. / 获取从 1 开始的 HTTP 尝试次数。</summary>
        public int Attempt { get; }
        /// <summary>Gets the observed average bytes per second. / 获取观测到的平均每秒字节数。</summary>
        public double BytesPerSecond { get; }
    }

    /// <summary>Represents one verified model package materialized from the catalog. / 表示一个从目录物化并验证的模型包。</summary>
    public sealed class MaterializedModel
    {
        internal MaterializedModel(ModelSelection selection, string cacheKey, string packageRoot, LocalModelPackage package)
        {
            Selection = selection;
            CacheKey = cacheKey;
            PackageRoot = packageRoot;
            Package = package;
        }

        /// <summary>Gets the catalog selection. / 获取目录选择结果。</summary>
        public ModelSelection Selection { get; }
        /// <summary>Gets the content-addressed cache key. / 获取内容寻址缓存键。</summary>
        public string CacheKey { get; }
        /// <summary>Gets the materialized package root. / 获取物化模型包根目录。</summary>
        public string PackageRoot { get; }
        /// <summary>Gets the fully verified local ModelPack. / 获取完整验证的本地 ModelPack。</summary>
        public LocalModelPackage Package { get; }
    }

    /// <summary>Represents one verified test or auxiliary asset. / 表示一个已验证测试或辅助资产。</summary>
    public sealed class MaterializedAsset
    {
        internal MaterializedAsset(ModelCatalogAsset asset, string fullPath)
        {
            Asset = asset;
            FullPath = fullPath;
        }

        /// <summary>Gets catalog metadata. / 获取目录元数据。</summary>
        public ModelCatalogAsset Asset { get; }
        /// <summary>Gets the verified absolute local path. / 获取已验证绝对本地路径。</summary>
        public string FullPath { get; }
    }

    /// <summary>Defines scoped cache cleanup criteria. / 定义有范围的缓存清理条件。</summary>
    public sealed class ModelCacheCleanupOptions
    {
        /// <summary>Initializes cleanup criteria. / 初始化清理条件。</summary>
        public ModelCacheCleanupOptions(TimeSpan? olderThan = null, long? maximumBytesToKeep = null, string? catalogRevision = null, string? releaseTag = null, bool dryRun = false)
        {
            if (olderThan.HasValue && olderThan.Value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(olderThan));
            if (maximumBytesToKeep.HasValue && maximumBytesToKeep.Value < 0) throw new ArgumentOutOfRangeException(nameof(maximumBytesToKeep));
            OlderThan = olderThan;
            MaximumBytesToKeep = maximumBytesToKeep;
            CatalogRevision = catalogRevision;
            ReleaseTag = releaseTag;
            DryRun = dryRun;
        }

        /// <summary>Gets the optional inactivity age threshold. / 获取可选非活动时长阈值。</summary>
        public TimeSpan? OlderThan { get; }
        /// <summary>Gets the optional cache byte budget after cleanup. / 获取清理后的可选缓存字节预算。</summary>
        public long? MaximumBytesToKeep { get; }
        /// <summary>Gets the optional catalog revision filter. / 获取可选目录修订筛选。</summary>
        public string? CatalogRevision { get; }
        /// <summary>Gets the optional Release tag filter. / 获取可选 Release 标签筛选。</summary>
        public string? ReleaseTag { get; }
        /// <summary>Gets whether deletion is simulated. / 获取是否模拟删除。</summary>
        public bool DryRun { get; }
    }

    /// <summary>Summarizes a scoped cache cleanup operation. / 汇总有范围的缓存清理操作。</summary>
    public sealed class ModelCacheCleanupResult
    {
        internal ModelCacheCleanupResult(int entriesRemoved, long bytesRemoved, bool dryRun)
        {
            EntriesRemoved = entriesRemoved;
            BytesRemoved = bytesRemoved;
            DryRun = dryRun;
        }

        /// <summary>Gets removed or selected entry count. / 获取已删除或选中的条目数。</summary>
        public int EntriesRemoved { get; }
        /// <summary>Gets removed or selected byte count. / 获取已删除或选中的字节数。</summary>
        public long BytesRemoved { get; }
        /// <summary>Gets whether this was a dry run. / 获取是否为模拟运行。</summary>
        public bool DryRun { get; }
    }

    /// <summary>Defines the validated catalog, selection, model retrieval, test-input, verification, and cleanup workflow. / 定义已验证目录、选择、模型获取、测试输入、验证和清理工作流。</summary>
    public interface IModelFactory : IDisposable
    {
        /// <summary>Gets the immutable catalog snapshot. / 获取不可变目录快照。</summary>
        public ValidatedModelCatalog Catalog { get; }
        /// <summary>Selects one required model artifact or throws a diagnostic no-match error. / 选择一个必需模型工件，未匹配时抛出可诊断错误。</summary>
        public ModelSelection Select(ModelQuery query);
        /// <summary>Downloads or reuses a verified model package. / 下载或复用已验证模型包。</summary>
        public Task<MaterializedModel> GetModelAsync(ModelSelection selection, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>Downloads or reuses a verified test input. / 下载或复用已验证测试输入。</summary>
        public Task<MaterializedAsset> GetTestInputAsync(ModelCatalogEntry entry, string assetId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>Revalidates an existing model cache entry without network access. / 在不访问网络的情况下重新验证已有模型缓存条目。</summary>
        public Task<bool> VerifyModelCacheAsync(ModelSelection selection, CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>Cleans only the managed ModelFactory cache namespace. / 仅清理 ModelFactory 管理的缓存命名空间。</summary>
        public Task<ModelCacheCleanupResult> CleanCacheAsync(ModelCacheCleanupOptions options, CancellationToken cancellationToken = default(CancellationToken));
    }
}
