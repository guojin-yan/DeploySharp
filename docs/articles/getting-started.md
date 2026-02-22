# Getting Started with DeploySharp
# DeploySharp 入门指南

## Overview / 概述

**DeploySharp** is a cross-platform deep learning model deployment framework for C# developers. It supports multiple inference backends (OpenVINO, ONNX Runtime, TensorRT) and various computer vision models.

**DeploySharp** 是一个面向C#开发者的跨平台深度学习模型部署框架。它支持多种推理后端（OpenVINO、ONNX Runtime、TensorRT）和各种计算机视觉模型。

## Prerequisites / 先决条件

- .NET 6.0, 8.0, 9.0 or .NET Framework 4.8+
- Visual Studio 2022 or VS Code with C# Dev Kit
- Basic knowledge of deep learning and computer vision

## Installation / 安装

### Install via NuGet / 通过 NuGet 安装

```bash
# Core library / 核心库
dotnet add package JYPPX.DeploySharp

# ImageSharp extension / ImageSharp 扩展
dotnet add package JYPPX.DeploySharp.ImageSharp

# OpenCvSharp extension / OpenCvSharp 扩展
dotnet add package JYPPX.DeploySharp.OpenCvSharp
```

### Backend Dependencies / 后端依赖

#### OpenVINO Setup / OpenVINO 设置

```bash
dotnet add package OpenVINO.runtime.win
```

#### ONNX Runtime Setup / ONNX Runtime 设置

```bash
dotnet add package Microsoft.ML.OnnxRuntime
```

## Quick Start Example / 快速开始示例

### Object Detection with YOLOv8 / 使用 YOLOv8 进行目标检测

```csharp
using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using DeploySharp.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// 1. Load image / 加载图像
using var image = Image.Load<Rgb24>("image.jpg");

// 2. Create model configuration / 创建模型配置
var config = new Yolov8DetConfig("yolov8n.onnx")
{
    ConfidenceThreshold = 0.5f,
    NmsThreshold = 0.45f
};

// 3. Create and load model / 创建并加载模型
using var model = new Yolov8DetModel(config);

// 4. Run inference / 运行推理
var results = model.Predict(image);

// 5. Process results / 处理结果
foreach (var detection in results)
{
    Console.WriteLine($"Class: {detection.Category}");
    Console.WriteLine($"Confidence: {detection.Confidence:F2}");
    Console.WriteLine($"Bounding Box: {detection.Bounds}");
}

// 6. Visualize results / 可视化结果
var visualized = Visualize.DrawDetResult(results, image, new VisualizeOptions());
visualized.Save("result.jpg");
```

## Project Structure / 项目结构

```
MyDeploySharpApp/
├── Models/
│   └── yolov8n.onnx          # Model files / 模型文件
├── Images/
│   └── test.jpg              # Test images / 测试图像
├── Program.cs                # Entry point / 入口点
└── MyDeploySharpApp.csproj   # Project file / 项目文件
```

## Next Steps / 下一步

- [Installation Guide](installation.md) - Detailed installation instructions
- [Object Detection](object-detection.md) - Complete detection tutorial
- [Best Practices](best-practices.md) - Performance optimization tips

## Troubleshooting / 故障排除

### Common Issues / 常见问题

| Issue | Solution |
|-------|----------|
| Model loading fails | Check model path and format |
| CUDA out of memory | Reduce batch size or image resolution |
| Slow inference | Enable GPU acceleration |

### Getting Help / 获取帮助

- GitHub Issues: [https://github.com/guojin-yan/DeploySharp/issues](https://github.com/guojin-yan/DeploySharp/issues)
- QQ Group: 945057948
