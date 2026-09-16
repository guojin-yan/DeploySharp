# Visual ROI 配置、导入与热更新

本文说明如何使用 `JYPPX.DeploySharp.Visual` 中内置的 JSON 能力保存、校验和热更新 Visual ROI。JSON 序列化与 ROI 合同随 Visual 主包一起发布，不再需要额外安装配置包。

## 安装

```xml
<PackageReference Include="JYPPX.DeploySharp.Visual" Version="2.0.0-alpha.1" />
```

`DeploySharp.Visual` 支持仓库声明的目标框架；内置 JSON 能力在 `net46`/`net461` 之外的目标框架中可用（这两个旧目标没有兼容的 `System.Text.Json` 资产）。JSON 只负责 ROI 快照的版本化序列化、容量限制和原子替换，不负责图像解码、模型推理或后端选择。

## 文件结构

当前 schema 为 `1.0`。一个文件至少包含源图尺寸、文件版本和 ROI 列表：

仓库中的可直接复制示例见 [`docs/examples/visual-roi/line-01.json`](../examples/visual-roi/line-01.json)。该示例同时展示了源像素矩形、Normalized 多边形、Include/CropAndInfer 和 Exclude/FilterResults。

```json
{
  "schemaVersion": "1.0",
  "source": { "width": 4096, "height": 2160 },
  "version": 1,
  "rois": [
    {
      "id": "inspection-zone-a",
      "name": "检测区域 A",
      "enabled": true,
      "priority": 10,
      "coordinateSpace": "SourcePixels",
      "inclusionMode": "Include",
      "executionMode": "CropAndInfer",
      "hitTestMode": "IntersectionOverResult",
      "hitThreshold": 0.5,
      "margin": 8,
      "confidenceOverride": 0.35,
      "taskFilter": ["object-detection"],
      "classFilter": [],
      "tags": ["inspection", "line-01"],
      "metadata": { "camera": "cam-01" },
      "geometry": { "type": "Rectangle", "x": 320, "y": 180, "width": 1800, "height": 1200 }
    }
  ]
}
```

`version` 是文件中的业务元数据；加载成功后，`VisualRoiManager` 会自己分配单调递增的快照版本，不会直接信任外部文件版本。`id` 必须唯一，集合顺序不会改变 ROI 的身份。

## 四种几何

矩形使用 `x`、`y`、`width`、`height`；旋转矩形使用 `centerX`、`centerY`、`width`、`height`、`angleDegrees`；多边形使用由 `{ "x": ..., "y": ... }` 对象组成的有序 `points`；Mask 使用 `width`、`height` 和 Base64 编码的 `valuesBase64`。矩形采用半开区间（右边界和下边界不属于 ROI），多边形边界按包含处理，确保检测框或关键点恰好落在边界上时结果稳定；非有限坐标始终不命中。`SourcePixels` Mask 的尺寸必须与 `source` 完全一致；`TileLocal` Mask 的尺寸必须与整数切片一致，`World` Mask 保留其世界栅格尺寸，并在显式坐标上下文中栅格化到源图。

Normalized ROI 可以把相同配置复用于不同分辨率的相机：几何坐标按源图宽高缩放到像素空间。ModelInput ROI 需要调用方提供真实的 `VisualInputFrame` 和可逆变换，不能在尚未确定预处理方式时仅凭 JSON 猜测源坐标。TileLocal 和 World 坐标需要应用提供额外的投影上下文，当前 JSON 包不会静默转换它们。

## 保存与加载

```csharp
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Configuration.Json;

VisualRoiSnapshot snapshot = manager.Snapshot;
string json = VisualRoiJsonSerializer.Serialize(snapshot);
File.WriteAllText("rois.json", json);

VisualRoiSnapshot loaded = VisualRoiJsonSerializer.Deserialize(File.ReadAllText("rois.json"));
VisualRoiSnapshot installed = VisualRoiJsonSerializer.LoadAndReplace(manager, json);
Console.WriteLine($"snapshot={installed.Version}, rois={installed.Rois.Count}");
```

`LoadAndReplace` 先完整读取、解析、限制检查、几何校验和 ID 唯一性检查，全部成功后才调用 `VisualRoiManager.Replace`。因此 JSON 语法错误、未知字段、非法几何或超限文档都不会替换当前有效快照。

## 稳定 diff

热更新前可以比较当前快照与候选快照。比较按 ROI ID 使用序号排序，忽略两个文件的 `version` 元数据，只报告会改变执行行为的字段：

```csharp
VisualRoiSnapshot current = manager.Snapshot;
VisualRoiSnapshot candidate = VisualRoiJsonSerializer.Deserialize(File.ReadAllText("rois.next.json"));
VisualRoiDiff diff = VisualRoiJsonDiff.Compare(current, candidate);

foreach (VisualRoiChange change in diff.Changes)
{
    Console.WriteLine($"{change.Kind}: {change.RoiId} [{string.Join(", ", change.ChangedPaths)}]");
}

if (diff.HasChanges)
{
    VisualRoiJsonSerializer.LoadAndReplace(manager, VisualRoiJsonSerializer.Serialize(candidate));
}
```

新增和删除项的 `ChangedPaths` 为空；修改项会列出 `name`、`geometry`、`executionMode`、`taskFilter` 等字段。`SourceSizeChanged` 单独表示源图尺寸变化。应用可以在切换前拒绝高风险字段或记录审计日志。

## 生产环境限制

默认限制为 16 MiB 文档、4096 个 ROI、每个多边形 4096 个顶点和每个 ROI 64 个元数据字段。默认 `RejectUnknownProperties = true`，建议生产环境保持默认值，使拼写错误在启动或热更新时立即失败：

```csharp
var options = new VisualRoiJsonOptions(
    maximumDocumentBytes: 4 * 1024 * 1024,
    maximumRois: 256,
    maximumPolygonPoints: 512,
    maximumMetadataEntries: 16,
    rejectUnknownProperties: true);

try
{
    VisualRoiJsonSerializer.LoadAndReplaceFile(manager, "rois.json", options);
}
catch (VisualRoiJsonException error)
{
    Console.Error.WriteLine($"ROI config rejected: {error.Code} at {error.Path}");
    // 保留 manager 当前快照，等待人工修复或回滚文件。
}
```

不要把不可信的大型 Mask 文档直接提交给高并发热更新线程；Mask 本身受 64 million 像素限制，还会占用与源图同量级的托管内存。需要频繁更新时，应在后台线程解析，成功后再用一次 `LoadAndReplace` 原子切换。

## 与推理执行的关系

JSON 只描述 ROI，不决定使用哪个后端。加载快照后可以选择：

