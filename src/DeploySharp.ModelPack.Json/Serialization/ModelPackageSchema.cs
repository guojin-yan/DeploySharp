using System;
using System.IO;
using System.Reflection;

namespace JYPPX.DeploySharp.ModelPack.Json.Serialization
{
    /// <summary>
    /// Provides the canonical JSON Schema bundled with this package.
    /// 提供随本包附带的规范 JSON Schema。
    /// </summary>
    public static class ModelPackageSchema
    {
        private const string ResourceName = "JYPPX.DeploySharp.ModelPack.Json.Schemas.deploysharp-model-package-2.0.schema.json";

        /// <summary>
        /// Gets the schema version represented by the bundled schema.
        /// 获取内置 Schema 所表示的版本。
        /// </summary>
        public static string Version => "2.0";

        /// <summary>
        /// Opens a readable stream for the bundled schema. The caller owns the returned stream.
        /// 打开内置 Schema 的可读流；调用方负责释放返回的流。
        /// </summary>
        /// <returns>The readable schema stream. / 可读的 Schema 流。</returns>
        public static Stream OpenStream()
        {
            Stream? stream = typeof(ModelPackageSchema).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null) throw new InvalidOperationException("The embedded DeploySharp model package schema is missing.");
            return stream;
        }

        /// <summary>
        /// Reads the bundled schema as UTF-8 text.
        /// 将内置 Schema 读取为 UTF-8 文本。
        /// </summary>
        /// <returns>The schema JSON text. / Schema JSON 文本。</returns>
        public static string GetJson()
        {
            using (Stream stream = OpenStream())
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
        }
    }
}
