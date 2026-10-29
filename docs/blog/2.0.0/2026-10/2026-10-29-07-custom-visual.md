# 在 C# 中串联 NanoDet-Plus 与 CLIP：从自定义模型接入到场景分析

> DeploySharp 2.0.0 系列 · 第 07 篇实战教程。基于正式版 NuGet `2.0.0`、源码 Tag `v2.0.0`，创建与修订：2026-10-29。

![DeploySharp 2.0.0：自定义接入 NanoDet-Plus，联合 CLIP 图像与文本编码器完成场景分析](assets/07-custom-visual/cover.png)

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

前面几篇文章讲了已有模型怎样使用。这一次换一个角度：**如果库中没有自己的模型，能否在业务工程里完成接入，再与其他模型组成一条完整任务？**

本篇选择尚未内置的 **NanoDet-Plus**，在业务工程中完成自定义接入，再联合 **CLIP 图像编码器与文本编码器**，完成一张街景图片中的目标定位、区域语义匹配和场景报告。

## 二、这次做一个多模型联合案例

### 2.1 从检测框走到自己的业务结果

假设我们要整理场景图片：先找出行人和车辆，再按自己提供的描述检索每个目标。例如，在“穿白色上衣的人”“穿蓝色上衣的人”等候选中比较行人区域；在白色、蓝色等车辆描述中比较车辆区域。

这里有三个 ONNX 模型、两条输入分支。文字候选先编码一次；场景图经过检测后，多个 ROI 组成图像 Batch。两类特征最后在 C# 中计算相似度，生成自己的结果类型。

![NanoDet 检测定位，CLIP 分别编码 ROI 与候选文本，组合为 SceneAnalysisResult](assets/07-custom-visual/decode-route.png)

| 阶段 | 使用的模型或代码 | 本阶段完成的事情 |
| --- | --- | --- |
| 目标定位 | 本文自定义的 NanoDet-Plus 模型定义 | 输出 COCO 类别、检测分数和源图坐标框 |
| 区域准备 | 已解码原图、源图 ROI、Batch | 扩展少量边距、裁到图内，按批准备 CLIP 输入 |
| 图像编码 | CLIP ViT-B/32 image encoder | 每个 ROI 得到 512 维图像特征 |
| 文本编码 | CLIP ViT-B/32 text encoder | 候选描述得到 512 维文本特征，后续复用 |
| 业务组织 | 自有 C# 匹配、复核与结果类 | 保存目标、Top-3 描述、分差、复核标记及图像 |

**这是候选描述匹配，不是自由生成图片描述，也不是给 NanoDet 增加任意新类别。** 检测阶段仍只能定位它训练过的 80 类目标。CLIP 负责比较已有 ROI 与给定文本的相似程度，匹配结果也不等同于经过专项训练的服装或颜色分类器。

### 2.2 哪些事情需要自定义

如果一个模型已经有对应的输入输出和解码器，只是要调整几何、颜色或归一化，可以使用 `profile.WithPreprocessing(...)` 创建配置副本。

当输出结构不同，或者最终需要自己的结果类时，就使用 `VisualModelDefinitionBuilder<TInput, TResult>`。这次 NanoDet 的多尺度输出、分布式边框解码和 `ObjectDetections` 都由随文工程定义，不修改 DeploySharp 主库。

![已有模型覆盖前处理与新模型定义两条接入路径，复用相同的后端执行能力](assets/07-custom-visual/extension-points.png)

本篇也把 CLIP 图像图包装成自己的 ROI Batch 模型定义，用来展示 `TInput` 可以包含已解码原图与区域集合，`TResult` 可以是自己的特征结果。CLIP 文本图有两个整型输入，则直接使用后端 Tensor 接口。

## 三、环境与 NuGet：按场景组合依赖

### 3.1 获取随文工程

完整工程包含检测解码、英文候选 tokenizer、图文匹配、坐标处理和可视化。正文挑关键部分解释，不要求大家从零拼接辅助类。

下文运行命令使用随文工程 `CustomVisual`；如果自行新建其他名称的项目，请同步替换 Ubuntu 命令中的程序集文件名。

```text
随文工程：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0/2026-10/examples/07-custom-visual

前处理与自定义模型教程：
https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/docs/articles/visual-custom-model.md
```

下载仓库后进入：

