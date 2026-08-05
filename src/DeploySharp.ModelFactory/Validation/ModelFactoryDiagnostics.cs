using System;
using System.Collections.Generic;
using System.Net;
using JYPPX.DeploySharp.Errors;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Defines stable ModelFactory diagnostic identifiers. / 定义稳定的 ModelFactory 诊断标识。</summary>
    public static class ModelFactoryDiagnosticCodes
    {
        /// <summary>The catalog has invalid structure or version. / 目录结构或版本无效。</summary>
        public const string CatalogInvalid = "model-factory.catalog-invalid";
        /// <summary>A release tag is mutable or does not match the recorded release. / Release 标签可变或与记录不匹配。</summary>
        public const string MutableReleaseTag = "model-factory.mutable-release-tag";
        /// <summary>An asset URL, path, or metadata is invalid. / 资产 URL、路径或元数据无效。</summary>
        public const string AssetInvalid = "model-factory.asset-invalid";
        /// <summary>An artifact is not eligible for supported distribution. / 工件不符合受支持分发准入条件。</summary>
        public const string AdmissionRejected = "model-factory.admission-rejected";
        /// <summary>No query candidate matched. / 没有匹配查询的候选项。</summary>
        public const string NoMatch = "model-factory.no-match";
        /// <summary>The HTTP response or status is invalid. / HTTP 响应或状态无效。</summary>
        public const string HttpFailure = "model-factory.http-failure";
        /// <summary>The request timed out. / 请求超时。</summary>
        public const string Timeout = "model-factory.timeout";
        /// <summary>The operation was cancelled. / 操作已取消。</summary>
        public const string Cancelled = "model-factory.cancelled";
        /// <summary>The downloaded asset failed integrity validation. / 下载资产未通过完整性验证。</summary>
        public const string IntegrityMismatch = "model-factory.integrity-mismatch";
        /// <summary>The offline cache does not contain a verified asset. / 离线缓存没有已验证资产。</summary>
        public const string OfflineCacheMiss = "model-factory.offline-cache-miss";
        /// <summary>The cache lock or cache layout is invalid. / 缓存锁或缓存布局无效。</summary>
        public const string CacheFailure = "model-factory.cache-failure";
        /// <summary>A configured resource limit was exceeded. / 超出配置资源限制。</summary>
        public const string LimitExceeded = "model-factory.limit-exceeded";
        /// <summary>The license or redistribution metadata is insufficient. / 许可证或再分发元数据不足。</summary>
        public const string LicenseRejected = "model-factory.license-rejected";
        /// <summary>The object has already been disposed. / 对象已释放。</summary>
        public const string ObjectDisposed = "model-factory.object-disposed";
    }

    /// <summary>Represents one structured ModelFactory diagnostic. / 表示一条结构化 ModelFactory 诊断。</summary>
    public sealed class ModelFactoryDiagnostic
    {
        /// <summary>Initializes a diagnostic. / 初始化诊断。</summary>
        public ModelFactoryDiagnostic(string code, string message, string? jsonPath = null, string? modelId = null, string? artifactId = null, string? assetId = null, Uri? uri = null, string? filePath = null, HttpStatusCode? statusCode = null)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("A diagnostic code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("A diagnostic message is required.", nameof(message));
            Code = code;
            Message = message;
            JsonPath = jsonPath;
            ModelId = modelId;
            ArtifactId = artifactId;
            AssetId = assetId;
            Uri = uri;
            FilePath = filePath;
            StatusCode = statusCode;
        }

        /// <summary>Gets the stable code. / 获取稳定代码。</summary>
        public string Code { get; }
        /// <summary>Gets the user-facing message. / 获取面向用户的消息。</summary>
        public string Message { get; }
        /// <summary>Gets the catalog JSON path. / 获取目录 JSON 路径。</summary>
        public string? JsonPath { get; }
        /// <summary>Gets the model identifier. / 获取模型标识。</summary>
        public string? ModelId { get; }
        /// <summary>Gets the artifact identifier. / 获取工件标识。</summary>
        public string? ArtifactId { get; }
        /// <summary>Gets the asset identifier. / 获取资产标识。</summary>
        public string? AssetId { get; }
        /// <summary>Gets the diagnostic URI without credentials. / 获取不含凭据的诊断 URI。</summary>
        public Uri? Uri { get; }
        /// <summary>Gets the local file path when known. / 获取已知的本地文件路径。</summary>
        public string? FilePath { get; }
        /// <summary>Gets the HTTP status when known. / 获取已知的 HTTP 状态。</summary>
        public HttpStatusCode? StatusCode { get; }
    }

    /// <summary>Reports one or more catalog, download, or cache failures. / 报告一个或多个目录、下载或缓存故障。</summary>
    public sealed class ModelFactoryException : DeploySharpException
    {
        private readonly IReadOnlyList<ModelFactoryDiagnostic> _diagnostics;

        /// <summary>Initializes a structured ModelFactory exception. / 初始化结构化 ModelFactory 异常。</summary>
        public ModelFactoryException(string message, IEnumerable<ModelFactoryDiagnostic> diagnostics, Exception? innerException = null, string? technicalDetails = null)
            : base(DeploySharpErrorCodes.ModelArtifactInvalid, message, innerException, technicalDetails: technicalDetails)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            var list = new List<ModelFactoryDiagnostic>();
            foreach (ModelFactoryDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic == null) throw new ArgumentException("Diagnostics cannot contain null values.", nameof(diagnostics));
                list.Add(diagnostic);
            }

            if (list.Count == 0) throw new ArgumentException("At least one diagnostic is required.", nameof(diagnostics));
            _diagnostics = list.AsReadOnly();
        }

        /// <summary>Gets all structured diagnostics. / 获取全部结构化诊断。</summary>
        public IReadOnlyList<ModelFactoryDiagnostic> Diagnostics => _diagnostics;
    }
}
