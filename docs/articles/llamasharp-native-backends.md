# LlamaSharp 原生后端部署

`DeploySharp.Backend.LlamaSharp` 只提供托管适配层，不把 CPU、CUDA 或 Vulkan 的 native 二进制打进自己的包。原生后端必须由最终应用按操作系统、RID、GPU 驱动和 LlamaSharp 版本显式选择。这样可以避免 NuGet 恢复出互相冲突的 native 库，也便于把部署责任放在应用的发布目录。

## 安装托管包和一个原生后端

```powershell
dotnet add package JYPPX.DeploySharp.LLM --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.LlamaSharp --version 2.0.0-alpha.1
dotnet add package LLamaSharp.Backend.Cpu --version 0.27.0
```

NVIDIA CUDA 12 或 Vulkan 应将最后一个包替换为对应的 LlamaSharp native 包；同一个应用不应无目的地同时安装 CPU、CUDA 和 Vulkan 后端。当前适配器使用 LLamaSharp `0.27.0` 合同，升级原生包时必须重新编译并验证。

## RID 和发布目录

在目标设备上使用与进程架构一致的 RID 发布，例如 Windows x64：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

检查发布目录中托管程序集、LlamaSharp native 文件和 CUDA/Vulkan 依赖是否来自同一架构。`Any CPU` 不能替代 native 架构检查；在 x86 进程中加载 x64 native 会直接失败。Linux、Windows 和 macOS 的驱动、loader 及安全策略由宿主系统负责。

## 选择设备和资源参数

```csharp
using JYPPX.DeploySharp.Backends.LlamaSharp;

var options = new LlamaSharpOptions(
    device: "cuda",
    gpuLayerCount: 24,
    mainGpu: 0,
    contextSize: 4096,
    threads: 8,
    batchThreads: 4,
    batchSize: 512,
    sequenceCount: 1,
    useMemoryMap: true);

registry.UseLlamaSharp(options);
```

`device` 支持 `auto`、`cpu`、`gpu`、`cuda` 和 `vulkan`。GPU 卸载层数、上下文长度、KV cache、batch 和序列数共同决定显存；应先用较小上下文和较少 GPU 层验证加载，再逐步提高。`useMemoryMap` 通常适合本地大模型，`useMemoryLock` 会增加系统内存压力，不应默认打开。

## 并发与生命周期

一个 LLamaSharp session 持有可变生成上下文，适配器要求单个 session 的调用串行。需要并发时创建有限数量的独立 session，每个 session 都会重新加载或持有 native 上下文；总内存必须按 session 数量规划。不要把同一个 session 放进无界 `Task.Run` 队列。

释放顺序应为：停止新请求，等待活动生成结束，释放 session，最后释放 registry。取消可能在生成结果中表现为 `OperationCanceledException` 或取消终止原因；应用应把两者都视为正常取消路径，并记录原始诊断。

## 常见故障

| 现象 | 检查项 |
| --- | --- |
| 找不到 native 库 | 安装匹配的 `LLamaSharp.Backend.*`，检查发布目录和 RID |
| CUDA 初始化失败 | 驱动、CUDA 主版本、GPU 架构和 native 包是否匹配 |
| 模型加载失败 | GGUF 是否完整、模型架构和量化是否被 LLamaSharp 支持 |
| 内存不足 | 降低上下文、batch、GPU layer 或独立 session 数量 |
| 并发结果互相影响 | 每个请求使用独立 session，不共享生成上下文 |

适配器会将 native 加载失败、损坏 GGUF 和上下文超限映射为稳定错误码，同时保留原始异常。详细模型选择和文本生成示例见[本地 LLM 快速开始](llm-getting-started.md)，版本边界见[LLamaSharp 兼容性](llamasharp-compatibility.md)。
