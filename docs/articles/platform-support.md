# 平台与后端支持

本文是 DeploySharp 2.0.0 源码关于目标框架、操作系统和推理后端的统一入口。这里的“可构建”仅表示托管程序集能够编译；只有完成真实模型推理验证的组合才算“已验证”。版本能力清单和已知边界见 [2.0.0 版本说明](../releases/2.0.0.md)。

## 当前发布范围

| 项目 | 当前状态 |
| --- | --- |
| Windows 10/11 x64 | 本版本的主要开发与验证平台 |
| Linux x64 | 可以构建，尚未纳入本版本完整运行验证 |
| macOS x64 / ARM64 | 可以构建，尚未纳入本版本完整运行验证 |
| Windows ARM64 | 尚未纳入本版本验证 |

跨平台使用时，还需要确认所选后端的原生库、驱动和模型格式在目标设备上可用。

2.0.0 的发布验证范围是 Windows 10/11 x64。Linux、macOS、Windows ARM64、移动平台、NPU 和未验证的 GPU 组合可以构建或存在适配代码，但不作本版本的运行兼容性承诺。只有验证矩阵中标记为“通过”的精确模型、工件、输入合同和后端组合，才表示完成了真实推理验证。

## 目标框架

| 包组 | 实际目标框架 |
| --- | --- |
| Core、Visual | <code>net46</code>-<code>net481</code>、<code>netstandard2.0</code>、<code>netcoreapp3.1</code>、<code>net5.0</code>-<code>net10.0</code> |
| Extensibility | <code>net46</code>、<code>netstandard2.0</code>、<code>net8.0</code>、<code>net10.0</code> |
| Visual.OpenCV、Backend.OpenCV、Backend.OpenVINO | <code>net46</code>-<code>net481</code>、<code>netcoreapp3.1</code>、<code>net5.0</code>-<code>net10.0</code>（不发布 <code>netstandard2.0</code>） |
| LLM、Multimodal | <code>netstandard2.0</code>、<code>netcoreapp3.1</code>、<code>net5.0</code>-<code>net10.0</code> |
| ModelPack.Json、ModelFactory | <code>netstandard2.0</code>、<code>net8.0</code>、<code>net9.0</code>、<code>net10.0</code> |
| Backend.OnnxRuntime、Backend.LlamaSharp | <code>netstandard2.0</code>、<code>net8.0</code> |
| Backend.TensorRT、Visual.TensorRT | <code>net8.0</code> |

建议新项目优先使用 .NET 10；需要兼容现有应用时可选择 .NET 8。Visual.TensorRT 和 Backend.TensorRT 当前只提供 net8.0 资产。

目标框架表描述程序集资产，不能推断每个资产都有相同的功能范围：Visual 的 `net46`/`net461` 不包含 ROI、滑动窗口检测与配置 JSON；内置 PaddleOCR-VL tokenizer 和基于 Microsoft.ML.Tokenizers 的公式 tokenizer 要求 `net8.0`、`net9.0` 或 `net10.0`。此外，部分当前依赖对 `netcoreapp3.1`、`net5.0`、`net6.0`、`net7.0` 会产生厂商 TFM 支持警告；这些资产可以构建，但不属于依赖厂商已测试的运行配置。旧框架应用应逐项验证，优先使用 .NET 8/10。

## 推理后端

| 后端 | 主要设备 | 当前说明 |
| --- | --- | --- |
| ONNX Runtime | CPU、CUDA | CPU 是通用路径；CUDA 需要匹配版本的 GPU Provider、CUDA、cuDNN 和驱动 |
| OpenVINO | CPU，部分 Intel 设备 | 支持 ONNX 与 OpenVINO IR；设备名称由应用显式配置 |
| OpenCV DNN | CPU，部分 OpenCV Target | 适合 OpenCV 可导入的 ONNX 图；动态 Shape 或未实现算子可能无法加载 |
| TensorRT | NVIDIA GPU | 使用预构建 Engine；Engine 必须与 TensorRT、CUDA、精度和目标 GPU 兼容 |
| LlamaSharp | CPU、CUDA、Vulkan 等原生后端 | 用于 GGUF 大语言模型，实际能力取决于随应用部署的原生后端包 |

同一个模型并不一定适用于所有后端。选择前请同时检查模型格式、动态输入、辅助输入、前后处理合同和运行时版本。

## 安装组合的判断方式

平台兼容性需要同时满足三层条件：

1. **托管层**：应用目标框架能够引用 DeploySharp 包；
2. **原生层**：对应 RID 的 runtime、驱动和 ABI 能在进程中加载；
3. **模型层**：工件格式、输入输出合同、动态 shape 和 decoder 在该后端有证据。

例如，`net8.0` 项目成功还原 `Backend.TensorRT` 只说明托管层通过；没有与 GPU、CUDA、TensorRT 版本匹配的 Engine 和 DLL，不能称为 TensorRT 可用。相反，ONNX Runtime CPU 可能在没有 GPU 的机器上完成完整验证，但仍应记录 CPU 型号和线程配置。

## 新设备的最小验证

在新 Windows x64 设备上，建议按顺序执行：

```powershell
dotnet --info
dotnet run --project tools/DeploySharp.VisualBenchmark/DeploySharp.VisualBenchmark.csproj -c Release -- --backend onnxruntime --kind all
dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release
```

然后再测试 OpenVINO、OpenCV 或 TensorRT。每个后端都应保存 preflight 输出、模型/测试图 SHA-256 和失败原因；“未安装”“未测试”和“导入器不支持”在矩阵中分别记录。

## 目标框架消费注意事项

直接构建 TFM 与应用能够间接消费的 TFM 不是同一概念。应用选择兼容资产后仍需验证图像库和 native runtime；旧 .NET Framework 应用尤其要检查 TLS、文件路径编码和 x64 进程。不要为绕过一次还原错误而把所有包改成 `netstandard2.0`，这会掩盖后端真实约束。

## 如何判断是否可用

- 查看[模型支持状态](model-support.md)，确认模型族已进入公开范围。
- 查看[模型与后端验证矩阵](../model-backend-verification-matrix.md)，确认具体模型与后端组合的实测状态。
- 查看[设备性能实测](device-performance-benchmarks.md)，了解已有设备上的延迟、吞吐和测试条件。
- 未验证状态不等同于不可用；不支持状态表示当前合同、算子或运行时存在已知限制。

## 相关文档

- [安装指南](installation.md)
- [ONNX Runtime 入门](onnxruntime-getting-started.md)
- [OpenVINO 入门](openvino-getting-started.md)
- [OpenCV 图像输入与 DNN 后端](visual-opencv-compatibility.md)
- [TensorRT CUDA 视觉推理](tensorrt-cuda-visual.md)
