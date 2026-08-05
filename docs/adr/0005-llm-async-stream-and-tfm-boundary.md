# ADR 0005: LLM async streams and target-framework boundary / LLM 异步流与目标框架边界

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-04

## Decision / 决策

`JYPPX.DeploySharp.LLM` exposes `IAsyncEnumerable<GenerationChunk>` and targets `netstandard2.0`, `netcoreapp3.1`, and .NET 5 through .NET 10. `JYPPX.DeploySharp.Core` additionally publishes a `netstandard2.0` asset without changing its public API. The LLM package references the official `Microsoft.Bcl.AsyncInterfaces` compatibility package. / `JYPPX.DeploySharp.LLM` 公开 `IAsyncEnumerable<GenerationChunk>`，目标框架为 `netstandard2.0`、`netcoreapp3.1` 以及 .NET 5 到 .NET 10。`JYPPX.DeploySharp.Core` 在不改变公共 API 的前提下额外发布 `netstandard2.0` 资产。LLM 包引用官方 `Microsoft.Bcl.AsyncInterfaces` 兼容包。

`netstandard2.0` supports .NET Framework 4.6.1 through 4.8.1 consumers. .NET Framework 4.6 is not supported by the LLM package because the required async-stream compatibility asset does not support that target. Core itself continues to support .NET Framework 4.6. / `netstandard2.0` 支持 .NET Framework 4.6.1 到 4.8.1 使用者。由于所需异步流兼容资产不支持该目标，LLM 包不支持 .NET Framework 4.6；Core 本身仍继续支持 .NET Framework 4.6。

`JYPPX.DeploySharp.Backend.LlamaSharp` publishes only `netstandard2.0` and `net8.0`, matching verified LLamaSharp 0.27.0 managed assets. Native CPU, CUDA, or Vulkan packages remain application-owned. / `JYPPX.DeploySharp.Backend.LlamaSharp` 只发布 `netstandard2.0` 和 `net8.0`，与已验证的 LLamaSharp 0.27.0 托管资产一致。原生 CPU、CUDA 或 Vulkan 包继续由应用程序自行选择和持有。

## Consequences / 影响

- Modern and compatible legacy consumers share one streaming contract. / 现代与兼容的旧框架使用者共享同一流式契约。
- Core remains free of LLamaSharp and image dependencies. / Core 继续不依赖 LLamaSharp 和图像库。
- .NET Framework 4.6 applications may still use Core, but must upgrade to 4.6.1 or later for LLM streaming. / .NET Framework 4.6 应用仍可使用 Core，但要使用 LLM 流式能力必须升级到 4.6.1 或更高版本。
