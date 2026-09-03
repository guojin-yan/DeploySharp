# 异步帧流水线与滑动窗口检测

视觉推理常见的两个吞吐瓶颈是：视频逐帧处理时 CPU 准备与设备推理彼此等待，以及大图缩小后小目标信息丢失。DeploySharp 分别提供 `RunPrefetchedAsync` 和 `SlidingWindowDetectionRunner` 解决这两类问题。两者都复用 `VisualPipeline` 的独立 Session 池，并保持结果顺序和有界内存。

## 选择正确的接口

| 场景 | 接口 | 实际执行方式 |
| --- | --- | --- |
| 单帧异步推理 | `RunAsync` | 等待一个准备完成的输入 |
| 多个 batch=1 输入 | `RunManyAsync` | 分派到独立 Session，按输入顺序返回 |
| 视频帧准备与推理重叠 | `RunPrefetchedAsync` | 有界准备后续帧，同时推理当前帧 |
| 模型真正支持动态 batch | `InferenceBatchScheduler` | 将多条输入组成一个模型张量 batch |
| 大图小目标检测 | `SlidingWindowDetectionRunner` | 重叠切片、并发推理、坐标还原和全局 NMS |

`RunPrefetchedAsync` 不是动态 batch。它不会修改模型输入形状，而是让 CPU 图像准备和独立后端 Session 同时工作。模型若只接受 batch=1，应选择 Session 池；只有输入、Profile 和 Decoder 都支持首维 batch 时，才使用真正的批量调度。

## 视频帧异步预取

下面的 `framePaths` 可以替换为相机采集后落入有界缓冲区的一组帧。OpenCV 工厂在工作线程中解码和准备下一帧，Pipeline 同时执行已经准备好的帧：

```csharp
var inputFactory = new OpenCvVisualInputFactory();
var preprocess = new OpenCvPreprocessOptions(
    modelSize: new VisualSize(640, 640),
    resizeMode: OpenCvResizeMode.Letterbox,
    colorOrder: VisualColorOrder.Rgb,
    inputDivisors: new[] { 255f, 255f, 255f });

using var pipeline = new VisualPipeline(
    backendRegistry,
    selection,
    request,
    new SessionOptions(maxConcurrency: 2));

IReadOnlyList<VisualInferenceResult> results =
    await pipeline.RunPrefetchedAsync(
        framePaths,
        (path, token) => inputFactory.CreateFromFile(
            path, "images", preprocess,
            inputId: path,
            cancellationToken: token),
        prefetch: 2,
        options: new VisualExecutionOptions(
            timeout: TimeSpan.FromSeconds(10)),
        cancellationToken: cancellationToken);
```

返回顺序与 `framePaths` 一致。准备回调必须返回不再被其他线程修改的 `PreparedVisualInput`。在实时视频中，不要无限累积路径或帧对象；应由应用使用有界队列分段提交，并明确采用“等待、丢旧帧或丢新帧”的背压策略。

### 并发参数怎么选

- 从 `maxConcurrency: 1`、`prefetch: 1` 开始，分别记录准备、推理、后处理和端到端时间。
- CPU 前处理较重且设备有空闲时，先增加 `prefetch`；GPU 仍有余量时，再测试 2 或 4 个独立 Session。
- 每个 Session 都会持有自己的原生推理上下文，可能复制权重或占用额外显存。吞吐没有继续提升时应回退到更小的池。
- 同一组参数必须同时检查 P50、P95、吞吐量和内存，不能只取一次最快耗时。

## 大图滑动窗口检测

滑动窗口运行器只接受目标检测 Profile。它生成覆盖完整源图的半开区间窗口，相邻窗口按比例重叠；每个窗口得到 batch=1 的 `DetectionResult` 后，运行器把框映射回源图坐标并执行一次全局 NMS。

```csharp
var runner = new SlidingWindowDetectionRunner(pipeline);
var options = new SlidingWindowDetectionOptions(
    windowSize: new VisualSize(1024, 1024),
    overlap: 0.20f,
    globalIouThreshold: 0.45f,
    nmsMode: DetectionNmsMode.ClassAware,
    maximumWindows: 256,
    maximumDetections: 300,
    includeFullImagePass: false,
    coordinateMode: SlidingWindowCoordinateMode.Auto);

SlidingWindowDetectionResult result = await runner.RunAsync(
    sourceSize,
    options,
    (window, token) => PrepareCropAsync(sourceImage, window.Bounds, token),
    new VisualExecutionOptions(timeout: TimeSpan.FromSeconds(30)),
    cancellationToken);

foreach (Detection detection in result.Detections.Detections)
{
    Console.WriteLine($"{detection.Label.Label}: {detection.Label.Score:F3} {detection.Box}");
}
```

`PrepareCropAsync` 是应用的图像适配回调：从同一张已解码源图裁出 `window.Bounds`，执行与模型 Profile 完全一致的 resize、letterbox、通道变换和归一化，并返回 `PreparedVisualInput`。避免在每个回调中重新解码整张图片；应复用源图、只创建当前切片需要的张量。

## 坐标模式

| 模式 | 回调输出 | 运行器行为 |
| --- | --- | --- |
| `Auto` | 根据 `PreparedVisualInput.SourceSize` 判断 | 切片尺寸表示局部坐标，完整源图尺寸表示已映射坐标 |
| `TileLocal` | 检测框相对于切片左上角 | 加上窗口原点并裁剪到源图边界 |
| `Source` | 检测框已经是完整源图坐标 | 不再增加窗口偏移 |

不确定时使用 `Auto`，并用跨越切片边界的已知目标验证一次。若使用 `TileLocal`，准备输入的 `SourceSize` 必须等于当前窗口尺寸，否则运行器会拒绝不一致的几何合同。

## 窗口、重叠和全图补检

窗口尺寸应接近模型训练或导出时的有效分辨率。建议从 20% 重叠开始：小目标密集时可提高重叠，细长目标可用 `horizontalOverlap` 和 `verticalOverlap` 分别调节。重叠越大，窗口数和推理成本增长越快，因此应设置 `maximumWindows` 防止错误参数造成无界任务。

`includeFullImagePass` 会额外推理一次完整图像，适合避免大目标被切断，但不能替代切片检测。全图结果与切片结果仍会一起执行全局 NMS。类别容易互相遮盖时使用 `ClassAware`；确实需要跨类别去重时再改为 `ClassAgnostic`。

## 性能与正确性验证

1. 固定模型、后端、输入图、窗口尺寸、重叠和 Session 数量，先预热再计时。
2. 分别记录一次图像解码、切片准备、后端推理、坐标合并和全局 NMS；不要把模型加载混入稳态耗时。
3. 用原图直接推理和滑动窗口结果对照，检查边缘目标、重复框、坐标越界和类别错误。
4. 用目标设备实测 `maxConcurrency`，因为 CPU、GPU 和不同后端的最佳 Session 数量不同。
5. 监测峰值内存。运行器只预取下一组切片，但过大的窗口、并发和源图仍会显著增加内存。

更多模型批量规则见[性能基准方法](performance-benchmarking.md)，检测 Profile 见[分类、检测与 YOLO](visual-yolo-detection.md)，后端实测状态见[模型与后端验证矩阵](../model-backend-verification-matrix.md)。
