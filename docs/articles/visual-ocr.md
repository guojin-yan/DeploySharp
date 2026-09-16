# OCR 检测与识别

DeploySharp 的 OCR Pipeline 将文字检测、透视裁剪、可选方向处理和 CTC 文字识别组合为一个可复用流程。检测器和识别器可以使用同一个后端，也可以分别选择 ONNX Runtime、OpenVINO、OpenCV DNN 或 TensorRT 中已经注册的后端。

Pipeline 只处理模型张量，图像解码和几何裁剪由可选的 <code>JYPPX.DeploySharp.Visual.OpenCV</code> 适配器完成。模型的输入输出名称、尺寸、归一化、字符表和 CTC 参数必须与实际导出文件一致。

## 流程

1. 图像适配器只解码一次源图，并准备检测器输入。
2. 检测 Decoder 校验 polygon 和 score，执行阈值过滤、精确 polygon NMS，并把保留区域按阅读顺序排列。
3. 每个区域生成一个裁剪请求，完成透视裁剪、直角旋转、resize、padding、颜色转换和归一化。
4. 文本行按目标宽度分组，以有界 batch 提交识别器；CTC Decoder 执行 argmax、repeat collapse、blank 移除和置信度计算。
5. 结果中的坐标通过同一个 <code>ImageTransform</code> 还原到原图，并带有阶段耗时和模型来源信息。

Alpha.1 的方向策略由 Profile 或调用方配置提供，仅支持 0、90、180、270 度直角旋转。Pipeline 不会偷偷运行方向分类器，也不会猜测 polygon 的点顺序。

## 快速使用

下面示例展示 OpenCV 图像输入和一个动态宽度识别器；检测器和识别器的 Profile、Artifact、输入输出名称需要替换为实际模型配置。

~~~csharp
var detectorOptions = new OpenCvPreprocessOptions(
    new VisualSize(960, 544),
    OpenCvResizeMode.Letterbox,
    VisualColorOrder.Rgb,
    outputType: OpenCvOutputType.Float32);

using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory()
    .CreateFromFile(imagePath, "images", detectorOptions);

var cropProfile = new TextCropProfile(
    "my-ocr/crop.v1",
    targetHeight: 48,
    widthMode: OcrRecognitionWidthMode.Dynamic,
    fixedWidth: 320,
    maximumWidth: 640,
    widthAlignment: 8,
    colorOrder: VisualColorOrder.Rgb,
    layout: VisualTensorLayout.Nchw,
    means: new[] { 127.5f },
    scales: new[] { 1f / 127.5f });

using var ocr = new OcrPipeline(
    backendRegistry,
    detectorSelection,
    detectorRequest,
    recognizerSelection,
    recognizerRequest,
    cropProfile,
    new OcrPipelineOptions(
        maximumConcurrency: 2,
        maximumRegions: 128,
        maximumRecognitionBatch: 16));

OcrResult result = await ocr.RunAsync(
    input,
    new OcrExecutionOptions(timeout: TimeSpan.FromSeconds(10)),
    cancellationToken);

foreach (OcrRegionResult region in result.Regions)
    Console.WriteLine($"{region.Recognition.Text} ({region.Recognition.Confidence:P1})");
~~~

<code>OpenCvOcrImageInputFactory</code> 在输入释放前保留源图；Pipeline 释放所有临时 transform、warp、旋转和 ROI。调用方负责释放自己创建的图像输入、Registry 和其他资源。

## 模型和后端配置

检测模型通常使用 batch=1；识别模型可声明动态宽度和动态 batch。`OcrPipelineOptions.MaximumRecognitionBatch` 限制一次提交的文本行数量；同一配置里的 `MaximumConcurrency` 限制并发的完整 OCR 调用，**不等于 REC Session 数量**。通过构造 `OcrPipeline` 时的 `recognitionSessionOptions: new SessionOptions(2)` 配置两个独立识别通道，再由 Pipeline 将有界批任务分派给空闲通道。

动态宽度会产生 padding。<code>MaximumRecognitionPaddingRatio</code> 默认为 1.0，只把等宽文本行放在同一批次。目标后端经过实测后，可以适当调高该值以减少 batch 数，但要同时观察填充计算和显存占用。

一个 ModelPack 可以同时携带检测和识别 ONNX，或对应的 OpenVINO IR XML/BIN，并通过 <code>deploysharp.ocr.*</code> 扩展键绑定 Profile、字符集和预处理版本。字符表的 blank、unknown、Unicode 顺序必须和 logits 导出一致。

## 长文本宽度诊断与超宽策略

本节新增接口以当前开发源码为准；旧 NuGet 包不会自动获得这些 API，使用前应确认所安装版本包含此能力。

`PaddleOcrProfiles.CreateRecognition` 的主库默认值为 `TargetHeight=48`、`MinimumWidth=48`、`MaximumWidth=3200`。基准工具历史默认的 320 只是测试参数，不是主库上限。提高宽度前，应确认模型、动态 shape 以及 TensorRT optimization profile 支持该范围；修改裁剪配置不会自动扩大 engine 的 shape 范围。

```csharp
// recognitionProfile 是 PaddleOcrProfiles.CreateRecognition(...) 的返回值。
TextCropProfile crop = recognitionProfile.CropProfile!
    .WithRecognitionOverflowMode(RecognitionOverflowMode.Reject);
// 将 crop 作为 OcrPipeline 的 cropProfile 参数。

foreach (OcrRegionResult item in result.Regions)
{
    if (item.RecognitionWidth is OcrRecognitionWidthInfo width)
        Console.WriteLine($"region={item.Region.SourceIndex}, natural={width.NaturalWidth}, " +
            $"target={width.TargetWidth}, tensor={width.TensorWidth}, compressed={width.WidthClamped}");
}
```

