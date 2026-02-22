# Installation Guide
# 安装指南

## Package Overview / 包概述

DeploySharp is organized into multiple NuGet packages for flexibility:

DeploySharp 分为多个 NuGet 包以提供灵活性：

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `JYPPX.DeploySharp` | Core library | ONNX Runtime |
| `JYPPX.DeploySharp.ImageSharp` | ImageSharp extension | ImageSharp |
| `JYPPX.DeploySharp.OpenCvSharp` | OpenCvSharp extension | OpenCvSharp4 |

## Installation Scenarios / 安装场景

### Scenario 1: OpenVINO + OpenCvSharp

For Intel CPU/GPU optimized inference with OpenCV image processing.

适用于 Intel CPU/GPU 优化推理与 OpenCV 图像处理。

```bash
dotnet add package JYPPX.DeploySharp
dotnet add package JYPPX.DeploySharp.OpenCvSharp
dotnet add package OpenVINO.runtime.win
dotnet add package OpenCvSharp4.runtime.win
```

### Scenario 2: OpenVINO + ImageSharp

For Intel CPU/GPU optimized inference with pure .NET image processing.

适用于 Intel CPU/GPU 优化推理与纯 .NET 图像处理。

```bash
dotnet add package JYPPX.DeploySharp
dotnet add package JYPPX.DeploySharp.ImageSharp
dotnet add package OpenVINO.runtime.win
```

### Scenario 3: ONNX Runtime + OpenCvSharp

For cross-platform inference with OpenCV.

适用于跨平台推理与 OpenCV。

```bash
dotnet add package JYPPX.DeploySharp
dotnet add package JYPPX.DeploySharp.OpenCvSharp
dotnet add package OpenCvSharp4.runtime.win
```

### Scenario 4: ONNX Runtime + ImageSharp

For cross-platform inference with pure .NET.

适用于跨平台推理与纯 .NET。

```bash
dotnet add package JYPPX.DeploySharp
dotnet add package JYPPX.DeploySharp.ImageSharp
```

### Scenario 5: ONNX Runtime with OpenVINO EP + ImageSharp

For using OpenVINO through ONNX Runtime Execution Provider.

适用于通过 ONNX Runtime 执行提供程序使用 OpenVINO。

```bash
dotnet add package JYPPX.DeploySharp
dotnet add package JYPPX.DeploySharp.ImageSharp
dotnet add package Intel.ML.OnnxRuntime.OpenVino
```

### Scenario 6: ONNX Runtime with DirectML + ImageSharp

For Windows DirectML GPU acceleration.

适用于 Windows DirectML GPU 加速。

```bash
dotnet add package JYPPX.DeploySharp
dotnet add package JYPPX.DeploySharp.ImageSharp
dotnet add package Microsoft.ML.OnnxRuntime.DirectML
```

### Scenario 7: ONNX Runtime with CUDA + ImageSharp

For NVIDIA GPU acceleration (requires matching CUDA/cuDNN versions).

适用于 NVIDIA GPU 加速（需要匹配的 CUDA/cuDNN 版本）。

```bash
dotnet add package JYPPX.DeploySharp
dotnet add package JYPPX.DeploySharp.ImageSharp
dotnet add package Microsoft.ML.OnnxRuntime.Gpu
```

> **Note**: For CUDA versions, refer to [ONNX Runtime CUDA requirements](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html#requirements).

> **注意**：关于 CUDA 版本，请参考 [ONNX Runtime CUDA 要求](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html#requirements)。

## Platform-Specific Notes / 平台特定说明

### Windows

All packages are fully supported on Windows 10/11.

所有包在 Windows 10/11 上都完全支持。

### Linux

- OpenVINO runtime packages may require additional setup
- CUDA packages require NVIDIA drivers

### macOS

- Limited support for some native backends
- ONNX Runtime CPU works out of the box

## Verification / 验证

After installation, verify with a simple test:

安装后，使用简单测试验证：

```csharp
using System;
using DeploySharp.Model;

class Program
{
    static void Main()
    {
        Console.WriteLine($"DeploySharp Version: {typeof(ModelType).Assembly.FullName}");
        Console.WriteLine("Installation successful!");
    }
}
```

## Troubleshooting / 故障排除

| Issue | Solution |
|-------|----------|
| `DllNotFoundException` | Install corresponding runtime packages |
| `BadImageFormatException` | Check platform target (x64 vs x86) |
| Missing dependencies | Restore NuGet packages |

## Next Steps / 下一步

- [Getting Started](getting-started.md) - Write your first application
- [Best Practices](best-practices.md) - Optimize your deployment
