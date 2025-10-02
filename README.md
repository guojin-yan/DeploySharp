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

# 🎨Supported Models

|  Model Name  |       Model Type        | OpenVINO | ONNX Runtime | TensorRT |
| :----------: | :---------------------: | :------: | :----------: | :------: |
|  **YOLOv5**  |        Detection        |    ✅     |      ✅       |  ing...  |
|  **YOLOv5**  |      Segmentation       |    ✅     |      ✅       |  ing...  |
|  **YOLOv6**  |        Detection        |    ✅     |      ✅       |  ing...  |
|  **YOLOv7**  |        Detection        |    ✅     |      ✅       |  ing...  |
|  **YOLOv8**  |        Detection        |    ✅     |      ✅       |  ing...  |
|  **YOLOv8**  |      Segmentation       |    ✅     |      ✅       |  ing...  |
|  **YOLOv8**  |          Pose           |    ✅     |      ✅       |  ing...  |
|  **YOLOv8**  | Oriented Bounding Boxes |    ✅     |      ✅       |  ing...  |
|  **YOLOv9**  |        Detection        |    ✅     |      ✅       |  ing...  |
|  **YOLOv9**  |      Segmentation       |    ✅     |      ✅       |  ing...  |
| **YOLOv10**  |        Detection        |    ✅     |      ✅       |  ing...  |
| **YOLOv11**  |        Detection        |    ✅     |      ✅       |  ing...  |
| **YOLOv11**  |      Segmentation       |    ✅     |      ✅       |  ing...  |
| **YOLOv11**  |          Pose           |    ✅     |      ✅       |  ing...  |
| **YOLOv11**  | Oriented Bounding Boxes |    ✅     |      ✅       |  ing...  |
| **YOLOv12**  |        Detection        |    ✅     |      ✅       |  ing...  |
| **Anomalib** |      Segmentation       |    ✅     |      ✅       |  ing...  |


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

## 🗂Documentation

&emsp;    Explore the full API: [DeploySharp API Documented](https://guojin-yan.github.io/DeploySharp.docs/index.html)

## 🎖 Contribution

&emsp;    If you are interested in using **Deploy Sharp** in C # and are interested in contributing to the open source community, please join us to develop **Deploy Sharp** together.

&emsp; If you have any ideas or improvement strategies for this project, please feel free to contact us for guidance on our work.

## <img title="" src="https://user-images.githubusercontent.com/48054808/157835345-f5d24128-abaf-4813-b793-d2e5bdc70e5a.png" alt="" width="40">  License

&emsp;    The release of this project is certified under the [Apache 2.0 license](https://github.com/guojin-yan/OpenVINO-CSharp-API/blob/csharp3.0/LICENSE.txt).

&emsp;    Finally, if any developers have any questions during use, please feel free to contact me.

![image-20250224211044113](https://ygj-images-container.oss-cn-nanjing.aliyuncs.com/BlogGallery/202502242110187.png)

