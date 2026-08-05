using System;
using System.Text;

namespace JYPPX.DeploySharp.LLM.Prompt
{
    /// <summary>Formats messages using stable role labels and newline separators. / 使用稳定角色标签和换行符格式化消息。</summary>
    public sealed class PlainTextPromptFormatter : IPromptFormatter
    {
        /// <summary>Formats chat messages as plain text. / 将聊天消息格式化为纯文本。</summary>
        public string Format(ChatHistory history)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));
            var builder = new StringBuilder();
            for (int index = 0; index < history.Messages.Count; index++)
            {
                ChatMessage message = history.Messages[index];
                if (index > 0) builder.Append('\n');
                builder.Append(RoleLabel(message.Role)).Append(": ").Append(message.Content);
            }

            builder.Append("\nAssistant:");
            return builder.ToString();
        }

        private static string RoleLabel(ChatRole role)
        {
            switch (role)
            {
                case ChatRole.System: return "System";
                case ChatRole.User: return "User";
                case ChatRole.Assistant: return "Assistant";
                case ChatRole.Tool: return "Tool";
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }
    }
}
