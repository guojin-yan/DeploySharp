using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net.Repository.Hierarchy;

namespace DeploySharp.Log
{

    /// <summary>
    /// Provides centralized logging management for the application using log4net
    /// 使用 log4net 提供应用程序的集中日志管理
    /// </summary>
    /// <remarks>
    /// <para>
    /// Thread-safe singleton implementation for logging configuration and management.
    /// Supports multiple output targets and log levels with flexible configuration.
    /// </para>
    /// <para>
    /// 线程安全的单例实现，用于日志配置和管理。
    /// 支持多种输出目标和日志级别，具有灵活的配置。
    /// </para>
    /// <example>
    /// Basic initialization:
    /// <code>
    /// LoggerManager.Initialize(LogLevel.DEBUG, LogOutput.All);
    /// LoggerManager.ProjectMainLogger.Info("Application started");
    /// </code>
    /// </example>
    /// </remarks>
    public static class LoggerManager
    {
        /// <summary>
        /// The main logger instance for the project
        /// 项目的主日志记录器实例
        /// </summary>
        /// <value>
        /// Configured log4net logger instance ready for use.
        /// Requires initialization before first use.
        /// 配置好的log4net日志记录器实例，可以直接使用。
        /// 首次使用前需要初始化。
        /// </value>
        public static readonly ILog ProjectMainLogger;

        /// <summary>
        /// Flag indicating whether logging system has been initialized
        /// 指示日志系统是否已初始化的标志
        /// </summary>
        private static bool _isInitialized = false;

        /// <summary>
        /// Lock object for thread-safe initialization
        /// 用于线程安全初始化的锁对象
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// Static constructor ensures logger is ready after class load
        /// 静态构造函数确保类加载后日志记录器就绪
        /// </summary>
        static LoggerManager()
        {
            ProjectMainLogger = LogManager.GetLogger(typeof(LoggerManager));
        }

        /// <summary>
        /// Initializes logger with default settings (DEBUG level, Console output)
        /// 使用默认设置(DEBUG级别，控制台输出)初始化日志记录器
        /// </summary>
        /// <remarks>
        /// Equivalent to calling Initialize(LogLevel.DEBUG, LogOutput.Console)
        /// 等同于调用Initialize(LogLevel.DEBUG, LogOutput.Console)
        /// </remarks>
        public static void InitializeDefault()
        {
            Initialize(LogLevel.DEBUG, LogOutput.All);
        }

        /// <summary>
        /// Initializes logging system with specified configuration
        /// 使用指定配置初始化日志系统
        /// </summary>
        /// <param name="level">Minimum log level to output 要输出的最低日志级别</param>
        /// <param name="output">Output targets (Console/File/All) 输出目标(控制台/文件/全部)</param>
        /// <param name="logPath">Directory path for log files (default: "DeploySharpLogs") 
        /// 日志文件的目录路径(默认："DeploySharpLogs")</param>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when lacking permission to create log directory
        /// 当缺少创建日志目录的权限时抛出
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when log file creation fails
        /// 当日志文件创建失败时抛出
        /// </exception>
        public static void Initialize(LogLevel level, LogOutput output, string logPath = "DeploySharpLogs")
        {
            lock (_lock)
            {
                if (_isInitialized) return;

                var hierarchy = (Hierarchy)LogManager.GetRepository();
                hierarchy.ResetConfiguration(); // Clear old configuration 清除旧配置
                hierarchy.Root.RemoveAllAppenders();
                hierarchy.Root.Level = Level.Off; // Disable root logger by default 默认禁用根记录器

                var defaultLogger = hierarchy.GetLogger("ProjectMainLogger") as Logger;

                // Configure console appender if Console output is enabled
                // 如果启用了控制台输出，则配置控制台附加器
                if (output.HasFlag(LogOutput.Console))
                {
                    var consoleAppender = new ConsoleAppender
                    {
                        Name = "ProjectMainLoggerAppender",
                        Layout = new CustomPatternLayout
                        {
                            ConversionPattern = "[%date] [%thread] [%-5level] (%filename:%line) - %message%newline%exception"
                        },
                    };

                    ((PatternLayout)consoleAppender.Layout).ActivateOptions();
                    consoleAppender.ActivateOptions();
                    defaultLogger.AddAppender(consoleAppender);
                }

                // Configure file appender if File output is enabled
                // 如果启用了文件输出，则配置文件附加器
                if (output.HasFlag(LogOutput.File))
                {
                    Directory.CreateDirectory(logPath);

                    var fileAppender = new RollingFileAppender
                    {
                        Name = "ProjectMainLoggerAppender",
                        File = Path.Combine(logPath, "DeploySharp.log"),
                        AppendToFile = true,
                        RollingStyle = RollingFileAppender.RollingMode.Composite,
                        MaxSizeRollBackups = 10,                 // Keep up to 10 archived logs 最多保留10个归档日志
                        MaximumFileSize = "10MB",                // Rotate after 10MB 10MB后轮转
                        DatePattern = "'_'yyyy-MM",              // Monthly archive naming 按月归档命名
                        LockingModel = new FileAppender.MinimalLock(),
                        Layout = new CustomPatternLayout
                        {
                            ConversionPattern = "[%date] [%thread] [%-5level] (%filename:%line) - %message%newline%exception"
                        },
                        PreserveLogFileNameExtension = true,
                        ImmediateFlush = true,                   // Immediate disk write for crash safety 立即写入磁盘以确保崩溃安全
                        StaticLogFileName = false
                    };
                    ((PatternLayout)fileAppender.Layout).ActivateOptions();
                    fileAppender.ActivateOptions();
                    defaultLogger.AddAppender(fileAppender);
                }

                defaultLogger.Level = ConvertLevel(level);
                defaultLogger.Additivity = false;                // Prevent duplicate logging 防止重复日志记录
                hierarchy.Configured = true;

                _isInitialized = true;
            }
        }