- `CropAndInfer`：按 ROI 准备局部输入，再使用 `VisualRoiRunner` 和 Session 池推理；
- `FilterResults`：整图推理一次，再用 `VisualRoiDetectionRunner` 过滤源图 Detection；
- `SlidingWindow`：在 ROI 边界内生成重叠窗口，使用全局 NMS 和来源追溯合并 Detection。

滑窗准备受 `SlidingWindowDetectionOptions.MaximumPreparedPixels` 保护（默认 1 GiB）。运行器会先按所有待执行窗口的源图面积求和，在第一个准备回调启动前拒绝超预算请求；`RunRoisAsync` 还会把多个 ROI 的窗口累计到同一预算中。这个限制只约束源图窗口的预处理工作量，不改变模型输入尺寸；显存或内存较小的设备应按实际峰值把它调低，并结合 `MaximumWindows`、`MaximumConcurrency` 和预取大小一起设置。超预算属于输入错误，调用方可以捕获后降低窗口数量、重叠率或拆分任务。

其他任务可以复用任务无关的 `VisualRoiSlidingWindowRunner<TResult>`：它统一窗口步长、ROI 覆盖率、最大窗口数、准备并发、Session 池调用和 owned input 释放；调用方只需要在 `materialize` 回调中把一个窗口的 `VisualInferenceResult` 转成带 `RoiId`、优先级和可选 `WindowIndex` 的 `RoiProjectedResult<TResult>`，再交给 Detection/OBB/Pose/OCR/实例分割等任务自己的投影器和合并器。运行器不会把分类或稠密结果错误地套用 Detection 的 NMS。

如果希望由运行器完成“解码 → 投影 → 合并”的标准串联，可以使用 `RunRoiAndMergeAsync`：传入 `decode`、`IRoiResultProjector<TResult>` 和 `IRoiResultMerger<TResult>`，它会为每个窗口创建具体 `RoiProjection`，再按指定 `RoiResultMergeMode` 合并。任务专用合并器仍应自行定义 NMS、OKS、文本几何或像素 ownership 规则；通用方法不会猜测任务语义。

对于常见任务，`VisualRoiTaskSlidingWindowExtensions` 提供了更短的强类型入口：Detection、Classification、OBB、Pose、OCR、文本检测、实例分割、语义分割、Anomaly 和 RMBG Alpha 均有对应的 `Run*RoisAndMergeAsync` 方法。入口自动组合模型空间 projector 与任务 merger（Pose 需要显式提供 `PoseTopology`，语义分割需要显式提供背景类别），同时保留 `prepareAsync` 与 `decode`，因此图像解码、模型输出契约和后端选择不会被隐藏；可传入自定义 merger 调整 IoU、拓扑或概率融合策略：

```csharp
var sliding = new VisualRoiSlidingWindowRunner<DetectionResult>(pipeline);
VisualRoiSlidingWindowMergedResult<DetectionResult> detections =
    await sliding.RunDetectionRoisAndMergeAsync(
        manager.Snapshot,
        new SlidingWindowDetectionOptions(new VisualSize(1024, 1024), overlap: .2f),
        prepareAsync,
        inference => inference.GetValue<DetectionResult>(),
        mergeMode: RoiResultMergeMode.ClassAwareNms);

foreach (DetectionResult result in detections.Merged)
    Console.WriteLine($"detections={result.Detections.Count}");
```

这些入口只选择与任务匹配的默认投影和合并策略：不会对 OCR/语义分割套用 Detection NMS，也不会把模型空间结果当成源图坐标；需要处理全局方向校正、非标准输出或自定义去重时，应传入自定义 projector/merger 使用通用 `RunRoisAndMergeAsync`。

`RunTextDetectionRoisAndMergeAsync` 专门处理只输出文本多边形的检测器（例如 PaddleOCR det）。它使用 `ModelSpaceTextDetectionRoiProjector` 将每个窗口的多边形恢复到源图，再由 `TextDetectionRoiResultMergerAdapter` 按多边形 IoU 去重；支持 `KeepAll`、`HighestConfidence`、`RoiPriority` 和无类别 NMS 模式，`WeightedBoxFusion` 与未定义业务规则的 `TaskSpecific` 会显式拒绝。若还需要识别文本和阅读顺序，应使用完整 OCR 入口。

如果自定义模型已经完成整图推理，并且结果项不是 DeploySharp 内置的 Detection/OBB/Pose/OCR 类型，可以使用 `VisualRoiCustomFilter.Filter` 复用同一套 FilterResults 语义，而不需要修改库内部的任务分支。调用方只需提供“一个结果项是否命中一个 ROI”的回调；库会统一解析 SourcePixels/Normalized/TileLocal/World 坐标、执行任务和类别过滤、应用 Include/Exclude，并为每个保留项记录确定性 `RoiIds`：

```csharp
IReadOnlyList<RoiFilteredItem<MyDetection>> kept = VisualRoiCustomFilter.Filter(
    decoded.Items,
    manager.Snapshot,
    VisualTaskId.ObjectDetection,
    (item, _, geometry) => geometry.Contains(item.Center),
    item => item.ClassIndex,
    coordinateContext: coordinateContext);

foreach (RoiFilteredItem<MyDetection> item in kept)
    Console.WriteLine($"{item.Item.Id}: {string.Join(",", item.RoiIds)}");
```

`classIndexSelector` 只有在 ROI 声明了 `classFilter` 时才需要提供；`confidenceSelector` 只有在 ROI 声明了 `confidenceOverride` 时才需要提供。若遗漏，API 会明确抛出输入错误，不会静默忽略类别或置信度条件。内置 Detection、OBB、Pose、OCR 文本区域和实例分割过滤器也会应用 `confidenceOverride`；Anomaly、语义分割和 RMBG 等稠密结果没有单个对象置信度，配置该字段时会明确拒绝。自定义命中回调应使用结果的权威几何（例如旋转框四边形、实例 mask 或关键点），不要用不匹配任务语义的外接矩形替代。该通用过滤器只使用 `ExecutionMode.FilterResults` 的 ROI；CropAndInfer 和 SlidingWindow ROI 必须通过对应运行器执行。

当配置中有多个 `SlidingWindow` ROI 时，使用 `RunRoisAndMergeAsync` 可以在同一调用中规划、执行并合并全部 ROI。该方法会先按快照顺序处理启用的 Include ROI，再把所有窗口的投影结果交给同一个任务合并器；`TileLocal`/`World` ROI 通过最后的 `coordinateContext` 参数显式解析。每个窗口仍保留 `RoiId`、优先级和 `WindowIndex`，因此合并器可以执行跨 ROI 全局 NMS、OKS 或像素 ownership，而不会依赖任务完成顺序。

