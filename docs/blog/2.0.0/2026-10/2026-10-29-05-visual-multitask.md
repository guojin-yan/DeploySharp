# DeploySharp 视觉多任务实战：在 C# 中完成分类、实例分割、关键点与旋转框检测

> DeploySharp 2.0.0 系列 · 第 05 篇实战教程，使用正式 NuGet `2.0.0`。

![DeploySharp 2.0.0 视觉多任务实战：分类、实例分割、关键点和旋转框，共用推理流程并保存图像与 JSON](assets/05-visual-multitask/cover.png)

## 一、DeploySharp 项目简介

**DeploySharp** 是一个面向 **C#/.NET 开发者**的开源模型部署框架，由「椒颜皮皮虾」开发，项目源码遵循 **Apache License 2.0**。项目封装了 **ONNX Runtime、OpenVINO、TensorRT、OpenCV DNN** 等推理引擎，提供模型加载、图像前处理、推理和结果后处理接口，方便大家在桌面软件、工业视觉和文档应用中部署模型。

项目已接入目标检测、图像分类、实例分割、关键点、旋转框、异常检测和背景移除等视觉任务，同时提供 **PP-OCRv4/v5/v6、PP-Structure 文档处理**及部分图文、本地语言模型的使用入口。常用模型可以直接使用配套前后处理，自有模型可以配置输入输出、类别、缩放和归一化方式，也可以扩展自己的解码器和结果类型。

2.0 版本增加了按需安装的 NuGet 包、Batch 批量推理、独立 Session 会话池、异步输入、感兴趣区域（ROI）管理和大图重叠切片等功能。本篇继续介绍不同视觉任务的配置和结果读取。

**项目源码与 NuGet：**

```text
项目源码：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0

NuGet（搜索 JYPPX.DeploySharp）：
https://www.nuget.org/packages?q=JYPPX.DeploySharp
```

前面几篇介绍了 YOLO26 检测、PaddleOCR 和多后端部署。这一篇用同一个 C# 工程完成**图像分类、实例分割、人体关键点和旋转框检测**，分别读取类别、掩码、骨架和带方向的目标框。

## 二、先看业务需要什么，再选择模型

### 2.1 四种任务，解决四类问题

产品分拣需要类别，轮廓提取需要掩码，姿态分析需要关节点，倾斜工件定位则需要带方向的框。按业务需要的结果选择任务，再准备对应模型：

![从业务结果选择视觉任务：整图类别对应分类，目标像素对应实例分割，人体关节点对应 Pose，目标方向对应 OBB](assets/05-visual-multitask/task-choice.png)

| 业务想获得什么 | 使用的任务 | 本文模型 | 主要返回信息 |
| --- | --- | --- | --- |
| 这张图片最可能是什么类别 | 图像分类 | YOLOv8s Classification | Top-K 类别索引、名称和分数 |
| 每个目标具体占据哪些像素 | 实例分割 | YOLO26s Segmentation | 每个实例的类别、框、分数与二值掩码 |
| 人体各个关节的位置 | 关键点 / Pose | YOLO26s Pose | 人体分数、17 个关键点及其有效性、骨架拓扑 |
| 倾斜目标的范围与方向 | 旋转框 / OBB | YOLO26s OBB | 类别、分数与源图上的四个顶点 |

四种任务分别加载各自的模型，共用 DeploySharp 的执行接口。分类选用公共 Release 中的 YOLOv8s，分割、Pose 和 OBB 使用 YOLO26。

### 2.2 本篇最终会保存什么

我们使用同一个控制台工程，通过 `classify / segment / pose / obb` 参数切换任务。每次执行后都会得到 `result.jpg` 和 `result.json`，分割任务还会把每个实例保存为独立的 `mask-NN.png`。

图片用来观察效果，JSON 用来接入业务。分类可以用于分拣，掩码可以统计前景像素，关键点可以计算姿态，旋转四边形可以定位目标方向。若要把像素换算为实际面积或长度，还需要相机标定和尺度换算。

