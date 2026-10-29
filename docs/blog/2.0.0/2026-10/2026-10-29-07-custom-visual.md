# 用 C# 做园区车辆与人员巡检：自定义 NanoDet，联合 CLIP 输出区域事件与复核台

> DeploySharp 2.0.0 系列 · 第 07 篇实战教程。基于正式 NuGet `2.0.0`、源码 Tag `v2.0.0`，创建与修订：2026-10-29。随文工程以当前文章目录为准。

![DeploySharp 园区巡检：NanoDet 检测、区域规则、CLIP 车型候选与事件复核](assets/07-custom-visual/cover.png)

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

前面几篇文章讲了已有模型怎样使用。这一篇我们做一个更贴近业务的案例：**接入库中尚未内置的 NanoDet-Plus，联合 CLIP，对多个固定视角的截图进行车辆与人员区域巡检。** 最终交付的不只是几个检测框，还有区域统计、待复核事件、证据图片及一个能直接打开的 HTML 复核台。

## 二、从监控截图到可以复核的巡检结果

### 2.1 先明确要完成的业务

园区里通常有车辆通道、人员通行区和需要关注的区域。仅把模型检测结果画出来，还缺少几个环节：哪些目标属于当前关注区？什么时候需要查看截图？能否补充车型候选？结果能不能交给后面的业务程序？

这次使用两组公开固定视角视频，各取 **0 s、4 s、8 s** 三张截图，模拟两台相机的输入。一组是道路车辆，一组是建筑庭院内的行人。它们用于复现方法，**不是作者已经部署的真实园区项目，也不是带人工真值的准确率数据集**。

![两台相机、六张截图与车辆和人员区域巡检的业务流程示意](assets/07-custom-visual/decode-route.png)

| 环节 | 实现 | 交付给下一步的内容 |
| --- | --- | --- |
| 接收截图 | `inspection.json` 绑定相机、文件和来源时间 | 明确截图来自哪个视角 |
| 找到目标 | 自定义 NanoDet-Plus | COCO 类别、分数、源图检测框 |
| 应用区域规则 | 多边形、类别过滤、底边中心点与数量阈值 | 每区域目标 ID、计数及是否触发 |
| 补充车型候选 | CLIP 图像与文本编码器 | 区域内车辆的 Top-3 候选和分差 |
| 留存与复核 | C# 结果类、图片、CSV、JSON、HTML | 可回看、可筛选、可继续集成的报告 |

例如，近侧车辆通道内检测到至少两个车辆目标时，生成一条数量关注事件；通行人员区域达到两个人员目标时，也生成待复核事件。阈值只是教程配置，读者应按现场需求调整。

**事件只由检测和几何规则产生，CLIP 的车型猜测不参与触发。** 这样即使车型候选不合适，区域计数与事件依据仍然可以单独检查。

### 2.2 这篇要学会的扩展方式

一个常见问题是：库中没有自己的模型，能不能直接接？可以。本文用 `VisualModelDefinitionBuilder<TInput, TResult>` 定义 NanoDet 的输入输出、前处理器、解码器以及自己的结果类，不修改 DeploySharp 主库。

![已有模型调整前处理与新模型自定义接入的两条路径](assets/07-custom-visual/extension-points.png)

如果只是调整已有模型的缩放、颜色或归一化，可以通过 `profile.WithPreprocessing(...)` 创建配置副本；如果输出含义不同，就需要自己的 Decoder。后续区域规则、候选匹配和事件报告则属于应用层，不必为了业务字段给主库增加新的模型类别。

## 三、准备工程与 NuGet 依赖

### 3.1 获取随文完整工程

完整源码、配置和六张输入图都放在随文目录。下载仓库后进入这个目录运行，辅助类已经配齐，无需照着正文逐段拼接。

```text
随文工程：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0/2026-10/examples/07-custom-visual

自定义视觉模型教程：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/visual-custom-model.md
```

```text
07-custom-visual/
  CustomVisual.csproj       # 正式 NuGet，不引用本地主库项目
  Program.cs               # 会话初始化与多截图任务编排
  NanoDetModel.cs           # 自定义检测模型与六输出解码
  ClipModels.cs             # 图文编码、英文 tokenizer、相似度
  Inspection.cs             # 配置、区域规则与业务结果
  InspectionOutput.cs       # JSON、CSV、静态 HTML 复核台
  PreviewWriter.cs          # 区域、检测框与 ROI 图片
  inspection.json
  prompts.json
  demo-inputs/              # 六张小型教程输入及来源说明
  models/                   # 自行下载，忽略提交
  results/                  # 运行生成，忽略提交
```

