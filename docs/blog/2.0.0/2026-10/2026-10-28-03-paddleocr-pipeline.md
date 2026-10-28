# 在 C# 中运行 PaddleOCR v4/v5/v6：从文字检测到完整识别结果

> DeploySharp 2.0.0 系列 · 第 03 篇实战教程。基于正式版 NuGet `2.0.0`、源码 Tag `v2.0.0`，创建与修订：2026-10-28。

![DeploySharp 2.0.0：在 C# 中运行 PaddleOCR，从图像、检测与识别到文字和区域结果](assets/03-paddleocr-pipeline/cover.png)

## 一、DeploySharp 项目简介

**DeploySharp** 是一个面向 **C#/.NET 开发者**的开源模型部署框架，由「椒颜皮皮虾」开发，项目源码遵循 **Apache License 2.0**。它基于 **ONNX Runtime、OpenVINO、TensorRT、OpenCV DNN** 等推理引擎，提供模型加载、图像前处理、推理执行和结果后处理接口，方便大家把模型接入桌面软件、工业视觉和文档应用。

项目已经接入 YOLO 等目标检测模型，以及分类、分割、关键点、旋转框、异常检测和背景移除等视觉任务；同时提供 **PP-OCRv4/v5/v6、PP-Structure 文档处理**和部分图文、本地语言模型的使用入口。常用模型可以复用配套处理，自有模型则可以配置输入、缩放、颜色与归一化，进一步自定义输出解码和结果类型。

**项目地址：**

```text
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0
```

**NuGet 包：**在包管理器中搜索 `JYPPX.DeploySharp`，按任务与后端选择组件。

```text
https://www.nuget.org/packages?q=JYPPX.DeploySharp
```

当前 2.0.0 正式版主要验证 Windows 10/11 x64。各模型和后端分别记录支持范围，使用前可以查阅仓库中的模型支持指南与验证矩阵。

## 二、2.0 新特性与本篇 OCR 实战

### 2.1 从模型调用，走到完整应用流程

上一篇用 YOLO26 展示了单个视觉模型的部署。这一篇继续使用正式版 NuGet，把多个模型组合起来，完成一次**文字检测、文本行裁剪、方向处理和文字识别**。

先简要看一下与本篇有关的新版能力：

| 新版能力 | 对 OCR 应用有什么用 |
| --- | --- |
| 多后端与按需安装 | 图像处理与推理引擎分别选择，先用 CPU 跑通，再评估目标设备 |
| v4/v5/v6 核心模型目录 | 根据模型 ID 取得配套输入输出与解码配置，减少逐版本手工填写 |
| 完整 `OcrPipeline` | 组织检测、透视裁剪、方向分类、识别及结果合并，返回原图区域与文字 |
| 动态 Batch 与独立 Session 池 | 多条文本行可以分批识别，也可以交给多个独立推理器调度 |
| 可配置的区域处理 | 按需要接入长文本策略、ROI 和文档区域工作流 |

支持的 TensorRT 路径还提供 CUDA 裁剪、前后处理和缓冲复用。本篇先用 **ONNX Runtime CPU** 完成通用流程，再在 **Ubuntu 22.04** 上运行不同后端，展示从 Windows 桌面到 Linux 设备的部署方式；CUDA 专用流水线和完整 PP-Structure 页面处理仍在对应专题展开。

### 2.2 为什么 OCR 需要一条流水线

一张图片里的文字数量、大小、角度都不固定。识别模型通常接收一条已经裁好的文本行，直接把整张商品图交给 REC，并不能替代完整 OCR。

这次使用三个模型，各自负责一件事：

| 阶段 | 作用 | 结果怎样传给下一步 |
| --- | --- | --- |
| DET：文字检测 | 找到图片中的文字位置 | 返回原图文字区域，用于透视裁剪 |
| CLS：文本行方向分类 | 判断裁好的文字行是否需要旋转 180° | 按方向处理后，再准备识别输入 |
| REC：文字识别 | 将文本行转换为字符序列 | CTC 解码得到文字和置信度 |

中间还有裁剪、缩放、归一化、Batch 组织和坐标对应。下图把整条处理过程放在一起，图中的多个文本行来自同一张原图：

![OCR 流水线：原图经 DET 定位，透视裁剪后交给可选 CLS 和 REC，最终合并文字、分数与原图区域](assets/03-paddleocr-pipeline/pipeline-workflow.png)

本文默认使用 **PP-OCRv5 Mobile DET + Mobile CLS + Mobile REC**，识别一张包含**大标题、多栏小字、照片、图注和引语**的中文报纸页面，并生成三个文件：`result.txt`、`result.json`、`result.jpg`。随后用同一份代码切换到 v4 Mobile 和 v6 Tiny，讲清模型、字典和方向分类怎样配对。

下面是实际使用的输入，来自 PaddleOCR 官方 PP-Structure 示例，原图为 `1524 × 1368`。本篇借用它验证通用 OCR；尚未运行版面分析，也不把 OCR 文本行拼接当作完整文章阅读顺序。

![实际输入：PaddleOCR 官方中文多栏报纸页面，包含标题、署名、密集正文、照片及图注](assets/03-paddleocr-pipeline/complex-page-input.png)

## 三、v4、v5、v6 应该怎样选择

### 3.1 先按部署需求选择组合

DeploySharp 已为以下七组核心组合提供目录与流水线配置。代码使用右侧参数选择组合，不需要为每个版本重写检测与裁剪流程：

| 版本与组合 | 主要选择方向 | 本文命令行参数 |
| --- | --- | --- |
| PP-OCRv4 Mobile | 已有 v4 轻量化项目，继续使用对应模型与字典 | `v4-mobile` |
| PP-OCRv4 Server | 评估 v4 Server 组合，允许更大的模型和计算开销 | `v4-server` |
| PP-OCRv5 Mobile | 本文默认组合，先完成完整部署与结果使用 | `v5-mobile` |
| PP-OCRv5 Server | 在目标图像上评估 v5 Server 组合 | `v5-server` |
| PP-OCRv6 Tiny | 从较小模型开始，评估设备延迟与识别效果 | `v6-tiny` |
| PP-OCRv6 Small | 进一步评估模型规模与效果之间的取舍 | `v6-small` |
| PP-OCRv6 Medium | 允许更大的模型，按实际数据评估效果 | `v6-medium` |

**版本号和模型大小不能直接替代业务测试。** 小字、低对比度、特殊字体、竖排和长文本，都会影响实际效果。本文新实跑的是 v4 Mobile、v5 Mobile 和 v6 Tiny；其他组合的已有后端证据见验证矩阵。

### 3.2 DET、CLS、REC 和字典要配套

三种版本的方向分类和识别字典存在差异：

| 组合 | 方向分类器 | REC 配套字典 |
| --- | --- | --- |
| v4 Mobile / Server | legacy 0°/180° CLS | `ppocrv4_keys.txt` |
| v5 Mobile / Server | 对应 Mobile / Server 的 PP-LCNet 文本行方向模型 | `ppocrv5_dict.txt` |
| v6 Tiny / Small / Medium | **复用 v5 Mobile CLS** | 对应规模的 `PP-OCRv6_*_rec_dict.txt` |

当前 v6 组合没有单独命名为 v6 的 CLS，代码会明确选择 `paddleocr/ppocrv5/mobile-cls`。也要区分**文本行方向分类**和整页文档的四方向分类：本篇选择的是文本行 0°/180° 模型。

识别字典决定“输出类别索引对应哪个字符”，必须与 REC 一起准备。不要因为都是中文模型，就把 v4、v5、v6 的字典混用；也不要自行排序、去重或删除字典中的字符。

## 四、创建项目与安装 NuGet

### 4.1 同一份 C# 代码，在 Windows 与 Ubuntu 运行

上一篇以 Windows 桌面为起点，这一篇再把相同模型和复杂页面放到 Ubuntu 设备上。两边都使用正式版 NuGet，没有引用 DeploySharp 源码项目：

| 项目 | Windows 本机 | Ubuntu 测试设备 |
| --- | --- | --- |
| 系统 | Windows 11 x64，`10.0.26200` | Ubuntu `22.04.5 LTS` x64，内核 `6.8.0-138-generic` |
| CPU | Ryzen 7 5800H，8 核 16 线程 | Core i7-1165G7，4 核 8 线程 |
| GPU | 本文 Windows 基线只使用 CPU | NVIDIA RTX 2060，6 GB；驱动 `580.178.04` |
| .NET | SDK `10.0.401` / 运行时 `10.0.12` | SDK `10.0.301` / 运行时 `10.0.9` |
| 应用与 DeploySharp | `net10.0`、`x64`、NuGet `2.0.0` | 同左 |
| 基础图像与推理组件 | OpenCV `5.0.0`、ORT CPU `1.28.0` | OpenCV `5.0.0`、ORT CPU `1.28.0` |
| 输入 | `pp_structure_v3_demo.png`，`1524 × 1368` | 相同图片、模型和字典 |

**Ubuntu 结果只说明本文这一组设备、模型与运行库已实际运行，不扩大为全部 Linux 发行版或全部模型的兼容承诺。** 2.0.0 的主要发布验证范围仍是 Windows x64。

准备 .NET 10 SDK，然后在 PowerShell 或 Ubuntu Bash 中创建项目：

```shell
dotnet new console -n DeploySharpOcrDemo -f net10.0
cd DeploySharpOcrDemo
```

后续命令均在这个项目目录执行。使用 Visual Studio 的朋友，也可以创建 .NET 10 控制台应用，再从 NuGet 管理器安装相同依赖。

### 4.2 按操作系统安装 CPU 依赖

OCR 编排、DB 检测后处理和 CTC 解码由视觉组件提供，`Visual.OpenCV` 负责图片和文本行处理，ONNX Runtime 后端执行模型。**不需要再寻找单独的 PaddleOCR NuGet 包，也不需要安装 Python 或 PaddlePaddle 才能运行本篇的 ONNX 模型。**

三个托管/推理包在两种系统中相同，先安装：

```shell
dotnet add package JYPPX.DeploySharp.Visual.OpenCV --version 2.0.0
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

**Windows x64** 再安装图像原生库：

```powershell
dotnet add package JYPPX.OpenCV.runtime.win-x64 --version 5.0.0
```

**Ubuntu 22.04 x64** 选择相应的原生库：

```bash
dotnet add package JYPPX.OpenCV.runtime.ubuntu.22.04-x64 --version 5.0.0
```

| 显式安装的包 | 在 OCR 中负责什么 |
| --- | --- |
| `JYPPX.DeploySharp.Visual.OpenCV` | 解码原图、检测前处理、文本行透视裁剪、缩放与归一化 |
| `JYPPX.DeploySharp.Backend.OnnxRuntime` | 将 DET、CLS、REC 推理交给 ONNX Runtime |
| `Microsoft.ML.OnnxRuntime` | 提供 CPU 原生推理运行库 |
| 对应操作系统的 `JYPPX.OpenCV.runtime.*` | 提供图像处理、绘制与保存所需的原生库，两种系统择一 |

`Core`、`Visual` 等基础依赖由 NuGet 自动引入。这里的 OpenCV 用于图像处理，推理仍交给 ONNX Runtime；**`Visual.OpenCV` 和 `Backend.OpenCV` 的作用不同**。

Ubuntu 下还需让系统找到 OpenCV 的 `.so` 及其同目录依赖。本例使用默认 NuGet 缓存目录，在完成还原后设置：

```bash
dotnet restore
opencv_native="$HOME/.nuget/packages/jyppx.opencv.runtime.ubuntu.22.04-x64/5.0.0/runtimes/ubuntu.22.04-x64/native"
export LD_LIBRARY_PATH="$opencv_native:${LD_LIBRARY_PATH:-}"
```

修改过 `NUGET_PACKAGES` 时，使用自己的缓存根目录替换 `$HOME/.nuget/packages`。这个环境变量要在启动 `dotnet` 之前设置。若 .NET 10 提示发行版 RID 的 `NETSDK1206` 警告，应继续核对原生库实际搜索路径；“包已还原”不等于系统能找到它的全部依赖。

### 4.3 其他场景怎样选包

如果已有其他后端环境，可以按下面的方向准备组件，但还要配置对应后端注册和设备请求：

| 部署场景 | 主要变化 |
| --- | --- |
| ONNX Runtime CUDA | 保留 ORT 适配器，选择匹配的 GPU runtime 与 CUDA/cuDNN，显式启用 CUDA Provider |
| OpenVINO | 选择 `Backend.OpenVINO`，准备匹配的 OpenVINO runtime |
| OpenCV DNN | 选择 `Backend.OpenCV`，确认实际导出图的 Batch 与动态宽度支持 |
| TensorRT 执行 DET/CLS/REC | 选择 `Backend.TensorRT`、匹配 bridge 和厂商运行库，在目标 GPU 构建 Engine；第八节有 Ubuntu 实跑 |
| TensorRT CUDA 专用 OCR | 进一步使用 `Visual.TensorRT` 的 CUDA 裁剪与专用处理路径，按任务单独验证 |
| 应用内自动下载模型 | 按需增加 `ModelFactory`，使用 `PaddleOcrReleaseClient` |

**本地文件加载不要求安装 ModelFactory。** Windows 按场景安装见第二篇；Ubuntu 后端例子见第 8.4 节。下面的基础代码固定为 ORT CPU，跨系统时替换图像 runtime 即可；切换引擎仍需调整注册、设备与模型格式。CPU/GPU 原生包不要混装到同一个输出目录。

```text
按场景选择 NuGet（第二篇第四节）：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/blog/2.0.0/2026-10/2026-10-28-02-yolo26-quickstart.md

TensorRT CUDA OCR：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/tensorrt-cuda-ocr.md
```

### 4.4 检查项目配置

本文使用的 `.csproj` 完整内容如下。显式设置 `x64`，避免进程架构和原生运行库不一致：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="JYPPX.DeploySharp.Visual.OpenCV" Version="2.0.0" />
    <PackageReference Include="JYPPX.DeploySharp.Backend.OnnxRuntime" Version="2.0.0" />
    <PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.28.0" />
    <PackageReference Include="JYPPX.OpenCV.runtime.win-x64" Version="5.0.0" Condition="'$(OS)' == 'Windows_NT'" />
    <PackageReference Include="JYPPX.OpenCV.runtime.ubuntu.22.04-x64" Version="5.0.0" Condition="$([MSBuild]::IsOSPlatform('Linux'))" />
  </ItemGroup>
</Project>
```

## 五、按需准备模型和测试图片

### 5.1 只下载当前组合需要的文件

PaddleOCR 模型放在独立的 **`models-paddleocr` Release** 中。每个 DET、CLS、REC 和字典均可单独下载，使用轻量组合时，不需要把全部模型打包下载。

下面分别给出 Bash 与 PowerShell 命令，**选择与当前终端对应的一组执行**。默认下载 v5 Mobile 的三个 ONNX 和一个字典；已有本地文件可以直接使用。

**Ubuntu Bash：** 修改 `variant` 可选择七种组合之一。

```bash
variant=v5-mobile
prefix="ppocr${variant}"
case "$variant" in
  v4-mobile|v4-server)
    cls=ppocrv4-legacy-cls.onnx; dictionary=ppocrv4_keys.txt ;;
  v5-mobile|v5-server)
    cls="$prefix-cls.onnx"; dictionary=ppocrv5_dict.txt ;;
  v6-tiny|v6-small|v6-medium)
    cls=ppocrv5-mobile-cls.onnx
    dictionary="PP-OCRv6_${variant#v6-}_rec_dict.txt" ;;
  *) printf '不支持的组合：%s\n' "$variant"; exit 1 ;;
esac
mkdir -p models images
model_base='https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
for file in "$prefix-det.onnx" "$cls" "$prefix-rec.onnx" "$dictionary"; do
  curl -fL --retry 2 "$model_base/$file" -o "models/$file"
done
curl -fL --retry 2 \
  'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/pp_structure_v3_demo.png' \
  -o images/pp_structure_v3_demo.png
```

**Windows PowerShell：** 修改 `$variant` 选择相同组合。

```powershell
$variant = 'v5-mobile'
$prefix = $variant -replace '^v', 'ppocrv'
switch -Regex ($variant) {
  '^v4-(mobile|server)$' {
    $cls = 'ppocrv4-legacy-cls.onnx'
    $dictionary = 'ppocrv4_keys.txt'
    break
  }
  '^v5-(mobile|server)$' {
    $cls = "$prefix-cls.onnx"
    $dictionary = 'ppocrv5_dict.txt'
    break
  }
  '^v6-(tiny|small|medium)$' {
    $cls = 'ppocrv5-mobile-cls.onnx'
    $size = $variant.Substring(3)
    $dictionary = "PP-OCRv6_${size}_rec_dict.txt"
    break
  }
  default { throw '请选择本文支持的七种组合之一。' }
}
New-Item -ItemType Directory -Force -Path .\models, .\images | Out-Null
$modelBase = 'https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
foreach ($file in @("$prefix-det.onnx", $cls, "$prefix-rec.onnx", $dictionary)) {
  Invoke-WebRequest -Uri "$modelBase/$file" -OutFile (Join-Path '.\models' $file)
}
Invoke-WebRequest `
  -Uri 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/pp_structure_v3_demo.png' `
  -OutFile '.\images\pp_structure_v3_demo.png'
```

如果命令行下载不方便，也可以在 Release 页面选择对应文件下载。已有本地模型和图片，直接按名称放入目录即可。本文选用的默认文件是：

| 文件 | 用途 |
| --- | --- |
| `ppocrv5-mobile-det.onnx` | 文字检测 |
| `ppocrv5-mobile-cls.onnx` | 文本行 0°/180° 方向分类 |
| `ppocrv5-mobile-rec.onnx` | 文本行识别 |
| `ppocrv5_dict.txt` | v5 REC 字符索引表 |
| `pp_structure_v3_demo.png` | 官方中文多栏报纸输入图片 |

```text
PaddleOCR 单文件模型：
https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr

默认测试图片：
https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/pp_structure_v3_demo.png
```

模型 Release 中还包含 PP-Structure 和 PaddleOCR-VL 等其他任务的资产，不能因为文件出现在同一个 Release 中，就把它当作本篇的 DET/CLS/REC。这里使用的是目录明确登记的核心 PP-OCR 工件。

### 5.2 项目目录

默认组合下载完成后，目录如下。其他组合可以放在同一个 `models` 目录，程序只会加载所选组合：

```text
DeploySharpOcrDemo/
  DeploySharpOcrDemo.csproj
  Program.cs
  models/
    ppocrv5-mobile-det.onnx
    ppocrv5-mobile-cls.onnx
    ppocrv5-mobile-rec.onnx
    ppocrv5_dict.txt
  images/
    pp_structure_v3_demo.png
```

## 六、编写完整 OCR 程序

下面代码可以直接替换 `Program.cs`。它根据组合选择模型，加载本地文件和配套字典，创建三阶段流水线，最后保存文字、结构化结果和原图文字框。

为方便切换，三个可选命令行参数依次为：**图片路径、模型组合、模型目录**。这里先使用 `batch=1`、`channels=1` 建立逐行识别基线，后面再说明怎样评估 Batch 和通道；密集页面的检测后处理与流水线区域上限都显式设为 512。

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgProc;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;

Console.OutputEncoding = Encoding.UTF8;
string imagePath = Path.GetFullPath(args.Length > 0 ? args[0] : "images/pp_structure_v3_demo.png");
string variant = args.Length > 1 ? args[1].ToLowerInvariant() : "v5-mobile";
string modelRoot = Path.GetFullPath(args.Length > 2 ? args[2] : "models");
const int batch = 1;
const int channels = 1;

var ids = variant switch
{
    "v4-mobile" => ("ppocrv4/mobile-det", "ppocrv4/legacy-cls", "ppocrv4/mobile-rec"),
    "v4-server" => ("ppocrv4/server-det", "ppocrv4/legacy-cls", "ppocrv4/server-rec"),
    "v5-mobile" => ("ppocrv5/mobile-det", "ppocrv5/mobile-cls", "ppocrv5/mobile-rec"),
    "v5-server" => ("ppocrv5/server-det", "ppocrv5/server-cls", "ppocrv5/server-rec"),
    "v6-tiny" => ("ppocrv6/tiny-det", "ppocrv5/mobile-cls", "ppocrv6/tiny-rec"),
    "v6-small" => ("ppocrv6/small-det", "ppocrv5/mobile-cls", "ppocrv6/small-rec"),
    "v6-medium" => ("ppocrv6/medium-det", "ppocrv5/mobile-cls", "ppocrv6/medium-rec"),
    _ => throw new ArgumentException("版本应为 v4-mobile/server、v5-mobile/server 或 v6-tiny/small/medium。")
};
PaddleOcrModelDescriptor detRow = Find(ids.Item1);
PaddleOcrModelDescriptor clsRow = Find(ids.Item2);
PaddleOcrModelDescriptor recRow = Find(ids.Item3);
string dictionaryPath = Require(recRow.ReleaseArtifact!.DictionaryAssetName!);
OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(
    dictionaryPath, "local." + variant, "1", useSpaceCharacter: true);
PaddleOcrProfile det = CreateProfile(detRow, maximumBatch: 1);
PaddleOcrProfile cls = CreateProfile(clsRow, maximumBatch: batch);
PaddleOcrProfile rec = CreateProfile(recRow, maximumBatch: batch, characters);

using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var profiles = new VisualProfileRegistry();
profiles.Register(det.VisualProfile);
profiles.Register(cls.VisualProfile);
profiles.Register(rec.VisualProfile);
profiles.Freeze();
var request = new BackendRequest(BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId, "cpu");

using var pipeline = new OcrPipeline(
    backends,
    Select(det, detRow), request,
    Select(cls, clsRow), request, cls.CropProfile!,
    Select(rec, recRow), request, rec.CropProfile!,
    new OcrPipelineOptions(maximumConcurrency: 1, maximumRegions: 512,
        maximumRecognitionBatch: batch, maximumRecognitionPaddingRatio: 1.0,
        autoRotateVerticalText: true),
    detectionSessionOptions: new SessionOptions(1),
    orientationSessionOptions: new SessionOptions(channels),
    recognitionSessionOptions: new SessionOptions(channels),
    orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);

// 此 Mat 只用于取得原图尺寸及绘图；OCR 输入在内部保留自己的源图。
if (!File.Exists(imagePath)) throw new FileNotFoundException("找不到图片。", imagePath);
using Mat canvas = ImageCodecs.ImRead(imagePath);
if (canvas.Empty) throw new IOException("无法解码输入图片。");
var sourceSize = new VisualSize(canvas.Cols, canvas.Rows);
var prepare = Stopwatch.StartNew();
using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(
    imagePath, det.VisualProfile.Input.Name,
    OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize, resizeLong: 1600));
prepare.Stop();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var run = Stopwatch.StartNew();
OcrResult result = await pipeline.RunAsync(input,
    new OcrExecutionOptions(timeout: TimeSpan.FromSeconds(60)), cancellation.Token);
run.Stop();

Console.WriteLine($"backend=onnxruntime;device=cpu;variant={variant};batch={batch};channels={channels}");
Console.WriteLine($"regions={result.Regions.Count}");
Console.WriteLine(FormattableString.Invariant(
    $"prepare_ms={prepare.Elapsed.TotalMilliseconds:F1};run_ms={run.Elapsed.TotalMilliseconds:F1}"));
for (int i = 0; i < result.Regions.Count; i++)
{
    OcrRegionResult item = result.Regions[i];
    Console.WriteLine(FormattableString.Invariant(
        $"[{i + 1:D2}] score={item.Recognition.Confidence:F3};angle={OrientationDegrees(item.Region.Orientation)};{item.Recognition.Text}"));
    var points = item.Region.Polygon.Vertices;
    for (int j = 0; j < points.Count; j++)
    {
        var a = points[j];
        var b = points[(j + 1) % points.Count];
        Drawing.Line(canvas, new Point((int)a.X, (int)a.Y), new Point((int)b.X, (int)b.Y),
            new Scalar(80, 210, 30), 2);
    }
    var first = points[0];
    Drawing.PutText(canvas, (i + 1).ToString("D2"),
        new Point(Math.Clamp((int)first.X, 0, Math.Max(0, canvas.Cols - 35)),
            Math.Clamp((int)first.Y - 4, 18, Math.Max(18, canvas.Rows - 1))),
        HersheyFonts.HersheySimplex, 0.5, new Scalar(10, 10, 230), 1);
}
File.WriteAllLines("result.txt", result.Regions.Select(x => x.Recognition.Text), new UTF8Encoding(false));
var report = new
{
    variant, backend = "onnxruntime", device = "cpu", batch, channels,
    sourceSize = new { width = result.SourceSize.Width, height = result.SourceSize.Height },
    timing = new { prepareMs = prepare.Elapsed.TotalMilliseconds, runMs = run.Elapsed.TotalMilliseconds },
    regions = result.Regions.Select((x, i) => new
    {
        index = i + 1, text = x.Recognition.Text, confidence = x.Recognition.Confidence,
        detectionScore = x.Region.Score, angle = OrientationDegrees(x.Region.Orientation),
        polygon = x.Region.Polygon.Vertices.Select(p => new { x = p.X, y = p.Y })
    })
};
File.WriteAllText("result.json", JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
}), new UTF8Encoding(false));
if (!ImageCodecs.ImWrite("result.jpg", canvas)) throw new IOException("无法保存结果图。");
Console.WriteLine("saved=result.txt,result.json,result.jpg");

static int OrientationDegrees(TextOrientation orientation) => orientation switch
{
    TextOrientation.Degrees0 => 0,
    TextOrientation.Clockwise90 => 90,
    TextOrientation.Degrees180 => 180,
    TextOrientation.CounterClockwise90 => 270,
    _ => throw new ArgumentOutOfRangeException(nameof(orientation))
};

PaddleOcrModelDescriptor Find(string id) => PaddleOcrModelCatalog.Find(new ModelId("paddleocr/" + id));

string Require(string file)
{
    string path = Path.Combine(modelRoot, file);
    return File.Exists(path) ? path : throw new FileNotFoundException("缺少模型或字典。", path);
}

PaddleOcrProfile CreateProfile(PaddleOcrModelDescriptor row, int maximumBatch, OcrCharacterSet? dictionary = null)
{
    // 本地加载：使用目录中的模型规格，不要求填写模型来源元数据。
    var contract = new PaddleOcrArtifactContract(row.ReleaseArtifact!.Opset,
        "", "", "", "Apache-2.0", "local-input-v1", "local-output-v1");
    if (row.Family == "det")
        return PaddleOcrProfiles.CreateDetection(row.ModelId, contract, row.InputName, row.OutputName,
            postprocess: new PaddleDbPostprocessOptions(maximumRegions: 512), maximumBatch: maximumBatch);
    return PaddleOcrModelCatalog.CreateProfile(row, contract, dictionary, maximumBatch);
}

VisualProfileSelection Select(PaddleOcrProfile profile, PaddleOcrModelDescriptor row) =>
    profiles.Select(profile.CreateArtifact(Require(row.ReleaseArtifact!.AssetName)),
        backends, request, profile.VisualProfile.Task);
```

## 七、关键实现：从三个模型到一个结果

### 7.1 目录选择和本地文件加载

代码中的 `PaddleOcrModelCatalog` 用于查找模型规格，例如 v4 Mobile DET 的输出名与 v5 Mobile DET 并不相同。通过目录选择 Profile，便可以复用对应的检测解码、方向类别和识别配置。

`ReleaseArtifact` 在这里仅提供已知文件名和模型规格，**不会自动联网下载**。`Require` 检查本地文件存在，`CreateArtifact` 把实际路径交给后端。`PaddleOcrArtifactContract` 中的可选来源字段留空，前后处理版本用本地标识，用户可以直接准备文件并运行。

`CreateProfile` 为 DET 声明单图输入；CLS 和 REC 的 Batch 上限由顶部的 `batch` 控制，本文默认逐行运行。这里选择的是实际支持相应输入的发布工件；只在代码中把 Batch 改大，不能让固定 Batch 模型自动变成动态模型。

**密集页面要同时检查两个区域上限。** DB 后处理的 `PaddleDbPostprocessOptions.maximumRegions` 默认是 128，即使只把 `OcrPipelineOptions.maximumRegions` 调到 512，前面仍可能只返回 128 个区域。因此示例为 DET 显式创建后处理配置，将两处上限一起设为 512。本图修改前返回 128 个，修改后返回 144 个；不能把“刚好到达默认上限”直接理解为整页已经处理完整。

### 7.2 检测前处理与文本行裁剪

检测输入使用 `CreatePaddleOcrOfficialInferenceDetectionOptions`。为保留这张页面中的小字，本文显式传入 `resizeLong: 1600`；`1524 × 1368` 原图按该方法的尺寸对齐规则准备为 `1536 × 1408`。使用 BGR 与配套均值、标准差归一化，**不是沿用 YOLO 的 640×640 Letterbox 配置**。更大的检测输入会增加计算与内存开销，换图时应按文字大小与设备能力调整。

DET 的 DB 后处理将概率图转换为文字区域，默认采用概率阈值 `0.3`、框阈值 `0.6` 和扩张比例 `1.5`。随后，`OcrPipeline` 从 OCR 输入保留的同一张原图中透视裁剪文本行，再按 CLS 和 REC 各自的配置准备输入。

REC 默认按高度 48 保持比例准备动态宽度，组批时为同一批文本行补齐宽度。方向分类则有不同配置：legacy CLS 使用 BGR、48×192；本篇 v5 CLS 使用 RGB、80×160。它们由对应 Profile 处理，不需要在主程序中手工旋转每一行。

本文为获取尺寸和绘图单独读了一份 `canvas`，OCR 输入也持有自己的源图。**重复读图发生在演示画布与 OCR 输入之间，文本行裁剪会复用 OCR 源图，不会每一行重新读取整张图片。** 连续帧应用应把解码、绘图与推理资源按实际需要复用。

### 7.3 字典和 CTC 为什么必须对应

REC 输出的每个时间步包含各字符类别的分数。CTC（Connectionist Temporal Classification）解码先选择类别，再按规则合并相邻重复项、移除 blank，最后按字典映射成文字。

下面用一段示意序列说明字典和 CTC 的关系；它表达解码原理，不是本篇模型的真实中间张量：

![CTC 字符解码：按训练索引查字典，合并连续重复项并移除 blank；blank 隔开的重复字符应保留](assets/03-paddleocr-pipeline/dictionary-ctc.png)

`LoadCharacterSet` 按字典原有顺序读取字符，本文这组发布字典使用 `useSpaceCharacter: true`，由加载器追加模型所需的空格类别。blank 则由 CTC 配置单独管理，**不要再往字典开头添加一个 blank，也不要在已经包含空格类的自有字典上重复追加空格**。

REC 的类别数量与字典对应不上时，应先检查模型、字典和空格配置。仅修改字典让维度“对上”，可能会让程序运行却输出错误文字。自有模型是否输出概率、blank 的索引、输入高度与字典格式，也要按实际导出约定配置。

### 7.4 方向、坐标、异步和资源释放

本例为普通文本行启用 CLS，并显式开启 `autoRotateVerticalText`：对于高宽比至少为 1.5 的细长文字框，流水线先按几何规则逆时针旋转 90°，其余区域执行文本行 0°/180° 分类。它适合侧着印刷的长条文字，不等同于完整的中文竖排阅读分析。

`UseZeroDegrees` 表示 CLS 被拒识时，明确按 0° 继续识别；默认方向拒识阈值为 `0.9`。这是一种应用选择，并不证明低置信度文本行一定正向。需要严格暴露方向问题时，可以使用 `Fail` 策略。

`item.Region.Polygon` 返回原图像素坐标，绘制时直接使用。`Region.Orientation` 表示文本行的离散方向处理，不是文字框倾斜角，也不代表本篇具备整页四方向自动矫正。

程序使用 `RunAsync` 等待完整结果，设置 60 秒调用超时，控制台 `Ctrl+C` 可以请求取消。调用方仍要等待任务结束后再释放输入；这个接口本身不代表三阶段可以任意并行，也不把不同图片自动拼成一个模型 Batch。

输入、流水线、后端注册表和 `Mat` 均通过 `using` 释放。`OpenCvOcrImageInput` 在调用期间保留原图及裁剪工作区；`OcrResult` 中的文字与坐标为托管结果，不包含必须继续保留的原生 `Mat`。批量处理图片时应复用 Profile、注册表和流水线，为每张图创建并释放输入对象。

## 八、运行程序，查看实际识别结果

### 8.1 默认 v5 Mobile 的真实输出

在项目目录执行：

```shell
dotnet run -c Release
```

这次输入是前面的中文报纸页面，Windows ORT CPU 程序返回 **144 个文字区域**。下面只展示部分输出，省略位置已标明；文字、分数与编号均保留实际结果，没有手工修正成“标准答案”。**完整文字由程序保存到 `result.txt`，全部区域、分数和坐标保存在 `result.json` 中。** Ubuntu 的实际运行与后端对照见第 8.4、8.5 节。

```text
backend=onnxruntime;device=cpu;variant=v5-mobile;batch=1;channels=1
regions=144
prepare_ms=107.2;run_ms=5879.9
[01] score=0.997;angle=0;助力双方交往
[02] score=0.999;angle=0;搭建友谊桥梁
[03] score=0.955;angle=0;本报记者沈小晓任彦黄培昭
[04] score=0.999;angle=0;身着中国传统民族服装的厄立特里亚青
[05] score=0.998;angle=0;厄立特里亚高等教育与研究院合作建立，开
[06] score=0.996;angle=0;年依次登台表演中国民族舞、现代舞、扇子舞
[07] score=0.997;angle=0;设了中国语言课程和中国文化课程，注册学
[08] score=0.997;angle=0;等，曼妙的舞姿赢得现场观众阵阵掌声。这
...（第 09—23 项省略）...
[24] score=0.234;angle=0;90-
...（第 25—44 项省略）...
[45] score=0.995;angle=0;在厄立特里亚不久前举办的第六届中国风筝文化节上，当地小学生体验风筝制作。
...（第 46—144 项省略）...
saved=result.txt,result.json,result.jpg
```

这是**未预热的单次功能运行**，计时范围见下一章，不能据此比较模型性能。这里的 144 是模型返回的区域数，不代表人工真值中的总行数，也不代表每条文字都正确。

下图是同次运行生成的 `result.jpg`。绿色线标出区域，红色编号与控制台编号、JSON 中的 `index` 对应，结果保留原图 `1524 × 1368` 尺寸：

![PP-OCRv5 Mobile 真实输出：中文多栏报纸页面上的 144 个文字候选区域，包含标题、正文、图注和照片内低分候选](assets/03-paddleocr-pipeline/v5-mobile-result.jpg)

可以看到，大标题、密集正文和长图注都进入了识别结果。第 24 区域位于照片内，输出 `90-`，识别分数仅为 `0.234`，应当作为低置信度候选检查，不能当作已确认文字。引号、标点和个别字也仍需校验；本篇保留这些实际结果，便于大家复现和判断。

**这是一页多栏排版，编号不是文章的语义阅读顺序。** 当前通用 OCR 主要按几何位置排序，因此相近高度的左右栏文字会穿插。需要“先读完左栏，再读下一栏”、区分标题/正文/图注或还原整页文档时，应进一步使用版面分析与区域 OCR；这部分会在 PP-Structure 专题展开。

### 8.2 三种输出文件怎样使用

| 文件 | 保存内容 | 使用方式 |
| --- | --- | --- |
| `result.txt` | 按结果顺序保存的逐区域文字，UTF-8 | 查看文本、复制到后续业务 |
| `result.json` | 文字、识别分数、检测分数、方向、原图 polygon 和计时 | 与区域编号关联，接入业务或界面 |
| `result.jpg` | 原图上的文字框和编号 | 检查定位、漏检、误检及坐标 |

JSON 的每个区域同时保留 `confidence` 和 `detectionScore`：前者是识别置信度，后者是文字检测区域分数，两者不是同一个指标。示例将方向枚举转换为顺时针角度 `0/90/180/270`，写入 `angle`；它表示识别前的离散旋转处理，不是文字框的倾斜角。`polygon` 是原图坐标。

绘图使用 OpenCV 内置 Hershey 字体，只写数字编号；该字体不适合直接绘制中文。需要在界面显示中文时，可以用 WPF/WinForms、SkiaSharp 或其他支持中文字体的绘制组件，按相同编号关联文字。

编号对应当前流水线的区域顺序。对于复杂多栏文档，不能把逐行拼接直接当作完整的语义阅读顺序；版面区域与多页组织会在 PP-Structure 专题中展开。

### 8.3 切换 v4、v6 或自己的图片

先把第五节下载命令中的 `variant`（PowerShell 为 `$variant`）改为所需组合，下载对应文件，再通过第二个程序参数选择相同组合。下面的正斜杠路径可用于 Bash 和 PowerShell：

```shell
dotnet run -c Release -- ./images/pp_structure_v3_demo.png v4-mobile ./models
dotnet run -c Release -- ./images/pp_structure_v3_demo.png v6-tiny ./models
```

同一份正文代码在三种组合上的功能验证结果如下，均使用 ORT CPU，执行了 DET、裁剪、CLS、REC 和结果合并：

| 本篇新实跑组合 | 返回区域数 | 第一行识别文字 | 运行结果 |
| --- | ---: | --- | --- |
| v4 Mobile + legacy CLS | 143 | 助力双方交往 | 完整运行并保存三个结果文件 |
| v5 Mobile + Mobile CLS | 144 | 助力双方交往 | 完整运行并保存三个结果文件 |
| v6 Tiny + v5 Mobile CLS | 145 | 助力双方交往 | 完整运行并保存三个结果文件 |

不同 DET 返回的候选区域、文字和分数均可能不同，区域数更多也不代表准确率更高。本表说明在同一张复杂页面上的版本切换与完整流程已经跑通，不作 CER/WER 或速度排名。

换自己的图片和模型目录时，传入实际路径：

```shell
dotnet run -c Release -- ./images/my-document.jpg v5-mobile ./my-models
```

程序按模型目录中的标准文件名加载。若使用自己训练或重新导出的模型，需要同时检查张量名称、输入高度、颜色、归一化、动态宽度/Batch 和配套字典。已知 PP-OCR 输出结构可以使用相应的 `PaddleOcrProfiles` 工厂，传入真实输入输出名；结构不同则需要自定义 Profile 或 decoder。给模型改名不会改变它的输入输出约定。

三个结果文件会写到当前工作目录，再次运行会覆盖前一次。需要保留多个版本或多张图的结果时，应按图片或组合名称设置各自的输出目录。

### 8.4 放到 Ubuntu，同一份代码怎样运行

在第 4.1 节的 Ubuntu 设备上安装 .NET 10，按第 4.2 节选择 Linux OpenCV runtime，再使用 Bash 下载命令准备文件。**第六节的 C# 主程序可以直接使用**，相对路径不需要改成某台测试机的私人目录：

```bash
dotnet run -c Release -- ./images/pp_structure_v3_demo.png v5-mobile ./models
```

这条基础路径同样返回 **144 个区域**，写出 `result.txt/result.json/result.jpg`。在本次固定输入上，Ubuntu ORT CPU 的逐区域文字与 Windows 基线一致。原生组件通过 NuGet 与厂商运行库提供，运行这个 C# ONNX 案例不需要安装 PaddlePaddle。

服务器或边缘设备没有图形桌面也可以运行。代码只读取、绘制和保存图片，没有调用 `ImShow` 或依赖窗口循环；把结果文件取回开发机查看即可。

如果提示 `dotnet: command not found`，先检查 SDK 是否已安装且命令目录加入了 `PATH`；若 SDK 安装在用户的 `.dotnet` 目录，可设置 `DOTNET_ROOT` 与对应 `PATH`。如果是 OpenCV `.so` 加载失败，则检查 `LD_LIBRARY_PATH` 和真实缺失依赖，两者不是同一个问题。

### 8.5 Ubuntu 上运行 OpenVINO、OpenCV DNN 与 TensorRT

为了让大家能直接复现，我把后端切换整理成另一个可运行工程，仍使用正式版 NuGet。它为 DET、CLS、REC 分别配置后端注册表，并保留**同一字典、原图、检测尺寸、DB 阈值、裁剪与 CTC 解码**。

```text
Ubuntu PaddleOCR 后端对照工程：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0/2026-10/examples/03-paddleocr-backends
```

将该目录中的 `Program.cs` 与 `PaddleOcrBackends.csproj` 下载到一个与基础案例**并列的新目录**。工程里已引用各后端包；下面列出这次使用的引擎与原生环境，方便核对：

| 后端 | 本次环境与准备 |
| --- | --- |
| ORT CPU | `Microsoft.ML.OnnxRuntime 1.28.0` |
| OpenVINO CPU | `OpenVINO.runtime.ubuntu.22-x86_64 2026.3.1`，选择 Latency 提示 |
| OpenCV DNN CPU | 与图像组件共用 `JYPPX.OpenCV.runtime.ubuntu.22.04-x64 5.0.0` |
| TensorRT CUDA | TensorRT `11.0.0.114`、CUDA `12.9`、cuDNN `9.22.0`、RTX 2060 |
| TensorRT bridge | `JYPPX.TensorRT.CSharp.API.Runtime.linux-x64.ubuntu22.04.trt11.0.cuda12.9.cudnn9.22.Bridge 4.0.0` |

Linux 的 OpenVINO 原生库还有同目录的版本化 `.so` 依赖。本次先还原包，再把 OpenCV 和 OpenVINO 的 native 目录加入搜索路径，**不需要改全局系统环境**。在后端对照工程目录中执行：

```bash
dotnet restore
opencv_native="$HOME/.nuget/packages/jyppx.opencv.runtime.ubuntu.22.04-x64/5.0.0/runtimes/ubuntu.22.04-x64/native"
openvino_native="$HOME/.nuget/packages/openvino.runtime.ubuntu.22-x86_64/2026.3.1/runtimes/linux-x64/native"
cuda_native=/usr/local/cuda-12.9/lib64
export LD_LIBRARY_PATH="$opencv_native:$openvino_native:$cuda_native:${LD_LIBRARY_PATH:-}"

image=../DeploySharpOcrDemo/images/pp_structure_v3_demo.png
models=../DeploySharpOcrDemo/models
dotnet run -c Release -- "$image" v5-mobile "$models" openvino
dotnet run -c Release -- "$image" v5-mobile "$models" opencv-dnn
dotnet run -c Release -- "$image" v5-mobile "$models" tensorrt
```

这里假定基础案例叫 `DeploySharpOcrDemo`；按自己的实际位置修改 `image/models`，CUDA 安装目录不同也要相应修改。需要重新运行 ORT CPU 时，最后一个参数使用 `ort-cpu`。结果按后端与组合写入 `results/<backend>/<variant>/`，控制台只打印前 10 个区域，完整文字和区域继续写入 TXT/JSON。

TensorRT 首次运行会通过库的 `TensorRtOnnxEngineBuilder` **在 Ubuntu 目标 GPU 上构建三份 Engine**，以后复用模型旁的 `.ubuntu-trt11.engine`。本次 Profile 如下：

| 阶段 | Engine 接受的输入 |
| --- | --- |
| DET | 固定 `1 × 3 × 1408 × 1536`，对应本文页面的前处理 |
| CLS | 固定 `1 × 3 × 80 × 160` |
| REC | `1 × 3 × 48 × W`，`W` 的 min/opt/max 为 `48/320/3200` |

代码请求 `TensorRtApiVersion.TensorRt11`，Profile 与工件格式同时设置为 `tensorrt-engine`。TensorRT 11 使用强类型网络，本次保持原始 Float32 图并关闭 TF32，没有套用 TensorRT 10 的 `Float16` 构建标志，也没有搬用 Windows Engine。更换输入尺寸、模型、GPU 或 TensorRT 版本时，应重新生成匹配的 Engine；本例固定 DET Shape 不是任意尺寸都通用的缓存。

下面是同一 Ubuntu 设备上的完整流程结果。四条路径都执行 **DET → 透视裁剪 → CLS → REC → 合并**，统一 `batch=1 / channels=1 / paddingRatio=1.0 / maximumRegions=512 / resizeLong=1600`：

| 后端 | 区域数 | 相对 ORT CPU 的逐区域文字 | 完整调用 P50 / ms | P95 / ms |
| --- | ---: | --- | ---: | ---: |
| ORT CPU `1.28.0` | 144 | 基线 | 4893.204 | 5103.264 |
| OpenVINO CPU `2026.3.1` | 144 | 144/144 一致 | 3882.420 | 3924.977 |
| OpenCV DNN CPU `5.0.0` | 144 | 144/144 一致 | 14151.585 | 14544.885 |
| TensorRT `11.0.0.114` CUDA | 144 | 144/144 一致 | 2160.845 | 2198.410 |

该表使用 **预热 2 次、统计 5 次、nearest-rank P50/P95**。每次从读图和检测前处理开始，到完整 `RunAsync` 返回结束；不含 Session/Engine 构建、绘图和文件写出。只有 5 个样本，表中的 P95 实际取本次最大值，主要服务教程复现，不作稳态性能承诺。表中时间来自单独计时的完整调用，不能用两个阶段 P50 相加代替。

**这张大图的结果不能与 500×500、十几行文字的商品图直接比较。** 本例有 144 个候选区域、较大的 DET 输入，并且逐行识别；TensorRT 只执行三种模型，图像裁剪与结果解码仍在 CPU，没有启用 CUDA 专用裁剪/CTC，也没有搜索最佳 Batch 或通道组合。因此不能把 2160ms 当作该显卡的最优 OCR 性能。

OpenCV DNN 在这页上的耗时也明显更高。此工程保留动态识别宽度，并使用容量 16 的 Shape 缓存；宽度种类较多时，仍可能反复专门化和导入网络。后续可以单独评估固定宽度、宽度分桶及补边，但它们会改变输入和可能的识别结果，不能静默替换后仍宣称“同条件更快”。

这里的 **144/144 一致只表示与本次 ORT CPU 输出一致**，不表示人工标注准确率为 100%；照片内低分候选、标点以及多栏阅读顺序仍有前面提到的边界。本次 Ubuntu 新增的是 **v5 Mobile 这一组完整流程**，不能据此扩大为 v4/v6 或 PP-Structure 全部模型的 Linux 多后端结论。

## 九、Batch、推理通道与时间应该怎样看

### 9.1 一张图，多条文字行

本例 DET 对整张图执行一次。检测完成后，CLS 和 REC 面对的是多条文本行：**Batch 控制一次送入多少行，Session 数量控制同时有多少个独立推理器处理批任务。**

下面把三个不同配置维度放在一起，避免把它们都理解成“线程数”：

![OCR 调度示意：文本行先按宽度分批，独立 Session 池处理多个批次；帧级并发与文本行 Batch 分开控制](assets/03-paddleocr-pipeline/batch-and-sessions.png)

| 配置 | 本文值 | 控制什么 |
| --- | ---: | --- |
| `maximumRecognitionBatch` | 1 | 一个识别批次最多包含多少条文本行 |
| CLS/REC Profile 的 `maximumBatch` | 1 | 声明对应模型允许的最大 Batch，要与实际导出能力匹配 |
| `orientationSessionOptions` / `recognitionSessionOptions` | 各 1 | 各阶段独立 Session 的数量 |
| `maximumConcurrency` | 1 | 同时进入这条流水线的完整 OCR 调用上限 |
| `maximumRecognitionPaddingRatio` | 1.0 | 宽度组批时允许的补边计算比例 |

想评估两个独立 CLS/REC 通道，可以将代码顶部的 `channels` 改为 `2`。这会创建独立推理会话，增加内存或显存开销；CPU 后端本身也可能使用多线程，因此增加通道不一定更快。

评估批处理时，可以将顶部 `batch` 从 `1` 改为 `8`，相应 Profile 与流水线会使用同一个上限；它**不保证每一批恰好有 8 行**。程序会考虑文本行宽度和补边比例，较短的文字与超长行放在同一批，可能浪费补边计算。本文保留默认补边比例 `1.0`；放宽到 `1.5` 会改变分组与实际输入，应同时对照每行文字，不能只比较耗时。

Batch 也可能影响识别结果。在一张商品图的对照测试中，v5 Mobile 使用 `batch=8 / paddingRatio=1.5` 时，价格行出现漏字；改为逐行识别后，该行恢复。因此本篇先采用逐行基线，再讨论吞吐调优，不把一个批处理配置当作通用最优组合。换用其他模型、补边方式或后端，都要重新验证，并核对补边归一化与模型的预期是否一致。

### 9.2 本文记录的时间包含什么

代码的两个计时器边界如下：

| 字段 | 包含内容 | 未包含内容 |
| --- | --- | --- |
| `prepare_ms` | 创建 OCR 输入时的图片解码、检测前处理和输入准备 | 模型加载、绘图画布的单独解码 |
| `run_ms` | 等待完整 `RunAsync`：DET、文本行准备、CLS、REC、解码与结果合并 | 模型/Session 构建、前面的输入创建、绘图、TXT/JSON/JPG 写出 |

第六节基础代码的两个数来自一次未预热运行，**不是应用启动耗时，也不是稳态完整性能指标**；第 8.5 节对照工程另有预热和重复测量，不能混为一次数据。进一步定位时，可以读取 `result.Timing` 中的 `Detection`、`OrientationClassification`、`Recognition` 等阶段信息。

`Timing.Details` 中部分字段记录多个批次累计的准备或推理工作时间；阶段存在并发时，工作时间之和不能当作墙钟总耗时。也不要把不同测试的 P50/P95 分别相加得到一个“总时间”。

正式比较应固定图片、模型、输入规格、后端、Batch 和通道配置，先预热，再重复测量 P50/P95，并同时检查结果是否变化。仓库中的设备实测包含 OCR 完整流水线与相应组合，GPU 历史结果不应直接标成这台 CPU 的结果。

```text
Batch、Session 池和并发：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/batch-session-concurrency.md

设备性能实测：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/device-performance-benchmarks.md

性能测试方法：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/performance-benchmarking.md
```

### 9.3 长文本、低分行与区域 OCR

本篇先使用基础配置。实际数据有超长文本时，可按需要选择动态宽度、宽度分桶、溢出拒绝或有重叠的长行窗口；低分行也可以评估方向重试、宽度重试和裁剪增强。这些策略会改变计算量和输出，应使用自己的标注数据评估，不能简单地把所有增强全部打开。

已有外部文字框、表格单元格或版面区域时，也不必再次运行整页 DET。可以使用仅识别流水线，按给定区域执行 REC。复杂表格、公式、印章和图表属于 PP-Structure 的后续案例，本篇的通用 OCR 不会自动把表格文字还原成 HTML 结构。

## 十、常见问题与处理方法

| 现象 | 优先检查 | 处理方向 |
| --- | --- | --- |
| 找不到模型、字典或图片 | 工作目录、所选 `variant`、第三个参数的模型根目录 | 按第五节下载所选组合，保留对应标准文件名 |
| 原生 DLL 加载失败 | `x64`、OpenCV runtime、ORT CPU 原生包和本机依赖 | 使用本文同版本组合；缺少 MSVC 运行库时按异常提示补齐 |
| Ubuntu 提示 `.so` 或版本化依赖找不到 | 对应 Linux runtime 是否已还原、native 目录是否在进程搜索路径 | 按第 4.2/8.5 节设置 `LD_LIBRARY_PATH`，结合 `ldd` 检查真实依赖；不要给 Linux 进程使用 Windows DLL |
| 输出张量名称不匹配 | 模型是否来自本文这组 Release，是否替换了导出图 | 对照实际张量名配置 Profile，不只修改文件名 |
| REC 类别数不一致或文字乱码 | 模型和字典是否配套、空格与 blank 的处理 | 保持字典顺序，确认空格类是否由加载器追加 |
| 图片里有字，但没有框 | 检测输入尺寸、颜色、归一化及 DB 阈值 | 先用示例模型和图片复现，再针对小字与低对比度逐项调整 |
| 文字被截断或长行错误较多 | 文本框边界、裁剪比例、识别最大宽度与溢出策略 | 对照原图检查；按需要评估长行窗口或宽度策略 |
| 中文在结果图上显示问号 | 是否使用 OpenCV 内置 Hershey 字体画中文 | 保留编号，中文在界面或支持中文字库的组件中绘制 |
| 加大 Batch 或 Session 后更慢 | 宽度补边、CPU 线程争抢、模型固定 Batch、内存占用 | 固定输入比较完整流水线，逐个调整配置 |
| 换成 TensorRT/OpenCV 后不能原样运行 | Engine、后端注册、Batch/Shape 与适配路径 | 按具体后端教程准备，先核对精确模型验证状态 |

遇到异常时保留完整错误及所在阶段。检测导入失败、裁剪失败和字典不匹配，需要处理的是不同部分；盲目降低阈值通常不能解决加载或输入输出问题。

```text
OCR 基础教程：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/visual-ocr.md

PaddleOCR 三模型流水线：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/visual-paddle-ocr3.md

模型与后端验证矩阵：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/model-backend-verification-matrix.md
```

## 十一、总结与后续

这篇完成了从空项目、按需准备模型，到 **DET → 文本行裁剪 → CLS → REC → 原图区域与文字结果** 的完整流程；相同 C# 基础代码在 Windows 与 Ubuntu 运行，并在 Ubuntu 对照了四种后端。使用正式版 NuGet，应用可以复用配套处理，同时保留模型选择、字典、Batch、独立通道和区域结果的控制。

大家可以先用 v5 Mobile 复现，再按自己的设备和图像评估 v4、v6 或 Server 组合；有了文字与原图坐标，就可以继续接入界面、业务规则或文档区域处理。模型部署成功后，仍要用实际数据检查文字是否正确，特别是小字、低分行和长文本。

接下来会展开多后端与离线部署，再进入 PP-Structure 的版面区域、表格结构与 HTML 输出，以及更完整的页面任务组合。

```text
本文主稿与配图：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/blog/2.0.0/2026-10/2026-10-28-03-paddleocr-pipeline.md

系列目录：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/blog/2.0.0/README.md

问题反馈：
https://github.com/guojin-yan/DeploySharp/issues
```

DeploySharp 库代码采用 Apache License 2.0；PaddleOCR 模型、字典和第三方运行库遵循各自许可。反馈问题时请附上组合、后端、环境、输入与最小复现代码，方便定位。
