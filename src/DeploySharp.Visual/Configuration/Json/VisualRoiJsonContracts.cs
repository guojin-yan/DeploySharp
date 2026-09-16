using System;

namespace JYPPX.DeploySharp.Visual.Configuration.Json
{
    /// <summary>Controls limits and unknown-field handling for ROI JSON operations. / 控制 ROI JSON 操作的容量限制和未知字段处理。</summary>
    public sealed class VisualRoiJsonOptions
    {
        /// <summary>Initializes JSON limits. / 初始化 JSON 限制。</summary>
        public VisualRoiJsonOptions(long maximumDocumentBytes = 16L * 1024 * 1024, int maximumRois = 4096, int maximumPolygonPoints = 4096, int maximumMetadataEntries = 64, bool rejectUnknownProperties = true)
        {
            if (maximumDocumentBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDocumentBytes));
            if (maximumRois <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRois));
            if (maximumPolygonPoints < 3) throw new ArgumentOutOfRangeException(nameof(maximumPolygonPoints));
            if (maximumMetadataEntries < 0) throw new ArgumentOutOfRangeException(nameof(maximumMetadataEntries));
            MaximumDocumentBytes = maximumDocumentBytes;
            MaximumRois = maximumRois;
            MaximumPolygonPoints = maximumPolygonPoints;
            MaximumMetadataEntries = maximumMetadataEntries;
            RejectUnknownProperties = rejectUnknownProperties;
        }

        /// <summary>Gets the maximum UTF-8 document size. / 获取 UTF-8 文档最大尺寸。</summary>
        public long MaximumDocumentBytes { get; }
        /// <summary>Gets the maximum number of ROI entries. / 获取最大 ROI 数量。</summary>
        public int MaximumRois { get; }
        /// <summary>Gets the maximum polygon vertex count. / 获取多边形最大顶点数。</summary>
        public int MaximumPolygonPoints { get; }
        /// <summary>Gets the maximum metadata entry count per ROI. / 获取每个 ROI 最大元数据项数量。</summary>
        public int MaximumMetadataEntries { get; }
        /// <summary>Gets whether unknown JSON properties are rejected. / 获取是否拒绝未知 JSON 字段。</summary>
        public bool RejectUnknownProperties { get; }
        /// <summary>Gets default bounded options. / 获取默认有界配置。</summary>
        public static VisualRoiJsonOptions Default { get; } = new VisualRoiJsonOptions();
    }

    /// <summary>Reports a malformed or unsafe ROI JSON document. / 报告格式错误或不安全的 ROI JSON 文档。</summary>
    public sealed class VisualRoiJsonException : Exception
    {
        /// <summary>Initializes a JSON error. / 初始化 JSON 错误。</summary>
        public VisualRoiJsonException(string message, string code, string path, Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <summary>Gets a stable error code. / 获取稳定错误代码。</summary>
        public string Code { get; }
        /// <summary>Gets the JSON path associated with the error. / 获取关联的 JSON 路径。</summary>
        public string Path { get; }
    }
}