对于 `CropAndInfer`，`VisualRoiRunner.RunCropAndMergeAsync<TResult>` 提供同样的强类型串联：它先运行有界 ROI 批次，再对每个成功项执行 `decode` 和 `IRoiResultProjector<TResult>`，最后调用 `IRoiResultMerger<TResult>`。返回值同时包含 `Batch`（包括失败项）、投影候选和合并结果，适合把新模型接入到现有 ROI 管线而不丢失错误诊断。结果已经处于源图坐标时可直接使用 `IdentityRoiResultProjector<TResult>`；只需少量业务代码时可使用 `DelegateRoiResultProjector<TResult>` 与 `DelegateRoiResultMerger<TResult>`。`KeepAllRoiResultMerger<TResult>` 只接受 `KeepAll`，防止通用适配器偷偷假设 Detection 的 NMS 语义。

Crop 运行器的普通准备回调对应一个 ROI，必须返回 `BatchSize == 1` 的 `PreparedVisualInput`，这样每个 ROI 都能关联自己的 `RoiProjection`。需要把多个 ROI 打包成一个真正的模型 Batch 时，使用 `VisualRoiRunner.RunCropBatchAsync`：批量适配器按快照顺序返回一个 rank-4 输入，运行器校验 `BatchFrames` 与 ROI 数量一致，并通过行选择器拆回逐 ROI 的 `RoiInferenceItem`。内置 Detection、Classification、OBB、Pose、Instance/Semantic、Anomaly 和 RMBG Batch 结果可直接使用 `VisualRoiBatchResultSelectors.SelectKnownBatchRow`；自定义模型只需提供一个 `Func<VisualInferenceResult,int,VisualInferenceResult>` 行选择器。不要把多行 Batch 伪装成一个 Crop ROI。`RoiExecutionOptions.PreserveIndividualResults = false` 可在合并成功后省略返回对象中的 `Candidates`，但 `Batch` 仍保留每个 ROI 的成功/失败诊断。

真 Batch 的行数还受 `RoiExecutionOptions.MaximumBatchSize` 限制（默认 64），可按模型声明的最大 Batch、显存预算或后端吞吐基准调小；普通逐 ROI `RunCropAsync` 会按 Session 池分块执行，不会被这个真 Batch 上限误限制。

`RoiExecutionOptions.MaximumPreparedPixels`（默认 1 GiB）同时约束普通 Crop 和真 Batch：运行器会在准备回调之前按已解析 ROI 外接区域的源像素面积求和，超出时立即返回输入错误。它用于限制源侧裁剪/预处理工作量，不替代模型输入 Tensor 的 Batch 或输出像素上限；非矩形 ROI 仍按其安全外接范围计入预算，因此在高分辨率场景应结合 ROI 数量、并发和实际内存调小。

Crop 失败策略通过 `RoiExecutionOptions.FailureMode` 选择：`FailFast` 在当前分组失败后立即抛出；`CompleteThenFail` 会继续完成后续有界分组，释放每组 owned 输入后再抛出首个失败；`ReturnPartialResults` 则返回成功项和逐 ROI `Failed` 诊断。调用方取消或超时不会被转换成“部分成功”，而是按取消/超时错误结束。

每次 `RunCropAsync`、`RunCropBatchAsync` 或 `VisualRoiSlidingWindowRunner` 执行都会返回 `Diagnostics`。其中 `SelectedRoiCount`、`PreparedInputCount`、`InferenceCallCount`、`SucceededResultCount`、`FailedResultCount` 和 `PreparedPixelCount` 可用于确认实际调度是否符合预期；`UsedTrueBatch` 只有 `RunCropBatchAsync` 在一次模型调用中包含多个 ROI 行时为 `true`，普通 Crop 的多个 ROI 即使由 Session 池并发也不会误报为真 Batch。`WindowCount` 对滑窗表示规划窗口总数，`Elapsed` 是该次 ROI 调用的墙钟耗时。诊断计数是只读快照，不包含额外图像/Tensor 分配，适合写入现场日志或性能报告：

```csharp
RoiInferenceBatchResult batch = await runner.RunCropAsync(snapshot, prepareAsync);
RoiExecutionDiagnostics d = batch.Diagnostics;
Console.WriteLine($"rois={d.SelectedRoiCount}, prepared={d.PreparedInputCount}, calls={d.InferenceCallCount}, " +
                  $"ok={d.SucceededResultCount}, failed={d.FailedResultCount}, trueBatch={d.UsedTrueBatch}, " +
                  $"pixels={d.PreparedPixelCount}, elapsed={d.Elapsed.TotalMilliseconds:F1}ms");
```

当 `FailureMode.ReturnPartialResults` 返回失败项时，`FailedResultCount` 与 `batch.Failed.Count` 一致；滑窗运行器采用 FailFast 语义，准备或推理异常会直接抛出，不会把未执行窗口伪装成成功结果。

### OpenCV ROI 输入与真 Batch

`DeploySharp.Visual.OpenCV` 将图像解码和像素准备留在输入适配器中，Visual 主包不依赖 OpenCV。轴对齐矩形使用 `SubMat` 视图；多边形/Mask 先在 ROI 外填零；四边形和旋转矩形使用 `GetPerspectiveTransform` + `WarpPerspective`，返回的 `ImageTransform.IsProjective` 为 `true`，因此检测框、OBB、Pose、OCR 多边形和稠密 mask 都能按同一单应矩阵回到源图：

```csharp
var factory = new OpenCvVisualInputFactory();
var options = new OpenCvPreprocessOptions(
    new VisualSize(640, 640),
    resizeMode: OpenCvResizeMode.Letterbox,
    layout: VisualTensorLayout.Nchw,
    outputType: OpenCvOutputType.Float32);

using PreparedVisualInput rotated = factory.CreateRotatedRectangleRoi(
    OpenCvImageSource.FromFile("image.jpg"),
    new RotatedRectangleRoiGeometry(new PointF(640, 360), new SizeF(320, 180), 12),
    "images",
    options);

using PreparedVisualInput polygon = factory.CreateRoi(
    OpenCvImageSource.FromFile("image.jpg"),
    new PolygonRoiGeometry(new[]
    {
        new PointF(100, 80), new PointF(700, 80),
        new PointF(760, 500), new PointF(120, 520)
    }),
    "images",
    options);
```

同一张图有多个 ROI 时使用 `CreateRoiBatch`，源图只解码一次，返回的输入张量是 rank-4 的真实 NCHW/NHWC Batch。`options.BatchSize` 在这个 API 中不会覆盖 ROI 数量；每一行的源图尺寸、ROI 变换和 `InputId` 保存在 `PreparedVisualInput.BatchFrames`：

