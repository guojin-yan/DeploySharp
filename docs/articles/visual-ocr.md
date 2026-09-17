# OCR 检测与识别

DeploySharp 的 OCR Pipeline 将文字检测、透视裁剪、可选方向处理和 CTC 文字识别组合为一个可复用流程。检测器和识别器可以使用同一个后端，也可以分别选择 ONNX Runtime、OpenVINO、OpenCV DNN 或 TensorRT 中已经注册的后端。

Pipeline 只处理模型张量，图像解码和几何裁剪由可选的 <code>JYPPX.DeploySharp.Visual.OpenCV</code> 适配器完成。模型的输入输出名称、尺寸、归一化、字符表和 CTC 参数必须与实际导出文件一致。

## 流程

1. 图像适配器只解码一次源图，并准备检测器输入。
2. 检测 Decoder 校验 polygon 和 score，执行阈值过滤、精确 polygon NMS，并把保留区域按阅读顺序排列。
3. 每个区域生成裁剪请求；显式启用超长行滑窗时生成多个有界窗口。适配器完成透视裁剪、直角旋转、resize、padding、颜色转换和归一化。
4. 文本行按目标宽度分组，以有界 batch 提交识别器；CTC Decoder 执行 argmax、repeat collapse、blank 移除和置信度计算。
5. 结果中的坐标通过同一个 <code>ImageTransform</code> 还原到原图，并带有阶段耗时和模型来源信息。

方向校正可以来自显式的 0、90、180、270 度配置、可选 CLS Pipeline，或竖排长宽比策略。四边形透视裁剪可处理显式倾斜角点；可选几何检查报告基线角度和透视风险，不自动猜测文字是否倒置。低置信度 REC 结果可显式启用有界方向重试。方向来源和 polygon 角点顺序必须明确。

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

## 可选源像素质量诊断

质量诊断用于定位低对比度、小区域或采样清晰度不足等线索，不自动增强图像、不拒绝低分行、不改变识别配置。当前源码提供 `OcrExecutionOptions.WithPixelQuality(...)`，默认关闭；旧 NuGet 包需升级到包含该接口的版本。

```csharp
var execution = new OcrExecutionOptions(timeout: TimeSpan.FromSeconds(10))
    .WithPixelQuality(new OcrPixelQualityOptions(
        maximumSamplesPerArea: 4096,
        maximumRegions: 128,
        maximumSamplesPerCall: 1048576));
OcrResult result = await ocr.RunAsync(input, execution, cancellationToken);
Console.WriteLine($"源图亮度均值: {result.PixelQuality?.MeanLuminance}");
foreach (OcrRegionResult line in result.Regions)
{
    OcrPixelQualityDiagnostics? q = line.PixelQuality;
    Console.WriteLine($"{line.Region.SourceIndex}: samples={q?.SampleCount}, " +
        $"contrast={q?.LuminanceStandardDeviation}, laplacianVariance={q?.LaplacianVariance}");
}
```

### 采样对象与结果含义

整图与每个原 DET 多边形各评估一次，直接读取解码后的**源像素**，不是 REC resize、padding 或归一化后的张量。区域指标保留原始清晰度信息，但不代表校正后的 crop 质量，更不是单字质量；滑窗和方向重试不会反复采样。

| 字段 | 定义与限制 |
| --- | --- |
| `MeanLuminance` | 8 位亮度均值，范围 0～255 |
| `LuminanceStandardDeviation` | 亮度总体标准差，反映采样对比度，不是识别率 |
| `DarkPixelFraction` / `LightPixelFraction` | 亮度 ≤8 / ≥247 的比例；黑字白纸也可能很高，不能据此断言欠曝或过曝 |
| `LaplacianVariance` | `L+R+U+D−4C` 的总体方差；使用原始相邻 1 像素，不把采样间距当作像素尺度；噪声也可能使其升高 |
| `MeanAbsoluteGradient` | `(|R−L|+|D−U|)/4` 的均值；是纹理/边缘线索，不区分文字、噪声和背景 |
| `RegionMinimumEdgePixels` | DET 多边形最短边；整图为 null，不应冒充字形或笔画高度 |
| `PlannedSampleCount` / `SampleCount` / `NeighborhoodCount` | 计划网格中心数、在区域内的有效中心数、四邻域均有效的中心数 |

中心以有界规则网格分布在多边形与图像边界相交的包围框内，只接受像素中心落在多边形中的样本；不创建 mask 或整图灰度副本。邻域只在四个相邻像素也属于区域时参与计算。没有有效中心时像素指标为 null；不足两个有效邻域时拉普拉斯方差为 null，不把缺失数据记为“质量好”。规则采样可能漏掉窄小结构或与周期纹理重合，不能与不同采样预算/分辨率的报告直接比较。

OpenCV 适配器使用 `Y=(77R+150G+29B+128)>>8`，支持 8 位 Gray/BGR/BGRA 和非连续 Mat，Alpha 与现有 OCR 预处理一致被忽略。`OpenCvOcrPixelQuality.Analyze(mat, ...)` 可独立检查借用的 Mat；调用期间必须防止其他线程修改或释放它。对 `OpenCvOcrImageInput` 的调用在其读锁内执行，复用已解码图像，不读取 GPU 张量。

### 资源、坐标和后端

- 每区域最多 4096 个中心，每中心最多读取 5 个亮度值。默认每次最多 128 个 DET 区域，另加整图；保守预算为 `(区域数+1)×maximumSamplesPerArea`。超出区域/总中心/结果内存限制返回 `DS-VISUAL-4102`，不静默漏报后面的区域。
- 检查与推理共享取消、超时、输入所有权和调用并发限制。统计计入 `CropAndBatch` 及端到端总时间，不藏在计时之外。不开启时不产生逐像素工作。
- `InputSize/InputPolygon/InputRegionIndex` 表示**评估时**的输入坐标与索引。整图方向恢复、ROI 投影和区域重新编号仍保留原证据，不将旧统计冒充新坐标的重新测量。多 ROI 合并保留各获选行的证据，合并后的整图 `PixelQuality` 为 null；可查看各原 ROI 结果的整图指标。
- `IOcrPixelQualityInput` 是可选输入能力，自定义适配器可调用纯托管 `OcrPixelQualityAnalyzer.Analyze` 提供亮度读取器。原 `IOcrImageInput` 接口不变；显式启用但适配器不支持时返回 `DS-VISUAL-4106`，不静默忽略。使用 OpenCV 输入时可配合不同推理后端；这不表示纯 CUDA 输入已经实现设备端质量分析。

当前没有通用质量总分、默认模糊阈值、噪声/JPEG 退化分类、精确字高或自动增强。需要在自己的图像分辨率、采样配置与标注数据上确定业务阈值。检查对文本/坐标/token 指纹没有影响；报告属于诊断而不是识别准确率证明。

## 识别裁剪的分阶段质量诊断

源图 `PixelQuality` 只描述原 DET 区域，不能代表透视校正与缩放后真正送入 REC 的内容。需要比较这两者时，在识别裁剪 Profile 上显式配置：

```csharp
TextCropProfile crop = recognitionProfile.CropProfile!
    .WithCropProcessing(new OcrCropProcessingOptions(
        maximumSamplesPerStage: 1024,
        maximumCropsPerCall: 1024));
// 将 crop 传给 OcrPipeline；图像输入使用 OpenCvOcrImageInputFactory。
```

每行 `OcrRegionResult.CropDiagnostics` 按实际识别窗口顺序保存：

- `Rectified`：透视/仿射校正与直角旋转完成后的局部图像，尚未 resize。
- `Content`：resize 后的有效内容，不包含右侧 tensor padding，也未做归一化。
- `TensorSize`：实际补齐张量的宽高，可能宽于 `Content.InputSize`。
- `InputRegionIndex`、`InputQuadrilateral`、`Orientation`：评估当时的区域、实际裁剪四角和附加旋转。滑窗四角已编码父行方向，因此窗口自身的附加旋转可为 0；不能再次套用父行旋转。

这些统计采用与源像素诊断相同的定义，但坐标是 crop 局部空间；其内部 `InputPolygon/InputRegionIndex` 为 null，不是缺少来源。ROI 投影、结果重新编号、全图方向恢复不改写已采集的证据。启用方向重试时，每个 `OrientationRetry.Attempts` 都保留自身诊断，最终行只引用获选尝试；不把多个窗口的统计平均后冒充一个真实裁剪。

默认 `CropProcessing=null`，不增加采样。只启用诊断不会改变张量、文字或既有结果 SHA。适配器复用既有 warp/resize 缓冲，不再次裁剪或保存像素；结果仅持有不可变数值。物理裁剪上限包含初次 REC、滑窗、重试以及 minimum-batch 重复补齐行，超限报 `DS-VISUAL-4102`，不静默截断。逐裁剪保守内存预算也计入 `MaximumResultBytes`，采样计入 `RecognitionPreparationWork` 与流水线总时间，不藏在计时外。