| 字段 | 含义 |
| --- | --- |
| `NaturalWidth` | 按已定向四边形长宽比计算的 `ceil(TargetHeight × width / height)`；在最小宽度、对齐和上限之前 |
| `TargetWidth` | 当前区域经最小值、对齐和上限处理后的规划宽度；固定宽度模式采用 `FixedWidth` |
| `TensorWidth` | 实际提交的 Batch 张量宽度，可能包含与其他更宽文本行共同补齐的 padding |
| `WidthClamped` | 自然宽度大于该区域受限宽度，意味着水平压缩；仅增加 padding 不算压缩 |
| `BatchPadded` | `TensorWidth > TargetWidth`，仅表示 Batch 补齐 |

这些值由几何合同计算；OpenCV 透视中间图的边长会先取整，因此不是逐像素测得的 crop 栅格宽度。90°/270°方向先交换长宽，再做计算。宽度对齐产生的额外 padding 被上限裁掉时，只要原始内容仍可放下，就不会误报压缩。

- **Clamp（兼容默认）**：超长行仍缩放到上限，结果的 `RecognitionWidth.WidthClamped=true`。它不是切分或无损长文本识别。
- **Reject（显式启用）**：在识别 crop 分配和 REC 调用之前抛出 `OcrPipelineException`，错误码 `DS-VISUAL-4103`，阶段 `CropAndBatch`；包含 crop Profile、原 `RegionIndex` 和自然/受限宽度。DET 和可选 CLS 此时可能已经执行。一个区域超宽会使本次完整 OCR 调用失败，不静默丢掉该行；输入释放与取消语义不变。
- **Split / SlidingWindow**：属于下一实施步骤，目前没有可用的公开枚举值或自动拼接能力，不能当作已支持。

`crop.DescribeWidth(quadrilateral, orientation)` 只返回诊断，即使是 Reject 也不抛超宽异常，便于应用先规划；`CalculateWidth`、`TextCropRequest` 和实际 Pipeline 会执行 Reject。固定宽度模型的有效限制是 `FixedWidth`，不能通过更大的 `MaximumWidth` 绕过。

既有构造函数和默认策略保留；手工使用旧 `OcrRegionResult(region, recognition)` 构造结果时，`RecognitionWidth=null` 表示诊断未知，不代表没有压缩。方向恢复及 ROI 投影/合并保留宽度来源。既有 `OcrResult.ComputeSha256()` 继续针对识别内容与几何，不加入这些诊断，以便与历史结果对照。

基准工具可设置 `DEPLOYSHARP_PADDLEOCR_OVERFLOW_MODE=Clamp/Reject`、`DEPLOYSHARP_PADDLEOCR_MAXIMUM_WIDTH` 和 `DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR`。报告在计时外导出输入/模型/程序集 SHA、每行文字、polygon、字典 SHA 和宽度信息；完整操作见[基准工具说明](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/tools/DeploySharp.PaddleOcrBenchmark)。

## 性能测量建议

### 本步正确性验证（2026-09-16）

使用同一 `demo_1.jpg`、v4 mobile/v5 mobile/v6 tiny，在 ONNX Runtime 1.23.2 CPU 与 CUDA 上验证了以下行为。设备为 Windows x64 / RTX3060 Laptop；这不是所有模型/后端的完整验收。

| 检查 | 实测结果 |
| --- | --- |
| 同样的 Clamp/320 设置，改动前后各 6 组 | 文本 SHA 与完整结果合同 SHA 均一致 |
| Clamp/320 的宽度记录 | v4 有 13/16 个区域压缩，v5/v6 各 12/16；两后端一致 |
| Reject/320，各 6 组负向调用 | 都返回 `DS-VISUAL-4103` 和源区域索引，未作为模型“不支持”掩盖 |
| Reject/3200，各 6 组 | 完整流水线通过；压缩区域为 0，同版本 CPU/CUDA 文本 SHA 一致 |

将宽度从 320 改为 3200 后，本图实际规划的最大宽度是 859～973，而不是给每行都分配 3200。文本指纹有变化，但目前没有人工标注真值，不能将变化直接称为准确率提升。warmup=1/iterations=3 的短测用于验证行为，不作为性能结论；性能调优与长文本完整性指标将在后续步骤单独验收。

- 检测、裁剪/warp、识别 batch 准备、后端推理、CTC 解码和合并应分别计时。
- 视频逐帧可使用 <code>VisualPipeline.RunPrefetchedAsync</code> 重叠下一帧准备与当前帧推理。
- 多张独立图片可用 <code>RunManyAsync</code>；它是独立 Session 并发，不会把 batch=1 模型变成真正 batch。
- GPU 后端应尽量复用输入缓冲区和 CUDA stream；TensorRT OCR 的设备侧前后处理边界见[TensorRT CUDA OCR](tensorrt-cuda-ocr.md)。

详细测速方法和批量调度方式见[推理性能测试](performance-benchmarking.md)，不同设备的实际结果见[设备性能实测](device-performance-benchmarks.md)。

## 常见问题

| 现象 | 处理 |
| --- | --- |
| 找不到输入或输出 | 用 Netron 检查名称、布局、dtype 和动态维度，并同步修改 Profile |
| 检测框位置偏移 | 检查 resize、letterbox、padding 和坐标空间是否与导出脚本一致 |
| 识别乱码 | 检查字符表版本、blank index、logits layout 和归一化参数 |
| batch 运行失败 | 确认识别输入声明动态 batch；静态 batch 模型应降低 batch 或创建多个 Session |
| 内存持续增长 | 复用 Pipeline，及时释放输入和结果；降低最大区域数、batch 数或并发通道 |

模型是否已经在某个后端完成真实验证，以[模型支持状态](model-support.md)和[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。
