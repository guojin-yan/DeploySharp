using System;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Represents one immutable chat message. / 表示一条不可变聊天消息。</summary>
    public sealed class ChatMessage
    {
        /// <summary>Initializes a chat message. / 初始化聊天消息。</summary>
        public ChatMessage(ChatRole role, string content, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Message content cannot be empty.", nameof(content));
            }

            Role = role;
            Content = content;
            Name = string.IsNullOrWhiteSpace(name) ? null : name;
        }

        /// <summary>Gets the message role. / 获取消息角色。</summary>
        public ChatRole Role { get; }

        /// <summary>Gets the message content. / 获取消息内容。</summary>
        public string Content { get; }

        /// <summary>Gets an optional participant or tool name. / 获取可选的参与者或工具名称。</summary>
        public string? Name { get; }
    }
}
