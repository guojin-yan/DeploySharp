using System;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Errors
{
    /// <summary>
    /// Base exception for DeploySharp failures. Constructing an exception never writes a log entry. / DeploySharp 故障的基础异常；构造异常时绝不写入日志。
    /// </summary>
    public class DeploySharpException : Exception
    {
        /// <summary>Initializes a DeploySharp exception. / 初始化 DeploySharp 异常。</summary>
        public DeploySharpException(
            string errorCode,
            string message,
            Exception? innerException = null,
            BackendId? backendId = null,
            ModelId? modelId = null,
            string? technicalDetails = null)
            : base(message, innerException)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode)
                ? throw new ArgumentException("An error code is required.", nameof(errorCode))
                : errorCode;
            BackendId = backendId;
            ModelId = modelId;
            TechnicalDetails = technicalDetails;
        }

        /// <summary>Gets the stable DeploySharp error code. / 获取稳定的 DeploySharp 错误码。</summary>
        public string ErrorCode { get; }

        /// <summary>Gets the associated backend when known. / 获取已知的关联后端。</summary>
        public BackendId? BackendId { get; }

        /// <summary>Gets the associated model when known. / 获取已知的关联模型。</summary>
        public ModelId? ModelId { get; }

        /// <summary>Gets optional technical details not intended as the primary user message. / 获取不作为主要用户消息的可选技术细节。</summary>
        public string? TechnicalDetails { get; }
    }
}
