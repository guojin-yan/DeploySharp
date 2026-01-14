![OpenVINO™ C# API](https://socialify.git.ci/guojin-yan/DeploySharp/image?description=1&descriptionEditable=💞%20Deploying%20Deep%20Learning%20Models%20On%20Multiple%20Platforms%20(OpenVINO/ONNX%20Runtime,%20etc.)%20💞%20&forks=1&issues=1&logo=https%3A%2F%2Fs2.loli.net%2F2023%2F01%2F26%2FylE1K5JPogMqGSW.png&name=1&owner=1&pattern=Circuit%20Board&pulls=1&stargazers=1&theme=Light)

<p align="center">    
    <a href="./LICENSE.txt">
        <img src="https://img.shields.io/github/license/guojin-yan/openvinosharp.svg">
    </a>    
    <a >
        <img src="https://img.shields.io/badge/Framework-.NET 8.0%2C%20.NET 6.0%2C%20.NET 5.0%2C%20.NET Framework 4.8%2C%20.NET Framework 4.7.2%2C%20.NET Framework 4.6%2C%20.NET Core 3.1-pink.svg">
    </a>    
</p>



简体中文| [English](README.md)

# 📚 简介

&emsp;  **DeploySharp** 是一个专为 C# 开发者设计的跨平台模型部署框架，提供从模型加载、配置管理到推理执行的端到端解决方案。其核心架构采用模块化命名空间设计，显著降低了 C# 生态中深度学习模型的集成复杂度，

#### 1. **架构设计与功能分层**

- 根命名空间 `DeploySharp` 作为统一入口，集成模型加载、推理执行等核心功能
- 通过子命名空间（如 `DeploySharp.Engine`）实现模块化分层设计
- 关键类采用泛型设计，支持图像处理/分类/检测等多任务标准数据交互

#### 2. **多引擎支持与扩展能力**

- 原生支持 OpenVINO（通过`OpenVinoSharp`）、ONNX Runtime 推理引擎
- 支持 YOLOv5-v12全系列模型、Anomaly及其他主流模型部署

#### 3. **跨平台运行时支持**

- 兼容 .NET Framework 4.8 及 .NET 6/7/8/9
- 深度集成 .NET 运行时生态（NuGet 包管理）

#### 4. **高性能推理能力**

- 异步推理支持（`System.Threading.Tasks`）
- 支持单张/批量图片推理模式
- 丰富的预处理（ImageSharp/OpenCvSharp）和后处理操作

#### 5. **开发者支持体系**

- 中英双语代码注释与技术文档
- `log4net` 分级日志系统（错误/警告/调试）
- 提供可视化结果展示方案
- 提供完善的示例代码库



该项目开源遵循 Apache License 2.0 协议，开发者可通过 QQ 群、微信公众号等渠道获取支持。未来版本计划扩展 TensorRT 支持并优化现有引擎的异构计算能力。

# 🎨模型支持列表

|  Model Name  |       Model Type        | OpenVINO | ONNX Runtime | TensorRT |
| :----------: | :---------------------: | :------: | :----------: | :------: |
|  **YOLOv5**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **YOLOv5**  |      Segmentation       |    ✅     |      ✅       |    ✅     |
|  **YOLOv6**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **YOLOv7**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **YOLOv8**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **YOLOv8**  |      Segmentation       |    ✅     |      ✅       |    ✅     |
|  **YOLOv8**  |          Pose           |    ✅     |      ✅       |    ✅     |
|  **YOLOv8**  | Oriented Bounding Boxes |    ✅     |      ✅       |    ✅     |
|  **YOLOv9**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **YOLOv9**  |      Segmentation       |    ✅     |      ✅       |    ✅     |
| **YOLOv10**  |        Detection        |    ✅     |      ✅       |    ✅     |
| **YOLOv11**  |        Detection        |    ✅     |      ✅       |    ✅     |
| **YOLOv11**  |      Segmentation       |    ✅     |      ✅       |    ✅     |
| **YOLOv11**  |          Pose           |    ✅     |      ✅       |    ✅     |
| **YOLOv11**  | Oriented Bounding Boxes |    ✅     |      ✅       |    ✅     |
| **YOLOv12**  |        Detection        |    ✅     |      ✅       |    ✅     |
| **Anomalib** |      Segmentation       |    ✅     |      ✅       |    ✅     |
| **PP-YOLOE** |        Detection        |    ✅     |      ✅       |    ✅     |
|  **DEIMv2**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **RFDETR**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **RFDETR**  |      Segmentation       |    ✅     |      ✅       |    ✅     |
|  **RTDETR**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **YOLO26**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **YOLO26**  |      Segmentation       |    ✅     |      ✅       |    ✅     |
|  **YOLO26**  |          Pose           |    ✅     |      ✅       |    ✅     |
|  **YOLO26**  | Oriented Bounding Boxes |    ✅     |      ✅       |    ✅     |

# <img title="NuGet" src="https://s2.loli.net/2023/08/08/jE6BHu59L4WXQFg.png" alt="" width="40">NuGet Package

## Core Managed Libraries

| Package                                        | Description                                               | Link                                                         |
| ---------------------------------------------- | --------------------------------------------------------- | ------------------------------------------------------------ |
| **JYPPX.DeploySharp**                          | DeploySharp API core libraries                            | [![NuGet Gallery ](https://badge.fury.io/nu/JYPPX.DeploySharp.svg)](https://www.nuget.org/packages/JYPPX.DeploySharp/) |


### Native Runtime Libraries

| Package                           | Description                                                  | Link                                                         |
| --------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| **JYPPX.DeploySharp.ImageSharp**  | An assembly that uses ImageSharp as an image processing tool. | [![NuGet Gallery ](https://badge.fury.io/nu/JYPPX.DeploySharp.ImageSharp.svg)](https://www.nuget.org/packages/JYPPX.DeploySharp.ImageSharp/) |
| **JYPPX.DeploySharp.OpenCvSharp** | An assembly that uses OpenCvSharp as an image processing tool. | [![NuGet Gallery ](https://badge.fury.io/nu/JYPPX.DeploySharp.OpenCvSharp.svg)](https://www.nuget.org/packages/JYPPX.DeploySharp.OpenCvSharp/) |


# ⚙ 如何安装

**&emsp;    DeploySharp**包含了OpenCvSharp、ImageSharp等图像处理方式，同时支持OpenVINO、ONNX Runtime模型部署引擎，因此用户可以根据自己需求自行组合，并安装对应的NuGet Package即可开箱使用。以下总结了常用的一些使用情况的NuGet Package安装场景：

- **OpenVINO推理+OpenCvSharp图像处理**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.OpenCvSharp

OpenVINO.runtime.win
OpenCvSharp4.runtime.win 
```

- **OpenVINO推理+ImageSharp图像处理**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.ImageSharp

OpenVINO.runtime.win
```

- **ONNX Runtime推理+OpenCvSharp图像处理**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.OpenCvSharp

OpenCvSharp4.runtime.win 
```

- **ONNX Runtime推理+ImageSharp图像处理**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.OpenCvSharp
```

- **ONNX Runtime(OpenVINO加速)推理+ImageSharp图像处理**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.ImageSharp

Intel.ML.OnnxRuntime.OpenVino
```

- **ONNX Runtime(DML加速)推理+ImageSharp图像处理**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.ImageSharp

Microsoft.ML.OnnxRuntime.DirectML
```

- **ONNX Runtime(CUDA加速)推理+ImageSharp图像处理**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.ImageSharp

Microsoft.ML.OnnxRuntime.DirectML
```

&emsp;    由于使用CUDA对ONNX Runtime加速受GPU设备型号以及软件版本影响，因此需要按照ONNX Runtime官方提供的版本对应关系进行下载使用，其中ONNX Runtime与CUDA、cuDNN对应关系请参考一下以下链接：

```
https://runtime.onnx.org.cn/docs/execution-providers/CUDA-ExecutionProvider.html#requirements
```

&emsp;    以上所列出的使用方式均可以通过NuGet Package一键安装，同样的，ONNX Runtime还支持更多加速方式，但需要用户自己进行代码构建，其构建流程与方式，参考官方教程即可，链接为：

```
https://runtime.onnx.org.cn/docs/execution-providers/
```



## 🏷开始使用

&emsp;    如果你不知道如何使用，通过下面代码简单了解使用方法。

### ImageSharp图像处理

```c#
using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace DeploySharp.ImageSharp.Demo
{
    public class YOLOv5DetDemo
    {
        public static void Run()
        {
            // 模型和测试图片可以前往QQ群(945057948)下载
            // 将下面的模型路径替换为你自己的模型路径
            string modelPath = @"E:\Model\Yolo\yolov5s.onnx";
            // 将下面的图片路径替换为你自己的图片路径
            string imagePath = @"E:\Data\image\bus.jpg";
            Yolov5DetConfig config = new Yolov5DetConfig(modelPath);
            //config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            Yolov5DetModel model = new Yolov5DetModel(config);
            var img = Image.Load(imagePath);
            var result = model.Predict(img);
            model.ModelInferenceProfiler.PrintAllRecords();
            var resultImg = Visualize.DrawDetResult(result, img as Image<Rgb24>, new VisualizeOptions(1.0f));
            resultImg.Save(@$"./result_{ModelType.YOLOv5Det.ToString()}.jpg");
        }
    }
}
```



### OpenCvSharp图像处理

```c#
using OpenCvSharp;
using System.Diagnostics;
using DeploySharp.Model;
using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp;
using System.Net.Http.Headers;

namespace DeploySharp.OpenCvSharp.Demo
{
    public class YOLOv5DetDemo
    {
        public static void Run()
        {
            // 模型和测试图片可以前往QQ群(945057948)下载
            // 将下面的模型路径替换为你自己的模型路径
            string modelPath = @"E:\Model\Yolo\yolov5s.onnx";
            // 将下面的图片路径替换为你自己的图片路径
            string imagePath = @"E:\Data\image\bus.jpg";
            Yolov5DetConfig config = new Yolov5DetConfig(modelPath);
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            Yolov5DetModel model = new Yolov5DetModel(config);
            Mat img = Cv2.ImRead(imagePath);
            var result = model.Predict(img);
            model.ModelInferenceProfiler.PrintAllRecords();
            var resultImg = Visualize.DrawDetResult(result, img, new VisualizeOptions(1.0f));
            Cv2.ImShow("image", resultImg);
            Cv2.WaitKey();
        }
    }
}

```



## 💻 应用案例

&emsp;    获取更多应用案例请参考：

|  案例类型  |               框架               |                             链接                             |
| :--------: | :------------------------------: | :----------------------------------------------------------: |
|  桌面应用  |        .NET Framework 4.8        | [DeploySharp.ImageSharp-ApplicationPlatform](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV1.0/applications/.NET%20Framework%204.8/DeploySharp.ImageSharp-ApplicationPlatform) |
|  桌面应用  |             .NET 6.0             | [DeploySharp.OpenCvSharp-ApplicationPlatform](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV1.0/applications/.NET%206.0/DeploySharp.OpenCvSharp-ApplicationPlatform) |
| 控制台应用 | .NET Framework 4.8、.NET 6.0-9.0 | [DeploySharp.samples](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV1.0/samples) |

## 🗂文档

&emsp;    如果想了解更多信息，可以参阅：[DeploySharp API Documented](https://guojin-yan.github.io/DeploySharp.docs/index.html)

## 🎖 贡献

&emsp;    如果您对**DeploySharp**在C#使用感兴趣，有兴趣对开源社区做出自己的贡献，欢迎加入我们，一起开发**DeploySharp**。

&emsp;    如果你对该项目有一些想法或改进思路，欢迎联系我们，指导下我们的工作。

## <img title="" src="https://user-images.githubusercontent.com/48054808/157835345-f5d24128-abaf-4813-b793-d2e5bdc70e5a.png" alt="" width="40"> 许可证书

&emsp;    本项目的发布受[Apache 2.0 license](https://github.com/guojin-yan/OpenVINO-CSharp-API/blob/csharp3.0/LICENSE.txt)许可认证。

 &emsp;   最后如果各位开发者在使用中有任何问题，欢迎大家与我联系。

![image-20250224211044113](https://ygj-images-container.oss-cn-nanjing.aliyuncs.com/BlogGallery/202502242110187.png)
