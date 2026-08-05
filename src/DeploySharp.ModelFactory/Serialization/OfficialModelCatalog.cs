using System;
using System.IO;
using System.Reflection;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Provides the audited catalog snapshot bundled with this package. / 提供随包附带的已审计目录快照。</summary>
    public static class OfficialModelCatalog
    {
        private const string ResourceName = "JYPPX.DeploySharp.ModelFactory.Catalog.deploysharp-official-catalog.json";

        /// <summary>Reads and validates the bundled official catalog. / 读取并验证内置官方目录。</summary>
        public static ValidatedModelCatalog Load()
        {
            using (Stream stream = typeof(OfficialModelCatalog).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException("The embedded official model catalog is missing."))
            {
                return ModelCatalogJsonSerializer.Deserialize(stream);
            }
        }

        /// <summary>Reads the bundled official catalog JSON text. / 读取内置官方目录 JSON 文本。</summary>
        public static string GetJson()
        {
            using (Stream stream = typeof(OfficialModelCatalog).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException("The embedded official model catalog is missing."))
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
        }
    }
}
