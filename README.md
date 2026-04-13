![OpenVINO™ C# API](https://socialify.git.ci/guojin-yan/DeploySharp/image?description=1&descriptionEditable=💞%20Deploying%20Deep%20Learning%20Models%20On%20Multiple%20Platforms%20(OpenVINO/ONNX%20Runtime,%20etc.)%20💞%20&forks=1&issues=1&logo=https%3A%2F%2Fs2.loli.net%2F2023%2F01%2F26%2FylE1K5JPogMqGSW.png&name=1&owner=1&pattern=Circuit%20Board&pulls=1&stargazers=1&theme=Light)

<p align="center">    
    <a href="./LICENSE.txt">
        <img src="https://img.shields.io/github/license/guojin-yan/openvinosharp.svg">
    </a>    
    <a >
        <img src="https://img.shields.io/badge/Framework-.NET 8.0%2C%20.NET 6.0%2C%20.NET 5.0%2C%20.NET Framework 4.8%2C%20.NET Framework 4.7.2%2C%20.NET Framework 4.6%2C%20.NET Core 3.1-pink.svg">
    </a>    
</p>




[简体中文](README_cn.md)| English

# 📚 Introduction

&emsp;    **DeploySharp** is a cross-platform model deployment framework designed for C# developers, offering end-to-end solutions from model loading and configuration management to inference execution. Its modular namespace architecture significantly reduces the complexity of integrating deep learning models into the C# ecosystem.

#### 1. **Architecture & Layered Design**

- Root namespace `DeploySharp` serves as a unified entry point for core features (model loading, inference, etc.).
- Modular sub-namespaces (e.g., `DeploySharp.Engine`) enable clear functional layers.
- Generic class designs support standard data interfaces for tasks like image processing/classification/detection.

#### 2. **Multi-Engine Support**

- Native integration with OpenVINO (`OpenVinoSharp`) and ONNX Runtime.
- Compatibility with YOLOv5-v12 models, Anomalib, and other mainstream architectures.

#### 3. **Cross-Platform Runtime**

- Supports .NET Framework 4.8+ and .NET 6/7/8/9.
- Deep integration with .NET NuGet ecosystem.

#### 4. **High-Performance Inference**

- Asynchronous operations (`System.Threading.Tasks`).
- Batch/single-image inference modes.
- Rich pre-/post-processing (ImageSharp/OpenCvSharp).

#### 5. **Developer Support**

- Bilingual (EN/CN) code comments and documentation.
- `log4net` logging (error/warning/debug levels).
- Visualization tools and comprehensive code samples.

Licensed under **Apache License 2.0**. Future updates will expand TensorRT support and optimize heterogenous computing.



# 📄Technical Blog

- [DeploySharp开源发布：让C#部署深度学习模型更加简单](https://mp.weixin.qq.com/s/K1mYc-R3CNkoi3GxeCb75Q)

- [基于DeploySharp 的深度学习模型部署测试平台：支持YOLO全系列模型](https://mp.weixin.qq.com/s/vs4ZyA-UPe5EWG7Zj2mNGA)

- [手把手运行教大家运行基于DeploySharp 的深度学习模型部署测试平台：快速实现在C#平台进行模型部署](https://mp.weixin.qq.com/s/DGqyNQ-iLDjJcEAmdd2nLw)

- [DeploySharp 全面支持 YOLO26 系列，助力开发者快速部署落地应用](https://mp.weixin.qq.com/s/Zjk4-tVa-GA5MUqFwswqPA)

- [使用 JYPPX.DeploySharp 高效部署 PaddleOCR，解锁多种高性能 OCR 文字识别方案](https://mp.weixin.qq.com/s/Luf10qJdO-XfxvVN-Qhj6Q)

# 🎨Supported Models

|  Model Name  |       Model Type        | OpenVINO | ONNX Runtime | TensorRT |
| :----------: | :---------------------: | :------: | :----------: | :------: |
|  **YOLOCls**  |        Classification (YOLO)        |    ✅     |      ✅       |    ✅     |
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
|  **YOLO26**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **YOLO26**  |      Segmentation       |    ✅     |      ✅       |    ✅     |
|  **YOLO26**  |          Pose           |    ✅     |      ✅       |    ✅     |
|  **YOLO26**  | Oriented Bounding Boxes |    ✅     |      ✅       |    ✅     |
| **Anomalib** |      Segmentation       |    ✅     |      ✅       |    ✅     |
| **PP-YOLOE** |        Detection        |    ✅     |      ✅       |    ✅     |
|  **DEIMv2**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **RFDETR**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **RFDETR**  |      Segmentation       |    ✅     |      ✅       |    ✅     |
|  **RTDETR**  |        Detection        |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v5**  |      Detection       |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v5**  |     Classification     |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v5**  | Recognize |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v5**  | Det+Cls+Rec |    ✅     |      ✅       |    ✅    |
|  **PP-OCR v4**  |      Detection       |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v4**  |     Classification     |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v4**  | Recognize |    ✅     |      ✅       |    ✅     |
|  **PP-OCR v4**  | Det+Cls+Rec |    ✅     |      ✅       |    ✅    |
| **BRIA** | Bria-RMBG v1.4，v2.0 | ✅ | ✅ | ✅ |


# <img title="NuGet" src="https://s2.loli.net/2023/08/08/jE6BHu59L4WXQFg.png" alt="" width="40">NuGet Package

## Core Managed Libraries

| Package               | Description                    | Link                                                         |
| --------------------- | ------------------------------ | ------------------------------------------------------------ |
| **JYPPX.DeploySharp** | DeploySharp API core libraries | [![NuGet Gallery ](https://badge.fury.io/nu/JYPPX.DeploySharp.svg)](https://www.nuget.org/packages/JYPPX.DeploySharp/) |


### Native Runtime Libraries

| Package                           | Description                                                  | Link                                                         |
| --------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| **JYPPX.DeploySharp.ImageSharp**  | An assembly that uses ImageSharp as an image processing tool. | [![NuGet Gallery ](https://badge.fury.io/nu/JYPPX.DeploySharp.ImageSharp.svg)](https://www.nuget.org/packages/JYPPX.DeploySharp.ImageSharp/) |
| **JYPPX.DeploySharp.OpenCvSharp** | An assembly that uses OpenCvSharp as an image processing tool. | [![NuGet Gallery ](https://badge.fury.io/nu/JYPPX.DeploySharp.OpenCvSharp.svg)](https://www.nuget.org/packages/JYPPX.DeploySharp.OpenCvSharp/) |


# ⚙  Installation

&emsp;    **DeploySharp** includes image processing methods such as **OpenCvSharp** and **ImageSharp**, as well as support for **OpenVINO** and **ONNX Runtime** model deployment engines. Therefore, users can combine them according to their own needs and install the corresponding VNet Package to use them out of the box. The following summarizes some commonly used scenarios for installing VNet Package:

- **OpenVINO inference+OpenCvSharp image processing**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.OpenCvSharp

OpenVINO.runtime.win
OpenCvSharp4.runtime.win 
```

- **OpenVINO inference+ImageSharp image processing**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.ImageSharp

OpenVINO.runtime.win
```

- **ONNX Runtime inference+OpenCvSharp image processing**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.OpenCvSharp

OpenCvSharp4.runtime.win 
```

- **ONNX Runtime inference+ImageSharp image processing **

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.OpenCvSharp
```

- **ONNX Runtime(OpenVINO) inference+ImageSharp image processing**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.ImageSharp

Intel.ML.OnnxRuntime.OpenVino
```

- **ONNX Runtime(DML) inference+ImageSharp image processing**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.ImageSharp

Microsoft.ML.OnnxRuntime.DirectML
```

- **ONNX Runtime(CUDA) inference+ImageSharp image processing**

```shell
JYPPX.DeploySharp
JYPPX.DeploySharp.ImageSharp

Microsoft.ML.OnnxRuntime.DirectML
```

&emsp;    Due to the influence of GPU device model and software version on using CUDA to accelerate ONNX Runtime, it is necessary to download and use according to the official version correspondence provided by ONNX Runtime. Please refer to the following link for the correspondence between ONNX Runtime, CUDA, and cuDNN:

```
https://runtime.onnx.org.cn/docs/execution-providers/CUDA-ExecutionProvider.html#requirements
```

&emsp;    The usage methods listed above can all be installed with just one click through the VNet Package. Similarly, ONNX Runtime also supports more acceleration methods, but users need to build their own code. For the construction process and method, please refer to the official tutorial. The link is:

```
https://runtime.onnx.org.cn/docs/execution-providers/
```



## 🏷 Quick Start

&emsp;    If you don't know how to use it, use the following code to briefly understand how to use it.

### ImageSharp

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
			//The model and test images can be downloaded from the QQ group (945057948)
			//Replace the following model path with your own model path
            string modelPath = @"E:\Model\Yolo\yolov5s.onnx";
            //Replace the image path below with your own image path
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



### OpenCvSharp

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
			//The model and test images can be downloaded from the QQ group (945057948)
			//Replace the following model path with your own model path
            string modelPath = @"E:\Model\Yolo\yolov5s.onnx";
            //Replace the image path below with your own image path
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



## 💻 Use Cases

&emsp;    For more application cases, please refer to:

|    Type     |            Framework             |                             Link                             |
| :---------: | :------------------------------: | :----------------------------------------------------------: |
| Desktop App |        .NET Framework 4.8        | [DeploySharp.ImageSharp-ApplicationPlatform](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV1.0/applications/.NET%20Framework%204.8/DeploySharp.ImageSharp-ApplicationPlatform) |
| Desktop App |             .NET 6.0             | [DeploySharp.OpenCvSharp-ApplicationPlatform](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV1.0/applications/.NET%206.0/DeploySharp.OpenCvSharp-ApplicationPlatform) |
| Console App | .NET Framework 4.8、.NET 6.0-9.0 | [DeploySharp.samples](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV1.0/samples) |
| Desktop App |             .NET 8.0             | [JYPPX.DeploySharp.OpenCvSharp.PaddleOcr ](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV1.0/applications/.NET 8.0/JYPPX.DeploySharp.OpenCvSharp.PaddleOcr) |



## 🗂Documentation

&emsp;    Explore the full API: [DeploySharp API Documented](https://guojin-yan.github.io/DeploySharp.docs/index.html)

## 🎖 Contribution

&emsp;    If you are interested in using **Deploy Sharp** in C # and are interested in contributing to the open source community, please join us to develop **Deploy Sharp** together.

&emsp; If you have any ideas or improvement strategies for this project, please feel free to contact us for guidance on our work.

## <img title="" src="https://user-images.githubusercontent.com/48054808/157835345-f5d24128-abaf-4813-b793-d2e5bdc70e5a.png" alt="" width="40">  License

&emsp;    The release of this project is certified under the [Apache 2.0 license](https://github.com/guojin-yan/OpenVINO-CSharp-API/blob/csharp3.0/LICENSE.txt).

## 🧑‍🔧 Technical Support

&emsp;If you have any questions or suggestions, feel free to reach out via the following channels:

- 📧 **GitHub Issues**: Submit an Issue or Pull Request in the project repository.
- 💬 **QQ Group**: Join **945057948** for faster and more convenient responses.



## 📢Software Notice

**1. Open Source License Statement** 

All open source project code of the author follows the **Apache License 2.0** open source agreement.

**Special Note:** This project integrates several third-party libraries. If the license terms of any third-party library conflict with or are inconsistent with the Apache License 2.0, the original license terms of the specific third-party library shall prevail. This project does not include nor represent the authorization declarations of these third-party libraries. Please be sure to read and comply with the relevant licenses of the third-party libraries before use.

**2. Code Development and Quality Description**

- **AI-Assisted Development:** Artificial Intelligence (AI) was used to assist in the generation and optimization of this code; it was not written entirely line-by-line by a human.
- **Safety Commitment:** The author solemnly declares that there are absolutely no intentional backdoors, viruses, trojans, or malicious code designed to damage user equipment or steal data in this code.
- **Technical Limitations:** Due to the author's personal technical level and ability limitations, there may be rudimentary issues in the code caused by loose logic, insufficient optimization, or lack of experience (including but not limited to memory leaks, occasional crashes, unreleased resources, etc.). These issues are purely due to insufficient ability and are not subjective intent.
- **Testing Scope:** Due to the author's limited energy, comprehensive testing covering all edge scenarios has not been performed on this software.

**3. Disclaimer (Important)** 

Please perform detailed and rigorous self-testing and verification before applying this code to any actual project (especially commercial, industrial, or critical mission environments). In view of the potential code defects and insufficient test coverage mentioned above, the author assumes no responsibility for any direct or indirect losses caused by the use of this code (including but not limited to equipment failure, data loss, system paralysis, or loss of profits). Once you start using this code, it indicates that you are aware of the above risks and agree to bear all consequences yourself; related issues have nothing to do with the author.

**4. Open Source Scope** 

This project commits to fully open-sourcing the core logic code. However, the binary files, source code, or related resources of the "third-party libraries" mentioned above are not within the scope of this project's open-source obligation; please obtain them according to their respective guidelines.

**5. Community and Feedback** 

Despite the aforementioned shortcomings, we still welcome everyone to download, use, submit Issues, or participate in testing to improve the project together. If you discover bugs, memory overflows, or have suggestions for improvement during use, please contact the author via the contact information provided on the project homepage, and we will do our best to assist within our limited time.

![image-20250224211044113](https://ygj-images-container.oss-cn-nanjing.aliyuncs.com/BlogGallery/202502242110187.png)

