using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Defines bounded catalog validation limits. / 定义有界目录验证限制。</summary>
    public sealed class ModelCatalogValidationOptions
    {
        private readonly IReadOnlyList<string> _admittedFormats;
        private readonly IReadOnlyList<string> _admittedBackends;
        /// <summary>Initializes catalog validation limits. / 初始化目录验证限制。</summary>
        public ModelCatalogValidationOptions(long maximumJsonBytes = 4 * 1024 * 1024, int maximumJsonDepth = 64, int maximumStringLength = 4096, int maximumEntries = 1024, int maximumArtifactsPerEntry = 64, int maximumAssetsPerArtifact = 4096, int supportedSchemaMajor = 1, int supportedSchemaMinor = 0, bool allowNewerMinorVersions = true, bool allowPreviewAndExternal = true, IEnumerable<string>? admittedFormats = null, IEnumerable<string>? admittedBackends = null)
        {
            if (maximumJsonBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumJsonBytes));
            if (maximumJsonDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumJsonDepth));
            if (maximumStringLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumStringLength));
            if (maximumEntries <= 0) throw new ArgumentOutOfRangeException(nameof(maximumEntries));
            if (maximumArtifactsPerEntry <= 0) throw new ArgumentOutOfRangeException(nameof(maximumArtifactsPerEntry));
            if (maximumAssetsPerArtifact <= 0) throw new ArgumentOutOfRangeException(nameof(maximumAssetsPerArtifact));
            if (supportedSchemaMajor <= 0) throw new ArgumentOutOfRangeException(nameof(supportedSchemaMajor));
            if (supportedSchemaMinor < 0) throw new ArgumentOutOfRangeException(nameof(supportedSchemaMinor));
            MaximumJsonBytes = maximumJsonBytes;
            MaximumJsonDepth = maximumJsonDepth;
            MaximumStringLength = maximumStringLength;
            MaximumEntries = maximumEntries;
            MaximumArtifactsPerEntry = maximumArtifactsPerEntry;
            MaximumAssetsPerArtifact = maximumAssetsPerArtifact;
            SupportedSchemaMajor = supportedSchemaMajor;
            SupportedSchemaMinor = supportedSchemaMinor;
            AllowNewerMinorVersions = allowNewerMinorVersions;
            AllowPreviewAndExternal = allowPreviewAndExternal;
            _admittedFormats = CopyIdentifiers(admittedFormats ?? new[] { "gguf", "onnx", "openvino-ir" }, nameof(admittedFormats));
            _admittedBackends = CopyIdentifiers(admittedBackends ?? new[] { "llama-sharp", "onnxruntime", "openvino" }, nameof(admittedBackends));
        }

        /// <summary>Gets the maximum UTF-8 catalog size. / 获取目录 UTF-8 最大大小。</summary>
        public long MaximumJsonBytes { get; }
        /// <summary>Gets the maximum JSON nesting depth. / 获取 JSON 最大嵌套深度。</summary>
        public int MaximumJsonDepth { get; }
        /// <summary>Gets the maximum string length. / 获取最大字符串长度。</summary>
        public int MaximumStringLength { get; }
        /// <summary>Gets the maximum entry count. / 获取最大条目数。</summary>
        public int MaximumEntries { get; }
        /// <summary>Gets the maximum artifacts per entry. / 获取每个条目的最大工件数。</summary>
        public int MaximumArtifactsPerEntry { get; }
        /// <summary>Gets the maximum assets per artifact. / 获取每个工件的最大资产数。</summary>
        public int MaximumAssetsPerArtifact { get; }
        /// <summary>Gets the supported schema major version. / 获取支持的 Schema 主版本。</summary>
        public int SupportedSchemaMajor { get; }
        /// <summary>Gets the supported schema minor version. / 获取支持的 Schema 次版本。</summary>
        public int SupportedSchemaMinor { get; }
        /// <summary>Gets whether newer minor versions are accepted. / 获取是否接受更新的次版本。</summary>
        public bool AllowNewerMinorVersions { get; }
        /// <summary>Gets whether Preview and External entries may appear in a validated catalog. / 获取已验证目录是否允许 Preview 和 External 条目。</summary>
        public bool AllowPreviewAndExternal { get; }
        /// <summary>Gets formats with current DeploySharp deployment evidence. / 获取当前具有 DeploySharp 部署证据的格式。</summary>
        public IReadOnlyList<string> AdmittedFormats => _admittedFormats;
        /// <summary>Gets backends with current DeploySharp deployment evidence. / 获取当前具有 DeploySharp 部署证据的后端。</summary>
        public IReadOnlyList<string> AdmittedBackends => _admittedBackends;
        /// <summary>Gets secure defaults. / 获取安全默认值。</summary>
        public static ModelCatalogValidationOptions Default { get; } = new ModelCatalogValidationOptions();

        private static IReadOnlyList<string> CopyIdentifiers(IEnumerable<string> values, string parameterName)
        {
            var result = new List<string>();
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Admission identifiers cannot be empty.", parameterName);
                result.Add(value.ToLowerInvariant());
            }

            return result.AsReadOnly();
        }
    }
}
