namespace JYPPX.DeploySharp.LLM.Prompt
{
    /// <summary>Converts structured chat history into a backend prompt. / 将结构化聊天历史转换为后端提示词。</summary>
    public interface IPromptFormatter
    {
        /// <summary>Formats a chat history. / 格式化聊天历史。</summary>
        public string Format(ChatHistory history);
    }
}
