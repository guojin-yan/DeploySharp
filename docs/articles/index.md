# 使用指南

本文档面向 `DeploySharp 2.0.0-alpha.1` 使用者，说明当前可验证的安装、推理、模型获取和性能复现路径。模型文件与原生运行时始终由应用显式选择和部署。

## 按任务选择入口

| 你要完成的事 | 从这里开始 | 下一步 |
| --- | --- | --- |
| 运行仓库或创建首个会话 | [快速开始](getting-started.md) | [安装与运行时](installation.md) |
| 了解版本边界 | [发布说明](../releases/2.0.0-alpha.1.md) | [平台与后端支持](platform-support.md) |
| 选择后端 | [平台与后端支持](platform-support.md) | [ONNX Runtime](onnxruntime-getting-started.md) 或 [OpenVINO](openvino-getting-started.md) |
| 编写视觉推理 | [Visual 快速开始](visual-getting-started.md) | 对应[视觉任务指南](visual-yolo-detection.md) |
| 处理视频或大图小目标 | [异步帧流水线与滑动窗口](visual-async-and-sliding-window.md) | [性能基准方法](performance-benchmarking.md) |
| 使用 GPU TensorRT 路径 | [TensorRT CUDA 视觉流水线](tensorrt-cuda-visual.md) | [TensorRT CUDA OCR](tensorrt-cuda-ocr.md) |
| 下载并验证模型 | [ModelFactory](modelfactory-getting-started.md) | [ModelFactory CLI](model-factory-cli.md) |
| 查询模型可用性 | [模型支持状态](model-support.md) | [模型与后端验证矩阵](../model-backend-verification-matrix.md) |
| 复现性能数据 | [性能基准方法](performance-benchmarking.md) | [设备性能实测](device-performance-benchmarks.md) |
| 设计批量与并发 | [Batch、Session 池与并发](batch-session-concurrency.md) | 对应视觉任务指南 |
| 选择 NuGet 包组合 | [NuGet 包组合与安装指南](package-combinations.md) | 按任务和后端安装最小组合 |

## 阅读约定

- 只有标为“通过”的模型与后端组合才表示在相应测试条件下完成真实推理。
- “当前不支持”和“未测试”不是失败的同义词；前者已有明确限制，后者表示没有可复现证据。
- 性能结果只代表列出的设备、模型制品、运行时和测试协议，不能外推为通用性能承诺。
- 视觉任务优先从 `Visual` 指南进入；模型制品下载与完整性校验优先从 `ModelFactory` 指南进入。
