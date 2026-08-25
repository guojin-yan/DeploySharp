# DeploySharp 2.0.0-alpha.1 / 首次版本说明

<code>2.0.0-alpha.1</code> is the first public engineering preview of DeploySharp V2. This document records the complete current snapshot; the root README keeps only a short summary. Future versions should add one document beside this file and keep the README summary short. / <code>2.0.0-alpha.1</code> 是 DeploySharp V2 的首次公开工程预览版。本文件记录当前完整快照；主页 README 只保留简短摘要。后续版本应在本目录新增对应版本文档，并保持主页简洁。

## Release identity / 版本身份

| Field / 项目 | Value / 内容 |
| --- | --- |
| Version / 版本 | <code>2.0.0-alpha.1</code> |
| Branch / 分支 | <code>DeploySharpV2.0</code> |
| Release target / 发布目标 | Windows 10/11 x64 |
| SDK | .NET SDK <code>10.0.301</code> from <code>global.json</code> |
| API compatibility / API 兼容 | New V2 API; no V1 source, binary, configuration, or behavior compatibility |
| Stability / 稳定性 | Alpha; public APIs may still change |

## What is included / 本次包含内容

### Core and workflow contracts / Core 与流程契约

- Backend-neutral model identity, artifacts, tensor shapes and typed buffers.
- Explicit backend registration, request capabilities, sessions, inference inputs/outputs, diagnostics, cancellation, timeouts, and disposal.
- Canonical visual results for classification, detection, semantic/instance segmentation, pose, OBB, OCR, anomaly maps, promptable segmentation, and vision-language workflows.
- Backend-neutral LLM generation, chat, streaming, embeddings, ordered multimodal media, and lifecycle contracts.

### Runtime adapters / 运行时适配器

- ONNX Runtime CPU named-tensor execution.
- OpenVINO CPU named-tensor execution.
- OpenCV DNN CPU execution and OpenCV image preparation.
- TensorRT 11/CUDA 12.9 managed inference, ONNX-to-engine building, CUDA/RTC contracts, and local cache boundaries.
- LLamaSharp GGUF generation and embeddings through an application-selected native backend.

### Model distribution / 模型分发

- Strict ModelPack JSON manifests with artifact path, size, SHA-256, format, precision, and backend identity.
- ModelFactory catalog queries, immutable GitHub Release asset downloads, cache verification, offline reuse, and cleanup.
- 42 catalog model entries, 42 dedicated sample cases, and a generated model/backend verification matrix.

### Repository experience / 仓库体验

- Samples grouped into seven complete workflows under <code>samples/01-core</code> through <code>samples/07-benchmarks</code>.
- Per-module READMEs and one case README for every catalog model.
- Bilingual API documentation, DocFX site, package lock files, Windows CI, model coverage checks, and backend verification scripts.

## Verification snapshot / 验证快照

- Windows locked restore: passed in an isolated NuGet cache.
- Full Release build: <code>0</code> warnings and <code>0</code> errors.
- Test solution: <code>466</code> passed, <code>74</code> skipped, <code>0</code> failed in the current Windows run.
- All six workflow samples: passed.
- Model case coverage: <code>42/42</code>; artifact manifest audit: <code>43/43</code>.
- ONNX Runtime, OpenVINO, and OpenCV DNN CPU evidence is recorded on Windows x64.
- TensorRT: <code>37/38</code> tested ONNX artifacts build and execute; BRIA RMBG 2.0 dynamic-int8 is explicitly unsupported.
- The repeatable speed sample measured the same tiny classification graph on Windows x64: ONNX Runtime P50/P95 <code>0.0177/0.0404 ms</code>, OpenCV DNN <code>0.0065/0.0111 ms</code>, and OpenVINO <code>0.0389/0.1317 ms</code> after 10 warmups and 100 timed iterations. These are local fixture measurements, not production-model claims.
- DocFX and bilingual API documentation: passed with the isolated cache.

Exact per-model results, commands, and failure reasons are in the [model/backend matrix](../model-backend-verification-matrix.md). / 每个模型的精确结果、命令和失败原因见[模型与后端矩阵](../model-backend-verification-matrix.md)。

## Known boundaries / 已知边界

- The current release statement is Windows 10/11 x64. Linux, macOS, ARM, Android, NPU, and other untested provider/RID combinations are deferred.
- Backend support is independent per model. Catalog discovery or download does not imply inference support on every backend.
- Model source/license fields remain catalog metadata; source/license review is not an Alpha admission gate. ModelPack size and SHA-256 integrity checks remain active.
- TensorRT engine files are device/runtime-bound and are not universal model catalog artifacts.
- NuGet stable publication, GA compatibility, and long-term support are outside this first Alpha snapshot.

## Reproduce this release / 复现本版本

Start with the [usage tutorial](usage-tutorial.md), then use the [platform guide](platform-support.md), [model support guide](model-support.md), and [performance benchmark](performance-benchmarking.md). Complete workflows are under <code>samples/</code>; maintainer-only stage records are kept in [engineering history](../history/README.md).