## 三、创建工程：四种任务共用一组依赖

### 3.1 本文实际运行环境

| 项目 | 本次验证环境 |
| --- | --- |
| 操作系统 | Windows 11 x64，`10.0.26200` |
| CPU | AMD Ryzen 7 5800H，8 核 16 线程 |
| .NET | SDK `10.0.401`，运行时 `10.0.12`，目标框架 `net10.0` |
| DeploySharp | 正式 NuGet `2.0.0` |
| 推理引擎 | ONNX Runtime CPU `1.28.0` |
| 图像组件 | JYPPX OpenCV 托管接口及 Windows x64 runtime `5.0.0` |
| 执行方式 | 四个静态 Batch=1 模型，分别进行单图推理与结果保存 |

四组示例都使用 **ONNX Runtime CPU（下文简称 ORT CPU）**。其他后端的安装和离线发布可以参考第 04 篇。

### 3.2 新建控制台并安装 NuGet

在 PowerShell 中执行以下命令：

```powershell
dotnet new console -n DeploySharpVisualDemo -f net10.0
cd DeploySharpVisualDemo

dotnet add package JYPPX.DeploySharp.Visual.OpenCV --version 2.0.0
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
dotnet add package JYPPX.OpenCV.runtime.win-x64 --version 5.0.0
```

在生成的 `.csproj` 的 `PropertyGroup` 中增加 `<PlatformTarget>x64</PlatformTarget>`。Visual、Core 等基础依赖由 NuGet 自动引入。

这组依赖中，OpenCV 负责读图、前处理和绘图，ORT CPU 负责执行模型：

![四种任务共用的 Windows CPU 依赖：Visual.OpenCV 与 OpenCV runtime 负责图像，ORT 后端与 ORT CPU runtime 负责模型执行](assets/05-visual-multitask/package-stack.png)

换成 NVIDIA GPU、OpenVINO 或 OpenCV DNN 时，保留任务配置和结果处理，按后端安装对应的适配器、原生运行库，并修改注册和设备请求。

### 3.3 取得完整随文代码

为了让主程序聚焦在调用流程，示例将 **任务配置放入 `Profiles.cs`，绘图与结果导出放入 `ResultWriter.cs`**。分类的 1000 类标签保存在 `imagenet-labels.json`，索引来自本文默认分类 ONNX 的 `names` 元数据。

可直接从下面目录下载完整工程：

```text
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0/2026-10/examples/05-visual-multitask
```

也可以在刚才创建的项目目录执行以下命令，取得四个配套文件。它会替换空控制台的 `Program.cs`；第五节会完整展示主程序的内容。

```powershell
$codeBase = "https://raw.githubusercontent.com/guojin-yan/DeploySharp/DeploySharpV2.0/docs/blog/2.0.0/2026-10/examples/05-visual-multitask"
foreach ($file in @("Program.cs", "Profiles.cs", "ResultWriter.cs", "imagenet-labels.json")) {
    Invoke-WebRequest "$codeBase/$file" -OutFile $file
}
```

在 `.csproj` 中增加下面这一项，让类别文件随程序进入输出目录：

```xml
<ItemGroup>
  <None Update="imagenet-labels.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## 四、准备四组模型与图片，确认预处理方式

### 4.1 下载默认资源

本文使用项目公共模型与测试图片 Release 中的文件，模型直接从本地路径加载。下面的下载命令会整理好程序默认使用的文件名。

```powershell
New-Item -ItemType Directory -Force -Path models, images | Out-Null

$modelBase = "https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1"
$models = @(
    @{ Remote = "yolo-v8-classify-s.model.onnx"; Local = "yolov8s-cls.onnx" },
    @{ Remote = "yolo-v26-segment-s.model.onnx"; Local = "yolo26s-seg.onnx" },
    @{ Remote = "yolo-v26-pose-s.model.onnx"; Local = "yolo26s-pose.onnx" },
    @{ Remote = "yolo-v26-obb-s.model.onnx"; Local = "yolo26s-obb.onnx" }
)
foreach ($model in $models) {
    Invoke-WebRequest "$modelBase/$($model.Remote)" -OutFile "models/$($model.Local)"
}

