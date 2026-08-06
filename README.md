# DeploySharp V2.0

DeploySharp is a modular .NET model deployment toolkit under active V2 development.

## Current update / 本次更新

`2.0.0-alpha.1` currently adds Visual anomaly detection/segmentation and the OCR text-orientation/automatic-correction slice: explicit four-angle semantics, one-decode OpenCV rotation, real CPU ONNX/IR execution, OCR provenance, ModelPack/ModelFactory evidence, tests, docs, packaging and clean consumers. See the [detailed release note](docs/releases/2.0.0-alpha.1.md) and [version index](docs/releases/README.md). / 当前 `2.0.0-alpha.1` 新增 Visual 异常检测/分割以及 OCR 文本方向分类/自动纠正切片，包括显式四方向语义、OpenCV 单次解码旋转、真实 CPU ONNX/IR 执行、OCR 来源、ModelPack/ModelFactory 证据、测试、文档、打包和 clean consumer。详见[本版本说明](docs/releases/2.0.0-alpha.1.md)与[版本索引](docs/releases/README.md)。

DeploySharp treats backend execution speed and official-model fidelity as product requirements. A backend contract fixture proves adapter behavior only; a model is not marked supported until its preprocessing, tensor interpretation, and postprocessing match the official implementation through reproducible golden comparisons. Performance work measures preprocessing, inference, transfer, and postprocessing separately and uses optimized modern-framework paths without dropping legacy compatibility. / DeploySharp 将后端执行速度与官方模型保真作为产品要求。后端合同夹具只能证明适配器行为；只有预处理、张量解释与后处理通过可复现黄金对照匹配官方实现后，模型才可标记为支持。性能工作分别测量预处理、推理、传输与后处理，并在保留旧框架兼容的同时使用现代框架优化路径。

The V2 architecture separates stable contracts, domain workflows, managed backend adapters, and platform-native runtimes. It does not provide source, binary, configuration, or behavioral compatibility with DeploySharp V1.

The first packages are `JYPPX.DeploySharp.Core`, `JYPPX.DeploySharp.Visual`, `JYPPX.DeploySharp.LLM`, `JYPPX.DeploySharp.Backend.LlamaSharp`, `JYPPX.DeploySharp.Backend.OnnxRuntime`, `JYPPX.DeploySharp.Backend.OpenVINO`, `JYPPX.DeploySharp.ModelPack.Json`, and `JYPPX.DeploySharp.ModelFactory`. Core contains dependency-free inference contracts, tensors, model metadata, canonical result DTOs, diagnostics, errors, and explicit backend registration. Visual provides image-library-neutral prepared-tensor pipelines, reversible geometry, classification, dense and oriented detection, image/pixel anomaly detection, two-model OCR with polygon detection and greedy CTC, semantic segmentation, Direct and Prototype instance segmentation, Pose estimation with direct/heatmap decoding, deterministic polygon IoU, rotated NMS, RLE, NMS, and OKS; it does not reference OpenCV or a concrete inference backend. The LLM vertical slice provides chat, streaming generation, cancellation, and embeddings; the LLamaSharp adapter loads GGUF models without bundling native runtimes. The ONNX Runtime and OpenVINO adapters perform real named-tensor CPU inference while leaving native runtime packages application-owned. ModelPack.Json provides strict schema 2.0 manifests and local integrity validation. ModelFactory adds audited catalogs, immutable GitHub Release downloads, content-addressed caching, and offline reuse without bundling model weights.

