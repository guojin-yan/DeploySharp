using log4net;
using log4net.Repository.Hierarchy;
using System;
namespace DeploySharp.Log
{
    /// <summary>
    /// Provides a static logger instance for the application with initialization and log level control
    /// </summary>
    public static class MyLogger
    {
        /// <summary>
        /// The static logger instance for the application
        /// </summary>
        public static ILog Log = LogManager.GetLogger("ProjectMainLogger");

        /// <summary>
        /// Static constructor that initializes the logger and outputs welcome message
        /// </summary>
        static MyLogger()
        {
            LoggerManager.InitializeDefault();
            string msg =
                "\n========================================================================\n" +
                "欢迎使用 DeploySharp 模型部署工具，如有问题，请通过以下方式联系：\n" +
                "【QQ交流群】\t945057948\n" +
                "【微信公众号】\tCSharp与边缘模型部署\n" +
                "【CSDN博客】\tguojin.blog.csdn.net\n" +
                "========================================================================\n";
            Log.Info(msg);
        }

        /// <summary>
        /// Sets the logging level for the main logger
        /// </summary>
        /// <param name="level">The log level to set (Debug, Info, Warn, Error, Fatal)</param>
        public static void SetLevel(LogLevel level)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            var defaultLogger = hierarchy.GetLogger("ProjectMainLogger") as Logger;
            defaultLogger.Level = LoggerManager.ConvertLevel(level);
        }
    }

}
