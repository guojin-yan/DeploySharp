<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/readme/hero-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/images/readme/hero-light.svg">
  <img alt="DeploySharp - 面向 .NET 的可复现 AI 推理工作流" src="docs/images/readme/hero-light.svg" width="100%">
</picture>

<p align="center">
  面向 .NET 的模块化模型部署工具，为视觉、语言和多模态模型提供可复现、可替换后端的推理工作流。
</p>

<p align="center">
  <a href="https://github.com/guojin-yan/DeploySharp/actions/workflows/ci.yml?query=branch%3ADeploySharpV2.0"><img src="https://github.com/guojin-yan/DeploySharp/actions/workflows/ci.yml/badge.svg?branch=DeploySharpV2.0" alt="Windows CI" /></a>
  <a href="https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/LICENSE.txt"><img src="https://img.shields.io/badge/License-Apache%202.0-blue.svg" alt="Apache-2.0 许可证" /></a>
  <a href="https://github.com/guojin-yan/DeploySharp/stargazers"><img src="https://img.shields.io/github/stars/guojin-yan/DeploySharp?style=flat&amp;label=stars" alt="GitHub Stars" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-net46%20to%20net10.0-512BD4" alt=".NET Framework 4.6 至 .NET 10" /></a>
  <a href="docs/articles/platform-support.md"><img src="https://img.shields.io/badge/platform-Windows%20x64%20Alpha-0078D4" alt="Windows x64 Alpha" /></a>
</p>

<p align="center">
  <a href="docs/index.md"><img src="https://img.shields.io/badge/docs-DocFX-2f80ed" alt="DocFX 文档" /></a>
  <a href="docs/articles/release-2.0.0-alpha.1.md"><img src="https://img.shields.io/badge/release-2.0.0--alpha.1-f59e0b" alt="DeploySharp 2.0.0-alpha.1" /></a>
  <a href="https://github.com/guojin-yan/DeploySharp/releases"><img src="https://img.shields.io/github/v/release/guojin-yan/DeploySharp?include_prereleases&amp;label=GitHub%20Release" alt="GitHub Release" /></a>
</p>

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

# DeploySharp

DeploySharp V2 为模型工件、类型化张量、Session、视觉流程、语言/多模态工作流、ModelPack 完整性、ModelFactory 获取和可替换推理后端提供明确契约。应用负责模型文件和原生运行时，DeploySharp 让后端选择与执行边界保持可见。

## 📖 项目介绍

项目围绕四个实际边界设计：

- **稳定的应用契约：**模型身份、类型化张量、命名输入/输出、Session、诊断、取消和释放。
- **完整推理工作流：**分类、检测、分割、姿态、OBB、OCR、异常、提示分割、视觉语言、LLM 和多模态路径。
- **显式后端所有权：**ONNX Runtime、OpenVINO、OpenCV DNN、TensorRT/CUDA 和 LLamaSharp 适配器，不会静默安装全部厂商运行时。
- **可复现模型交付：**ModelPack 清单、工件大小/SHA-256 校验、不可变 Release 下载、离线缓存复用，以及每个目录模型一份可运行案例。

V2 是全新 API 设计，不提供 V1 的源码、二进制、配置或行为兼容。

## ✨ 版本亮点

- Core、Visual、LLM、Multimodal、ModelPack、ModelFactory、五类后端和七个分组示例模块。
- 42 个 Preview 目录条目、43 个工件变体，以及生成的模型/后端验证矩阵。
- Windows x64 上已完成 ONNX Runtime、OpenVINO 和 OpenCV DNN CPU 验证；RTX 3060 上留存 TensorRT 11 + CUDA 12.9 本机证据。
- 可复现的跨后端速度案例，记录热推理延迟、P50/P95、吞吐、托管分配和环境信息。
- 双语 API 文档和 DocFX 站点，开发/审计历史与用户文档入口分离。

## 📢 当前更新：2.0.0-alpha.1

<code>2.0.0-alpha.1</code> 是 DeploySharp V2 的首次工程预览版。目前以 Windows 10/11 x64 源码复现为主，公共 API 和包边界仍会继续调整。

完整的首发变更、验证快照、已知边界和复现命令见 [2.0.0-alpha.1 发布说明](docs/articles/release-2.0.0-alpha.1.md)。后续版本将新增独立的详细版本文档，主页只保留摘要。

## 🚀 30 秒开始

### 1. 安装包

Alpha 包当前作为带版本号的 Release 候选在本地构建。包源可用时，按同一版本安装 Core 和所需后端；源码复现时直接使用本仓库项目引用。

~~~powershell
dotnet add package JYPPX.DeploySharp.Core --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
~~~

