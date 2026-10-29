# 在 C# 中使用 PP-Structure：从页面区域到表格 HTML

> DeploySharp 2.0.0 系列 · 第 06 篇实战教程。基于正式版 NuGet `2.0.0`、源码 Tag `v2.0.0`，创建与修订：2026-10-29。

![DeploySharp 2.0.0 PP-Structure 实战：在 C# 中还原文档结构，完成页面区域、OCR 和表格 HTML](assets/06-paddle-structure/cover.png)

## 一、DeploySharp 项目简介

**DeploySharp** 是一个面向 **C#/.NET 开发者**的开源模型部署框架，由「椒颜皮皮虾」开发，项目源码遵循 **Apache License 2.0**。它基于 **ONNX Runtime、OpenVINO、TensorRT、OpenCV DNN** 等推理引擎，将模型加载、图像前处理、推理执行和结果后处理组织成可复用的接口，方便大家在桌面软件、工业视觉和文档应用中接入模型。

目前项目已接入目标检测、分类、实例分割、关键点、旋转框、异常检测、背景移除等视觉任务，也提供 **PP-OCRv4/v5/v6、PP-Structure 文档处理**及部分图文、本地语言模型的使用入口。常用模型可以直接使用配套前后处理，自有模型可以配置输入输出、缩放、颜色和归一化，也可以扩展自己的解码器和结果类。

2.0 版本增加了按需组合的 NuGet 包、Batch 与独立 Session 池、异步输入、ROI 和大图重叠切片等能力。支持的 TensorRT 路径还可以使用 CUDA 前后处理与缓冲复用；具体模型、平台和后端的可用范围分别记录。

**项目源码与 NuGet：**

```text
项目源码：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0

NuGet（搜索 JYPPX.DeploySharp）：
https://www.nuget.org/packages?q=JYPPX.DeploySharp
```

当前正式版主要验证 Windows 10/11 x64，其他系统及各模型的后端状态请查看仓库验证矩阵。库代码、第三方引擎和模型分别遵循各自的许可。

第 03 篇已经讲过完整 OCR。这一篇继续往前走：**当输入是一页报纸、一张文档或一块表格时，除了文字，我们怎样在 C# 中获得区域类别、单元格位置和可使用的 HTML？**

## 二、从“识别文字”到“组织文档”

### 2.1 为什么只有 OCR 还不够

普通 OCR 可以返回文字、置信度和文字框。但在文档应用里，我们往往还要知道：哪些内容属于标题，哪些属于正文，哪里是一张图，哪里是一块表格，以及表格中的文字应该放进哪一行、哪一列。

这些任务需要组合多个模型。下面先把普通 OCR 和文档结构化的关系放到一起。

![普通 OCR 识别文字与位置；文档结构化先划分版面，再按区域组织文字识别、表格结构和其他专用任务](assets/06-paddle-structure/document-workflow.png)

**PP-Structure 是一组文档处理能力的组合。** 本篇使用 PaddleOCR 体系中的版面、表格及文字模型，在 DeploySharp 中组装两条可以独立运行的路径：

| 命令 | 输入 | 处理路径 | 最终产物 |
| --- | --- | --- | --- |
| `page` | 多栏文档页面 | PP-DocLayout-L → 文字类区域 → 区域内 DET + REC | 版面框图、文字行框图、分区 TXT、区域与文字 JSON |
| `table` | 已裁出的单张表格 | 表格分类 → 单元格检测 → SLANeXt → 对齐 → REC → HTML | 单元格图、结构 JSON、对齐诊断、文字 JSON、带文字 HTML |

这两个案例分别讲清页面区域处理和表格内部处理。`page` 不自动将所有表格区域送入 `table`，也不宣称复刻官方 PP-StructureV3 的完整产品流水线。业务上可以再把版面检测得到的 `table` 区域裁出，交给后者处理，并保留坐标变换。

### 2.2 其他文档模块放在哪里

方向分类用于纠正整页方向，UVDoc 用于文本图像矫正；公式、印章和图表区域需要各自的模型与结果处理。PaddleOCR-VL 则属于视觉语言解析路径，有独立的 Bundle、生成配置和运行成本。

| 模块 | 在实际应用中的位置 | 本篇范围 |
| --- | --- | --- |
| 文档方向、UVDoc 矫正 | 页面分析之前，按输入质量选择 | 解释接入位置，不加载模型 |
| PP-DocLayout | 确定页面有哪些区域 | 实跑 L 模型 |
| PP-OCR DET / REC | 对文字区域定位文字行，再识别 | 实跑 v5 Mobile，不加方向 CLS |
| 表格分类、单元格、SLANeXt | 选择表格模型，恢复结构，定位格子 | 实跑有线表格 |
| 公式、印章、Chart2Table | 按版面类型进入专用阶段 | 本篇不测试质量和后端 |
| PaddleOCR-VL | 独立的视觉语言解析任务 | 本篇不加载、不比较 |

我们的输入已经朝向正确，省去了方向分类；页面文字区域仍可能包含多行，因此必须先 DET，再 REC。表格示例中的单元格主要是单行文字，才使用 REC-only；多行格子要改为在格内运行 DET + REC。

## 三、环境与 NuGet：按使用场景安装

### 3.1 本次实际验证环境

| 项目 | Windows 实测 | Ubuntu 实测 |
| --- | --- | --- |
| 操作系统 | Windows 11 x64，`10.0.26200` | Ubuntu `22.04.5 LTS` x86_64 |
| CPU | AMD Ryzen 7 5800H，8 核 16 线程 | Intel Core i7-1165G7，4 核 8 线程 |
| .NET | SDK `10.0.401`，运行时 `10.0.12` | SDK `10.0.301`，运行时 `10.0.9` |
| DeploySharp | 正式 NuGet `2.0.0`，`net10.0` | 同一份 C# 源码与正式 NuGet `2.0.0` |
| ORT | `Microsoft.ML.OnnxRuntime 1.28.0`，CPU | 同版本，CPU |
| OpenVINO | Windows runtime `2026.2.1`，CPU | Ubuntu runtime `2026.3.1`，CPU |
| 图像组件 | JYPPX OpenCV Windows x64 runtime `5.0.0` | JYPPX OpenCV Ubuntu 22.04 x64 runtime `5.0.0` |
| 调度 | 文档模型 Batch=1；REC 上限 16；单 Session，区域按顺序执行 | 相同配置 |

本文在两个系统上分别验证了两张图片、两个 CPU 后端的完整功能路径，没有进行预热、重复采样和性能排名。页面流水线按区域逐次执行 DET，是便于接入业务的示例配置，不能据此代表整页一次 OCR 或并发调度的最优速度。

### 3.2 Windows：只用 ONNX Runtime CPU

先创建一个 x64 控制台工程，然后安装图像层和 ORT 后端：

```powershell
dotnet new console -n DeploySharpStructureDemo -f net10.0
cd DeploySharpStructureDemo

dotnet add package JYPPX.DeploySharp.Visual.OpenCV --version 2.0.0
dotnet add package JYPPX.OpenCV.runtime.win-x64 --version 5.0.0
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

在 `.csproj` 的 `PropertyGroup` 中增加 `<PlatformTarget>x64</PlatformTarget>`。Visual、Core 等依赖会自动引入，无须为版面、表格分类、单元格和结构识别各安装一个任务包。

### 3.3 Windows：需要 OpenVINO，或者想切换两个后端

OpenVINO 场景保留公共图像层，选择 OpenVINO 后端和原生运行时。**随文完整工程同时引用 ORT 和 OpenVINO**，所以在上面的基础上再安装：

```powershell
dotnet add package JYPPX.DeploySharp.Backend.OpenVINO --version 2.0.0
dotnet add package OpenVINO.runtime.win --version 2026.2.1
```

只保留 ORT 的业务项目可以使用前面四个包，同时删除示例中的 `using JYPPX.DeploySharp.Backends.OpenVINO`、`openvino` 分支和 `UseOpenVino()` 调用。直接下载完整示例时保留六个包，即可按参数切换，不必先改源码。

下面这张图说明每一层的职责。**OpenCV 在这里负责读图、裁剪和绘制，模型推理分别由 ORT 或 OpenVINO 完成。**

![公共图像层与 ORT、OpenVINO 后端的安装选择，原生运行时按 Windows 或 Ubuntu 分别选择](assets/06-paddle-structure/package-stack.png)

### 3.4 Ubuntu：保留 C# 代码，选择 Linux 原生包

**同一个随文工程可以直接在 Ubuntu 22.04 x64 构建。** 托管层和 ORT 包不变，图像层与 OpenVINO 选择各自的 Linux 运行时：

| 职责 | Windows 包 | Ubuntu 包 |
| --- | --- | --- |
| 图像原生库 | `JYPPX.OpenCV.runtime.win-x64 5.0.0` | `JYPPX.OpenCV.runtime.ubuntu.22.04-x64 5.0.0` |
| OpenVINO 原生库 | `OpenVINO.runtime.win 2026.2.1` | `OpenVINO.runtime.ubuntu.22-x86_64 2026.3.1` |

随文 `.csproj` 根据当前系统选择包；显式传入 `RuntimeIdentifier` 时按该值选择，当前配置覆盖 `win-x64` 和 `linux-x64`。这里的 Linux 包针对 **Ubuntu 22.04 x64**，不应直接当作所有 Linux 发行版的通用安装组合。

工程还通过 MSBuild 将包里的 `.so` 复制到本地输出目录：OpenCV 放在 `native/opencv`，OpenVINO 放在 `native/openvino`。运行前设置本进程的 `LD_LIBRARY_PATH` 即可，后面给出完整 Bash 命令；读者不用查找或修改 NuGet 缓存里的文件。

如果从空工程开始，建议采用随文的完整 `.csproj`，它同时包含条件选包与原生文件复制配置。仅执行 `dotnet add package` 并不能替代这部分部署设置。两套系统安装 .NET SDK 10 即可，不要求 SDK 小版本与本次设备完全相同。

## 四、准备模型、图片与随文源码

### 4.1 按任务下载，不必下载整个模型集合

模型集中存放在 `models-paddleocr` Release。进入页面后，按文件名下载需要的 ONNX 和识别字典：

```text
https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr

单文件地址前缀（后面接完整文件名）：
https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr/
```

| 文件 | `page` | `table` | 用途 |
| --- | --- | --- | --- |
| `pp-doclayout-l.onnx` | 必需 | — | 23 类版面检测 |
| `ppocrv5-mobile-det.onnx` | 必需 | 单行表格不需要 | 区域内文字检测 |
| `ppocrv5-mobile-rec.onnx` | 必需 | 必需 | 文字行或单元格文字识别 |
| `ppocrv5_dict.txt` | 必需 | 必需 | 与 v5 REC 配套的字典 |
| `pp-lcnet-x1-0-table-cls.onnx` | — | 必需 | 有线 / 无线表格分类 |
| `rt-detr-l-wired-cell-det.onnx` | — | 有线表格必需 | 独立单元格位置 |
| `slanext-wired.onnx` | — | ORT 有线表格必需 | 表格结构序列 |
| `slanext-wired-openvino-compat.onnx` | — | OpenVINO 有线表格必需 | 与当前 OpenVINO 路径匹配的兼容图 |

分类为 `wireless_table` 时，代码选择对应的 `rt-detr-l-wireless-cell-det.onnx` 和 `slanext-wireless.onnx`；OpenVINO 使用 `slanext-wireless-openvino-compat.onnx`。需要处理这类输入，再下载三个对应文件。本篇实测输入属于有线表格，不能将结果扩展为无线模型也经过本次验证。

例如，只下载一个版面模型可以这样写：

```powershell
New-Item -ItemType Directory -Force models, images | Out-Null
$base = 'https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
Invoke-WebRequest "$base/pp-doclayout-l.onnx" -OutFile models/pp-doclayout-l.onnx
```

Ubuntu 的 Bash 对应写法：

```bash
mkdir -p models images
model_base='https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
curl -fL "$model_base/pp-doclayout-l.onnx" -o models/pp-doclayout-l.onnx
```

其他模型同样按表格中的文件名下载。也可以使用自己的本地 ONNX：示例直接从 `models` 路径加载，**不要求读者计算模型或图片摘要**。更换模型时要同步核对 Profile、类别、张量名和预处理，不能只把任意文件改成同名。

### 4.2 使用官方公开示例图

本文页面图为 `1524 × 1368` 的多栏报纸，包含标题、正文和图片；表格图为 `551 × 132`，含中英文、数字和首行合并单元格。图片地址如下：

```text
多栏页面：
https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/pp_structure_v3_demo.png

有线表格：
https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/table_recognition.jpg
```

保存到 `images` 目录：

```powershell
Invoke-WebRequest 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/pp_structure_v3_demo.png' `
  -OutFile images/pp_structure_v3_demo.png
Invoke-WebRequest 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/table_recognition.jpg' `
  -OutFile images/table_recognition.jpg
```

Ubuntu 使用同样的两张输入：

```bash
mkdir -p images
curl -fL 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/pp_structure_v3_demo.png' \
  -o images/pp_structure_v3_demo.png
curl -fL 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/table_recognition.jpg' \
  -o images/table_recognition.jpg
```

换成自己的图片时只需修改命令中的输入路径。PDF 应先使用 PDF 渲染组件转成页面图，再逐页处理；本例不直接读取 PDF。

### 4.3 完整代码在哪里

为了让正文集中解释关键环节，参数处理、模型配置、绘图和文件保存放在随文工程里。下载目录中的 `.csproj` 和三个 `.cs` 文件后，可直接还原运行；也可以将三个源码文件放入前面创建的工程。

```text
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0/2026-10/examples/06-paddle-structure
```

```text
06-paddle-structure/
  PaddleStructure.csproj     # 正式 NuGet 2.0.0，按系统选择原生包，含两个后端
  Program.cs                # 命令、取消、退出码
  StructureDemo.cs          # 模型、两条任务路径、结果保存与绘图
  PageRoiOcrInput.cs         # 共享原图的区域 DET + REC 输入适配器
  models/                   # 自行下载，或放入自己的匹配模型
  images/                   # 输入图片
  results/                  # 程序实际生成
```

后面代码段摘自这个工程，标出的辅助方法也在工程内提供。`models/images/results` 为本地运行目录，不随源码提交。

## 五、先把每个模型的预处理配对

这几个模型虽然都属于 PaddleOCR 文档体系，输入约定却不同。比如 PP-DocLayout-L 使用 RGB 直接缩放，而 SLANeXt 使用 BGR、最长边等比例缩放和右下补齐。LCNet 又需要先短边缩放，再中心裁剪。

![PP-DocLayout-L、RT-DETR、LCNet、SLANeXt、OCR DET 与 REC 的输入几何、颜色和归一化差异](assets/06-paddle-structure/model-preprocessing.png)

版面模型通过以下 Profile 选择类别、输入大小、几何辅助输入和阈值：

```csharp
var descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
    descriptor,
    PaddleDocumentProfiles.Layout23Labels,
    new VisualSize(640, 640),
    includeGeometryInputs: true,
    scoreThreshold: .5f);
