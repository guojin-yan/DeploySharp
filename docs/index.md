# DeploySharp API Documentation
# DeploySharp API 文档

<p align="center">
  <img src="https://socialify.git.ci/guojin-yan/DeploySharp/image?description=1&descriptionEditable=💞%20Deploying%20Deep%20Learning%20Models%20On%20Multiple%20Platforms%20💞&logo=https%3A%2F%2Fs2.loli.net%2F2023%2F01%2F26%2FylE1K5JPogMqGSW.png" alt="DeploySharp Logo" width="600"/>
</p>

## 🌐 Language / 语言

- [English](#english)
- [中文](#中文)

---

## English

**DeploySharp** is a cross-platform model deployment framework designed for C# developers, offering end-to-end solutions from model loading and configuration management to inference execution.

### 📚 Documentation Structure

| Section | Description |
|---------|-------------|
| [API Reference](api/) | Complete API documentation for all namespaces and types |
| [Articles](articles/) | Tutorials, guides, and best practices |
| [GitHub Repository](https://github.com/guojin-yan/DeploySharp) | Source code and samples |

### 🚀 Quick Start

```csharp
using DeploySharp.Model;
using DeploySharp.Engine;
using DeploySharp.Data;

// Load model configuration
var config = new Yolov8DetConfig("path/to/model.onnx");

// Create inference engine
var engine = InferEngineFactory.CreateEngine(InferenceBackend.OpenVINO);
engine.LoadModel(ref config);

// Run inference
var result = engine.Predict(inputTensor);
```

### 📦 Supported Models

- **YOLO Series**: YOLOv5-v13 (Detection, Segmentation, Pose, OBB)
- **Anomalib**: Industrial anomaly detection
- **PaddleOCR**: Text detection and recognition
- **DEIMv2, RFDETR, RTDETR**: Advanced detection models

### 🔧 Supported Backends

| Backend | CPU | GPU | Notes |
|---------|-----|-----|-------|
| OpenVINO | ✅ | ✅ | Intel optimized |
| ONNX Runtime | ✅ | ✅ | Cross-platform |
| TensorRT | ✅ | ✅ | NVIDIA GPU only |

---

## 中文

**DeploySharp** 是一个为C#开发者设计的跨平台模型部署框架，提供从模型加载、配置管理到推理执行的端到端解决方案。

### 📚 文档结构

| 章节 | 描述 |
|------|------|
| [API 参考](api/) | 所有命名空间和类型的完整API文档 |
| [文章](articles/) | 教程、指南和最佳实践 |
| [GitHub 仓库](https://github.com/guojin-yan/DeploySharp) | 源代码和示例 |

### 🚀 快速开始

```csharp
using DeploySharp.Model;
using DeploySharp.Engine;
using DeploySharp.Data;

// 加载模型配置
var config = new Yolov8DetConfig("path/to/model.onnx");

// 创建推理引擎
var engine = InferEngineFactory.CreateEngine(InferenceBackend.OpenVINO);
engine.LoadModel(ref config);

// 运行推理
var result = engine.Predict(inputTensor);
```

### 📦 支持的模型

- **YOLO系列**: YOLOv5-v13 (目标检测、实例分割、姿态估计、旋转框检测)
- **Anomalib**: 工业异常检测
- **PaddleOCR**: 文本检测与识别
- **DEIMv2, RFDETR, RTDETR**: 高级检测模型

### 🔧 支持的后端

| 后端 | CPU | GPU | 说明 |
|------|-----|-----|------|
| OpenVINO | ✅ | ✅ | Intel优化 |
| ONNX Runtime | ✅ | ✅ | 跨平台 |
| TensorRT | ✅ | ✅ | 仅NVIDIA GPU |

---

## 📄 License

This project is licensed under the [Apache License 2.0](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV1.0/LICENSE.txt).

本项目采用 [Apache License 2.0](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV1.0/LICENSE.txt) 开源许可证。
