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
using System.Diagnostics;
using System;
using DeploySharp.Model;
using DeploySharp.Engine;
using DeploySharp.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using DeploySharp;

namespace TestDemoImageSharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            //Pipeline YOLOv5DetPipeline = new Pipeline(ModelType.YOLOv5Det, @"E:\Model\Yolo\yolov5s.onnx");
            //Pipeline YOLOv5SegPipeline = new Pipeline(ModelType.YOLOv5Seg, @"E:\Model\Yolo\yolov5s-seg.onnx");
            //Pipeline YOLOv6DetPipeline = new Pipeline(ModelType.YOLOv6Det, @"E:\Model\Yolo\yolov6s.onnx");
            //Pipeline YOLOv7DetPipeline = new Pipeline(ModelType.YOLOv7Det, @"E:\Model\Yolo\yolov7.onnx");
            //Pipeline YOLOv8DetPipeline = new Pipeline(ModelType.YOLOv8Det, @"E:\Model\Yolo\yolov8s.onnx");
            //Pipeline YOLOv8SegPipeline = new Pipeline(ModelType.YOLOv8Seg, @"E:\Model\Yolo\yolov8s-seg.onnx");
            //Pipeline YOLOv8ObbPipeline = new Pipeline(ModelType.YOLOv8Obb, @"E:\Model\Yolo\yolov8s-obb.onnx");
            //Pipeline YOLOv8PosePipeline = new Pipeline(ModelType.YOLOv8Pose, @"E:\Model\Yolo\yolov8s-pose.onnx");

            //Pipeline YOLOv9DetPipeline = new Pipeline(ModelType.YOLOv9Det, @"E:\Model\Yolo\yolov9s.onnx");
            //Pipeline YOLOv9SegPipeline = new Pipeline(ModelType.YOLOv9Seg, @"E:\Model\Yolo\yolov9-c-seg.onnx");
            //Pipeline YOLOv10DetPipeline = new Pipeline(ModelType.YOLOv10Det, @"E:\Model\Yolo\yolov10s.onnx");
            //Pipeline YOLOv11DetPipeline = new Pipeline(ModelType.YOLOv11Det, @"E:\Model\Yolo\yolo11s.onnx");
            //Pipeline YOLOv11SegPipeline = new Pipeline(ModelType.YOLOv11Seg, @"E:\Model\Yolo\yolov8s-seg.onnx");
            //Pipeline YOLOv11ObbPipeline = new Pipeline(ModelType.YOLOv11Obb, @"E:\Model\Yolo\yolov8s-obb.onnx");
            //Pipeline YOLOv11PosePipeline = new Pipeline(ModelType.YOLOv11Pose, @"E:\Model\Yolo\yolov8s-pose.onnx");
            //Pipeline YOLOv12DetPipeline = new Pipeline(ModelType.YOLOv12Det, @"E:\Model\Yolo\yolo12s.onnx");
            //Pipeline YOLOv13DetPipeline = new Pipeline(ModelType.YOLOv13Det, @"E:\Model\Yolo\yolov13n.onnx");



            Pipeline YOLOv5DetPipeline = new Pipeline(ModelType.YOLOv5Det, @"E:\Model\Yolo\yolov5s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv5SegPipeline = new Pipeline(ModelType.YOLOv5Seg, @"E:\Model\Yolo\yolov5s-seg.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv6DetPipeline = new Pipeline(ModelType.YOLOv6Det, @"E:\Model\Yolo\yolov6s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv7DetPipeline = new Pipeline(ModelType.YOLOv7Det, @"E:\Model\Yolo\yolov7.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv8DetPipeline = new Pipeline(ModelType.YOLOv8Det, @"E:\Model\Yolo\yolov8s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv8SegPipeline = new Pipeline(ModelType.YOLOv8Seg, @"E:\Model\Yolo\yolov8s-seg.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv8ObbPipeline = new Pipeline(ModelType.YOLOv8Obb, @"E:\Model\Yolo\yolov8s-obb.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv8PosePipeline = new Pipeline(ModelType.YOLOv8Pose, @"E:\Model\Yolo\yolov8s-pose.onnx", InferenceBackend.OnnxRuntime);

            Pipeline YOLOv9DetPipeline = new Pipeline(ModelType.YOLOv9Det, @"E:\Model\Yolo\yolov9s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv9SegPipeline = new Pipeline(ModelType.YOLOv9Seg, @"E:\Model\Yolo\yolov9-c-seg.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv10DetPipeline = new Pipeline(ModelType.YOLOv10Det, @"E:\Model\Yolo\yolov10s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv11DetPipeline = new Pipeline(ModelType.YOLOv11Det, @"E:\Model\Yolo\yolo11s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv11SegPipeline = new Pipeline(ModelType.YOLOv11Seg, @"E:\Model\Yolo\yolov8s-seg.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv11ObbPipeline = new Pipeline(ModelType.YOLOv11Obb, @"E:\Model\Yolo\yolov8s-obb.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv11PosePipeline = new Pipeline(ModelType.YOLOv11Pose, @"E:\Model\Yolo\yolov8s-pose.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv12DetPipeline = new Pipeline(ModelType.YOLOv12Det, @"E:\Model\Yolo\yolo12s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv13DetPipeline = new Pipeline(ModelType.YOLOv13Det, @"E:\Model\Yolo\yolov13n.onnx", InferenceBackend.OnnxRuntime);


            // 1. 使用ImageSharp加载图像
            using (var image = Image.Load(@"E:\Data\image\demo_2.jpg"))
            {
                Image<Rgb24> YOLOv5DetResult = YOLOv5DetPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv5DetResult.Save(@$"./result/result_{ModelType.YOLOv5Det.ToString()}.jpg");
                Image<Rgb24> YOLOv5SegResult = YOLOv5SegPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv5SegResult.Save(@$"./result/result_{ModelType.YOLOv5Seg.ToString()}.jpg");
                Image<Rgb24> YOLOv6DetResult = YOLOv6DetPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv6DetResult.Save(@$"./result/result_{ModelType.YOLOv6Det.ToString()}.jpg");
                Image<Rgb24> YOLOv7DetResult = YOLOv7DetPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv7DetResult.Save(@$"./result/result_{ModelType.YOLOv7Det.ToString()}.jpg");
                Image<Rgb24> YOLOv8DetResult = YOLOv8DetPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv8DetResult.Save(@$"./result/result_{ModelType.YOLOv8Det.ToString()}.jpg");
                Image<Rgb24> YOLOv8SegResult = YOLOv8SegPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv8SegResult.Save(@$"./result/result_{ModelType.YOLOv8Seg.ToString()}.jpg");
                Image<Rgb24> YOLOv9DetResult = YOLOv9DetPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv9DetResult.Save(@$"./result/result_{ModelType.YOLOv9Det.ToString()}.jpg");
                Image<Rgb24> YOLOv9SegResult = YOLOv9SegPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv9SegResult.Save(@$"./result/result_{ModelType.YOLOv9Seg.ToString()}.jpg");
                Image<Rgb24> YOLOv10DetResult = YOLOv10DetPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv10DetResult.Save(@$"./result/result_{ModelType.YOLOv10Det.ToString()}.jpg");
                Image<Rgb24> YOLOv11DetResult = YOLOv11DetPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv11DetResult.Save(@$"./result/result_{ModelType.YOLOv11Det.ToString()}.jpg");
                Image<Rgb24> YOLOv11SegResult = YOLOv11SegPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv11SegResult.Save(@$"./result/result_{ModelType.YOLOv11Seg.ToString()}.jpg");
                Image<Rgb24> YOLOv12DetResult = YOLOv12DetPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv12DetResult.Save(@$"./result/result_{ModelType.YOLOv12Det.ToString()}.jpg");
                Image<Rgb24> YOLOv13DetResult = YOLOv13DetPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv13DetResult.Save(@$"./result/result_{ModelType.YOLOv13Det.ToString()}.jpg");
            }

            using (var image = Image.Load(@"E:\Data\image\plane.png"))
            {
                Image<Rgb24> YOLOv8ObbResult = YOLOv8ObbPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv8ObbResult.Save(@$"./result/result_{ModelType.YOLOv8Obb.ToString()}.jpg");
                Image<Rgb24> YOLOv11ObbResult = YOLOv11ObbPipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv11ObbResult.Save(@$"./result/result_{ModelType.YOLOv11Obb.ToString()}.jpg");
            }

            // 1. 使用ImageSharp加载图像
            using (var image = Image.Load(@"E:\Data\image\demo_9.jpg"))
            { 
                Image<Rgb24> YOLOv8PoseResult = YOLOv8PosePipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv8PoseResult.Save(@$"./result/result_{ModelType.YOLOv8Pose.ToString()}.jpg");
                Image<Rgb24> YOLOv11PoseResult = YOLOv11PosePipeline.PredictAndDrawing(image as Image<Rgb24>);
                YOLOv11PoseResult.Save(@$"./result/result_{ModelType.YOLOv11Pose.ToString()}.jpg");
               

            }
        }
    }
}