```text
docs/blog/2.0.0/2026-10/examples/07-custom-visual
```

该工程使用正式 NuGet，目标框架为 `net10.0`，不通过 `ProjectReference` 引用本地 DeploySharp 源码。基本目录如下，模型、图片和运行结果不提交进源码仓库：

```text
07-custom-visual/
  CustomVisual.csproj
  Program.cs
  NanoDetModel.cs
  ClipModels.cs
  PreviewWriter.cs
  prompts.json
  models/
  images/
  results/
```

### 3.2 只使用 ORT CPU 时安装什么

新建控制台工程时，先安装图像适配器和 ONNX Runtime 后端。前者会带入 Visual 等托管依赖；CPU 版 ORT 包提供本例的推理原生库。

```powershell
dotnet new console -n CustomScene -f net10.0
cd CustomScene
dotnet add package JYPPX.DeploySharp.Visual.OpenCV --version 2.0.0
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

然后根据实际操作系统选择 OpenCV 原生运行时：

| 场景 | 额外安装的包 | 职责 |
| --- | --- | --- |
| Windows x64，ORT CPU | `JYPPX.OpenCV.runtime.win-x64` `5.0.0` | 图像解码、变换和绘图 |
| Ubuntu 22.04 x64，ORT CPU | `JYPPX.OpenCV.runtime.ubuntu.22.04-x64` `5.0.0` | Ubuntu 对应的图像原生库 |
| 增加 OpenVINO CPU | `JYPPX.DeploySharp.Backend.OpenVINO` `2.0.0` | 注册 OpenVINO 后端 |
| Windows 的 OpenVINO 原生库 | `OpenVINO.runtime.win` `2026.2.1` | 在 Windows 运行推理 |
| Ubuntu 的 OpenVINO 原生库 | `OpenVINO.runtime.ubuntu.22-x86_64` `2026.3.1` | 在 Ubuntu 运行推理 |

这次 tokenizer 使用 Visual 依赖的 `Microsoft.ML.Tokenizers` `2.0.0`，不再安装一套 Python 环境。Python 只用于作者侧的 tokenizer 对照，读者运行 C# 示例不需要它。

随文 `csproj` 已按 Windows/Linux 条件选择原生包，并显式复制 Ubuntu 的库到 `native/opencv` 和 `native/openvino`。如果直接使用随文工程，执行 `dotnet restore` 即可，不用重复手动添加上面所有包。

依赖准备和模型准备可以分开检查：NuGet 负责接口及运行时，三个 ONNX 图和候选配置由应用自己管理。下面这张图把它们放在一起，避免把“包安装完成”当成“模型已经自动下载”。

![托管接口、各系统原生运行时与 NanoDet 和 CLIP 模型文件分别准备](assets/07-custom-visual/deployment.png)

## 四、准备模型、图片与候选描述

### 4.1 模型来自哪里

检测图来自 OpenCV Zoo 的 NanoDet 示例目录；CLIP 使用 DeploySharp 模型 Release 中配套的双编码器。三个 ONNX 图都放在 `models` 中，保留下面的文件名。

```text
NanoDet 固定版本目录：
https://github.com/opencv/opencv_zoo/tree/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet

NanoDet ONNX：
https://media.githubusercontent.com/media/opencv/opencv_zoo/48b10fa505aeb51fbd127fc60a5c660040e823b7/models/object_detection_nanodet/object_detection_nanodet_2022nov.onnx

CLIP 模型 Release：
https://github.com/guojin-yan/DeploySharp/releases/tag/models-visual.1

CLIP 图像编码器：
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.image-encoder.onnx

CLIP 文本编码器：
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.text-encoder.onnx

CLIP 词表：
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.vocab.json

CLIP BPE 合并表：
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.merges.txt

