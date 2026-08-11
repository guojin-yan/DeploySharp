# Local LLM quick start / 本地 LLM 快速开始

DeploySharp separates the stable LLM workflow from the LLamaSharp implementation and its native runtime. An application installs `JYPPX.DeploySharp.LLM`, `JYPPX.DeploySharp.Backend.LlamaSharp`, and exactly one native backend selected for its deployment. / DeploySharp 将稳定的 LLM 工作流、LLamaSharp 实现和原生运行时分离。应用程序需要安装 `JYPPX.DeploySharp.LLM`、`JYPPX.DeploySharp.Backend.LlamaSharp`，以及一个与部署环境匹配的原生后端。

For repository integration evidence, an exact GGUF must first pass `eng/models/llm/Test-GgufAdmission.ps1 -RequireAdmitted` with `DEPLOYSHARP_LLAMA_MODEL` and `DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST`. A local filename or valid GGUF header alone is not admission evidence. / 对仓库集成证据而言，精确 GGUF 必须先通过上述准入脚本；仅有本地文件名或有效 GGUF 文件头不能构成准入证据。

```powershell
dotnet add package JYPPX.DeploySharp.LLM --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.LlamaSharp --version 2.0.0-alpha.1
dotnet add package LLamaSharp.Backend.Cpu --version 0.27.0
```

The CPU package above is only an example. CUDA and Vulkan users must select the matching LLamaSharp package instead; do not install several native backends unless the LLamaSharp deployment guidance explicitly requires it. / 上述 CPU 包只是示例。CUDA 和 Vulkan 用户必须改选匹配的 LLamaSharp 包；除非 LLamaSharp 部署指南明确要求，否则不要同时安装多个原生后端。

```csharp
using JYPPX.DeploySharp.Backends.LlamaSharp;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.LLM.Registry;
using JYPPX.DeploySharp.Models;

using var registry = new LanguageModelRegistry();
registry.UseLlamaSharp(new LlamaSharpOptions(contextSize: 2048));

var artifact = new ModelArtifact(
    new ModelId("local/chat-model"),
    "gguf",
    @"D:\models\chat-model.gguf");

using ILanguageModelSession session = registry.CreateSession(
    artifact,
    new LanguageModelRequest(
        LanguageModelCapabilities.TextGeneration |
        LanguageModelCapabilities.Streaming));

var request = new TextGenerationRequest(
    "Explain dependency injection in one sentence.",
    new GenerationOptions(maxTokens: 64, temperature: 0.2f));

await foreach (var chunk in session.StreamAsync(request))
{
    Console.Write(chunk.Text);
}
```

Use `ChatHistory` with `session.Generate(...)`, `GenerateAsync(...)`, or `StreamAsync(...)` extension methods for structured chat. The default `PlainTextPromptFormatter` is intentionally model-neutral; replace `IPromptFormatter` with the exact template required by the selected GGUF model. / 使用 `ChatHistory` 配合 `session.Generate(...)`、`GenerateAsync(...)` 或 `StreamAsync(...)` 扩展方法可以执行结构化聊天。默认 `PlainTextPromptFormatter` 有意保持模型无关；请针对所选 GGUF 模型替换为准确的 `IPromptFormatter` 模板。

## Embeddings / 文本嵌入

The loaded session reports embedding support through `session.Metadata.Capabilities`. LLamaSharp 0.27.0 exposes a real embedding API, but an individual model may still reject embedding mode or a pooling configuration. DeploySharp then returns a stable `DS-LLM-4002` or another diagnostic error instead of a fabricated vector. / 已加载会话通过 `session.Metadata.Capabilities` 报告嵌入支持。LLamaSharp 0.27.0 提供真实嵌入 API，但具体模型仍可能拒绝嵌入模式或池化配置。此时 DeploySharp 返回稳定的 `DS-LLM-4002` 或其他诊断错误，不会伪造向量。

```csharp
if ((session.Metadata.Capabilities & LanguageModelCapabilities.Embeddings) != 0)
{
    var embedding = await session.EmbedAsync(
        new TextEmbeddingRequest("DeploySharp", normalize: true));
    Console.WriteLine(embedding.Dimensions);
}
```
