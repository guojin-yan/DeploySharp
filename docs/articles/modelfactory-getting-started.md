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
    modelId: "organization/model",
    backend: "llama-sharp",
    format: "gguf"));

var progress = new Progress<ModelDownloadProgress>(value =>
    Console.WriteLine($"{value.AssetId}: {value.Stage} {value.ReceivedBytes}/{value.TotalBytes}"));

MaterializedModel model = await factory.GetModelAsync(selection, progress);
var coreArtifacts = model.Package.ToCoreArtifacts();
```

The bundled official catalog is intentionally empty until a model, test input, and reproducible expected result pass redistribution review and a real immutable Release is explicitly approved. Applications can load their own catalog with `ModelCatalogJsonSerializer` or download a strict snapshot with `ModelCatalogClient.LoadAsync`. / 在模型、测试输入和可复现预期结果通过再分发审核且真实不可变 Release 获得明确批准前，内置官方目录有意保持为空。应用可以使用 `ModelCatalogJsonSerializer` 加载自己的目录，或使用 `ModelCatalogClient.LoadAsync` 下载严格快照。

The development-time inventory at `eng/models/inventory/development-model-inventory.json` records every Stage 1-29 model and its metadata/upload/download state. It is not a downloadable catalog: all current structured manifests declare `redistributionAllowed:false`. Before planning any upload, also read the external round-closeout plan at `E:\GitSpace\DeploySharp-V2.0\plan\开发计划-轮次收口清单.md`. / 开发清单记录阶段 1-29 的模型及元数据/上传/下载状态，但它不是可下载目录；当前全部结构化 Manifest 均禁止再分发。规划上传前还必须阅读仓库外的 `E:\GitSpace\DeploySharp-V2.0\plan\开发计划-轮次收口清单.md`。

## Query and Preview entries / 查询与 Preview 条目

Selection is deterministic and filters by ModelId, task, family, format, backend, precision, quantization, portability, tokenizer/processor identities, resolution, image count, context length, generation mode, and KV schema. Complete bundle selection rejects mixed versions, missing sidecars, and mixed identities. `Supported` is the default. Set `includePreview: true` only when the application explicitly accepts a non-stable artifact. External records are never materialized as supported models. / 选择过程可按模型、格式、后端、精度、Tokenizer/Processor、分辨率、图像数、Context、生成模式与 KV Schema 筛选；完整 Bundle 会拒绝混版本、缺 Sidecar 与混 Identity。默认仅选择 Supported。

## Offline reuse / 离线复用

Create another client with `offline: true` and the same cache root. It succeeds only if all catalog files, hashes, sizes, paths, completion marker, and ModelPack checks still pass. A missing or damaged file returns `model-factory.offline-cache-miss`; offline mode never silently falls back to the network. / 使用同一缓存根并设置 `offline: true` 创建新客户端。只有目录文件、hash、大小、路径、完成标记和 ModelPack 检查全部通过时才成功。文件缺失或损坏会返回 `model-factory.offline-cache-miss`；离线模式绝不会静默联网。
