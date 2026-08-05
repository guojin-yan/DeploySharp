using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Owns an ordered, appendable sequence of chat messages. / 持有有序且可追加的聊天消息序列。</summary>
    public sealed class ChatHistory
    {
        private readonly List<ChatMessage> _messages;

        /// <summary>Initializes an empty or copied chat history. / 初始化空聊天历史或复制已有历史。</summary>
        public ChatHistory(IEnumerable<ChatMessage>? messages = null)
        {
            _messages = new List<ChatMessage>();
            if (messages == null)
            {
                return;
            }

            foreach (ChatMessage message in messages)
            {
                Add(message);
            }
        }

        /// <summary>Gets a read-only snapshot of messages. / 获取消息的只读快照。</summary>
        public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

        /// <summary>Appends a message and returns this history. / 追加消息并返回当前历史对象。</summary>
        public ChatHistory Add(ChatMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            _messages.Add(message);
            return this;
        }

        /// <summary>Gets the last system instruction, when present. / 获取最后一条系统指令（如果存在）。</summary>
        public string? GetSystemPrompt()
        {
            for (int index = _messages.Count - 1; index >= 0; index--)
            {
                if (_messages[index].Role == ChatRole.System)
                {
                    return _messages[index].Content;
                }
            }

            return null;
        }
    }
}