$imageBase = "https://github.com/guojin-yan/DeploySharp/releases/download/test-assets.1"
foreach ($image in @("demo_7.jpg", "bus.jpg", "demo_9.jpg", "plane.png")) {
    Invoke-WebRequest "$imageBase/$image" -OutFile "images/$image"
}
```

公开资源入口也列在这里，方便按需选择文件：

```text
视觉模型：
https://github.com/guojin-yan/DeploySharp/releases/tag/models-visual.1

测试图片：
https://github.com/guojin-yan/DeploySharp/releases/tag/test-assets.1
```

如果只使用某一种任务，只下载相应模型和图片即可。已有本地模型时，后面通过命令行传入自己的路径，无需沿用这里的目录。

### 4.2 配置缩放方式与输出结构

先看输入配置。本文四个模型都是 **Float32、RGB、NCHW、像素除以 255**，但几何处理和输出规格不同。

| 任务 | 输入 `images` | 图像几何 | 输出张量 |
| --- | --- | --- | --- |
| 分类 | `[1,3,224,224]` | CenterCrop | `output0 [1,1000]`，已输出类别概率 |
| 实例分割 | `[1,3,640,640]` | Letterbox，填充 114 | `output0 [1,300,38]`，`output1 [1,32,160,160]` |
| Pose | `[1,3,640,640]` | Letterbox，填充 114 | `output0 [1,300,57]` |
| OBB | `[1,3,1024,1024]` | Letterbox，填充 114 | `output0 [1,300,7]` |

`NCHW` 表示批次、通道、高度、宽度的顺序。分割输出包含 300 条端到端候选及 32 通道的原型掩码；Pose 的每条候选包含 17 组关键点；OBB 的候选带有角度。这些输出分别交给对应任务的解码器处理。

三种常用几何方式的区别如下，图中的圆和两侧标记可以直观看到内容如何变化。

![Resize、Letterbox 与 CenterCrop 的区别：独立缩放会改变形状，留边保留完整图像，中心裁剪会移除边缘内容](assets/05-visual-multitask/preprocessing.png)

**CenterCrop** 等比缩放后取中心区域，适用于本文分类模型，但边缘内容可能被裁掉。**Letterbox** 等比缩放并留边，保留完整视野，再由坐标变换还原结果。**Resize** 直接缩放到目标尺寸，宽高比可能发生改变。

缩放、颜色、归一化和插值都可以配置，按模型训练及导出时的要求选择。本例分类模型已经输出概率，因此使用 `ClassificationScoreMode.Probabilities`，直接读取分数。

## 五、共用推理流程，让任务配置决定解码方式

### 5.1 用 Profile 配置不同任务

四种任务共用“注册后端 → 选择 Profile → 准备图像 → 推理解码 → 获取结果”的流程。`Profile` 配置输入名称、尺寸、标签、输出规格、前处理和解码器，`ModelArtifact` 指向本地模型文件。

![同一套 VisualPipeline 调用流程：本地模型与任务 Profile 参与初始化，图像经前处理和推理后返回对应任务的结果类型](assets/05-visual-multitask/common-pipeline.png)

`Profiles.Create(task)` 封装了这四个模型的配置。分割、Pose 和 OBB 使用 `YoloMultiTaskProfiles` 工厂，分类使用通用 `ClassificationDecoder`。目标分数阈值为 `0.25`，掩码和关键点阈值均为 `0.5`；分割临时工作区按需分配，上限为 `512 MiB`。

### 5.2 完整主程序

下面是随文工程的 `Program.cs`。将其与前面取得的配置、绘图和标签文件放在一起，即可编译运行。

```csharp
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using OrientedDetectionResult = JYPPX.DeploySharp.Visual.OrientedDetectionResult;

