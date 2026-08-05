# DeploySharp V2.0

DeploySharp is a modular .NET model deployment toolkit under active V2 development.

The V2 architecture separates stable contracts, domain workflows, managed backend adapters, and platform-native runtimes. It does not provide source, binary, configuration, or behavioral compatibility with DeploySharp V1.

The first packages are `JYPPX.DeploySharp.Core`, `JYPPX.DeploySharp.Visual`, `JYPPX.DeploySharp.LLM`, `JYPPX.DeploySharp.Backend.LlamaSharp`, `JYPPX.DeploySharp.Backend.OnnxRuntime`, `JYPPX.DeploySharp.Backend.OpenVINO`, `JYPPX.DeploySharp.ModelPack.Json`, and `JYPPX.DeploySharp.ModelFactory`. Core contains dependency-free inference contracts, tensors, model metadata, canonical result DTOs, diagnostics, errors, and explicit backend registration. Visual provides image-library-neutral prepared-tensor pipelines, reversible geometry, classification, dense detection, and NMS; it does not reference OpenCV or a concrete inference backend. The LLM vertical slice provides chat, streaming generation, cancellation, and embeddings; the LLamaSharp adapter loads GGUF models without bundling native runtimes. The ONNX Runtime and OpenVINO adapters perform real named-tensor CPU inference while leaving native runtime packages application-owned. ModelPack.Json provides strict schema 2.0 manifests and local integrity validation. ModelFactory adds audited catalogs, immutable GitHub Release downloads, content-addressed caching, and offline reuse without bundling model weights.

See [the local LLM quick start](docs/articles/llm-getting-started.md) and [the native backend guide](docs/articles/llamasharp-native-backends.md). / 请参阅[本地 LLM 快速开始](docs/articles/llm-getting-started.md)和 [原生后端指南](docs/articles/llamasharp-native-backends.md)。
See [the Visual prepared-tensor quick start](docs/articles/visual-getting-started.md) and [Visual lifecycle guide](docs/articles/visual-lifecycle-compatibility.md). / 请参阅 [Visual 已准备张量快速开始](docs/articles/visual-getting-started.md) 与 [Visual 生命周期指南](docs/articles/visual-lifecycle-compatibility.md)。
See [the ModelPack JSON quick start](docs/articles/modelpack-json-getting-started.md) for portable ONNX, GGUF, and multi-file package manifests. / 可移植 ONNX、GGUF 和多文件模型包清单请参阅 [ModelPack JSON 快速开始](docs/articles/modelpack-json-getting-started.md)。
See [the ModelFactory quick start](docs/articles/modelfactory-getting-started.md) and [official catalog](docs/articles/model-catalog.md). / 请参阅 [ModelFactory 快速开始](docs/articles/modelfactory-getting-started.md)与[官方模型目录](docs/articles/model-catalog.md)。
See [the ONNX Runtime quick start](docs/articles/onnxruntime-getting-started.md) and [compatibility guide](docs/articles/onnxruntime-compatibility.md). / 请参阅 [ONNX Runtime 快速开始](docs/articles/onnxruntime-getting-started.md)与[兼容性指南](docs/articles/onnxruntime-compatibility.md)。

See [the OpenVINO quick start](docs/articles/openvino-getting-started.md) and [compatibility guide](docs/articles/openvino-compatibility.md). / 请参阅 [OpenVINO 快速开始](docs/articles/openvino-getting-started.md)与[兼容性指南](docs/articles/openvino-compatibility.md)。

## Status

Version `2.0.0-alpha.1` is an early architecture baseline. Public APIs may change before the first release candidate.

See [README_cn.md](README_cn.md) for Chinese documentation.

## License

Apache-2.0.