```

这里的导出图包含后置 NMS，结果按 `[class, score, x1, y1, x2, y2]` 解析，并有对应数量输出，不应套用 YOLO 原始候选框的 decoder。类别顺序使用 `Layout23Labels`，更换 Plus-L 等其他模型时应核对其专用类别表。

图像输入交给文档预处理适配器创建，辅助张量也随之生成：

```csharp
var factory = new OpenCvVisualInputFactory();
OpenCvImageSource source = OpenCvImageSource.FromFile(Path.GetFullPath(imagePath));
using OpenCvDecodedRoiImage image = factory.DecodeForRois(source);

using PreparedVisualInput input =
    OpenCvPaddleDocumentPreprocessing.CreateFromPage(
        image, profile, source.Sha256, cancellationToken);

VisualInferenceResult inference =
    await layout.RunAsync(input, cancellationToken: cancellationToken);
DetectionResult regions = inference.GetValue<DetectionResult>();
```

`layout` 由随文工程的 `DocumentSession` 辅助方法创建：注册 Profile，使用普通本地路径创建 `ModelArtifact`，再创建 `VisualPipeline`。`source.Sha256` 是图像对象自动提供的身份信息，用于关联同页阶段结果，无须用户手算或填入模型摘要。

`CreateFromPage` 同时处理图像输入与 `im_shape/scale_factor`。不要为了让推理“先跑起来”而随意把比例填成 1；几何辅助输入和图像变换必须一致，否则框的位置可能错误。

## 六、页面路径：版面区域 → 区域内 OCR

### 6.1 先看真实版面结果

本次 `PP-DocLayout-L` 在 `0.5` 阈值下返回 **31 个区域**：30 个属于本文选取的文字类别，另一个是 `image`。下面左边为输入，右边为同一次推理的原图坐标结果。

![多栏页面的真实版面检测结果，原图与 31 个区域框对照，包含文字类型和一个图片区域](assets/06-paddle-structure/layout-result.png)

代码依据 `Category` 决定是否进入 OCR，选择了正文、文档标题、段落标题、页眉页脚等文字类型；图片区域不进入文字识别。其他业务可以在这里将表格、公式、印章区域交给专用处理器。

### 6.2 为什么一个段落框不能直接送 REC

REC 通常识别一条文字行。版面框可能包含十几行，如果把整个段落压成高 48 像素的输入，多行内容会挤在一起。正确路径是：**先在段落内 DET 得到文字行，再按文字四边形裁剪并组批 REC。**

这时还涉及两套坐标：检测器处理的是区域图像，最终业务需要的是整页位置。随文示例让 `PrepareRectangle` 保留裁剪与缩放变换，decoder 将文字框还原到整页；REC 则共享原图，直接按整页四边形裁文字行。

![版面 ROI 上准备 DET 张量，文字框通过变换还原整页坐标，再从共享原图裁剪和批量识别](assets/06-paddle-structure/roi-coordinates.png)

循环内的主要代码如下，`Clip`、`ocr` 和 `det` 的完整定义见 `StructureDemo.cs`：

```csharp
RectangleF box = Clip(region.Bounds, image.SourceSize);
var options = OpenCvStage19Preprocessing
    .CreatePaddleOcrOfficialInferenceDetectionOptions(
        new VisualSize((int)box.Width, (int)box.Height), resizeLong: 960);