string task = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
if (task == "all" && args.Length > 1)
    throw new ArgumentException("自定义模型和图片路径请使用单个任务，不要使用 all。");
string[] tasks = task == "all" ? ["classify", "segment", "pose", "obb"] : [task];
using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var request = new BackendRequest(BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId, "cpu");

foreach (string current in tasks)
{
    var defaults = current switch
    {
        "classify" => (Model: "yolov8s-cls.onnx", Image: "demo_7.jpg"),
        "segment" => (Model: "yolo26s-seg.onnx", Image: "bus.jpg"),
        "pose" => (Model: "yolo26s-pose.onnx", Image: "demo_9.jpg"),
        "obb" => (Model: "yolo26s-obb.onnx", Image: "plane.png"),
        _ => throw new ArgumentException("任务应为 all / classify / segment / pose / obb。")
    };
    string modelPath = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine("models", defaults.Model));
    string imagePath = Path.GetFullPath(args.Length > 2 ? args[2] : Path.Combine("images", defaults.Image));
    if (!File.Exists(modelPath)) throw new FileNotFoundException("找不到本地模型。", modelPath);
    if (!File.Exists(imagePath)) throw new FileNotFoundException("找不到输入图片。", imagePath);

    VisualModelProfile profile = Profiles.Create(current, args.Length > 3 ? args[3] : null);
    var profiles = new VisualProfileRegistry();
    profiles.Register(profile);
    profiles.Freeze();
    var artifact = new ModelArtifact(profile.ModelId, "onnx", modelPath);
    using var pipeline = new VisualPipeline(backends,
        profiles.Select(artifact, backends, request, profile.Task), request);
    using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile);
    VisualInferenceResult result = pipeline.Run(input);

    string directory = Path.GetFullPath(Path.Combine("results", current));
    Directory.CreateDirectory(directory);
    object details = current switch
    {
        "classify" => ResultWriter.Classification(imagePath, directory, result.GetValue<ClassificationResult>()),
        "segment" => ResultWriter.Segmentation(imagePath, directory, result.GetValue<InstanceSegmentationResult>()),
        "pose" => ResultWriter.Pose(imagePath, directory, result.GetValue<PoseEstimationResult>()),
        "obb" => ResultWriter.Obb(imagePath, directory, result.GetValue<OrientedDetectionResult>()),
        _ => throw new InvalidOperationException()
    };
    File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
    {
        task = current, backend = "onnxruntime", device = "cpu", packageVersion = "2.0.0",
        model = Path.GetFileName(modelPath), image = Path.GetFileName(imagePath),
        environment = new
        {
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        },
        preprocessing = new { size = profile.Input.ShapePattern.ToString(),
            resize = profile.Preprocessing!.ResizeMode.ToString(), color = "RGB", scaleDivisor = 255 },
        outputs = profile.Outputs.Select(o => new { o.Name, shape = o.ShapePattern.ToString() }),
        result = details
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{current}: saved={directory}");
}
```

主程序通过 `GetValue<T>()` 取得对应任务的结果。切换任务时，模型、Profile 和结果类型一起选择，后面的四节会分别展示读取方法。

`BackendRegistry` 在任务之间复用，每个任务单独创建并释放 `VisualPipeline`。处理视频或批量图片时，可以把 Pipeline 放在循环外复用，每张图的 `PreparedVisualInput` 和绘图 `Mat` 则在使用后通过 `using` 释放。

### 5.3 执行全部任务，或只执行一项

在项目目录执行：

```powershell
dotnet run -c Release -- all
```

也可以分别运行：

```powershell
dotnet run -c Release -- classify
dotnet run -c Release -- segment
dotnet run -c Release -- pose
dotnet run -c Release -- obb
```

`all` 会依次加载四个模型，各处理一张图，结果按任务分别保存：

```text
results/
  classify/ result.jpg  result.json
  segment/  result.jpg  result.json  mask-00.png  mask-01.png ...
  pose/     result.jpg  result.json
  obb/      result.jpg  result.json
