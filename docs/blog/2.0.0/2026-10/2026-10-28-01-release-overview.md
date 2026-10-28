# DeploySharp 2.0.0 正式发布：多后端、多模型，让 C# 模型部署更简单

> DeploySharp 2.0.0 系列 · 第 01 篇发布总览。基于源码 Tag `v2.0.0`，创建与修订：2026-10-28。

![DeploySharp 2.0.0 核心特色：多后端、多模型、简洁接入、OCR 与文档处理、并发与区域处理、模型按需获取](assets/01-release-overview/features-overview.png)

在 C# 应用里用上一个模型，通常要同时处理图像、推理引擎、输出解析和部署环境。模型多起来以后，分类、检测、OCR 又各有一套处理方式。

**DeploySharp 2.0.0 正式发布了。** 这一版围绕多后端、多模型和简洁接入，把常用视觉任务、OCR 与文档处理，以及批量、视频和区域处理组织到一套可组合的 .NET 类库中。

这篇先带大家认识项目：它适合做什么，2.0 有哪些值得关注的特色，各个模块怎样配合，以及接下来可以从哪里上手。完整的部署过程和代码，会在后续系列中按场景展开。

## 1. 项目介绍：面向 C#/.NET 应用的模型部署工具

DeploySharp 是一个开源的模型部署与推理类库，面向需要在 C#/.NET 软件中使用模型的开发者。应用可以在自己的进程里调用它，完成输入准备、模型执行和结果解析，再把结果接入界面或业务逻辑。

例如，工业视觉软件需要目标框、缺陷分数和区域结果；桌面工具需要识别图片里的文字；文档业务需要页面区域、表格和公式。DeploySharp 为这些任务提供配套处理，并把后端选择、模型获取和运行流程连接起来。

**2.0 的重点，是让“选模型、跑推理、拿结果、接业务”这几件事更容易组合。** 你可以先从一个 CPU 检测案例开始，再按实际需要加入 OCR、替换已验证的后端，或扩展自己的模型。

当前正式版的主要验证环境是 **Windows 10/11 x64**。具体模型与后端的可用范围分别记录在支持指南和验证矩阵中，文末提供入口；部署时还需准备所选后端的原生运行时。

## 2. 2.0.0 的核心特色

### 2.1 多后端：根据设备选引擎，让业务围绕任务展开

同一个视觉任务，可以根据模型和部署环境选择 ONNX Runtime、OpenVINO、TensorRT 或 OpenCV DNN。应用侧围绕图像与任务结果组织代码，模型执行交给显式选择的后端。

