using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Identifies a PaddleOCR document module without coupling it to Paddle Inference. / 标识 PaddleOCR 文档模块且不耦合飞桨推理运行时。</summary>
    public enum PaddleDocumentModule
    {
        DocumentOrientation = 0,
        TextImageUnwarping = 1,
        LayoutDetection = 2,
        TableClassification = 3,
        TableCellDetection = 4,
        TableStructureRecognition = 5,
        FormulaRecognition = 6,
        SealTextDetection = 7,
        ChartParsing = 8,
        StructurePipeline = 9
    }

    /// <summary>Describes the artifact format currently available for a Paddle document model. / 描述 Paddle 文档模型当前可用的工件格式。</summary>
    public enum PaddleDocumentArtifactStatus
    {
        Catalogued = 0,
        Downloaded = 1,
        OnnxConverted = 2,
        RuntimeVerified = 3,
        ConversionBlocked = 4
    }

    /// <summary>Identifies one official or converted Paddle document model. / 标识一个官方或转换后的 Paddle 文档模型。</summary>
    public sealed class PaddleDocumentModelDescriptor
    {
        public PaddleDocumentModelDescriptor(string modelId, string officialName, PaddleDocumentModule module, string sourceUrl, string modelFormat, PaddleDocumentArtifactStatus status = PaddleDocumentArtifactStatus.Catalogued, string? artifactPath = null, string? sha256 = null)
        {
            if (string.IsNullOrWhiteSpace(modelId)) throw new ArgumentException("A model id is required.", nameof(modelId));
            if (string.IsNullOrWhiteSpace(officialName)) throw new ArgumentException("An official model name is required.", nameof(officialName));
            if (!Enum.IsDefined(typeof(PaddleDocumentModule), module)) throw new ArgumentOutOfRangeException(nameof(module));
            if (string.IsNullOrWhiteSpace(sourceUrl)) throw new ArgumentException("A source URL is required.", nameof(sourceUrl));
            if (string.IsNullOrWhiteSpace(modelFormat)) throw new ArgumentException("A model format is required.", nameof(modelFormat));
            if (!Enum.IsDefined(typeof(PaddleDocumentArtifactStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (status >= PaddleDocumentArtifactStatus.OnnxConverted && string.IsNullOrWhiteSpace(artifactPath)) throw new ArgumentException("A converted artifact path is required for converted status.", nameof(artifactPath));
            if (sha256 != null && (sha256.Length != 64 || !IsHex(sha256))) throw new ArgumentException("SHA-256 must be 64 hexadecimal characters.", nameof(sha256));
            ModelId = modelId; OfficialName = officialName; Module = module; SourceUrl = sourceUrl; ModelFormat = modelFormat;
            Status = status; ArtifactPath = artifactPath; Sha256 = sha256?.ToLowerInvariant();
        }
        public string ModelId { get; }
        public string OfficialName { get; }
        public PaddleDocumentModule Module { get; }
        public string SourceUrl { get; }
        public string ModelFormat { get; }
        public PaddleDocumentArtifactStatus Status { get; }
        public string? ArtifactPath { get; }
        public string? Sha256 { get; }
        private static bool IsHex(string value) { foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F')) return false; return true; }
    }

    /// <summary>Common provenance for a document-module result. / 文档模块结果的通用来源信息。</summary>
    public sealed class PaddleDocumentResultMetadata
    {
        public PaddleDocumentResultMetadata(PaddleDocumentModelDescriptor model, string backend, TimeSpan elapsed, string inputSha256, int pageIndex = 0)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException("A backend is required.", nameof(backend));
            if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (string.IsNullOrWhiteSpace(inputSha256)) throw new ArgumentException("An input hash is required.", nameof(inputSha256));
            if (pageIndex < 0) throw new ArgumentOutOfRangeException(nameof(pageIndex));
            Backend = backend; Elapsed = elapsed; InputSha256 = inputSha256; PageIndex = pageIndex;
        }
        public PaddleDocumentModelDescriptor Model { get; }
        public string Backend { get; }
        public TimeSpan Elapsed { get; }
        public string InputSha256 { get; }
        public int PageIndex { get; }
    }

    /// <summary>Shared page-space geometry for layout, table and seal outputs. / 版面、表格和印章输出共享的页面坐标几何。</summary>
    public sealed class PaddleDocumentRegion
    {
        public PaddleDocumentRegion(string category, float score, RectangleF bounds, IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("A category is required.", nameof(category));
            if (float.IsNaN(score) || float.IsInfinity(score) || score < 0 || score > 1) throw new ArgumentOutOfRangeException(nameof(score));
            if (bounds.Width < 0 || bounds.Height < 0) throw new ArgumentOutOfRangeException(nameof(bounds));
            Category = category; Score = score; Bounds = bounds;
            var values = new Dictionary<string, string>();
            if (metadata != null) foreach (KeyValuePair<string, string> item in metadata) values.Add(item.Key, item.Value);
            Metadata = values;
        }
        public string Category { get; }
        public float Score { get; }
        public RectangleF Bounds { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }
    }

    /// <summary>Base result contract shared by independently runnable Paddle document modules. / 独立运行 Paddle 文档模块共享的结果合同。</summary>
    public abstract class PaddleDocumentModuleResult
    {
        protected PaddleDocumentModuleResult(PaddleDocumentModule module, PaddleDocumentResultMetadata metadata, IReadOnlyList<PaddleDocumentRegion>? regions = null, IReadOnlyList<string>? warnings = null)
        {
            if (!Enum.IsDefined(typeof(PaddleDocumentModule), module)) throw new ArgumentOutOfRangeException(nameof(module));
            Module = module; Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Regions = regions == null ? Array.Empty<PaddleDocumentRegion>() : new List<PaddleDocumentRegion>(regions).AsReadOnly();
            Warnings = warnings == null ? Array.Empty<string>() : new List<string>(warnings).AsReadOnly();
        }
        public PaddleDocumentModule Module { get; }
        public PaddleDocumentResultMetadata Metadata { get; }
        public IReadOnlyList<PaddleDocumentRegion> Regions { get; }
        public IReadOnlyList<string> Warnings { get; }
    }

    /// <summary>Stores the selected document orientation. / 保存选中的文档方向。</summary>
    public sealed class PaddleDocumentOrientationResult : PaddleDocumentModuleResult
    {
        public PaddleDocumentOrientationResult(PaddleDocumentResultMetadata metadata, string label, int rotationDegrees, IReadOnlyList<string>? warnings = null)
            : base(PaddleDocumentModule.DocumentOrientation, metadata, warnings: warnings)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("An orientation label is required.", nameof(label));
            if (rotationDegrees != 0 && rotationDegrees != 90 && rotationDegrees != 180 && rotationDegrees != 270) throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
            Label = label; RotationDegrees = rotationDegrees;
        }
        public string Label { get; }
        public int RotationDegrees { get; }
    }

    /// <summary>Stores a document unwarping result and its optional transform provenance. / 保存文档图像矫正结果及可选变换来源。</summary>
    public sealed class PaddleDocumentUnwarpingResult : PaddleDocumentModuleResult
    {
        public PaddleDocumentUnwarpingResult(PaddleDocumentResultMetadata metadata, int width, int height, string? transform = null, IReadOnlyList<string>? warnings = null, float[]? pixels = null, int channels = 3)
            : base(PaddleDocumentModule.TextImageUnwarping, metadata, warnings: warnings)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (pixels != null && pixels.LongLength != (long)width * height * channels) throw new ArgumentException("Unwarped pixel count does not match dimensions and channels.", nameof(pixels));
            Width = width; Height = height; Transform = transform ?? string.Empty; Channels = channels;
            Pixels = pixels == null ? Array.Empty<float>() : (float[])pixels.Clone();
        }
        public int Width { get; }
        public int Height { get; }
        public string Transform { get; }
        public int Channels { get; }
        public IReadOnlyList<float> Pixels { get; }
    }

    /// <summary>Stores one decoded table-structure token and its model confidence. / 保存一个已解码的表格结构 token 及其模型置信度。</summary>
    public sealed class PaddleDocumentTableToken
    {
        public PaddleDocumentTableToken(int index, string value, float score)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("A table token value is required.", nameof(value));
            if (float.IsNaN(score) || float.IsInfinity(score) || score < 0 || score > 1) throw new ArgumentOutOfRangeException(nameof(score));
            Index = index;
            Value = value;
            Score = score;
        }

        public int Index { get; }
        public string Value { get; }
        public float Score { get; }
    }

    /// <summary>Stores a recognized table structure and its cell regions. / 保存识别出的表格结构及单元格区域。</summary>
    public sealed class PaddleDocumentTableResult : PaddleDocumentModuleResult
    {
        public PaddleDocumentTableResult(PaddleDocumentModule module, PaddleDocumentResultMetadata metadata, string markup, IEnumerable<PaddleDocumentRegion>? cells = null, string? tableType = null, IReadOnlyList<string>? warnings = null, IEnumerable<PaddleDocumentTableToken>? tokens = null, float averageScore = 0)
            : base(module, metadata, cells == null ? null : new List<PaddleDocumentRegion>(cells), warnings)
        {
            if (module != PaddleDocumentModule.TableStructureRecognition && module != PaddleDocumentModule.TableCellDetection) throw new ArgumentException("Table result module must be table structure recognition or cell detection.", nameof(module));
            if (markup == null) throw new ArgumentNullException(nameof(markup));
            if (float.IsNaN(averageScore) || float.IsInfinity(averageScore) || averageScore < 0 || averageScore > 1) throw new ArgumentOutOfRangeException(nameof(averageScore));
            Markup = markup; TableType = tableType ?? string.Empty;
            Tokens = tokens == null ? Array.Empty<PaddleDocumentTableToken>() : new List<PaddleDocumentTableToken>(tokens).AsReadOnly();
            AverageScore = averageScore;
        }
        public string Markup { get; }
        public string TableType { get; }
        public IReadOnlyList<PaddleDocumentTableToken> Tokens { get; }
        public float AverageScore { get; }
    }

    /// <summary>Contains one table result per true model-batch row. / 包含真实模型 Batch 每一行的表格结果。</summary>
    public sealed class PaddleDocumentTableBatchResult
    {
        private readonly IReadOnlyList<PaddleDocumentTableResult> _items;

        public PaddleDocumentTableBatchResult(IEnumerable<PaddleDocumentTableResult> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var copied = new List<PaddleDocumentTableResult>();
            foreach (PaddleDocumentTableResult item in items) copied.Add(item ?? throw new ArgumentException("Table results cannot contain null values.", nameof(items)));
            if (copied.Count <= 1) throw new ArgumentException("A table batch result requires at least two items; batch one uses PaddleDocumentTableResult.", nameof(items));
            _items = copied.AsReadOnly();
        }

        public int Count => _items.Count;
        public PaddleDocumentTableResult this[int index] => _items[index];
        public IReadOnlyList<PaddleDocumentTableResult> Items => _items;
    }

    /// <summary>Stores formula recognition text in LaTeX or the converter's declared sequence format. / 保存 LaTeX 或转换器声明的公式序列文本。</summary>
    public sealed class PaddleDocumentFormulaResult : PaddleDocumentModuleResult
    {
        public PaddleDocumentFormulaResult(PaddleDocumentResultMetadata metadata, string latex, IReadOnlyList<string>? warnings = null, IEnumerable<int>? tokenIds = null)
            : base(PaddleDocumentModule.FormulaRecognition, metadata, warnings: warnings)
        {
            if (latex == null) throw new ArgumentNullException(nameof(latex));
            Latex = latex;
            TokenIds = tokenIds == null ? Array.Empty<int>() : new List<int>(tokenIds).AsReadOnly();
        }
        public string Latex { get; }
        public IReadOnlyList<int> TokenIds { get; }
    }

    /// <summary>Stores chart-to-table output in a caller-selected structured representation. / 以调用方选择的结构化表示保存图表转表格输出。</summary>
    public sealed class PaddleDocumentChartResult : PaddleDocumentModuleResult
    {
        public PaddleDocumentChartResult(PaddleDocumentResultMetadata metadata, string structuredData, IReadOnlyList<PaddleDocumentRegion>? regions = null, IReadOnlyList<string>? warnings = null)
            : base(PaddleDocumentModule.ChartParsing, metadata, regions, warnings)
        {
            if (structuredData == null) throw new ArgumentNullException(nameof(structuredData));
            StructuredData = structuredData;
        }
        public string StructuredData { get; }
    }

    /// <summary>Stores connected seal regions and the output mask dimensions. / 保存连通印章区域及输出掩码尺寸。</summary>
    public sealed class PaddleDocumentSealResult : PaddleDocumentModuleResult
    {
        public PaddleDocumentSealResult(PaddleDocumentResultMetadata metadata, int maskWidth, int maskHeight, IEnumerable<PaddleDocumentRegion> regions, IReadOnlyList<string>? warnings = null)
            : base(PaddleDocumentModule.SealTextDetection, metadata, regions == null ? null : new List<PaddleDocumentRegion>(regions), warnings)
        {
            if (maskWidth <= 0) throw new ArgumentOutOfRangeException(nameof(maskWidth));
            if (maskHeight <= 0) throw new ArgumentOutOfRangeException(nameof(maskHeight));
            MaskWidth = maskWidth; MaskHeight = maskHeight;
        }
        public int MaskWidth { get; }
        public int MaskHeight { get; }
    }
}
#pragma warning restore CS1591
