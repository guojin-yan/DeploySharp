# ADR 0035: Extract a backend-neutral Multimodal package / 抽取后端中立的 Multimodal 包

## Status

Implemented in the current `2.0.0-alpha.1` working tree; external native/model evidence remains a GA blocker. / 已在当前 `2.0.0-alpha.1` 工作树实现；外部 native/model 证据仍是 GA blocker。

## Context

The project plan places orchestration in `JYPPX.DeploySharp.Multimodal`, while common result DTOs remain in Core. The package now provides immutable ordered media inputs, request validation, capability descriptors, single-writer lifecycle, cancellation/timeout mapping, and stream terminal validation. Visual-native VLM is bridged by an explicit adapter; `llamasharp-mtmd` and OpenVINO GenAI VLM are represented by no-throw unavailable probes until caller-owned native/model evidence exists. / 方案书将编排放在 `JYPPX.DeploySharp.Multimodal`，通用结果 DTO 继续位于 Core。当前包已提供不可变有序媒体输入、请求校验、能力描述、单写入生命周期、取消/超时映射和流式终止校验。Visual 原生 VLM 通过显式适配器接入；在调用方持有 native/model 证据前，`llamasharp-mtmd` 与 OpenVINO GenAI VLM 由不抛异常的 unavailable probe 表示。

## Decision

Keep Core free of backend dependencies and retain its result DTOs. Put orchestration and backend-neutral request contracts in the independent package, with project/package references only to Core, LLM, and Visual contracts. Native runtimes, model files, tokenizers, image buffers, and vendor handles remain caller-owned and never enter the public API. / Core 继续不依赖后端并保留结果 DTO；将编排和后端中立请求合同放入独立包，包仅引用 Core、LLM 与 Visual 合同。native runtime、模型文件、tokenizer、图像缓冲区和厂商句柄继续由调用方持有，不进入公共 API。

## Consequences

The extraction is additive and leaves existing Core/Visual APIs intact. Package-only consumers, tests, pack, XML documentation and samples prove the managed boundary. The package is still alpha: no claim is made for unverified mtmd/OpenVINO GenAI native/model redistribution or cross-platform runtime support. / 本次抽取为增量改动并保留现有 Core/Visual API。独立 consumer、测试、pack、XML 文档和样例证明托管边界。该包仍为 alpha，不宣称未验证的 mtmd/OpenVINO GenAI native/model 再分发或跨平台运行时支持。

## Exit criteria

Managed package work is closed by the API inventory, dependency graph, package-only consumer, bilingual XML, DocFX input, pack audit, samples and tests now present. Native/model GA claims remain blocked until exact licensed bundles and repeatable platform evidence are supplied.