PreparedVisualInput detectorInput = image.PrepareRectangle(
    new RectangleRoiGeometry(box),
    det.VisualProfile.Input.Name, options, cancellationToken);

using var input = new PageRoiOcrInput(image, detectorInput);
OcrResult result = await ocr.RunAsync(
    input, cancellationToken: cancellationToken);
```

`PageRoiOcrInput` 是随文提供的输入适配器，实现 `IOcrImageInput`。它将 ROI 检测张量和 `image.CreateRecognitionInput()` 提供的整页识别视图组合在一起；识别组批直接委托给该视图。它拥有这两个对象，`Dispose()` 时释放它们，整页图像由外层 `using` 维护。

这条路径避免了“每个区域先编码成 JPEG/PNG，再重新解码”的中间步骤。**推理张量准备、透视裁剪和归一化仍有计算与分配**，不能把它描述为整条链路零拷贝。文字框已是整页坐标，后面绘图时也不要再加一次 ROI 偏移。

### 6.3 用文档流水线连接阶段

文档编排负责顺序、依赖、页码和阶段结果；每个阶段仍由我们调用对应模型。页面案例注册版面阶段，再注册依赖版面输出的文字阶段：

```csharp
var pipeline = new PaddleDocumentPipeline(
    new IPaddleDocumentPipelineStage[] { layoutStage, ocrStage });

