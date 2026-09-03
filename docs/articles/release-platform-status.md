# 发布与平台状态

## 当前版本

DeploySharp 2.0.0-alpha.1 是面向 Windows 的开源预览版本，当前优先保证 Windows 10/11 x64。Linux、macOS、ARM、NPU 及未验证的 GPU 组合暂不作兼容性承诺；公共 API 在 Alpha 阶段仍可能调整。

ModelFactory 目录当前包含 42 个 Preview 条目。Preview 是可下载的实验性资产，下载时按 ModelPack、文件大小和 SHA-256 校验；查询时需显式开启 includePreview。

## Windows 后端状态

| 组件 | 目标框架范围 | 当前 Windows 状态 |
| --- | --- | --- |
| Core、Visual | net46-net481、netstandard2.0、netcoreapp3.1、net5.0-net10.0 | 已完成声明框架的托管构建与测试 |
| ModelPack.Json、ModelFactory | netstandard2.0、net8.0、net9.0、net10.0 | 目录、缓存和纯包示例可用 |
| LLM、LlamaSharp | 以各包声明为准，至 net10.0 | GGUF CPU 路径已验证 |
| ONNX Runtime | netstandard2.0、net8.0 | Windows x64 CPU 模型推理已验证 |
| OpenVINO | net46-net481、netcoreapp3.1、net5.0-net10.0 | Windows x64 CPU 模型推理已验证 |
| Visual.OpenCV | net46-net481、netcoreapp3.1、net5.0-net10.0 | Windows x64 图像加载与预处理已验证 |
| OpenCV DNN | net46-net481、netcoreapp3.1、net5.0-net10.0 | CPU 数值张量合同、动态 shape 专门化和辅助输入已覆盖；具体模型仍以矩阵为准 |
| TensorRT | net8.0 | Windows x64 CUDA/TensorRT 11 路径已验证，RMBG 2.0 dynamic-int8 除外 |
| Multimodal | netstandard2.0、netcoreapp3.1、net5.0-net10.0 | 托管编排、流式生成、取消和纯包示例已验证 |

具体模型与后端结果见[模型与后端验证矩阵](../model-backend-verification-matrix.md)。矩阵中的 “—” 表示尚未验证或不适用，不代表已支持。

## Alpha 收尾

发布 Windows Alpha 前建议在干净环境中完成一次 locked restore、Release 构建、测试和纯包示例，并确认 README、模型目录、案例和包版本保持一致。模型来源/许可证材料不作为本版本的额外准入门槛，但第三方库仍应按其原始许可使用。

## 暂缓范围

- Linux、macOS、Windows ARM64、Android 和其他 RID；
- 未验证的 NPU、GPU 或第三方 Provider；
- 稳定版/长期支持承诺；
- NuGet.org 稳定发布及跨平台兼容矩阵。

这些工作可在 Windows Alpha 基线稳定后继续推进。
