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

# 📄技术博客

- [DeploySharp开源发布：让C#部署深度学习模型更加简单](https://mp.weixin.qq.com/s/K1mYc-R3CNkoi3GxeCb75Q)

- [基于DeploySharp 的深度学习模型部署测试平台：支持YOLO全系列模型](https://mp.weixin.qq.com/s/vs4ZyA-UPe5EWG7Zj2mNGA)

- [手把手运行教大家运行基于DeploySharp 的深度学习模型部署测试平台：快速实现在C#平台进行模型部署](https://mp.weixin.qq.com/s/DGqyNQ-iLDjJcEAmdd2nLw)

- [DeploySharp 全面支持 YOLO26 系列，助力开发者快速部署落地应用](https://mp.weixin.qq.com/s/Zjk4-tVa-GA5MUqFwswqPA)

- [使用 JYPPX.DeploySharp 高效部署 PaddleOCR，解锁多种高性能 OCR 文字识别方案](https://mp.weixin.qq.com/s/Luf10qJdO-XfxvVN-Qhj6Q)

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
|  **PP-OCR v5**  |      Detection       |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v5**  |     Classification     |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v5**  | Recognize |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v5**  | Det+Cls+Rec |    ✅     |      ✅       |    ✅    |
|  **PP-OCR v4**  |      Detection       |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v4**  |     Classification     |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v4**  | Recognize |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v4**  | Det+Cls+Rec |    ✅     |      ✅       |    ✅    |

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
|  桌面应用  |             .NET 8.0             | [JYPPX.DeploySharp.OpenCvSharp.PaddleOcr ](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV1.0/applications/.NET 8.0/JYPPX.DeploySharp.OpenCvSharp.PaddleOcr) |

## 🗂文档

&emsp;    如果想了解更多信息，可以参阅：[DeploySharp API Documented](https://guojin-yan.github.io/DeploySharp.docs/index.html)

## 🎖 贡献

&emsp;    如果您对**DeploySharp**在C#使用感兴趣，有兴趣对开源社区做出自己的贡献，欢迎加入我们，一起开发**DeploySharp**。

&emsp;    如果你对该项目有一些想法或改进思路，欢迎联系我们，指导下我们的工作。

## <img title="" src="https://user-images.githubusercontent.com/48054808/157835345-f5d24128-abaf-4813-b793-d2e5bdc70e5a.png" alt="" width="40"> 许可证书

&emsp;    本项目的发布受[Apache 2.0 license](https://github.com/guojin-yan/OpenVINO-CSharp-API/blob/csharp3.0/LICENSE.txt)许可认证。

## 🧑‍🔧技术支持

 &emsp;  如有问题或建议，欢迎通过以下方式交流：

- 📧 **GitHub Issues**：在项目仓库提 Issue 或 Pull Request
- 💬 **QQ 交流群**：加入 **945057948**，回复更方便更快哦



## 📢软件声明

**1. 开源协议声明** 

作者所有开源项目代码均遵循 **Apache License 2.0** 开源协议。 

*特别说明：本项目集成了若干第三方库。若任何第三方库的许可协议与 Apache 2.0 协议存在冲突或不一致，均以该第三方库的原始许可协议为准。本项目不包含也不代表这些第三方库的授权声明，使用前请务必阅读并遵守第三方库的相关许可。*

**2. 代码开发与质量说明**

- **AI 辅助开发**：本代码在开发过程中使用了人工智能（AI）辅助生成与优化，并非完全由人工逐行编写。
- **安全性承诺**：**作者郑重声明，本代码中绝无任何有意设置的后门、病毒、木马或旨在破坏用户设备、窃取数据的恶意代码。**
- **技术局限性**：受限于作者个人的技术水平与能力，代码中可能存在因逻辑不严谨、优化不足或经验欠缺导致的低级问题（例如但不限于内存泄漏、偶发崩溃、资源未释放等）。这些问题纯属能力不足所致，并非主观故意。
- **测试范围**：由于作者精力有限，未对本软件进行全方位、覆盖所有边缘场景的完整测试。

**3. 免责声明（重要）** 

**请在将本代码应用于任何实际项目（特别是商业、工业或关键任务环境）之前，务必进行详尽、严格的自行测试与验证。** 鉴于上述可能存在的代码缺陷及测试覆盖不足，**因使用本代码而导致的任何直接或间接损失（包括但不限于设备故障、数据丢失、系统瘫痪或利润损失等），本作者概不负责。** 一旦您开始使用本代码，即表示您已知晓上述风险并同意自行承担一切后果，相关问题与本作者无关。

**4. 代码开源范围** 

本项目承诺核心逻辑代码完全开源，但上述提到的“第三方库”的二进制文件、源代码或相关资源不在本项目的开源义务范围内，请根据其各自的指引获取。

**5. 社区与反馈** 

尽管存在上述不足，我们仍欢迎大家下载使用、提交 Issue 或参与测试，共同完善项目。如果您在使用过程中发现 Bug、内存溢出或有改进建议，欢迎通过项目主页提供的联系方式与作者取得联系，我们将尽力在有限的时间内提供协助。



![image-20250224211044113](https://ygj-images-container.oss-cn-nanjing.aliyuncs.com/BlogGallery/202502242110187.png)