PaddleDocumentPipelineResult result = await pipeline.RunAsync(
    new PaddleDocumentPage(source, image.SourceSize), cancellationToken);

var layoutResult = result.GetRequired<PaddleDocumentRegionResult>(
    PaddleDocumentModule.LayoutDetection);
var textResult = result.GetRequired<PaddleDocumentTextResult>(
    PaddleDocumentModule.TextRecognition);
```

`layoutStage` 使用 `PaddleDocumentPipelineStage` 调用版面模型；`ocrStage` 使用 `PaddleDocumentDependentStage` 声明依赖，再遍历文字类版面框。两个阶段的完整实现都在 `RunPageAsync` 中，未省略成需要读者自行补齐的空回调。

每个 OCR 区域保存父版面索引，逐行结果另存四边形、文字和置信度，方便后续点击定位、人工纠错和业务字段提取。本文按框的 `Y/X` 做几何展示排序，**没有实现多栏报纸的完整阅读顺序推断**；输出区域索引也不是阅读顺序。

### 6.4 真实文字只节选，完整内容写入文件

此次 Windows、Ubuntu 的两种后端均得到 **30 个文字区域、142 行文字**。下面展示 Windows ORT 的文字行框及前 8 行输出，其余内容由程序保存，不在文章中全部展开。

![真实区域 OCR：142 行文字的原图框与 8 行输出节选，完整文字和坐标保存到文件](assets/06-paddle-structure/region-ocr-result.png)

```text
[29/doc_title] 搭建友谊桥梁
[29/doc_title] 助力双方交往
[19/text] 厄立特里亚高等教育与研究院合作建立，开
[19/text] 设了中国语言课程和中国文化课程，注册学
……此处仅节选，完整结果见 page.txt 和 page-lines.json
```

这些内容是程序原样输出，没有手工补全或修正。标题在图中位于页面右侧，展示排序先列出它，并不表示整页应从右边按这套顺序阅读。业务若要恢复文章栏序，应在版面结果之上增加阅读顺序和段落关联逻辑。

## 七、表格路径：分类 → 单元格与结构 → 文字填充

### 7.1 三类结果分别负责什么

表格处理要同时解决结构、位置和内容。**SLANeXt 输出结构序列；RT-DETR cell 输出可裁剪的格子位置；REC 输出文字。** 表格分类在前面决定使用 wired 还是 wireless 模型对。

下面这张图将模型路径和失败出口一起画出。只有对齐通过，才能把文字写进结构槽位。

![表格分类选择模型，单元格位置与 SLANeXt 结构经对齐检查后识别文字并填 HTML；失败保留结构和诊断](assets/06-paddle-structure/table-alignment.png)

分类与结构 Profile 的创建分别是：

```csharp
var clsProfile = PaddleDocumentProfiles.CreateClassification(
    PaddleDocumentModelCatalog.Get("paddle-table/pp-lcnet-x1-0-table-cls"),
    new[] { "wired_table", "wireless_table" },
    VisualTaskId.TableClassification);