CLIP 许可：
https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1/clip-vit-b-32.LICENSE
```

本例运行需要三个 ONNX 文件，以及 CLIP 的 `vocab.json`、`merges.txt`。Release 中的 `tokenizer.json`、`preprocessor_config.json` 和 ModelPack 可以用来核对来源和配置，不必为了本例把其他视觉模型一起下载。NanoDet 目录使用 Apache 2.0，CLIP 模型资产保留自己的 MIT 许可。

CLIP 的两个图都是 FP32，体积约 352 MB 和 254 MB；NanoDet 图约 3.8 MB。先把模型下载完整，再运行，避免把 Git LFS 指针文件当成 ONNX。

测试图片使用项目图片 Release 的 `bus.jpg`，也可以直接指定自己的本地图片：

```text
测试图片 Release：
https://github.com/guojin-yan/DeploySharp/releases/tag/test-assets.1
```

请将图片保存为 `images/bus.jpg`。所有运行都读取本地路径，不要求用户计算模型或图片的 SHA-256。换成自己的 ONNX 时，关键是同步检查端口、几何、归一化和后处理含义。

### 4.2 候选描述由业务决定

`prompts.json` 把候选文本按检测类别分组。`ClassIds` 使用本文 COCO 标签顺序，`0` 是行人，`2/5/7` 是汽车、公交车、卡车。下面节选行人组：

```json
{
  "Name": "person-clothing",
  "ClassIds": [0],
  "Candidates": [
    { "Key": "person-white", "Text": "a photo of a person wearing a white shirt." },
    { "Key": "person-black", "Text": "a photo of a person wearing a black shirt." },
    { "Key": "person-blue", "Text": "a photo of a person wearing a blue shirt." }
  ]
}
```

完整文件为两组、共 12 条描述。增加或更换描述后，文本特征需要重新编码；同一候选集处理多张图片时，可以继续缓存特征。没有配置候选组的检测类别保留原始检测结果，并记录 `no-prompt-group`，不会强行分到不相关的描述。

## 五、自定义接入 NanoDet：前处理、六输出与自己的结果

### 5.1 先核对实际的模型端口

同一个模型名称，导出方式不同，输入输出也可能不同。工程提供元数据检查入口，分别打印三个图的端口：

```powershell
dotnet run -c Release -- --inspect
```

本篇 NanoDet 图的输入为 `input.1`，形状 `[1,3,416,416]`。实际只有 **8、16、32 三个 stride，共六个输出**；不要照着其他训练配置额外增加第四尺度。

![NanoDet 三个尺度的六个输出，分类概率与四条边的距离分布分别解码，再还原坐标并做 NMS](assets/07-custom-visual/model-contract.png)

| stride | 网格位置数 | 分类输出 | 边框输出 |
| --- | --- | --- | --- |
| 8 | 2704 | `792`，`[1,2704,80]` | `795`，`[1,2704,32]` |
| 16 | 676 | `814`，`[1,676,80]` | `817`，`[1,676,32]` |
| 32 | 169 | `836`，`[1,169,80]` | `839`，`[1,169,32]` |

模型输出的分类值已经是概率，不再重复 Sigmoid；边框的 32 个数是四条边、每边八档的距离分布 logit，需要逐边 Softmax。输出名称和形状在 Builder 中明确绑定，换图后应重新检查。

### 5.2 两个模型，分别配置前处理

本例 NanoDet 采用原始验证配置的直接缩放、BGR 和逐通道 mean/std；CLIP 则使用 RGB、最短边缩放、中心裁剪，以及自己的 mean/std。两者不能混用已经处理好的图像张量。

![NanoDet 整图 BGR Resize 与 CLIP ROI RGB 短边缩放中心裁剪使用独立的前处理配置](assets/07-custom-visual/normalization.png)

NanoDet 的配置代码如下。这里 mean/std 对应原始 0—255 像素域，因此不要再额外除一次 255：

```csharp
var processing = new VisualPreprocessingOptions(
    new VisualSize(416, 416),
    VisualResizeMode.Resize,
    VisualColorOrder.Bgr,
    VisualNormalizationOptions.MeanStandardDeviation(
        [103.53f, 116.28f, 123.675f],
        [57.375f, 57.12f, 58.395f]),
    VisualTensorLayout.Nchw,
    interpolation: VisualInterpolationMode.Linear);
```

`Resize` 可以改成 `Letterbox`、`CenterCrop` 等模式，颜色、插值和归一化也有独立选项。**这些选项用于匹配模型的训练和导出约定，不能把“接口允许切换”理解成“随便改都不影响精度”。** 本篇实测统一使用代码中的默认配置，没有比较不同几何的准确率。

CLIP 使用 Profile 提供的参数，代码中的 mean/std 已换算成等价的 0—255 域值。ROI 采用 OpenCV Cubic 处理；它与官方 Pillow 处理器的像素结果存在差异，本篇不宣称与官方 Processor 逐像素一致。

### 5.3 定义自己的输入和结果

检测输入可以直接是 `OpenCvDecodedRoiImage`。它拥有一张已解码原图，可反复准备整图和区域输入。结果不要求继承库中的 `DetectionResult`，可以使用自己的记录类型：

```csharp
public sealed record PixelBox(
    float X, float Y, float Width, float Height);

