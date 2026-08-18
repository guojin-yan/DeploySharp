# ModelFactory quick start / ModelFactory 快速开始

`JYPPX.DeploySharp.ModelFactory` selects a backend-compatible artifact from a validated catalog, downloads immutable GitHub Release assets, verifies SHA256 and size, validates the ModelPack manifest, and reuses the content-addressed cache offline. It does not perform inference and does not install native runtimes. / `JYPPX.DeploySharp.ModelFactory` 从已验证目录中选择后端兼容工件，下载不可变 GitHub Release 资产，验证 SHA256 和大小，校验 ModelPack 清单，并离线复用内容寻址缓存。它不执行推理，也不安装原生运行时。

```powershell
dotnet add package JYPPX.DeploySharp.ModelFactory --version 2.0.0-alpha.1
```

```csharp
using JYPPX.DeploySharp.ModelFactory;

ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
var options = new ModelFactoryOptions(
    cacheRoot: @"D:\DeploySharpCache",
    requestTimeout: TimeSpan.FromMinutes(10),
    maximumRetries: 3);

using var factory = new ModelFactoryClient(catalog, options);
ModelSelection selection = factory.Select(new ModelQuery(
    modelId: "llm/qwen2.5-0.5b-instruct-q4-k-m",
    backend: "llamasharp",
    format: "gguf",
    includePreview: true));

var progress = new Progress<ModelDownloadProgress>(value =>
    Console.WriteLine($"{value.AssetId}: {value.Stage} {value.ReceivedBytes}/{value.TotalBytes}"));

MaterializedModel model = await factory.GetModelAsync(selection, progress);
var coreArtifacts = model.Package.ToCoreArtifacts();
```

For a user-facing install path, the repository includes a small CLI that owns the catalog/options/client setup for you:

```powershell
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- list --preview
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- install --model-id yolo/v8/detect/n --backend onnxruntime --format onnx --preview
```

`install` uses `%LOCALAPPDATA%\DeploySharp\ModelFactory` by default. Pass `--cache <path>` for an application-owned cache, or `--offline` to require a previously verified cache entry. The CLI prints the materialized package root so the caller can pass the verified files to its inference adapter.

The bundled official catalog contains the published Qwen, shared vision, YOLO, and DETR preview entries. Its immutable GitHub Release assets include ModelPack manifests, model sidecars, upstream licenses, exact sizes, and SHA-256 values. `includePreview: true` opts into these alpha entries. Applications can also load their own catalog with `ModelCatalogJsonSerializer` or download a strict snapshot with `ModelCatalogClient.LoadAsync`. / 内置官方目录包含已发布的 Qwen、共享视觉、YOLO 与 DETR 预览条目；不可变 GitHub Release 资产包含 ModelPack 清单、模型 Sidecar、上游许可证、精确大小与 SHA-256。设置 `includePreview: true` 即显式选择这些 alpha 条目；应用也可通过 `ModelCatalogJsonSerializer` 加载自有目录，或以 `ModelCatalogClient.LoadAsync` 下载严格快照。

The development-time inventory at `eng/models/inventory/development-model-inventory.json` remains a record of local/external candidates, not a replacement for the official catalog. The published Qwen preview uses the separate release ModelPack at `eng/models/llm/releases/qwen2.5-0.5b-instruct-q4-k-m.modelpack.json`. / 开发清单仍是本地/外部候选的记录，不能替代官方目录；已发布的 Qwen 预览使用独立的 Release ModelPack：`eng/models/llm/releases/qwen2.5-0.5b-instruct-q4-k-m.modelpack.json`。

## Query and Preview entries / 查询与 Preview 条目

Selection is deterministic and filters by ModelId, task, family, format, backend, precision, quantization, portability, tokenizer/processor identities, resolution, image count, context length, generation mode, and KV schema. Complete bundle selection rejects mixed versions, missing sidecars, and mixed identities. `Supported` is the default. Set `includePreview: true` only when the application explicitly accepts a non-stable artifact. External records are never materialized as supported models. / 选择过程可按模型、格式、后端、精度、Tokenizer/Processor、分辨率、图像数、Context、生成模式与 KV Schema 筛选；完整 Bundle 会拒绝混版本、缺 Sidecar 与混 Identity。默认仅选择 Supported。

## Offline reuse / 离线复用

Create another client with `offline: true` and the same cache root. It succeeds only if all catalog files, hashes, sizes, paths, completion marker, and ModelPack checks still pass. A missing or damaged file returns `model-factory.offline-cache-miss`; offline mode never silently falls back to the network. / 使用同一缓存根并设置 `offline: true` 创建新客户端。只有目录文件、hash、大小、路径、完成标记和 ModelPack 检查全部通过时才成功。文件缺失或损坏会返回 `model-factory.offline-cache-miss`；离线模式绝不会静默联网。