```csharp
using PreparedVisualInput batch = factory.CreateRoiBatch(
    OpenCvImageSource.FromFile("image.jpg"),
    new IVisualRoiGeometry[]
    {
        new RectangleRoiGeometry(new RectangleF(0, 0, 640, 640)),
        new RectangleRoiGeometry(new RectangleF(640, 0, 640, 640))
    },
    "images",
    options);

Console.WriteLine(batch.Tensor.Shape);       // [2, 3, 640, 640]
RoiProjection second = batch.CreateRoiProjection(
    new VisualRoi("right", new RectangleRoiGeometry(new RectangleF(640, 0, 640, 640))),
    batchIndex: 1);
```

真 Batch 的所有 ROI 行必须产生相同 rank-4 元素类型和空间尺寸；如果任务模型不接受动态 Batch，应改用 `VisualRoiRunner` 的 Session 池并发。OpenCV 的 ROI 专用入口在 net46/net461 目标上因 Visual ROI 类型不可用而显式排除，基础 `Create`/`CreateFromFile` 输入仍保持可用。`SelectKnownBatchRow` 还覆盖 OCR 文本识别和方向分类的 Batch 载荷；完整 OCR 检测/识别流水线仍应遵循其自身的 det/cls/rec 行关联合同。

分类任务不应使用空间 NMS；对分类 CropAndInfer 结果使用 `RoiClassificationResultMerger` 或 `ClassificationRoiResultMergerAdapter`，在 `KeepAll`、`HighestConfidence` 和 `RoiPriority` 中选择业务语义。Detection 结果则使用 `RoiDetectionResultMerger`，并根据模型输出选择 class-aware/class-agnostic NMS，或在滑窗重叠需要融合框坐标时选择 `WeightedBoxFusion`。WBF 只对同类别轴对齐框生效，OBB、Pose、OCR、实例分割和稠密任务会明确拒绝该模式，避免把错误的几何语义套到其他任务。

OBB 和 Pose 的整图 `FilterResults` 入口分别使用 `VisualRoiOrientedDetectionFilter` 与 `VisualRoiPoseFilter`。OBB 以权威四边形中心和多边形相交面积执行命中；Pose 优先使用 decoder 的 `BoundingBox`，没有边界框时按有效关键点计算保守边界。Pose 还支持 `KeypointCoverage`（有效关键点比例）、`AllKeypoints`（全部有效关键点）、`AnyKeypoint`（至少一个有效关键点）和 `VisibleKeypointRatio`（模型显式标记为 Visible 的关键点比例）；这些模式必须把 `taskFilter` 限定为 `PoseEstimation`，不会退化成外接框判断。两者都会返回 `RoiId` 和快照版本，方便业务追溯。OCR 检测可使用 `VisualRoiTextDetectionFilter`，完整检测/识别结果可使用 `VisualRoiOcrFilter`；两者均以文本权威多边形计算中心点、相交面积、IoU，并保留原阅读顺序和 ROI 来源，不会重新运行识别器。

对于 OBB、Pose、OCR 和实例分割，可显式使用任务合并器或对应的通用适配器：`RoiOrientedDetectionResultMerger`/`OrientedDetectionRoiResultMergerAdapter` 使用四边形精确 IoU，`RoiPoseResultMerger`/`PoseRoiResultMergerAdapter` 使用 topology 中声明的 OKS sigma，`RoiOcrResultMerger`/`OcrRoiResultMergerAdapter` 使用规范化文本加文本多边形 IoU，`RoiInstanceSegmentationResultMerger`/`InstanceSegmentationRoiResultMergerAdapter` 使用前景像素 IoU。它们都支持确定性排序；任务专用合并器保留贡献 ROI，通用适配器返回规范任务结果，而 `RunCropAndMergeAsync`/`RunRoiAndMergeAsync` 的 `Candidates`/`Windows.Results` 仍保留 ROI 与窗口来源。实例分割还可选生成 `ScorePriorityOwnership` 所有权图；实例掩码命中可使用 `MaskIntersectionRatio`，该模式必须把 `taskFilter` 限定为 `InstanceSegmentation`，按前景掩码像素与 ROI 的相交比例判断。

异常检测和背景移除属于稠密输出任务，不能把图像级分数当成 ROI 内的空间结果。`VisualRoiAnomalyFilter` 要求 decoder 已将归一化异常图和二值掩码恢复到源图尺寸，返回每个 Include ROI 的覆盖像素数、异常像素数、平均值、最大值、P95 和异常比例；`VisualRoiAlphaFilter` 对 RMBG Alpha 返回对应的覆盖数、平均/最大/P95 Alpha 和不透明比例。两者都会应用 Exclude ROI，不修改原始 map、mask 或 Alpha。通用 Crop/SlidingWindow 串联可以使用 `AnomalyRoiResultMergerAdapter` 或 `AlphaRoiResultMergerAdapter`，但它们要求调用方的 projector 已将稠密结果恢复到相同源分辨率；底层 `RoiAnomalyResultComposer`/`RoiAlphaResultComposer` 仍负责显式逐像素策略。多个 RMBG CropAndInfer 结果可使用 `RoiAlphaResultComposer` 按 `KeepFirst`、`KeepLast`、`MaxAlpha` 或 `RoiPriority` 合成完整源图 Alpha，并要求所有候选使用相同源尺寸、Profile 和 Model 来源：

```csharp
RoiAnomalyResult anomalyRois = VisualRoiAnomalyFilter.Filter(anomalyResult, manager.Snapshot);
foreach (RoiAnomalyRegion region in anomalyRois.Regions)
    Console.WriteLine($"{region.RoiId}: max={region.MaximumScore}, p95={region.Percentile95Score}, ratio={region.AnomalousPixelRatio:P2}");

RoiAlphaResult alphaRois = VisualRoiAlphaFilter.Filter(rmbgResult, manager.Snapshot, opaqueThreshold: 0.5f);
BackgroundRemovalResult mergedAlpha = new RoiAlphaResultComposer().Compose(alphaCandidates, RoiAlphaMergeMode.MaxAlpha);
```

如果 decoder 返回的 map、mask 或 Alpha 仍处于模型/张量尺寸，必须先使用模型专用恢复逻辑，不能由 ROI 统计 API 猜测缩放关系；尺寸不一致会显式抛出 `VisualException`。模型空间的 Anomaly 结果可以通过 `ModelSpaceAnomalyRoiProjector` 将归一化 map 和阈值 mask 以源图像素中心最近邻采样恢复到完整源图；RMBG Alpha 可以通过 `ModelSpaceAlphaRoiProjector` 完成同样的回映射。两个投影器都带有输出像素预算，只接受与 `RoiProjection.ModelSize` 完全一致的局部结果，并以源图空间的 identity transform 返回。多个已经投影的 Alpha 结果再交给 `RoiAlphaResultComposer`，显式选择 `KeepFirst`、`KeepLast`、`MaxAlpha` 或 `RoiPriority`。