public sealed record DetectedObject(
    int ClassId, string Label, float Score, PixelBox Box);

public sealed record ObjectDetections(
    IReadOnlyList<DetectedObject> Objects);
```

`Box` 统一表示源图坐标中的 `x/y/width/height`，右边与下边采用半开区间。后续 ROI 扩边、裁剪和叠框都基于它，不把 416 画布坐标直接交给下一阶段。

Builder 将模型说明、预处理器和解码器连在一起。下面是随文代码的关键部分，`levels` 的三个尺度及完整解码实现位于 `NanoDetModel.cs`：

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

`VisualTaskId` 是开放标识，自定义任务不需要修改主库枚举。前处理器也可以实现 `IVisualInputPreprocessor<TInput>`，或者像这里一样直接使用委托。

### 5.4 距离分布、坐标与按类 NMS

每个位置先取最大类别分数，低于阈值时跳过昂贵的距离解码。四条边分别进行稳定 Softmax，再计算距离期望：

```text
边距离 = stride × Σ[k × softmax(logits)[k]]，k = 0…7

中心 x = 网格列 × stride + (stride − 1) / 2
中心 y = 网格行 × stride + (stride − 1) / 2

模型边框 = [中心 x − 左距离, 中心 y − 上距离,
            中心 x + 右距离, 中心 y + 下距离]
```

接着用 `context.Input.Transform.ToSource(...)` 还原源图坐标，并裁到源图范围。默认 `--threshold 0.35`、`--nms 0.6`，按类别执行非极大值抑制（NMS），避免不同类别的重叠目标互相抑制。

这段解码读取后端输出时不改写其缓冲区。它只把最终框、类别和分数复制到自己的结果对象；NaN、Infinity 或明显不合法的分类概率会直接报错，而不是变成看起来正常的业务结果。

## 六、把检测结果交给 CLIP

### 6.1 文本只编码一次

CLIP 的文本图输入为 `input_ids`、`attention_mask`，均为 INT64，形状 `[候选数,77]`；输出为 `[候选数,512]` 的 `text_embedding`。

随文英文 tokenizer 使用配套词表和 BPE 合并表，完成小写清洗、切词、BOS/EOS、77 位填充和 attention mask。描述太长时直接报错，不静默丢弃尾部。它限定可打印 ASCII 英文候选，不把简化清洗冒充支持任意语言与 Unicode 修复的完整 tokenizer。

作者侧使用同一 Release 的 `tokenizer.json` 做了独立对照：12 条默认描述加 3 条大小写、标点与缩写测试，共 15 条、1155 个位置，**token ID 和 mask 全部一致**。这不是对所有文本的覆盖性证明。

文本模型通过后端 Tensor 接口运行，核心输入组织如下：

```csharp
var inputs = new InferenceInputs([
    new NamedTensor("input_ids",
        new Tensor<long>(new TensorShape(prompts.Length, 77), ids)),
    new NamedTensor("attention_mask",
        new Tensor<long>(new TensorShape(prompts.Length, 77), mask))
]);

var outputs = await textSession.RunAsync(inputs, cancellationToken);
```

`ids` 和 `mask` 来自完整工程中的 `EnglishClipTokenizer.Encode`。得到文本特征后，示例释放文本 Session，保留自己的归一化特征数组，再加载大的图像 Encoder，减少两套权重同时驻留；每个 ROI 不再重复执行文本编码。

### 6.2 源图 ROI 组成真正的图像 Batch

检测框每侧先扩展默认 `0.06` 的相对边距，再取整并裁到图内。推理阶段原图只解码一次，`PrepareBatch` 按区域集合准备 CLIP 的 `[B,3,224,224]` 输入；每个区域有独立的几何信息。最后的绘图工具会另读原图，用来保存叠框图与原始 ROI，不参与模型输入准备。

```csharp
using var decoded = new OpenCvVisualInputFactory()
    .DecodeForRois(OpenCvImageSource.FromFile(options.Image));

