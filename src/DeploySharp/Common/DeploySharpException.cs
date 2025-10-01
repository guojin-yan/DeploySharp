using DeploySharp.Log;
using log4net.Core;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Common
{

    /// <summary>
    /// The base custom exception class for DeploySharp project.
    /// Provides standardized error codes, technical details recording and automatic logging.
    /// DeploySharp项目自定义异常基类
    /// 提供标准化的错误代码、技术细节记录和自动日志功能
    /// </summary>
    /// <remarks>
    /// <para>
    /// All custom exceptions in the project should inherit from this class.
    /// 项目中所有自定义异常都应继承自此基类。
    /// </para>
    /// <para>
    /// Automatically logs errors using MyLogger when instantiated.
    /// 实例化时会自动通过MyLogger记录错误。
    /// </para>
    /// </remarks>
    public class DeploySharpException : Exception
    {
        /// <summary>
        /// Gets the error code following project conventions (e.g. DEPLOY_001 format).
        /// 获取遵循项目规范的错误代码(如DEPLOY_001格式)
        /// </summary>
        /// <value>
        /// Standard error code string.
        /// 标准错误代码字符串
        /// </value>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets technical details for developer debugging purposes.
        /// 获取用于开发人员调试的技术细节
        /// </summary>
        /// <value>
        /// Detailed technical information about the error.
        /// 错误的详细技术信息
        /// </value>
        public string TechnicalDetails { get; }

        /// <summary>
        /// Basic constructor with error message.
        /// 基础构造函数(仅错误信息)
        /// </summary>
        /// <param name="message">User-friendly error message.用户友好的错误信息</param>
        /// <remarks>
        /// Automatically logs the error at Error level.
        /// 自动以Error级别记录日志
        /// </remarks>
        public DeploySharpException(string message)
            : base(message)
        {
            MyLogger.Log.Error($"DeploySharp异常: {Message}");
        }

        /// <summary>
        /// Constructor with message and inner exception.
        /// 包含内部异常的构造函数
        /// </summary>
        /// <param name="message">User-friendly error message.用户友好的错误信息</param>
        /// <param name="innerException">The underlying exception that caused this exception.引发当前异常的底层异常</param>
        /// <remarks>
        /// Automatically logs both the message and inner exception at Error level.
        /// 自动记录错误信息和内部异常
        /// </remarks>
        public DeploySharpException(string message, Exception innerException)
            : base(message, innerException)
        {
            MyLogger.Log.Error($"DeploySharp异常: {Message}", innerException);
        }

        /// <summary>
        /// Full parameter constructor with error code and optional technical details.
        /// 完整参数构造函数(包含错误代码和技术细节)
        /// </summary>
        /// <param name="errorCode">Standard error code following project conventions.标准错误代码</param>
        /// <param name="message">User-friendly error message.用户友好的错误信息</param>
        /// <param name="technicalDetails">Technical debugging information (optional).技术调试信息(可选)</param>
        /// <remarks>
        /// Logs as a business exception with error code prefix.
        /// 作为业务异常记录，包含错误代码前缀
        /// </remarks>
        public DeploySharpException(string errorCode, string message, string technicalDetails = null)
            : base(message)
        {
            ErrorCode = errorCode;
            TechnicalDetails = technicalDetails;
            MyLogger.Log.Error($"DeploySharp业务异常 [{ErrorCode}]: {Message}");
        }

        /// <summary>
        /// Full parameter constructor with error code, inner exception and optional technical details.
        /// 完整参数构造函数(包含错误代码、内部异常和技术细节)
        /// </summary>
        /// <param name="errorCode">Standard error code following project conventions.标准错误代码</param>
        /// <param name="message">User-friendly error message.用户友好的错误信息</param>
        /// <param name="innerException">The underlying exception that caused this exception.引发当前异常的底层异常</param>
        /// <param name="technicalDetails">Technical debugging information (optional).技术调试信息(可选)</param>
        /// <remarks>
        /// Logs as a system exception with error code prefix and full exception details.
        /// 作为系统异常记录，包含错误代码前缀和完整异常详情
        /// </remarks>
        public DeploySharpException(string errorCode, string message,
            Exception innerException, string technicalDetails = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            TechnicalDetails = technicalDetails;
            MyLogger.Log.Error($"DeploySharp系统异常 [{ErrorCode}]: {Message}", innerException);
        }

        /// <summary>
        /// Overrides ToString() to include error code and technical details when available.
        /// 重写ToString()方法，包含错误代码和技术细节(如果存在)
        /// </summary>
        /// <returns>
        /// Formatted string containing all exception information.
        /// 包含所有异常信息的格式化字符串
        /// </returns>
        public override string ToString()
        {
            var result = new StringBuilder(base.ToString());

            if (!string.IsNullOrEmpty(ErrorCode))
            {
                result.AppendLine().Append($"Error Code: {ErrorCode}");
            }

            if (!string.IsNullOrEmpty(TechnicalDetails))
            {
                result.AppendLine().Append($"Technical Details: {TechnicalDetails}");
            }

            return result.ToString();
        }
    }

}