自定义输入可实现 `IOcrCropProcessingInput`，返回 `OcrPreparedCropBatch`，一条物理输入对应一份诊断。成功构造后 wrapper 拥有 `PreparedVisualInput`，调用方必须释放；诊断不允许持有 native/GPU 缓冲。未实现可选接口时显式开启返回 `DS-VISUAL-4107`。OpenCV 输入的 `PrepareRecognitionBatch` 只接受未启用该功能的 Profile，启用后应使用 `PrepareProcessedRecognitionBatch`，防止直接调用时静默丢弃配置。TensorRT device-only 输入尚未实现该可选接口；OpenCV 输入与推理后端是独立选择。

采样上限每阶段 1～65536、每调用物理 crop 1～4096。使用固定图像、插值、角点、采样预算与尺寸做对照；resize 本身就会改变锐度和方差，因此不能直接把两阶段数值之差等同于识别质量损失。当前没有经过标注数据标定的噪声/JPEG 分类、精确字高或通用拒绝分数。

### 可选低对比度增强

诊断与增强独立：不配置 `WithEnhancement` 就不改像素。当前提供六种局部操作，每个 Profile 选择一种。对比度类操作和自适应阈值仅在校正后 crop 的亮度标准差处于 `[minimumContrast, lowContrastThreshold)` 时应用；高斯去噪使用独立的拉普拉斯方差门限，反遮罩锐化使用相反的低清晰度门限；局部放大只在明确配置或作为重试候选时执行：

```csharp
var processing = new OcrCropProcessingOptions(1024, 1024)
    .WithEnhancement(new OcrCropEnhancementOptions(
        OcrCropEnhancementMode.ContrastNormalize,
        lowContrastThreshold: 32,
        minimumContrast: 1,
        targetStandardDeviation: 64,
        maximumGain: 3,
        maximumPixelsPerCrop: 1024 * 1024));
TextCropProfile crop = recognitionProfile.CropProfile!.WithCropProcessing(processing);
// 若明确需要灰度局部均衡，改用 GrayClahe，并配置 claheClipLimit/claheGridSize。
// 含脉冲/高频噪声的候选可改用 GaussianDenoise，并设置 noiseThreshold/denoiseKernelSize。
// 光照不均且对比度不足的候选可改用 AdaptiveThreshold，并配置 adaptiveBlockSize/adaptiveConstant。
// 低清晰度候选可改用 UnsharpMask，并设置 sharpnessThreshold/sharpenAmount。
// 小字候选可改用 LocalUpscale，并设置 upscaleFactor/upscaleInterpolation。
```

- `ContrastNormalize`：`gain=min(maximumGain, targetStandardDeviation/实测标准差)`，以实测亮度均值为中心对颜色通道执行线性映射、四舍五入和 0～255 饱和。保留 RGB/BGR 的通道关系，不转换成灰度；BGRA 的 alpha 不变且仍不参与 REC。颜色饱和可能改变色差，输出标准差不保证等于目标。
- `GrayClahe`：先按 OCR 同一整数亮度系数转为灰度，再执行 OpenCV CLAHE。`claheClipLimit` 默认2，`claheGridSize` 默认8表示每轴8块，不是每块8像素。RGB/BGR 模型输入中会复制灰度到三个通道，之后依然使用模型自己的均值、尺度和补边。对于依赖颜色的文本，必须验证后再选择。
- `GaussianDenoise`：当 `Rectified.LaplacianVariance > noiseThreshold` 且中间裁剪像素不超限时，使用 OpenCV `GaussianBlur` 在原通道数上执行一次轻度平滑；默认 `denoiseKernelSize=3`、`denoiseSigma=0`（由 OpenCV 根据核尺寸选择）。低于或缺少该指标时不修改像素并报告 `SufficientQuality`。拉普拉斯方差同时受文字边缘、纹理、采样和分辨率影响，不是经过标定的噪声分类器；默认不启用，建议只作为保留原文的重试候选。
- `AdaptiveThreshold`：低对比度且校正 crop 的宽高都不小于 `adaptiveBlockSize` 时，先按 OCR 亮度系数转灰度，再使用 OpenCV Gaussian adaptive threshold 输出二值图；默认 `adaptiveBlockSize=15`、`adaptiveConstant=5`，奇数邻域范围3～31。灰度结果会按模型通道数复制，颜色和细灰笔画可能丢失；尺寸不足或对比度已足够时报告 `SufficientQuality`/`SufficientContrast`，不修改像素。
- `UnsharpMask`：当校正 crop 的亮度标准差不低于 `minimumContrast` 且 `Rectified.LaplacianVariance < sharpnessThreshold` 时，使用一次 GaussianBlur 和 `AddWeighted` 恢复局部边缘；默认 `sharpnessThreshold=512`、`sharpenKernelSize=3`、`sharpenAmount=0.5`、`sharpenSigma=1`。低于最小变化、缺少指标或已经超过清晰度门限时报告 `SufficientQuality`，不修改像素；该指标不是标定的模糊分类器。
- `LocalUpscale`：使用 `upscaleFactor`（1～4 之间的有界倍数，默认2）和独立的 `upscaleInterpolation`（默认 `Cubic`）将校正后的中间 crop 放大，再进入原有 resize、padding 和归一化。放大后的像素数受 `maximumPixelsPerCrop` 限制，超限报告 `DS-VISUAL-4102`；这是插值候选，不是超分辨率模型，也不保证增加信息量或准确率。`Enhanced.InputSize` 会记录放大后的尺寸，便于与原始 `Rectified` 对照。

执行顺序是 `warp/rotate → Rectified采样 → 质量门限 → 可选增强/Enhanced采样 → resize → Content采样 → 原有padding/normalize`。不重复 DET/CLS、不改变检测区域、识别宽度、坐标或字典。超低变化（纯色等）和已达到阈值的裁剪不增强，也不分配增强像素缓冲。OpenCV 延迟创建线程本地工作区，复用 LUT、Mat 和 CLAHE 对象；不产生每像素托管对象，输入销毁时释放工作区。应用增强多一次 crop 内处理和可选灰度缓冲，并非零开销。

`CropDiagnostics.EnhancementDecision` 区分 `NotConfigured`、`InsufficientVariation`、`SufficientContrast`、`SufficientQuality`、`Applied`；`Enhancement` 保存参数，`Enhanced` 仅在实际应用时保存增强后、resize 前的统计，`AppliedGain` 仅在线性方式应用时非空。每次方向重试各自计算质量门限并保存证据。增强实际应用时张量/文字/置信度/SHA均可能改变，不能继续宣称与原始路径逐值等价。

门限是可配置启发式，不是通用质量判据。默认32/1只是起始参数，白底小文字可能因空白占比高而被判低对比度；噪声也可能被增强。请在自己的标注集上比较逐行准确率、CER/WER和端到端P50/P95，不能只看锐度/置信度上升。最大增益8，CLAHE clip≤16、每轴分块2～16，每裁剪增强像素数默认1048576、最大16777216，超限 `DS-VISUAL-4102`，不静默漏处理。像素循环检查取消；单次 native CLAHE 调用在前后检查取消，不能中断其内部执行。

当前尚无自动噪声/JPEG识别、阴影消除配方或换模型重试；`GaussianDenoise`、`AdaptiveThreshold`、`UnsharpMask` 和 `LocalUpscale` 都是有界、显式的操作，不能冒充噪声/光照/模糊分类器、超分辨率模型或准确率保证。已实现显式低对比度/高频/低清晰度门限路径，以及下面的单候选低置信度增强重试，两者均默认关闭。

功能回归示例：2026-09-17，在 Windows RTX3060 Laptop、ORT1.23.2 CPU/CUDA 下，使用同一 `demo_1.jpg`，v4 mobile/v5 mobile/v6 tiny、B4/单通道、Clamp320，对 Report/ContrastNormalize/GrayClahe 共18组运行成功。Report 的文字和完整合同SHA与关闭增强基线一致；本图各模型均仅第12号区域触发默认门限。线性方式文字不变但置信度/token使合同SHA改变；v5 CLAHE 将 `(成品包材)` 变成 `（成品包材）`，置信度从CPU基线0.848219升到0.9010367，不能据此判断更准确。该轮只有1次预热/3次采样且存在开发负载，不作为稳定性能或准确率结论；复现时请保存 sidecar 和原文差异，而非仅比较置信度。

### 低置信度单候选增强重试

需要“先保留原始识别，再检查局部增强是否值得采用”时，不要在首轮 `CropProcessing` 中设置增强，而应显式启用：

```csharp
var retry = new OcrEnhancementRetryOptions(
    new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe),
    confidenceThreshold: 0.9f,
    selectionPolicy: OcrEnhancementSelectionPolicy.PreserveOriginal,
    minimumConfidenceGain: 0.05f,
    maximumRegionsPerImage: 16,
    maximumCropsPerImage: 128);

TextCropProfile crop = recognitionProfile.CropProfile!
    .WithCropProcessing(new OcrCropProcessingOptions(1024, 1024))
    .WithEnhancementRetry(retry);
// 将 crop 传给 OcrPipeline。通过结果行的 EnhancementRetry 查看对照。
```

