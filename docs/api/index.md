# API Reference
# API 参考文档

Welcome to the DeploySharp API Reference documentation. This section provides detailed documentation for all public APIs in the DeploySharp framework.

欢迎使用 DeploySharp API 参考文档。本节提供了 DeploySharp 框架中所有公共 API 的详细文档。

## Namespaces / 命名空间

### Core Namespaces / 核心命名空间

| Namespace | Description | 描述 |
|-----------|-------------|------|
| DeploySharp | Root namespace containing core types | 包含核心类型的根命名空间 |
| DeploySharp.Common | Common utilities and base types | 通用工具类和基类 |
| DeploySharp.Data | Data processing and transformation | 数据处理和转换 |
| DeploySharp.Engine | Runtime execution engines | 运行时执行引擎 |
| DeploySharp.Logger | Logging and diagnostics | 日志记录和诊断 |
| DeploySharp.Model | Model interfaces and metadata | 模型接口和元数据 |

### Data Sub-namespaces / 数据子命名空间

| Namespace | Description | 描述 |
|-----------|-------------|------|
| DeploySharp.Data.ImageData | Image data structures | 图像数据结构 |
| DeploySharp.Data.ProcessData | Processing data types | 处理数据类型 |
| DeploySharp.Data.Processor | Data processors | 数据处理器 |
| DeploySharp.Data.ResultData | Result data types | 结果数据类型 |

### Model Sub-namespaces / 模型子命名空间

| Namespace | Description | 描述 |
|-----------|-------------|------|
| DeploySharp.Model.Config | Model configurations | 模型配置 |
| DeploySharp.Model.ModelService | Model service interfaces | 模型服务接口 |

### Extension Libraries / 扩展库

| Namespace | Description | 描述 |
|-----------|-------------|------|
| DeploySharp.ImageSharp | ImageSharp integration | ImageSharp 集成 |
| DeploySharp.ImageSharp.Data | ImageSharp data extensions | ImageSharp 数据扩展 |
| DeploySharp.ImageSharp.Model | ImageSharp model implementations | ImageSharp 模型实现 |
| DeploySharp.OpenCvSharp | OpenCvSharp integration | OpenCvSharp 集成 |
| DeploySharp.OpenCvSharp.Data | OpenCvSharp data extensions | OpenCvSharp 数据扩展 |
| DeploySharp.OpenCvSharp.Model | OpenCvSharp model implementations | OpenCvSharp 模型实现 |

## Quick Reference / 快速参考

### Getting Started / 开始使用

```csharp
// 1. Create model configuration / 创建模型配置
var config = new Yolov8DetConfig("model.onnx");

// 2. Create inference engine / 创建推理引擎
var engine = InferEngineFactory.CreateEngine(InferenceBackend.OpenVINO);

// 3. Load model / 加载模型
engine.LoadModel(ref config);

// 4. Prepare input / 准备输入
var input = new DataTensor(imageData);

// 5. Run inference / 运行推理
var result = engine.Predict(input);

// 6. Process results / 处理结果
foreach (var detection in result.Detections)
{
    Console.WriteLine($"Detected: {detection.Category} at {detection.Bounds}");
}
```

## Type Hierarchy / 类型层次结构

```
DeploySharp
├── Common
│   ├── DeploySharpException
│   └── Speed
├── Data
│   ├── DataTensor
│   ├── ImageData
│   │   ├── ImageData<T>
│   │   ├── ImageDataB
│   │   └── ImageDataF
│   ├── ProcessData
│   │   └── BoundingBox
│   ├── Processor
│   │   └── DataProcessorConfig
│   └── ResultData
│       ├── Result
│       ├── DetResult
│       ├── SegResult
│       ├── ObbResult
│       └── KeyPointResult
├── Engine
│   ├── IModelInferEngine
│   ├── OnnxRuntimeInferEngine
│   ├── OpenVinoInferEngine
│   └── TensorRtInferEngine
├── Logger
│   └── LogManager
└── Model
    ├── ModelType
    ├── IConfig
    └── ModelService
        └── IModel<T>
```

## See Also / 另请参阅

- [GitHub Repository](https://github.com/guojin-yan/DeploySharp)
- [NuGet Packages](https://www.nuget.org/packages?q=JYPPX.DeploySharp)
- [Issue Tracker](https://github.com/guojin-yan/DeploySharp/issues)
