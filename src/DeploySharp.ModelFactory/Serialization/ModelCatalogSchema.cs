using System;
using System.IO;
using System.Reflection;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Provides the canonical catalog JSON Schema bundled with the package. / 提供随包附带的规范目录 JSON Schema。</summary>
    public static class ModelCatalogSchema
    {
        private const string ResourceName = "JYPPX.DeploySharp.ModelFactory.Schemas.deploysharp-model-catalog-1.0.schema.json";

        /// <summary>Gets the bundled schema version. / 获取内置 Schema 版本。</summary>
        public static string Version => "1.0";

        /// <summary>Opens a readable schema stream owned by the caller. / 打开由调用方负责释放的可读 Schema 流。</summary>
        public static Stream OpenStream()
        {
            Stream? stream = typeof(ModelCatalogSchema).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null) throw new InvalidOperationException("The embedded DeploySharp model catalog schema is missing.");
            return stream;
        }

        /// <summary>Reads the bundled schema as UTF-8 text. / 将内置 Schema 读取为 UTF-8 文本。</summary>
        public static string GetJson()
        {
            using (Stream stream = OpenStream())
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
        }
    }
}
