# 自定义视觉模型与预处理合同

这篇文章说明如何把一个尚未内置的视觉模型接入 DeploySharp，以及如何在不修改后端代码的情况下切换图像预处理方式。接入分成四个明确的合同：

1. 输入张量合同：输入名称、元素类型、布局和形状；
2. 预处理合同：几何、颜色、归一化、插值、填充和 Batch；
3. 输出张量合同：输出名称、类型和形状；
4. 结果解码合同：把已经校验过的输出转换为自己的结果类型。

这样做的好处是，模型文件、预处理和后处理不会再通过文件名或隐式约定关联。一个自定义模型可以复用 `VisualPipeline`、ONNX Runtime、OpenVINO、TensorRT 等后端；只有输入图像适配器和结果 Decoder 需要由应用提供。

## 最小接入路径

下面的示例假设模型输入为 `images`，形状为 `[1,3,640,640]`，输出为 `scores`。`CustomClassificationResult` 是应用自己的结果类，模型的 ONNX 工件和 SHA-256 应在实际项目中填写。

```csharp
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

var task = new VisualTaskId("custom-classification");

var definition = new VisualModelDefinitionBuilder<OpenCvImageSource, CustomClassificationResult>(
        "custom/classification.v1",
        new ModelId("custom/classification"),
        task,
        "1",
        "onnx")
    .WithInput(
        "images",
        TensorElementType.Float32,
        new TensorShape(1, 3, 640, 640),
        VisualTensorLayout.Nchw)
    .WithPreprocessing(new VisualPreprocessingOptions(
        new VisualSize(640, 640),
        VisualResizeMode.Letterbox,
        VisualColorOrder.Rgb,
        VisualNormalizationOptions.Scale(255f),
        VisualTensorLayout.Nchw,
        paddingColor: new VisualRgbColor(114, 114, 114)))
    .AddOutput("scores", TensorElementType.Float32, new TensorShape(1, 1000))
    .WithInputPreprocessor((source, profile, cancellationToken) =>
    {
        // The factory consumes the same contract registered on the profile.
        return new OpenCvVisualInputFactory().Create(source, profile, cancellationToken: cancellationToken);
    })
    .WithDecoder(context =>
    {
        Tensor<float> scores = context.GetOutput<float>("scores");
        float[] values = (float[])scores.Buffer;
        int best = 0;
        float bestScore = values[0];
        for (int index = 1; index < values.Length; index++)
        {
            if (values[index] > bestScore)
            {
                best = index;
                bestScore = values[index];
            }
        }

        return new CustomClassificationResult(best, bestScore);
    })
    .Build();

// CreateRunner owns the selected inference session. The registry remains application-owned.
using var runner = definition.CreateRunner(backendRegistry, artifact, backendRequest);
var source = OpenCvImageSource.FromFile(Path.GetFullPath("image.jpg"));
CustomClassificationResult result = runner.Run(source);

public sealed class CustomClassificationResult
{
    public CustomClassificationResult(int classIndex, float score)
    {
        ClassIndex = classIndex;
        Score = score;
    }

    public int ClassIndex { get; }
    public float Score { get; }
}
```

`WithInputPreprocessor(Func<...>)` 和 `WithDecoder(Func<...>)` 是为了减少一次性模型接入的样板代码。规模较大的项目可以把它们提取为实现 `IVisualInputPreprocessor<TInput>` 和 `IVisualResultDecoder<TResult>` 的独立类，便于单元测试和复用。

## 自定义输入和结果

如果输入不是图片，也可以把 `TInput` 换成任意应用类型。例如视频帧、相机缓冲区或已经解码的自定义图像对象。预处理器只需要返回一个 `PreparedVisualInput`：

```csharp
var custom = new VisualModelDefinitionBuilder<MyFrame, MyResult>(
        "custom/segmentation.v1",
        new ModelId("custom/segmentation"),
        new VisualTaskId("custom-segmentation"),
        "1",
        "onnx")
    .WithInput("input", TensorElementType.Float32,
        new TensorShape(1, 3, 512, 512), VisualTensorLayout.Nchw)
    .AddOutput("masks", TensorElementType.Float32,
        new TensorShape(1, 1, 512, 512))
    .WithInputPreprocessor((frame, profile, cancellationToken) =>
    {
        // 这里可以调用相机 SDK 或自己的 SIMD/GPU 预处理器。
        ITensor tensor = MyPreprocessor.ToTensor(frame, cancellationToken);
        return new PreparedVisualInput(
            profile.Input.Name,
            tensor,
            frame.Size,
            new VisualSize(512, 512),
            1,
            VisualTensorLayout.Nchw,
            ImageTransform.Resize(frame.Size, new VisualSize(512, 512)));
    })
    .WithDecoder(context => MyResult.FromOutputs(context))
    .Build();
```

自定义 `PreparedVisualInput` 的规则如下：