See [the local LLM quick start](docs/articles/llm-getting-started.md) and [the native backend guide](docs/articles/llamasharp-native-backends.md). / 请参阅[本地 LLM 快速开始](docs/articles/llm-getting-started.md)和 [原生后端指南](docs/articles/llamasharp-native-backends.md)。
See [the Visual prepared-tensor quick start](docs/articles/visual-getting-started.md) and [Visual lifecycle guide](docs/articles/visual-lifecycle-compatibility.md). / 请参阅 [Visual 已准备张量快速开始](docs/articles/visual-getting-started.md) 与 [Visual 生命周期指南](docs/articles/visual-lifecycle-compatibility.md)。
See [the Visual semantic segmentation guide](docs/articles/visual-semantic-segmentation.md) for logits, probability maps, integer label maps, source restoration, and row-major RLE. / logits、概率图、整数标签图、源图恢复与行优先 RLE 请参阅 [Visual 语义分割指南](docs/articles/visual-semantic-segmentation.md)。
See [the Visual Pose guide](docs/articles/visual-pose-estimation.md) for direct/heatmap schemas, keypoint topology, coordinate restoration, deterministic peaks, and OKS. / direct/heatmap Schema、关键点拓扑、坐标恢复、确定性峰值与 OKS 请参阅 [Visual Pose 指南](docs/articles/visual-pose-estimation.md)。
See [the Visual instance segmentation guide](docs/articles/visual-instance-segmentation.md) for Direct/Prototype masks, exact interpolation/crop/threshold order, independent masks, ownership maps, and supply-chain evidence. / Direct/Prototype 掩码、精确插值/裁剪/阈值顺序、独立掩码、所有权图与供应链证据请参阅 [Visual 实例分割指南](docs/articles/visual-instance-segmentation.md)。
See [the Visual OBB guide](docs/articles/visual-oriented-detection.md) for center-size-angle and four-corner contracts, angle conventions, authoritative source quadrilaterals, polygon IoU, and rotated NMS. / 中心宽高角与四角点契约、角度约定、权威源图四边形、多边形 IoU 与 rotated NMS 请参阅 [Visual OBB 指南](docs/articles/visual-oriented-detection.md)。
See [the Visual OCR guide](docs/articles/visual-ocr.md) and [OCR AlgorithmVerified template](docs/articles/ocr-algorithm-verification-template.md) for strict two-model OCR contracts, perspective crops, CTC, supply-chain bindings, and production admission evidence. / 严格双模型 OCR 契约、透视裁剪、CTC、供应链绑定与正式准入证据请参阅 [Visual OCR 指南](docs/articles/visual-ocr.md) 和 [OCR AlgorithmVerified 模板](docs/articles/ocr-algorithm-verification-template.md)。
See [the OCR orientation guide](docs/articles/visual-ocr-orientation.md) for explicit four-angle mapping, confidence rejection, one-decode OpenCV correction, and OCR provenance. / 显式四方向映射、置信度拒绝、OpenCV 单次解码纠正和 OCR 来源请参阅 [OCR 方向指南](docs/articles/visual-ocr-orientation.md)。
See [the supported-model roadmap](docs/articles/supported-models.md) for the reviewed Ultralytics family matrix and ModelFactory admission state. / 已审阅的 Ultralytics 模型族矩阵与 ModelFactory 准入状态请参阅[支持模型路线表](docs/articles/supported-models.md)。
See [the Visual anomaly guide](docs/articles/visual-anomaly-detection.md) and [anomaly AlgorithmVerified template](docs/articles/anomaly-algorithm-verification-template.md) for image scores, pixel maps, explicit normalization/thresholding, source restoration, and official fidelity evidence. / 图像分数、像素异常图、显式归一化/阈值、源图恢复与官方保真证据请参阅 [Visual 异常检测指南](docs/articles/visual-anomaly-detection.md) 和 [异常模型 AlgorithmVerified 模板](docs/articles/anomaly-algorithm-verification-template.md)。
See [the ModelPack JSON quick start](docs/articles/modelpack-json-getting-started.md) for portable ONNX, GGUF, and multi-file package manifests. / 可移植 ONNX、GGUF 和多文件模型包清单请参阅 [ModelPack JSON 快速开始](docs/articles/modelpack-json-getting-started.md)。
See [the ModelFactory quick start](docs/articles/modelfactory-getting-started.md) and [official catalog](docs/articles/model-catalog.md). / 请参阅 [ModelFactory 快速开始](docs/articles/modelfactory-getting-started.md)与[官方模型目录](docs/articles/model-catalog.md)。
See [the ONNX Runtime quick start](docs/articles/onnxruntime-getting-started.md) and [compatibility guide](docs/articles/onnxruntime-compatibility.md). / 请参阅 [ONNX Runtime 快速开始](docs/articles/onnxruntime-getting-started.md)与[兼容性指南](docs/articles/onnxruntime-compatibility.md)。

See [the OpenVINO quick start](docs/articles/openvino-getting-started.md) and [compatibility guide](docs/articles/openvino-compatibility.md). / 请参阅 [OpenVINO 快速开始](docs/articles/openvino-getting-started.md)与[兼容性指南](docs/articles/openvino-compatibility.md)。

For real image input, install `JYPPX.DeploySharp.Visual.OpenCV` and explicitly choose the matching `JYPPX.OpenCV.runtime.win-x64` preview runtime. The adapter keeps OpenCV out of Core and Visual, copies pixels into owned tensors, and currently verifies Windows x64 only. / 如需真实图像输入，请安装 `JYPPX.DeploySharp.Visual.OpenCV` 并显式选择匹配的 `JYPPX.OpenCV.runtime.win-x64` preview runtime。适配器不会把 OpenCV 引入 Core 和 Visual，会将像素复制到自有张量，目前仅核验 Windows x64。

See the [Visual.OpenCV quick start](docs/articles/visual-opencv-getting-started.md) and [compatibility guide](docs/articles/visual-opencv-compatibility.md). / 请参阅 [Visual.OpenCV 快速开始](docs/articles/visual-opencv-getting-started.md) 与 [兼容性指南](docs/articles/visual-opencv-compatibility.md)。

## Status

Version `2.0.0-alpha.1` is an early architecture baseline. Public APIs may change before the first release candidate.

See [README_cn.md](README_cn.md) for Chinese documentation.

## License

Apache-2.0.