Anomaly 的多个 ROI 结果可以使用 `RoiAnomalyResultComposer` 合成源分辨率分数图。所有候选必须拥有相同的 map 语义、尺寸和阈值；合成后会按统一阈值重新生成二值 mask。`MaxScore` 适合 ROI 裁剪图在未覆盖区域填零的情况，`RoiPriority` 适合固定工位优先级，`MeanScore` 只有在每个候选都覆盖同一源图坐标时才有明确意义。合并器不会从零值反推 ROI 覆盖范围，也不会把不同阈值的 mask 静默混合：

```csharp
AnomalyDetectionResult fusedAnomaly = new RoiAnomalyResultComposer().Compose(
    anomalyCandidates,
    RoiAnomalyMergeMode.MaxScore);

BackgroundRemovalResult fusedAlpha = new RoiAlphaResultComposer().Compose(
    alphaCandidates,
    RoiAlphaMergeMode.RoiPriority);
```

如果自定义 decoder 输出的是 ROI 或模型局部坐标，而不是 DeploySharp 默认的源图坐标，可以通过 `ModelSpaceDetectionRoiProjector`、`ModelSpaceOrientedDetectionRoiProjector`、`ModelSpacePoseRoiProjector`、`ModelSpaceTextDetectionRoiProjector`、`ModelSpaceOcrRoiProjector`、`ModelSpaceInstanceSegmentationRoiProjector` 和 `ModelSpaceSemanticSegmentationRoiProjector` 显式回映射。`ModelSpaceOcrRoiProjector` 会同时转换文本权威多边形、可选透视四边形和识别文本关联；如果 `OcrResult.Orientation` 携带了全局方向校正来源，则必须使用知道该方向链路的业务投影器，通用投影器会明确拒绝猜测逆旋转。稠密 mask 投影使用有界最近邻采样，并要求模型 mask 与 `RoiProjection.ModelSize` 完全一致；实例分割投影会按源图边界计算新的 mask 和边界框，语义分割投影会用指定背景类填充 ROI 外区域，同时只在 ROI 几何真正拥有的像素上写入标签和概率。自定义稠密投影器可使用 `RoiProjection.ContainsSourcePixel(x, y)` 复用同一像素中心归属规则，避免把非矩形 ROI 的外接矩形误当成有效区域。只有在结果尺寸等于 `RoiProjection.ModelSize` 时才允许投影；内置 decoder 已经根据 `PreparedVisualInput.BatchFrames` 回映射到源图，不能再次调用这些 projector，否则会重复应用裁剪偏移。

多个已投影语义标签图可以使用 `RoiSemanticSegmentationResultMerger` 进行跨 ROI 融合。标签入口只接受相同尺寸、相同类别契约的 label mask，并支持 `RoiSemanticLabelMergeMode.KeepFirst`、`KeepLast` 和 `RoiPriority`；结果会重新计算全图类别统计，同时返回逐像素 `PixelOwnerRoiIds`。如果需要保留置信度，使用独立的 `MergeWithProbabilities` 入口：它要求所有结果携带源分辨率 HWC 概率图，并支持 `KeepFirst`、`KeepLast`、`Mean`、`Max` 和 `RoiPriority`；融合后按最大类别概率重建标签并保留概率图：

```csharp
RoiMergedSemanticSegmentationResult merged = new RoiSemanticSegmentationResultMerger().Merge(
    projectedSemanticCandidates,
    RoiSemanticLabelMergeMode.RoiPriority);
Console.WriteLine($"part pixels={merged.Result.Statistics.Single(item => item.ClassIndex == 1).PixelCount}");

RoiMergedSemanticSegmentationResult probabilityMerged = new RoiSemanticSegmentationResultMerger().MergeWithProbabilities(
    projectedSemanticCandidates,
    RoiSemanticProbabilityMergeMode.Mean);
```

例如，自定义实例分割 decoder 返回的是 ROI 模型画布坐标时，先使用同一帧的 `RoiProjection` 恢复源图坐标，再合并多个 ROI：

```csharp
RoiProjection projection = prepared.CreateRoiProjection(roi);
InstanceSegmentationResult sourceResult =
    new ModelSpaceInstanceSegmentationRoiProjector().Project(modelResult, projection);

var candidates = new[]
{
    new RoiProjectedResult<InstanceSegmentationResult>(roi.Id, roi.Priority, sourceResult)
};
RoiMergedInstanceSegmentationResult merged =
    new RoiInstanceSegmentationResultMerger().Merge(
        candidates,
        RoiResultMergeMode.ClassAwareNms,
        iouThreshold: 0.5f);
```

OCR 滑窗结果可以按文本和几何合并；`KeepAll` 不去重，`HighestConfidence` 或 `RoiPriority` 会抑制同文本的重叠候选，并在 `Items` 中保留所有贡献 ROI：

```csharp
RoiMergedOcrResult ocr = new RoiOcrResultMerger().Merge(
    ocrCandidates,
    RoiResultMergeMode.HighestConfidence,
    polygonIouThreshold: 0.5f,
    ignoreCase: true,
    collapseWhitespace: true);
foreach (RoiOcrRegion item in ocr.Items)
    Console.WriteLine($"{item.Region.Recognition.Text}: {string.Join(",", item.RoiIds)}");
```

投影器会复制规范结果元数据并创建新的源图 mask；不会修改模型 decoder 返回的输入对象。大图必须根据实际内存设置 `maximumTotalMaskPixels` 或 `maximumOutputPixels`，不要无限制地把每个窗口展开成完整源图 mask。

## SAM 提示生成

对于 Promptable Segmentation（例如 SAM）不需要把 ROI 重新编码成模型专用张量。`VisualRoiPromptFactory` 会从启用的 `Include` ROI 生成源图坐标的 box、前景点和可选边界负点；多边形/旋转矩形会采样边界顶点，Mask ROI 会使用前景像素质心作为锚点。点数有上限，结果顺序稳定，适合缓存和重试：

```csharp
VisualRoiSnapshot snapshot = manager.Snapshot;
VisualRoi roi = snapshot.Rois.Single(item => item.Id == "inspection-zone-a");
var prompt = VisualRoiPromptFactory.Create(
    snapshot,
    roi,
    new VisualRoiPromptOptions(maximumPoints: 12, includeBoundaryNegatives: true, returnMultipleMasks: false));

using var session = new PromptableSegmentationImageSession(registry, bundle, request);
PromptableSegmentationResult result = session.Predict(prompt);
```

