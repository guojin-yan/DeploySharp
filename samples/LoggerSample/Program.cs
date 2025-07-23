using DeploySharp.Log;

namespace LoggerSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            LoggerManager.Initialize(LogLevel.DEBUG, LogOutput.All, "CustomLogs");
            MyLogger.Log.Debug("这是一个Debug");
            MyLogger.Log.Info("这是一个Info");
            MyLogger.Log.Warn("这是一个Warn");
            MyLogger.Log.Error("这是一个Error", new Exception("111"));

            // 动态修改日志级别
            MyLogger.SetLevel(LogLevel.WARN);
            MyLogger.Log.Debug("这是一个Debug");
        }
    }
}
