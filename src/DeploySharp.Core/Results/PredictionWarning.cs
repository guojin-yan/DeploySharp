using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp.Results
{
    /// <summary>
    /// Describes a non-fatal condition associated with an otherwise successful prediction. / 描述与其他方面成功的预测关联的非致命状况。
    /// </summary>
    public sealed class PredictionWarning
    {
        /// <summary>Initializes a warning. / 初始化警告。</summary>
        public PredictionWarning(string code, string message)
        {
            Code = Guard.Identifier(code, nameof(code));
            Message = Guard.NotNullOrWhiteSpace(message, nameof(message));
        }

        /// <summary>Gets the stable warning code. / 获取稳定的警告代码。</summary>
        public string Code { get; }

        /// <summary>Gets the human-readable warning message. / 获取可读的警告消息。</summary>
        public string Message { get; }
    }
}