`CreateMany(snapshot, task)` 只返回适用于指定任务的启用 Include ROI；Exclude ROI 不会被转换成正向提示。对于已经完成 `SetImage` 的 `PromptableSegmentationImageSession`，可以使用 `VisualRoiPromptRunner` 顺序执行整组 Prompt，并按 `RoiExecutionOptions.FailureMode` 选择 FailFast、ReturnPartialResults 或 CompleteThenFail：

```csharp
using var session = new PromptableSegmentationImageSession(registry, bundle, request);
session.SetImage(preparedImage);

VisualRoiPromptBatchResult prompts = await new VisualRoiPromptRunner().RunAsync(
    session,
    manager.Snapshot,
    new VisualRoiPromptOptions(maximumPoints: 12, returnMultipleMasks: false),
    new RoiExecutionOptions(
        failureMode: RoiFailureMode.ReturnPartialResults,
        maximumRois: 64,
        correlationId: "camera-01/frame-00042"));

foreach (VisualRoiPromptResultItem item in prompts.Succeeded)
    Console.WriteLine($"{item.Roi.Id}: masks={item.Result!.Segmentation.Instances.Count}");
foreach (VisualRoiPromptResultItem item in prompts.Failed)
    Console.Error.WriteLine($"{item.Roi.Id}: {item.Failure!.Message}");
```

该运行器不会并发调用同一个有状态 SAM Session：Session 本身要求单图像 Embedding 串行解码，因此按快照顺序执行可避免并发状态冲突，同时保留每个 ROI 的 Prompt、结果和异常。`PromptableSegmentationImageSession.CurrentImage.SourceSize` 必须与快照源尺寸一致；ROI 的 `classFilter` 对 Prompt 没有语义，配置后会被明确拒绝。交互式 mask-feedback、VLM/VQA 语义提示和多帧跟踪仍由上层应用负责，ROI 工厂不伪造这些模型输入。

## SAM2/SAM3 视频 ROI：计划与状态提交

SAM2/SAM3 的官方视频 Predictor 会修改 memory、对象状态和 tracker 状态，不能把每一帧当成无状态 `RunManyAsync`。`VisualRoiVideoPromptPlanner` 只负责可验证的编排边界，不替代官方 Predictor：

1. `CreatePlan` 根据一个不可变 ROI 快照生成首帧 `Initialize`、连续 `Propagate` 或修正 `Correct` 计划；ROI 按快照顺序获得稳定 `ObjectIndex`。
2. 应用把计划交给 SAM2/SAM3 官方 Predictor；只有 Predictor 成功接受该帧后才调用 `Commit`。取消、异常或丢帧不会推进规划器状态。
3. 规划器强制源尺寸、快照版本和帧索引严格一致，并执行 Profile 的 `MaximumObjects`/`MaximumFrames` 上限；要换视频或热更新区域必须 `Reset` 后重新初始化。

应用适配一次 `IVisualRoiVideoPromptPredictor<TFrame,TResult>` 后，可以让 `VisualRoiVideoPromptRunner` 串行执行 Predictor，并只在成功后提交计划：

```csharp
var planner = new VisualRoiVideoPromptPlanner(sam2VideoProfile);
var runner = new VisualRoiVideoPromptRunner<VideoFrame, SamVideoMasks>(
    planner,
    applicationOwnedSam2Predictor);

VisualRoiVideoFrameResult<SamVideoMasks> first = await runner.RunAsync(
    frame0, snapshot, 0, VisualRoiVideoFrameMode.Initialize,
    cancellationToken: cancellationToken);

VisualRoiVideoFrameResult<SamVideoMasks> next = await runner.RunAsync(
    frame1, snapshot, 1, VisualRoiVideoFrameMode.Propagate,
    cancellationToken: cancellationToken);

// 换视频或换 ROI 配置时，先成功清理 Predictor，再清理 Planner。
await runner.ResetAsync(cancellationToken);
```

Runner 不拥有也不释放应用的 Predictor。适配器的 `ResetAsync` 必须清理官方实现的 memory bank、对象槽位和帧缓存；若重置失败，Planner 会保留原状态，避免一边已清理、一边仍认为可以继续传播。也可继续直接使用 `CreatePlan -> Predictor -> Commit` 三步形式集成已有框架。

当前仓库中的 SAM2/SAM3 Profile 仍是 `ExternalContractOnly`，没有可由 DeploySharp 创建的官方 ONNX/OpenVINO/TensorRT 视频 Bundle，因此 `RequiresExternalPredictor` 会保持 `true`。[官方视频适配案例](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/samples/02-visual/roi-official-sam) 提供真实视频解码、Python Predictor、源图掩码导出和重置一致性验证。SAM2 tiny 已通过 6 帧 CPU 短测；SAM3 官方 CPU 构造直接依赖 CUDA，其入口仍未完成端到端实测。不要把外部 PyTorch Predictor 的成功扩展为原生多后端支持。

## VLM/VQA 的 ROI 执行策略

图像描述、VQA 和图像条件生成的输出是文本，不是可用来做框/Mask NMS 的空间对象。DeploySharp 采用“每个 ROI 独立 CropAndInfer、独立保留问题与文本、按快照顺序返回”的策略：不把整图文本错误地复制给多个 ROI，也不对文本结果套用 Detection 合并器。`VisualRoiGenerativeVisionLanguageRunner` 会拒绝 `FilterResults`、`ModelInput` 坐标和 class filter，限制 Batch=1，并保留每个 ROI 的准备失败或生成失败。

```csharp
VisualRoiGenerativeVisionLanguageBatchResult answers =
    await new VisualRoiGenerativeVisionLanguageRunner().RunAsync(
        session, tokenizer, manager.Snapshot, request,
        (roi, geometry, token) => PrepareVlmCropAsync(roi, geometry, token),
        new RoiExecutionOptions(
            failureMode: RoiFailureMode.ReturnPartialResults,
            maximumRois: 16,
            maximumPreparedPixels: 256L * 1024 * 1024,
            correlationId: "camera-01/frame-42"),
        cancellationToken);

foreach (VisualRoiGenerativeVisionLanguageResultItem item in answers.Succeeded)
    Console.WriteLine($"{item.Roi.Id}: {item.Result!.Generation.Text}");
```

生成式 Session 通常携带当前图像和 KV/解码状态，运行器会串行调用同一 Session；要并行处理多个 ROI，请创建有界的独立 Session 池，不要共享可变图像状态。若需要把文本回答与检测框关联，关联应由应用使用 `RoiId`/`CorrelationId` 保存，不能从文字内容反推空间位置。

