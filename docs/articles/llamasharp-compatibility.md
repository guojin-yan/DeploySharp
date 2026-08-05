# Compatibility, lifecycle, and model record / 兼容性、生命周期与模型记录

## Target frameworks / 目标框架

| Package / 包 | Published TFM / 发布 TFM | Consumer coverage / 使用者覆盖 |
|---|---|---|
| Core | .NET Framework 4.6–4.8.1, `netstandard2.0`, `netcoreapp3.1`, .NET 5–10 | Full Core contract matrix / 完整 Core 契约矩阵 |
| LLM | `netstandard2.0`, `netcoreapp3.1`, .NET 5–10 | .NET Framework 4.6.1–4.8.1 through `netstandard2.0`; .NET Framework 4.6 is unsupported / 通过 `netstandard2.0` 支持 .NET Framework 4.6.1–4.8.1；不支持 .NET Framework 4.6 |
| Backend.LlamaSharp | `netstandard2.0`, `net8.0` | Matches verified LLamaSharp 0.27.0 managed assets / 与已验证的 LLamaSharp 0.27.0 托管资产一致 |

RID support is determined by the application-selected LLamaSharp native package, not by DeploySharp. Validate Windows/Linux/macOS and x64/Arm64 combinations against that package before shipping. / RID 支持由应用程序选择的 LLamaSharp 原生包决定，而不是由 DeploySharp 决定。发布前必须针对该包验证 Windows/Linux/macOS 与 x64/Arm64 组合。

## Model and quantization limits / 模型与量化限制

- The artifact must be a readable local `.gguf` file with a valid `GGUF` header; an optional SHA256 is checked before native loading. / 工件必须是可读的本地 `.gguf` 文件且包含有效 `GGUF` 头；可选 SHA256 会在原生加载前校验。
- Quantization support follows the bundled llama.cpp version in the selected LLamaSharp native package. A `.gguf` extension alone does not prove architecture or quantization support. / 量化支持取决于所选 LLamaSharp 原生包中附带的 llama.cpp 版本；仅有 `.gguf` 扩展名不能证明架构或量化受支持。
- Context size, KV cache, batch size, GPU layer offload, and model size jointly determine memory use. Start with CPU-safe `GpuLayerCount=0` and increase only after measurement. / 上下文长度、KV cache、批大小、GPU 卸载层数和模型大小共同决定内存使用。建议从 CPU 安全值 `GpuLayerCount=0` 开始，测量后再增加。
- A chat model's prompt template is model metadata, not a DeploySharp architecture class. Supply a matching `IPromptFormatter`. / 聊天模型的提示词模板属于模型元数据，不是 DeploySharp 架构类；应提供匹配的 `IPromptFormatter`。

## Concurrency, cancellation, and disposal / 并发、取消与释放

An LLamaSharp session owns model weights and mutable generation/embedding contexts. Calls on the same session are serialized; create separate sessions for true concurrent requests and account for duplicated native memory. `MaxConcurrency` must be 1. / LLamaSharp 会话持有模型权重以及可变的生成/嵌入上下文。同一会话内的调用会串行执行；需要真正并发时应创建多个会话，并计入重复的原生内存占用。`MaxConcurrency` 必须为 1。

Caller cancellation and request timeout are linked. Cancellation can appear as `OperationCanceledException` or a terminal chunk/result with `GenerationFinishReason.Cancelled`, depending on whether cancellation occurs before native execution or while LLamaSharp is already streaming. / 调用方取消与请求超时会被关联。根据取消发生在原生执行之前还是 LLamaSharp 已经流式输出期间，取消可能表现为 `OperationCanceledException`，也可能表现为 `GenerationFinishReason.Cancelled` 的终止片段/结果。

Dispose a session only after its pending callers have stopped. Disposal waits for the active operation, frees embedder/context/model handles, and is idempotent. A registry owns registered providers, but returned sessions remain caller-owned. / 只有在所有等待调用停止后才释放会话。释放会等待活动操作，释放 embedder/context/model 句柄，并且可重复调用。注册表持有已注册提供程序，但已返回的会话仍由调用方持有。

## Verified model record template / 已验证模型记录模板

Copy this block into a release record after a real test. Do not claim support from an extension-only or header-only check. / 完成真实测试后，将下列模板复制到发布记录中。不要仅凭扩展名或文件头检查声称支持。

```text
Model name/revision / 模型名称与修订：
Source and license / 来源与许可证：
GGUF filename and SHA256 / GGUF 文件名与 SHA256：
Architecture and quantization / 架构与量化：
DeploySharp version / DeploySharp 版本：
LLamaSharp managed/native version / LLamaSharp 托管/原生版本：
OS, RID, device, driver / OS、RID、设备、驱动：
Context/batch/GPU layers / 上下文、批大小、GPU 层数：
Generation result / 生成结论：
Streaming and cancellation result / 流式与取消结论：
Embedding capability/result / 嵌入能力与结论：
Known limitations / 已知限制：
Test date and owner / 测试日期与负责人：
```
