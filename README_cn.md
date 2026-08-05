# DeploySharp V2.0

DeploySharp 是一套正在重构中的模块化 .NET 深度学习模型部署工具。

DeploySharp 将算法速度和官方模型保真作为产品要求。后端微型合同夹具只能证明适配器行为；只有预处理、张量解释和后处理通过可复现黄金对照与官方实现一致后，具体模型才会标记为支持。性能测量会拆分预处理、主机/设备传输、后端执行、后处理和端到端耗时，并在保留旧框架兼容的同时为现代 TFM 提供经过测量的优化路径。

V2 将稳定契约、领域流程、托管后端适配器和平台原生运行时完全拆分，不提供与 DeploySharp V1 的源码、二进制、配置或行为兼容。

当前基础包包括 `JYPPX.DeploySharp.Core`、`JYPPX.DeploySharp.Visual`、`JYPPX.DeploySharp.LLM`、`JYPPX.DeploySharp.Backend.LlamaSharp`、`JYPPX.DeploySharp.Backend.OnnxRuntime`、`JYPPX.DeploySharp.Backend.OpenVINO`、`JYPPX.DeploySharp.ModelPack.Json` 和 `JYPPX.DeploySharp.ModelFactory`。Core 不依赖推理框架和图像库，提供推理契约、张量、模型元数据、统一结果 DTO、诊断、错误和显式后端注册机制；Visual 提供不绑定图像库的已准备张量流程、可逆几何、分类、稠密检测、语义分割、确定性 RLE 和 NMS，不引用 OpenCV 或具体推理后端；LLM 提供聊天、流式生成、取消和 Embedding；LLamaSharp 后端加载 GGUF，但不打包原生运行时；ONNX Runtime 与 OpenVINO 后端执行真实命名张量 CPU 推理，同时由应用持有原生运行时包；ModelPack.Json 提供严格的模型清单与本地完整性校验；ModelFactory 提供经过审核的目录、不可变 GitHub Release 下载、内容寻址缓存和离线复用，但不打包模型权重。

请先阅读 [本地 LLM 快速开始](docs/articles/llm-getting-started.md)、[LLamaSharp 原生后端指南](docs/articles/llamasharp-native-backends.md) 和 [LLM 兼容性与生命周期](docs/articles/llamasharp-compatibility.md)。
视觉流程请阅读 [Visual 已准备张量快速开始](docs/articles/visual-getting-started.md)、[Visual 坐标与解码](docs/articles/visual-coordinate-decoding.md)、[Visual 语义分割](docs/articles/visual-semantic-segmentation.md) 和 [Visual 生命周期与兼容性](docs/articles/visual-lifecycle-compatibility.md)。项目级门禁见 [性能与模型保真](docs/articles/performance-and-model-fidelity.md)。

模型清单和下载流程请阅读 [ModelPack JSON 快速开始](docs/articles/modelpack-json-getting-started.md)、[ModelFactory 快速开始](docs/articles/modelfactory-getting-started.md) 与 [官方模型目录](docs/articles/model-catalog.md)。官方目录在获得真实 Release 和模型再分发授权前保持为空。

ONNX CPU 推理请阅读 [ONNX Runtime 快速开始](docs/articles/onnxruntime-getting-started.md) 与 [兼容性和生命周期](docs/articles/onnxruntime-compatibility.md)。

OpenVINO CPU 推理请阅读 [OpenVINO 快速开始](docs/articles/openvino-getting-started.md) 与 [兼容性和生命周期](docs/articles/openvino-compatibility.md)。

## 当前状态

`2.0.0-alpha.1` 是早期架构基线，在首个 RC 版本前公共 API 仍可能调整。

## 许可证

Apache-2.0。