ObjectDetections detected = await detectionRunner.RunAsync(
    decoded, cancellationToken: cancellationToken);

// regions 是从检测源图框生成的 RectangleRoiGeometry 列表。
ImageEmbeddings embeddings = await imageRunner.RunAsync(
    new RoiBatch(decoded, regions), cancellationToken: cancellationToken);
```

图像模型定义使用动态 Batch，范围 `1—16`；示例默认 `--batch 4`，最后一批按实际区域数处理。`--max-rois 16` 限制单图进入语义阶段的区域数，超出后仍保存检测结果，并记录 `max-rois-limit`。

这属于模型自身的真实 Batch。它没有为每个区域创建新 Session，也没有用多条单图线程冒充 Batch。若换成只接受 Batch=1 的自有图，必须同步修改输入定义和调度。

### 6.3 分数含义与复核标记

图像与文本的 512 维特征分别做 L2 归一化。点积就是余弦相似度，乘 CLIP 的尺度后，在该目标对应的候选组内做稳定 Softmax。

| 字段 | 实际含义 | 使用时注意 |
| --- | --- | --- |
| `Detection.Score` | NanoDet 对闭集类别的检测分数 | 用于检测筛选，不是 CLIP 匹配分数 |
| `Cosine` | 图像与一条文本的特征余弦相似度 | 保存原始配对信息，便于分析 |
| `CandidateScore` | 当前候选组内的相对 Softmax 分数 | 候选改变，分数也会改变；不是经过标定的业务概率 |
| `Margin` | Top-1 与 Top-2 相对分数之差 | 可用于提示歧义，不能证明业务准确率 |
| `RequiresReview` | 分差小于默认 `0.1` | 教程演示规则，需要用业务数据重新标定 |

完整报告中的每个目标还包含原始框、实际 ROI、候选 Top-3、候选分组及跳过原因。即使没有对应候选或超过 ROI 上限，也不会从报告中无声消失。

## 七、会话、内存和异步调用怎样安排

三个模型联合时，如果每个目标都加载一次模型、反复读取整张图，运行开销会明显增加。本例把检测和图像 Runner 放到循环外创建，文本特征提前计算，推理阶段共享一次原图解码，各阶段仅按需要准备自己的张量。

![模型会话在循环外创建，原图解码一次，文本特征缓存，每批输入由 Runner 释放，业务结果持有自己的数据](assets/07-custom-visual/lifecycle.png)

| 资源 | 谁拥有 | 什么时候释放 |
| --- | --- | --- |
| `BackendRegistry` | 应用 | 所有模型任务结束后 |
| 检测、图像 Runner 与其 Session | 应用 | 完成当前处理任务后，生产应用可跨图片复用 |
| 文本 Session | 文本编码函数 | 候选编码完成后 |
| `OpenCvDecodedRoiImage` | 应用 | 当前图片的所有区域推理与输出完成后 |
| `PreparedVisualInput` | 返回后由 Runner 接管 | 该次推理及结果解码结束后 |
| 归一化特征和场景结果 | 业务代码 | 普通托管对象，不引用已释放 Mat 或后端数组 |

这条链路存在阶段依赖，示例按顺序 `await`。`RunAsync` 提供异步入口，并不自动创建独立 Session 池，也不等于多帧视频已经流水线并行；相关调度放到后续文章展开。

按 `Ctrl+C` 会请求取消，程序等待当前调用返回后释放资源，以退出码 `2` 结束；普通失败返回 `1`。成功返回 `0`。本例是单图工具，进程结束会释放文本特征；做常驻服务时，可以将候选特征与两个 Runner 提升到服务生命周期。

## 八、Windows 与 Ubuntu 运行

### 8.1 Windows 命令

在随文工程目录执行，先跑 ORT CPU，再切换 OpenVINO CPU。输出按后端分目录，互不覆盖。

```powershell
dotnet restore
dotnet build -c Release

dotnet run -c Release --no-build -- --backend ort-cpu `
  --models models --image images/bus.jpg --prompts prompts.json `
  --batch 4 --out results

dotnet run -c Release --no-build -- --backend openvino `
  --models models --image images/bus.jpg --prompts prompts.json `
  --batch 4 --out results
