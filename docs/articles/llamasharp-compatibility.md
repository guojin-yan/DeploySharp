# LlamaSharp 兼容性

DeploySharp.Backend.LlamaSharp 是托管适配层，不内置 CPU、CUDA 或 Vulkan native 二进制。最终应用决定 LlamaSharp 原生包、RID、驱动和部署方式。

## 目标框架

| 包 | 目标框架 |
| --- | --- |
| DeploySharp.Core | net46-net481、netstandard2.0、netcoreapp3.1、net5.0-net10.0 |
| DeploySharp.LLM | netstandard2.0、netcoreapp3.1、net5.0-net10.0 |
| DeploySharp.Backend.LlamaSharp | netstandard2.0、net8.0 |

## 原生后端选择

| 设备 | 应用包 | 首要检查 |
| --- | --- | --- |
| CPU | LLamaSharp.Backend.Cpu 0.27.0 | OS/RID 和指令集 |
| NVIDIA CUDA 12 | LLamaSharp.Backend.Cuda12 0.27.0 | CUDA 驱动和运行时 |
| Vulkan | LLamaSharp.Backend.Vulkan 0.27.0 | Vulkan loader 与设备驱动 |

托管版和 native 版必须作为一个整体测试。GGUF 扩展名或文件头只能证明文件可读，不能证明模型架构、量化和上下文配置一定受支持。

## 模型、并发与诊断

上下文长度、KV cache、batch、GPU layer 和模型大小共同决定内存。单个 session 的生成/嵌入上下文是可变的，调用会串行；并发请求请创建独立 session 并限制总数。加载失败映射为 DS-NATIVE-6001，损坏 GGUF 为 DS-MODEL-2001，上下文超限为 DS-LLM-4003，同时保留原始异常。

具体可下载模型和当前状态见[官方模型目录](model-catalog.md)与[模型支持指南](model-support.md)。
