# LLamaSharp native backend guide / LLamaSharp 原生后端指南

`JYPPX.DeploySharp.Backend.LlamaSharp` contains only the managed adapter and depends on the managed `LLamaSharp` 0.27.0 package. It never embeds CPU, CUDA, or Vulkan native binaries. The final application owns the native package, RID, driver, and redistribution decision. / `JYPPX.DeploySharp.Backend.LlamaSharp` 只包含托管适配器，并依赖托管 `LLamaSharp` 0.27.0 包。它绝不内置 CPU、CUDA 或 Vulkan 原生二进制文件。最终应用程序负责原生包、RID、驱动程序和再分发决策。

| Deployment / 部署 | Application package / 应用包 | Primary check / 首要检查 |
|---|---|---|
| CPU | `LLamaSharp.Backend.Cpu` 0.27.0 | OS/architecture RID and instruction-set support / OS、架构 RID 与指令集支持 |
| NVIDIA CUDA 12 | `LLamaSharp.Backend.Cuda12` 0.27.0 | CUDA 12 driver/runtime compatibility / CUDA 12 驱动与运行时兼容性 |
| Vulkan | `LLamaSharp.Backend.Vulkan` 0.27.0 | Vulkan loader and device driver / Vulkan 加载器与设备驱动 |

Managed and native LLamaSharp versions must be tested as one set. The adapter was compiled against managed LLamaSharp 0.27.0; installing a different native release can cause missing entry points, ABI mismatches, or silent behavior changes. / LLamaSharp 托管版和原生版必须作为一个整体测试。本适配器针对托管 LLamaSharp 0.27.0 编译；安装不同版本的原生包可能导致入口点缺失、ABI 不匹配或静默行为变化。

DeploySharp maps common loader failures to `DS-NATIVE-6001` while preserving the original exception in `InnerException` and `TechnicalDetails`. A malformed GGUF is reported as `DS-MODEL-2001`; a context overflow is `DS-LLM-4003`. / DeploySharp 将常见加载失败映射为 `DS-NATIVE-6001`，并在 `InnerException` 与 `TechnicalDetails` 中保留原始异常。损坏的 GGUF 报告为 `DS-MODEL-2001`，上下文溢出报告为 `DS-LLM-4003`。

The environment variable `DEPLOYSHARP_LLAMA_MODEL` activates the real integration test. A missing model or native runtime is reported as an explicit skipped/inconclusive test, never as a pass. / 环境变量 `DEPLOYSHARP_LLAMA_MODEL` 用于启用真实集成测试。缺少模型或原生运行时时，测试会明确显示为跳过/不确定，绝不会伪装成通过。

```powershell
$env:DEPLOYSHARP_LLAMA_MODEL = 'D:\models\model.gguf'
dotnet test tests\DeploySharp.Backend.LlamaSharp.Tests\DeploySharp.Backend.LlamaSharp.Tests.csproj -c Release
```
