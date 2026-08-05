using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Identifies whether a model artifact resolves to a file or directory. / 标识模型工件解析为文件还是目录。</summary>
    public enum ModelArtifactLocationKind
    {
        /// <summary>The entrypoint is a file. / 入口点是文件。</summary>
        File = 0,
        /// <summary>The entrypoint is a directory containing listed files. / 入口点是包含已列出文件的目录。</summary>
        Directory = 1
    }

    /// <summary>Describes the role of one file inside a model artifact. / 描述模型工件中一个文件的角色。</summary>
    public enum ModelFileRole
    {
        /// <summary>Primary model graph or weights. / 主模型图或权重。</summary>
        Model = 0,
        /// <summary>Additional model weights. / 附加模型权重。</summary>
        Weights = 1,
        /// <summary>External tensor data referenced by a graph. / 模型图引用的外部张量数据。</summary>
        ExternalData = 2,
        /// <summary>Class or token labels. / 类别或 token 标签。</summary>
        Labels = 3,
        /// <summary>Vocabulary data. / 词表数据。</summary>
        Vocabulary = 4,
        /// <summary>Tokenizer data. / Tokenizer 数据。</summary>
        Tokenizer = 5,
        /// <summary>Chat template data. / 聊天模板数据。</summary>
        ChatTemplate = 6,
        /// <summary>Backend or model configuration. / 后端或模型配置。</summary>
        Configuration = 7,
        /// <summary>License or attribution text. / 许可证或归属文本。</summary>
        License = 8,
        /// <summary>Test or example input. / 测试或示例输入。</summary>
        TestInput = 9,
        /// <summary>Another declared data role. / 其他已声明的数据角色。</summary>
        Other = 10
    }

    /// <summary>Represents an unvalidated model-package manifest. / 表示尚未验证的模型包清单。</summary>
    public sealed class ModelPackageDocument
    {
        private readonly IReadOnlyList<ModelTensorSignatureDocument> _inputs;
        private readonly IReadOnlyList<ModelTensorSignatureDocument> _outputs;
        private readonly IReadOnlyList<ModelArtifactDocument> _artifacts;
        private readonly IReadOnlyDictionary<string, string> _extensions;

        /// <summary>Initializes an unvalidated model-package document. / 初始化尚未验证的模型包文档。</summary>
        public ModelPackageDocument(
            string? schemaVersion,
            string? modelId,
            string? name,
            string? family,
            string? task,
            string? modelVersion,
            ModelExporterDocument? exporter,
            ModelSourceDocument? source,
            DateTimeOffset? generatedAt,
            string? profileId,
            IEnumerable<ModelTensorSignatureDocument>? inputs,
            IEnumerable<ModelTensorSignatureDocument>? outputs,
            IEnumerable<ModelArtifactDocument>? artifacts,
            IEnumerable<KeyValuePair<string, string>>? extensions = null)
        {
            SchemaVersion = schemaVersion;
            ModelId = modelId;
            Name = name;
            Family = family;
            Task = task;
            ModelVersion = modelVersion;
            Exporter = exporter;
            Source = source;
            GeneratedAt = generatedAt;
            ProfileId = profileId;
            _inputs = DocumentCopies.Copy(inputs);
            _outputs = DocumentCopies.Copy(outputs);
            _artifacts = DocumentCopies.Copy(artifacts);
            _extensions = DocumentCopies.CopyDictionary(extensions);
        }

        /// <summary>Gets the schema version text. / 获取 schema 版本文本。</summary>
        public string? SchemaVersion { get; }
        /// <summary>Gets the stable model identifier text. / 获取稳定模型标识文本。</summary>
        public string? ModelId { get; }
        /// <summary>Gets the display name. / 获取显示名称。</summary>
        public string? Name { get; }
        /// <summary>Gets the model family. / 获取模型族。</summary>
        public string? Family { get; }
        /// <summary>Gets the task identifier. / 获取任务标识。</summary>
        public string? Task { get; }
        /// <summary>Gets the model version. / 获取模型版本。</summary>
        public string? ModelVersion { get; }
        /// <summary>Gets exporter metadata. / 获取导出器元数据。</summary>
        public ModelExporterDocument? Exporter { get; }
        /// <summary>Gets source and license metadata. / 获取来源和许可证元数据。</summary>
        public ModelSourceDocument? Source { get; }
        /// <summary>Gets the optional generation timestamp. / 获取可选生成时间。</summary>
        public DateTimeOffset? GeneratedAt { get; }
        /// <summary>Gets the DeploySharp processing profile identifier. / 获取 DeploySharp 处理 profile 标识。</summary>
        public string? ProfileId { get; }
        /// <summary>Gets input tensor signatures. / 获取输入张量签名。</summary>
        public IReadOnlyList<ModelTensorSignatureDocument> Inputs => _inputs;
        /// <summary>Gets output tensor signatures. / 获取输出张量签名。</summary>
        public IReadOnlyList<ModelTensorSignatureDocument> Outputs => _outputs;
        /// <summary>Gets model artifacts. / 获取模型工件。</summary>
        public IReadOnlyList<ModelArtifactDocument> Artifacts => _artifacts;
        /// <summary>Gets non-critical string extension metadata. / 获取非关键字符串扩展元数据。</summary>
        public IReadOnlyDictionary<string, string> Extensions => _extensions;
    }

    /// <summary>Describes the tool that exported a model artifact. / 描述导出模型工件的工具。</summary>
    public sealed class ModelExporterDocument
    {
        /// <summary>Initializes exporter metadata. / 初始化导出器元数据。</summary>
        public ModelExporterDocument(string? name, string? version, string? sourceRevision = null)
        {
            Name = name;
            Version = version;
            SourceRevision = sourceRevision;
        }

        /// <summary>Gets exporter name. / 获取导出器名称。</summary>
        public string? Name { get; }
        /// <summary>Gets exporter version. / 获取导出器版本。</summary>
        public string? Version { get; }
        /// <summary>Gets source revision used for export. / 获取导出时使用的源码修订。</summary>
        public string? SourceRevision { get; }
    }

    /// <summary>Describes model provenance, license, and redistribution permission. / 描述模型来源、许可证和再分发许可。</summary>
    public sealed class ModelSourceDocument
    {
        /// <summary>Initializes source metadata. / 初始化来源元数据。</summary>
        public ModelSourceDocument(
            string? sourceUrl,
            string? projectUrl,
            string? revision,
            string? author,
            string? copyright,
            string? licenseExpression,
            string? licenseFile,
            bool redistributionAllowed)
        {
            SourceUrl = sourceUrl;
            ProjectUrl = projectUrl;
            Revision = revision;
            Author = author;
            Copyright = copyright;
            LicenseExpression = licenseExpression;
            LicenseFile = licenseFile;
            RedistributionAllowed = redistributionAllowed;
        }

        /// <summary>Gets the direct upstream source URL. / 获取直接上游来源 URL。</summary>
        public string? SourceUrl { get; }
        /// <summary>Gets the upstream project URL. / 获取上游项目 URL。</summary>
        public string? ProjectUrl { get; }
        /// <summary>Gets the upstream revision. / 获取上游修订。</summary>
        public string? Revision { get; }
        /// <summary>Gets author or organization attribution. / 获取作者或组织归属。</summary>
        public string? Author { get; }
        /// <summary>Gets copyright text. / 获取版权文本。</summary>
        public string? Copyright { get; }
        /// <summary>Gets an SPDX license expression. / 获取 SPDX 许可证表达式。</summary>
        public string? LicenseExpression { get; }
        /// <summary>Gets a relative license file path. / 获取相对许可证文件路径。</summary>
        public string? LicenseFile { get; }
        /// <summary>Gets whether redistribution is explicitly allowed. / 获取是否明确允许再分发。</summary>
        public bool RedistributionAllowed { get; }
    }

    /// <summary>Describes one tensor signature without binding to a backend tensor type. / 描述一个不绑定后端张量类型的张量签名。</summary>
    public sealed class ModelTensorSignatureDocument
    {
        private readonly IReadOnlyList<long> _shape;

        /// <summary>Initializes a tensor signature. / 初始化张量签名。</summary>
        public ModelTensorSignatureDocument(string? name, string? elementType, IEnumerable<long>? shape)
        {
            Name = name;
            ElementType = elementType;
            _shape = DocumentCopies.CopyValues(shape);
        }

        /// <summary>Gets tensor name. / 获取张量名称。</summary>
        public string? Name { get; }
        /// <summary>Gets normalized element-type text. / 获取规范化元素类型文本。</summary>
        public string? ElementType { get; }
        /// <summary>Gets dimensions, where -1 represents a dynamic dimension. / 获取维度，其中 -1 表示动态维度。</summary>
        public IReadOnlyList<long> Shape => _shape;
    }

    /// <summary>Describes one backend-consumable model artifact. / 描述一个可由后端使用的模型工件。</summary>
    public sealed class ModelArtifactDocument
    {
        private readonly IReadOnlyList<string> _compatibleBackends;
        private readonly IReadOnlyList<ModelFileDocument> _files;
        private readonly IReadOnlyDictionary<string, string> _extensions;

        /// <summary>Initializes model-artifact metadata. / 初始化模型工件元数据。</summary>
        public ModelArtifactDocument(
            string? artifactId,
            string? format,
            ModelArtifactLocationKind locationKind,
            string? entrypoint,
            IEnumerable<string>? compatibleBackends,
            IEnumerable<ModelFileDocument>? files,
            string? precision = null,
            string? quantization = null,
            int? opset = null,
            bool portable = true,
            string? minimumBackendVersion = null,
            string? minimumRuntimeVersion = null,
            IEnumerable<KeyValuePair<string, string>>? extensions = null)
        {
            ArtifactId = artifactId;
            Format = format;
            LocationKind = locationKind;
            Entrypoint = entrypoint;
            _compatibleBackends = DocumentCopies.CopyValues(compatibleBackends);
            _files = DocumentCopies.Copy(files);
            Precision = precision;
            Quantization = quantization;
            Opset = opset;
            Portable = portable;
            MinimumBackendVersion = minimumBackendVersion;
            MinimumRuntimeVersion = minimumRuntimeVersion;
            _extensions = DocumentCopies.CopyDictionary(extensions);
        }

        /// <summary>Gets the stable artifact identifier. / 获取稳定工件标识。</summary>
        public string? ArtifactId { get; }
        /// <summary>Gets normalized model format text. / 获取规范化模型格式文本。</summary>
        public string? Format { get; }
        /// <summary>Gets file or directory location semantics. / 获取文件或目录位置语义。</summary>
        public ModelArtifactLocationKind LocationKind { get; }
        /// <summary>Gets the relative entrypoint path. / 获取相对入口点路径。</summary>
        public string? Entrypoint { get; }
        /// <summary>Gets compatible backend identifiers. / 获取兼容后端标识。</summary>
        public IReadOnlyList<string> CompatibleBackends => _compatibleBackends;
        /// <summary>Gets all files required by this artifact. / 获取该工件所需的全部文件。</summary>
        public IReadOnlyList<ModelFileDocument> Files => _files;
        /// <summary>Gets precision text. / 获取精度文本。</summary>
        public string? Precision { get; }
        /// <summary>Gets quantization text. / 获取量化文本。</summary>
        public string? Quantization { get; }
        /// <summary>Gets an optional ONNX opset. / 获取可选 ONNX opset。</summary>
        public int? Opset { get; }
        /// <summary>Gets whether this artifact is portable across compatible devices. / 获取该工件是否可在兼容设备间复用。</summary>
        public bool Portable { get; }
        /// <summary>Gets the minimum managed backend version. / 获取最低托管后端版本。</summary>
        public string? MinimumBackendVersion { get; }
        /// <summary>Gets the minimum native runtime version. / 获取最低原生运行时版本。</summary>
        public string? MinimumRuntimeVersion { get; }
        /// <summary>Gets non-critical artifact extension metadata. / 获取非关键工件扩展元数据。</summary>
        public IReadOnlyDictionary<string, string> Extensions => _extensions;
    }

    /// <summary>Describes one integrity-protected file in an artifact. / 描述工件中一个受完整性保护的文件。</summary>
    public sealed class ModelFileDocument
    {
        /// <summary>Initializes model-file metadata. / 初始化模型文件元数据。</summary>
        public ModelFileDocument(string? relativePath, string? sha256, long size, string? mediaType, ModelFileRole role)
        {
            RelativePath = relativePath;
            Sha256 = sha256;
            Size = size;
            MediaType = mediaType;
            Role = role;
        }

        /// <summary>Gets the package-relative path. / 获取包内相对路径。</summary>
        public string? RelativePath { get; }
        /// <summary>Gets lowercase or uppercase SHA256 text. / 获取大写或小写 SHA256 文本。</summary>
        public string? Sha256 { get; }
        /// <summary>Gets expected byte size. / 获取预期字节大小。</summary>
        public long Size { get; }
        /// <summary>Gets optional media type. / 获取可选媒体类型。</summary>
        public string? MediaType { get; }
        /// <summary>Gets the declared file role. / 获取声明的文件角色。</summary>
        public ModelFileRole Role { get; }
    }

    internal static class DocumentCopies
    {
        public static IReadOnlyList<T> Copy<T>(IEnumerable<T>? source) where T : class
        {
            var values = new List<T>();
            if (source != null)
            {
                foreach (T value in source)
                {
                    if (value == null) throw new ArgumentException("Document collections cannot contain null values.", nameof(source));
                    values.Add(value);
                }
            }

            return values.AsReadOnly();
        }

        public static IReadOnlyList<T> CopyValues<T>(IEnumerable<T>? source)
        {
            return new List<T>(source ?? new T[0]).AsReadOnly();
        }

        public static IReadOnlyDictionary<string, string> CopyDictionary(IEnumerable<KeyValuePair<string, string>>? source)
        {
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (KeyValuePair<string, string> value in source)
                {
                    if (value.Key == null || value.Value == null) throw new ArgumentException("Extension keys and values cannot be null.", nameof(source));
                    values.Add(value.Key, value.Value);
                }
            }

            return new ReadOnlyDictionary<string, string>(values);
        }
    }
}