原生运行时由应用显式负责。OpenCV DNN 和 OpenVINO 需要匹配目标 RID 的原生运行时包；TensorRT 需要用户安装 TensorRT/CUDA/cuDNN 环境。

### 2. 编写几行 C# 代码

创建模型工件、注册 ONNX Runtime、建立命名张量 Session，并执行一次类型化输入：

~~~csharp
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;

using var backends = new BackendRegistry();
backends.UseOnnxRuntime();

var artifact = new ModelArtifact(
    new ModelId("examples/classifier"),
    "onnx",
    @"models\classifier.onnx",
    preferredBackend: OnnxRuntimeBackendProvider.BackendId);
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId,
    "cpu");
using IInferenceSession session = backends.CreateSession(
    artifact, request, SessionOptions.Default);

var input = new Tensor<float>(
    new TensorShape(1, 3),
    new[] { 0.1f, 0.2f, 0.7f });
InferenceOutputs outputs = session.Run(
    InferenceInputs.Create("images", input),
    CancellationToken.None);
Console.WriteLine(outputs.Count);
~~~

完整的代码路径、视觉准备、ModelFactory 下载流程和模型案例见[使用教程](docs/articles/usage-tutorial.md)及 [samples](samples/README.md)。

## 📦 包结构

| 包族 | 内容 | 原生运行时所有权 |
| --- | --- | --- |
| <code>JYPPX.DeploySharp.Core</code> | 模型、张量、Session、结果、诊断、后端注册 | 无 |
| <code>JYPPX.DeploySharp.Visual</code> | 视觉 Profile、预处理元数据、解码器、规范化结果 | 无 |
| <code>JYPPX.DeploySharp.Visual.OpenCV</code> | OpenCV 图像读取和张量准备 | 应用选择 OpenCV runtime |
| <code>JYPPX.DeploySharp.LLM</code> / <code>Multimodal</code> | 生成、对话、Embedding、有序媒体、流式 | 应用选择模型运行时 |
| <code>JYPPX.DeploySharp.ModelPack.Json</code> / <code>ModelFactory</code> | 清单、完整性校验、目录下载、离线缓存 | 无，模型文件由应用持有 |
| <code>JYPPX.DeploySharp.Backend.*</code> | ONNX Runtime、OpenVINO、OpenCV DNN、TensorRT、LLamaSharp 适配器 | 按后端显式选择 |

## 🌐 公共包与 Release 资产

当前 DeploySharp 包尚未发布到 nuget.org。这里保留包 ID 和精确的 Alpha 候选版本，确保第一次发布时无需修改应用引用。