`WithEnhancementRetry` 在未配置 `CropProcessing` 时自动启用默认有界裁剪诊断，首轮仍不增强。可以先设置诊断预算，但首轮直接增强与增强重试不能组合，任一配置顺序都明确抛出 `ArgumentException`，避免把已经增强的结果称为“原始结果”。没有调用此方法时，不引入重试或额外诊断。

执行顺序为 `DET → CLS/初次REC → 可选方向重试 → 单次增强候选 → 最终结果`。只考虑当前所选识别结果为空或置信度低于阈值的行；默认阈值0.8，示例0.9是业务配置，不是通用推荐值。再读取该行已有的 `Rectified` 统计，至少一个裁剪满足增强质量门限才生成候选。没有合格裁剪就标记 `NoEligibleCrop`，不再跑REC。对于滑窗行，重试整行窗口，只有质量合格的窗口应用增强；其他窗口保持原像素，以保证完整行合并与接缝上下文。候选从原图重新裁剪，不在上次增强像素上叠加处理，也不重新解码、DET或CLS。

候选保留每个原始窗口的实际 `TensorWidth`，只与相同宽度候选组批，不因单行重试而缩小或扩大原始补边上下文；模型的batch维仍可能变化，minimum-batch仍需补齐。这样避免把宽度改变导致的分数变化误当作增强效果，但不同batch/backend数值仍不保证逐位一致。固定宽度引擎合同不变。

每行最多一个候选，不尝试所有增强组合或不断循环。`PreserveOriginal` 是默认选择策略：实际运行候选并留下结果，但最终文字和原始合同SHA不变。选择 `ConfidenceGain` 后，非空候选仅在严格优于当前非空结果且增量达到 `minimumConfidenceGain` 时替换；平局、增量不足和空候选保留原结果，非空候选可以替换空原文。它只是可选启发式，不能保证准确率，尤其不能保证全角/半角和业务字段符合预期。

结果行 `EnhancementRetry` 为 null 表示未启用或当前识别置信度足够；非空时保存：

| 字段/决策 | 含义 |
|---|---|
| `Options` | 实际门限、操作、选择策略和资源上限 |
| `Original` | 增强前所选结果，位于可选方向重试之后；包括原文、token、置信度、宽度、窗口和裁剪诊断 |
| `Candidate` | 唯一候选的相同字段；null表示未运行额外REC |
| `NoEligibleCrop` | 没有裁剪满足质量门限 |
| `RegionLimit` | 前面的合格行用完接收额度；不丢原文 |
| `PreservedByPolicy` | 已运行候选，按显式策略保留原文 |
| `InsufficientGain` | 候选为空、平局或增量不足 |
| `CandidateSelected` | 按配置启发式选中候选，不是正确性判定 |

若先做方向重试，顶层 `OrientationRetry` 仍保留该阶段的全部尝试，其 `SelectedIndex` 表示增强之前的方向胜者；最终文字是否来自增强，应看 `EnhancementRetry.Decision`。顶层 `CropDiagnostics` 对应最终选中的识别。ROI重新编号同步更新两份识别结果和窗口的SourceRegionIndex，但诊断四角/旋转/评估索引保持原来源；全图方向恢复也不将旧证据冒充恢复后的坐标。

资源上限有两层：`maximumRegionsPerImage` 按阅读顺序接收质量合格的低分行；超出保留原文并标记。`maximumCropsPerImage` 限制增强阶段额外物理行，包含滑窗和模型minimum-batch重复补齐，超限整次调用报 `DS-VISUAL-4102`。此外，初次REC、方向重试、增强重试共享 `CropProcessing.MaximumCropsPerCall`、`MaximumResultBytes`、取消和超时。原始和全部候选诊断/识别trace一并计入保守结果预算，错误不返回部分成功；Batch/Session池复用现有模型并发边界，不创建无上限的任务或新模型会话。

候选规划、采样、增强、REC、合并计入 `Recognition` 墙钟和端到端总时间；各批次准备/推理/后处理累计到详细work计时，`RecognitionBatchCount`包含额外批次。阶段work在并发时可能大于墙钟。启用重试即使选择保留原文，也会产生真实计算成本；不要把它当作免费精度开关。当前只实现单增强候选，不包含动态扩大模型宽度、换模型或多配方候选链，真实准确率仍需标注集验证。

验证示例（2026-09-17）：同一Windows RTX3060 Laptop、ORT1.23.2 CPU/CUDA、demo_1.jpg、B4/单通道、Clamp320、1次预热/3次短测，v4 mobile/v5 mobile/v6 tiny的关闭/保留/显式选择共18组通过，前两种模式的完整合同SHA均与既有基线一致。置信度阈值0.9时，v5的第6号区域因质量门限不合格不重试，第12号区域生成一个候选；保持原始TensorWidth=282时，该行CPU置信度0.848219→0.9010367，显式选择把半角括号改为全角，默认保留策略不改原文。v4/v6本图没有低分行触发。另以v6 tiny、SlidingWindow320、方向/增强阈值1、最多接收1行、增强质量阈值128作组合边界测试，两后端均保留2窗口候选及原始方向证据；这些强制参数是测试用例，不是生产推荐值。没有标注真值，不据此声称准确率提高。

### 动态宽度低置信度重试（B3b-width）

长文本在首轮 `MaximumWidth` 限制下可能被压缩。对于声明了动态宽度输入的识别器，可以在首轮结果为空或置信度低于阈值时，**有界地再跑一次更宽的 REC 候选**：

```csharp
var widthRetry = new OcrWidthRetryOptions(
    candidateMaximumWidth: 1280,
    confidenceThreshold: 0.8f,
    selectionPolicy: OcrWidthRetrySelectionPolicy.PreserveOriginal,
    minimumConfidenceGain: 0.05f,
    maximumRegionsPerImage: 8,
    maximumCropsPerImage: 64);

TextCropProfile crop = recognitionProfile.CropProfile!
    .WithWidthRetry(widthRetry);
// 识别器的输入宽度必须是动态 shape；将 crop 传给 OcrPipeline。
```

`WithWidthRetry` 只接受 `OcrRecognitionWidthMode.Dynamic`，并要求候选上限严格大于首轮 `MaximumWidth`。Pipeline 会复用现有的宽度分组、minimum-batch 补齐、Session 池和取消/结果预算；不会创建新的模型会话，也不会修改 DET、CLS 或首轮裁剪。滑窗区域会按候选宽度重新规划窗口，窗口数和 batch padding 都计入 `MaximumCropsPerImage`。固定宽度模型、固定 TensorRT engine 或超出模型 Profile 的候选会在输入合同阶段以 `DS-VISUAL-4103`/`ProfileInvalid` 拒绝，不能通过配置掩盖 engine shape 限制。

候选默认只做证据收集，不改变业务结果：`PreserveOriginal` 会保留首轮文本，同时在 `OcrRegionResult.WidthRetry` 中保存候选。若明确选择 `ConfidenceGain`，只有候选非空、置信度严格更高且增益达到 `MinimumConfidenceGain` 时才替换；这仍是启发式选择，不是准确率判定。`WidthRetry.Original`、`Candidate` 都包含文本、CTC trace、实际 `TensorWidth`、窗口和裁剪诊断，但不持有图像或 GPU 缓冲。`Candidate=null` 且决策为 `RegionLimit` 表示因行数预算未执行候选，不表示候选失败。

重试最多发生一次；现已可将 `LocalUpscale` 作为该单候选增强策略，但仍不包含换模型、多配方候选链或自动准确率评估，这些属于后续 B3b 工作。启用后即使最终保留原文也会增加裁剪、REC 和合并耗时，必须在带标注的业务集上比较 CER/WER、召回率及端到端 P50/P95，不能仅根据置信度上涨宣称优化有效。当前单元测试覆盖候选选择、动态/固定 shape 合同、行数与物理 crop 限制，以及 OpenCV 局部放大尺寸和全流程候选证据；真实模型和各后端的收益矩阵仍需单独测量。

## 字符集覆盖审计

在创建推理会话前，先检查模型字典能否表示业务所需的字符。`OcrCharacterSetAuditor` 位于 `JYPPX.DeploySharp.Visual`，可复用同一个只读索引检查多个业务字符范围，无需图片或推理后端。以下接口以当前源码为准。

