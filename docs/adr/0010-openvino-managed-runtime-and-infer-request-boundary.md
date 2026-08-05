# ADR 0010: OpenVINO managed/runtime and InferRequest boundary / OpenVINO 托管、运行时与 InferRequest 边界

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-05

## Context / 背景

`JYPPX.OpenVINO.CSharp.API` 3.3.0 publishes exact desktop assets from .NET Framework 4.6 through 4.8.1, .NET Core 3.1, and .NET 5 through 10 without carrying a native runtime. `OpenVINO.runtime.win` 2026.2.1 is about 70 MB and supplies Windows x64 native libraries, ONNX/IR frontends, and multiple device plug-ins. Coupling that package to the adapter would force every consumer to receive one platform and unused plug-ins. / `JYPPX.OpenVINO.CSharp.API` 3.3.0 为 .NET Framework 4.6 至 4.8.1、.NET Core 3.1 与 .NET 5 至 10 发布精确桌面资产，但不携带原生运行时。`OpenVINO.runtime.win` 2026.2.1 约 70 MB，提供 Windows x64 原生库、ONNX/IR 前端和多个设备插件。若把它绑定到适配器，会强制所有用户获得单一平台及未使用插件。

The wrapper exposes owned `Core`, `Model`, `CompiledModel`, and `InferRequest` objects and borrowed port/tensor views. It also exposes native `StartAsync`, `WaitFor`, and `Cancel`. The 3.3.0 scalar factory currently creates `[0]`, not a writable rank-zero Tensor, so scalar binding cannot be represented safely through this managed API. / 包装器暴露自有的 `Core`、`Model`、`CompiledModel` 与 `InferRequest`，以及借用的端口/张量视图，并提供原生 `StartAsync`、`WaitFor` 与 `Cancel`。3.3.0 的标量工厂当前创建 `[0]` 而非可写零秩 Tensor，因此无法通过该托管 API 安全表示标量绑定。

NuGet.org also served different byte payloads under the same managed package version during the audit. An older global cache contained only net8.0 while the current package contains the full matrix. / 审计期间 NuGet.org 还曾在相同托管包版本下提供不同字节；旧全局缓存仅含 net8.0，而当前包包含完整矩阵。

## Decision / 决策

The DeploySharp adapter references only managed 3.3.0 and publishes its exact direct TFM matrix. Applications explicitly install a platform runtime. Runtime preflight calls the OpenVINO C ABI and verifies the 2026.2 line before any wrapper static initialization. CPU is the only admitted device until other runners exist. / DeploySharp 适配器仅引用托管 3.3.0 并发布其精确直接 TFM 矩阵；应用显式安装平台运行时。在任何包装器静态初始化前，运行时预检调用 OpenVINO C ABI 并验证 2026.2 版本线。其他 runner 建立前仅准入 CPU。

Each inference call owns one `InferRequest`, input tensors, and output copies. `RunAsync` uses native start/wait/cancel; `Run` has boundary-only cancellation. Session disposal cancels linked async operations and waits for all request slots before reverse-order resource release. Rank-zero input binding fails with a stable `DS-OV-5103` until a verified managed API can allocate it. / 每次推理调用独占一个 `InferRequest`、输入张量和输出副本。`RunAsync` 使用原生 start/wait/cancel；`Run` 仅在边界观察取消。会话释放会取消关联异步操作，并在逆序释放资源前等待全部请求槽位。零秩输入绑定在可验证托管 API 能分配前稳定返回 `DS-OV-5103`。

Package validation and CI use an isolated `NUGET_PACKAGES` cache to avoid accepting the stale same-version payload. / 包验证与 CI 使用隔离的 `NUGET_PACKAGES` 缓存，避免接受相同版本的旧载荷。

## Evidence / 证据

- [OpenVINO C# API v3.3.0](https://github.com/guojin-yan/OpenVINO-CSharp-API/releases/tag/openvino-csharp-api-v3.3.0) / OpenVINO C# API v3.3.0 发布页
- [Managed NuGet 3.3.0](https://www.nuget.org/packages/JYPPX.OpenVINO.CSharp.API/3.3.0) / 托管 NuGet
- [OpenVINO runtime 2026.2.1](https://github.com/guojin-yan/OpenVINO-CSharp-API/releases/tag/openvino-runtime-v2026.2.1) / OpenVINO runtime 发布页
- [Intel OpenVINO C API](https://docs.openvino.ai/2026/api/c_cpp_api/group__ov__runtime__c__api.html) / Intel OpenVINO C API

## Consequences / 影响

The adapter package remains managed-only and portable across its declared TFMs. Consumers choose runtime/RID explicitly and receive stable missing-native diagnostics. GPU/NPU/AUTO and scalar inputs remain unadvertised rather than silently falling back or corrupting memory. / 适配器包保持纯托管并覆盖声明 TFM；消费者显式选择运行时/RID，并获得稳定的 native 缺失诊断。GPU/NPU/AUTO 与标量输入不会被虚假声明，也不会静默回退或破坏内存。
