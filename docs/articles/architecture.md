# Architecture / 架构

DeploySharp separates stable contracts, domain workflows, managed backend adapters, and native runtimes. / DeploySharp 将稳定契约、领域流程、托管后端适配器与原生运行时分层隔离。

| Layer / 层 | Responsibility / 职责 |
|---|---|
| `JYPPX.DeploySharp.Core` | Framework-neutral contracts, tensors, results, errors, logging, and registry / 框架无关的契约、张量、结果、错误、日志与注册中心 |
| `JYPPX.DeploySharp.Visual` | Prepared-tensor workflows, reversible geometry, profiles, and decoders; no pixel or image-library dependency / 已准备张量流程、可逆几何、Profile 与解码器；不依赖像素处理或图像库 |
| `JYPPX.DeploySharp.LLM` | Language-model workflows and generation contracts / 大语言模型流程与生成契约 |
| `JYPPX.DeploySharp.Multimodal` | Backend-neutral ordered-media orchestration, streaming and lifecycle / 后端中立的有序媒体编排、流式与生命周期 |
| `JYPPX.DeploySharp.Backend.*` | Optional managed adapters for inference engines / 可选的推理引擎托管适配器 |
| Application runtime packages / 应用运行时包 | Platform and execution-provider native assets selected by the final application / 由最终应用选择的平台与 Execution Provider 原生资产 |

Core does not reference an inference engine or imaging library. Domain packages select behavior through Core contracts, while applications compose concrete providers at startup. / Core 不引用推理引擎或图像库；领域包通过 Core 契约表达行为，由应用在启动时组合具体后端提供程序。

Visual receives the output of an external image adapter through `PreparedVisualInput`. The current official preview adapter is `JYPPX.DeploySharp.Visual.OpenCV`; alternative adapters remain separate and never multiply with backend packages. `JYPPX.DeploySharp.Backend.OpenCV` is a separate OpenCV DNN inference backend with an explicit static tensor contract. / Visual 通过 `PreparedVisualInput` 接收外部图像适配器的输出。当前官方 preview 适配器为 `JYPPX.DeploySharp.Visual.OpenCV`；其他适配器保持独立，不与后端组合成乘积包。`JYPPX.DeploySharp.Backend.OpenCV` 是具有显式静态张量合同的独立 OpenCV DNN 推理后端。

The ONNX Runtime adapter depends only on the official managed API package. Applications install CPU, CUDA, or DirectML native packages explicitly; only CPU is currently admitted and tested. See ADR 0009. / ONNX Runtime 适配器只依赖官方托管 API 包。应用显式安装 CPU、CUDA 或 DirectML 原生包；当前只有 CPU 通过准入与测试。详见 ADR 0009。

The independent Multimodal package is additive: canonical result DTOs remain in Core, orchestration lives in Multimodal, and native/model ownership remains in the application. OpenCV DNN is implemented as a separate Windows CPU preview backend under ADR 0036. See the [Multimodal/OpenCV guide](multimodal-opencv-dnn.md) and [release status](release-platform-status.md). / 独立 Multimodal 包为增量边界：规范结果 DTO 继续位于 Core，编排位于 Multimodal，native/model 所有权保留在应用。OpenCV DNN 按 ADR 0036 实现为独立 Windows CPU preview 后端。详见[多模态/OpenCV 指南](multimodal-opencv-dnn.md)与[发布状态](release-platform-status.md)。