```

### 8.2 Ubuntu 22.04 命令

同一份业务源码在 Ubuntu x64 构建时会选择 Linux 原生包。需要 .NET 10 SDK，以及 OpenCV/OpenVINO 运行时依赖的系统库。在工程目录中执行：

```bash
dotnet restore
dotnet build -c Release

export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"

dotnet bin/Release/net10.0/CustomVisual.dll \
  --backend ort-cpu --models models --image images/bus.jpg \
  --prompts prompts.json --batch 4 --out results

dotnet bin/Release/net10.0/CustomVisual.dll \
  --backend openvino --models models --image images/bus.jpg \
  --prompts prompts.json --batch 4 --out results
```

Ubuntu 的 OpenCV 入口是 `libJYPPX.OpenCV.Native.so`，OpenVINO C 入口是 `libopenvino_c.so`。不要照搬 Windows DLL 名，也不要写成其他 OpenCV C# 封装使用的 `libOpenCvSharpExtern.so`。

如构建出现 `NETSDK1206`，需要核对发行版 RID 资产是否部署。随文工程已经显式复制 Ubuntu 原生文件；警告是否存在与运行是否成功要分别判断。

### 8.3 输出文件怎样阅读

```text
results/
  ort-cpu/
    scene.json
    scene.jpg
    roi-00.jpg
    roi-01.jpg
    ...
  openvino/
    scene.json
    scene.jpg
    roi-00.jpg
    ...
