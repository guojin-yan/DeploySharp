---
uid: index
---

<!-- 此文件内容会自动从 README_cn.md 复制更新 -->
<!-- This file content is automatically copied from README_cn.md -->

# DeploySharp API 文档 / API Documentation

<p align="center">
  <img src="https://socialify.git.ci/guojin-yan/DeploySharp/image?description=1&descriptionEditable=💞%20Deploying%20Deep%20Learning%20Models%20On%20Multiple%20Platforms%20💞&logo=https%3A%2F%2Fs2.loli.net%2F2023%2F01%2F26%2FylE1K5JPogMqGSW.png&name=1&owner=1&pattern=Circuit%20Board&pulls=1&stargazers=1&theme=Light" alt="DeploySharp Logo" width="600"/>
</p>

<p align="center">
  <a href="./LICENSE.txt">
    <img src="https://img.shields.io/github/license/guojin-yan/openvinosharp.svg">
  </a>
  <a>
    <img src="https://img.shields.io/badge/Framework-.NET%2010.0%2C%20.NET%208.0%2C%20.NET%206.0%2C%20.NET%20Framework%204.8-pink.svg">
  </a>
</p>

## 🌐 语言 / Language

- [简体中文](#简体中文) | [English](#english)

---

## 简体中文

### 📚 简介

**DeploySharp** 是一个专为 C# 开发者设计的跨平台模型部署框架，提供从模型加载、配置管理到推理执行的端到端解决方案。

#### 核心特性

- **架构设计与功能分层**：模块化命名空间设计，显著降低深度学习模型的集成复杂度
- **多引擎支持**：原生支持 OpenVINO、ONNX Runtime、TensorRT 推理引擎
- **跨平台运行时**：兼容 .NET Framework 4.8 及 .NET 6/7/8/9/10
- **高性能推理**：异步推理支持，单张/批量图片推理模式
- **开发者支持**：中英双语代码注释，完善的示例代码库

### 🎨 模型支持列表

| 模型名称 | 模型类型 | OpenVINO | ONNX Runtime | TensorRT |
|:--------:|:--------:|:--------:|:------------:|:--------:|
| **YOLOv5** | 目标检测 | ✅ | ✅ | ✅ |
| **YOLOv5** | 实例分割 | ✅ | ✅ | ✅ |
| **YOLOv6** | 目标检测 | ✅ | ✅ | ✅ |
| **YOLOv7** | 目标检测 | ✅ | ✅ | ✅ |
| **YOLOv8** | 目标检测 | ✅ | ✅ | ✅ |
| **YOLOv8** | 实例分割 | ✅ | ✅ | ✅ |
| **YOLOv8** | 姿态估计 | ✅ | ✅ | ✅ |
| **YOLOv8** | 旋转框检测 | ✅ | ✅ | ✅ |
| **YOLOv9** | 目标检测 | ✅ | ✅ | ✅ |
| **YOLOv9** | 实例分割 | ✅ | ✅ | ✅ |
| **YOLOv10** | 目标检测 | ✅ | ✅ | ✅ |
| **YOLOv11** | 目标检测 | ✅ | ✅ | ✅ |
| **YOLOv11** | 实例分割 | ✅ | ✅ | ✅ |
| **YOLOv11** | 姿态估计 | ✅ | ✅ | ✅ |
| **YOLOv11** | 旋转框检测 | ✅ | ✅ | ✅ |
| **YOLOv12** | 目标检测 | ✅ | ✅ | ✅ |
| **YOLOv26** | 目标检测 | ✅ | ✅ | ✅ |
| **YOLOv26** | 实例分割 | ✅ | ✅ | ✅ |
| **YOLOv26** | 姿态估计 | ✅ | ✅ | ✅ |
| **YOLOv26** | 旋转框检测 | ✅ | ✅ | ✅ |
| **Anomalib** | 异常检测 | ✅ | ✅ | ✅ |
| **PP-YOLOE** | 目标检测 | ✅ | ✅ | ✅ |
| **DEIMv2** | 目标检测 | ✅ | ✅ | ✅ |
| **RFDETR** | 目标检测 | ✅ | ✅ | ✅ |
| **RFDETR** | 实例分割 | ✅ | ✅ | ✅ |
| **RTDETR** | 目标检测 | ✅ | ✅ | ✅ |
| **PP-OCR v4/v5** | 文字识别 | ✅ | ✅ | ✅ |

### 📖 文档导航

| 章节 | 描述 |
|------|------|
| [API 参考](api/) | 所有命名空间和类型的完整API文档 |
| [入门指南](articles/getting-started.md) | 快速开始使用DeploySharp |
| [安装指南](articles/installation.md) | 详细的安装说明 |
| [目标检测](articles/object-detection.md) | 目标检测教程 |
| [图像分割](articles/image-segmentation.md) | 图像分割教程 |
| [姿态估计](articles/pose-estimation.md) | 姿态估计教程 |
| [OCR](articles/ocr.md) | 文字识别教程 |
| [最佳实践](articles/best-practices.md) | 性能优化和最佳实践 |

### 🚀 快速开始

```csharp
using DeploySharp.Model;
using DeploySharp.ImageSharp.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// 加载图像
using var image = Image.Load<Rgb24>("photo.jpg");

// 创建模型配置
var config = new Yolov8DetConfig("yolov8n.onnx");

// 创建模型并推理
using var model = new Yolov8DetModel(config);
var results = model.Predict(image);

// 处理结果
foreach (var detection in results)
{
    Console.WriteLine($"检测到: {detection.Category}, 置信度: {detection.Confidence:F2}");
}
```

### 📦 NuGet 包

```shell
# 核心库
dotnet add package JYPPX.DeploySharp

# ImageSharp 扩展
dotnet add package JYPPX.DeploySharp.ImageSharp

# OpenCvSharp 扩展
dotnet add package JYPPX.DeploySharp.OpenCvSharp
```

---

## English

### 📚 Introduction

**DeploySharp** is a cross-platform model deployment framework designed for C# developers, offering end-to-end solutions from model loading and configuration management to inference execution.

#### Key Features

- **Modular Architecture**: Namespace-based design reduces complexity of integrating deep learning models
- **Multi-Engine Support**: Native integration with OpenVINO, ONNX Runtime, TensorRT
- **Cross-Platform**: Compatible with .NET Framework 4.8 and .NET 6/7/8/9/10
- **High Performance**: Asynchronous inference, single/batch image processing
- **Developer Friendly**: Bilingual code comments, comprehensive examples

### 🎨 Supported Models

| Model Name | Model Type | OpenVINO | ONNX Runtime | TensorRT |
|:----------:|:----------:|:--------:|:------------:|:--------:|
| **YOLOv5** | Detection | ✅ | ✅ | ✅ |
| **YOLOv5** | Segmentation | ✅ | ✅ | ✅ |
| **YOLOv6** | Detection | ✅ | ✅ | ✅ |
| **YOLOv7** | Detection | ✅ | ✅ | ✅ |
| **YOLOv8** | Detection | ✅ | ✅ | ✅ |
| **YOLOv8** | Segmentation | ✅ | ✅ | ✅ |
| **YOLOv8** | Pose | ✅ | ✅ | ✅ |
| **YOLOv8** | OBB | ✅ | ✅ | ✅ |
| **YOLOv9** | Detection | ✅ | ✅ | ✅ |
| **YOLOv9** | Segmentation | ✅ | ✅ | ✅ |
| **YOLOv10** | Detection | ✅ | ✅ | ✅ |
| **YOLOv11** | Detection | ✅ | ✅ | ✅ |
| **YOLOv11** | Segmentation | ✅ | ✅ | ✅ |
| **YOLOv11** | Pose | ✅ | ✅ | ✅ |
| **YOLOv11** | OBB | ✅ | ✅ | ✅ |
| **YOLOv12** | Detection | ✅ | ✅ | ✅ |
| **YOLOv26** | Detection | ✅ | ✅ | ✅ |
| **Anomalib** | Anomaly | ✅ | ✅ | ✅ |
| **PP-OCR** | OCR | ✅ | ✅ | ✅ |

### 📖 Documentation

| Section | Description |
|---------|-------------|
| [API Reference](api/) | Complete API documentation |
| [Getting Started](articles/getting-started.md) | Quick start guide |
| [Installation](articles/installation.md) | Installation instructions |
| [Object Detection](articles/object-detection.md) | Detection tutorial |
| [Image Segmentation](articles/image-segmentation.md) | Segmentation tutorial |
| [Pose Estimation](articles/pose-estimation.md) | Pose estimation tutorial |
| [OCR](articles/ocr.md) | OCR tutorial |
| [Best Practices](articles/best-practices.md) | Performance optimization |

### 🚀 Quick Start

```csharp
using DeploySharp.Model;
using DeploySharp.ImageSharp.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Load image
using var image = Image.Load<Rgb24>("photo.jpg");

// Create model configuration
var config = new Yolov8DetConfig("yolov8n.onnx");

// Create model and run inference
using var model = new Yolov8DetModel(config);
var results = model.Predict(image);

// Process results
foreach (var detection in results)
{
    Console.WriteLine($"Detected: {detection.Category}, Confidence: {detection.Confidence:F2}");
}
```

### 📦 NuGet Packages

```shell
# Core library
dotnet add package JYPPX.DeploySharp

# ImageSharp extension
dotnet add package JYPPX.DeploySharp.ImageSharp

# OpenCvSharp extension
dotnet add package JYPPX.DeploySharp.OpenCvSharp
```

---

## 🔗 Links / 链接

- [GitHub Repository](https://github.com/guojin-yan/DeploySharp)
- [NuGet Packages](https://www.nuget.org/packages?q=JYPPX.DeploySharp)
- [Issues](https://github.com/guojin-yan/DeploySharp/issues)
- [QQ Group: 945057948](http://qm.qq.com)

## 📄 License / 许可证

This project is licensed under the [Apache License 2.0](../LICENSE.txt).
本项目采用 [Apache License 2.0](../LICENSE.txt) 开源许可证。
