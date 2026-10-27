# Qwen2.5 0.5B Instruct Q4_K_M

这是 ModelFactory 中的 Alpha Preview GGUF 条目，面向 LlamaSharp CPU 后端。模型文件、ModelPack 清单和 tokenizer/config sidecar 由 [`models-llm.1`](https://github.com/guojin-yan/DeploySharp/releases/tag/models-llm.1) 语言模型 Release 集合提供；后续兼容语言模型会继续追加到该集合并在 Release notes 中记录上传日期。客户端下载后会按清单校验大小与 SHA-256。

## 使用方式

安装 DeploySharp.LLM、DeploySharp.ModelFactory 和应用选择的 LlamaSharp CPU runtime，然后在 ModelFactory 查询中显式打开 includePreview。模型缓存由 ModelFactory 管理，原生运行时仍由应用负责部署。

~~~csharp
ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
ModelSelection selection = ModelCatalogQuery.Select(
    catalog,
    new ModelQuery(
        modelId: "llm/qwen2.5-0.5b-instruct-q4-k-m",
        backend: "llamasharp",
        format: "gguf",
        includePreview: true)).Single();
~~~

该条目只代表当前 GGUF Bundle 可被 ModelFactory 识别并在声明的 CPU 路径运行，不等于所有提示、硬件或原生后端都具有相同质量和速度。模型 ID、可下载状态和版本以[官方模型目录](model-catalog.md)为准。

先用 CLI 确认条目并下载完整 Bundle。缓存目录应由应用固定配置，不要从 Release 手工下载后再猜测 sidecar 路径：

```powershell
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- show `
  --model-id llm/qwen2.5-0.5b-instruct-q4-k-m --preview
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- install `
  --model-id llm/qwen2.5-0.5b-instruct-q4-k-m `
  --backend llamasharp --format gguf --preview `
  --cache .\.cache\deploysharp
```

`install` 成功只表示 Bundle 已下载并校验。应用还必须引用与设备匹配的 LLamaSharp native backend；CPU、CUDA 和 Vulkan runtime 不能混装后依赖加载顺序碰运气。

## 资产和推理边界

一个可复现的 LLM Bundle 至少包含 GGUF、tokenizer、聊天模板和目录清单。应用应把这些文件放在同一个 ModelFactory materialized root，不要只复制 `.gguf` 文件；缺少 tokenizer 或模板时，模型可能能够加载，但对话格式和停止条件会错误。

当前条目的验证重点是小模型 CPU 文本生成。`contextLength`、`gpuLayerCount`、线程数和 batch 会改变内存与速度，不能把本文条目的默认值当成所有 GGUF 的通用值。第一次生成包含 mmap 和 KV cache 初始化，应与后续 token/s 的 steady state 分开记录。

```csharp
var llamaOptions = new LlamaSharpOptions(
    contextSize: 2048,
    gpuLayerCount: 0,
    threads: Math.Max(1, Environment.ProcessorCount / 2),
    device: "cpu");
using var languageRegistry = new LanguageModelRegistry();
languageRegistry.UseLlamaSharp(llamaOptions);
```

通过 `LanguageModelRegistry` 按 `ModelArtifact` 创建 `ILanguageModelSession`，使用 `GenerateAsync` 或流式生成扩展完成请求，并在 `using` 结束时释放会话。LLamaSharp 会话默认串行；若需要多个并发请求，应创建多个独立会话并按内存预算限制数量，而不是把一个会话同时交给多个线程。切换到 CUDA 原生后端时，必须重新记录 GPU、驱动、原生后端版本和显存占用；CPU 与 CUDA 的输出速度不能直接横向外推。

下面展示已经获得本地 GGUF 绝对路径后的最小推理合同。`ggufPath` 应来自 ModelFactory 验证后的 materialized root，而不是未经校验的临时下载文件：

```csharp
var artifact = new ModelArtifact(
    new ModelId("llm/qwen2.5-0.5b-instruct-q4-k-m"),
    "gguf",
    ggufPath,
    preferredBackend: LlamaSharpBackendProvider.BackendId);

var request = new LanguageModelRequest(
    LanguageModelCapabilities.TextGeneration,
    LlamaSharpBackendProvider.BackendId,
    device: "cpu");

using ILanguageModelSession session = languageRegistry.CreateSession(artifact, request);
GenerationResult result = await session.GenerateAsync(
    new TextGenerationRequest(
        prompt,
        new GenerationOptions(
            maxTokens: 128,
            temperature: 0.7f,
            topP: 0.9f,
            topK: 40,
            seed: 42,
            stopSequences: new[] { "<|im_end|>" },
            timeout: TimeSpan.FromMinutes(2))),
    cancellationToken);

Console.WriteLine(result.Text);
```

`prompt` 必须使用该模型对应的聊天模板。直接拼接用户文本虽可能输出内容，但角色边界、停止条件和结果质量不可复现；多轮对话建议使用 `ChatHistory` 与会话的 `PromptFormatter`，并保留系统提示和历史裁剪规则。

## 参数与内存

| 参数 | 影响 | 调整原则 |
| --- | --- | --- |
| `contextSize` | KV cache 内存和最长上下文 | 从业务实际上限开始，不要直接设为模型最大值 |
| `threads` / `batchThreads` | CPU prompt eval 和 token 生成 | 在固定输入上扫描，线程数并非越大越快 |
| `batchSize` | prompt ingestion 吞吐与临时内存 | 长提示可增大，短对话应避免无效内存占用 |
| `gpuLayerCount` | GPU 卸载层数和显存 | 从较小值递增，同时监控显存与回退日志 |
| `temperature/topP/topK` | 输出随机性和可重复性 | 性能回归使用固定 seed 和固定采样参数 |
| `maxTokens` | 单次请求上限 | 与超时、取消和产品输出长度共同设置 |

模型加载后会长期占用映射文件、上下文和 KV cache。服务进程应复用会话；频繁创建/释放会把模型 mmap、权重初始化和上下文分配算入每次请求。需要并发时，每增加一个会话都要重新核算内存或显存，先设有界池大小，再做压力测试。

## 性能测试协议

至少分别报告模型加载时间、首 token 延迟、prompt tokens/s、generation tokens/s 和峰值工作集。固定模型 SHA-256、提示文本、上下文长度、生成 token 上限、线程数、native backend、CPU/GPU 和电源策略；预热请求不能混入稳态 P50/P95。

小模型一次生成很快时，控制台输出和日志也可能影响测量。计时范围应从请求提交到 terminal chunk，输出文本在计时结束后统一写出。比较 CPU 与 GPU 时还要报告 `gpuLayerCount`，否则“CUDA”可能实际只卸载了部分层或完全落在 CPU。

## 常见故障

| 现象 | 原因与处理 |
| --- | --- |
| 找不到目录记录 | 查询必须带 `includePreview: true`/CLI `--preview`，并使用完整模型 ID |
| Bundle 已下载但无法加载 DLL | native runtime、RID 或 CPU 指令集不匹配；先按 LlamaSharp 原生后端页做预检 |
| 输出不遵循角色或无法停止 | 聊天模板、stop sequence 或 tokenizer 合同不匹配 |
| 第二次请求继承上次内容 | 应用复用了对话历史；区分有状态历史与独立请求 |
| 并发请求排队 | 单个 session 按合同串行；增加受内存约束的独立 Session 池 |
| CPU 占满但速度下降 | 线程过量或设备降频；固定电源策略后重新扫描线程数 |
| CUDA 显存不足 | 降低 `gpuLayerCount`、context 或并发会话数，不能静默改成另一测试口径 |

LlamaSharp runtime 选择、部署、通用请求、流式输出与取消语义见 [LLM 入门](llm-getting-started.md)。