```csharp
OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(
    dictionaryPath, "my-ocr/dictionary", "1", useSpaceCharacter: true);
var auditor = new OcrCharacterSetAuditor(characters);
OcrCharacterCoverageReport coverage = auditor.Audit("订单编号ABCabc0123456789-_/￥€㎡");
foreach (OcrMissingCharacter missing in coverage.MissingCharacters)
    Console.WriteLine($"{missing.CodePoint} '{missing.Text}', count={missing.Occurrences}, " +
        $"firstUtf16Offset={missing.FirstUtf16Offset}, inCompound={missing.AppearsInCompoundToken}");

// 业务要求每个字符均可独立输出时，在创建 Pipeline 前执行。
coverage.EnsureCovered();
// 把同一个 characters 传给 PaddleOcrProfiles.CreateRecognition(...)。
```

### 覆盖的含义

审计按 **Unicode 标量**逐个匹配独立字典 token。`😀` 或扩展区汉字各算一个标量；报告偏移使用 .NET 字符串的 UTF-16 索引。重复需求按标量去重后计算覆盖率，同时保留出现次数。缺失列表按 Unicode 值排序，便于稳定比较。

- 字典只有 `"AB"` 时，`A`、`B` 标记为缺少独立 token，`AppearsInCompoundToken=true`；完整词条仍可作为模型输出。这个接口不做词条分词，也不判定一个完整短语能否由多个词条组合出来。
- `A`、`a`、`Ａ` 分开检查，`é` 与 `e` 加组合重音也分开检查。空格、全角空格、换行和制表符均保留；不会自动 trim、转大小写、NFC/NFKC 或全半角替换。
- `Coverage` 是不同需求标量的覆盖比例。空需求的 `Coverage=null`、`IsCovered=false`；`EnsureCovered()` 对空需求或缺失字符抛出 `DS-VISUAL-4105`。
- 字典覆盖只是必要条件，不是实际识别准确率。模型缺少某字符时可能输出另一个已知字符，不能依靠预测结果反推完整字典覆盖。

常用汉字应从业务文本、人工标注或业务自选字表导入。仓库示例中的 47 个中文业务字符是演示样本，不代表覆盖全部常用汉字。

### 字典索引、重复项和身份

`PaddleOcrProfiles.LoadCharacterSet` 接受严格 UTF-8，可带文件头 BOM；首个 BOM 不计入第一项，非法 UTF-8 或内部空行明确失败。它保留重复词条、原始顺序和词条空白，不能通过去重或给重复项加后缀“修复”字典，否则模型类别映射会发生变化。`useSpaceCharacter` 必须与模型导出一致；启用时追加一个空格类别，即使文件已含空格也不自行去重。

`DuplicateTokens` 列出重复项的原始文本、首次索引及重复索引。索引从 0 开始，属于字典，不包含 CTC blank/unknown 保留类别。两个类别可合法映射为同一文字；CTC 仍按类别索引折叠相邻重复。

报告同时保留现有 `CharacterSetId/Version/Sha256`，并增加 `TokenMappingSha256`。新指纹区分词条边界、顺序和重复次数，例如 `["AB","C"]` 与 `["A","BC"]` 会得到不同指纹；身份名称不参与新指纹。其序列格式为 .NET `BinaryWriter` 的 UTF-8 字符串前缀 `DeploySharp.OcrTokenMapping/v1`、小端 Int32 词条数、逐项长度前缀 UTF-8 字符串。原文件 SHA 单独记录，三种哈希用途不同。

`GreedyCtcDecoder.ExpectedClassCount` 继续严格校验模型输出类别数。显式 unknown 类别的 `Throw/Skip/Replace` 仍按配置执行，trace 的 `IsUnknown` 可查；unknown 替换文字不会因此成为字典支持的独立字符。审计不会改写 CTC 输出或原 OCR 结果哈希。

### 资源边界和运行工具

审计默认最多索引 4,194,304 个字典 UTF-16 单元，每次需求最多 65,536 个 UTF-16 单元，超限抛出 `ArgumentOutOfRangeException`，无截断或部分成功。两个限制可在审计器构造时配置；取消抛出 `OperationCanceledException`。索引只保留字符和字典信息，可并发调用 `Audit`，不接触图片、张量或 GPU 缓冲。把审计放在启动阶段，避免逐帧重复创建索引。

现有 [PaddleOCR Benchmark](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/tools/DeploySharp.PaddleOcrBenchmark) 提供 `--audit-characters` 命令，以及推理前的 `Report/Require` 模式。需求 JSON 将分组名映射为所需原始字符，覆盖汉字样本、大小写、数字、标点、全半角、单位和业务编号；缺失信息与字典文件 SHA 全部导出。完整运行时，审计在加载图片和创建后端会话之前完成，不计入推理时间。

### 本机字典样例（2026-09-17）

使用工具的 `character-requirements.example.json` 对本机五份字典检查，均按模型约定追加空格。表中只列该示例涉及的缺失项，不表示完整字符覆盖矩阵：

| 本机字典 | token 数（含追加空格） | 重复项数 | 示例中缺少的独立字符 |
| --- | ---: | ---: | --- |
| v4 | 6624 | 28 | `＠` U+FF20、`¢` U+00A2、`£` U+00A3、`¥` U+00A5、`€` U+20AC |
| v5 | 18384 | 0 | `‘` U+2018、`‰` U+2030、`㎏` U+338F、`㎡` U+33A1 |
| v6 tiny | 6905 | 0 | `㎏` U+338F、`㎡` U+33A1 |
| v6 small | 18709 | 0 | `㎏` U+338F、`㎡` U+33A1 |
| v6 medium | 18709 | 0 | `㎏` U+338F、`㎡` U+33A1 |

本次使用的 v4 文件含 28 个重复项，工具已统一使用主库加载器保留原索引和文字。示例里的中文样本、ASCII 大小写、数字和 URL/邮箱/电话/编号字符在五份字典中均有独立 token。`¥` 与 `￥`、`㎏` 与两个字符 `kg` 是不同需求，报告不会擅自替换。

## 文本规范化（可选、保留原文）

OCR 解码器只负责还原模型输出，不应在 CTC 阶段偷偷改变字符。业务需要统一 Unicode、全角/半角、空白或领域别名时，可以在 OCR 完成后显式调用 `OcrTextNormalizer`。该层不修改 `OcrResult`、`OcrRegionResult` 或 `RecognizedText`，因此原始文本、token trace、置信度和既有 `OcrResult.ComputeSha256()` 始终可回看。

```csharp
var normalization = new OcrTextNormalizationOptions(
    unicodeForm: OcrUnicodeNormalizationForm.Nfc,
    convertFullWidthAscii: true,
    normalizeLineEndings: true,
    collapseWhitespace: true,
    trimWhitespace: true,
    replacements: new[]
    {
        // id 必须稳定且唯一；替换按声明顺序、Ordinal 精确匹配。
        new OcrTextReplacementRule("invoice-prefix", "发票号：", "发票号码:")
    },
    maximumLength: 512,
    overflowMode: OcrTextNormalizationOverflowMode.Reject);

OcrNormalizedResult view = OcrTextNormalizer.Normalize(result, normalization, cancellationToken);
foreach (OcrNormalizedRegionResult line in view.Regions)
{
    Console.WriteLine($"raw={line.Text.RawText}; normalized={line.Text.NormalizedText}; " +
        $"rules={string.Join(",", line.Text.AppliedRules)}");
}
```

### 执行顺序和规则

每一行按固定顺序执行：Unicode NFC/NFKC → 全角 ASCII 与 U+3000 转换 → CRLF/CR 转 LF → 控制字符清理 → 空白折叠 → 首尾空白裁剪 → 自定义精确替换 → Unicode 标量长度限制。只有实际改变文本的规则才出现在 `AppliedRules`；配置中未命中的替换仍包含在 `ConfigurationSha256`，避免“看起来相同但配置不同”的结果无法复现。

- `Nfc` 只做规范组合；`Nfkc` 还会执行 Unicode 兼容折叠，可能改变单位、圈号或格式字符，需先在业务样本上确认。
- `convertFullWidthAscii` 将 U+FF01～U+FF5E 和全角空格 U+3000 映射为半角；不会转换大小写、中文标点或货币符号。
- `collapseWhitespace` 将连续 Unicode 空白（包括换行、制表符）压成一个 ASCII 空格；`trimWhitespace` 单独控制首尾裁剪。需要保留段落时不要启用折叠。
- `removeControlCharacters` 移除控制字符，但保留 `CR`、`LF` 和制表符，便于后续选择是否折叠。
- 自定义规则不是正则表达式，不会执行代码；最多 64 条，按 Ordinal 精确替换。若规则互相重叠，应显式排列顺序并在结果中检查应用记录。
- `maximumLength` 按 Unicode 标量计数，零表示不限制；`Reject` 抛出 `DS-VISUAL-4108`，`Truncate` 在标量边界截断并记录 `length:truncate`。默认不设长度上限，不会静默截断。