```

下面分别看每种结果的读取方法和运行效果。

## 六、图像分类：读取 Top-K 类别与分数

### 6.1 分类结果是什么

分类结果使用 `ClassificationResult`，其中 `Predictions` 按分数从高到低排列，`TopPrediction` 对应首项。每项有 `Index`、`Label` 和 `Score`，不包含目标位置。

下面摘录绘图器中的结果读取方式，代码运行在已获得 `result` 的上下文中：

```csharp
foreach (var item in result.Predictions)
{
    Console.WriteLine($"{item.Index}: {item.Label} = {item.Score:F4}");
}
```

`Label` 按类别索引查表得到，标签顺序要与训练时一致。随文标签来自默认 ONNX 元数据，更换自有分类模型时，一起换成对应的类别表。

### 6.2 默认图片的实际输出

本文在 `demo_7.jpg` 上取得以下 Top-5，图中的条形长度和右侧数字来自真实模型结果。

![YOLOv8s 分类真实结果：棕熊图片的首项 brown_bear 分数为 0.9937447，右侧展示同一次推理的 Top-5](assets/05-visual-multitask/classification-result.png)

```text
#1 brown_bear  0.9937
#2 hyena       0.0033
#3 timber_wolf 0.0014
#4 white_wolf  0.0003
#5 coyote      0.0003
```

第一项是 `brown_bear`，类别索引为 `294`，与输入的棕熊图片对应。

## 七、实例分割：每个目标都有独立掩码

### 7.1 除了框，还要取出 Mask

分割结果使用 `InstanceSegmentationResult`。`Instances` 中的每项包含类别、分数、框和 `Mask`；同一类别的多个目标仍然保留各自的实例身份。

这里取得的 `Mask` 已还原到**完整源图坐标**：宽高为 `810 × 1080`，`OriginX/OriginY` 均为 `0`。绘图器根据坐标空间、原点和大小，将前景像素叠加到原图。

```csharp
foreach (var item in result.Instances)
{
    InstanceBinaryMask mask = item.Mask;
    byte[] pixels = mask.ToArray();
    int foreground = pixels.Count(value => value != 0);
    Console.WriteLine($"{item.Label}: foreground={foreground}");
}
```

`ToArray()` 会复制像素数组，每个实例在像素循环外调用一次即可。随文绘图器按行处理原图，使用 `Mat.Step` 计算行地址，并将前景值转换为 255，保存为二值 PNG。

### 7.2 公交车图片的真实分割结果

下面依次为原图、彩色实例叠加结果和其中公交车实例的二值掩码。黑色是背景，白色是该实例的前景。

![YOLO26s 实例分割真实结果：公交车图片上获得 4 个实例，分别保存轮廓，右侧展示公交车的源图二值掩码](assets/05-visual-multitask/segmentation-result.png)

本次得到 **4 个实例：3 个 person、1 个 bus**。对应分数约为 `0.949`、`0.943`、`0.929`、`0.895`，每项在 JSON 中保留框和掩码文件名。公交车掩码的前景像素数量为 `266226`，可用于检查结果或进一步做像素统计。

图中也有可见人物未被检出，遮挡和图像边缘目标需要重点检查。用于自己的场景时，可以先保留默认结果作基线，再比较模型和阈值调整后的漏检情况。

## 八、人体关键点：有效点才参加骨架连线

### 8.1 人体分数和关键点分数分别使用

Pose 结果使用 `PoseEstimationResult`。每个人体实例有自己的 `Score` 和 `Keypoints`；每个关键点又有 `Index`、坐标、分数和 `IsValid`。`Topology` 保存点的定义及骨架连接关系，本文采用 COCO-17 点序。

绘图时按每个关节的有效性筛选。随文绘图器根据点索引寻找骨架两端，两端都有效时才连线：

```csharp
var points = item.Keypoints.ToDictionary(p => p.Index);
foreach (var edge in result.Topology.Edges)
{
    var a = points[edge.FirstKeypointIndex];
    var b = points[edge.SecondKeypointIndex];
    if (a.IsValid && b.IsValid)
        Drawing.Line(canvas, Point(a.Point), Point(b.Point), color, 3);
}
```

这段代码摘自 `ResultWriter.cs`，`Point` 是该文件中将浮点坐标转换为 OpenCV 整数绘图点的辅助方法。业务计算可以继续使用原始浮点坐标，避免过早取整。

### 8.2 球员图片的真实结果

在 `demo_9.jpg` 上检测到 **1 人**，人体分数约为 `0.944`，17 个关键点中有 **16 个**达到本文 `0.5` 的有效性阈值。

![YOLO26s Pose 真实结果：原图和骨架对照，1 人、16 个有效关键点，低置信度左耳不绘制](assets/05-visual-multitask/pose-result.png)

COCO-17 索引 `3` 对应左耳，分数约为 `0.102`，因此跳过该点及相关骨架边。JSON 仍保存它的坐标、分数和 `IsValid=false`，方便调试。计算关节角度时同样筛选有效点；连续动作分析则进一步结合跟踪和时序逻辑。

## 九、旋转框：用四个顶点还原目标方向

### 9.1 使用四个顶点绘制 OBB

OBB 是 Oriented Bounding Box，即有方向的包围框。本文返回类型是 **`JYPPX.DeploySharp.Visual.OrientedDetectionResult`**，每个目标的 `Quadrilateral.Vertices` 保存源图中的四个顶点。

绘图时按顶点顺序闭合连接，就能保留目标方向。需要水平外接框时，再读取 `AxisAlignedBounds`。

```csharp
var vertices = item.Quadrilateral.Vertices;
for (int i = 0; i < vertices.Count; i++)
{
    Drawing.Line(canvas,
        Point(vertices[i]),
        Point(vertices[(i + 1) % vertices.Count]),
        color, 2);
}
```

如果同时引入 Core 的结果命名空间，可能遇到同名类型。随文代码使用下面的别名明确选择 Visual 的多任务结果：

```csharp
using OrientedDetectionResult = JYPPX.DeploySharp.Visual.OrientedDetectionResult;
```

### 9.2 飞机场景的真实结果

在 `plane.png` 上得到 **12 个 plane**。左侧为完整检测图，右侧是同一结果右下区域的放大，便于观察小飞机的方向。

![YOLO26s OBB 真实结果：同一张飞机图片中得到 12 个旋转四边形，右侧放大原结果中的小飞机区域](assets/05-visual-multitask/obb-result.png)

JSON 保存四个顶点、`AngleRadiansCounterClockwise` 和 `HasExactRotatedRectangle`。绘图和区域定位可以直接使用顶点；接入机械定位或其他图像工具时，再核对角度单位、正方向、宽高交换及坐标轴约定。这里的角度以弧度表示。

非等比缩放后，源图四边形可能不再是精确旋转矩形，可通过 `HasExactRotatedRectangle` 判断。保留四顶点能完整表达变换后的形状。本例默认使用 Letterbox。

## 十、使用自己的模型和图片

### 10.1 相同导出规格，可以直接指定路径

输入输出规格相同的模型，可以直接传入自己的文件路径。参数顺序为：**任务、模型路径、图片路径、可选分类标签 JSON**。

```powershell
dotnet run -c Release -- segment "models/my-yolo26-seg.onnx" "images/my-image.jpg"
dotnet run -c Release -- pose "models/my-yolo26-pose.onnx" "images/my-person.jpg"
dotnet run -c Release -- obb "models/my-yolo26-obb.onnx" "images/my-scene.png"
dotnet run -c Release -- classify "models/my-cls.onnx" "images/my-image.jpg" "my-labels.json"
```

最后一条使用与本例相同的 **1000 类、224 输入、概率输出**配置。标签 JSON 按索引排列，格式为字符串数组，例如 `["class-a", "class-b"]`。自有模型若只有两个类别，还要同步修改 `Profiles.cs` 中的输出维度、标签数量检查和 `topK`。

YOLO26 自定义类别的分割或 OBB，将 `Coco80 / Dota15` 换成训练时的类别列表。Pose 默认使用 COCO-17，自定义点数或动物骨架时，同步修改输出规格和关键点拓扑。

### 10.2 需要同时核对的配置

| 更换了什么 | 需要同步检查 |
| --- | --- |
| 输入尺寸或输入名称 | `VisualInputBinding`、`ModelSize`、名称和实际 ONNX 输入 |
| Resize / Letterbox / CenterCrop | `VisualPreprocessingOptions` 与训练、上游推理流程是否一致 |
| 颜色或数值范围 | RGB/BGR、除数、均值和标准差 |
| 模型任务或导出布局 | 对应端到端或原始候选布局的解码器、候选数、输出名称与维度 |
| 类别表或分类分数类型 | 类别顺序、类数、Top-K，以及输出是 Logits 还是概率 |
| Batch 或多线程方式 | 模型允许的批次大小，以及应用使用的独立会话数量 |

前处理可以通过 `profile.WithPreprocessing(...)` 创建新配置，例如选择 `VisualResizeMode.Resize` 或 `VisualNormalizationOptions.ImageNet`。按模型的训练和导出要求设置，再用固定输入比较结果。

本例 YOLO26 使用端到端候选输出，解码器直接处理候选；其他导出布局需要对应的解码配置。掩码、关键点和旋转框返回时都已映射到源图，可以直接用于绘图和后续计算。

### 10.3 常见现象与检查顺序

| 现象 | 优先检查与处理 |
| --- | --- |
| 分类分数很高，名字却明显不对 | 检查类别索引顺序；默认模型使用随文标签，自己的模型使用训练时标签 |
| 目标框偏移、掩码错位 | 检查输入几何和源图大小，确认应用直接使用返回的源图坐标 |
| 骨架连到原点或遮挡位置 | 先筛选 `IsValid`，仅连接两端都有效的点，并核对拓扑索引 |
| OBB 与目标方向不一致 | 先直接绘制四顶点；需要角度时再核对单位、方向和宽高约定 |
| 提示输出 Shape 或张量名称不匹配 | 对照实际 ONNX 输出调整 Profile 和任务解码器 |
| 出现原生 DLL 加载错误 | 确认 `x64`、OpenCV Windows runtime 和 ORT CPU runtime 均进入输出目录，先排除 CPU/GPU runtime 混装 |

比较后端速度时，固定模型、图片和配置，预热后重复采样，分别记录前处理、模型推理、后处理和完整流程的时间。

模型支持和设备性能入口如下：

```text
模型支持指南：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/model-support.md

逐模型后端验证矩阵：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/model-backend-verification-matrix.md

设备性能实测：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/device-performance-benchmarks.md
```

## 十一、总结

本篇用同一个 C# 控制台工程完成了四种视觉任务：分类读取 Top-K，分割保存实例掩码，Pose 绘制骨架，OBB 输出旋转四边形。切换模型和 Profile 后，公共调用流程可以继续复用。

实际应用中，可以把检测区域交给分类，把分割掩码用于标定后的测量，或把视频关键点与跟踪器结合，再按吞吐需求加入 Batch、Session 池和异步处理。

**下一篇继续介绍 PP-Structure 文档处理**，从页面区域检测、区域 OCR，到表格结构与 HTML 输出，讲解怎样把多个专用模型组合成一条可使用的流程。自定义模型、视频并发、ROI 和大图切片也会在后续文章分别展开。

```text
本系列文章与完整案例：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0
```
