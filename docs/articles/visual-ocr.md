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
