namespace JYPPX.DeploySharp
{
    /// <summary>Provides the minimal identity for a serializable backend options schema. / 提供可序列化后端参数 schema 的最小标识。</summary>
    public interface IBackendOptionsSchema
    {
        /// <summary>Gets the stable schema identifier. / 获取稳定的 schema 标识。</summary>
        public string SchemaId { get; }
    }
}
