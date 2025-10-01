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
//  1. 支持 OpenVINO、ONNX Runtime、TensorRT 等主流模型格式部署
//  2. 支持目标检测、图像分割、关键点检测等多种任务
//  3. 支持 YOLOv5-v12全系列模型部署，同时支持更多其它模型部署
//  4. 支持 C# .NET Framework 4.8 、.NET 6/7/8/9 桌面端和服务器端部署
//  ------------------------------------------------------------------------
//  【官方支持】
//  📌 GitHub仓库：https://github.com/guojin-yan/YoloDeployCsharp
//  📌 QQ交流群：945057948（加入获取最新资料）
//  📌 微信公众号：CSharp与边缘模型部署（教程+案例）
//  📌 CSDN博客：guojin.blog.csdn.net（技术文章）
//  ------------------------------------------------------------------------
//  【联系我们】
//  ✉ 商务合作：guojin_yjs@cumt.edu.cn / 微信：15253793309
//  🐞 Bug反馈：guojin_yjs@cumt.edu.cn / 微信：15253793309 / QQ群：945057948
//  ⚡ 技术支持：guojin_yjs@cumt.edu.cn / 微信：15253793309 / QQ群：945057948
//  ========================================================================
//  
//  【使用声明】
//  1. 本工具免费用于学术和非商业用途，如需修改源码，请保留版权信息并遵循 Apache 2.0 协议。
//  2. 使用本工具即表示您同意《用户许可协议》。
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
    public class AnomalibSegDemos
    {
        public static void Run() 
        {
            // 将下面的模型路径替换为你自己的模型路径
            string modelPath = @"E:\Model\anomalib\Padim\model\padim.onnx";
            // 将下面的图片路径替换为你自己的图片路径
            string imagePath = @"E:\Model\anomalib\Padim\images\broken_small\000.png";
            AnomalibSegConfig config = new AnomalibSegConfig(modelPath);
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            config.InputSizes.Add(new int[4] { 1, 3, 256, 256 });
            AnomalibSegModel model = new AnomalibSegModel(config);
            Mat img = Cv2.ImRead(imagePath);
            var result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            model.ModelInferenceProfiler.PrintAllRecords();
            var resultImg = Visualize.DrawSegResult(result, img, new VisualizeOptions(1.0f));
            Cv2.ImShow("image", resultImg);
            Cv2.ImShow("mask", result[0].Mask.ToMat());
            Cv2.ImShow("rawmask", result[0].RawMask.ToMat());
            Cv2.WaitKey();
        }
    }
}
