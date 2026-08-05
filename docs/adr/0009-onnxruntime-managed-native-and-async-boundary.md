# ADR 0009: ONNX Runtime managed/native and async boundary / ONNX Runtime 托管、原生与异步边界

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-04

## Context / 背景

ONNX Runtime 1.28.0 publishes `Microsoft.ML.OnnxRuntime.Managed` separately from execution-provider packages. The managed package supplies `netstandard2.0` and `net8.0` desktop APIs but cannot execute without a matching native package. The CPU package is about 139 MB while the managed package is about 1 MB, so placing CPU, CUDA, and DirectML binaries in every DeploySharp adapter would violate modular installation. / ONNX Runtime 1.28.0 将 `Microsoft.ML.OnnxRuntime.Managed` 与 Execution Provider 包分开发行。托管包提供 `netstandard2.0` 和 `net8.0` 桌面 API，但没有匹配的原生包就不能执行。CPU 包约 139 MB，托管包约 1 MB；若把 CPU、CUDA 和 DirectML 二进制全部放入 DeploySharp 适配器，会破坏模块化安装。

The 1.28 C# API provides real native `InferenceSession.RunAsync` only with caller-preallocated outputs, which requires static output shapes. Upstream tests also require an intra-op pool larger than one. DeploySharp testing found that racing `RunOptions.Terminate` with the native async callback can prevent callback completion, while synchronous `Run` responds correctly to terminate between executable nodes. / 1.28 C# API 提供真正的原生 `InferenceSession.RunAsync`，但调用方必须预分配输出，因此要求静态输出形状；上游测试还要求算子内线程池大于 1。DeploySharp 实测发现，`RunOptions.Terminate` 与原生异步回调竞争可能使回调无法完成，而同步 `Run` 能在可执行节点之间正确响应 terminate。

## Decision / 决策

`JYPPX.DeploySharp.Backend.OnnxRuntime` directly references only `Microsoft.ML.OnnxRuntime.Managed` 1.28.0 and targets its direct desktop assets: `netstandard2.0` and `net8.0`. Applications explicitly install a matching official native package. The adapter declares and verifies CPU only; CUDA and DirectML are documented but are not advertised capabilities. / `JYPPX.DeploySharp.Backend.OnnxRuntime` 仅直接引用 `Microsoft.ML.OnnxRuntime.Managed` 1.28.0，并面向其桌面直接资产 `netstandard2.0` 与 `net8.0`。应用显式安装匹配的官方原生包。适配器只声明并验证 CPU；CUDA 与 DirectML 仅记录包边界，不声明为已支持能力。

Static-output calls with a non-cancellable caller token and an intra-op setting other than one use native `RunAsync`. Dynamic outputs, single-thread sessions, and calls whose caller token can cancel use documented synchronous native fallback; DeploySharp never uses `Task.Run` to fabricate backend async. The fallback connects a per-call `RunOptions.Terminate`. Native-async disposal waits for native completion, then reports disposal cancellation. / 静态输出、调用方 token 不可取消且算子内线程数不为 1 时使用原生 `RunAsync`。动态输出、单线程会话和调用方 token 可取消的调用使用已记录的同步原生 fallback；DeploySharp 绝不使用 `Task.Run` 伪造后端异步。Fallback 为每次调用连接独立的 `RunOptions.Terminate`。原生异步期间释放会等待原生调用完成，然后报告释放取消。

All outputs are copied into owned Core arrays before ORT values are disposed. External-data files remain beside the model and are integrity-bounded by ModelPack.Json. / 所有输出都会在释放 ORT 值之前复制到 Core 自有数组。External data 文件保持在模型旁边，并由 ModelPack.Json 完整性边界约束。

## Evidence / 证据

- [ONNX Runtime v1.28.0 release](https://github.com/microsoft/onnxruntime/releases/tag/v1.28.0) / ONNX Runtime v1.28.0 发布页
- [Official C# API source](https://github.com/microsoft/onnxruntime/tree/v1.28.0/csharp/src/Microsoft.ML.OnnxRuntime) / 官方 C# API 源码
- [Managed NuGet 1.28.0](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.Managed/1.28.0) and [CPU NuGet 1.28.0](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/1.28.0) / 托管包与 CPU 包

## Consequences / 影响

The backend NuGet remains small and provider-neutral, but installing it alone intentionally produces `DS-NATIVE-6001`. Consumers must align managed and native versions. GPU and DirectML require future runner evidence and API work before admission. / 后端 NuGet 保持小型且不绑定 Provider，但仅安装该包会有意产生 `DS-NATIVE-6001`。消费者必须对齐托管与原生版本。GPU 与 DirectML 在准入前仍需要未来 runner 证据和 API 工作。
