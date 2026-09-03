# 本地 LLM 快速开始

DeploySharp 将稳定的文本生成接口、LlamaSharp 适配器和原生运行时分开。应用安装 DeploySharp.LLM、DeploySharp.Backend.LlamaSharp，并根据部署设备选择一个 LlamaSharp 原生包。

~~~powershell
dotnet add package JYPPX.DeploySharp.LLM --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.LlamaSharp --version 2.0.0-alpha.1
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

## 模型与后端状态

GGUF 文件必须由应用选择并放在本地，原生包的 RID、驱动和指令集也由应用负责。可下载的 Preview 模型见[官方模型目录](model-catalog.md)，具体后端状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)。
