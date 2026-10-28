# 本地 LLM 快速开始

DeploySharp 将稳定的文本生成接口、LlamaSharp 适配器和原生运行时分开。应用安装 DeploySharp.LLM、DeploySharp.Backend.LlamaSharp，并根据部署设备选择一个 LlamaSharp 原生包。

~~~powershell
dotnet add package JYPPX.DeploySharp.LLM --version 2.0.0
dotnet add package JYPPX.DeploySharp.Backend.LlamaSharp --version 2.0.0
dotnet add package LLamaSharp.Backend.Cpu --version 0.27.0
~~~

CUDA 和 Vulkan 应改为对应的 LlamaSharp 原生包，不要在同一应用中无目的地安装多个 native 后端。

## 文本生成与流式输出

~~~csharp
using var registry = new LanguageModelRegistry();
registry.UseLlamaSharp(new LlamaSharpOptions(contextSize: 2048));
var artifact = new ModelArtifact(
    new ModelId("local/chat-model"),
    "gguf",
    modelPath);
using ILanguageModelSession session = registry.CreateSession(
    artifact,
    new LanguageModelRequest(
        LanguageModelCapabilities.TextGeneration |
        LanguageModelCapabilities.Streaming));
var request = new TextGenerationRequest(
    "用一句话解释依赖注入。",
    new GenerationOptions(maxTokens: 64, temperature: 0.2f));
await foreach (var chunk in session.StreamAsync(request))
{
    Console.Write(chunk.Text);
}
~~~

聊天模型的 prompt template 属于模型元数据，应提供匹配的 IPromptFormatter。会话同时支持 Generate、GenerateAsync 和 StreamAsync；支持 Embeddings 的能力由 session.Metadata.Capabilities 报告，不支持时返回稳定诊断而不会伪造向量。

## 并发、取消与内存

一个 LlamaSharp session 持有模型权重和可变生成上下文，同一 session 内调用会串行执行。需要并发时创建有限数量的独立 session，并把重复的 native 内存计入容量规划。上下文长度、KV cache、batch 和 GPU layer 数量共同决定内存，建议先以 CPU 配置验证，再逐步启用卸载。

取消可能表现为 OperationCanceledException，或流式结果中的 Cancelled 终止原因，取决于取消发生的阶段。释放 session 前应等待活动调用结束；Dispose 可重复调用。

## 原生后端与发布目录

`DeploySharp.Backend.LlamaSharp` 不内置 CPU、CUDA 或 Vulkan 二进制。最终应用必须安装一个与 LLamaSharp 版本、操作系统、RID、进程架构和驱动匹配的 `LLamaSharp.Backend.*` 包；当前适配器使用 LLamaSharp `0.27.0` 合同。同一应用不要无目的地安装多个 native 后端。

发布时使用明确 RID，例如：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

检查发布目录中的托管程序集、LlamaSharp native 文件和 CUDA/Vulkan 依赖是否来自同一架构。`Any CPU` 不能替代 native 架构检查。

```csharp
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

上下文、KV cache、batch、序列数和 GPU 卸载层数共同决定内存。先以较小配置验证加载，再逐步提高；`useMemoryLock` 会增加系统内存压力，不应默认开启。

## 模型与后端状态

GGUF 文件必须由应用选择并放在本地，原生包的 RID、驱动和指令集也由应用负责。可下载的 Preview 模型见[官方模型目录](model-catalog.md)，具体后端状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)。

## Prompt、停止条件和生成参数

聊天模型的 Prompt 模板不是普通字符串拼接。应为模型选择与其训练格式一致的 `IPromptFormatter`，把 system/user/assistant 角色、BOS/EOS 和 generation prompt 作为同一合同维护。出现“模型能加载但回答重复或不停止”时，先检查模板、EOS token 和 `stop` 序列，再调 temperature。

建议把可复现参数写入日志或请求记录：`maxTokens`、temperature、top-p/top-k、seed（如果后端支持）、context size、batch size、线程数和 GPU layer 数。性能报告区分首 token 延迟、生成 token/s、总时长和峰值内存；不要只报告整个请求的平均毫秒数。

## Embedding 和多会话

生成会话与 embedding 会话可能使用不同的 context 和 pooling 配置。只有 `session.Metadata.Capabilities` 明确包含 `Embeddings` 时才调用 `EmbedAsync`；不要把文本生成 logits 当作通用向量。Embedding 批量请求先确认模型和 native 后端支持多序列，再选择 `SequenceCount`/`BatchSize`；不支持时使用有限的独立会话。

## 失败恢复

加载失败通常来自 GGUF 版本、原生后端或显存预算；生成中断则可能来自取消、上下文超限或 native 错误。保留原始错误码和 InnerException，释放当前会话后再创建一个新会话重试。不要在同一个已报告 native 错误的可变会话上继续发送请求。

| 现象 | 优先检查 |
| --- | --- |
| 找不到 native 库 | `LLamaSharp.Backend.*`、RID、进程架构和发布目录 |
| CUDA 初始化失败 | 驱动、CUDA 主版本、GPU 架构和 native 包 |
| GGUF 加载失败 | 文件完整性、GGUF 版本、模型架构和量化支持 |
| 内存不足 | context、batch、GPU layers 和独立 Session 数量 |
| 并发结果互相影响 | 是否错误共享了可变生成上下文 |