| 包 | 版本 | NuGet.org | GitHub Packages | 用途 |
| --- | --- | --- | --- | --- |
| <code>JYPPX.DeploySharp.Core</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Core) | 未发布 | Core 契约和后端注册 |
| <code>JYPPX.DeploySharp.Visual</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Visual) | 未发布 | 视觉 Profile、预处理和解码器 |
| <code>JYPPX.DeploySharp.Visual.OpenCV</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Visual.OpenCV) | 未发布 | OpenCV 图像准备 |
| <code>JYPPX.DeploySharp.LLM</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.LLM) | 未发布 | LLM 生成与 Embedding 契约 |
| <code>JYPPX.DeploySharp.Multimodal</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Multimodal) | 未发布 | 有序多模态编排 |
| <code>JYPPX.DeploySharp.ModelPack.Json</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.ModelPack.Json) | 未发布 | 模型清单和完整性校验 |
| <code>JYPPX.DeploySharp.ModelFactory</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.ModelFactory) | 未发布 | 目录选择、下载、缓存和离线复用 |
| <code>JYPPX.DeploySharp.Backend.OnnxRuntime</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.OnnxRuntime) | 未发布 | ONNX Runtime 命名张量适配器 |
| <code>JYPPX.DeploySharp.Backend.OpenVINO</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.OpenVINO) | 未发布 | OpenVINO 命名张量适配器 |
| <code>JYPPX.DeploySharp.Backend.OpenCV</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.OpenCV) | 未发布 | OpenCV DNN 适配器 |
| <code>JYPPX.DeploySharp.Backend.TensorRT</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.TensorRT) | 未发布 | TensorRT 推理和 ONNX 转 Engine 边界 |
| <code>JYPPX.DeploySharp.Backend.LlamaSharp</code> | <code>2.0.0-alpha.1</code> | 未发布；[搜索](https://www.nuget.org/packages?q=JYPPX.DeploySharp.Backend.LlamaSharp) | 未发布 | LLamaSharp GGUF 生成和 Embedding |

| 发布渠道 | 当前状态 | 资产 |
| --- | --- | --- |
| [NuGet.org](https://www.nuget.org/) | DeploySharp 包尚未发布 | 后续托管包源 |
| [GitHub Packages](https://github.com/guojin-yan/DeploySharp/packages) | DeploySharp 包尚未发布 | 后续包镜像 |
| [GitHub Releases](https://github.com/guojin-yan/DeploySharp/releases) | 当前用于模型工件交付 | 不可变 ModelPack 资产和验证元数据 |

### 应用负责的运行时包

下面是当前 Windows Alpha 使用的依赖/运行时包。它们不会由 DeploySharp 托管契约静默安装：

| 包 | 版本 | 作用 |
| --- | --- | --- |
| [Microsoft.ML.OnnxRuntime](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/) | [![NuGet version](https://img.shields.io/nuget/v/Microsoft.ML.OnnxRuntime.svg?label=version)](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/) | ONNX Runtime CPU 原生执行 |
| [JYPPX.OpenCV.runtime.win-x64](https://www.nuget.org/packages/JYPPX.OpenCV.runtime.win-x64/) | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.OpenCV.runtime.win-x64.svg?label=version)](https://www.nuget.org/packages/JYPPX.OpenCV.runtime.win-x64/) | Windows x64 OpenCV 原生运行时 |
| [OpenVINO.runtime.win](https://www.nuget.org/packages/OpenVINO.runtime.win/) | [![NuGet version](https://img.shields.io/nuget/v/OpenVINO.runtime.win.svg?label=version)](https://www.nuget.org/packages/OpenVINO.runtime.win/) | Windows OpenVINO 原生运行时 |
| [JYPPX.TensorRT.CSharp.API](https://www.nuget.org/packages/JYPPX.TensorRT.CSharp.API/) | [![NuGet version](https://img.shields.io/nuget/v/JYPPX.TensorRT.CSharp.API.svg?label=version)](https://www.nuget.org/packages/JYPPX.TensorRT.CSharp.API/) | 托管 TensorRT/CUDA API；NVIDIA 库仍由用户安装 |
| [LLamaSharp.Backend.Cpu](https://www.nuget.org/packages/LLamaSharp.Backend.Cpu/) | [![NuGet version](https://img.shields.io/nuget/v/LLamaSharp.Backend.Cpu.svg?label=version)](https://www.nuget.org/packages/LLamaSharp.Backend.Cpu/) | LLamaSharp GGUF 工作流的 CPU 原生后端 |

托管包表格不代表自动安装原生依赖。选择部署 RID 前请阅读[安装和运行时所有权说明](docs/articles/installation.md)。

## 🖥️ 平台与目标框架

| 平台 | 构建/包边界 | 推理验证 | 原生运行时/包 |
| --- | --- | --- | --- |
| Windows 10 x64 | Alpha 支持 | ONNX Runtime、OpenVINO、OpenCV DNN CPU；TensorRT GPU 本机证据 | 已验证 Windows 包和本机 NVIDIA 环境 |
| Windows 11 x64 | Alpha 支持 | 与 Windows 10 x64 使用同一代码路径 | 匹配的 Windows x64 runtime 包 |
| Windows ARM64 | 仅构建范围 | 未测试 | 暂缓 |
| Linux x64/ARM64 | 托管源码可能构建 | 本 Alpha 未测试 | 暂缓；启用时安装匹配厂商 runtime |
| macOS x64/ARM64 | 托管源码可能构建 | 本 Alpha 未测试 | 暂缓 |
| Android/iOS/NPU | 无发布声明 | 未测试 | 暂缓 |

完整框架列表和后端证据见[平台与后端支持](docs/articles/platform-support.md)。可构建不等于已完成推理验证。

## 🤖 支持的模型

首个目录包含 42 个 Preview 条目和 43 个工件变体：

| 模型族 | 条目数 | 当前范围 |
| --- | ---: | --- |
| YOLO v5-v13/v26 | 22 | 检测、分类、分割、姿态和 OBB |
| DETR 系列 | 8 | DEIMv2、PP-YOLOE、RF-DETR 和 RT-DETR 变体 |
| PP-OCRv5 | 6 | Mobile/Server 分类、检测和识别 |
| Anomalib / BRIA | 3 条目 / 4 工件 | PaDiM、RMBG 1.4、RMBG 2.0 fp32/dynamic-int8 |
| 视觉语言 / 分割 / LLM | 4 | CLIP、BLIP、SAM 和 Qwen GGUF |

全部目录 ID 见[模型支持指南](docs/articles/model-support.md)，每个工件的当前状态见[43 工件模型/后端矩阵](docs/model-backend-verification-matrix.md)。

## 🧪 示例系列

示例按完整工作流组织，而不是一个方法一个示例：

| 模块 | 演示内容 |
| --- | --- |
| <code>01-core</code> | 后端无关的模型/张量生命周期 |
| <code>02-visual</code> | 视觉 Profile、预处理元数据、解码器所有权 |
| <code>03-backends</code> | OpenCV DNN 原生加载和命名张量执行 |
| <code>04-multimodal</code> | 有序媒体、流式、取消和清理 |
| <code>05-llm</code> | 对话历史和提示词格式化 |
| <code>06-models</code> | 目录选择、模型案例、Release 下载/推理 |
| <code>07-benchmarks</code> | 同模型后端/平台延迟和吞吐 |

详见[示例学习路径](samples/README.md)。速度测试器可写出 JSON 报告，并显式记录不可用的原生运行时。

## 📚 文档

| 资源 | 链接 | 说明 |
| --- | --- | --- |
| 文档索引 | [docs/index.md](docs/index.md) | DocFX 入口和双语文档索引 |
| 首次版本说明 | [2.0.0-alpha.1](docs/articles/release-2.0.0-alpha.1.md) | 首发版本完整快照 |
| 使用教程 | [使用教程](docs/articles/usage-tutorial.md) | 代码优先的张量和视觉工作流 |
| 平台/后端支持 | [支持表](docs/articles/platform-support.md) | 目标框架和验证边界 |
| 模型支持 | [模型指南](docs/articles/model-support.md) | 目录 ID、模型族和状态语义 |
| 性能基准 | [基准指南](docs/articles/performance-benchmarking.md) | 跨后端、跨平台测试方法 |
| 工程历史 | [历史记录](docs/history/README.md) | 独立于用户指南的维护记录 |

## 🔨 源码构建

~~~powershell
dotnet restore DeploySharp.sln --locked-mode
dotnet build DeploySharp.sln -c Release --no-restore
dotnet test DeploySharp.sln -c Release --no-build --no-restore
~~~

当前 Windows 验证使用发布说明中记录的隔离缓存。默认全局缓存中可能存在已知的 OpenVINO NU1403 上游包内容哈希不一致；这是本地包缓存问题，不是 DeploySharp API 失败。

## ⚖️ 许可证

DeploySharp 源码采用 [Apache License 2.0](LICENSE.txt) 许可证。模型和厂商运行时属于独立工件，分别遵循其运行时和分发条款。

## 📮 联系与赞助

如果你有使用问题、Issue、测试反馈或赞助意愿，欢迎通过项目主页和 Issue 区与我们联系。

<p align="center">
  <img src="docs/images/readme/contact-support-zh.png" width="100%" alt="作者联系方式、社区入口与微信和支付宝赞助二维码">
</p>

---

## ⚠️ 软件声明与免责声明

### 📜 1. 开源协议声明

作者所有开源项目代码均遵循 **Apache License 2.0** 开源协议。

*特别说明：本项目集成了若干第三方库。若任何第三方库的许可协议与 Apache 2.0 协议存在冲突或不一致，均以该第三方库的原始许可协议为准。本项目不包含也不代表这些第三方库的授权声明，使用前请务必阅读并遵守第三方库的相关许可。*

### 🤖 2. 代码开发与质量说明

- **AI 辅助开发**：本代码在开发过程中使用了人工智能（AI）辅助生成与优化，并非完全由人工逐行编写。
- **安全性承诺**：**作者郑重声明，本代码中绝无任何有意设置的后门、病毒、木马或旨在破坏用户设备、窃取数据的恶意代码。**
- **技术局限性**：受限于作者个人的技术水平与能力，代码中可能存在因逻辑不严谨、优化不足或经验欠缺导致的低级问题（例如但不限于内存泄漏、偶发崩溃、资源未释放等）。这些问题纯属能力不足所致，并非主观故意。
- **测试范围**：由于作者精力有限，未对本软件进行全方位、覆盖所有边缘场景的完整测试。

### 🚨 3. 免责声明（重要）

**请在将本代码应用于任何实际项目（特别是商业、工业或关键任务环境）之前，务必进行详尽、严格的自行测试与验证。** 鉴于上述可能存在的代码缺陷及测试覆盖不足，**因使用本代码而导致的任何直接或间接损失（包括但不限于设备故障、数据丢失、系统瘫痪或利润损失等），本作者概不负责。** 一旦您开始使用本代码，即表示您已知晓上述风险并同意自行承担一切后果，相关问题与本作者无关。

### 🔓 4. 代码开源范围

本项目承诺核心逻辑代码完全开源，但上述提到的“第三方库”的二进制文件、源代码或相关资源不在本项目的开源义务范围内，请根据其各自的指引获取。

### 🤝 5. 社区与反馈

尽管存在上述不足，我们仍欢迎大家下载使用、提交 Issue 或参与测试，共同完善项目。如果你在使用过程中发现 Bug、内存溢出或有改进建议，欢迎通过项目主页提供的联系方式与作者取得联系，我们将尽力在有限的时间内提供协助。