```

`scene.jpg` 叠加检测框和最优候选 Key；标签后的 `?` 表示触发教程中的复核规则。`roi-XX.jpg` 是原图实际裁出的区域，供读者检查上下文。CLIP 仍会在 ROI 内进一步执行短边缩放与中心裁剪；不要把保存的 ROI 图片误认为已经送入模型的 224×224 张量。

`scene.json` 保留完整候选配置、设备运行时信息、检测框、实际裁剪框和 Top-3 匹配值。控制台只展示简要结果，不把完整报告全部贴到文章里。

### 8.4 两套系统、两个后端的实际结果

这次使用相同的三个公开模型、`bus.jpg`、12 条候选和默认检测参数，在下面两套设备上运行完整工程。两套系统都使用 .NET 10，依赖分别按操作系统恢复。

| 项目 | Windows 设备 | Ubuntu 设备 |
| --- | --- | --- |
| 系统 / 架构 | Windows 11，Build 26200，x64 | Ubuntu 22.04.5 LTS，x64 |
| CPU | AMD Ryzen 7 5800H | Intel Core i7-1165G7 |
| .NET SDK / Runtime | 10.0.401 / 10.0.12 | 10.0.301 / 10.0.9 |
| DeploySharp / OpenCV 原生包 | 2.0.0 / 5.0.0 | 2.0.0 / 5.0.0 |
| ORT CPU / OpenVINO CPU 原生包 | 1.28.0 / 2026.2.1 | 1.28.0 / 2026.3.1 |

四组运行均正常结束，检测到 **4 名行人、1 辆公交车**；5 个目标都进入 CLIP，实际图像批次为 **4 + 1**。这同时覆盖了动态 Batch 和尾批处理。

| 系统 | 后端 | 检测 / 语义 ROI | 五个目标的候选 Top-1 | 需要复核 |
| --- | --- | --- | --- | --- |
| Windows | ONNX Runtime CPU | 5 / 5 | gray、green、blue、red、gray | #3 |
| Windows | OpenVINO CPU | 5 / 5 | gray、green、blue、red、gray | #3 |
| Ubuntu | ONNX Runtime CPU | 5 / 5 | gray、green、blue、red、gray | #3 |
| Ubuntu | OpenVINO CPU | 5 / 5 | gray、green、blue、red、gray | #3 |

上表按报告中的目标 ID 排列；行人 Key 前缀为 `person-`，车辆为 `vehicle-`。四组的检测类别、候选 Top-1 和复核标记一致，浮点分数存在小幅差异。下面是 Windows ORT CPU 的实际输出节选：

```text
ort-cpu: detections=5; semantic-rois=5; prompts=12; clip-batch=4
#0 person det=0.7648 => person-gray; candidate-score=0.3909; review=False
#1 bus det=0.6348 => vehicle-green; candidate-score=0.9012; review=False
#2 person det=0.4792 => person-blue; candidate-score=0.5710; review=False
#3 person det=0.4779 => person-red; candidate-score=0.3761; review=True
```

![Windows 的真实检测叠框、CLIP 候选匹配与两个 CPU 后端的完整链路结果](assets/07-custom-visual/windows-result.png)

**能跑通链路，并不代表每条描述都正确。** 例如，图中公交车呈蓝白配色并带有绿色图案，CLIP 却把 `vehicle-green` 排在第一；前景人物穿米色外套，默认候选组没有米色外套，只能在现有“某颜色 shirt”描述中比较。这里的 `0.9012` 仍然只是组内匹配分数，不能当作已标定的绿色属性概率。

这里的 ROI 会继续中心裁剪，外观上下文可能变化；候选描述是否完整、颜色是否混合、遮挡和目标尺寸都会影响匹配。这个例子适合学习区域检索的接法；要用于属性判定，应补齐候选、选择合适的 ROI 策略，或接入经过业务标注验证的专用模型。即使 `RequiresReview=False`，也不能据此保证语义正确。

Ubuntu 使用同一份业务源码和相同模型，在本机原生运行时上得到下面的结果。图中保留真实候选值，没有把有歧义的匹配改成手工标签。

![Ubuntu 上三个 ONNX 模型联合运行的真实结果，ORT 与 OpenVINO 均完成五个 ROI](assets/07-custom-visual/ubuntu-result.png)

作者侧还用独立 NumPy/ORT 实现核对了 NanoDet 的默认解码：五个框的最大坐标差约 `0.00006` 像素，检测分数差小于 `0.00000003`。它检查的是这张图的实现一致性；本篇没有用单图证明检测准确率，也没有把两种 CPU 路径的结果扩展为 TensorRT、OpenCV DNN 或全部模型可用，更没有进行推理速度排名。

## 九、换模型或换业务时，先检查什么

| 现象或需求 | 先检查的地方 | 修改方式 |
| --- | --- | --- |
| 端口、shape 不匹配 | `--inspect`、输入与六个输出合同 | 按新图同步 Builder 和 Decoder，不能只改文件名 |
| 框位置整体偏移或缩放不对 | `ImageTransform`、源图/模型坐标、padding | 使用实际前处理的逆变换，再裁到原图范围 |
| 检测或匹配看起来明显错误 | BGR/RGB、mean/std、几何、类别顺序 | 每个模型独立核对处理约定，先保存实际输入排查 |
| CLIP 高分但语义不符合业务 | 候选描述是否覆盖真实情况，ROI 是否缺上下文 | 重新设计候选与裁剪策略，用标注集验证，不能只调分差 |
| 想使用中文或任意语言候选 | tokenizer 的语言范围和模型适用性 | 替换为经过对照的对应 tokenizer，不强塞进英文适配器 |
| 目标多，内存或推理开销上升 | `--batch`、`--max-rois`、候选数 | 分批、有界处理，复用 Session 与文本特征 |
| OpenCV 或 OpenVINO 原生库加载失败 | OS、架构、输出目录、Linux 库搜索路径 | 安装匹配的原生包，检查入口与依赖库 |

原始 NanoDet 图可能提示 initializer 同时列在 graph inputs 中。它来自旧导出格式，会影响部分常量优化；需要将模型转换优化与结果验证分开，不能仅凭这条警告判定推理失败。本篇直接使用原始公开图，没有要求读者先写转换脚本。

这个案例的重点是 **新模型接入与多模型组合**：NanoDet 接入在业务工程完成，CLIP 配套资产已有图文接口，联合报告则是自己的业务代码。这不代表主库新增了内置 NanoDet Profile，也不代表其他 NanoDet 导出、CLIP 变体或全部后端都已经验证。

## 十、下一步：让连续输入也能高效运行

从这一篇开始，我们已经不局限于“加载一个模型、打印一个结果”。输入可以携带已解码图像和区域集合，输出可以是自己的业务对象，多模型之间通过源图坐标、张量与特征明确交接。

接下来会继续展开 **Batch、独立 Session 池和异步预取**：当输入变成一批图片或连续视频帧时，怎样选择批量与并发，怎样让准备、推理和结果处理合理衔接，以及如何管理取消和资源释放。

```text
DeploySharp 项目：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0

系列文章目录：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0

本篇完整代码：
https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/docs/blog/2.0.0/2026-10/examples/07-custom-visual
```
