namespace JYPPX.DeploySharp.Results.Language
{
    /// <summary>
    /// Describes why text generation stopped. / 描述文本生成停止的原因。
    /// </summary>
    public enum GenerationFinishReason
    {
        /// <summary>The reason is not known or generation has not finished. / 原因未知或生成尚未完成。</summary>
        None = 0,
        /// <summary>The model emitted an end-of-sequence token. / 模型输出了序列结束令牌。</summary>
        EndOfSequence,
        /// <summary>A configured stop sequence was reached. / 已达到配置的停止序列。</summary>
        StopSequence,
        /// <summary>The maximum output length was reached. / 已达到最大输出长度。</summary>
        MaxTokens,
        /// <summary>The caller cancelled generation. / 调用方取消了生成。</summary>
        Cancelled,
        /// <summary>The backend reported an error after partial output. / 后端在部分输出后报告错误。</summary>
        Error
    }
}
