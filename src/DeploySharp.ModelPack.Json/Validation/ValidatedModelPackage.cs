using System;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Represents a normalized manifest that passed all schema-level checks. / 表示已通过全部 schema 层检查的规范化清单。</summary>
    public sealed class ValidatedModelPackage
    {
        internal ValidatedModelPackage(ModelPackageDocument document, Version schemaVersion, ModelId modelId)
        {
            Document = document;
            SchemaVersion = schemaVersion;
            ModelId = modelId;
        }

        /// <summary>Gets the normalized immutable document. / 获取规范化的不可变文档。</summary>
        public ModelPackageDocument Document { get; }
        /// <summary>Gets the parsed schema version. / 获取解析后的 schema 版本。</summary>
        public Version SchemaVersion { get; }
        /// <summary>Gets the validated Core model identifier. / 获取已验证的 Core 模型标识。</summary>
        public ModelId ModelId { get; }
    }
}
