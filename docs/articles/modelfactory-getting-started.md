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

## Query and Preview entries / 查询与 Preview 条目

Selection is deterministic and filters by ModelId, task, family, format, backend, precision, quantization, and portability. `Supported` is the default. Set `includePreview: true` only when the application explicitly accepts a non-stable artifact. External records are never materialized as supported models. / 选择过程是确定性的，可按 ModelId、任务、模型族、格式、后端、精度、量化和可移植性筛选。默认只选择 `Supported`。只有应用明确接受非稳定工件时才设置 `includePreview: true`。External 记录不会作为受支持模型物化。

## Offline reuse / 离线复用

Create another client with `offline: true` and the same cache root. It succeeds only if all catalog files, hashes, sizes, paths, completion marker, and ModelPack checks still pass. A missing or damaged file returns `model-factory.offline-cache-miss`; offline mode never silently falls back to the network. / 使用同一缓存根并设置 `offline: true` 创建新客户端。只有目录文件、hash、大小、路径、完成标记和 ModelPack 检查全部通过时才成功。文件缺失或损坏会返回 `model-factory.offline-cache-miss`；离线模式绝不会静默联网。
