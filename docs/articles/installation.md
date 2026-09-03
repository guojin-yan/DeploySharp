# 安装与运行时

## 安装 DeploySharp

当前版本为源码优先的 `2.0.0-alpha.1`。NuGet 包尚未发布到公共源时，可以直接引用仓库项目；包 ID、职责和候选版本见根目录 README。

```xml
<ProjectReference Include="..\DeploySharp\src\DeploySharp.Core\DeploySharp.Core.csproj" />
```

使用视觉流程时，安装 `JYPPX.DeploySharp.Visual` 以及一个图像适配器（例如 `JYPPX.DeploySharp.Visual.OpenCV`）。后端包按应用实际需要单独安装。

## 后端运行时

DeploySharp 后端包主要包含托管适配器，不会静默安装厂商原生运行时。应用需要为目标 RID 准备与适配器匹配的运行时：

| 后端 | DeploySharp 适配器 | 应用需要准备 |
| --- | --- | --- |
| ONNX Runtime | `JYPPX.DeploySharp.Backend.OnnxRuntime` | `Microsoft.ML.OnnxRuntime` 或 CUDA 运行时包 |
| OpenVINO | `JYPPX.DeploySharp.Backend.OpenVINO` | Windows x64 OpenVINO runtime |
| OpenCV DNN | `JYPPX.DeploySharp.Backend.OpenCV` | `JYPPX.OpenCV.runtime.win-x64` 等匹配包 |
| TensorRT | `JYPPX.DeploySharp.Backend.TensorRT`、可选 `Visual.TensorRT` | CUDA、cuDNN、TensorRT、匹配 Engine/bridge |
| LLamaSharp | `JYPPX.DeploySharp.Backend.LlamaSharp` | LLamaSharp CPU/CUDA 原生后端与 GGUF |

例如，Windows x64 的 ONNX Runtime CPU 项目可以使用：

```powershell
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

模型文件、字典、Tokenizer、OpenVINO XML/BIN、TensorRT Engine 和原生 DLL 均由应用负责部署。请勿仅根据包能够还原来推断目标设备已经具备可运行的原生环境。

目标框架兼容性和已验证设备见[平台与后端支持](platform-support.md)；运行真实模型前请查阅[模型 × 后端验证矩阵](../model-backend-verification-matrix.md)。
