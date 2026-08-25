# Documentation guide / 文档指南

DeploySharp documentation is organized by the job you are trying to complete: start the repository, build a visual workflow, select a backend, acquire a model, or inspect historical engineering decisions. / DeploySharp 文档按实际任务组织：启动仓库、构建视觉流程、选择后端、获取模型，或查看历史工程决策。

> **Current scope / 当前范围**
>
> `2.0.0-alpha.1` is a Windows 10/11 x64 engineering preview. The runnable release path is source-first; Linux, macOS, ARM, and NPU validation remain deferred. / `2.0.0-alpha.1` 是 Windows 10/11 x64 工程预览版，当前以源码复现为主；Linux、macOS、ARM 和 NPU 验证暂缓。

## Choose a path / 选择入口

| Goal / 目标 | Start here / 从这里开始 | Next / 下一步 |
| --- | --- | --- |
| Run the repository / 运行仓库 | [Getting started](getting-started.md) | [Installation](installation.md) |
| Understand this release / 了解当前版本 | [2.0.0-alpha.1 release notes](release-2.0.0-alpha.1.md) | [Release and platform status](release-platform-status.md) |
| Choose a platform or backend / 选择平台或后端 | [Platform and backend support](platform-support.md) | [Compatibility notes](onnxruntime-compatibility.md) |
| See all supported models / 查看全部支持模型 | [Supported model guide](model-support.md) | [Model/backend matrix](../model-backend-verification-matrix.md) |
| Follow a code tutorial / 跟随代码教程 | [Usage tutorial](usage-tutorial.md) | [Visual quick start](visual-getting-started.md) |
| Understand the layers / 理解分层 | [Architecture](architecture.md) | [Visual quick start](visual-getting-started.md) |
| Build a visual task / 构建视觉任务 | [Visual quick start](visual-getting-started.md) | [Visual task guides](visual-yolo-detection.md) |
| Choose a runtime / 选择运行时 | [Backend guides](onnxruntime-getting-started.md) | [Compatibility notes](onnxruntime-compatibility.md) |
| Compare inference speed / 比较推理速度 | [Performance benchmarking](performance-benchmarking.md) | <code>samples/07-benchmarks/InferenceSpeedBenchmark</code> |
| Download a model / 下载模型 | [ModelFactory quick start](modelfactory-getting-started.md) | [Release inference](model-release-inference-getting-started.md) |
| Reproduce a catalog result / 复现目录结果 | [Official model catalog](model-catalog.md) | [Model/backend matrix](../model-backend-verification-matrix.md) |
| Read project history / 查看项目历史 | [Release history](../releases/README.md) | [API change notes](api-changes-alpha1.md) |

## Documentation layers / 文档层次

- **Start here / 入门**: repository setup, package boundaries, architecture, compatibility, current release scope, and the repeatable performance benchmark.
- **Visual workflows / 视觉流程**: prepared tensors, preprocessing geometry, task decoders, OCR, segmentation, multimodal, and document workflows.
- **Backends / 后端**: ONNX Runtime, OpenVINO, OpenCV, TensorRT, and LLamaSharp integration boundaries.
- **Models / 模型**: catalog entries, ModelPack/ModelFactory, model acquisition, release inference, and reproducibility.
- **History / 历史记录**: stage-by-stage API and release engineering notes. These pages explain how the current surface was built; they are not the shortest path for a new user.

The [model/backend verification matrix](../model-backend-verification-matrix.md) is the authoritative place for passed, unsupported, and untested combinations. The [release and platform status](release-platform-status.md) is the authoritative place for the current Windows Alpha boundary. / [模型与后端验证矩阵](../model-backend-verification-matrix.md) 是通过、不支持和未测试组合的权威入口；[发布与平台状态](release-platform-status.md) 是当前 Windows Alpha 范围的权威入口。