`OcrTextNormalizationResult` 同时保存 `RawText`、`NormalizedText`、原始/规范化标量长度、`SourceRegionIndex`、应用规则、配置 SHA 和结果 SHA。`OcrNormalizedResult.Source` 指向原始不可变结果，规范化视图的 hash 额外包含源结果 hash 和配置 hash；它不是新的检测或识别结果，不应把规范化后的字符串回写到原 token trace 中。空文本也会经过相同规则和长度策略，取消令牌在每个阶段检查。

规范化不是准确率或字段合法性判断：全角转半角可能不适合中文地址，NFKC 可能合并业务上有意义的符号，自定义替换也可能掩盖模型错误。日期、金额、电话、URL、发票号等格式校验应放在独立字段验证器中；先保留原文和规范化视图，再由业务决定采用哪一个。默认不调用 `OcrTextNormalizer` 时，现有 OCR 行为和结果指纹完全不变。

## 结构化字段校验（独立于 OCR）

字段校验不嵌入检测、CTC 解码或模型字典。先选择业务需要的规范化视图，再把同一个 `OcrTextNormalizationResult` 交给一个或多个 `IOcrFieldValidator`；校验器只读文本，不回写 `OcrResult`。

```csharp
OcrNormalizedRegionResult line = view.Regions[0];
IOcrFieldValidator invoice = new InvoiceNumberOcrFieldValidator();
OcrFieldValidationResult check = invoice.Validate(line.Text, cancellationToken);

if (!check.IsValid)
    Console.WriteLine($"region={check.SourceRegionIndex}, code={check.Code}, reason={check.Message}");
else
    Console.WriteLine($"invoice={check.CanonicalValue}");

// 需要同时检查多个候选字段时，结果顺序与声明顺序一致，并且每个结果都保留原文。
OcrFieldValidationReport report = OcrFieldValidators.ValidateAll(
    line.Text,
    new IOcrFieldValidator[] { new DateOcrFieldValidator(), new AmountOcrFieldValidator() },
    cancellationToken);
```

当前内置校验器及其边界如下：

| 类型 | 默认规则 | 规范值/限制 |
| --- | --- | --- |
| `Date` | `yyyy-MM-dd`、斜线/点分隔、中文年月日和 `yyyyMMdd` | 输出 `yyyy-MM-dd`；只校验日期存在性 |
| `Amount` | 可带 `¥ ￥ $ € £`、千分位、正负号，最多两位小数 | 输出不带货币符号的 InvariantCulture 十进制；不解析中文大写金额 |
| `Phone` | 中国大陆手机、区号座机和可选 `+86`/空格/括号 | 保留可识别格式；不查询运营商或号码归属 |
| `ChineseIdCard` | 18 位或旧 15 位身份证，生日和 MOD-11 校验码 | 输出 18 位大写 `X`；不验证行政区划真实性 |
| `Url` | 绝对 `http`/`https`，非空主机，最长 2048 | 输出 `Uri.AbsoluteUri`；不发起网络请求 |
| `Email` | 常规 `local@host.tld` 形状，最长 254 且无连续点 | 保留原大小写；不做 DNS/SMTP 验证 |
| `Identifier` | 字母/数字及 `._-/`，可配置标量长度和 Unicode 字母 | 默认 ASCII 工业编号；必须含字母或数字 |
| `Address` | 有界长度、至少含字母/数字/CJK、无控制字符 | 仅启发式，不验证地址是否真实存在 |
| `InvoiceNumber` | 8 或 20 位 ASCII 字母数字 | 输出大写；不包含发票真伪或校验码验证 |
| `DeviceSerialNumber` | `Identifier` 规则，默认至少 4 个标量 | 可自定义最大长度和 Unicode 字母 |

每个 `OcrFieldValidationResult` 都提供 `Status`（`Valid`/`Invalid`/`Empty`）、稳定 `Code`、消息、可选 `CanonicalValue`、`RawText`、`NormalizedText`、`SourceRegionIndex`、规范化结果哈希和校验结果哈希。`OcrFieldValidators.CreateDefault()` 返回 10 个独立校验器；应用也可以继承 `OcrFieldValidatorBase` 或直接实现 `IOcrFieldValidator`，为设备号、订单号等项目定义自己的规则。校验器集合最多 64 个且 ID 不可重复，调用共享取消令牌。

格式通过不等于业务事实成立：OCR 误识别后的字符串可能恰好满足日期、身份证或金额格式，规范值也不应覆盖原文。生产流程应同时保存 OCR 原始/规范化文本、字段校验 `Code` 和人工或数据库复核结果；需要跨行一致性、金额合计、地址库或发票平台验证时，在应用层组合这些结果。

## 跨字段一致性（独立于 OCR）

字段格式通过后，票据和设备表单通常还需要检查日期先后、明细金额合计或两个字段是否相同。`OcrFieldConsistencyContext` 将应用字段名绑定到已有的 `OcrFieldValidationResult`，规则只读取规范值，不回写 `OcrResult`，也不会把业务不一致误报为模型推理失败：

```csharp
OcrFieldConsistencyContext fields = new OcrFieldConsistencyContext(new[]
{
    new OcrFieldBinding("start", startValidation),
    new OcrFieldBinding("end", endValidation),
    new OcrFieldBinding("line_1", line1Validation),
    new OcrFieldBinding("line_2", line2Validation),
    new OcrFieldBinding("total", totalValidation)
});

OcrFieldConsistencyReport report = OcrFieldConsistency.EvaluateAll(fields, new IOcrFieldConsistencyRule[]
{
    new DateOrderOcrFieldConsistencyRule("start", "end"),
    new AmountTotalOcrFieldConsistencyRule(new[] { "line_1", "line_2" }, "total", tolerance: .01m)
});

if (!report.IsConsistent)
    foreach (OcrFieldConsistencyResult item in report.Results)
        Console.WriteLine(item.Code + ": " + item.Message);
```

内置规则包括 `EqualOcrFieldConsistencyRule`、`DateOrderOcrFieldConsistencyRule` 和有界的 `AmountTotalOcrFieldConsistencyRule`。结果会保留规则 ID、字段顺序、观察到的规范值、上下文哈希和结果哈希；`Consistent` 表示关系成立，`Inconsistent` 表示值存在但关系不成立，`Missing` 表示字段没有绑定，`InvalidDependency` 表示依赖字段未通过格式校验。字段和规则 ID 均会规范化且分别限制为 128 个字段、64 个字段引用和 64 条规则，规则集合中的 ID 必须唯一。

这些规则只验证文本之间的可计算关系，不查询数据库、不验证发票真伪、不判断地址或设备归属，也不替代金额税率、币种、跨页合计和人工审核。需要更多业务关系时，继承 `OcrFieldConsistencyRuleBase`，在自定义规则中返回稳定原因码并保留同一上下文即可。

## 行、段落与多栏布局视图

检测器返回的是带 polygon 的文本行；当页面还需要段落或多栏阅读顺序时，可以在 OCR 完成后使用 `OcrTextLayoutBuilder`。它只读取源图边界和文本，不重新裁剪、不重新推理，也不修改 `OcrResult`。如果已经创建 C1 规范化视图，可将它一并传入，版面结果会使用规范化字符串，同时保留 `OcrTextLayoutResult.Source` 供追溯。

```csharp
OcrTextLayoutOptions layoutOptions = new OcrTextLayoutOptions(
    readingOrder: TextReadingOrder.LeftToRightThenTopToBottom,
    lineCenterToleranceRatio: .5f,
    paragraphGapRatio: 1.5f,
    columnGapRatio: 2.5f,
    regionSeparator: " ");

OcrTextLayoutResult layout = OcrTextLayoutBuilder.Build(
    result,
    normalized,
    layoutOptions,
    cancellationToken);

foreach (OcrTextColumn column in layout.Columns)
    foreach (OcrTextParagraph paragraph in column.Paragraphs)
        Console.WriteLine(paragraph.Text);
```

布局器先用文本 polygon 的轴对齐边界按垂直重叠/中心距离聚类成行，再用横向重叠和间隙聚类成栏，最后在每栏内按垂直间距聚类成段落。`LineCenterToleranceRatio`、`MinimumVerticalOverlapRatio`、`MaximumInlineGapRatio`、`ParagraphGapRatio` 和 `ColumnGapRatio` 都是显式有界参数；默认值适合规则文档起步，不会自动学习页面版式。行内区域按 X 排序并插入 `RegionSeparator`，段落行和段落之间分别使用 `ParagraphLineSeparator`/`ParagraphSeparator`。

`Lines` 和 `Paragraphs` 按 `TextReadingOrder` 返回，`Columns` 始终按从左到右保存，且每个行/段落/栏都带源坐标 `Bounds`。默认的 `TopToBottomThenLeftToRight` 适合单栏或跨栏逐行读取；双栏文档通常应选择 `LeftToRightThenTopToBottom`，并在自己的页面样本上确认栏间隙。跨栏标题、表格、竖排文本、复杂阅读顺序和真正的版面语义仍需要专用 layout/table 模型，不能仅靠几何阈值推断。

