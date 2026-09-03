# Batch、Session 池与并发

DeploySharp 同时支持真正的模型 Batch、多个 batch-one Session 并发，以及“准备下一帧与当前推理重叠”三种模式。它们解决的问题不同，不能仅凭方法名互相替代。

## 三种模式

| 模式 | 入口 | 模型要求 | 典型用途 |
| --- | --- | --- | --- |
| 真正模型 Batch | `InferenceBatchScheduler<TInput,TOutput>` | 输入、输出和 decoder 都声明动态首维 | 同一模型一次处理多张图 |
| Session 池并发 | `VisualPipeline.RunManyAsync`、`SessionOptions.MaxConcurrency` | batch-one 也可用；每个 Session 独立 | 多张独立图片、吞吐优先 |
| 异步预取 | `VisualPipeline.RunPrefetchedAsync` | 任意可异步准备的输入 | 视频中准备下一帧时执行当前帧 |

真正 Batch 改变模型张量的首维；Session 池只增加独立 native 执行上下文；异步预取只重叠 CPU 准备和后端调用。一个 batch-one 模型不能通过 `RunManyAsync` 变成真正 Batch。

## 真正 Batch

Profile 必须同时声明动态或有界 batch 输入、对应输出形状和 batch decoder。解码时，`PreparedVisualInput.BatchFrames` 为每一行保存源图尺寸和 `ImageTransform`，这样检测框、分割图、关键点和 Alpha 可以分别还原到各自源图。

```csharp
var scheduler = new InferenceBatchScheduler<ImageItem, DetectionResult>(
    session,
    maximumBatchSize: 8,
    prepareBatch: items => PrepareDetectionBatch(items),
    decodeBatch: (outputs, count) => DecodeDetectionBatch(outputs, count),
    maximumInFlightBatches: 2);
IReadOnlyList<DetectionResult> results =
    await scheduler.RunAsync(images, cancellationToken);
```

`prepareBatch` 必须按照输入合同创建一个连续张量；`decodeBatch` 必须返回与 `count` 完全相同的结果数。调度器会限制在途批次，避免在前面批次尚未解码时保留所有预处理张量。准备失败、取消或解码数量不一致会终止本次调用，不返回部分结果。

## batch-one Session 池

后端注册时传入 `SessionOptions(maxConcurrency: n)` 会从头创建 n 个独立 Session：

```csharp
using var pipeline = new VisualPipeline(
    backends, selection, request,
    new SessionOptions(maxConcurrency: 4));
IReadOnlyList<VisualInferenceResult> results =
    await pipeline.RunManyAsync(preparedInputs, cancellationToken: cancellationToken);
```

调用会租用空闲 Session，超过池容量的输入排队，并按输入顺序返回。每个 Session 都拥有独立的 native context、scratch buffer 和 backend state；只复制托管包装器会导致线程安全问题或隐式串行。

Session 数量不是越大越好。CPU 后端需要在通道数与每通道线程数之间平衡；GPU 后端需要考虑显存、CUDA stream、TensorRT execution context 和功耗限制。建议固定输入和计时协议，在 `1,2,4` 个 Session 上分别测量，而不是在运行时无限扩容。

## PaddleOCR 的特殊调度

PaddleOCR 是检测、可选方向分类和识别三个阶段：

- 检测阶段通常对整张图执行一次，使用一个独立 Session；
- 方向分类和识别阶段将文本行按宽度分组，分别使用动态 batch；
- 分类和识别可以各自创建多个独立 Session，批次超过空闲通道时等待；
- 最后按检测顺序合并 polygon、文本、置信度和方向结果。

`maximumRecognitionBatch` 是单批行数，`SessionOptions.MaxConcurrency` 是独立推理通道数，二者必须联合调优。过大的 batch 会产生宽度 padding，过多通道会争抢 CPU 核心或 GPU stream。完整流水线和最佳组合见 [PaddleOCR 三模型流水线](visual-paddle-ocr3.md) 与[设备性能实测](device-performance-benchmarks.md)。

## 异步预取

```csharp
IReadOnlyList<VisualInferenceResult> frames =
    await pipeline.RunPrefetchedAsync(
        frameIds,
        (frame, token) => PrepareFrame(frame, token),
        prefetch: 2,
        cancellationToken: cancellationToken);
```

预取回调在线程池执行，最多保留 `MaximumConcurrency + prefetch` 个已准备项目，并保持源顺序。它不改变模型 shape，也不会绕过单个有状态 Session 的串行约束。回调应返回不可变的 `PreparedVisualInput`，并明确其所有权；不应在回调完成后复用或修改同一底层缓冲。

## 结果类型

| 任务 | batch-one 结果 | 真正 Batch 结果 |
| --- | --- | --- |
| 分类 | `ClassificationResult` | `ClassificationBatchResult` |
| 检测 | `DetectionResult` | `DetectionBatchResult` |
| 语义分割 | `SemanticSegmentationResult` | `SemanticSegmentationBatchResult` |
| 实例分割 | `InstanceSegmentationResult` | `InstanceSegmentationBatchResult` |
| 姿态 | `PoseEstimationResult` | `PoseEstimationBatchResult` |
| OBB | `OrientedDetectionResult` | `OrientedDetectionBatchResult` |
| 异常检测 | `AnomalyDetectionResult` | `AnomalyDetectionBatchResult` |
| RMBG | `BackgroundRemovalResult` | `BackgroundRemovalBatchResult` |

Batch 结果按输入行索引访问；每行携带自己的源图变换。固定 batch-one 任务即使通过 Session 池并发，也仍返回 batch-one 结果列表，不会包装成 `*BatchResult`。

## 取消、超时和释放

所有异步入口都接受 `CancellationToken`。取消会停止未开始的准备和排队工作，并等待已经进入 native 的调用安全退出；不会发布半成品结果。释放 Pipeline 前应停止新请求，等待活动调用完成，再释放拥有的 Session 池。`BackendRegistry` 由应用统一拥有，不能在 Pipeline 仍使用时释放。

## 测量协议

报告至少记录：模型制品 SHA-256、输入尺寸和数量、真正 batch 大小、Session 数量、每 Session 线程数、预热次数、计时次数、预处理/推理/后处理/总耗时的 mean/P50/P95、托管分配，以及 GPU 驱动、CUDA/TensorRT/OpenVINO 版本和锁频/功耗状态。冷启动和稳态结果分开记录；不同输入、设备或运行时不要放在同一行比较。
