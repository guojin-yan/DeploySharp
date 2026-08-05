# ADR 0008: Visual prepared-tensor and coordinate contract

- Status: Accepted
- Date: 2026-08-04
- Refines: ADR 0004

## Context

Visual model workflows need stable input, output, geometry, cancellation, and lifecycle semantics before a concrete image library or inference backend is available. Putting encoded images, OpenCV objects, or backend tensors into the stable contract would couple every visual model to one optional dependency. / 在具体图像库或推理后端可用前，视觉模型流程就需要稳定的输入、输出、几何、取消和生命周期语义。若把编码图像、OpenCV 对象或后端张量放入稳定契约，会让每个视觉模型都耦合到某个可选依赖。

## Decision

`JYPPX.DeploySharp.Visual` accepts a `PreparedVisualInput`: a Core `ITensor` that an external image adapter has already decoded, resized, reordered, and normalized, plus source/model sizes, layout, preprocessing metadata, and a reversible `ImageTransform`. Visual does not decode or modify pixels. / `JYPPX.DeploySharp.Visual` 接收 `PreparedVisualInput`：由外部图像适配器完成解码、缩放、通道调整和归一化后的 Core `ITensor`，以及源图/模型尺寸、布局、预处理元数据和可逆 `ImageTransform`。Visual 本身不解码或修改像素。

Coordinates use finite single-precision values and half-open rectangles `[left, top, right, bottom)`. Detection decoders map model-space boxes back to source space and clip them before returning Core result DTOs. Resize, letterbox, and crop transformations are explicit and reversible. / 坐标使用有限的单精度值，矩形采用半开区间 `[left, top, right, bottom)`。检测解码器在返回 Core 结果 DTO 前，把模型空间框映射回源图空间并裁剪。Resize、Letterbox 和 Crop 变换均显式且可逆。

Prepared tensors are caller-owned by default. An adapter may attach exactly one owned `IDisposable` resource; `PreparedVisualInput.Dispose` releases it idempotently. A pipeline owns its backend session but not the registry. Calls on one session are bounded by `SessionOptions.MaxConcurrency`; cancellation, timeout, and disposal are reported with stable Visual error codes. / 已准备张量默认由调用方拥有。适配器可附加一个明确拥有的 `IDisposable` 资源；`PreparedVisualInput.Dispose` 幂等释放该资源。Pipeline 拥有后端会话但不拥有注册中心。同一会话调用受 `SessionOptions.MaxConcurrency` 限制；取消、超时和释放通过稳定 Visual 错误码报告。

Classification and generic dense detection are implemented as profile-selected decoders. A profile binds model ID, format, task, tensor names/shapes, labels, required backend capabilities, and decoder semantics. Profiles are held in an instance-scoped, freezable registry and backend selection reuses Core `BackendRegistry`. / 分类与通用稠密检测通过 Profile 选择的解码器实现。Profile 绑定模型 ID、格式、任务、张量名称/形状、标签、所需后端能力和解码语义。Profile 保存在实例级、可冻结注册中心中，后端选择复用 Core `BackendRegistry`。

## Consequences

- Core and Visual have no OpenCV, ImageSharp, TensorRT, ONNX Runtime, or other image/backend dependency. / Core 与 Visual 均不依赖 OpenCV、ImageSharp、TensorRT、ONNX Runtime 或其他图像/后端库。
- The future `JYPPX.DeploySharp.Visual.OpenCV` package is the official default pixel adapter and produces `PreparedVisualInput`; alternative image libraries remain optional peer adapters. / 后续 `JYPPX.DeploySharp.Visual.OpenCV` 是官方默认像素适配器并生成 `PreparedVisualInput`；其他图像库作为可选同级适配器存在。
- Image-adapter and inference-backend packages remain orthogonal, avoiding a package for every pair. / 图像适配器与推理后端包保持正交，避免为每种组合创建产品包。
- Model-specific output layouts are represented by profiles/decoders rather than guessed from model filenames. / 模型特定输出布局由 Profile/解码器表达，而不是根据模型文件名猜测。