![C# 视觉应用通过 DeploySharp 的模型 Profile 和工作流，选择 ONNX Runtime、OpenVINO、TensorRT 或 OpenCV DNN](assets/01-release-overview/backend-workflow.png)

- **CPU 起步方便：**可以先跑通检测或 OCR，再评估目标设备上的加速方案。
- **GPU 路径可选：**支持已验证模型的 ONNX Runtime CUDA、TensorRT，以及相应的 CUDA 视觉处理路径。
- **依赖按需组合：**图像适配器与推理后端分开选择，例如 OpenCV 负责读图、ONNX Runtime 负责推理。

本地语言模型另有 LlamaSharp 适配器，当前已验证 Qwen2.5 GGUF 的 CPU 生成路径。它与图中的视觉张量推理属于不同任务入口。

各模型的后端范围分别记录，选型时可以按验证矩阵确认自己的模型与目标设备。

### 2.2 多模型：从常用视觉任务，延伸到 OCR 和文档处理

模型部署的工作量，常常集中在模型前后的处理。DeploySharp 为已接入模型提供配套的输入准备、输出解码和任务结果，让应用直接处理框、掩码、关键点、文字等信息。

| 你要完成的任务 | 当前已接入的代表模型 |
| --- | --- |
| 目标检测 | YOLOv5/v6/v7/v8/v9/v10、YOLO11/12/13/26 的具体型号；DEIMv2、RF-DETR、PP-YOLOE、RT-DETR |
| 分类、实例分割、关键点、旋转框 | YOLO 的相应任务工件，以及 RF-DETR Segment |
| 工业异常与背景移除 | PaDiM MVTec AD Bottle、BRIA RMBG 1.4/2.0 |
| 文字识别 | PP-OCRv4、v5、v6 的具体 DET/REC 与方向分类组合 |
| 版面、表格与文档区域 | PP-DocLayout、SLANeXt、UVDoc、PP-FormulaNet、UniMERNet、印章检测、PP-Chart2Table 等 |
| 提示分割、图文匹配与描述 | SAM ViT-B、CLIP ViT-B/32、BLIP Caption Base |
| 本地文本生成 | Qwen2.5 0.5B Instruct Q4_K_M |

这张表给出能力地图；精确型号、模型 ID 和状态在模型支持指南中列出。同一模型族的不同规模、导出图和后端，仍需要分别确认。

### 2.3 OCR 与文档：把多个模型组织成能使用的流程

OCR 需要把文字检测、文本行裁剪、方向处理、识别和结果合并串起来。PP-Structure 还会面对页面里的表格、公式、印章和图表。

2.0 提供这些阶段的任务接口与组合流程，并为识别阶段加入 Batch 和独立 Session 调度。

![PP-OCR 的检测、裁剪、可选方向、识别与合并；PP-Structure 按页面区域组合 OCR、表格、公式、印章和图表任务](assets/01-release-overview/ocr-document-workflow.png)

**对普通 OCR，**你可以选择 Mobile、Server，或 v6 的 Tiny/Small/Medium 等具体组合，获得文字、区域坐标和置信度。

**对文档处理，**可以按需要组合页面准备、版面定位和区域任务。仓库已有区域 OCR、有线表格到 HTML 等案例，后续文章会展开完整过程。

当前 v6 OCR 组合复用 v5 Mobile CLS；文档模块各自的后端范围独立。后续会分别用 OCR 和 PP-Structure 的完整案例，讲清模型选择、阶段组合和结果使用。

### 2.4 接入更直接，关键处理仍然由你控制

完成模型 Profile 和后端初始化后，核心检测调用可以保持得很短：

```csharp
using var input = new OpenCvVisualInputFactory()
    .CreateFromFile(imagePath, profile.VisualProfile);
var result = pipeline.Run(input).GetValue<DetectionResult>();

foreach (var item in result.Detections)
    Console.WriteLine($"{item.Label.Label}: {item.Label.Score:F3}");
```

这里的 `profile` 描述模型输入输出，`pipeline` 持有对应推理流程。片段展示的是初始化后的调用；包安装、模型下载、初始化和结果绘制会放到下一篇完整教程中。上述路径已经使用正式版 NuGet 包实际运行。

对于自己的模型，接入方式也保留了选择：

| 实际需求 | 可以配置或扩展的部分 |
| --- | --- |
| 模型训练时使用了不同缩放方式 | Resize / Letterbox / CenterCrop |
| 输入颜色、归一化不同 | 通道顺序、布局、均值与缩放等预处理选项 |
| 输出结构或业务结果不同 | 自定义 decoder、前处理和结果类型 |

常用模型直接使用配套处理，自有模型沿用同一套工作流扩展。自定义模型篇会用实际案例，展开这些配置与扩展方式。

### 2.5 面向连续帧与大图：把性能优化放进处理流程

真实应用还会遇到视频帧、批量图片和大尺寸图像。2.0 在这些场景中提供了相应的组织方式：

![视频在当前帧推理时预处理后续帧；大图通过重叠切片检测，统一还原源图坐标并合并结果](assets/01-release-overview/throughput-roi.png)

**批量与视频：**模型支持时使用真 Batch；多个任务可以通过独立 Session 池调度。视频帧支持有界异步预取，让后续帧准备与当前帧推理重叠，并保持结果顺序。

**ROI 与大图：**配置矩形、多边形等业务区域，或使用重叠滑动窗口检测大图，之后统一还原坐标、合并重复结果。

**TensorRT CUDA：**支持的路径可以在 GPU 上执行图像缩放、归一化和部分后处理，复用缓冲与 Stream，减少中间数据传输；其他处理保留 CPU 回退。

这些设计为应用留下了吞吐优化空间。实际收益取决于模型、输入和设备；项目文档记录了具名设备实测，可据此选择 Batch、通道与预取配置。

### 2.6 模型按需下载，也方便离线复用

公开模型已经按视觉、PaddleOCR、GGUF 等集合整理到 GitHub Release。可以选择单个模型或所需 Bundle，不必为了一个轻量模型下载整个集合。

ModelFactory 与专用下载器负责模型选择、大小和 SHA-256 校验、本地缓存与离线复用。模型、字典和 tokenizer 按配套清单获取，应用再准备所选后端的原生运行时。

对应用交付来说，同一套模型清单可以用于在线获取和离线准备，也更容易明确应用实际使用的是哪一份模型。

## 3. 模块导览：这些能力怎样配合

从使用角度看，2.0 可以分成下面几组。它们对应不同职责，使用时按任务选择需要的组合；其中 OCR 与文档工作流由视觉模块提供。

| 模块 | 在项目中负责什么 | 后续文章会展开什么 |
| --- | --- | --- |
| 推理基础与扩展：Core / Extensibility | 模型、张量、Session、结果和诊断；插件及运行时描述 | 随案例讲清初始化、资源管理和扩展接入 |
| 视觉任务：Visual 与图像适配器 | 图像准备、任务 Profile、输出解码及区域处理；支持的 TensorRT 路径另有 CUDA 视觉适配 | YOLO 入门、分类/分割/关键点/旋转框、自定义模型 |
| OCR 与文档工作流 | 组织文字识别、版面、表格、公式等阶段，输出对应任务结果 | PP-OCR 完整识别、PP-Structure 页面与表格案例 |
| 推理后端：Backend.* | 连接 ONNX Runtime、OpenVINO、OpenCV DNN、TensorRT 或 LlamaSharp | 后端选择、运行时准备和 TensorRT 优化 |
| 模型管理：ModelPack / ModelFactory | 描述模型工件、校验完整性、按需获取和缓存复用 | 模型下载、离线部署及清单使用 |
| 语言与多模态：LLM / Multimodal | 提供文本生成、Embedding 和多模态工作流接口 | 本地 Qwen2.5 GGUF 专题；其他模型按验证范围另行介绍 |

例如，一个 YOLO 检测应用可以由“模型获取 + 视觉处理 + 一个推理后端”组成；OCR 在视觉处理之上组织多个阶段；视频和大图再按需要加入异步预取、Session 调度或重叠切片。

这些能力可以逐步加入现有项目。语言与多模态的验证范围按具体模型标注，PaddleOCR-VL 等实验性路径另有说明。

## 4. 项目优势：让模型接入更贴近业务开发

**减少重复的模型接入工作。** 对于已接入的任务，常用输入准备、解码和坐标处理已有配套实现。工业视觉应用可以从检测框、掩码、关键点或异常结果继续开发；文档应用可以从文字、表格和区域结果继续组织业务。

**从原型到目标设备，有可以沿用的任务组织方式。** 可以先在 CPU 跑通一个案例，再针对已验证模型评估其他后端，把环境与执行配置的差异集中处理。批量图片、视频和大图也有相应入口，便于在业务逐步扩大时继续调整。

**自有模型和应用交付都有扩展空间。** 前后处理、结果类型和后端保持可配置、可扩展，模型获取、缓存与离线复用也有配套入口。项目同时提供源码、任务教程和模型案例，方便从一个可运行起点改成自己的应用。

如果你手里已经有模型，正在为 C# 桌面程序、产线检测工具或 OCR/文档系统补上推理能力，这些就是值得先试用的部分。

## 5. 从哪里开始，后续文章怎样展开

2.0.0 已发布到 **NuGet.org、GitHub Packages 和 GitHub Release**。建议先选一个明确任务，用相应案例跑通，再替换自己的输入、模型和配置。安装时只需选择该任务需要的包，模型与原生运行时按后端分别准备。

后续系列先写 **YOLOv8 完整部署、PaddleOCR 流水线、多后端与离线部署**，让读者完成从安装到真实结果的过程。接着展开视觉多任务、PP-Structure 和自定义模型，再进入并发、ROI、大图切片与 CUDA 优化，最后集中整理设备性能实测。

每篇会围绕一个完整场景展开，给出关键代码、真实输入输出和适用范围。当前这一篇负责项目全貌与能力导览，系列目录会随成稿持续更新。

> 升级提醒：V2 使用新的 API 设计，V1 项目需要迁移；Alpha 应用升级到正式版后需要重新编译。升级前请结合版本说明与迁移指南检查调用代码。

## 6. 项目、文档与交流入口

为了方便在博客中复制使用，下面直接列出网址。

**项目与正式版：**

```text
项目仓库：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0

2.0.0 Release：
https://github.com/guojin-yan/DeploySharp/releases/tag/v2.0.0

NuGet（以 Core 2.0.0 为例，其他包在项目 README 中列出）：
https://www.nuget.org/packages/JYPPX.DeploySharp.Core/2.0.0
```

**动手使用与模型选型：**

```text
使用教程：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/usage-tutorial.md

任务与模型案例：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/README.md

精确模型名单与支持状态：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/model-support.md

模型与后端验证矩阵：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/model-backend-verification-matrix.md

设备性能实测：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/device-performance-benchmarks.md
```

**系列文章与反馈：**

```text
2.0.0 博客系列目录：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/blog/2.0.0/README.md

问题反馈：
https://github.com/guojin-yan/DeploySharp/issues
```

如果你正在为 C# 软件加入模型能力，欢迎试用，也欢迎带着实际模型、设备和需求来交流。

DeploySharp 库代码采用 Apache License 2.0，第三方库与模型遵循各自许可。AI 辅助开发说明、软件声明及联系方式见项目 README。