随文工程的目标框架为 `net10.0`，需要 .NET 10 SDK、x64 环境。读者运行不需要 Python、视频解码脚本或本地 DeploySharp 源码。

### 3.2 按运行场景选择依赖

如果自行创建项目，ORT CPU 的基础安装如下；使用随文工程时，直接 `dotnet restore` 即可。

```powershell
dotnet new console -n CustomVisual -f net10.0
cd CustomVisual
dotnet add package JYPPX.DeploySharp.Visual.OpenCV --version 2.0.0
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

图像适配器和推理后端分别负责不同工作，原生运行时还要按操作系统选择：

| 使用场景 | 额外安装的包 | 作用 |
| --- | --- | --- |
| Windows x64，ORT CPU | `JYPPX.OpenCV.runtime.win-x64 5.0.0` | 解码、图像变换、绘图 |
| Ubuntu 22.04 x64，ORT CPU | `JYPPX.OpenCV.runtime.ubuntu.22.04-x64 5.0.0` | 对应系统的图像原生库 |
| 增加 OpenVINO 后端 | `JYPPX.DeploySharp.Backend.OpenVINO 2.0.0` | 注册 OpenVINO 推理能力 |
| Windows OpenVINO | `OpenVINO.runtime.win 2026.2.1` | Windows 推理原生库 |
| Ubuntu OpenVINO | `OpenVINO.runtime.ubuntu.22-x86_64 2026.3.1` | Ubuntu 推理原生库 |

`Microsoft.ML.Tokenizers 2.0.0` 来自 Visual 的传递依赖。随文 `.csproj` 已按 Windows/Linux 选择原生包，并显式复制 Ubuntu 的原生文件到 `native/opencv` 和 `native/openvino`。

![托管接口、各系统原生库与三个 ONNX 模型分别准备](assets/07-custom-visual/deployment.png)

只运行 ORT 的应用，可以删除 OpenVINO 的包、原生文件复制项及代码注册分支。包安装完成后，还需要下载下面的模型；NuGet 不会自动带上这些权重。

## 四、准备模型、截图与相机区域

### 4.1 下载三个模型和 CLIP 词表

NanoDet 图来自 OpenCV Zoo 的固定提交，CLIP 使用项目模型 Release 的双编码器。文件统一放入 `models`，保留以下名称。

```text
NanoDet 固定版本目录：
https://github.com/opencv/opencv_zoo/tree/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet

NanoDet ONNX：
https://media.githubusercontent.com/media/opencv/opencv_zoo/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet/object_detection_nanodet_2022nov.onnx

CLIP 图像编码器：
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.image-encoder.onnx

CLIP 文本编码器：
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.text-encoder.onnx

CLIP 词表与 BPE 合并表：
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.vocab.json
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.merges.txt

CLIP 许可：
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.LICENSE
```

NanoDet 图约 3.8 MB，CLIP 的图像、文本 FP32 图分别约 352 MB、254 MB。NanoDet 目录采用 Apache 2.0，CLIP 资产保留 MIT 许可。具体的 PowerShell/Bash 下载命令放在随文 README 中，也可以用浏览器下载已有本地文件。

不需要用户计算模型或图片的 SHA-256。替换自有模型时，应检查真实端口、形状和处理约定，不能只修改文件名。

### 4.2 六张输入随工程提供

道路图为 1920×1080，庭院图为 768×576。同一相机的三张图对应原视频 0 s、4 s、8 s；`SourceSeconds` 只表示素材位置，不虚构拍摄日期或墙钟告警时间。

```text
道路素材：
https://media.roboflow.com/supervision/video-examples/vehicles.mp4

