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
            //string msg =
            //    "\n========================================================================\n" +
            //    "欢迎使用 DeploySharp 模型部署工具，如有问题，请通过以下方式联系：\n" +
            //    "【QQ交流群】\t945057948\n" +
            //    "【微信公众号】\tCSharp与边缘模型部署\n" +
            //    "【CSDN博客】\tguojin.blog.csdn.net\n" +
            //    "========================================================================\n";
            //Log.Info(msg);
            string msg1 ="\n" + 
            "//========================================================================\n" +
            "//  \n" +
            "//  ██████╗ ███████╗██████╗ ██╗      ██████╗ ██╗   ██╗███████╗██╗  ██╗\n" +
            "//  ██╔══██╗██╔════╝██╔══██╗██║     ██╔═══██╗╚██╗ ██╔╝██╔════╝╚██╗██╔╝\n" +
            "//  ██║  ██║█████╗  ██████╔╝██║     ██║   ██║ ╚████╔╝ █████╗   ╚███╔╝ \n" +
            "//  ██║  ██║██╔══╝  ██╔═══╝ ██║     ██║   ██║  ╚██╔╝  ██╔══╝   ██╔██╗ \n" +
            "//  ██████╔╝███████╗██║     ███████╗╚██████╔╝   ██║   ███████╗██╔╝ ██╗\n" +
            "//  ╚═════╝ ╚══════╝╚═╝     ╚══════╝ ╚═════╝    ╚═╝   ╚══════╝╚═╝  ╚═╝\n" +
            "//  \n" +
            "//  ========================================================================\n" +
            "//  【工具名称】DeploySharp\n" +
            "//  【版权声明】© 2025 Yan Guojin. All Rights Reserved.\n" +
            "//  【开源协议】Apache License 2.0（请遵守许可证条款）\n" +
            "//  ------------------------------------------------------------------------\n" +
            "//  【功能简介】\n" +
            "//  1. 支持 OpenVINO、ONNX Runtime、TensorRT 等主流模型格式部署\n" +
            "//  2. 支持目标检测、图像分割、关键点检测等多种任务\n" +
            "//  3. 支持 YOLOv5-v12全系列模型部署，同时支持更多其它模型部署\n" +
            "//  4. 支持 C# .NET Framework 4.8 、.NET 6/7/8/9 桌面端和服务器端部署\n" +

            "//  5. 支持 ImageSharp 和 OpenCvSharp 两大图像处理库。\n" +
            "//  6. 支持单张图片和批量图片多种推理方式。\n" +
            "//  7. 支持丰富的推理前处理和后处理操作。\n" +
            "//  8. 支持详细的推理性能分析和日志记录。\n" +
            "//  9. 提供多种可视化结果展示方式。\n" +
            "//  10. 提供完善的文档和示例代码。\n" +
            "//  11. 持续更新和维护，紧跟最新技术发展。\n" +
            "//  ------------------------------------------------------------------------\n" +
            "//  【官方支持】\n" +
            "//  📌 GitHub仓库：https://github.com/guojin-yan/YoloDeployCsharp\n" +
            "//  📌 QQ交流群：945057948（加入获取最新资料）\n" +
            "//  📌 微信公众号：CSharp与边缘模型部署（教程+案例）\n" +
            "//  📌 CSDN博客：guojin.blog.csdn.net（技术文章）\n" +
            "//  ------------------------------------------------------------------------\n" +
            "//  【联系我们】\n" +
            "//  ✉ 商务合作：guojin_yjs@cumt.edu.cn / 微信：15253793309\n" +
            "//  🐞 Bug反馈：guojin_yjs@cumt.edu.cn / 微信：15253793309 / QQ群：945057948\n" +
            "//  ⚡ 技术支持：guojin_yjs@cumt.edu.cn / 微信：15253793309 / QQ群：945057948\n" +
            "//  ========================================================================\n" +
            "//  \n" +
            "//  【使用声明】\n" +

            "//  1. 本工具免费用于学术和非商业用途，如需修改源码，请保留版权信息并遵循 Apache 2.0 协议。 \n" +
            "//  2. 使用本工具即表示您同意《用户许可协议》、《Apache 2.0 协议》。 \n" +
            "//  3. 本工具不保证完全无误，使用过程中请自行评估风险并承担相应责任。 \n" +
            "//  4. 本工具在开发中使用AI工具辅助生成部分代码，难免存在不完善之处，敬请谅解。 \n" +
            "//  5. 如需商业使用或有任何疑问，请联系我们获取支持。 \n" +
            "//  ========================================================================\n" +
            "//\n" +
            "//  【赞助支持】\n" +
            "//  🌟 如果本项目对您有帮助，欢迎赞助支持我们：\n" +
            "//  - 支付宝/微信赞助码：手机号15253793309\n" +
            "//========================================================================\n";
            Log.Info(msg1);
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
