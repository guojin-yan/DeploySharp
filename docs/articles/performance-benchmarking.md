# 推理性能测试

本页说明 DeploySharp 的统一测速方法。它用于比较同一模型、同一输入和同一设备上的后端差异，不把一次小型基准图的结果当成所有模型的性能承诺。

## 运行基准

在仓库根目录执行：

~~~powershell
dotnet run --project samples/07-benchmarks/InferenceSpeedBenchmark/InferenceSpeedBenchmark.csproj -c Release -- --backend all --warmup 10 --iterations 100 --output artifacts/benchmark.json
~~~

<code>--backend</code> 可选 <code>onnxruntime</code>、<code>opencv-dnn</code>、<code>openvino</code> 或 <code>all</code>。缺少原生运行时、驱动或模型时，报告会记录为 <code>unavailable</code>，不会伪装成通过。参数和输出格式可用下面的脚本快速检查：

~~~powershell
pwsh -NoProfile -File eng/benchmarks/Test-InferenceSpeedBenchmark.ps1
~~~

视觉模型的完整模型、后端和设备结果见[设备性能实测](device-performance-benchmarks.md)。

## 测量边界

| 项目 | 说明 |
| --- | --- |
| Warmup | 预热推理，不计入统计，用于稳定 JIT、内存分配器和后端缓存 |
| Min、P50、P95、Max | 同步模型调用的墙钟耗时分布，单位为毫秒 |
| Average、Throughput | 平均耗时和 <code>1000 / average_ms</code>；吞吐不是并发压力测试结果 |
| Managed allocation | 计时线程在一次推理中的托管分配字节数 |
| 环境信息 | 操作系统、进程架构、.NET 版本、后端、设备和运行时版本 |

计时循环不包含 Session 创建、模型解析/编译、原生库加载、模型下载、输入解码以及结果展示。比较两次结果时，模型文件、输入尺寸、精度、线程数、构建配置、预热次数和迭代次数必须保持一致。

## 批量与并发

对于 batch=1 的视觉模型，可为 Pipeline 配置多个独立 Session，再用 <code>RunManyAsync</code> 分派已准备输入。它不会把模型变成真正的 Batch，而是限制并发通道数量，并按输入顺序返回结果。

~~~csharp
using var pipeline = new VisualPipeline(
    registry,
    selection,
    request,
    new SessionOptions(maxConcurrency: 2));

IReadOnlyList<VisualInferenceResult> results = await pipeline.RunManyAsync(
    preparedInputs,
    new VisualExecutionOptions(timeout: TimeSpan.FromSeconds(30)),
    cancellationToken);
~~~

当 Profile 声明动态 batch 轴时，使用 <code>InferenceBatchScheduler</code> 生成一个真正的批量张量。Session 池大小决定同时执行的批次数；调度器会限制处于准备或执行状态的批次，避免大任务一次性保留全部张量。

~~~csharp
using var session = runtime.CreateSession(
    artifact,
    new BackendRequest(BackendCapabilities.TensorInference),
    new SessionOptions(maxConcurrency: 2));

var scheduler = new InferenceBatchScheduler<ImageItem, float[]>(
    session,
    maximumBatchSize: 8,
    prepareBatch: items => PrepareNchwFloatTensor(items),
    decodeBatch: (outputs, count) => DecodeScores(outputs, count));

IReadOnlyList<float[]> scores = await scheduler.RunAsync(items, cancellationToken);
~~~

<code>RunManyAsync</code> 适合固定 batch=1 的检测、分类和分割模型；<code>InferenceBatchScheduler</code> 适合 Profile、输入张量和 Decoder 都支持动态 batch 的模型。几何敏感任务应为每一行保留对应的 <code>VisualInputFrame</code>，这样解码坐标时无需重新准备图像。

## 视频与大图

视频逐帧处理可以用 <code>RunPrefetchedAsync</code>，在后端推理当前帧时准备后续帧。<code>prefetch</code> 应保持较小，并根据内存和设备队列实测调整；这只是准备与推理重叠，不会改变模型 batch。

大图小目标检测可以使用[异步帧流水线与滑动窗口](visual-async-and-sliding-window.md)中的切片、重叠和全局 NMS 组合。窗口重叠默认 20%，合并后的框会还原到原图坐标。窗口尺寸、重叠比例、是否执行整图通道和 NMS 阈值应和目标尺寸一起通过实测确定。

## 结果解读

- 同一设备上先比较 P50，再观察 P95 和分配量；P95 较高通常表示初始化、线程争用或内存压力。
- 推理时间与前处理、后处理分开记录。后端更换只影响模型调用阶段，图像解码和几何处理仍应单独优化。
- GPU 结果必须同时记录驱动、CUDA、TensorRT/OpenVINO 版本、功耗模式和锁频状态。
- <code>unavailable</code> 表示环境未满足运行条件；<code>unsupported</code> 表示模型合同或算子当前不被该后端支持；两者都不能用于性能排名。

## 相关文档

- [平台与后端支持](platform-support.md)
- [设备性能实测](device-performance-benchmarks.md)
- [模型与后端验证矩阵](../model-backend-verification-matrix.md)
- [使用教程](usage-tutorial.md)
- [异步帧流水线与滑动窗口](visual-async-and-sliding-window.md)