庭院素材：
https://github.com/opencv/opencv/blob/4.x/samples/data/vtest.avi
```

来源与处理方式记录在 `demo-inputs/README.md`。第三方素材的权利归其原权利人，本项目代码许可不授予额外素材许可。

换成自己的截图时，修改 `inspection.json` 中的 `Image` 和 `CameraId`。图像相对路径以配置文件所在目录解析；每台相机的视角变化后，要重新配置区域。

### 4.3 区域规则采用归一化多边形

下面节选车辆通道规则。多边形坐标位于 `[0,1]`，用来表达当前相机视角下的关注区；它不是地面真实距离。

```json
{
  "Id": "vehicle-lane",
  "Name": "近侧车辆通道",
  "Polygon": [[0.01,0.99],[0.30,0.40],[0.44,0.40],[0.49,0.99]],
  "ClassIds": [2,5,7],
  "TriggerAtLeast": 2,
  "Reason": "vehicle-occupancy-review"
}
```

本例 COCO 标签中，`0` 为 person，`2/5/7` 为 car/bus/truck。同一多边形可以分别配置车辆规则与人员规则；每条规则独立筛选类别、计数和触发。

区域归属使用检测框的**底边中心点**，近似表达行人脚部或车辆落地点。它比“框与区域只要有交集就算进入”更适合本例固定地面视角，但遮挡、截断和俯视角仍会影响判断。

![源图检测框底边中心归一化后判断多边形归属，按类别计数再产生待复核事件](assets/07-custom-visual/zone-rules.png)

区域判断的核心代码如下，完整实现位于 `InspectionRules.Evaluate`：

```csharp
var anchor = new PointF(
    (box.X + box.Width / 2) / sourceSize.Width,
    Math.Min(sourceSize.Height - .001f, box.Y + box.Height)
        / sourceSize.Height);

bool belongs = zone.ClassIds.Contains(classId)
    && InspectionConfig.Geometry(zone).Contains(anchor);
```

图中黄色线是配置区域，青绿色框是命中任一区域规则的目标，灰框保留其他检测，圆点标出底边参考点。**这是先做整图检测，再按业务区域过滤**；没有把多边形外的图像抹掉后重新推理，也没有把本例等同于完整视频跟踪系统。

## 五、自定义 NanoDet 的输入、前处理与六输出解码

### 5.1 端口以实际 ONNX 图为准

工程提供检查入口，打印三个图的真实端口，不执行截图任务：

```powershell
dotnet run -c Release -- --inspect
```

NanoDet 的输入是 `input.1`，形状 `[1,3,416,416]`。这个 Zoo 导出只有 **8、16、32 三个 stride、六个输出**，不能照着其他训练配置再加一个尺度。

![NanoDet 三尺度分类概率与距离分布，解码后还原到源图并执行按类 NMS](assets/07-custom-visual/model-contract.png)

| stride | 位置数 | 分类概率 | 边框距离 logit |
| --- | --- | --- | --- |
| 8 | 2704 | `792`：`[1,2704,80]` | `795`：`[1,2704,32]` |
| 16 | 676 | `814`：`[1,676,80]` | `817`：`[1,676,32]` |
| 32 | 169 | `836`：`[1,169,80]` | `839`：`[1,169,32]` |

分类输出已经是概率，不重复 Sigmoid；32 个边框值表示四条边，每边八档距离分布，需要分别做稳定 Softmax 并求期望。

### 5.2 不要只检查归一化，颜色和几何同样重要

OpenCV Zoo 的 `demo.py` 在调用模型前先做 BGR→RGB 和居中黑边 Letterbox，`nanodet.py` 内部才进行 mean/std。只读模型封装里的归一化代码，很容易遗漏入口处的颜色与几何步骤。

本例采用 **RGB、416×416 Letterbox、短边尺寸向下取整、黑色填充**。已发布的公共适配器使用 Linear 插值，官方 demo 使用 Area，二者不作逐像素一致声明。CLIP 使用自己的 RGB、短边缩放与中心裁剪，不能共享 NanoDet 处理后的输入张量。

![NanoDet 使用 RGB 黑边 Letterbox，CLIP 对源图 ROI 使用独立的短边缩放中心裁剪与归一化](assets/07-custom-visual/normalization.png)

```csharp
var processing = new VisualPreprocessingOptions(
    new VisualSize(416, 416),
    VisualResizeMode.Letterbox,
    VisualColorOrder.Rgb,
    VisualNormalizationOptions.MeanStandardDeviation(
        [103.53f, 116.28f, 123.675f],
        [57.375f, 57.12f, 58.395f]),
    VisualTensorLayout.Nchw,
    paddingColor: VisualRgbColor.Black,
    dimensionRounding: VisualDimensionRounding.Floor,
    interpolation: VisualInterpolationMode.Linear);