var structureProfile = PaddleDocumentProfiles.CreateTableStructure(
    PaddleDocumentModelCatalog.Get("paddle-table/slanext-wired"),
    modelSize: new VisualSize(512, 512));
```

完整代码根据分类标签拼接对应模型名，不把有线模型强行用于无线输入。RT-DETR 的 Profile 与版面路径同样使用 `CreatePaddleNmsRegions`，类别为 `table-cell`，输入 `640 × 640`，包含几何辅助输入，本例阈值为 `0.3`。

### 7.2 先检查对齐，再裁图识别

表格的结构顺序和检测输出顺序不一定一致。示例使用 `PaddleDocumentTableCellAligner`，检查同页同源、结构完整、单元格数量以及行几何，再建立 `StructureCellIndex → DetectionIndex` 对应。

```csharp
var table = result.GetRequired<PaddleDocumentTableResult>(
    PaddleDocumentModule.TableStructureRecognition);
var cells = result.GetRequired<PaddleDocumentRegionResult>(
    PaddleDocumentModule.TableCellDetection);

var alignment = PaddleDocumentTableCellAligner.Align(
    table, cells, image.SourceSize.Width, image.SourceSize.Height);

if (!alignment.Succeeded)
{
    // 完整工程先保存结构、检测图和诊断，再返回非零退出码。
    throw new InvalidOperationException(
        "Cell alignment failed: " + string.Join(", ", alignment.Diagnostics));
}
```

这个检查不会证明模型识别的每一个格子都正确。它提供的是明确的自动填充前提，避免结构有 13 个槽位、检测却只有 12 个框时，程序仍按数组顺序硬塞文字。

当前对齐器支持本例的 `colspan`，但**不会自动处理未验证的 `rowspan` 跨行布局**。遇到跨行、格子漏检、重复框或行几何不明确，应该查看诊断，增加业务匹配策略或交由人工处理。

### 7.3 从匹配的格子识别文字，再填 HTML

对齐后，根据检测框构造 `TextRegion`，把 `StructureCellIndex` 作为区域索引。`CellRegion` 辅助方法会裁定边界，保留小数坐标并创建四边形；不要从 SLANeXt 的异常框盲目裁图。

```csharp
using IOcrRecognitionImageInput input = image.CreateRecognitionInput();
var regions = alignment.Matches
    .Select(match => CellRegion(match, image.SourceSize)).ToArray();

