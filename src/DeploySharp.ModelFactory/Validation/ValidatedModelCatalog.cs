using System;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Represents a catalog that passed all schema, provenance, and admission checks. / 表示已通过 Schema、来源和准入检查的目录。</summary>
    public sealed class ValidatedModelCatalog
    {
        internal ValidatedModelCatalog(ModelCatalogDocument document, Version schemaVersion)
        {
            Document = document;
            SchemaVersion = schemaVersion;
        }

        /// <summary>Gets the immutable validated document. / 获取不可变已验证文档。</summary>
        public ModelCatalogDocument Document { get; }
        /// <summary>Gets the parsed catalog schema version. / 获取解析后的目录 Schema 版本。</summary>
        public Version SchemaVersion { get; }
        /// <summary>Gets the catalog revision. / 获取目录修订。</summary>
        public string CatalogRevision => Document.CatalogRevision!;
    }
}