```

mean/std 对应 0—255 像素域，不再额外除以 255。CLIP 的 mean/std 也换算到等价像素域，采用 OpenCV Cubic；它与官方 Pillow 处理器仍有像素差异。

接口提供 `Resize/Letterbox/CenterCrop`、颜色和归一化切换，目的是匹配各模型约定。**能配置不代表可以随意改变而不影响精度。** 本例没有把更换几何当成无损操作。

### 5.3 自己的结果类也能接入 Runner

检测结果可以是普通 C# 记录，不需要继承库内置的结果类型：

```csharp
public sealed record PixelBox(
    float X, float Y, float Width, float Height);

public sealed record DetectedObject(
    int ClassId, string Label, float Score, PixelBox Box);

public sealed record ObjectDetections(
    IReadOnlyList<DetectedObject> Objects);
```

框统一使用源图 `x/y/width/height`，右、下边采用半开区间。区域判断、CLIP 裁剪与绘图都使用这个坐标系。

下面是 Builder 的关键部分；`levels` 对应上表三个尺度，完整代码在 `NanoDetModel.cs`：

```csharp
var builder = new VisualModelDefinitionBuilder<
        OpenCvDecodedRoiImage, ObjectDetections>(
        "blog.nanodet-plus-416",
        new ModelId("blog/nanodet-plus-416"),
        NanoDetModel.Task, "1", "onnx")
    .WithInput("input.1", TensorElementType.Float32,
        new TensorShape(1, 3, 416, 416), VisualTensorLayout.Nchw)
    .WithPreprocessing(processing)
    .WithInputPreprocessor((image, profile, ct) =>
        image.PrepareRectangle(
            new RectangleRoiGeometry(new RectangleF(
                0, 0, image.SourceSize.Width, image.SourceSize.Height)),
            profile.Input.Name,
            profile.Preprocessing!.ToOpenCvOptions(), ct))
    .WithDecoder(new NanoDetDecoder(levels, threshold, nmsThreshold));

foreach (var level in levels)
{
    int count = level.Rows * level.Columns;
    builder.AddOutput(level.Scores, TensorElementType.Float32,
        new TensorShape(1, count, 80));
    builder.AddOutput(level.Distances, TensorElementType.Float32,
        new TensorShape(1, count, 32));
}

var definition = builder.Build();
```

`VisualTaskId` 是开放标识，新任务不需要修改主库枚举。前处理既可以用委托，也可以实现 `IVisualInputPreprocessor<TInput>`，适合逐步迁移自己的模型代码。

### 5.4 距离分布与坐标还原

每个位置取最大类别分数，低于默认 `0.35` 时跳过距离解码；四条边分别求分布期望：

```text
边距离 = stride × Σ[k × softmax(logits)[k]]，k = 0…7
中心 x = 网格列 × stride + (stride − 1) / 2
中心 y = 网格行 × stride + (stride − 1) / 2
模型框 = [中心 x − 左, 中心 y − 上, 中心 x + 右, 中心 y + 下]
```

然后通过 `context.Input.Transform.ToSource(...)` 去掉 padding 并还原缩放，再裁到源图。默认 `--nms 0.6`，按类别执行非极大值抑制（NMS），保留不同类别的重叠目标。

Decoder 不改写后端输出缓冲区，只把最终类别、分数与框保存到自己的对象。NaN、Infinity 或不合法概率会报错，不能把异常张量当成正常业务结果。

## 六、CLIP 负责车型候选，规则负责事件

### 6.1 提前编码六条车型描述

`prompts.json` 默认只为 COCO 车辆 `2/5/7` 配置候选：passenger-car、suv、delivery-van、pickup、box-truck、bus。下面是配置节选；完整文件仍是候选组数组。

```json
{
  "Name": "vehicle-body",
  "ClassIds": [2,5,7],
  "Candidates": [
    { "Key": "passenger-car", "Text": "a photo of a passenger car." },
    { "Key": "suv", "Text": "a photo of a sport utility vehicle." },
    { "Key": "box-truck", "Text": "a photo of a large box truck transporting goods." }
  ]
}
```

文本图有 `input_ids` 与 `attention_mask` 两个 INT64 输入，形状 `[候选数,77]`，输出 `[候选数,512]` 的 `text_embedding`。随文 tokenizer 使用配套词表和 BPE，限定可打印 ASCII 英文；超长文本直接报错，不静默截断。需要中文时，应替换并验证对应 tokenizer 与模型。

六条候选只编码一次。编码后释放文本 Session，保留自己的归一化特征，再加载图像 Encoder，避免每个目标都重新处理文本。

### 6.2 只给需要的目标准备 ROI Batch

先计算区域归属，只有**命中区域且存在候选组**的目标才进入 CLIP。庭院人员仍会参与区域规则，只是默认不做车型语义匹配。

原图在推理阶段解码一次，检测框每侧扩展默认 `0.06` 的边距并裁到图内。多个源图 ROI 一起准备成 CLIP 的 `[B,3,224,224]` 输入：

```csharp
// regions 来自命中区域的车辆源图框。
ImageEmbeddings embeddings = await imageRunner.RunAsync(
    new RoiBatch(decoded, regions),
    cancellationToken: cancellationToken);
