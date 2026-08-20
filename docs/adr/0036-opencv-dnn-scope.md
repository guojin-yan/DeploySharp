# ADR 0036: Add an explicit OpenCV DNN backend contract / 增加显式 OpenCV DNN 后端合同

## Status

Implemented in `2.0.0-alpha.1` as a Windows CPU preview backend; broader platform/operator evidence remains open. / 已在 `2.0.0-alpha.1` 实现为 Windows CPU preview 后端；更广的平台/算子证据仍未完成。

## Evidence

`JYPPX.DeploySharp.Visual.OpenCV` continues to own image decode, preprocessing, geometry, and input factories. The new `JYPPX.DeploySharp.Backend.OpenCV` loads ONNX networks through the OpenCV DNN API and consumes Core named tensors, keeping image preprocessing and inference lifecycle separately auditable. / `JYPPX.DeploySharp.Visual.OpenCV` 继续负责图像解码、前处理、几何与输入工厂。新增的 `JYPPX.DeploySharp.Backend.OpenCV` 通过 OpenCV DNN API 加载 ONNX 网络并消费 Core 命名张量，使图像预处理和推理生命周期保持可审计分离。

## Decision

Add `Backend.OpenCV` with a deliberately narrow first contract: static batch-one NCHW float32 image input, static float32 outputs, explicit input/output names and shapes, caller-owned native runtime, SHA-256 model validation, CPU target only, serialized session operations, and stable missing-native/configuration/tensor/dispose errors. The first fixture is the pinned 297-byte ReduceMean ONNX model. / 增加 `Backend.OpenCV`，首版合同刻意收窄为静态 batch-one NCHW float32 图像输入、静态 float32 输出、显式输入/输出名称与形状、调用方持有 native runtime、模型 SHA-256 校验、仅 CPU、会话串行操作以及稳定的 native 缺失/配置/张量/释放错误。首个夹具为固定的 297 字节 ReduceMean ONNX 模型。

## Consequences

Visual.OpenCV remains a preprocessing/input adapter, while the separate DNN package is a Windows CPU preview. Dynamic shapes, implicit preprocessing, GPU/NPU targets, and native runtime redistribution remain unsupported by this contract. / Visual.OpenCV 继续是前处理/输入适配器，独立 DNN 包目前为 Windows CPU preview。动态 shape、隐式预处理、GPU/NPU target 和 native runtime 再分发在该合同中仍不支持。
