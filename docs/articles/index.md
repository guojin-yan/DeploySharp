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
| 下载并验证模型 | [ModelFactory 与 CLI](modelfactory-getting-started.md) | [官方模型目录](model-catalog.md) |
| 查询模型可用性 | [模型支持状态](model-support.md) | [模型与后端验证矩阵](../model-backend-verification-matrix.md) |
| 复现性能数据 | [性能基准方法](performance-benchmarking.md) | [设备性能实测](device-performance-benchmarks.md) |
| 比较 ROI 多后端性能 | [设备性能实测：ROI](device-performance-benchmarks.md#roi-与滑动窗口) | [异步帧流水线与滑动窗口](visual-async-and-sliding-window.md) |
| 设计批量与并发 | [Batch、Session 池与并发](batch-session-concurrency.md) | 对应视觉任务指南 |
| 选择 NuGet 包组合 | [NuGet 包组合与安装指南](package-combinations.md) | 按任务和后端安装最小组合 |

## 阅读约定

- 只有标为“通过”的模型与后端组合才表示在相应测试条件下完成真实推理。
- “当前不支持”和“未测试”不是失败的同义词；前者已有明确限制，后者表示没有可复现证据。
- 性能结果只代表列出的设备、模型制品、运行时和测试协议，不能外推为通用性能承诺。
- 视觉任务优先从 `Visual` 指南进入；模型制品下载与完整性校验优先从 `ModelFactory` 指南进入。

## 推荐阅读顺序

第一次使用时，建议按下面的顺序完成一次最小闭环，而不是同时阅读所有任务页：

1. 在[安装与运行时](installation.md)确认目标框架、进程位数和 native runtime。
2. 用[快速开始](getting-started.md)创建一个最小张量 Session，先证明模型和后端能够加载。
3. 处理图像时阅读 [Visual 快速开始](visual-getting-started.md)，再进入具体任务页。任务页中的 Profile、输入尺寸、输出名称和标签属于模型合同，不能只复制类名。
4. 需要连续视频、批量图片或大图切片时，先看[Batch、Session 池与并发](batch-session-concurrency.md)，再看[异步帧流水线与滑动窗口](visual-async-and-sliding-window.md)。
5. 下载模型时先看[官方模型目录](model-catalog.md)，然后使用 [ModelFactory](modelfactory-getting-started.md) 或 CLI。下载成功不代表后端已经通过端到端验证。
6. 发布前按[性能基准方法](performance-benchmarking.md)记录预处理、模型执行、解码和总耗时，并把模型工件、运行时版本和设备写进结果。

## 遇到问题先排哪一层

| 现象 | 先检查 | 不要先做的事 |
| --- | --- | --- |
| DLL 加载失败 | 进程架构、RID、native DLL 搜索路径和运行时版本 | 不要反复更换模型文件 |
| 模型能加载但输入失败 | 输入名称、元素类型、rank、动态维度和 Profile | 不要用 `First()` 猜输入端口 |
| 结果位置偏移 | 颜色顺序、缩放模式、归一化、`ImageTransform` 和坐标空间 | 不要在 decoder 外再缩放一次 |
| 单张很快、批量很慢 | 是否真正支持动态 batch、Session 池大小、内存峰值 | 不要无界创建 `Task.Run` |
| OpenCV 导入失败 | [OpenCV 兼容性](visual-opencv-compatibility.md)和矩阵中的工件状态 | 不要把 unsupported 当成后处理 bug |
| GPU 无法执行 | 驱动、CUDA/cuDNN/TensorRT 组合和设备编号 | 不要只根据包还原成功判断可用 |

## 版本和证据

本文档中的“支持”始终指模型、后端、设备和测试协议组合，而不是某个抽象接口存在。发布候选版本应同时保存：模型文件的 SHA-256、Profile 配置、输入图片、后端/驱动版本、warmup 次数、迭代次数以及 P50/P95。证据不足的组合会明确标为“未测试”或“不支持”，不会用理论兼容性替代实测。

## 维护者入口

维护代码前先读[架构概览](architecture.md)；迁移 V1 模型时读[V1 模型能力迁移](v1-model-migration.md)。仓库级 AI 工作流位于 `.agents/skills`，分别约束模型接入、后端验证、性能分析、包发布和文档治理。开发审计、逐次测试记录和临时排障脚本不属于公开文档。