## TensorRT CUDA ROI 与设备端后处理边界

[两设备实测矩阵](roi-backend-performance-matrix.md) 已记录 Windows RTX3060 Laptop 与 Ubuntu RTX2060 上 YOLOv8n/YOLOv8n-seg 的 ROI P50/P95，包含 TensorRT 原地转换与 ORT CUDA。两台设备的 YOLOv8n-seg CPU/GPU 掩码逐像素差异为 0；设备 NMS 候选上限为 32768。该证据不替代其他模型/几何的专门测试。

`TensorRtVisualPipeline.RunRoi` 对轴对齐矩形提供直接设备采样：完整紧凑 BGR 帧只上传一次，CUDA 融合内核通过 `sourceOffsetX/sourceOffsetY/cropWidth/cropHeight` 在原始设备缓冲区上完成双线性采样、Resize/Letterbox、颜色转换和归一化，不创建 CPU ROI 临时图。`Run` 与 `RunRoi` 共用同一 stream、输入输出缓冲区和后处理状态，单个 Pipeline 的并发调用仍由执行门串行化。

需要并发帧或并发 ROI 时，不要把同一个 `TensorRtVisualPipeline` 放到多个线程直接调用；应为每个 TensorRT execution context 创建独立 Pipeline，再交给 `TensorRtVisualPipelinePool`。池只负责有界租约、等待取消、输入顺序恢复和释放时等待活动调用，不会复制 context，也不会把动态 Batch 或真多 stream 能力伪装出来：

```csharp
var pipelines = enginePaths.Select(path => new TensorRtVisualPipeline(
    profile, path, preprocessing, backendOptions,
    TensorRtCudaVisualPostprocessingMode.WhenSupported));
using var pool = new TensorRtVisualPipelinePool(pipelines);

IReadOnlyList<VisualInferenceResult> results = await pool.RunRoiManyAsync(
    roiFrames.Select(frame => (frame.Image, frame.Roi)).ToArray(),
    cancellationToken);
```

`RunRoiManyAsync` 只能接受轴对齐源像素矩形；旋转矩形、多边形、Mask 和透视 ROI 必须走 OpenCV/CPU 精确路径，除非应用明确接入并验证了对应 projective CUDA kernel。池成员必须使用相同 `ModelId` 和任务 Profile，且由池接管释放；如果需要跨任务或跨模型并发，应建立多个池并分别限制显存预算。

YOLO 实例分割的 admitted CUDA 路径会在同一 stream 上执行候选字段校验/阈值筛选、有限候选的设备端贪心 NMS、原型组合和源图 Mask 恢复；候选数超过设备 NMS 上限或 CUDA 合同不满足时自动回到现有 CPU decoder。设备 NMS 当前是确定性的有界实现，优先保证与 managed decoder 的排序/类别语义一致，不应把它解释为所有模型和所有候选规模的最优 NMS。

旋转矩形、多边形、Mask 和透视 ROI 暂时使用 `DeploySharp.Visual.OpenCV` 的精确仿射/单应路径；在 projective CUDA kernel、真 Batch、多 stream 和设备端结果一致性完成真实设备证据前，不会静默把它们近似成轴对齐矩形。建议使用 `DEPLOYSHARP_TENSORRT_CUDA_VALIDATE_POSTPROCESSING=1` 做同进程 CPU/GPU 结果对照，并保存模型、输入、驱动、TensorRT、CUDA、时钟状态以及 P50/P95。

## 视频区域事件

### 长时视频运行器与资源边界

如果应用已经有解码器和检测/跟踪器，可以使用 `VisualRoiVideoRunner<TFrame>` 把逐帧调度、空帧过期、取消、失败策略和资源健康指标固定下来。运行器不会把视频帧收集到内存，也不会替应用持有或释放解码器；`frames` 应该是延迟枚举，`releaseFrame` 用于归还应用自己的帧缓冲。

```csharp
var runner = new VisualRoiVideoRunner<DecodedFrame>();
VisualRoiVideoRunReport report = await runner.RunAsync(
    frames: decoder.ReadFrames(cancellationToken),
    trackAsync: (frame, token) => tracker.TrackAsync(frame, token),
    timestamp: frame => frame.Timestamp,
    processor: processor,
    options: new VisualRoiVideoRunOptions(
        maximumFrames: 200_000,
        continueOnFrameFailure: true,
        maximumRecordedFailures: 256),
    releaseFrame: frame => decoder.Release(frame),
    cancellationToken: cancellationToken);

Console.WriteLine($"frames={report.ProcessedFrames}, failures={report.FailedFrames}, " +
                  $"peakTracks={report.PeakActiveTrackCount}, " +
                  $"peakCooldown={report.PeakCooldownEntryCount}, " +
                  $"elapsed={report.Elapsed}");
```

每一帧都会先调用跟踪器，再将非空观测提交给 `ProcessFrame`，随后调用 `Advance`。因此没有检测结果的空帧也会推进 TrackId 超时；不能为了节省调用而跳过 `Advance`。`continueOnFrameFailure=false` 时第一个非取消异常会终止运行；启用后异常会计入 `FailedFrames`，最多保留 `maximumRecordedFailures` 个异常对象，其余数量在 `DroppedFailureCount` 中统计，并继续推进过期状态。取消异常始终向上传递，不会被当作普通帧失败吞掉。

`MaximumFrames` 是防止错误解码器或无限流导致运行失控的硬上限；如果源序列继续产生第 `MaximumFrames + 1` 帧，运行器会释放该帧后抛出 `InputInvalid`，不会静默截断。跟踪器、解码器和 GPU Session 的并发/队列上限仍由应用控制。生产监控至少应记录 `ProcessedFrames`、`FailedFrames`、`DroppedFailureCount`、`PeakActiveTrackCount`、`PeakCooldownEntryCount` 和 `PeakCountKeyCount`。峰值只增不减，应检查其是否保持有界，并另外检查处理器当前活动状态在目标过期后是否回收。库内的 20,000 帧合同测试只验证状态回收和有界计数；真实设备长时间 soak 本轮暂缓。

ROI 事件处理器消费外部跟踪器输出的 `TrackId`、目标框/质心和单调时间戳，负责区域进入、离开、停留、穿线、方向、去抖、冷却、丢失过期和计数。它不内置检测器或跟踪器，因此可以与现有 YOLO、ByteTrack、DeepSORT 或自研跟踪器组合：

