# ModelFactory CLI

`DeploySharp.ModelFactory.Cli` 是仓库内的源代码工具，用于检查模型目录、查看工件和下载经过完整性校验的模型包。它不执行推理，也不安装 ONNX Runtime、OpenVINO、OpenCV、TensorRT 或 LlamaSharp 的 native 运行时；下载完成后仍需由应用选择后端并创建 Session。

## 运行方式

在仓库根目录执行：

```powershell
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- doctor --json
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- list --preview
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- show --model-id bria/rmbg-2.0 --preview
```

`doctor` 输出 .NET、操作系统、进程架构、目录修订号和缓存位置；`list` 按模型工件列出格式、精度、量化和兼容后端；`show` 展开指定模型的 Release 资产、大小、SHA-256、sidecar 和文档路径。默认只显示 `Supported`，查看 `Preview` 或 `External` 必须显式添加 `--preview`。

## 下载模型

```powershell
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- install `
  --model-id yolo/v8/detect/n `
  --backend onnxruntime `
  --format onnx `
  --preview
```

命令会选择唯一匹配的目录工件，下载每个必需资产，校验文件大小、SHA-256、完成标记和 ModelPack 清单，然后输出 `package-root`。默认缓存是 `%LOCALAPPDATA%\DeploySharp\ModelFactory`，可用 `--cache D:\DeploySharpCache` 指定应用目录。

常用筛选项：

```powershell
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- install `
  --model-id bria/rmbg-2.0 --backend onnxruntime --format onnx `
  --precision fp32 --cache D:\DeploySharpCache --preview
```

`--precision`、`--quantization` 用于选择目录中的明确变体；`--timeout-minutes` 和 `--max-retries` 控制网络操作。安装命令不会静默选择不兼容后端，也不会把 Preview 条目当成稳定支持。

## 离线复现

```powershell
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- install `
  --model-id yolo/v8/detect/n --backend onnxruntime --format onnx `
  --cache D:\DeploySharpCache --offline
```

离线模式只读取已经验证的缓存；缺文件、哈希不符、路径逃逸、完成标记缺失或 ModelPack 无效都会返回 `model-factory.offline-cache-miss`，不会自动联网。建议把 `package-root` 交给应用自己的 Profile 和后端适配器，而不是直接假设文件名或目录结构。

完整 API 选择、缓存策略和安全边界见[ModelFactory 快速开始](modelfactory-getting-started.md)、[发布与缓存安全](modelfactory-release-cache-security.md)以及[官方模型目录](model-catalog.md)。