所有集合和拼接文本都受 `MaximumLines`、`MaximumParagraphs`、`MaximumColumns` 限制，超限返回 `DS-VISUAL-4102`；输入为空时返回空视图，取消令牌在分组循环中生效。`OcrTextLayoutResult.ComputeSha256()` 包含源 OCR hash、布局配置 hash、文本和来源索引，规范化视图的配置也会纳入；它是布局证据，不是新的模型推理指纹。不要把按几何启发式拼出的段落当作字段、表格或语义实体。

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
| `WidthClamped` | 单窗口自然宽度超过受限宽度时为 true；滑窗合并结果为 false，逐窗口都通过宽度检查 |
| `BatchPadded` | `TensorWidth > TargetWidth`，仅表示 Batch 补齐 |
| `WindowCount` | 普通行是 1；大于 1 时，`TargetWidth`/`TensorWidth` 分别报告所有窗口中的最大值，完整信息在 `RecognitionWindows` |

这些值由几何合同计算；OpenCV 透视中间图的边长会先取整，因此不是逐像素测得的 crop 栅格宽度。90°/270°方向先交换长宽，再做计算。宽度对齐产生的额外 padding 被上限裁掉时，只要原始内容仍可放下，就不会误报压缩。

- **Clamp（兼容默认）**：超长行仍缩放到上限，结果的 `RecognitionWidth.WidthClamped=true`。它不是切分或无损长文本识别。
- **Reject（显式启用）**：在识别 crop 分配和 REC 调用之前抛出 `OcrPipelineException`，错误码 `DS-VISUAL-4103`，阶段 `CropAndBatch`；包含 crop Profile、原 `RegionIndex` 和自然/受限宽度。DET 和可选 CLS 此时可能已经执行。一个区域超宽会使本次完整 OCR 调用失败，不静默丢掉该行；输入释放与取消语义不变。
- **SlidingWindow（显式启用）**：在校正并定向后的文字行中规划重叠窗口，逐窗口执行有界识别，再合并回原检测区域。不会增加 `OcrResult.Regions` 中的行数。
- **Split**：基于文字间隙的切分尚未提供，不含占位枚举值。

`crop.DescribeWidth(quadrilateral, orientation)` 只返回诊断，即使是 Reject 也不抛超宽异常，便于应用先规划；`CalculateWidth`、`TextCropRequest` 和实际 Pipeline 会执行 Reject。固定宽度模型的有效限制是 `FixedWidth`，不能通过更大的 `MaximumWidth` 绕过。

既有构造函数和默认策略保留；手工使用旧 `OcrRegionResult(region, recognition)` 构造结果时，`RecognitionWidth=null` 表示诊断未知，不代表没有压缩。方向恢复及 ROI 投影/合并保留宽度来源。既有 `OcrResult.ComputeSha256()` 继续针对识别内容与几何，不加入这些诊断，以便与历史结果对照。

基准工具可设置 `DEPLOYSHARP_PADDLEOCR_OVERFLOW_MODE=Clamp/Reject/SlidingWindow`、`DEPLOYSHARP_PADDLEOCR_MAXIMUM_WIDTH`、`DEPLOYSHARP_PADDLEOCR_CROP_TRANSFORM=Perspective/AffineWhenEquivalent` 和 `DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR`。报告在计时外导出输入/模型/程序集 SHA、每行文字、polygon、字典 SHA、宽度和窗口来源信息；完整操作见[基准工具说明](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/tools/DeploySharp.PaddleOcrBenchmark)。

## 超长行滑窗识别

优先在模型支持范围内选择合适的动态宽度。确实超过上限的 URL、订单号或长句可以启用滑窗。滑窗会增加 REC 工作量，适合解决宽度约束，不能作为普通行必然提速的开关。

```csharp
TextCropProfile crop = recognitionProfile.CropProfile!.WithRecognitionWindows(
    new OcrRecognitionWindowOptions(
        overlapRatio: 0.2,
        maximumWindowsPerRegion: 32,
        maximumWindowsPerImage: 1024,
        maximumMergedCharacters: 16384,
        maximumOverlapTokens: 64,
        minimumOverlapTokens: 2,
        maximumMergedTimesteps: 65536));

// 仍通过现有 OcrPipeline 构造函数传入 crop。
// REC Batch 和独立 Session 数量继续由既有参数控制。
foreach (OcrRegionResult line in result.Regions)
{
    bool needsReview = line.RecognitionWindows.Any(window => window.SeamUncertain);
    Console.WriteLine($"{line.Region.SourceIndex}: {line.Recognition.Text}, review={needsReview}");
    foreach (OcrRecognitionWindowResult window in line.RecognitionWindows)
        Console.WriteLine($"  window={window.Index}, range={window.Start:F3}..{window.End:F3}, " +
            $"raw={window.Recognition.Text}, removed={window.RemovedPrefixTokens}");
}
```

### 裁剪、Batch 和坐标

`OcrRecognitionWindowPlanner.Plan(region, crop)` 可在创建任何图像或张量之前预览窗口。`Start`/`End` 是**定向后校正行的归一化水平区间**，不是原图 x 像素。规划器用单应变换把窗口角点映回源图，并将 90°/180°/270°校正编码到角点角色中，避免后续重复旋转。透视明显的区域会根据局部宽度缩小窗口；无法在几何和数量限制内覆盖时明确失败。

每个窗口使用原 `SourceIndex`，经过既有按宽度分组、Batch padding 和独立 Session 调度。原始 DET polygon、方向元数据和阅读顺序保留在最终行中。`RecognitionWindows` 只保留区间、逐窗口宽度、原始识别结果及接缝决策，不持有图像或张量。普通行的窗口列表为空，仍执行单次识别。

直接用超宽区域构造 `TextCropRequest` 不会自动生成多个请求：在 SlidingWindow 模式下会抛出 `DS-VISUAL-4103`；应使用规划器或 `OcrPipeline`。`DescribeWidth` 始终只规划；固定模型仍以 `FixedWidth` 为上限，不能绕过 engine shape 限制。

### 接缝匹配及不确定结果

`OcrRecognitionWindowMerger` 仅合并相邻窗口的完全匹配 token 后缀/前缀，并检查近似 CTC 位置是否位于共享几何范围。映射会扣除输入填充，并容纳半个时间步的离散定位误差。token 可能是一个中文字符、emoji，或包含多个 Unicode 标量的字典项；比较和删除都以完整 token 为单位。

默认至少要求 2 个匹配 token。`minimumOverlapTokens: 1` 可匹配只包含一个 token 的短接缝，但重复编号场景更需要验证；增大 `overlapRatio`（最多 0.5）可提供更多上下文，同时增加推理量。CTC 时间位置并不是精确字符框，匹配属于保守启发式，必须结合应用样本评估。

无法匹配时保留两侧文字，并设置 `SeamUncertain=true`，因此**最终文本可能保留重复内容**。应用可以据此人工复核、增加兼容宽度或采用另一组窗口参数重试；当前 Pipeline 不自动选择重试结果。原始窗口文字与 CTC trace 一直可查。被匹配移除的前缀在合并 trace 中改为不发射，局部原始 trace 保持不变；合并 trace 的时间步是窗口序列串接索引，不是原模型一次推理的时间轴。

### 资源与失败行为

窗口数、UTF-16 字符数、原始 CTC 时间步和 `OcrPipelineOptions.MaximumResultBytes` 都有边界。窗口总数超限在 REC 分配前失败；结果超限不会返回部分文本，错误码为 `DS-VISUAL-4102`（`OcrLimitExceeded`）。识别时对已保留结果预算进行检查，合并前再预算原始 trace 与合并 trace；最小 Batch 的补齐行按保守预算计入。结果字节数是近似托管开销，不等于整个进程的内存硬限额。

所有窗口使用同一次调用的取消和超时 token。一个窗口失败使该次调用失败，已创建的准备输入由调度器释放。Windows/OpenCV 的四方向真实像素裁剪已加入单元回归；TensorRT 和其他后端仍需各自验证模型 shape、CTC 合同及窗口后的吞吐，不能从 CPU/CUDA 的通过结果推断全部后端完成验收。

## 几何质量、倾斜角度与边界风险

`TextCropProfile.WithGeometryValidation(...)` 在 DET 返回后、CLS 和 REC 裁剪前，对**原检测区域**执行一次有界几何检查。滑窗子区域不重复计算这些行级诊断。既有构造函数默认 `Disabled`，不会增加逐行诊断计算，也不改变既有透视采样、CTC 和阅读顺序。