```

`--batch` 范围 `1—16`，默认 4；尾批使用实际数量，不重复填充。`--max-rois 16` 限制单图进入语义阶段的目标数；其余检测保留，并记录跳过原因。

本次六图默认运行的 CLIP 批次为道路首图 `[2]`、末图 `[1]`，其他图为空。这来自当前输入的实际目标数。示例调用模型动态 Batch，也可以用 `--batch 1` 顺序分批。

### 6.3 匹配分数与事件状态分别解释

图像与文本的 512 维特征先 L2 归一化，用点积计算余弦相似度，再在当前候选组内计算 Softmax 分数。

| 字段 | 含义 |
| --- | --- |
| `Detection.Score` | NanoDet 闭集类别的检测分数 |
| `Cosine` | ROI 与候选文本的余弦相似度 |
| `CandidateScore` | 当前组内的相对匹配分数，改变候选会改变它 |
| `Margin` / `RequiresReview` | Top-1 与 Top-2 分差及语义歧义提示，默认分差阈值 0.1 |
| `Events[].Status` | 区域规则触发后统一为 `pending-human-review` |

**所有区域事件都待人工复核。** `RequiresReview=False` 只表示候选分差没有触发额外提示，不表示车型真实，也不表示事件已经处理。

CLIP 是候选集检索，不是自由描述，不给 NanoDet 增加新检测类别，也不是经过本场景标注验证的车型分类器。它在这里的工程价值是补充检索线索，而不是替代区域判断或人工结论。

## 七、会话复用与业务输出

### 7.1 多张图复用会话，原图按张释放

检测与图像 Runner 在截图循环外创建，文本特征提前缓存。每张图执行“检测→区域→必要 ROI 编码→报告”，完成所有 `await` 后再释放该张原图。

![六张截图复用检测和图像会话，文本特征缓存，各张原图和每批输入按生命周期释放](assets/07-custom-visual/lifecycle.png)

| 资源 | 所有者与释放时机 |
| --- | --- |
| 注册中心、检测与图像 Runner | 应用拥有，六张截图处理结束后释放 |
| 文本 Session | 候选编码函数拥有，编码完成后释放 |
| 已解码原图 | 当前截图拥有，所有 ROI 调用与输出完成后释放 |
| `PreparedVisualInput` | 返回后由 Runner 接管，该次推理与解码后释放 |
| 文本特征、检测与报告 | 自有托管数据，不持有已释放 Mat 或后端缓冲区 |

绘图工具会另读原图保存叠框与 ROI；它不参与模型输入准备。阶段间存在数据依赖，本例顺序 `await`；异步入口不会自动创建 Session 池或跨帧流水线。

`Ctrl+C` 请求取消后，等待当前调用返回并释放资源，退出码为 `2`；普通错误为 `1`，正常完成为 `0`。常驻服务可把 Runner 与候选特征放到服务生命周期中。

### 7.2 输出是一组可以交接的业务文件

```text
results/ort-cpu/
  index.html                # 摄像头与事件筛选、证据入口
  summary.json              # 汇总与逐图完整报告
  observations.csv          # 每张图、每条区域规则的观测
  review.csv                # 待人工复核事件
  road-00/
    scene.json
    scene.jpg
    roi-00.jpg
    roi-02.jpg
  road-04/...
  courtyard-00/...
