//========================================================================
//  
//  ██████╗ ███████╗██████╗ ██╗      ██████╗ ██╗   ██╗███████╗██╗  ██╗
//  ██╔══██╗██╔════╝██╔══██╗██║     ██╔═══██╗╚██╗ ██╔╝██╔════╝╚██╗██╔╝
//  ██║  ██║█████╗  ██████╔╝██║     ██║   ██║ ╚████╔╝ █████╗   ╚███╔╝ 
//  ██║  ██║██╔══╝  ██╔═══╝ ██║     ██║   ██║  ╚██╔╝  ██╔══╝   ██╔██╗ 
//  ██████╔╝███████╗██║     ███████╗╚██████╔╝   ██║   ███████╗██╔╝ ██╗
//  ╚═════╝ ╚══════╝╚═╝     ╚══════╝ ╚═════╝    ╚═╝   ╚══════╝╚═╝  ╚═╝
//  
//  ========================================================================
//  【工具名称】DeploySharp
//  【版权声明】© 2025 Yan Guojin. All Rights Reserved.
//  【开源协议】Apache License 2.0（请遵守许可证条款）
//  ------------------------------------------------------------------------
//  【功能简介】
//  1. 支持 OpenVINO、ONNX Runtime、TensorRT 等主流模型格式部署。
//  2. 支持目标检测、图像分割、关键点检测等多种任务。
//  3. 支持 YOLOv5-v12全系列模型部署，同时支持更多其它模型部署。
//  4. 支持 C# .NET Framework 4.8 、.NET 6/7/8/9 桌面端和服务器端部署。
//  5. 支持 ImageSharp 和 OpenCvSharp 两大图像处理库。
//  6. 支持单张图片和批量图片多种推理方式。
//  7. 支持丰富的推理前处理和后处理操作。
//  8. 支持详细的推理性能分析和日志记录。
//  9. 提供多种可视化结果展示方式。
//  10. 提供完善的文档和示例代码。
//  11. 持续更新和维护，紧跟最新技术发展。
//  ------------------------------------------------------------------------
//  【官方支持】
//  📌 GitHub仓库：https://github.com/guojin-yan/YoloDeployCsharp
//  📌 QQ交流群：945057948（加入获取最新资料）
//  📌 微信公众号：CSharp与边缘模型部署（教程+案例）
//  📌 CSDN博客：guojin.blog.csdn.net（技术文章）
//  ------------------------------------------------------------------------
//  【联系我们】
//  ✉ 商务合作：guojin_yjs@cumt.edu.cn / 微信：15253793309
//  🐞 Bug反馈：guojin_yjs@cumt.edu.cn / 微信：15253793309
//  ⚡ 技术支持：guojin_yjs@cumt.edu.cn / 微信：15253793309
//  ========================================================================
//  
//  【使用声明】
//  1. 本工具免费用于学术和非商业用途，如需修改源码，请保留版权信息并遵循 Apache 2.0 协议。
//  2. 使用本工具即表示您同意《用户许可协议》、《Apache 2.0 协议》。
//  3. 本工具不保证完全无误，使用过程中请自行评估风险并承担相应责任。
//  4. 本工具在开发中使用AI工具辅助生成部分代码，难免存在不完善之处，敬请谅解。
//  5. 如需商业使用或有任何疑问，请联系我们获取支持。
//  ========================================================================
//
//  【赞助支持】
//  🌟 如果本项目对您有帮助，欢迎赞助支持我们：
//  - 支付宝/微信赞助码：手机号15253793309
//========================================================================
using OpenCvSharp;
using System.Diagnostics;
using DeploySharp.Model;
using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp;
using System.Net.Http.Headers;

namespace DeploySharp.OpenCvSharp.Demo
{
    public class YOLOv8ObbDemo
    {
        public static void Run()
        {
            // 模型和测试图片可以前往QQ群(945057948)下载
            // 将下面的模型路径替换为你自己的模型路径
            string modelPath = @"E:\Model\Yolo\yolov8s-obb.onnx";
            // 将下面的图片路径替换为你自己的图片路径
            string imagePath = @"E:\Data\image\plane.png";

            Yolov8ObbConfig config = new Yolov8ObbConfig(modelPath);
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            Yolov8ObbModel model = new Yolov8ObbModel(config);
            Mat img = Cv2.ImRead(imagePath);
            var result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            model.ModelInferenceProfiler.PrintAllRecords();
            var resultImg = Visualize.DrawObbResult(result, img, new VisualizeOptions(1.0f));
            Cv2.ImShow("image", resultImg);
            Cv2.WaitKey();
        }
    }
}