IReadOnlyList<OcrRegionResult> recognized =
    await recognizer.RecognizeOnlyAsync(input, regions, cancellationToken);

var byIndex = recognized.ToDictionary(x => x.Region.SourceIndex);
string[] texts = Enumerable.Range(0, alignment.StructureCellCount)
    .Select(i => byIndex[i].Recognition.Text).ToArray();

string tableElement = PaddleDocumentTableCellAligner.FillMarkup(
    table, alignment, texts);
```

`recognizer` 使用 v5 Mobile REC、对应字典和 `CreateRecognitionOnly` 创建，Batch 上限 16。`FillMarkup` 按结构索引填充，并转义文字里的 HTML 特殊字符。保存时再添加 UTF-8 声明、页面外壳和样式，得到可由浏览器打开的 `table.html`。

这里的单元格多数只有一行，因此直接使用 REC。自己的表格如果包含多行，可以复用页面案例的思路：以单元格为 ROI 运行 DET + REC，将格内各行合并后，仍按同一个结构索引填充。

### 7.4 真实结果与需要保留的提示

本次表格分类为 `wired_table`，结构为 **4 行、13 个单元格**，第一行 `colspan="4"`。下图分别展示原图、独立单元格框，以及实际生成的 HTML 浏览器截图。

![官方表格输入、13 个独立单元格框和带文字 HTML 的真实浏览器截图，保留原始识别误差与空格子](assets/06-paddle-structure/table-result.png)

控制台输出如下：

```text
table: backend=ort-cpu; type=wired; rows=4; cells=13; alignment=True
Structure warnings: table-cell-geometry-outside-source:12/13
```

**警告仍然存在。** 当前 SLANeXt 结果中有 12/13 个结构单元格的几何范围超出源图，示例没有拿这些框做裁剪，而是保留结构序列，用独立 RT-DETR 框定位文字。因此本次结构和检测框可以对齐、HTML 可以填充，同时也必须保留异常几何提示。

文本没有经过人工润色，图中保留了 `连续工作3` 等原始识别文字，也保留了空字符串。空值可能来自原本空白的单元格，也可能来自识别失败，应与原图核对。对齐通过、HTML 能打开与文字完全正确是不同的判断；本文没有逐格人工标注或计算准确率。用于金额、编号或业务入库时，应结合原图、置信度、字段规则和人工复核。

## 八、直接运行，并找到完整结果

### 8.1 Windows：四条实际执行命令

进入完整随文工程目录，按前面的文件名准备 `models` 和 `images`，执行：

```powershell
dotnet restore

dotnet run -c Release -- page images/pp_structure_v3_demo.png models ort-cpu # 页面：ORT CPU

dotnet run -c Release --no-build -- table images/table_recognition.jpg models ort-cpu # 表格：ORT CPU

dotnet run -c Release --no-build -- page images/pp_structure_v3_demo.png models openvino # 页面：OpenVINO

