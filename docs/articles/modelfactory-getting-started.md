# ModelFactory 快速开始

`JYPPX.DeploySharp.ModelFactory` 从模型目录选择与后端匹配的工件，下载不可变 Release 资产，校验大小和 SHA-256，验证 ModelPack 清单，并复用应用自己的离线缓存。它不执行推理，也不安装厂商原生运行时。

PaddleOCR/PP-Structure 的单模型 Release 使用同包中的 `PaddleOcrReleaseClient`。它面向 `models-paddleocr` 的独立 ONNX 资产，不要求下载整包；识别模型会自动获取对应字典，返回路径可直接交给 Visual 的 Profile 和后端：

```csharp
string paddleCache = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "DeploySharp", "PaddleOCR");
using var paddle = new PaddleOcrReleaseClient(
    new PaddleOcrReleaseClientOptions(paddleCache));
PaddleOcrReleaseModelMaterialization rec =
    await paddle.GetModelAsync("paddleocr/ppocrv6/tiny-rec");
// rec.ModelPath: 已校验 ONNX；rec.DictionaryPath: 匹配字典
// --offline 可强制只读取已经校验的 Release 缓存。
```

PP-Structure 模型使用 `paddle-doc/`、`paddle-table/`、`paddle-formula/` 或 `paddle-seal/` 别名，例如 `paddle-doc/pp-doclayout-s`。下载层不会把未转换的 Paddle `.tar` 或未验证的后端工件伪装成 ONNX；实际部署仍需由 Visual Profile 和目标后端完成合同检查。

## 安装

```powershell
dotnet add package JYPPX.DeploySharp.ModelFactory --version 2.0.0-alpha.1
```

## 通过 API 下载

```csharp
using JYPPX.DeploySharp.ModelFactory;

ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
var options = new ModelFactoryOptions(
    cacheRoot: cacheRoot,
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

常用诊断和离线复现：

```powershell
# 查看模型、精确工件和 sidecar
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- show `
  --model-id paddleocr/ppocrv5/mobile-rec --preview --json

# 在 CI 中只接受已经校验的缓存
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- install `
  --model-id yolo/v8/detect/n --backend onnxruntime --format onnx `
  --cache .cache/deploysharp --offline
```

`doctor --json`、`show --json` 和安装输出适合随问题报告保存；重点检查目录 revision、artifact、backend/format/precision、包根、SHA-256 和 offline 命中状态。CLI 用于人工复现和 CI；桌面应用、服务端及需要自定义进度或凭据的程序应直接使用 API。

## 查询、离线和自有目录

`ModelQuery` 可按模型 ID、任务、格式、后端、精度、量化、Tokenizer/Processor、分辨率和生成模式筛选。离线模式会再次检查文件、大小、哈希、路径、完成标记和 ModelPack；任何缺失或损坏都返回 `model-factory.offline-cache-miss`，不会静默联网。

应用也可以通过 `ModelCatalogJsonSerializer` 加载自己的目录，或使用 `ModelCatalogClient.LoadAsync` 获取严格快照。官方目录的精确条目和下载入口见[模型支持指南](model-support.md)与[官方模型目录](model-catalog.md)。

## 一个完整的应用流程

```csharp
ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
using var factory = new ModelFactoryClient(
    catalog,
    new ModelFactoryOptions(cacheRoot: cacheRoot));
ModelSelection selection = factory.Select(new ModelQuery(
    modelId: "yolo/v8/detect/n",
    backend: "onnxruntime",
    format: "onnx",
    includePreview: true));
MaterializedModel package = await factory.GetModelAsync(
    selection, progress: null, cancellationToken);

// package.Package.ToCoreArtifacts() 交给 Profile/Backend；不要假设主文件名。
```

下载层完成后再创建 Profile 和 Session，失败时可以安全重试下载或切换后端。应用不应把 `MaterializedModel` 的临时路径缓存到数据库而忽略目录 revision；升级模型时应重新选择并校验。

## 进度和取消

进度回调可能跨多个 artifact，不能把单个文件百分比当作整个包百分比。回调中不要执行阻塞 UI 或再次调用 ModelFactory。取消只取消当前调用，调用方可以稍后从已校验缓存继续；释放 factory 前应等待正在进行的下载任务结束。

## 自有目录的最低要求

私有模型目录至少应包含稳定 `modelId`、不可变 revision、完整 artifact 列表、格式、兼容后端、大小和 SHA-256。若模型需要标签、词表、tokenizer、OpenVINO `.bin` 或 ONNX external data，必须作为必需 artifact 列出，而不是写在说明文字里。先调用 Schema/ModelPack validator，再交给下载器。

## 缓存、并发与安全

ModelFactory 只接受目录声明的 Release 资产。下载采用同目录临时文件、刷新和原子重命名；只有清单、全部文件、大小、SHA-256 和完成标记都通过后，缓存才对离线查询可见。网络请求仅对超时、`408`、`429` 和 `5xx` 做有界重试；路径错误、完整性错误和其他 `4xx` 不重试。

同一应用应复用一个 `ModelFactoryClient`，让相同目录项的并发下载合并。`CleanCacheAsync` 支持按未使用时长、字节预算、目录 revision 或 Release 标签清理和 dry-run；它只管理缓存根下的 `.deploysharp-model-factory/v1`，不会删除应用的同级文件。

安全边界：

- 模型目录按不可信数据处理，不执行其中的脚本、插件或 DLL；
- 拒绝盘符、UNC、根路径、`.`、`..`、重解析点和重复规范化路径；
- 为下载设置超时、重试、文件大小和取消上限；
- 日志不要记录令牌、私有 URL 或包含凭据的请求头；
- 正在使用的模型版本不清理，更新模型后重新选择并校验 revision。

模型缓存只解决工件交付，不安装 ONNX Runtime、OpenVINO、OpenCV、TensorRT 或 LLamaSharp 的 native runtime。后端仍由最终应用显式部署。
