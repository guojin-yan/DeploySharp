# API changes in 2.0.0-alpha.1 / 2.0.0-alpha.1 API 变更

DeploySharp V2 is a clean API and does not provide V1 compatibility. This record describes changes made inside the V2 alpha baseline while implementing the local LLM vertical slice. / DeploySharp V2 是全新 API，不提供 V1 兼容。本记录描述实现本地 LLM 垂直切片时对 V2 alpha 基线所做的变更。

## Core additions / Core 新增项

- Added the `netstandard2.0` build and NuGet asset without removing any existing Core target framework. This gives LLM and future format packages a correct common asset instead of a .NET Framework fallback. / 在不移除任何 Core 目标框架的前提下新增 `netstandard2.0` 构建和 NuGet 资产，使 LLM 与后续格式包获得正确公共资产，不再回退到 .NET Framework。
- Added `DeploySharpErrorCodes.ModelArtifactInvalid` (`DS-MODEL-2001`). / 新增模型工件无效错误码。
- Added `DeploySharpErrorCodes.LanguageModelFailed` (`DS-LLM-4001`). / 新增语言模型操作失败错误码。
- Added `DeploySharpErrorCodes.LanguageModelCapabilityUnavailable` (`DS-LLM-4002`). / 新增语言模型能力不可用错误码。
- Added `DeploySharpErrorCodes.ContextLimitExceeded` (`DS-LLM-4003`). / 新增上下文长度超限错误码。
- Added `DeploySharpErrorCodes.NativeRuntimeUnavailable` (`DS-NATIVE-6001`). / 新增原生运行时不可用错误码。

No existing Core public type or member was removed, renamed, or behaviorally redirected. Core did not gain an LLamaSharp, OpenCV, TensorRT, or JSON dependency. / 未删除、重命名或重定向任何已有 Core 公共类型或成员。Core 没有新增 LLamaSharp、OpenCV、TensorRT 或 JSON 依赖。

## New packages / 新增包

- `JYPPX.DeploySharp.Backend.OpenVINO` adds verified CPU inference for ONNX and OpenVINO IR, exact named multi-I/O, dynamic metadata, native async cancellation, bounded concurrency, managed/native preflight, and stable diagnostics without adding OpenVINO to Core or Visual. / `JYPPX.DeploySharp.Backend.OpenVINO` 新增经过验证的 ONNX 与 OpenVINO IR CPU 推理、精确命名多输入输出、动态元数据、原生异步取消、有界并发、托管/原生预检与稳定诊断，同时不向 Core 或 Visual 添加 OpenVINO 依赖。

- `JYPPX.DeploySharp.LLM` introduces the backend-neutral chat, generation, streaming, timeout, cancellation, embedding, prompt formatter, provider, session, and registry contracts. / `JYPPX.DeploySharp.LLM` 新增后端无关的聊天、生成、流式、超时、取消、嵌入、提示词格式化器、提供程序、会话和注册契约。
- `JYPPX.DeploySharp.Backend.LlamaSharp` introduces the GGUF adapter for LLamaSharp 0.27.0 and intentionally excludes native runtime packages. / `JYPPX.DeploySharp.Backend.LlamaSharp` 新增面向 LLamaSharp 0.27.0 的 GGUF 适配器，并有意排除原生运行时包。
- `JYPPX.DeploySharp.ModelPack.Json` introduces strict schema 2.0 manifests, deterministic JSON, source/license metadata, SHA256 and size verification, multi-file artifacts, and safe local resolution. / `JYPPX.DeploySharp.ModelPack.Json` 新增严格 Schema 2.0 清单、确定性 JSON、来源/许可证元数据、SHA256 和大小验证、多文件工件以及安全本地解析。
- No Core public API or third-party image/backend dependency was added for ModelPack.Json. / ModelPack.Json 未新增 Core 公共 API，也未新增图像或推理后端第三方依赖。
- `JYPPX.DeploySharp.ModelFactory` adds validated catalog schema 1.0, deterministic selection, immutable GitHub Release downloads, retry/progress/cancellation, integrity-protected caching, offline reuse, and scoped cleanup. / `JYPPX.DeploySharp.ModelFactory` 新增已验证目录 Schema 1.0、确定性选择、不可变 GitHub Release 下载、重试/进度/取消、完整性保护缓存、离线复用和有范围清理。
- `ModelFileIntegrity.NormalizeSha256` was added to ModelPack.Json so ModelFactory reuses the canonical SHA256 rule instead of creating a divergent validator. Core remained unchanged. / ModelPack.Json 新增 `ModelFileIntegrity.NormalizeSha256`，使 ModelFactory 复用规范 SHA256 规则而不创建分叉验证器；Core 保持不变。
- `JYPPX.DeploySharp.Visual` adds image-library-neutral prepared tensor input, resize/letterbox/crop transforms, instance-scoped profile selection, classification decoding, generic dense detection, NMS, Core backend pipeline execution, cancellation, timeout, and explicit ownership rules. It targets the full Core TFM matrix and depends only on Core. / `JYPPX.DeploySharp.Visual` 新增不绑定图像库的已准备张量输入、Resize/Letterbox/Crop 变换、实例级 Profile 选择、分类解码、通用稠密检测、NMS、Core 后端 Pipeline 执行、取消、超时及显式所有权规则。它面向完整 Core TFM 矩阵且仅依赖 Core。
- No Core public API changed for the Visual vertical slice, and no OpenCV, TensorRT, ONNX Runtime, OpenVINO, model weight, or test-image dependency was introduced. / Visual 垂直切片未修改 Core 公共 API，也未引入 OpenCV、TensorRT、ONNX Runtime、OpenVINO、模型权重或测试图片依赖。
- `JYPPX.DeploySharp.Backend.OnnxRuntime` adds a real ONNX Runtime 1.28.0 managed adapter with named multi-input/output tensors, metadata, dynamic shapes, eleven verified numeric element types, native async where safe, cancellable synchronous fallback, concurrency, disposal, and stable diagnostics. It directly targets `netstandard2.0` and `net8.0` and intentionally excludes native execution-provider packages. / `JYPPX.DeploySharp.Backend.OnnxRuntime` 新增真实 ONNX Runtime 1.28.0 托管适配器，支持命名多输入输出、元数据、动态形状、十一种已验证数值元素类型、安全条件下的原生异步、可取消同步 fallback、并发、释放与稳定诊断。它直接面向 `netstandard2.0` 和 `net8.0`，并有意排除原生 Execution Provider 包。
- Visual's internal cancellation mapping now preserves a backend-provided stable cancellation exception as Visual cancelled, timeout, or disposal state. No Visual public API or dependency changed. / Visual 内部取消映射现在会把后端提供的稳定取消异常保留为 Visual 取消、超时或释放状态；Visual 公共 API 与依赖均未改变。
- ModelFactory admits `onnx` and `onnxruntime` only after the real CPU inference, Visual, integrity, and clean-consumer evidence in this stage. The embedded official catalog remains empty. / ModelFactory 仅在本阶段取得真实 CPU 推理、Visual、完整性与 clean consumer 证据后准入 `onnx` 和 `onnxruntime`；嵌入式官方目录仍为空。

See ADR 0005 for target-framework and async-stream rationale, ADR 0008 for the Visual prepared-tensor boundary, and ADR 0009 for ONNX Runtime managed/native and async boundaries. / 目标框架和异步流理由见 ADR 0005，Visual 已准备张量边界见 ADR 0008，ONNX Runtime 托管、原生与异步边界见 ADR 0009。