- `Tensor` 的元素类型、布局、Batch 和形状必须与 `WithInput` 一致；
- `Transform` 必须描述源图到模型图的真实变换，检测框、掩码和关键点才能还原到源图坐标；
- 如果由预处理器拥有 native 资源，使用 `PreparedInputOwnership.Owned` 和 `ownedResource`，不要把已经释放的 Mat 或外部缓冲区交给后端；
- `PreparedVisualInput` 用完后由 Runner 释放，长期缓存时必须明确所有权；
- Decoder 只读取 `VisualDecodeContext.Outputs`，不要重新执行后端推理或修改输出张量。

## 切换几何方式

内置视觉 Profile 会携带官方默认预处理。默认值不能根据模型名称猜测；例如 YOLO 检测通常是居中 Letterbox，而分类模型可能是固定 Resize 或 Shortest-edge CenterCrop。需要改变默认值时，基于现有合同创建副本：

```csharp
VisualPreprocessingOptions overrideOptions = profile.Preprocessing!
    .WithResizeMode(VisualResizeMode.Resize)
    .WithNormalization(VisualNormalizationOptions.Scale(255f))
    .WithInterpolation(VisualInterpolationMode.Linear)
    .WithScaleUp(false);

VisualModelProfile configuredProfile = profile.WithPreprocessing(overrideOptions);
using PreparedVisualInput input =
    new OpenCvVisualInputFactory().CreateFromFile("image.jpg", configuredProfile);
```

可用的几何方式：

| 模式 | 行为 | 适用场景 |
| --- | --- | --- |
| `Resize` | 宽高独立缩放，不保持宽高比 | 模型训练就是直接缩放，或模型明确要求固定画布 |
| `Letterbox` | 保持宽高比，四周填充 | YOLO 检测、需要还原坐标的检测任务 |
| `CenterCrop` | 按目标宽高比从中心裁剪，再缩放 | 分类和固定视野模型 |
| `LongestSidePadBottomRight` | 缩放最长边，在右侧和底部补齐 | SAM 类最长边输入或导出合同明确要求的模型 |
| `ShortestEdgeCenterCrop` | 缩放最短边，再居中裁剪 | CLIP/SigLIP 等图像编码器 |

`WithScaleUp(false)` 可禁止保持宽高比模式放大较小图片。`WithDimensionRounding` 控制 Letterbox 的整数尺寸，`WithInterpolation` 控制插值，`WithPaddingColor` 控制填充值，`WithColorOrder` 控制 RGB/BGR/灰度/RGBA 输出。

## 切换归一化

归一化始终在同一份合同中声明：

```csharp
VisualNormalizationOptions.None;
VisualNormalizationOptions.Scale(255f); // pixel / 255
VisualNormalizationOptions.MeanStandardDeviation(
    new[] { .485f, .456f, .406f },
    new[] { .229f, .224f, .225f },
    new[] { 255f }); // (pixel / 255 - mean) / std
VisualNormalizationOptions.DivideByStandardDeviation(
    new[] { 127.5f, 127.5f, 127.5f }); // pixel / 127.5
VisualNormalizationOptions.ImageNet;
```

均值、标准差和输入除数既可以提供一个标量，也可以提供逐通道的三个值。`UInt8` 输出不能使用浮点归一化；如果模型输入是 `UInt8`，请使用 `VisualPreprocessingOutputType.UInt8` 和 `VisualNormalizationOptions.None`。

## 后端边界

共享合同由适配器翻译，不会让后端重复处理：

- OpenCV 使用 `OpenCvVisualInputFactory.Create(..., VisualPreprocessingOptions)`；
- TensorRT 的视觉流水线使用同样的几何、填充和归一化字段；
- ONNX Runtime、OpenVINO 和自定义后端接收已经准备好的 `PreparedVisualInput`，不需要再次 Resize、交换通道或归一化；
- OpenCV DNN 的动态 shape、辅助输入和 importer 限制仍由其兼容性文档约束，合同可表达不代表底层 importer 一定支持；
- 动态尺寸的 OCR、多阶段 PaddleOCR、SAM prompt 输入和生成式视觉语言模型具有额外的坐标、Tokenizer 或多图合同，应优先使用它们的专用 Factory，不能用通用 Resize 覆盖破坏官方协议。

覆盖预处理后，必须重新验证模型输出。几何改变会影响检测坐标、掩码和关键点，归一化改变会影响分类分数和生成模型的输出分布；DeploySharp 不会声称覆盖后的结果与官方处理完全等价。

## 测试清单

自定义模型至少应覆盖：

1. 一张非正方形图片，确认 `Transform` 能把结果还原到源图；
2. `Resize`、`Letterbox`、`CenterCrop` 三种几何模式的尺寸和边界；
3. RGB 与 BGR、标量与逐通道归一化；
4. Batch=1 和模型实际支持的最大 Batch；
5. 一个后端的数值结果，以及另一个后端的结构/坐标一致性；
6. 取消、异常输出、NaN/Infinity 和输入释放；
7. 使用 `RunAsync` 时，确认预处理产生的资源在推理完成后才释放。

更多生命周期、批处理和并发行为见[Visual 快速开始与生命周期](visual-getting-started.md)和[Batch、Session 池与并发](batch-session-concurrency.md)。
