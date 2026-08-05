# Visual lifecycle and compatibility / Visual 生命周期与兼容性

## Package and dependency boundary / 包与依赖边界

`JYPPX.DeploySharp.Visual` depends only on `JYPPX.DeploySharp.Core`. It contains no image codec, OpenCV binding, inference engine, or native runtime. `JYPPX.DeploySharp.Visual.OpenCV` is the optional default image adapter, while each application installs only the backend packages it needs. / `JYPPX.DeploySharp.Visual` 仅依赖 `JYPPX.DeploySharp.Core`，不包含图像编解码器、OpenCV 绑定、推理引擎或原生运行时。`JYPPX.DeploySharp.Visual.OpenCV` 是可选的默认图像适配器，应用只安装实际需要的后端包。

| Package / 包 | Supported TFMs / 支持 TFM | Notes / 说明 |
|---|---|---|
| `JYPPX.DeploySharp.Visual` | `net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`, `netstandard2.0`, `netcoreapp3.1`, `net5.0`–`net10.0` | Managed contracts/decoders only; actual backend and adapter may support fewer targets. / 仅托管契约与解码器；实际后端和适配器可能支持更少目标。 |

Compatibility with an old TFM does not restore vendor support or security servicing for an end-of-life runtime. Always intersect the Visual, image-adapter, backend, and native-runtime matrices. / 兼容旧 TFM 不代表厂商支持或已停止生命周期运行时的安全维护恢复。必须取 Visual、图像适配器、后端和原生运行时支持矩阵的交集。

## Ownership and disposal / 所有权与释放

- `BackendRegistry` is application-owned and should normally live for the application scope. / `BackendRegistry` 由应用拥有，通常具有应用级生命周期。
- `VisualProfileRegistry` is application-owned; register during startup, then freeze. / `VisualProfileRegistry` 由应用拥有；启动期注册后冻结。
- `VisualPipeline` owns exactly one backend session and releases it idempotently. / `VisualPipeline` 恰好拥有一个后端会话并幂等释放。
- `PreparedVisualInput` is borrowed by default. Owned mode releases only the attached resource; automatic release requires `DisposeOwnedInputOnCompletion`. / `PreparedVisualInput` 默认借用；Owned 模式仅释放附加资源，自动释放需显式启用 `DisposeOwnedInputOnCompletion`。

## Concurrency, cancellation, and timeout / 并发、取消与超时

`SessionOptions.MaxConcurrency` bounds concurrent calls on the single backend session. The safe default is one; increase it only when the backend documents concurrent session use. Waiting calls remain cancellable. / `SessionOptions.MaxConcurrency` 限制单个后端会话上的并发调用。安全默认值为 1；仅当后端明确支持会话并发时才提高。等待中的调用仍可取消。

Caller cancellation maps to `DS-VISUAL-5007`, timeout to `DS-VISUAL-5008`, and disposal during a call to `DS-VISUAL-5009`. Pipeline disposal cancels active work, waits for backend calls to unwind, then releases the session. A backend that ignores cancellation can therefore delay disposal; consult its documentation. / 调用方取消映射到 `DS-VISUAL-5007`，超时映射到 `DS-VISUAL-5008`，调用期间释放映射到 `DS-VISUAL-5009`。Pipeline 释放会取消活动任务、等待后端调用退出，再释放会话。因此忽略取消的后端可能延迟释放，请查阅其文档。

## Current scope / 当前范围

This alpha implements classification, generic dense detection, semantic segmentation, instance segmentation, Pose estimation, transforms, decoding, NMS, and OKS in Visual. Optional packages currently provide OpenCV image preparation and ONNX Runtime/OpenVINO tensor inference. TensorRT, OCR, VLM, official model weights, and official test images are not included; the embedded ModelFactory catalog remains empty until legally redistributable assets pass admission. / 本 alpha 在 Visual 中实现分类、通用稠密检测、语义分割、实例分割、姿态估计、变换、解码、NMS 与 OKS；可选包现已提供 OpenCV 图像准备及 ONNX Runtime/OpenVINO 张量推理。当前不包含 TensorRT、OCR、VLM、官方模型权重或官方测试图片；在可合法再分发资产通过准入前，内嵌 ModelFactory 目录保持为空。
