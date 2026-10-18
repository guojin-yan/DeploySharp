# PP-OCRv6 Small Linux RTX 2060 Prefetched Pipeline Benchmark

## 结果

远端 Ubuntu 主机以 PP-OCRv6 Small DET/REC、PP-OCRv5 Mobile CLS 和 ONNX Runtime CPU 完成完整 `DET → CLS → REC → merge`。每个计时样本处理同一组三张不同的 HierText JPEG，并通过 `OcrPipeline.RunPrefetchedAsync(prefetch: 2)` 并行准备输入。

| 测量项 | 结果 |
| --- | ---: |
| 协议 | 5 次预热 + 50 次计时 |
| 计时单位 | 一次三图完整异步预取流水线 |
| P50 | `2513.460 ms / 三图` |
| P95 | `2857.975 ms / 三图` |
| 均值 | `2568.837 ms / 三图` |
| 最小 / 最大 | `2319.842 / 3087.415 ms / 三图` |

50 次计时中的所有结果合同与基线相同，输入顺序保持不变，每张图都返回文本区域和识别结果。逐次原始样本、模型/输入 SHA 和环境字段见[机器报告](paddleocr-v6-small-session-pool-prefetch-linux-rtx2060-benchmark-20261018.json)。

## 测试环境与执行配置

| 项目 | 值 |
| --- | --- |
| 主机 | `ygj-NUC11PHi7` |
| 操作系统 | Ubuntu 22.04.5 LTS |
| CPU | Intel Core i7-1165G7，8 个逻辑 CPU |
| CPU governor | `powersave`；scaling min/max `400/4700 MHz` |
| GPU / 驱动 | NVIDIA GeForce RTX 2060 6 GB / `580.178.04` |
| 推理后端 | ONNX Runtime CPU；GPU 未参与推理 |
| ORT | `Microsoft.ML.OnnxRuntime 1.28.0` |
| .NET | `10.0.9` |
| 并发 | Pipeline 2；DET/CLS/REC 各 2 路独立 Session |
| Batch 上限 | DET 1、CLS 2、REC 16 |
| 输入预取 | `RunPrefetchedAsync(prefetch: 2)` |

## 计时边界与解释

每个 measured invocation 包含三张图片的文件读取/解码、预处理与张量准备、异步预取、DET/CLS/REC 执行，以及结果解码/合并；排除模型加载、Session 创建、初始尺寸探测和五次预热。该协议专门观察三图异步流水线，不是单图延迟，不能与已准备输入的 OCR `steady` 基准横向排名。`2.5 s` 级别的 P50 是每组三张图的批次墙钟，不是每张图的推理时间。

CPU governor 是 `powersave`，频率没有锁定；仅记录频率范围，没有测量整个运行期间的实际时钟、功耗或温度。RTX 2060 仅为主机设备信息，ORT 执行设备是 CPU；本报告不提供 CUDA/TensorRT 性能证据。设备较空闲也不代表严格隔离了操作系统调度。结果仅对应本报告精确模型、字典、输入、运行时、Session 配置和这台设备，不代表精度、全模型矩阵或其它后端。

源码基于提交 `7b03d77`，测试入口工作树有改动；JSON 记录了测试源码 SHA-256 `75fd525236f04cd323225be4977d8db84d4e69851b9594f4a5618389cc5ccab8` 与 dirty 状态。代码在该远端环境 Release 构建通过，外部测试 `1/1` 通过。
