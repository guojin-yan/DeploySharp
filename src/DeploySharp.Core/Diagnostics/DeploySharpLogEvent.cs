using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp.Diagnostics
{
    /// <summary>
    /// Represents one immutable structured DeploySharp log event. / 表示一个不可变的 DeploySharp 结构化日志事件。
    /// </summary>
    public sealed class DeploySharpLogEvent
    {
        /// <summary>Initializes a structured log event. / 初始化结构化日志事件。</summary>
        public DeploySharpLogEvent(
            int eventId,
            DeploySharpLogLevel level,
            string category,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, object?>? properties = null,
            string? correlationId = null)
        {
            EventId = eventId;
            Level = level;
            Category = Guard.NotNullOrWhiteSpace(category, nameof(category));
            Message = Guard.NotNullOrWhiteSpace(message, nameof(message));
            Exception = exception;
            CorrelationId = correlationId;
            Timestamp = DateTimeOffset.UtcNow;

            var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (properties != null)
            {
                foreach (KeyValuePair<string, object?> pair in properties)
                {
                    copy.Add(pair.Key, pair.Value);
                }
            }

            Properties = new ReadOnlyDictionary<string, object?>(copy);
        }

        /// <summary>Gets the UTC event timestamp. / 获取事件的 UTC 时间戳。</summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>Gets the stable numeric event identifier. / 获取稳定的数字事件标识符。</summary>
        public int EventId { get; }

        /// <summary>Gets the event severity. / 获取事件严重级别。</summary>
        public DeploySharpLogLevel Level { get; }

        /// <summary>Gets the event category. / 获取事件类别。</summary>
        public string Category { get; }

        /// <summary>Gets the formatted human-readable message. / 获取格式化的可读消息。</summary>
        public string Message { get; }

        /// <summary>Gets the associated exception, when present. / 获取关联异常（如果有）。</summary>
        public Exception? Exception { get; }

        /// <summary>Gets immutable structured event properties. / 获取不可变的结构化事件属性。</summary>
        public IReadOnlyDictionary<string, object?> Properties { get; }

        /// <summary>Gets the optional operation correlation identifier. / 获取可选的操作关联标识符。</summary>
        public string? CorrelationId { get; }
    }
}