dotnet run -c Release --no-build -- table images/table_recognition.jpg models openvino # 表格：OpenVINO
```

OpenVINO 表格路径会自动选择 `-openvino-compat.onnx` 文件。这个派生图用于避开原始 SLANeXt 图在当前 OpenVINO 路径中的 `Loop` 导入限制，不能把原图简单改名当成兼容图。

输入替换为自己的文件，例如：

```powershell
dotnet run -c Release --no-build -- page images/my-document.png models ort-cpu
dotnet run -c Release --no-build -- table images/my-cropped-table.jpg models ort-cpu
```

程序支持 `Ctrl+C` 取消；成功退出码为 `0`，取消为 `2`，其他失败为 `1`。表格对齐失败时会保留诊断和结构展示文件，但不会输出冒充成功的文字表格。

### 8.2 Ubuntu：同一份源码运行两种 CPU 后端

在 Ubuntu 22.04 x64 安装 .NET SDK 10，进入随文工程目录，准备相同的模型与图片。先构建，再把输出目录中的两组原生库加入**当前终端进程**的搜索路径：

```bash
dotnet --info
dotnet restore
dotnet build -c Release --no-restore

export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"

dotnet run -c Release --no-build -- page images/pp_structure_v3_demo.png models ort-cpu
dotnet run -c Release --no-build -- table images/table_recognition.jpg models ort-cpu
dotnet run -c Release --no-build -- page images/pp_structure_v3_demo.png models openvino
dotnet run -c Release --no-build -- table images/table_recognition.jpg models openvino
```

如果按用户目录安装了 SDK，终端提示找不到 `dotnet`，先执行 `export PATH="$HOME/.dotnet:$PATH"`。上述原生库路径基于本例的 `Release/net10.0` 输出；改变构建目录或交付目录时也要相应调整，部署到服务时将搜索路径配置在服务进程中。

这四条命令已在前述 Ubuntu 设备实际执行成功。下面节选自 ORT CPU 的控制台输出；OpenVINO 的计数和对齐结果相同：

```text
page: backend=ort-cpu; layout_regions=31; ocr_blocks=30; text_lines=142
table: backend=ort-cpu; type=wired; rows=4; cells=13; alignment=True
Structure warnings: table-cell-geometry-outside-source:12/13
```

下面的结果汇总图使用 **Ubuntu 实际生成的文字行框图与单元格框图**，并展示保存结果中的文字和 HTML 片段。它不是桌面截图，图中结果没有手工改字。

![Ubuntu 22.04 上 PP-Structure 的真实结果汇总：ORT 与 OpenVINO CPU 都完成页面 OCR 和表格 HTML，文字与 Windows 基线逐项一致](assets/06-paddle-structure/ubuntu-result.png)

两个系统运行相同的 C# 源码，只切换原生运行时。此次 Ubuntu 使用 OpenVINO `2026.3.1`，Windows 为 `2026.2.1`；这里只记录功能结果，不据此判断平台或后端的速度优劣。

### 8.3 输出目录与用途

```text
results/
  ort-cpu/
    page/
      page.json             # 文档阶段结果、页码、版面和分区文字
      page-lines.json       # 每条文字的父区域、类别、置信度、源图四边形
      page.txt              # 分区文字，供查看，不宣称完整阅读顺序
      layout.jpg            # 版面检测框与类型
      page-ocr.jpg          # 文字行框
    table/
      table.json            # 分类、单元格、结构、原始警告
      alignment.json        # 数量、结构索引、检测索引与诊断
      cell-texts.json       # 成功时保存各格文字与置信度
      cells.jpg             # 独立单元格框与结构索引
      table.html            # 成功时为带文字表格；失败时显示结构及失败提示
  openvino/
    page/                   # 同样的文件布局
    table/