```csharp
var processor = new VisualRoiEventProcessor(
    manager.Snapshot,
    VisualTaskId.ObjectDetection,
    lines: new[] { new VisualRoiLine("gate-1", "inspection-zone-a", new PointF(640, 0), new PointF(640, 1080), VisualRoiLineDirection.Positive) },
    options: new VisualRoiEventOptions(
        dwellDuration: TimeSpan.FromSeconds(2),
        debounceDuration: TimeSpan.FromMilliseconds(100),
        cooldownDuration: TimeSpan.FromSeconds(1),
        minimumStableObservations: 2));

foreach (VisualRoiTrackObservation observation in trackerFrame)
{
    foreach (VisualRoiEvent item in processor.Process(observation))
        OnRoiEvent(item);
}

// 当前帧没有观测到目标时推进时钟，超时后会发出 Exited。
foreach (VisualRoiEvent item in processor.Advance(frameTimestamp, frameIndex))
    OnRoiEvent(item);

IReadOnlyDictionary<string, long> counters = processor.GetCounts();
```

视频采集通常按帧同时提交多个跟踪目标时，可使用 `ProcessFrame`。它会在同一个临界区内先校验整帧的重复 ID、乱序时间戳和 `MaximumTracks` 容量，再按输入顺序更新状态并返回事件；因此一帧不会因为中途发现坏数据而只更新一部分目标：

```csharp
IReadOnlyList<VisualRoiEvent> frameEvents = processor.ProcessFrame(trackerFrame);
foreach (VisualRoiEvent item in frameEvents)
    OnRoiEvent(item);
```

`ProcessFrame` 不会替代跟踪器，也不会改变单目标 `Process` 的语义。默认的乱序策略仍为忽略；若使用 `VisualRoiOutOfOrderMode.Reject`，整帧在状态修改前失败。帧内 `TrackId` 必须唯一，空帧是合法的 no-op。

事件状态绑定一个不可变 ROI 快照。需要在线更新时，可以在同一处理器上调用 `UpdateSnapshot`：默认按仍存在的 ROI ID 保留轨迹状态，删除的 ROI 状态会被清理；如果几何变化足以改变业务语义，可使用 `VisualRoiSnapshotUpdateMode.ResetTrackState` 清空全部轨迹，或使用 `ResetChangedRoiState` 只清除几何、命中规则、坐标空间、任务/类别过滤发生变化的 ROI，保留其余区域的连续观测。需要平滑迁移时可使用 `ReevaluateTrackState`，它根据每个目标最近一次源图位置在新几何上重建稳定 inside/outside 状态，不合成虚假的 Entered/Exited，再等待真实下一帧变化。替换快照必须保持源图尺寸不变，线定义会和快照一起原子校验。`minimumStableObservations` 与 `debounceDuration` 同时满足后才触发进入/离开；`hysteresisPixels` 可为矩形/旋转矩形/多边形提供仅用于退出的空间滞回，抑制边界抖动。乱序时间戳默认忽略，也可以用 `VisualRoiOutOfOrderMode.Reject` 显式失败；`MaximumTracks` 和超时用于限制长时间运行的内存增长。目标过期时，其逐 Track 冷却键也会同步删除；可采集 `ActiveTrackCount`、`CooldownEntryCount` 和 `CountKeyCount` 作为长时间运行健康指标。自动化合同已经覆盖 2,000 个同时轮换 Track 和 20,000 帧滚动 Track 的过期回收，但这仍不能代替真实解码器、跟踪器和 GPU 流水线的设备 soak。若事件 ROI 使用 `TileLocal` 或 `World`，在构造处理器时传入与源图尺寸一致的 `VisualRoiCoordinateContext`，处理器会在同一快照下完成解析。

```csharp
long version = processor.UpdateSnapshot(nextSnapshot, nextLines);
processor.UpdateSnapshot(safetySnapshot, updateMode: VisualRoiSnapshotUpdateMode.ResetTrackState);
processor.UpdateSnapshot(tunedSnapshot, updateMode: VisualRoiSnapshotUpdateMode.ResetChangedRoiState);
processor.UpdateSnapshot(migratedSnapshot, updateMode: VisualRoiSnapshotUpdateMode.ReevaluateTrackState);
```

## TileLocal 与 World 坐标上下文

滑窗内部坐标和标定后的工位坐标不能仅凭 ROI JSON 猜测源图位置。执行时提供显式 `VisualRoiCoordinateContext`：

```csharp
VisualRoiCoordinateContext tileContext = VisualRoiCoordinateContext.ForTile(
    new VisualSize(4096, 2160), new RectangleF(1024, 512, 1024, 1024));
IVisualRoiGeometry sourceGeometry = snapshot.Resolve(tileLocalRoi, tileContext);

VisualRoiWorldTransform worldToSource = VisualRoiWorldTransform.CreateAffine(
    scaleX: 4, shearX: 0, offsetX: 120,
    shearY: 0, scaleY: 4, offsetY: 80);
VisualRoiCoordinateContext worldContext = VisualRoiCoordinateContext.ForWorld(
    new VisualSize(4096, 2160), worldToSource);
IVisualRoiGeometry calibratedGeometry = snapshot.Resolve(worldRoi, worldContext);
```

TileLocal 的矩形、旋转矩形和多边形会平移回源图；整数切片的 Mask 会复制到源图尺寸的对应区域。World 使用有限、可逆的 3×3 单应矩阵，并将几何转换为源图多边形。World Mask 使用该单应矩阵的逆变换，按源图像素中心以最近邻方式栅格化到 `SourceSize`；这不是外接框近似，且输出受 64M 像素上限保护。上下文的 `SourceSize` 必须与当前帧一致，否则立即失败。

当前 OBB/Pose/OCR/文本检测/实例分割/语义分割已有通用 Crop/SlidingWindow 强类型执行入口和合并合同；SAM 提供基于缓存 Embedding 的多 ROI Prompt 执行器，SAM2/SAM3 另有外部 Predictor 计划器。VLM/VQA 已提供独立 CropAndInfer 运行器。真实模型、多后端证据仍不完整，TensorRT 目前只对轴对齐矩形和有限 YOLO 实例分割后处理有设备路径。OpenCV 已提供矩形、旋转矩形、四边形透视、多边形/Mask 以及一次解码的真 Batch 输入；Anomaly 的源图 map/mask ROI 统计与跨 ROI 分数图合成、RMBG 的 Alpha ROI 统计/显式合成/模型局部投影、语义分割的 label/概率融合以及 SAM 的 box/point prompt 生成与执行已可用。JSON 文件可以保存这些公共几何，但在对应真实后端验证完成前，应用必须根据能力矩阵拒绝或回退，不能把“配置可保存”或“合同测试通过”当作“所有后端已支持”。
