# DeploySharp 2.0.0 正式发布：多后端、多模型，让 C# 模型部署更简单

> DeploySharp 2.0.0 系列 · 第 01 篇发布总览。基于源码 Tag `v2.0.0`，创建与修订：2026-10-28。

![DeploySharp 2.0.0 核心特色：多后端、多模型、简洁接入、OCR 与文档处理、并发与区域处理、模型按需获取](assets/01-release-overview/features-overview.png)

使用 C# 做模型部署，除了让模型跑起来，还要处理图像缩放、归一化、输出解析、坐标还原，以及不同推理引擎的接口和运行环境。一个检测模型需要这些工作，换成 OCR，又要把检测、裁剪、方向分类和识别重新组织起来。

我开发 DeploySharp，就是希望把这些重复的接入工作整理好，让大家能够在熟悉的 C# 项目中，更方便地使用模型、选择推理引擎，并继续开发自己的业务功能。

**目前，DeploySharp 2.0.0 正式版已经发布！** 这一版带来了更完整的多模型、多引擎部署能力，扩展了 OCR 和文档处理流程，也加入了面向批量图片、视频和大图的处理功能。下面先带大家了解项目和本次更新的主要内容，具体部署案例会在后续文章中逐步展开。

## 一、DeploySharp 项目简介

**DeploySharp** 是一个专为 **C#/.NET 开发者**设计的模型部署框架，由「椒颜皮皮虾」开发并开源，项目源码遵循 **Apache License 2.0**。它基于 **ONNX Runtime、OpenVINO、TensorRT、OpenCV DNN** 等推理引擎，提供模型加载、参数配置、图像前处理、推理执行和结果后处理的配套接口，帮助开发者将模型接入自己的应用。

目前，项目已经接入了目标检测、图像分类、分割、姿态估计、旋转框、工业异常检测、背景移除等视觉任务，以及 **PP-OCRv4/v5/v6 文字识别和 PP-Structure 文档处理**；同时提供 SAM 提示分割、CLIP 图文匹配、BLIP 图像描述和 Qwen2.5 本地文本生成的相应模型入口。

**多模型、多引擎、简洁调用和灵活扩展，是这个项目的主要特点。** 常用模型可以直接使用配套前后处理；自己的模型也可以自定义输入处理、输出解码和结果类型。模型按需下载、批量推理、多推理器调度、异步预取、ROI 和大图切片，则为实际项目提供了进一步组合与优化的空间。

**项目地址：**

```text
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0
```

**NuGet 包：**搜索 `JYPPX.DeploySharp`，按任务选择所需组件。

```text
https://www.nuget.org/packages?q=JYPPX.DeploySharp
```

当前 2.0.0 正式版主要验证 **Windows 10/11 x64**；新项目建议使用 .NET 10，也可根据现有工程选择 .NET 8 等兼容目标框架。不同组件、模型和后端的支持范围有所差异，后文提供详细文档与验证矩阵入口。

本系列也提供 **Ubuntu 22.04 x64 上的 PP-OCRv5 Mobile 完整流程与多后端实跑**，展示同一份 C# 基础代码在不同系统中的使用方式。跨平台组合以对应教程和验证矩阵中的具体记录为准。

## 二、DeploySharp 2.0.0 核心特性

### 2.1 多推理引擎支持，按设备选择部署方案

DeploySharp 将视觉任务与推理引擎分开组织。对于已验证的模型，可以根据设备和部署环境选择 ONNX Runtime、OpenVINO、TensorRT 或 OpenCV DNN，应用代码继续围绕图像和任务结果展开。