        /// <summary>
        /// Converts custom LogLevel to log4net.Core.Level
        /// 将自定义LogLevel转换为log4net.Core.Level
        /// </summary>
        /// <param name="level">Custom log level 自定义日志级别</param>
        /// <returns>Corresponding log4net level 对应的log4net级别</returns>
        public static log4net.Core.Level ConvertLevel(LogLevel level) => level switch
        {
            LogLevel.DEBUG => log4net.Core.Level.Debug,
            LogLevel.INFO => log4net.Core.Level.Info,
            LogLevel.WARN => log4net.Core.Level.Warn,
            LogLevel.ERROR => log4net.Core.Level.Error,
            LogLevel.FATAL => log4net.Core.Level.Fatal,
            _ => log4net.Core.Level.All
        };

        /// <summary>
        /// Checks if logging system is initialized
        /// 检查日志系统是否已初始化
        /// </summary>
        /// <returns>True if initialized 如果已初始化则为true</returns>
        public static bool IsInitialized()
        {
            lock (_lock)
            {
                return _isInitialized;
            }
        }
    }

    /// <summary>
    /// Enum defining available log output targets
    /// 定义可用日志输出目标的枚举
    /// </summary>
    /// <remarks>
    /// Can be combined using bitwise operations (Flags attribute)
    /// 可以使用位操作进行组合(Flags特性)
    /// </remarks>
    [Flags]
    public enum LogOutput
    {
        /// <summary>Output to console 输出到控制台</summary>
        Console = 1,
        /// <summary>Output to file 输出到文件</summary>
        File = 2,
        /// <summary>Output to both console and file 同时输出到控制台和文件</summary>
        All = Console | File
    }

    /// <summary>
    /// Enum defining available log levels
    /// 定义可用日志级别的枚举
    /// </summary>
    /// <remarks>
    /// Ordered from most verbose (DEBUG) to most severe (FATAL)
    /// 按从最详细(DEBUG)到最严重(FATAL)的顺序排列
    /// </remarks>
    public enum LogLevel
    {
        /// <summary>Debug level (most verbose) 调试级别(最详细)</summary>
        DEBUG,
        /// <summary>Information level 信息级别</summary>
        INFO,
        /// <summary>Warning level 警告级别</summary>
        WARN,
        /// <summary>Error level 错误级别</summary>
        ERROR,
        /// <summary>Fatal/critical level 致命/关键级别</summary>
        FATAL
    }


}
