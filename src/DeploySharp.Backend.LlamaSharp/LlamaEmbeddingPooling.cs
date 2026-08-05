namespace JYPPX.DeploySharp.Backends.LlamaSharp
{
    /// <summary>Specifies how token embeddings are pooled without exposing LLamaSharp enums. / 指定 token 嵌入的池化方式，且不暴露 LLamaSharp 枚举。</summary>
    public enum LlamaEmbeddingPooling
    {
        /// <summary>Use model metadata or backend default. / 使用模型元数据或后端默认值。</summary>
        ModelDefault = 0,
        /// <summary>Average token embeddings. / 对 token 嵌入取平均值。</summary>
        Mean = 1,
        /// <summary>Use the classification token embedding. / 使用分类 token 的嵌入。</summary>
        ClassificationToken = 2,
        /// <summary>Use the final token embedding. / 使用最后一个 token 的嵌入。</summary>
        LastToken = 3
    }
}