```

单图 `scene.json` 包含相机、多边形、来源时间、配置、完整检测、区域成员、CLIP Top-3 与事件。`roi-XX.jpg` 是实际源图裁剪，不是已经中心裁剪后的 224×224 输入张量。没有进入语义阶段的目标会记录 `outside-zones`、`no-prompt-group` 或 `max-rois-limit`。

CSV 采用 UTF-8 BOM 和带引号字段，方便表格软件打开；HTML 直接链接相同目录下的证据。复核台是静态页面，可以筛选与查看，审核结果回写或告警通知需要业务应用自行接入。

![实际程序生成的 HTML 巡检复核台，六张截图、区域统计、候选结果与证据链接](assets/07-custom-visual/review-dashboard.jpg)

这里共用 `scene.jpg` 作为对应截图中事件的证据，并用目标 ID 关联区域成员。事件有稳定的截图/区域 ID，方便业务系统定位；这不是对物理事件做跨帧合并的 ID。

## 八、在 Windows 与 Ubuntu 实际运行

### 8.1 Windows 命令

先下载模型，在随文工程目录运行。六张输入和默认配置已经随源码提供：

```powershell
dotnet restore
dotnet build -c Release

dotnet run -c Release --no-build -- --backend ort-cpu `
  --manifest inspection.json --models models --prompts prompts.json `
  --batch 4 --out results

dotnet run -c Release --no-build -- --backend openvino `
  --manifest inspection.json --models models --prompts prompts.json `
  --batch 4 --out results
```

完成后打开 `results/ort-cpu/index.html` 或 `results/openvino/index.html`。选择相机、勾选“只看待复核截图”，可以减少逐张翻图的工作。

### 8.2 Ubuntu 22.04 命令

同一份业务源码在 Ubuntu x64 构建时选择 Linux 原生包。除 .NET 10 SDK 外，还需要原生包依赖的系统库。

```bash
dotnet restore
dotnet build -c Release

export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"

dotnet bin/Release/net10.0/CustomVisual.dll \
  --backend ort-cpu --manifest inspection.json --models models \
  --prompts prompts.json --batch 4 --out results

dotnet bin/Release/net10.0/CustomVisual.dll \
  --backend openvino --manifest inspection.json --models models \
  --prompts prompts.json --batch 4 --out results
