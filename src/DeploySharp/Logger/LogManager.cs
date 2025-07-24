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
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System.Xml.Linq;

namespace DeploySharp.Log
{


    /// <summary>
    /// Provides centralized logging management for the application using log4net
    /// </summary>
    public static class LoggerManager
    {
        /// <summary>
        /// The main logger instance for the project
        /// </summary>
        public static readonly ILog ProjectMainLogger;

        /// <summary>
        /// Flag indicating whether logging system has been initialized
        /// </summary>
        private static bool _isInitialized = false;

        /// <summary>
        /// Lock object for thread-safe initialization
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes logger with default settings (DEBUG level, Console output)
        /// </summary>
        public static void InitializeDefault()
        {
            Initialize(LogLevel.DEBUG, LogOutput.All);
        }

        /// <summary>
        /// Initializes logging system with specified configuration
        /// </summary>
        /// <param name="level">Minimum log level to output</param>
        /// <param name="output">Output targets (Console/File/All)</param>
        /// <param name="logPath">Directory path for log files (default: "Logs")</param>
        public static void Initialize(LogLevel level, LogOutput output, string logPath = "DeploySharpLogs")
        {
            lock (_lock)
            {
                if (_isInitialized) return;


                var hierarchy = (Hierarchy)LogManager.GetRepository();
                hierarchy.ResetConfiguration(); // 清除旧配置
                hierarchy.Root.RemoveAllAppenders();
                hierarchy.Root.Level = Level.Off;

                var defaultLogger = hierarchy.GetLogger("ProjectMainLogger") as Logger;
                // Configure console appender if Console output is enabled
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
                if (output.HasFlag(LogOutput.File))
                {

                    Directory.CreateDirectory((logPath));

                    var fileAppender = new RollingFileAppender
                    {
                        Name = "ProjectMainLoggerAppender",
                        File = (logPath)+"/",
                        AppendToFile = true,
                        RollingStyle = RollingFileAppender.RollingMode.Composite,
                        MaxSizeRollBackups = 10,
                        MaximumFileSize = "10MB",
                        DatePattern = "'DeploySharp_'yyyy-MM'.log'",
                        LockingModel = new FileAppender.MinimalLock(),
                        Layout = new CustomPatternLayout
                        {
                            ConversionPattern = "[%date] [%thread] [%-5level] (%filename:%line) - %message%newline%exception"
                        },
                        PreserveLogFileNameExtension = true,
                        ImmediateFlush = true,
                        StaticLogFileName = false

                    };
                    ((PatternLayout)fileAppender.Layout).ActivateOptions();
                    fileAppender.ActivateOptions();


                    defaultLogger.AddAppender(fileAppender);
                  

                   
                }
                defaultLogger.Level = ConvertLevel(level);
                defaultLogger.Additivity = false;
                hierarchy.Configured = true;

                _isInitialized = true;
            }
        }


        /// <summary>
        /// Converts custom LogLevel to log4net.Core.Level
        /// </summary>
        /// <param name="level">Custom log level</param>
        /// <returns>Corresponding log4net level</returns>
        public static log4net.Core.Level ConvertLevel(LogLevel level) => level switch
        {
            LogLevel.DEBUG => log4net.Core.Level.Debug,
            LogLevel.INFO => log4net.Core.Level.Info,
            LogLevel.WARN => log4net.Core.Level.Warn,
            LogLevel.ERROR => log4net.Core.Level.Error,
            LogLevel.FATAL => log4net.Core.Level.Fatal,
            _ => log4net.Core.Level.All
        };
    }

    /// <summary>
    /// Enum defining available log output targets
    /// </summary>
    [Flags]
    public enum LogOutput
    {
        /// <summary>Output to console</summary>
        Console = 1,
        /// <summary>Output to file</summary>
        File = 2,
        /// <summary>Output to both console and file</summary>
        All = Console | File
    }

    /// <summary>
    /// Enum defining available log levels
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Debug level (most verbose)</summary>
        DEBUG,
        /// <summary>Information level</summary>
        INFO,
        /// <summary>Warning level</summary>
        WARN,
        /// <summary>Error level</summary>
        ERROR,
        /// <summary>Fatal/critical level</summary>
        FATAL
    }


}
