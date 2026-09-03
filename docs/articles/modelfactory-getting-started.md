# ModelFactory 快速开始

`JYPPX.DeploySharp.ModelFactory` 从模型目录选择与后端匹配的工件，下载不可变 Release 资产，校验大小和 SHA-256，验证 ModelPack 清单，并复用应用自己的离线缓存。它不执行推理，也不安装厂商原生运行时。

## 安装

```powershell
dotnet add package JYPPX.DeploySharp.ModelFactory --version 2.0.0-alpha.1
```

## 通过 API 下载

```csharp
using JYPPX.DeploySharp.ModelFactory;

ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
var options = new ModelFactoryOptions(
    cacheRoot: @"D:\DeploySharpCache",
    requestTimeout: TimeSpan.FromMinutes(10),
    maximumRetries: 3);

using var factory = new ModelFactoryClient(catalog, options);
ModelSelection selection = factory.Select(new ModelQuery(
    modelId: "yolo/v8/detect/n",
    backend: "onnxruntime",
    format: "onnx",
    includePreview: true));

var progress = new Progress<ModelDownloadProgress>(value =>
    Console.WriteLine($"{value.AssetId}: {value.Stage} {value.ReceivedBytes}/{value.TotalBytes}"));

MaterializedModel model = await factory.GetModelAsync(selection, progress);
var artifacts = model.Package.ToCoreArtifacts();
```

`includePreview: true` 是选择 Alpha 目录工件的显式开关。目录会拒绝混合版本、缺少 Sidecar 或身份不一致的 Bundle；外部记录不会被物化为官方模型。

## 使用命令行

```powershell
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- doctor --json
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- list --preview --json
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- show --model-id yolo/v8/detect/n --preview --json
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- install --model-id yolo/v8/detect/n --backend onnxruntime --format onnx --preview
```

默认缓存目录为 `%LOCALAPPDATA%\DeploySharp\ModelFactory`，可使用 `--cache <path>` 指定应用目录，使用 `--offline` 强制只从已验证缓存读取。命令行只输出已验证的包根目录，推理由应用将该目录交给相应后端适配器。

## 查询、离线和自有目录

`ModelQuery` 可按模型 ID、任务、格式、后端、精度、量化、Tokenizer/Processor、分辨率和生成模式筛选。离线模式会再次检查文件、大小、哈希、路径、完成标记和 ModelPack；任何缺失或损坏都返回 `model-factory.offline-cache-miss`，不会静默联网。

应用也可以通过 `ModelCatalogJsonSerializer` 加载自己的目录，或使用 `ModelCatalogClient.LoadAsync` 获取严格快照。官方目录的精确条目和下载入口见[模型支持指南](model-support.md)与[官方模型目录](model-catalog.md)。
