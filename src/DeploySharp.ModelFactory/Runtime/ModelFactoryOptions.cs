using System;
using System.Collections.Generic;
using System.Net;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Controls ModelFactory networking, cache, retry, and resource limits. / 控制 ModelFactory 网络、缓存、重试和资源限制。</summary>
    public sealed class ModelFactoryOptions
    {
        private readonly IReadOnlyList<string> _allowedSchemes;

        /// <summary>Initializes runtime options. / 初始化运行时选项。</summary>
        public ModelFactoryOptions(
            string cacheRoot,
            Uri? catalogUri = null,
            bool offline = false,
            bool allowTestInputs = true,
            long maximumAssetBytes = 20L * 1024L * 1024L * 1024L,
            long maximumOperationBytes = 50L * 1024L * 1024L * 1024L,
            int maximumConcurrentDownloads = 4,
            TimeSpan? requestTimeout = null,
            int maximumRetries = 3,
            TimeSpan? baseRetryDelay = null,
            TimeSpan? maximumRetryDelay = null,
            IWebProxy? proxy = null,
            IEnumerable<string>? allowedSchemes = null,
            string userAgent = "DeploySharp-ModelFactory/2.0")
        {
            if (string.IsNullOrWhiteSpace(cacheRoot)) throw new ArgumentException("A cache root is required.", nameof(cacheRoot));
            if (maximumAssetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumAssetBytes));
            if (maximumOperationBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOperationBytes));
            if (maximumConcurrentDownloads <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConcurrentDownloads));
            if (maximumRetries < 0 || maximumRetries > 20) throw new ArgumentOutOfRangeException(nameof(maximumRetries));
            RequestTimeout = requestTimeout ?? TimeSpan.FromMinutes(10);
            BaseRetryDelay = baseRetryDelay ?? TimeSpan.FromSeconds(1);
            MaximumRetryDelay = maximumRetryDelay ?? TimeSpan.FromSeconds(30);
            if (RequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
            if (BaseRetryDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(baseRetryDelay));
            if (MaximumRetryDelay < BaseRetryDelay) throw new ArgumentOutOfRangeException(nameof(maximumRetryDelay));
            if (string.IsNullOrWhiteSpace(userAgent)) throw new ArgumentException("A User-Agent is required.", nameof(userAgent));
            CacheRoot = System.IO.Path.GetFullPath(cacheRoot);
            CatalogUri = catalogUri;
            Offline = offline;
            AllowTestInputs = allowTestInputs;
            MaximumAssetBytes = maximumAssetBytes;
            MaximumOperationBytes = maximumOperationBytes;
            MaximumConcurrentDownloads = maximumConcurrentDownloads;
            MaximumRetries = maximumRetries;
            Proxy = proxy;
            UserAgent = userAgent;
            var schemes = new List<string>();
            foreach (string scheme in allowedSchemes ?? new[] { Uri.UriSchemeHttps })
            {
                if (string.IsNullOrWhiteSpace(scheme)) throw new ArgumentException("Allowed schemes cannot contain empty values.", nameof(allowedSchemes));
                string normalized = scheme.ToLowerInvariant();
                if (!schemes.Contains(normalized)) schemes.Add(normalized);
            }

            if (schemes.Count == 0) throw new ArgumentException("At least one allowed scheme is required.", nameof(allowedSchemes));
            _allowedSchemes = schemes.AsReadOnly();
        }

        /// <summary>Gets the application-owned parent cache root. / 获取应用所有的父缓存根目录。</summary>
        public string CacheRoot { get; }
        /// <summary>Gets the optional remote catalog URI. / 获取可选远程目录 URI。</summary>
        public Uri? CatalogUri { get; }
        /// <summary>Gets whether network access is prohibited. / 获取是否禁止网络访问。</summary>
        public bool Offline { get; }
        /// <summary>Gets whether test inputs may be downloaded. / 获取是否允许下载测试输入。</summary>
        public bool AllowTestInputs { get; }
        /// <summary>Gets the maximum bytes for one asset. / 获取单个资产最大字节数。</summary>
        public long MaximumAssetBytes { get; }
        /// <summary>Gets the maximum declared bytes for one operation. / 获取单次操作声明总字节上限。</summary>
        public long MaximumOperationBytes { get; }
        /// <summary>Gets the maximum concurrent asset downloads. / 获取最大并发资产下载数。</summary>
        public int MaximumConcurrentDownloads { get; }
        /// <summary>Gets the timeout for one HTTP attempt. / 获取单次 HTTP 尝试超时。</summary>
        public TimeSpan RequestTimeout { get; }
        /// <summary>Gets the maximum retry count after the initial attempt. / 获取首次尝试后的最大重试次数。</summary>
        public int MaximumRetries { get; }
        /// <summary>Gets the exponential retry base delay. / 获取指数重试基础延迟。</summary>
        public TimeSpan BaseRetryDelay { get; }
        /// <summary>Gets the maximum retry delay. / 获取最大重试延迟。</summary>
        public TimeSpan MaximumRetryDelay { get; }
        /// <summary>Gets the optional proxy used by an internally-created HttpClient. / 获取内部创建 HttpClient 使用的可选代理。</summary>
        public IWebProxy? Proxy { get; }
        /// <summary>Gets allowed catalog and asset URI schemes. / 获取允许的目录和资产 URI scheme。</summary>
        public IReadOnlyList<string> AllowedSchemes => _allowedSchemes;
        /// <summary>Gets the HTTP User-Agent. / 获取 HTTP User-Agent。</summary>
        public string UserAgent { get; }
    }
}