```

Ubuntu OpenCV 入口为 `libJYPPX.OpenCV.Native.so`，OpenVINO C 入口为 `libopenvino_c.so`。若提示找不到库，先检查原生文件与 `LD_LIBRARY_PATH`，不要复制 Windows DLL 过去。

### 8.3 两套设备、两个 CPU 后端的结果

四组运行使用相同三个模型、六张截图、六条车型候选与默认阈值。两套环境均编译通过，0 警告、0 错误；以下结果只说明本文工程的运行范围。

| 项目 | Windows 设备 | Ubuntu 设备 |
| --- | --- | --- |
| 系统 / 架构 | Windows 11，Build 26200，x64 | Ubuntu 22.04.5 LTS，x64 |
| CPU | AMD Ryzen 7 5800H | Intel Core i7-1165G7 |
| .NET SDK / Runtime | 10.0.401 / 10.0.12 | 10.0.301 / 10.0.9 |
| DeploySharp / OpenCV 原生包 | 2.0.0 / 5.0.0 | 2.0.0 / 5.0.0 |
| ORT CPU / OpenVINO CPU 原生包 | 1.28.0 / 2026.2.1 | 1.28.0 / 2026.3.1 |

| 系统 | 后端 | 截图 | 目标观测 | CLIP ROI | 待复核事件 |
| --- | --- | --- | --- | --- | --- |
| Windows | ORT CPU | 6 | 40 | 3 | 4 |
| Windows | OpenVINO CPU | 6 | 40 | 3 | 4 |
| Ubuntu | ORT CPU | 6 | 40 | 3 | 4 |
| Ubuntu | OpenVINO CPU | 6 | 40 | 3 | 4 |

**40 是六张截图的检测观测总和，包含非业务类别和误报，不是 40 个独立人车。** 四组的区域计数、事件成员与三条车型 Top-1 一致，浮点分数可能有差异；没有做推理速度排名，也没有把结果扩展到其他后端。

![Windows 六图实跑汇总：道路区域车辆与庭院人员，保留实际检测和候选结果](assets/07-custom-visual/windows-result.png)

逐图结果如下。`road-00` 产生车辆数量关注事件，三张庭院截图分别产生人员数量关注事件。

| 截图 | 检测总数 | 区域内目标 | CLIP ROI | 事件数 |
| --- | --- | --- | --- | --- |
| road-00 | 4 | 2 | 2 | 1 |
| road-04 | 2 | 0 | 0 | 0 |
| road-08 | 1 | 1 | 1 | 0 |
| courtyard-00 | 9 | 3 | 0 | 1 |
| courtyard-04 | 12 | 6 | 0 | 1 |
| courtyard-08 | 12 | 6 | 0 | 1 |

道路首图两辆区域内车辆分别匹配到 `passenger-car` 和 `box-truck`，末图匹配到 `suv`。它们是给复核人员的候选线索，不能当作已标定的车型真值。

下面展示 Ubuntu 实际运行报告中的对应图像和区域统计，同一业务代码复用 ORT、OpenVINO 两条 CPU 路径：

![Ubuntu 六图运行结果与区域规则统计，三模型链路均实际运行](assets/07-custom-visual/ubuntu-result.png)

### 8.4 真实画面也暴露了需要复核的地方

道路远处车辆很小，整图缩到 416 后会漏检；道路首图的支架区域还出现了 `train` 误报。它保留在完整检测报告中，但因类别与区域规则不匹配，不进入本例车辆计数。

庭院中，路锥和标牌附近有部分低分 person 误报，其中一些进入区域统计。因此表中的“6”是模型观测数，不能直接称为真实人数。把阈值提高可以抑制部分误报，也可能漏掉遮挡目标；要用于现场，应补充标注数据并联合调整模型、输入尺寸、规则和阈值。

这正是本例保留原图、目标 ID、规则配置与人工复核清单的原因。单看“模型正常运行”或候选分数较高，不足以保证业务结论正确。

## 九、换到自己的项目时，哪些地方需要改

| 需求或现象 | 先检查 | 修改位置 |
| --- | --- | --- |
| 换相机、换截图 | 视角、尺寸与关注区是否改变 | `inspection.json`，重新画多边形 |
| 调整关注类别与数量 | COCO 类别顺序、阈值含义 | `ClassIds`、`TriggerAtLeast`、`Reason` |
| 更换检测模型 | 真实端口、shape、颜色、几何和输出含义 | Builder、前处理与 Decoder 一起修改 |
| 框整体偏移 | padding、实际缩放、模型/源图坐标 | `ImageTransform` 与坐标还原 |
| CLIP 高分但候选不合适 | 候选覆盖、ROI 上下文、中心裁剪 | `prompts.json` 和 CLIP 前处理，用标注数据验证 |
| 目标多，语义开销大 | 是否只处理必要 ROI | `--batch`、`--max-rois`，复用会话和文本特征 |
| 原生库加载失败 | OS、架构、文件部署、Linux 搜索路径 | 匹配的运行时包与输出目录 |
| 想统计停留、越线、去重人数 | 是否有连续帧、跟踪 ID 和时间状态 | 增加跟踪与时序规则，不能累加当前截图计数冒充 |

原始 NanoDet 图可能提示 initializer 同时列在 graph inputs 中，这是旧导出格式警告，不等于推理失败。模型图优化要与结果回归分开，本教程直接使用原始公开图。

本例仍为快照巡检：不做跨帧跟踪、事件去重、停留时长或越线统计；没有真实接入 RTSP，也没有把静态 HTML 当作多用户告警系统。后续可以在相同模型接口外增加这些业务能力。

## 十、从自定义模型走到完整任务

这篇把模型接入放到一个完整应用中：NanoDet 负责定位，区域规则给出明确的触发依据，CLIP 提供候选检索线索，最后用图片、CSV、JSON 与 HTML 把结果交给人或后续程序。

对于自己的模型，可以保留这套应用编排，替换模型定义与 Decoder；对于自己的现场，则重点修改区域、候选与规则。下一篇继续展开 **Batch、独立 Session 池和异步预取**，处理图片批量和连续输入时的调度与资源管理。

```text
DeploySharp 项目：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0

系列文章目录：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0

本篇完整代码：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0/2026-10/examples/07-custom-visual
```
