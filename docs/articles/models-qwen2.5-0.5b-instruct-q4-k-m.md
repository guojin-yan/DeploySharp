# Qwen2.5 0.5B Instruct Q4_K_M

这是 ModelFactory 中的 Alpha Preview GGUF 条目，面向 LlamaSharp CPU 后端。模型文件、ModelPack 清单和 tokenizer/config sidecar 由同一个 Release 资产提供，客户端下载后会按清单校验大小与 SHA-256。

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