```csharp
// 先观察业务图片的风险分布，再设置拒绝阈值。
var crop = cropProfile.WithGeometryValidation(new OcrGeometryOptions(
    mode: OcrGeometryValidationMode.Report,
    minimumArea: 4,
    minimumEdgeLength: 1,
    maximumAspectRatio: 200,
    maximumOutsideFraction: 0.25,
    edgeMargin: 1,
    maximumPerspectiveCondition: 10000));

// 把 crop 传给 OcrPipeline 的识别裁剪参数。
// 已有区域也可单独分析，不必加载模型或图片：
OcrGeometryDiagnostics geometry = OcrGeometryAnalyzer.Analyze(
    textRegion, new VisualSize(imageWidth, imageHeight));
Console.WriteLine($"risks={geometry.Risks}; outside={geometry.OutsideFraction:P1}");
Console.WriteLine($"angle={geometry.BaselineAngleRadians}; condition={geometry.PerspectiveCondition}");
```

### 模式与错误处理

| 模式 | 行为 |
| --- | --- |
| `Disabled` | 兼容默认，结果 `Geometry=null`，不执行流水线几何检查 |
| `Report` | 保留全部行并提供诊断；不阻止原有 crop/模型合同检查 |
| `Reject` | 任一区域的风险命中 `RejectedRisks` 时，整次调用报 `DS-VISUAL-4104`，阶段 `CropAndBatch`，带原 `RegionIndex` 和 crop Profile |

Reject 不静默删行、不自动回退到另一组阈值。默认拒绝集合包括过小面积、过短边、极端宽高比、图外面积过大、病态透视和缺失角点；**不包含 `EdgeContact`**，因为贴边文字不一定被截断。可以只拒绝业务确定不接受的情况：

```csharp
var strictCrop = cropProfile.WithGeometryValidation(new OcrGeometryOptions(
    OcrGeometryValidationMode.Reject,
    maximumOutsideFraction: 0.1,
    rejectedRisks: OcrGeometryRisk.OutsideImage
        | OcrGeometryRisk.IllConditionedPerspective));
```

阈值均为启发式验收配置，不是所有模型通用的准确率标准。面积以输入像素平方计，边长/边界距离以像素计；输入缩放后应重新评估阈值。NaN、Infinity、负距离、不合法模式和风险位在配置时拒绝。

`OcrGeometryAnalyzer.Analyze` 是独立的纯分析接口：无论 options 的 Mode 如何都返回诊断，不执行 Reject；需要自行判断 `Risks & options.RejectedRisks`。`TextCropRequest` 没有源图尺寸，不能单独实施图外面积检查；直接调用适配器时，应先调用 Analyzer。`Report` 也不能让缺失明确角点的区域自动变成可裁剪区域。

### 诊断字段和坐标空间

| 字段 | 精确定义及用途 |
| --- | --- |
| `InputPolygon` / `InputSize` | 评估时 OCR 输入空间中的多边形/图像尺寸；不可变，后续 ROI/全图方向恢复不改写这份证据 |
| `Area` / `MinimumEdgeLength` | 凸多边形面积和最短边；不是轴对齐包围框面积 |
| `AspectRatio` | 四边形两组对边最大长度的长短比，与 90°旋转无关；缺失明确角点时为 null |
| `OutsideFraction` | 多边形与 `[0,width] × [0,height]` 的精确凸裁剪计算出的图外面积比例；不是包围框越界比例 |
| `BaselineAngleRadians` | 按显式 TL→TR、BL→BR 角色求和得到的校正前基线角度，图像顺时针为正；不是 CLS 预测值 |
| `RectificationAngleRadians` | 上述基线角度的负值，仅表示去倾斜分量，不是实测残余角度 |
| `PerspectiveCondition` | 输入包围框两轴归一化到单位正方形后，单应矩阵 `||H||F × ||H⁻¹||F`；恒等矩阵为 3，无法求解为 null 并标记病态风险 |
| `ParallelogramError` | `|TL + BR − TR − BL| / 最长边`；零表示平行四边形，不能据此宣称两种采样逐像素等价 |
| `Risks` | 可组合的阈值风险，`None` 只代表本次阈值没有触发，不代表文字一定识别正确 |

条件数通过平移/缩放归一化避免仅因图像分辨率变化就误报；细长文本的风险仍由 `AspectRatio` 和 `MinimumEdgeLength` 独立检查。接近或跨越任意边界都可报告 `EdgeContact`，它只是截断**风险**，无法知道原图外是否真的存在字符。

结果 `OcrRegionResult.Geometry` 保留上述来源。若使用全图方向校正，`InputPolygon` 位于纠正后空间，而最终 `Region.Polygon` 在原图；若经过 ROI 投影，前者仍在该 ROI 的 OCR 输入空间。绘制最终检测框必须使用 `Region.Polygon`。CLS 之后的直角校正仍由 `Region.Orientation` 和方向 metadata 说明，不覆盖校正前基线角度。

### 可选仿射快路径和为什么不能仅按角度切换

上下边几乎水平的梯形仍可能具有明显透视变化。仅按“小于 5°”选择仿射会破坏四角对应关系，因此默认保持**四角透视校正 → 可选直角旋转 → resize/padding**。如明确接受平行四边形近似，可用 `WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent)` 开启 OpenCV 的 `GetAffineTransform/WarpAffine` 路径；策略会同时检查归一化闭合误差和仿射基底条件数。梯形、非平行四边形或病态基底自动回退到透视，绝不只按角度切换。`ParallelogramError` 与 `OcrCropTransformPolicy.GetParallelogramError(...)` 提供可观察依据，但无法证明不同插值实现逐像素相同。OpenCV 批次的 `VisualPreprocessingDescriptor.Notes` 会记录实际的 `cropTransform=Perspective/Affine/Mixed`，便于在启用后确认是否真的命中快路径。

```csharp
var affineWhenSafe = cropProfile.WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent);
```

该模式只改变 OpenCV OCR 适配器的采样算子；ONNX Runtime、OpenVINO 和 TensorRT 仍接收相同的识别张量合同。默认 `Perspective` 可用于跨后端逐像素回归；启用仿射时应固定插值、目标尺寸和输入图像，并对文本结果与 `cropTransform` 诊断分别验收。仿射判断使用平移/缩放不变的闭合误差与基底条件，不会为低角度梯形强行降级。

自交、重复顶点、非有限坐标及不符合声明顺序的几何在 `TextPolygon`/`TextQuadrilateral` 构造时拒绝，不会伪装成可识别区域。检查只读取少量顶点，不复制图片或张量；诊断工作计入 `CropAndBatch`，持有诊断的近似空间计入 `MaximumResultBytes`。超出预算报 `DS-VISUAL-4102`。既有文本/结果 SHA 不加入诊断，允许与关闭检查时对照。

## 低置信度方向重试

透视去倾斜不等于知道文字正反。可通过 `WithOrientationRetry` 对初次识别仍不可靠的行尝试其他直角方向；默认不启用，不额外执行 REC。

```csharp
var crop = cropProfile.WithOrientationRetry(new OcrOrientationRetryOptions(
    confidenceThreshold: 0.8f,
    minimumConfidenceGain: 0.05f,
    maximumRegionsPerImage: 16,
    rotations: new[] { TextOrientation.Degrees180 },
    maximumCropsPerImage: 1024));

// 把 crop 传给 OcrPipeline 后，结果保留原始与候选识别：
foreach (OcrRegionResult row in result.Regions)
{
    if (row.OrientationRetry is not { } retry) continue;
    Console.WriteLine($"selected={retry.SelectedIndex}; skipped={retry.SkippedByRegionLimit}");
    foreach (OcrOrientationAttempt attempt in retry.Attempts)
        Console.WriteLine($"{attempt.Orientation}: {attempt.Recognition.Text} ({attempt.Recognition.Confidence:F3})");
}
```

### 触发、方向和选择规则

- REC 置信度**严格低于** `ConfidenceThreshold`，或文本为空时触发。它不是 CLS 置信度阈值；CLS 的 Fail 策略仍优先执行，不能靠 REC 重试绕过。
- `Rotations` 可显式选择顺时针 90°、180°、逆时针 90°，最多三个、不重复且不含 0°；默认只试 180°。它们相对于**初次经过 CLS/竖排规则之后的裁剪方向**，不是相对于上次候选逐步累加。例如初次为 180°，再试相对 180°时，实际裁剪方向为 0°。
- 初次非空结果仅在候选非空、置信度严格更高且增量达到 `MinimumConfidenceGain` 时被替换；平局或增益不足保持当前结果。初次/当前为空时，非空候选可以替换它；空候选不会替换非空结果。
- 每轮都与当前获选结果比较；一旦获选结果非空且达到阈值，该行不再进入后续轮次。不同候选置信度不可视为经过校准的正确率，增益也可能选择错误文字；应结合业务真值评估。
- `Region.Orientation` 反映最终获选裁剪方向，polygon、SourceIndex 和阅读顺序不变。原 CLS metadata 保留其原始预测，不伪装成重试结果；完整决策见 `OrientationRetry`。

### Batch、滑窗与资源

每轮只重试仍符合条件且已接收的行，不重复 DET、CLS 或图像解码。复用原识别器的独立 Session 池、动态宽度分组、真 Batch、最小批量补齐及输入释放；裁剪仍在工作槽位可用时准备。