![C# 视觉应用通过 DeploySharp 的模型 Profile 和工作流，选择 ONNX Runtime、OpenVINO、TensorRT 或 OpenCV DNN](assets/01-release-overview/backend-workflow.png)

- **CPU 起步方便：**可以先跑通检测或 OCR，再评估目标设备上的加速方案。
- **GPU 路径可选：**支持已验证模型的 ONNX Runtime CUDA、TensorRT，以及相应的 CUDA 视觉处理路径。
- **依赖按需组合：**图像适配器与推理后端分开选择，例如 OpenCV 负责读图、ONNX Runtime 负责推理。

本地语言模型使用单独的 LlamaSharp 适配器，目前已验证 Qwen2.5 GGUF 的 CPU 文本生成。图中四种引擎主要面向视觉模型部署。

各模型的后端范围分别记录，选型时可以按验证矩阵确认自己的模型与目标设备。

### 2.2 多种模型支持，覆盖视觉、OCR 与文档处理

模型部署的工作量，常常集中在模型前后的处理。DeploySharp 为已接入模型提供配套的输入准备、输出解码和任务结果，让应用直接处理框、掩码、关键点、文字等信息。

| 你要完成的任务 | 当前已接入的代表模型 |
| --- | --- |
| 目标检测 | YOLO26n，以及 YOLOv5/v6/v7/v8/v9/v10、YOLO11/12/13 的具体型号；DEIMv2、RF-DETR、PP-YOLOE、RT-DETR |
| 分类、实例分割、关键点、旋转框 | YOLO 的相应任务模型，以及 RF-DETR Segment |
| 工业异常与背景移除 | PaDiM MVTec AD Bottle、BRIA RMBG 1.4/2.0 |
| 文字识别 | PP-OCRv4、v5、v6 的具体 DET/REC 与方向分类组合 |
| 版面、表格与文档区域 | PP-DocLayout、SLANeXt、UVDoc、PP-FormulaNet、UniMERNet、印章检测、PP-Chart2Table 等 |
| 提示分割、图文匹配与描述 | SAM ViT-B、CLIP ViT-B/32、BLIP Caption Base |
| 本地文本生成 | Qwen2.5 0.5B Instruct Q4_K_M |

上表列出代表模型，精确型号、模型 ID 和支持状态在模型支持指南中列出。同一系列的不同型号、导出图和后端，仍需要分别确认。

### 2.3 OCR 与文档处理，从单个模型到完整流程

OCR 需要把文字检测、文本行裁剪、方向处理、识别和结果合并串起来。PP-Structure 还会面对页面里的表格、公式、印章和图表。

2.0 提供这些阶段的任务接口与组合流程，并为识别阶段加入 Batch 和独立 Session 调度。

![PP-OCR 的检测、裁剪、可选方向、识别与合并；PP-Structure 按页面区域组合 OCR、表格、公式、印章和图表任务](assets/01-release-overview/ocr-document-workflow.png)

**对普通 OCR，**你可以选择 Mobile、Server，或 v6 的 Tiny/Small/Medium 等具体组合，获得文字、区域坐标和置信度。

**对文档处理，**可以按需要组合页面准备、版面定位和区域任务。仓库已有区域 OCR、有线表格到 HTML 等案例，后续文章会展开完整过程。

第三篇已经用一张包含大标题、多栏正文、照片和图注的中文报纸页面，展示了完整 OCR、文字框绘制和 TXT/JSON 导出。通用 OCR 提供逐区域文字；跨栏阅读顺序和标题、正文等区域划分，需要进一步结合版面分析。

当前 v6 OCR 组合复用 v5 Mobile CLS；文档模块各自的后端范围独立。PP-Structure 专题会在此基础上继续展开区域 OCR、表格与其他文档任务。

### 2.4 简洁的调用方式，保留自定义扩展能力

以 YOLO26 检测为例，完成模型 Profile 和后端初始化后，核心调用可以保持得很短：

```csharp
using var input = new OpenCvVisualInputFactory()
    .CreateFromFile(imagePath, profile);
var result = pipeline.Run(input).GetValue<DetectionResult>();

foreach (var item in result.Detections)
    Console.WriteLine($"{item.Label.Label}: {item.Label.Score:F3}");
```

这里的 `profile` 是描述输入输出与前后处理的 `VisualModelProfile`，`pipeline` 持有对应推理流程。片段只展示初始化后的调用；第二篇 YOLO26 实战提供包安装、模型准备、初始化和结果绘制的完整代码，并使用正式版 NuGet 包完成了 Windows 多后端对照。

对于自己的模型，接入方式也保留了选择：

| 实际需求 | 可以配置或扩展的部分 |
| --- | --- |
| 模型训练时使用了不同缩放方式 | Resize / Letterbox / CenterCrop |
| 输入颜色、归一化不同 | 通道顺序、布局、均值与缩放等预处理选项 |
| 输出结构或业务结果不同 | 自定义 decoder、前处理和结果类型 |

常用模型直接使用配套处理，自有模型沿用同一套工作流扩展。自定义模型篇会用实际案例，展开这些配置与扩展方式。

### 2.5 批量、视频与大图处理，兼顾实际应用需求

真实应用还会遇到视频帧、批量图片和大尺寸图像。2.0 在这些场景中提供了相应的组织方式：

![视频在当前帧推理时预处理后续帧；大图通过重叠切片检测，统一还原源图坐标并合并结果](assets/01-release-overview/throughput-roi.png)

**批量与视频：**模型支持时使用真 Batch；多个任务可以通过独立 Session 池调度。视频帧支持有界异步预取，让后续帧准备与当前帧推理重叠，并保持结果顺序。

**ROI 与大图：**配置矩形、多边形等业务区域，或使用重叠滑动窗口检测大图，之后统一还原坐标、合并重复结果。

**TensorRT CUDA：**支持的路径可以在 GPU 上执行图像缩放、归一化和部分后处理，复用缓冲与 Stream，减少中间数据传输；其他处理保留 CPU 回退。

这些功能可以用于批量任务和连续帧处理。调优时应同时检查完整耗时、结果质量和资源占用，Batch 与通道数量需要按模型、输入和设备选择。第三篇先建立逐行 OCR 基线，再解释调度参数；设备实测文档另有相应组合的结果可供参考。

### 2.6 模型按需下载，支持缓存与离线复用

公开模型已经按视觉、PaddleOCR、GGUF 等集合整理到 GitHub Release。可以选择单个模型或所需 Bundle，不必为了一个轻量模型下载整个集合。

需要在线获取公开模型时，可以使用 ModelFactory 与专用下载器，按配套清单准备模型、字典和 tokenizer，并复用本地缓存。应用再准备所选后端的原生运行时。

**已有本地模型或自己训练的模型，也可以直接通过文件路径加载。** 配置好类别、输入规格和对应的前后处理后，就能接入同一套推理流程；第二篇提供了图片、模型和类别文件的替换方法。

## 三、模块组成与应用场景

DeploySharp 2.0 采用模块化设计，核心接口、视觉处理、推理后端和模型管理分别组织，使用时按任务选择需要的包。主要模块如下，其中 OCR 与文档流程由视觉模块提供。

| 模块 | 主要功能 | 典型应用 |
| --- | --- | --- |
| 推理基础与扩展：Core / Extensibility | 模型、张量、推理会话、结果和诊断；插件及运行时描述 | 自定义推理任务、运行环境检查与扩展接入 |
| 视觉任务：Visual 与图像适配器 | 图像准备、任务配置、输出解码及区域处理；支持的 TensorRT 路径另有 CUDA 视觉适配 | 目标检测、缺陷检测、图像分割、视频与大图分析 |
| OCR 与文档工作流 | 组合文字检测、识别、版面、表格、公式等阶段 | 图片文字提取、页面区域处理、表格 HTML 输出 |
| 推理后端：Backend.* | 连接 ONNX Runtime、OpenVINO、OpenCV DNN、TensorRT 或 LlamaSharp | 根据模型和设备选择执行引擎 |
| 模型管理：ModelPack / ModelFactory | 描述模型文件、校验完整性、按需获取和缓存复用 | 在线下载模型、准备离线模型目录 |
| 语言与多模态：LLM / Multimodal | 提供文本生成、向量表示和多模态工作流接口 | 当前已验证的本地 Qwen2.5 GGUF 生成；其他组合按模型说明选择 |

例如，一个 YOLO 检测应用可以由“模型获取 + 视觉处理 + 一个推理后端”组成；OCR 在视觉处理之上组织多个阶段；视频和大图再按需要加入异步预取、Session 调度或重叠切片。

这些能力可以逐步加入现有项目。语言与多模态的验证范围按具体模型标注，PaddleOCR-VL 等实验性路径另有说明。

## 四、为什么选择 DeploySharp

对于使用 C# 开发桌面软件、工业视觉或文档应用的朋友，DeploySharp 主要提供以下几方面便利：

- **接入方便，减少重复开发。** 已接入模型提供配套前后处理，应用可以直接使用检测框、掩码、关键点和文字等结果，把精力放在界面与业务功能上。
- **多种引擎可选，便于适配设备。** 可以先用 CPU 跑通任务，再根据模型支持情况评估 OpenVINO、ONNX Runtime CUDA 或 TensorRT；批量和视频任务也有相应的调度方式。
- **配置灵活，自己的模型也能接。** 缩放、颜色、归一化、输出解码和结果类型都保留扩展入口，方便适配训练方式不同的模型和业务数据。
- **开源代码与使用资料配套提供。** 源码、模型下载入口、教程和案例集中在仓库中，既能从现有模型开始，也方便查看实现、修改代码和准备离线部署。

如果你正在为自己的 C# 项目加入模型能力，可以先选一个对应案例运行，再逐步替换成实际模型和业务输入。

## 五、软件获取与使用教程

### 5.1 NuGet 安装与版本获取

DeploySharp 2.0.0 已发布到 **NuGet.org、GitHub Packages 和 GitHub Release**。在 Visual Studio 的 NuGet 包管理器中搜索 `JYPPX.DeploySharp`，根据需要安装对应包即可；常见组合如下：

| 使用需求 | 主要组件（省略共同前缀 `JYPPX.DeploySharp.`） |
| --- | --- |
| 使用 OpenCV 读图，ONNX Runtime 执行视觉推理 | `Visual.OpenCV` + `Backend.OnnxRuntime` |
| 使用 OpenVINO 或 OpenCV DNN 执行视觉推理 | `Visual.OpenCV` + `Backend.OpenVINO` 或 `Backend.OpenCV` |
| 使用 OpenCV 处理图像，TensorRT 执行模型 | `Visual.OpenCV` + `Backend.TensorRT` |
| 使用 TensorRT CUDA 图像处理与专用视觉流程 | `Visual.TensorRT` + `Backend.TensorRT`，按输入来源选择图像适配器 |
| 按模型目录下载并缓存模型 | 需要自动下载时，再加入 `ModelFactory`；已有本地模型可直接加载 |

以上是主要组件组合，基础包依赖由 NuGet 解析。所有 DeploySharp 包请保持同为 `2.0.0`，再按后端准备对应的 OpenCV、ONNX Runtime、OpenVINO 或 CUDA/TensorRT 等原生运行时。

**`Visual.OpenCV` 负责图像输入与前处理，`Backend.OpenCV` 负责 OpenCV DNN 推理**；使用 ONNX Runtime 推理时，也可以用 OpenCV 处理图片。第二篇按 CPU、CUDA、OpenVINO、OpenCV DNN 和 TensorRT 等场景列出安装组合；通用依赖说明如下：

```text
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/package-combinations.md
```

**正式版下载与完整更新说明：**

```text
https://github.com/guojin-yan/DeploySharp/releases/tag/v2.0.0
```

> 从旧版本升级时需要注意：V2 重新设计了 API，V1 项目需要迁移，Alpha 应用升级到正式版后需要重新编译。请先阅读仓库中的版本说明与迁移指南，再调整现有调用。

### 5.2 模型、案例与使用文档

目标检测可以从**第二篇 YOLO26 入门实战**开始，文字识别可以直接看**第三篇 PaddleOCR 完整流水线**。两篇都使用正式版 NuGet，提供可运行代码、真实结果和按后端选择的运行配置：

| 实战案例 | 本系列已运行的环境与路径 | 实际展示 |
| --- | --- | --- |
| YOLO26n 目标检测 | Windows：ORT CPU/CUDA、OpenVINO CPU、OpenCV DNN CPU、TensorRT CUDA | 4 个行人、1 辆公交车；坐标还原、结果绘制与后端一致性对照 |
| PP-OCRv5 Mobile 完整识别 | Windows 与 Ubuntu 22.04；Ubuntu 对照 ORT CPU、OpenVINO CPU、OpenCV DNN CPU、TensorRT CUDA | 复杂中文报纸页面的 144 个文字候选区域，保存 TXT、JSON 和文字框图片 |

第二篇还说明如何替换自己的模型、图片和类别文件，并提供本机 ORT CUDA 使用的同版本 CUDA 12 运行库配置。第三篇另外在 Windows 切换运行了 v4 Mobile、v6 Tiny，说明字典配对、Batch 和独立推理通道。各篇保留具体设备、输入与计时范围，便于大家复现。

```text
YOLO26 入门实战（第二篇）：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/blog/2.0.0/2026-10/2026-10-28-02-yolo26-quickstart.md

PaddleOCR v4/v5/v6 完整流水线（第三篇）：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/blog/2.0.0/2026-10/2026-10-28-03-paddleocr-pipeline.md
```

仓库也按功能和具体模型整理了其他使用案例；模型支持指南列出精确型号，验证矩阵说明各模型在哪些后端完成了运行验证。

```text
使用教程：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/usage-tutorial.md

模型案例：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/README.md

模型支持指南：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/model-support.md

模型与后端验证矩阵：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/model-backend-verification-matrix.md
```

不同设备上的推理时间，以及 OCR 的 Batch 和推理通道配置，单独整理在设备性能实测文档中，便于大家选择方案和复测：

```text
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/device-performance-benchmarks.md
```

## 六、总结与后续计划

DeploySharp 2.0.0 把常用模型的部署、多个推理引擎的接入，以及 OCR、文档、视频和大图处理整理到了一套可组合的 C# 类库中。对于已经接入的模型，大家可以使用现成处理流程；有自己的模型和业务需求，也可以继续扩展。

这篇先把新版的主要功能介绍给大家。第二、第三篇已经给出了 YOLO26 检测和 PaddleOCR 完整识别的实战，并展示 Windows 多后端与 Ubuntu OCR 部署。后续会在这些案例基础上，继续展开以下几组专题：

- **视觉模型部署：**以 YOLO26 检测为起点，再讲分类、分割、关键点和旋转框，完成从模型获取到结果绘制的过程。
- **OCR 与文档处理：**承接第三篇的完整识别流程，进一步讲解 PP-Structure 的版面分析、区域 OCR、表格识别与 HTML 输出。
- **自定义模型与区域处理：**接入自己的前后处理和结果类型，配置 ROI，并用重叠切片处理大图中的小目标。
- **多引擎部署与性能优化：**展开运行环境、离线部署、Batch、多推理器、视频异步预取和 TensorRT CUDA 优化，结合设备实测解释参数选择。

具体文章会随完成情况更新到系列目录，大家可以根据自己的需求选择阅读：

```text
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/blog/2.0.0/README.md
```

欢迎大家下载使用、Star 或 Fork 项目，也欢迎在使用中提交问题和改进建议。反馈时请附上模型名称、后端、运行环境和最小复现代码，方便定位与修复。

**问题反馈：**

```text
https://github.com/guojin-yan/DeploySharp/issues
```

DeploySharp 库代码采用 Apache License 2.0，第三方库与模型遵循各自许可。AI 辅助开发说明、软件声明及联系方式见项目 README。
