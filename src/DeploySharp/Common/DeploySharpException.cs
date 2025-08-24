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
    /// DeploySharp项目自定义异常基类
    /// 提供标准化的错误代码、技术细节记录和自动日志功能
    /// </summary>
    public class DeploySharpException : Exception
    {

        /// <summary>
        /// 错误代码（遵循项目规范如DEPLOY_001格式）
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// 技术细节（用于开发人员调试的详细信息）
        /// </summary>
        public string TechnicalDetails { get; }

        /// <summary>
        /// 基础构造函数
        /// </summary>
        /// <param name="message">用户友好的错误信息</param>
        public DeploySharpException(string message)
            : base(message)
        {
            MyLogger.Log.Error($"DeploySharp异常: {Message}");
        }

        /// <summary>
        /// 包含内部异常的构造函数
        /// </summary>
        /// <param name="message">用户友好的错误信息</param>
        /// <param name="innerException">引发当前异常的底层异常</param>
        public DeploySharpException(string message, Exception innerException)
            : base(message, innerException)
        {
            MyLogger.Log.Error($"DeploySharp异常: {Message}", innerException);
        }

        /// <summary>
        /// 完整参数构造函数
        /// </summary>
        /// <param name="errorCode">标准错误代码</param>
        /// <param name="message">用户友好的错误信息</param>
        /// <param name="technicalDetails">技术调试信息（可选）</param>
        public DeploySharpException(string errorCode, string message, string technicalDetails = null)
            : base(message)
        {
            ErrorCode = errorCode;
            TechnicalDetails = technicalDetails;
            MyLogger.Log.Error($"DeploySharp业务异常 [{ErrorCode}]: {Message}");
        }

        /// <summary>
        /// 完整参数构造函数（包含内部异常）
        /// </summary>
        /// <param name="errorCode">标准错误代码</param>
        /// <param name="message">用户友好的错误信息</param>
        /// <param name="innerException">引发当前异常的底层异常</param>
        /// <param name="technicalDetails">技术调试信息（可选）</param>
        public DeploySharpException(string errorCode, string message,
            Exception innerException, string technicalDetails = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            TechnicalDetails = technicalDetails;
            MyLogger.Log.Error($"DeploySharp系统异常 [{ErrorCode}]: {Message}", innerException);
        }
    }
}