`maximumRegionsPerImage` 按原阅读顺序接收，超出的符合条件行保留原结果，并给出 `SkippedByRegionLimit=true`；不要把它当成完成重试。`maximumCropsPerImage` 是**跨所有重试轮次**的额外裁剪硬上限，不含初次 REC，也不含满足模型 minimum batch 的重复行。默认 1024，上限 4096；达到硬限制时抛出 `DS-VISUAL-4102`，不返回部分成功。

在 SlidingWindow 模式下，会按候选方向重新规划有界窗口、执行识别并归并为原行；长行窗口数量也计入重试裁剪上限，并保留逐窗口文本和接缝不确定信息。既有每行窗口限制及每轮图像窗口限制仍生效。90°旋转可能改变自然宽度：在 Reject 模式下，超宽候选报 `DS-VISUAL-4103`，不会静默改成 Clamp；模型/engine 自身的 shape 限制也不放宽。

初次与候选 token、窗口及合并结果共同计入近似 `MaximumResultBytes`，包含 minimum-batch 重复行的保守预算。所有轮次共用一次总超时、取消和 Pipeline 生命周期；后端错误、取消、超时或硬限制失败会使**整个调用失败**，不悄悄吞掉后返回原结果。仅接收行数限制使用明确标记的跳过语义。

### 诊断、计时和复现

`OrientationRetry=null` 表示未开启或没有触发，不表示“尝试过但没改善”。非 null 时，`Attempts[0]` 始终保存初次原文/置信度/宽度/窗口，之后为实际执行的候选，`SelectedIndex` 标明选择；最多保留四次尝试。诊断引用不可变识别结果，不保留图像或 GPU 缓冲。

ROI 投影、合并重新编号、全图方向恢复都保留记录并同步原区域索引；尝试中的方向仍是 OCR 输入空间的裁剪方向。绘制最终框使用最终 `Region.Polygon`。`Geometry` 仍是初次 DET 后的几何证据，不被候选覆盖。

重试的规划、裁剪、REC 和合并墙钟时间计入 `Timing.Recognition`；`Timing.Details` 的识别准备/推理/后处理工作及 batch 数累加所有轮次，DET/CLS 不重复计时。最终结果的 SHA 仍只包含最终选中的文字/几何/方向；诊断本身不加入，所以保留初次结果时可以继续比较历史 SHA。

先测试默认 180°候选及少量低置信度行，再决定是否开启三个候选；不要为了更高置信度无限增加推理。该接口不是任意角度搜索、图像去噪/锐化或语义纠错器。

## 性能测量建议

### 几何检查验证（2026-09-16）

使用同一 `demo_1.jpg`、PP-OCRv4 mobile/v5 mobile/v6 tiny、ORT 1.23.2 CPU/CUDA、Batch=4/独立通道=1、Clamp/320、warmup=1/iterations=3：

- Disabled 和 Report 各 6 组完整调用成功，文本与结果合同 SHA 均与此前 Clamp 基线相同。
- 每组保留原 16 行并输出几何证据；此图默认风险均为 `None`。各版本最大归一化条件数约 3.003、3.025、3.010，同版本 CPU/CUDA 相同。
- 将 `minimumArea` 故意设为 100000000 后，6 组调用均按预期返回 `DS-VISUAL-4104`，并非后端不支持。
- 原生像素测试覆盖 0°、±5°、±15°、±30°、±45°、90°、180°和梯形透视；检查采样坐标、颜色通道、ROI 投影及方向恢复后的诊断来源。

这是正确性和兼容性回归，不是最佳性能或 CER/WER 测试。本轮 Report 的 `crop_ms`（几何检查加分组）约 0.037～0.237 ms，但开发机有其他任务且样本仅 3 次，不能由此做稳定性能结论。真实文字的多角度标注集和其他后端几何模式矩阵仍未完成；仿射快路径的安全策略及 OpenCV 适配器回归已在本轮补齐，仍需在目标设备上独立测量收益。后续方向重试验证见下节。

### 方向重试验证（2026-09-17）

使用同一图片、v4 mobile/v5 mobile/v6 tiny、ORT CPU/CUDA、Batch=4/通道=1、Clamp/320、warmup=1/iterations=3，对照关闭、默认启用、强制触发三组模式，共 18 组完整调用通过。

- 关闭时全部结果合同 SHA 与上一批几何 Report 基线一致。
- 默认阈值 0.8、最小增量 0.05、只试相对 180°：v4/v6 未触发，v5 触发 1 行且保留原结果；同版本 CPU/CUDA 的最终文字及决策一致。
- 将阈值设为 1 以强制试验时，三版本每个后端均对 16 行额外试一次 180°，没有候选被接受。此项证明调度和原始结果保留，不证明准确率改善。
- v6 tiny 的 SlidingWindow/320、最多重试 1 行、阈值 1 组合在 CPU/CUDA 上通过：1 行重试，15 行明确标记被行数限制跳过；重试行包含 2 个窗口，两后端最终文字一致。完整合同 SHA 可因浮点置信度不同而不同，不能据文字一致宣称逐位数值一致。
- 单元及原生测试覆盖改善候选被接受、多轮早停/平局、空文本、相对于 CLS 结果的旋转、旋转后 Reject 宽度限制、并发独立 Session、取消/失败/资源释放、ROI 重新编号、全图方向恢复。

这些是开发机短时行为验证，未形成有人工真值的“倒置文字修复率”、CER/WER 或最优速度结论。重试会额外执行模型；业务中应保留关闭时的基线，并根据触发比例评估收益与耗时。

### 宽度诊断与兼容性验证（2026-09-16）

使用同一 `demo_1.jpg`、v4 mobile/v5 mobile/v6 tiny，在 ONNX Runtime 1.23.2 CPU 与 CUDA 上验证了以下行为。设备为 Windows x64 / RTX3060 Laptop；这不是所有模型/后端的完整验收。

| 检查 | 实测结果 |
| --- | --- |
| 同样的 Clamp/320 设置，改动前后各 6 组 | 文本 SHA 与完整结果合同 SHA 均一致 |
| Clamp/320 的宽度记录 | v4 有 13/16 个区域压缩，v5/v6 各 12/16；两后端一致 |
| Reject/320，各 6 组负向调用 | 都返回 `DS-VISUAL-4103` 和源区域索引，未作为模型“不支持”掩盖 |
| Reject/3200，各 6 组 | 完整流水线通过；压缩区域为 0，同版本 CPU/CUDA 文本 SHA 一致 |

将宽度从 320 改为 3200 后，本图实际规划的最大宽度是 859～973，而不是给每行都分配 3200。文本指纹有变化，但目前没有人工标注真值，不能将变化直接称为准确率提升。warmup=1/iterations=3 的短测用于验证行为，不作为性能结论；性能调优与长文本完整性指标将在后续步骤单独验收。

### 滑窗调用与接缝诊断验证（2026-09-16）

同一 Windows x64 / RTX3060 Laptop 6 GB、驱动 576.02，使用 ORT 1.23.2 CPU/CUDA、OpenCV 5.0、完整流水线 Batch=4、独立通道=1、warmup=1、iterations=3。输入仍为 `demo_1.jpg`，SHA-256 为 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`。先验证 Clamp/320：6 组文本 SHA 和完整结果 SHA 与加入滑窗前相同。

SlidingWindow/320、20% 重叠、最少匹配 2 个 token 时，6 组调用都成功，原检测行仍为 16 个；同一版本 CPU/CUDA 的文本 SHA、切片数和接缝诊断一致。中间 crop 宽度不超过配置上限，几何诊断压缩数为 0。

| 模型 | 切片行数 | 总 REC 窗口（含普通行） | 不确定接缝 / 接缝总数 | CPU / CUDA 完整流水线 P50（ms） |
| --- | --- | --- | --- | --- |
| v4 mobile | 13/16 | 40 | 18/24 | 1005.012 / 531.559 |
| v5 mobile | 12/16 | 37 | 15/21 | 776.414 / 596.205 |
| v6 tiny | 12/16 | 35 | 11/19 | 224.065 / 359.944 |

将重叠调到 35% 后，6 组调用再次成功且同版本 CPU/CUDA 文字一致；v4/v5/v6 tiny 的总窗口变成 45/40/38，不确定接缝分别为 13/12/11。该图仍存在未匹配接缝，不能把诊断减少直接当作准确率提升。重复字、截断字符和相邻窗口识别分歧需要结合人工真值评估。

上表是繁忙开发设备上的短测，含构建与其他负载干扰，**不是最优性能记录**。SlidingWindow 增加了识别工作量；支持更大动态宽度的模型应同时对照 Reject/3200。当前验证覆盖 v4 mobile、v5 mobile、v6 tiny 的 CPU/CUDA；v6 small/medium、TensorRT/OpenVINO/OpenCV DNN 的滑窗矩阵和标注长文本 CER/WER 仍待补齐。

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
