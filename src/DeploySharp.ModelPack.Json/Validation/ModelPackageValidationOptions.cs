using System;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Defines resource and compatibility limits for JSON and manifest validation. / 定义 JSON 与清单验证的资源和兼容限制。</summary>
    public sealed class ModelPackageValidationOptions
    {
        /// <summary>Initializes validation limits. / 初始化验证限制。</summary>
        public ModelPackageValidationOptions(
            long maximumJsonBytes = 4 * 1024 * 1024,
            int maximumJsonDepth = 64,
            int maximumStringLength = 4096,
            int maximumArtifacts = 128,
            int maximumFiles = 4096,
            int maximumTensors = 512,
            int maximumExtensions = 256,
            int supportedSchemaMajor = 2,
            int supportedSchemaMinor = 0,
            bool allowNewerMinorVersions = true)
        {
            if (maximumJsonBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumJsonBytes));
            if (maximumJsonDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumJsonDepth));
            if (maximumStringLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumStringLength));
            if (maximumArtifacts <= 0) throw new ArgumentOutOfRangeException(nameof(maximumArtifacts));
            if (maximumFiles <= 0) throw new ArgumentOutOfRangeException(nameof(maximumFiles));
            if (maximumTensors <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTensors));
            if (maximumExtensions < 0) throw new ArgumentOutOfRangeException(nameof(maximumExtensions));
            if (supportedSchemaMajor <= 0) throw new ArgumentOutOfRangeException(nameof(supportedSchemaMajor));
            if (supportedSchemaMinor < 0) throw new ArgumentOutOfRangeException(nameof(supportedSchemaMinor));
            MaximumJsonBytes = maximumJsonBytes;
            MaximumJsonDepth = maximumJsonDepth;
            MaximumStringLength = maximumStringLength;
            MaximumArtifacts = maximumArtifacts;
            MaximumFiles = maximumFiles;
            MaximumTensors = maximumTensors;
            MaximumExtensions = maximumExtensions;
            SupportedSchemaMajor = supportedSchemaMajor;
            SupportedSchemaMinor = supportedSchemaMinor;
            AllowNewerMinorVersions = allowNewerMinorVersions;
        }

        /// <summary>Gets maximum UTF-8 JSON bytes. / 获取 UTF-8 JSON 最大字节数。</summary>
        public long MaximumJsonBytes { get; }
        /// <summary>Gets maximum JSON nesting depth. / 获取 JSON 最大嵌套深度。</summary>
        public int MaximumJsonDepth { get; }
        /// <summary>Gets maximum length of one string value. / 获取单个字符串值最大长度。</summary>
        public int MaximumStringLength { get; }
        /// <summary>Gets maximum artifact count. / 获取最大工件数量。</summary>
        public int MaximumArtifacts { get; }
        /// <summary>Gets maximum total file count. / 获取最大文件总数。</summary>
        public int MaximumFiles { get; }
        /// <summary>Gets maximum total tensor-signature count. / 获取最大张量签名总数。</summary>
        public int MaximumTensors { get; }
        /// <summary>Gets maximum extension count per object. / 获取每个对象的最大扩展数量。</summary>
        public int MaximumExtensions { get; }
        /// <summary>Gets supported schema major version. / 获取支持的 schema 主版本。</summary>
        public int SupportedSchemaMajor { get; }
        /// <summary>Gets implemented schema minor version. / 获取已实现的 schema 次版本。</summary>
        public int SupportedSchemaMinor { get; }
        /// <summary>Gets whether newer minor versions are accepted when no unknown critical property exists. / 获取在不存在未知关键属性时是否接受更新次版本。</summary>
        public bool AllowNewerMinorVersions { get; }

        /// <summary>Gets default safe limits. / 获取默认安全限制。</summary>
        public static ModelPackageValidationOptions Default { get; } = new ModelPackageValidationOptions();
    }
}