```

再次运行同一个后端和模式会覆盖该目录下的同名结果。想保留多张图的结果，可以为每个输入生成独立输出子目录；文章示例不自动做批量归档。

### 8.4 本次验证范围

| 系统与后端 | 页面版面 / 文字区域 / 文字行 | 有线表格行 / 格子 | HTML 填充 | 结构框警告 |
| --- | --- | --- | --- | --- |
| Windows · ORT CPU `1.28.0` | 31 / 30 / 142 | 4 / 13 | 对齐通过并填充 | 12/13 框越界 |
| Windows · OpenVINO CPU `2026.2.1` | 31 / 30 / 142 | 4 / 13 | 使用兼容图，对齐通过并填充 | 12/13 框越界 |
| Ubuntu · ORT CPU `1.28.0` | 31 / 30 / 142 | 4 / 13 | 对齐通过并填充 | 12/13 框越界 |
| Ubuntu · OpenVINO CPU `2026.3.1` | 31 / 30 / 142 | 4 / 13 | 使用兼容图，对齐通过并填充 | 12/13 框越界 |

对本次保存结果逐项比较，四组的 **142 行 OCR 字符串和 13 个单元格字符串均与 Windows ORT CPU 基线相同**；未将坐标、置信度或浮点张量宣称为逐位一致。这只反映这两个输入的功能结果，不代表所有模型、所有输入的后端精度相同。

本次没有跑 TensorRT、OpenCV DNN、无线表格、方向矫正或其他文档任务，也没有进行 CER/WER、TEDS 等数据集评测。需要判断更大范围的支持状态，查看仓库矩阵：

```text
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/model-backend-verification-matrix.md
```

## 九、接入自己的文档应用时，先处理这几个问题

### 9.1 常见现象与检查位置

| 现象 | 优先检查 | 处理方向 |
| --- | --- | --- |
| 提示缺少模型或字典 | 传入的 `models` 路径、Release 文件名、分类结果 | 只下载需要的文件；wireless 输入准备对应模型对 |
| 提示缺少输入张量，或者框错位 | 是否用了正确 Profile 和文档预处理适配器 | 同步 `image/im_shape/scale_factor`，检查缩放与坐标变换 |
| 版面类别明显不对 | 模型与标签顺序 | L/M/S 的 23 类不能随意套给其他标签体系 |
| 一个段落只识别出零碎文字 | 是否把多行版面块直接交给 REC | 改为区域内 DET + REC，检查输入清晰度和缩放 |
| Ubuntu 提示无法加载 OpenCV 或 OpenVINO `.so` | 系统架构、选包、输出文件和进程搜索路径 | 使用完整 `.csproj`，检查 `native` 目录与 `LD_LIBRARY_PATH`，再用 `ldd` 查缺失依赖 |
| 构建出现 `NETSDK1206` | OpenCV 包中的发行版 RID `ubuntu.22.04-x64` | .NET 8+ 不默认选择该 RID；本例显式复制原生文件，继续确认输出和加载结果 |
| OpenVINO 导入 SLANeXt 失败 | 是否实际加载了兼容 ONNX | 使用 Release 中的对应兼容图，勿只重命名原图 |
| `alignment.Succeeded == false` | `alignment.json` 的诊断和 `cells.jpg` | 检查漏框、重复框、跨行结构与行几何，不盲目填充 |
| 表格可以显示，但格内文字有误 | 原图、字典、裁剪、REC 结果 | 多行格改走 DET + REC；关键字段增加规则与复核 |

本次 Ubuntu 构建出现了上表中的 `NETSDK1206` 警告，随后通过显式部署原生库跑通四条路径，没有将警告隐藏。需要排查 Linux 动态依赖时，可在工程目录执行：

```bash
ldd bin/Release/net10.0/native/opencv/libJYPPX.OpenCV.Native.so
ldd bin/Release/net10.0/native/openvino/libopenvino_c.so
```

重点查找 `not found`，按缺失项补齐该系统的依赖；同时确认原生库与进程架构相同。本次两个入口的 `ldd` 检查均未发现缺失依赖。Windows DLL 不能代替 Linux `.so`，切换系统后也应重新构建对应输出目录。

### 9.2 生命周期、坐标和业务结果

随文工程按“Session → 页面输入 → 单次张量”的层次释放资源。长时间业务程序应复用模型 Session，把每页图像、ROI 视图和单次张量放进局部 `using`，不要为每个文字框重新创建模型。

页面文字结果保留整页坐标。若从整页再裁出表格，表格结果首先属于表格输入图，应通过保存的裁剪变换回到整页；若前面做了方向纠正或 UVDoc 矫正，还要考虑对应的旋转或变形映射。不要把不同坐标空间的框直接混合。

多页场景可以为每页创建 `PaddleDocumentPage` 并指定 `pageIndex`，通过 `RunManyAsync` 编排已准备好的页面。文件解码、PDF 渲染、阅读顺序、跨页表格拼接和字段业务规则仍由应用层负责；需要并发时，还要结合 Session 数量和内存预算配置，不能无限并行加载大模型。

## 十、下一篇：接入自己的模型和结果类型

这一篇已经将 **版面检测与区域 OCR**、**表格结构与文字填充** 两条路径实际跑通。做文档应用时，可以先选自己需要的模块，再逐步加入方向、矫正、公式或图表任务，保留每个阶段的坐标、结果和失败信息。

下一篇继续讲 **自定义模型接入**：怎样声明输入输出和结果类型，选择 Resize / Letterbox / CenterCrop、颜色与归一化，并接入自己的后处理。这样当现有模型不能满足业务时，也可以沿用 DeploySharp 的后端、Session 与执行接口。

```text
本篇完整示例：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0/2026-10/examples/06-paddle-structure

PP-Structure 使用指南：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/visual-paddle-structure.md

系列文章索引：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0
```
