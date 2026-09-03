# 平台与后端支持

本文是 DeploySharp 2.0.0-alpha.1 关于目标框架、操作系统和推理后端的唯一事实来源。这里的“可构建”仅表示托管程序集能够编译；只有完成真实模型推理验证的组合才算“已验证”。版本能力清单和已知阻断见 [2.0.0-alpha.1 发布说明](../releases/2.0.0-alpha.1.md)，本页不重复维护另一份发布状态表。

## 当前发布范围

| 项目 | 当前状态 |
| --- | --- |
| Windows 10/11 x64 | Alpha 版本的主要开发与验证平台 |
| Linux x64 | 可以构建，尚未纳入本版本完整运行验证 |
| macOS x64 / ARM64 | 可以构建，尚未纳入本版本完整运行验证 |
| Windows ARM64 | 尚未纳入本版本验证 |

跨平台使用时，还需要确认所选后端的原生库、驱动和模型格式在目标设备上可用。

当前 Alpha 的发布基线是 Windows 10/11 x64。Linux、macOS、Windows ARM64、移动平台、NPU 和未验证的 GPU 组合可以构建或存在适配代码，但不作本版本的运行兼容性承诺。只有验证矩阵中标记为“通过”的精确模型、工件、输入合同和后端组合，才表示完成了真实推理验证。

## 目标框架

| 包组 | 实际目标框架 |
| --- | --- |
| Core、Visual | <code>net46</code>-<code>net481</code>、<code>netstandard2.0</code>、<code>netcoreapp3.1</code>、<code>net5.0</code>-<code>net10.0</code> |
| Visual.OpenCV、Backend.OpenCV、Backend.OpenVINO | <code>net46</code>-<code>net481</code>、<code>netcoreapp3.1</code>、<code>net5.0</code>-<code>net10.0</code>（不发布 <code>netstandard2.0</code>） |
| LLM、Multimodal | <code>netstandard2.0</code>、<code>netcoreapp3.1</code>、<code>net5.0</code>-<code>net10.0</code> |
| ModelPack.Json、ModelFactory | <code>netstandard2.0</code>、<code>net8.0</code>、<code>net9.0</code>、<code>net10.0</code> |
| Backend.OnnxRuntime、Backend.LlamaSharp | <code>netstandard2.0</code>、<code>net8.0</code> |
| Backend.TensorRT、Visual.TensorRT | <code>net8.0</code> |

建议新项目优先使用 .NET 10；需要兼容现有应用时可选择 .NET 8。Visual.TensorRT 和 Backend.TensorRT 当前只提供 net8.0 资产。

## 推理后端

| 后端 | 主要设备 | 当前说明 |
| --- | --- | --- |
| ONNX Runtime | CPU、CUDA | CPU 是通用路径；CUDA 需要匹配版本的 GPU Provider、CUDA、cuDNN 和驱动 |
| OpenVINO | CPU，部分 Intel 设备 | 支持 ONNX 与 OpenVINO IR；设备名称由应用显式配置 |
| OpenCV DNN | CPU，部分 OpenCV Target | 适合 OpenCV 可导入的 ONNX 图；动态 Shape 或未实现算子可能无法加载 |
| TensorRT | NVIDIA GPU | 使用预构建 Engine；Engine 必须与 TensorRT、CUDA、精度和目标 GPU 兼容 |
| LlamaSharp | CPU、CUDA、Vulkan 等原生后端 | 用于 GGUF 大语言模型，实际能力取决于随应用部署的原生后端包 |

同一个模型并不一定适用于所有后端。选择前请同时检查模型格式、动态输入、辅助输入、前后处理合同和运行时版本。

## 如何判断是否可用

- 查看[模型支持状态](model-support.md)，确认模型族已进入公开范围。
- 查看[模型与后端验证矩阵](../model-backend-verification-matrix.md)，确认具体模型与后端组合的实测状态。
- 查看[设备性能实测](device-performance-benchmarks.md)，了解已有设备上的延迟、吞吐和测试条件。
- 未验证状态不等同于不可用；不支持状态表示当前合同、算子或运行时存在已知限制。

## 相关文档

- [安装指南](installation.md)
- [ONNX Runtime 入门](onnxruntime-getting-started.md)
- [OpenVINO 入门](openvino-getting-started.md)
- [OpenCV DNN 入门](visual-opencv-getting-started.md)
- [TensorRT CUDA 视觉推理](tensorrt-cuda-visual.md)
