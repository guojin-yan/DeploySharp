namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Identifies the author role of a chat message. / 标识聊天消息的作者角色。</summary>
    public enum ChatRole
    {
        /// <summary>System instruction. / 系统指令。</summary>
        System = 0,
        /// <summary>Human or application input. / 人类或应用程序输入。</summary>
        User = 1,
        /// <summary>Model-generated response. / 模型生成的响应。</summary>
        Assistant = 2,
        /// <summary>Tool or function result. / 工具或函数结果。</summary>
        Tool = 3
    }
}
